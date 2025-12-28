using ManagerApp.Classes.Setting;
using ManagerApp.Data.ScharedData;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ManagerApp.Pages
{
    public partial class EditPricePage : Page, INotifyPropertyChanged
    {
        public ObservableCollection<ProductPriceViewModel> Products { get; set; }

        private decimal _totalSum;
        public decimal TotalSum
        {
            get => _totalSum;
            set
            {
                _totalSum = value;
                OnPropertyChanged(nameof(TotalSum));
            }
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

        // Конструктор без параметров
        public EditPricePage()
        {
            InitializeComponent();
            Products = new ObservableCollection<ProductPriceViewModel>();
            DataContext = this;
        }

        // Конструктор с параметром (список сопоставленных товаров)
        public EditPricePage(List<MatchedProduct> matchedProducts) : this()
        {
            if (matchedProducts != null && matchedProducts.Count > 0)
            {
                LoadMatchedProducts(matchedProducts);
            }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            // Загружаем НДС из настроек
            UpdateVATDisplay();

            // Если продукты еще не загружены, пробуем загрузить из менеджера
            if (!Products.Any())
            {
                var matchedProducts = PriceDataManager.GetMatchedProducts();
                if (matchedProducts != null && matchedProducts.Count > 0)
                {
                    LoadMatchedProducts(matchedProducts);
                }
                else
                {
                    // Загружаем тестовые данные
                    LoadSampleData();
                }
            }

            CalculateTotal();
        }

        private void LoadMatchedProducts(List<MatchedProduct> matchedProducts)
        {
            Products.Clear();

            foreach (var matchedProduct in matchedProducts)
            {
                var product = new ProductPriceViewModel
                {
                    BitrixProductId = matchedProduct.BitrixProductId,
                    OriginalProductName = matchedProduct.OriginalProductName,
                    BitrixProductName = matchedProduct.BitrixProductName,
                    BitrixPrice = matchedProduct.BitrixPrice,
                    PurchasingPrice = matchedProduct.PurchasingPrice, // Загружаем из MatchedProduct
                    CustomPrice = matchedProduct.CustomPrice > 0 ? matchedProduct.CustomPrice : matchedProduct.BitrixPrice,
                    Quantity = matchedProduct.Quantity > 0 ? matchedProduct.Quantity : 1,
                    Unit = matchedProduct.Unit ?? "шт.",
                    VAT = SettingsHelper.GetVATAsString()
                };

                product.PropertyChanged += Product_PropertyChanged;
                Products.Add(product);
            }

            dataGridProducts.ItemsSource = Products;
            UpdateVATDisplay();

            // Запускаем загрузку закупочных цен асинхронно
            LoadPurchasingPricesBackground();
        }

        private async void LoadPurchasingPricesBackground()
        {
            // Проверяем, нужно ли загружать закупочные цены
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

        private async Task LoadPurchasingPricesAsync()
        {
            try
            {
                // Сначала проверяем, есть ли что загружать
                bool hasProductsToLoad = await Dispatcher.InvokeAsync(() =>
                {
                    return Products.Any(p => p.BitrixProductId > 0);
                });

                if (!hasProductsToLoad)
                    return;

                // Показываем индикатор загрузки
                await Dispatcher.InvokeAsync(() => ShowLoadingIndicator());

                // Собираем ID товаров
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

                // Загружаем закупочные цены
                var purchasingPrices = await BitrixPurchasePriceService.GetPurchasingPricesBatchAsync(productIds);

                // Обновляем данные в UI
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
                    // Можно показать сообщение пользователю
                    MessageBox.Show($"Не удалось загрузить закупочные цены: {ex.Message}",
                        "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                });
            }
            finally
            {
                await Dispatcher.InvokeAsync(() => HideLoadingIndicator());
            }
        }

        private void ShowLoadingIndicator()
        {
            // Простая реализация индикатора загрузки
            // Можно использовать ProgressBar или другой индикатор
            Cursor = Cursors.Wait;
            IsEnabled = false;

            // Если у вас есть ProgressBar, раскомментируйте:
            // progressBar.Visibility = Visibility.Visible;
        }

        private void HideLoadingIndicator()
        {
            Cursor = Cursors.Arrow;
            IsEnabled = true;

            // Если у вас есть ProgressBar, раскомментируйте:
            // progressBar.Visibility = Visibility.Collapsed;
        }



        private void LoadSampleData()
        {
            Products.Clear();

            // Тестовые данные
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
                    VAT = SettingsHelper.GetVATAsString()
                },
                new ProductPriceViewModel
                {
                    OriginalProductName = "Труба ПНД 32мм",
                    BitrixProductName = "Труба ПНД 32мм для кабеля",
                    BitrixPrice = 125.00m,
                    CustomPrice = 125.00m,
                    Quantity = 50,
                    Unit = "м",
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

            // Обновляем НДС для всех продуктов
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
            // Сохраняем данные перед уходом
            SaveProductsToManager();

            // Возвращаемся назад
            if (NavigationService.CanGoBack)
            {
                NavigationService.GoBack();
            }
        }

        private void btnNext_Click(object sender, RoutedEventArgs e)
        {
            // Проверяем, что все цены заполнены
            var emptyPriceProducts = Products.Where(p => p.CustomPrice <= 0).ToList();
            if (emptyPriceProducts.Any())
            {
                MessageBox.Show($"У {emptyPriceProducts.Count} товаров не указана цена.\n" +
                              "Пожалуйста, заполните цены для всех товаров.",
                              "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Сохраняем данные
            SaveProductsToManager();

            // Создаем список товаров для передачи на страницу формирования счета
            var invoiceItems = Products.Select(p => new InvoiceItem
            {
                BitrixProductId = p.BitrixProductId, // ДОБАВЬТЕ!
                ProductName = p.BitrixProductName,
                OriginalProductName = p.OriginalProductName,
                Price = p.PriceWithVAT, // Передаем цену с НДС
                Quantity = p.Quantity,
                Unit = p.Unit,
                VAT = p.VAT,
                Total = p.TotalWithVAT
            }).ToList();

            // Переходим на страницу формирования счета
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
            var matchedProducts = Products.Select(p => new MatchedProduct
            {
                OriginalProductName = p.OriginalProductName,
                BitrixProductName = p.BitrixProductName,
                BitrixProductId = p.BitrixProductId,
                BitrixPrice = p.BitrixPrice,
                PurchasingPrice = p.PurchasingPrice, // Сохраняем закупочную цену
                CustomPrice = p.CustomPrice,
                Quantity = p.Quantity,
                Unit = p.Unit,
                VAT = p.VAT,
                PriceWithVAT = p.PriceWithVAT,
                TotalWithVAT = p.TotalWithVAT
            }).ToList();

            PriceDataManager.SetMatchedProducts(matchedProducts);
        }

        // Обработчики для TextBox
        private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            // Разрешаем только цифры, запятую и точку
            foreach (char c in e.Text)
            {
                if (!char.IsDigit(c) && c != ',' && c != '.')
                {
                    e.Handled = true;
                    return;
                }
            }

            // Проверяем, что это корректное число
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

            // Разрешаем только цифры
            foreach (char c in e.Text)
            {
                if (!char.IsDigit(c))
                {
                    e.Handled = true;
                    return;
                }
            }

            // Проверяем, что это корректное число
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

            // Форматируем значение при потере фокуса
            if (string.IsNullOrWhiteSpace(textBox.Text))
            {
                textBox.Text = "0";
                return;
            }

            if (decimal.TryParse(textBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
            {
                // Ограничиваем минимальное значение
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
            e.Row.Header = (e.Row.GetIndex() + 1).ToString();
        }

        private void DataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Commit)
            {
                // Пересчитываем итоги при редактировании ячейки
                CalculateTotal();
            }
        }
    }

    // Класс для передачи данных о товарах в счет
    public class InvoiceItem
    {
        public int BitrixProductId { get; set; } // ДОБАВЬТЕ ЭТУ СТРОЧКУ
        public string ProductName { get; set; }
        public string OriginalProductName { get; set; }
        public decimal Price { get; set; }
        public decimal Quantity { get; set; }
        public string Unit { get; set; }
        public string VAT { get; set; }
        public decimal Total { get; set; }
    }
}