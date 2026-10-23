/****************************************************************************
 * Description: Validate Excel enum references against source proto files.
 *
 * Document: https://github.com/hiramtan/HiProtobuf
 * Author: hiramtan@live.com
 ****************************************************************************/

using System;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Google.Protobuf.Reflection;
using OfficeOpenXml;
using OfficeOpenXml.Table;

namespace HiProtobuf.Lib
{
    internal sealed class EnumRegistry
    {
        private const string SnapshotFileName = "enum-mapping.tsv";
        private readonly Dictionary<string, Dictionary<string, uint>> _enums =
            new Dictionary<string, Dictionary<string, uint>>(StringComparer.Ordinal);
        private readonly Dictionary<string, Dictionary<string, uint>> _labels =
            new Dictionary<string, Dictionary<string, uint>>(StringComparer.Ordinal);
        private readonly HashSet<string> _referencedEnums = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _negativeMembers = new HashSet<string>(StringComparer.Ordinal);

        internal static string GetBaseType(string type)
        {
            string enumType;
            bool isArray;
            return TryGetEnumType(type, out enumType, out isArray)
                ? (isArray ? Common.uint32_s : Common.uint32_) : type;
        }

        internal static bool TryGetEnumType(string type, out string enumType, out bool isArray)
        {
            enumType = null;
            isArray = false;
            if (type == null)
            {
                return false;
            }
            const string scalarPrefix = "uint32@";
            const string arrayPrefix = "uint32[]@";
            if (type.StartsWith(scalarPrefix, StringComparison.Ordinal)
                || type.StartsWith(arrayPrefix, StringComparison.Ordinal))
            {
                string prefix = type.StartsWith(arrayPrefix, StringComparison.Ordinal) ? arrayPrefix : scalarPrefix;
                enumType = type.Substring(prefix.Length).TrimStart('.');
                isArray = prefix == arrayPrefix;
            }
            else
            {
                // A fully qualified proto enum name is also an Excel field type.
                // Unqualified names and built-in scalar types remain unchanged.
                isArray = type.EndsWith("[]", StringComparison.Ordinal);
                enumType = isArray ? type.Substring(0, type.Length - 2) : type;
                enumType = enumType.TrimStart('.');
                if (enumType.IndexOf('.') <= 0 || enumType.Any(char.IsWhiteSpace))
                {
                    enumType = null;
                    isArray = false;
                    return false;
                }
            }
            if (string.IsNullOrWhiteSpace(enumType))
            {
                throw new InvalidDataException("枚举类型声明缺少名称：" + type);
            }
            return true;
        }

        internal static EnumRegistry Preflight()
        {
            EnumRegistry registry = new EnumRegistry();
            if (!Directory.Exists(Settings.Excel_Folder))
            {
                throw new DirectoryNotFoundException("Excel 目录不存在：" + Settings.Excel_Folder);
            }
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            foreach (string path in Directory.GetFiles(Settings.Excel_Folder, "*.xlsx", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(path).StartsWith("~$", StringComparison.Ordinal))
                {
                    continue;
                }
                using (ExcelPackage package = new ExcelPackage(new FileInfo(path)))
                {
                    foreach (ExcelWorksheet sheet in package.Workbook.Worksheets)
                    {
                        if (sheet.Tables == null || sheet.Tables.Count == 0)
                        {
                            continue;
                        }
                        ExcelTable table = sheet.Tables[0];
                        int firstRow = table.Address.Start.Row;
                        for (int column = table.Address.Start.Column; column <= table.Address.End.Column; column++)
                        {
                            string type = sheet.Cells[firstRow + 1, column].Value?.ToString();
                            string enumType;
                            bool isArray;
                            if (!TryGetEnumType(type, out enumType, out isArray))
                            {
                                continue;
                            }
                            registry._referencedEnums.Add(enumType);
                        }
                    }
                }
            }
            if (registry._referencedEnums.Count == 0)
            {
                return registry;
            }
            registry.LoadProtoDefinitions();
            registry.CompareSnapshot();
            registry.ValidateExcel();
            return registry;
        }

