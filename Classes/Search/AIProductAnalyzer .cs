using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ManagerApp.Classes.Search
{
    public class AIProductAnalyzer
    {
        private const string API_URL = "http://185.177.216.82:5000/api/ProductAnalysis/analyze";
        private static readonly Dictionary<string, bool> _aiCache = new Dictionary<string, bool>();
        private static readonly object _cacheLock = new object();

        /// <summary>
        /// Анализирует список строк через AI API для поиска товаров
        /// </summary>
        /// <param name="lines">Список строк для анализа</param>
        /// <param name="progressCallback">Колбек для отображения прогресса</param>
        /// <param name="cancellationToken">Токен отмены</param>
        /// <returns>Список найденных товаров</returns>
        public async Task<List<string>> AnalyzeViaAIAsync(
            List<string> lines,
            Action<string> progressCallback = null,
            CancellationToken cancellationToken = default)
        {
            var foundProducts = new List<string>();

            try
            {
                progressCallback?.Invoke($"🔄 Начинаем AI анализ {lines.Count} строк...");

                int processed = 0;
                int total = lines.Count;

                // Обрабатываем параллельно с ограничением
                var options = new ParallelOptions
                {
                    MaxDegreeOfParallelism = 2,
                    CancellationToken = cancellationToken
                };

                await Task.Run(() =>
                {
                    Parallel.ForEach(lines, options, (line, state) =>
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            state.Stop();
                            return;
                        }

                        int current = Interlocked.Increment(ref processed);

                        try
                        {
                            progressCallback?.Invoke($"[{current}/{total}] Анализ: {Truncate(line, 40)}");

                            // Проверяем через AI
                            bool isProduct = CheckWithAISimple(line).Result;

                            if (isProduct)
                            {
                                lock (foundProducts)
                                {
                                    foundProducts.Add(line);
                                }
                                progressCallback?.Invoke($"  ✅ AI определил как товар");
                            }
                            else
                            {
                                progressCallback?.Invoke($"  ❌ AI определил как не товар");
                            }
                        }
                        catch (Exception ex)
                        {
                            progressCallback?.Invoke($"  ⚠️ Ошибка AI анализа: {ex.Message}");
                        }
                    });
                }, cancellationToken);

                progressCallback?.Invoke($"📊 AI анализ завершен. Найдено товаров: {foundProducts.Count}");
            }
            catch (OperationCanceledException)
            {
                progressCallback?.Invoke("⏹️ AI анализ прерван");
            }
            catch (Exception ex)
            {
                progressCallback?.Invoke($"❌ Ошибка AI анализа: {ex.Message}");
            }

            return foundProducts;
        }

        /// <summary>
        /// Проверка одной строки через AI API
        /// </summary>
        private async Task<bool> CheckWithAISimple(string text)
        {
            // Проверяем кэш
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

                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(15);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
                    {
                        var response = await client.PostAsync(API_URL, content, cts.Token);

                        if (!response.IsSuccessStatusCode)
                            return false;

                        string responseText = await response.Content.ReadAsStringAsync();
                        bool isProduct = responseText.Contains("1") && !responseText.Contains("2");

                        // Сохраняем в кэш
                        lock (_cacheLock)
                        {
                            _aiCache[text] = isProduct;
                        }

                        return isProduct;
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        private string Truncate(string text, int maxLength)
        {
            return string.IsNullOrEmpty(text) || text.Length <= maxLength
                ? text
                : text.Substring(0, maxLength) + "...";
        }
    }
}
