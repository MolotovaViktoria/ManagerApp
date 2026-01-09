using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ManagerApp.Data.ScharedData
{
    public class MatchedProduct
    {
        public int BitrixProductId { get; set; } // ДОБАВЬТЕ ЭТО!
        public string OriginalProductName { get; set; }
        public string BitrixProductName { get; set; }
        public decimal BitrixPrice { get; set; }
        public decimal CustomPrice { get; set; }
        public decimal Quantity { get; set; }
        public string Unit { get; set; }
        public string VAT { get; set; }
        public decimal PriceWithVAT { get; set; }
        public decimal TotalWithVAT { get; set; }
        public decimal PurchasingPrice { get; set; } // ДОБАВЬТЕ ЭТУ СТРОЧКУ

        public string Measure { get; set; }
    }
}
