using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Text.RegularExpressions;
namespace ManagerApp.Classes.Search
{


    public class ProductListIdentifier
    {
        private readonly List<string> _contextMarkers;
        private readonly List<string> _tableHeaders;
        private readonly List<string> _quantityMarkers;
        private readonly List<string> _priceMarkers;
        private readonly List<string> _technicalIndicators;
        private readonly List<string> _endMarkers;

        public ProductListIdentifier()
        {
            // Инициализация паттернов
            _contextMarkers = new List<string>
        {
            "Ответ на Ценовой запрос №",
            "Технико-коммерческая часть заявки",
            "Спецификация МТРиО",
            "Техническое задание на поставку",
            "Котировочная спецификация"
        };

            _tableHeaders = new List<string>
        {
            "№ п/п", "pos_number", "№ пп", "Наименование позиции площадки",
            "Наименование ТМЦ", "Наименование продукции", "Описание запроса",
            "Наименование позиции поставщика"
        };

            _quantityMarkers = new List<string>
        {
            "Единица измерения", "Ед. изм.", "Ед. измерения", "Кол-во",
            "Количество", "Quantity", "Срок поставки", "График поставки"
        };

            _priceMarkers = new List<string>
        {
            "Цена за ед. без НДС", "Стоимость с НДС", "Артикул", "Завод-производитель"
        };

            _technicalIndicators = new List<string>
        {
            "ВВГнг-LS", "КГН", "АВВГнг", "IP44", "IP65", "ГОСТ", "ТУ"
        };

            _endMarkers = new List<string>
        {
            "Всего по заявке:", "Итого:", "Базис поставки", "Гарантийные обязательства"
        };
        }

        public string ExtractProductList(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var lines = text.Split('\n').Select(l => l.Trim()).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();

            int startIndex = FindProductListStart(lines);
            if (startIndex == -1)
                return string.Empty;

            int endIndex = FindProductListEnd(lines, startIndex);
            if (endIndex == -1)
                endIndex = lines.Length;

            return string.Join("\n", lines.Skip(startIndex).Take(endIndex - startIndex));
        }

        private int FindProductListStart(string[] lines)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                if (IsProductListStart(lines, i))
                    return i;
            }
            return -1;
        }

        private bool IsProductListStart(string[] lines, int currentIndex)
        {
            // Проверяем, что текущая строка похожа на начало товарной позиции
            if (!IsProductDataLine(lines[currentIndex]))
                return false;

            // Проверяем наличие заголовков таблицы в предыдущих строках
            bool hasTableHeaders = CheckTableHeadersInPreviousLines(lines, currentIndex);

            // Проверяем контекстные маркеры в документе
            bool hasContextMarkers = CheckContextMarkers(lines);

            return hasTableHeaders && hasContextMarkers;
        }

        private bool IsProductDataLine(string line)
        {
            // Проверка на числовой индекс в начале строки
            if (Regex.IsMatch(line, @"^\s*\d+\s+[^\d]") || Regex.IsMatch(line, @"^\d+\\."))
                return true;

            // Проверка на наличие технических характеристик
            if (_technicalIndicators.Any(indicator => line.Contains(indicator)))
                return true;

            // Проверка на наличие единиц измерения
            if (Regex.IsMatch(line, @"\b(шт|м|кг|т|км|л)\b", RegexOptions.IgnoreCase))
                return true;

            // Проверка на наличие числовых параметров (количество, размеры)
            if (Regex.IsMatch(line, @"\d+\s*[хx×]\s*\d+") || // размеры типа 3x2,5
                Regex.IsMatch(line, @"\b\d+\s*(шт|м|кг)\b", RegexOptions.IgnoreCase)) // количество с единицами
                return true;

            return false;
        }

        private bool CheckTableHeadersInPreviousLines(string[] lines, int currentIndex)
        {
            int searchWindow = Math.Min(10, currentIndex); // проверяем до 10 предыдущих строк

            for (int i = 1; i <= searchWindow; i++)
            {
                int checkIndex = currentIndex - i;
                if (checkIndex < 0) break;

                string line = lines[checkIndex];

                // Проверяем наличие заголовков таблицы
                bool hasTableHeaders = _tableHeaders.Any(header =>
                    line.Contains(header));

                bool hasQuantityHeaders = _quantityMarkers.Any(header =>
                    line.Contains(header));

                bool hasPriceHeaders = _priceMarkers.Any(header =>
                    line.Contains(header));

                if (hasTableHeaders || hasQuantityHeaders || hasPriceHeaders)
                    return true;
            }

            return false;
        }

        private bool CheckContextMarkers(string[] lines)
        {
            // Проверяем первые 20 строк на наличие контекстных маркеров
            int searchLimit = Math.Min(20, lines.Length);

            for (int i = 0; i < searchLimit; i++)
            {
                if (_contextMarkers.Any(marker =>
                    lines[i].Contains(marker)))
                    return true;
            }

            return false;
        }

        private int FindProductListEnd(string[] lines, int startIndex)
        {
            for (int i = startIndex + 1; i < lines.Length; i++)
            {
                if (IsProductListEnd(lines[i]))
                    return i;
            }
            return -1;
        }

        private bool IsProductListEnd(string line)
        {
            // Проверка на маркеры конца списка
            if (_endMarkers.Any(marker =>
                line.Contains(marker)))
                return true;

            // Проверка на смену раздела (новые заголовки)
            if (_tableHeaders.Any(header =>
                line.Contains(header)) &&
                !line.Contains("продолжение"))
                return true;

            // Проверка на технические примечания
            if (line.Contains("Примечание") ||
                line.Contains("Примечания"))
                return true;

            return false;
        }

        // Дополнительные методы для тонкой настройки
        public void AddCustomContextMarker(string marker)
        {
            _contextMarkers.Add(marker);
        }

        public void AddCustomTableHeader(string header)
        {
            _tableHeaders.Add(header);
        }

        public void AddCustomEndMarker(string marker)
        {
            _endMarkers.Add(marker);
        }
    }
}
