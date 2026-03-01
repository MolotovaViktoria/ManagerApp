using ManagerApp.Classes.Read;
using ManagerApp.Data.ScharedData;
using ManagerApp.Data.StructureList;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ManagerApp.Pages
{
    /// <summary>
    /// Логика взаимодействия для DombPage.xaml
    /// </summary>
    public partial class DombPage : Page
    {
        // Информация для доступа к AI
        private const string ApiToken = "eyJhbGciOiJSUzUxMiIsInR5cCI6IkpXVCIsImtpZCI6IjFrYnhacFJNQGJSI0tSbE1xS1lqIn0.eyJ1c2VyIjoibXoxNjUxODMiLCJ0eXBlIjoiYXBpX2tleSIsImFwaV9rZXlfaWQiOiIxZmI0YWQ0NS0zYjBjLTRiMGQtODJjZS02NzQ0NzFkYWVhYTkiLCJpYXQiOjE3NzIzNzg2Mzl9.K0nQulksfXGzqPYOeudwSVbS2cv0ryHqRsVbElmNg87FuA8BOdUeBq5mPQCr-H0h3cXgg62CJNTfa1ULBd3yCC0y4POj2KbIe_gX_y1May08SC0YP9dQyFEhBgmtcIgOBAg-PvGwlOkkFnjKPxCjOsEkYe2Uf2NaSFqn3yjfZYydxrLTSk4DNlro0zZi7AbAEJvlrefj3fwDdSV3IJIQMApffWhlFpxiqQhmGURMlWdvREadoGY-rtmaZYFVOuZccJeKznQ5bmlZ4KgfRKViacAfVL6zDMP3jLQlWY7aw0ujOG13DUfrwAHSGWXXM-t6CaMQI4DHGjUzHHlZrXZ8h7565am41xxsaE0Alxi7y5vLQrkQvyhEXlWC9Ris3jIaKcIUCMvVrYQCIzPxUurEoKrnEZ8GlZM29mJXexFX_5BP0god0fY08sVZ2IgEu2kTiUJpthO3WyDxQLk3ALAQySYgrkx4YRM-h1YqK8nKnM6vy_E1sA0Jh3mcuVYHyZkS";
        private const string ApiUrl = "https://agent.timeweb.cloud/api/v1/cloud-ai/agents/7ed67ebb-f658-4716-ac81-35422c12cb21/v1/chat/completions";

        public DombPage()
        {
            InitializeComponent();
            SetupFileUploadHandlers();
        }

        private void SetupFileUploadHandlers()
        {
            // Настройка обработчиков drag-and-drop
            DropArea.Drop += DropArea_Drop;
            DropArea.DragEnter += DropArea_DragEnter;
            DropArea.DragLeave += DropArea_DragLeave;
            DropArea.AllowDrop = true;
        }

        // Обработчик клика по кнопке "Выберите файл"
        private void SelectFileButton_Click(object sender, RoutedEventArgs e)
        {
            LoadFile();
        }

        // Обработчик события DragEnter
        private void DropArea_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                DropArea.Opacity = 0.8;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        // Обработчик события DragLeave
        private void DropArea_DragLeave(object sender, DragEventArgs e)
        {
            DropArea.Opacity = 1.0;
        }

        // Обработчик события Drop
        private async void DropArea_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0)
                {
                    await ProcessFileAsync(files[0]);
                }
            }
            DropArea.Opacity = 1.0;
        }

        // Метод загрузки файла через диалог
        private async void LoadFile()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = CreateFileFilter(),
                Title = "Выберите файл",
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() == true)
            {
                await ProcessFileAsync(openFileDialog.FileName);
            }
        }

        // Создание фильтра для диалога выбора файла
        private string CreateFileFilter()
        {
            string filter = "Все поддерживаемые файлы (";

            // Добавляем Word форматы
            foreach (var format in FormatLists.WordFormatList)
            {
                filter += $"*{format};";
            }

            // Добавляем PDF формат
            filter += $"*{FormatLists.PdfFormatList[0]};";

            // Добавляем Excel форматы
            foreach (var format in FormatLists.ExcelFormatList)
            {
                filter += $"*{format};";
            }

            // Добавляем форматы изображений
            foreach (var format in FormatLists.ImageFormatList)
            {
                filter += $"*{format};";
            }

            filter = filter.TrimEnd(';') + ")|";

            // Формируем полный фильтр
            foreach (var format in FormatLists.WordFormatList)
            {
                filter += $"*{format};";
            }
            foreach (var format in FormatLists.PdfFormatList)
            {
                filter += $"*{format};";
            }
            foreach (var format in FormatLists.ExcelFormatList)
            {
                filter += $"*{format};";
            }
            foreach (var format in FormatLists.ImageFormatList)
            {
                filter += $"*{format};";
            }

            filter = filter.TrimEnd(';') + "|";

            // Отдельные фильтры для каждого типа
            filter += "Word документы (*.docx, *.dotx, *.docm, *.dotm)|*.docx;*.dotx;*.docm;*.dotm|";
            filter += "PDF документы (*.pdf)|*.pdf|";
            filter += "Excel файлы (*.xlsx, *.xls, *.xlsm, *.xlsb, *.csv)|*.xlsx;*.xls;*.xlsm;*.xlsb;*.csv|";
            filter += "Изображения (*.png, *.jpg, *.jpeg, *.bmp, *.gif, *.tiff, *.ico, *.webp)|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tiff;*.ico;*.webp|";
            filter += "Все файлы (*.*)|*.*";

            return filter;
        }

        private async Task ProcessFileAsync(string filePath)
        {
            // Получаем расширение файла
            string extension = System.IO.Path.GetExtension(filePath)?.ToLower();

            try
            {
                // Показываем индикатор загрузки (можно добавить визуальный элемент)
                Mouse.OverrideCursor = Cursors.Wait;

                // Читаем текст из файла
                string fileText = await ReadFileTextAsync(filePath, extension);

                if (string.IsNullOrWhiteSpace(fileText))
                {
                    MessageBox.Show("Не удалось извлечь текст из файла или файл пуст.",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Отправляем текст в AI и получаем список товаров
                List<string> products = await ExtractProductsFromTextAsync(fileText);

                if (products == null || products.Count == 0)
                {
                    MessageBox.Show("Не удалось извлечь товары из текста заявки.",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Сохраняем товары в менеджер
                ProductSelectionManager.SetProducts(products);

                // Переходим на страницу сравнения товаров
                ComparisonProduct comparisonPage = new ComparisonProduct(products);
                this.NavigationService.Navigate(comparisonPage);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обработке файла: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private async Task<string> ReadFileTextAsync(string filePath, string extension)
        {
            return await Task.Run(() =>
            {
                ReadRequst reader = new ReadRequst();

                if (FormatLists.ExcelFormatList.Contains(extension) ||
                    FormatLists.PdfFormatList.Contains(extension) ||
                    FormatLists.WordFormatList.Contains(extension))
                {
                    return reader.ReadFileAll(filePath);
                }
                else if (FormatLists.ImageFormatList.Contains(extension))
                {
                    // Для изображений возвращаем пустую строку или можно добавить OCR
                    return string.Empty;
                }

                return string.Empty;
            });
        }

        private async Task<List<string>> ExtractProductsFromTextAsync(string text)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    // Настраиваем заголовки
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {ApiToken}");

                    // Формируем запрос к AI
                    var requestBody = new
                    {
                        model = "gpt-4o-mini",
                        messages = new[]
                        {
                    new
                    {
                        role = "user",
                        content = $"Изучи внимательно текст заявки. Напиши наименование товаров. Каждое - с новой строки.\n\nТекст заявки:\n{text}"
                    }
                },
                        temperature = 0.3,
                        max_tokens = 1000
                    };

                    string jsonRequest = JsonSerializer.Serialize(requestBody);
                    var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                    // Отправляем запрос
                    HttpResponseMessage response = await client.PostAsync(ApiUrl, content);

                    if (response.IsSuccessStatusCode)
                    {
                        string jsonResponse = await response.Content.ReadAsStringAsync();

                        // Используем JsonDocument в блоке using со скобками
                        using (JsonDocument doc = JsonDocument.Parse(jsonResponse))
                        {
                            // Извлекаем текст ответа из структуры
                            if (doc.RootElement.TryGetProperty("choices", out JsonElement choices) &&
                                choices.GetArrayLength() > 0)
                            {
                                var firstChoice = choices[0];
                                if (firstChoice.TryGetProperty("message", out JsonElement message) &&
                                    message.TryGetProperty("content", out JsonElement contentElement))
                                {
                                    string aiResponse = contentElement.GetString();

                                    // Разбиваем ответ на строки и очищаем от лишних пробелов
                                    var products = aiResponse
                                        .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                                        .Select(p => p.Trim())
                                        .Where(p => !string.IsNullOrWhiteSpace(p))
                                        .ToList();

                                    return products;
                                }
                            }
                        }
                    }
                    else
                    {
                        string errorResponse = await response.Content.ReadAsStringAsync();
                        throw new Exception($"Ошибка API: {response.StatusCode}\n{errorResponse}");
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при обращении к AI: {ex.Message}", ex);
            }

            return new List<string>();
        }

        private void ProcessFile(string filePath)
        {
            // Этот метод больше не используется, оставляем для совместимости
            // Теперь используется асинхронная версия ProcessFileAsync
        }

        // Обработка файла изображения
        private void ProcessImageFile(string filePath)
        {
            try
            {
                PngPage pngPage = new PngPage(filePath);
                this.NavigationService.Navigate(pngPage);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии изображения: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Метод для открытия изображения (если нужно предпросмотр)
        private void OpenImageForViewing(string filePath)
        {
            try
            {
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(filePath);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();

                // Создаем окно для просмотра изображения
                Window imageWindow = new Window
                {
                    Title = System.IO.Path.GetFileName(filePath),
                    Width = 800,
                    Height = 600,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };

                Image imageControl = new Image
                {
                    Source = bitmap,
                    Stretch = Stretch.Uniform
                };

                ScrollViewer scrollViewer = new ScrollViewer
                {
                    Content = imageControl,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
                };

                imageWindow.Content = scrollViewer;
                imageWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось открыть изображение: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Проверка поддерживаемого формата
        private bool IsSupportedFormat(string extension)
        {
            return FormatLists.WordFormatList.Contains(extension) ||
                   FormatLists.PdfFormatList.Contains(extension) ||
                   FormatLists.ExcelFormatList.Contains(extension) ||
                   FormatLists.ImageFormatList.Contains(extension);
        }
    }
}