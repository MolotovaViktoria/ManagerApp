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
        private static Dictionary<int, List<Product>> _productsByCategory = new Dictionary<int, List<Product>>();
        private static bool _isInitialized = false;
        private static object _lockObject = new object();

        public static async Task InitializeAsync()
        {
            if (!_isInitialized)
            {
                lock (_lockObject)
                {
                    if (!_isInitialized)
                    {
                        // Можно предзагрузить основные категории здесь
                        _isInitialized = true;
                    }
                }
            }
        }

        public static async Task<List<Product>> GetProductsByCategory(int categoryId)
        {
            // Если данные уже в кеше - возвращаем их
            if (_productsByCategory.ContainsKey(categoryId))
            {
                return _productsByCategory[categoryId];
            }

            // Загружаем данные из Bitrix
            var products = await _bitrixService.GetProductsByCategory(categoryId);

            // Сохраняем в кеш
            _productsByCategory[categoryId] = products;

            return products;
        }

        public static void ClearCache()
        {
            _productsByCategory.Clear();
            _isInitialized = false;
        }

        public static bool IsCategoryCached(int categoryId)
        {
            return _productsByCategory.ContainsKey(categoryId);
        }
    }
}
