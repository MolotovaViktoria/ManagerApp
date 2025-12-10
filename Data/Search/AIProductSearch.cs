using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ManagerApp.Data.Search
{
    public class AIProductSearch
    {
        private const string API_URL = "http://185.177.216.82:5000/api/ProductAnalysis/analyze";
        private static readonly Dictionary<string, bool> _aiCache = new Dictionary<string, bool>();
        private static readonly object _cacheLock = new object();
        private readonly HttpClient _httpClient;

        public AIProductSearch()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(15);
        }

        public async Task<bool> IsProductAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            lock (_cacheLock)
            {
                if (_aiCache.TryGetValue(text, out bool cached))
                    return cached;
            }

            try
            {
                var prompt = $"Это позиция/наименование/материал/продукция/изделие/ТМЦ? 1=да, 2=нет. Текст: {text}";
                var requestData = new { text = prompt };
                string json = JsonConvert.SerializeObject(requestData);

                var content = new StringContent(json, Encoding.UTF8, "application/json");

                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
                {
                    var response = await _httpClient.PostAsync(API_URL, content, cts.Token);

                    if (!response.IsSuccessStatusCode)
                        return false;

                    string responseText = await response.Content.ReadAsStringAsync();
                    bool isProduct = responseText.Contains("1") && !responseText.Contains("2");

                    lock (_cacheLock)
                    {
                        _aiCache[text] = isProduct;
                    }

                    return isProduct;
                }
            }
            catch
            {
                return false;
            }
        }

        public void ClearCache()
        {
            lock (_cacheLock)
            {
                _aiCache.Clear();
            }
        }
    }
}
