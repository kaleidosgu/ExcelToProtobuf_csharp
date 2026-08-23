using System;
using System.IO;
using Newtonsoft.Json;

namespace OllamaLocalization
{
    internal class Program
    {
        private static int Main(string[] args)
        {
            string configPath = args.Length > 0 ? args[0] : "appsettings.json";

            if (!File.Exists(configPath))
            {
                Console.WriteLine("Config file not found: " + configPath);
                Console.WriteLine("Usage: OllamaLocalization.exe [appsettings.json]");
                return 1;
            }

            try
            {
                string configJson = File.ReadAllText(configPath);
                var config = JsonConvert.DeserializeObject<OllamaLocalizationConfig>(configJson);

                var manager = new OllamaLocalizationManager();
                manager.Initialize(config, Path.GetFullPath(configPath));
                manager.Translate();

                Console.WriteLine("Done!");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed: " + ex.Message);
                return 2;
            }
        }
    }
}
