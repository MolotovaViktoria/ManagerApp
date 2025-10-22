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
        public List<Category> Categories { get; set; }
    }

    public class Category
    {

        [JsonProperty("SECTION_ID")]
        public string SelectionId { get; set; }

        [JsonProperty("NAME")]
        public string Name { get; set; }


    }
}
