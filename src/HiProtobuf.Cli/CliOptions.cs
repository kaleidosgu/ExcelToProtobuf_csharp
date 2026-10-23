using System;
using System.IO;

namespace HiProtobuf.Cli
{
    internal sealed class CliUsageException : Exception
    {
        internal CliUsageException(string message) : base(message)
        {
        }
    }

    internal sealed class CliOptions
    {
        internal string Command;
        internal string ExcelFolder;
        internal string ExportFolder;
        internal string CscPath;
        internal string EnumProtoFolder;
        internal string ProtocDir;
        internal string ConfigPath;
        internal bool DryRun;
        internal bool Verbose = true;
        internal bool ShowHelp;

        internal string ExcelSource;
        internal string ExportSource;
        internal string CscSource;
        internal string EnumSource;
        internal string ProtocSource;

        private bool _verboseFromCommandLine;

        internal static CliOptions Parse(string[] args)
        {
            CliOptions options = new CliOptions();

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (string.IsNullOrWhiteSpace(arg))
                {
                    continue;
                }

                string name = arg;
                string inlineValue = null;
                int equals = arg.IndexOf('=');
                if (arg.StartsWith("--", StringComparison.Ordinal) && equals > 2)
                {
                    name = arg.Substring(0, equals);
                    inlineValue = arg.Substring(equals + 1);
                }

                switch (name)
                {
                    case "-h":
                    case "--help":
                        options.ShowHelp = true;
                        break;
                    case "--dry-run":
                        options.DryRun = true;
                        break;
                    case "--verbose":
                        options.Verbose = ParseBool(name, inlineValue, true);
                        options._verboseFromCommandLine = true;
                        break;
                    case "--quiet":
                        options.Verbose = ParseBool(name, inlineValue, false);
                        options._verboseFromCommandLine = true;
                        break;
                    case "--excel":
                        options.ExcelFolder = ReadValue(name, inlineValue, args, ref i);
                        options.ExcelSource = "命令行";
                        break;
                    case "--out":
                    case "--export":
                        options.ExportFolder = ReadValue(name, inlineValue, args, ref i);
                        options.ExportSource = "命令行";
                        break;
                    case "--csc":
                        options.CscPath = ReadValue(name, inlineValue, args, ref i);
                        options.CscSource = "命令行";
                        break;
                    case "--enum-proto":
                        options.EnumProtoFolder = ReadValue(name, inlineValue, args, ref i);
                        options.EnumSource = "命令行";
                        break;
                    case "--protoc-dir":
                        options.ProtocDir = ReadValue(name, inlineValue, args, ref i);
                        options.ProtocSource = "命令行";
                        break;
                    case "--config":
                        options.ConfigPath = ReadValue(name, inlineValue, args, ref i);
                        break;
                    default:
                        if (name.StartsWith("-", StringComparison.Ordinal))
                        {
                            throw new CliUsageException("未知参数：" + name);
                        }
                        if (options.Command != null)
                        {
                            throw new CliUsageException("只能指定一个命令，已指定 " + options.Command + "，多余：" + arg);
                        }
                        options.Command = name;
                        break;
                }
            }

            return options;
        }

        internal void ApplyConfig(CliConfig config)
        {
            if (config == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(ExcelFolder) && !string.IsNullOrEmpty(config.ExcelFolder))
            {
                ExcelFolder = config.ExcelFolder;
                ExcelSource = "配置文件";
            }
            if (string.IsNullOrEmpty(ExportFolder) && !string.IsNullOrEmpty(config.ExportFolder))
            {
                ExportFolder = config.ExportFolder;
                ExportSource = "配置文件";
            }
            if (string.IsNullOrEmpty(CscPath) && !string.IsNullOrEmpty(config.CscPath))
            {
                CscPath = config.CscPath;
                CscSource = "配置文件";
            }
            if (string.IsNullOrEmpty(EnumProtoFolder) && !string.IsNullOrEmpty(config.EnumProtoFolder))
            {
                EnumProtoFolder = config.EnumProtoFolder;
                EnumSource = "配置文件";
            }
            if (!_verboseFromCommandLine && config.Verbose.HasValue)
            {
                Verbose = config.Verbose.Value;
            }
        }

        internal void ApplyDefaults()
        {
            ExcelFolder = ToFullPath(ExcelFolder);
            ExportFolder = ToFullPath(ExportFolder);
            CscPath = ToFullPath(CscPath);
            EnumProtoFolder = ToFullPath(EnumProtoFolder);
            if (!string.IsNullOrEmpty(ProtocDir))
            {
                ProtocDir = ToFullPath(ProtocDir);
            }

            if (string.IsNullOrEmpty(CscPath))
            {
                CscPath = DefaultCscPath();
                CscSource = "默认值";
            }
        }

        private static bool ParseBool(string name, string value, bool defaultValue)
        {
            if (value == null)
            {
                return defaultValue;
            }
            bool parsed;
            if (!bool.TryParse(value, out parsed))
            {
                throw new CliUsageException("参数 " + name + " 的值不是布尔值：" + value);
            }
            return parsed;
        }

        private static string ReadValue(string name, string inlineValue, string[] args, ref int index)
        {
            if (inlineValue != null)
            {
                return inlineValue;
            }
            if (index + 1 >= args.Length)
            {
                throw new CliUsageException("参数缺少值：" + name);
            }
            index++;
            return args[index];
        }

        private static string ToFullPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }
            return Path.GetFullPath(path);
        }

        internal static string DefaultCscPath()
        {
            string windir = Environment.GetEnvironmentVariable("WINDIR");
            if (string.IsNullOrEmpty(windir))
            {
                windir = @"C:\Windows";
            }

            string framework64 = Path.Combine(windir, @"Microsoft.NET\Framework64\v4.0.30319\csc.exe");
            if (File.Exists(framework64))
            {
                return framework64;
            }

            string framework = Path.Combine(windir, @"Microsoft.NET\Framework\v4.0.30319\csc.exe");
            return File.Exists(framework) ? framework : framework64;
        }
    }
}
