using ManagerApp.Data.StructureList;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ManagerApp.Data.Search
{
    public class SearchResult
    {
        public Product Product { get; set; }
        public double Score { get; set; } // 0-1, где 1 - полное совпадение
        public List<string> MatchedWords { get; set; }
        public string Message { get; set; }
    }
}
