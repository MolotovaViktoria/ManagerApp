using ManagerApp.Classes.Normalizer;
using ManagerApp.Classes.Read;
using ManagerApp.Data.GetInfo;
using ManagerApp.Data.StructureList;
using Org.BouncyCastle.Asn1.Pkcs;
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


        private async void ShowLoadedData()
        {
            // Данные уже в кеше, просто отображаем
            var products = await BitrixCache.GetProductsByCategory(691);
            txtOutput.Text = $"Готово! Загружено {products.Count} товаров";
        }


        private void btnLoadRequest_Click(object sender, RoutedEventArgs e)
        {
            // Создаем диалог выбора файла
            var openFileDialog = new Microsoft.Win32.OpenFileDialog();

            // Устанавливаем фильтры для форматов файлов
            openFileDialog.Filter = "Документы Word (*.docx, *.dotx, *.docm, *.dotm)|*.docx;*.dotx;*.docm;*.dotm|" +
                                   "PDF файлы (*.pdf)|*.pdf|" +
                                   "Excel файлы (*.xlsx, *.xls, *.xlsm, *.xlsb, *.csv)|*.xlsx;*.xls;*.xlsm;*.xlsb;*.csv|" +
                                   "Все файлы (*.*)|*.*";

            openFileDialog.FilterIndex = 1; // Устанавливаем фильтр по умолчанию
            openFileDialog.Multiselect = false; // Разрешаем выбор только одного файла

            // Показываем диалог и проверяем, был ли выбран файл
            if (openFileDialog.ShowDialog() == true)
            {
                string selectedFilePath = openFileDialog.FileName;

                // Получаем расширение файла для проверки типа
                string fileExtension = System.IO.Path.GetExtension(selectedFilePath).ToLower();

                Classes.Read.ReadRequst readRequst = new Classes.Read.ReadRequst();

                //txtOutput.Text = readRequst.ReadFileAll(selectedFilePath);


                string requestText = readRequst.ReadFileAll(selectedFilePath);
                var parser = new ParserRequst();
                List<ProductRequst> products = parser.ParseProducts(requestText);

                txtOutput.Text = "всего товаров " + products.Count + "\n";
                foreach (var product in products)
                {
                    txtOutput.Text += "\n ИМЯ: " + product.Name + " КОЛИЧЕСТВО " + product.Quantity;
                }
            }
        }

        private async void btnTestBitrix_Click(object sender, RoutedEventArgs e)
        {
            var categories = await BitrixCache.GetProductsByCategory(691);

            string text = $"Товаров: {categories.Count}\n";
            foreach (var category in categories.Take(10))
            {
                text += $"\n{category.Name} - {category.Price} рублей";
            }

            txtOutput.Text = text;
        }
    }
}
