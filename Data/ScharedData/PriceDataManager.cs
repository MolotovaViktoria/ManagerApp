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

        public static List<MatchedProduct> GetMatchedProducts()
        {
            return _matchedProducts;
        }

        public static void SetMatchedProducts(List<MatchedProduct> products)
        {
            _matchedProducts = products;
        }

        public static void ClearMatchedProducts()
        {
            _matchedProducts.Clear();
        }
    }
}
