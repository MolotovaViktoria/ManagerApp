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

                int directlyAdded = 0;
                int aiChecked = 0;
                int aiApproved = 0;
                int skipped = 0;
                int processed = 0;
                int total = linesToProcess.Count;

                await Task.Run(() =>
                {
                    Parallel.ForEach(linesToProcess, new ParallelOptions
                    {
                        MaxDegreeOfParallelism = 2,
                        CancellationToken = cancellationToken
                    }, (line, state) =>
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            state.Stop();
                            return;
                        }

                        int current = Interlocked.Increment(ref processed);
                        double matchPercent = 0;
                        bool aiOk = false;
                        bool aiCheckedThis = false;

                        try
                        {
                            UpdateOutput4Fast($"[{current}/{total}] {Truncate(line, 50)}\n");

                            // Шаг 1: Проверка Bitrix
                            var bitrixResult = _productSearch.SearchSimple(line).Result;
                            matchPercent = GetPercentFromText(bitrixResult);

                            UpdateOutput4Fast($"  Совпадение Bitrix: {matchPercent:F1}%\n");

                            // Шаг 2: Логика по процентам
                            if (matchPercent >= 45)
                            {
                                // Высокое совпадение - добавляем сразу
                                Dispatcher.Invoke(() => lstProducts.Items.Add(line));
                                UpdateOutput4Fast($"  🎯 Высокое совпадение - добавлено сразу!\n");
                                Interlocked.Increment(ref directlyAdded);
                            }
                            else if (matchPercent >= 20 && matchPercent < 45)
                            {
                                // Среднее совпадение - проверяем ИИ
                                Interlocked.Increment(ref aiChecked);
                                aiCheckedThis = true;

                                try
                                {
                                    UpdateOutput4Fast($"  🤔 Среднее совпадение - проверяем ИИ...\n");
                                    aiOk = CheckWithAISimple(line).Result;

                                    UpdateOutput4Fast($"  ИИ: {(aiOk ? "✅" : "❌")}\n");

                                    if (aiOk)
                                    {
                                        Dispatcher.Invoke(() => lstProducts.Items.Add(line));
                                        UpdateOutput4Fast($"  🎯 Добавлено по решению ИИ!\n");
                                        Interlocked.Increment(ref aiApproved);
                                    }
                                }
                                catch (Exception aiEx)
                                {
                                    UpdateOutput4Fast($"  ИИ: ❌ (ошибка: {aiEx.Message})\n");
                                }
                            }
                            else
                            {
                                // Низкое совпадение - пропускаем
                                UpdateOutput4Fast($"  ⏭️ Низкое совпадение - пропущено\n");
                                Interlocked.Increment(ref skipped);
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
                                 $"├─ Высокое (>45%): {directlyAdded} (добавлено сразу)\n" +
                                 $"├─ Среднее (20-45%): {aiChecked} (проверено ИИ)\n" +
                                 $"│  └─ Подтверждено ИИ: {aiApproved}\n" +
                                 $"└─ Низкое (<20%): {skipped} (пропущено)\n" +
                                 $"\n" +
                                 $"✅ Всего добавлено: {directlyAdded + aiApproved} строк\n");
            }
            catch (Exception ex)
            {
                UpdateOutput4Fast($"\n❌ Ошибка анализа: {ex.Message}\n");
            }
        }

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