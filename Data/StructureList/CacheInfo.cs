using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ManagerApp.Data.StructureList
{
    public class CacheInfo
    {
        public DateTime LastCacheUpdate { get; set; }
        public int CacheVersion { get; set; } = 1;
        public bool IsFirstRun { get; set; } = true;
        public int ProductsCount { get; set; }
        public int MeasuresCount { get; set; }
        public long CacheSizeBytes { get; set; }
    }
}
