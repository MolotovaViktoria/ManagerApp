using ManagerApp.Data.GetInfo;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ManagerApp.Data.StructureList
{
    public class BitrixCategoryResponse
    {
        [JsonProperty("result")]
        public List<BitrixCategory> Categories { get; set; }

        [JsonProperty("total")]
        public int Total { get; set; }

        [JsonProperty("time")]
        public BitrixTime Time { get; set; }
    }
    public class BitrixTime
    {
        [JsonProperty("start")]
        public long Start { get; set; }

        [JsonProperty("finish")]
        public double Finish { get; set; }

        [JsonProperty("duration")]
        public double Duration { get; set; }
    }
    public class Category
    {
        public string Name { get; set; }
        public string SelectionId { get; set; }  // Теперь это ID раздела
        public string ParentId { get; set; }     // ID родительского раздела
        public string Code { get; set; }
    }

    public class BitrixCategory
    {
        [JsonProperty("ID")]
        public string Id { get; set; }  // Измените с int на string

        [JsonProperty("NAME")]
        public string Name { get; set; }

        [JsonProperty("SECTION_ID")]
        public string SectionId { get; set; }  // Это ID родительского раздела

        [JsonProperty("CODE")]
        public string Code { get; set; }

        [JsonProperty("CATALOG_ID")]
        public string CatalogId { get; set; }

        [JsonProperty("XML_ID")]
        public string XmlId { get; set; }
    }

    
}
