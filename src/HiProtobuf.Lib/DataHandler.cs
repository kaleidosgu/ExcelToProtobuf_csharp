/****************************************************************************
 * Description: 
 * 
 * Document: https://github.com/hiramtan/HiProtobuf
 * Author: hiramtan@live.com
 ****************************************************************************/

using Google.Protobuf;
using Google.Protobuf.Collections;
using HiFramework.Assert;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Reflection;
using OfficeOpenXml;
using HiFramework.Log;
using System.Collections.Generic;
using System.Threading;

namespace HiProtobuf.Lib
{
    internal class DataHandler
    {
        public const string NameSpace = "Depth.Tmp";
        private Assembly _assembly;
        private object _excelIns;
        private readonly Dictionary<string, string> _textValueToKey = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _textKeyToValue = new Dictionary<string, string>();
        private int _textKeyCounter = 100000;

        public DataHandler()
        {
            var folder = Settings.Export_Folder + Settings.dat_folder;
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
            Directory.CreateDirectory(folder);

            var locFolder = Settings.Export_Folder + Settings.localization_folder;
            // Don't delete localization folder to preserve existing localization.json
            Directory.CreateDirectory(locFolder);
        }

        public void Process()
        {
            var dllPath = Settings.Export_Folder + Settings.language_folder + Settings.csharp_dll_folder + Compiler.DllName;
            // Load into memory to avoid locking the DLL file, so the folder can be deleted next run
            var dllBytes = File.ReadAllBytes(dllPath);
            _assembly = Assembly.Load(dllBytes);
            
            // Load existing localization data before processing
            LoadLocalization();
            
            var protoFolder = Settings.Export_Folder + Settings.proto_folder;
            string[] files = Directory.GetFiles(protoFolder, "*.proto", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string protoPath = files[i];
                string strClassName = Path.GetFileNameWithoutExtension(protoPath);
                if (strClassName == "Common" || strClassName == "Vector")
                {
                    continue;
                }
                string strNameSpace = "";
                if (ProtoHandler.ClassNamespaceMap.TryGetValue(strClassName, out var ns))
                {
                    strNameSpace = ns;
                }
                else
                {
                    Log.Info($"Class {strClassName} not found in ProtoHandler.ClassNamespaceMap, using empty namespace.");
                }
                string excelPath = Settings.Excel_Folder + "/" + strNameSpace + ".xlsx";
                ProcessData(excelPath, strNameSpace, strClassName);
            }
            ExportLocalization();
        }

