using ManagerApp.Classes.Setting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
        private ObservableCollection<BitrixUser> _employees = new ObservableCollection<BitrixUser>();
        private BitrixUser _selectedEmployee;

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

        public ObservableCollection<BitrixUser> Employees
        {
            get { return _employees; }
            set
            {
                _employees = value;
                OnPropertyChanged(nameof(Employees));
            }
        }

        public BitrixUser SelectedEmployee
        {
            get { return _selectedEmployee; }
            set
            {
                if (_selectedEmployee != value)
                {
                    _selectedEmployee = value;
                    OnPropertyChanged(nameof(SelectedEmployee));

                    // Сохраняем ID выбранного сотрудника
                    if (value != null)
                    {
                        SaveEmployeeId(value.id);
                    }
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
            LoadEmployees();
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

        private void LoadEmployees()
        {
            try
            {
                // Загружаем статичный список сотрудников
                var employees = StaticEmployeeProvider.GetAllEmployees();

                Employees.Clear();
                foreach (var employee in employees)
                {
                    Employees.Add(employee);
                }

                // Загружаем сохраненного сотрудника
                var savedEmployeeId = GetSavedEmployeeId();
                if (savedEmployeeId > 0)
                {
                    var savedEmployee = Employees.FirstOrDefault(e => e.id == savedEmployeeId);
                    if (savedEmployee != null)
                    {
                        SelectedEmployee = savedEmployee;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки сотрудников: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SaveEmployeeId(int employeeId)
        {
            try
            {
                IDSetting.SaveSelectedEmployeeId(employeeId);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения ID сотрудника: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private int GetSavedEmployeeId()
        {
            return IDSetting.GetSelectedEmployeeId();
        }

        // Статический метод для получения ID из любого места в коде
        public static int GetSelectedEmployeeId()
        {
            return IDSetting.GetSelectedEmployeeId();
        }

        // Статический метод для получения данных сотрудника по ID
        public static BitrixUser GetSelectedEmployeeData()
        {
            var employeeId = IDSetting.GetSelectedEmployeeId();
            if (employeeId > 0)
            {
                return StaticEmployeeProvider.GetEmployeeById(employeeId);
            }
            return null;
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
                                return vat / 100m;
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
            foreach (char c in e.Text)
            {
                if (!char.IsDigit(c) && c != ',' && c != '.')
                {
                    e.Handled = true;
                    return;
                }
            }

            var textBox = sender as TextBox;
            var newText = textBox.Text.Insert(textBox.SelectionStart, e.Text);

            if (decimal.TryParse(newText, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
            {
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
            if (string.IsNullOrWhiteSpace(txtVAT.Text))
            {
                txtVAT.Text = DefaultVAT;
                return;
            }

            if (decimal.TryParse(txtVAT.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
            {
                if (value < 0)
                    value = 0;
                else if (value > 100)
                    value = 100;

                txtVAT.Text = value.ToString("0.##");
                SaveVATToFile();
            }
            else
            {
                txtVAT.Text = DefaultVAT;
            }
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            SaveVATToFile();
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

    // Класс для хранения данных сотрудника
    public class BitrixUser : INotifyPropertyChanged
    {
        public int id { get; set; }
        public string name { get; set; }
        public string work_position { get; set; }
        public string Initials { get; set; }

        // Свойство для отображения в комбобоксе
        public string DisplayName => !string.IsNullOrWhiteSpace(work_position)
            ? $"{id}. {name} ({work_position})"
            : $"{id}. {name}";

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}