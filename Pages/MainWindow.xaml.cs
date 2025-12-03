using ManagerApp.Classes.Read;
using ManagerApp.Classes.Search;
using ManagerApp.Data.GetInfo;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ManagerApp.Pages
{
    public partial class MainWindows : Window
    {
        private BitrixService _bitrixService;
        private SmartProductSearch _productSearch;
        private string _originalFileText;
        private const string API_URL = "http://185.177.216.82:5000/api/ProductAnalysis/analyze";

        private static readonly Dictionary<string, bool> _aiCache = new Dictionary<string, bool>();
        private static readonly object _cacheLock = new object();
        private readonly StringBuilder _output4Buffer = new StringBuilder();
        private CancellationTokenSource _analysisCancellationTokenSource;

        public MainWindows()
        {
            InitializeComponent();
            _bitrixService = new BitrixService();
            _productSearch = new SmartProductSearch();
        }

        private void btnMenu_Click(object sender, RoutedEventArgs e)
        {
            pnlMenu.Visibility = pnlMenu.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        }

        private async void btnLoadRequest_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.Filter = "Документы Word (*.docx, *.dotx, *.docm, *.dotm)|*.docx;*.dotx;*.docm;*.dotm|" +
                           "PDF (*.pdf)|*.pdf|" +
                           "Excel (*.xlsx, *.xls, *.xlsm, *.xlsb, *.csv)|*.xlsx;*.xls;*.xlsm;*.xlsb;*.csv|" +
                           "Все файлы (*.*)|*.*";

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    btnLoadRequest.IsEnabled = false;
                    btnLoadRequest.Content = "Загрузка...";

                    var reader = new ReadRequst();
                    _originalFileText = reader.ReadFileAll(dialog.FileName);

                    txtOutput1.Text = _originalFileText;

                    lstProducts.Items.Clear();
                    txtOutput4.Text = "🚀 Начинаем анализ...\n";

                    _analysisCancellationTokenSource?.Cancel();
                    _analysisCancellationTokenSource = new CancellationTokenSource();

                    await AnalyzeFileSimple(_originalFileText, _analysisCancellationTokenSource.Token);

                    txtOutput1.Text += $"\n✅ Анализ завершен! Найдено товаров: {lstProducts.Items.Count}";
                    btnSearchProducts.IsEnabled = lstProducts.Items.Count > 0;
                }
                catch (OperationCanceledException)
                {
                    txtOutput1.Text += "\n❌ Анализ прерван";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    btnLoadRequest.IsEnabled = true;
                    btnLoadRequest.Content = "Загрузить запрос";
                }
            }
        }







      
        // Метод извлечения процента из текста (остается без изменений)
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


        private async Task AnalyzeFileSimple(string text, CancellationToken cancellationToken)
        {
            try
            {
                var allLines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                  .Select(l => l.Trim())
                                  .Where(l => l.Length >= 5 && l.Length <= 100)
                                  .ToList();

                AddStatusMessage($"Всего строк: {allLines.Count}");
                UpdateOutput4Fast($"📄 Всего строк: {allLines.Count}\n");
                UpdateOutput4Fast($"Фильтр: 5-100 символов\n\n");

                if (allLines.Count == 0)
                {
                    UpdateOutput4Fast("⚠️ Нет строк для анализа.\n");
                    return;
                }

                var linesToProcess = allLines.Take(100).ToList();
                UpdateOutput4Fast($"Будет обработано: {linesToProcess.Count}\n\n");

                int totalAdded = 0;
                int processed = 0;
                int total = linesToProcess.Count;

                // Создаем список для анализа соседних строк
                var linesArray = linesToProcess.ToArray();

                await Task.Run(() =>
                {
                    Parallel.For(0, linesArray.Length, new ParallelOptions
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
                        var line = linesArray[i];

                        try
                        {
                            UpdateOutput4Fast($"[{current}/{total}] {Truncate(line, 50)}\n");

                            // Шаг 0: Быстрая проверка на ОЧЕНЬ ОЧЕВИДНЫЙ не товар
                            if (IsVeryObviousNotProduct(line))
                            {
                                UpdateOutput4Fast($"  ⛔️ ОЧЕВИДНЫЙ НЕ ТОВАР\n");
                                return;
                            }

                            // Шаг 1: Проверка Bitrix (базовый процент)
                            var bitrixResult = _productSearch.SearchSimple(line).Result;
                            double basePercent = GetPercentFromText(bitrixResult);

                            UpdateOutput4Fast($"  Базовое совпадение Bitrix: {basePercent:F1}%\n");

                            // Шаг 2: Проверяем наличие ключевых слов и формата
                            double keywordBonus = CheckKeywordsAndFormatBonus(line);
                            if (keywordBonus > 0)
                            {
                                UpdateOutput4Fast($"  Бонус за ключевые слова/формат: +{keywordBonus:F1}%\n");
                            }

                            // Шаг 3: Проверяем специфические признаки товара
                            double specialBonus = CheckSpecialProductSigns(line);
                            if (specialBonus > 0)
                            {
                                UpdateOutput4Fast($"  Бонус за спец.признаки: +{specialBonus:F1}%\n");
                            }

                            // Шаг 4: Проверяем соседние строки (если basePercent между 15 и 35)
                            double neighborBonus = 0;
                            if (basePercent >= 15 && basePercent < 35)
                            {
                                neighborBonus = CheckNeighborBonus(i, linesArray, basePercent);
                                if (neighborBonus > 0)
                                {
                                    UpdateOutput4Fast($"  Бонус за соседние строки: +{neighborBonus:F1}%\n");
                                }
                            }

                            // Шаг 5: Итоговый процент
                            double finalPercent = basePercent + keywordBonus + specialBonus + neighborBonus;

                            // Ограничиваем максимум 100%
                            finalPercent = Math.Min(finalPercent, 100);

                            UpdateOutput4Fast($"  ИТОГО: {finalPercent:F1}%\n");

                            // Шаг 6: Принимаем решение - ПОНИЗИЛИ ПОРОГ ДО 35%!
                            if (finalPercent >= 35) // ПОНИЗИЛИ С 45% ДО 35%
                            {
                                Dispatcher.Invoke(() => lstProducts.Items.Add(line));
                                UpdateOutput4Fast($"  🎯 ТОВАР (итог ≥35%)\n");
                                Interlocked.Increment(ref totalAdded);
                            }
                            else if (basePercent < 15) // ПОНИЗИЛИ С 25% ДО 15%
                            {
                                UpdateOutput4Fast($"  ⏭️ НЕ ТОВАР (<15%)\n");
                            }
                            else
                            {
                                UpdateOutput4Fast($"  ⏭️ НЕ ТОВАР (итог <35%)\n");
                            }
                        }
                        catch (Exception ex)
                        {
                            UpdateOutput4Fast($"❌ Ошибка: {ex.Message}\n");
                        }
                    });
                }, cancellationToken);

                // Итоговая статистика
                UpdateOutput4Fast($"\n📊 ИТОГИ АНАЛИЗА:\n" +
                                 $"Всего обработано: {processed}\n" +
                                 $"✅ Добавлено товаров: {totalAdded}\n");
            }
            catch (Exception ex)
            {
                UpdateOutput4Fast($"\n❌ Ошибка анализа: {ex.Message}\n");
            }
        }

        // Метод для быстрой проверки ОЧЕНЬ ОЧЕВИДНЫХ не-товаров
        private bool IsVeryObviousNotProduct(string line)
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

        // Метод проверки ключевых слов и формата (упрощенный)
        private double CheckKeywordsAndFormatBonus(string line)
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

            // БОНУС 1: Ключевое слово (ОСНОВНОЙ БОНУС)
            if (hasProductKeyword)
            {
                bonus += 30; // УВЕЛИЧИЛИ С 15 ДО 30
                UpdateOutput4Fast($"    🔤 Ключевое слово (+30%)\n");
            }

            // БОНУС 2: Начинается с числа (номер позиции)
            bool startsWithNumber = Regex.IsMatch(line, @"^\d+[\.\s\t]+");
            if (startsWithNumber)
            {
                bonus += 15; // ДОПОЛНИТЕЛЬНЫЙ БОНУС
                UpdateOutput4Fast($"    🔢 Начинается с числа (+15%)\n");
            }

            // БОНУС 3: Единицы измерения
            var units = new[] { "м.", "шт.", "кг.", "т.", "л.", "м2", "м3", "п.м.", "км", "мп" };
            bool hasUnit = units.Any(unit => lowerLine.Contains(unit));
            if (hasUnit && hasProductKeyword)
            {
                bonus += 10;
                UpdateOutput4Fast($"    📏 Единица измерения (+10%)\n");
            }

            return bonus;
        }

        // Проверка специальных признаков товара
        private double CheckSpecialProductSigns(string line)
        {
            if (string.IsNullOrEmpty(line))
                return 0;

            double bonus = 0;
            string lowerLine = line.ToLower();

            // Признак 1: Содержит сечение кабеля/провода (3х2,5, 4х120, 5*16 и т.д.)
            if (Regex.IsMatch(line, @"\b\d+[хx\*]\d+([,\.]\d+)?\b"))
            {
                bonus += 25; // ОЧЕНЬ СИЛЬНЫЙ ПРИЗНАК
                UpdateOutput4Fast($"    ⚡️ Сечение кабеля (+25%)\n");
            }

            // Признак 2: Содержит маркировку кабеля (ВВГ, ПНСВ, КГВВ и т.д.)
            var cableMarkings = new[] { "ввг", "пнсв", "кгвв", "кввг", "пвс", "пугв", "кпсэ", "ксб", "ftp", "utp", "stp" };
            bool hasCableMarking = cableMarkings.Any(marking => lowerLine.Contains(marking));
            if (hasCableMarking)
            {
                bonus += 20;
                UpdateOutput4Fast($"    🔌 Маркировка кабеля (+20%)\n");
            }

            // Признак 3: Содержит ГОСТ или ТУ
            if (Regex.IsMatch(line, @"(ГОСТ|ТУ|TU|GOST)\s*[\d\-\.]+", RegexOptions.IgnoreCase))
            {
                bonus += 15;
                UpdateOutput4Fast($"    📋 ГОСТ/ТУ (+15%)\n");
            }

            // Признак 4: Табличный формат (число пробел/таб текст пробел/таб число)
            if (Regex.IsMatch(line, @"^\d+[\s\t]+\S+[\s\t]+\d+"))
            {
                bonus += 20;
                UpdateOutput4Fast($"    📊 Табличный формат (+20%)\n");
            }

            return bonus;
        }

        // Метод проверки соседних строк (обновленный)
        private double CheckNeighborBonus(int currentIndex, string[] lines, double currentPercent)
        {
            double bonus = 0;
            int checkedNeighbors = 0;

            // Проверяем в радиусе 2 строки
            for (int offset = -2; offset <= 2; offset++)
            {
                if (offset == 0) continue; // Пропускаем текущую строку

                int neighborIndex = currentIndex + offset;
                if (neighborIndex >= 0 && neighborIndex < lines.Length && checkedNeighbors < 2)
                {
                    var neighborLine = lines[neighborIndex];

                    // Не проверяем очевидные не-товары
                    if (!IsVeryObviousNotProduct(neighborLine))
                    {
                        try
                        {
                            var neighborResult = _productSearch.SearchSimple(neighborLine).Result;
                            double neighborPercent = GetPercentFromText(neighborResult);

                            // Если сосед имеет высокий процент
                            if (neighborPercent >= 40)
                            {
                                bonus += 15; // БОНУС ЗА КАЖДОГО ХОРОШЕГО СОСЕДА
                                checkedNeighbors++;
                                UpdateOutput4Fast($"    {GetDirectionSymbol(offset)} Соседняя строка - товар ({neighborPercent:F1}%)\n");

                                if (checkedNeighbors >= 2) break;
                            }
                        }
                        catch
                        {
                            // Игнорируем ошибки при проверке соседей
                        }
                    }
                }
            }

            return Math.Min(bonus, 30); // Максимум 30% за соседей
        }

        private string GetDirectionSymbol(int offset)
        {
            if (offset < 0) return "←";
            if (offset > 0) return "→";
            return "";
        }

        // Метод извлечения процента из текста (остается без изменений)
      

        private async Task<bool> CheckWithAISimple(string text)
        {
            lock (_cacheLock)
            {
                if (_aiCache.TryGetValue(text, out bool cached))
                    return cached;
            }

            try
            {
                var prompt = $"Это позиция/наименование/материал/продукция/изделие/ТМЦ? 1=да, 2=нет. Текст: {text}";
                var requestData = new { text = prompt };
                string json = JsonConvert.SerializeObject(requestData);

                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(15);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
                    {
                        var response = await client.PostAsync(API_URL, content, cts.Token);

                        if (!response.IsSuccessStatusCode)
                            return false;

                        string responseText = await response.Content.ReadAsStringAsync();
                        bool isProduct = responseText.Contains("1") && !responseText.Contains("2");

                        lock (_cacheLock)
                        {
                            _aiCache[text] = isProduct;
                        }

                        return isProduct;
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        private void UpdateOutput4Fast(string message)
        {
            Dispatcher.Invoke(() =>
            {
                txtOutput4.Text += message;
                txtOutput4.ScrollToEnd();
            });
        }

        private void AddStatusMessage(string message)
        {
            Dispatcher.Invoke(() => txtOutput1.Text += "\n" + message);
        }

        private async void btnSearchProducts_Click(object sender, RoutedEventArgs e)
        {
            if (lstProducts.Items.Count == 0)
            {
                MessageBox.Show("Нет товаров для поиска", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                btnSearchProducts.IsEnabled = false;
                btnSearchProducts.Content = "Поиск...";
                txtOutput3.Text = "Начинаем поиск в системе...\n";

                var products = lstProducts.Items.Cast<string>().ToList();
                var results = new StringBuilder();
                results.AppendLine($"🔍 Поиск {products.Count} товаров:\n");

                int processed = 0;
                foreach (var product in products)
                {
                    if (string.IsNullOrWhiteSpace(product)) continue;

                    try
                    {
                        var searchResult = await _productSearch.SearchSimple(product.Trim());
                        results.AppendLine($"📦 {product}");
                        results.AppendLine($"📋 {searchResult}");
                        results.AppendLine("─".PadRight(50, '─'));

                        processed++;

                        if (processed % 3 == 0)
                        {
                            Dispatcher.Invoke(() =>
                                txtOutput3.Text = $"Обработано {processed} из {products.Count}...\n\n{results}");
                        }

                        await Task.Delay(50);
                    }
                    catch
                    {
                        results.AppendLine($"❌ Ошибка при поиске: {product}");
                    }
                }

                results.AppendLine($"\n✅ Поиск завершен! Найдено: {processed} товаров");
                Dispatcher.Invoke(() => txtOutput3.Text = results.ToString());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка поиска: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                txtOutput3.Text = $"Ошибка: {ex.Message}";
            }
            finally
            {
                btnSearchProducts.IsEnabled = true;
                btnSearchProducts.Content = "Найти товары";
            }
        }

        private string Truncate(string text, int maxLength)
        {
            return string.IsNullOrEmpty(text) || text.Length <= maxLength
                ? text
                : text.Substring(0, maxLength) + "...";
        }

        private void btnCancelAnalysis_Click(object sender, RoutedEventArgs e)
        {
            _analysisCancellationTokenSource?.Cancel();
            UpdateOutput4Fast("\n❌ Анализ прерван\n");
        }
    }
}