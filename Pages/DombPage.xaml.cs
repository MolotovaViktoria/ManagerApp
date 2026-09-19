// Добавляем using для работы с Excel
using ClosedXML.Excel;
using ManagerApp.Classes.Read;
using ManagerApp.Data.ScharedData;

using ManagerApp.Data.StructureList;
using Microsoft.Win32;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ManagerApp.Pages
{
    /// <summary>
    /// Логика взаимодействия для DombPage.xaml
    /// </summary>
    public partial class DombPage : Page
    {
        // Информация для доступа к AI - ОСНОВНОЙ
        private const string PrimaryApiToken = "eyJhbGciOiJSUzUxMiIsInR5cCI6IkpXVCIsImtpZCI6IjFrYnhacFJNQGJSI0tSbE1xS1lqIn0.eyJ1c2VyIjoibXoxNjUxODMiLCJ0eXBlIjoiYXBpX2tleSIsImFwaV9rZXlfaWQiOiIxZmI0YWQ0NS0zYjBjLTRiMGQtODJjZS02NzQ0NzFkYWVhYTkiLCJpYXQiOjE3NzIzNzg2Mzl9.K0nQulksfXGzqPYOeudwSVbS2cv0ryHqRsVbElmNg87FuA8BOdUeBq5mPQCr-H0h3cXgg62CJNTfa1ULBd3yCC0y4POj2KbIe_gX_y1May08SC0YP9dQyFEhBgmtcIgOBAg-PvGwlOkkFnjKPxCjOsEkYe2Uf2NaSFqn3yjfZYydxrLTSk4DNlro0zZi7AbAEJvlrefj3fwDdSV3IJIQMApffWhlFpxiqQhmGURMlWdvREadoGY-rtmaZYFVOuZccJeKznQ5bmlZ4KgfRKViacAfVL6zDMP3jLQlWY7aw0ujOG13DUfrwAHSGWXXM-t6CaMQI4DHGjUzHHlZrXZ8h7565am41xxsaE0Alxi7y5vLQrkQvyhEXlWC9Ris3jIaKcIUCMvVrYQCIzPxUurEoKrnEZ8GlZM29mJXexFX_5BP0god0fY08sVZ2IgEu2kTiUJpthO3WyDxQLk3ALAQySYgrkx4YRM-h1YqK8nKnM6vy_E1sA0Jh3mcuVYHyZkS";
        private const string PrimaryApiUrl = "https://agent.timeweb.cloud/api/v1/cloud-ai/agents/7ed67ebb-f658-4716-ac81-35422c12cb21/v1/chat/completions";

        // Информация для доступа к AI - ЗАПАСНОЙ (НОВЫЙ)
        private const string BackupApiToken = "eyJhbGciOiJSUzUxMiIsInR5cCI6IkpXVCIsImtpZCI6IjFrYnhacFJNQGJSI0tSbE1xS1lqIn0.eyJ1c2VyIjoibXoxNjUxODMiLCJ0eXBlIjoiYXBpX2tleSIsImFwaV9rZXlfaWQiOiI1ZjVmOGU0OC00ZjAyLTQwOTEtYTlkNC0zZmZjYmZiNGU3NGMiLCJpYXQiOjE3NzI3MTg4OTB9.zj0oLSbBNc6ZZgNfcJn8zsdtpSuYHvsdI7IwKjZqUa-9DGLfSq0cyGUxJmi-YcS2zpDal09gpkOhRLMssMJ29WWCNLWjt8mG92B5-MG9T8y6pAcg2s-wDOusO07jYWomR7eXWml8CvODB3hahArcOM6R_FqUNj1PUGnFNFz0JiIl1yu1idT0SfUO-iGam1aclujeX09PMKXUPcN33gOGXrdWTiFAG7vywZCv-vOaSDEsHH7tlVPggns0BbdgOOzrBqR8f1CiGFlvoo_xNr14sf1hvyDvAK5zf5qQl_JL2M7weSK9KUt3vpCqFoaLzgRp4ikQEX_08nKqf3I-cD_SgpDUhCAR8TgCTvsfjD3ABEgfGDfmuTld_MY2wqhLfPyWi1fr4TYiThcoyeGwL_U6mNnPz_lQUvm-zHem2oYAU3FyvKy2W-wO4YFu-DcZKjz_OY59aGM3l62SOxjh96n2eCSjdv9FF6Gu3344G9RumRzftDaTBUOJULVuu-_W4SiE";
        private const string BackupApiUrl = "https://agent.timeweb.cloud/api/v1/cloud-ai/agents/db9009b9-7858-4e0a-8568-d2bc975dbbe8/v1/chat/completions";

        private bool _handlersInitialized = false;
        private WaitingWindow _waitingWindow;

        // Показывает окно ожидания с сообщением о текущем этапе обработки
        // (без этого во время долгого запроса к ИИ страница выглядит зависшей/сломанной)
        private void ShowWaiting(string message)
        {
            Dispatcher.Invoke(() =>
            {
                if (_waitingWindow == null)
                {
                    _waitingWindow = new WaitingWindow("Обработка заявки", message)
                    {
                        Owner = Window.GetWindow(this)
                    };
                    _waitingWindow.Show();
                }
                else
                {
                    _waitingWindow.UpdateMessage(message);
                }
            });
        }

        private void CloseWaiting()
        {
            Dispatcher.Invoke(() =>
            {
                if (_waitingWindow != null)
                {
                    _waitingWindow.Close();
                    _waitingWindow = null;
                }
            });
        }

        public DombPage()
        {
            InitializeComponent();

            // Используем Dispatcher для отложенной инициализации
            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                InitializeHandlersSafely();
            }), DispatcherPriority.Loaded);
        }

        private void InitializeHandlersSafely()
        {
            try
            {
                if (!_handlersInitialized)
                {
                    SetupFileUploadHandlers();
                    _handlersInitialized = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при инициализации обработчиков: {ex.Message}");
            }
        }

        private void SetupFileUploadHandlers()
        {
            // Проверяем, что элемент не null перед настройкой обработчиков
            if (FileUploadArea != null)
            {
                FileUploadArea.AllowDrop = true;
                FileUploadArea.Drop += FileUploadArea_Drop;
                FileUploadArea.DragEnter += FileUploadArea_DragEnter;
                FileUploadArea.DragLeave += FileUploadArea_DragLeave;
            }
            else
            {
                // Если элемент все еще null, пробуем еще раз через Dispatcher
                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    SetupFileUploadHandlers();
                }), DispatcherPriority.ContextIdle);
            }
        }

        // Обработчик переключения способа ввода
        private void InputMethod_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                if (FileInputRadio?.IsChecked == true)
                {
                    if (FileUploadArea != null) FileUploadArea.Visibility = Visibility.Visible;
                    if (TextInputArea != null) TextInputArea.Visibility = Visibility.Collapsed;
                    if (FileNameText != null) FileNameText.Visibility = Visibility.Collapsed;
                }
                else
                {
                    if (FileUploadArea != null) FileUploadArea.Visibility = Visibility.Collapsed;
                    if (TextInputArea != null) TextInputArea.Visibility = Visibility.Visible;
                    if (FileNameText != null) FileNameText.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка в InputMethod_Checked: {ex.Message}");
            }
        }

        // Обработчик клика по кнопке "Выберите файл"
        private void SelectFileButton_Click(object sender, RoutedEventArgs e)
        {
            LoadFile();
        }

        // Обработчик клика по кнопке "Обработать текст"
        private async void ProcessTextButton_Click(object sender, RoutedEventArgs e)
        {
            string text = RequestTextBox?.Text?.Trim();

            if (string.IsNullOrWhiteSpace(text))
            {
                MessageBox.Show("Введите текст заявки для обработки.",
                    "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            await ProcessTextAsync(text);
        }

        // Обработчик события DragEnter для FileUploadArea
        private void FileUploadArea_DragEnter(object sender, DragEventArgs e)
        {
            try
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    e.Effects = DragDropEffects.Copy;
                    if (FileUploadArea != null)
                    {
                        FileUploadArea.Opacity = 0.8;
                        FileUploadArea.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFE8E0"));
                    }
                }
                else
                {
                    e.Effects = DragDropEffects.None;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка в FileUploadArea_DragEnter: {ex.Message}");
            }
        }

        // Обработчик события DragLeave для FileUploadArea
        private void FileUploadArea_DragLeave(object sender, DragEventArgs e)
        {
            try
            {
                if (FileUploadArea != null)
                {
                    FileUploadArea.Opacity = 1.0;
                    FileUploadArea.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF8F5"));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка в FileUploadArea_DragLeave: {ex.Message}");
            }
        }

        // Обработчик события Drop для FileUploadArea
        private async void FileUploadArea_Drop(object sender, DragEventArgs e)
        {
            try
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    if (files != null && files.Length > 0)
                    {
                        await ProcessFileAsync(files[0]);
                    }
                }

                if (FileUploadArea != null)
                {
                    FileUploadArea.Opacity = 1.0;
                    FileUploadArea.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF8F5"));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка в FileUploadArea_Drop: {ex.Message}");
            }
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
                ShowWaiting("Чтение файла...");

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
                    CloseWaiting();
                    PngPage pngPage = new PngPage(filePath);
                    this.NavigationService?.Navigate(pngPage);
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

                // Показываем имя файла
                if (FileNameText != null)
                {
                    FileNameText.Text = $"Загружен файл: {System.IO.Path.GetFileName(filePath)}";
                    FileNameText.Visibility = Visibility.Visible;
                }

                await ProcessTextAsync(fileText);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обработке файла: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
                CloseWaiting();
            }
        }

        private async Task ProcessTextAsync(string text)
        {
            try
            {
                Console.WriteLine($"=== PROCESS TEXT ===");
                Console.WriteLine($"Длина текста: {text.Length} символов");
                Console.WriteLine($"Первые 300 символов: {text.Substring(0, Math.Min(300, text.Length))}");
                Mouse.OverrideCursor = Cursors.Wait;
                ShowWaiting("Анализ текста с помощью ИИ...");

                // Отправляем текст в AI и получаем список товаров с полной информацией
                List<ExtractedProductInfo> products = await ExtractProductsFromTextWithFallbackAsync(text);

                if (products == null || products.Count == 0)
                {
                    MessageBox.Show("Не удалось извлечь товары из текста заявки.",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                ShowWaiting($"Найдено товаров: {products.Count}. Подстановка товаров...");

                // Выводим для отладки
                foreach (var p in products)
                {
                    Console.WriteLine($"Товар: {p.Name}");
                    Console.WriteLine($"  Количество: {p.Quantity} {p.MeasureSymbol}");
                    Console.WriteLine($"  Описание: {p.Description}");
                }

                // Сохраняем товары с полной информацией в менеджер
                ProductSelectionManager.SetExtractedProducts(products);

                // Переходим на страницу сравнения товаров
                ComparisonProduct comparisonPage = new ComparisonProduct();
                this.NavigationService?.Navigate(comparisonPage);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обработке текста: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
                CloseWaiting();
            }
        }

        /// <summary>
        /// Чтение Excel файла с использованием NPOI и ClosedXML
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
                    if (firstRow == null) continue;
                    for (int col = 1; col <= range.ColumnCount(); col++)
                    {
                        string headerText = "";
                        try { headerText = firstRow.Cell(col)?.GetString() ?? ""; } catch { }
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
                            string cellValue = "";
                            try { cellValue = worksheet.Cell(row, col)?.GetString() ?? ""; } catch { }
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

        private async Task<List<ExtractedProductInfo>> ExtractProductsFromTextWithFallbackAsync(string text)
        {
            // Для больших текстов разбиваем на части чтобы AI не обрезал ответ
            const int maxChunkSize = 15000;
            if (text.Length > maxChunkSize)
            {
                Console.WriteLine($"📦 Текст большой ({text.Length} символов), разбиваем на части");
                var chunkedProducts = await ExtractProductsFromLargeTextAsync(text);
                if (chunkedProducts != null && chunkedProducts.Count > 0)
                    return chunkedProducts;
                Console.WriteLine("⚠️ Чанкинг не дал результатов, пробуем единым запросом");
            }

            List<ExtractedProductInfo> products = null;
            string lastError = null;

            // Пробуем основной AI
            try
            {
                Console.WriteLine("🔄 Пробуем основной AI провайдер...");
                products = await ExtractProductsFromTextAsync(text, PrimaryApiToken, PrimaryApiUrl);
                if (products != null && products.Count > 0)
                {
                    Console.WriteLine($"✅ Основной AI вернул {products.Count} товаров");
                    return products;
                }
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                Console.WriteLine($"⚠️ Основной AI недоступен: {ex.Message}");
            }

            // Если основной не сработал, пробуем запасной
            try
            {
                Console.WriteLine("🔄 Пробуем запасной AI провайдер...");
                products = await ExtractProductsFromTextAsync(text, BackupApiToken, BackupApiUrl);
                if (products != null && products.Count > 0)
                {
                    Console.WriteLine($"✅ Запасной AI вернул {products.Count} товаров");
                    return products;
                }
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                Console.WriteLine($"⚠️ Запасной AI недоступен: {ex.Message}");
            }

            // Если оба AI недоступны, используем резервный метод извлечения
            Console.WriteLine("⚠️ Оба AI недоступны, используем резервный метод извлечения");
            return await ExtractProductsManuallyWithDetailsAsync(text);
        }

        // Обработка большого текста по частям
        private async Task<List<ExtractedProductInfo>> ExtractProductsFromLargeTextAsync(string text)
        {
            const int maxChunkSize = 15000;

            var allLines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            // Определяем заголовочные строки (первые строки с "===", "Наименование" и т.п.)
            var headerLines = new List<string>();
            int dataStart = 0;
            for (int i = 0; i < Math.Min(6, allLines.Length); i++)
            {
                string lower = allLines[i].ToLower();
                if (lower.Contains("=== лист") || lower.Contains("наименование") ||
                    lower.Contains("название") || lower.Contains("товар") ||
                    lower.Contains("кол-во") || lower.Contains("количество") ||
                    lower.Contains("ед.изм") || lower.Contains("цена"))
                {
                    headerLines.Add(allLines[i]);
                    dataStart = i + 1;
                }
                else if (headerLines.Count > 0)
                {
                    break;
                }
            }

            // Если заголовок не найден — берём первую строку
            if (headerLines.Count == 0 && allLines.Length > 0)
            {
                headerLines.Add(allLines[0]);
                dataStart = 1;
            }

            string header = string.Join("\n", headerLines) + "\n";

            // Разбиваем строки данных на чанки
            var chunks = new List<string>();
            var currentChunk = new StringBuilder(header);

            for (int i = dataStart; i < allLines.Length; i++)
            {
                string line = allLines[i] + "\n";
                if (currentChunk.Length + line.Length > maxChunkSize && currentChunk.Length > header.Length)
                {
                    chunks.Add(currentChunk.ToString());
                    currentChunk = new StringBuilder(header);
                }
                currentChunk.Append(line);
            }
            if (currentChunk.Length > header.Length)
                chunks.Add(currentChunk.ToString());

            Console.WriteLine($"📦 Разбито на {chunks.Count} частей по ~{maxChunkSize} символов");

            var allProducts = new List<ExtractedProductInfo>();
            // Дедупликация: имя+количество чтобы не добавлять одно и то же из разных чанков
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < chunks.Count; i++)
            {
                Console.WriteLine($"🔄 Обрабатываем часть {i + 1}/{chunks.Count}...");

                List<ExtractedProductInfo> chunkProducts = null;

                try
                {
                    chunkProducts = await ExtractProductsFromTextAsync(chunks[i], PrimaryApiToken, PrimaryApiUrl);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Основной AI ошибка для части {i + 1}: {ex.Message}");
                }

                if (chunkProducts == null || chunkProducts.Count == 0)
                {
                    try
                    {
                        chunkProducts = await ExtractProductsFromTextAsync(chunks[i], BackupApiToken, BackupApiUrl);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ Запасной AI ошибка для части {i + 1}: {ex.Message}");
                    }
                }

                if (chunkProducts != null)
                {
                    foreach (var product in chunkProducts)
                    {
                        if (string.IsNullOrWhiteSpace(product.Name)) continue;
                        string key = $"{product.Name.ToLower()}|{product.Quantity}";
                        if (!seen.Contains(key))
                        {
                            seen.Add(key);
                            allProducts.Add(product);
                        }
                    }
                }

                Console.WriteLine($"✅ Часть {i + 1}: извлечено {chunkProducts?.Count ?? 0}, всего {allProducts.Count}");
            }

            return allProducts;
        }

        private List<ExtractedProductInfo> ParseProductsManually(string text)
        {
            var products = new List<ExtractedProductInfo>();

            try
            {
                // Пробуем извлечь товары из текста простыми правилами
                var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (trimmed.Length < 5) continue;

                    // Проверяем, похоже ли на товар
                    if (Regex.IsMatch(trimmed, @"\d+[\.\)]\s+[А-Яа-яA-Za-z0-9\s\-\(\)]+") &&
                        !trimmed.Contains("ООО") && !trimmed.Contains("ИНН") &&
                        !trimmed.StartsWith("№") && !trimmed.StartsWith("п/п"))
                    {
                        var product = new ExtractedProductInfo
                        {
                            Name = trimmed,
                            Quantity = 1,
                            MeasureSymbol = "шт",
                            Description = ""
                        };
                        products.Add(product);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в ручном парсинге: {ex.Message}");
            }

            if (products.Count == 0)
            {
                products = GetSampleProducts();
            }

            return products;
        }
        private async Task<List<ExtractedProductInfo>> ExtractProductsFromTextAsync(string text, string apiToken, string apiUrl)
        {
            using (HttpClient client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(180);
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiToken}");

                string prompt = @"Ты - ИИ ассистент. Извлеки ВСЕ товары из этого текста ЛЮБЫМ способом.

ГЛАВНОЕ ПРАВИЛО: НЕ ПРОПУСТИ НИ ОДНОГО ТОВАРА! ДАЖЕ ЕСЛИ ИХ 1000 ШТУК!

Что считается товаром:
- Любое оборудование: розетки, выключатели, кабели, провода, лампы, стартеры, коробки, шины, штанги
- Любой продукт с ценой или количеством
- Любая строка, где есть название + число (количество)

Как найти товары (используй ВСЕ методы сразу):
1. Ищи в тексте слова: ШТ, шт., штука, м, метр, кг, литр, упак
2. Ищи числа рядом с этими словами - это количество
3. Название товара - это текст ДО количества и единицы измерения
4. Если таблица с колонками - найди столбцы ""Наименование"", ""Количество"", ""Кол-во"", ""Ед.изм."" и извлеки из них данные ТОЧНО
5. Если список с номерами (1., 2.) - бери текст после номера
6. Если просто перечисление - бери каждую строку

КОЛИЧЕСТВО: КРИТИЧЕСКИ ВАЖНО!
- Ищи столбец ""Количество"", ""Кол-во"", ""Кол."" в таблицах
- Ищи число перед единицей измерения: ""100 шт"", ""50 м"", ""5 кг""
- Ищи число в соседней ячейке таблицы после названия
- НЕ СТАВЬ 1 ПО УМОЛЧАНИЮ если количество есть в тексте - всегда ищи его
- Если количество действительно не указано - тогда ставь 1
- quantity ВСЕГДА должно быть числом (не строкой, не null)

Формат ответа (ТОЛЬКО JSON, НАЧИНАЙ С [ И ЗАКАНЧИВАЙ ]):
[
  {""name"": ""название товара"", ""quantity"": 100, ""measure"": ""шт/м/кг"", ""description"": ""описание если есть""}
]

НЕ ПИШИ НИЧЕГО, КРОМЕ JSON. НЕ ОБРЕЗАЙ ОТВЕТ. НЕ ПРОПУСКАЙ ТОВАРЫ.

Текст для анализа:
" + text + @"

НАЙДИ ВСЕ ТОВАРЫ С ТОЧНЫМ КОЛИЧЕСТВОМ! ВЕРНИ JSON МАССИВ СО ВСЕМИ!";

                var requestBody = new
                {
                    model = "gpt-4o-mini",
                    messages = new[] { new { role = "user", content = prompt } },
                    temperature = 0.0,
                    max_tokens = 32768
                };

                string jsonRequest = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await client.PostAsync(apiUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"AI Response длина: {jsonResponse.Length} символов");

                    using (JsonDocument doc = JsonDocument.Parse(jsonResponse))
                    {
                        if (doc.RootElement.TryGetProperty("choices", out JsonElement choices) &&
                            choices.GetArrayLength() > 0)
                        {
                            var firstChoice = choices[0];
                            if (firstChoice.TryGetProperty("message", out JsonElement message) &&
                                message.TryGetProperty("content", out JsonElement contentElement))
                            {
                                string aiResponse = contentElement.GetString();
                                var products = ParseAIResponseToProducts(aiResponse);

                                Console.WriteLine($"Извлечено товаров: {products.Count}");

                                //if (products.Count < 60)
                                //{
                                //    Console.WriteLine("⚠️ AI вернул мало товаров, пробуем улучшенный парсинг...");
                                //    products = await ExtractProductsManuallyWithDetailsAsync(text);
                                //}

                                return products;
                            }
                        }
                    }
                }
                else
                {
                    string errorResponse = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Ошибка API: {response.StatusCode} - {errorResponse}");
                }

                return new List<ExtractedProductInfo>();
            }
        }

        private List<ExtractedProductInfo> ParseAIResponseToProducts(string aiResponse)
        {
            var products = new List<ExtractedProductInfo>();

            try
            {
                // Пытаемся найти JSON в ответе
                int startIndex = aiResponse.IndexOf('[');
                int endIndex = aiResponse.LastIndexOf(']');

                if (startIndex >= 0 && endIndex > startIndex)
                {
                    string jsonPart = aiResponse.Substring(startIndex, endIndex - startIndex + 1);
                    var rawProducts = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(jsonPart);

                    foreach (var raw in rawProducts)
                    {
                        var product = new ExtractedProductInfo();

                        if (raw.ContainsKey("name") && raw["name"] != null)
                            product.Name = raw["name"].ToString().Trim();

                        if (raw.ContainsKey("quantity") && raw["quantity"] != null)
                        {
                            string qtyStr = raw["quantity"].ToString();
                            if (!string.IsNullOrEmpty(qtyStr) && qtyStr != "null" &&
                                decimal.TryParse(qtyStr, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal qty) && qty > 0)
                                product.Quantity = qty;
                        }

                        if (raw.ContainsKey("measure") && raw["measure"] != null)
                        {
                            string measure = raw["measure"].ToString().ToLower();
                            product.MeasureSymbol = NormalizeMeasureSymbol(measure);
                        }

                        if (raw.ContainsKey("description") && raw["description"] != null)
                            product.Description = raw["description"].ToString().Trim();

                        if (!string.IsNullOrWhiteSpace(product.Name))
                            products.Add(product);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка парсинга AI ответа: {ex.Message}");
                // Ручной парсинг как fallback
                products = ParseProductsManually(aiResponse);
            }

            return products;
        }
        private async Task<List<ExtractedProductInfo>> ExtractProductsManuallyWithDetailsAsync(string text)
        {
            return await Task.Run(() =>
            {
                var products = new List<ExtractedProductInfo>();
                var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

                // Регулярное выражение для поиска строк с товарами
                var productRegex = new Regex(
                    @"^(\d+)[\.\)]\s+(.+?)(?:\t|\|)\s*(\d+(?:[.,]\d+)?)\s*(?:[мшткгл]|шт\.?|м\.?|кг\.?|л\.?|упак\.?)",
                    RegexOptions.IgnoreCase | RegexOptions.Multiline
                );

                ExtractedProductInfo currentProduct = null;

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    // Поиск строки с номером и наименованием
                    var match = productRegex.Match(line);
                    if (match.Success)
                    {
                        // Сохраняем предыдущий товар
                        if (currentProduct != null && !string.IsNullOrWhiteSpace(currentProduct.Name))
                            products.Add(currentProduct);

                        currentProduct = new ExtractedProductInfo
                        {
                            Name = match.Groups[2].Value.Trim(),
                            Quantity = 1
                        };

                        // Извлекаем количество если есть
                        if (match.Groups[3].Success && decimal.TryParse(
                                match.Groups[3].Value.Replace(',', '.'),
                                NumberStyles.Any, CultureInfo.InvariantCulture, out decimal qty))
                            currentProduct.Quantity = qty;

                        // Извлекаем единицу измерения
                        string measureMatch = Regex.Match(line, @"(\d+(?:[.,]\d+)?)\s*([мшткгл]|шт\.?|м\.?|кг\.?|л\.?|упак\.?)", RegexOptions.IgnoreCase).Groups[2].Value;
                        if (!string.IsNullOrEmpty(measureMatch))
                            currentProduct.MeasureSymbol = NormalizeMeasureSymbol(measureMatch);
                    }
                    else if (currentProduct != null)
                    {
                        // Собираем характеристики из последующих строк
                        if (line.Contains("ГОСТ") || line.Contains("ТУ") ||
                            line.Contains("характеристик") || line.Contains("параметр") ||
                            line.Contains("Сечение") || line.Contains("Материал") ||
                            line.Contains("Напряжение") || line.Contains("температур"))
                        {
                            if (string.IsNullOrEmpty(currentProduct.Description))
                                currentProduct.Description = line;
                            else
                                currentProduct.Description += "; " + line;
                        }
                    }
                }

                // Добавляем последний товар
                if (currentProduct != null && !string.IsNullOrWhiteSpace(currentProduct.Name))
                    products.Add(currentProduct);

                // Если ничего не нашли, возвращаем пример
                if (products.Count == 0)
                {
                    products = GetSampleProducts();
                }

                return products;
            });
        }

        private List<ExtractedProductInfo> GetSampleProducts()
        {
            return new List<ExtractedProductInfo>
    {
      
        new ExtractedProductInfo
        {
            Name = "Товары не найдены",
            Quantity = 0,
            MeasureSymbol = "шт",
            Description = ""
        }
    };
        }
        private string NormalizeMeasureSymbol(string measure)
        {
            var measureMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "м", "м" }, { "метр", "м" }, { "m", "м" }, { "metr", "м" },
        { "шт", "шт" }, { "шт.", "шт" }, { "штука", "шт" }, { "pc", "шт" }, { "pcs", "шт" },
        { "кг", "кг" }, { "килограмм", "кг" }, { "kg", "кг" }, { "kilogram", "кг" },
        { "л", "л" }, { "литр", "л" }, { "l", "л" }, { "liter", "л" },
        { "упак", "упак" }, { "упаковка", "упак" }, { "pack", "упак" }
    };

            if (measureMap.TryGetValue(measure, out string normalized))
                return normalized;

            return "шт"; // По умолчанию
        }

        // РЕЗЕРВНЫЙ МЕТОД: Ручное извлечение товаров из текста
        private async Task<List<string>> ExtractProductsManuallyAsync(string text)
        {
            return await Task.Run(() =>
            {
                var products = new List<string>();

                // Разбиваем текст на строки
                var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var line in lines)
                {
                    string trimmed = line.Trim();

                    // Пропускаем служебную информацию
                    if (trimmed.StartsWith("№") || trimmed.StartsWith("п/п") ||
                        trimmed.StartsWith("Итого") || trimmed.StartsWith("Всего") ||
                        trimmed.Contains("телефон") || trimmed.Contains("email") ||
                        trimmed.Contains("факс") || trimmed.Contains("www.") ||
                        trimmed.Contains("http") || trimmed.Length < 5 ||
                        Regex.IsMatch(trimmed, @"^\d+$")) // Только цифры
                        continue;

                    // Если строка похожа на товар (содержит буквы и цифры)
                    if (Regex.IsMatch(trimmed, @"[а-яА-Яa-zA-Z]") && Regex.IsMatch(trimmed, @"\d"))
                    {
                        products.Add(trimmed);
                    }
                    // Или если это просто текст с названием товара
                    else if (Regex.IsMatch(trimmed, @"[а-яА-Яa-zA-Z]{3,}") &&
                             !trimmed.Contains("ООО") && !trimmed.Contains("ИНН"))
                    {
                        products.Add(trimmed);
                    }
                }

                // Если ничего не нашли, возвращаем тестовые данные
                if (products.Count == 0)
                {
                    products = new List<string>
                    {
                        "Кабель ВВГнг(А)-LS 3×2,5",
                        "Провод ПВС 2×1,5",
                        "Кабель ВВГнг-LS 5×6",
                        "Кабель ВВГнг-LS 3×1,5",
                        "Выключатель автоматический ВА47-29 16А",
                        "Розетка Schneider Electric 16А IP44",
                        "Светильник LED 40Вт IP65"
                    };
                }

                return products;
            });
        }
    }
}