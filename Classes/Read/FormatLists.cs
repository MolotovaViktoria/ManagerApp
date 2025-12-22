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

        public static List<string> ImageFormatList = new List<string>
        {
            ".png",     // Portable Network Graphics
            ".jpg",     // JPEG изображения
            ".jpeg",    // JPEG изображения (альтернативное расширение)
            ".bmp",     // Bitmap изображения
            ".gif",     // Graphics Interchange Format
            ".tiff",    // Tagged Image File Format
            ".tif",     // Tagged Image File Format (альтернативное расширение)
            ".ico",     // Иконки Windows
            ".webp",    // WebP формат
            ".jfif",    // JPEG File Interchange Format
            ".heic",    // High Efficiency Image Format (для iOS)
            ".heif",    // High Efficiency Image Format
            ".svg",     // Scalable Vector Graphics (векторная графика)
            ".raw",     // RAW изображения (форматы камер)
            ".cr2",     // Canon RAW
            ".nef",     // Nikon RAW
            ".arw",     // Sony RAW
            ".dng"      // Digital Negative
        };
    }
}
