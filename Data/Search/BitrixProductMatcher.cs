using ManagerApp.Data.GetInfo;
using ManagerApp.Data.StructureList;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text.Json;

namespace ManagerApp.Data.Search
{
    public class BitrixProductMatcher
    {
        private List<ProductWithCategoryInfo> _allProductsCache;
        private DateTime _cacheTimestamp;
        private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(30);
        private readonly object _cacheLock = new object();

        // Информация для доступа к AI
        // Информация для доступа к AI - ОСНОВНОЙ
        private const string PrimaryApiToken = "eyJhbGciOiJSUzUxMiIsInR5cCI6IkpXVCIsImtpZCI6IjFrYnhacFJNQGJSI0tSbE1xS1lqIn0.eyJ1c2VyIjoibXoxNjUxODMiLCJ0eXBlIjoiYXBpX2tleSIsImFwaV9rZXlfaWQiOiIxZmI0YWQ0NS0zYjBjLTRiMGQtODJjZS02NzQ0NzFkYWVhYTkiLCJpYXQiOjE3NzIzNzg2Mzl9.K0nQulksfXGzqPYOeudwSVbS2cv0ryHqRsVbElmNg87FuA8BOdUeBq5mPQCr-H0h3cXgg62CJNTfa1ULBd3yCC0y4POj2KbIe_gX_y1May08SC0YP9dQyFEhBgmtcIgOBAg-PvGwlOkkFnjKPxCjOsEkYe2Uf2NaSFqn3yjfZYydxrLTSk4DNlro0zZi7AbAEJvlrefj3fwDdSV3IJIQMApffWhlFpxiqQhmGURMlWdvREadoGY-rtmaZYFVOuZccJeKznQ5bmlZ4KgfRKViacAfVL6zDMP3jLQlWY7aw0ujOG13DUfrwAHSGWXXM-t6CaMQI4DHGjUzHHlZrXZ8h7565am41xxsaE0Alxi7y5vLQrkQvyhEXlWC9Ris3jIaKcIUCMvVrYQCIzPxUurEoKrnEZ8GlZM29mJXexFX_5BP0god0fY08sVZ2IgEu2kTiUJpthO3WyDxQLk3ALAQySYgrkx4YRM-h1YqK8nKnM6vy_E1sA0Jh3mcuVYHyZkS";
        private const string PrimaryApiUrl = "https://agent.timeweb.cloud/api/v1/cloud-ai/agents/7ed67ebb-f658-4716-ac81-35422c12cb21/v1/chat/completions";

        // Информация для доступа к AI - ЗАПАСНОЙ
        private const string BackupApiToken = "eyJhbGciOiJSUzUxMiIsInR5cCI6IkpXVCIsImtpZCI6IjFrYnhacFJNQGJSI0tSbE1xS1lqIn0.eyJ1c2VyIjoibXoxNjUxODMiLCJ0eXBlIjoiYXBpX2tleSIsImFwaV9rZXlfaWQiOiI1ZjVmOGU0OC00ZjAyLTQwOTEtYTlkNC0zZmZjYmZiNGU3NGMiLCJpYXQiOjE3NzI3MTg4OTB9.zj0oLSbBNc6ZZgNfcJn8zsdtpSuYHvsdI7IwKjZqUa-9DGLfSq0cyGUxJmi-YcS2zpDal09gpkOhRLMssMJ29WWCNLWjt8mG92B5-MG9T8y6pAcg2s-wDOusO07jYWomR7eXWml8CvODB3hahArcOM6R_FqUNj1PUGnFNFz0JiIl1yu1idT0SfUO-iGam1aclujeX09PMKXUPcN33gOGXrdWTiFAG7vywZCv-vOaSDEsHH7tlVPggns0BbdgOOzrBqR8f1CiGFlvoo_xNr14sf1hvyDvAK5zf5qQl_JL2M7weSK9KUt3vpCqFoaLzgRp4ikQEX_08nKqf3I-cD_SgpDUhCAR8TgCTvsfjD3ABEgfGDfmuTld_MY2wqhLfPyWi1fr4TYiThcoyeGwL_U6mNnPz_lQUvm-zHem2oYAU3FyvKy2W-wO4YFu-DcZKjz_OY59aGM3l62SOxjh96n2eCSjdv9FF6Gu3344G9RumRzftDaTBUOJULVuu-_W4SiE";
        private const string BackupApiUrl = "https://agent.timeweb.cloud/api/v1/cloud-ai/agents/db9009b9-7858-4e0a-8568-d2bc975dbbe8/v1/chat/completions";
        private const string ApiUrl = "https://agent.timeweb.cloud/api/v1/cloud-ai/agents/7ed67ebb-f658-4716-ac81-35422c12cb21/v1/chat/completions";

