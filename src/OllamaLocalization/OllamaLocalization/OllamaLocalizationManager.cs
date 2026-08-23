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

            var translatedRoots = new Dictionary<string, JToken>(StringComparer.OrdinalIgnoreCase)
            {
                [_config.SourceLanguage] = sourceRoot
            };
            var completedLanguages = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                _config.SourceLanguage
            };

            foreach (string targetLanguage in GetTargetLanguages())
            {
                EnsureLanguageOutput(targetFile, sourceFilePath, targetLanguage, translatedRoots, completedLanguages, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            }
        }

        private JToken EnsureLanguageOutput(
            LocalizationTargetFile targetFile,
            string sourceFilePath,
            string targetLanguage,
            IDictionary<string, JToken> translatedRoots,
            ISet<string> completedLanguages,
            ISet<string> visitingLanguages)
        {
            if (translatedRoots.ContainsKey(targetLanguage))
            {
                return translatedRoots[targetLanguage];
            }

            if (completedLanguages.Contains(targetLanguage))
            {
                string outputPath = GetOutputPath(targetFile, sourceFilePath, targetLanguage);
                JToken existingRoot = JToken.Parse(File.ReadAllText(outputPath, Encoding.UTF8));
                translatedRoots[targetLanguage] = existingRoot;
                return existingRoot;
            }

            if (!visitingLanguages.Add(targetLanguage))
            {
                throw new Exception("Circular language dependency detected: " + string.Join(" -> ", visitingLanguages) + " -> " + targetLanguage);
            }

            string sourceLanguage = GetSourceLanguageForTarget(targetLanguage);
            if (string.Equals(sourceLanguage, targetLanguage, StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception("Language " + targetLanguage + " cannot use itself as translation source.");
            }

            JToken sourceRoot = EnsureLanguageOutput(targetFile, sourceFilePath, sourceLanguage, translatedRoots, completedLanguages, visitingLanguages);

            Console.WriteLine("Translating " + sourceLanguage + " -> " + targetLanguage);
            JToken translatedRoot = sourceRoot.DeepClone();
            TranslateJsonToken(translatedRoot, sourceLanguage, targetLanguage);
            WriteOutput(targetFile, sourceFilePath, targetLanguage, translatedRoot);

            translatedRoots[targetLanguage] = translatedRoot;
            completedLanguages.Add(targetLanguage);
            visitingLanguages.Remove(targetLanguage);
            return translatedRoot;
        }

        private void TranslateJsonToken(JToken token, string sourceLanguage, string targetLanguage)
        {
            var stringValues = new List<JValue>();
            CollectStringValues(token, stringValues);

            var memory = CreateMemory(sourceLanguage, targetLanguage);
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
                IList<string> translatedTexts = TranslateBatchWithFallback(batch, sourceLanguage, targetLanguage);
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

        private IList<string> TranslateBatchWithFallback(IList<string> texts, string sourceLanguage, string targetLanguage)
        {
            try
            {
                return _client.TranslateBatch(texts, sourceLanguage, targetLanguage);
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
                return TranslateBatchWithFallback(first, sourceLanguage, targetLanguage)
                    .Concat(TranslateBatchWithFallback(second, sourceLanguage, targetLanguage))
                    .ToList();
            }
        }

        private TranslationMemory CreateMemory(string sourceLanguage, string targetLanguage)
        {
            TranslationMemoryOptions options = _config.TranslationMemory;
            if (options == null || !options.Enabled)
            {
                return null;
            }

            string directory = string.IsNullOrWhiteSpace(options.Directory) ? "translation-memory" : options.Directory;
            string memoryDirectory = ResolvePath(directory);
            string fileName = SanitizeFileName(sourceLanguage) + "_to_" + SanitizeFileName(targetLanguage) + ".json";
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
            string outputPath = GetOutputPath(targetFile, sourceFilePath, language);
            string outputDirectory = Path.GetDirectoryName(outputPath);
            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            File.WriteAllText(outputPath, root.ToString(Formatting.Indented), Encoding.UTF8);
            Console.WriteLine("  Saved: " + outputPath);
        }

        private string GetOutputPath(LocalizationTargetFile targetFile, string sourceFilePath, string language)
        {
            string outputRootDirectory = string.IsNullOrWhiteSpace(targetFile.OutputDirectory)
                ? Path.GetDirectoryName(sourceFilePath)
                : ResolvePath(targetFile.OutputDirectory);
            string outputDirectory = Path.Combine(outputRootDirectory, SanitizeFileName(language));

            string sourceName = Path.GetFileNameWithoutExtension(sourceFilePath);
            string extension = Path.GetExtension(sourceFilePath);
            string pattern = string.IsNullOrWhiteSpace(targetFile.OutputFileNamePattern)
                ? "{name}{ext}"
                : targetFile.OutputFileNamePattern;

            string fileName = pattern
                .Replace("{name}", sourceName)
                .Replace("{language}", language)
                .Replace("{ext}", extension);

            return Path.Combine(outputDirectory, fileName);
        }

        private void ValidateConfig()
        {
            RequireText(_config.SourceLanguage, "SourceLanguage");
            if (string.IsNullOrWhiteSpace(_config.DefaultTranslationSourceLanguage))
            {
                _config.DefaultTranslationSourceLanguage = _config.SourceLanguage;
            }

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

        private string GetSourceLanguageForTarget(string targetLanguage)
        {
            if (_config.LanguageSourceLanguages != null)
            {
                string sourceLanguage;
                if (_config.LanguageSourceLanguages.TryGetValue(targetLanguage, out sourceLanguage) && !string.IsNullOrWhiteSpace(sourceLanguage))
                {
                    return sourceLanguage;
                }

                foreach (KeyValuePair<string, string> item in _config.LanguageSourceLanguages)
                {
                    if (string.Equals(item.Key, targetLanguage, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(item.Value))
                    {
                        return item.Value;
                    }
                }
            }

            return _config.DefaultTranslationSourceLanguage;
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
