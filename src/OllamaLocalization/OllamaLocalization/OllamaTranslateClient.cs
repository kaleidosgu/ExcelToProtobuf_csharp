using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OllamaLocalization
{
    public class OllamaTranslateClient
    {
        private const string DefaultEndpoint = "http://localhost:11434/api/generate";
        private const string DefaultModel = "translategemma:12b";

        private readonly OllamaOptions _options;

        public OllamaTranslateClient(OllamaOptions options)
        {
            _options = options ?? new OllamaOptions();
            if (string.IsNullOrWhiteSpace(_options.Endpoint))
            {
                _options.Endpoint = DefaultEndpoint;
            }

            if (string.IsNullOrWhiteSpace(_options.Model))
            {
                _options.Model = DefaultModel;
            }

            if (_options.TimeoutSeconds <= 0)
            {
                _options.TimeoutSeconds = 300;
            }

            if (_options.MaxRetries <= 0)
            {
                _options.MaxRetries = 2;
            }
        }

        public IList<string> TranslateBatch(IList<string> texts, string sourceLanguage, string targetLanguage)
        {
            if (texts == null || texts.Count == 0)
            {
                return new List<string>();
            }

            Exception lastError = null;
            for (int attempt = 1; attempt <= _options.MaxRetries + 1; attempt++)
            {
                try
                {
                    return TranslateBatchOnce(texts, sourceLanguage, targetLanguage);
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    if (attempt > _options.MaxRetries)
                    {
                        break;
                    }

                    Console.WriteLine("  Ollama retry " + attempt + "/" + _options.MaxRetries + ": " + ex.Message);
                }
            }

            throw lastError ?? new Exception("Ollama translation failed.");
        }

        private IList<string> TranslateBatchOnce(IList<string> texts, string sourceLanguage, string targetLanguage)
        {
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);

                string prompt = BuildPrompt(texts, sourceLanguage, targetLanguage);
                var request = new OllamaGenerateRequest
                {
                    model = _options.Model,
                    prompt = prompt,
                    stream = false,
                    format = "json",
                    options = BuildModelOptions()
                };

                string requestJson = JsonConvert.SerializeObject(request);
                var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
                var response = client.PostAsync(_options.Endpoint, content).Result;
                string responseJson = response.Content.ReadAsStringAsync().Result;

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception("Ollama API error: " + response.StatusCode + " - " + responseJson);
                }

                var result = JsonConvert.DeserializeObject<OllamaGenerateResponse>(responseJson);
                if (result == null || string.IsNullOrWhiteSpace(result.response))
                {
                    throw new Exception("Ollama returned an empty response.");
                }

                IList<string> translations = ParseTranslations(result.response);
                if (translations.Count != texts.Count)
                {
                    throw new Exception("Ollama returned " + translations.Count + " translations for " + texts.Count + " source texts.");
                }

                return translations;
            }
        }

        private string BuildPrompt(IList<string> texts, string sourceLanguage, string targetLanguage)
        {
            string systemPrompt = string.IsNullOrWhiteSpace(_options.SystemPrompt)
                ? "You are a professional game/localization translator. Keep placeholders, markup, escape sequences, numbers, and punctuation semantics unchanged."
                : _options.SystemPrompt;

            var payload = new JObject
            {
                ["source_language"] = sourceLanguage,
                ["target_language"] = targetLanguage,
                ["texts"] = JArray.FromObject(texts)
            };

            var builder = new StringBuilder();
            builder.AppendLine(systemPrompt);
            builder.AppendLine("Translate every item in texts from source_language to target_language.");
            builder.AppendLine("Return only valid JSON in this exact shape: {\"translations\":[\"...\"]}");
            builder.AppendLine("The translations array must have the same order and item count as texts.");
            builder.AppendLine(payload.ToString(Formatting.None));
            return builder.ToString();
        }

        private Dictionary<string, object> BuildModelOptions()
        {
            var options = new Dictionary<string, object>
            {
                ["temperature"] = _options.Temperature
            };

            if (_options.TopP > 0)
            {
                options["top_p"] = _options.TopP;
            }

            return options;
        }

        private static IList<string> ParseTranslations(string responseText)
        {
            string json = ExtractJson(responseText);
            JToken root = JToken.Parse(json);

            JArray array = root as JArray;
            if (array == null)
            {
                JObject obj = root as JObject;
                if (obj != null)
                {
                    array = obj["translations"] as JArray;
                }
            }

            if (array == null)
            {
                throw new Exception("Ollama response JSON must be an array or an object with a translations array.");
            }

            return array.Select(item => item.Type == JTokenType.Null ? string.Empty : item.ToString()).ToList();
        }

        private static string ExtractJson(string text)
        {
            string trimmed = (text ?? string.Empty).Trim();
            if (trimmed.StartsWith("```"))
            {
                int firstLineEnd = trimmed.IndexOf('\n');
                int lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
                if (firstLineEnd >= 0 && lastFence > firstLineEnd)
                {
                    trimmed = trimmed.Substring(firstLineEnd + 1, lastFence - firstLineEnd - 1).Trim();
                }
            }

            if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
            {
                return trimmed;
            }

            int objectStart = trimmed.IndexOf('{');
            int arrayStart = trimmed.IndexOf('[');
            int start;
            if (objectStart < 0)
            {
                start = arrayStart;
            }
            else if (arrayStart < 0)
            {
                start = objectStart;
            }
            else
            {
                start = Math.Min(objectStart, arrayStart);
            }

            int objectEnd = trimmed.LastIndexOf('}');
            int arrayEnd = trimmed.LastIndexOf(']');
            int end = Math.Max(objectEnd, arrayEnd);

            if (start < 0 || end <= start)
            {
                throw new Exception("Ollama response did not contain JSON.");
            }

            return trimmed.Substring(start, end - start + 1);
        }

        private class OllamaGenerateRequest
        {
            public string model { get; set; }
            public string prompt { get; set; }
            public bool stream { get; set; }
            public string format { get; set; }
            public Dictionary<string, object> options { get; set; }
        }

        private class OllamaGenerateResponse
        {
            public string response { get; set; }
        }
    }
}
