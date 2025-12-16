using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ManagerApp.Classes.Search
{
    public class AlgorithmicProductAnalyzer
    {
        private readonly SmartProductSearch _productSearch;

        public AlgorithmicProductAnalyzer()
        {
            _productSearch = new SmartProductSearch();
        }

        /// <summary>
        /// Анализирует список строк через алгоритмические правила
        /// </summary>
        public async Task<List<string>> AnalyzeViaAlgorithmAsync(
            List<string> lines,
            Action<string> progressCallback = null,
            CancellationToken cancellationToken = default)
        {
            var foundProducts = new List<string>();

            try
            {
                progressCallback?.Invoke($"🔄 Начинаем алгоритмический анализ {lines.Count} строк...");

                // Фильтруем строки
                var linesToProcess = lines
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .Where(l => l.Length >= 5 && l.Length <= 100)
                    .Where(l => !IsVeryObviousNotProduct(l))
                    .Take(100) // Ограничиваем для скорости
                    .ToArray();

                progressCallback?.Invoke($"Будет обработано: {linesToProcess.Length} строк");

                int processed = 0;
                int total = linesToProcess.Length;
                int totalAdded = 0;

                await Task.Run(() =>
                {
                    Parallel.For(0, linesToProcess.Length, new ParallelOptions
                    {
                        MaxDegreeOfParallelism = 2,
                        CancellationToken = cancellationToken
                    }, (i, state) =>
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            state.Stop();
                            return;
                        }

                        int current = Interlocked.Increment(ref processed);
                        var line = linesToProcess[i];

                        try
                        {
                            progressCallback?.Invoke($"[{current}/{total}] {Truncate(line, 50)}");

                            // Шаг 1: Проверка Bitrix (базовый процент)
                            var bitrixResult = _productSearch.SearchSimple(line).Result;
                            double basePercent = GetPercentFromText(bitrixResult);

                            progressCallback?.Invoke($"  Базовое совпадение: {basePercent:F1}%");

                            // Шаг 2: Бонусы за ключевые слова и формат
                            double keywordBonus = CheckKeywordsAndFormatBonus(line, progressCallback);
                            double specialBonus = CheckSpecialProductSigns(line, progressCallback);

                            // Шаг 3: Бонус за соседние строки
                            double neighborBonus = 0;
                            if (basePercent >= 15 && basePercent < 35)
                            {
                                neighborBonus = CheckNeighborBonus(i, linesToProcess, basePercent, progressCallback);
                            }

                            // Итоговый процент
                            double finalPercent = basePercent + keywordBonus + specialBonus + neighborBonus;
                            finalPercent = Math.Min(finalPercent, 100);

                            progressCallback?.Invoke($"  ИТОГО: {finalPercent:F1}%");

                            // Решение (порог 35%)
                            if (finalPercent >= 35)
                            {
                                lock (foundProducts)
                                {
                                    foundProducts.Add(line);
                                }
                                Interlocked.Increment(ref totalAdded);
                                progressCallback?.Invoke($"  ✅ ТОВАР (итог ≥35%)");
                            }
                            else
                            {
                                progressCallback?.Invoke($"  ❌ НЕ ТОВАР (итог <35%)");
                            }
                        }
                        catch (Exception ex)
                        {
                            progressCallback?.Invoke($"  ⚠️ Ошибка: {ex.Message}");
                        }
                    });
                }, cancellationToken);

                progressCallback?.Invoke($"📊 Алгоритмический анализ завершен. Найдено товаров: {totalAdded}");
            }
            catch (OperationCanceledException)
            {
                progressCallback?.Invoke("⏹️ Алгоритмический анализ прерван");
            }
            catch (Exception ex)
            {
                progressCallback?.Invoke($"❌ Ошибка анализа: {ex.Message}");
            }

            return foundProducts;
        }

        // Метод извлечения процента из текста
        private double GetPercentFromText(string text)
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

        // Проверка на очевидные не-товары
        private bool IsVeryObviousNotProduct(string line)
        {
            if (string.IsNullOrEmpty(line))
                return true;

            string lowerLine = line.ToLower();

            var veryObviousNegativeKeywords = new[]
            {
                "утверждаю", "согласовано", "разработано",
                "директор", "инженер", "начальник", "заместитель",
                "подраздел", "приложение", "приложение №", "прил. №",
                "техническое", "задание", "техзадание", "тз",
                "утверждаю:", "согласовано:", "разработано:"
            };

            foreach (var keyword in veryObviousNegativeKeywords)
            {
                if (lowerLine == keyword || lowerLine.StartsWith(keyword + " ") || lowerLine.StartsWith(keyword + ":"))
                    return true;
            }

            if (Regex.IsMatch(lowerLine, @"^раздел\s+\d"))
                return true;

            return false;
        }

        // Проверка ключевых слов и формата
        private double CheckKeywordsAndFormatBonus(string line, Action<string> progressCallback)
        {
            if (string.IsNullOrEmpty(line) || line.Length < 3)
                return 0;

            string lowerLine = line.ToLower();
            double bonus = 0;

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

            if (hasProductKeyword)
            {
                bonus += 30;
                progressCallback?.Invoke($"    🔤 Ключевое слово (+30%)");
            }

            bool startsWithNumber = Regex.IsMatch(line, @"^\d+[\.\s\t]+");
            if (startsWithNumber)
            {
                bonus += 15;
                progressCallback?.Invoke($"    🔢 Начинается с числа (+15%)");
            }

            var units = new[] { "м.", "шт.", "кг.", "т.", "л.", "м2", "м3", "п.м.", "км", "мп" };
            bool hasUnit = units.Any(unit => lowerLine.Contains(unit));
            if (hasUnit && hasProductKeyword)
            {
                bonus += 10;
                progressCallback?.Invoke($"    📏 Единица измерения (+10%)");
            }

            return bonus;
        }

        // Проверка специальных признаков
        private double CheckSpecialProductSigns(string line, Action<string> progressCallback)
        {
            if (string.IsNullOrEmpty(line))
                return 0;

            double bonus = 0;
            string lowerLine = line.ToLower();

            // Сечение кабеля
            if (Regex.IsMatch(line, @"\b\d+[хx\*]\d+([,\.]\d+)?\b"))
            {
                bonus += 25;
                progressCallback?.Invoke($"    ⚡️ Сечение кабеля (+25%)");
            }

            // Маркировка кабеля
            var cableMarkings = new[] { "ввг", "пнсв", "кгвв", "кввг", "пвс", "пугв", "кпсэ", "ксб", "ftp", "utp", "stp" };
            bool hasCableMarking = cableMarkings.Any(marking => lowerLine.Contains(marking));
            if (hasCableMarking)
            {
                bonus += 20;
                progressCallback?.Invoke($"    🔌 Маркировка кабеля (+20%)");
            }

            // ГОСТ/ТУ
            if (Regex.IsMatch(line, @"(ГОСТ|ТУ|TU|GOST)\s*[\d\-\.]+", RegexOptions.IgnoreCase))
            {
                bonus += 15;
                progressCallback?.Invoke($"    📋 ГОСТ/ТУ (+15%)");
            }

            // Табличный формат
            if (Regex.IsMatch(line, @"^\d+[\s\t]+\S+[\s\t]+\d+"))
            {
                bonus += 20;
                progressCallback?.Invoke($"    📊 Табличный формат (+20%)");
            }

            return bonus;
        }

        // Проверка соседних строк
        private double CheckNeighborBonus(int currentIndex, string[] lines, double currentPercent, Action<string> progressCallback)
        {
            double bonus = 0;
            int checkedNeighbors = 0;

            for (int offset = -2; offset <= 2; offset++)
            {
                if (offset == 0) continue;

                int neighborIndex = currentIndex + offset;
                if (neighborIndex >= 0 && neighborIndex < lines.Length && checkedNeighbors < 2)
                {
                    var neighborLine = lines[neighborIndex];

                    if (!IsVeryObviousNotProduct(neighborLine))
                    {
                        try
                        {
                            var neighborResult = _productSearch.SearchSimple(neighborLine).Result;
                            double neighborPercent = GetPercentFromText(neighborResult);

                            if (neighborPercent >= 40)
                            {
                                bonus += 15;
                                checkedNeighbors++;
                                progressCallback?.Invoke($"    {GetDirectionSymbol(offset)} Соседняя строка - товар ({neighborPercent:F1}%)");

                                if (checkedNeighbors >= 2) break;
                            }
                        }
                        catch
                        {
                            // Игнорируем ошибки
                        }
                    }
                }
            }

            return Math.Min(bonus, 30);
        }

        private string GetDirectionSymbol(int offset)
        {
            if (offset < 0) return "←";
            if (offset > 0) return "→";
            return "";
        }

        private string Truncate(string text, int maxLength)
        {
            return string.IsNullOrEmpty(text) || text.Length <= maxLength
                ? text
                : text.Substring(0, maxLength) + "...";
        }
    }
}
