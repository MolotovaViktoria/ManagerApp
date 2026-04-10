using ManagerApp.Classes;
using ManagerApp.Classes.Setting;
using ManagerApp.Data.GetInfo;
using ManagerApp.Data.ScharedData;
using ManagerApp.Data.Search;
using ManagerApp.Data.StructureList;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace ManagerApp.Pages
{
    public partial class ComparisonProduct : Page, INotifyPropertyChanged
    {
        public ObservableCollection<ProductItemViewModel> ProductItems { get; set; }
        private BitrixProductMatcher _productMatcher;
        private bool _isLoading = false;
        public int Index { get; set; }
        private Dictionary<string, List<Data.ScharedData.BitrixProductViewModel>> _searchCache = new Dictionary<string, List<Data.ScharedData.BitrixProductViewModel>>();
        // НОВЫЕ ПОЛЯ ДЛЯ ЕДИНИЦ ИЗМЕРЕНИЯ
        private ObservableCollection<BitrixMeasure> _allMeasures;
        private Dictionary<string, BitrixMeasure> _measuresDictionary = new Dictionary<string, BitrixMeasure>();
        private bool _measuresLoaded = false;
        private readonly object _measuresLock = new object();

        public ObservableCollection<BitrixMeasure> AllMeasures
        {
            get => _allMeasures;
            set
            {
                _allMeasures = value;
                OnPropertyChanged(nameof(AllMeasures));
            }
        }

        private async Task EnsureMeasuresLoaded()
        {
            lock (_measuresLock)
            {
                if (_measuresLoaded)
                    return;
            }

            try
            {
                AllMeasures = new ObservableCollection<BitrixMeasure>();

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
                        _measuresLoaded = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка загрузки единиц измерения: {ex.Message}");
            }
        }
        private string GetMeasureSymbol(string measureId)
        {
            if (string.IsNullOrEmpty(measureId))
                return "шт.";

            lock (_measuresLock)
            {
                if (_measuresDictionary.TryGetValue(measureId, out var measure))
                {
                    if (!string.IsNullOrEmpty(measure.SYMBOL_RUS))
                        return measure.SYMBOL_RUS;
                    if (!string.IsNullOrEmpty(measure.SYMBOL_INTL))
                        return measure.SYMBOL_INTL;
                }
            }
            return "шт.";
        }

        private string GetMeasureName(string measureId)
        {
            if (string.IsNullOrEmpty(measureId))
                return "Штука";

            lock (_measuresLock)
            {
                if (_measuresDictionary.TryGetValue(measureId, out var measure))
                {
                    if (!string.IsNullOrEmpty(measure.MEASURE_TITLE))
                        return measure.MEASURE_TITLE;
                }
            }
            return "Штука";
        }

        private async Task AddProductItemAsync(string productName)
        {
            if (ProductItems.Any(p => p.OriginalProduct.Equals(productName, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show($"Товар '{productName}' уже существует в списке", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var item = new ProductItemViewModel
            {
                OriginalProduct = productName,
                BitrixProducts = new ObservableCollection<Data.ScharedData.BitrixProductViewModel>(),
                SelectedBitrixProduct = null,
                Quantity = 1,
                Description = string.Empty, // 👈 Пустое описание для нового товара
                SelectedMeasureId = GetDefaultMeasureId()
            };

            if (!string.IsNullOrEmpty(item.SelectedMeasureId))
            {
                item.MeasureSymbol = GetMeasureSymbol(item.SelectedMeasureId);
                item.MeasureName = GetMeasureName(item.SelectedMeasureId);
            }
            else
            {
                item.MeasureSymbol = "шт.";
                item.MeasureName = "Штука";
            }

            item.AddCommand = new RelayCommand(AddProduct);
            item.RemoveCommand = new RelayCommand(RemoveProductItem);

            ProductItems.Add(item);
            UpdateItemsIndexes();

            var currentProducts = ProductSelectionManager.GetProducts() ?? new List<string>();
            if (!currentProducts.Contains(productName, StringComparer.OrdinalIgnoreCase))
            {
                currentProducts.Add(productName);
                ProductSelectionManager.SetProducts(currentProducts);
            }

            _totalSearchTasks = 1;
            _completedSearchTasks = 0;
            ShowProgress($"Поиск: {productName}");

            try
            {
                await SmartSearchProductsWithProgressAsync(item, productName);
                UpdateProgress(1, 1, "Готово!");
                await Task.Delay(500);
            }
            finally
            {
                HideProgress();
            }
        }

     

        private string GetMeasureNameBySymbol(string symbol)
        {
            if (string.IsNullOrEmpty(symbol)) return "Штука";

            lock (_measuresLock)
            {
                var measure = _measuresDictionary.Values
                    .FirstOrDefault(m =>
                        (m.SYMBOL_RUS?.Equals(symbol, StringComparison.OrdinalIgnoreCase) == true) ||
                        (m.SYMBOL_INTL?.Equals(symbol, StringComparison.OrdinalIgnoreCase) == true));

                if (measure != null && !string.IsNullOrEmpty(measure.MEASURE_TITLE))
                    return measure.MEASURE_TITLE;
            }

            // Fallback на словарь
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

        private string GetDefaultMeasureId()
        {
            // Ищем единицу измерения "Штука" по умолчанию
            lock (_measuresLock)
            {
                var defaultMeasure = _measuresDictionary.Values
                    .FirstOrDefault(m => (m.SYMBOL_RUS == "шт" || m.SYMBOL_RUS == "шт.") ||
                                          m.MEASURE_TITLE == "Штука" ||
                                          m.CODE == "796"); // Код для штук в Bitrix

                if (defaultMeasure != null)
                    return defaultMeasure.ID;
            }
            return null; // Если не нашли, вернется null, и будет использоваться "шт." по умолчанию
        }
        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            await EnsureMeasuresLoaded();
        }


        public ComparisonProduct()
        {
            InitializeComponent();
            _productMatcher = new BitrixProductMatcher();
            ProductItems = new ObservableCollection<ProductItemViewModel>();
            DataContext = this;

            LoadProductsFromManager();
        }
        // Добавьте эти поля в класс
        private int _totalSearchTasks = 0;
        private int _completedSearchTasks = 0;
        private readonly object _progressLock = new object();

        // Методы для управления прогрессом
        private void ShowProgress(string message)
        {
            Dispatcher.Invoke(() =>
            {
                ProgressPanel.Visibility = Visibility.Visible;
                ProgressText.Text = message;
                ProgressBar.Value = 0;
                ProgressCounter.Text = $"0/{_totalSearchTasks}";

                // Блокируем кнопки во время поиска
                btnQuickAdd.IsEnabled = false;
                btnClearAll.IsEnabled = false;
                btnNext.IsEnabled = false;
                txtNewProduct.IsEnabled = false;
            });
        }
        private void DescriptionTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox?.DataContext is ProductItemViewModel item)
            {
                // Автоматически сохраняем описание при изменении
                Console.WriteLine($"Описание для {item.OriginalProduct}: {item.Description}");
            }
        }
        private void UpdateProgress(int completed, int total, string currentProduct = null)
        {
            Dispatcher.Invoke(() =>
            {
                if (total > 0)
                {
                    double percentage = (double)completed / total * 100;
                    ProgressBar.Value = percentage;
                    ProgressCounter.Text = $"{completed}/{total}";

                    if (!string.IsNullOrEmpty(currentProduct))
                    {
                        ProgressText.Text = $"Поиск: {currentProduct}";
                    }
                    else
                    {
                        ProgressText.Text = $"Поиск товаров... {completed}/{total}";
                    }
                }
            });
        }

        private void HideProgress()
        {
            Dispatcher.Invoke(() =>
            {
                ProgressPanel.Visibility = Visibility.Collapsed;

                // Разблокируем кнопки
                btnQuickAdd.IsEnabled = true;
                btnClearAll.IsEnabled = true;
                btnNext.IsEnabled = true;
                txtNewProduct.IsEnabled = true;
            });
        }

        // Обновленный метод поиска для всех продуктов
        private async Task SmartSearchForAllProductsAsync()
        {
            if (!ProductItems.Any()) return;

            try
            {
                _totalSearchTasks = ProductItems.Count;
                _completedSearchTasks = 0;

                ShowProgress($"Поиск товаров... 0/{_totalSearchTasks}");

                var searchTasks = new List<Task>();

                foreach (var item in ProductItems)
                {
                    if (string.IsNullOrWhiteSpace(item.OriginalProduct))
                    {
                        lock (_progressLock)
                        {
                            _completedSearchTasks++;
                            UpdateProgress(_completedSearchTasks, _totalSearchTasks);
                        }
                        continue;
                    }

                    searchTasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            await SmartSearchProductsWithProgressAsync(item, item.OriginalProduct);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Ошибка при поиске '{item.OriginalProduct}': {ex.Message}");
                        }
                        finally
                        {
                            lock (_progressLock)
                            {
                                _completedSearchTasks++;
                                UpdateProgress(_completedSearchTasks, _totalSearchTasks);
                            }
                        }
                    }));
                }

                await Task.WhenAll(searchTasks);

                // Показываем завершение
                UpdateProgress(_totalSearchTasks, _totalSearchTasks, "Завершено!");
                await Task.Delay(500); // Показываем "Завершено!" полсекунды
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при автопоиске: {ex.Message}");
            }
            finally
            {
                HideProgress();
            }
        }

        // Новый метод поиска с обновлением прогресса
        private async Task SmartSearchProductsWithProgressAsync(ProductItemViewModel item, string searchText)
        {
            try
            {
                // Обновляем статус с текущим товаром
                Dispatcher.Invoke(() =>
                {
                    UpdateProgress(_completedSearchTasks, _totalSearchTasks,
                        searchText.Length > 30 ? searchText.Substring(0, 27) + "..." : searchText);
                });

                if (string.IsNullOrWhiteSpace(searchText))
                    return;

                string cacheKey = searchText.ToLower().Trim();

                if (_searchCache.TryGetValue(cacheKey, out var cachedResults))
                {
                    await UpdateProductsListAsync(item, cachedResults);
                    return;
                }

                Console.WriteLine($"🔍 Поиск: '{searchText}'");
                var searchResults = await _productMatcher.FindSimilarProductsAsync(searchText, 50);

                var bitrixProducts = searchResults.Select(p => new Data.ScharedData.BitrixProductViewModel
                {
                    ProductId = p.ProductId,
                    ProductName = p.ProductName,
                    CategoryName = p.CategoryName,
                    Price = p.Price,
                    HasPrice = p.HasPrice,
                    SectionId = p.SectionId
                }).ToList();

                Console.WriteLine($"✅ Найдено {bitrixProducts.Count} товаров");

                if (bitrixProducts.Any())
                {
                    _searchCache[cacheKey] = bitrixProducts;
                }

                await UpdateProductsListAsync(item, bitrixProducts);
                await TryAutoSelectBestMatchAsync(item, bitrixProducts, searchText);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка при поиске '{searchText}': {ex.Message}");
            }
        }


        public ComparisonProduct(List<string> products) : this()
        {
            if (products != null && products.Count > 0)
            {
                ProductSelectionManager.SetProducts(products);
            }
        }

        // Метод загрузки продуктов
        private async void LoadProductsFromManager()
        {
            _isLoading = true;

            try
            {
                await EnsureMeasuresLoaded();

                // Используем новый метод получения расширенных данных
                var extractedProducts = ProductSelectionManager.GetExtractedProducts();

                if (extractedProducts == null || extractedProducts.Count == 0)
                {
                    // Fallback на старый метод
                    var oldProducts = ProductSelectionManager.GetProducts();
                    if (oldProducts == null || oldProducts.Count == 0)
                    {
                        LoadSampleData();
                    }
                    else
                    {
                        LoadFromOldFormat(oldProducts);
                    }
                }
                else
                {
                    // Загружаем с полными данными
                    ProductItems.Clear();

                    foreach (var extracted in extractedProducts)
                    {
                        Console.WriteLine($"📦 Загрузка товара: {extracted.Name}");
                        Console.WriteLine($"   Количество: {extracted.Quantity} {extracted.MeasureSymbol}");
                        Console.WriteLine($"   Описание: {extracted.Description ?? "Нет описания"}");

                        var item = new ProductItemViewModel
                        {
                            OriginalProduct = extracted.Name,
                            BitrixProducts = new ObservableCollection<Data.ScharedData.BitrixProductViewModel>(),
                            SelectedBitrixProduct = null,
                            Quantity = extracted.Quantity > 0 ? extracted.Quantity : 1,
                            Description = extracted.Description ?? string.Empty, // 👈 КЛЮЧЕВОЕ: сохраняем описание
                            MeasureSymbol = extracted.MeasureSymbol,
                            MeasureName = GetMeasureNameBySymbol(extracted.MeasureSymbol),
                            SelectedMeasureId = FindMeasureIdBySymbol(extracted.MeasureSymbol)
                        };

                        // Если единица измерения не найдена, используем "Штука" по умолчанию
                        if (string.IsNullOrEmpty(item.SelectedMeasureId))
                        {
                            var defaultMeasure = GetDefaultMeasureId();
                            item.SelectedMeasureId = defaultMeasure;
                            item.MeasureSymbol = GetMeasureSymbol(defaultMeasure);
                            item.MeasureName = GetMeasureName(defaultMeasure);
                        }

                        item.AddCommand = new RelayCommand(AddProduct);
                        item.RemoveCommand = new RelayCommand(RemoveProductItem);

                        ProductItems.Add(item);
                    }

                    UpdateItemsIndexes();
                    await SmartSearchForAllProductsAsync();
                }
            }
            finally
            {
                _isLoading = false;
            }
        }

        // Вспомогательный метод для конвертации старого формата
        private void LoadFromOldFormat(List<string> products)
        {
            ProductItems.Clear();

            foreach (var product in products)
            {
                var item = new ProductItemViewModel
                {
                    OriginalProduct = product,
                    BitrixProducts = new ObservableCollection<Data.ScharedData.BitrixProductViewModel>(),
                    SelectedBitrixProduct = null,
                    Quantity = 1,
                    Description = string.Empty,
                    SelectedMeasureId = GetDefaultMeasureId()
                };

                if (!string.IsNullOrEmpty(item.SelectedMeasureId))
                {
                    item.MeasureSymbol = GetMeasureSymbol(item.SelectedMeasureId);
                    item.MeasureName = GetMeasureName(item.SelectedMeasureId);
                }
                else
                {
                    item.MeasureSymbol = "шт.";
                    item.MeasureName = "Штука";
                }

                item.AddCommand = new RelayCommand(AddProduct);
                item.RemoveCommand = new RelayCommand(RemoveProductItem);

                ProductItems.Add(item);
            }

            UpdateItemsIndexes();
        }

        private string FindMeasureIdBySymbol(string symbol)
        {
            if (string.IsNullOrEmpty(symbol)) return null;

            lock (_measuresLock)
            {
                var measure = _measuresDictionary.Values
                    .FirstOrDefault(m =>
                        (m.SYMBOL_RUS?.Equals(symbol, StringComparison.OrdinalIgnoreCase) == true) ||
                        (m.SYMBOL_INTL?.Equals(symbol, StringComparison.OrdinalIgnoreCase) == true) ||
                        (m.MEASURE_TITLE?.Equals(symbol, StringComparison.OrdinalIgnoreCase) == true));

                return measure?.ID;
            }
        }

      

        // 👇 ДОБАВЬТЕ ЭТОТ МЕТОД В КЛАСС ComparisonProduct
        private void UpdateItemsIndexes()
        {
            for (int i = 0; i < ProductItems.Count; i++)
            {
                ProductItems[i].Index = i + 1;
            }
        }

        // Метод загрузки тестовых данных
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

        //// Метод поиска для всех продуктов
        //private async Task SmartSearchForAllProductsAsync()
        //{
        //    if (!ProductItems.Any()) return;

        //    try
        //    {
        //        var searchTasks = new List<Task>();

        //        foreach (var item in ProductItems)
        //        {
        //            if (string.IsNullOrWhiteSpace(item.OriginalProduct)) continue;

        //            searchTasks.Add(SmartSearchProductsAsync(item, item.OriginalProduct));
        //        }

        //        await Task.WhenAll(searchTasks);
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"Ошибка при автопоиске: {ex.Message}");
        //    }
        //}

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

        //private async Task AddProductItemAsync(string productName)
        //{
        //    if (ProductItems.Any(p => p.OriginalProduct.Equals(productName, StringComparison.OrdinalIgnoreCase)))
        //    {
        //        MessageBox.Show($"Товар '{productName}' уже существует в списке",
        //                      "Внимание",
        //                      MessageBoxButton.OK,
        //                      MessageBoxImage.Warning);
        //        return;
        //    }

        //    var item = new ProductItemViewModel
        //    {
        //        OriginalProduct = productName,
        //        BitrixProducts = new ObservableCollection<Data.ScharedData.BitrixProductViewModel>(),
        //        SelectedBitrixProduct = null
        //    };

        //    item.AddCommand = new RelayCommand(AddProduct);
        //    item.RemoveCommand = new RelayCommand(RemoveProductItem);

        //    ProductItems.Add(item);

        //    var currentProducts = ProductSelectionManager.GetProducts() ?? new List<string>();
        //    if (!currentProducts.Contains(productName, StringComparer.OrdinalIgnoreCase))
        //    {
        //        currentProducts.Add(productName);
        //        ProductSelectionManager.SetProducts(currentProducts);
        //    }

        //    await SmartSearchProductsAsync(item, productName);
        //}

        //private void RemoveProductItem(object parameter)
        //{
        //    if (parameter is ProductItemViewModel item)
        //    {
        //        var result = MessageBox.Show($"Удалить товар '{item.OriginalProduct}'?",
        //                                   "Подтверждение удаления",
        //                                   MessageBoxButton.YesNo,
        //                                   MessageBoxImage.Question);

        //        if (result == MessageBoxResult.Yes)
        //        {
        //            ProductItems.Remove(item);

        //            var currentProducts = ProductSelectionManager.GetProducts()?.ToList() ?? new List<string>();
        //            currentProducts.RemoveAll(p => p.Equals(item.OriginalProduct, StringComparison.OrdinalIgnoreCase));
        //            ProductSelectionManager.SetProducts(currentProducts);

        //            MessageBox.Show($"Товар '{item.OriginalProduct}' удален",
        //                          "Успешно",
        //                          MessageBoxButton.OK,
        //                          MessageBoxImage.Information);
        //        }
        //    }
        //}

        //private void ClearAllProducts()
        //{
        //    if (!ProductItems.Any())
        //    {
        //        MessageBox.Show("Список товаров пуст",
        //                      "Информация",
        //                      MessageBoxButton.OK,
        //                      MessageBoxImage.Information);
        //        return;
        //    }

        //    var result = MessageBox.Show($"Вы уверены, что хотите удалить все товары ({ProductItems.Count} шт.)?",
        //                               "Подтверждение удаления",
        //                               MessageBoxButton.YesNo,
        //                               MessageBoxImage.Warning);

        //    if (result == MessageBoxResult.Yes)
        //    {
        //        ProductItems.Clear();
        //        ProductSelectionManager.ClearProducts();
        //        _searchCache.Clear();

        //        MessageBox.Show("Все товары удалены",
        //                      "Готово",
        //                      MessageBoxButton.OK,
        //                      MessageBoxImage.Information);
        //    }
        //}

        #endregion

        #region ПРОСТОЙ ПОИСК ЧЕРЕЗ BitrixProductMatcher

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

                Console.WriteLine($"🔍 Поиск через BitrixProductMatcher: '{searchText}'");

                // ✅ ИСПОЛЬЗУЕМ ТОЛЬКО BitrixProductMatcher для поиска
                var searchResults = await _productMatcher.FindSimilarProductsAsync(searchText, 50);

                // Конвертируем результаты в BitrixProductViewModel
                var bitrixProducts = searchResults.Select(p => new Data.ScharedData.BitrixProductViewModel
                {
                    ProductId = p.ProductId,
                    ProductName = p.ProductName,
                    CategoryName = p.CategoryName,
                    Price = p.Price,
                    HasPrice = p.HasPrice,
                    SectionId = p.SectionId
                }).ToList();

                Console.WriteLine($"✅ Найдено {bitrixProducts.Count} товаров через BitrixProductMatcher");

                if (bitrixProducts.Any())
                {
                    _searchCache[cacheKey] = bitrixProducts;
                }

                await UpdateProductsListAsync(item, bitrixProducts);
                await TryAutoSelectBestMatchAsync(item, bitrixProducts, searchText);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка при поиске: {ex.Message}");
            }
        }

        // Метод форматирования имени для отображения
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

        // Метод обновления списка товаров
        private async Task UpdateProductsListAsync(ProductItemViewModel item, List<Data.ScharedData.BitrixProductViewModel> products)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                item.BitrixProducts.Clear();

                // Добавляем пустой элемент
                item.BitrixProducts.Add(new Data.ScharedData.BitrixProductViewModel
                {
                    ProductId = "0",
                    ProductName = "-- Не выбран --",
                    CategoryName = string.Empty,
                    HasPrice = false,
                    Price = 0
                });

                if (products == null || !products.Any())
                {
                    item.BitrixProducts.Add(new Data.ScharedData.BitrixProductViewModel
                    {
                        ProductId = "-1",
                        ProductName = "❌ Товар не найден",
                        CategoryName = "Нажмите кнопку поиска для ручного подбора",
                        HasPrice = false
                    });
                    return;
                }

                // ✅ Добавляем ВСЕ найденные товары
                foreach (var product in products)
                {
                    item.BitrixProducts.Add(product);
                }

                Console.WriteLine($"✅ В комбобокс добавлено {item.BitrixProducts.Count} товаров");
            });
        }

        // Метод авто-выбора лучшего совпадения
        private async Task TryAutoSelectBestMatchAsync(ProductItemViewModel item, List<Data.ScharedData.BitrixProductViewModel> results, string searchText)
        {
            if (!results.Any() || item.SelectedBitrixProduct != null)
                return;

            var bestMatch = results.FirstOrDefault();

            if (bestMatch != null)
            {
                await Task.Delay(300);

                await Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    var displayProduct = item.BitrixProducts.FirstOrDefault(p => p.ProductId == bestMatch.ProductId);
                    if (displayProduct != null)
                    {
                        item.SelectedBitrixProduct = displayProduct;

                        // АВТОМАТИЧЕСКИ ПОДСТАВЛЯЕМ ЕДИНИЦУ ИЗМЕРЕНИЯ
                        await AutoSetMeasureFromBitrixProduct(item, bestMatch);

                        Console.WriteLine($"✅ Автоматически выбрано: '{item.OriginalProduct}' → '{bestMatch.ProductName}'");
                    }
                });
            }
        }

        // Метод для автоматической подстановки единицы измерения из Bitrix
        private async Task AutoSetMeasureFromBitrixProduct(ProductItemViewModel item, Data.ScharedData.BitrixProductViewModel selectedProduct)
        {
            try
            {
                // Пробуем получить детали товара из Bitrix
                if (int.TryParse(selectedProduct.ProductId, out int productId) && productId > 0)
                {
                    var productDetail = await BitrixProductService.GetProductAsync(productId);

                    if (productDetail != null && !string.IsNullOrEmpty(productDetail.MEASURE))
                    {
                        string measureId = productDetail.MEASURE;

                        // Проверяем, существует ли такая единица измерения в нашем словаре
                        lock (_measuresLock)
                        {
                            if (_measuresDictionary.TryGetValue(measureId, out var measure))
                            {
                                item.SelectedMeasureId = measureId;
                                item.MeasureSymbol = measure.SYMBOL_RUS ?? measure.SYMBOL_INTL ?? "шт.";
                                item.MeasureName = measure.MEASURE_TITLE ?? "Штука";

                                Console.WriteLine($"✅ Автоматически установлена единица измерения: {item.MeasureSymbol} ({item.MeasureName}) для товара {item.OriginalProduct}");
                                return;
                            }
                        }

                        // Если единица измерения не найдена в словаре, пробуем загрузить её отдельно
                        var singleMeasure = await GetMeasureById(measureId);
                        if (singleMeasure != null)
                        {
                            lock (_measuresLock)
                            {
                                if (!_measuresDictionary.ContainsKey(measureId))
                                {
                                    _measuresDictionary[measureId] = singleMeasure;
                                }
                            }

                            // Обновляем UI
                            await Dispatcher.InvokeAsync(() =>
                            {
                                if (!AllMeasures.Any(m => m.ID == measureId))
                                {
                                    AllMeasures.Add(singleMeasure);
                                }
                            });

                            item.SelectedMeasureId = measureId;
                            item.MeasureSymbol = singleMeasure.SYMBOL_RUS ?? singleMeasure.SYMBOL_INTL ?? "шт.";
                            item.MeasureName = singleMeasure.MEASURE_TITLE ?? "Штука";

                            Console.WriteLine($"✅ Загружена и установлена единица измерения: {item.MeasureSymbol} для товара {item.OriginalProduct}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при автоматической подстановке единицы измерения: {ex.Message}");
            }
        }

        // Метод для получения единицы измерения по ID
        private async Task<BitrixMeasure> GetMeasureById(string measureId)
        {
            try
            {
                using (var httpClient = new HttpClient())
                {
                    httpClient.Timeout = TimeSpan.FromSeconds(30);
                    string apiUrl = $"https://crmnvr.ru/rest/241/5gkwkk4657uafc2x/crm.measure.get.json?id={measureId}";

                    var response = await httpClient.GetAsync(apiUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        var result = JsonConvert.DeserializeObject<BitrixMeasureGetResponse>(json);
                        return result?.Result;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка получения единицы измерения {measureId}: {ex.Message}");
            }
            return null;
        }
        // Обработчики для ComboBox
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
            // Очищаем данные в менеджерах
            PriceDataManager.ClearMatchedProducts();
            ProductSelectionManager.ClearProducts();

            // Переходим на DombPage
            var dombPage = new DombPage();
            NavigationService.Navigate(dombPage);
        }

        private async void AddProduct(object parameter)
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

                        // АВТОМАТИЧЕСКИ ПОДСТАВЛЯЕМ ЕДИНИЦУ ИЗМЕРЕНИЯ
                        await AutoSetMeasureFromBitrixProduct(item, productToSelect);

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
            await EnsureMeasuresLoaded();

            BitrixService bitrixService = new BitrixService();

            if (!ProductItems.Any())
            {
                MessageBox.Show("Список товаров пуст. Добавьте хотя бы один товар для сопоставления.",
                    "Нет товаров", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var matchedProductList = new List<MatchedProduct>();
            var unmatchedProducts = new List<ProductItemViewModel>();

            foreach (var item in ProductItems)
            {
                // Проверяем, выбран ли товар
                bool isSelected = item.SelectedBitrixProduct != null &&
                                  item.SelectedBitrixProduct.ProductId != "0" &&
                                  item.SelectedBitrixProduct.ProductId != "-1" &&
                                  !item.SelectedBitrixProduct.ProductName.Contains("-- Не выбран --") &&
                                  !item.SelectedBitrixProduct.ProductName.Contains("❌ Товар не найден");

                if (!isSelected)
                {
                    unmatchedProducts.Add(item);
                    continue;
                }

                if (!int.TryParse(item.SelectedBitrixProduct?.ProductId, out int bitrixProductId) || bitrixProductId <= 0)
                {
                    MessageBox.Show($"Некорректный ID товара для '{item.OriginalProduct}'",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // ВАЖНО: Используем количество и единицу измерения из item
                string measureId = item.SelectedMeasureId;
                string measureSymbol = !string.IsNullOrEmpty(item.MeasureSymbol) ? item.MeasureSymbol : GetMeasureSymbol(measureId);
                string measureName = !string.IsNullOrEmpty(item.MeasureName) ? item.MeasureName : GetMeasureName(measureId);
                decimal quantity = item.Quantity > 0 ? item.Quantity : 1;

                Console.WriteLine($"Сохранение товара: {item.OriginalProduct}");
                Console.WriteLine($"  Quantity: {quantity}");
                Console.WriteLine($"  MeasureId: {measureId}");
                Console.WriteLine($"  MeasureSymbol: {measureSymbol}");
                Console.WriteLine($"  MeasureName: {measureName}");

                var matchedProduct = new MatchedProduct
                {
                    BitrixProductId = bitrixProductId,
                    OriginalProductName = item.OriginalProduct,
                    BitrixProductName = item.SelectedBitrixProduct.ProductName,
                    BitrixPrice = item.SelectedBitrixProduct.Price,
                    CustomPrice = item.SelectedBitrixProduct.Price,
                    Quantity = item.Quantity,
                    Unit = item.MeasureSymbol,
                    UnitFullName = item.MeasureName,
                    VAT = SettingsHelper.GetVATAsString(),
                    ProductQuantity = item.Quantity,
                    MeasureId = item.SelectedMeasureId,
                    MeasureSymbol = item.MeasureSymbol,
                    MeasureName = item.MeasureName,
                    Measure = item.SelectedMeasureId,
                    Description = item.Description  // ДОБАВЬТЕ ЭТУ СТРОКУ
                };
                matchedProductList.Add(matchedProduct);
            }

            // Обработка несопоставленных товаров (оставляем без изменений)
            if (unmatchedProducts.Any())
            {
                var result = MessageBox.Show($"Найдено {unmatchedProducts.Count} неподобранных товаров:\n\n" +
                                            string.Join("\n", unmatchedProducts.Select(p => $"• {p.OriginalProduct}")) +
                                            "\n\nХотите создать их в Битрикс24 автоматически?",
                                            "Создание товаров",
                                            MessageBoxButton.YesNo,
                                            MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        foreach (var item in unmatchedProducts)
                        {
                            int createdProductId = await bitrixService.CreateProductAsync(item.OriginalProduct);

                            if (createdProductId > 0)
                            {
                                var matchedProduct = new MatchedProduct
                                {
                                    BitrixProductId = createdProductId,
                                    OriginalProductName = item.OriginalProduct,
                                    BitrixProductName = item.OriginalProduct,
                                    BitrixPrice = 0,
                                    CustomPrice = 0,
                                    Quantity = item.Quantity > 0 ? item.Quantity : 1,
                                    Unit = item.MeasureSymbol ?? "шт.",
                                    UnitFullName = item.MeasureName ?? "Штука",
                                    VAT = SettingsHelper.GetVATAsString(),
                                    ProductQuantity = item.Quantity > 0 ? item.Quantity : 1,
                                    MeasureId = item.SelectedMeasureId,
                                    MeasureSymbol = item.MeasureSymbol ?? "шт.",
                                    MeasureName = item.MeasureName ?? "Штука",
                                    Measure = item.SelectedMeasureId
                                };
                                matchedProductList.Add(matchedProduct);
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

            if (!matchedProductList.Any())
            {
                MessageBox.Show("Не удалось сформировать список товаров для обработки.",
                              "Ошибка",
                              MessageBoxButton.OK,
                              MessageBoxImage.Error);
                return;
            }

            // СОХРАНЯЕМ В МЕНЕДЖЕР
            PriceDataManager.SetMatchedProducts(matchedProductList);

            // ПЕРЕДАЕМ СПИСОК В КОНСТРУКТОР
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
        private void QuantityTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            foreach (char c in e.Text)
            {
                if (!char.IsDigit(c) && c != ',' && c != '.')
                {
                    e.Handled = true;
                    return;
                }
            }
        }

        private void QuantityTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            if (string.IsNullOrWhiteSpace(textBox.Text))
            {
                textBox.Text = "1";
                return;
            }

            if (decimal.TryParse(textBox.Text, out var value))
            {
                if (value <= 0)
                    value = 1;
                textBox.Text = value.ToString("0.##");
            }
            else
            {
                textBox.Text = "1";
            }
        }

        private async void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (comboBox?.DataContext is ProductItemViewModel item)
            {
                // При ручном выборе товара из ComboBox
                if (item.SelectedBitrixProduct != null &&
                    item.SelectedBitrixProduct.ProductId != "0" &&
                    item.SelectedBitrixProduct.ProductId != "-1")
                {
                    // Автоматически подставляем единицу измерения
                    await AutoSetMeasureFromBitrixProduct(item, item.SelectedBitrixProduct);
                }
            }
        }



        // Таймер для debounce поиска (чтобы не искать после каждой буквы)
        private readonly Dictionary<ProductItemViewModel, System.Timers.Timer> _searchTimers =
            new Dictionary<ProductItemViewModel, System.Timers.Timer>();
        private readonly object _timersLock = new object();

        // Обработчик изменения текста в TextBox таблицы
        private async void ProductTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox?.DataContext is ProductItemViewModel item)
            {
                // Отменяем предыдущий таймер для этого элемента
                lock (_timersLock)
                {
                    if (_searchTimers.TryGetValue(item, out var existingTimer))
                    {
                        existingTimer.Stop();
                        existingTimer.Dispose();
                        _searchTimers.Remove(item);
                    }
                }

                // Если текст пустой - очищаем результаты
                if (string.IsNullOrWhiteSpace(item.OriginalProduct))
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        item.BitrixProducts.Clear();
                        item.SelectedBitrixProduct = null;
                    });
                    return;
                }

                // Сбрасываем выбранный товар при изменении текста
                item.SelectedBitrixProduct = null;

                // Создаем новый таймер с задержкой 800 мс (debounce)
                var timer = new System.Timers.Timer(800);
                timer.AutoReset = false;

                timer.Elapsed += async (s, args) =>
                {
                    try
                    {
                        // Запускаем поиск
                        await SmartSearchProductsWithProgressAsync(item, item.OriginalProduct);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Ошибка при поиске: {ex.Message}");
                    }
                    finally
                    {
                        // Удаляем таймер после завершения
                        lock (_timersLock)
                        {
                            if (_searchTimers.ContainsKey(item))
                            {
                                _searchTimers.Remove(item);
                            }
                        }
                    }
                };

                lock (_timersLock)
                {
                    _searchTimers[item] = timer;
                }

                timer.Start();
            }
        }

        // Не забудьте очистить таймеры при удалении элемента
        private void RemoveProductItem(object parameter)
        {
            if (parameter is ProductItemViewModel item)
            {
                // Останавливаем и удаляем таймер для этого элемента
                lock (_timersLock)
                {
                    if (_searchTimers.TryGetValue(item, out var timer))
                    {
                        timer.Stop();
                        timer.Dispose();
                        _searchTimers.Remove(item);
                    }
                }

                var result = MessageBox.Show($"Удалить товар '{item.OriginalProduct}'?",
                                           "Подтверждение удаления",
                                           MessageBoxButton.YesNo,
                                           MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    ProductItems.Remove(item);

                    // 👇 ДОБАВЬТЕ ЭТУ СТРОЧКУ
                    UpdateItemsIndexes();

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

        // Также очищаем таймеры при очистке всех товаров
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
                // Останавливаем все таймеры
                lock (_timersLock)
                {
                    foreach (var timer in _searchTimers.Values)
                    {
                        timer.Stop();
                        timer.Dispose();
                    }
                    _searchTimers.Clear();
                }

                ProductItems.Clear();
                UpdateItemsIndexes();
                ProductSelectionManager.ClearProducts();
                _searchCache.Clear();

                MessageBox.Show("Все товары удалены",
                              "Готово",
                              MessageBoxButton.OK,
                              MessageBoxImage.Information);
            }
        }

        private void dataGridProducts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }

    #region Вспомогательные классы

    public class ProductItemViewModel : INotifyPropertyChanged
    {
        private int _index;
        private string _originalProduct;
        private Data.ScharedData.BitrixProductViewModel _selectedBitrixProduct;
        private decimal _quantity = 1;
        private string _selectedMeasureId;
        private string _measureSymbol;
        private string _measureName;
        private string _description;

        public string Description
        {
            get => _description;
            set
            {
                _description = value;
                OnPropertyChanged();
            }
        }
        public int Index
        {
            get => _index;
            set
            {
                _index = value;
                OnPropertyChanged();
            }
        }

        public string OriginalProduct
        {
            get => _originalProduct;
            set
            {
                _originalProduct = value;
                OnPropertyChanged();
            }
        }

        public decimal Quantity
        {
            get => _quantity;
            set
            {
                _quantity = value;
                OnPropertyChanged();
            }
        }

        public string SelectedMeasureId
        {
            get => _selectedMeasureId;
            set
            {
                _selectedMeasureId = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(MeasureSymbol));
                OnPropertyChanged(nameof(MeasureName));
            }
        }

        public string MeasureSymbol
        {
            get => _measureSymbol;
            set
            {
                _measureSymbol = value;
                OnPropertyChanged();
            }
        }

        public string MeasureName
        {
            get => _measureName;
            set
            {
                _measureName = value;
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

    // Класс для ответа от Bitrix при получении единицы измерения
    public class BitrixMeasureGetResponse
    {
        [JsonProperty("result")]
        public BitrixMeasure Result { get; set; }
    }

}