        private void LoadProtoDefinitions()
        {
            string root = Settings.Enum_Proto_Folder;
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                throw new DirectoryNotFoundException("请配置有效的枚举 proto 源目录：" + root);
            }
            root = Path.GetFullPath(root);
            string outputProto = Path.GetFullPath(Settings.Export_Folder + Settings.proto_folder);
            if (root.Equals(outputProto, StringComparison.OrdinalIgnoreCase)
                || root.StartsWith(outputProto + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || outputProto.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("枚举 proto 源目录不能包含或位于生成的 proto 目录中：" + root);
            }
            string[] paths = Directory.GetFiles(root, "*.proto", SearchOption.AllDirectories);
            if (paths.Length == 0)
            {
                throw new InvalidDataException("枚举 proto 源目录中没有 .proto 文件：" + root);
            }
            string descriptorPath = Path.Combine(Path.GetTempPath(), "hiprotobuf-enums-" + Guid.NewGuid().ToString("N") + ".pb");
            try
            {
                ProcessStartInfo info = new ProcessStartInfo(Settings.Protoc_Path);
                string includePath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Settings.Protoc_Path), "..", "include"));
                string includes = " -I=" + Quote(root);
                if (Directory.Exists(includePath))
                {
                    includes += " -I=" + Quote(includePath);
                }
                info.Arguments = "--descriptor_set_out=" + Quote(descriptorPath)
                    + " --include_imports --include_source_info" + includes
                    + " " + string.Join(" ", paths.Select(path => Quote(GetRelativePath(root, path))));
                info.WorkingDirectory = root;
                info.UseShellExecute = false;
                info.CreateNoWindow = true;
                info.RedirectStandardError = true;
                info.RedirectStandardOutput = true;
                using (Process process = Process.Start(info))
                {
                    string error = process.StandardError.ReadToEnd();
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    if (process.ExitCode != 0 || !File.Exists(descriptorPath))
                    {
                        throw new InvalidDataException("读取枚举 proto 失败：" + error + output);
                    }
                }
                // This repository's Google.Protobuf exposes descriptor messages internally.
                // Read the compiler's descriptor set through reflection instead of parsing proto text.
                Type setType = typeof(FileDescriptor).Assembly.GetType("Google.Protobuf.Reflection.FileDescriptorSet", true);
                object parser = setType.GetProperty("Parser", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                    .GetValue(null);
                object descriptor = parser.GetType().GetMethod("ParseFrom", new[] { typeof(byte[]) })
                    .Invoke(parser, new object[] { File.ReadAllBytes(descriptorPath) });
                foreach (object file in Items(descriptor, "File"))
                {
                    string package = Text(file, "Package");
                    string prefix = string.IsNullOrEmpty(package) ? string.Empty : package + ".";
                    Dictionary<string, string> comments = ReadTrailingComments(file);
                    int enumIndex = 0;
                    foreach (object enumDefinition in Items(file, "EnumType"))
                    {
                        AddEnum(prefix + Text(enumDefinition, "Name"), enumDefinition, comments,
                            "5," + enumIndex);
                        enumIndex++;
                    }
                    int messageIndex = 0;
                    foreach (object message in Items(file, "MessageType"))
                    {
                        AddMessageEnums(prefix + Text(message, "Name"), message, comments,
                            "4," + messageIndex);
                        messageIndex++;
                    }
                }
            }
            finally
            {
                if (File.Exists(descriptorPath))
                {
                    File.Delete(descriptorPath);
                }
            }
        }

        private void AddMessageEnums(string prefix, object message, Dictionary<string, string> comments, string path)
        {
            int enumIndex = 0;
            foreach (object definition in Items(message, "EnumType"))
            {
                AddEnum(prefix + "." + Text(definition, "Name"), definition, comments,
                    path + ",4," + enumIndex);
                enumIndex++;
            }
            int messageIndex = 0;
            foreach (object nested in Items(message, "NestedType"))
            {
                AddMessageEnums(prefix + "." + Text(nested, "Name"), nested, comments,
                    path + ",3," + messageIndex);
                messageIndex++;
            }
        }

        private void AddEnum(string name, object definition, Dictionary<string, string> comments, string path)
        {
            Dictionary<string, uint> members = new Dictionary<string, uint>(StringComparer.Ordinal);
            Dictionary<string, uint> labels = new Dictionary<string, uint>(StringComparer.Ordinal);
            HashSet<string> labelNames = new HashSet<string>(StringComparer.Ordinal);
            int valueIndex = 0;
            foreach (object value in Items(definition, "Value"))
            {
                int number = (int)Property(value, "Number");
                string memberName = Text(value, "Name");
                if (number >= 0)
                {
                    members.Add(memberName, (uint)number);
                }
                else
                {
                    _negativeMembers.Add(name + "." + memberName);
                }

                string label;
                if (_referencedEnums.Contains(name) && comments.TryGetValue(path + ",2," + valueIndex, out label))
                {
                    label = label.Trim();
                    if (label.Length > 0)
                    {
                        if (label.IndexOfAny(new[] { '\r', '\n', '\t' }) >= 0)
                        {
                            throw new InvalidDataException("枚举行尾注释须为单行文字：" + name + "." + memberName);
                        }
                        if (!labelNames.Add(label))
                        {
                            throw new InvalidDataException("枚举中有重复的行尾注释：" + name + " / " + label);
                        }
                        if (number < 0)
                        {
                            _negativeMembers.Add(name + "." + label);
                        }
                        else
                        {
                            labels.Add(label, (uint)number);
                        }
                    }
                }
                valueIndex++;
            }
            foreach (string label in labelNames)
            {
                if (members.ContainsKey(label))
                {
                    throw new InvalidDataException("枚举注释与成员名冲突：" + name + " / " + label);
                }
            }
            _enums.Add(name, members);
            _labels.Add(name, labels);
        }

        private static Dictionary<string, string> ReadTrailingComments(object file)
        {
            Dictionary<string, string> comments = new Dictionary<string, string>(StringComparer.Ordinal);
            object sourceInfo = Property(file, "SourceCodeInfo");
            if (sourceInfo == null)
            {
                return comments;
            }
            foreach (object location in Items(sourceInfo, "Location"))
            {
                string comment = Text(location, "TrailingComments");
                if (string.IsNullOrWhiteSpace(comment))
                {
                    continue;
                }
                string path = string.Join(",", Items(location, "Path").Cast<int>());
                comments[path] = comment;
            }
            return comments;
        }

        internal uint Resolve(string enumType, string member)
        {
            Dictionary<string, uint> values;
            if (!_enums.TryGetValue(enumType, out values))
            {
                throw new InvalidDataException("找不到枚举类型：" + enumType);
            }
            string name = (member ?? string.Empty).Trim();
            if (name.Length == 0)
            {
                if (values.Values.Contains(0u))
                {
                    return 0;
                }
                throw new InvalidDataException("枚举 " + enumType + " 没有零值，不能留空");
            }
            uint number;
            if (_negativeMembers.Contains(enumType + "." + name))
            {
                throw new InvalidDataException("枚举 " + enumType + "." + name + " 是负数，不能写入 uint32");
            }
            if (!TryResolve(enumType, name, out number))
            {
                throw new InvalidDataException("枚举 " + enumType + " 中没有成员名或行尾注释 " + name);
            }
            return number;
        }

        private bool TryResolve(string enumType, string name, out uint number)
        {
            Dictionary<string, uint> values;
            if (_enums.TryGetValue(enumType, out values) && values.TryGetValue(name, out number))
            {
                return true;
            }
            Dictionary<string, uint> labels;
            if (_labels.TryGetValue(enumType, out labels) && labels.TryGetValue(name, out number))
            {
                return true;
            }
            number = 0;
            return false;
        }

        private void ValidateExcel()
        {
            foreach (string path in Directory.GetFiles(Settings.Excel_Folder, "*.xlsx", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(path).StartsWith("~$", StringComparison.Ordinal))
                {
                    continue;
                }
                using (ExcelPackage package = new ExcelPackage(new FileInfo(path)))
                {
                    foreach (ExcelWorksheet sheet in package.Workbook.Worksheets)
                    {
                        if (sheet.Tables == null || sheet.Tables.Count == 0)
                        {
                            continue;
                        }
                        ExcelTable table = sheet.Tables[0];
                        int firstRow = table.Address.Start.Row;
                        for (int column = table.Address.Start.Column; column <= table.Address.End.Column; column++)
                        {
                            string enumType;
                            bool isArray;
                            string type = sheet.Cells[firstRow + 1, column].Value?.ToString();
                            if (!TryGetEnumType(type, out enumType, out isArray))
                            {
                                continue;
                            }
                            if (!_enums.ContainsKey(enumType))
                            {
                                throw new InvalidDataException(path + " / " + sheet.Name + " / "
                                    + sheet.Cells[firstRow + 1, column].Address + "：找不到枚举类型 " + enumType);
                            }
                            for (int row = firstRow + 3; row <= table.Address.End.Row; row++)
                            {
                                string value = sheet.Cells[row, column].Value?.ToString();
                                try
                                {
                                    if (isArray && !string.IsNullOrWhiteSpace(value))
                                    {
                                        foreach (string item in value.Trim('"').Split('|'))
                                        {
                                            Resolve(enumType, item);
                                        }
                                    }
                                    else if (!isArray)
                                    {
                                        Resolve(enumType, value);
                                    }
                                }
                                catch (InvalidDataException ex)
                                {
                                    throw new InvalidDataException(path + " / " + sheet.Name + " / "
                                        + sheet.Cells[row, column].Address + "：" + ex.Message, ex);
                                }
                            }
                        }
                    }
                }
            }
        }

        private string SnapshotPath => Path.Combine(Settings.Export_Folder, SnapshotFileName);

        private void CompareSnapshot()
        {
            if (!File.Exists(SnapshotPath))
            {
                return;
            }
            foreach (string line in File.ReadAllLines(SnapshotPath))
            {
                string[] parts = line.Split('\t');
                if (parts.Length != 3)
                {
                    throw new InvalidDataException("枚举映射快照格式错误：" + SnapshotPath);
                }
                uint oldNumber;
                uint newNumber;
                if (!uint.TryParse(parts[2], out oldNumber))
                {
                    throw new InvalidDataException("枚举映射快照格式错误：" + SnapshotPath);
                }
                if (_referencedEnums.Contains(parts[0]) && TryResolve(parts[0], parts[1], out newNumber)
                    && oldNumber != newNumber)
                {
                    throw new InvalidDataException("枚举映射重编号或注释重用：" + parts[0] + "." + parts[1]
                        + " 原值 " + oldNumber + "，现值 " + newNumber + "。请先迁移数据。");
                }
            }
        }

        internal void SaveSnapshot()
        {
            if (_enums.Count == 0)
            {
                return;
            }
            string[] lines = _enums.Where(item => _referencedEnums.Contains(item.Key))
                .OrderBy(item => item.Key, StringComparer.Ordinal)
                .SelectMany(item => item.Value.Concat(_labels[item.Key])
                    .OrderBy(member => member.Key, StringComparer.Ordinal)
                    .Select(member => item.Key + "\t" + member.Key + "\t" + member.Value))
                .ToArray();
            string tempPath = SnapshotPath + ".tmp";
            File.WriteAllLines(tempPath, lines);
            if (File.Exists(SnapshotPath))
            {
                File.Replace(tempPath, SnapshotPath, null);
            }
            else
            {
                File.Move(tempPath, SnapshotPath);
            }
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static object Property(object instance, string name)
        {
            return instance.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(instance);
        }

        private static string Text(object instance, string name)
        {
            return (string)Property(instance, name);
        }

        private static IEnumerable Items(object instance, string name)
        {
            return (IEnumerable)Property(instance, name);
        }

        private static string GetRelativePath(string root, string path)
        {
            return path.Substring(root.TrimEnd(Path.DirectorySeparatorChar).Length + 1)
                .Replace(Path.DirectorySeparatorChar, '/');
        }
    }
}