        // Список стоп-слов для кабелей и проводов (то, что НЕ должно быть в названии)
        private readonly HashSet<string> _cableStopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "удлинитель", "катушк", "бухт", "барабан", "кабель-канал", "кабель роут",
            "гирлянд", "светильник", "розетк", "выключатель", "автомат", "узо",
            "щит", "дин-рейк", "шин", "led", "светодиод", "ламп", "люстр", "бра",
            "писсуар", "сифон", "унитаз", "раковин", "смеситель", "душ", "ванн",
            "перенос", "удлинитель-шнур", "сетевой фильтр", "шнур"
        };

        // Список обязательных ключевых слов для кабелей и проводов
        private readonly HashSet<string> _cableRequiredKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "провод", "кабель", "пвс", "ввг", "кг", "шввп", "сип", "вббшв",
            "кпп", "кпс", "кввг", "кгвв", "кгпп", "пугв", "пув"
        };

        // ДОПОЛНИТЕЛЬНЫЕ СТОП-СЛОВА ДЛЯ КАБЕЛЕЙ
        private readonly HashSet<string> _additionalCableStopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "шнур", "удлинитель", "переноска", "сетевой фильтр", "пилот", "разветвитель",
            "тройник", "адаптер", "переходник", "кабель-канал", "кабель роут", "кабель роутер"
        };

        public BitrixProductMatcher()
        {
            // Пустой конструктор
        }

        // Получение всех товаров с кешированием
        private async Task<List<ProductWithCategoryInfo>> GetAllProductsAsync()
        {
            lock (_cacheLock)
            {
                if (_allProductsCache != null && (DateTime.Now - _cacheTimestamp) < _cacheDuration)
                {
                    return _allProductsCache;
                }
            }

            // Загружаем из статического BitrixCache
            var products = await BitrixCache.GetAllProductsWithCategories();

            lock (_cacheLock)
            {
                _allProductsCache = products;
                _cacheTimestamp = DateTime.Now;
            }

            return products;
        }



