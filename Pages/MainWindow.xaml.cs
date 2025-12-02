using ManagerApp.Classes.ModelsStudy;
using ManagerApp.Classes.Normalizer;
using ManagerApp.Classes.Read;
using ManagerApp.Classes.Search;
using ManagerApp.Data.GetInfo;
using ManagerApp.Data.StructureList;
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
        private static readonly Dictionary<string, bool> _bitrixCache = new Dictionary<string, bool>();
        private static readonly object _cacheLock = new object();
        private readonly StringBuilder _output4Buffer = new StringBuilder();
        private int _uiUpdateCounter = 0;
        private System.Windows.Threading.DispatcherTimer _uiUpdateTimer;
        private CancellationTokenSource _analysisCancellationTokenSource;

        public MainWindows()
        {
            InitializeComponent();
            _bitrixService = new BitrixService();
            _productSearch = new SmartProductSearch();

            _uiUpdateTimer = new System.Windows.Threading.DispatcherTimer();
            _uiUpdateTimer.Interval = TimeSpan.FromMilliseconds(300);
            _uiUpdateTimer.Tick += (s, e) => UpdateUIFromBuffer();
        }

        private void btnMenu_Click(object sender, RoutedEventArgs e)
        {
            pnlMenu.Visibility = pnlMenu.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        }

        private void btnShowCache_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var bitrixCacheType = typeof(BitrixCache);
                var allProductsField = bitrixCacheType.GetField("_allProductsCache",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

                if (allProductsField?.GetValue(null) is List<Product> products && products != null)
                    MessageBox.Show($"Загружено товаров: {products.Count}");
                else
                    MessageBox.Show("Кеш товаров пуст");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}");
            }
        }

        private async void btnTestApi_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                btnTestApi.IsEnabled = false;
                btnTestApi.Content = "Тестируем...";
                txtOutput1.Text = "Тестируем API...";

                var requestData = new { text = "Тестовый запрос" };
                string json = JsonConvert.SerializeObject(requestData);

                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(30);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    var response = await client.PostAsync(API_URL, content);
                    string responseText = await response.Content.ReadAsStringAsync();

                    txtOutput1.Text = response.IsSuccessStatusCode
                        ? $"✅ API работает!\nОтвет: {responseText}"
                        : $"❌ Ошибка: {response.StatusCode}\n{responseText}";
                }
            }
            catch (Exception ex)
            {
                txtOutput1.Text = $"❌ Ошибка: {ex.Message}";
            }
            finally
            {
                btnTestApi.IsEnabled = true;
                btnTestApi.Content = "Тест API";
            }
        }

        private async void btnCheckServer_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                btnCheckServer.IsEnabled = false;
                btnCheckServer.Content = "Проверяем...";
                txtOutput1.Text = "Проверяем сервер...";

                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(5);
                    try
                    {
                        var response = await client.GetAsync("http://185.177.216.82:5000/");
                        txtOutput1.Text = response.IsSuccessStatusCode ? "✅ Сервер доступен" : "❌ Сервер недоступен";
                    }
                    catch
                    {
                        txtOutput1.Text = "❌ Не удалось подключиться к серверу";
                    }
                }
            }
            finally
            {
                btnCheckServer.IsEnabled = true;
                btnCheckServer.Content = "Проверить сервер";
            }
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

                    var reader = new Classes.Read.ReadRequst();
                    _originalFileText = reader.ReadFileAll(dialog.FileName);

                    txtOutput1.Text = _originalFileText;

                    lstProducts.Items.Clear();
                    txtOutput4.Text = "🚀 Начинаем анализ...\n";

                    // Отмена предыдущего анализа, если есть
                    _analysisCancellationTokenSource?.Cancel();
                    _analysisCancellationTokenSource = new CancellationTokenSource();

                    await AnalyzeFileSimple(_originalFileText, _analysisCancellationTokenSource.Token);

                    txtOutput1.Text += $"\n✅ Анализ завершен! Найдено товаров: {lstProducts.Items.Count}";
                    btnSearchProducts.IsEnabled = lstProducts.Items.Count > 0;
                }
                catch (OperationCanceledException)
                {
                    txtOutput1.Text += "\n❌ Анализ прерван пользователем";
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
                // 1. Разбиваем на строки и фильтруем по длине
                var allLines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                  .Select(l => l.Trim())
                                  .Where(l => l.Length >= 3 && l.Length <= 60) // Шаг 1: длина 3-100 символов
                                  .ToList();

                AddStatusMessage($"Всего строк: {allLines.Count}");
                UpdateOutput4Fast($"📄 Строк для анализа: {allLines.Count}\n\n");

                if (allLines.Count == 0)
                {
                    UpdateOutput4Fast("⚠️ Нет строк для анализа.\n");
                    return;
                }

                // Ограничиваем количество для скорости
                var linesToProcess = allLines.Take(100).ToList();
                UpdateOutput4Fast($"Будет обработано: {linesToProcess.Count}\n\n");

                var results = new ConcurrentBag<(string line, bool bitrixOk, bool aiOk)>();
                int processed = 0;
                int total = linesToProcess.Count;

                // ПАРАЛЛЕЛЬНАЯ обработка 2 строк за раз
                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = 2, // Две строки одновременно
                    CancellationToken = cancellationToken
                };

                await Task.Run(() =>
                {
                    Parallel.ForEach(linesToProcess, parallelOptions, (line, state) =>
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            state.Stop();
                            return;
                        }

                        int current = Interlocked.Increment(ref processed);
                        bool bitrixOk = false;
                        bool aiOk = false;

                        try
                        {
                            UpdateOutput4Fast($"[{current}/{total}] {Truncate(line, 50)}\n");

                            // Шаг 2: Проверка Bitrix (минимум 20%)
                            var bitrixResult = _productSearch.SearchSimple(line).Result;
                            bitrixOk = IsGoodBitrixMatchSimple(bitrixResult);
                            UpdateOutput4Fast($"  Bitrix: {(bitrixOk ? "✅" : "❌")}\n");

                            if (bitrixOk)
                            {
                                // Шаг 3: Проверка ИИ
                                try
                                {
                                    aiOk = CheckWithAISimple(line).Result;
                                    UpdateOutput4Fast($"  ИИ: {(aiOk ? "✅" : "❌")}\n");

                                    if (aiOk)
                                    {
                                        Dispatcher.Invoke(() => lstProducts.Items.Add(line));
                                        UpdateOutput4Fast($"  🎯 Добавлено!\n");
                                    }
                                }
                                catch
                                {
                                    UpdateOutput4Fast($"  ИИ: ❌ (ошибка)\n");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            UpdateOutput4Fast($"❌ Ошибка: {ex.Message}\n");
                        }

                        results.Add((line, bitrixOk, aiOk));
                    });
                }, cancellationToken);

                int bitrixPassed = results.Count(r => r.bitrixOk);
                int aiPassed = results.Count(r => r.aiOk);

                UpdateOutput4Fast($"\n📊 ИТОГИ:\n" +
                                 $"Обработано: {processed}\n" +
                                 $"Прошли Bitrix: {bitrixPassed}\n" +
                                 $"Подтверждено ИИ: {aiPassed}\n" +
                                 $"✅ Добавлено: {aiPassed}\n");
            }
            catch (Exception ex)
            {
                UpdateOutput4Fast($"\n❌ Ошибка анализа: {ex.Message}\n");
            }
        }

        // Простая проверка Bitrix - только 20% порог
        private bool IsGoodBitrixMatchSimple(string searchResult)
        {
            if (string.IsNullOrWhiteSpace(searchResult))
                return false;

            // Ищем проценты совпадения
            var percentMatches = Regex.Matches(searchResult, @"(\d+)%");
            foreach (Match match in percentMatches)
            {
                if (int.TryParse(match.Groups[1].Value, out int percentage) && percentage >= 20)
                    return true;
            }

            return false;
        }

        // СУПЕР простая проверка ИИ
        private async Task<bool> CheckWithAISimple(string text)
        {
            // Проверяем кеш
            lock (_cacheLock)
            {
                if (_aiCache.TryGetValue(text, out bool cached))
                    return cached;
            }

            try
            {
                // ТОЛЬКО ОДНА СТРОКА ПРОМПТА как просили
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

                        // Очень простой парсинг - ищем "1"
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
            lock (_output4Buffer)
            {
                _output4Buffer.Append(message);
                _uiUpdateCounter++;

                if (_uiUpdateCounter >= 1) // Обновляем после каждого сообщения
                    UpdateUIFromBuffer();
                else if (!_uiUpdateTimer.IsEnabled)
                    _uiUpdateTimer.Start();
            }
        }

        private void UpdateUIFromBuffer()
        {
            lock (_output4Buffer)
            {
                if (_output4Buffer.Length > 0)
                {
                    Dispatcher.Invoke(() =>
                    {
                        txtOutput4.Text += _output4Buffer.ToString();
                        txtOutput4.ScrollToEnd();
                    });
                    _output4Buffer.Clear();
                }
                _uiUpdateTimer.Stop();
                _uiUpdateCounter = 0;
            }
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

        private async void btnDebug_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                txtOutput4.Text = "🧪 Простой тест...\n";

                var testLines = new[]
                {
                    "1 Провод ПНСВ 1,2 ГОСТ 26445-85",
                    "УТВЕРЖДАЮ: Директор",
                    "Кабель ВВГ 3х2,5",
                    "Приложение №1"
                };

                foreach (var testLine in testLines)
                {
                    UpdateOutput4Fast($"\nТест: {Truncate(testLine, 40)}\n");

                    // Проверяем Bitrix
                    var bitrixResult = await _productSearch.SearchSimple(testLine);
                    bool bitrixOk = IsGoodBitrixMatchSimple(bitrixResult);
                    UpdateOutput4Fast($"Bitrix: {(bitrixOk ? "✅" : "❌")}\n");

                    if (bitrixOk)
                    {
                        bool aiOk = await CheckWithAISimple(testLine);
                        UpdateOutput4Fast($"ИИ: {(aiOk ? "✅ ТОВАР" : "❌ НЕ ТОВАР")}\n");
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateOutput4Fast($"❌ Ошибка: {ex.Message}\n");
            }
        }

        private void btnClearCache_Click(object sender, RoutedEventArgs e)
        {
            lock (_cacheLock)
            {
                _aiCache.Clear();
                _bitrixCache.Clear();
            }

            MessageBox.Show("Кеш очищен", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void btnCancelAnalysis_Click(object sender, RoutedEventArgs e)
        {
            _analysisCancellationTokenSource?.Cancel();
            UpdateOutput4Fast("\n❌ Анализ прерван\n");
        }
    }
}