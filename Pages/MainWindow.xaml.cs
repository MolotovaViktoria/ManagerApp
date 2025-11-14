using ManagerApp.Classes.ModelsStudy;
using ManagerApp.Classes.Normalizer;
using ManagerApp.Classes.Read;
using ManagerApp.Classes.Search;
using ManagerApp.Data.GetInfo;
using ManagerApp.Data.StructureList;
using Newtonsoft.Json;
using Org.BouncyCastle.Asn1.Pkcs;
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
        private string _originalFileText;
        private const string API_URL = "https://localhost:7199/api/ProductAnalysis/analyze";

        public MainWindows()
        {
            InitializeComponent();
            _bitrixService = new BitrixService();
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

                    // СОХРАНЯЕМ исходный текст в переменную
                    _originalFileText = readRequst.ReadFileAll(selectedFilePath);

                    // Показываем исходный текст
                    txtOutput1.Text = $"Файл загружен!\nИсходный текст:\n{_originalFileText}";

                    // Анализируем текст через API
                    string analysisResult = await AnalyzeViaApiAsync(_originalFileText);

                    // Показываем результат анализа
                    txtOutput2.Text = $"Результат анализа API:\n{analysisResult}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при обработке файла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    txtOutput2.Text = $"Ошибка: {ex.Message}";
                }
                finally
                {
                    // Восстанавливаем кнопку
                    btnLoadRequest.IsEnabled = true;
                    btnLoadRequest.Content = "Загрузить запрос";
                }
            }
        }

        public static async Task<string> AnalyzeViaApiAsync(string text)
        {
            var requestData = new
            {
                text = text
            };

            string jsonRequest = JsonConvert.SerializeObject(requestData);
            var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

            // Игнорируем SSL ошибки для локальной разработки
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };

            using (var client = new HttpClient(handler))
            {
                HttpResponseMessage response = await client.PostAsync(API_URL, content);

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"Ошибка API: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
                }

                string jsonResponse = await response.Content.ReadAsStringAsync();
                return ParseApiResponse(jsonResponse);
            }
        }

        private static string ParseApiResponse(string jsonResponse)
        {
            // Используем Newtonsoft.Json вместо System.Text.Json для совместимости с C# 7.3
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
                // Если десериализация не удалась, возможно это просто строка
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