        // ... existing code ...
        private void ProcessData(string path, string strNameSpace, string strClassName)
        {
            AssertThat.IsTrue(File.Exists(path), "Excel file can not find");
            var name = Path.GetFileNameWithoutExtension(path);
            var fileInfo = new FileInfo(path);
            using (ExcelPackage excelPackage = new ExcelPackage(fileInfo))
            {
                foreach (ExcelWorksheet _worksheet in excelPackage.Workbook.Worksheets)
                {
                    if (strClassName.Equals(_worksheet.Name) == false)
                    {
                        continue;
                    }
                    if (_worksheet.Tables == null || _worksheet.Tables.Count == 0)
                    {
                        Log.Error($"Worksheet {_worksheet.Name} 没有找到Table");
                        continue;
                    }
                    var _table = _worksheet.Tables[0];
                    var startRow = _table.Address.Start.Row;
                    var endRow = _table.Address.End.Row;
                    var startCol = _table.Address.Start.Column;
                    var endCol = _table.Address.End.Column;

                    string excelInsName = $"{strNameSpace}.Excel_" + _worksheet.Name;
                    _excelIns = _assembly.CreateInstance(excelInsName);
                    if (_excelIns == null)
                    {
                        string errorInfo = $"文件不存在空间: {strNameSpace}, 类名: {_worksheet.Name}, Excel文件: {name}.xlsx";
                        Log.Error(errorInfo);
                        continue;
                    }

                    var excel_Type = _excelIns.GetType();
                    var dataProp = excel_Type.GetProperty("Data");
                    var dataIns = dataProp.GetValue(_excelIns);
                    var dataType = dataProp.PropertyType;
                    var insTypeName = $"{strNameSpace}.{_worksheet.Name}";

                    // 数据从table的第4行开始
                    for (int i = startRow + 3; i <= endRow; i++)
                    {
                        var ins = _assembly.CreateInstance(insTypeName);
                        if (ins == null)
                        {
                            string errorInfo = $"Excel文件: {name}.xlsx，命名空间: [{strNameSpace}], 类名: [{_worksheet.Name}] 无法在被注册列表找到";
                            Log.Error(errorInfo);
                            break;
                        }
                        var addMethod = dataType.GetMethod("Add", new Type[] { ins.GetType() });
                        addMethod.Invoke(dataIns, new[] { ins });
                        for (int j = startCol; j <= endCol; j++)
                        {
                            var variableType = _worksheet.Cells[startRow + 1, j].Value?.ToString();
                            var variableName = _worksheet.Cells[startRow + 2, j].Value?.ToString();
                            var variableValue = _worksheet.Cells[i, j].Value?.ToString();
                            var insType = ins.GetType();
                            FieldInfo insField = FindFieldInfo(insType, variableName);
                            var value = GetVariableValue(variableType, variableValue);
                            if (insField == null)
                            {
                                Log.Info($"文件： {name} 属性： {variableName} 没有反射获取到对应的数据，请检查命名规范");
                            }
                            else
                            {
                                if (variableType == Common.text_)
                                {
                                    var depthTextType = _assembly.GetType("Depth.Localize.DepthText");
                                    if (depthTextType == null)
                                    {
                                        foreach (var t in _assembly.GetTypes())
                                        {
                                            if (t.Name == "DepthText")
                                            {
                                                depthTextType = t;
                                                Log.Info($"Found type: {t.FullName}");
                                                break;
                                            }
                                        }
                                    }
                                    var depthText = Activator.CreateInstance(depthTextType);
                                    var idProp = depthTextType.GetProperty("Id");
                                    idProp.SetValue(depthText, value);
                                    insField.SetValue(ins, depthText);
                                }
                                else if (variableType == Common.vector2_ || variableType == Common.vector3_)
                                {
                                    insField.SetValue(ins, value);
                                }
                                else
                                {
                                    insField.SetValue(ins, value);
                                }
                            }
                        }
                    }
                    Console.WriteLine($"_excelIns  {path} ");
                    Serialize(_excelIns);
                }
            }
        }

        private string GetTextKey(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }
            if (_textValueToKey.TryGetValue(value, out var existingKey))
            {
                return existingKey;
            }
            var newKey = (_textKeyCounter++).ToString();
            _textValueToKey[value] = newKey;
            _textKeyToValue[newKey] = value;
            return newKey;
        }

        private void ExportLocalization()
        {
            if (_textKeyToValue.Count == 0)
            {
                return;
            }
            var path = Settings.Export_Folder + Settings.localization_folder + "/localization.json";
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("{");
            int count = 0;
            foreach (var kvp in _textKeyToValue)
            {
                count++;
                var key = EscapeJsonString(kvp.Key);
                var value = EscapeJsonString(kvp.Value);
                sb.AppendLine($"  \"{key}\": \"{value}\"{(count < _textKeyToValue.Count ? "," : "")}");
            }
            sb.AppendLine("}");
            WriteAllTextWithRetry(path, sb.ToString());
            Log.Info($"Localization file generated: {path}");
        }

        private void WriteAllTextWithRetry(string path, string contents)
        {
            const int maxRetryCount = 5;
            const int retryDelayMilliseconds = 200;

            for (int i = 0; i < maxRetryCount; i++)
            {
                try
                {
                    File.WriteAllText(path, contents);
                    return;
                }
                catch (IOException ex)
                {
                    if (i == maxRetryCount - 1)
                    {
                        Log.Error($"Failed to write localization file: {path}. {ex.Message}");
                        throw;
                    }
                    Thread.Sleep(retryDelayMilliseconds);
                }
            }
        }

        private string EscapeJsonString(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            return input.Replace("\\", "\\\\")
                        .Replace("\"", "\\\"")
                        .Replace("\n", "\\n")
                        .Replace("\r", "\\r")
                        .Replace("\t", "\\t");
        }

