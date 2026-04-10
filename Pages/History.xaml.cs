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
                    // ОТЛАДКА - выводим что пришло из парсинга
                    Console.WriteLine($"=== ИЗ ПАРСИНГА ===");
                    Console.WriteLine($"Название: {product.Name}");
                    Console.WriteLine($"Количество: {product.Quantity}");
                    Console.WriteLine($"Цена: {product.Price}");
                    Console.WriteLine($"Ед.изм: {product.MeasureSymbol}");

                    decimal priceWithoutVAT = product.Price * 0.78m;
                    priceWithoutVAT = Math.Round(priceWithoutVAT, 2);

                    matchedProducts.Add(new ManagerApp.Data.ScharedData.MatchedProduct
                    {
                        OriginalProductName = product.Name,
                        BitrixProductName = product.Name,
                        Quantity = product.Quantity,
                        ProductQuantity = product.Quantity,  // ← ДОБАВЬТЕ ЭТУ СТРОКУ
                        Unit = product.MeasureName,
                        UnitFullName = product.MeasureName,
                        MeasureSymbol = product.MeasureSymbol,
                        MeasureName = product.MeasureName,
                        MeasureId = product.MeasureId,
                        CustomPrice = priceWithoutVAT,
                        BitrixPrice = 0,
                        VAT = SettingsHelper.GetVATAsString()
                    });

                    Console.WriteLine($"=== ДОБАВЛЕНО В MatchedProduct с Quantity: {matchedProducts.Last().Quantity} ===");
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
        /// <summary>
        /// Извлечение товаров из текста счета (без AI)
        /// </summary>
        /// <summary>
        /// Извлечение товаров из текста счета (без AI)
        /// </summary>
        /// <summary>
        /// Извлечение товаров из текста счета (без AI)
        /// </summary>
        /// <summary>
        /// Извлечение товаров из текста счета (без AI)
        /// </summary>
        /// <summary>
        /// Извлечение товаров из текста счета (без AI)
        /// </summary>
        /// <summary>
        /// Извлечение товаров из текста счета (без AI)
        /// </summary>
        private List<ExtractedProductInfo> ExtractProductsFromInvoiceText(string text)
        {
            var products = new List<ExtractedProductInfo>();

            try
            {
                // Находим строки с товарами по паттерну: номер товара, потом название, потом число (количество)
                var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (string line in lines)
                {
                    // Ищем строки, которые начинаются с цифры и содержат "м" или "шт"
                    if (Regex.IsMatch(line, @"^\d+\s+") && (line.Contains("м") || line.Contains("шт")))
                    {
                        // Разбиваем строку по пробелам и табуляции
                        var parts = Regex.Split(line.Trim(), @"\s+");
                        if (parts.Length >= 5)
                        {
                            // Номер товара - первый элемент
                            string productName = "";
                            decimal quantity = 0;
                            string measure = "";
                            decimal price = 0;

                            // Ищем количество (целое число, не содержащее точку)
                            for (int i = 1; i < parts.Length; i++)
                            {
                                if (Regex.IsMatch(parts[i], @"^\d+$") && !parts[i].Contains("."))
                                {
                                    quantity = decimal.Parse(parts[i]);
                                    // Название - все что между номером и количеством
                                    productName = string.Join(" ", parts.Skip(1).Take(i - 1));

                                    // Единица измерения - следующий элемент после количества
                                    if (i + 1 < parts.Length && (parts[i + 1] == "м" || parts[i + 1] == "шт"))
                                    {
                                        measure = parts[i + 1];

                                        // Цена - следующий элемент после единицы измерения
                                        if (i + 2 < parts.Length)
                                        {
                                            string priceStr = parts[i + 2].Replace(".", ",");
                                            decimal.TryParse(priceStr, out price);
                                        }
                                    }
                                    break;
                                }
                            }

                            if (quantity > 0 && !string.IsNullOrEmpty(productName) && !string.IsNullOrEmpty(measure))
                            {
                                var product = new ExtractedProductInfo
                                {
                                    Name = productName.Trim(),
                                    Quantity = quantity,
                                    MeasureSymbol = measure == "м" ? "м" : "шт",
                                    MeasureName = GetMeasureFullName(measure),
                                    Description = "",
                                    Price = price
                                };
                                products.Add(product);
                                Console.WriteLine($"✅ Найден товар: {product.Name}, Количество: {product.Quantity}, Цена: {product.Price}");
                            }
                        }
                    }
                }

                Console.WriteLine($"=== ВСЕГО НАЙДЕНО ТОВАРОВ: {products.Count} ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка парсинга счета: {ex.Message}");
            }

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