using ManagerApp.Classes.Read;
using ManagerApp.Classes.Setting;
using ManagerApp.Data.ScharedData;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ManagerApp.Pages
{
    public partial class History : Page
    {
        private string historyFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "History");
        private ObservableCollection<InvoiceFile> invoiceFiles = new ObservableCollection<InvoiceFile>();

        public History()
        {
            InitializeComponent();
            Loaded += History_Loaded;
        }

        private void History_Loaded(object sender, RoutedEventArgs e)
        {
            LoadHistoryFiles();
        }

        private void LoadHistoryFiles()
        {
            try
            {
                invoiceFiles.Clear();

                if (!Directory.Exists(historyFolderPath))
                {
                    Directory.CreateDirectory(historyFolderPath);
                    ShowNoFilesMessage();
                    return;
                }

                // Получаем все папки с датами
                var dateFolders = Directory.GetDirectories(historyFolderPath)
                    .OrderByDescending(f => f);

                if (!dateFolders.Any())
                {
                    ShowNoFilesMessage();
                    return;
                }

                foreach (var dateFolder in dateFolders)
                {
                    string folderName = Path.GetFileName(dateFolder);

                    // Получаем все Word файлы в папке
                    var wordFiles = Directory.GetFiles(dateFolder, "*.docx")
                        .OrderByDescending(f => File.GetCreationTime(f));

                    foreach (var file in wordFiles)
                    {
                        var fileInfo = new FileInfo(file);
                        invoiceFiles.Add(new InvoiceFile
                        {
                            FileName = Path.GetFileNameWithoutExtension(file),
                            FullPath = file,
                            Date = folderName,
                            CreatedTime = fileInfo.CreationTime,
                            FileSize = fileInfo.Length
                        });
                    }
                }

                if (invoiceFiles.Count == 0)
                {
                    ShowNoFilesMessage();
                }
                else
                {
                    listInvoices.ItemsSource = invoiceFiles;
                    listInvoices.Visibility = Visibility.Visible;
                    txtNoFiles.Visibility = Visibility.Collapsed;
                    txtFilesCount.Text = $"Найдено счетов: {invoiceFiles.Count}";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки истории: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowNoFilesMessage()
        {
            txtNoFiles.Visibility = Visibility.Visible;
            listInvoices.Visibility = Visibility.Collapsed;
            txtFilesCount.Text = "Счетов не найдено";
        }

        // Двойной клик по файлу
        private void listInvoices_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            OpenInvoiceFromFile();
        }

        // Одиночный клик + кнопка или просто открытие
        private void listInvoices_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Можно добавить логику при выделении, если нужно
        }

        // Метод для открытия счета и загрузки в ComparisonProduct
        // Метод для открытия счета и загрузки в EditPricePage
        private async void OpenInvoiceFromFile()
        {
            var selectedFile = listInvoices.SelectedItem as InvoiceFile;
            if (selectedFile == null)
            {
                MessageBox.Show("Выберите счет для открытия", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                // Читаем текст из Word файла
                ReadRequst reader = new ReadRequst();
                string fileText = await Task.Run(() => reader.ReadFileAll(selectedFile.FullPath));

                if (string.IsNullOrWhiteSpace(fileText))
                {
                    MessageBox.Show("Не удалось прочитать файл счета.", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Извлекаем товары из текста счета
                List<ExtractedProductInfo> products = ExtractProductsFromInvoiceText(fileText);

                if (products == null || products.Count == 0)
                {
                    MessageBox.Show("Не удалось извлечь товары из счета. Возможно, файл имеет нестандартный формат.",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Конвертируем в MatchedProduct для EditPricePage
                var matchedProducts = new List<ManagerApp.Data.ScharedData.MatchedProduct>();
                foreach (var product in products)
                {
                    matchedProducts.Add(new ManagerApp.Data.ScharedData.MatchedProduct
                    {
                        OriginalProductName = product.Name,
                        BitrixProductName = product.Name,
                        Quantity = product.Quantity,
                        Unit = product.MeasureName,
                        UnitFullName = product.MeasureName,
                        MeasureSymbol = product.MeasureSymbol,
                        MeasureName = product.MeasureName,
                        MeasureId = product.MeasureId,
                        CustomPrice = 0, // Цена будет заполнена в EditPricePage
                        BitrixPrice = 0,
                        VAT = SettingsHelper.GetVATAsString()
                    });
                }

                // Сохраняем в менеджер
                PriceDataManager.SetMatchedProducts(matchedProducts);

                // Переходим на страницу редактирования цен
                EditPricePage editPricePage = new EditPricePage(matchedProducts);
                NavigationService?.Navigate(editPricePage);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии счета: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        /// <summary>
        /// Извлечение товаров из текста счета (без AI)
        /// </summary>
        private List<ExtractedProductInfo> ExtractProductsFromInvoiceText(string text)
        {
            var products = new List<ExtractedProductInfo>();

            try
            {
                // Ищем таблицу с товарами в счете
                // Обычно таблица начинается после строки "№	Товары (работы, услуги)	Количество	Цена	Сумма"
                int tableStart = text.IndexOf("№");
                if (tableStart == -1) tableStart = text.IndexOf("Товары");

                int tableEnd = text.IndexOf("Итого:", tableStart);
                if (tableEnd == -1) tableEnd = text.IndexOf("Всего наименований", tableStart);
                if (tableEnd == -1) tableEnd = text.Length;

                if (tableStart >= 0)
                {
                    string tableText = text.Substring(tableStart, Math.Min(tableEnd - tableStart, 10000));
                    var lines = tableText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

                    // Регулярное выражение для поиска строки товара
                    // Формат: "1	Название товара	1	шт	20.33	20.33"
                    var productRegex = new Regex(
                        @"^(\d+)\s+(.+?)\s+(\d+(?:[.,]\d+)?)\s+(шт|м|кг|л|упак|ШТ|М|КГ|Л)\s+(\d+(?:[.,]\d+)?)\s+(\d+(?:[.,]\d+)?)",
                        RegexOptions.IgnoreCase | RegexOptions.Multiline);

                    foreach (string line in lines)
                    {
                        var match = productRegex.Match(line);
                        if (match.Success)
                        {
                            string name = match.Groups[2].Value.Trim();
                            string quantityStr = match.Groups[3].Value.Replace('.', ',');
                            string measure = match.Groups[4].Value.ToLower();

                            if (measure == "шт") measure = "шт";
                            else if (measure == "м") measure = "м";
                            else if (measure == "кг") measure = "кг";
                            else if (measure == "л") measure = "л";
                            else if (measure == "упак") measure = "упак";

                            decimal quantity = 1;
                            if (decimal.TryParse(quantityStr, out decimal qty))
                                quantity = qty;

                            var product = new ExtractedProductInfo
                            {
                                Name = name,
                                Quantity = quantity,
                                MeasureSymbol = measure,
                                MeasureName = GetMeasureFullName(measure),
                                Description = "" // В счете обычно нет подробного описания
                            };

                            products.Add(product);
                            Console.WriteLine($"Найден товар: {product.Name} - {product.Quantity} {product.MeasureSymbol}");
                        }
                    }
                }

                // Если регуляркой не нашли, пробуем другой подход - ищем строки с табуляцией
                if (products.Count == 0)
                {
                    var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        // Ищем строки с цифрами и "шт"
                        if (line.Contains("шт") || line.Contains("ШТ"))
                        {
                            var parts = line.Split(new[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 3)
                            {
                                string name = parts[0].Trim();
                                // Убираем номер в начале
                                name = Regex.Replace(name, @"^\d+\s+", "");

                                string quantityStr = "";
                                string measure = "шт";

                                for (int i = 0; i < parts.Length; i++)
                                {
                                    if (parts[i].Trim().ToLower() == "шт" && i > 0)
                                    {
                                        quantityStr = parts[i - 1].Trim();
                                        break;
                                    }
                                }

                                if (string.IsNullOrEmpty(quantityStr) && parts.Length > 1)
                                {
                                    quantityStr = parts[1].Trim();
                                }

                                decimal quantity = 1;
                                if (decimal.TryParse(quantityStr, out decimal qty))
                                    quantity = qty;

                                if (!string.IsNullOrEmpty(name) && name.Length > 2)
                                {
                                    var product = new ExtractedProductInfo
                                    {
                                        Name = name,
                                        Quantity = quantity,
                                        MeasureSymbol = measure,
                                        MeasureName = GetMeasureFullName(measure),
                                        Description = ""
                                    };
                                    products.Add(product);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка парсинга счета: {ex.Message}");
            }

            // Удаляем дубликаты
            products = products
                .GroupBy(p => p.Name)
                .Select(g => g.First())
                .ToList();

            return products;
        }

        private string GetMeasureFullName(string symbol)
        {
            var measureMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "м", "Метр" },
                { "шт", "Штука" },
                { "кг", "Килограмм" },
                { "л", "Литр" },
                { "упак", "Упаковка" }
            };
            return measureMap.ContainsKey(symbol) ? measureMap[symbol] : "Штука";
        }

        private void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadHistoryFiles();
        }

        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = txtSearch.Text.ToLower();

            if (string.IsNullOrWhiteSpace(searchText))
            {
                listInvoices.ItemsSource = invoiceFiles;
                txtFilesCount.Text = $"Найдено счетов: {invoiceFiles.Count}";
                return;
            }

            var filtered = invoiceFiles.Where(f =>
                f.FileName.ToLower().Contains(searchText) ||
                f.Date.ToLower().Contains(searchText)).ToList();

            listInvoices.ItemsSource = filtered;
            txtFilesCount.Text = $"Найдено: {filtered.Count}";
        }

        // Класс для отображения информации о файле
        public class InvoiceFile
        {
            public string FileName { get; set; }
            public string FullPath { get; set; }
            public string Date { get; set; }
            public DateTime CreatedTime { get; set; }
            public long FileSize { get; set; }

            public string FileSizeFormatted
            {
                get
                {
                    if (FileSize < 1024) return $"{FileSize} Б";
                    if (FileSize < 1024 * 1024) return $"{(FileSize / 1024.0):0.0} КБ";
                    return $"{(FileSize / (1024.0 * 1024.0)):0.0} МБ";
                }
            }

            public string CreatedTimeFormatted => CreatedTime.ToString("HH:mm:ss");
        }
    }
}