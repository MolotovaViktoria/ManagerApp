using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ManagerApp.Classes.Read
{
    static public class FormatLists
    {

        public static List<string> WordFormatList = new List<string>
        {
        ".docx",  // Word 2007 и новее
        ".dotx",  // Шаблоны Word 2007+
        ".docm",  // Word с макросами 2007+
        ".dotm"   // Шаблоны с макросами 2007+
        };

        public static List<string> PdfFormatList = new List<string>
        {
        ".pdf"
        };

        public static List<string> ExcelFormatList = new List<string>
        {   ".xlsx", 
            ".xls", 
            ".xlsm", 
            ".xlsb", 
            ".csv" 
        };
    }
}
