/****************************************************************************
 * Description: Localization tool entry point
 * 
 * Document: https://github.com/hiramtan/HiProtobuf
 * Author: hiramtan@live.com
 ****************************************************************************/
using System;
using System.IO;
using Localize;
using Newtonsoft.Json;

namespace Localize
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            string configPath = args.Length > 0 ? args[0] : "config.json";

            if (!File.Exists(configPath))
            {
                Console.WriteLine($"Config file not found: {configPath}");
                Console.WriteLine("Usage: Localize.exe [config.json]");
                return;
            }

            string configJson = File.ReadAllText(configPath);
            var config = JsonConvert.DeserializeObject<LocalizeConfig>(configJson);

            if (config == null)
            {
                Console.WriteLine("Failed to parse config file");
                return;
            }

            var manager = new LocalizeManager();
            manager.Initialize(config);
            manager.Translate();

            Console.WriteLine("Done!");
        }
    }
}
