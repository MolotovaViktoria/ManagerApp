using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ManagerApp.Data.StructureList
{
    public class ProductWithCategoryInfo
    {
        public string CategoryName { get; set; }        // 1- название категории
        public string ProductName { get; set; }         // 2- товар (название)см 
        public decimal Price { get; set; }              // 3- цена
        public bool HasPrice { get; set; }              // Флаг наличия цены
        public string SectionId { get; set; }           // ID категории (для ссылок)
        public string ProductCode { get; set; }         // Код товара
        public string ProductId { get; set; }           // ID товара
    }
}
