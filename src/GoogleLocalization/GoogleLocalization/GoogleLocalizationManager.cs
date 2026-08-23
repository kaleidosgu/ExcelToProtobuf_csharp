using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GoogleLocalization
{
    public class GoogleLocalizationManager
    {
        private const int DefaultBatchSize = 64;
        private GoogleLocalizationConfig _config;
        private string _configDirectory;
        private GoogleTranslateClient _client;

        public void Initialize(GoogleLocalizationConfig config, string configPath)
        {
            _config = config ?? throw new ArgumentNullException("config");
            _configDirectory = Path.GetDirectoryName(configPath) ?? Directory.GetCurrentDirectory();

            ValidateConfig();
            _client = new GoogleTranslateClient(_config.Google);
        }

        public void Translate()
        {
            foreach (var targetFile in _config.TargetFiles)
            {
                TranslateFile(targetFile);
            }
        }

        private void TranslateFile(LocalizationTargetFile targetFile)
        {
            string sourceFilePath = ResolvePath(targetFile.SourceFilePath);
            if (!File.Exists(sourceFilePath))
            {
                throw new FileNotFoundException("Source json file not found.", sourceFilePath);
            }

            Console.WriteLine("Source: " + sourceFilePath);
            string sourceJson = File.ReadAllText(sourceFilePath, Encoding.UTF8);
            JToken sourceRoot = JToken.Parse(sourceJson);

            if (targetFile.WriteSourceLanguageCopy)
            {
                WriteOutput(targetFile, sourceFilePath, _config.SourceLanguage, sourceRoot);
            }

            foreach (string targetLanguage in GetTargetLanguages())
            {
                if (string.Equals(targetLanguage, _config.SourceLanguage, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Console.WriteLine("Translating " + _config.SourceLanguage + " -> " + targetLanguage);
                JToken translatedRoot = sourceRoot.DeepClone();
                TranslateJsonToken(translatedRoot, targetLanguage);
                WriteOutput(targetFile, sourceFilePath, targetLanguage, translatedRoot);
            }
        }

        private void TranslateJsonToken(JToken token, string targetLanguage)
        {
            var stringValues = new List<JValue>();
            CollectStringValues(token, stringValues);

            var memory = CreateMemory(targetLanguage);
            var translatedMap = new Dictionary<string, string>();
            var missingTexts = new List<string>();
            var missingSet = new HashSet<string>();

            foreach (JValue value in stringValues)
            {
                string sourceText = value.Value<string>();
                string cachedText;
                if (memory != null && memory.TryGet(sourceText, out cachedText))
                {
                    value.Value = cachedText;
                    continue;
                }

                if (!string.IsNullOrEmpty(sourceText) && missingSet.Add(sourceText))
                {
                    missingTexts.Add(sourceText);
                }
            }

            int translatedCount = 0;
            foreach (var batch in SplitBatches(missingTexts, GetBatchSize()))
            {
                IList<string> translatedTexts = _client.TranslateBatch(batch, _config.SourceLanguage, targetLanguage);
                for (int i = 0; i < batch.Count; i++)
                {
                    translatedMap[batch[i]] = translatedTexts[i];
                    memory?.Set(batch[i], translatedTexts[i]);
                }

                translatedCount += batch.Count;
                Console.WriteLine("  Google translated " + translatedCount + "/" + missingTexts.Count);
            }

            if (memory != null && missingTexts.Count > 0)
            {
                memory.Save();
            }

            foreach (JValue value in stringValues)
            {
                string sourceText = value.Value<string>();
                string translatedText;
                if (memory != null && memory.TryGet(sourceText, out translatedText))
                {
                    value.Value = translatedText;
                }
                else if (translatedMap.TryGetValue(sourceText, out translatedText))
                {
                    value.Value = translatedText;
                }
            }
        }

        private TranslationMemory CreateMemory(string targetLanguage)
        {
            var options = _config.TranslationMemory;
            if (options == null || !options.Enabled)
            {
                return null;
            }

            string directory = string.IsNullOrWhiteSpace(options.Directory) ? "translation-memory" : options.Directory;
            string memoryDirectory = ResolvePath(directory);
            string fileName = SanitizeFileName(_config.SourceLanguage) + "_to_" + SanitizeFileName(targetLanguage) + ".json";
            return new TranslationMemory(Path.Combine(memoryDirectory, fileName), options.SaveIndented);
        }

        private static void CollectStringValues(JToken token, IList<JValue> values)
        {
            var value = token as JValue;
            if (value != null)
            {
                if (value.Type == JTokenType.String)
                {
                    values.Add(value);
                }

                return;
            }

            var container = token as JContainer;
            if (container == null)
            {
                return;
            }

            foreach (JToken child in container.Children())
            {
                CollectStringValues(child, values);
            }
        }

        private void WriteOutput(LocalizationTargetFile targetFile, string sourceFilePath, string language, JToken root)
        {
            string outputDirectory = string.IsNullOrWhiteSpace(targetFile.OutputDirectory)
                ? Path.GetDirectoryName(sourceFilePath)
                : ResolvePath(targetFile.OutputDirectory);

            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            string sourceName = Path.GetFileNameWithoutExtension(sourceFilePath);
            string extension = Path.GetExtension(sourceFilePath);
            string pattern = string.IsNullOrWhiteSpace(targetFile.OutputFileNamePattern)
                ? "{name}.{language}{ext}"
                : targetFile.OutputFileNamePattern;

            string fileName = pattern
                .Replace("{name}", sourceName)
                .Replace("{language}", language)
                .Replace("{ext}", extension);

            string outputPath = Path.Combine(outputDirectory, fileName);
            File.WriteAllText(outputPath, root.ToString(Formatting.Indented), Encoding.UTF8);
            Console.WriteLine("  Saved: " + outputPath);
        }

        private void ValidateConfig()
        {
            RequireText(_config.SourceLanguage, "SourceLanguage");

            if (GetTargetLanguages().Count == 0)
            {
                throw new Exception("TargetLanguages or SupportedLanguages is required.");
            }

            if (_config.TargetFiles == null || _config.TargetFiles.Count == 0)
            {
                throw new Exception("TargetFiles is required.");
            }

            if (_config.Google == null)
            {
                _config.Google = new GoogleTranslateOptions();
            }

            if (string.IsNullOrWhiteSpace(_config.Google.ApiKey) && string.IsNullOrWhiteSpace(_config.Google.OAuthAccessToken))
            {
                throw new Exception("Google.ApiKey or Google.OAuthAccessToken is required.");
            }

            foreach (var targetFile in _config.TargetFiles)
            {
                RequireText(targetFile.SourceFilePath, "TargetFiles.SourceFilePath");
            }
        }

        private static void RequireText(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new Exception(name + " is required.");
            }
        }

        private string ResolvePath(string path)
        {
            if (Path.IsPathRooted(path))
            {
                return path;
            }

            return Path.GetFullPath(Path.Combine(_configDirectory, path));
        }

        private int GetBatchSize()
        {
            return _config.Google != null && _config.Google.BatchSize > 0 ? _config.Google.BatchSize : DefaultBatchSize;
        }

        private IList<string> GetTargetLanguages()
        {
            if (_config.TargetLanguages != null && _config.TargetLanguages.Count > 0)
            {
                return _config.TargetLanguages;
            }

            return _config.SupportedLanguages ?? new List<string>();
        }

        private static IEnumerable<IList<string>> SplitBatches(IList<string> items, int batchSize)
        {
            for (int i = 0; i < items.Count; i += batchSize)
            {
                yield return items.Skip(i).Take(batchSize).ToList();
            }
        }

        private static string SanitizeFileName(string value)
        {
            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalidChar, '_');
            }

            return value;
        }
    }
}
