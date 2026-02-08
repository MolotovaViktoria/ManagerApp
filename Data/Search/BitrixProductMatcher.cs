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

        // ✅ ИСПРАВЛЕННЫЙ МЕТОД: 10 ТОВАРОВ, МИНИМУМ 3 ПОСТАВЩИКА
        public async Task<List<ProductWithCategoryInfo>> FindSimilarProductsAsync(
            string searchQuery,
            int maxResults = 10) // ✅ ФИКСИРУЕМ 10 ТОВАРОВ
        {
            if (string.IsNullOrWhiteSpace(searchQuery))
                return new List<ProductWithCategoryInfo>();

            try
            {
                // Получаем все товары
                var allProducts = await GetAllProductsAsync();

                if (allProducts == null || !allProducts.Any())
                    return new List<ProductWithCategoryInfo>();

                var query = searchQuery.Trim();
                Console.WriteLine($"🔍 Поиск: '{query}'");

                // ✅ ШАГ 1: НАХОДИМ ВСЕ ПОСТАВЩИКОВ В БАЗЕ
                var allSuppliers = allProducts
                    .Where(p => !string.IsNullOrEmpty(p.CategoryName))
                    .Select(p => p.CategoryName)
                    .Distinct()
                    .ToList();

                Console.WriteLine($"📊 Всего поставщиков в базе: {allSuppliers.Count}");

                // ✅ ШАГ 2: ДЛЯ КАЖДОГО ПОСТАВЩИКА НАХОДИМ САМЫЙ ПОХОЖИЙ ТОВАР
                var supplierBestProducts = new List<ProductWithSupplierRelevance>();

                foreach (var supplier in allSuppliers)
                {
                    var supplierProducts = allProducts
                        .Where(p => p.CategoryName == supplier)
                        .ToList();

                    // ✅ НАХОДИМ САМЫЙ ПОХОЖИЙ ТОВАР ОТ ЭТОГО ПОСТАВЩИКА
                    var bestProduct = FindBestProductForSupplier(supplierProducts, query);

                    if (bestProduct != null)
                    {
                        var relevance = CalculateDeepRelevance(bestProduct, query);
                        supplierBestProducts.Add(new ProductWithSupplierRelevance
                        {
                            Product = bestProduct,
                            Supplier = supplier,
                            Relevance = relevance
                        });

                        Console.WriteLine($"   📦 {supplier}: '{bestProduct.ProductName}' (релевантность: {relevance})");
                    }
                }

                // ✅ ШАГ 3: СОРТИРУЕМ ПО РЕЛЕВАНТНОСТИ (ОТ САМОГО ПОДХОДЯЩЕГО)
                supplierBestProducts = supplierBestProducts
                    .OrderByDescending(r => r.Relevance)
                    .ThenByDescending(p => p.Product.HasPrice)
                    .ThenBy(p => p.Product.ProductName.Length)
                    .ToList();

                Console.WriteLine($"✅ Найдено {supplierBestProducts.Count} лучших товаров от {supplierBestProducts.Select(r => r.Supplier).Distinct().Count()} поставщиков");

                // ✅ ШАГ 4: ФОРМИРУЕМ ФИНАЛЬНЫЙ СПИСОК С ГАРАНТИЕЙ 3+ ПОСТАВЩИКОВ
                var finalResults = new List<ProductWithSupplierRelevance>();
                var usedSuppliers = new HashSet<string>();
                var maxProductsPerSupplier = 3; // Максимум 3 товара от одного поставщика

                // ✅ ПРАВИЛО 1: ГАРАНТИРУЕМ МИНИМУМ 3 РАЗНЫХ ПОСТАВЩИКА
                // Берем самый релевантный товар от каждого поставщика
                foreach (var supplierGroup in supplierBestProducts.GroupBy(p => p.Supplier))
                {
                    var bestFromSupplier = supplierGroup.OrderByDescending(p => p.Relevance).First();
                    finalResults.Add(bestFromSupplier);
                    usedSuppliers.Add(supplierGroup.Key);

                    if (usedSuppliers.Count >= 3 && finalResults.Count >= 5)
                        break;
                }

                Console.WriteLine($"📦 Гарантировано {usedSuppliers.Count} поставщиков, {finalResults.Count} товаров");

                // ✅ ПРАВИЛО 2: ДОБИРАЕМ ДО 10 ТОВАРОВ, СОБЛЮДАЯ ЛИМИТ ПО ПОСТАВЩИКАМ
                foreach (var product in supplierBestProducts.Where(p => !finalResults.Contains(p)))
                {
                    if (finalResults.Count >= maxResults) // ✅ ФИКСИРОВАННЫЙ ЛИМИТ 10
                        break;

                    // Проверяем, не превысили ли лимит для этого поставщика
                    var supplierCount = finalResults.Count(r => r.Supplier == product.Supplier);
                    if (supplierCount < maxProductsPerSupplier)
                    {
                        finalResults.Add(product);
                    }
                }

                // ✅ ПРАВИЛО 3: ЕСЛИ ВСЕ РАВНО МЕНЬШЕ 3 ПОСТАВЩИКОВ - ДОБАВЛЯЕМ ЕЩЕ
                if (usedSuppliers.Count < 3)
                {
                    Console.WriteLine($"⚠️  Меньше 3 поставщиков ({usedSuppliers.Count})! Ищем дополнительные...");

                    // Ищем еще поставщиков
                    var remainingSuppliers = allSuppliers
                        .Where(s => !usedSuppliers.Contains(s))
                        .Take(3 - usedSuppliers.Count);

                    foreach (var supplier in remainingSuppliers)
                    {
                        if (finalResults.Count >= maxResults)
                            break;

                        var supplierProducts = allProducts
                            .Where(p => p.CategoryName == supplier)
                            .ToList();

                        var bestProduct = FindBestProductForSupplier(supplierProducts, query);
                        if (bestProduct != null)
                        {
                            var relevance = CalculateDeepRelevance(bestProduct, query);
                            finalResults.Add(new ProductWithSupplierRelevance
                            {
                                Product = bestProduct,
                                Supplier = supplier,
                                Relevance = relevance
                            });
                            usedSuppliers.Add(supplier);
                            Console.WriteLine($"   ➕ Дополнительный поставщик: {supplier}");
                        }
                    }
                }

                // ✅ ШАГ 5: ФИНАЛЬНАЯ СОРТИРОВКА И ОГРАНИЧЕНИЕ
                var resultProducts = finalResults
                    .OrderByDescending(r => r.Relevance) // ✅ СОРТИРОВКА ОТ САМОГО ПОДХОДЯЩЕГО
                    .ThenByDescending(p => p.Product.HasPrice)
                    .ThenBy(p => p.Product.ProductName.Length)
                    .Take(maxResults) // ✅ ТОЧНО 10 ТОВАРОВ
                    .Select(r => r.Product)
                    .ToList();

                Console.WriteLine($"🎯 ФИНАЛЬНО: {resultProducts.Count} товаров от {usedSuppliers.Count} поставщиков");

                // Выводим все результаты
                for (int i = 0; i < resultProducts.Count; i++)
                {
                    var product = resultProducts[i];
                    var supplier = product.CategoryName ?? "БЕЗ ПОСТАВЩИКА";
                    Console.WriteLine($"   {i + 1}. [{supplier}] {product.ProductName}");
                }

                // Статистика по поставщикам
                var supplierStats = resultProducts
                    .GroupBy(p => p.CategoryName ?? "БЕЗ ПОСТАВЩИКА")
                    .Select(g => new { Supplier = g.Key, Count = g.Count() })
                    .OrderByDescending(g => g.Count);

                Console.WriteLine($"📊 Статистика поставщиков в результатах:");
                foreach (var stat in supplierStats)
                {
                    Console.WriteLine($"   {stat.Supplier}: {stat.Count} товаров");
                }

                return resultProducts;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка: {ex.Message}");
                return new List<ProductWithCategoryInfo>();
            }
        }

        // ✅ МЕТОД ДЛЯ ПОИСКА САМОГО ПОХОЖЕГО ТОВАРА ОТ ПОСТАВЩИКА
        private ProductWithCategoryInfo FindBestProductForSupplier(
            List<ProductWithCategoryInfo> supplierProducts,
            string query)
        {
            if (!supplierProducts.Any())
                return null;

            var queryLower = query.ToLower();
            var searchWords = ExtractSearchWords(queryLower);

            ProductWithCategoryInfo bestProduct = null;
            int bestRelevance = -1;

            foreach (var product in supplierProducts)
            {
                var relevance = CalculateDeepRelevance(product, queryLower, searchWords);

                if (relevance > bestRelevance)
                {
                    bestRelevance = relevance;
                    bestProduct = product;
                }
            }

            return bestProduct;
        }

        // ✅ ГЛУБОКИЙ РАСЧЕТ РЕЛЕВАНТНОСТИ
        private int CalculateDeepRelevance(ProductWithCategoryInfo product, string query)
        {
            var searchWords = ExtractSearchWords(query);
            return CalculateDeepRelevance(product, query, searchWords);
        }

        private int CalculateDeepRelevance(ProductWithCategoryInfo product, string query, List<string> searchWords)
        {
            var productName = product.ProductName?.ToLower() ?? "";
            var categoryName = product.CategoryName?.ToLower() ?? "";

            int relevance = 0;

            // 1. ТОЧНОЕ СОВПАДЕНИЕ (максимальный балл)
            if (productName == query)
                relevance += 1000;

            // 2. СОДЕРЖИТ ВЕСЬ ЗАПРОС
            if (productName.Contains(query))
                relevance += 800;

            // 3. ВСЕ СЛОВА ИЗ ЗАПРОСА
            if (searchWords.All(word => productName.Contains(word)))
                relevance += 600;

            // 4. БОЛЬШИНСТВО СЛОВ
            int matchingWords = searchWords.Count(word => productName.Contains(word));
            if (matchingWords > 0)
                relevance += matchingWords * 100;

            // 5. СОВПАДЕНИЕ С УЧЕТОМ РАЗНЫХ ВАРИАНТОВ НАПИСАНИЯ
            var normalizedProductName = NormalizeText(productName);
            var normalizedQuery = NormalizeText(query);

            if (normalizedProductName.Contains(normalizedQuery))
                relevance += 400;

            // 6. СОВПАДЕНИЕ ЧИСЕЛ (сечения, диаметры)
            var queryNumbers = ExtractNumbers(query);
            var productNumbers = ExtractNumbers(productName);

            foreach (var number in queryNumbers)
            {
                if (productNumbers.Contains(number))
                    relevance += 150;
            }

            // 7. СОВПАДЕНИЕ МАРКИРОВОК (ВВГ, ППТ и т.д.)
            var queryMarkings = ExtractMarkings(query);
            var productMarkings = ExtractMarkings(productName);

            foreach (var marking in queryMarkings)
            {
                if (productMarkings.Contains(marking))
                    relevance += 200;
            }

            // 8. ДОПОЛНИТЕЛЬНЫЕ БАЛЛЫ
            if (product.HasPrice)
                relevance += 50;

            if (productName.Length < 100) // Короткие названия предпочтительнее
                relevance += 30;

            return relevance;
        }

        // ✅ НОРМАЛИЗАЦИЯ ТЕКСТА (для поиска разных вариантов написания)
        private string NormalizeText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            // Убираем пробелы, дефисы, приводим к нижнему регистру
            var normalized = text.ToLower()
                .Replace(" ", "")
                .Replace("-", "")
                .Replace("нг(а)", "нг")
                .Replace("нг(а)-", "нг")
                .Replace("нг-ls", "нгls")
                .Replace("нг(а)-ls", "нгls");

            return normalized;
        }

        // ✅ ИЗВЛЕЧЕНИЕ ПОИСКОВЫХ СЛОВ
        private List<string> ExtractSearchWords(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<string>();

            // Разбиваем на слова, убираем общие слова
            var words = query.Split(new[] { ' ', ',', '.', '-', '_', '/', '\\', '(', ')', '[', ']', 'х', '×' },
                                  StringSplitOptions.RemoveEmptyEntries)
                            .Select(p => p.Trim().ToLower())
                            .Where(p => p.Length >= 2)
                            .ToList();

            // Фильтруем общие слова
            var commonWords = new HashSet<string>
            {
                "кабель", "кабельный", "кабельная", "кабельное",
                "провод", "проводной", "провода", "проводная",
                "силовой", "монтажный", "установочный",
                "м", "мм", "кв", "квадратный", "квадрат"
            };

            return words.Where(w => !commonWords.Contains(w)).ToList();
        }

        // ✅ ИЗВЛЕЧЕНИЕ МАРКИРОВОК (ВВГ, ППТ, АВВГ и т.д.)
        private List<string> ExtractMarkings(string text)
        {
            var markings = new List<string>();

            // Ищем маркировки типа ВВГ, ППТ, АВВГ (2-4 заглавные буквы подряд)
            var markingMatches = System.Text.RegularExpressions.Regex.Matches(text.ToUpper(), @"[А-ЯЁ]{2,4}");
            foreach (System.Text.RegularExpressions.Match match in markingMatches)
            {
                markings.Add(match.Value);
            }

            return markings.Distinct().ToList();
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

            // Последнее число
            if (currentNumber.Length > 0)
            {
                numbers.Add(currentNumber.ToString());
            }

            return numbers;
        }

        // Вспомогательный класс для хранения товара с релевантностью
        private class ProductWithSupplierRelevance
        {
            public ProductWithCategoryInfo Product { get; set; }
            public string Supplier { get; set; }
            public int Relevance { get; set; }
        }

        // Остальные методы остаются без изменений
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
    }
}