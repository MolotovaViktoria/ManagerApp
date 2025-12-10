using ManagerApp.Classes;
using ManagerApp.Data.ScharedData;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ManagerApp.Pages
{
    public partial class ComparisonProduct : Page
    {
        // ViewModel для элемента таблицы
        public class ProductItemViewModel
        {
            public string OriginalProduct { get; set; }
            public ObservableCollection<BitrixProductViewModel> BitrixProducts { get; set; }
            public BitrixProductViewModel SelectedBitrixProduct { get; set; }
            public ICommand AddCommand { get; set; }
        }

        // ViewModel для товара из Bitrix
        public class BitrixProductViewModel
        {
            public string ProductId { get; set; }
            public string ProductName { get; set; }
            public string CategoryName { get; set; }
            public decimal Price { get; set; }
            public bool HasPrice { get; set; }
            public string SectionId { get; set; }
        }

        // Свойство для привязки данных
        public ObservableCollection<ProductItemViewModel> ProductItems { get; set; }

        // Конструктор без параметров (использует сохраненные данные)
        public ComparisonProduct()
        {
            InitializeComponent();
            ProductItems = new ObservableCollection<ProductItemViewModel>();
            DataContext = this;

            // Загружаем сохраненные товары
            LoadProductsFromManager();
        }

        // Конструктор с параметром (для прямого вызова)
        public ComparisonProduct(List<string> products) : this()
        {
            // Сохраняем переданные товары
            if (products != null && products.Count > 0)
            {
                ProductSelectionManager.SetProducts(products);
                LoadProductsFromManager();
            }
        }

        private void LoadProductsFromManager()
        {
            var products = ProductSelectionManager.GetProducts();

            if (products == null || products.Count == 0)
            {
                // Если нет сохраненных товаров, загружаем тестовые
                LoadSampleData();
                return;
            }

            foreach (var product in products)
            {
                var item = new ProductItemViewModel
                {
                    OriginalProduct = product,
                    BitrixProducts = new ObservableCollection<BitrixProductViewModel>(),
                    SelectedBitrixProduct = null,
                    AddCommand = new RelayCommand(AddProduct)
                };

                // Загружаем товары из Bitrix
                LoadBitrixProducts(item, product);

                ProductItems.Add(item);
            }
        }

        private void LoadSampleData()
        {
            // Тестовые данные для дизайнера
            var sampleProducts = new List<string>
            {
                "Кабель ВВГ 3х2,5",
                "Труба ПНД 32мм",
                "Розетка компьютерная RJ45",
                "Автомат выключатель 16А"
            };

            foreach (var product in sampleProducts)
            {
                var item = new ProductItemViewModel
                {
                    OriginalProduct = product,
                    BitrixProducts = new ObservableCollection<BitrixProductViewModel>
                    {
                        new BitrixProductViewModel
                        {
                            ProductId = "1",
                            ProductName = "Кабель ВВГ-Пнг(А) 3х2,5",
                            CategoryName = "Кабельная продукция",
                            Price = 125.50m,
                            HasPrice = true,
                            SectionId = "685"
                        },
                        new BitrixProductViewModel
                        {
                            ProductId = "2",
                            ProductName = "Кабель ВВГ 3х2,5",
                            CategoryName = "Кабельная продукция",
                            Price = 118.75m,
                            HasPrice = true,
                            SectionId = "685"
                        }
                    },
                    SelectedBitrixProduct = null,
                    AddCommand = new RelayCommand(AddProduct)
                };

                ProductItems.Add(item);
            }
        }

        private async void LoadBitrixProducts(ProductItemViewModel item, string productName)
        {
            try
            {
                // Здесь будет логика загрузки товаров из Bitrix
                // Пока используем заглушку

                // Пример заглушки
                item.BitrixProducts.Add(new BitrixProductViewModel
                {
                    ProductId = "22328",
                    ProductName = "Розетка компьютерная 1-м СП Florence RJ45 кат.5E",
                    CategoryName = "Электротовары",
                    Price = 0m,
                    HasPrice = false,
                    SectionId = "684"
                });

                item.BitrixProducts.Add(new BitrixProductViewModel
                {
                    ProductId = "22329",
                    ProductName = "Инструмент для зачистки многожил. кабеля",
                    CategoryName = "Инструменты",
                    Price = 1560.00m,
                    HasPrice = true,
                    SectionId = "684"
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки товаров: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

       

        // Команда для кнопки
        private class RelayCommand : ICommand
        {
            private readonly Action<object> _execute;
            private readonly Predicate<object> _canExecute;

            public RelayCommand(Action<object> execute) : this(execute, null) { }

            public RelayCommand(Action<object> execute, Predicate<object> canExecute)
            {
                _execute = execute ?? throw new ArgumentNullException(nameof(execute));
                _canExecute = canExecute;
            }

            public bool CanExecute(object parameter) => _canExecute?.Invoke(parameter) ?? true;

            public void Execute(object parameter) => _execute(parameter);

            public event EventHandler CanExecuteChanged
            {
                add { CommandManager.RequerySuggested += value; }
                remove { CommandManager.RequerySuggested -= value; }
            }
        }

        // Обработчики кнопок навигации
        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Возврат на предыдущую страницу",
                "Назад", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void btnNext_Click(object sender, RoutedEventArgs e)
        {
            // Проверяем, что все товары сопоставлены
            var unmatchedProducts = ProductItems.Where(p => p.SelectedBitrixProduct == null).ToList();

            if (unmatchedProducts.Any())
            {
                MessageBox.Show($"Есть неподобранные товары: {unmatchedProducts.Count}\n" +
                              "Пожалуйста, сопоставьте все товары перед продолжением.",
                              "Внимание",
                              MessageBoxButton.OK,
                              MessageBoxImage.Warning);
                return;
            }

            MessageBox.Show("Переход к редактированию цены",
                "Далее", MessageBoxButton.OK, MessageBoxImage.Information);
        }


        private void AddProduct(object parameter)
        {
            if (parameter is ProductItemViewModel item)
            {
                // Открываем модальное окно поиска
                var searchWindow = new SearchBitrixProduct();
                searchWindow.Owner = Window.GetWindow(this);
                searchWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;

                // Подписываемся на событие выбора товара
                searchWindow.ProductSelected += (selectedProduct) =>
                {
                    // Обновляем выбранный товар
                    item.SelectedBitrixProduct = selectedProduct;

                    // Показываем сообщение
                    MessageBox.Show($"Добавлено сопоставление:\n" +
                                  $"Заявка: {item.OriginalProduct}\n" +
                                  $"Bitrix: {selectedProduct.ProductName}\n" +
                                  $"Цена: {(selectedProduct.HasPrice ? selectedProduct.Price.ToString("C") : "Нет цены")}",
                                  "Сопоставление добавлено",
                                  MessageBoxButton.OK,
                                  MessageBoxImage.Information);
                };

                // Показываем окно как модальное
                searchWindow.ShowDialog();
            }
        }
    }
}