using ManagerApp.Classes;
using ManagerApp.Classes.Setting;
using ManagerApp.Data.GetInfo;
using ManagerApp.Data.ScharedData;
using ManagerApp.Data.Search;
using ManagerApp.Data.StructureList;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ManagerApp.Pages
{
    public partial class ComparisonProduct : Page, INotifyPropertyChanged
    {
        public ObservableCollection<ProductItemViewModel> ProductItems { get; set; }
        private BitrixProductMatcher _productMatcher;
        private bool _isLoading = false;
        private Dictionary<string, List<Data.ScharedData.BitrixProductViewModel>> _searchCache = new Dictionary<string, List<Data.ScharedData.BitrixProductViewModel>>();

        public ComparisonProduct()
        {
            InitializeComponent();
            _productMatcher = new BitrixProductMatcher();
            ProductItems = new ObservableCollection<ProductItemViewModel>();
            DataContext = this;

            LoadProductsFromManager();
        }

        public ComparisonProduct(List<string> products) : this()
        {
            if (products != null && products.Count > 0)
            {
                ProductSelectionManager.SetProducts(products);
            }
        }

        #region Обработчики кнопок

        private async void BtnQuickAdd_Click(object sender, RoutedEventArgs e)
        {
            await AddProductFromTextBoxAsync();
        }

        private void BtnClearAll_Click(object sender, RoutedEventArgs e)
        {
            ClearAllProducts();
        }

        private async void TxtNewProduct_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(txtNewProduct.Text))
            {
                await AddProductFromTextBoxAsync();
            }
        }

        private void TxtNewProduct_TextChanged(object sender, TextChangedEventArgs e)
        {
            btnQuickAdd.IsEnabled = !string.IsNullOrWhiteSpace(txtNewProduct.Text);
        }

        #endregion

        #region Методы добавления/удаления товаров

        private async Task AddProductFromTextBoxAsync()
        {
            string productName = txtNewProduct.Text.Trim();
            if (!string.IsNullOrEmpty(productName))
            {
                txtNewProduct.Text = string.Empty;
                txtNewProduct.Focus();

                await AddProductItemAsync(productName);
            }
        }

        private async Task AddProductItemAsync(string productName)
        {
            if (ProductItems.Any(p => p.OriginalProduct.Equals(productName, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show($"Товар '{productName}' уже существует в списке",
                              "Внимание",
                              MessageBoxButton.OK,
                              MessageBoxImage.Warning);
                return;
            }

            var item = new ProductItemViewModel
            {
                OriginalProduct = productName,
                BitrixProducts = new ObservableCollection<Data.ScharedData.BitrixProductViewModel>(),
                SelectedBitrixProduct = null
            };

            item.AddCommand = new RelayCommand(AddProduct);
            item.RemoveCommand = new RelayCommand(RemoveProductItem);

            ProductItems.Add(item);

            var currentProducts = ProductSelectionManager.GetProducts() ?? new List<string>();
            if (!currentProducts.Contains(productName, StringComparer.OrdinalIgnoreCase))
            {
                currentProducts.Add(productName);
                ProductSelectionManager.SetProducts(currentProducts);
            }

            await SmartSearchProductsAsync(item, productName);
        }

        private void RemoveProductItem(object parameter)
        {
            if (parameter is ProductItemViewModel item)
            {
                var result = MessageBox.Show($"Удалить товар '{item.OriginalProduct}'?",
                                           "Подтверждение удаления",
                                           MessageBoxButton.YesNo,
                                           MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    ProductItems.Remove(item);

                    var currentProducts = ProductSelectionManager.GetProducts()?.ToList() ?? new List<string>();
                    currentProducts.RemoveAll(p => p.Equals(item.OriginalProduct, StringComparison.OrdinalIgnoreCase));
                    ProductSelectionManager.SetProducts(currentProducts);

                    MessageBox.Show($"Товар '{item.OriginalProduct}' удален",
                                  "Успешно",
                                  MessageBoxButton.OK,
                                  MessageBoxImage.Information);
                }
            }
        }

        private void ClearAllProducts()
        {
            if (!ProductItems.Any())
            {
                MessageBox.Show("Список товаров пуст",
                              "Информация",
                              MessageBoxButton.OK,
                              MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show($"Вы уверены, что хотите удалить все товары ({ProductItems.Count} шт.)?",
                                       "Подтверждение удаления",
                                       MessageBoxButton.YesNo,
                                       MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                ProductItems.Clear();
                ProductSelectionManager.ClearProducts();
                _searchCache.Clear();

                MessageBox.Show("Все товары удалены",
                              "Готово",
                              MessageBoxButton.OK,
                              MessageBoxImage.Information);
            }
        }

        #endregion

        #region УЛУЧШЕННЫЙ ПОИСК ТОВАРОВ

        private async void LoadProductsFromManager()
        {
            _isLoading = true;

            try
            {
                var products = ProductSelectionManager.GetProducts();

                if (products == null || products.Count == 0)
                {
                    LoadSampleData();
                }
                else
                {
                    ProductItems.Clear();

                    foreach (var product in products.Distinct(StringComparer.OrdinalIgnoreCase))
                    {
                        var item = new ProductItemViewModel
                        {
                            OriginalProduct = product,
                            BitrixProducts = new ObservableCollection<Data.ScharedData.BitrixProductViewModel>(),
                            SelectedBitrixProduct = null
                        };

                        item.AddCommand = new RelayCommand(AddProduct);
                        item.RemoveCommand = new RelayCommand(RemoveProductItem);

                        ProductItems.Add(item);
                    }

                    await SmartSearchForAllProductsAsync();
                }
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void LoadSampleData()
        {
            var sampleProducts = new List<string>();

            foreach (var product in sampleProducts)
            {
                var item = new ProductItemViewModel
                {
                    OriginalProduct = product,
                    BitrixProducts = new ObservableCollection<Data.ScharedData.BitrixProductViewModel>(),
                    SelectedBitrixProduct = null
                };

                item.AddCommand = new RelayCommand(AddProduct);
                item.RemoveCommand = new RelayCommand(RemoveProductItem);

                ProductItems.Add(item);
            }
        }

        private async Task SmartSearchForAllProductsAsync()
        {
            if (!ProductItems.Any()) return;

            try
            {
                var searchTasks = new List<Task>();

                foreach (var item in ProductItems)
                {
                    if (string.IsNullOrWhiteSpace(item.OriginalProduct)) continue;

                    searchTasks.Add(SmartSearchProductsAsync(item, item.OriginalProduct));
                }

                await Task.WhenAll(searchTasks);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при автопоиске: {ex.Message}");
            }
        }

        private async Task SmartSearchProductsAsync(ProductItemViewModel item, string searchText)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(searchText))
                {
                    return;
                }

                string cacheKey = searchText.ToLower().Trim();

                if (_searchCache.TryGetValue(cacheKey, out var cachedResults))
                {
                    await UpdateProductsListAsync(item, cachedResults);
                    return;
                }

                var allResults = new List<Data.ScharedData.BitrixProductViewModel>();
                // Основной поиск
                var searchResults = await _productMatcher.FindSimilarProductsAsync(searchText, maxResults: 10);
                // Преобразуем ProductWithCategoryInfo в BitrixProductViewModel
                var convertedResults = searchResults.Select(p => new Data.ScharedData.BitrixProductViewModel
                {
                    ProductId = p.ProductId,
                    ProductName = p.ProductName,
                    CategoryName = p.CategoryName,
                    Price = p.Price,
                    HasPrice = p.HasPrice,
                    SectionId = p.SectionId
                }).ToList();

                allResults.AddRange(convertedResults);

                // Если мало результатов, ищем по словам
                if (allResults.Count < 3)
                {
                    var words = searchText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var word in words.Where(w => w.Length > 2))
                    {
                        var wordResults = await _productMatcher.FindSimilarProductsAsync(word, maxResults: 5);
                        var convertedWordResults = wordResults.Select(p => new Data.ScharedData.BitrixProductViewModel
                        {
                            ProductId = p.ProductId,
                            ProductName = p.ProductName,
                            CategoryName = p.CategoryName,
                            Price = p.Price,
                            HasPrice = p.HasPrice,
                            SectionId = p.SectionId
                        }).ToList();

                        allResults.AddRange(convertedWordResults.Where(r => !allResults.Any(er => er.ProductId == r.ProductId)));
                    }
                }

                var rankedResults = RankProducts(allResults, searchText)
                    .Take(10)
                    .ToList();

                if (rankedResults.Any())
                {
                    _searchCache[cacheKey] = rankedResults;
                }

                await UpdateProductsListAsync(item, rankedResults);

                await TryAutoSelectBestMatchAsync(item, rankedResults, searchText);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при умном поиске: {ex.Message}");
            }
        }

        private List<Data.ScharedData.BitrixProductViewModel> RankProducts(List<Data.ScharedData.BitrixProductViewModel> products, string searchQuery)
        {
            if (!products.Any()) return products;

            var searchQueryLower = searchQuery.ToLower().Trim();
            var words = searchQueryLower.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            return products
                .Select(p => new
                {
                    Product = p,
                    Score = CalculateRelevanceScore(p, searchQueryLower, words)
                })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .Select(x => x.Product)
                .ToList();
        }

        private double CalculateRelevanceScore(Data.ScharedData.BitrixProductViewModel product, string searchQuery, string[] searchWords)
        {
            double score = 0;

            if (product == null || string.IsNullOrEmpty(product.ProductName)) return 0;

            string productNameLower = product.ProductName.ToLower();

            // 1. Точное совпадение
            if (productNameLower == searchQuery)
                score += 100;

            // 2. Содержит весь поисковый запрос
            if (productNameLower.Contains(searchQuery))
                score += 80;

            // 3. Совпадение всех слов
            if (searchWords.All(word => productNameLower.Contains(word)))
                score += 60;

            // 4. Совпадение большинства слов
            int matchingWords = searchWords.Count(word => productNameLower.Contains(word));
            if (matchingWords > 0)
                score += matchingWords * 15;

            // 5. Начинается с поискового запроса
            if (productNameLower.StartsWith(searchQuery))
                score += 30;

            // 6. Учитываем наличие цены
            if (product.HasPrice && product.Price > 0)
                score += 10;

            return Math.Max(0, score);
        }

        private async Task UpdateProductsListAsync(ProductItemViewModel item, List<Data.ScharedData.BitrixProductViewModel> products)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                item.BitrixProducts.Clear();

                if (products == null || !products.Any())
                {
                    item.BitrixProducts.Add(new Data.ScharedData.BitrixProductViewModel
                    {
                        ProductId = "0",
                        ProductName = "❌ Товар не найден",
                        CategoryName = "Нажмите кнопку поиска для ручного подбора",
                        HasPrice = false
                    });
                    return;
                }

                foreach (var product in products)
                {
                    item.BitrixProducts.Add(new Data.ScharedData.BitrixProductViewModel
                    {
                        ProductId = product.ProductId,
                        ProductName = FormatProductNameForDisplay(product),
                        CategoryName = product.CategoryName,
                        Price = product.Price,
                        HasPrice = product.HasPrice,
                        SectionId = product.SectionId
                    });
                }
            });
        }

        private string FormatProductNameForDisplay(Data.ScharedData.BitrixProductViewModel product)
        {
            var sb = new StringBuilder();

            sb.Append(product.ProductName);

            if (!string.IsNullOrEmpty(product.CategoryName))
                sb.Append($" ({product.CategoryName})");

            if (product.HasPrice && product.Price > 0)
                sb.Append($" - {product.Price:#,##0.00} ₽");

            return sb.ToString();
        }

        private async Task TryAutoSelectBestMatchAsync(ProductItemViewModel item, List<Data.ScharedData.BitrixProductViewModel> results, string searchText)
        {
            if (!results.Any() || item.SelectedBitrixProduct != null)
                return;

            var bestMatch = results.FirstOrDefault();

            if (bestMatch != null && IsGoodAutoMatch(bestMatch, searchText))
            {
                await Task.Delay(300);

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var displayProduct = item.BitrixProducts.FirstOrDefault(p => p.ProductId == bestMatch.ProductId);
                    if (displayProduct != null)
                    {
                        item.SelectedBitrixProduct = displayProduct;
                        Console.WriteLine($"✅ Автоматически выбрано: '{item.OriginalProduct}' → '{bestMatch.ProductName}'");
                    }
                });
            }
        }

        private bool IsGoodAutoMatch(Data.ScharedData.BitrixProductViewModel product, string searchText)
        {
            if (product == null || string.IsNullOrEmpty(product.ProductName))
                return false;

            string productNameLower = product.ProductName.ToLower();
            string searchLower = searchText.ToLower();

            // 1. Точное совпадение
            if (productNameLower == searchLower)
                return true;

            // 2. Содержит весь поисковый запрос
            if (productNameLower.Contains(searchLower))
                return true;

            // 3. Все слова из запроса присутствуют
            var searchWords = searchLower.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (searchWords.All(word => productNameLower.Contains(word)))
                return true;

            // 4. Высокая релевантность
            var score = CalculateRelevanceScore(product, searchLower, searchWords);
            return score >= 80;
        }

        private async void ComboBox_DropDownOpened(object sender, EventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (comboBox?.DataContext is ProductItemViewModel item)
            {
                if (item.BitrixProducts.Any(p => p.ProductId != "0"))
                    return;

                await SmartSearchProductsAsync(item, item.OriginalProduct);
            }
        }

        private async void ComboBox_KeyUp(object sender, KeyEventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (comboBox?.DataContext is ProductItemViewModel item)
            {
                if (e.Key == Key.Enter || e.Key == Key.Escape || e.Key == Key.Tab)
                    return;

                await Task.Delay(500);

                string searchText = comboBox.Text?.Trim();
                if (string.IsNullOrWhiteSpace(searchText) || searchText.Length < 2)
                    return;

                if (item.SelectedBitrixProduct != null &&
                    !item.SelectedBitrixProduct.ProductName.Contains(searchText))
                {
                    item.SelectedBitrixProduct = null;
                }

                await SmartSearchProductsAsync(item, searchText);
            }
        }

        #endregion

        #region Существующие методы

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService.CanGoBack)
            {
                NavigationService.GoBack();
            }
        }

        private void AddProduct(object parameter)
        {
            if (parameter is ProductItemViewModel item)
            {
                ProductDataManager.SetOriginalProduct(item.OriginalProduct);

                var searchWindow = new SearchBitrixProduct();
                searchWindow.Owner = Window.GetWindow(this);
                searchWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;

                bool? result = searchWindow.ShowDialog();

                if (result == true)
                {
                    var selectedProduct = ProductDataManager.GetSelectedProduct(item.OriginalProduct);

                    if (selectedProduct != null)
                    {
                        var productToSelect = item.BitrixProducts.FirstOrDefault(p =>
                            p.ProductId == selectedProduct.ProductId);

                        if (productToSelect == null)
                        {
                            productToSelect = new Data.ScharedData.BitrixProductViewModel
                            {
                                ProductId = selectedProduct.ProductId,
                                ProductName = FormatProductNameForDisplay(selectedProduct),
                                CategoryName = selectedProduct.CategoryName,
                                Price = selectedProduct.Price,
                                HasPrice = selectedProduct.HasPrice,
                                SectionId = selectedProduct.SectionId
                            };
                            item.BitrixProducts.Insert(0, productToSelect);
                        }

                        item.SelectedBitrixProduct = productToSelect;

                        MessageBox.Show($"Добавлено сопоставление:\n" +
                                      $"Заявка: {item.OriginalProduct}\n" +
                                      $"Bitrix: {selectedProduct.ProductName}\n" +
                                      $"Цена: {(selectedProduct.HasPrice ? selectedProduct.Price.ToString("C") : "Нет цены")}",
                                      "Сопоставление добавлено",
                                      MessageBoxButton.OK,
                                      MessageBoxImage.Information);

                        item.OnPropertyChanged(nameof(item.SelectedBitrixProduct));
                    }
                }
            }
        }

        private async void btnNext_Click(object sender, RoutedEventArgs e)
        {
            BitrixService bitrixService = new BitrixService();

            // Проверка 1: Есть ли вообще товары в списке
            if (!ProductItems.Any())
            {
                MessageBox.Show("Список товаров пуст. Добавьте хотя бы один товар для сопоставления.",
                              "Нет товаров",
                              MessageBoxButton.OK,
                              MessageBoxImage.Warning);
                return;
            }

            // Итоговый список сопоставленных товаров
            var matchedProductList = new List<MatchedProduct>();

            // Обрабатываем уже сопоставленные товары
            foreach (var item in ProductItems.Where(p => p.SelectedBitrixProduct != null))
            {
                // Безопасное преобразование ID
                if (!int.TryParse(item.SelectedBitrixProduct?.ProductId, out int bitrixProductId) || bitrixProductId <= 0)
                {
                    MessageBox.Show($"Некорректный ID товара для '{item.OriginalProduct}'",
                                  "Ошибка",
                                  MessageBoxButton.OK,
                                  MessageBoxImage.Error);
                    return;
                }

                var matchedProduct = new MatchedProduct
                {
                    BitrixProductId = bitrixProductId,
                    OriginalProductName = item.OriginalProduct,
                    BitrixProductName = item.SelectedBitrixProduct.ProductName,
                    BitrixPrice = item.SelectedBitrixProduct.Price,
                    CustomPrice = item.SelectedBitrixProduct.Price,
                    Quantity = 1,
                    Unit = "шт.",
                    VAT = SettingsHelper.GetVATAsString()
                };
                matchedProductList.Add(matchedProduct);
            }

            // Проверяем несопоставленные товары
            var unmatchedProducts = ProductItems.Where(p => p.SelectedBitrixProduct == null).ToList();

            if (unmatchedProducts.Any())
            {
                var result = MessageBox.Show($"Найдено {unmatchedProducts.Count} неподобранных товаров.\n\n" +
                                            "Хотите создать их в Битрикс24 автоматически?",
                                            "Создание товаров",
                                            MessageBoxButton.YesNo,
                                            MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        // Создаем каждый несопоставленный товар и сразу добавляем в список
                        foreach (var item in unmatchedProducts)
                        {
                            int createdProductId = await bitrixService.CreateProductAsync(item.OriginalProduct);

                            if (createdProductId > 0)
                            {
                                // Сразу добавляем созданный товар в итоговый список
                                var matchedProduct = new MatchedProduct
                                {
                                    BitrixProductId = createdProductId,
                                    OriginalProductName = item.OriginalProduct,
                                    BitrixProductName = item.OriginalProduct, // Используем оригинальное название
                                    BitrixPrice = 0, // Цена по умолчанию
                                    CustomPrice = 0, // Цена по умолчанию
                                    Quantity = 1,
                                    Unit = "шт.",
                                    VAT = SettingsHelper.GetVATAsString()
                                };
                                matchedProductList.Add(matchedProduct);



                                Console.WriteLine($"✅ Создан товар '{item.OriginalProduct}' (ID: {createdProductId})");
                            }
                            else
                            {
                                MessageBox.Show($"Не удалось создать товар '{item.OriginalProduct}'",
                                              "Ошибка создания",
                                              MessageBoxButton.OK,
                                              MessageBoxImage.Error);
                                return;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка при создании товаров: {ex.Message}",
                                      "Ошибка",
                                      MessageBoxButton.OK,
                                      MessageBoxImage.Error);
                        return;
                    }
                }
                else
                {
                    MessageBox.Show("Пожалуйста, сопоставьте все товары вручную перед продолжением.",
                                  "Необходимо сопоставление",
                                  MessageBoxButton.OK,
                                  MessageBoxImage.Warning);
                    return;
                }
            }

            // Проверка: есть ли товары в итоговом списке
            if (!matchedProductList.Any())
            {
                MessageBox.Show("Не удалось сформировать список товаров для обработки.",
                              "Ошибка",
                              MessageBoxButton.OK,
                              MessageBoxImage.Error);
                return;
            }

            // Сохраняем и переходим на следующую страницу
            PriceDataManager.SetMatchedProducts(matchedProductList);
            var editPricePage = new EditPricePage(matchedProductList);
            NavigationService.Navigate(editPricePage);
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void txtNewProduct_TextChanged_1(object sender, TextChangedEventArgs e)
        {
            // Пустая реализация
        }

        #endregion
    }

    #region Вспомогательные классы

    public class ProductItemViewModel : INotifyPropertyChanged
    {
        private string _originalProduct;
        private Data.ScharedData.BitrixProductViewModel _selectedBitrixProduct;

        public string OriginalProduct
        {
            get => _originalProduct;
            set
            {
                _originalProduct = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<Data.ScharedData.BitrixProductViewModel> BitrixProducts { get; set; }

        public Data.ScharedData.BitrixProductViewModel SelectedBitrixProduct
        {
            get => _selectedBitrixProduct;
            set
            {
                _selectedBitrixProduct = value;
                OnPropertyChanged();
            }
        }

        public ICommand AddCommand { get; set; }
        public ICommand RemoveCommand { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        public virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class RelayCommand : ICommand
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

    #endregion
}