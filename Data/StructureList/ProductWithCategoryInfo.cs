using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ManagerApp.Data.StructureList
{
    // В классе ProductWithCategoryInfo (если существует)
    public class ProductWithCategoryInfo
    {
        public string ProductId { get; set; }
        public string ProductName { get; set; }
        public string SectionId { get; set; }
        public string CategoryName { get; set; }
        public decimal Price { get; set; }
        public decimal? PurchasingPrice { get; set; } // Добавить
        public string ProductCode { get; set; }
        public string Measure { get; set; } // Добавить
        public bool HasPrice { get; set; }
        public bool HasPurchasingPrice { get; set; } // Добавить
    }
}
