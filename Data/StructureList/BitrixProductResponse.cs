using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ManagerApp.Data.StructureList
{
    public class BitrixProductResponse
    {
        [JsonProperty("result")]
        public List<Product> Products { get; set; }
    }

    public class Product
    {
        [JsonProperty("ID")]
        public string Id { get; set; }

        [JsonProperty("NAME")]
        public string Name { get; set; }

        [JsonProperty("CODE")]
        public string Code { get; set; }


        [JsonProperty("PRICE")]
        public double? Price { get; set; }

    }
}
