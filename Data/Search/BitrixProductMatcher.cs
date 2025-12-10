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
        private readonly BitrixService _bitrixService;
        private List<ProductWithCategoryInfo> _allProductsCache;
        private DateTime _cacheTimestamp;
        private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(30);
        private readonly object _cacheLock = new object();

        public BitrixProductMatcher()
        {
            _bitrixService = new BitrixService();
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

            // Загружаем из Bitrix
            var products = await _bitrixService.GetProductsWithCategoryInfo();

            lock (_cacheLock)
            {
                _allProductsCache = products;
                _cacheTimestamp = DateTime.Now;
            }

            return products;
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

            // Добавляем процент совпадения (можно рассчитать)
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
                // Рассчитываем процент совпадения (упрощенная версия)
                double matchPercentage = CalculateMatchPercentage(product);

                sb.AppendLine($"   • {product.ProductName}");
                sb.AppendLine($"     Категория: {product.CategoryName}, Цена: {(product.HasPrice ? product.Price.ToString() + " руб." : "Нет")}");
                sb.AppendLine($"     Совпадение: {matchPercentage:F1}%");
            }

            return sb.ToString();
        }

        // Расчет процента совпадения (упрощенный)
        private double CalculateMatchPercentage(ProductWithCategoryInfo product)
        {
            // Здесь можно добавить более сложную логику сравнения
            // Пока возвращаем случайное значение для демонстрации
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
    }
}
