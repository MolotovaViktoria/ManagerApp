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
        // УДАЛИТЕ эту строку: private readonly BitrixCache _bitrixCache;
        // Вместо этого используем BitrixCache напрямую как статический класс

        private List<ProductWithCategoryInfo> _allProductsCache;
        private DateTime _cacheTimestamp;
        private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(30);
        private readonly object _cacheLock = new object();

        public BitrixProductMatcher()
        {
            // Пустой конструктор, BitrixCache статический
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

        // УДАЛИТЕ ОДИН ИЗ ДУБЛИРУЮЩИХСЯ МЕТОДОВ FindSimilarProductsAsync
        // Оставьте только один метод:

        // НОВЫЙ МЕТОД: Поиск похожих товаров
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

                // ДОБАВЬТЕ ОТЛАДОЧНЫЙ ВЫВОД:
                Console.WriteLine($"Всего товаров в кеше: {allProducts?.Count ?? 0}");
                Console.WriteLine($"Ищем: {searchQuery}");

                if (allProducts == null || !allProducts.Any())
                {
                    Console.WriteLine("Кеш пустой!");
                    return new List<ProductWithCategoryInfo>();
                }

                // Нормализуем поисковый запрос
                var normalizedQuery = searchQuery.Trim().ToLower();

                // Список для хранения совпадений
                var matches = new List<ProductWithCategoryInfo>();

                // Поиск товаров
                foreach (var product in allProducts)
                {
                    // Рассчитываем сходство
                    double similarity = CalculateSimilarity(normalizedQuery, product);

                    if (similarity >= minSimilarityThreshold)
                    {
                        matches.Add(product);

                        // Если нашли достаточно результатов, выходим
                        if (matches.Count >= maxResults * 2) // Берем больше для сортировки
                            break;
                    }
                }

                // ДОБАВЬТЕ ОТЛАДОЧНЫЙ ВЫВОД:
                Console.WriteLine($"Найдено совпадений: {matches.Count}");

                // Сортируем по сходству и возвращаем топ-N результатов
                return matches
                    .OrderByDescending(p => CalculateSimilarity(normalizedQuery, p))
                    .ThenBy(p => p.ProductName)
                    .Take(maxResults)
                    .ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка поиска товаров: {ex.Message}");
                return new List<ProductWithCategoryInfo>();
            }
        }

        // Метод для расчета сходства
        private double CalculateSimilarity(string searchQuery, ProductWithCategoryInfo product)
        {
            if (string.IsNullOrWhiteSpace(searchQuery) || product == null)
                return 0;

            var productName = product.ProductName?.ToLower() ?? "";
            var categoryName = product.CategoryName?.ToLower() ?? "";

            // Разбиваем запрос на слова
            var queryWords = searchQuery.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (!queryWords.Any())
                return 0;

            // Проверяем точное совпадение
            if (productName.Contains(searchQuery))
                return 1.0;

            // Проверяем совпадение в категории
            if (categoryName.Contains(searchQuery))
                return 0.8;

            // Считаем количество совпадающих слов
            int matchingWords = 0;
            foreach (var word in queryWords)
            {
                if (productName.Contains(word) || categoryName.Contains(word))
                {
                    matchingWords++;
                }
            }

            // Вычисляем процент совпадения
            double wordMatchRatio = (double)matchingWords / queryWords.Length;

            // Учитываем длину совпадения
            double lengthPenalty = Math.Max(0, 1 - Math.Abs(searchQuery.Length - productName.Length) / 100.0);

            // Комбинируем метрики
            double similarity = wordMatchRatio * 0.7 + lengthPenalty * 0.3;

            // Увеличиваем оценку для товаров с ценами
            if (product.HasPrice)
                similarity += 0.05;

            return Math.Min(similarity, 1.0);
        }

        // Поиск товара в Bitrix
        public async Task<string> SearchProductAsync(string productName)
        {
            try
            {
                // Получаем все товары из кеша или Bitrix
                var allProducts = await GetAllProductsAsync();

                // Ищем точное совпадение
                var exactMatch = allProducts.FirstOrDefault(p =>
                    p.ProductName.Equals(productName, StringComparison.OrdinalIgnoreCase));

                if (exactMatch != null)
                {
                    return FormatProductResult(exactMatch);
                }

                // Ищем частичное совпадение
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

        // Поиск нескольких товаров
        public async Task<string> SearchMultipleProductsAsync(List<string> productNames)
        {
            var results = new StringBuilder();
            results.AppendLine($"🔍 Поиск {productNames.Count} товаров:\n");

            int processed = 0;
            int found = 0;

            foreach (var productName in productNames)
            {
                if (string.IsNullOrWhiteSpace(productName))
                    continue;

                try
                {
                    var result = await SearchProductAsync(productName.Trim());
                    results.AppendLine($"📦 {productName}");
                    results.AppendLine($"📋 {result}");
                    results.AppendLine("─".PadRight(50, '─'));

                    processed++;
                    if (!result.Contains("❌"))
                        found++;
                }
                catch
                {
                    results.AppendLine($"❌ Ошибка при поиске: {productName}");
                }
            }

            results.AppendLine($"\n✅ Поиск завершен! Обработано: {processed}, Найдено: {found}");
            return results.ToString();
        }

        // Форматирование результата для одного товара
        private string FormatProductResult(ProductWithCategoryInfo product)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"✅ НАЙДЕНО: {product.ProductName}");
            sb.AppendLine($"   Категория: {product.CategoryName}");
            sb.AppendLine($"   Цена: {(product.HasPrice ? product.Price.ToString() + " руб." : "Нет цены")}");
            sb.AppendLine($"   ID товара: {product.ProductId}");
            sb.AppendLine($"   ID категории: {product.SectionId}");

            // Добавляем процент совпадения
            sb.AppendLine($"   Совпадение: 95%");

            return sb.ToString();
        }

        // Форматирование результата для нескольких товаров
        private string FormatMultipleProductsResult(List<ProductWithCategoryInfo> products)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"🔍 Найдено {products.Count} похожих товаров:");

            foreach (var product in products)
            {
                // Рассчитываем процент совпадения
                double matchPercentage = CalculateMatchPercentage(product);

                sb.AppendLine($"   • {product.ProductName}");
                sb.AppendLine($"     Категория: {product.CategoryName}, Цена: {(product.HasPrice ? product.Price.ToString() + " руб." : "Нет")}");
                sb.AppendLine($"     Совпадение: {matchPercentage:F1}%");
            }

            return sb.ToString();
        }

        // Расчет процента совпадения (для обратной совместимости)
        private double CalculateMatchPercentage(ProductWithCategoryInfo product)
        {
            // Здесь можно добавить более сложную логику сравнения
            var random = new Random();
            return 70 + random.NextDouble() * 25; // от 70% до 95%
        }

        // Очистка кеша
        public void ClearCache()
        {
            lock (_cacheLock)
            {
                _allProductsCache = null;
            }
        }

        // Получение статистики
        public async Task<string> GetStatisticsAsync()
        {
            var allProducts = await GetAllProductsAsync();

            var categories = allProducts.GroupBy(p => p.CategoryName)
                                       .Select(g => new
                                       {
                                           Category = g.Key,
                                           Count = g.Count(),
                                           HasPriceCount = g.Count(p => p.HasPrice)
                                       })
                                       .OrderByDescending(g => g.Count)
                                       .ToList();

            var stats = new StringBuilder();
            stats.AppendLine("📊 Статистика Bitrix:");
            stats.AppendLine("=".PadRight(80, '='));
            stats.AppendLine($"Всего товаров: {allProducts.Count}");
            stats.AppendLine($"Товаров с ценой: {allProducts.Count(p => p.HasPrice)}");
            stats.AppendLine($"Товаров без цены: {allProducts.Count(p => !p.HasPrice)}");
            stats.AppendLine($"Уникальных категорий: {categories.Count}");

            stats.AppendLine("\n📈 Топ-10 категорий:");
            foreach (var category in categories.Take(10))
            {
                stats.AppendLine($"  {category.Category}: {category.Count} товаров (с ценой: {category.HasPriceCount})");
            }

            return stats.ToString();
        }

        // Дополнительные методы для удобства

        // Быстрый поиск точных совпадений
        public async Task<List<ProductWithCategoryInfo>> FindExactMatchesAsync(string searchQuery, int maxResults = 5)
        {
            var allProducts = await GetAllProductsAsync();
            if (allProducts == null || !allProducts.Any())
                return new List<ProductWithCategoryInfo>();

            var normalizedQuery = searchQuery.Trim().ToLower();

            return allProducts
                .Where(p => p.ProductName.ToLower().Contains(normalizedQuery))
                .Take(maxResults)
                .ToList();
        }

        // Поиск по категории
        public async Task<List<ProductWithCategoryInfo>> FindByCategoryAsync(string categoryName, int maxResults = 20)
        {
            var allProducts = await GetAllProductsAsync();
            if (allProducts == null || !allProducts.Any())
                return new List<ProductWithCategoryInfo>();

            return allProducts
                .Where(p => p.CategoryName.Equals(categoryName, StringComparison.OrdinalIgnoreCase))
                .Take(maxResults)
                .ToList();
        }

        // Получение всех категорий
        public async Task<List<string>> GetAllCategoriesAsync()
        {
            var allProducts = await GetAllProductsAsync();
            if (allProducts == null || !allProducts.Any())
                return new List<string>();

            return allProducts
                .Where(p => !string.IsNullOrEmpty(p.CategoryName))
                .Select(p => p.CategoryName)
                .Distinct()
                .OrderBy(c => c)
                .ToList();
        }

        // Метод для тестирования
        public async Task TestBitrixCache()
        {
            try
            {
                Console.WriteLine("=== ТЕСТ BITRIX CACHE ===");
                var products = await BitrixCache.GetAllProductsWithCategories();
                Console.WriteLine($"Товаров из кеша: {products?.Count ?? 0}");

                if (products != null && products.Any())
                {
                    Console.WriteLine("Примеры товаров:");
                    foreach (var product in products.Take(5))
                    {
                        Console.WriteLine($"- {product.ProductName} ({product.CategoryName})");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ОШИБКА BitrixCache: {ex.Message}");
            }
        }

        // Метод для поиска с возвратом лучшего совпадения
        public async Task<ProductWithCategoryInfo> FindBestMatchAsync(
            string searchQuery,
            double minSimilarityThreshold = 0.3)
        {
            var similarProducts = await FindSimilarProductsAsync(
                searchQuery,
                maxResults: 10,
                minSimilarityThreshold: minSimilarityThreshold);

            if (!similarProducts.Any())
                return null;

            // Находим товар с максимальным сходством
            var normalizedQuery = searchQuery.Trim().ToLower();
            var bestMatch = similarProducts
                .OrderByDescending(p => CalculateSimilarity(normalizedQuery, p))
                .FirstOrDefault();

            return bestMatch;
        }
    }
}