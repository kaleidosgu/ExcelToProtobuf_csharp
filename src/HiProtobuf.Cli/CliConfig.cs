using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace HiProtobuf.Cli
{
    internal sealed class CliConfig
    {
        [JsonProperty("protocDir")]
        public string ProtocDir { get; set; }

        [JsonProperty("cscPath")]
        public string CscPath { get; set; }

        [JsonProperty("excelFolder")]
        public string ExcelFolder { get; set; }

        [JsonProperty("exportFolder")]
        public string ExportFolder { get; set; }

        [JsonProperty("enumProtoFolder")]
        public string EnumProtoFolder { get; set; }

        [JsonProperty("verbose")]
        public bool? Verbose { get; set; }

        [JsonIgnore]
        public string FilePath { get; private set; }

        /// <summary>
        /// 读取配置文件：explicitPath 为空时使用程序目录下的 appsettings.json，不存在则返回 null。
        /// 配置文件里的相对路径以配置文件所在目录为基准解析。
        /// </summary>
        internal static CliConfig Load(string explicitPath)
        {
            bool required = !string.IsNullOrWhiteSpace(explicitPath);
            string path = required
                ? Path.GetFullPath(explicitPath)
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

            if (!File.Exists(path))
            {
                if (required)
                {
                    throw new CliUsageException("配置文件不存在：" + path);
                }
                return null;
            }

            CliConfig config;
            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                config = JsonConvert.DeserializeObject<CliConfig>(json);
            }
            catch (Exception ex)
            {
                throw new CliUsageException("配置文件解析失败：" + path + Environment.NewLine + "  " + ex.Message);
            }

            if (config == null)
            {
                throw new CliUsageException("配置文件内容为空：" + path);
            }

            config.FilePath = path;
            config.ResolveRelativePaths();
            return config;
        }

        private void ResolveRelativePaths()
        {
            string baseDirectory = Path.GetDirectoryName(FilePath);
            ProtocDir = Resolve(baseDirectory, ProtocDir);
            CscPath = Resolve(baseDirectory, CscPath);
            ExcelFolder = Resolve(baseDirectory, ExcelFolder);
            ExportFolder = Resolve(baseDirectory, ExportFolder);
            EnumProtoFolder = Resolve(baseDirectory, EnumProtoFolder);
        }

        private static string Resolve(string baseDirectory, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }
            return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(baseDirectory, path));
        }
    }
}
