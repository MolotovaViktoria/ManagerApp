using ManagerApp.Classes.ModelsStudy;
using ManagerApp.Classes.Normalizer;
using ManagerApp.Classes.Read;
using ManagerApp.Classes.Search;
using ManagerApp.Data.GetInfo;
using ManagerApp.Data.StructureList;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ManagerApp.Pages
{
    public partial class MainWindows : Window
    {
        private BitrixService _bitrixService;
        private SmartProductSearch _productSearch;
        private string _originalFileText;
        private const string API_URL = "http://185.177.216.82:5000/api/ProductAnalysis/analyze";

        public MainWindows()
        {
            InitializeComponent();
            _bitrixService = new BitrixService();
            _productSearch = new SmartProductSearch();
        }

        private void btnMenu_Click(object sender, RoutedEventArgs e)
        {
            if (pnlMenu.Visibility == Visibility.Visible)
            {
                pnlMenu.Visibility = Visibility.Collapsed;
                // Возвращаем узкую ширину для синей панели
                var border = VisualTreeHelper.GetParent(pnlMenu) as StackPanel;
                if (border != null)
                {
                    var mainBorder = VisualTreeHelper.GetParent(border) as Border;
                    if (mainBorder != null)
                    {
                        mainBorder.Width = 40;
                    }
                }
            }
            else
            {
                pnlMenu.Visibility = Visibility.Visible;
                // Расширяем синюю панель чтобы вместить кнопки
                var border = VisualTreeHelper.GetParent(pnlMenu) as StackPanel;
                if (border != null)
                {
                    var mainBorder = VisualTreeHelper.GetParent(border) as Border;
                    if (mainBorder != null)
                    {
                        mainBorder.Width = 150;
                    }
                }
            }
        }

        private async void btnTestApi_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                btnTestApi.IsEnabled = false;
                btnTestApi.Content = "Тестируем...";

                // Точная копия работающего запроса из Swagger
                var exactRequestData = new
                {
                    text = "Файл загружен!\nИсходный текст:\nПриложение 1\nСпецификация МТРиО\nна поставку кабеля \n№ п/п\tПолное наименование МТРиО, тип, марка\tКл. безоп.\tКатегория сейсмос.\tКол-во\tЕд. изм\tСрок  поставки\tЦех-заказчик(справочно)\n1.\tКабель АВВГнг(A)-LS 4х120мс(N)-1\tОбщепром\t\t900\tМ\t01.12.2025 -29.12.2025.\tСП"
                };

                string jsonRequest = JsonConvert.SerializeObject(exactRequestData);
                txtOutput1.Text = "Подготавливаем запрос...";

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("accept", "text/plain");
                    client.Timeout = TimeSpan.FromMinutes(2); // Увеличиваем таймаут до 2 минут

                    txtOutput1.Text = "Отправляем тестовый запрос...";

                    var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                    // Добавляем обработку отмены
                    using (var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(2)))
                    {
                        try
                        {
                            HttpResponseMessage response = await client.PostAsync(API_URL, content, cancellationTokenSource.Token);
                            string responseContent = await response.Content.ReadAsStringAsync();

                            if (response.IsSuccessStatusCode)
                            {
                                txtOutput1.Text = $"✅ API работает!\nСтатус: {response.StatusCode}\nОтвет: {responseContent}";
                            }
                            else
                            {
                                txtOutput1.Text = $"❌ Ошибка API: {response.StatusCode}\n{responseContent}";
                            }
                        }
                        catch (TaskCanceledException)
                        {
                            txtOutput1.Text = "❌ Таймаут: Запрос отменен по времени. Проверьте:\n" +
                                             "1. Доступность API по адресу: http://185.177.216.82:5000\n" +
                                             "2. Настройки брандмауэра\n" +
                                             "3. Сетевое подключение";
                        }
                        catch (HttpRequestException httpEx)
                        {
                            txtOutput1.Text = $"❌ Ошибка сети: {httpEx.Message}\n" +
                                             "Проверьте сетевое подключение и доступность сервера";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                txtOutput1.Text = $"❌ Неожиданная ошибка: {ex.Message}\n\nДетали:\n{ex}";
            }
            finally
            {
                btnTestApi.IsEnabled = true;
                btnTestApi.Content = "Тест API";
            }
        }

        private async Task<bool> CheckServerAvailability()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(10);

                    // Пробуем разные эндпоинты
                    var endpoints = new[]
                    {
                "http://185.177.216.82:5000/",
                "http://185.177.216.82:5000/swagger",
                "http://185.177.216.82:5000/api/ProductAnalysis/analyze"
            };

                    foreach (var endpoint in endpoints)
                    {
                        try
                        {
                            var response = await client.GetAsync(endpoint);
                            if (response.IsSuccessStatusCode)
                            {
                                txtOutput1.Text = $"✅ Сервер доступен: {endpoint}";
                                return true;
                            }
                        }
                        catch { }
                    }

                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        private async void btnCheckServer_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                btnCheckServer.IsEnabled = false;
                btnCheckServer.Content = "Проверяем...";

                txtOutput1.Text = "Проверяем доступность сервера...";

                bool isAvailable = await CheckServerAvailability();

                if (!isAvailable)
                {
                    txtOutput1.Text = "❌ Сервер недоступен!\n\nВозможные причины:\n" +
                                     "• API сервер не запущен\n" +
                                     "• Проблемы с сетью\n" +
                                     "• Блокировка брандмауэром\n" +
                                     "• Неправильный адрес сервера";
                }
            }
            catch (Exception ex)
            {
                txtOutput1.Text = $"❌ Ошибка проверки: {ex.Message}";
            }
            finally
            {
                btnCheckServer.IsEnabled = true;
                btnCheckServer.Content = "Проверить сервер";
            }
        }
        private async void btnLoadRequest_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.Filter = "Документы Word (*.docx, *.dotx, *.docm, *.dotm)|*.docx;*.dotx;*.docm;*.dotm|" +
                                   "PDF файлы (*.pdf)|*.pdf|" +
                                   "Excel файлы (*.xlsx, *.xls, *.xlsm, *.xlsb, *.csv)|*.xlsx;*.xls;*.xlsm;*.xlsb;*.csv|" +
                                   "Все файлы (*.*)|*.*";

            if (openFileDialog.ShowDialog() == true)
            {
                string selectedFilePath = openFileDialog.FileName;
                Classes.Read.ReadRequst readRequst = new Classes.Read.ReadRequst();

                try
                {
                    btnLoadRequest.IsEnabled = false;
                    btnLoadRequest.Content = "Загрузка...";

                    // Читаем файл
                    _originalFileText = readRequst.ReadFileAll(selectedFilePath);

                    // Показываем только начало текста в txtOutput1
                    //string previewText = _originalFileText.Length > 1000
                    //    ? _originalFileText.Substring(0, 1000) + "...\n\n[текст сокращен для удобства просмотра]"
                    //    : _originalFileText;

                    txtOutput1.Text = $"Файл загружен!\nИсходный текст (превью):\n{_originalFileText}";

                    // Очищаем ListView перед началом загрузки
                    lstProducts.ItemsSource = new List<string> { "Начинаем анализ... Товары будут появляться по мере обработки" };

                    // Запускаем анализ - товары будут добавляться постепенно
                    string analysisResult = await AnalyzeViaApiAsync(_originalFileText);

                    // Финальное обновление статуса
                    var finalProducts = lstProducts.Items.Cast<string>().Where(x => !x.Contains("Начинаем анализ")).ToList();
                    UpdateStatus($"\n✅ Анализ завершен! Итоговое количество товаров: {finalProducts.Count}");

                    btnSearchProducts.IsEnabled = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    lstProducts.ItemsSource = new List<string> { $"Ошибка: {ex.Message}" };
                }
                finally
                {
                    btnLoadRequest.IsEnabled = true;
                    btnLoadRequest.Content = "Загрузить запрос";
                }
            }
        }

        private async void btnSearchProducts_Click(object sender, RoutedEventArgs e)
        {
            if (lstProducts.Items.Count == 0)
            {
                MessageBox.Show("Сначала загрузите файл с товарами", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                // Показываем индикатор загрузки
                btnSearchProducts.IsEnabled = false;
                btnSearchProducts.Content = "Поиск...";
                txtOutput3.Text = "Начинаем поиск товаров...";

                // Получаем список товаров из ListView
                var products = lstProducts.Items.Cast<string>().ToList();

                // Фильтруем служебные сообщения
                products = products.Where(p => !p.Contains("Начинаем анализ") && !p.Contains("Ошибка")).ToList();

                if (!products.Any())
                {
                    txtOutput3.Text = "Не удалось найти товары для поиска";
                    return;
                }

                var searchResults = new StringBuilder();
                searchResults.AppendLine($"Найдено товаров для поиска: {products.Count}");
                searchResults.AppendLine("==========================================");

                int processed = 0;
                foreach (var productName in products)
                {
                    if (string.IsNullOrWhiteSpace(productName)) continue;

                    // Ищем товар
                    var result = await _productSearch.SearchSimple(productName.Trim());

                    searchResults.AppendLine($"🔍 Поиск: {productName}");
                    searchResults.AppendLine($"📋 Результат: {result}");
                    searchResults.AppendLine("──────────────────────────────────────────");

                    processed++;

                    // Обновляем прогресс в реальном времени
                    txtOutput3.Text = $"Обработано {processed} из {products.Count} товаров...\n\n{searchResults}";

                    // Небольшая задержка чтобы не перегружать API
                    await Task.Delay(100);
                }

                searchResults.AppendLine($"✅ Поиск завершен! Обработано товаров: {processed}");
                txtOutput3.Text = searchResults.ToString();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при поиске товаров: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                txtOutput3.Text = $"Ошибка при поиске: {ex.Message}";
            }
            finally
            {
                // Восстанавливаем кнопку
                btnSearchProducts.IsEnabled = true;
                btnSearchProducts.Content = "Найти товары в системе";
            }
        }


        //private async Task<string> AnalyzeViaApiAsync(string text)
        //{
        //    try
        //    {
        //        // Разбиваем текст на строки
        //        string[] allLines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        //        // Группируем строки по 5 штук
        //        var lineGroups = new List<List<string>>();
        //        var currentGroup = new List<string>();

        //        foreach (var line in allLines)
        //        {
        //            currentGroup.Add(line);
        //            if (currentGroup.Count >= 5)
        //            {
        //                lineGroups.Add(currentGroup);
        //                currentGroup = new List<string>();
        //            }
        //        }

        //        // Добавляем последнюю группу, если она не пустая
        //        if (currentGroup.Count > 0)
        //        {
        //            lineGroups.Add(currentGroup);
        //        }

        //        UpdateStatus($"Разбили текст на {lineGroups.Count} групп по 5 строк...\n");

        //        var results = new List<string>();
        //        var tempProducts = new List<string>();

        //        // ОГРАНИЧИВАЕМ до 2 параллельных запросов
        //        var semaphore = new SemaphoreSlim(2);
        //        var tasks = new List<Task<string>>();

        //        for (int i = 0; i < lineGroups.Count; i++)
        //        {
        //            var lineGroup = lineGroups[i];
        //            var groupText = string.Join("\n", lineGroup);
        //            var groupNumber = i + 1;

        //            // Ждем свободный слот
        //            await semaphore.WaitAsync();

        //            tasks.Add(Task.Run(async () =>
        //            {
        //                try
        //                {
        //                    UpdateStatus($"Обрабатываем группу {groupNumber} из {lineGroups.Count} ({lineGroup.Count} строк)...");

        //                    var requestData = new { text = groupText };
        //                    string jsonRequest = JsonConvert.SerializeObject(requestData);
        //                    var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

        //                    using (var client = new HttpClient())
        //                    {
        //                        client.DefaultRequestHeaders.Add("accept", "text/plain");
        //                        client.Timeout = TimeSpan.FromMinutes(2);

        //                        var response = await client.PostAsync(API_URL, content);
        //                        string jsonResponse = await response.Content.ReadAsStringAsync();

        //                        if (response.IsSuccessStatusCode)
        //                        {
        //                            var responseObj = JsonConvert.DeserializeObject<dynamic>(jsonResponse);
        //                            string result = responseObj.analysisResult?.ToString();
        //                            txtOutput1.Text += result;

        //                            if (!string.IsNullOrEmpty(result))
        //                            {
        //                                UpdateStatus($"✅ Группа {groupNumber} обработана");

        //                                // НЕМЕДЛЕННО обрабатываем результат и добавляем в ListView
        //                                var productsFromGroup = ExtractProductsFromResult(result);
        //                                if (productsFromGroup.Any())
        //                                {
        //                                    lock (tempProducts)
        //                                    {
        //                                        tempProducts.AddRange(productsFromGroup);
        //                                        Dispatcher.Invoke(() =>
        //                                        {
        //                                            // Получаем текущие товары, исключая служебные сообщения
        //                                            var currentProducts = lstProducts.Items.Cast<string>()
        //                                                .Where(item => !item.Contains("Начинаем анализ") && !item.Contains("Ошибка"))
        //                                                .ToList();

        //                                            // Добавляем новые товары
        //                                            currentProducts.AddRange(productsFromGroup);

        //                                            // Обновляем ListView
        //                                            lstProducts.ItemsSource = currentProducts;
        //                                        });
        //                                    }
        //                                }

        //                                return $"--- Группа {groupNumber} ---\n{result}";
        //                            }
        //                        }
        //                        else
        //                        {
        //                            UpdateStatus($"❌ Ошибка в группе {groupNumber}: {response.StatusCode}");
        //                            return $"❌ Ошибка в группе {groupNumber}: {response.StatusCode}";
        //                        }
        //                    }
        //                    return string.Empty;
        //                }
        //                finally
        //                {
        //                    semaphore.Release();
        //                }
        //            }));

        //            // Небольшая задержка между запуском задач
        //            if (i < lineGroups.Count - 1)
        //            {
        //                await Task.Delay(200);
        //            }
        //        }

        //        // Ждем завершения ВСЕХ задач
        //        var completedResults = await Task.WhenAll(tasks);
        //        results.AddRange(completedResults.Where(r => !string.IsNullOrEmpty(r)));

        //        // ОБЪЕДИНЯЕМ все результаты
        //        string finalResult = string.Join("\n\n", results);
        //        UpdateStatus($"✅ Обработка завершена! Групп: {lineGroups.Count}. Найдено товаров: {tempProducts.Count}");

        //        return finalResult;
        //    }
        //    catch (Exception ex)
        //    {
        //        return $"❌ Ошибка обработки: {ex.Message}";
        //    }
        //}

        private async Task<string> AnalyzeViaApiAsync(string text)
        {
            try
            {
                // ВРЕМЕННАЯ ЗАГЛУШКА - ищем строки, начинающиеся с цифры
                var tempProducts = new List<string>();

                // Разбиваем текст на строки
                string[] allLines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                UpdateStatus($"Найдено строк: {allLines.Length}");

                int foundCount = 0;

                foreach (var line in allLines)
                {
                    var cleanLine = line.Trim();

                    // Ищем строки, которые начинаются с цифры
                    if (!string.IsNullOrEmpty(cleanLine) && char.IsDigit(cleanLine[0]))
                    {
                        // Пропускаем очевидно не товарные строки
                        if (cleanLine.StartsWith("---") ||
                            cleanLine.StartsWith("Раздел") ||
                            cleanLine.StartsWith("Подраздел") ||
                            cleanLine.StartsWith("Содержание") ||
                            cleanLine.StartsWith("Техническое") ||
                            cleanLine.Contains("Наименование") && cleanLine.Contains("Ед.изм."))
                        {
                            continue;
                        }

                        // ФЕЙКОВАЯ ЗАГРУЗКА - 1 секунда
                        UpdateStatus($"⏳ Обрабатываем строку: {Truncate(cleanLine, 50)}...");
                        await Task.Delay(1000);

                        tempProducts.Add(cleanLine);
                        foundCount++;
                        //UpdateStatus($"✅ Найдена строка с цифрой: {Truncate(cleanLine, 50)}");

                        // Немедленно обновляем ListView после каждой найденной строки
                        Dispatcher.Invoke(() =>
                        {
                            var currentProducts = lstProducts.Items.Cast<string>()
                                .Where(item => !item.Contains("Начинаем анализ") && !item.Contains("Ошибка"))
                                .ToList();
                            currentProducts.Add(cleanLine); // Добавляем по одной строке
                            lstProducts.ItemsSource = currentProducts;
                        });
                    }
                }

                //UpdateStatus($"✅ Заглушка: найдено строк с цифрами: {foundCount}");

                return $"Найдено строк с товарами: {foundCount}\n" + string.Join("\n", tempProducts);
            }
            catch (Exception ex)
            {
                return $"❌ Ошибка обработки: {ex.Message}";
            }
        }

        // Вспомогательный метод для обрезки длинного текста
        private string Truncate(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
                return text;
            return text.Substring(0, maxLength) + "...";
        }


        // Метод для извлечения товаров из результата одной части
        private List<string> ExtractProductsFromResult(string result)
        {
            var products = new List<string>();

            if (string.IsNullOrEmpty(result))
                return products;

            var lines = result.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var cleanLine = line.Trim();

                // Пропускаем строки, которые не начинаются с цифры
                if (string.IsNullOrEmpty(cleanLine) ||
                    !char.IsDigit(cleanLine[0]) ||
                    cleanLine.StartsWith("---") ||
                    cleanLine.StartsWith("Результат анализа") ||
                    cleanLine.StartsWith("Файл загружен!") ||
                    cleanLine.StartsWith("Исходный текст:"))
                {
                    continue;
                }

                // Добавляем товар в список
                products.Add(cleanLine);
            }

            return products;
        }

        // Метод для безопасного обновления UI
        private void UpdateStatus(string message)
        {
            Dispatcher.Invoke(() =>
            {
                txtOutput1.Text += message + "\n";
            });
        }

        private static List<string> SplitTextIntoParts(string text, int maxPartSize)
        {
            var parts = new List<string>();

            for (int i = 0; i < text.Length; i += maxPartSize)
            {
                int length = Math.Min(maxPartSize, text.Length - i);
                string part = text.Substring(i, length);
                parts.Add(part);
            }

            return parts;
        }

        // Метод для загрузки элементов в ListView (для обратной совместимости)
        private void LoadProductsToListView(string analysisResult)
        {
            try
            {
                var products = ExtractProductsFromResult(analysisResult);

                // Обновляем ListView
                lstProducts.ItemsSource = products;

                // Показываем количество загруженных товаров
                UpdateStatus($"✅ Загружено товаров: {products.Count}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке товаров: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void btnDebug_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                lstProducts.ItemsSource = new List<string> { "Дебаг..." };

                // Тест 1: Простой текст
                string result1 = await AnalyzeViaApiAsync("Кабель АВВГнг(A)-LS 4х120мс(N)-1");
                LoadProductsToListView(result1);

                // Тест 2: Текст из файла (ограниченный)
                if (!string.IsNullOrEmpty(_originalFileText))
                {
                    string result2 = await AnalyzeViaApiAsync(_originalFileText);
                    LoadProductsToListView(result2);
                }
            }
            catch (Exception ex)
            {
                lstProducts.ItemsSource = new List<string> { $"Дебаг ошибка: {ex.Message}" };
            }
        }

        private static string ParseApiResponse(string jsonResponse)
        {
            try
            {
                var responseObject = JsonConvert.DeserializeObject<dynamic>(jsonResponse);
                return responseObject.analysisResult?.ToString()?.Trim() ?? jsonResponse;
            }
            catch
            {
                return jsonResponse;
            }
        }
    }
}