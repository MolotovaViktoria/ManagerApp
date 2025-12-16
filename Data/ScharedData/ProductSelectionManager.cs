using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ManagerApp.Data.ScharedData
{
    public static class ProductSelectionManager
    {
        public static List<string> SelectedProducts { get; set; } = new List<string>();


        public static void SetProducts(List<string> products)
        {
            SelectedProducts = products ?? new List<string>();
        }

        public static List<string> GetProducts()
        {
            return SelectedProducts;
        }

        public static void ClearProducts()
        {
            SelectedProducts.Clear();
        }

    }
}
