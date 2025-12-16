using ManagerApp.Data.StructureList;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ManagerApp.Data.GetInfo
{
    public static class BitrixCache
    {
        private static BitrixService _bitrixService = new BitrixService();
        private static Dictionary<int, List<ProductWithCategoryInfo>> _productsByCategory = new Dictionary<int, List<ProductWithCategoryInfo>>();
        private static List<ProductWithCategoryInfo> _allProductsWithCategories = null;

        // НОВЫЕ КОЛЛЕКЦИИ ДЛЯ БЫСТРОГО ПОИСКА
        private static ConcurrentDictionary<string, int> _productIdByName = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private static ConcurrentDictionary<int, ProductWithCategoryInfo> _productById = new ConcurrentDictionary<int, ProductWithCategoryInfo>();
        private static ConcurrentDictionary<string, List<ProductWithCategoryInfo>> _productsByNameSearch = new ConcurrentDictionary<string, List<ProductWithCategoryInfo>>();

        private static bool _isInitialized = false;
        private static object _lockObject = new object();
        private static List<Product> _allProductsCache = null;
        private static DateTime _allProductsCacheTimestamp = DateTime.MinValue;
        private static readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(30);

        public static async Task InitializeAsync()
        {
            if (!_isInitialized)
            {
                lock (_lockObject)
                {
                    if (!_isInitialized)
                    {
                        _isInitialized = true;
                    }
                }

                // ЗАГРУЖАЕМ ДАННЫЕ ПРИ ИНИЦИАЛИЗАЦИИ
                try
                {
                    // Загружаем все данные
                    await GetAllProductsWithCategories();
                    await GetAllProductsSimple();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка загрузки данных в кеш: {ex.Message}");
                    // Можно сбросить флаг, если загрузка не удалась
                    _isInitialized = false;
                    throw;
                }
            }
        }

        // Обновленный метод для получения всех товаров с категориями
        public static async Task<List<ProductWithCategoryInfo>> GetAllProductsWithCategories()
        {
            // Если данные уже в кеше и не устарели - возвращаем их
            if (_allProductsWithCategories != null &&
                (DateTime.Now - _allProductsCacheTimestamp) < _cacheDuration)
            {
                return _allProductsWithCategories;
            }

            // Загружаем данные из Bitrix
            _allProductsWithCategories = await _bitrixService.GetProductsWithCategoryInfo();
            _allProductsCacheTimestamp = DateTime.Now;

            // ОБНОВЛЯЕМ СЛОВАРИ ДЛЯ БЫСТРОГО ПОИСКА
            UpdateProductDictionaries(_allProductsWithCategories);

            return _allProductsWithCategories;
        }

        // Метод для обновления словарей быстрого поиска
        private static void UpdateProductDictionaries(List<ProductWithCategoryInfo> products)
        {
            // Очищаем старые данные
            _productIdByName.Clear();
            _productById.Clear();
            _productsByNameSearch.Clear();

            foreach (var product in products)
            {
                if (string.IsNullOrEmpty(product.ProductName) || string.IsNullOrEmpty(product.ProductId))
                    continue;

                try
                {
                    int productId = int.TryParse(product.ProductId, out int id) ? id : 0;
                    if (productId <= 0)
                        continue;

                    // 1. Словарь "Название товара -> ID"
                    _productIdByName.TryAdd(product.ProductName.Trim(), productId);

                    // 2. Словарь "ID -> Полная информация о товаре"
                    _productById.TryAdd(productId, product);

                    // 3. Для каждого слова в названии товара добавляем в индекс поиска
                    var words = product.ProductName.ToLower().Split(new[] { ' ', ',', '.', '-', '_' },
                        StringSplitOptions.RemoveEmptyEntries);

                    foreach (var word in words.Where(w => w.Length > 2)) // Игнорируем слишком короткие слова
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
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка добавления товара в кэш: {ex.Message}");
                }
            }

            Console.WriteLine($"Кэш обновлен. Товаров: {_productIdByName.Count}, Индекс поиска: {_productsByNameSearch.Count} слов");
        }

        // НОВЫЙ МЕТОД: Получить ID товара по названию (из кэша)
        public static int GetProductIdByName(string productName)
        {
            if (string.IsNullOrWhiteSpace(productName))
                return 0;

            var normalizedName = productName.Trim();

            // 1. Прямой поиск по полному названию
            if (_productIdByName.TryGetValue(normalizedName, out int productId))
            {
                Console.WriteLine($"Найден товар в кэше (точное совпадение): {normalizedName} -> ID: {productId}");
                return productId;
            }

            // 2. Если точного совпадения нет, ищем частичное
            var foundProducts = FindProductsByPartialName(normalizedName);
            if (foundProducts.Any())
            {
                var firstProduct = foundProducts.First();
                if (int.TryParse(firstProduct.ProductId, out int id) && id > 0)
                {
                    Console.WriteLine($"Найден товар в кэше (частичное совпадение): {normalizedName} -> {firstProduct.ProductName} (ID: {id})");
                    return id;
                }
            }

            Console.WriteLine($"Товар '{productName}' не найден в кэше");
            return 0;
        }

        // НОВЫЙ МЕТОД: Поиск товаров по частичному названию
        public static List<ProductWithCategoryInfo> FindProductsByPartialName(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return new List<ProductWithCategoryInfo>();

            var normalizedSearch = searchTerm.Trim().ToLower();
            var foundProducts = new List<ProductWithCategoryInfo>();

            // Ищем по словам в индексе
            var searchWords = normalizedSearch.Split(new[] { ' ', ',', '.', '-', '_' },
                StringSplitOptions.RemoveEmptyEntries);

            foreach (var word in searchWords.Where(w => w.Length > 2))
            {
                if (_productsByNameSearch.TryGetValue(word, out var products))
                {
                    foundProducts.AddRange(products);
                }
            }

            // Удаляем дубликаты
            foundProducts = foundProducts.Distinct().ToList();

            // Фильтруем по точному содержанию
            foundProducts = foundProducts.Where(p =>
                p.ProductName?.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalizedSearch.IndexOf(p.ProductName?.ToLower() ?? "", StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            return foundProducts;
        }

        // НОВЫЙ МЕТОД: Получить информацию о товаре по ID
        public static ProductWithCategoryInfo GetProductById(int productId)
        {
            if (productId <= 0)
                return null;

            _productById.TryGetValue(productId, out var product);
            return product;
        }

        // НОВЫЙ МЕТОД: Обновить кэш (принудительно)
        public static async Task RefreshCacheAsync()
        {
            try
            {
                Console.WriteLine("Обновление кэша товаров...");
                _allProductsWithCategories = null;
                _allProductsCache = null;
                _productIdByName.Clear();
                _productById.Clear();
                _productsByNameSearch.Clear();

                await GetAllProductsWithCategories();
                Console.WriteLine("Кэш обновлен успешно");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка обновления кэша: {ex.Message}");
            }
        }

        // Существующие методы (оставляем для совместимости)
        public static async Task<List<ProductWithCategoryInfo>> GetProductsByCategory(int categoryId)
        {
            // Если данные уже в кеше - возвращаем их
            if (_productsByCategory.ContainsKey(categoryId))
            {
                return _productsByCategory[categoryId];
            }

            // Загружаем данные из Bitrix
            var products = await _bitrixService.GetProductsWithCategoryInfo();

            // Сохраняем в кеш
            _productsByCategory[categoryId] = products;

            return products;
        }

        public static async Task<List<Product>> GetAllProductsSimple()
        {
            // Если данные уже в кеше - возвращаем их
            if (_allProductsCache != null)
            {
                return _allProductsCache;
            }

            // Загружаем данные из Bitrix
            var products = await _bitrixService.GetProducts();

            // Сохраняем в кеш
            _allProductsCache = products;

            return products;
        }

        public static void ClearCache()
        {
            _productsByCategory.Clear();
            _allProductsWithCategories = null;
            _allProductsCache = null;
            _productIdByName.Clear();
            _productById.Clear();
            _productsByNameSearch.Clear();
            _isInitialized = false;
        }

        public static bool IsCategoryCached(int categoryId)
        {
            return _productsByCategory.ContainsKey(categoryId);
        }

        // НОВЫЙ МЕТОД: Проверить, инициализирован ли кэш
        public static bool IsCacheReady()
        {
            return _isInitialized && _allProductsWithCategories != null && _productIdByName.Count > 0;
        }

        // НОВЫЙ МЕТОД: Получить статистику кэша
        public static string GetCacheStats()
        {
            return $"Кэш товаров: {_productIdByName.Count} товаров, " +
                   $"индекс поиска: {_productsByNameSearch.Count} слов, " +
                   $"инициализирован: {_isInitialized}";
        }
    }
}