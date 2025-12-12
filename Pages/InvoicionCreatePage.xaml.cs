using ManagerApp.Classes.Setting;
using ManagerApp.Data.GetInfo;
using ManagerApp.Data.ScharedData;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using static ManagerApp.Data.GetInfo.BitrixService;

namespace ManagerApp.Pages
{
    /// <summary>
    /// Логика взаимодействия для InvoicionCreatePage.xaml
    /// </summary>
    public partial class InvoicionCreatePage : Page, INotifyPropertyChanged
    {
        private BitrixService _bitrixService;
        private List<Company> _allCompanies; // Полный список компаний для фильтрации


        // Коллекции для комбобоксов
        public ObservableCollection<Company> Companies { get; set; }
        public ObservableCollection<Company> MyCompanies { get; set; }
        public ObservableCollection<PaymentStatus> PaymentStatuses { get; set; }

        // Выбранные значения
        private Company _selectedCompany;
        public Company SelectedCompany
        {
            get => _selectedCompany;
            set
            {
                _selectedCompany = value;
                OnPropertyChanged(nameof(SelectedCompany));
                if (value != null)
                {
                    DisplayCompanyDetails(value);
                }
            }
        }

        private Company _selectedMyCompany;
        public Company SelectedMyCompany
        {
            get => _selectedMyCompany;
            set
            {
                _selectedMyCompany = value;
                OnPropertyChanged(nameof(SelectedMyCompany));
                if (value != null)
                {
                    DisplayMyCompanyDetails(value);
                }
            }
        }
        private PaymentStatus _selectedPaymentStatus;
        public PaymentStatus SelectedPaymentStatus;

        // Реквизиты и контакты
        private string _companyDetails;
        public string CompanyDetails
        {
            get => _companyDetails;
            set
            {
                _companyDetails = value;
                OnPropertyChanged(nameof(CompanyDetails));
            }
        }

        private ObservableCollection<ContactInfo> _companyContacts;
        public ObservableCollection<ContactInfo> CompanyContacts
        {
            get => _companyContacts;
            set
            {
                _companyContacts = value;
                OnPropertyChanged(nameof(CompanyContacts));
            }
        }

        private string _myCompanyDetails;
        public string MyCompanyDetails
        {
            get => _myCompanyDetails;
            set
            {
                _myCompanyDetails = value;
                OnPropertyChanged(nameof(MyCompanyDetails));
            }
        }

        // Остальные свойства...
        public ObservableCollection<InvoiceItemViewModel> InvoiceItems { get; set; }

        private decimal _totalAmount;
        public decimal TotalAmount
        {
            get => _totalAmount;
            set
            {
                _totalAmount = value;
                OnPropertyChanged(nameof(TotalAmount));
                CalculateGrandTotal();
            }
        }

        private decimal _taxAmount;
        public decimal TaxAmount
        {
            get => _taxAmount;
            set
            {
                _taxAmount = value;
                OnPropertyChanged(nameof(TaxAmount));
                CalculateGrandTotal();
            }
        }

