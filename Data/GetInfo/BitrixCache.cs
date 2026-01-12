using ManagerApp.Data.StructureList;
using ManagerApp.Pages;
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
        private static bool _isBackgroundUpdateInProgress = false;

        // Основные кэши
        private static List<ProductWithCategoryInfo> _allProductsWithCategories = null;
        private static List<BitrixMeasure> _allMeasures = null;
        private static List<Product> _allProductsSimple = null;

        // Информация о кеше
        private static CacheInfo _cacheInfo = null;

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

        // Основной метод инициализации
        public static async Task<bool> InitializeAsync(bool forceUpdate = false)
        {
            if (_isInitialized && !forceUpdate)
                return true;

            lock (_lockObject)
            {
                if (_isInitialized && !forceUpdate)
                    return true;
            }

            try
            {
                Console.WriteLine("[BitrixCache] Начало инициализации кэша...");

                // Загружаем информацию о кеше
                _cacheInfo = await CacheFileManager.LoadCacheInfo();
                Console.WriteLine($"[BitrixCache] Последнее обновление кеша: {_cacheInfo.LastCacheUpdate}");

                // Определяем, нужно ли обновлять кеш
                bool needsUpdate = forceUpdate ||
                                  _cacheInfo.LastCacheUpdate == DateTime.MinValue ||
                                  CacheFileManager.ShouldUpdateCache(_cacheInfo.LastCacheUpdate);

                // Запускаем фоновое обновление если нужно (но не при принудительном обновлении)
                if (needsUpdate && !forceUpdate)
                {
                    Console.WriteLine("[BitrixCache] Кеш устарел, запуск фонового обновления...");
                    _ = BackgroundUpdateCacheAsync(); // Запускаем фоновое обновление
                }

                // Пытаемся загрузить из файлового кеша
                bool loadedFromCache = await LoadFromFileCache();

                if (!loadedFromCache || forceUpdate)
                {
                    Console.WriteLine("[BitrixCache] Загрузка из файлового кеша не удалась или требуется принудительное обновление");

                    // Загружаем из Bitrix
                    await LoadFromBitrixAndSaveToCache();

                    _cacheInfo.IsFirstRun = false;
                    _cacheInfo.LastCacheUpdate = DateTime.Now;

                    // Сохраняем обновленную информацию о кеше
                    await CacheFileManager.SaveCacheInfo(_cacheInfo);
                }
                else
                {
                    Console.WriteLine("[BitrixCache] Кеш успешно загружен из файла");
                }

                // Обновляем словари быстрого поиска
                UpdateSearchDictionaries(_allProductsWithCategories);

                lock (_lockObject)
                {
                    _isInitialized = true;
                }

                Console.WriteLine($"[BitrixCache] Кэш успешно загружен. Товаров: {_allProductsWithCategories?.Count ?? 0}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BitrixCache] Ошибка инициализации: {ex.Message}");
                ClearCache(); // Полностью очищаем при ошибке
                return false;
            }
        }

        // Фоновое обновление кеша
        private static async Task BackgroundUpdateCacheAsync()
        {
            if (_isBackgroundUpdateInProgress)
                return;

            _isBackgroundUpdateInProgress = true;

            try
            {
                Console.WriteLine("[BitrixCache] Начало фонового обновления кеша...");

                // Сохраняем текущие данные на случай ошибки
                var oldProducts = _allProductsWithCategories;
                var oldMeasures = _allMeasures;

                // Загружаем новые данные из Bitrix
                var productsTask = _bitrixService.GetProductsWithCategoryInfo();
                var measuresTask = _bitrixService.GetMeasuresAsync();

                await Task.WhenAll(productsTask, measuresTask);

                var newProducts = await productsTask;
                var newMeasures = await measuresTask;

                if (newProducts != null && newProducts.Any() && newMeasures != null && newMeasures.Any())
                {
                    lock (_lockObject)
                    {
                        _allProductsWithCategories = newProducts;
                        _allMeasures = newMeasures;

                        _cacheInfo.LastCacheUpdate = DateTime.Now;
                        _cacheInfo.ProductsCount = newProducts.Count;
                        _cacheInfo.MeasuresCount = newMeasures.Count;
                    }

                    // Сохраняем в файловый кеш
                    await CacheFileManager.SaveProductsToFile(newProducts);
                    await CacheFileManager.SaveMeasuresToFile(newMeasures);
                    await CacheFileManager.SaveCacheInfo(_cacheInfo);

                    // Обновляем словари
                    UpdateSearchDictionaries(newProducts);

                    Console.WriteLine($"[BitrixCache] Фоновое обновление завершено. Обновлено {newProducts.Count} товаров и {newMeasures.Count} единиц измерения");
                }
                else
                {
                    Console.WriteLine("[BitrixCache] Не удалось загрузить новые данные, оставляем старый кеш");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BitrixCache] Ошибка фонового обновления: {ex.Message}");
                // При ошибке оставляем старый кеш
            }
            finally
            {
                _isBackgroundUpdateInProgress = false;
            }
        }

        // Принудительное обновление кеша
        public static async Task ForceUpdateCacheAsync()
        {
            Console.WriteLine("[BitrixCache] Принудительное обновление кеша...");
            await InitializeAsync(true);
        }

        // Получить все товары с категориями
        public static async Task<List<ProductWithCategoryInfo>> GetAllProductsWithCategories()
        {
            try
            {
                if (!_isInitialized)
                {
                    await InitializeAsync();
                }

                return _allProductsWithCategories ?? new List<ProductWithCategoryInfo>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BitrixCache] Ошибка получения товаров с категориями: {ex.Message}");
                return new List<ProductWithCategoryInfo>();
            }
        }

        // Получить все единицы измерения
        public static async Task<List<BitrixMeasure>> GetAllMeasures()
        {
            try
            {
                if (!_isInitialized)
                {
                    await InitializeAsync();
                }

                return _allMeasures ?? new List<BitrixMeasure>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BitrixCache] Ошибка получения единиц измерения: {ex.Message}");
                return new List<BitrixMeasure>();
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
                var allProducts = await GetAllProductsWithCategories();
                if (allProducts == null || !allProducts.Any())
                    return new List<ProductWithLowerSection>();

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
                            Measure = product.Measure,
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

        // Методы быстрого поиска
        public static int GetProductIdByName(string productName)
        {
            if (string.IsNullOrWhiteSpace(productName))
                return 0;

            var normalizedName = productName.Trim();

            if (_productIdByName.TryGetValue(normalizedName, out int productId))
                return productId;

            var foundProducts = FindProductsByPartialName(normalizedName);
            var firstProduct = foundProducts.FirstOrDefault();

            if (firstProduct != null && int.TryParse(firstProduct.ProductId, out int id) && id > 0)
                return id;

            return 0;
        }

        public static List<ProductWithCategoryInfo> FindProductsByPartialName(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return new List<ProductWithCategoryInfo>();

            var normalizedSearch = searchTerm.Trim().ToLower();
            var foundProducts = new HashSet<ProductWithCategoryInfo>();

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

            return foundProducts
                .Where(p => p.ProductName?.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0 ||
                           normalizedSearch.IndexOf(p.ProductName?.ToLower() ?? "", StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
        }

        // Обновить кэш принудительно
        public static async Task RefreshCacheAsync()
        {
            await ForceUpdateCacheAsync();
        }

        // Статистика кэша
        public static string GetCacheStats()
        {
            var productsCount = _allProductsWithCategories?.Count ?? 0;
            var measuresCount = _allMeasures?.Count ?? 0;
            var cacheSizeMB = CacheFileManager.GetCacheSizeMB();

            return $"Кэш: {productsCount} товаров, {measuresCount} единиц измерения, " +
                   $"размер: {cacheSizeMB} MB, " +
                   $"последнее обновление: {_cacheInfo?.LastCacheUpdate:g}";
        }

        // Проверка готовности кэша
        public static bool IsCacheReady()
        {
            return _isInitialized &&
                   _allProductsWithCategories != null &&
                   _allProductsWithCategories.Any() &&
                   _allMeasures != null &&
                   _allMeasures.Any();
        }

        // Проверить, нужно ли обновление кеша
        public static async Task<bool> NeedsUpdateAsync()
        {
            var cacheInfo = await CacheFileManager.LoadCacheInfo();
            return CacheFileManager.ShouldUpdateCache(cacheInfo.LastCacheUpdate);
        }

        // Очистить кэш
        public static void ClearCache()
        {
            _allProductsWithCategories = null;
            _allMeasures = null;
            _allProductsSimple = null;
            _productsByCategory.Clear();
            _productsByLowerSection.Clear();
            _productIdByName.Clear();
            _productById.Clear();
            _productsByNameSearch.Clear();
            _isInitialized = false;

            CacheFileManager.ClearCache();
        }

        // ============ ПРИВАТНЫЕ МЕТОДЫ ============

        private static async Task<bool> LoadFromFileCache()
        {
            try
            {
                Console.WriteLine("[BitrixCache] Попытка загрузки из файлового кеша...");
                CacheFileManager.DebugCacheFiles();

                // Загружаем товары
                var products = await CacheFileManager.LoadProductsFromFile<List<ProductWithCategoryInfo>>();
                if (products == null || !products.Any())
                {
                    Console.WriteLine("[BitrixCache] Не удалось загрузить товары из файлового кеша");
                    return false;
                }

                // Загружаем единицы измерения
                var measures = await CacheFileManager.LoadMeasuresFromFile<List<BitrixMeasure>>();
                if (measures == null || !measures.Any())
                {
                    Console.WriteLine("[BitrixCache] Не удалось загрузить единицы измерения из файлового кеша");
                    return false;
                }

                _allProductsWithCategories = products;
                _allMeasures = measures;
                _cacheInfo.ProductsCount = products.Count;
                _cacheInfo.MeasuresCount = measures.Count;

                Console.WriteLine($"[BitrixCache] Загружено {products.Count} товаров и {measures.Count} единиц измерения из файлового кеша");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BitrixCache] Ошибка загрузки из файлового кеша: {ex.Message}");
                return false;
            }
        }
        // Измените метод LoadFromBitrixAndSaveToCache в BitrixCache:

        private static async Task LoadFromBitrixAndSaveToCache()
        {
            try
            {
                Console.WriteLine("[BitrixCache] Загрузка данных из Bitrix...");

                // Параллельная загрузка товаров и единиц измерения
                var productsTask = _bitrixService.GetProductsWithCategoryInfo();
                var measuresTask = _bitrixService.GetMeasuresAsync();

                await Task.WhenAll(productsTask, measuresTask);

                _allProductsWithCategories = await productsTask;
                _allMeasures = await measuresTask;

                if (_allProductsWithCategories != null && _allProductsWithCategories.Any() &&
                    _allMeasures != null && _allMeasures.Any())
                {
                    Console.WriteLine($"[BitrixCache] Загружено {_allProductsWithCategories.Count} товаров и {_allMeasures.Count} единиц измерения из Bitrix");

                    // Сохраняем в файловый кеш
                    await CacheFileManager.SaveProductsToFile(_allProductsWithCategories);
                    await CacheFileManager.SaveMeasuresToFile(_allMeasures);

                    _cacheInfo.ProductsCount = _allProductsWithCategories.Count;
                    _cacheInfo.MeasuresCount = _allMeasures.Count;

                    // Также сохраняем информацию о кеше
                    _cacheInfo.LastCacheUpdate = DateTime.Now;
                    _cacheInfo.IsFirstRun = false;
                    await CacheFileManager.SaveCacheInfo(_cacheInfo);

                    Console.WriteLine($"[BitrixCache] Данные успешно сохранены в файловый кеш");
                }
                else
                {
                    Console.WriteLine("[BitrixCache] Предупреждение: не удалось загрузить данные из Bitrix");

                    // Если товары загрузились, но нет единиц измерения, создаем пустые
                    if (_allMeasures == null)
                    {
                        _allMeasures = new List<BitrixMeasure>();
                        Console.WriteLine("[BitrixCache] Создан пустой список единиц измерения");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BitrixCache] Ошибка загрузки из Bitrix: {ex.Message}");
            }
        }


        private static void UpdateSearchDictionaries(List<ProductWithCategoryInfo> products)
        {
            if (products == null) return;

            _productIdByName.Clear();
            _productById.Clear();
            _productsByNameSearch.Clear();

            foreach (var product in products)
            {
                if (string.IsNullOrEmpty(product.ProductName) || string.IsNullOrEmpty(product.ProductId))
                    continue;

                try
                {
                    if (int.TryParse(product.ProductId, out int productId) && productId > 0)
                    {
                        _productIdByName.TryAdd(product.ProductName.Trim(), productId);
                        _productById.TryAdd(productId, product);

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

        private static string BuildCategoryPath(string categoryId, Dictionary<string, Category> categoryDict)
        {
            if (string.IsNullOrEmpty(categoryId) || !categoryDict.ContainsKey(categoryId))
                return "Без категории";

            var pathParts = new List<string>();
            var currentId = categoryId;
            var visited = new HashSet<string>();

            while (!string.IsNullOrEmpty(currentId) && categoryDict.TryGetValue(currentId, out var category))
            {
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
    }
}