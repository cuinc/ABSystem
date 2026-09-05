#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;

namespace UIToolkit.Editor
{
    public static class UxmlBindingGenerator
    {
        #region Public Methods

        [MenuItem("Tools/UI Toolkit/Generate All Bindings")]
        public static void GenerateAll()
        {
            string[] uxmlGuids = AssetDatabase.FindAssets("t:VisualTreeAsset", new[] { "Assets/UI", "Assets/UIToolkit" });
            int count = 0;

            foreach (string guid in uxmlGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (GenerateForFile(path))
                    count++;
            }

            AssetDatabase.Refresh();
            Debug.Log($"[UxmlBindingGenerator] Generated bindings for {count} UXML file(s).");
        }

        public static bool GenerateForFile(string uxmlPath)
        {
            if (!uxmlPath.EndsWith(".uxml"))
                return false;

            string uxmlContent = File.ReadAllText(uxmlPath);
            XDocument doc;

            try
            {
                doc = XDocument.Parse(uxmlContent);
            }
            catch (Exception e)
            {
                Debug.LogError($"[UxmlBindingGenerator] Failed to parse {uxmlPath}: {e.Message}");
                return false;
            }

            string fileName = Path.GetFileNameWithoutExtension(uxmlPath);
            var entries = new List<BindingEntry>();
            var errors = new List<string>();
            var names = new HashSet<string>();

            CollectNamedElements(doc.Root, entries, errors, names);

            foreach (string error in errors)
                Debug.LogError($"[UxmlBindingGenerator] {uxmlPath}: {error}");

            if (entries.Count == 0)
                return false;

            bool hasRootConflict = entries.Any(e => e.PropertyName == fileName);
            if (hasRootConflict)
            {
                var rootEntry = entries.First(e => e.PropertyName == fileName);
                rootEntry.PropertyName = fileName + "Root";
            }

            string code = GenerateCode(fileName, uxmlPath, entries);
            string outputDir = GetGenDirectory(uxmlPath);
            Directory.CreateDirectory(outputDir);

            string outputPath = Path.Combine(outputDir, $"{fileName}.Gen.cs");
            string existing = File.Exists(outputPath) ? File.ReadAllText(outputPath) : "";

            if (existing == code)
                return false;

            File.WriteAllText(outputPath, code);
            return true;
        }

        #endregion

        #region Private Methods

        private static void CollectNamedElements(XElement element, List<BindingEntry> entries,
            List<string> errors, HashSet<string> names)
        {
            string nameAttr = element.Attribute("name")?.Value;

            if (!string.IsNullOrEmpty(nameAttr))
            {
                ValidateName(nameAttr, element, errors);

                if (names.Contains(nameAttr))
                {
                    errors.Add($"Duplicate name '{nameAttr}'");
                }
                else
                {
                    names.Add(nameAttr);
                    string csharpType = MapElementType(element);
                    bool optional = nameAttr.StartsWith("_");
                    string propName = optional ? nameAttr.Substring(1) : nameAttr;

                    entries.Add(new BindingEntry
                    {
                        ElementName = nameAttr,
                        PropertyName = propName,
                        CSharpType = csharpType,
                        Optional = optional
                    });
                }
            }

            foreach (XElement child in element.Elements())
                CollectNamedElements(child, entries, errors, names);
        }

        private static void ValidateName(string name, XElement element, List<string> errors)
        {
            string checkName = name.StartsWith("_") ? name.Substring(1) : name;

            if (string.IsNullOrEmpty(checkName))
            {
                errors.Add($"Element name is empty or only underscore prefix");
                return;
            }

            if (!char.IsUpper(checkName[0]))
                errors.Add($"Name '{name}' must start with an uppercase letter (PascalCase)");

            if (Regex.IsMatch(checkName, @"[-]"))
                errors.Add($"Name '{name}' must not contain hyphens, use PascalCase");
        }

