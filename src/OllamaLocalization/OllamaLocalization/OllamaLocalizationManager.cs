using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OllamaLocalization
{
    public class OllamaLocalizationManager
    {
        private const int DefaultBatchSize = 16;
        private OllamaLocalizationConfig _config;
        private string _configDirectory;
        private OllamaTranslateClient _client;

        public void Initialize(OllamaLocalizationConfig config, string configPath)
        {
            _config = config ?? throw new ArgumentNullException("config");
            _configDirectory = Path.GetDirectoryName(configPath) ?? Directory.GetCurrentDirectory();

            ValidateConfig();
            _client = new OllamaTranslateClient(_config.Ollama);
        }

        public void Translate()
        {
            foreach (LocalizationTargetFile targetFile in _config.TargetFiles)
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
            var sourceTextsByValue = stringValues.Select(value => value.Value<string>()).ToList();
            var translatedMap = new Dictionary<string, string>();
            var missingTexts = new List<string>();
            var missingSet = new HashSet<string>();

            foreach (string sourceText in sourceTextsByValue)
            {
                string cachedText;
                if (memory != null && memory.TryGet(sourceText, out cachedText))
                {
                    translatedMap[sourceText] = cachedText;
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(sourceText) && missingSet.Add(sourceText))
                {
                    missingTexts.Add(sourceText);
                }
                else if (string.IsNullOrWhiteSpace(sourceText))
                {
                    translatedMap[sourceText] = sourceText;
                }
            }

            int translatedCount = 0;
            foreach (IList<string> batch in SplitBatches(missingTexts, GetBatchSize()))
            {
                IList<string> translatedTexts = TranslateBatchWithFallback(batch, targetLanguage);
                for (int i = 0; i < batch.Count; i++)
                {
                    translatedMap[batch[i]] = translatedTexts[i];
                    if (memory != null)
                    {
                        memory.Set(batch[i], translatedTexts[i]);
                    }
                }

                translatedCount += batch.Count;
                Console.WriteLine("  Ollama translated " + translatedCount + "/" + missingTexts.Count);
            }

            if (memory != null && missingTexts.Count > 0)
            {
                memory.Save();
            }

            for (int i = 0; i < stringValues.Count; i++)
            {
                string sourceText = sourceTextsByValue[i];
                string translatedText;
                if (translatedMap.TryGetValue(sourceText, out translatedText))
                {
                    stringValues[i].Value = translatedText;
                }
            }
        }

        private IList<string> TranslateBatchWithFallback(IList<string> texts, string targetLanguage)
        {
            try
            {
                return _client.TranslateBatch(texts, _config.SourceLanguage, targetLanguage);
            }
            catch (Exception ex)
            {
                if (texts.Count <= 1)
                {
                    throw new Exception("Ollama failed to translate source text: " + texts[0], ex);
                }

                int firstCount = texts.Count / 2;
                IList<string> first = texts.Take(firstCount).ToList();
                IList<string> second = texts.Skip(firstCount).ToList();

                Console.WriteLine("  Batch failed, split " + texts.Count + " into " + first.Count + "+" + second.Count + ": " + ex.Message);
                return TranslateBatchWithFallback(first, targetLanguage)
                    .Concat(TranslateBatchWithFallback(second, targetLanguage))
                    .ToList();
            }
        }

        private TranslationMemory CreateMemory(string targetLanguage)
        {
            TranslationMemoryOptions options = _config.TranslationMemory;
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
            string outputRootDirectory = string.IsNullOrWhiteSpace(targetFile.OutputDirectory)
                ? Path.GetDirectoryName(sourceFilePath)
                : ResolvePath(targetFile.OutputDirectory);
            string outputDirectory = Path.Combine(outputRootDirectory, SanitizeFileName(language));

            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            string sourceName = Path.GetFileNameWithoutExtension(sourceFilePath);
            string extension = Path.GetExtension(sourceFilePath);
            string pattern = string.IsNullOrWhiteSpace(targetFile.OutputFileNamePattern)
                ? "{name}{ext}"
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

            if (_config.Ollama == null)
            {
                _config.Ollama = new OllamaOptions();
            }

            foreach (LocalizationTargetFile targetFile in _config.TargetFiles)
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
            return _config.Ollama != null && _config.Ollama.BatchSize > 0 ? _config.Ollama.BatchSize : DefaultBatchSize;
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
