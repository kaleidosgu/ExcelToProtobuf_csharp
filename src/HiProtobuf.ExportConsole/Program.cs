/****************************************************************************
 * Description: Console smoke test for exported protobuf data.
 *
 * Document: https://github.com/hiramtan/HiProtobuf
 * Author: hiramtan@live.com
 ****************************************************************************/

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Google.Protobuf;

namespace HiProtobuf.ExportConsole
{
    internal static class Program
    {
        private const string GeneratedAssemblyName = "HiProtobuf.Excel.csharp.dll";

        private static int Main(string[] args)
        {
            if (args.Length > 2)
            {
                Console.Error.WriteLine("用法：HiProtobuf.ExportConsole.exe [数据.dat] [生成的DLL路径]");
                return 2;
            }

            string dataPath = Path.GetFullPath(args.Length >= 1 ? args[0] : GetExampleDataPath());
            string assemblyPath = Path.GetFullPath(args.Length == 2 ? args[1] : GetGeneratedAssemblyPath(dataPath));
            if (!File.Exists(dataPath))
            {
                Console.Error.WriteLine("找不到导出文件：" + dataPath);
                return 2;
            }
            if (!File.Exists(assemblyPath))
            {
                Console.Error.WriteLine("找不到生成的DLL：" + assemblyPath);
                Console.Error.WriteLine("可用第二个命令行参数指定与该 .dat 同次导出的DLL。");
                return 2;
            }

            try
            {
                Assembly assembly = Assembly.LoadFrom(assemblyPath);
                string typeName = Path.GetFileNameWithoutExtension(dataPath);
                Type[] matchingTypes = assembly.GetTypes().Where(type => type.Name == typeName).ToArray();
                if (matchingTypes.Length != 1)
                {
                    Console.Error.WriteLine("生成的DLL中无法唯一找到类型 " + typeName + "：" + assemblyPath);
                    return 1;
                }

                PropertyInfo parserProperty = matchingTypes[0].GetProperty("Parser", BindingFlags.Public | BindingFlags.Static);
                MessageParser parser = parserProperty == null ? null : parserProperty.GetValue(null) as MessageParser;
                if (parser == null)
                {
                    Console.Error.WriteLine("生成类型没有可用的 Protobuf Parser：" + matchingTypes[0].FullName);
                    return 1;
                }

                IMessage table;
                using (FileStream input = File.OpenRead(dataPath))
                {
                    table = parser.ParseFrom(input);
                }

                PropertyInfo dataProperty = matchingTypes[0].GetProperty("Data", BindingFlags.Public | BindingFlags.Instance);
                IEnumerable rows = dataProperty == null ? null : dataProperty.GetValue(table) as IEnumerable;
                if (rows == null)
                {
                    Console.Error.WriteLine("导出类型缺少 Data 列表：" + matchingTypes[0].FullName);
                    return 1;
                }

                Console.WriteLine("文件：" + dataPath);
                Console.WriteLine("生成DLL：" + assemblyPath);
                Console.WriteLine("类型：" + matchingTypes[0].FullName);

                int count = 0;
                int errors = 0;
                HashSet<ulong> ids = new HashSet<ulong>();
                foreach (object row in rows)
                {
                    count++;
                    Console.WriteLine("[{0}] {1}", count - 1, row);

                    PropertyInfo idProperty = row.GetType().GetProperty("ID", BindingFlags.Public | BindingFlags.Instance);
                    if (idProperty == null)
                    {
                        continue;
                    }

                    ulong id = Convert.ToUInt64(idProperty.GetValue(row));
                    if (id == 0 || !ids.Add(id))
                    {
                        Console.Error.WriteLine("错误：第 {0} 条记录的 ID {1} 为空或重复。", count - 1, id);
                        errors++;
                    }
                }

                Console.WriteLine("记录数：" + count);
                if (count == 0)
                {
                    Console.Error.WriteLine("错误：导出文件没有记录。");
                    errors++;
                }
                if (errors > 0)
                {
                    Console.Error.WriteLine("检查失败：{0} 个错误。", errors);
                    return 1;
                }

                Console.WriteLine("检查通过。");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("读取或解析失败：" + ex.Message);
                Console.Error.WriteLine("请确认 .dat 与生成DLL来自同一次导出。");
                return 1;
            }
        }

        private static string GetExampleDataPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "..", "Example", "Export", "dat", "Excel_EnemyInfo.dat");
        }

        private static string GetGeneratedAssemblyPath(string dataPath)
        {
            string dataDirectory = Path.GetDirectoryName(dataPath);
            return Path.Combine(dataDirectory, "..", "language", "csharp_dll", GeneratedAssemblyName);
        }
    }
}
