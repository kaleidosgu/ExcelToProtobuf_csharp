using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;

namespace GoogleLocalization
{
    public class GoogleTranslateClient
    {
        private const string DefaultEndpoint = "https://translation.googleapis.com/language/translate/v2";

        private readonly GoogleTranslateOptions _options;

        public GoogleTranslateClient(GoogleTranslateOptions options)
        {
            _options = options ?? new GoogleTranslateOptions();
            if (string.IsNullOrWhiteSpace(_options.Endpoint))
            {
                _options.Endpoint = DefaultEndpoint;
            }

            if (_options.TimeoutSeconds <= 0)
            {
                _options.TimeoutSeconds = 60;
            }
        }

        public IList<string> TranslateBatch(IList<string> texts, string sourceLanguage, string targetLanguage)
        {
            if (texts == null || texts.Count == 0)
            {
                return new List<string>();
            }

            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);

                string endpoint = BuildEndpoint();
                if (!string.IsNullOrWhiteSpace(_options.OAuthAccessToken))
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.OAuthAccessToken);
                }

                var request = new GoogleTranslateRequest
                {
                    q = texts.ToList(),
                    source = sourceLanguage,
                    target = targetLanguage,
                    format = "text"
                };

                string requestJson = JsonConvert.SerializeObject(request);
                var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
                var response = client.PostAsync(endpoint, content).Result;
                string responseJson = response.Content.ReadAsStringAsync().Result;

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception("Google Translate API error: " + response.StatusCode + " - " + responseJson);
                }

                var result = JsonConvert.DeserializeObject<GoogleTranslateResponse>(responseJson);
                var translations = result != null && result.data != null && result.data.translations != null
                    ? result.data.translations
                    : new List<GoogleTranslation>();

                if (translations.Count != texts.Count)
                {
                    throw new Exception("Google Translate API returned " + translations.Count + " translations for " + texts.Count + " source texts.");
                }

                return translations.Select(item => WebUtility.HtmlDecode(item.translatedText ?? string.Empty)).ToList();
            }
        }

        private string BuildEndpoint()
        {
            if (string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                return _options.Endpoint;
            }

            string separator = _options.Endpoint.Contains("?") ? "&" : "?";
            return _options.Endpoint + separator + "key=" + Uri.EscapeDataString(_options.ApiKey);
        }

        private class GoogleTranslateRequest
        {
            public List<string> q { get; set; }
            public string source { get; set; }
            public string target { get; set; }
            public string format { get; set; }
        }

        private class GoogleTranslateResponse
        {
            public GoogleTranslateData data { get; set; }
        }

        private class GoogleTranslateData
        {
            public List<GoogleTranslation> translations { get; set; }
        }

        private class GoogleTranslation
        {
            public string translatedText { get; set; }
        }
    }
}
