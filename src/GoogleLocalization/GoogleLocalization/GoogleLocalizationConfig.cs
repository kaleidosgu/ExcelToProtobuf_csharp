using System.Collections.Generic;

namespace GoogleLocalization
{
    public class GoogleLocalizationConfig
    {
        public string SourceLanguage { get; set; }
        public List<string> TargetLanguages { get; set; }
        public List<string> SupportedLanguages { get; set; }
        public GoogleTranslateOptions Google { get; set; }
        public TranslationMemoryOptions TranslationMemory { get; set; }
        public List<LocalizationTargetFile> TargetFiles { get; set; }
    }

    public class GoogleTranslateOptions
    {
        public string ApiKey { get; set; }
        public string OAuthAccessToken { get; set; }
        public string Endpoint { get; set; }
        public int TimeoutSeconds { get; set; }
        public int BatchSize { get; set; }
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
