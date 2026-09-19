using ManagerApp.Data.GetInfo;
using ManagerApp.Data.ScharedData;
using ManagerApp.Data.StructureList;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using static ManagerApp.Pages.ComparisonProduct;

namespace ManagerApp.Pages
{


    public partial class SearchBitrixProduct : Window
    {
        public BitrixProductViewModel SelectedProduct { get; private set; }
        private ObservableCollection<BitrixProductViewModel> _allProducts;
        private ObservableCollection<BitrixProductViewModel> _currentPageProducts;
        private List<BitrixProductViewModel> _filteredProducts;
        private bool _isLoading = false;
        private int _currentPage = 1;
        private int _pageSize = 50;
        private int _totalPages = 1;
        private string _currentSearchText = "";
        private List<string> _selectedCategories = new List<string>();
        private DispatcherTimer _searchTimer;

        public SearchBitrixProduct()
        {
            InitializeComponent();
            InitializeSearchTimer();

            _allProducts = new ObservableCollection<BitrixProductViewModel>();
            _currentPageProducts = new ObservableCollection<BitrixProductViewModel>();
            _filteredProducts = new List<BitrixProductViewModel>();
            dgSearchResults.ItemsSource = _currentPageProducts;

            Loaded += SearchBitrixProduct_Loaded;
            dgSearchResults.SelectionChanged += DgSearchResults_SelectionChanged;
        }

        private void InitializeSearchTimer()
        {
            _searchTimer = new DispatcherTimer();
            _searchTimer.Interval = TimeSpan.FromMilliseconds(500);
            _searchTimer.Tick += (s, e) =>
            {
                _searchTimer.Stop();
                SearchProducts();
            };
        }

