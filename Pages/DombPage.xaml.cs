using ManagerApp.Classes.Read;
using ManagerApp.Data.ScharedData;
using ManagerApp.Data.StructureList;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
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

// Добавляем using для работы с Excel
using ClosedXML.Excel;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

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
                // Показываем индикатор загрузки
                Mouse.OverrideCursor = Cursors.Wait;

                string fileText = "";

                // Для Excel файлов используем NPOI и ClosedXML
                if (FormatLists.ExcelFormatList.Contains(extension))
                {
                    fileText = await ReadExcelFileAsync(filePath);
                }
                else if (FormatLists.PdfFormatList.Contains(extension) ||
                         FormatLists.WordFormatList.Contains(extension))
                {
                    // Для PDF и Word используем существующий метод
                    ReadRequst reader = new ReadRequst();
                    fileText = await Task.Run(() => reader.ReadFileAll(filePath));
                }
                else if (FormatLists.ImageFormatList.Contains(extension))
                {
                    // Для изображений переходим на PngPage
                    PngPage pngPage = new PngPage(filePath);
                    this.NavigationService.Navigate(pngPage);
                    return;
                }
                else
                {
                    MessageBox.Show($"Неподдерживаемый формат файла: {extension}",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

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

        /// <summary>
        /// Чтение Excel файла с использованием NPOI и ClosedXML (как в классе ExcelFile)
        /// </summary>
        private async Task<string> ReadExcelFileAsync(string filePath)
        {
            return await Task.Run(() =>
            {
                StringBuilder result = new StringBuilder();
                string extension = Path.GetExtension(filePath).ToLower();

                try
                {
                    if (extension == ".xls")
                    {
                        // Для старых .xls файлов используем NPOI HSSF
                        ReadXlsFileWithNpoi(filePath, result);
                    }
                    else if (extension == ".xlsx" || extension == ".xlsm" || extension == ".xlsb")
                    {
                        // Для новых .xlsx файлов используем ClosedXML
                        ReadXlsxFileWithClosedXml(filePath, result);
                    }
                    else
                    {
                        result.Append("Неподдерживаемый формат Excel файла");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при чтении Excel: {ex.Message}");
                    result.Append($"Ошибка чтения Excel: {ex.Message}");
                }

                return result.ToString();
            });
        }

        /// <summary>
        /// Чтение .xls файлов с помощью NPOI HSSF
        /// </summary>
        private void ReadXlsFileWithNpoi(string filePath, StringBuilder result)
        {
            IWorkbook workbook;

            using (FileStream file = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                workbook = new HSSFWorkbook(file);
            }

            for (int i = 0; i < workbook.NumberOfSheets; i++)
            {
                ISheet sheet = workbook.GetSheetAt(i);
                result.AppendLine($"=== Лист: {sheet.SheetName} ===");
                result.AppendLine();

                if (sheet.PhysicalNumberOfRows == 0)
                    continue;

                // Читаем заголовки из первой строки
                IRow headerRow = sheet.GetRow(0);
                if (headerRow != null)
                {
                    for (int col = headerRow.FirstCellNum; col < headerRow.LastCellNum; col++)
                    {
                        ICell cell = headerRow.GetCell(col);
                        string headerText = cell != null ? cell.ToString() : $"Column {col + 1}";
                        result.Append(headerText);
                        if (col < headerRow.LastCellNum - 1)
                            result.Append("\t");
                    }
                    result.AppendLine();
                }

                // Читаем данные со 2-й строки
                for (int row = 1; row <= sheet.LastRowNum; row++)
                {
                    IRow dataRow = sheet.GetRow(row);
                    if (dataRow == null) continue;

                    for (int col = dataRow.FirstCellNum; col < dataRow.LastCellNum; col++)
                    {
                        ICell cell = dataRow.GetCell(col);
                        string cellValue = cell != null ? cell.ToString() : "";
                        result.Append(cellValue);
                        if (col < dataRow.LastCellNum - 1)
                            result.Append("\t");
                    }
                    result.AppendLine();
                }
                result.AppendLine();
                result.AppendLine();
            }
        }

        /// <summary>
        /// Чтение .xlsx файлов с помощью ClosedXML
        /// </summary>
        private void ReadXlsxFileWithClosedXml(string filePath, StringBuilder result)
        {
            using (var workbook = new XLWorkbook(filePath))
            {
                foreach (var worksheet in workbook.Worksheets)
                {
                    result.AppendLine($"=== Лист: {worksheet.Name} ===");
                    result.AppendLine();

                    var range = worksheet.RangeUsed();
                    if (range == null) continue;

                    // Читаем заголовки из первой строки
                    var firstRow = range.FirstRow();
                    for (int col = 1; col <= range.ColumnCount(); col++)
                    {
                        string headerText = firstRow.Cell(col).GetString();
                        if (string.IsNullOrEmpty(headerText))
                            headerText = $"Column {col}";
                        result.Append(headerText);
                        if (col < range.ColumnCount())
                            result.Append("\t");
                    }
                    result.AppendLine();

                    // Читаем данные со 2-й строки
                    for (int row = 2; row <= range.RowCount(); row++)
                    {
                        for (int col = 1; col <= range.ColumnCount(); col++)
                        {
                            string cellValue = worksheet.Cell(row, col).GetString();
                            result.Append(cellValue);
                            if (col < range.ColumnCount())
                                result.Append("\t");
                        }
                        result.AppendLine();
                    }
                    result.AppendLine();
                    result.AppendLine();
                }
            }
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
                                content = $"Проанализируй текст заявки и выдели список товаров. Товары могут быть указаны в виде таблицы, списка или простого текста.\n\n" +
          $"Правила:\n" +
          $"1. В каждой заявке обязательно есть товары (их не может быть 0)\n" +
          $"2. Названия товаров могут находиться в разных частях документа: в таблицах, списках, абзацах\n" +

          $"3. Игнорируй техническую информацию: даты, номера документов, реквизиты, адреса, телефоны\n" +

          $"4. Выведи ТОЛЬКО названия товаров, каждое с новой строки\n" +
          $"5. НЕ добавляй никаких пояснений, предисловий или комментариев\n\n" +
          $"Текст заявки:\n{text}"
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
                                        .Where(p => !string.IsNullOrWhiteSpace(p) && !p.StartsWith("```"))
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