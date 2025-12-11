using ManagerApp.Classes.Setting;
using ManagerApp.Data.ScharedData;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
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
                    OriginalProductName = matchedProduct.OriginalProductName,
                    BitrixProductName = matchedProduct.BitrixProductName,
                    BitrixPrice = matchedProduct.BitrixPrice,
                    CustomPrice = matchedProduct.BitrixPrice, // По умолчанию цена из Bitrix
                    Quantity = matchedProduct.Quantity > 0 ? matchedProduct.Quantity : 1,
                    Unit = matchedProduct.Unit ?? "шт.",
                    VAT = SettingsHelper.GetVATAsString()
                };

                product.PropertyChanged += Product_PropertyChanged;
                Products.Add(product);
            }

            dataGridProducts.ItemsSource = Products;
            UpdateVATDisplay();
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

            MessageBox.Show("Переход к формированию счета",
                "Далее", MessageBoxButton.OK, MessageBoxImage.Information);

            // Здесь будет переход на следующую страницу
            // var invoicePage = new InvoicePage(Products.ToList());
            // NavigationService.Navigate(invoicePage);
        }

        private void SaveProductsToManager()
        {
            var matchedProducts = Products.Select(p => new MatchedProduct
            {
                OriginalProductName = p.OriginalProductName,
                BitrixProductName = p.BitrixProductName,
                BitrixPrice = p.BitrixPrice,
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
}