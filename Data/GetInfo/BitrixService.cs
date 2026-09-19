using ManagerApp.Classes.Setting;
using ManagerApp.Data.StructureList;
using ManagerApp.Pages;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;  // Для Process
using System.IO;           // Для File
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BitrixProductResponse = ManagerApp.Data.StructureList.BitrixProductResponse;
namespace ManagerApp.Data.GetInfo
{
    public class BitrixService
    {
        // ============ НОВЫЙ API КАТАЛОГА (замена Bitrix24, см. crmnvr.ru — отключен) ============
        private const string ApiBaseUrl = "http://83.217.203.29:8090";
        private const string ApiKey = "aXmKuJy2EHRLOrUDF8IRTNolTkvPgmLYssA0S54m-Vc";

        // Общий HttpClient для запросов к новому API (с заголовком X-Api-Key)
        private static readonly HttpClient _apiClient = CreateApiClient();

        private static HttpClient CreateApiClient()
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(60)
            };
            client.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);
            return client;
        }

        private readonly HttpClient _httpClient;

        public BitrixService()
        {
            _httpClient = new HttpClient();
        }
        /// <summary>
        /// Получает название раздела для товара из кэша
        /// </summary>
       private static List<Category> _cachedCategories = null;
        private static DateTime _lastCategoriesUpdate = DateTime.MinValue;
        private static readonly TimeSpan _categoriesCacheDuration = TimeSpan.FromMinutes(30);
        private static readonly object _categoriesLock = new object();

        // Кратковременный кэш полного дампа товаров (/api/products/all), чтобы GetProducts()/
        // GetProductsByCategory() не тянули по 50к строк из сети при каждом вызове подряд.
        private static List<ApiProductDto> _allProductsRawCache = null;
        private static DateTime _allProductsRawCacheTime = DateTime.MinValue;
        private static readonly TimeSpan _allProductsRawCacheDuration = TimeSpan.FromMinutes(10);
        private static readonly SemaphoreSlim _allProductsRawLock = new SemaphoreSlim(1, 1);


        public async Task<int> CreateProductAsync(string productName)
        {
            try
            {
                // Сначала проверяем, нет ли уже такого товара в каталоге
                var existingId = await GetProductIdByName(productName);
                if (existingId > 0)
                {
                    Console.WriteLine($"Товар '{productName}' уже существует (ID: {existingId})");
                    return existingId;
                }

                // ПРОБЕЛ В API: сервер каталога (http://83.217.203.29:8090) не предоставляет
                // endpoint для создания нового товара — каталог read-only (импортирован из Bitrix).
                // Возвращаем 0, вызывающий код (ComparisonProduct.xaml.cs) уже обрабатывает
                // отрицательный/нулевой результат как "не удалось создать" и не падает.
                Console.WriteLine($"⚠️ Товар '{productName}' не найден в каталоге. Создание нового товара не поддерживается новым API (нет endpoint создания товара).");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка при создании товара '{productName}': {ex.Message}");
                return 0;
            }
        }

        // В классе BitrixService добавьте:
        public async Task<List<BitrixMeasure>> GetMeasuresAsync()
        {
            // Новый каталог хранит единицы измерения простым текстом (шт/м/кг/упак),
            // а не через справочник Bitrix, поэтому обходимся без сетевого запроса.
            return await Task.FromResult(GetStaticMeasures());
        }

        internal static List<BitrixMeasure> GetStaticMeasures()
        {
            return new List<BitrixMeasure>
            {
                new BitrixMeasure { ID = "796", CODE = "796", SYMBOL_RUS = "шт",   MEASURE_TITLE = "Штука" },
                new BitrixMeasure { ID = "006", CODE = "006", SYMBOL_RUS = "м",    MEASURE_TITLE = "Метр" },
                new BitrixMeasure { ID = "166", CODE = "166", SYMBOL_RUS = "кг",   MEASURE_TITLE = "Килограмм" },
                new BitrixMeasure { ID = "778", CODE = "778", SYMBOL_RUS = "упак", MEASURE_TITLE = "Упаковка" },
            };
        }

        // Класс для десериализации ответа от Bitrix (оставлены для совместимости, больше не используются)

        public class BitrixMeasureResponse
        {
            [JsonProperty("result")]
            public BitrixMeasureListResult Result { get; set; }
        }

        public class BitrixMeasureListResult
        {
            [JsonProperty("measures")]
            public List<BitrixMeasure> Measures { get; set; }
        }

        // TODO(local-invoice-generation): генерация документа переписывается на локальный OpenXML
        // отдельной параллельной задачей. Bitrix (crmnvr.ru) отключен — метод больше не должен
        // никуда стучаться, просто безопасно возвращает null.
        public Task<byte[]> DownloadDocumentBytes(string documentUrl)
        {
            Console.WriteLine("[BitrixService] DownloadDocumentBytes: заглушка, Bitrix отключен, генерация документов теперь локальная (OpenXML).");
            return Task.FromResult<byte[]>(null);
        }

        /// <summary>
        /// TODO(local-invoice-generation): генерация документа переписывается на локальный OpenXML
        /// отдельной параллельной задачей. Заглушка, безопасно возвращает null вместо обращения
        /// к отключенному Bitrix (crmnvr.ru).
        /// </summary>
        public Task<string> GenerateInvoiceDocument(
            int invoiceId,
            int templateId,
            string format = "docx")
        {
            Console.WriteLine("[BitrixService] GenerateInvoiceDocument: заглушка, Bitrix отключен, генерация документов теперь локальная (OpenXML).");
            return Task.FromResult<string>(null);
        }

        // Больше не используется нигде в проекте (единственный вызывающий код,
        // InvoicionCreatePage.CheckInvoiceExistsAsync, переписан на новый API напрямую).
        // Оставлен как безопасная заглушка на случай, если что-то забытое ещё на него ссылается -
        // Bitrix (crmnvr.ru) отключен, поэтому метод больше никуда не стучится.
        public async Task<JObject> CallMethodAsync(string method, object parameters)
        {
            await Task.CompletedTask;
            Console.WriteLine($"[BitrixService] CallMethodAsync('{method}') вызван, но Bitrix отключен - возвращаю пустой результат");
            return new JObject();
        }

        public async Task<int> CreateSmartInvoice(
     DateTime invoiceDate,
     int clientCompanyId,
     int myCompanyId,
     string orderTopic,
     List<InvoiceProduct> products,
     string number_chet,
     string adress,
     string day_dostavka,
     string sposob_oplata,
     int responsibleId,
     string statusId = "DT31_1:NEW",
     DateTime? payBeforeDate = null)
        {
            try
            {
                Console.WriteLine("=== СОЗДАНИЕ НОВОГО СЧЕТА (новый API каталога) ===");
                Console.WriteLine($"📅 Дата: {invoiceDate:dd.MM.yyyy}");

                // 32 = СПК, 34 = НВР (см. MyLocalCompany.TemplateId в InvoicionCreatePage.xaml.cs).
                // Всё, что не 34, по умолчанию считаем СПК — так же, как раньше вело себя
                // GetTemplateIdForMyCompany() (дефолт 32/СПК при отсутствии выбора).
                string sellerCompany = myCompanyId == 34 ? "NVR" : "SPK";

                int? deliveryDays = ParseDeliveryDays(day_dostavka);

                var itemsPayload = (products ?? new List<InvoiceProduct>())
                    .Select(p => new
                    {
                        productId = p.ProductId > 0 ? (int?)p.ProductId : null,
                        productName = p.ProductName,
                        quantity = p.Quantity,
                        price = p.Price,
                        unit = string.IsNullOrWhiteSpace(p.UnitName) ? "шт" : p.UnitName
                    })
                    .ToList();

                var payload = new
                {
                    sellerCompany = sellerCompany,
                    buyerCompanyId = clientCompanyId > 0 ? (int?)clientCompanyId : null,
                    invoiceDate = invoiceDate,
                    address = adress,
                    deliveryDays = deliveryDays,
                    paymentMethod = sposob_oplata,
                    status = statusId,
                    responsible = responsibleId > 0 ? responsibleId.ToString() : null,
                    items = itemsPayload,
                    number = string.IsNullOrWhiteSpace(number_chet) ? null : number_chet.Trim()
                };

                string json = JsonConvert.SerializeObject(payload);
                Console.WriteLine($"📤 Отправляемый JSON:\n{json}");

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _apiClient.PostAsync($"{ApiBaseUrl}/api/invoices", content);
                string responseJson = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"📥 Ответ API:\n{responseJson}");

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"❌ Ошибка HTTP при создании счета: {response.StatusCode}");
                    return 0;
                }

                var result = JsonConvert.DeserializeObject<ApiInvoiceCreateResponse>(responseJson);

                if (result == null || result.Id <= 0)
                {
                    Console.WriteLine("⚠️ Не удалось получить ID созданного счета");
                    return 0;
                }

                Console.WriteLine($"✅ Счет создан. ID: {result.Id}, номер: {result.Number}, продавец: {result.SellerCompany}");

                return result.Id;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Исключение: {ex.Message}");
                return 0;
            }
        }

        private static int? ParseDeliveryDays(string dayDostavka)
        {
            if (string.IsNullOrWhiteSpace(dayDostavka))
                return null;

            if (int.TryParse(dayDostavka.Trim(), out int direct))
                return direct;

            var digits = new string(dayDostavka.Where(char.IsDigit).ToArray());
            if (!string.IsNullOrEmpty(digits) && int.TryParse(digits, out int parsed))
                return parsed;

            return null;
        }

        // ============ МЕТОДЫ ДЛЯ РАБОТЫ С ТОВАРАМИ ============

        /// <summary>
        /// Ищет товар по имени (использует кеш и API)
        /// </summary>
        public async Task<int> GetProductIdByName(string productName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(productName))
                    return 0;

                // 1. Пробуем найти в кэше
                if (BitrixCache.IsCacheReady())
                {
                    int cachedId = BitrixCache.GetProductIdByName(productName);
                    if (cachedId > 0)
                    {
                        Console.WriteLine($"Товар найден в кэше: '{productName}' -> ID: {cachedId}");
                        return cachedId;
                    }
                }
                else
                {
                    // Если кэш не готов, инициализируем его
                    await BitrixCache.InitializeAsync();
                    int cachedId = BitrixCache.GetProductIdByName(productName);
                    if (cachedId > 0)
                        return cachedId;
                }

                // 2. Если не нашли в кэше, ищем через API (/api/products/search)
                Console.WriteLine($"Товар '{productName}' не найден в кэше, ищем через API...");

                var searchName = productName.Trim();
                var searchResults = await SearchProductsAsync(searchName);

                // Ищем точное совпадение
                var product = searchResults.FirstOrDefault(p =>
                    !string.IsNullOrEmpty(p.Name) &&
                    p.Name.Trim().Equals(searchName, StringComparison.OrdinalIgnoreCase));

                if (product != null && product.Id > 0)
                {
                    Console.WriteLine($"Найден товар через API: {product.Name}, ID: {product.Id}");
                    return product.Id;
                }

                // Если точного совпадения нет, ищем частичное
                product = searchResults.FirstOrDefault(p =>
                    !string.IsNullOrEmpty(p.Name) &&
                    searchName.IndexOf(p.Name.Trim(), StringComparison.OrdinalIgnoreCase) >= 0);

                if (product != null && product.Id > 0)
                {
                    Console.WriteLine($"Найден товар через API (частичное совпадение): {product.Name}, ID: {product.Id}");
                    return product.Id;
                }

                Console.WriteLine($"Товар '{productName}' не найден ни в кэше, ни через API");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при поиске товара '{productName}': {ex.Message}");
                return 0;
            }
        }

        private async Task<List<ApiProductSearchDto>> SearchProductsAsync(string query)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                    return new List<ApiProductSearchDto>();

                string url = $"{ApiBaseUrl}/api/products/search?q={Uri.EscapeDataString(query)}";
                var response = await _apiClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[BitrixService] Ошибка HTTP при поиске товаров: {response.StatusCode}");
                    return new List<ApiProductSearchDto>();
                }

                string json = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<List<ApiProductSearchDto>>(json) ?? new List<ApiProductSearchDto>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BitrixService] Ошибка поиска товаров '{query}': {ex.Message}");
                return new List<ApiProductSearchDto>();
            }
        }

        /// <summary>
        /// Создает товар, если он не существует
        /// </summary>
        public async Task<int> CreateProductIfNotExists(string productName, decimal price = 0)
        {
            try
            {
                // Сначала проверяем, существует ли товар
                var existingId = await GetProductIdByName(productName);
                if (existingId > 0)
                {
                    Console.WriteLine($"Товар '{productName}' уже существует (ID: {existingId})");
                    return existingId;
                }

                // ПРОБЕЛ В API: сервер каталога не предоставляет endpoint для создания товара
                // (каталог read-only, импортирован из Bitrix). Возвращаем 0 — вызывающий код
                // (InvoicionCreatePage.PrepareProductsAsync) уже трактует 0 как "товар не создан"
                // и добавляет позицию в notFoundProducts, не прерывая создание счета.
                Console.WriteLine($"⚠️ Товар '{productName}' не найден. Создание нового товара не поддерживается новым API (нет endpoint создания товара).");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при создании товара '{productName}': {ex.Message}");
                return 0;
            }
        }


        public async Task<string> GetCategoryPath(string categoryId)
        {
            try
            {
                if (string.IsNullOrEmpty(categoryId))
                    return "Без категории";

                var categories = await GetСategories(); // Использует кэш

                // Создаем словарь для быстрого поиска
                var categoryDict = categories.ToDictionary(c => c.SelectionId, c => c);

                return BuildCategoryPath(categoryId, categoryDict);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка получения пути категории: {ex.Message}");
                return "Неизвестная категория";
            }
        }

        public void ClearCategoriesCache()
        {
            lock (_categoriesLock)
            {
                _cachedCategories = null;
                _lastCategoriesUpdate = DateTime.MinValue;
                Console.WriteLine("[BitrixService] Кэш категорий очищен");
            }
        }

        public async Task<List<Category>> GetСategories(bool forceRefresh = false)
        {
            try
            {
                // Проверяем кэш
                lock (_categoriesLock)
                {
                    if (!forceRefresh &&
                        _cachedCategories != null &&
                        (DateTime.Now - _lastCategoriesUpdate) < _categoriesCacheDuration)
                    {
                        Console.WriteLine($"[BitrixService] Возвращаем категории из кэша: {_cachedCategories.Count} категорий");
                        return _cachedCategories;
                    }
                }

                Console.WriteLine("[BitrixService] Загрузка категорий из нового API...");

                var response = await _apiClient.GetAsync($"{ApiBaseUrl}/api/categories");

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[BitrixService] Ошибка HTTP при загрузке категорий: {response.StatusCode}");

                    lock (_categoriesLock)
                    {
                        if (_cachedCategories != null)
                            return _cachedCategories;
                    }
                    return new List<Category>();
                }

                string jsonResponse = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[BitrixService] Ответ API (категории): {jsonResponse.Length} символов");

                var raw = JsonConvert.DeserializeObject<List<ApiCategoryDto>>(jsonResponse) ?? new List<ApiCategoryDto>();

                var categories = raw.Select(item => new Category
                {
                    Name = item.Name ?? "Без названия",
                    SelectionId = item.Id ?? "0",
                    ParentId = item.ParentId,
                    Code = null
                }).ToList();

                // Сохраняем в кэш
                lock (_categoriesLock)
                {
                    _cachedCategories = categories;
                    _lastCategoriesUpdate = DateTime.Now;
                }

                Console.WriteLine($"[BitrixService] Загружено {categories.Count} категорий");
                return categories;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BitrixService] Ошибка в GetСategories: {ex.Message}");

                // В случае ошибки возвращаем кэшированные данные, если они есть
                lock (_categoriesLock)
                {
                    if (_cachedCategories != null)
                    {
                        Console.WriteLine($"[BitrixService] Возвращаем старые кэшированные категории: {_cachedCategories.Count}");
                        return _cachedCategories;
                    }
                }

                return new List<Category>();
            }
        }

        // Загружает (и кратковременно кэширует в памяти) полный дамп товаров нового API,
        // чтобы GetProducts()/GetProductsByCategory() не тянули по 50к строк из сети
        // при каждом вызове подряд.
        private async Task<List<ApiProductDto>> GetAllProductsRawAsync(bool forceRefresh = false)
        {
            if (!forceRefresh)
            {
                var cached = _allProductsRawCache;
                if (cached != null && (DateTime.Now - _allProductsRawCacheTime) < _allProductsRawCacheDuration)
                    return cached;
            }

            await _allProductsRawLock.WaitAsync();
            try
            {
                if (!forceRefresh)
                {
                    var cached = _allProductsRawCache;
                    if (cached != null && (DateTime.Now - _allProductsRawCacheTime) < _allProductsRawCacheDuration)
                        return cached;
                }

                Console.WriteLine("[BitrixService] Загрузка полного каталога товаров из нового API...");

                var response = await _apiClient.GetAsync($"{ApiBaseUrl}/api/products/all");
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[BitrixService] Ошибка HTTP при загрузке товаров: {response.StatusCode}");
                    return _allProductsRawCache ?? new List<ApiProductDto>();
                }

                string json = await response.Content.ReadAsStringAsync();
                var products = JsonConvert.DeserializeObject<List<ApiProductDto>>(json) ?? new List<ApiProductDto>();

                Console.WriteLine($"[BitrixService] Загружено {products.Count} товаров из нового API");

                _allProductsRawCache = products;
                _allProductsRawCacheTime = DateTime.Now;

                return products;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BitrixService] Ошибка загрузки товаров: {ex.Message}");
                return _allProductsRawCache ?? new List<ApiProductDto>();
            }
            finally
            {
                _allProductsRawLock.Release();
            }
        }

        private static Product MapToProduct(ApiProductDto p)
        {
            return new Product
            {
                Id = p.Id.ToString(),
                Name = p.Name,
                Code = p.Article,
                SectionId = p.CategoryId,
                Price = p.Price,
                Measure = p.Unit,
                PurchasingPrice = p.PriceBase
            };
        }

        public async Task<List<Product>> GetProducts()
        {
            var raw = await GetAllProductsRawAsync();
            return raw.Select(MapToProduct).ToList();
        }

        public async Task<List<Product>> GetProductsByCategory(int categoryId)
        {
            var raw = await GetAllProductsRawAsync();
            string catStr = categoryId.ToString();
            return raw
                .Where(p => string.Equals(p.CategoryId, catStr, StringComparison.OrdinalIgnoreCase))
                .Select(MapToProduct)
                .ToList();
        }

        /// <summary>
        /// Получает все товары с информацией о категории, цене, количестве и единице измерения
        /// </summary>
        /// <returns>Список товаров с расширенной информацией</returns>


        public async Task<List<Company>> GetAllCompanies()
        {
            try
            {
                var response = await _apiClient.GetAsync($"{ApiBaseUrl}/api/companies");

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[BitrixService] Ошибка HTTP при загрузке компаний: {response.StatusCode}");
                    return new List<Company>();
                }

                string jsonResponse = await response.Content.ReadAsStringAsync();
                var raw = JsonConvert.DeserializeObject<List<ApiCompanyDto>>(jsonResponse) ?? new List<ApiCompanyDto>();

                return raw.Select(c => new Company
                {
                    Id = c.Id,
                    Title = c.Title,
                    // AssignedById/CreatedTime/IsMyCompany не имеют аналога в новом API
                    // (это чисто отображаемые поля Bitrix) — зануляем.
                    AssignedById = 0,
                    CreatedTime = default(DateTime),
                    IsMyCompany = null
                }).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BitrixService] Ошибка получения компаний: {ex.Message}");
                return new List<Company>();
            }
        }

        /// <summary>
        /// Раньше возвращал компании Bitrix с флагом isMyCompany=Y (это и были СПК/НВР).
        /// Теперь "мои компании" (продавцы) — это два статических значения в
        /// InvoicionCreatePage.MyLocalCompanies (СПК/НВР), а не записи в таблице покупателей
        /// нового API (она хранит только компании-покупателей). Метод оставлен для
        /// совместимости вызовов (InvoicionCreatePage использует его только чтобы исключить
        /// "мои компании" из списка покупателей), поэтому безопасно возвращает пустой список.
        /// </summary>
        public async Task<List<Company>> GetMyCompanies()
        {
            return await Task.FromResult(new List<Company>());
        }

        public async Task<int> CreateCompany(string title, string phone = null, string address = null,
                                        string inn = null, string kpp = null, bool registerEvent = true)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException("Название компании обязательно");
            }

            try
            {
                var payload = new
                {
                    title = title.Trim(),
                    inn = string.IsNullOrWhiteSpace(inn) ? null : inn.Trim(),
                    kpp = string.IsNullOrWhiteSpace(kpp) ? null : kpp.Trim(),
                    address = string.IsNullOrWhiteSpace(address) ? null : address.Trim(),
                    phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim()
                };

                string jsonRequest = JsonConvert.SerializeObject(payload);

                Console.WriteLine("=== ДАННЫЕ ДЛЯ СОЗДАНИЯ КОМПАНИИ ===");
                Console.WriteLine(jsonRequest);
                Console.WriteLine("===================================");

                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                var response = await _apiClient.PostAsync($"{ApiBaseUrl}/api/companies", content);
                string jsonResponse = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"Ответ API: {jsonResponse}");

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Ошибка создания компании: {response.StatusCode} {jsonResponse}");
                    return 0;
                }

                var result = JsonConvert.DeserializeObject<ApiIdResponse>(jsonResponse);

                int companyId = result?.Id ?? 0;
                if (companyId > 0)
                {
                    Console.WriteLine($"✅ Компания '{title}' создана с ID: {companyId}");
                }

                return companyId;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Исключение при создании компании: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                return 0;
            }
        }


        public async Task<List<ProductWithCategoryInfo>> GetProductsWithCategoryInfoByCategory(int categoryId)
        {
            try
            {
                // Используем кэшированные категории
                var categories = await GetСategories();
                var categoryDict = categories.ToDictionary(c => c.SelectionId, c => c);

                string categoryName = categoryDict.TryGetValue(categoryId.ToString(), out var category)
                    ? category.Name
                    : "Неизвестная категория";

                // Получаем товары для категории
                var products = await GetProductsByCategory(categoryId);

                var result = new List<ProductWithCategoryInfo>();

                foreach (var product in products)
                {
                    try
                    {
                        var purchasingPrice = product.GetPurchasingPrice();

                        var productInfo = new ProductWithCategoryInfo
                        {
                            CategoryName = categoryName,
                            ProductName = product.Name ?? "Без названия",
                            Price = product.Price.HasValue ? product.Price.Value : 0m,
                            HasPrice = product.Price.HasValue && product.Price.Value > 0,
                            SectionId = product.SectionId,
                            ProductCode = product.Code,
                            ProductId = product.Id,
                            Measure = product.Measure,
                            PurchasingPrice = purchasingPrice,
                            HasPurchasingPrice = purchasingPrice.HasValue && purchasingPrice.Value > 0
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
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в GetProductsWithCategoryInfoByCategory: {ex.Message}");
                return new List<ProductWithCategoryInfo>();
            }
        }


        public async Task<List<ProductWithCategoryInfo>> GetProductsWithCategoryInfo()
        {
            try
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Начало загрузки категорий...");
                var categories = await GetСategories();
                var categoryDict = categories.ToDictionary(c => c.SelectionId, c => c);
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Загружено категорий: {categories.Count}");

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Начало загрузки товаров...");
                var allProducts = await GetProducts();
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Загружено товаров: {allProducts.Count}");

                if (allProducts.Count == 0)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Нет товаров для обработки");
                    return new List<ProductWithCategoryInfo>();
                }

                // 1. ПРЕДВЫЧИСЛЯЕМ ВСЕ ПУТИ КАТЕГОРИЙ ЗАРАНЕЕ
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Предвычисление путей категорий...");
                var categoryPathCache = PrecomputeCategoryPaths(categoryDict);

                // 2. ПАРАЛЛЕЛЬНАЯ АСИНХРОННАЯ ОБРАБОТКА БАТЧАМИ
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Параллельная обработка батчами...");
                var result = await ProcessProductsInBatchesAsync(allProducts, categoryPathCache);

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ===== ОБРАБОТКА ЗАВЕРШЕНА =====");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Всего обработано: {result.Count} товаров");

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] КРИТИЧЕСКАЯ ОШИБКА: {ex.Message}");
                File.AppendAllText("critical_error_log.txt",
                    $"[{DateTime.Now:HH:mm:ss}] {ex.Message}\n{ex.StackTrace}\n\n");
                return new List<ProductWithCategoryInfo>();
            }
        }

        // 1. ПРЕДВЫЧИСЛЕНИЕ ВСЕХ ПУТЕЙ КАТЕГОРИЙ ЗАРАНЕЕ
        private Dictionary<string, string> PrecomputeCategoryPaths(Dictionary<string, Category> categoryDict)
        {
            var cache = new Dictionary<string, string>();

            foreach (var category in categoryDict.Values)
            {
                if (!cache.ContainsKey(category.SelectionId))
                {
                    cache[category.SelectionId] = BuildCategoryPath(category.SelectionId, categoryDict);
                }
            }

            // Добавляем значение по умолчанию для пустых SectionId
            cache[string.Empty] = "Без категории";
            cache["null"] = "Без категории";

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Предвычислено путей категорий: {cache.Count}");
            return cache;
        }

        // 2. ОБРАБОТКА БАТЧАМИ С АСИНХРОННОСТЬЮ
        private async Task<List<ProductWithCategoryInfo>> ProcessProductsInBatchesAsync(
            List<Product> allProducts,
            Dictionary<string, string> categoryPathCache)
        {
            int totalProducts = allProducts.Count;
            int processedCount = 0;
            DateTime startTime = DateTime.Now;

            // Используем ConcurrentBag для потокобезопасности
            var result = new ConcurrentBag<ProductWithCategoryInfo>();

            // Размер батча - оптимально для процессора
            int batchSize = Math.Max(1000, Environment.ProcessorCount * 250);
            int batchCount = (int)Math.Ceiling((double)totalProducts / batchSize);

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Батчей: {batchCount}, размер батча: {batchSize}");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Используется процессоров: {Environment.ProcessorCount}");

            // Обрабатываем батчи параллельно
            var tasks = new List<Task>();

            for (int batchIndex = 0; batchIndex < batchCount; batchIndex++)
            {
                int start = batchIndex * batchSize;
                int end = Math.Min(start + batchSize, totalProducts);
                var batch = allProducts.Skip(start).Take(end - start).ToList();

                // Запускаем обработку батча асинхронно
                tasks.Add(Task.Run(() => ProcessBatch(batch, categoryPathCache, result,
                    ref processedCount, totalProducts, startTime)));
            }

            // Ждем завершения всех батчей
            await Task.WhenAll(tasks);

            return result.ToList();
        }

        // 3. ОБРАБОТКА ОДНОГО БАТЧА
        private void ProcessBatch(
            List<Product> batch,
            Dictionary<string, string> categoryPathCache,
            ConcurrentBag<ProductWithCategoryInfo> result,
            ref int processedCount,
            int totalProducts,
            DateTime startTime)
        {
            foreach (var product in batch)
            {
                try
                {
                    // СУПЕРБЫСТРЫЙ ПОИСК ПУТИ КАТЕГОРИИ (O(1))
                    string categoryPath = categoryPathCache.TryGetValue(
                        product.SectionId ?? string.Empty,
                        out var path) ? path : "Без категории";

                    var purchasingPrice = product.GetPurchasingPrice();

                    var productInfo = new ProductWithCategoryInfo
                    {
                        CategoryName = categoryPath,
                        ProductName = product.Name ?? "Без названия",
                        Price = product.Price.GetValueOrDefault(),
                        HasPrice = product.Price.HasValue && product.Price.Value > 0,
                        SectionId = product.SectionId,
                        ProductCode = product.Code,
                        ProductId = product.Id,
                        Measure = product.Measure,
                        PurchasingPrice = purchasingPrice,
                        HasPurchasingPrice = purchasingPrice.HasValue && purchasingPrice.Value > 0

                    };

                    result.Add(productInfo);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Ошибка товара {product?.Id}: {ex.Message}");
                }

                // ОТСЛЕЖИВАНИЕ ПРОГРЕССА
                int current = Interlocked.Increment(ref processedCount);

                if (current % 1000 == 0 || current == totalProducts)
                {
                    var elapsed = DateTime.Now - startTime;
                    double percentage = (double)current / totalProducts * 100;
                    double speed = current / elapsed.TotalSeconds;

                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Обработано: {current}/{totalProducts} " +
                                     $"({percentage:F1}%) | " +
                                     $"Скорость: {speed:F0} товаров/сек");
                }
            }
        }

        // 4. ОПТИМИЗИРОВАННЫЙ BuildCategoryPath
        private string BuildCategoryPath(string categoryId, Dictionary<string, Category> categoryDict)
        {
            if (string.IsNullOrEmpty(categoryId) || !categoryDict.ContainsKey(categoryId))
                return "Без категории";

            var path = new List<string>();
            var currentId = categoryId;

            // Кэширование родительских путей
            while (currentId != null && categoryDict.TryGetValue(currentId, out var category))
            {
                path.Insert(0, category.Name ?? "Без названия");
                currentId = category.ParentId;

                // Защита от циклов
                if (path.Count > 20) break;
            }

            return path.Count > 0 ? string.Join(" / ", path) : "Без категории";
        }




        public class InvoiceProduct
        {
            public int ProductId { get; set; }
            public string ProductName { get; set; }
            public decimal Quantity { get; set; }
            public decimal Price { get; set; }

            public string UnitName { get; set; }
            public string MeasureId { get; set; }
        }

        public class CompanyRequisites
        {
            public string CompanyName { get; set; }
            public string INN { get; set; }
            public string KPP { get; set; }
            public string Address { get; set; }
            public string ContactPerson { get; set; }
            public string Phone { get; set; }
            public string Email { get; set; }
        }

        // Вспомогательный класс для десериализации ответа от метода add (Bitrix, оставлен для совместимости)
        public class BitrixAddResponse
        {
            [JsonProperty("result")]
            public int Result { get; set; }

            [JsonProperty("error")]
            public string Error { get; set; }

            [JsonProperty("error_description")]
            public string ErrorDescription { get; set; }
        }

        // ============ DTO НОВОГО API КАТАЛОГА (http://83.217.203.29:8090) ============

        private class ApiProductDto
        {
            [JsonProperty("id")]
            public int Id { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("article")]
            public string Article { get; set; }

            [JsonProperty("barcode")]
            public string Barcode { get; set; }

            [JsonProperty("unit")]
            public string Unit { get; set; }

            [JsonProperty("categoryId")]
            public string CategoryId { get; set; }

            [JsonProperty("price")]
            public decimal Price { get; set; }

            [JsonProperty("priceBase")]
            public decimal PriceBase { get; set; }

            [JsonProperty("priceAlt")]
            public decimal PriceAlt { get; set; }

            [JsonProperty("stockQty")]
            public decimal StockQty { get; set; }

            [JsonProperty("taxRate")]
            public decimal TaxRate { get; set; }
        }

        private class ApiProductSearchDto
        {
            [JsonProperty("id")]
            public int Id { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("article")]
            public string Article { get; set; }

            [JsonProperty("barcode")]
            public string Barcode { get; set; }

            [JsonProperty("unit")]
            public string Unit { get; set; }

            [JsonProperty("categoryId")]
            public string CategoryId { get; set; }

            [JsonProperty("price")]
            public decimal Price { get; set; }

            [JsonProperty("stockQty")]
            public decimal StockQty { get; set; }
        }

        private class ApiCategoryDto
        {
            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("parentId")]
            public string ParentId { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }
        }

        private class ApiCompanyDto
        {
            [JsonProperty("id")]
            public int Id { get; set; }

            [JsonProperty("title")]
            public string Title { get; set; }

            [JsonProperty("inn")]
            public string Inn { get; set; }

            [JsonProperty("kpp")]
            public string Kpp { get; set; }

            [JsonProperty("address")]
            public string Address { get; set; }

            [JsonProperty("phone")]
            public string Phone { get; set; }
        }

        private class ApiIdResponse
        {
            [JsonProperty("id")]
            public int Id { get; set; }
        }

        private class ApiInvoiceCreateResponse
        {
            [JsonProperty("id")]
            public int Id { get; set; }

            [JsonProperty("number")]
            public string Number { get; set; }

            [JsonProperty("sellerCompany")]
            public string SellerCompany { get; set; }
        }
    }


    // Вспомогательные классы для десериализации ответа от Bitrix24 (оставлены для совместимости,
    // используются моделями ниже — CreateSmartInvoice/CreateCompany/GetAllCompanies/GetСategories
    // больше не десериализуют в них напрямую, но публичные типы сохранены как есть)
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

    public class BitrixCategoryListResponse
    {
        [JsonProperty("result")]
        public List<CategoryItem> Result { get; set; }

        [JsonProperty("total")]
        public int Total { get; set; }

        [JsonProperty("next")]
        public int? Next { get; set; }
    }

    public class CategoryItem
    {
        [JsonProperty("ID")]
        public string Id { get; set; }

        [JsonProperty("NAME")]
        public string Name { get; set; }

        [JsonProperty("SECTION_ID")]
        public string SectionId { get; set; }

        [JsonProperty("CODE")]
        public string Code { get; set; }

        [JsonProperty("XML_ID")]
        public string XmlId { get; set; }
    }
}