        private string UnescapeJsonString(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < input.Length; i++)
            {
                if (input[i] == '\\' && i + 1 < input.Length)
                {
                    switch (input[i + 1])
                    {
                        case 'n': sb.Append('\n'); i++; break;
                        case 'r': sb.Append('\r'); i++; break;
                        case 't': sb.Append('\t'); i++; break;
                        case '\\': sb.Append('\\'); i++; break;
                        case '"': sb.Append('"'); i++; break;
                        default: sb.Append(input[i]); break;
                    }
                }
                else
                {
                    sb.Append(input[i]);
                }
            }
            return sb.ToString();
        }

        private void LoadLocalization()
        {
            var path = Settings.Export_Folder + Settings.localization_folder + "/localization.json";
            if (!File.Exists(path))
            {
                Log.Info("No existing localization.json found, starting fresh.");
                return;
            }

            try
            {
                var content = File.ReadAllText(path);
                var pattern = @"""([^""\\]*(?:\\.[^""\\]*)*)""\s*:\s*""([^""\\]*(?:\\.[^""\\]*)*)""";
                var matches = Regex.Matches(content, pattern);
                int maxKey = 99999;
                foreach (Match match in matches)
                {
                    var key = UnescapeJsonString(match.Groups[1].Value);
                    var value = UnescapeJsonString(match.Groups[2].Value);
                    _textKeyToValue[key] = value;
                    _textValueToKey[value] = key;
                    if (int.TryParse(key, out int keyInt))
                    {
                        if (keyInt > maxKey)
                        {
                            maxKey = keyInt;
                        }
                    }
                }
                _textKeyCounter = maxKey + 1;
                Log.Info($"Loaded {matches.Count} localization entries from {path}. Next key: {_textKeyCounter}");
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to load localization.json: {ex.Message}");
            }
        }

        // ... existing code ...

        object GetVariableValue(string type, string value)
        {
            var isEmpty = false;
            if (string.IsNullOrEmpty(value))
            {
                isEmpty = true;
            }
            if (type == Common.double_)
                return isEmpty ? 0 : double.Parse(value);
            if (type == Common.float_)
                return isEmpty ? 0 : float.Parse(value);
            if (type == Common.int32_)
                return isEmpty ? 0 : int.Parse(value);
            if (type == Common.int64_)
                return isEmpty ? 0 : long.Parse(value);
            if (type == Common.uint32_)
                return isEmpty ? 0 : uint.Parse(value);
            if (type == Common.uint64_)
                return isEmpty ? 0 : ulong.Parse(value);
            if (type == Common.sint32_)
                return isEmpty ? 0 : int.Parse(value);
            if (type == Common.sint64_)
                return isEmpty ? 0 : long.Parse(value);
            if (type == Common.fixed32_)
                return isEmpty ? 0 : uint.Parse(value);
            if (type == Common.fixed64_)
                return isEmpty ? 0 : ulong.Parse(value);
            if (type == Common.sfixed32_)
                return isEmpty ? 0 : int.Parse(value);
            if (type == Common.sfixed64_)
                return isEmpty ? 0 : long.Parse(value);
            if (type == Common.bool_)
                return isEmpty ? false : (value == "1");
            if (type == Common.string_)
                return isEmpty ? string.Empty : value.ToString();
            if (type == Common.bytes_)
                return isEmpty ? ByteString.CopyFromUtf8(string.Empty) : ByteString.CopyFromUtf8(value.ToString());
            if (type == Common.text_)
                return isEmpty ? 0 : uint.Parse(GetTextKey(value));
            if (type == Common.vector2_)
            {
                var vector2Type = _assembly.GetType("Depth.Core.Vector2");
                if (vector2Type == null)
                {
                    foreach (var t in _assembly.GetTypes())
                    {
                        if (t.Name == "Vector2")
                        {
                            vector2Type = t;
                            break;
                        }
                    }
                }
                if (vector2Type == null)
                {
                    Log.Error($"Cannot find Vector2 type in assembly");
                    return null;
                }
                var vector2 = Activator.CreateInstance(vector2Type);
                if (!isEmpty)
                {
                    var parts = value.Split(';');
                    if (parts.Length >= 1)
                    {
                        var xProp = vector2Type.GetProperty("X");
                        xProp.SetValue(vector2, float.Parse(parts[0]));
                    }
                    if (parts.Length >= 2)
                    {
                        var yProp = vector2Type.GetProperty("Y");
                        yProp.SetValue(vector2, float.Parse(parts[1]));
                    }
                }
                return vector2;
            }
            if (type == Common.vector3_)
            {
                var vector3Type = _assembly.GetType("Depth.Core.Vector3");
                if (vector3Type == null)
                {
                    foreach (var t in _assembly.GetTypes())
                    {
                        if (t.Name == "Vector3")
                        {
                            vector3Type = t;
                            break;
                        }
                    }
                }
                if (vector3Type == null)
                {
                    Log.Error($"Cannot find Vector3 type in assembly");
                    return null;
                }
                var vector3 = Activator.CreateInstance(vector3Type);
                if (!isEmpty)
                {
                    var parts = value.Split(';');
                    if (parts.Length >= 1)
                    {
                        var xProp = vector3Type.GetProperty("X");
                        xProp.SetValue(vector3, float.Parse(parts[0]));
                    }
                    if (parts.Length >= 2)
                    {
                        var yProp = vector3Type.GetProperty("Y");
                        yProp.SetValue(vector3, float.Parse(parts[1]));
                    }
                    if (parts.Length >= 3)
                    {
                        var zProp = vector3Type.GetProperty("Z");
                        zProp.SetValue(vector3, float.Parse(parts[2]));
                    }
                }
                return vector3;
            }
            if (type == Common.double_s)
            {
                RepeatedField<double> newValue = new RepeatedField<double>();
                if (!isEmpty)
                {
                    string data = value.Trim('"');
                    string[] datas = data.Split('|');
                    for (int i = 0; i < datas.Length; i++)
                    {
                        newValue.Add(double.Parse(datas[i]));
                    }
                }
                return newValue;
            }
            if (type == Common.float_s)
            {
                RepeatedField<float> newValue = new RepeatedField<float>();
                if (!isEmpty)
                {
                    string data = value.Trim('"');
                    string[] datas = data.Split('|');
                    for (int i = 0; i < datas.Length; i++)
                    {
                        newValue.Add(float.Parse(datas[i]));
                    }
                }
                return newValue;
            }
            if (type == Common.int32_s)
            {
                RepeatedField<int> newValue = new RepeatedField<int>();
                if (!isEmpty)
                {
                    string data = value.Trim('"');
                    string[] datas = data.Split('|');
                    for (int i = 0; i < datas.Length; i++)
                    {
                        newValue.Add(int.Parse(datas[i]));
                    }
                }
                return newValue;
            }
            if (type == Common.int64_s)
            {
                RepeatedField<long> newValue = new RepeatedField<long>();
                if (!isEmpty)
                {
                    string data = value.Trim('"');
                    string[] datas = data.Split('|');
                    for (int i = 0; i < datas.Length; i++)
                    {
                        newValue.Add(long.Parse(datas[i]));
                    }
                }
                return newValue;
            }
            if (type == Common.uint32_s)
            {
                RepeatedField<uint> newValue = new RepeatedField<uint>();
                if (!isEmpty)
                {
                    string data = value.Trim('"');
                    string[] datas = data.Split('|');
                    for (int i = 0; i < datas.Length; i++)
                    {
                        newValue.Add(uint.Parse(datas[i]));
                    }
                }
                return newValue;
            }
            if (type == Common.uint64_s)
            {
                RepeatedField<ulong> newValue = new RepeatedField<ulong>();
                if (!isEmpty)
                {
                    string data = value.Trim('"');
                    string[] datas = data.Split('|');
                    for (int i = 0; i < datas.Length; i++)
                    {
                        newValue.Add(ulong.Parse(datas[i]));
                    }
                }
                return newValue;
            }
            if (type == Common.sint32_s)
            {
                RepeatedField<int> newValue = new RepeatedField<int>();
                if (!isEmpty)
                {
                    string data = value.Trim('"');
                    string[] datas = data.Split('|');
                    for (int i = 0; i < datas.Length; i++)
                    {
                        newValue.Add(int.Parse(datas[i]));
                    }
                }
                return newValue;
            }
            if (type == Common.sint64_s)
            {
                RepeatedField<long> newValue = new RepeatedField<long>();
                if (!isEmpty)
                {
                    string data = value.Trim('"');
                    string[] datas = data.Split('|');
                    for (int i = 0; i < datas.Length; i++)
                    {
                        newValue.Add(long.Parse(datas[i]));
                    }
                }
                return newValue;
            }
            if (type == Common.fixed32_s)
            {
                RepeatedField<uint> newValue = new RepeatedField<uint>();
                if (!isEmpty)
                {
                    string data = value.Trim('"');
                    string[] datas = data.Split('|');
                    for (int i = 0; i < datas.Length; i++)
                    {
                        newValue.Add(uint.Parse(datas[i]));
                    }
                }
                return newValue;
            }
            if (type == Common.fixed64_s)
            {
                RepeatedField<ulong> newValue = new RepeatedField<ulong>();
                if (!isEmpty)
                {
                    string data = value.Trim('"');
                    string[] datas = data.Split('|');
                    for (int i = 0; i < datas.Length; i++)
                    {
                        newValue.Add(ulong.Parse(datas[i]));
                    }
                }
                return newValue;
            }
            if (type == Common.sfixed32_s)
            {
                RepeatedField<int> newValue = new RepeatedField<int>();
                if (!isEmpty)
                {
                    string data = value.Trim('"');
                    string[] datas = data.Split('|');
                    for (int i = 0; i < datas.Length; i++)
                    {
                        newValue.Add(int.Parse(datas[i]));
                    }
                }
                return newValue;
            }
            if (type == Common.sfixed64_s)
            {
                RepeatedField<long> newValue = new RepeatedField<long>();
                if (!isEmpty)
                {
                    string data = value.Trim('"');
                    string[] datas = data.Split('|');
                    for (int i = 0; i < datas.Length; i++)
                    {
                        newValue.Add(long.Parse(datas[i]));
                    }
                }
                return newValue;
            }
            if (type == Common.bool_s)
            {
                RepeatedField<bool> newValue = new RepeatedField<bool>();
                if (!isEmpty)
                {
                    string data = value.Trim('"');
                    string[] datas = data.Split('|');
                    for (int i = 0; i < datas.Length; i++)
                    {
                        newValue.Add(datas[i] == "1");
                    }
                }
                return newValue;
            }
            if (type == Common.string_s)
            {
                RepeatedField<string> newValue = new RepeatedField<string>();
                if (!isEmpty)
                {
                    string data = value.Trim('"');
                    string[] datas = data.Split('|');
                    for (int i = 0; i < datas.Length; i++)
                    {
                        newValue.Add(datas[i]);
                    }
                }
                return newValue;
            }
            if (type == Common.text_s)
            {
                RepeatedField<uint> newValue = new RepeatedField<uint>();
                if (!isEmpty)
                {
                    string data = value.Trim('"');
                    string[] datas = data.Split('|');
                    for (int i = 0; i < datas.Length; i++)
                    {
                        newValue.Add(uint.Parse(GetTextKey(datas[i])));
                    }
                }
                return newValue;
            }
            Log.Error($"type: {type}  value: {value}");
            return null;
        }

        void Serialize(object obj)
        {
            var type = obj.GetType();
            var path = Settings.Export_Folder + Settings.dat_folder + "/" + type.Name + ".dat";
            using (var output = File.Create(path))
            {
                MessageExtensions.WriteTo((IMessage)obj, output);
            }
        }

        public string FirstCharToLower(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;
            string str = input.First().ToString().ToLower() + input.Substring(1);
            return str;
        }

        private FieldInfo FindFieldInfo(Type insType, string variableName)
        {
            // 1. 原始：首字母小写+下划线
            string fieldName = FirstCharToLower(variableName + "_");
            FieldInfo field = insType.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null) return field;

            // 2. 下划线转驼峰（如 enemy_id -> enemyId_）
            string camelCase = ToCamelCase(variableName) + "_";
            field = insType.GetField(camelCase, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null) return field;

            // 3. 去掉末尾下划线
            string noEndUnderscore = camelCase.TrimEnd('_');
            field = insType.GetField(noEndUnderscore, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null) return field;

            // 4. 全部小写
            string lower = variableName.ToLower() + "_";
            field = insType.GetField(lower, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null) return field;

            // 5. 直接用原名
            field = insType.GetField(variableName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null) return field;

            return null;
        }

        private string ToCamelCase(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            var parts = input.Split('_');
            for (int i = 1; i < parts.Length; i++)
            {
                if (parts[i].Length > 0)
                    parts[i] = char.ToUpper(parts[i][0]) + parts[i].Substring(1);
            }
            return string.Join("", parts);
        }
    }
}
