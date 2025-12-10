using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ManagerApp.Data.Search
{
    public class AlgorithmicProductSearch
    {
        // Метод для быстрой проверки ОЧЕНЬ ОЧЕВИДНЫХ не-товаров
        public bool IsVeryObviousNotProduct(string line)
        {
            if (string.IsNullOrEmpty(line))
                return true;

            string lowerLine = line.ToLower();

            // Только САМЫЕ очевидные не-товары
            var veryObviousNegativeKeywords = new[]
            {
                "утверждаю", "согласовано", "разработано",
                "директор", "инженер", "начальник", "заместитель",
                "подраздел", "приложение", "приложение №", "прил. №",
                "техническое", "задание", "техзадание", "тз",
                "утверждаю:", "согласовано:", "разработано:"
            };

            // Если строка СОВПАДАЕТ полностью или начинается с этих слов - точно не товар
            foreach (var keyword in veryObviousNegativeKeywords)
            {
                if (lowerLine == keyword || lowerLine.StartsWith(keyword + " ") || lowerLine.StartsWith(keyword + ":"))
                    return true;
            }

            // Если начинается с "РАЗДЕЛ [число]" или "РАЗДЕЛ [число]." - не товар
            if (Regex.IsMatch(lowerLine, @"^раздел\s+\d"))
                return true;

            return false;
        }

        // Метод проверки ключевых слов и формата
        public double CheckKeywordsAndFormatBonus(string line)
        {
            if (string.IsNullOrEmpty(line) || line.Length < 3)
                return 0;

            string lowerLine = line.ToLower();
            double bonus = 0;

            // Ключевые слова ТОВАРА
            var productKeywords = new[]
            {
                "кабель", "провод", "труба", "арматура", "вентиль",
                "кран", "счетчик", "розетка", "выключатель", "автомат",
                "трансформатор", "щит", "панель", "бокс", "изоляция",
                "муфта", "фитинг", "задвижка", "клапан", "насос",
                "двигатель", "компрессор", "вентилятор", "радиатор",
                "светильник", "лампа", "кабель-канал", "трубопровод",
                "соединитель", "переходник", "адаптер", "разветвитель"
            };

            bool hasProductKeyword = productKeywords.Any(keyword => lowerLine.Contains(keyword));

            // БОНУС 1: Ключевое слово
            if (hasProductKeyword)
            {
                bonus += 30;
            }

            // БОНУС 2: Начинается с числа (номер позиции)
            bool startsWithNumber = Regex.IsMatch(line, @"^\d+[\.\s\t]+");
            if (startsWithNumber)
            {
                bonus += 15;
            }

            // БОНУС 3: Единицы измерения
            var units = new[] { "м.", "шт.", "кг.", "т.", "л.", "м2", "м3", "п.м.", "км", "мп" };
            bool hasUnit = units.Any(unit => lowerLine.Contains(unit));
            if (hasUnit && hasProductKeyword)
            {
                bonus += 10;
            }

            return bonus;
        }

        // Проверка специальных признаков товара
        public double CheckSpecialProductSigns(string line)
        {
            if (string.IsNullOrEmpty(line))
                return 0;

            double bonus = 0;
            string lowerLine = line.ToLower();

            // Признак 1: Содержит сечение кабеля/провода
            if (Regex.IsMatch(line, @"\b\d+[хx\*]\d+([,\.]\d+)?\b"))
            {
                bonus += 25;
            }

            // Признак 2: Содержит маркировку кабеля
            var cableMarkings = new[] { "ввг", "пнсв", "кгвв", "кввг", "пвс", "пугв", "кпсэ", "ксб", "ftp", "utp", "stp" };
            bool hasCableMarking = cableMarkings.Any(marking => lowerLine.Contains(marking));
            if (hasCableMarking)
            {
                bonus += 20;
            }

            // Признак 3: Содержит ГОСТ или ТУ
            if (Regex.IsMatch(line, @"(ГОСТ|ТУ|TU|GOST)\s*[\d\-\.]+", RegexOptions.IgnoreCase))
            {
                bonus += 15;
            }

            // Признак 4: Табличный формат
            if (Regex.IsMatch(line, @"^\d+[\s\t]+\S+[\s\t]+\d+"))
            {
                bonus += 20;
            }

            return bonus;
        }

        // Извлечение процента из текста
        public double GetPercentFromText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 0;

            var matches = Regex.Matches(text, @"(\d+)%");
            if (matches.Count == 0)
                return 0;

            double maxPercent = 0;
            foreach (Match match in matches)
            {
                if (double.TryParse(match.Groups[1].Value, out double percent))
                {
                    if (percent > maxPercent)
                        maxPercent = percent;
                }
            }

            return maxPercent;
        }

        // Метод анализа строки с использованием Bitrix поиска
        public async Task<(bool IsProduct, double Percent, string Details)> AnalyzeLineAsync(
            string line,
            Func<string, Task<string>> bitrixSearchFunc,
            string[] neighborLines = null)
        {
            var details = new StringBuilder();

            // Шаг 0: Быстрая проверка на ОЧЕНЬ ОЧЕВИДНЫЙ не товар
            if (IsVeryObviousNotProduct(line))
            {
                details.AppendLine("⛔️ ОЧЕВИДНЫЙ НЕ ТОВАР");
                return (false, 0, details.ToString());
            }

            // Шаг 1: Проверка Bitrix (базовый процент)
            var bitrixResult = await bitrixSearchFunc(line);
            double basePercent = GetPercentFromText(bitrixResult);
            details.AppendLine($"Базовое совпадение Bitrix: {basePercent:F1}%");

            // Шаг 2: Проверяем наличие ключевых слов и формата
            double keywordBonus = CheckKeywordsAndFormatBonus(line);
            if (keywordBonus > 0)
            {
                details.AppendLine($"Бонус за ключевые слова/формат: +{keywordBonus:F1}%");
            }

            // Шаг 3: Проверяем специфические признаки товара
            double specialBonus = CheckSpecialProductSigns(line);
            if (specialBonus > 0)
            {
                details.AppendLine($"Бонус за спец.признаки: +{specialBonus:F1}%");
            }

            // Шаг 4: Проверяем соседние строки (если basePercent между 15 и 35)
            double neighborBonus = 0;
            if (neighborLines != null && basePercent >= 15 && basePercent < 35)
            {
                neighborBonus = CheckNeighborBonus(line, neighborLines, bitrixSearchFunc);
                if (neighborBonus > 0)
                {
                    details.AppendLine($"Бонус за соседние строки: +{neighborBonus:F1}%");
                }
            }

            // Шаг 5: Итоговый процент
            double finalPercent = basePercent + keywordBonus + specialBonus + neighborBonus;
            finalPercent = Math.Min(finalPercent, 100);
            details.AppendLine($"ИТОГО: {finalPercent:F1}%");

            // Шаг 6: Принимаем решение
            bool isProduct = finalPercent >= 35;
            details.AppendLine(isProduct ? "🎯 ТОВАР (итог ≥35%)" : "⏭️ НЕ ТОВАР");

            return (isProduct, finalPercent, details.ToString());
        }

        // Метод проверки соседних строк
        private double CheckNeighborBonus(string currentLine, string[] neighborLines, Func<string, Task<string>> bitrixSearchFunc)
        {
            double bonus = 0;
            int checkedNeighbors = 0;

            foreach (var neighborLine in neighborLines)
            {
                if (neighborLine == currentLine || IsVeryObviousNotProduct(neighborLine))
                    continue;

                try
                {
                    var neighborResult = bitrixSearchFunc(neighborLine).Result;
                    double neighborPercent = GetPercentFromText(neighborResult);

                    // Если сосед имеет высокий процент
                    if (neighborPercent >= 40)
                    {
                        bonus += 15;
                        checkedNeighbors++;

                        if (checkedNeighbors >= 2) break;
                    }
                }
                catch
                {
                    // Игнорируем ошибки
                }
            }

            return Math.Min(bonus, 30);
        }

        // Метод анализа файла
        public async Task<List<string>> AnalyzeFileAsync(
            string fileText,
            Func<string, Task<string>> bitrixSearchFunc,
            IProgress<string> progress = null,
            CancellationToken cancellationToken = default)
        {
            var products = new List<string>();

            var allLines = fileText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                  .Select(l => l.Trim())
                                  .Where(l => l.Length >= 5 && l.Length <= 100)
                                  .ToList();

            if (allLines.Count == 0)
                return products;

            var linesToProcess = allLines.Take(100).ToList();
            int total = linesToProcess.Count;
            int processed = 0;

            // Обрабатываем строки
            foreach (var line in linesToProcess)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                processed++;
                progress?.Report($"[{processed}/{total}] Анализ: {Truncate(line, 50)}");

                // Получаем соседние строки
                int currentIndex = linesToProcess.IndexOf(line);
                var neighborLines = GetNeighborLines(linesToProcess, currentIndex, 2);

                // Анализируем строку
                var result = await AnalyzeLineAsync(line, bitrixSearchFunc, neighborLines);

                if (result.IsProduct)
                {
                    products.Add(line);
                }

                progress?.Report(result.Details);
            }

            return products;
        }

        // Получение соседних строк
        private string[] GetNeighborLines(List<string> allLines, int currentIndex, int radius)
        {
            var neighbors = new List<string>();
            int start = Math.Max(0, currentIndex - radius);
            int end = Math.Min(allLines.Count - 1, currentIndex + radius);

            for (int i = start; i <= end; i++)
            {
                if (i != currentIndex)
                {
                    neighbors.Add(allLines[i]);
                }
            }

            return neighbors.ToArray();
        }

        private string Truncate(string text, int maxLength)
        {
            return string.IsNullOrEmpty(text) || text.Length <= maxLength
                ? text
                : text.Substring(0, maxLength) + "...";
        }
    }
}
