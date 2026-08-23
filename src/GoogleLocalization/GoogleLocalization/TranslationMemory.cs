using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace GoogleLocalization
{
    public class TranslationMemory
    {
        private readonly string _filePath;
        private readonly bool _saveIndented;
        private readonly Dictionary<string, string> _items;

        public TranslationMemory(string filePath, bool saveIndented)
        {
            _filePath = filePath;
            _saveIndented = saveIndented;
            _items = Load(filePath);
        }

        public bool TryGet(string sourceText, out string translatedText)
        {
            if (string.IsNullOrEmpty(sourceText))
            {
                translatedText = sourceText;
                return true;
            }

            return _items.TryGetValue(sourceText, out translatedText);
        }

        public void Set(string sourceText, string translatedText)
        {
            if (string.IsNullOrEmpty(sourceText))
            {
                return;
            }

            _items[sourceText] = translatedText ?? string.Empty;
        }

        public void Save()
        {
            string directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var formatting = _saveIndented ? Formatting.Indented : Formatting.None;
            string json = JsonConvert.SerializeObject(_items, formatting);
            File.WriteAllText(_filePath, json, Encoding.UTF8);
        }

        private static Dictionary<string, string> Load(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return new Dictionary<string, string>();
            }

            string json = File.ReadAllText(filePath, Encoding.UTF8);
            var items = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            return items ?? new Dictionary<string, string>();
        }
    }
}