        private decimal _grandTotal;
        public decimal GrandTotal
        {
            get => _grandTotal;
            set
            {
                _grandTotal = value;
                OnPropertyChanged(nameof(GrandTotal));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Конструктор без параметров (для дизайнера)
        public InvoicionCreatePage()
        {
            InitializeComponent();
            InitializeData();
            LoadCompaniesAsync();
            InitializePaymentStatuses(); // Добавьте эту строку
        }
        private void InitializePaymentStatuses()
        {
            PaymentStatuses = new ObservableCollection<PaymentStatus>
    {
        new PaymentStatus { Id = "N", Name = "Новый" },
        new PaymentStatus { Id = "P", Name = "Оплачен" },
        new PaymentStatus { Id = "D", Name = "Оплата в долг" },
        new PaymentStatus { Id = "S", Name = "Частично оплачен" },
        new PaymentStatus { Id = "F", Name = "Отклонен" }
    };

            if (PaymentStatuses.Count > 0)
            {
                SelectedPaymentStatus = PaymentStatuses[0];
                OnPropertyChanged(nameof(PaymentStatuses));
            }
        }

        public class PaymentStatus
        {
            public string Id { get; set; }
            public string Name { get; set; }
        }


        private async Task CreateInvoiceAsync()
        {
            try
            {
                // ТЕСТОВАЯ ФИКСИРОВАННАЯ КОМПАНИЯ
                // Вместо проверки выбранных компаний используем фиксированные значения для теста
                int clientCompanyId = 897; // Фиксированный ID тестовой компании клиента
                string clientCompanyTitle = "ООО 'Тестовая компания'";

                int myCompanyId = 6; // Фиксированный ID вашей компании
                string myCompanyTitle = "ООО 'Моя компания'";

                // Проверяем наличие товаров
                if (!InvoiceItems.Any())
                {
                    MessageBox.Show("Добавьте товары в счет!", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // ТЕСТОВОЕ ПОДТВЕРЖДЕНИЕ
                var confirmResult = MessageBox.Show(
                    $"ТЕСТОВЫЙ РЕЖИМ!\n\n" +
                    $"Создать ТЕСТОВЫЙ счет?\n" +
                    $"Клиент: {clientCompanyTitle} (ID: {clientCompanyId})\n" +
                    $"Ваша компания: {myCompanyTitle} (ID: {myCompanyId})\n" +
                    $"Товаров: {InvoiceItems.Count}\n" +
                    $"Итого: {GrandTotal:#,##0.00} ₽\n\n" +
                    $"ВСЕ ТОВАРЫ БУДУТ С ID = 34045 (тестовый)",
                    "ТЕСТ: Подтверждение создания счета",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (confirmResult != MessageBoxResult.Yes)
                {
                    return;
                }

                // ТЕСТОВЫЕ ТОВАРЫ - ИСПОЛЬЗУЕМ ФИКСИРОВАННЫЙ ID 34045 ДЛЯ ВСЕХ ТОВАРОВ
                var invoiceProducts = InvoiceItems.Select((item, index) => new InvoiceProduct
                {
                    ProductId = 34045, // ТЕСТОВЫЙ ID для всех товаров
                    ProductName = $"[ТЕСТ] {item.ProductName}",
                    Quantity = item.Quantity,
                    Price = item.Price
                }).ToList();

                // ТЕСТОВАЯ ТЕМА СЧЕТА
                string orderTopic = $"[ТЕСТ] Счет для {clientCompanyTitle} от {DateTime.Now:dd.MM.yyyy HH:mm}";

                // Показываем статус создания
                txtCompanyStatus.Text = "Создание ТЕСТОВОГО счета...";
                btnCreateInvoice.IsEnabled = false;
                btnFinish.IsEnabled = false;
                btnPreview.IsEnabled = false;

                // СОЗДАЕМ ТЕСТОВЫЙ СЧЕТ
                int invoiceId = await _bitrixService.CreateInvoiceUniversal(
                    clientCompanyId,      // Фиксированный ID клиентской компании
                    myCompanyId,          // Фиксированный ID вашей компании
                    orderTopic,
                    invoiceProducts);

                if (invoiceId > 0)
                {
                    txtCompanyStatus.Text = $"✅ ТЕСТОВЫЙ счет создан! ID: {invoiceId}";

                    MessageBox.Show($"ТЕСТОВЫЙ счет успешно создан!\n\n" +
                                  $"Режим: ТЕСТ\n" +
                                  $"Номер счета: {invoiceId}\n" +
                                  $"Клиент: {clientCompanyTitle}\n" +
                                  $"Ваша компания: {myCompanyTitle}\n" +
                                  $"Товаров: {InvoiceItems.Count}\n" +
                                  $"ProductId для всех товаров: 34045\n" +
                                  $"Итого: {GrandTotal:#,##0.00} ₽",
                                  "ТЕСТ: Счет создан",
                                  MessageBoxButton.OK,
                                  MessageBoxImage.Information);

                    // Журналирование для отладки
                    Console.WriteLine($"ТЕСТОВЫЙ СЧЕТ СОЗДАН:");
                    Console.WriteLine($"ID счета: {invoiceId}");
                    Console.WriteLine($"Клиент ID: {clientCompanyId}");
                    Console.WriteLine($"Моя компания ID: {myCompanyId}");
                    Console.WriteLine($"Товары: {InvoiceItems.Count}");
                    Console.WriteLine($"Все ProductId: 34045");

                    // Возвращаемся на главную страницу через 3 секунды
                    await Task.Delay(3000);

                    if (NavigationService != null && NavigationService.CanGoBack)
                    {
                        NavigationService.GoBack();
                    }
                }
                else if (invoiceId == 0)
                {
                    txtCompanyStatus.Text = "❌ Ошибка создания ТЕСТОВОГО счета";

                    // Более подробное сообщение об ошибке
                    MessageBox.Show($"ТЕСТОВЫЙ счет не создан!\n\n" +
                                  $"Проверьте:\n" +
                                  $"1. Существует ли товар с ID=34045 в Bitrix24?\n" +
                                  $"2. Проверьте консоль приложения для деталей ошибки\n" +
                                  $"3. Компании с ID {clientCompanyId} и {myCompanyId} должны существовать\n" +
                                  $"4. Вебхук должен быть активен",
                                  "ТЕСТ: Ошибка",
                                  MessageBoxButton.OK,
                                  MessageBoxImage.Error);
                }
                else if (invoiceId == -1)
                {
                    txtCompanyStatus.Text = "❌ Компания уже существует (это нормально для теста)";
                }
            }
            catch (Exception ex)
            {
                txtCompanyStatus.Text = $"❌ ТЕСТОВАЯ ошибка: {ex.Message}";

                // Детальное сообщение об ошибке
                string errorDetails = $"ТЕСТОВАЯ ошибка при создании счета:\n\n" +
                                     $"Сообщение: {ex.Message}\n" +
                                     $"Тип: {ex.GetType().Name}\n\n" +
                                     $"Детали:\n{ex.StackTrace}";

                MessageBox.Show(errorDetails,
                              "ТЕСТ: Ошибка",
                              MessageBoxButton.OK,
                              MessageBoxImage.Error);
            }
            finally
            {
                btnCreateInvoice.IsEnabled = true;
                btnFinish.IsEnabled = true;
                btnPreview.IsEnabled = true;
            }
        }
        // Конструктор с передачей списка товаров
        public InvoicionCreatePage(List<InvoiceItem> invoiceItems) : this()
        {
            if (invoiceItems != null && invoiceItems.Any())
            {
                LoadInvoiceItems(invoiceItems);
            }
        }

        private void InitializeData()
        {
            _bitrixService = new BitrixService();
            Companies = new ObservableCollection<Company>();
            MyCompanies = new ObservableCollection<Company>();
            _allCompanies = new List<Company>();
            CompanyContacts = new ObservableCollection<ContactInfo>();
            InvoiceItems = new ObservableCollection<InvoiceItemViewModel>();
            DataContext = this;
        }

        // Асинхронная загрузка компаний
        private async void LoadCompaniesAsync()
        {
            try
            {
                txtCompanyStatus.Text = "Загрузка компаний...";
                btnCreateNewCompany.IsEnabled = false;
                btnRefreshCompanies.IsEnabled = false;

                // Загружаем все компании (клиентов)
                var allCompanies = await _bitrixService.GetAllCompanies();
                // Загружаем "мои" компании (реквизиты)
                var myCompanies = await _bitrixService.GetMyCompanies();

                Companies.Clear();
                MyCompanies.Clear();
                _allCompanies.Clear();

                // Получаем ID "моих" компаний для фильтрации
                var myCompanyIds = myCompanies.Select(c => c.Id).ToHashSet();

                // Фильтруем общие компании: исключаем "мои" компании
                foreach (var company in allCompanies)
                {
                    // Проверяем, что компания не является "моей"
                    if (!myCompanyIds.Contains(company.Id))
                    {
                        Companies.Add(company);
                        _allCompanies.Add(company);
                    }
                }

                // Добавляем "мои" компании
                foreach (var company in myCompanies)
                {
                    MyCompanies.Add(company);
                }

                // Выбираем первую компанию по умолчанию, если есть
                if (Companies.Count > 0)
                {
                    SelectedCompany = Companies[0];
                }

                if (MyCompanies.Count > 0)
                {
                    SelectedMyCompany = MyCompanies[0];
                }

                txtCompanyStatus.Text = $"Загружено компаний: {Companies.Count}";
            }
            catch (Exception ex)
            {
                txtCompanyStatus.Text = $"Ошибка: {ex.Message}";
                MessageBox.Show($"Ошибка загрузки компаний: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnCreateNewCompany.IsEnabled = true;
                btnRefreshCompanies.IsEnabled = true;
            }
        }

        // Кнопка создания новой компании
        private async void btnCreateNewCompany_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Создаем и показываем окно создания компании
                var createCompanyWindow = new CreateCompany(_bitrixService)
                {
                    Owner = Application.Current.MainWindow,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };

                var result = createCompanyWindow.ShowDialog();

                if (result == true && createCompanyWindow.CreatedCompanyId > 0)
                {
                    // Компания успешно создана
                    int newCompanyId = createCompanyWindow.CreatedCompanyId;

                    // Показываем сообщение об успехе
                    txtCompanyStatus.Text = $"Компания создана! ID: {newCompanyId}. Обновление списка...";

                    // Ждем 1 секунду для обновления данных в Bitrix
                    await Task.Delay(1000);

                    // Обновляем список компаний
                    await RefreshCompaniesList();

                    // Пытаемся найти и выбрать новую компанию
                    await SelectNewCompany(newCompanyId);
                }
                else if (result == false)
                {
                    // Пользователь отменил создание
                    txtCompanyStatus.Text = "Создание компании отменено";
                }
            }
            catch (Exception ex)
            {
                txtCompanyStatus.Text = $"Ошибка: {ex.Message}";
                MessageBox.Show($"Ошибка при создании компании: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Асинхронное обновление списка компаний
        private async Task RefreshCompaniesList()
        {
            try
            {
                btnCreateNewCompany.IsEnabled = false;
                btnRefreshCompanies.IsEnabled = false;
                txtCompanyStatus.Text = "Обновление списка компаний...";

                // Сохраняем текущее выбранное значение
                var currentSelectedId = SelectedCompany?.Id;

                // Получаем обновленные данные
                var allCompanies = await _bitrixService.GetAllCompanies();
                var myCompanies = await _bitrixService.GetMyCompanies();

                Companies.Clear();
                MyCompanies.Clear();
                _allCompanies.Clear();

                // Получаем ID "моих" компаний для фильтрации
                var myCompanyIds = myCompanies.Select(c => c.Id).ToHashSet();

                // Фильтруем общие компании
                foreach (var company in allCompanies)
                {
                    if (!myCompanyIds.Contains(company.Id))
                    {
                        Companies.Add(company);
                        _allCompanies.Add(company);
                    }
                }

                // Добавляем "мои" компании
                foreach (var company in myCompanies)
                {
                    MyCompanies.Add(company);
                }

                // Выбираем компанию по сохраненному ID или первую
                if (currentSelectedId.HasValue)
                {
                    var companyToSelect = Companies.FirstOrDefault(c => c.Id == currentSelectedId.Value);
                    SelectedCompany = companyToSelect ?? Companies.FirstOrDefault();
                }
                else if (Companies.Count > 0)
                {
                    SelectedCompany = Companies[0];
                }

                txtCompanyStatus.Text = $"Список обновлен. Компаний: {Companies.Count}";
            }
            catch (Exception ex)
            {
                txtCompanyStatus.Text = $"Ошибка обновления: {ex.Message}";
                MessageBox.Show($"Ошибка обновления списка компаний: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnCreateNewCompany.IsEnabled = true;
                btnRefreshCompanies.IsEnabled = true;
            }
        }

        // Кнопка ручного обновления списка
        private async void btnRefreshCompanies_Click(object sender, RoutedEventArgs e)
        {
            await RefreshCompaniesList();
        }

     

        // Выбор новосозданной компании
        private async Task SelectNewCompany(int companyId)
        {
            try
            {
                // Делаем несколько попыток найти новую компанию
                for (int i = 0; i < 3; i++)
                {
                    await Task.Delay(1000); // Ждем перед каждой попыткой

                    var companyToSelect = Companies.FirstOrDefault(c => c.Id == companyId);
                    if (companyToSelect != null)
                    {
                        SelectedCompany = companyToSelect;
                        txtCompanyStatus.Text = $"Выбрана новая компания: {companyToSelect.Title}";
                        return;
                    }
                }

                // Если не нашли после нескольких попыток
                txtCompanyStatus.Text = "Новая компания пока не появилась в списке. Обновите список через некоторое время.";
            }
            catch (Exception ex)
            {
                txtCompanyStatus.Text = $"Ошибка выбора компании: {ex.Message}";
            }
        }

        private void DisplayCompanyDetails(Company company)
        {
            // Формируем строку с реквизитами компании
            var details = new System.Text.StringBuilder();
            details.AppendLine($"Компания: {company.Title ?? "Без названия"}");
            details.AppendLine($"ID: {company.Id}");
            details.AppendLine($"Ответственный: ID {company.AssignedById}");
            details.AppendLine($"Создана: {company.CreatedTime:dd.MM.yyyy}");

            // TODO: Здесь нужно реализовать получение реальных реквизитов компании
            // из Bitrix24 через дополнительный API-запрос

            CompanyDetails = details.ToString();

            // TODO: Здесь нужно реализовать загрузку контактов компании
            // через дополнительный API-запрос к Bitrix24
            LoadCompanyContacts(company.Id);
        }

        private async void LoadCompanyContacts(int companyId)
        {
            try
            {
                // TODO: Реализовать метод для получения контактов компании
                // Временная заглушка
                CompanyContacts.Clear();

                // Здесь будет реальный код для получения контактов
                // Например: var contacts = await _bitrixService.GetCompanyContacts(companyId);

                // Показываем сообщение, что контакты загружаются
                CompanyContacts.Add(new ContactInfo
                {
                    Name = "Загрузка контактов...",
                    Email = "",
                    Phone = ""
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки контактов: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DisplayMyCompanyDetails(Company company)
        {
            // Формируем строку с реквизитами "моей" компании
            var details = new System.Text.StringBuilder();
            details.AppendLine($"Компания: {company.Title ?? "Без названия"}");
            details.AppendLine($"ID: {company.Id}");
            details.AppendLine($"Ответственный: ID {company.AssignedById}");
            details.AppendLine($"Создана: {company.CreatedTime:dd.MM.yyyy}");

            // TODO: Здесь нужно реализовать получение реальных реквизитов "моей" компании
            // из Bitrix24 через дополнительный API-запрос
            // В Bitrix24 для "моих" компаний обычно есть поля с реквизитами

            MyCompanyDetails = details.ToString();
        }

        private void LoadInvoiceItems(List<InvoiceItem> items)
        {
            InvoiceItems.Clear();

            foreach (var item in items)
            {
                var invoiceItem = new InvoiceItemViewModel
                {
                    ProductName = item.ProductName,
                    OriginalProductName = item.OriginalProductName,
                    Price = item.Price,
                    Quantity = item.Quantity,
                    Unit = item.Unit,
                    VAT = item.VAT,
                    Total = item.Total
                };

                invoiceItem.PropertyChanged += InvoiceItem_PropertyChanged;
                InvoiceItems.Add(invoiceItem);
            }

            CalculateTotals();
        }

        private void InvoiceItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(InvoiceItemViewModel.Total) ||
                e.PropertyName == nameof(InvoiceItemViewModel.Quantity) ||
                e.PropertyName == nameof(InvoiceItemViewModel.Price))
            {
                CalculateTotals();
            }
        }

        private void CalculateTotals()
        {
            TotalAmount = InvoiceItems.Sum(i => i.Total);

            // Рассчитываем НДС
            var vatPercentage = SettingsHelper.GetVATAsDecimal() / 100;
            TaxAmount = TotalAmount * vatPercentage / (1 + vatPercentage);

            CalculateGrandTotal();
        }

        private void CalculateGrandTotal()
        {
            GrandTotal = TotalAmount;
        }

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService.CanGoBack)
            {
                NavigationService.GoBack();
            }
        }

        private async void btnFinish_Click(object sender, RoutedEventArgs e)
        {
            // Проверяем наличие выбранных компаний
            if (SelectedCompany == null)
            {
                MessageBox.Show("Выберите компанию клиента!", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (SelectedMyCompany == null)
            {
                MessageBox.Show("Выберите вашу компанию!", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                // Отключаем кнопку на время операции
                btnFinish.IsEnabled = false;

                // Сохраняем данные асинхронно
                await CreateInvoiceAsync();

                MessageBox.Show($"Счет успешно создан для:\n" +
                              $"Клиент: {SelectedCompany.Title}\n" +
                              $"Ваша компания: {SelectedMyCompany.Title}\n" +
                              $"Товаров: {InvoiceItems.Count}\n" +
                              $"Итого: {GrandTotal:#,##0.00} ₽",
                              "Счет создан", MessageBoxButton.OK, MessageBoxImage.Information);

                // Возвращаемся на главную страницу
                if (NavigationService.CanGoBack)
                {
                    NavigationService.GoBack();
                }
            }
            catch (Exception ex)
            {
                // Логируем ошибку и показываем пользователю

                MessageBox.Show($"Ошибка при создании счета: {ex.Message}",
                              "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Включаем кнопку обратно
                btnFinish.IsEnabled = true;
            }
        }

      

        private void PreviewInvoice()
        {
            try
            {
                if (SelectedCompany == null || SelectedMyCompany == null || !InvoiceItems.Any())
                {
                    MessageBox.Show("Заполните все обязательные поля для предпросмотра!", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Формируем текст для предпросмотра
                var previewText = new System.Text.StringBuilder();
                previewText.AppendLine("=== ПРЕДПРОСМОТР СЧЕТА ===");
                previewText.AppendLine($"Дата: {DateTime.Now:dd.MM.yyyy}");
                previewText.AppendLine();
                previewText.AppendLine("=== КЛИЕНТ ===");
                previewText.AppendLine($"Компания: {SelectedCompany.Title}");
                previewText.AppendLine($"ID: {SelectedCompany.Id}");
                previewText.AppendLine();
                previewText.AppendLine("=== ПРОДАВЕЦ ===");
                previewText.AppendLine($"Компания: {SelectedMyCompany.Title}");
                previewText.AppendLine($"ID: {SelectedMyCompany.Id}");
                previewText.AppendLine();
                previewText.AppendLine("=== ТОВАРЫ ===");

                int itemNumber = 1;
                foreach (var item in InvoiceItems)
                {
                    previewText.AppendLine($"{itemNumber}. {item.ProductName}");
                    previewText.AppendLine($"   Количество: {item.Quantity}");
                    previewText.AppendLine($"   Цена: {item.Price:#,##0.00} ₽");
                    previewText.AppendLine($"   Итого: {item.Total:#,##0.00} ₽");
                    previewText.AppendLine();
                    itemNumber++;
                }

                previewText.AppendLine("=== ИТОГО ===");
                previewText.AppendLine($"Сумма: {TotalAmount:#,##0.00} ₽");
                previewText.AppendLine($"НДС: {TaxAmount:#,##0.00} ₽");
                previewText.AppendLine($"Всего к оплате: {GrandTotal:#,##0.00} ₽");

                // Показываем предпросмотр
                MessageBox.Show(previewText.ToString(), "Предпросмотр счета",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при создании предпросмотра: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void btnCreateInvoice_Click(object sender, RoutedEventArgs e)
        {
            await CreateInvoiceAsync();
        }

        private void btnPreview_Click(object sender, RoutedEventArgs e)
        {
            PreviewInvoice();
        }
    }

    // Класс для отображения контакта
    public class ContactInfo
    {
        public string Name { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
    }

    // ViewModel для элемента счета
    public class InvoiceItemViewModel : INotifyPropertyChanged
    {
        private string _productName;
        public string ProductName
        {
            get => _productName;
            set
            {
                _productName = value;
                OnPropertyChanged(nameof(ProductName));
            }
        }

        private string _originalProductName;
        public string OriginalProductName
        {
            get => _originalProductName;
            set
            {
                _originalProductName = value;
                OnPropertyChanged(nameof(OriginalProductName));
            }
        }

        private decimal _price;
        public decimal Price
        {
            get => _price;
            set
            {
                _price = value;
                OnPropertyChanged(nameof(Price));
                CalculateTotal();
            }
        }

        private decimal _quantity;
        public decimal Quantity
        {
            get => _quantity;
            set
            {
                _quantity = value;
                OnPropertyChanged(nameof(Quantity));
                CalculateTotal();
            }
        }

        private string _unit;
        public string Unit
        {
            get => _unit;
            set
            {
                _unit = value;
                OnPropertyChanged(nameof(Unit));
            }
        }

        private string _vat;
        public string VAT
        {
            get => _vat;
            set
            {
                _vat = value;
                OnPropertyChanged(nameof(VAT));
            }
        }

        private decimal _total;
        public decimal Total
        {
            get => _total;
            set
            {
                _total = value;
                OnPropertyChanged(nameof(Total));
            }
        }

        private void CalculateTotal()
        {
            Total = Price * Quantity;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }


      
        public class PaymentStatus
{
    public string Id { get; set; }
    public string Name { get; set; }
}


    }


}