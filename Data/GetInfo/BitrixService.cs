using ManagerApp.Classes.Setting;
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
        /// <summary>
        /// Получает название раздела для товара из кэша
        /// </summary>
        private async Task<string> GetSectionNameForProductFromCache(int productId)
        {
            try
            {
                var allProducts = await BitrixCache.GetAllProductsWithCategories();
                var productInfo = allProducts?.FirstOrDefault(p => p.ProductId == productId.ToString());

                // Просто возвращаем CategoryName - возможно это уже и есть название раздела
                return productInfo?.CategoryName ?? string.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка получения раздела из кэша: {ex.Message}");
                return string.Empty;
            }
        }
        public async Task<byte[]> DownloadDocumentBytes(string documentUrl)
        {
            try
            {
                // Если это REST API URL, делаем POST запрос
                if (documentUrl.Contains("/rest/"))
                {
                    var response = await _httpClient.PostAsync(documentUrl, null);
                    if (response.IsSuccessStatusCode)
                    {
                        return await response.Content.ReadAsByteArrayAsync();
                    }
                }
                else // Если это прямой URL, делаем GET запрос
                {
                    var response = await _httpClient.GetAsync(documentUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        return await response.Content.ReadAsByteArrayAsync();
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка скачивания документа: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Генерирует и возвращает документ (Word или PDF) для смарт-счета по шаблону
        /// </summary>
        /// <param name="invoiceId">ID счета (Smart Invoice, entityTypeId=31)</param>
        /// <param name="templateId">ID шаблона. По умолчанию = 2 (Счет России)</param>
        /// <param name="format">Формат файла: "docx" или "pdf". По умолчанию "docx".</param>
        /// <returns>URL для скачивания документа или null в случае ошибки</returns>
        public async Task<string> GenerateInvoiceDocument(
            int invoiceId,
            int templateId = 2,
            string format = "docx")
        {
            try
            {
                Console.WriteLine($"=== ГЕНЕРАЦИЯ ДОКУМЕНТА ДЛЯ СЧЕТА {invoiceId} ===");
                Console.WriteLine($"Шаблон: {templateId}, Формат: {format}");

                // Важно: для смарт-счетов используем правильный провайдер данных
                string webhookUrl = "https://crmnvr.ru/rest/241/5gkwkk4657uafc2x/crm.documentgenerator.document.add";

                var requestData = new
                {
                    templateId = templateId,
                    entityTypeId = 31,          // Смарт-счета (Smart Invoice)
                    entityId = invoiceId,       // ID нашего счета
                    values = new { }            // Дополнительные значения (можно оставить пустым)
                };

                string jsonRequest = JsonConvert.SerializeObject(requestData);
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(webhookUrl, content);
                string jsonResponse = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"Ответ при создании документа: {jsonResponse}");

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"❌ Ошибка HTTP при создании документа: {response.StatusCode}");
                    return null;
                }

                dynamic result = JsonConvert.DeserializeObject(jsonResponse);

                if (result?.error != null)
                {
                    Console.WriteLine($"❌ Ошибка API: {result.error}");
                    return null;
                }

                // Проверяем, что документ создан успешно
                if (result?.result?.document?.id == null)
                {
                    Console.WriteLine("⚠️ Не удалось получить ID созданного документа");
                    return null;
                }

                int documentId = result.result.document.id;
                Console.WriteLine($"✅ Документ создан. ID документа: {documentId}");

                // Получаем ссылку для скачивания
                return await GetDocumentDownloadUrl(documentId, format);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Исключение при генерации документа: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Получает прямую ссылку для скачивания созданного документа
        /// </summary>
        private async Task<string> GetDocumentDownloadUrl(int documentId, string format)
        {
            try
            {
                // Получаем информацию о документе
                string infoUrl = "https://crmnvr.ru/rest/241/5gkwkk4657uafc2x/crm.documentgenerator.document.get";
                var infoRequest = new { id = documentId };

                string infoJson = JsonConvert.SerializeObject(infoRequest);
                var infoContent = new StringContent(infoJson, Encoding.UTF8, "application/json");

                var infoResponse = await _httpClient.PostAsync(infoUrl, infoContent);
                string infoJsonResponse = await infoResponse.Content.ReadAsStringAsync();

                dynamic docInfo = JsonConvert.DeserializeObject(infoJsonResponse);

                // В зависимости от формата возвращаем соответствующую ссылку
                if (format.ToLower() == "pdf" && docInfo?.result?.document?.pdfUrl != null)
                {
                    string pdfUrl = docInfo.result.document.pdfUrl.ToString();
                    Console.WriteLine($"✅ Ссылка на PDF: {pdfUrl}");
                    return pdfUrl;
                }
                else if (docInfo?.result?.document?.fileUrl != null)
                {
                    string docxUrl = docInfo.result.document.fileUrl.ToString();
                    Console.WriteLine($"✅ Ссылка на DOCX: {docxUrl}");
                    return docxUrl;
                }

                // Если не нашли ссылки в ответе, пробуем получить через download
                string downloadUrl = "https://crmnvr.ru/rest/241/5gkwkk4657uafc2x/crm.documentgenerator.document.download";
                var downloadRequest = new { id = documentId };

                string downloadJson = JsonConvert.SerializeObject(downloadRequest);
                var downloadContent = new StringContent(downloadJson, Encoding.UTF8, "application/json");

                var downloadResponse = await _httpClient.PostAsync(downloadUrl, downloadContent);

                if (downloadResponse.IsSuccessStatusCode)
                {
                    // Для простоты возвращаем URL для запроса скачивания
                    return downloadUrl + $"?id={documentId}";
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка получения ссылки: {ex.Message}");
                return null;
            }
        }
        public async Task<int> CreateSmartInvoice(
            string webhooc,
      int clientCompanyId,
      int myCompanyId,
      string orderTopic,
      List<InvoiceProduct> products,
      DateTime? payBeforeDate = null,
      int responsibleId = 1,
      string statusId = "DT31_1:NEW")
        {
            try
            {
                Console.WriteLine("=== СОЗДАНИЕ НОВОГО СМАРТ-СЧЕТА (entityTypeId = 31) ===");

                string webhookUrl = $"{webhooc}crm.item.add";

                var invoiceRequestData = new
                {
                    entityTypeId = 31, // Ключевое изменение: ID для новых счетов
                    fields = new
                    {
                        TITLE = orderTopic,
                        UF_COMPANY_ID = clientCompanyId,
                        UF_MYCOMPANY_ID = myCompanyId,
                        ASSIGNED_BY_ID = responsibleId,
                        STATUS_ID = statusId
                    }
                };

                string invoiceJson = JsonConvert.SerializeObject(invoiceRequestData);
                var invoiceContent = new StringContent(invoiceJson, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(webhookUrl, invoiceContent);
                string jsonResponse = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"❌ Ошибка HTTP при создании счета: {response.StatusCode}");
                    Console.WriteLine($"Тело ответа: {jsonResponse}");
                    return 0;
                }

                dynamic result = JsonConvert.DeserializeObject(jsonResponse);

                if (result?.error != null)
                {
                    Console.WriteLine($"❌ Ошибка API (создание счета): {result.error}");
                    return 0;
                }

                int invoiceId = result?.result?.item?.id != null ? (int)result.result.item.id : 0;

                if (invoiceId <= 0)
                {
                    Console.WriteLine("⚠️ Не удалось получить ID созданного счета.");
                    return 0;
                }

                Console.WriteLine($"✅ Смарт-счет создан. ID: {invoiceId}");

                // Добавляем товары в созданный счет
                bool productsAdded = await AddProductsToSmartInvoice(invoiceId, products);

                if (productsAdded)
                {
                    Console.WriteLine($"✅ Счет #{invoiceId} полностью готов с товарами.");
                }
                else
                {
                    Console.WriteLine($"⚠️ Счет #{invoiceId} создан, но добавление товаров завершилось с ошибками.");
                }

                return invoiceId;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Исключение в CreateSmartInvoice: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Старый метод (теперь вызывает новый SmartInvoice)
        /// </summary>
        public async Task<int> CreateInvoiceWithProducts(
       int clientCompanyId,
       int myCompanyId,
       string orderTopic,
       List<InvoiceProduct> products)
        {
            // Получаем вебхук из настроек
            string webhook = WebhookManager.Webhook;

            // Просто вызываем новый метод с вебхуком из настроек
            return await CreateSmartInvoice(webhook, clientCompanyId, myCompanyId, orderTopic, products);
        }
        /// <summary>
        /// Добавляет товары в смарт-счет
        /// </summary>
        private async Task<bool> AddProductsToSmartInvoice(int invoiceId, List<InvoiceProduct> products)
        {
            Console.WriteLine($"=== ДОБАВЛЕНИЕ ТОВАРОВ В СЧЕТ #{invoiceId} ===");

            if (products == null || products.Count == 0)
            {
                Console.WriteLine("Список товаров пуст.");
                return true;
            }

            string webhookUrl = "https://crmnvr.ru/rest/241/5gkwkk4657uafc2x/crm.item.productrow.add";
            int successCount = 0;

            foreach (var product in products)
            {
                try
                {
                    // ПОЛУЧАЕМ КАТЕГОРИЮ ТОВАРА
                    string categoryName = await GetSectionNameForProductFromCache(product.ProductId);

                    var productRowRequestData = new
                    {
                        fields = new
                        {
                            ownerId = invoiceId,
                            ownerType = "SI",
                            productId = product.ProductId > 0 ? (int?)product.ProductId : null,
                            // ДОБАВЛЯЕМ РАЗДЕЛ В НАЗВАНИЕ ТОВАРА
                            productName = $"{categoryName} | {product.ProductName}",
                            price = product.Price,
                            quantity = product.Quantity,
                            taxRate = 20.0,
                            taxIncluded = "N",
                            measureCode = 796,
                            measureName = "шт."
                        }
                    };
                    string productJson = JsonConvert.SerializeObject(productRowRequestData);
                    var productContent = new StringContent(productJson, Encoding.UTF8, "application/json");

                    var response = await _httpClient.PostAsync(webhookUrl, productContent);
                    string jsonResponse = await response.Content.ReadAsStringAsync();

                    Console.WriteLine($"Ответ при добавлении товара '{product.ProductName}': {jsonResponse}");

                    if (response.IsSuccessStatusCode)
                    {
                        dynamic result = JsonConvert.DeserializeObject(jsonResponse);
                        if (result?.result?.productRow?.id != null)
                        {
                            Console.WriteLine($"  ✅ Товар '{product.ProductName}' добавлен. Категория: {categoryName}");
                            successCount++;
                        }
                        else
                        {
                            Console.WriteLine($"  ⚠️ Для '{product.ProductName}' не получен ID товарной позиции.");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"  ❌ Ошибка при добавлении '{product.ProductName}': {response.StatusCode}");
                        Console.WriteLine($"  Тело ошибки: {jsonResponse}");
                    }

                    await Task.Delay(200);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ❌ Исключение для товара '{product.ProductName}': {ex.Message}");
                }
            }

            Console.WriteLine($"=== ИТОГО: Успешно добавлено {successCount} из {products.Count} товаров. ===");
            return successCount > 0;
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

                // 2. Если не нашли в кэше, ищем через API
                Console.WriteLine($"Товар '{productName}' не найден в кэше, ищем через API...");

                var allProducts = await GetProducts();
                var searchName = productName.Trim();

                // Ищем точное совпадение
                var product = allProducts.FirstOrDefault(p =>
                    !string.IsNullOrEmpty(p.Name) &&
                    p.Name.Trim().Equals(searchName, StringComparison.OrdinalIgnoreCase));

                if (product != null && Convert.ToInt32(product.Id) > 0)
                {
                    Console.WriteLine($"Найден товар через API: {product.Name}, ID: {product.Id}");
                    await BitrixCache.RefreshCacheAsync();
                    return Convert.ToInt32(product.Id);
                }

                // Если точного совпадения нет, ищем частичное
                product = allProducts.FirstOrDefault(p =>
                    !string.IsNullOrEmpty(p.Name) &&
                    searchName.IndexOf(p.Name.Trim(), StringComparison.OrdinalIgnoreCase) >= 0);

                if (product != null && Convert.ToInt32(product.Id) > 0)
                {
                    Console.WriteLine($"Найден товар через API (частичное совпадение): {product.Name}, ID: {product.Id}");
                    await BitrixCache.RefreshCacheAsync();
                    return Convert.ToInt32(product.Id);
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



        public async Task<Dictionary<string, int>> GetProductIdsByNames(List<string> productNames)
        {
            var result = new Dictionary<string, int>();

            try
            {
                var allProducts = await GetProducts();

                foreach (var productName in productNames)
                {
                    var searchName = productName.Trim();
                    int productId = 0;

                    // Точное совпадение (без учета регистра)
                    var product = allProducts.FirstOrDefault(p =>
                        !string.IsNullOrEmpty(p.Name) &&
                        p.Name.Trim().Equals(searchName, StringComparison.OrdinalIgnoreCase));

                    if (product != null)
                    {
                        productId = Convert.ToInt32(product.Id);
                    }
                    else
                    {
                        // Частичное совпадение (без учета регистра)
                        product = allProducts.FirstOrDefault(p =>
                            !string.IsNullOrEmpty(p.Name) &&
                            searchName.IndexOf(p.Name.Trim(), StringComparison.OrdinalIgnoreCase) >= 0);

                        if (product != null)
                        {
                            productId = Convert.ToInt32(product.Id);
                        }
                    }

                    result[productName] = productId;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при пакетном поиске товаров: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Улучшенный метод поиска товаров с несколькими стратегиями
        /// </summary>
        public async Task<(int productId, string matchedName)> FindProduct(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return (0, null);

            var allProducts = await GetProducts();
            var normalizedSearch = searchTerm.Trim();

            // 1. Точное совпадение (полное)
            var product = allProducts.FirstOrDefault(p =>
                !string.IsNullOrEmpty(p.Name) &&
                p.Name.Trim().Equals(normalizedSearch, StringComparison.OrdinalIgnoreCase));

            if (product != null)
                return (Convert.ToInt32(product.Id), product.Name);

            // 2. Точное совпадение (без лишних пробелов и символов)
            product = allProducts.FirstOrDefault(p =>
                !string.IsNullOrEmpty(p.Name) &&
                NormalizeString(p.Name).Equals(NormalizeString(normalizedSearch), StringComparison.OrdinalIgnoreCase));

            if (product != null)
                return (Convert.ToInt32(product.Id), product.Name);

            // 3. Частичное совпадение (содержит)
            product = allProducts.FirstOrDefault(p =>
                !string.IsNullOrEmpty(p.Name) &&
                normalizedSearch.IndexOf(p.Name.Trim(), StringComparison.OrdinalIgnoreCase) >= 0);

            if (product != null)
                return (Convert.ToInt32(product.Id), product.Name);

            // 4. Частичное совпадение (содержится в)
            product = allProducts.FirstOrDefault(p =>
                !string.IsNullOrEmpty(p.Name) &&
                p.Name.Trim().IndexOf(normalizedSearch, StringComparison.OrdinalIgnoreCase) >= 0);

            if (product != null)
                return (Convert.ToInt32(product.Id), product.Name);

            return (0, null);
        }

        /// <summary>
        /// Нормализует строку для сравнения (убирает лишние пробелы, символы)
        /// </summary>
        private string NormalizeString(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            return input.Trim()
                        .Replace("  ", " ")
                        .Replace("\t", " ")
                        .Replace("\n", " ")
                        .Replace("\r", " ");
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

                string webhookUrl = "https://crmnvr.ru/rest/241/5gkwkk4657uafc2x/crm.product.add";

                var requestData = new
                {
                    fields = new
                    {
                        NAME = productName.Trim(),
                        PRICE = price,
                        CURRENCY_ID = "RUB"
                    }
                };

                Console.WriteLine($"Создаем товар: {productName}");
                string jsonRequest = JsonConvert.SerializeObject(requestData);
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(webhookUrl, content);
                string jsonResponse = await response.Content.ReadAsStringAsync();

                var result = JsonConvert.DeserializeObject<BitrixAddResponse>(jsonResponse);

                if (result?.Result > 0)
                {
                    Console.WriteLine($"✅ Товар '{productName}' создан с ID: {result.Result}");
                    return result.Result;
                }
                else
                {
                    Console.WriteLine($"❌ Не удалось создать товар '{productName}': {result?.Error}");
                    return 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при создании товара '{productName}': {ex.Message}");
                return 0;
            }
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
        /// Создает счет с товарами через правильный API
        /// </summary>
      

        /// <summary>
        /// Получает реквизиты компании
        /// </summary>
        public async Task<CompanyRequisites> GetCompanyRequisites(int companyId)
        {
            string webhookUrl = "https://crmnvr.ru/rest/241/5gkwkk4657uafc2x/crm.requisite.list";

            var requestData = new
            {
                filter = new
                {
                    ENTITY_TYPE_ID = 4, // 4 - компания
                    ENTITY_ID = companyId
                },
                order = new
                {
                    DATE_CREATE = "DESC"
                },
                select = new[] { "ID", "NAME", "RQ_COMPANY_NAME", "RQ_INN", "RQ_KPP", "RQ_ADDR" }
            };

            try
            {
                string jsonRequest = JsonConvert.SerializeObject(requestData);
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(webhookUrl, content);
                string jsonResponse = await response.Content.ReadAsStringAsync();

                dynamic result = JsonConvert.DeserializeObject(jsonResponse);

                // TODO: Парсинг реквизитов
                return new CompanyRequisites
                {
                    CompanyName = result?.result?[0]?.RQ_COMPANY_NAME,
                    INN = result?.result?[0]?.RQ_INN,
                    KPP = result?.result?[0]?.RQ_KPP,
                    Address = result?.result?[0]?.RQ_ADDR
                };
            }
            catch
            {
                return null;
            }
        }





        public class InvoiceProduct
        {
            public int ProductId { get; set; }
            public string ProductName { get; set; }
            public decimal Quantity { get; set; }
            public decimal Price { get; set; }
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



