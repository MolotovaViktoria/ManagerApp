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

        // ОСНОВНОЙ МЕТОД ПОИСКА - МАКСИМАЛЬНО ПРОСТОЙ
        public async Task<List<ProductWithCategoryInfo>> FindSimilarProductsAsync(
            string searchQuery,
            int maxResults = 5)
        {
            if (string.IsNullOrWhiteSpace(searchQuery))
                return new List<ProductWithCategoryInfo>();

            try
            {
                // Получаем все товары
                var allProducts = await GetAllProductsAsync();

                if (allProducts == null || !allProducts.Any())
                    return new List<ProductWithCategoryInfo>();

                var query = searchQuery.Trim().ToLower();
                Console.WriteLine($"🔍 Поиск: '{query}'");

                // Разбиваем запрос на важные части
                var searchParts = ExtractSearchParts(query);

                if (!searchParts.Any())
                    return new List<ProductWithCategoryInfo>();

                Console.WriteLine($"Поисковые части: [{string.Join(", ", searchParts)}]");

                // 1. Ищем товары по разным стратегиям
                var allMatches = new HashSet<ProductWithCategoryInfo>();

                // Стратегия 1: Точное совпадение
                var exactMatches = allProducts
                    .Where(p => p.ProductName?.ToLower().Contains(query) == true)
                    .Take(10);
                AddToSet(allMatches, exactMatches);

                // Если нашли точные совпадения - возвращаем их
                if (allMatches.Count >= 3)
                {
                    return allMatches.Take(maxResults).ToList();
                }

                // Стратегия 2: По всем частям запроса
                foreach (var product in allProducts)
                {
                    var productName = product.ProductName?.ToLower() ?? "";
                    var categoryName = product.CategoryName?.ToLower() ?? "";
                    var fullText = productName + " " + categoryName;

                    // Считаем совпадения
                    int matchCount = 0;
                    foreach (var part in searchParts)
                    {
                        if (fullText.Contains(part))
                        {
                            matchCount++;
                        }
                    }

                    // Если нашли хотя бы 50% частей
                    if (matchCount >= (searchParts.Count / 2) + 1)
                    {
                        allMatches.Add(product);
                        if (allMatches.Count >= maxResults * 3)
                            break;
                    }
                }

                // Стратегия 3: По первым двум частям (самые важные)
                if (allMatches.Count < maxResults && searchParts.Count >= 2)
                {
                    var firstTwoParts = searchParts.Take(2).ToList();

                    foreach (var product in allProducts.Where(p => !allMatches.Contains(p)))
                    {
                        var productName = product.ProductName?.ToLower() ?? "";

                        bool hasFirst = firstTwoParts.Count > 0 && productName.Contains(firstTwoParts[0]);
                        bool hasSecond = firstTwoParts.Count > 1 && productName.Contains(firstTwoParts[1]);

                        if (hasFirst && hasSecond)
                        {
                            allMatches.Add(product);
                            if (allMatches.Count >= maxResults * 3)
                                break;
                        }
                    }
                }

                // Стратегия 4: По числам (размеры, диаметры и т.д.)
                if (allMatches.Count < maxResults)
                {
                    var numbersInQuery = ExtractNumbers(query);
                    if (numbersInQuery.Any())
                    {
                        foreach (var product in allProducts.Where(p => !allMatches.Contains(p)))
                        {
                            var productName = product.ProductName?.ToLower() ?? "";
                            var numbersInProduct = ExtractNumbers(productName);

                            // Если есть совпадение чисел
                            if (numbersInQuery.Any(q => numbersInProduct.Contains(q)))
                            {
                                allMatches.Add(product);
                                if (allMatches.Count >= maxResults * 3)
                                    break;
                            }
                        }
                    }
                }

                // 2. СОРТИРУЕМ И ВОЗВРАЩАЕМ
                var sortedResults = allMatches
                    .OrderByDescending(p => CalculateRelevance(p, searchParts)) // Сначала самые релевантные
                    .ThenByDescending(p => p.HasPrice) // С ценами выше
                    .ThenBy(p => p.ProductName?.Length ?? int.MaxValue) // Короткие названия выше
                    .Take(maxResults) // Только потом обрезаем
                    .ToList();

                Console.WriteLine($"✅ Найдено: {sortedResults.Count} товаров");

                if (sortedResults.Any())
                {
                    Console.WriteLine("Лучшие результаты:");
                    for (int i = 0; i < Math.Min(3, sortedResults.Count); i++)
                    {
                        Console.WriteLine($"  {i + 1}. {sortedResults[i].ProductName}");
                    }
                }

                return sortedResults;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка: {ex.Message}");
                return new List<ProductWithCategoryInfo>();
            }
        }

        // Извлечение значимых частей из запроса
        private List<string> ExtractSearchParts(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<string>();

            // Просто разбиваем по всем разделителям
            var parts = query.Split(new[] { ' ', ',', '.', '-', '_', '/', '\\', '(', ')', '[', ']' },
                                 StringSplitOptions.RemoveEmptyEntries)
                           .Select(p => p.Trim().ToLower())
                           .Where(p => p.Length >= 2) // Не берем слишком короткие
                           .ToList();

            // Фильтруем совсем общие слова
            var commonWords = new HashSet<string>
            {
                "и", "в", "на", "с", "по", "для", "из", "от", "до",
                "шт", "уп", "комплект", "набор", "метров", "штук"
            };

            return parts.Where(p => !commonWords.Contains(p)).ToList();
        }

        // Простой расчет релевантности
        private int CalculateRelevance(ProductWithCategoryInfo product, List<string> searchParts)
        {
            var productName = product.ProductName?.ToLower() ?? "";
            var categoryName = product.CategoryName?.ToLower() ?? "";
            var fullText = productName + " " + categoryName;

            int relevance = 0;

            // 1. За каждую совпавшую часть +10 баллов
            foreach (var part in searchParts)
            {
                if (productName.Contains(part)) relevance += 10;
                else if (categoryName.Contains(part)) relevance += 5;
            }

            // 2. Бонус за совпадение всех частей
            bool hasAllParts = searchParts.All(p => fullText.Contains(p));
            if (hasAllParts) relevance += 30;

            // 3. Бонус за совпадение первых двух частей
            if (searchParts.Count >= 2)
            {
                bool hasFirst = productName.Contains(searchParts[0]);
                bool hasSecond = productName.Contains(searchParts[1]);

                if (hasFirst && hasSecond) relevance += 20;
                else if (hasFirst) relevance += 10;
                else if (hasSecond) relevance += 10;
            }

            // 4. Бонусы
            if (product.HasPrice) relevance += 5;
            if (productName.Length < 60) relevance += 3; // Короткие названия лучше

            return relevance;
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
                if (char.IsDigit(c))
                {
                    currentNumber.Append(c);
                }
                else if (currentNumber.Length > 0)
                {
                    numbers.Add(currentNumber.ToString());
                    currentNumber.Clear();
                }
            }

            // Последнее число
            if (currentNumber.Length > 0)
            {
                numbers.Add(currentNumber.ToString());
            }

            return numbers;
        }

        // Добавление в множество с проверкой дубликатов
        private void AddToSet(HashSet<ProductWithCategoryInfo> set, IEnumerable<ProductWithCategoryInfo> items)
        {
            foreach (var item in items)
            {
                set.Add(item);
            }
        }

        // БЫСТРЫЙ ПОИСК ДЛЯ КОМБОБОКСОВ
        public async Task<List<ProductWithCategoryInfo>> QuickSearchAsync(string searchQuery, int maxResults = 5)
        {
            try
            {
                var allProducts = await GetAllProductsAsync();

                if (allProducts == null || !allProducts.Any())
                    return new List<ProductWithCategoryInfo>();

                var query = searchQuery.Trim().ToLower();

                // Простой поиск по вхождению слов
                var words = ExtractSearchParts(query);

                if (!words.Any())
                    return new List<ProductWithCategoryInfo>();

                var results = allProducts
                    .Where(p =>
                    {
                        var name = p.ProductName?.ToLower() ?? "";
                        return words.Any(w => name.Contains(w));
                    })
                    .OrderByDescending(p => p.HasPrice)
                    .ThenBy(p => p.ProductName?.Length ?? int.MaxValue)
                    .Take(maxResults)
                    .ToList();

                return results;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка быстрого поиска: {ex.Message}");
                return new List<ProductWithCategoryInfo>();
            }
        }

        // Остальные методы остаются без изменений
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
    }
}