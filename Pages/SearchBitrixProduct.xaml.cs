using ManagerApp.Data.GetInfo;
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

        public event Action<BitrixProductViewModel> ProductSelected;

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
                txtStatus.Text = "Загрузка товаров из Bitrix...";

                // Используем BitrixCache для загрузки товаров
                var products = await BitrixCache.GetAllProductsWithCategories();

                if (products == null || products.Count == 0)
                {
                    txtStatus.Text = "Не удалось загрузить товары";
                    return;
                }

                // Очищаем коллекции
                _allProducts.Clear();

                // Конвертируем в BitrixProductViewModel
                foreach (var product in products)
                {
                    var viewModel = new BitrixProductViewModel
                    {
                        ProductId = product.ProductId,
                        ProductName = product.ProductName,
                        CategoryName = product.CategoryName,
                        Price = product.Price,
                        HasPrice = product.HasPrice,
                        SectionId = product.SectionId
                    };

                    _allProducts.Add(viewModel);
                }

                // Создаем чекбоксы для категорий
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
                    _selectedCategories.Add(checkBox.Tag.ToString());
                }
            }
        }

        private void UpdateFilteredProducts(string searchText)
        {
            _currentSearchText = searchText;

            // Сначала фильтруем по категориям
            List<BitrixProductViewModel> categoryFiltered = _allProducts.ToList();

            if (_selectedCategories.Count > 0)
            {
                categoryFiltered = _allProducts
                    .Where(product => _selectedCategories.Contains(product.CategoryName))
                    .ToList();
            }

            // Затем по поисковому запросу
            if (string.IsNullOrWhiteSpace(searchText))
            {
                _filteredProducts = categoryFiltered;
            }
            else
            {
                var searchWords = searchText.ToLower().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                _filteredProducts = categoryFiltered
                    .Where(product => searchWords.All(word =>
                        product.ProductName.ToLower().Contains(word) ||
                        product.CategoryName.ToLower().Contains(word)))
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

        private void SelectProduct()
        {
            if (dgSearchResults.SelectedItem is BitrixProductViewModel selectedProduct)
            {
                SelectedProduct = selectedProduct;
                ProductSelected?.Invoke(selectedProduct);
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("Выберите товар из списка",
                    "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
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

        private void btnSelectFromGrid_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.DataContext is BitrixProductViewModel product)
            {
                SelectedProduct = product;
                ProductSelected?.Invoke(product);
                DialogResult = true;
                Close();
            }
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

            // Получаем уникальные категории из загруженных товаров
            var categories = _allProducts
                .Where(p => !string.IsNullOrEmpty(p.CategoryName))
                .GroupBy(p => p.CategoryName)
                .Select(g => new
                {
                    Name = g.Key,
                    Count = g.Count()
                })
                .OrderBy(c => c.Name)
                .ToList();

            if (categories.Count == 0)
                return;

            // Кнопка "Выбрать все"
            var selectAllCheckBox = new CheckBox
            {
                Content = $"Все категории ({categories.Sum(c => c.Count)})",
                Margin = new Thickness(0, 0, 20, 0),
                FontSize = 11,
                IsChecked = true,
                Tag = "ALL",
                ToolTip = "Выбрать все категории"
            };

            selectAllCheckBox.Checked += CategoryCheckBox_CheckedChanged;
            selectAllCheckBox.Unchecked += CategoryCheckBox_CheckedChanged;

            spCategories.Children.Add(selectAllCheckBox);

            // Создаем чекбоксы для каждой категории с количеством товаров
            foreach (var category in categories)
            {
                var checkBox = new CheckBox
                {
                    Content = $"{category.Name} ({category.Count})",
                    Margin = new Thickness(0, 0, 20, 0),
                    FontSize = 11,
                    IsChecked = true,
                    Tag = category.Name,
                    ToolTip = $"{category.Name} - {category.Count} товаров"
                };

                checkBox.Checked += CategoryCheckBox_CheckedChanged;
                checkBox.Unchecked += CategoryCheckBox_CheckedChanged;

                spCategories.Children.Add(checkBox);
            }
        }
    }
}