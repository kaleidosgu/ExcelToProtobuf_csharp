using System;
using System.IO;
using System.Text;
using HiFramework.Log;
using HiProtobuf.Lib;

namespace HiProtobuf.Cli
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            TryConfigureConsole();

            CliOptions options;
            CliConfig config;
            try
            {
                options = CliOptions.Parse(args);
                if (options.ShowHelp)
                {
                    PrintUsage();
                    return 0;
                }
                if (string.IsNullOrEmpty(options.Command))
                {
                    throw new CliUsageException("缺少命令，当前只支持 export。");
                }
                if (!string.Equals(options.Command, "export", StringComparison.OrdinalIgnoreCase))
                {
                    throw new CliUsageException("未知命令：" + options.Command);
                }

                config = CliConfig.Load(options.ConfigPath);
                options.ApplyConfig(config);
                options.ApplyDefaults();
            }
            catch (CliUsageException ex)
            {
                Console.Error.WriteLine("[错误] " + ex.Message);
                Console.Error.WriteLine();
                PrintUsage();
                return 2;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[错误] 初始化失败：" + ex.Message);
                return 2;
            }

            string protocSource;
            string protocError;
            options.ProtocDir = ResolveProtoc(options.ProtocDir,
                config == null ? null : config.ProtocDir, out protocSource, out protocError);
            if (protocError != null)
            {
                Console.Error.WriteLine("[错误] " + protocError);
                return 2;
            }
            options.ProtocSource = protocSource;

            string validationError = Validate(options);
            if (validationError != null)
            {
                Console.Error.WriteLine("[错误] " + validationError);
                return 2;
            }

            PrintSettings(options, config);

            if (options.DryRun)
            {
                PrintDryRun(options);
                return 0;
            }

            Log.LogHandler = new ConsoleLogHandler(options.Verbose);
            Settings.Export_Folder = options.ExportFolder;
            Settings.Excel_Folder = options.ExcelFolder;
            Settings.Compiler_Path = options.CscPath;
            Settings.Enum_Proto_Folder = options.EnumProtoFolder ?? string.Empty;
            Settings.Protoc_Dir = options.ProtocDir;

            try
            {
                if (!Manager.TryExport())
                {
                    Console.Error.WriteLine("[错误] 导出失败，请检查上面的日志。");
                    return 1;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[错误] 导出异常：" + ex.Message);
                if (options.Verbose)
                {
                    Console.Error.WriteLine(ex.ToString());
                }
                return 1;
            }

            PrintSummary(options);
            return 0;
        }

        internal static string Now()
        {
            return DateTime.Now.ToString("HH:mm:ss");
        }

        private static void TryConfigureConsole()
        {
            try
            {
                Console.OutputEncoding = new UTF8Encoding(false);
            }
            catch
            {
                // 控制台被重定向或不可用时保持默认编码即可。
            }
        }

        private static string ResolveProtoc(string commandLineDir, string configDir,
            out string source, out string error)
        {
            if (!string.IsNullOrWhiteSpace(commandLineDir))
            {
                if (IsProtocDir(commandLineDir))
                {
                    source = "命令行";
                    error = null;
                    return commandLineDir;
                }
                source = null;
                error = "protoc 目录无效：" + commandLineDir + Environment.NewLine
                    + "  需要包含 bin\\protoc.exe 和 include 子目录。";
                return null;
            }

            if (!string.IsNullOrWhiteSpace(configDir))
            {
                if (IsProtocDir(configDir))
                {
                    source = "配置文件";
                    error = null;
                    return configDir;
                }
                source = null;
                error = "appsettings.json 里的 protocDir 无效：" + configDir + Environment.NewLine
                    + "  需要包含 bin\\protoc.exe 和 include 子目录。";
                return null;
            }

            string adjacent = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "protoc-3.8.0-win64");
            if (IsProtocDir(adjacent))
            {
                source = "程序目录";
                error = null;
                return adjacent;
            }

            source = null;
            error = "找不到 protoc 工具链。已尝试：" + Environment.NewLine
                + "  - " + adjacent + "（程序目录）" + Environment.NewLine
                + "请用 --protoc-dir 或 appsettings.json 的 protocDir 指定。";
            return null;
        }

        private static bool IsProtocDir(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                return false;
            }
            return File.Exists(Path.Combine(directory, "bin", "protoc.exe"))
                && Directory.Exists(Path.Combine(directory, "include"));
        }

        private static string Validate(CliOptions options)
        {
            if (string.IsNullOrEmpty(options.ExcelFolder))
            {
                return "缺少 Excel 目录：用 --excel 指定，或写进 appsettings.json 的 excelFolder。";
            }
            if (string.IsNullOrEmpty(options.ExportFolder))
            {
                return "缺少导出目录：用 --out 指定，或写进 appsettings.json 的 exportFolder。";
            }
            if (!Directory.Exists(options.ExcelFolder))
            {
                return "Excel 目录不存在：" + options.ExcelFolder;
            }

            string exportRoot = options.ExportFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string excelRoot = options.ExcelFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(exportRoot, excelRoot, StringComparison.OrdinalIgnoreCase))
            {
                return "导出目录不能和 Excel 目录相同：" + options.ExportFolder;
            }

            string pathRoot = Path.GetPathRoot(options.ExportFolder);
            if (!string.IsNullOrEmpty(pathRoot)
                && string.Equals(pathRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    exportRoot, StringComparison.OrdinalIgnoreCase))
            {
                return "导出目录不能是盘符根目录：" + options.ExportFolder;
            }

            if (!File.Exists(options.CscPath))
            {
                return "找不到 C# 编译器：" + options.CscPath + "（用 --csc 指定）";
            }

            if (!string.IsNullOrEmpty(options.EnumProtoFolder) && !Directory.Exists(options.EnumProtoFolder))
            {
                return "枚举 proto 目录不存在：" + options.EnumProtoFolder;
            }

            return null;
        }

        private static void PrintSettings(CliOptions options, CliConfig config)
        {
            if (config != null && !string.IsNullOrEmpty(config.FilePath))
            {
                Console.WriteLine("[{0}] 配置文件：{1}", Now(), config.FilePath);
            }
            Console.WriteLine("[{0}] 导出目录：{1}    ({2})", Now(), options.ExportFolder, options.ExportSource);
            Console.WriteLine("[{0}] Excel目录：{1}    ({2})", Now(), options.ExcelFolder, options.ExcelSource);
            Console.WriteLine("[{0}] 编译器  ：{1}    ({2})", Now(), options.CscPath, options.CscSource);
            if (!string.IsNullOrEmpty(options.EnumProtoFolder))
            {
                Console.WriteLine("[{0}] 枚举Proto：{1}    ({2})", Now(), options.EnumProtoFolder, options.EnumSource);
            }
            Console.WriteLine("[{0}] protoc  ：{1}    ({2})", Now(), options.ProtocDir, options.ProtocSource);
        }

        private static void PrintDryRun(CliOptions options)
        {
            Console.WriteLine();
            Console.WriteLine("--dry-run：不会写入任何文件。正式导出时会先删除并重建：");
            string[] subFolders = { "proto", "language", "dat", Path.Combine("language", "csharp_dll"), "Localization" };
            for (int i = 0; i < subFolders.Length; i++)
            {
                Console.WriteLine("  - " + Path.Combine(options.ExportFolder, subFolders[i]));
            }
        }

        private static void PrintSummary(CliOptions options)
        {
            string export = options.ExportFolder;
            Console.WriteLine();
            Console.WriteLine("[{0}] 导出完成", Now());
            Console.WriteLine("  proto 文件：{0} 个", CountFiles(Path.Combine(export, "proto"), "*.proto"));
            Console.WriteLine("  C# 源文件：{0} 个", CountFiles(Path.Combine(export, "language", "csharp"), "*.cs"));
            Console.WriteLine("  dat 数据  ：{0} 个", CountFiles(Path.Combine(export, "dat"), "*.dat"));
            Console.WriteLine("  生成 DLL  ：{0} 个", CountFiles(Path.Combine(export, "language", "csharp_dll"), "*.dll"));
            Console.WriteLine("  输出目录  ：{0}", export);
        }

        private static int CountFiles(string directory, string pattern)
        {
            if (!Directory.Exists(directory))
            {
                return 0;
            }
            return Directory.GetFiles(directory, pattern, SearchOption.AllDirectories).Length;
        }

        private static void PrintUsage()
        {
            Console.WriteLine("用法：");
            Console.WriteLine("  HiProtobuf.Cli.exe export --excel <目录> --out <目录> [选项]");
            Console.WriteLine();
            Console.WriteLine("选项：");
            Console.WriteLine("  --excel <目录>       Excel 根目录（递归扫描 .xlsx）");
            Console.WriteLine("  --out <目录>         导出根目录，会写入 proto/language/dat");
            Console.WriteLine("  --csc <文件>         C# 编译器 csc.exe，默认使用 .NET Framework 4.8");
            Console.WriteLine("  --enum-proto <目录>  枚举 proto 源目录（可选）");
            Console.WriteLine("  --protoc-dir <目录>  protoc 工具链目录（含 bin 与 include）");
            Console.WriteLine("  --config <文件>      配置文件，默认使用同目录下的 appsettings.json");
            Console.WriteLine("  --dry-run            只打印配置与将清理的目录，不写文件");
            Console.WriteLine("  --quiet              只输出警告、错误和最终结果");
            Console.WriteLine("  -h, --help           显示帮助");
            Console.WriteLine();
            Console.WriteLine("优先级：命令行参数 > appsettings.json > 内置默认值");
            Console.WriteLine("退出码：0 成功；1 导出失败；2 参数、配置或环境错误");
        }
    }
}
