using ManagerApp.Data.GetInfo;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Threading.Tasks;

namespace ManagerApp.Pages
{
    public partial class CreateCompany : Window
    {
        private readonly BitrixService _bitrixService;

        public int CreatedCompanyId { get; private set; }

        public CreateCompany(BitrixService bitrixService)
        {
            InitializeComponent();
            _bitrixService = bitrixService;
            CreatedCompanyId = 0;
        }

        private void txtTitle_TextChanged(object sender, TextChangedEventArgs e)
        {
            ValidateForm();
        }

        // Добавляем параметры в метод ValidateForm
        private void ValidateForm(object sender, TextChangedEventArgs e)
        {
            ValidateForm();
        }

        // Перегрузка метода без параметров
        private void ValidateForm()
        {
            bool isValid = !string.IsNullOrWhiteSpace(txtTitle.Text) &&
                          !string.IsNullOrWhiteSpace(txtInn.Text) &&
                          !string.IsNullOrWhiteSpace(txtKpp.Text) &&
                          !string.IsNullOrWhiteSpace(txtAddress.Text);

            btnCreate.IsEnabled = isValid;
        }

        private async void btnCreate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Блокируем кнопку
                btnCreate.IsEnabled = false;
                btnCreate.Content = "Создание...";
                txtMessage.Text = "Создание компании...";
                txtMessage.Foreground = System.Windows.Media.Brushes.Black;

                // Получаем данные
                string title = txtTitle.Text.Trim();
                string inn = txtInn.Text.Trim();
                string kpp = txtKpp.Text.Trim();
                string address = txtAddress.Text.Trim();
                string phone = txtPhone.Text.Trim();

                // Вызываем метод создания компании
                int companyId = await _bitrixService.CreateCompany(title, phone, address, inn, kpp);

                if (companyId > 0)
                {
                    // Успешно создано
                    CreatedCompanyId = companyId;
                    txtMessage.Text = $"✅ Компания создана! ID: {companyId}";
                    txtMessage.Foreground = System.Windows.Media.Brushes.Green;

                    // Закрываем окно через 2 секунды
                    await Task.Delay(2000);
                    DialogResult = true;
                }
                else if (companyId == -1)
                {
                    // Компания уже существует
                    txtMessage.Text = "⚠ Компания с таким названием уже существует";
                    txtMessage.Foreground = System.Windows.Media.Brushes.Orange;
                    btnCreate.IsEnabled = true;
                    btnCreate.Content = "Создать";
                }
                else
                {
                    // Ошибка создания
                    txtMessage.Text = "❌ Ошибка создания компании";
                    txtMessage.Foreground = System.Windows.Media.Brushes.Red;
                    btnCreate.IsEnabled = true;
                    btnCreate.Content = "Создать";
                }
            }
            catch (Exception ex)
            {
                txtMessage.Text = $"❌ Ошибка: {ex.Message}";
                txtMessage.Foreground = System.Windows.Media.Brushes.Red;
                btnCreate.IsEnabled = true;
                btnCreate.Content = "Создать";
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
    }
}