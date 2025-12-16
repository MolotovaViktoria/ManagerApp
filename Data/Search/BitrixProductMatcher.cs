using ManagerApp.Data.GetInfo;
using ManagerApp.Data.StructureList;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ManagerApp.Data.Search
{
    public class BitrixProductMatcher
    {
        private List<ProductWithCategoryInfo> _allProductsCache;
        private DateTime _cacheTimestamp;
        private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(30);
        private readonly object _cacheLock = new object();

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

        // УЛУЧШЕННЫЙ ПОИСК ПОХОЖИХ ТОВАРОВ
        public async Task<List<ProductWithCategoryInfo>> FindSimilarProductsAsync(
            string searchQuery,
            int maxResults = 10,
            double minSimilarityThreshold = 0.3)
        {
            if (string.IsNullOrWhiteSpace(searchQuery))
                return new List<ProductWithCategoryInfo>();

            try
            {
                // Получаем все товары из кеша
                var allProducts = await GetAllProductsAsync();

                // Отладочный вывод
                Console.WriteLine($"🔍 Поиск: '{searchQuery}'");
                Console.WriteLine($"Всего товаров в кеше: {allProducts?.Count ?? 0}");

                if (allProducts == null || !allProducts.Any())
                {
                    Console.WriteLine("⚠️ Кеш пустой!");
                    return new List<ProductWithCategoryInfo>();
                }

                // Нормализуем поисковый запрос
                var normalizedQuery = searchQuery.Trim().ToLower();

                // РАЗДЕЛЯЕМ ПОИСК НА ЭТАПЫ:

                // 1. БЫСТРЫЙ ПОИСК ТОЧНЫХ СОВПАДЕНИЙ
                var stage1Results = new List<ProductWithCategoryInfo>();
                Console.WriteLine($"Этап 1: Точный поиск...");

                // Точное совпадение названия
                var exactMatches = allProducts
                    .Where(p => p.ProductName?.ToLower() == normalizedQuery)
                    .ToList();

                if (exactMatches.Any())
                {
                    Console.WriteLine($"✅ Найдено точных совпадений: {exactMatches.Count}");
                    stage1Results.AddRange(exactMatches);
                }

                // Частичное совпадение названия (содержит запрос)
                var containsMatches = allProducts
                    .Where(p => p.ProductName?.ToLower().Contains(normalizedQuery) == true)
                    .Where(p => !exactMatches.Contains(p)) // Не дублируем точные совпадения
                    .ToList();

                if (containsMatches.Any())
                {
                    Console.WriteLine($"✅ Найдено частичных совпадений: {containsMatches.Count}");
                    stage1Results.AddRange(containsMatches);
                }

                // 2. УМНЫЙ ПОИСК ПО СЛОВАМ (если результатов мало)
                var stage2Results = new List<ProductWithCategoryInfo>();
                if (stage1Results.Count < maxResults)
                {
                    Console.WriteLine($"Этап 2: Умный поиск по словам...");

                    // Разбиваем запрос на значимые слова
                    var searchWords = ExtractSignificantWords(normalizedQuery);

                    if (searchWords.Any())
                    {
                        // Поиск по каждому слову
                        foreach (var word in searchWords)
                        {
                            var wordMatches = allProducts
                                .Where(p => ContainsWord(p.ProductName?.ToLower(), word) ||
                                           ContainsWord(p.CategoryName?.ToLower(), word))
                                .Where(p => !stage1Results.Contains(p) && !stage2Results.Contains(p))
                                .Take(maxResults - (stage1Results.Count + stage2Results.Count))
                                .ToList();

                            if (wordMatches.Any())
                            {
                                stage2Results.AddRange(wordMatches);
                            }

                            // Если набрали достаточно результатов, выходим
                            if (stage1Results.Count + stage2Results.Count >= maxResults)
                                break;
                        }
                    }
                }

                // 3. ПОИСК ПО СХОДСТВУ (если все еще мало результатов)
                var stage3Results = new List<ProductWithCategoryInfo>();
                if (stage1Results.Count + stage2Results.Count < maxResults)
                {
                    Console.WriteLine($"Этап 3: Поиск по сходству...");

                    // Используем улучшенный алгоритм сходства
                    var scoredProducts = new List<(ProductWithCategoryInfo Product, double Score)>();

                    foreach (var product in allProducts)
                    {
                        // Пропускаем уже найденные товары
                        if (stage1Results.Contains(product) || stage2Results.Contains(product))
                            continue;

                        double similarity = CalculateEnhancedSimilarity(normalizedQuery, product);

                        if (similarity >= minSimilarityThreshold)
                        {
                            scoredProducts.Add((product, similarity));
                        }


                    }

                    // Сортируем по сходству и берем лучшие
                    stage3Results = scoredProducts
                        .OrderByDescending(x => x.Score)
                        .Select(x => x.Product)
                        .Take(maxResults - (stage1Results.Count + stage2Results.Count))
                        .ToList();
                }

                // ОБЪЕДИНЯЕМ ВСЕ РЕЗУЛЬТАТЫ
                var allResults = new List<ProductWithCategoryInfo>();
                allResults.AddRange(stage1Results);
                allResults.AddRange(stage2Results);
                allResults.AddRange(stage3Results);

                // Убираем дубликаты (на всякий случай)
                allResults = allResults
                    .GroupBy(p => p.ProductId)
                    .Select(g => g.First())
                    .Take(maxResults)
                    .ToList();

                Console.WriteLine($"✅ ИТОГО найдено: {allResults.Count} товаров");
                Console.WriteLine($"   Этап 1: {stage1Results.Count}, Этап 2: {stage2Results.Count}, Этап 3: {stage3Results.Count}");

                if (allResults.Any())
                {
                    Console.WriteLine("Примеры найденных товаров:");
                    foreach (var product in allResults.Take(3))
                    {
                        Console.WriteLine($"   - {product.ProductName}");
                    }
                }

                return allResults;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка поиска товаров: {ex.Message}");
                return new List<ProductWithCategoryInfo>();
            }
        }

        // УЛУЧШЕННЫЙ АЛГОРИТМ РАСЧЕТА СХОДСТВА
        private double CalculateEnhancedSimilarity(string searchQuery, ProductWithCategoryInfo product)
        {
            if (string.IsNullOrWhiteSpace(searchQuery) || product == null)
                return 0;

            var productName = product.ProductName?.ToLower() ?? "";
            var categoryName = product.CategoryName?.ToLower() ?? "";

            // 1. ПРЯМОЕ СОВПАДЕНИЕ (самый высокий вес)
            if (productName.Contains(searchQuery))
                return 1.0;

            // 2. РАЗБИВАЕМ НА СЛОВА
            var queryWords = ExtractSignificantWords(searchQuery);
            var productWords = ExtractSignificantWords(productName);
            var categoryWords = ExtractSignificantWords(categoryName);

            if (!queryWords.Any())
                return 0;

            // 3. СОВПАДЕНИЕ СЛОВ В НАЗВАНИИ
            double nameWordMatch = CalculateWordMatchScore(queryWords, productWords);

            // 4. СОВПАДЕНИЕ СЛОВ В КАТЕГОРИИ
            double categoryWordMatch = CalculateWordMatchScore(queryWords, categoryWords);

            // 5. СОВПАДЕНИЕ ПО НАЧАЛУ СЛОВ
            double prefixMatch = CalculatePrefixMatchScore(queryWords, productWords);

            // 6. УЧЕТ ЦИФРОВЫХ КОМПОНЕНТОВ (для размеров, диаметров и т.д.)
            double numberMatch = CalculateNumberMatchScore(searchQuery, productName);

            // 7. УЧЕТ СОКРАЩЕНИЙ И АББРЕВИАТУР
            double abbreviationMatch = CalculateAbbreviationMatchScore(searchQuery, productName);

            // ВЗВЕШЕННАЯ СУММА ВСЕХ МЕТРИК
            double totalScore =
                nameWordMatch * 0.4 +           // 40% за совпадение слов в названии
                categoryWordMatch * 0.2 +       // 20% за совпадение в категории
                prefixMatch * 0.15 +            // 15% за совпадение начала слов
                numberMatch * 0.15 +            // 15% за совпадение чисел
                abbreviationMatch * 0.1;        // 10% за совпадение сокращений

            // БОНУСЫ:
            if (product.HasPrice) totalScore += 0.05;        // +5% за наличие цены
            if (!string.IsNullOrEmpty(categoryName)) totalScore += 0.03; // +3% за наличие категории

            return Math.Min(totalScore, 1.0);
        }

        // УЛУЧШЕННЫЕ ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ:

        // Извлечение значимых слов (игнорирует стоп-слова)
        private List<string> ExtractSignificantWords(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new List<string>();

            // Список стоп-слов (короткие и неинформативные слова)
            var stopWords = new HashSet<string>
            {
                "и", "в", "на", "с", "по", "для", "из", "от", "до", "за",
                "а", "но", "или", "то", "же", "бы", "ли", "как", "что",
                "мм", "см", "м", "кг", "г", "л", "шт", "уп", "набор"
            };

            // Разбиваем на слова, фильтруем стоп-слова и короткие слова
            return text.Split(new[] { ' ', '-', '_', ',', '.', '(', ')', '[', ']', '/' },
                           StringSplitOptions.RemoveEmptyEntries)
                      .Where(word => word.Length > 2) // Игнорируем слова короче 3 символов
                      .Where(word => !stopWords.Contains(word.ToLower()))
                      .Select(word => word.ToLower())
                      .ToList();
        }

        // Проверка содержит ли строка слово (с учетом разделителей)
        private bool ContainsWord(string text, string word)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(word))
                return false;

            // Ищем слово целиком, учитывая разделители
            var separators = new[] { ' ', '-', '_', ',', '.', '(', ')', '[', ']', '/' };

            // Разбиваем текст на слова
            var words = text.Split(separators, StringSplitOptions.RemoveEmptyEntries)
                          .Select(w => w.ToLower())
                          .ToList();

            return words.Contains(word) || words.Any(w => w.Contains(word));
        }

        // Расчет совпадения слов
        private double CalculateWordMatchScore(List<string> queryWords, List<string> targetWords)
        {
            if (!queryWords.Any() || !targetWords.Any())
                return 0;

            double matchCount = 0;
            foreach (var queryWord in queryWords)
            {
                // Ищем точное совпадение слова
                if (targetWords.Contains(queryWord))
                {
                    matchCount++;
                }
                else
                {
                    // Ищем частичное совпадение (слово содержится в другом слове)
                    if (targetWords.Any(tw => tw.Contains(queryWord) || queryWord.Contains(tw)))
                    {
                        matchCount += 0.5; // Половина балла за частичное совпадение
                    }
                }
            }

            return (double)matchCount / queryWords.Count;
        }

        // Расчет совпадения по началу слов
        private double CalculatePrefixMatchScore(List<string> queryWords, List<string> targetWords)
        {
            if (!queryWords.Any() || !targetWords.Any())
                return 0;

            int prefixMatchCount = 0;
            foreach (var queryWord in queryWords)
            {
                // Ищем слова, начинающиеся с запроса или наоборот
                if (targetWords.Any(tw => tw.StartsWith(queryWord) || queryWord.StartsWith(tw)))
                {
                    prefixMatchCount++;
                }
            }

            return (double)prefixMatchCount / queryWords.Count;
        }

        // Расчет совпадения чисел (для размеров, диаметров и т.д.)
        private double CalculateNumberMatchScore(string query, string productName)
        {
            // Извлекаем все числа из запроса
            var queryNumbers = ExtractNumbers(query);
            if (!queryNumbers.Any())
                return 0;

            // Извлекаем все числа из названия товара
            var productNumbers = ExtractNumbers(productName);
            if (!productNumbers.Any())
                return 0;

            // Проверяем совпадение чисел
            int matchCount = 0;
            foreach (var queryNumber in queryNumbers)
            {
                if (productNumbers.Contains(queryNumber))
                {
                    matchCount++;
                }
            }

            return (double)matchCount / queryNumbers.Count;
        }

        // Извлечение чисел из строки
        private List<string> ExtractNumbers(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new List<string>();

            var numbers = new List<string>();
            var currentNumber = new StringBuilder();

            foreach (char c in text)
            {
                if (char.IsDigit(c) || c == '.' || c == ',')
                {
                    currentNumber.Append(c);
                }
                else if (currentNumber.Length > 0)
                {
                    numbers.Add(currentNumber.ToString());
                    currentNumber.Clear();
                }
            }

            if (currentNumber.Length > 0)
            {
                numbers.Add(currentNumber.ToString());
            }

            return numbers;
        }

        // Расчет совпадения сокращений (например: "ВВГ" -> "кабель ВВГ")
        private double CalculateAbbreviationMatchScore(string query, string productName)
        {
            // Извлекаем возможные аббревиатуры из запроса (слова из заглавных букв)
            var queryAbbreviations = ExtractAbbreviations(query);
            if (!queryAbbreviations.Any())
                return 0;

            // Извлекаем аббревиатуры из названия товара
            var productAbbreviations = ExtractAbbreviations(productName);
            if (!productAbbreviations.Any())
                return 0;

            // Проверяем совпадение аббревиатур
            int matchCount = 0;
            foreach (var abbr in queryAbbreviations)
            {
                if (productAbbreviations.Contains(abbr))
                {
                    matchCount++;
                }
            }

            return (double)matchCount / queryAbbreviations.Count;
        }

        // Извлечение аббревиатур (слов из заглавных букв)
        private List<string> ExtractAbbreviations(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new List<string>();

            return text.Split(new[] { ' ', '-', '_', ',', '.', '(', ')', '[', ']', '/' },
                           StringSplitOptions.RemoveEmptyEntries)
                      .Where(word => word.Length >= 2 && word.All(c => char.IsUpper(c) || char.IsDigit(c)))
                      .Select(word => word.ToUpper())
                      .ToList();
        }

        // Остальные методы остаются без изменений:
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

                var partialMatches = allProducts
                    .Where(p => p.ProductName.IndexOf(productName, StringComparison.OrdinalIgnoreCase) >= 0)
                    .Take(5)
                    .ToList();

                if (partialMatches.Any())
                {
                    return FormatMultipleProductsResult(partialMatches);
                }

                return $"❌ Товар не найден в системе: {productName}";
            }
            catch (Exception ex)
            {
                return $"❌ Ошибка поиска: {ex.Message}";
            }
        }

        // ... остальные методы остаются без изменений ...

        private string FormatProductResult(ProductWithCategoryInfo product)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"✅ НАЙДЕНО: {product.ProductName}");
            sb.AppendLine($"   Категория: {product.CategoryName}");
            sb.AppendLine($"   Цена: {(product.HasPrice ? product.Price.ToString() + " руб." : "Нет цены")}");
            sb.AppendLine($"   ID товара: {product.ProductId}");
            sb.AppendLine($"   ID категории: {product.SectionId}");
            return sb.ToString();
        }

        private string FormatMultipleProductsResult(List<ProductWithCategoryInfo> products)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"🔍 Найдено {products.Count} похожих товаров:");
            foreach (var product in products)
            {
                sb.AppendLine($"   • {product.ProductName}");
                sb.AppendLine($"     Категория: {product.CategoryName}");
            }
            return sb.ToString();
        }

        public void ClearCache()
        {
            lock (_cacheLock)
            {
                _allProductsCache = null;
            }
        }
    }
}