        private async void SearchBitrixProduct_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadProductsFromBitrix();
        }

     
        private async Task LoadProductsFromBitrix()
        {
            try
            {
                _isLoading = true;
                txtStatus.Text = "Загрузка товаров...";

                // ИСПОЛЬЗУЕМ НОВЫЙ МЕТОД: получаем все товары с нижними разделами
                var productsWithSections = await BitrixCache.GetAllProductsWithLowerSections();

                if (productsWithSections == null || productsWithSections.Count == 0)
                {
                    // Если новый метод не работает, пробуем старый
                    var products = await BitrixCache.GetAllProductsWithCategories();

                    if (products == null || products.Count == 0)
                    {
                        txtStatus.Text = "Не удалось загрузить товары";
                        return;
                    }

                    // Конвертируем из старого формата
                    ConvertFromOldFormat(products);
                }
                else
                {
                    // Конвертируем из нового формата
                    ConvertFromNewFormat(productsWithSections);
                }

                // Создаем чекбоксы для категорий (нижних разделов)
                CreateCategoryFilters();

                // Показываем все товары по умолчанию
                UpdateFilteredProducts("");
                UpdatePaginationInfo();
                LoadCurrentPage();

                txtStatus.Text = $"Загружено товаров: {_allProducts.Count}";
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Ошибка загрузки: {ex.Message}";
                MessageBox.Show($"Ошибка загрузки товаров: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isLoading = false;
            }
        }

        // Новый метод для конвертации из формата с нижними разделами
        private void ConvertFromNewFormat(List<ProductWithLowerSection> products)
        {
            _allProducts.Clear();

            foreach (var product in products)
            {
                var viewModel = new BitrixProductViewModel
                {
                    ProductId = product.ProductId,
                    ProductName = product.ProductName,
                    // ИСПОЛЬЗУЕМ НИЖНИЙ РАЗДЕЛ КАК КАТЕГОРИЮ
                    CategoryName = product.LowerSectionName ?? "Без категории",
                    LowerSectionName = product.LowerSectionName,
                    FullCategoryPath = product.CategoryPath,
                    Price = product.Price,
                    HasPrice = product.Price > 0,
                    SectionId = product.LowerSectionId,
                    Measure = product.Measure,
                    
                };

                _allProducts.Add(viewModel);
            }
        }

        // Метод для конвертации из старого формата
        private void ConvertFromOldFormat(List<ProductWithCategoryInfo> products)
        {
            _allProducts.Clear();

            foreach (var product in products)
            {
                var viewModel = new BitrixProductViewModel
                {
                    ProductId = product.ProductId,
                    ProductName = product.ProductName,
                    CategoryName = product.CategoryName,
                    LowerSectionName = product.CategoryName, // Дублируем
                    FullCategoryPath = product.CategoryName,
                    Price = product.Price,
                    HasPrice = product.HasPrice,
                    SectionId = product.SectionId,
                    Measure = product.Measure,
                };

                _allProducts.Add(viewModel);
            }
        }
        private void SelectProduct()
        {
            if (dgSearchResults.SelectedItem is BitrixProductViewModel selectedProduct)
            {
                SelectedProduct = selectedProduct;

                // Сохраняем в менеджере
                ProductDataManager.SetSelectedProduct(selectedProduct);

                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("Выберите товар из списка",
                    "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void btnSelectFromGrid_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.DataContext is BitrixProductViewModel product)
            {
                SelectedProduct = product;

                // Сохраняем в менеджере
                ProductDataManager.SetSelectedProduct(product);

                DialogResult = true;
                Close();
            }
        }

        private void CategoryCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;

            if (checkBox?.Tag?.ToString() == "ALL")
            {
                // Выбраны все категории - отмечаем все чекбоксы
                bool isChecked = checkBox.IsChecked == true;
                foreach (var child in spCategories.Children)
                {
                    if (child is CheckBox cb && cb.Tag?.ToString() != "ALL")
                    {
                        cb.IsChecked = isChecked;
                    }
                }
            }
            else
            {
                // Если снимаем галочку с обычного чекбокса - снимаем галочку с "Все категории"
                if (checkBox?.IsChecked == false)
                {
                    foreach (var child in spCategories.Children)
                    {
                        if (child is CheckBox cb && cb.Tag?.ToString() == "ALL")
                        {
                            cb.IsChecked = false;
                            break;
                        }
                    }
                }

                // Проверяем, все ли категории выбраны для чекбокса "Все категории"
                bool allChecked = spCategories.Children
                    .OfType<CheckBox>()
                    .Where(cb => cb.Tag?.ToString() != "ALL")
                    .All(cb => cb.IsChecked == true);

                var allCheckBox = spCategories.Children
                    .OfType<CheckBox>()
                    .FirstOrDefault(cb => cb.Tag?.ToString() == "ALL");

                if (allCheckBox != null)
                {
                    allCheckBox.IsChecked = allChecked;
                }
            }

            // Обновляем выбранные категории
            UpdateSelectedCategories();

            // Применяем фильтры
            if (!_isLoading)
            {
                ApplyFilters();
            }
        }

        private void UpdateSelectedCategories()
        {
            _selectedCategories.Clear();

            foreach (var child in spCategories.Children)
            {
                if (child is CheckBox checkBox &&
                    checkBox.Tag?.ToString() != "ALL" &&
                    checkBox.IsChecked == true)
                {
                    // Теперь фильтруем по LowerSectionName
                    _selectedCategories.Add(checkBox.Tag.ToString());
                }
            }
        }

        private void UpdateFilteredProducts(string searchText)
        {
            _currentSearchText = searchText;

            // Фильтруем по выбранным НИЖНИМ РАЗДЕЛАМ
            List<BitrixProductViewModel> sectionFiltered = _allProducts.ToList();


            foreach (var section in sectionFiltered)
            {
                Console.WriteLine("Единица измерения " +  section.Measure);
            }

            if (_selectedCategories.Count > 0)
            {
                sectionFiltered = _allProducts
                    .Where(product => _selectedCategories.Contains(product.LowerSectionName ?? ""))
                    .ToList();
            }

            // Затем по поисковому запросу
            if (string.IsNullOrWhiteSpace(searchText))
            {
                _filteredProducts = sectionFiltered;
            }
            else
            {
                var searchWords = searchText.ToLower().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                _filteredProducts = sectionFiltered
                    .Where(product => searchWords.All(word =>
                        (product.ProductName?.ToLower() ?? "").Contains(word) ||
                        (product.LowerSectionName?.ToLower() ?? "").Contains(word) ||
                        (product.FullCategoryPath?.ToLower() ?? "").Contains(word)))
                    .ToList();

                
            }

            _currentPage = 1;
            UpdatePaginationInfo();
            LoadCurrentPage();
        }

        private void UpdatePaginationInfo()
        {
            _totalPages = (int)Math.Ceiling((double)_filteredProducts.Count / _pageSize);
            if (_totalPages == 0) _totalPages = 1;

            UpdatePageInfoText();
            UpdatePaginationButtons();
        }

        private void UpdatePageInfoText()
        {
            int startIndex = (_currentPage - 1) * _pageSize + 1;
            int endIndex = Math.Min(_currentPage * _pageSize, _filteredProducts.Count);

            if (_filteredProducts.Count == 0)
            {
                txtStatus.Text = "Товары не найдены";
            }
            else
            {
                txtStatus.Text = $"Показано {startIndex}-{endIndex} из {_filteredProducts.Count} товаров";
            }
        }

        private void UpdatePaginationButtons()
        {
            btnPrevPage.IsEnabled = _currentPage > 1;
            btnNextPage.IsEnabled = _currentPage < _totalPages;
        }

        private void LoadCurrentPage()
        {
            _currentPageProducts.Clear();

            if (_filteredProducts.Count == 0)
            {
                dgSearchResults.ItemsSource = _currentPageProducts;
                return;
            }

            int startIndex = (_currentPage - 1) * _pageSize;
            int endIndex = Math.Min(startIndex + _pageSize, _filteredProducts.Count);

            for (int i = startIndex; i < endIndex; i++)
            {
                _currentPageProducts.Add(_filteredProducts[i]);
            }

            dgSearchResults.ItemsSource = _currentPageProducts;
            UpdatePageInfoText();
        }

        private void btnSearch_Click(object sender, RoutedEventArgs e)
        {
            SearchProducts();
        }

        private void SearchProducts()
        {
            if (_isLoading)
            {
                txtStatus.Text = "Идет загрузка товаров...";
                return;
            }

            string searchText = txtSearch.Text?.Trim();
            UpdateFilteredProducts(searchText);
        }

        private void ApplyFilters()
        {
            SearchProducts();
        }

        private void btnClearSearch_Click(object sender, RoutedEventArgs e)
        {
            txtSearch.Text = "";
            txtSearch.Focus();
            btnClearSearch.Visibility = Visibility.Collapsed;
            SearchProducts();
        }

        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Показываем/скрываем кнопку очистки
            btnClearSearch.Visibility = string.IsNullOrEmpty(txtSearch.Text)
                ? Visibility.Collapsed
                : Visibility.Visible;

            // Запускаем таймер для поиска с задержкой
            _searchTimer.Stop();
            _searchTimer.Start();
        }

        private void txtSearch_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                _searchTimer.Stop();
                SearchProducts();
            }
        }

        private void btnSelect_Click(object sender, RoutedEventArgs e)
        {
            SelectProduct();
        }




        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void DgSearchResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            btnSelect.IsEnabled = dgSearchResults.SelectedItem != null;
        }



        // Пагинация
        private void btnPrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                LoadCurrentPage();
            }
        }

        private void btnNextPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage < _totalPages)
            {
                _currentPage++;
                LoadCurrentPage();
            }
        }


        private void CreateCategoryFilters()
        {
            spCategories.Children.Clear();

            if (_allProducts == null || _allProducts.Count == 0)
                return;

            // Группируем по НИЖНИМ РАЗДЕЛАМ (LowerSectionName)
            var lowerSections = _allProducts
                .Where(p => !string.IsNullOrEmpty(p.LowerSectionName))
                .GroupBy(p => p.LowerSectionName)
                .Select(g => new
                {
                    Name = g.Key,
                    Count = g.Count(),
                    // Дополнительно: полный путь для tooltip
                    FullPath = g.FirstOrDefault()?.FullCategoryPath ?? g.Key
                })
                .OrderBy(c => c.Name)
                .ToList();

            if (lowerSections.Count == 0)
                return;

            // Чекбокс "Все разделы"
            var selectAllCheckBox = new CheckBox
            {
                Content = $"Все разделы ({lowerSections.Sum(c => c.Count)})",
                Margin = new Thickness(0, 0, 20, 0),
                FontSize = 11,
                IsChecked = true,
                Tag = "ALL",
                ToolTip = "Выбрать все нижние разделы"
            };

            selectAllCheckBox.Checked += CategoryCheckBox_CheckedChanged;
            selectAllCheckBox.Unchecked += CategoryCheckBox_CheckedChanged;

            spCategories.Children.Add(selectAllCheckBox);

            // Чекбоксы для каждого нижнего раздела
            foreach (var section in lowerSections)
            {
                var checkBox = new CheckBox
                {
                    Content = $"{section.Name} ({section.Count})",
                    Margin = new Thickness(0, 0, 20, 0),
                    FontSize = 11,
                    IsChecked = true,
                    Tag = section.Name,
                    ToolTip = section.FullPath // Показываем полный путь при наведении
                };

                checkBox.Checked += CategoryCheckBox_CheckedChanged;
                checkBox.Unchecked += CategoryCheckBox_CheckedChanged;

                spCategories.Children.Add(checkBox);
            }
        }
    }
}