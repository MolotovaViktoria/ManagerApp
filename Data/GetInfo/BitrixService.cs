using ManagerApp.Data.StructureList;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ManagerApp.Data.GetInfo
{
    public class BitrixService
    {
        private readonly HttpClient _httpClient;

        public BitrixService()
        {
            _httpClient = new HttpClient();
        }

        public async Task<List<Category>> GetСategories()
        {
            string webhookUrl = "https://crmnvr.ru/rest/241/5gkwkk4657uafc2x/crm.productsection.list";

            // Делаем запрос
            var response = await _httpClient.GetAsync(webhookUrl);

            // Получаем ответ как текст
            string jsonResponse = await response.Content.ReadAsStringAsync();

            // Парсим JSON и берем только нужные поля
            BitrixCategoryResponse data = JsonConvert.DeserializeObject<BitrixCategoryResponse>(jsonResponse);

            return data.Categories;
        }

        public async Task<List<Product>> GetProducts()
        {
            string categoryId = "683";
            string webhookUrl = $"https://crmnvr.ru/rest/241/5gkwkk4657uafc2x/crm.product.list?filter[SECTION_ID]={categoryId}";

            // Делаем запрос
            var response = await _httpClient.GetAsync(webhookUrl);

            // Получаем ответ как текст
            string jsonResponse = await response.Content.ReadAsStringAsync();

            // Парсим JSON и берем только нужные поля
            BitrixProductResponse data = JsonConvert.DeserializeObject<BitrixProductResponse>(jsonResponse);

            return data.Products;
        }

        public async Task<List<Product>> GetProductsByCategory(string categoryId)
        {
            string webhookUrl = $"https://crmnvr.ru/rest/241/5gkwkk4657uafc2x/crm.product.list?filter[SECTION_ID]={categoryId}";

            var response = await _httpClient.GetAsync(webhookUrl);
            string jsonResponse = await response.Content.ReadAsStringAsync();

            BitrixProductResponse data = JsonConvert.DeserializeObject<BitrixProductResponse>(jsonResponse);
            return data.Products;
        }


    }
    
}
