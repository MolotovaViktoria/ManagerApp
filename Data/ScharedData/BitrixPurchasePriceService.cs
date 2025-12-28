using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ManagerApp.Data.ScharedData
{
    public class BitrixPurchasePriceService
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private static readonly string _webhookUrl = "https://crmnvr.ru/rest/241/5gkwkk4657uafc2x/";
        private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

        static BitrixPurchasePriceService()
        {
            _httpClient.Timeout = _timeout;
        }

        public static async Task<decimal> GetPurchasingPriceAsync(int productId)
        {
            try
            {
                // Используем метод catalog.product.get, который содержит purchasingPrice
                string url = $"{_webhookUrl}catalog.product.get?id={productId}";

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                string json = await response.Content.ReadAsStringAsync();
                var data = JsonConvert.DeserializeObject<JObject>(json);

                // Извлекаем закупочную цену
                var purchasingPrice = data?["result"]?["product"]?["purchasingPrice"]?.Value<decimal>();

                if (purchasingPrice.HasValue && purchasingPrice.Value > 0)
                {
                    return purchasingPrice.Value;
                }

                return 0;
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

            // Для оптимизации можно использовать batch-запросы, но для простоты делаем последовательно
            // (у вас небольшое количество товаров)
            foreach (var productId in productIds)
            {
                try
                {
                    var price = await GetPurchasingPriceAsync(productId);
                    result[productId] = price;
                }
                catch
                {
                    result[productId] = 0;
                }
            }

            return result;
        }
    }
}