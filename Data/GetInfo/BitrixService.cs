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

            foreach (var item in data.Categories)
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
                    // Логирование ошибки
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

        /// <summary>
        /// Получает все товары с информацией о категории, цене, количестве и единице измерения
        /// </summary>
        /// <returns>Список товаров с расширенной информацией</returns>
        public async Task<List<ProductWithCategoryInfo>> GetProductsWithCategoryInfo()
        {
            // Получаем все категории и создаем словарь для быстрого поиска по ID
            var categories = await GetСategories();

            // Создаем словарь категорий с проверкой на null
            var categoryDict = new Dictionary<string, string>();

            foreach (var category in categories)
            {
                // Проверяем, что SelectionId не null и не пустой
                if (category.SelectionId != null)
                {
                    string key = category.SelectionId.ToString(); // Преобразуем в строку
                    if (!string.IsNullOrEmpty(key) && !categoryDict.ContainsKey(key))
                    {
                        categoryDict[key] = category.Name ?? "Без названия";
                    }
                }
            }

            // Получаем все товары
            var allProducts = await GetProducts();

            var result = new List<ProductWithCategoryInfo>();

            foreach (var product in allProducts)
            {
                try
                {
                    var productInfo = new ProductWithCategoryInfo
                    {
                        // Название категории (ищем по SECTION_ID)
                        CategoryName = !string.IsNullOrEmpty(product.SectionId) &&
                                       categoryDict.ContainsKey(product.SectionId)
                                     ? categoryDict[product.SectionId]
                                     : "Без категории",

                        // Информация о товаре
                        ProductName = product.Name ?? "Без названия",

                        // Цена (проверяем наличие)
                        Price = product.Price.HasValue ? product.Price.Value : 0m,
                        HasPrice = product.Price.HasValue,

                        // SECTION_ID для возможной дальнейшей обработки
                        SectionId = product.SectionId,

                        // Дополнительная информация из продукта (если есть)
                        ProductCode = product.Code,
                        ProductId = product.Id
                    };

                    result.Add(productInfo);
                }
                catch (Exception ex)
                {
                    // Логирование ошибки обработки товара
                    Console.WriteLine($"Ошибка обработки товара {product?.Id}: {ex.Message}");
                }
            }

            return result;
        }

        /// <summary>
        /// Получает товары с информацией о категории для определенной категории
        /// </summary>
        /// <param name="categoryId">ID категории</param>
        /// <returns>Список товаров с информацией о категории</returns>
        public async Task<List<ProductWithCategoryInfo>> GetProductsWithCategoryInfoByCategory(int categoryId)
        {
            // Получаем название категории
            var categories = await GetСategories();
            var categoryName = categories.FirstOrDefault(c => c.SelectionId.ToString() == categoryId.ToString())?.Name
                               ?? "Неизвестная категория";

            // Получаем товары для категории
            var products = await GetProductsByCategory(categoryId);

            var result = new List<ProductWithCategoryInfo>();

            foreach (var product in products)
            {
                try
                {
                    var productInfo = new ProductWithCategoryInfo
                    {
                        CategoryName = categoryName,
                        ProductName = product.Name ?? "Без названия",
                        Price = product.Price.HasValue ? product.Price.Value : 0m,
                        HasPrice = product.Price.HasValue,
                        SectionId = product.SectionId,
                        ProductCode = product.Code,
                        ProductId = product.Id
                    };

                    result.Add(productInfo);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка обработки товара {product?.Id}: {ex.Message}");
                }
            }

            return result;
        }
    }


  
}