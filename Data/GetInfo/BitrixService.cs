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
        public async Task<List<Company>> GetAllCompanies()
        {
            var allCompanies = new List<Company>();
            int start = 0;
            const int pageSize = 50; // Bitrix24 использует по 50 записей на страницу

            while (true)
            {
                // entityTypeId = 4 для компаний
                string webhookUrl = $"https://crmnvr.ru/rest/241/5gkwkk4657uafc2x/crm.item.list";
                
                // Подготавливаем POST-запрос с параметрами
                var requestData = new
                {
                    entityTypeId = 4, // 4 - компании
                    select = new[] { "id", "title", "assignedById", "createdTime" }, // Основные поля
                    start = start
                };

                string jsonRequest = JsonConvert.SerializeObject(requestData);
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                // Делаем POST-запрос
                var response = await _httpClient.PostAsync(webhookUrl, content);

                // Получаем ответ как текст
                string jsonResponse = await response.Content.ReadAsStringAsync();

                // Парсим JSON
                var result = JsonConvert.DeserializeObject<BitrixListResponse>(jsonResponse);

                if (result?.Result?.Items == null || result.Result.Items.Count == 0)
                    break;

                // Преобразуем в список компаний
                foreach (var item in result.Result.Items)
                {
                    var company = new Company
                    {
                        Id = item.Id,
                        Title = item.Title,
                        AssignedById = item.AssignedById,
                        CreatedTime = item.CreatedTime
                    };
                    allCompanies.Add(company);
                }

                // Если получено меньше записей, чем размер страницы - значит это последняя страница
                if (result.Result.Items.Count < pageSize)
                    break;

                // Проверяем, есть ли еще данные
                if (result.Next.HasValue && result.Next.Value > start)
                {
                    start = result.Next.Value;
                }
                else
                {
                    start += pageSize;
                }
            }

            return allCompanies;
        }

        /// <summary>
        /// Получает список только моих компаний (компании где isMyCompany = "Y")
        /// </summary>
        /// <returns>Список моих компаний</returns>
        public async Task<List<Company>> GetMyCompanies()
        {
            var myCompanies = new List<Company>();
            int start = 0;
            const int pageSize = 50;

            while (true)
            {
                string webhookUrl = $"https://crmnvr.ru/rest/241/5gkwkk4657uafc2x/crm.item.list";
                
                // Используем фильтр isMyCompany = "Y" как показано в документации
                var requestData = new
                {
                    entityTypeId = 4, // 4 - компании
                    select = new[] { "id", "title", "assignedById", "createdTime", "isMyCompany" },
                    filter = new
                    {
                        isMyCompany = "Y"
                    },
                    start = start
                };

                string jsonRequest = JsonConvert.SerializeObject(requestData);
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(webhookUrl, content);
                string jsonResponse = await response.Content.ReadAsStringAsync();

                var result = JsonConvert.DeserializeObject<BitrixListResponse>(jsonResponse);

                if (result?.Result?.Items == null || result.Result.Items.Count == 0)
                    break;

                foreach (var item in result.Result.Items)
                {
                    var company = new Company
                    {
                        Id = item.Id,
                        Title = item.Title,
                        AssignedById = item.AssignedById,
                        CreatedTime = item.CreatedTime,
                        IsMyCompany = item.IsMyCompany
                    };
                    myCompanies.Add(company);
                }

                if (result.Result.Items.Count < pageSize)
                    break;

                if (result.Next.HasValue && result.Next.Value > start)
                {
                    start = result.Next.Value;
                }
                else
                {
                    start += pageSize;
                }
            }

            return myCompanies;
        }

        public async Task<int> CreateCompany(string title, string phone = null, string address = null, bool registerEvent = true)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException("Название компании обязательно");
            }

            string webhookUrl = "https://crmnvr.ru/rest/241/5gkwkk4657uafc2x/crm.company.add";

            // Подготавливаем данные для создания компании
            var requestData = new
            {
                fields = new
                {
                    TITLE = title.Trim(),
                    PHONE = !string.IsNullOrWhiteSpace(phone) ?
                           new[] { new { VALUE = phone.Trim(), VALUE_TYPE = "WORK" } } :
                           null,
                    ADDRESS = !string.IsNullOrWhiteSpace(address) ? address.Trim() : null
                },
                @params = new
                {
                    REGISTER_SONET_EVENT = registerEvent ? "Y" : "N"
                }
            };

            try
            {
                string jsonRequest = JsonConvert.SerializeObject(requestData);
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                // Делаем POST-запрос
                var response = await _httpClient.PostAsync(webhookUrl, content);
                string jsonResponse = await response.Content.ReadAsStringAsync();

                // Парсим ответ
                var result = JsonConvert.DeserializeObject<BitrixAddResponse>(jsonResponse);

                // Проверяем на ошибки
                if (!string.IsNullOrEmpty(result?.Error))
                {
                    Console.WriteLine($"Ошибка создания компании: {result.Error}");
                    return 0;
                }

                // Возвращаем ID созданной компании
                return result?.Result ?? 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Исключение при создании компании: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Создает компанию с расширенными полями (все поля необязательные, кроме названия)
        /// </summary>
        public async Task<int> CreateCompanyExtended(
            string title,
            string phone = null,
            string address = null,
            string companyType = null,
            string industry = null,
            string employees = null,
            string currencyId = null,
            decimal? revenue = null,
            bool isOpened = true,
            int? assignedById = null,
            bool registerEvent = true)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException("Название компании обязательно");
            }

            string webhookUrl = "https://crmnvr.ru/rest/241/5gkwkk4657uafc2x/crm.company.add";

            // Основные поля
            var fields = new Dictionary<string, object>
            {
                ["TITLE"] = title.Trim()
            };

            // Телефон
            if (!string.IsNullOrWhiteSpace(phone))
            {
                fields["PHONE"] = new[]
                {
                    new { VALUE = phone.Trim(), VALUE_TYPE = "WORK" }
                };
            }

            // Адрес
            if (!string.IsNullOrWhiteSpace(address))
            {
                fields["ADDRESS"] = address.Trim();
            }

            // Дополнительные поля (если указаны)
            if (!string.IsNullOrWhiteSpace(companyType))
                fields["COMPANY_TYPE"] = companyType;

            if (!string.IsNullOrWhiteSpace(industry))
                fields["INDUSTRY"] = industry;

            if (!string.IsNullOrWhiteSpace(employees))
                fields["EMPLOYEES"] = employees;

            if (!string.IsNullOrWhiteSpace(currencyId))
                fields["CURRENCY_ID"] = currencyId;

            if (revenue.HasValue)
                fields["REVENUE"] = revenue.Value;

            fields["OPENED"] = isOpened ? "Y" : "N";

            if (assignedById.HasValue)
                fields["ASSIGNED_BY_ID"] = assignedById.Value;

            var requestData = new
            {
                fields = fields,
                @params = new
                {
                    REGISTER_SONET_EVENT = registerEvent ? "Y" : "N"
                }
            };

            try
            {
                string jsonRequest = JsonConvert.SerializeObject(requestData);
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(webhookUrl, content);
                string jsonResponse = await response.Content.ReadAsStringAsync();

                var result = JsonConvert.DeserializeObject<BitrixAddResponse>(jsonResponse);

                if (!string.IsNullOrEmpty(result?.Error))
                {
                    Console.WriteLine($"Ошибка создания компании: {result.Error}");
                    return 0;
                }

                return result?.Result ?? 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Исключение при создании компании: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Пытается создать компанию, если не существует компаний с таким названием
        /// </summary>
        public async Task<int> CreateCompanyIfNotExists(string title, string phone = null, string address = null)
        {
            // Получаем все компании для проверки дубликатов
            var companies = await GetAllCompanies();

            // Проверяем, существует ли компания с таким названием
            var exists = companies.Any(c =>
                c.Title?.Equals(title, StringComparison.OrdinalIgnoreCase) ?? false);

            if (exists)
            {
                Console.WriteLine($"Компания '{title}' уже существует");
                return -1; // Специальный код для существующей компании
            }

            // Создаем новую компанию
            return await CreateCompany(title, phone, address);
        }

        // Вспомогательный класс для десериализации ответа от метода add
        public class BitrixAddResponse
        {
            [JsonProperty("result")]
            public int Result { get; set; }

            [JsonProperty("error")]
            public string Error { get; set; }

            [JsonProperty("error_description")]
            public string ErrorDescription { get; set; }
        }
    }


    // Вспомогательные классы для десериализации ответа от Bitrix24
    public class BitrixListResponse
    {
        [JsonProperty("result")]
        public BitrixResult Result { get; set; }

        [JsonProperty("total")]
        public int Total { get; set; }

        [JsonProperty("next")]
        public int? Next { get; set; }
    }

    public class BitrixResult
    {
        [JsonProperty("items")]
        public List<BitrixItem> Items { get; set; }
    }

    public class BitrixItem
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("assignedById")]
        public int AssignedById { get; set; }

        [JsonProperty("createdTime")]
        public DateTime CreatedTime { get; set; }

        [JsonProperty("isMyCompany")]
        public string IsMyCompany { get; set; }
    }

    public class Company
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public int AssignedById { get; set; }
        public DateTime CreatedTime { get; set; }
        public string IsMyCompany { get; set; }
    }
}



