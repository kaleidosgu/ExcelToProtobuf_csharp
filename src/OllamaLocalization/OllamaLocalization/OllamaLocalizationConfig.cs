using System.Collections.Generic;

namespace OllamaLocalization
{
    public class OllamaLocalizationConfig
    {
        public string SourceLanguage { get; set; }
        public List<string> TargetLanguages { get; set; }
        public List<string> SupportedLanguages { get; set; }
        public OllamaOptions Ollama { get; set; }
        public TranslationMemoryOptions TranslationMemory { get; set; }
        public List<LocalizationTargetFile> TargetFiles { get; set; }
    }

    public class OllamaOptions
    {
        public string Endpoint { get; set; }
        public string Model { get; set; }
        public int TimeoutSeconds { get; set; }
        public int BatchSize { get; set; }
        public int MaxRetries { get; set; }
        public double Temperature { get; set; }
        public double TopP { get; set; }
        public string SystemPrompt { get; set; }
    }

    public class TranslationMemoryOptions
    {
        public string Directory { get; set; }
        public bool Enabled { get; set; }
        public bool SaveIndented { get; set; }
    }

    public class LocalizationTargetFile
    {
        public string SourceFilePath { get; set; }
        public string OutputDirectory { get; set; }
        public string OutputFileNamePattern { get; set; }
        public bool WriteSourceLanguageCopy { get; set; }
    }
}
