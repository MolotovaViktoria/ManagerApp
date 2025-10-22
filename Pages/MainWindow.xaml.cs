using ManagerApp.Data.GetInfo;
using ManagerApp.Data.StructureList;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ManagerApp.Pages
{
    /// <summary>
    /// Логика взаимодействия для MainWindows.xaml
    /// </summary>
    public partial class MainWindows : Window
    {
        private BitrixService _bitrixService;

        public MainWindows()
        {
            InitializeComponent();
            _bitrixService = new BitrixService();
        }

        private async void LoadProducts_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                // Загружаем категории и товары
                var categories = await _bitrixService.GetСategories();
                categories = categories.Where(x => x.SelectionId != null).ToList();
                var products = await _bitrixService.GetProducts();

                // Показываем в ListBox
                categoriesListBox.ItemsSource = categories;
                productsListBox.ItemsSource = products;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }
    }
}
