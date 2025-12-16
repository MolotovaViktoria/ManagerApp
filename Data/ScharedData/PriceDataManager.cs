using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ManagerApp.Data.ScharedData
{
    public static class PriceDataManager
    {
        private static List<MatchedProduct> _matchedProducts = new List<MatchedProduct>();

        public static void SetMatchedProducts(List<MatchedProduct> products)
        {
            _matchedProducts.Clear(); // Важно: очищаем перед добавлением новых
            _matchedProducts.AddRange(products);
        }

        public static List<MatchedProduct> GetMatchedProducts() => _matchedProducts.ToList();

        // НОВЫЙ МЕТОД: Очистить данные
        public static void ClearMatchedProducts()
        {
            _matchedProducts.Clear();
        }
    }
}
