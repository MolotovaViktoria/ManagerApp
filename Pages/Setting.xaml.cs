using ManagerApp.Classes.Setting;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ManagerApp.Pages
{
    public partial class Setting : Page, INotifyPropertyChanged
    {
        private const string SettingsFileName = "settings.txt";
        private const string DefaultVAT = "20";

        private string _vat = DefaultVAT;
        private string _webhook = WebhookManager.GetWebhookFromFile();

        public string VAT
        {
            get { return _vat; }
            set
            {
                if (_vat != value)
                {
                    _vat = value;
                    OnPropertyChanged(nameof(VAT));
                }
            }
        }

        public string Webhook
        {
            get { return _webhook; }
            set
            {
                if (_webhook != value)
                {
                    _webhook = value;
                    OnPropertyChanged(nameof(Webhook));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public Setting()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadVATFromFile();
            // Вебхук уже загружен в конструкторе через WebhookManager
        }

        private void LoadVATFromFile()
        {
            try
            {
                if (File.Exists(SettingsFileName))
                {
                    var lines = File.ReadAllLines(SettingsFileName);
                    foreach (var line in lines)
                    {
                        if (line.StartsWith("VAT="))
                        {
                            var value = line.Substring(4);
                            if (!string.IsNullOrEmpty(value))
                            {
                                _vat = value;
                                OnPropertyChanged(nameof(VAT));
                            }
                        }
                    }
                }
                else
                {
                    // Создаем файл с настройками по умолчанию
                    SaveVATToFile();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки настроек НДС: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                VAT = DefaultVAT;
            }
        }

        private void SaveVATToFile()
        {
            try
            {
                var lines = new List<string> { $"VAT={VAT}" };
                File.WriteAllLines(SettingsFileName, lines);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения настроек НДС: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveWebhookToFile()
        {
            try
            {
                WebhookManager.SaveWebhookToFile(Webhook);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения вебхука: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Статический метод для получения НДС из любого места в коде
        public static decimal GetVAT()
        {
            try
            {
                if (File.Exists(SettingsFileName))
                {
                    var lines = File.ReadAllLines(SettingsFileName);
                    foreach (var line in lines)
                    {
                        if (line.StartsWith("VAT="))
                        {
                            var value = line.Substring(4);
                            if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var vat))
                            {
                                return vat / 100m; // Возвращаем как коэффициент (0.20 для 20%)
                            }
                        }
                    }
                }
            }
            catch
            {
                // В случае ошибки возвращаем значение по умолчанию
            }

            return 0.20m; // 20% по умолчанию
        }

        private void txtVAT_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Разрешаем только цифры и запятую/точку
            foreach (char c in e.Text)
            {
                if (!char.IsDigit(c) && c != ',' && c != '.')
                {
                    e.Handled = true;
                    return;
                }
            }

            // Проверяем, что вводится корректное число
            var textBox = sender as TextBox;
            var newText = textBox.Text.Insert(textBox.SelectionStart, e.Text);

            if (decimal.TryParse(newText, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
            {
                // Ограничиваем максимальное значение
                if (value > 100)
                {
                    e.Handled = true;
                }
            }
            else
            {
                e.Handled = true;
            }
        }

        private void txtVAT_LostFocus(object sender, RoutedEventArgs e)
        {
            // При потере фокуса форматируем значение
            if (string.IsNullOrWhiteSpace(txtVAT.Text))
            {
                txtVAT.Text = DefaultVAT;
                return;
            }

            if (decimal.TryParse(txtVAT.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
            {
                // Ограничиваем диапазон значений
                if (value < 0)
                    value = 0;
                else if (value > 100)
                    value = 100;

                txtVAT.Text = value.ToString("0.##");
            }
            else
            {
                txtVAT.Text = DefaultVAT;
            }
        }

        private void txtWebhook_LostFocus(object sender, RoutedEventArgs e)
        {
            // Проверяем и очищаем URL при потере фокуса
            if (!string.IsNullOrWhiteSpace(txtWebhook.Text))
            {
                // Удаляем пробелы в начале и конце
                txtWebhook.Text = txtWebhook.Text.Trim();


            }
        }

        private void btnCheckInternet_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MessageBox.Show("Проверка подключения к интернету...\n\n(Эта функция находится в разработке)",
                    "Проверка", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnCheckApi_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MessageBox.Show("Проверка API...\n\n(Эта функция находится в разработке)",
                    "Проверка", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void btnCheckBitrix_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Webhook))
                {
                    MessageBox.Show("Вебхук Bitrix24 не указан. Пожалуйста, укажите URL вебхука в настройках.",
                        "Вебхук не указан",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

            
                // Здесь можно добавить реальную проверку подключения
                // bool isConnected = await WebhookManager.TestConnectionAsync();
                // MessageBox.Show(isConnected ? "Подключение успешно!" : "Ошибка подключения", 
                //                 "Результат проверки", MessageBoxButton.OK, 
                //                 isConnected ? MessageBoxImage.Information : MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            // Сохраняем настройки НДС
            SaveVATToFile();

            // Сохраняем вебхук в отдельный файл
            SaveWebhookToFile();

            MessageBox.Show("Настройки успешно сохранены!", "Успех",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService.CanGoBack)
            {
                NavigationService.GoBack();
            }
        }
    }
}