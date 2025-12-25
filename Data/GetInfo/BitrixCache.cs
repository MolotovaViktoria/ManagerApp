using ManagerApp.Data.StructureList;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ManagerApp.Data.GetInfo
{
    public static class BitrixCache
    {
        private static BitrixService _bitrixService = new BitrixService();
        private static bool _isInitialized = false;
        private static readonly object _lockObject = new object();

        // Основные кэши
        private static List<ProductWithCategoryInfo> _allProductsWithCategories = null;
        private static List<Product> _allProductsSimple = null;
        private static DateTime _lastCacheUpdate = DateTime.MinValue;
        private static readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(30);

        // Словари для быстрого поиска
        private static readonly ConcurrentDictionary<string, int> _productIdByName =
            new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<int, ProductWithCategoryInfo> _productById =
            new ConcurrentDictionary<int, ProductWithCategoryInfo>();
        private static readonly ConcurrentDictionary<string, List<ProductWithCategoryInfo>> _productsByNameSearch =
            new ConcurrentDictionary<string, List<ProductWithCategoryInfo>>();

        // Кэш по категориям
        private static readonly Dictionary<string, List<ProductWithCategoryInfo>> _productsByCategory =
            new Dictionary<string, List<ProductWithCategoryInfo>>();

        // Кэш по нижним разделам
        private static readonly ConcurrentDictionary<string, List<ProductWithLowerSection>> _productsByLowerSection =
            new ConcurrentDictionary<string, List<ProductWithLowerSection>>();

        // ============ ПУБЛИЧНЫЕ МЕТОДЫ ============

        public static async Task<bool> InitializeAsync()
        {
            if (_isInitialized)
                return true;

            lock (_lockObject)
            {
                if (_isInitialized)
                    return true;
            }

            try
            {
                Console.WriteLine("[BitrixCache] Начало инициализации кэша...");

                // Загружаем основные данные
                //Console.WriteLine("[BitrixCache] Загрузка категорий...");
                //var categories = await _bitrixService.GetСategories();
                //Console.WriteLine($"[BitrixCache] Загружено {categories?.Count ?? 0} категорий");

                Console.WriteLine("[BitrixCache] Загрузка товаров с категориями...");
                var products = await _bitrixService.GetProductsWithCategoryInfo();

                if (products == null || !products.Any())
                {
                    Console.WriteLine("[BitrixCache] Предупреждение: не удалось загрузить товары");
                    return false;
                }

                _allProductsWithCategories = products;
                _lastCacheUpdate = DateTime.Now;

                Console.WriteLine("[BitrixCache] Загрузка простых товаров...");
                //_allProductsSimple = await _bitrixService.GetProducts();

                // Обновляем словари
                UpdateSearchDictionaries(products);

                lock (_lockObject)
                {
                    _isInitialized = true;
                }

                Console.WriteLine($"[BitrixCache] Кэш успешно загружен. Товаров: {_productIdByName.Count}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BitrixCache] Ошибка инициализации: {ex.Message}");
                ClearCache(); // Полностью очищаем при ошибке
                return false;
            }
        }

        // Получить все товары с категориями
        public static async Task<List<ProductWithCategoryInfo>> GetAllProductsWithCategories()
        {
            try
            {
                // Проверяем, нужно ли обновлять кэш
                bool needsUpdate = _allProductsWithCategories == null ||
                                   (DateTime.Now - _lastCacheUpdate) > _cacheDuration;

                if (needsUpdate)
                {
                    Console.WriteLine("[BitrixCache] Обновление кэша товаров...");
                    _allProductsWithCategories = await _bitrixService.GetProductsWithCategoryInfo();
                    _lastCacheUpdate = DateTime.Now;

                    // Обновляем словари быстрого поиска
                    if (_allProductsWithCategories != null)
                    {
                        UpdateSearchDictionaries(_allProductsWithCategories);
                        Console.WriteLine($"[BitrixCache] Кэш обновлен: {_allProductsWithCategories.Count} товаров");
                    }
                }

                return _allProductsWithCategories ?? new List<ProductWithCategoryInfo>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BitrixCache] Ошибка получения товаров с категориями: {ex.Message}");
                return new List<ProductWithCategoryInfo>();
            }
        }

        // Получить все товары (простая версия)
        public static async Task<List<Product>> GetAllProductsSimple()
        {
            try
            {
                if (_allProductsSimple == null)
                {
                    _allProductsSimple = await _bitrixService.GetProducts();
                }
                return _allProductsSimple ?? new List<Product>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BitrixCache] Ошибка получения простых товаров: {ex.Message}");
                return new List<Product>();
            }
        }
        public static async Task<List<ProductWithLowerSection>> GetAllProductsWithLowerSections()
        {
            try
            {
                // Получаем все товары с категориями
                var allProducts = await GetAllProductsWithCategories();
                if (allProducts == null || !allProducts.Any())
                    return new List<ProductWithLowerSection>();

                // Используем кэшированные категории
                var categories = await _bitrixService.GetСategories();
                var categoryDict = categories.ToDictionary(c => c.SelectionId, c => c);

                var result = new List<ProductWithLowerSection>();

                foreach (var product in allProducts)
                {
                    try
                    {
                        string lowerSectionName = "Без категории";
                        string lowerSectionId = null;
                        string fullPath = "Без категории";

                        if (!string.IsNullOrEmpty(product.SectionId) &&
                            categoryDict.TryGetValue(product.SectionId, out var category))
                        {
                            lowerSectionName = category.Name;
                            lowerSectionId = category.SelectionId;
                            fullPath = BuildCategoryPath(category.SelectionId, categoryDict);
                        }

                        result.Add(new ProductWithLowerSection
                        {
                            ProductId = product.ProductId,
                            ProductName = product.ProductName,
                            LowerSectionId = lowerSectionId,
                            LowerSectionName = lowerSectionName,
                            CategoryPath = fullPath,
                            Price = product.Price,
                            HasPrice = product.HasPrice,
                            Code = product.ProductCode ?? "",
                            Measure = ""
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[BitrixCache] Ошибка преобразования товара {product.ProductId}: {ex.Message}");
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BitrixCache] Ошибка получения товаров с нижними разделами: {ex.Message}");
                return new List<ProductWithLowerSection>();
            }
        }

        // Обновите метод BuildCategoryPath в BitrixCache:
        private static string BuildCategoryPath(string categoryId, Dictionary<string, Category> categoryDict)
        {
            if (string.IsNullOrEmpty(categoryId) || !categoryDict.ContainsKey(categoryId))
                return "Без категории";

            var pathParts = new List<string>();
            var currentId = categoryId;
            var visited = new HashSet<string>();

            while (!string.IsNullOrEmpty(currentId) && categoryDict.TryGetValue(currentId, out var category))
            {
                // Защита от циклических ссылок
                if (visited.Contains(currentId))
                    break;
                visited.Add(currentId);

                pathParts.Insert(0, category.Name);
                currentId = category.ParentId;

                if (pathParts.Count > 10)
                    break;
            }

            return pathParts.Count > 0 ? string.Join(" → ", pathParts) : "Без категории";
        }


        // Получить ID товара по названию (быстрый поиск из кэша)
        public static int GetProductIdByName(string productName)
        {
            if (string.IsNullOrWhiteSpace(productName))
                return 0;

            var normalizedName = productName.Trim();

            // Прямой поиск
            if (_productIdByName.TryGetValue(normalizedName, out int productId))
                return productId;

            // Частичный поиск
            var foundProducts = FindProductsByPartialName(normalizedName);
            var firstProduct = foundProducts.FirstOrDefault();

            if (firstProduct != null && int.TryParse(firstProduct.ProductId, out int id) && id > 0)
                return id;

            return 0;
        }
       

        // Найти товары по частичному названию
        public static List<ProductWithCategoryInfo> FindProductsByPartialName(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return new List<ProductWithCategoryInfo>();

            var normalizedSearch = searchTerm.Trim().ToLower();
            var foundProducts = new HashSet<ProductWithCategoryInfo>();

            // Разбиваем на слова для поиска в индексе
            var searchWords = normalizedSearch.Split(new[] { ' ', ',', '.', '-', '_' },
                StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 2);

            foreach (var word in searchWords)
            {
                if (_productsByNameSearch.TryGetValue(word, out var products))
                {
                    foreach (var product in products)
                        foundProducts.Add(product);
                }
            }

            // Фильтруем по точному совпадению
            return foundProducts
                .Where(p => p.ProductName?.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0 ||
                           normalizedSearch.IndexOf(p.ProductName?.ToLower() ?? "", StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
        }

        // Обновить кэш принудительно
        public static async Task RefreshCacheAsync()
        {
            try
            {
                Console.WriteLine("[BitrixCache] Принудительное обновление кэша...");

                // Сбрасываем кэши
                _allProductsWithCategories = null;
                _allProductsSimple = null;
                _productsByCategory.Clear();
                _productsByLowerSection.Clear();
                _productIdByName.Clear();
                _productById.Clear();
                _productsByNameSearch.Clear();

                // Перезагружаем данные
                await GetAllProductsWithCategories();

                Console.WriteLine("[BitrixCache] Кэш успешно обновлен");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BitrixCache] Ошибка обновления кэша: {ex.Message}");
            }
        }

        // Очистить кэш
        public static void ClearCache()
        {
            _allProductsWithCategories = null;
            _allProductsSimple = null;
            _productsByCategory.Clear();
            _productsByLowerSection.Clear();
            _productIdByName.Clear();
            _productById.Clear();
            _productsByNameSearch.Clear();
            _isInitialized = false;
            _lastCacheUpdate = DateTime.MinValue;
        }

        // Проверка готовности кэша
        public static bool IsCacheReady()
        {
            return _isInitialized && _allProductsWithCategories != null && _allProductsWithCategories.Any();
        }

        // Статистика кэша
        public static string GetCacheStats()
        {
            return $"Кэш: {_productIdByName.Count} товаров, " +
                   $"индекс: {_productsByNameSearch.Count} слов, " +
                   $"готов: {IsCacheReady()}";
        }

        // ============ ПРИВАТНЫЕ МЕТОДЫ ============

        private static void UpdateSearchDictionaries(List<ProductWithCategoryInfo> products)
        {
            _productIdByName.Clear();
            _productById.Clear();
            _productsByNameSearch.Clear();

            foreach (var product in products)
            {
                if (string.IsNullOrEmpty(product.ProductName) || string.IsNullOrEmpty(product.ProductId))
                    continue;

                try
                {
                    // ID товара
                    if (int.TryParse(product.ProductId, out int productId) && productId > 0)
                    {
                        // Название -> ID
                        _productIdByName.TryAdd(product.ProductName.Trim(), productId);

                        // ID -> Объект товара
                        _productById.TryAdd(productId, product);

                        // Индекс для поиска по словам
                        var words = product.ProductName.ToLower()
                            .Split(new[] { ' ', ',', '.', '-', '_' }, StringSplitOptions.RemoveEmptyEntries)
                            .Where(w => w.Length > 2);

                        foreach (var word in words)
                        {
                            _productsByNameSearch.AddOrUpdate(
                                word,
                                new List<ProductWithCategoryInfo> { product },
                                (key, existingList) =>
                                {
                                    if (!existingList.Contains(product))
                                        existingList.Add(product);
                                    return existingList;
                                });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[BitrixCache] Ошибка обновления словаря для товара {product.ProductId}: {ex.Message}");
                }
            }
        }

        private static string BuildCategoryPath(string categoryId, Dictionary<string, Category> categoryDict, string parentId)
        {
            var pathParts = new List<string>();
            var currentId = categoryId;

            // Строим путь от текущей категории к корню
            while (!string.IsNullOrEmpty(currentId) && categoryDict.TryGetValue(currentId, out var category))
            {
                pathParts.Insert(0, category.Name);
                currentId = category.ParentId;

                // Защита от возможного зацикливания
                if (pathParts.Count > 10)
                    break;
            }

            return pathParts.Count > 0 ? string.Join(" → ", pathParts) : "Без категории";
        }
    }
}