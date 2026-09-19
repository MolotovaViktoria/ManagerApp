using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ManagerApp.Data.ScharedData
{
    // Закупочная (себестоимостная) цена товара. Раньше бралась из Bitrix (catalog.product.get ->
    // purchasingPrice), теперь берётся из поля priceBase нового каталога через
    // GET /api/products/{id} (http://83.217.203.29:8090) — эндпоинт для получения одного товара
    // по id. Результаты кратковременно кэшируются в памяти по productId, чтобы повторные обращения
    // к одному и тому же товару (например, при перерисовке грида) не били в сеть каждый раз.
    public class BitrixPurchasePriceService
    {
        private const string ApiBaseUrl = "http://83.217.203.29:8090";
        private const string ApiKey = "aXmKuJy2EHRLOrUDF8IRTNolTkvPgmLYssA0S54m-Vc";

        // Ограничиваем параллелизм при пакетных запросах, чтобы не заддосить сервер каталога.
        private const int MaxParallelRequests = 8;

        private static readonly HttpClient _httpClient = CreateClient();

        private static readonly ConcurrentDictionary<int, (decimal Price, DateTime CachedAt)> _priceCache =
            new ConcurrentDictionary<int, (decimal, DateTime)>();
        private static readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(10);

        private static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            client.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);
            return client;
        }

        private class ApiProductPriceDto
        {
            [JsonProperty("id")]
            public int Id { get; set; }

            [JsonProperty("priceBase")]
            public decimal PriceBase { get; set; }
        }

        public static async Task<decimal> GetPurchasingPriceAsync(int productId)
        {
            if (productId <= 0)
                return 0;

            try
            {
                if (_priceCache.TryGetValue(productId, out var cached) &&
                    (DateTime.Now - cached.CachedAt) < _cacheDuration)
                {
                    return cached.Price;
                }

                var response = await _httpClient.GetAsync($"{ApiBaseUrl}/api/products/{productId}");

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    _priceCache[productId] = (0, DateTime.Now);
                    return 0;
                }

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Ошибка HTTP при получении товара {productId}: {response.StatusCode}");
                    return 0;
                }

                string json = await response.Content.ReadAsStringAsync();
                var dto = JsonConvert.DeserializeObject<ApiProductPriceDto>(json);
                decimal price = dto != null && dto.PriceBase > 0 ? dto.PriceBase : 0;

                _priceCache[productId] = (price, DateTime.Now);
                return price;
            }
            catch (Exception ex)
            {
                // Логируем ошибку, но возвращаем 0, чтобы не блокировать интерфейс
                Console.WriteLine($"Ошибка получения закупочной цены для товара {productId}: {ex.Message}");
                return 0;
            }
        }

        public static async Task<Dictionary<int, decimal>> GetPurchasingPricesBatchAsync(List<int> productIds)
        {
            var result = new Dictionary<int, decimal>();

            if (productIds == null || !productIds.Any())
                return result;

            var distinctIds = productIds.Distinct().ToList();

            using (var throttler = new SemaphoreSlim(MaxParallelRequests))
            {
                var tasks = distinctIds.Select(async id =>
                {
                    await throttler.WaitAsync();
                    try
                    {
                        decimal price = await GetPurchasingPriceAsync(id);
                        return (Id: id, Price: price);
                    }
                    finally
                    {
                        throttler.Release();
                    }
                });

                var results = await Task.WhenAll(tasks);
                foreach (var item in results)
                {
                    result[item.Id] = item.Price;
                }
            }

            return result;
        }
    }
}
