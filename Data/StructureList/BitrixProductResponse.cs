using Newtonsoft.Json;
using System.Collections.Generic;

namespace ManagerApp.Data.StructureList
{
    public class BitrixProductResponse
    {
        [JsonProperty("result")]
        public List<Product> Products { get; set; }

        [JsonProperty("next")]
        public int? Next { get; set; }

        [JsonProperty("total")]
        public int Total { get; set; }
    }

    public class Product
    {
        [JsonProperty("ID")]
        public string Id { get; set; }

        [JsonProperty("NAME")]
        public string Name { get; set; }

        [JsonProperty("CODE")]
        public string Code { get; set; }

        [JsonProperty("SECTION_ID")]
        public string SectionId { get; set; }

        [JsonProperty("PRICE")]
        public decimal? Price { get; set; }

        [JsonProperty("MEASURE")]
        public string Measure { get; set; } // ДОБАВЬТЕ ЭТО
    }

    public class ProductWithLowerSection
    {
        public string ProductId { get; set; }
        public string ProductName { get; set; }
        public string LowerSectionId { get; set; }
        public string LowerSectionName { get; set; }
        public string CategoryPath { get; set; }
        public decimal Price { get; set; }
        public string Code { get; set; }
        public string Measure { get; set; }
        public bool HasPrice { get; set; } // ДОБАВЬТЕ ЭТО
    }
}