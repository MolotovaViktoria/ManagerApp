using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ManagerApp.Data.ScharedData
{
    public static class ProductSelectionManager
    {
        private static List<string> _products = new List<string>();
        private static List<ExtractedProductInfo> _extractedProducts = new List<ExtractedProductInfo>();

        // НОВЫЕ МЕТОДЫ ДЛЯ РАСШИРЕННЫХ ДАННЫХ (с описанием, количеством, единицами измерения)
        public static void SetExtractedProducts(List<ExtractedProductInfo> products)
        {
            _extractedProducts = products ?? new List<ExtractedProductInfo>();

            // Для обратной совместимости обновляем и старый список
            _products = _extractedProducts.Select(p => p.Name).ToList();
        }

        public static List<ExtractedProductInfo> GetExtractedProducts()
        {
            return _extractedProducts ?? new List<ExtractedProductInfo>();
        }

        // СТАРЫЕ МЕТОДЫ ДЛЯ ОБРАТНОЙ СОВМЕСТИМОСТИ (только названия товаров)
        public static void SetProducts(List<string> products)
        {
            _products = products ?? new List<string>();

            // Конвертируем в новый формат (создаем ExtractedProductInfo с значениями по умолчанию)
            _extractedProducts = _products.Select(p => new ExtractedProductInfo
            {
                Name = p,
                Quantity = 1,
                MeasureSymbol = "шт",
                MeasureName = "Штука",
                Description = ""
            }).ToList();
        }

        public static List<string> GetProducts()
        {
            return _products ?? new List<string>();
        }

        public static void ClearProducts()
        {
            _products?.Clear();
            _extractedProducts?.Clear();
        }
    }
}