using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.ComponentModel;

namespace ManagerApp.Pages
{
    public partial class Setting : Page, INotifyPropertyChanged
    {
        private const string SettingsFileName = "settings.txt";
        private const string DefaultVAT = "20";

        private string _vat = DefaultVAT;

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
                                // Используем поле напрямую, а не свойство, чтобы не вызывать сохранение
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
                MessageBox.Show($"Ошибка загрузки настроек: {ex.Message}", "Ошибка",
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
                MessageBox.Show($"Ошибка сохранения настроек: {ex.Message}", "Ошибка",
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

        private void btnCheckInternet_Click(object sender, RoutedEventArgs e)
        {
            // Заглушка для проверки интернета
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
            // Заглушка для проверки API
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

        private void btnCheckBitrix_Click(object sender, RoutedEventArgs e)
        {
            // Заглушка для проверки Bitrix
            try
            {
                MessageBox.Show("Проверка подключения к Bitrix...\n\n(Эта функция находится в разработке)",
                    "Проверка", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            // Сохраняем настройки
            SaveVATToFile();
            MessageBox.Show("Настройки успешно сохранены!", "Успех",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            // Возвращаемся назад
            if (NavigationService.CanGoBack)
            {
                NavigationService.GoBack();
            }
        }
    }
}