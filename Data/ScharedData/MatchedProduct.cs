// ManagerApp.Data.ScharedData.MatchedProduct
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ManagerApp.Data.ScharedData
{
    public class MatchedProduct
    {
        public int BitrixProductId { get; set; }
        public string OriginalProductName { get; set; }
        public string BitrixProductName { get; set; }
        public decimal BitrixPrice { get; set; }
        public decimal CustomPrice { get; set; }
        public decimal Quantity { get; set; }
        public string Unit { get; set; }
        public string UnitFullName { get; set; }  // ДОБАВЬТЕ
        public string VAT { get; set; }
        public decimal PriceWithVAT { get; set; }
        public decimal TotalWithVAT { get; set; }
        public decimal PurchasingPrice { get; set; }
        public string Measure { get; set; }

        // НОВЫЕ ПОЛЯ
        public decimal ProductQuantity { get; set; } = 1;
        public string MeasureId { get; set; }
        public string MeasureSymbol { get; set; }
        public string MeasureName { get; set; }
    }
}