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
using System.Reflection;
using OfficeOpenXml;
using HiFramework.Log;
using System.Collections.Generic;

namespace HiProtobuf.Lib
{
    internal class DataHandler
    {
        public const string NameSpace = "Depth.Tmp";
        private Assembly _assembly;
        private object _excelIns;
        public DataHandler()
        {
            var folder = Settings.Export_Folder + Settings.dat_folder;
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
            Directory.CreateDirectory(folder);
        }

        public void Process()
        {
            var dllPath = Settings.Export_Folder + Settings.language_folder + Settings.csharp_dll_folder + Compiler.DllName;
            _assembly = Assembly.LoadFrom(dllPath);
            var protoFolder = Settings.Export_Folder + Settings.proto_folder;
            string[] files = Directory.GetFiles(protoFolder, "*.proto", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string protoPath = files[i];
                string strClassName = Path.GetFileNameWithoutExtension(protoPath);
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
                                insField.SetValue(ins, value);
                            }
                        }
                    }
                    Console.WriteLine($"_excelIns  {path} ");
                    Serialize(_excelIns);
                }
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