private async Task<string> DetermineProductTypeWithAIRetry(string query, int maxRetries = 3)
        {
            // Пробуем основной AI
            string result = await TryDetermineProductTypeWithAI(query, PrimaryApiToken, PrimaryApiUrl, "основной");
            if (!string.IsNullOrEmpty(result))
                return result;

            // Если основной не сработал, пробуем запасной
            Console.WriteLine("🔄 Переключаемся на запасной AI провайдер...");
            result = await TryDetermineProductTypeWithAI(query, BackupApiToken, BackupApiUrl, "запасной");

            return result;
        }
        // Вспомогательный метод для попытки определения типа через конкретный AI
        private async Task<string> TryDetermineProductTypeWithAI(string query, string apiToken, string apiUrl, string providerName)
        {
            int attempt = 0;
            int delayMs = 1000;

            while (attempt < 2) // Для каждого провайдера делаем 2 попытки
            {
                try
                {
                    attempt++;
                    Console.WriteLine($"🔄 {providerName} AI попытка #{attempt}...");

                    using (HttpClient client = new HttpClient())
                    {
                        client.Timeout = TimeSpan.FromSeconds(30);
                        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiToken}");

                        var requestBody = new
                        {
                            model = "gpt-4o-mini",
                            messages = new[]
                            {
                        new
                        {
                            role = "user",
                            content = $"Определи тип товара по его названию. Ответь одним словом: 'кабель', 'провод' или 'другое'.\n\nТовар: {query}"
                        }
                    },
                            temperature = 0.1,
                            max_tokens = 10
                        };

                        string jsonRequest = JsonSerializer.Serialize(requestBody);
                        var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                        HttpResponseMessage response = await client.PostAsync(apiUrl, content);

                        if (response.IsSuccessStatusCode)
                        {
                            string jsonResponse = await response.Content.ReadAsStringAsync();

                            using (JsonDocument doc = JsonDocument.Parse(jsonResponse))
                            {
                                if (doc.RootElement.TryGetProperty("choices", out JsonElement choices) &&
                                    choices.GetArrayLength() > 0)
                                {
                                    var firstChoice = choices[0];
                                    if (firstChoice.TryGetProperty("message", out JsonElement message) &&
                                        message.TryGetProperty("content", out JsonElement contentElement))
                                    {
                                        string result = contentElement.GetString().Trim().ToLower();
                                        Console.WriteLine($"✅ {providerName} AI ответил: {result}");
                                        return result;
                                    }
                                }
                            }
                        }
                        else
                        {
                            string errorResponse = await response.Content.ReadAsStringAsync();
                            Console.WriteLine($"⚠️ {providerName} AI ошибка {response.StatusCode}: {errorResponse}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ {providerName} AI исключение: {ex.Message}");
                }

                if (attempt < 2)
                {
                    Console.WriteLine($"⏳ Ожидание {delayMs / 1000} сек перед следующей попыткой...");
                    await Task.Delay(delayMs);
                    delayMs *= 2;
                }
            }

            Console.WriteLine($"❌ {providerName} AI недоступен после всех попыток");
            return null;
        }

        // ПРОВЕРКА НАЛИЧИЯ СЕЧЕНИЯ В ЗАПРОСЕ
        private bool HasSectionPattern(string query)
        {
            var sectionPatterns = new[]
            {
                @"\d+[×хx*]\d+[.,]?\d*",  // 3×2,5, 3х2.5
                @"\d+\s*[×хx*]\s*\d+[.,]?\d*" // 3 × 2,5 с пробелами
            };

            foreach (var pattern in sectionPatterns)
            {
                if (Regex.IsMatch(query, pattern))
                {
                    return true;
                }
            }

            return false;
        }

        // ПОИСК ДЛЯ КАБЕЛЕЙ И ПРОВОДОВ (УЛУЧШЕННАЯ ВЕРСИЯ)


        // ОБНОВЛЕННАЯ ПРОВЕРКА, ЯВЛЯЕТСЯ ЛИ ТОВАР КАБЕЛЕМ/ПРОВОДОМ
        private bool IsCableOrWireProduct(string productName)
        {
            if (string.IsNullOrEmpty(productName))
                return false;

            string lowerName = productName.ToLower();

            // Сначала проверяем дополнительные стоп-слова
            foreach (var stopWord in _additionalCableStopWords)
            {
                if (lowerName.Contains(stopWord))
                {
                    return false;
                }
            }

            // Проверяем основные стоп-слова
            foreach (var stopWord in _cableStopWords)
            {
                if (lowerName.Contains(stopWord))
                {
                    return false;
                }
            }

            // Проверяем на обязательные ключевые слова
            foreach (var keyword in _cableRequiredKeywords)
            {
                if (lowerName.Contains(keyword))
                {
                    return true;
                }
            }

            // Проверяем на наличие сечения
            if (HasSectionPattern(productName))
            {
                return true;
            }

            return false;
        }

        // РАСШИРЕННОЕ ИЗВЛЕЧЕНИЕ МОДЕЛИ И СЕЧЕНИЯ
        private (string Model, string Section) ExtractModelAndSectionAdvanced(string query)
        {
            string model = "";
            string section = "";

            // Паттерны для сечения
            var sectionPatterns = new[]
            {
                @"(\d+)[×хx*](\d+[.,]?\d*)",
                @"(\d+)\s*[×хx*]\s*(\d+[.,]?\d*)"
            };

            foreach (var pattern in sectionPatterns)
            {
                var match = Regex.Match(query, pattern);
                if (match.Success)
                {
                    string first = match.Groups[1].Value;
                    string second = match.Groups[2].Value;
                    section = $"{first}×{second}";

                    // Удаляем сечение для поиска модели
                    string queryWithoutSection = Regex.Replace(query, pattern, "").Trim();

                    // Ищем модель
                    model = ExtractCableModelAdvanced(queryWithoutSection);
                    break;
                }
            }

            return (model, section);
        }

        // РАСШИРЕННОЕ ИЗВЛЕЧЕНИЕ МОДЕЛИ КАБЕЛЯ
        private string ExtractCableModelAdvanced(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";

            // Приоритетный список марок кабелей
            var cablePatterns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // ВВГ семейство
                { @"ВВГ[ -]?П?нг\([А]\)[-]?LS", "ВВГ-Пнг(А)-LS" },
                { @"ВВГнг\(А\)[-]?LS", "ВВГнг(А)-LS" },
                { @"ВВГ[ -]?П?нг\([А]\)[-]?FRLS", "ВВГ-Пнг(А)-FRLS" },
                { @"ВВГнг\(А\)[-]?FRLS", "ВВГнг(А)-FRLS" },
                { @"ВВГ[ -]?П?нг[-]?LS", "ВВГ-Пнг-LS" },
                { @"ВВГнг[-]?LS", "ВВГнг-LS" },
                { @"ВВГ[ -]?П?нг\([А]\)", "ВВГ-Пнг(А)" },
                { @"ВВГнг\(А\)", "ВВГнг(А)" },
                { @"ВВГ[ -]?П?нг", "ВВГ-Пнг" },
                { @"ВВГнг", "ВВГнг" },
                { @"ВВГ", "ВВГ" },
                
                // ПВС семейство
                { @"ПВС", "ПВС" },
                
                // КГ семейство
                { @"КГ(?:ТП)?(?:ВВ)?(?:ПП)?нг\([А]\)[-]?(?:LS|HF|FRHF)?", "КГ" },
                { @"КГ(?:ТП)?(?:ВВ)?(?:ПП)?нг[-]?(?:LS|HF|FRHF)?", "КГ" },
                { @"КГ(?:-ХЛ)?", "КГ" },
                
                // Другие
                { @"ШВВП", "ШВВП" },
                { @"ВБбШв", "ВБбШв" },
                { @"СИП", "СИП" }
            };

            foreach (var kvp in cablePatterns)
            {
                if (Regex.IsMatch(text, kvp.Key, RegexOptions.IgnoreCase))
                {
                    var match = Regex.Match(text, kvp.Key, RegexOptions.IgnoreCase);
                    return match.Value;
                }
            }

            return "";
        }

        // НОРМАЛИЗАЦИЯ МОДЕЛИ
        private string NormalizeModel(string model)
        {
            if (string.IsNullOrEmpty(model))
                return "";

            // Убираем все небуквенные символы
            string normalized = Regex.Replace(model.ToLower(), @"[^а-яa-z]", "");

            // Нормализуем распространенные варианты
            normalized = normalized
                .Replace("нг", "нг")
                .Replace("ls", "ls")
                .Replace("frls", "frls")
                .Replace("ввг", "ввг")
                .Replace("пвс", "пвс")
                .Replace("кг", "кг");

            return normalized;
        }

        // НОРМАЛИЗАЦИЯ СЕЧЕНИЯ
        private string NormalizeSection(string section)
        {
            if (string.IsNullOrEmpty(section))
                return "";

            // Заменяем разделители на ×
            section = Regex.Replace(section, @"[хx*]", "×");

            // Заменяем запятые на точки в числах
            section = Regex.Replace(section, @"(\d+),(\d+)", "$1.$2");

            return section;
        }

        // РАСШИРЕННЫЙ АНАЛИЗ КАБЕЛЬНОГО ТОВАРА
        private CableAnalysis AnalyzeCableProductAdvanced(ProductWithCategoryInfo product, string expectedModel, string expectedSection)
        {
            if (product == null || string.IsNullOrEmpty(product.ProductName))
                return null;

            string productName = product.ProductName ?? "";
            var analysis = new CableAnalysis
            {
                Product = product,
                HasExactModel = false,
                HasExactSection = false,
                Relevance = 0
            };

            // Извлекаем сечение из названия товара
            string productSection = ExtractSectionFromText(productName);

            // Проверяем сечение
            if (!string.IsNullOrEmpty(productSection) && CompareSections(productSection, expectedSection))
            {
                analysis.HasExactSection = true;
                analysis.Relevance += 500;
            }

            if (!analysis.HasExactSection)
                return null;

            // Проверяем модель
            if (!string.IsNullOrEmpty(expectedModel))
            {
                string productModel = ExtractCableModelAdvanced(productName);
                string normalizedExpected = NormalizeModel(expectedModel);
                string normalizedProduct = NormalizeModel(productModel);

                if (!string.IsNullOrEmpty(normalizedProduct) && normalizedProduct == normalizedExpected)
                {
                    analysis.HasExactModel = true;
                    analysis.Relevance += 1000;
                }
            }

            // Штрафуем за стоп-слова
            string lowerName = productName.ToLower();
            foreach (var stopWord in _cableStopWords)
            {
                if (lowerName.Contains(stopWord))
                {
                    analysis.Relevance -= 500;
                }
            }

            // Бонус за цену
            if (product.HasPrice && product.Price > 0)
                analysis.Relevance += 50;

            return analysis;
        }

        // СРАВНЕНИЕ СЕЧЕНИЙ
        private bool CompareSections(string section1, string section2)
        {
            if (string.IsNullOrEmpty(section1) || string.IsNullOrEmpty(section2))
                return false;

            try
            {
                var match1 = Regex.Match(section1, @"(\d+)[×хx*](\d+[.,]?\d*)");
                var match2 = Regex.Match(section2, @"(\d+)[×хx*](\d+[.,]?\d*)");

                if (match1.Success && match2.Success)
                {
                    // Безопасное парсинг количества жил
                    if (!int.TryParse(match1.Groups[1].Value, out int count1) ||
                        !int.TryParse(match2.Groups[1].Value, out int count2))
                    {
                        return false;
                    }

                    if (count1 != count2)
                        return false;

                    // Безопасный парсинг сечения
                    string sectionValue1 = match1.Groups[2].Value.Replace(',', '.');
                    string sectionValue2 = match2.Groups[2].Value.Replace(',', '.');

                    if (!double.TryParse(sectionValue1, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out double value1) ||
                        !double.TryParse(sectionValue2, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out double value2))
                    {
                        return false;
                    }

                    return Math.Abs(value1 - value2) < 0.01;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка сравнения сечений: {ex.Message}");
            }

            return section1 == section2;
        }

        // ИЗВЛЕЧЕНИЕ СЕЧЕНИЯ ИЗ ТЕКСТА
        private string ExtractSectionFromText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";

            try
            {
                var sectionPatterns = new[]
                {
                    @"(\d+)[×хx*](\d+[.,]?\d*)"
                };

                foreach (var pattern in sectionPatterns)
                {
                    var match = Regex.Match(text, pattern);
                    if (match.Success)
                    {
                        return $"{match.Groups[1].Value}×{match.Groups[2].Value}";
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка извлечения сечения: {ex.Message}");
            }

            return "";
        }

        // ПОИСК ДЛЯ ОБЫЧНЫХ ТОВАРОВ
        private async Task<List<ProductWithCategoryInfo>> SearchGeneralProductAsync(
            List<ProductWithCategoryInfo> allProducts,
            string query,
            int maxResults)
        {
            Console.WriteLine($"📊 Общий поиск для: '{query}'");

            var analyzedProducts = new List<GeneralAnalysis>();

            foreach (var product in allProducts)
            {
                var analysis = AnalyzeGeneralProduct(product, query);
                if (analysis != null && analysis.Relevance >= 60)
                {
                    analyzedProducts.Add(analysis);
                }
            }

            Console.WriteLine($"📊 Найдено товаров с релевантностью >60%: {analyzedProducts.Count}");

            // Группируем по поставщикам
            var groupedBySupplier = analyzedProducts
                .GroupBy(p => p.Product.CategoryName ?? "БЕЗ ПОСТАВЩИКА")
                .SelectMany(g => g.OrderByDescending(p => p.Relevance).Take(3))
                .OrderByDescending(p => p.Relevance)
                .ThenByDescending(p => p.Product.HasPrice)
                .Take(maxResults)
                .ToList();

            var suppliers = groupedBySupplier
                .Select(p => p.Product.CategoryName ?? "БЕЗ ПОСТАВЩИКА")
                .Distinct()
                .ToList();

            Console.WriteLine($"📊 Поставщиков в результате: {suppliers.Count}");

            return groupedBySupplier.Select(p => p.Product).ToList();
        }

        // АНАЛИЗ ОБЫЧНОГО ТОВАРА
        private GeneralAnalysis AnalyzeGeneralProduct(ProductWithCategoryInfo product, string query)
        {
            if (product == null || string.IsNullOrEmpty(product.ProductName))
                return null;

            string productName = product.ProductName ?? "";
            string queryLower = query.ToLower();
            string productLower = productName.ToLower();

            int relevance = 0;

            // Точное совпадение
            if (productLower == queryLower)
            {
                relevance = 100;
            }
            // Содержит весь запрос
            else if (productLower.Contains(queryLower))
            {
                relevance = 95;
            }
            else
            {
                var queryWords = queryLower.Split(new[] { ' ', ',', '.', '-', '(', ')' },
                    StringSplitOptions.RemoveEmptyEntries)
                    .Where(w => w.Length > 1)
                    .ToList();

                if (queryWords.Any())
                {
                    int matches = queryWords.Count(word => productLower.Contains(word));
                    relevance = (int)((double)matches / queryWords.Count * 100);
                }
            }

            if (product.HasPrice && product.Price > 0)
                relevance = Math.Min(100, relevance + 5);

            return relevance >= 60 ? new GeneralAnalysis { Product = product, Relevance = relevance } : null;
        }

        // ВСПОМОГАТЕЛЬНЫЕ КЛАССЫ
        private class CableAnalysis
        {
            public ProductWithCategoryInfo Product { get; set; }
            public bool HasExactModel { get; set; }
            public bool HasExactSection { get; set; }
            public int Relevance { get; set; }
        }

        private class GeneralAnalysis
        {
            public ProductWithCategoryInfo Product { get; set; }
            public int Relevance { get; set; }
        }

        // ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ
        public async Task<List<ProductWithCategoryInfo>> QuickSearchAsync(string searchQuery, int maxResults = 5)
        {
            try
            {
                return await FindSimilarProductsAsync(searchQuery, maxResults);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка быстрого поиска: {ex.Message}");
                return new List<ProductWithCategoryInfo>();
            }
        }

        public async Task<string> SearchProductAsync(string productName)
        {
            try
            {
                var allProducts = await GetAllProductsAsync();
                var exactMatch = allProducts.FirstOrDefault(p =>
                    p.ProductName.Equals(productName, StringComparison.OrdinalIgnoreCase));

                if (exactMatch != null)
                {
                    return FormatProductResult(exactMatch);
                }

                return $"❌ Товар не найден: {productName}";
            }
            catch (Exception ex)
            {
                return $"❌ Ошибка: {ex.Message}";
            }
        }

        private string FormatProductResult(ProductWithCategoryInfo product)
        {
            return $"✅ НАЙДЕНО: {product.ProductName}\n" +
                   $"   Категория: {product.CategoryName}\n" +
                   $"   Цена: {(product.HasPrice ? product.Price.ToString() + " руб." : "Нет цены")}";
        }

        public void ClearCache()
        {
            lock (_cacheLock)
            {
                _allProductsCache = null;
            }
        }


        // ОСНОВНОЙ МЕТОД ПОИСКА
        public async Task<List<ProductWithCategoryInfo>> FindSimilarProductsAsync(
            string searchQuery,
            int maxResults = 50)
        {
            if (string.IsNullOrWhiteSpace(searchQuery))
                return new List<ProductWithCategoryInfo>();

            try
            {
                var allProducts = await GetAllProductsAsync();
                if (allProducts == null || !allProducts.Any())
                    return new List<ProductWithCategoryInfo>();

                var query = searchQuery.Trim();
                Console.WriteLine($"🔍 Поиск: '{query}'");

                // Пытаемся использовать AI с повторными попытками
                string productType = await DetermineProductTypeWithAIRetry(query, maxRetries: 3);

                if (!string.IsNullOrEmpty(productType) &&
                    (productType.Contains("кабель") || productType.Contains("провод")))
                {
                    Console.WriteLine($"📌 AI определил как: {productType}");

                    // Проверяем, есть ли сечение в запросе
                    if (HasSectionPattern(query))
                    {
                        return await SearchCableOrWireAsync(allProducts, query, maxResults);
                    }
                    else
                    {
                        Console.WriteLine("⚠️ AI определил как кабель/провод, но нет сечения - используем общий поиск");
                        return await SearchGeneralProductAsync(allProducts, query, maxResults);
                    }
                }

                // Если AI не сработал или определил другое, используем резервный метод
                Console.WriteLine("📌 Используем резервный метод определения");

                if (HasSectionPattern(query))
                {
                    // Извлекаем модель и сечение для проверки
                    var extractedData = ExtractModelAndSectionAdvanced(query);

                    // Если есть и модель, и сечение - используем поиск для кабелей
                    if (!string.IsNullOrEmpty(extractedData.Section))
                    {
                        return await SearchCableOrWireAsync(allProducts, query, maxResults);
                    }
                    else
                    {
                        Console.WriteLine("⚠️ Есть сечение, но не удалось извлечь - используем общий поиск");
                        return await SearchGeneralProductAsync(allProducts, query, maxResults);
                    }
                }
                else
                {
                    // Нет сечения - используем общий поиск
                    return await SearchGeneralProductAsync(allProducts, query, maxResults);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка: {ex.Message}");
                return new List<ProductWithCategoryInfo>();
            }
        }

        // ИСПРАВЛЕННЫЙ МЕТОД ПОИСКА ДЛЯ КАБЕЛЕЙ И ПРОВОДОВ
        private async Task<List<ProductWithCategoryInfo>> SearchCableOrWireAsync(
            List<ProductWithCategoryInfo> allProducts,
            string query,
            int maxResults)
        {
            // Извлекаем модель и сечение
            var extractedData = ExtractModelAndSectionAdvanced(query);
            string expectedModel = extractedData.Model;
            string expectedSection = extractedData.Section;
            string normalizedExpectedSection = NormalizeSection(expectedSection);

            Console.WriteLine($"📊 Извлечено: Модель='{expectedModel}', Сечение='{expectedSection}'");

            // Если нет сечения - сразу переходим к общему поиску
            if (string.IsNullOrEmpty(expectedSection))
            {
                Console.WriteLine("⚠️ Сечение не найдено, переключаемся на общий поиск");
                return await SearchGeneralProductAsync(allProducts, query, maxResults);
            }

            // Определяем тип искомого товара (кабель или провод)
            bool searchForCable = query.ToLower().Contains("кабель");
            bool searchForWire = query.ToLower().Contains("провод") || query.ToLower().Contains("пвс");

            // Анализируем товары
            var exactMatches = new List<CableAnalysis>();
            var possibleMatches = new List<CableAnalysis>();

            foreach (var product in allProducts)
            {
                string productName = product.ProductName ?? "";

                // Проверяем, что товар действительно является кабелем/проводом
                if (!IsCableOrWireProduct(productName))
                {
                    continue;
                }

                // Дополнительная проверка типа товара
                bool isCable = productName.ToLower().Contains("кабель");
                bool isWire = productName.ToLower().Contains("провод") ||
                              productName.ToLower().Contains("пвс") ||
                              productName.ToLower().Contains("пугв");

                // Если ищем кабель, а товар - провод/шнур - пропускаем
                if (searchForCable && !isCable)
                {
                    continue;
                }

                // Если ищем провод, а товар - кабель - пропускаем
                if (searchForWire && !isWire)
                {
                    continue;
                }

                var analysis = AnalyzeCableProductAdvanced(product, expectedModel, normalizedExpectedSection);
                if (analysis != null)
                {
                    if (analysis.HasExactModel && analysis.HasExactSection)
                    {
                        exactMatches.Add(analysis);
                    }
                    else if (analysis.HasExactSection)
                    {
                        possibleMatches.Add(analysis);
                    }
                }
            }

            Console.WriteLine($"✅ Точных совпадений (модель+сечение): {exactMatches.Count}");
            Console.WriteLine($"✅ Совпадений только по сечению: {possibleMatches.Count}");

            // Если нет никаких совпадений - используем общий поиск
            if (!exactMatches.Any() && !possibleMatches.Any())
            {
                Console.WriteLine("⚠️ Нет совпадений для кабеля/провода, переключаемся на общий поиск");
                return await SearchGeneralProductAsync(allProducts, query, maxResults);
            }

            // Функция для определения приоритета поставщика
            bool IsEnergoprom(string supplier)
            {
                return !string.IsNullOrEmpty(supplier) &&
                       supplier.IndexOf("Энергопром", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            // Функция для приоритета точного соответствия типа
            bool IsExactTypeMatch(string productName)
            {
                string lowerName = productName.ToLower();

                if (searchForCable)
                    return lowerName.Contains("кабель") &&
                           !lowerName.Contains("шнур") &&
                           !lowerName.Contains("удлинитель") &&
                           !lowerName.Contains("провод");

                if (searchForWire)
                    return (lowerName.Contains("провод") || lowerName.Contains("пвс")) &&
                           !lowerName.Contains("шнур") &&
                           !lowerName.Contains("удлинитель") &&
                           !lowerName.Contains("кабель");

                return true;
            }

            // Сортируем точные совпадения
            exactMatches = exactMatches
                .OrderByDescending(p => IsExactTypeMatch(p.Product.ProductName))
                .ThenByDescending(p => IsEnergoprom(p.Product.CategoryName))
                .ThenByDescending(p => p.Relevance)
                .ThenByDescending(p => p.Product.HasPrice)
                .ToList();

            // Сортируем возможные совпадения
            possibleMatches = possibleMatches
                .OrderByDescending(p => IsExactTypeMatch(p.Product.ProductName))
                .ThenByDescending(p => IsEnergoprom(p.Product.CategoryName))
                .ThenByDescending(p => p.Relevance)
                .ThenByDescending(p => p.Product.HasPrice)
                .ToList();

            // Формируем результат
            List<ProductWithCategoryInfo> resultProducts;

            if (exactMatches.Any())
            {
                resultProducts = exactMatches
                    .Select(p => p.Product)
                    .Take(maxResults)
                    .ToList();
                Console.WriteLine($"📌 Найдено товаров с точным совпадением модели+сечения: {resultProducts.Count}");
            }
            else if (possibleMatches.Any())
            {
                resultProducts = possibleMatches
                    .Select(p => p.Product)
                    .Take(maxResults)
                    .ToList();
                Console.WriteLine($"📌 Найдено товаров только по сечению: {resultProducts.Count}");
            }
            else
            {
                Console.WriteLine("❌ Не найдено подходящих товаров");
                return new List<ProductWithCategoryInfo>();
            }

            // Выводим результаты
            for (int i = 0; i < Math.Min(10, resultProducts.Count); i++)
            {
                var product = resultProducts[i];
                var analysis = i < exactMatches.Count ? exactMatches[i] : possibleMatches[i - exactMatches.Count];
                string matchType = analysis.HasExactModel ? "✅ МОДЕЛЬ+СЕЧЕНИЕ" : "📐 ТОЛЬКО СЕЧЕНИЕ";
                string energopromMark = IsEnergoprom(product.CategoryName) ? " [ЭНЕРГОПРОМ]" : "";
                string typeMark = IsExactTypeMatch(product.ProductName) ? "" : " ⚠️ НЕ СООТВЕТСТВУЕТ ТИПУ";
                Console.WriteLine($"   {i + 1}. [{matchType}{energopromMark}{typeMark}] [{product.CategoryName}] {product.ProductName}");
            }

            return resultProducts;
        }
    }
}