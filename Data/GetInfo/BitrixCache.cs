using ManagerApp.Data.StructureList;
using System;
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

        // Новый метод для получения всех товаров с категориями
        public static async Task<List<ProductWithCategoryInfo>> GetAllProductsWithCategories()
        {
            // Если данные уже в кеше - возвращаем их
            if (_allProductsWithCategories != null &&
                (DateTime.Now - _allProductsCacheTimestamp) < _cacheDuration)
            {
                return _allProductsWithCategories;
            }

            // Загружаем данные из Bitrix
            _allProductsWithCategories = await _bitrixService.GetProductsWithCategoryInfo();
            _allProductsCacheTimestamp = DateTime.Now;

            return _allProductsWithCategories;
        }

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
            _isInitialized = false;
        }

        public static bool IsCategoryCached(int categoryId)
        {
            return _productsByCategory.ContainsKey(categoryId);
        }
    }
}