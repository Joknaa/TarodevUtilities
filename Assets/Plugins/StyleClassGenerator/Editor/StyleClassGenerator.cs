using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Tarodev.StyleClassGenerator
{
    internal class StyleClassGenerator
    {
        private readonly List<StyleClassGeneratorConfig> _configs;
        private const string CLASS_PATTERN = @"(?<![0-9])\.([a-zA-Z_][a-zA-Z0-9_\-]*)(?=[\s\.\{\:\#\,])";
        private const string UNITY_CLASS_PREFIX = "unity-";
        public StyleClassGenerator(List<StyleClassGeneratorConfig> configs) => _configs = configs;

        internal void Generate(bool isAutoGenerating)
        {
            foreach (var config in _configs)
            {
                if (isAutoGenerating && !config.AutoGenerate) continue;

                var targetDirectory = $"{Application.dataPath}/{config.TargetDirectory}";
                
                if (!Directory.Exists(targetDirectory))
                {
                    Debug.LogWarning($"Style generator cannot resolve path: {targetDirectory}");
                    continue;
                }

                var ussFiles = Directory.GetFiles(targetDirectory, "*.uss", SearchOption.AllDirectories);

                var foundClasses = new HashSet<string>();

                foreach (var asset in ussFiles)
                {
                    if (Path.GetExtension(asset).Replace(".", "") != StyleClassGeneratorShared.USS_EXTENSION) continue;

                    var fileContent = File.ReadAllText(asset);
                    var matches = Regex.Matches(fileContent, CLASS_PATTERN);

                    foreach (Match match in matches)
                    {
                        var className = match.Groups[1].Value;
                        if (className.StartsWith(UNITY_CLASS_PREFIX) && !config.IncludeUnityClasses) continue;

                        foundClasses.Add(className);
                    }
                }

                var generatedFileName = StyleClassGeneratorShared.GeneratePathAndFileName(config.TargetDirectory, config.FileName);

                if (File.Exists(generatedFileName))
                {
                    if (IsIdentical()) return;
                    WriteFile(GenerateFileContents(config, foundClasses));
                }
                else
                {
                    WriteFile(GenerateFileContents(config, foundClasses));
                }

                void WriteFile(string content)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(generatedFileName) ?? string.Empty);
                    using (var fs = File.Create(generatedFileName))
                    {
                        var info = new UTF8Encoding(true).GetBytes(content);
                        fs.Write(info, 0, info.Length);
                    }

                    AssetDatabase.Refresh();
                }

                bool IsIdentical()
                {
                    try
                    {
                        var currentClasses = File.ReadLines(generatedFileName).Skip(1).First()[2..].Split(',').ToHashSet();
                        return currentClasses.SetEquals(foundClasses);
                    }
                    catch (Exception)
                    {
                        Debug.LogWarning("A parsing error occured. Regenerating file.");
                        return false;
                    }
                }
            }
        }

        private static string GenerateFileContents(StyleClassGeneratorConfig config, HashSet<string> classNames)
        {
            var classNamesSplitByComma = string.Join(",", classNames);
            var useNamespaces = !string.IsNullOrEmpty(config.Namespace);
            var builder = new StringBuilder();
            var tabCount = 0;

            AddLine("// This file is auto-generated by Style Class Generator. Do not edit.");
            AddLine($"// {classNamesSplitByComma}");
            AddLine("");

            AddLine("// ReSharper disable All");

            if (useNamespaces)
            {
                AddLine($"namespace {config.Namespace}");
                AddLine("{");
            }

            AddLine($"public static class {(string.IsNullOrEmpty(config.FileName) ? StyleClassGeneratorShared.DEFAULT_FILE_NAME : config.FileName)}");
            AddLine("{");

            foreach (var className in classNames)
            {
                var constName = ToPascalCase(className);
                AddLine($"public const string {constName} = \"{className}\";");
            }

            AddLine("}");

            if (useNamespaces)
            {
                AddLine("}");
            }

            return builder.ToString();

            void AddLine(string line)
            {
                if (line.Contains("}")) tabCount--;

                for (var i = 0; i < tabCount; i++)
                {
                    builder.Append("\t");
                }

                builder.Append($"{line}\n");

                if (line.Contains("{")) tabCount++;
            }

            string ToPascalCase(string s)
            {
                var words = s.Split(new[] { '-', '_' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(word => word[..1].ToUpper() +
                                    word[1..].ToLower());

                var result = string.Concat(words);
                return result;
            }
        }
    }
}