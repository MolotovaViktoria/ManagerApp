using ManagerApp.Classes.ModelsStudy;
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
        private string _originalFileText; // ← ДОБАВЬТЕ ЭТУ ПЕРЕМЕННУЮ

        public MainWindows()
        {
            InitializeComponent();
            _bitrixService = new BitrixService();
        }

        private void btnLoadRequest_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.Filter = "Документы Word (*.docx, *.dotx, *.docm, *.dotm)|*.docx;*.dotx;*.docm;*.dotm|" +
                                   "PDF файлы (*.pdf)|*.pdf|" +
                                   "Excel файлы (*.xlsx, *.xls, *.xlsm, *.xlsb, *.csv)|*.xlsx;*.xls;*.xlsm;*.xlsb;*.csv|" +
                                   "Все файлы (*.*)|*.*";

            if (openFileDialog.ShowDialog() == true)
            {
                string selectedFilePath = openFileDialog.FileName;
                Classes.Read.ReadRequst readRequst = new Classes.Read.ReadRequst();

                // СОХРАНЯЕМ исходный текст в переменную
                _originalFileText = readRequst.ReadFileAll(selectedFilePath);

                // Показываем пользователю только часть текста или количество товаров
                txtOutput.Text = $"Файл загружен! Текст содержит {_originalFileText.Length} символов\n";

                // Можно показать первые 500 символов для preview
                txtOutput.Text += "Текст файла:\n" + _originalFileText;
            }
        }

        private void StartModelTraining()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_originalFileText))
                {
                    MessageBox.Show("Сначала загрузите файл с данными!",
                                  "Нет данных",
                                  MessageBoxButton.OK,
                                  MessageBoxImage.Warning);
                    return;
                }

                // Используем сохраненный исходный текст
                var parser = new MLParserRequst();
                var allPotentialProducts = parser.ExtractAllPotentialProducts(_originalFileText);

                // Берем только названия для обучения
                var textsToClassify = new List<string>();

                foreach (var product in allPotentialProducts)
                {
                    if (!string.IsNullOrWhiteSpace(product.Name))
                    {
                        textsToClassify.Add(product.Name);
                    }
                }

                // Добавляем также явные не-товары для баланса
                textsToClassify.AddRange(new List<string>
            {
                "Российская Федерация РФ Россия",
                "Нет в наличии",
                "Страна происхождения",
                "Максимальная цена поставщиков",
                "ООО ТД МК НВР",
                "Ценовой запрос №829183",
                "ГАЗПРОМ ТРАНСГАЗ СУРГУТ",
                "Ответ на ценовой запрос",
                "Наименование позиции площадки",
                "Комментарий заказчика"
            });

                if (textsToClassify.Count == 0)
                {
                    MessageBox.Show("Не найдено текстов для обучения.",
                                  "Нет данных",
                                  MessageBoxButton.OK,
                                  MessageBoxImage.Warning);
                    return;
                }

                // Запускаем окно обучения
                var trainWindow = new TrainModelWindow(textsToClassify);
                trainWindow.Owner = this;
                trainWindow.ShowDialog();

                MessageBox.Show($"Обучение завершено! Обработано {textsToClassify.Count} текстов.",
                              "Готово",
                              MessageBoxButton.OK,
                              MessageBoxImage.Information);

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при запуске обучения: {ex.Message}",
                              "Ошибка",
                              MessageBoxButton.OK,
                              MessageBoxImage.Error);
            }
        }

        private void TrainModelButton_Click(object sender, RoutedEventArgs e)
        {
            StartModelTraining();
        }

        // Дополнительный метод для просмотра что именно будет обучаться
        private void btnPreviewTrainingData_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_originalFileText))
            {
                MessageBox.Show("Сначала загрузите файл!", "Нет данных", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var parser = new MLParserRequst();
            var allPotentialProducts = parser.ExtractAllPotentialProducts(_originalFileText);

            string previewText = $"Найдено потенциальных товаров: {allPotentialProducts.Count}\n\n";

            foreach (var product in allPotentialProducts.Take(20)) // Покажем первые 20
            {
                previewText += $"📦 {product.Name} - {product.Quantity} шт.\n";
            }

            if (allPotentialProducts.Count > 20)
            {
                previewText += $"\n... и еще {allPotentialProducts.Count - 20} товаров";
            }

            txtOutput.Text = previewText;
        }
    }
}