        private static string MapElementType(XElement element)
        {
            string localName = element.Name.LocalName;
            string fullName = element.Name.ToString();

            if (fullName.Contains("Button") || localName == "Button")
                return "Button";
            if (fullName.Contains("Label") || localName == "Label")
                return "Label";
            if (fullName.Contains("TextField") || localName == "TextField")
                return "TextField";
            if (fullName.Contains("Toggle") || localName == "Toggle")
                return "Toggle";
            if (fullName.Contains("SliderInt") || localName == "SliderInt")
                return "SliderInt";
            if (fullName.Contains("MinMaxSlider") || localName == "MinMaxSlider")
                return "MinMaxSlider";
            if (fullName.Contains("Slider") || localName == "Slider")
                return "Slider";
            if (fullName.Contains("DropdownField") || localName == "DropdownField")
                return "DropdownField";
            if (fullName.Contains("ScrollView") || localName == "ScrollView")
                return "ScrollView";
            if (fullName.Contains("ListView") || localName == "ListView")
                return "ListView";
            if (fullName.Contains("TreeView") || localName == "TreeView")
                return "TreeView";
            if (fullName.Contains("ProgressBar") || localName == "ProgressBar")
                return "ProgressBar";
            if (fullName.Contains("Foldout") || localName == "Foldout")
                return "Foldout";
            if (fullName.Contains("GroupBox") || localName == "GroupBox")
                return "GroupBox";
            if (fullName.Contains("RadioButtonGroup") || localName == "RadioButtonGroup")
                return "RadioButtonGroup";
            if (fullName.Contains("RadioButton") || localName == "RadioButton")
                return "RadioButton";
            if (fullName.Contains("IntegerField") || localName == "IntegerField")
                return "IntegerField";
            if (fullName.Contains("FloatField") || localName == "FloatField")
                return "FloatField";
            if (fullName.Contains("LongField") || localName == "LongField")
                return "LongField";
            if (fullName.Contains("DoubleField") || localName == "DoubleField")
                return "DoubleField";
            if (fullName.Contains("EnumField") || localName == "EnumField")
                return "EnumField";
            if (fullName.Contains("ColorField") || localName == "ColorField")
                return "ColorField";
            if (fullName.Contains("Vector2Field") || localName == "Vector2Field")
                return "Vector2Field";
            if (fullName.Contains("Vector3Field") || localName == "Vector3Field")
                return "Vector3Field";
            if (fullName.Contains("Vector4Field") || localName == "Vector4Field")
                return "Vector4Field";
            if (fullName.Contains("Image") || localName == "Image")
                return "Image";

            return "VisualElement";
        }

        private static string GenerateCode(string className, string uxmlPath, List<BindingEntry> entries)
        {
            var sb = new StringBuilder();

            sb.AppendLine("// <auto-generated>");
            sb.AppendLine("// 由 UxmlBindingGenerator 自动生成，请勿手动修改");
            sb.AppendLine($"// Source: {uxmlPath}");
            sb.AppendLine("// </auto-generated>");
            sb.AppendLine();
            sb.AppendLine("using UnityEngine.UIElements;");
            sb.AppendLine();
            sb.AppendLine($"public partial class {className}");
            sb.AppendLine("{");
            sb.AppendLine("    #region Auto-Generated Bindings");
            sb.AppendLine();

            foreach (var entry in entries)
            {
                sb.AppendLine($"    protected {entry.CSharpType} {entry.PropertyName} {{ get; private set; }}");
            }

            sb.AppendLine();
            sb.AppendLine("    protected void AutoBind(VisualElement root)");
            sb.AppendLine("    {");

            foreach (var entry in entries)
            {
                sb.AppendLine($"        {entry.PropertyName} = root.Q<{entry.CSharpType}>(\"{entry.ElementName}\");");
            }

            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    #endregion");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string GetGenDirectory(string uxmlPath)
        {
            string dir = Path.GetDirectoryName(uxmlPath);
            string parentDir = Path.GetDirectoryName(dir);
            return Path.Combine(parentDir ?? dir, "Gen");
        }

        #endregion

        #region Inner Types

        private class BindingEntry
        {
            public string ElementName;
            public string PropertyName;
            public string CSharpType;
            public bool Optional;
        }

        #endregion
    }

    public class UxmlBindingPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            bool needsRefresh = false;

            foreach (string path in importedAssets)
            {
                if (path.EndsWith(".uxml"))
                {
                    if (UxmlBindingGenerator.GenerateForFile(path))
                        needsRefresh = true;
                }
            }

            if (needsRefresh)
                AssetDatabase.Refresh();
        }
    }
}
#endif
