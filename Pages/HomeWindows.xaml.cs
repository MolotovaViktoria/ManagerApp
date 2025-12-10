using System;
using System.Configuration;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ManagerApp.Pages
{
    public partial class HomeWindows : Window
    {
        private bool _isMenuExpanded = false;

        public HomeWindows()
        {
            InitializeComponent();

            // Загружаем главную страницу при запуске
            LoadMainPage();
        }

        private void btnMenu_Click(object sender, RoutedEventArgs e)
        {
            _isMenuExpanded = !_isMenuExpanded;

            if (_isMenuExpanded)
            {
                MenuColumn.Width = new GridLength(200);
                LeftMenuPanel.MinWidth = 200;

                // Показываем текст
                txtHome.Visibility = Visibility.Visible;
                txtHistory.Visibility = Visibility.Visible;
                txtSettings.Visibility = Visibility.Visible;
            }
            else
            {
                MenuColumn.Width = new GridLength(60);
                LeftMenuPanel.MinWidth = 60;

                // Скрываем текст
                txtHome.Visibility = Visibility.Collapsed;
                txtHistory.Visibility = Visibility.Collapsed;
                txtSettings.Visibility = Visibility.Collapsed;
            }
        }

        // Метод для загрузки главной страницы
        private void LoadMainPage()
        {
            try
            {
                DombPage mainPage = new DombPage();
                MainFrame.Navigate(mainPage);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки главной страницы: {ex.Message}");
            }
        }
        private void btnHome_Click(object sender, RoutedEventArgs e)
        {
            LoadMainPage();
        }

        public void OpenExcelFilePage(string filePath)
        {
            try
            {
                // Проверяем расширение файла
                string extension = System.IO.Path.GetExtension(filePath)?.ToLower();

                // Список поддерживаемых расширений Excel
                string[] excelExtensions = { ".xlsx", ".xls", ".xlsm", ".xlsb", ".csv" };

                if (!string.IsNullOrEmpty(extension) && excelExtensions.Contains(extension))
                {
                    ExcelFile excelFilePage = new ExcelFile();
                    excelFilePage.LoadFile(filePath);
                    MainFrame.Navigate(excelFilePage);
                }
                else
                {
                    WorldPdfFile worldPdfFile = new WorldPdfFile();
                    worldPdfFile.LoadFile(filePath);
                    MainFrame.Navigate(worldPdfFile);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка открытия файла: {ex.Message}");
            }
        }
        // Метод для навигации на любую страницу
        public void NavigateToPage(Page page)
        {
            MainFrame.Navigate(page);
        }

        // Метод для открытия страницы CheakFile
        public void OpenCheakFilePage(string filePath)
        {
            try
            {
                // Создаем страницу CheakFile и передаем путь к файлу
                DombPage cheakFilePage = new DombPage();
                MainFrame.Navigate(cheakFilePage);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка открытия страницы проверки: {ex.Message}");
            }
        }




        private void btnHistory_Click(object sender, RoutedEventArgs e)
        {
            // Загружаем страницу истории
            //HistoryPage historyPage = new HistoryPage();
            //MainFrame.Navigate(historyPage);
        }

        private void btnSettings_Click(object sender, RoutedEventArgs e)
        {
            // Загружаем страницу настроек
            //SettingsPage settingsPage = new SettingsPage();
            //MainFrame.Navigate(settingsPage);
        }





       
    }
}