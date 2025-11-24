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
    /// <summary>
    /// Логика взаимодействия для MainWindows.xaml
    /// </summary>
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

        private async Task<bool> CheckApiAvailability()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(10);
                    var response = await client.GetAsync("http://185.177.216.82:5000/swagger/index.html");
                    return response.IsSuccessStatusCode;
                }
            }
            catch
            {
                return false;
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
                    // Показываем индикатор загрузки
                    btnLoadRequest.IsEnabled = false;
                    btnLoadRequest.Content = "Загрузка...";

                    // Проверяем доступность API
                    txtOutput1.Text = "Проверяем доступность API...";
                    bool isApiAvailable = await CheckApiAvailability();

                    if (!isApiAvailable)
                    {
                        MessageBox.Show($"API недоступно по адресу {API_URL}. Пожалуйста, проверьте:\n\n1. Запущен ли API сервер\n2. Доступность сети\n3. Файрволы и антивирусы", "Ошибка подключения", MessageBoxButton.OK, MessageBoxImage.Warning);
                        txtOutput1.Text = "API недоступно. Проверьте подключение и настройки.";
                        return;
                    }

                    // СОХРАНЯЕМ исходный текст в переменную
                    _originalFileText = readRequst.ReadFileAll(selectedFilePath);

                    // Показываем исходный текст
                    txtOutput1.Text = $"Файл загружен!\nИсходный текст:\n{_originalFileText}";

                    // Показываем прогресс анализа
                    txtOutput2.Text = "Анализируем текст через API...";

                    // Анализируем текст через API
                    string analysisResult = await AnalyzeViaApiAsync(_originalFileText);

                    // Показываем результат анализа
                    txtOutput2.Text = $"Результат анализа API:\n{analysisResult}";

                    // Активируем кнопку поиска
                    btnSearchProducts.IsEnabled = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при обработке файла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    txtOutput2.Text = $"Ошибка: {ex.Message}";

                    // Показываем подсказку для пользователя
                    if (ex.Message.Contains("таймаут") || ex.Message.Contains("Timeout"))
                    {
                        txtOutput2.Text += $"\n\nРекомендации:\n1. Проверьте доступность API по адресу: {API_URL}\n2. Убедитесь что API сервер запущен\n3. Проверьте настройки сети";
                    }
                }
                finally
                {
                    // Восстанавливаем кнопку
                    btnLoadRequest.IsEnabled = true;
                    btnLoadRequest.Content = "Загрузить запрос";
                }
            }
        }

        private async void btnSearchProducts_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtOutput2.Text))
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

                // Получаем список товаров из txtOutput2
                var products = ExtractProductsFromText(txtOutput2.Text);

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

        /// <summary>
        /// Извлекает список товаров из текста (каждая строка - отдельный товар)
        /// </summary>
        private List<string> ExtractProductsFromText(string text)
        {
            var products = new List<string>();

            if (string.IsNullOrEmpty(text)) return products;

            // Разбиваем текст на строки
            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var cleanLine = line.Trim();

                // Пропускаем пустые строки и служебную информацию
                if (string.IsNullOrEmpty(cleanLine) ||
                    cleanLine.StartsWith("Результат анализа API:") ||
                    cleanLine.StartsWith("Файл загружен!") ||
                    cleanLine.StartsWith("Исходный текст:") ||
                    cleanLine.Length < 2) // Слишком короткие строки
                {
                    continue;
                }

                products.Add(cleanLine);
            }

            return products;
        }

        public static async Task<string> AnalyzeViaApiAsync(string text)
        {
            var requestData = new
            {
                text = text
            };

            string jsonRequest = JsonConvert.SerializeObject(requestData);
            var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

            using (var client = new HttpClient())
            {
                // Добавляем таймаут 5 минут
                client.Timeout = TimeSpan.FromMinutes(5);

                try
                {
                    HttpResponseMessage response = await client.PostAsync(API_URL, content);

                    if (!response.IsSuccessStatusCode)
                    {
                        string errorContent = await response.Content.ReadAsStringAsync();
                        throw new HttpRequestException($"Ошибка API: {response.StatusCode} - {errorContent}");
                    }

                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    return ParseApiResponse(jsonResponse);
                }
                catch (TaskCanceledException ex)
                {
                    throw new HttpRequestException($"Таймаут подключения к API. Убедитесь что API запущено на {API_URL}", ex);
                }
                catch (HttpRequestException ex)
                {
                    throw new HttpRequestException($"Ошибка подключения к API: {ex.Message}. Убедитесь что API запущено на {API_URL}", ex);
                }
            }
        }

        private static string ParseApiResponse(string jsonResponse)
        {
            try
            {
                var responseObject = JsonConvert.DeserializeObject<dynamic>(jsonResponse);

                if (responseObject.analysisResult != null)
                {
                    return responseObject.analysisResult?.ToString()?.Trim() ?? "Пустой ответ от API";
                }

                // Если ответ является простой строкой
                if (responseObject is string)
                {
                    return responseObject?.ToString()?.Trim() ?? "Пустой ответ";
                }

                // Альтернативный вариант - если структура другая
                if (responseObject.result != null)
                {
                    return responseObject.result?.ToString()?.Trim() ?? "Пустой ответ от API";
                }

                throw new Exception("Не удалось распарсить ответ от API");
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrWhiteSpace(jsonResponse))
                {
                    return jsonResponse.Trim();
                }
                throw new Exception($"Не удалось распарсить ответ от API: {ex.Message}");
            }
        }

        // Старый метод для обработки через ProductListIdentifier (оставлен для обратной совместимости)
        private string ProcessTextWithProductIdentifier(string text)
        {
            try
            {
                var productIdentifier = new ProductListIdentifier();
                string result = productIdentifier.ExtractProductList(text);

                if (string.IsNullOrEmpty(result))
                {
                    return "Список товаров не обнаружен в документе.";
                }

                return result;
            }
            catch (Exception ex)
            {
                return $"Ошибка при обработке текста: {ex.Message}";
            }
        }
    }
}