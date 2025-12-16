using ManagerApp.Classes;
using ManagerApp.Classes.Setting;
using ManagerApp.Data.ScharedData;
using ManagerApp.Data.Search;
using ManagerApp.Data.StructureList;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ManagerApp.Pages
{
    public partial class ComparisonProduct : Page
    {
        // Свойство для привязки данных
        public ObservableCollection<ProductItemViewModel> ProductItems { get; set; }
        private BitrixProductMatcher _productMatcher;

        // Конструктор без параметров (использует сохраненные данные)
        public ComparisonProduct()
        {
            InitializeComponent();
            _productMatcher = new BitrixProductMatcher();
            ProductItems = new ObservableCollection<ProductItemViewModel>();
            DataContext = this;

            // Загружаем сохраненные товары
            //LoadProductsFromManager();
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

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService.CanGoBack)
            {
                NavigationService.GoBack();
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
                    BitrixProducts = new ObservableCollection<BitrixProductViewModel>(),
                    SelectedBitrixProduct = null,
                    AddCommand = new RelayCommand(AddProduct)
                };

                ProductItems.Add(item);
            }
        }

        // Обработчик открытия комбобокса
        private async void ComboBox_DropDownOpened(object sender, EventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (comboBox?.DataContext is ProductItemViewModel item)
            {
                // Проверяем, не загружены ли уже товары
                if (item.BitrixProducts.Any())
                    return;

                await SearchProductsForComboBox(item, item.OriginalProduct);
            }
        }

     
        // ДОБАВЬТЕ ЭТОТ МЕТОД В ComparisonProduct.cs
        private async Task TestSearchImmediately()
        {
            Console.WriteLine("=== ТЕСТ ПРЯМОГО ПОИСКА ===");

            if (!ProductItems.Any())
            {
                Console.WriteLine("Список товаров пустой!");
                return;
            }

            var firstItem = ProductItems.First();
            Console.WriteLine($"Тестируем поиск для: {firstItem.OriginalProduct}");

            await SearchProductsForComboBox(firstItem, firstItem.OriginalProduct);

            // Проверяем результат
            if (firstItem.BitrixProducts.Any())
            {
                Console.WriteLine($"УСПЕХ! Найдено товаров: {firstItem.BitrixProducts.Count}");
                foreach (var product in firstItem.BitrixProducts)
                {
                    Console.WriteLine($"- {product.ProductName}");
                }
            }
            else
            {
                Console.WriteLine("ПРОВАЛ! Товары не найдены");
            }
        }

        // ВЫЗОВИТЕ ЭТОТ МЕТОД ПОСЛЕ ЗАГРУЗКИ ТОВАРОВ:
        private void LoadProductsFromManager()
        {
            var products = ProductSelectionManager.GetProducts();

            if (products == null || products.Count == 0)
            {
                // Если нет сохраненных товаров, загружаем тестовые
                LoadSampleData();
            }
            else
            {
                foreach (var product in products)
                {
                    var item = new ProductItemViewModel
                    {
                        OriginalProduct = product,
                        BitrixProducts = new ObservableCollection<BitrixProductViewModel>(),
                        SelectedBitrixProduct = null,
                        AddCommand = new RelayCommand(AddProduct)
                    };

                    ProductItems.Add(item);
                }
            }

            // ТЕСТ: Запустите тестовый поиск
            _ = TestSearchImmediately(); // async void вызов
        }

        // Обработчик изменения текста в комбобоксе через подписку на TextBox
        private async void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            // Находим родительский ComboBox
            var comboBox = FindParent<ComboBox>(textBox);
            if (comboBox?.DataContext is ProductItemViewModel item)
            {
                string searchText = textBox.Text?.Trim();

                // Если текст пустой или слишком короткий, закрываем выпадающий список
                if (string.IsNullOrWhiteSpace(searchText) || searchText.Length < 2)
                {
                    comboBox.IsDropDownOpen = false;
                    return;
                }

                // Задержка перед поиском (дебаунс)
                await Task.Delay(300);

                // Если текст изменился снова, не выполняем поиск
                if (textBox.Text?.Trim() != searchText)
                    return;

                await SearchProductsForComboBox(item, searchText);

                // Открываем выпадающий список если есть результаты
                if (item.BitrixProducts.Any())
                {
                    comboBox.IsDropDownOpen = true;
                }
            }
        }

        // Вспомогательный метод для поиска родительского элемента
        private static T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T parent)
                    return parent;
                child = VisualTreeHelper.GetParent(child);
            }
            return null;
        }

        private void AddProduct(object parameter)
        {
            if (parameter is ProductItemViewModel item)
            {
                // Сохраняем текущий товар в статическом менеджере
                ProductDataManager.SetOriginalProduct(item.OriginalProduct);

                // Открываем модальное окно поиска
                var searchWindow = new SearchBitrixProduct();
                searchWindow.Owner = Window.GetWindow(this);
                searchWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;

                // Показываем окно как модальное
                bool? result = searchWindow.ShowDialog();

                if (result == true)
                {
                    // Получаем выбранный товар из менеджера
                    var selectedProduct = ProductDataManager.GetSelectedProduct(item.OriginalProduct);

                    if (selectedProduct != null)
                    {
                        // Обновляем выбранный товар
                        item.SelectedBitrixProduct = selectedProduct;

                        // Добавляем в список BitrixProducts, если его там нет
                        if (!item.BitrixProducts.Any(p => p.ProductId == selectedProduct.ProductId))
                        {
                            item.BitrixProducts.Add(selectedProduct);
                        }

                        // Показываем сообщение
                        MessageBox.Show($"Добавлено сопоставление:\n" +
                                      $"Заявка: {item.OriginalProduct}\n" +
                                      $"Bitrix: {selectedProduct.ProductName}\n" +
                                      $"Цена: {(selectedProduct.HasPrice ? selectedProduct.Price.ToString("C") : "Нет цены")}",
                                      "Сопоставление добавлено",
                                      MessageBoxButton.OK,
                                      MessageBoxImage.Information);

                        // Обновляем привязку данных
                        item.OnPropertyChanged(nameof(item.SelectedBitrixProduct));
                    }
                }
            }
        }

        // Обработчики кнопок навигации


        private void btnNext_Click(object sender, RoutedEventArgs e)
        {
            // Проверяем, что все товары сопоставлены
            var unmatchedProducts = ProductItems.Where(p => p.SelectedBitrixProduct == null).ToList();

            if (unmatchedProducts.Any())
            {
                string unmatchedList = string.Join("\n", unmatchedProducts.Select(p => $"• {p.OriginalProduct}"));

                MessageBox.Show($"Есть неподобранные товары: {unmatchedProducts.Count}\n\n" +
                              $"Неподобранные товары:\n{unmatchedList}\n\n" +
                              "Пожалуйста, сопоставьте все товары перед продолжением.",
                              "Внимание",
                              MessageBoxButton.OK,
                              MessageBoxImage.Warning);
                return;
            }

            // Собираем список сопоставленных товаров
            var matchedProducts = new List<MatchedProduct>();
            foreach (var item in ProductItems)
            {
                if (item.SelectedBitrixProduct != null)
                {
                    var matchedProduct = new MatchedProduct
                    {
                        BitrixProductId = Convert.ToInt32(item.SelectedBitrixProduct.ProductId), // ДОБАВЬТЕ!
                        OriginalProductName = item.OriginalProduct,
                        BitrixProductName = item.SelectedBitrixProduct.ProductName,
                        BitrixPrice = item.SelectedBitrixProduct.Price,
                        CustomPrice = item.SelectedBitrixProduct.Price,
                        Quantity = 1,
                        Unit = "шт.",
                        VAT = SettingsHelper.GetVATAsString()
                    };
                    matchedProducts.Add(matchedProduct);
                }
            }

            // Сохраняем сопоставленные товары в менеджере
            PriceDataManager.SetMatchedProducts(matchedProducts);

            // Переходим на страницу редактирования цен
            var editPricePage = new EditPricePage(matchedProducts);
            NavigationService.Navigate(editPricePage);
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

        // Обработчик нажатия клавиш в комбобоксе
        private async void ComboBox_KeyUp(object sender, KeyEventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (comboBox?.DataContext is ProductItemViewModel item)
            {
                // Ждем небольшое время после ввода
                await Task.Delay(500);

                // Получаем текущий текст
                string searchText = comboBox.Text?.Trim();

                // Игнорируем короткие запросы
                if (string.IsNullOrWhiteSpace(searchText) || searchText.Length < 2)
                    return;

                // Выполняем поиск
                await SearchProductsForComboBox(item, searchText);
            }
        }


        // Асинхронный поиск товаров для комбобокса
        // Асинхронный поиск товаров для комбобокса
        private async Task SearchProductsForComboBox(ProductItemViewModel item, string searchText)
        {
            try
            {
                Console.WriteLine($"=== ПОИСК ДЛЯ: '{searchText}' ===");

                // Ищем похожие товары в Bitrix
                var similarProducts = await _productMatcher.FindSimilarProductsAsync(
                    searchText,
                    maxResults: 10,
                    minSimilarityThreshold: 0.5);

                Console.WriteLine($"Получено товаров: {similarProducts.Count}");

                // Очищаем текущий список
                item.BitrixProducts.Clear();

                // Добавляем найденные товары
                foreach (var product in similarProducts)
                {
                    Console.WriteLine($"Добавляем товар: {product.ProductName}");

                    item.BitrixProducts.Add(new BitrixProductViewModel
                    {
                        ProductId = product.ProductId,
                        ProductName = product.ProductName,
                        CategoryName = product.CategoryName,
                        Price = product.Price,
                        HasPrice = product.HasPrice,
                        SectionId = product.SectionId
                    });
                }

                // Если ничего не найдено, комбобокс останется пустым
                if (!item.BitrixProducts.Any())
                {
                    Console.WriteLine($"Товары не найдены для: {searchText}");
                }
                else
                {
                    Console.WriteLine($"Добавлено товаров: {item.BitrixProducts.Count}");

                    //// Автоматически выбираем первый товар (самый релевантный, так как уже отсортирован)
                    //if (item.SelectedBitrixProduct == null && item.BitrixProducts.Any())
                    //{
                    //    item.SelectedBitrixProduct = item.BitrixProducts.First();
                    //    Console.WriteLine($"Автоматически выбран: {item.SelectedBitrixProduct.ProductName}");
                    //}
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ОШИБКА поиска товаров: {ex.Message}");
                MessageBox.Show($"Ошибка поиска товаров: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

     
        
    }
}