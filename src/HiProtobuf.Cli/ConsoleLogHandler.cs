using System;
using System.Text;
using HiFramework.Log;

namespace HiProtobuf.Cli
{
    internal sealed class ConsoleLogHandler : ILogHandler
    {
        private readonly bool _showInfo;

        internal ConsoleLogHandler(bool showInfo)
        {
            _showInfo = showInfo;
        }

        public void Info(params object[] args)
        {
            if (!_showInfo)
            {
                return;
            }
            Console.WriteLine("[{0}] {1}", Program.Now(), Join(args));
        }

        public void Warning(params object[] args)
        {
            Console.WriteLine("[{0}] [警告] {1}", Program.Now(), Join(args));
        }

        public void Error(params object[] args)
        {
            Console.Error.WriteLine("[{0}] [错误] {1}", Program.Now(), Join(args));
        }

        private static string Join(object[] args)
        {
            if (args == null || args.Length == 0)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] != null)
                {
                    builder.Append(args[i]);
                }
            }
            return builder.ToString();
        }
    }
}
