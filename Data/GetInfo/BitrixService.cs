using ManagerApp.Data.StructureList;
using Microsoft.Office.Interop.Word;
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

        public async Task<List<StructureList.Category>> GetСategories()
        {
            string webhookUrl = "https://crmnvr.ru/rest/241/5gkwkk4657uafc2x/crm.productsection.list";

            // Делаем запрос
            var response = await _httpClient.GetAsync(webhookUrl);

            // Получаем ответ как текст
            string jsonResponse = await response.Content.ReadAsStringAsync();

            // Парсим JSON и берем только нужные поля
            BitrixCategoryResponse data = JsonConvert.DeserializeObject<BitrixCategoryResponse>(jsonResponse);


            List<StructureList.Category> anwer = new List<StructureList.Category>();

            foreach(var  item in data.Categories)
            {
                try
                {
                    
                    StructureList.Category category = new StructureList.Category();
                    category.Name = item.Name;
                    category.SelectionId = item.SelectionId + 1;
                    anwer.Add(category);
                }
                catch
                {

                }
            }

            return anwer;
        }

        public async Task<List<Product>> GetProducts()
        {
            var allProducts = new List<Product>();
            int start = 0;
            const int pageSize = 50; // Bitrix24 обычно использует 50

            while (true)
            {
                string webhookUrl = $"https://crmnvr.ru/rest/241/5gkwkk4657uafc2x/crm.product.list?start={start}";

                // Делаем запрос
                var response = await _httpClient.GetAsync(webhookUrl);

                // Получаем ответ как текст
                string jsonResponse = await response.Content.ReadAsStringAsync();

                // Парсим JSON и берем только нужные поля
                BitrixProductResponse data = JsonConvert.DeserializeObject<BitrixProductResponse>(jsonResponse);

                if (data.Products == null || data.Products.Count == 0)
                    break;

                allProducts.AddRange(data.Products);

                // Если получено меньше записей, чем размер страницы - значит это последняя страница
                if (data.Products.Count < pageSize)
                    break;

                start += pageSize;
            }

            return allProducts;
        }

        public async Task<List<Product>> GetProductsByCategory(int categoryId)
        {
            var allProducts = new List<Product>();
            int start = 0;
            const int pageSize = 50; // Bitrix24 обычно использует 50

            while (true)
            {
                string webhookUrl = $"https://crmnvr.ru/rest/241/5gkwkk4657uafc2x/crm.product.list?filter[SECTION_ID]={categoryId}&start={start}";

                var response = await _httpClient.GetAsync(webhookUrl);
                string jsonResponse = await response.Content.ReadAsStringAsync();

                BitrixProductResponse data = JsonConvert.DeserializeObject<BitrixProductResponse>(jsonResponse);

                if (data.Products == null || data.Products.Count == 0)
                    break;

                allProducts.AddRange(data.Products);

                // Если получено меньше записей, чем размер страницы - значит это последняя страница
                if (data.Products.Count < pageSize)
                    break;

                start += pageSize;
            }

            return allProducts;
        }

         


    }
    
}
