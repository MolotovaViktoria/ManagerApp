using ManagerApp.Classes.Setting;
using ManagerApp.Data.ScharedData;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using static ManagerApp.Pages.NullableDecimalConverter;

namespace ManagerApp.Pages
{
    public partial class EditPricePage : Page, INotifyPropertyChanged
    {
        public ObservableCollection<ProductPriceViewModel> Products { get; set; }

        // Словарь для хранения данных об единицах измерения из Bitrix
        private Dictionary<string, BitrixMeasure> _measuresDictionary = new Dictionary<string, BitrixMeasure>();
        private bool _measuresLoaded = false;
        private readonly object _measuresLock = new object();

        private decimal _totalSum;
        private ObservableCollection<BitrixMeasure> _allMeasures;
        public ObservableCollection<BitrixMeasure> AllMeasures
        {
            get => _allMeasures;
            set
            {
                _allMeasures = value;
                OnPropertyChanged(nameof(AllMeasures));
            }
        }
        public decimal TotalSum
        {
            get => _totalSum;
            set
            {
                _totalSum = value;
                OnPropertyChanged(nameof(TotalSum));
            }
        }

        private async void CustomPriceTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var textBox = sender as TextBox;
                if (textBox != null)
                {
                    // Сохраняем значение в текущем TextBox
                    textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();

                    // Получаем текущую строку
                    var currentRow = FindParent<DataGridRow>(textBox);
                    if (currentRow != null)
                    {
                        var dataGrid = FindParent<DataGrid>(textBox);
                        if (dataGrid != null)
                        {
                            // Получаем индекс текущей строки
                            int currentIndex = dataGrid.Items.IndexOf(currentRow.DataContext);

                            // Проверяем, есть ли следующая строка
                            if (currentIndex + 1 < dataGrid.Items.Count)
                            {
                                var nextItem = dataGrid.Items[currentIndex + 1];

                                // Сначала снимаем фокус с текущего TextBox
                                Keyboard.ClearFocus();

                                // Небольшая задержка перед переключением
                                await Task.Delay(50);

                                // Перемещаемся на следующую строку
                                dataGrid.SelectedItem = nextItem;
                                dataGrid.ScrollIntoView(nextItem);

                                // Ждем отрисовки
                                await Task.Delay(50);

                                // Находим ячейку "Своя цена" и переводим в режим редактирования
                                var nextRow = dataGrid.ItemContainerGenerator.ContainerFromItem(nextItem) as DataGridRow;
                                if (nextRow == null)
                                {
                                    // Если строка еще не создана, принудительно прокручиваем
                                    dataGrid.ScrollIntoView(nextItem);
                                    await Task.Delay(50);
                                    nextRow = dataGrid.ItemContainerGenerator.ContainerFromItem(nextItem) as DataGridRow;
                                }

                                if (nextRow != null)
                                {
                                    // Находим ячейку в колонке "Своя цена" (индекс 7)
                                    var cell = GetCell(dataGrid, nextRow, 7);
                                    if (cell != null)
                                    {
                                        // Устанавливаем текущую ячейку
                                        dataGrid.CurrentCell = new DataGridCellInfo(cell, dataGrid.Columns[7]);

                                        // Начинаем редактирование
                                        dataGrid.BeginEdit();

                                        // Находим TextBox в ячейке
                                        await Task.Delay(50);
                                        var nextTextBox = FindVisualChild<TextBox>(cell);
                                        if (nextTextBox != null)
                                        {
                                            nextTextBox.Focus();
                                            nextTextBox.SelectAll();
                                        }
                                    }
                                }
                            }
                            else
                            {
                                // Если последняя строка, просто выделяем текст
                                textBox.Focus();
                                textBox.SelectAll();
                            }
                        }
                    }
                }
                e.Handled = true;
            }
        }




        // Вспомогательные методы
        private T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T parent)
                    return parent;
                child = VisualTreeHelper.GetParent(child);
            }
            return null;
        }

        private DataGridCell GetCell(DataGrid dataGrid, DataGridRow row, int columnIndex)
        {
            if (row == null) return null;

            var presenter = FindVisualChild<DataGridCellsPresenter>(row);
            if (presenter == null) return null;

            // Прокручиваем к нужной колонке
            dataGrid.ScrollIntoView(row, dataGrid.Columns[columnIndex]);

            var cell = presenter.ItemContainerGenerator.ContainerFromIndex(columnIndex) as DataGridCell;
            return cell;
        }

        private T FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = VisualTreeHelper.GetChild(obj, i);
                if (child != null && child is T)
                    return (T)child;

                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                    return childOfChild;
            }
            return null;
        }
        // Обработчик кнопки "ПРИМЕНИТЬ НАЦЕНКУ" (для текущего выбранного товара)
        private void btnApplyMarkup_Click(object sender, RoutedEventArgs e)
        {
            // Получаем выбранный товар
            var selectedProduct = dataGridProducts.SelectedItem as ProductPriceViewModel;
            if (selectedProduct == null)
            {
                MessageBox.Show("Выберите товар, к которому хотите применить наценку.",
                                "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Получаем процент наценки
            var selectedItem = cmbMarkup.SelectedItem as ComboBoxItem;
            if (selectedItem == null) return;

            string markupText = selectedItem.Content.ToString().Replace("%", "");
            if (!decimal.TryParse(markupText, out decimal markupPercent))
            {
                markupPercent = 0;
            }

            // Рассчитываем цену с наценкой от закупочной цены
            decimal newPrice = selectedProduct.PurchasingPrice * (1 + markupPercent / 100);

            // Если закупочная цена 0, используем розничную
            if (selectedProduct.PurchasingPrice == 0 && selectedProduct.BitrixPrice > 0)
            {
                newPrice = selectedProduct.BitrixPrice * (1 + markupPercent / 100);
            }

            selectedProduct.CustomPrice = Math.Round(newPrice, 2);

            MessageBox.Show($"Применена наценка {markupPercent}%\n" +
                            $"Новая цена: {selectedProduct.CustomPrice:#,##0.00} ₽",
                            "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Обработчик кнопки "ПРИМЕНИТЬ КО ВСЕМ"
        private void btnApplyToAll_Click(object sender, RoutedEventArgs e)
        {
            if (!Products.Any())
            {
                MessageBox.Show("Нет товаров для применения наценки.",
                                "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Получаем процент наценки
            var selectedItem = cmbMarkup.SelectedItem as ComboBoxItem;
            if (selectedItem == null) return;

            string markupText = selectedItem.Content.ToString().Replace("%", "");
            if (!decimal.TryParse(markupText, out decimal markupPercent))
            {
                markupPercent = 0;
            }

            int updatedCount = 0;
            int zeroPriceCount = 0;

            foreach (var product in Products)
            {
                decimal newPrice = 0;

                // Пытаемся рассчитать от закупочной цены
                if (product.PurchasingPrice > 0)
                {
                    newPrice = product.PurchasingPrice * (1 + markupPercent / 100);
                }
                // Если закупочной нет, используем розничную
                else if (product.BitrixPrice > 0)
                {
                    newPrice = product.BitrixPrice * (1 + markupPercent / 100);
                }
                else
                {
                    zeroPriceCount++;
                    continue;
                }

                product.CustomPrice = Math.Round(newPrice, 2);
                updatedCount++;
            }

            string message = $"Применена наценка {markupPercent}% к {updatedCount} товарам.";
            if (zeroPriceCount > 0)
            {
                message += $"\n{zeroPriceCount} товаров пропущено (нет закупочной и розничной цены).";
            }

            MessageBox.Show(message, "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
            CalculateTotal();
        }

        // Дополнительно: метод для применения наценки к конкретному товару по правой кнопке
        private void ApplyMarkupToProduct(ProductPriceViewModel product, decimal markupPercent)
        {
            if (product == null) return;

            decimal newPrice = 0;

            if (product.PurchasingPrice > 0)
            {
                newPrice = product.PurchasingPrice * (1 + markupPercent / 100);
            }
            else if (product.BitrixPrice > 0)
            {
                newPrice = product.BitrixPrice * (1 + markupPercent / 100);
            }

            product.CustomPrice = Math.Round(newPrice, 2);
        }
        private string _vatValue;
        public string VATValue
        {
            get => _vatValue;
            set
            {
                _vatValue = value;
                OnPropertyChanged(nameof(VATValue));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public EditPricePage()
        {
            InitializeComponent();
            Products = new ObservableCollection<ProductPriceViewModel>();
            AllMeasures = new ObservableCollection<BitrixMeasure>();
            DataContext = this;
        }

        public EditPricePage(List<ManagerApp.Data.ScharedData.MatchedProduct> matchedProducts) : this()
        {
            if (matchedProducts != null && matchedProducts.Count > 0)
            {
                // Не загружаем сразу, будет загружено в Page_Loaded
            }
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdateVATDisplay();

                // Загружаем данные об единицах измерения из Bitrix
                await EnsureMeasuresLoaded();

                if (!Products.Any())
                {
                    var matchedProducts = PriceDataManager.GetMatchedProducts();
                    if (matchedProducts != null && matchedProducts.Count > 0)
                    {
                        await LoadMatchedProductsAsync(matchedProducts);
                    }
                    else
                    {
                        LoadSampleData();
                    }
                }

                CalculateTotal();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке страницы: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Метод для загрузки единиц измерения из Bitrix
        private async Task EnsureMeasuresLoaded()
        {
            lock (_measuresLock)
            {
                if (_measuresLoaded)
                    return;
            }

            try
            {
                ShowLoadingIndicator("Загрузка единиц измерения...");
                Console.WriteLine("Начало загрузки единиц измерения из Bitrix...");

                var measures = await BitrixMeasureService.GetMeasuresAsync();

                if (measures != null && measures.Any())
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        AllMeasures.Clear();
                        foreach (var measure in measures)
                        {
                            AllMeasures.Add(measure);
                        }
                    });

                    lock (_measuresLock)
                    {
                        _measuresDictionary.Clear();
                        foreach (var measure in measures)
                        {
                            string id = measure.ID?.ToString() ?? "";
                            if (!string.IsNullOrEmpty(id))
                            {
                                _measuresDictionary[id] = measure;
                            }
                        }

                        Console.WriteLine($"Загружено {_measuresDictionary.Count} единиц измерения из Bitrix");
                        _measuresLoaded = true;
                    }
                }
                else
                {
                    Console.WriteLine("Не удалось загрузить единицы измерения из Bitrix");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка загрузки единиц измерения: {ex.Message}");
            }
            finally
            {
                HideLoadingIndicator();
            }
        }
        // Метод для получения названия единицы измерения по ID
        private string GetMeasureName(string measureId)
        {
            // Если ID пустой, возвращаем "Штука" по умолчанию
            if (string.IsNullOrEmpty(measureId))
            {
                Console.WriteLine($"GetMeasureName: Пустой measureId, возвращаем 'Штука' по умолчанию");
                return "Штука";
            }

            lock (_measuresLock)
            {
                if (_measuresDictionary.TryGetValue(measureId, out var measure))
                {
                    if (!string.IsNullOrEmpty(measure.MEASURE_TITLE))
                    {
                        Console.WriteLine($"GetMeasureName: Найдена мера ID={measureId}, Название={measure.MEASURE_TITLE}");
                        return measure.MEASURE_TITLE;
                    }
                }
            }

            Console.WriteLine($"GetMeasureName: Мера не найдена для ID={measureId}, возвращаем 'Штука' по умолчанию");
            return "Штука"; // Возвращаем "Штука" по умолчанию
        }

        // Метод для получения символа единицы измерения по ID
        private string GetMeasureSymbol(string measureId)
        {
            // Если ID пустой, возвращаем "шт." по умолчанию
            if (string.IsNullOrEmpty(measureId))
            {
                Console.WriteLine($"GetMeasureSymbol: Пустой measureId, возвращаем 'шт.' по умолчанию");
                return "шт.";
            }

            lock (_measuresLock)
            {
                if (_measuresDictionary.TryGetValue(measureId, out var measure))
                {
                    // Используем русский символ, если он есть
                    if (!string.IsNullOrEmpty(measure.SYMBOL_RUS))
                    {
                        Console.WriteLine($"GetMeasureSymbol: Найден символ для ID={measureId}: {measure.SYMBOL_RUS}");
                        return measure.SYMBOL_RUS;
                    }

                    // Или международный символ
                    if (!string.IsNullOrEmpty(measure.SYMBOL_INTL))
                    {
                        Console.WriteLine($"GetMeasureSymbol: Найден международный символ для ID={measureId}: {measure.SYMBOL_INTL}");
                        return measure.SYMBOL_INTL;
                    }

                    // Или название
                    if (!string.IsNullOrEmpty(measure.MEASURE_TITLE))
                    {
                        Console.WriteLine($"GetMeasureSymbol: Используем название как символ для ID={measureId}: {measure.MEASURE_TITLE}");
                        return measure.MEASURE_TITLE;
                    }
                }
            }

            Console.WriteLine($"GetMeasureSymbol: Мера не найдена для ID={measureId}, возвращаем 'шт.' по умолчанию");
            return "шт."; // Возвращаем "шт." по умолчанию
        }


        private async Task LoadMatchedProductsAsync(List<ManagerApp.Data.ScharedData.MatchedProduct> matchedProducts)
        {
            if (matchedProducts == null)
                return;

            Products.Clear();

            Console.WriteLine($"Загружаем {matchedProducts.Count} сопоставленных товаров");

            foreach (var matchedProduct in matchedProducts)
            {
                try
                {
                    Console.WriteLine($"Товар: {matchedProduct.OriginalProductName}");
                    Console.WriteLine($"  Quantity из MatchedProduct: {matchedProduct.ProductQuantity}");
                    Console.WriteLine($"  MeasureId: {matchedProduct.MeasureId}");
                    Console.WriteLine($"  MeasureSymbol: {matchedProduct.MeasureSymbol}");
                    Console.WriteLine($"  MeasureName: {matchedProduct.MeasureName}");

                    string measureId = matchedProduct.MeasureId;
                    string unitSymbol = matchedProduct.MeasureSymbol;
                    string unitName = matchedProduct.MeasureName;
                    decimal quantity = matchedProduct.ProductQuantity > 0 ? matchedProduct.ProductQuantity :
                                       (matchedProduct.Quantity > 0 ? matchedProduct.Quantity : 1);
                    decimal purchasingPrice = 0;

                    // Если нет символа или названия, пробуем получить из словаря
                    if (string.IsNullOrEmpty(unitSymbol) || string.IsNullOrEmpty(unitName))
                    {
                        if (!string.IsNullOrEmpty(measureId))
                        {
                            unitSymbol = GetMeasureSymbol(measureId);
                            unitName = GetMeasureName(measureId);
                        }
                        else
                        {
                            unitSymbol = "шт.";
                            unitName = "Штука";
                        }
                    }

                    // Пробуем получить закупочную цену из каталога
                    if (matchedProduct.BitrixProductId > 0)
                    {
                        try
                        {
                            var catalogProduct = await BitrixCatalogProductService.GetCatalogProductAsync(matchedProduct.BitrixProductId);
                            if (catalogProduct != null && catalogProduct.PurchasingPrice.HasValue)
                            {
                                purchasingPrice = catalogProduct.PurchasingPrice.Value;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Ошибка при получении закупочной цены: {ex.Message}");
                        }
                    }

                    var product = new ProductPriceViewModel
                    {
                        BitrixProductId = matchedProduct.BitrixProductId,
                        OriginalProductName = matchedProduct.OriginalProductName,
                        BitrixProductName = matchedProduct.BitrixProductName,
                        BitrixPrice = matchedProduct.BitrixPrice,
                        PurchasingPrice = purchasingPrice,
                        CustomPrice = matchedProduct.CustomPrice,
                        Quantity = quantity,  // ← Используем количество из matchedProduct
                        Unit = unitSymbol,
                        UnitFullName = unitName,
                        MeasureId = measureId,
                        SelectedMeasureId = measureId,
                        IsMeasureEditable = true,  // Можно редактировать
                        VAT = SettingsHelper.GetVATAsString()
                    };

                    Console.WriteLine($"  Создан продукт: Quantity={product.Quantity}, Unit={product.Unit}");

                    product.PropertyChanged += Product_PropertyChanged;
                    Products.Add(product);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при загрузке товара {matchedProduct.OriginalProductName}: {ex.Message}");
                }
            }

            dataGridProducts.ItemsSource = Products;
            UpdateVATDisplay();
            CalculateTotal();
        }



        // Остальные методы без изменений...
        private async void LoadPurchasingPricesBackground()
        {
            try
            {
                bool needLoad = false;
                await Dispatcher.InvokeAsync(() =>
                {
                    needLoad = Products.Any(p => p.BitrixProductId > 0 && p.PurchasingPrice == 0);
                });

                if (needLoad)
                {
                    await LoadPurchasingPricesAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при загрузке закупочных цен: {ex.Message}");
            }
        }

        private async Task LoadPurchasingPricesAsync()
        {
            try
            {
                bool hasProductsToLoad = await Dispatcher.InvokeAsync(() =>
                {
                    return Products.Any(p => p.BitrixProductId > 0);
                });

                if (!hasProductsToLoad)
                    return;

                await Dispatcher.InvokeAsync(() => ShowLoadingIndicator("Загрузка закупочных цен..."));

                List<int> productIds = null;
                await Dispatcher.InvokeAsync(() =>
                {
                    productIds = Products
                        .Where(p => p.BitrixProductId > 0)
                        .Select(p => p.BitrixProductId)
                        .Distinct()
                        .ToList();
                });

                if (!productIds.Any())
                    return;

                var purchasingPrices = await BitrixPurchasePriceService.GetPurchasingPricesBatchAsync(productIds);

                await Dispatcher.InvokeAsync(() =>
                {
                    foreach (var product in Products)
                    {
                        if (purchasingPrices.TryGetValue(product.BitrixProductId, out var price))
                        {
                            product.PurchasingPrice = price;
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    Console.WriteLine($"Ошибка загрузки закупочных цен: {ex.Message}");
                    MessageBox.Show($"Не удалось загрузить закупочные цены: {ex.Message}",
                        "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                });
            }
            finally
            {
                await Dispatcher.InvokeAsync(() => HideLoadingIndicator());
            }
        }

        private void ShowLoadingIndicator(string message = "Загрузка...")
        {
            Cursor = Cursors.Wait;
            IsEnabled = false;
        }

        private void HideLoadingIndicator()
        {
            Cursor = Cursors.Arrow;
            IsEnabled = true;
        }

        private void LoadSampleData()
        {
            Products.Clear();

            var sampleProducts = new List<ProductPriceViewModel>
            {
                new ProductPriceViewModel
                {
                    OriginalProductName = "Кабель ВВГ 3х2,5",
                    BitrixProductName = "Кабель силовой ВВГ 3х2,5",
                    BitrixPrice = 85.50m,
                    CustomPrice = 85.50m,
                    Quantity = 100,
                    Unit = "м",
                    UnitFullName = "Метр",
                    MeasureId = "1",
                    VAT = SettingsHelper.GetVATAsString()
                }
            };

            foreach (var product in sampleProducts)
            {
                product.PropertyChanged += Product_PropertyChanged;
                Products.Add(product);
            }

            dataGridProducts.ItemsSource = Products;
        }

        // Остальные методы без изменений...
        private void Product_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ProductPriceViewModel.TotalWithVAT) ||
                e.PropertyName == nameof(ProductPriceViewModel.CustomPrice) ||
                e.PropertyName == nameof(ProductPriceViewModel.Quantity))
            {
                CalculateTotal();
            }
        }

        private void UpdateVATDisplay()
        {
            VATValue = SettingsHelper.GetVATAsString();
            txtVATValue.Text = VATValue;

            foreach (var product in Products)
            {
                product.VAT = VATValue;
            }
        }

        private void CalculateTotal()
        {
            TotalSum = Products.Sum(p => p.TotalWithVAT);
            txtTotalSum.Text = TotalSum.ToString("#,##0.00");
        }

        private void btnRecalculate_Click(object sender, RoutedEventArgs e)
        {
            UpdateVATDisplay();
            CalculateTotal();
            MessageBox.Show("Цены пересчитаны с учетом НДС",
                "Пересчет", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            SaveProductsToManager();
            if (NavigationService.CanGoBack)
            {
                NavigationService.GoBack();
            }
        }

        private void btnNext_Click(object sender, RoutedEventArgs e)
        {
            var emptyPriceProducts = Products.Where(p => p.CustomPrice <= 0).ToList();
            if (emptyPriceProducts.Any())
            {
                MessageBox.Show($"У {emptyPriceProducts.Count} товаров не указана цена.\n" +
                              "Пожалуйста, заполните цены для всех товаров.",
                              "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SaveProductsToManager();

            var invoiceItems = Products.Select(p => new InvoiceItem
            {
                BitrixProductId = p.BitrixProductId,
                ProductName = p.BitrixProductName,
                OriginalProductName = p.OriginalProductName,
                Price = p.PriceWithVAT,
                Quantity = p.Quantity,
                Unit = p.Unit,
                UnitFullName = p.UnitFullName,
                VAT = p.VAT,
                MeasureId = p.MeasureId,
                Total = p.TotalWithVAT
            }).ToList();

            try
            {
                var invoicePage = new InvoicionCreatePage(invoiceItems);
                NavigationService.Navigate(invoicePage);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка перехода к формированию счета: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveProductsToManager()
        {
            var matchedProducts = Products.Select(p => new ManagerApp.Data.ScharedData.MatchedProduct
            {
                OriginalProductName = p.OriginalProductName,
                BitrixProductName = p.BitrixProductName,
                BitrixProductId = p.BitrixProductId,
                BitrixPrice = p.BitrixPrice,
                PurchasingPrice = p.PurchasingPrice,
                CustomPrice = p.CustomPrice,
                Quantity = p.Quantity,
                Unit = p.UnitFullName, // Сохраняем полное название в Unit
                Measure = p.SelectedMeasureId, // Сохраняем выбранную единицу

                VAT = p.VAT,
                PriceWithVAT = p.PriceWithVAT,
                TotalWithVAT = p.TotalWithVAT
            }).ToList();

            Console.WriteLine("Сохранение товаров перед переходом на другую страницу:");
            foreach (var product in Products)
            {
                Console.WriteLine($"Товар: {product.BitrixProductName}, UnitFullName: {product.UnitFullName}, Unit: {product.Unit}, VAT: {product.VAT}");
            }

            PriceDataManager.SetMatchedProducts(matchedProducts);
        }

        // Обработчики событий для TextBox
        private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            foreach (char c in e.Text)
            {
                if (!char.IsDigit(c) && c != ',' && c != '.')
                {
                    e.Handled = true;
                    return;
                }
            }

            string newText = textBox.Text.Insert(textBox.SelectionStart, e.Text);
            if (!decimal.TryParse(newText, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
            {
                e.Handled = true;
            }
        }

        private void TextBoxQuantity_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            foreach (char c in e.Text)
            {
                if (!char.IsDigit(c))
                {
                    e.Handled = true;
                    return;
                }
            }

            string newText = textBox.Text.Insert(textBox.SelectionStart, e.Text);
            if (!decimal.TryParse(newText, out _))
            {
                e.Handled = true;
            }
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            if (string.IsNullOrWhiteSpace(textBox.Text))
            {
                textBox.Text = "0";
                return;
            }

            if (decimal.TryParse(textBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
            {
                if (value < 0)
                    value = 0;
                textBox.Text = value.ToString("0.##");
            }
            else
            {
                textBox.Text = "0";
            }
        }

        private void DataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            // Показываем номер в заголовке строки (слева)
            e.Row.Header = (e.Row.GetIndex() + 1).ToString();

            // Если хотите показывать в колонке, нужно добавить свойство в ViewModel
            var product = e.Row.DataContext as ProductPriceViewModel;
            if (product != null)
            {
                product.Index = e.Row.GetIndex() + 1;
            }
        }

        private void DataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Commit)
            {
                CalculateTotal();
            }
        }

        private void dataGridProducts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }
    }

    // ============ КЛАССЫ ДЛЯ BITRIX API ============

    // Класс модели для единицы измерения из Bitrix
    public class BitrixMeasure
    {
        [JsonProperty("ID")]
        public string ID { get; set; }

        [JsonProperty("CODE")]
        public string CODE { get; set; }

        [JsonProperty("MEASURE_TITLE")]
        public string MEASURE_TITLE { get; set; }

        [JsonProperty("SYMBOL_RUS")]
        public string SYMBOL_RUS { get; set; }

        [JsonProperty("SYMBOL_INTL")]
        public string SYMBOL_INTL { get; set; }

        [JsonProperty("SYMBOL_LETTER_INTL")]
        public string SYMBOL_LETTER_INTL { get; set; }

        [JsonProperty("IS_DEFAULT")]
        public string IS_DEFAULT { get; set; }
    }

    // Класс для ответа от Bitrix API для единиц измерения
    public class BitrixMeasureResponse
    {
        [JsonProperty("result")]
        public List<BitrixMeasure> Result { get; set; }
    }

    // Класс для деталей товара из Bitrix с правильной десериализацией
    public class BitrixProductDetail
    {
        [JsonProperty("ID")]
        public string ID { get; set; }

        [JsonProperty("NAME")]
        public string NAME { get; set; }

        [JsonProperty("PRICE")]
        [JsonConverter(typeof(NullableDecimalConverter))]
        public decimal? PRICE { get; set; }

        [JsonProperty("CURRENCY_ID")]
        public string CURRENCY_ID { get; set; }

        [JsonProperty("MEASURE")]
        public string MEASURE { get; set; }

        [JsonProperty("DESCRIPTION")]
        public string DESCRIPTION { get; set; }

        [JsonProperty("ACTIVE")]
        public string ACTIVE { get; set; }

        [JsonProperty("SECTION_ID")]
        public string SECTION_ID { get; set; }

        [JsonProperty("VAT_ID")]
        public string VAT_ID { get; set; }

        [JsonProperty("VAT_INCLUDED")]
        public string VAT_INCLUDED { get; set; }

        [JsonProperty("XML_ID")]
        public string XML_ID { get; set; }

        [JsonProperty("CODE")]
        public string CODE { get; set; }
    }

    // Конвертер для nullable decimal
    public class NullableDecimalConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(decimal?) || objectType == typeof(decimal);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            if (reader.TokenType == JsonToken.String)
            {
                if (string.IsNullOrEmpty(reader.Value?.ToString()))
                    return null;

                if (decimal.TryParse(reader.Value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
                    return result;

                return null;
            }

            if (reader.TokenType == JsonToken.Float || reader.TokenType == JsonToken.Integer)
            {
                return Convert.ToDecimal(reader.Value);
            }

            throw new JsonSerializationException($"Unexpected token type: {reader.TokenType}");
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteValue(value);
        }

        // Сервис для получения деталей товара из Bitrix Catalog (с закупочной ценой)
        public static class BitrixCatalogProductService
        {
            public static async Task<BitrixCatalogProductDetail> GetCatalogProductAsync(int productId)
            {
                try
                {
                    Console.WriteLine($"BitrixCatalogProductService.GetCatalogProductAsync: Запрос товара ID={productId}");

                    using (var httpClient = new HttpClient())
                    {
                        httpClient.Timeout = TimeSpan.FromSeconds(30);

                        string apiUrl = $"https://crmnvr.ru/rest/241/5gkwkk4657uafc2x/catalog.product.get?id={productId}";
                        Console.WriteLine($"Запрос товара из каталога: {apiUrl}");

                        var response = await httpClient.GetAsync(apiUrl);

                        if (response.IsSuccessStatusCode)
                        {
                            var json = await response.Content.ReadAsStringAsync();
                            Console.WriteLine($"Получены детали товара из каталога ID={productId}, длина: {json.Length} символов");

                            // Для отладки
                            int previewLength = Math.Min(300, json.Length);
                            Console.WriteLine($"Начало ответа: {json.Substring(0, previewLength)}...");

                            try
                            {
                                var result = JsonConvert.DeserializeObject<BitrixCatalogProductResponse>(json);
                                var productDetail = result?.GetProductDetail();

                                if (productDetail != null)
                                {
                                    Console.WriteLine($"Успешно загружены детали товара из каталога ID={productId}");
                                    Console.WriteLine($"PurchasingPrice: {productDetail.PurchasingPrice}, Quantity: {productDetail.Quantity}");
                                    return productDetail;
                                }
                                else
                                {
                                    Console.WriteLine($"Не удалось десериализовать ответ для товара ID={productId}");
                                    return null;
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Ошибка JSON при десериализации товара ID={productId}: {ex.Message}");
                                return null;
                            }
                        }
                        else
                        {
                            Console.WriteLine($"HTTP ошибка при загрузке товара ID={productId}: {response.StatusCode} - {response.ReasonPhrase}");
                            return null;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка в BitrixCatalogProductService.GetCatalogProductAsync для ID={productId}: {ex.Message}");
                    return null;
                }
            }
        }
    }

    // Класс для ответа от Bitrix API для товара
    public class BitrixProductResponse
    {
        [JsonProperty("result")]
        public BitrixProductDetail Result { get; set; }
    }

    // ============ СЕРВИСЫ ДЛЯ BITRIX API ============

    // Сервис для загрузки единиц измерения из Bitrix
    public static class BitrixMeasureService
    {
        public static async Task<List<BitrixMeasure>> GetMeasuresAsync()
        {
            try
            {
                Console.WriteLine("BitrixMeasureService.GetMeasuresAsync: Начало запроса...");

                using (var httpClient = new HttpClient())
                {
                    httpClient.Timeout = TimeSpan.FromSeconds(30);

                    string apiUrl = "https://crmnvr.ru/rest/241/5gkwkk4657uafc2x/crm.measure.list.json";
                    Console.WriteLine($"Запрос единиц измерения: {apiUrl}");

                    var response = await httpClient.GetAsync(apiUrl);

                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"Получены единицы измерения, длина ответа: {json.Length} символов");

                        var result = JsonConvert.DeserializeObject<BitrixMeasureResponse>(json);

                        if (result?.Result != null)
                        {
                            Console.WriteLine($"Успешно загружено {result.Result.Count} единиц измерения");
                            return result.Result;
                        }
                        else
                        {
                            Console.WriteLine("Не удалось десериализовать ответ с единицами измерения");
                            return new List<BitrixMeasure>();
                        }
                    }
                    else
                    {
                        Console.WriteLine($"HTTP ошибка при загрузке единиц измерения: {response.StatusCode} - {response.ReasonPhrase}");
                        return new List<BitrixMeasure>();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в BitrixMeasureService: {ex.Message}");
                return new List<BitrixMeasure>();
            }
        }
    }

    // Сервис для получения деталей товара из Bitrix
    public static class BitrixProductService
    {
        public static async Task<BitrixProductDetail> GetProductAsync(int productId)
        {
            try
            {
                Console.WriteLine($"BitrixProductService.GetProductAsync: Запрос товара ID={productId}");

                using (var httpClient = new HttpClient())
                {
                    httpClient.Timeout = TimeSpan.FromSeconds(30);

                    string apiUrl = $"https://crmnvr.ru/rest/241/5gkwkk4657uafc2x/crm.product.get.json?id={productId}";
                    Console.WriteLine($"Запрос товара: {apiUrl}");

                    var response = await httpClient.GetAsync(apiUrl);

                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"Получены детали товара ID={productId}, длина: {json.Length} символов");

                        // Для отладки показываем часть ответа
                        if (json.Length > 0)
                        {
                            int previewLength = Math.Min(500, json.Length);
                            Console.WriteLine($"Начало ответа: {json.Substring(0, previewLength)}...");
                        }

                        try
                        {
                            // Десериализуем с использованием кастомного конвертера
                            var settings = new JsonSerializerSettings
                            {
                                Converters = new List<JsonConverter> { new NullableDecimalConverter() },
                                NullValueHandling = NullValueHandling.Ignore
                            };

                            var result = JsonConvert.DeserializeObject<BitrixProductResponse>(json, settings);

                            if (result?.Result != null)
                            {
                                Console.WriteLine($"Успешно загружены детали товара ID={productId}");
                                Console.WriteLine($"MEASURE поле: '{result.Result.MEASURE}'");

                                // Если MEASURE пустое, проверяем PROPERTY_4935
                                if (string.IsNullOrEmpty(result.Result.MEASURE))
                                {
                                    Console.WriteLine("MEASURE пустое, проверяем PROPERTY_4935...");

                                    // Парсим JSON для поиска PROPERTY_4935
                                    var jObject = JObject.Parse(json);
                                    var property4935 = jObject["result"]?["PROPERTY_4935"]?["value"]?.ToString();

                                    if (!string.IsNullOrEmpty(property4935))
                                    {
                                        Console.WriteLine($"Найдено PROPERTY_4935: '{property4935}'");
                                        // Можно попробовать сопоставить значение с единицами измерения
                                    }
                                }

                                return result.Result;
                            }
                            else
                            {
                                Console.WriteLine($"Не удалось десериализовать ответ для товара ID={productId}");
                                return null;
                            }
                        }
                        catch (JsonException jex)
                        {
                            Console.WriteLine($"Ошибка JSON при десериализации товара ID={productId}: {jex.Message}");

                            // Попробуем ручной парсинг
                            try
                            {
                                var jObject = JObject.Parse(json);
                                var result = jObject["result"];

                                if (result != null)
                                {
                                    var productDetail = new BitrixProductDetail
                                    {
                                        ID = result["ID"]?.ToString(),
                                        NAME = result["NAME"]?.ToString(),
                                        MEASURE = result["MEASURE"]?.ToString(),
                                        CODE = result["CODE"]?.ToString(),
                                        ACTIVE = result["ACTIVE"]?.ToString()
                                    };

                                    // Парсим PRICE
                                    if (result["PRICE"] != null && result["PRICE"].Type != JTokenType.Null)
                                    {
                                        if (decimal.TryParse(result["PRICE"].ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var price))
                                        {
                                            productDetail.PRICE = price;
                                        }
                                    }

                                    Console.WriteLine($"Ручной парсинг успешен. MEASURE: '{productDetail.MEASURE}'");
                                    return productDetail;
                                }
                            }
                            catch (Exception parseEx)
                            {
                                Console.WriteLine($"Ошибка ручного парсинга: {parseEx.Message}");
                            }

                            return null;
                        }
                    }
                    else
                    {
                        Console.WriteLine($"HTTP ошибка при загрузке товара ID={productId}: {response.StatusCode} - {response.ReasonPhrase}");
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в BitrixProductService.GetProductAsync для ID={productId}: {ex.Message}");
                return null;
            }
        }
    }

    // Класс для передачи данных в счет
    public class InvoiceItem
    {
        public int BitrixProductId { get; set; }
        public string ProductName { get; set; }
        public string OriginalProductName { get; set; }
        public decimal Price { get; set; }
        public decimal Quantity { get; set; }
        public string Unit { get; set; }
        public string UnitFullName { get; set; }
        public string VAT { get; set; }
        public decimal Total { get; set; }
        public string MeasureId { get; set; }
        public int Count { get; set; }
    }

    // Класс ProductPriceViewModel
    public class ProductPriceViewModel : INotifyPropertyChanged
    {
        private int _index;
        public int Index
        {
            get => _index;
            set
            {
                _index = value;
                OnPropertyChanged(nameof(Index));
            }
        }
        public int BitrixProductId { get; set; }
        public string OriginalProductName { get; set; }
        public string BitrixProductName { get; set; }
        public decimal BitrixPrice { get; set; }
        public decimal PurchasingPrice { get; set; }

        private decimal _customPrice;
        public decimal CustomPrice
        {
            get => _customPrice;
            set
            {
                _customPrice = value;
                OnPropertyChanged(nameof(CustomPrice));
                OnPropertyChanged(nameof(PriceWithVAT));
                OnPropertyChanged(nameof(TotalWithVAT));
            }
        }

        private decimal _quantity;
        public decimal Quantity
        {
            get => _quantity;
            set
            {
                _quantity = value;
                OnPropertyChanged(nameof(Quantity));
                OnPropertyChanged(nameof(TotalWithVAT));
            }
        }

        public string Unit { get; set; }
        public string UnitFullName { get; set; }
        public string MeasureId { get; set; }

        private string _vat;
        public string VAT
        {
            get => _vat;
            set
            {
                _vat = value;
                OnPropertyChanged(nameof(VAT));
                OnPropertyChanged(nameof(PriceWithVAT));
                OnPropertyChanged(nameof(TotalWithVAT));
            }
        }

        // Добавляем свойство для выбранной единицы измерения
        private string _selectedMeasureId;
        public string SelectedMeasureId
        {
            get => _selectedMeasureId;
            set
            {
                if (_selectedMeasureId != value)
                {
                    _selectedMeasureId = value;
                    MeasureId = value;

                    // Обновляем Unit и UnitFullName при изменении выбора
                    if (!string.IsNullOrEmpty(value))
                    {
                        Unit = GetMeasureSymbol(value);
                        UnitFullName = GetMeasureName(value);
                        OnPropertyChanged(nameof(Unit));
                        OnPropertyChanged(nameof(UnitFullName));
                    }

                    OnPropertyChanged(nameof(SelectedMeasureId));
                }
            }
        }

        // Добавляем свойство для возможности редактирования
        public bool IsMeasureEditable { get; set; } = true;

        public decimal PriceWithVAT
        {
            get
            {
                if (decimal.TryParse(VAT?.Replace("%", ""), out decimal vatPercent))
                {
                    return CustomPrice * (1 + vatPercent / 100);
                }
                return CustomPrice;
            }
        }

        public decimal TotalWithVAT => PriceWithVAT * Quantity;

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Временные методы (будут заменены внешними)
        private string GetMeasureSymbol(string measureId)
        {
            return "шт."; // Заменится на реальную логику
        }

        private string GetMeasureName(string measureId)
        {
            return "Штука"; // Заменится на реальную логику
        }
    }

    // Класс для деталей товара из Bitrix Catalog с закупочной ценой
    public class BitrixCatalogProductDetail
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("purchasingPrice")]
        [JsonConverter(typeof(NullableDecimalConverter))]
        public decimal? PurchasingPrice { get; set; }

        [JsonProperty("purchasingCurrency")]
        public string PurchasingCurrency { get; set; }

        [JsonProperty("quantity")]
        public decimal? Quantity { get; set; }

        [JsonProperty("measure")]
        public string Measure { get; set; }

        [JsonProperty("active")]
        public string Active { get; set; }

        [JsonProperty("code")]
        public string Code { get; set; }
    }

    // Класс для ответа от Bitrix Catalog API
    public class BitrixCatalogProductResponse
    {
        [JsonProperty("result")]
        public JObject Result { get; set; } // Используем JObject для гибкости

        public BitrixCatalogProductDetail GetProductDetail()
        {
            if (Result?["product"] == null)
                return null;

            var product = Result["product"];

            return new BitrixCatalogProductDetail
            {
                Id = product["id"]?.Value<int>() ?? 0,
                Name = product["name"]?.ToString(),
                PurchasingPrice = product["purchasingPrice"]?.Value<decimal?>(),
                PurchasingCurrency = product["purchasingCurrency"]?.ToString(),
                Quantity = product["quantity"]?.Value<decimal?>(),
                Measure = product["measure"]?.ToString(),
                Active = product["active"]?.ToString(),
                Code = product["code"]?.ToString()
            };
        }
    }
}