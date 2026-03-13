/****************************************************************************
 * Description: Localization tool that translates JSON files using Ollama API
 * 
 * Document: https://github.com/hiramtan/HiProtobuf
 * Author: hiramtan@live.com
 ****************************************************************************/
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using HiFramework;

namespace Localize
{
    public class LocalizeConfig
    {
        public string DefaultLanguage { get; set; }
        public string JsonFilePath { get; set; }
        public List<string> SupportedLanguages { get; set; }
        public string OllamaApiUrl { get; set; }
        public string Model { get; set; }
    }

    public class LocalizeManager
    {
        private const string DefaultOllamaUrl = "http://127.0.0.1:11434";
        private const string DefaultModel = "llama3";

        private string _ollamaApiUrl;
        private string _model;
        private string _jsonFilePath;
        private string _defaultLanguage;
        private List<string> _supportedLanguages;

        public void Initialize(LocalizeConfig config)
        {
            AssertThat.IsNotNullOrEmpty(config.JsonFilePath, "JsonFilePath");
            AssertThat.IsNotNullOrEmpty(config.DefaultLanguage, "DefaultLanguage");

            _jsonFilePath = config.JsonFilePath;
            _defaultLanguage = config.DefaultLanguage;
            _supportedLanguages = config.SupportedLanguages;
            _ollamaApiUrl = string.IsNullOrEmpty(config.OllamaApiUrl) ? DefaultOllamaUrl : config.OllamaApiUrl;
            _model = string.IsNullOrEmpty(config.Model) ? DefaultModel : config.Model;

            Log.Info($"Localize initialized with default language: {_defaultLanguage}");
            Log.Info($"Json file: {_jsonFilePath}");
            Log.Info($"Supported languages: {string.Join(", ", _supportedLanguages)}");
            Log.Info($"Ollama API: {_ollamaApiUrl}");
            Log.Info($"Model: {_model}");
        }

        public void Translate()
        {
            AssertThat.IsNotNullOrEmpty(_jsonFilePath, "JsonFilePath");
            AssertThat.IsTrue(File.Exists(_jsonFilePath), $"Json file not found: {_jsonFilePath}");

            string jsonContent = File.ReadAllText(_jsonFilePath, Encoding.UTF8);
            Log.Info($"Loaded localization JSON, size: {jsonContent.Length} bytes");

            var directory = Path.GetDirectoryName(_jsonFilePath);
            AssertThat.IsNotNullOrEmpty(directory, "Json file directory");

            string defaultLangDir = Path.Combine(directory, _defaultLanguage);
            if (!Directory.Exists(defaultLangDir))
            {
                Directory.CreateDirectory(defaultLangDir);
                Log.Info($"Created default language directory: {defaultLangDir}");
            }

            string defaultLangFile = Path.Combine(defaultLangDir, Path.GetFileName(_jsonFilePath));
            File.WriteAllText(defaultLangFile, jsonContent, Encoding.UTF8);
            Log.Info($"Saved default language file: {defaultLangFile}");

            foreach (var lang in _supportedLanguages)
            {
                if (lang.Equals(_defaultLanguage, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                TranslateToLanguage(jsonContent, lang, directory);
            }

            Log.Info("Localization translation completed");
        }

        private void TranslateToLanguage(string jsonContent, string targetLanguage, string baseDirectory)
        {
            Log.Info($"Translating to: {targetLanguage}");

            string prompt = $"Translate the following JSON localization file to {targetLanguage}. " +
                            "Keep the JSON structure exactly the same, only translate the values. " +
                            "Return only the translated JSON, no explanation.\n\n" + jsonContent;

            string translatedJson = CallOllama(prompt);

            string langDir = Path.Combine(baseDirectory, targetLanguage);
            if (!Directory.Exists(langDir))
            {
                Directory.CreateDirectory(langDir);
            }

            string outputFile = Path.Combine(langDir, Path.GetFileName(_jsonFilePath));
            File.WriteAllText(outputFile, translatedJson, Encoding.UTF8);
            Log.Info($"Saved translated file: {outputFile}");
        }

        private string CallOllama(string prompt)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromMinutes(5);

                    var requestBody = new
                    {
                        model = _model,
                        prompt = prompt,
                        stream = false
                    };

                    string jsonRequest = JsonConvert.SerializeObject(requestBody);

                    var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
                    var response = client.PostAsync($"{_ollamaApiUrl}/api/generate", content).Result;

                    if (!response.IsSuccessStatusCode)
                    {
                        string error = response.Content.ReadAsStringAsync().Result;
                        Log.Error($"Ollama API error: {response.StatusCode} - {error}");
                        throw new Exception($"Ollama API call failed: {response.StatusCode}");
                    }

                    string responseJson = response.Content.ReadAsStringAsync().Result;
                    var responseObj = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseJson);

                    if (responseObj != null && responseObj.ContainsKey("response"))
                    {
                        return responseObj["response"].ToString();
                    }

                    Log.Warning("Ollama response does not contain 'response' field");
                    return JsonConvert.SerializeObject(responseObj);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to call Ollama API: {ex.Message}");
                throw;
            }
        }
    }
}