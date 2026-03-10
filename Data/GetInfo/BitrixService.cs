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


        public async Task<int> CreateProductAsync(string productName)
        {
            try
            {
                string url = "https://crmnvr.ru/rest/241/5gkwkk4657uafc2x/catalog.product.add";

                var productData = new
                {
                    fields = new
                    {
                        iblockId = 14,
                        name = productName,
                        iblockSectionId = 697,
                        active = "Y"
                    }
                };

                string jsonData = System.Text.Json.JsonSerializer.Serialize(productData);

                using (var client = new HttpClient())
                {
                    var content = new StringContent(jsonData, Encoding.UTF8, "application/json");

                    var response = await client.PostAsync(url, content);

                    if (!response.IsSuccessStatusCode)
                    {
                        string error = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"❌ Ошибка HTTP: {response.StatusCode}, {error}");
                        return 0;
                    }

                    string result = await response.Content.ReadAsStringAsync();

                    // Используем стандартный using вместо using declaration
                    using (var doc = System.Text.Json.JsonDocument.Parse(result))
                    {
                        var root = doc.RootElement;

                        if (root.TryGetProperty("result", out var resultElement) &&
                            resultElement.TryGetProperty("element", out var element) &&
                            element.TryGetProperty("id", out var idElement))
                        {
                            int productId = idElement.GetInt32();
                            Console.WriteLine($"✅ Товар '{productName}' успешно создан");
                            Console.WriteLine($"   ID: {productId}");
                            return productId;
                        }

                        Console.WriteLine($"❌ Не удалось получить ID товара из ответа: {result}");
                        return 0;
                    }
                }
            }
            catch (System.Text.Json.JsonException jsonEx)
            {
                Console.WriteLine($"❌ Ошибка парсинга JSON: {jsonEx.Message}");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Общая ошибка: {ex.Message}");
                return 0;
            }
        }
        //GetSectionNameForProductFromCache
        private async Task<string> GetSectionNameForProductFromCache(int productId)
        {
            try
            {
                Console.WriteLine($"🔍 Получаем информацию о товаре ID: {productId} напрямую...");

                // Получаем товар напрямую через crm.product.get
                string url = $"https://crmnvr.ru/rest/241/5gkwkk4657uafc2x/crm.product.get?id={productId}";

                using (var client = new HttpClient())
                {
                    var response = await client.GetAsync(url);

                    if (!response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"   ❌ Ошибка HTTP: {response.StatusCode}");
                        return string.Empty;
                    }

                    string result = await response.Content.ReadAsStringAsync();

                    if (string.IsNullOrEmpty(result) || result.Contains("\"error\""))
                    {
                        Console.WriteLine($"   ❌ Ошибка в ответе API");
                        return string.Empty;
                    }

                    // Парсим JSON для получения SECTION_ID
                    try
                    {
                        dynamic productData = Newtonsoft.Json.JsonConvert.DeserializeObject(result);
                        string sectionId = productData?.result?.SECTION_ID;

                        if (!string.IsNullOrEmpty(sectionId))
                        {
                            Console.WriteLine($"   ✅ Нашли SECTION_ID: {sectionId}");

                            // Получаем название раздела
                            string sectionName = await GetSectionNameById(sectionId);
                            Console.WriteLine($"   📁 Раздел: {sectionName}");
                            return sectionName;
                        }
                        else
                        {
                            Console.WriteLine($"   ⚠️ SECTION_ID не найден в ответе");
                            return string.Empty;
                        }
                    }
                    catch (Exception jsonEx)
                    {
                        Console.WriteLine($"   ❌ Ошибка парсинга JSON: {jsonEx.Message}");
                        return string.Empty;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка в GetSectionNameForProductDirect для ID {productId}: {ex.Message}");
                return string.Empty;
            }
        }

        private async Task<string> GetSectionNameById(string sectionId)
        {
            try
            {
                if (string.IsNullOrEmpty(sectionId) || sectionId == "0")
                    return string.Empty;

                Console.WriteLine($"   🔍 Получаем название раздела ID: {sectionId}");

                // Получаем информацию о разделе
                string url = $"https://crmnvr.ru/rest/241/5gkwkk4657uafc2x/crm.productsection.get?id={sectionId}";

                using (var client = new HttpClient())
                {
                    var response = await client.GetAsync(url);

                    if (!response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"      ❌ Ошибка HTTP при запросе раздела: {response.StatusCode}");
                        return string.Empty;
                    }

                    string result = await response.Content.ReadAsStringAsync();

                    try
                    {
                        dynamic sectionData = Newtonsoft.Json.JsonConvert.DeserializeObject(result);
                        string sectionName = sectionData?.result?.NAME;

                        if (!string.IsNullOrEmpty(sectionName))
                        {
                            Console.WriteLine($"      ✅ Название раздела: {sectionName}");
                            return sectionName;
                        }
                    }
                    catch { }

                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ Ошибка получения раздела: {ex.Message}");
                return string.Empty;
            }
        }
        // В классе BitrixService добавьте:
        public async Task<List<BitrixMeasure>> GetMeasuresAsync()
        {
            try
            {
                Console.WriteLine("[BitrixService] Загрузка единиц измерения из Bitrix...");

                using (var httpClient = new HttpClient())
                {
                    httpClient.Timeout = TimeSpan.FromSeconds(30);

                    string apiUrl = "https://crmnvr.ru/rest/241/5gkwkk4657uafc2x/crm.measure.list.json";
                    Console.WriteLine($"[BitrixService] Запрос: {apiUrl}");

                    var response = await httpClient.GetAsync(apiUrl);

                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"[BitrixService] Получен ответ, длина: {json.Length} символов");

                        // Десериализуем JSON
                        var result = JsonConvert.DeserializeObject<BitrixMeasureResponse>(json);

                        if (result?.Result != null)
                        {
                            Console.WriteLine($"[BitrixService] Загружено {result.Result.Count} единиц измерения");
                            return result.Result;
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[BitrixService] HTTP ошибка: {response.StatusCode}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BitrixService] Ошибка получения единиц измерения: {ex.Message}");
            }

            // Возвращаем пустой список при ошибке
            return new List<BitrixMeasure>();
        }

        // Класс для десериализации ответа от Bitrix



        public class BitrixMeasureResponse
        {
            public List<BitrixMeasure> Result { get; set; }
        }        public async Task<byte[]> DownloadDocumentBytes(string documentUrl)
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
        /// <summary>
        /// Генерирует и возвращает документ (Word или PDF) для смарт-счета по шаблону
        /// </summary>
        /// <param name="invoiceId">ID счета (Smart Invoice, entityTypeId=31)</param>
        /// <param name="templateId">ID шаблона: 32 для СПК, 34 для НВР</param>
        /// <param name="format">Формат файла: "docx" или "pdf". По умолчанию "docx".</param>
        /// <returns>URL для скачивания документа или null в случае ошибки</returns>
        public async Task<string> GenerateInvoiceDocument(
            int invoiceId,
            int templateId, // Убираем значение по умолчанию, теперь обязательный параметр
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
        public  async Task<JObject> CallMethodAsync(string method, object parameters)
        {
            using (var client = new HttpClient())
            {
                var jsonContent = JsonConvert.SerializeObject(parameters);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(
                    $"https://crmnvr.ru/rest/241/5gkwkk4657uafc2x/{method}",
                    content);

                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                return JObject.Parse(json);
            }
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
                Console.WriteLine("=== СОЗДАНИЕ НОВОГО СМАРТ-СЧЕТА (entityTypeId = 31) ===");
                Console.WriteLine($"📅 Полученная дата: {invoiceDate:dd.MM.yyyy}");

                string webhookUrl = "https://crmnvr.ru/rest/241/5gkwkk4657uafc2x/crm.item.add";


                // Группируем товары по ProductId для подсчета уникальных наименований
                var groupedProducts = products
                    .GroupBy(p => new { p.ProductId, p.ProductName })
                    .Select(g => g.First())
                    .ToList();

                // Количество уникальных наименований после группировки
                int uniqueProductNamesCount = groupedProducts.Count;

                Console.WriteLine($"📊 Всего позиций в списке (с учетом количества): {products.Count}");
                Console.WriteLine($"📊 Уникальных наименований товаров: {uniqueProductNamesCount}");

                // Выводим уникальные товары для проверки
                foreach (var product in groupedProducts)
                {
                    Console.WriteLine($"   - {product.ProductName} (ID: {product.ProductId})");
                }

                var invoiceRequestData = new
                {
                    entityTypeId = 31,
                    fields = new
                    {
                        // ВАЖНО: используем invoiceDate, а не DateTime.Now!
                        TITLE = $"Счет на оплату № от {invoiceDate:dd.MM.yyyy}",

                        // ОЧЕНЬ ВАЖНО: добавляем поле для генератора документов!
                        DOCUMENT_CREATE_TIME = invoiceDate.ToString("yyyy-MM-ddTHH:mm:ss"),

                        // Обязательные поля
                        ACCOUNT_NUMBER = number_chet,
                        COMPANY_ID = clientCompanyId,
                        MYCOMPANY_ID = myCompanyId,
                        ASSIGNED_BY_ID = responsibleId,
                        STAGE_ID = statusId,

                        // ДАТЫ - используем invoiceDate
                        BEGINDATE = invoiceDate.ToString("yyyy-MM-ddTHH:mm:ss"),
                        CLOSEDATE = CalculateBusinessDays(invoiceDate, 3).ToString("yyyy-MM-ddTHH:mm:ss"),

                        // ПОЛЯ ДЛЯ ГЕНЕРАТОРА ДОКУМЕНТОВ
                        XML_ID = adress ?? string.Empty,
                        COMMENTS = day_dostavka ?? string.Empty,
                        SOURCE_DESCRIPTION = sposob_oplata ?? string.Empty,

                        // Пробуем оба поля - одно из них должно сработать
                        COMPANY_LEAD_TOTAL_ROWS = uniqueProductNamesCount,
                        COMPANY_LEAD_TOTAL_QUANTITY = uniqueProductNamesCount,

                        // Дополнительные полезные поля
                        CATEGORY_ID = 3,
                        UTM_SOURCE = "ManagerApp",
                        OPPORTUNITY = products.Sum(p => p.Price * p.Quantity)
                    }
                };

                string invoiceJson = JsonConvert.SerializeObject(invoiceRequestData);
                Console.WriteLine($"📤 Отправляемый JSON:\n{invoiceJson}");

                var invoiceContent = new StringContent(invoiceJson, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(webhookUrl, invoiceContent);
                string jsonResponse = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"📥 Ответ от Bitrix:\n{jsonResponse}");

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"❌ Ошибка HTTP: {response.StatusCode}");
                    return 0;
                }

                dynamic result = JsonConvert.DeserializeObject(jsonResponse);

                if (result?.error != null)
                {
                    Console.WriteLine($"❌ Ошибка API: {result.error}");
                    return 0;
                }

                int invoiceId = result?.result?.item?.id != null ? (int)result.result.item.id : 0;

                if (invoiceId <= 0)
                {
                    Console.WriteLine("⚠️ Не удалось получить ID счета");
                    return 0;
                }

                Console.WriteLine($"✅ Счет создан. ID: {invoiceId}");

                // Добавляем товары
                bool productsAdded = await AddProductsToSmartInvoice(invoiceId, products);

                if (productsAdded)
                {
                    Console.WriteLine($"✅ Товары добавлены в счет #{invoiceId}");
                }

                // Проверяем, какие поля реально сохранились
                await CheckActualInvoiceFields(invoiceId);

                return invoiceId;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Исключение: {ex.Message}");
                return 0;
            }
        }

        // Добавьте этот метод для проверки полей
        private async Task CheckActualInvoiceFields(int invoiceId)
        {
            try
            {
                Console.WriteLine($"\n🔍 ПРОВЕРКА ПОЛЕЙ СЧЕТА #{invoiceId}");

                string webhookUrl = "https://crmnvr.ru/rest/241/5gkwkk4657uafc2x/crm.item.get";
                var requestData = new
                {
                    entityTypeId = 31,
                    id = invoiceId
                };

                string jsonRequest = JsonConvert.SerializeObject(requestData);
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(webhookUrl, content);
                string jsonResponse = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"📊 Данные счета из Bitrix:\n{jsonResponse}");

                // Парсим ответ для проверки конкретных полей
                dynamic result = JsonConvert.DeserializeObject(jsonResponse);

                if (result?.result?.item != null)
                {
                    var item = result.result.item;
                    Console.WriteLine($"\n📋 СОХРАНЕННЫЕ ПОЛЯ:");
                    Console.WriteLine($"COMPANY_LEAD_TOTAL_ROWS: {item.companyLeadTotalRows}");
                    Console.WriteLine($"COMPANY_LEAD_TOTAL_QUANTITY: {item.companyLeadTotalQuantity}");
                    Console.WriteLine($"PRODUCTS_PRODUCT_QUANTITY: {item.productsProductQuantity}");
                    Console.WriteLine($"XML_ID: {item.xmlId}");
                    Console.WriteLine($"COMMENTS: {item.comments}");
                    Console.WriteLine($"SOURCE_DESCRIPTION: {item.sourceDescription}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка проверки полей: {ex.Message}");
            }
        }

        private async Task<bool> AddProductsToSmartInvoice(int invoiceId, List<InvoiceProduct> products)
        {
            Console.WriteLine($"=== ДОБАВЛЕНИЕ ТОВАРОВ В СЧЕТ #{invoiceId} ===");

            if (products == null || products.Count == 0)
            {
                Console.WriteLine("Список товаров пуст.");
                return true;
            }

            // ПОЛУЧАЕМ СПИСОК ЕДИНИЦ ИЗМЕРЕНИЯ ИЗ BITRIX24
            var measures = await GetMeasuresAsync();
            Console.WriteLine($"Загружено единиц измерения из Bitrix24: {measures?.Count ?? 0}");

            if (measures == null || measures.Count == 0)
            {
                Console.WriteLine("⚠️ Не удалось загрузить единицы измерения");
                measures = new List<BitrixMeasure>();
            }

            // СОЗДАЕМ СЛОВАРЬ ДЛЯ ПОИСКА ПО ID
            var measureDictById = new Dictionary<string, BitrixMeasure>();

            foreach (var measure in measures)
            {
                if (!string.IsNullOrEmpty(measure.ID))
                {
                    var id = measure.ID.Trim();
                    if (!string.IsNullOrEmpty(id))
                    {
                        measureDictById[id] = measure;
                        Console.WriteLine($"Доступна единица: ID={measure.ID}, CODE={measure.CODE}, SYMBOL={measure.SYMBOL_RUS}, TITLE={measure.MEASURE_TITLE}");
                    }
                }
            }

            string webhookUrl = "https://crmnvr.ru/rest/241/5gkwkk4657uafc2x/crm.item.productrow.add";
            int successCount = 0;
            int errorCount = 0;
            int duplicateCount = 0;

            foreach (var product in products)
            {
                try
                {
                    // Получаем категорию товара
                    string categoryName = await GetSectionNameForProductFromCache(product.ProductId);

                    // Очищаем название товара от категории в скобках
                    string cleanProductName = CleanProductNameSkobka(product.ProductName);
                    Console.WriteLine($"\nТовар: '{cleanProductName}' (ID: {product.ProductId})");

                    // ОПРЕДЕЛЯЕМ КОД ЕДИНИЦЫ ИЗМЕРЕНИЯ
                    int measureCode = 796; // по умолчанию - Штука
                    string measureSymbol = "шт"; // по умолчанию
                    string measureTitle = "Штука"; // по умолчанию

                    if (!string.IsNullOrEmpty(product.MeasureId))
                    {
                        var measureId = product.MeasureId.Trim();
                        Console.WriteLine($"  measureId из продукта: '{measureId}'");

                        if (measureDictById.TryGetValue(measureId, out BitrixMeasure foundMeasure))
                        {
                            if (foundMeasure.CODE != null)
                            {
                                string codeString = foundMeasure.CODE.ToString();
                                if (int.TryParse(codeString, out int parsedCode2))
                                {
                                    measureCode = parsedCode2;
                                }
                            }

                            measureSymbol = !string.IsNullOrEmpty(foundMeasure.SYMBOL_RUS)
                                ? foundMeasure.SYMBOL_RUS
                                : "шт";

                            measureTitle = !string.IsNullOrEmpty(foundMeasure.MEASURE_TITLE)
                                ? foundMeasure.MEASURE_TITLE
                                : "Штука";

                            Console.WriteLine($"  Найдена единица измерения: ID={foundMeasure.ID}, CODE={foundMeasure.CODE}");
                            Console.WriteLine($"  Используем: measureCode={measureCode}, symbol='{measureSymbol}', title='{measureTitle}'");
                        }
                        else
                        {
                            Console.WriteLine($"  ⚠️ Единица измерения с ID='{measureId}' не найдена, используем значения по умолчанию");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"  ⚠️ MeasureId не указан, используем по умолчанию: код 796 (Штука)");
                    }

                    // ПЕРВАЯ ПОПЫТКА - добавляем с оригинальным productId
                    bool added = await TryAddProductToInvoice(
                        invoiceId,
                        product.ProductId,
                        cleanProductName,
                        product.Price,
                        product.Quantity,
                        measureCode,
                        measureSymbol);

                    if (added)
                    {
                        successCount++;
                        await Task.Delay(200);
                        continue;
                    }

                    // ЕСЛИ НЕ ПОЛУЧИЛОСЬ - создаем дубликат в папке "Прочее"
                    Console.WriteLine($"  ⚠️ Не удалось добавить товар ID {product.ProductId}. Создаем дубликат в папке 'Прочее'...");

                    int newProductId = await CreateProductInOtherFolder(
                        cleanProductName,
                        product.Price,
                        measureCode);

                    if (newProductId > 0)
                    {
                        Console.WriteLine($"  ✅ Создан дубликат товара с ID: {newProductId}");

                        // ВТОРАЯ ПОПЫТКА - добавляем с новым productId
                        bool addedDuplicate = await TryAddProductToInvoice(
                            invoiceId,
                            newProductId,
                            cleanProductName,
                            product.Price,
                            product.Quantity,
                            measureCode,
                            measureSymbol);

                        if (addedDuplicate)
                        {
                            duplicateCount++;
                            successCount++;
                            Console.WriteLine($"  ✅ Товар успешно добавлен в счет через дубликат!");
                        }
                        else
                        {
                            errorCount++;
                            Console.WriteLine($"  ❌ Не удалось добавить даже дубликат товара");
                        }
                    }
                    else
                    {
                        errorCount++;
                        Console.WriteLine($"  ❌ Не удалось создать дубликат товара");
                    }

                    await Task.Delay(200);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ❌ Исключение: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        Console.WriteLine($"  Внутреннее исключение: {ex.InnerException.Message}");
                    }
                    errorCount++;
                }
            }

            Console.WriteLine($"\n=== ИТОГО: Успешно добавлено {successCount} из {products.Count} товаров. ===");
            Console.WriteLine($"   - Обычных: {successCount - duplicateCount}");
            Console.WriteLine($"   - Дубликатов: {duplicateCount}");
            Console.WriteLine($"   - Ошибок: {errorCount}");

            return successCount > 0;
        }

        /// <summary>
        /// Пытается добавить товар в счет
        /// </summary>
        private async Task<bool> TryAddProductToInvoice(int invoiceId, int productId, string productName,
            decimal price, decimal quantity, int measureCode, string measureSymbol)
        {
            try
            {
                string webhookUrl = "https://crmnvr.ru/rest/241/5gkwkk4657uafc2x/crm.item.productrow.add";

                var productRowRequestData = new
                {
                    fields = new
                    {
                        ownerId = invoiceId,
                        ownerType = "SI",
                        productId = productId,
                        productName = productName,
                        price = price,
                        quantity = quantity,
                        measureCode = measureCode,
                        measureName = measureSymbol,
                        taxRate = 20.0,
                        taxIncluded = "N"
                    }
                };

                string productJson = JsonConvert.SerializeObject(productRowRequestData);
                Console.WriteLine($"  Отправляем запрос с productId={productId}: measureCode={measureCode}, measureName='{measureSymbol}'");

                var productContent = new StringContent(productJson, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(webhookUrl, productContent);
                string jsonResponse = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"  Ответ Bitrix: {jsonResponse}");

                if (response.IsSuccessStatusCode)
                {
                    try
                    {
                        dynamic result = JsonConvert.DeserializeObject(jsonResponse);
                        if (result?.result?.productRow?.id != null)
                        {
                            var savedMeasureCode = result?.result?.productRow?.measureCode?.ToString();
                            var savedMeasureName = result?.result?.productRow?.measureName?.ToString();

                            Console.WriteLine($"  ✅ Товар добавлен. ID позиции: {result?.result?.productRow?.id}");
                            Console.WriteLine($"     Сохранено в Bitrix: код={savedMeasureCode}, название='{savedMeasureName}'");
                            return true;
                        }
                        else
                        {
                            Console.WriteLine($"  ⚠️ Товар добавлен, но не получен ID позиции");
                            return true;
                        }
                    }
                    catch (Exception jsonEx)
                    {
                        Console.WriteLine($"  ⚠️ Ошибка парсинга ответа: {jsonEx.Message}");
                        return true;
                    }
                }
                else
                {
                    Console.WriteLine($"  ❌ Ошибка HTTP: {response.StatusCode}");
                    Console.WriteLine($"  Тело ошибки: {jsonResponse}");

                    // Проверяем, является ли ошибка проблемой прав
                    if (jsonResponse.Contains("Insufficient permission") ||
                        jsonResponse.Contains("permission") ||
                        jsonResponse.Contains("доступа"))
                    {
                        Console.WriteLine($"  🔍 Обнаружена ошибка прав доступа - будем создавать дубликат");
                    }

                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ Исключение в TryAddProductToInvoice: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Создает товар в папке "Прочее" (iblockSectionId = 697)
        /// </summary>
        private async Task<int> CreateProductInOtherFolder(string productName, decimal price, int measureCode)
        {
            try
            {
                Console.WriteLine($"  🔧 Создаем дубликат товара в папке 'Прочее': {productName}");

                string url = "https://crmnvr.ru/rest/241/5gkwkk4657uafc2x/catalog.product.add";

                // Добавляем суффикс, чтобы отличать от оригинала
                string duplicateName = $"{productName} (дубликат)";

                var productData = new
                {
                    fields = new
                    {
                        iblockId = 14,
                        name = duplicateName,
                        iblockSectionId = 697, // ID папки "Прочее"
                        active = "Y",
                        price = new
                        {
                            price = price,
                            currency = "RUB"
                        },
                        measure = measureCode // Код единицы измерения
                    }
                };

                string jsonData = System.Text.Json.JsonSerializer.Serialize(productData);

                using (var client = new HttpClient())
                {
                    var content = new StringContent(jsonData, Encoding.UTF8, "application/json");
                    var response = await client.PostAsync(url, content);

                    if (!response.IsSuccessStatusCode)
                    {
                        string error = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"  ❌ Ошибка HTTP при создании дубликата: {response.StatusCode}, {error}");
                        return 0;
                    }

                    string result = await response.Content.ReadAsStringAsync();

                    using (var doc = System.Text.Json.JsonDocument.Parse(result))
                    {
                        var root = doc.RootElement;

                        if (root.TryGetProperty("result", out var resultElement) &&
                            resultElement.TryGetProperty("element", out var element) &&
                            element.TryGetProperty("id", out var idElement))
                        {
                            int productId = idElement.GetInt32();
                            Console.WriteLine($"  ✅ Дубликат товара создан с ID: {productId}");

                            // Обновляем кэш, чтобы новый товар был доступен
                            _ = Task.Run(async () => await BitrixCache.RefreshCacheAsync());

                            return productId;
                        }

                        Console.WriteLine($"  ❌ Не удалось получить ID дубликата из ответа: {result}");
                        return 0;
                    }
                }
            }
            catch (System.Text.Json.JsonException jsonEx)
            {
                Console.WriteLine($"  ❌ Ошибка парсинга JSON при создании дубликата: {jsonEx.Message}");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ Общая ошибка при создании дубликата: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Проверяет, существует ли уже дубликат товара
        /// </summary>
        private async Task<int> FindExistingDuplicate(string originalProductName)
        {
            try
            {
                string duplicateName = $"{originalProductName} (дубликат)";

                // Используем существующий метод поиска
                int existingId = await GetProductIdByName(duplicateName);

                if (existingId > 0)
                {
                    Console.WriteLine($"  🔍 Найден существующий дубликат товара с ID: {existingId}");
                    return existingId;
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ Ошибка при поиске дубликата: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Проверяем какие поля реально сохранены в счете
        /// </summary>
        /// 



        private DateTime CalculateBusinessDays(DateTime startDate, int businessDays)
        {
            int direction = businessDays < 0 ? -1 : 1;
            businessDays = Math.Abs(businessDays);

            DateTime currentDate = startDate;

            while (businessDays > 0)
            {
                currentDate = currentDate.AddDays(direction);

                // Пропускаем выходные
                if (currentDate.DayOfWeek == DayOfWeek.Saturday ||
                    currentDate.DayOfWeek == DayOfWeek.Sunday)
                {
                    continue;
                }

                businessDays--;
            }

            return currentDate;
        }


        // Вспомогательный метод для проверки сохраненных данных
        private async Task TestRetrieveInvoiceData(int invoiceId)
        {
            try
            {
                Console.WriteLine($"\n=== ПРОВЕРКА ДАННЫХ СЧЕТА #{invoiceId} ===");

                string webhookUrl = $"https://crmnvr.ru/rest/241/5gkwkk4657uafc2x/crm.item.get";
                var requestData = new
                {
                    entityTypeId = 31,
                    id = invoiceId
                };

                string jsonRequest = JsonConvert.SerializeObject(requestData);
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(webhookUrl, content);
                string jsonResponse = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"Данные счета из Bitrix:\n{jsonResponse}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при проверке данных счета: {ex.Message}");
            }
        }




       

        // Метод для очистки названия товара от категории в скобках
        private string CleanProductNameSkobka(string productName)
        {
            if (string.IsNullOrEmpty(productName))
                return productName;

            // Удаляем всё, что в круглых скобках и сами скобки
            // Ищем последнюю открывающую скобку
            int lastOpenBracket = productName.LastIndexOf('(');

            if (lastOpenBracket > 0)
            {
                // Проверяем, есть ли закрывающая скобка после нее
                int closeBracket = productName.IndexOf(')', lastOpenBracket);
                if (closeBracket > lastOpenBracket)
                {
                    // Удаляем всё начиная с пробела перед скобкой
                    // Ищем пробел перед последней открывающей скобкой
                    int spaceBeforeBracket = productName.LastIndexOf(' ', lastOpenBracket - 1);

                    if (spaceBeforeBracket > 0)
                    {
                        // Удаляем от пробела до конца (включая скобки и категорию)
                        return productName.Substring(0, spaceBeforeBracket).TrimEnd();
                    }
                    else
                    {
                        // Если пробела нет, удаляем от открывающей скобки
                        return productName.Substring(0, lastOpenBracket).TrimEnd();
                    }
                }
            }

            // Если скобок нет или они не образуют пару, возвращаем оригинал
            return productName;
        }




        // Метод для очистки названия товара от категории в скобках
        private string CleanProductName(string productName)
        {
            if (string.IsNullOrEmpty(productName))
                return productName;

            // Удаляем всё, что в круглых скобках и сами скобки
            // Ищем последнюю открывающую скобку
            int lastOpenBracket = productName.LastIndexOf('(');

            if (lastOpenBracket > 0)
            {
                // Проверяем, есть ли закрывающая скобка после нее
                int closeBracket = productName.IndexOf(')', lastOpenBracket);
                if (closeBracket > lastOpenBracket)
                {
                    // Удаляем всё начиная с пробела перед скобкой
                    // Ищем пробел перед последней открывающей скобкой
                    int spaceBeforeBracket = productName.LastIndexOf(' ', lastOpenBracket - 1);

                    if (spaceBeforeBracket > 0)
                    {
                        // Удаляем от пробела до конца (включая скобки и категорию)
                        return productName.Substring(0, spaceBeforeBracket).TrimEnd();
                    }
                    else
                    {
                        // Если пробела нет, удаляем от открывающей скобки
                        return productName.Substring(0, lastOpenBracket).TrimEnd();
                    }
                }
            }

            // Если скобок нет или они не образуют пару, возвращаем оригинал
            return productName;
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

                Console.WriteLine("[BitrixService] Загрузка категорий из Bitrix...");

                string webhookUrl = "https://crmnvr.ru/rest/241/5gkwkk4657uafc2x/crm.productsection.list";

                var categories = new List<Category>();
                int start = 0;
                const int pageSize = 50;

                while (true)
                {
                    var requestData = new
                    {
                        order = new { NAME = "ASC" },
                        select = new[] { "ID", "NAME", "SECTION_ID", "CODE" },
                        start = start
                    };

                    string jsonRequest = JsonConvert.SerializeObject(requestData);
                    var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                    var response = await _httpClient.PostAsync(webhookUrl, content);

                    if (!response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"[BitrixService] Ошибка HTTP при загрузке категорий: {response.StatusCode}");
                        break;
                    }

                    string jsonResponse = await response.Content.ReadAsStringAsync();

                    // Дебаг
                    Console.WriteLine($"[BitrixService] Ответ от Bitrix (категории): {jsonResponse.Length} символов");

                    // Парсим JSON - используем правильный класс
                    var data = JsonConvert.DeserializeObject<BitrixCategoryListResponse>(jsonResponse);

                    if (data?.Result == null || data.Result.Count == 0)
                        break;

                    foreach (var item in data.Result)
                    {
                        try
                        {
                            Category category = new Category
                            {
                                Name = item.Name ?? "Без названия",
                                SelectionId = item.Id ?? "0",
                                ParentId = item.SectionId,
                                Code = item.Code
                            };
                            categories.Add(category);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[BitrixService] Ошибка обработки категории: {ex.Message}");
                        }
                    }

                    // Пагинация - проверяем по next
                    if (data.Next.HasValue && data.Next.Value > 0)
                    {
                        start = data.Next.Value;
                    }
                    else if (data.Result.Count < pageSize)
                    {
                        break;
                    }
                    else
                    {
                        start += pageSize;
                    }

                    // Защита от бесконечного цикла
                    if (start > 500) // Максимум 500 категорий
                    {
                        Console.WriteLine("[BitrixService] Достигнут лимит выборки категорий (500)");
                        break;
                    }

                    // Небольшая задержка для Bitrix API
                    await Task.Delay(100);
                }

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

        public async Task<int> CreateCompany(string title, string phone = null, string address = null,
                                        string inn = null, string kpp = null, bool registerEvent = true)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException("Название компании обязательно");
            }

            if (string.IsNullOrWhiteSpace(inn))
            {
                throw new ArgumentException("ИНН обязательно");
            }

            if (string.IsNullOrWhiteSpace(kpp))
            {
                throw new ArgumentException("КПП обязательно");
            }

            if (string.IsNullOrWhiteSpace(address))
            {
                throw new ArgumentException("Адрес обязателен");
            }

            string webhookUrl = "https://crmnvr.ru/rest/241/5gkwkk4657uafc2x/crm.company.add";

            // Подготавливаем данные для создания компании с новыми полями
            var requestData = new
            {
                fields = new
                {
                    TITLE = title.Trim(),
                    UF_CRM_67055523A3E63 = inn.Trim(), // ИНН
                    UF_CRM_67055523AA362 = kpp.Trim(), // КПП
                    ADDRESS = address.Trim(), // Адрес
                    PHONE = !string.IsNullOrWhiteSpace(phone) ?
                           new[] { new { VALUE = phone.Trim(), VALUE_TYPE = "WORK" } } :
                           null,
                    // НЕ УКАЗЫВАЕМ OWNER_ID - Bitrix назначит автоматически
                    // НЕ УКАЗЫВАЕМ ASSIGNED_BY_ID - будет использован текущий пользователь
                },
                @params = new
                {
                    REGISTER_SONET_EVENT = registerEvent ? "Y" : "N"
                }
            };

            try
            {
                // Используем настройки игнорирования null-значений
                string jsonRequest = JsonConvert.SerializeObject(requestData,
                    new JsonSerializerSettings
                    {
                        NullValueHandling = NullValueHandling.Ignore
                    });

                Console.WriteLine("=== ДАННЫЕ ДЛЯ СОЗДАНИЯ КОМПАНИИ ===");
                Console.WriteLine(jsonRequest);
                Console.WriteLine("===================================");

                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                // Делаем POST-запрос
                var response = await _httpClient.PostAsync(webhookUrl, content);
                string jsonResponse = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"Ответ от Bitrix: {jsonResponse}");

                // Парсим ответ
                var result = JsonConvert.DeserializeObject<BitrixAddResponse>(jsonResponse);

                // Проверяем на ошибки
                if (!string.IsNullOrEmpty(result?.Error))
                {
                    // Проверяем специфическую ошибку про OWNER
                    if (result.Error.Contains("невозможно указать себя в свойстве Owner"))
                    {
                        Console.WriteLine("⚠️ Обнаружена ошибка OWNER. Пробуем создать без параметров...");
                        return await CreateCompanyMinimal(title, phone, address);
                    }

                    Console.WriteLine($"Ошибка создания компании: {result.Error}");
                    return 0;
                }

                // Возвращаем ID созданной компании
                int companyId = result?.Result ?? 0;
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

        // Метод для минимального создания компании (без дополнительных полей)
        private async Task<int> CreateCompanyMinimal(string title, string phone = null, string address = null)
        {
            string webhookUrl = "https://crmnvr.ru/rest/241/5gkwkk4657uafc2x/crm.company.add";

            var requestData = new
            {
                fields = new
                {
                    TITLE = title.Trim()
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
                    Console.WriteLine($"Ошибка минимального создания: {result.Error}");
                    return 0;
                }

                return result?.Result ?? 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Исключение при минимальном создании компании: {ex.Message}");
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

                    var productInfo = new ProductWithCategoryInfo
                    {
                        CategoryName = categoryPath,
                        ProductName = product.Name ?? "Без названия",
                        Price = product.Price.GetValueOrDefault(),
                        HasPrice = product.Price.HasValue,
                        SectionId = product.SectionId,
                        ProductCode = product.Code,
                        ProductId = product.Id,
                        Measure = product.Measure

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
        /// <summary>
        /// Создает компанию с расширенными полями (все поля необязательные, кроме названия)
        /// </summary>
     
        /// <summary>
        /// Пытается создать компанию, если не существует компаний с таким названием
        /// </summary>
     
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



