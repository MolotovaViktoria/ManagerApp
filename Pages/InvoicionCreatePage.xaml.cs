using ManagerApp.Classes.Setting;
using ManagerApp.Data.GetInfo;
using ManagerApp.Data.ScharedData;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
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

        // Добавляем новое свойство для поиска
        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged(nameof(SearchText));
            }
        }

        private string _companyInfo;
        public string CompanyInfo
        {
            get => _companyInfo;
            set
            {
                _companyInfo = value;
                OnPropertyChanged(nameof(CompanyInfo));
            }
        }
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
            // Подписываемся на событие изменения текста в комбобоксе
            cmbCompanies.AddHandler(TextBox.TextChangedEvent,
                new TextChangedEventHandler(cmbCompanies_TextChanged),
                true);
        }

        // Метод для поиска компании по кнопке
        private void btnSearch_Click(object sender, RoutedEventArgs e)
        {
            SearchAndSelectCompany();
        }

        // Обработка изменения текста в реальном времени
        private void cmbCompanies_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.IsEditable)
            {
                // Автоматически открываем выпадающий список при вводе
                if (!string.IsNullOrWhiteSpace(comboBox.Text))
                {
                    comboBox.IsDropDownOpen = true;
                }
            }
        }

        // Умный поиск компании
        private void SearchAndSelectCompany()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(SearchText))
                {
                    txtCompanyStatus.Text = "Введите название или ID компании для поиска";
                    return;
                }

                string searchTerm = SearchText.Trim().ToLower();
                Company foundCompany = null;

                // 1. Поиск по точному названию
                foundCompany = Companies.FirstOrDefault(c =>
                    c.Title?.ToLower() == searchTerm);

                // 2. Поиск по части названия
                if (foundCompany == null)
                {
                    foundCompany = Companies.FirstOrDefault(c =>
                        c.Title?.ToLower().Contains(searchTerm) == true);
                }

                // 3. Поиск по ID
                if (foundCompany == null && int.TryParse(searchTerm, out int companyId))
                {
                    foundCompany = Companies.FirstOrDefault(c => c.Id == companyId);
                }

                if (foundCompany != null)
                {
                    // Нашли компанию
                    SelectedCompany = foundCompany;
                    CompanyInfo = $"Найдена компания:\nНазвание: {foundCompany.Title}\nID: {foundCompany.Id}";
                    txtCompanyStatus.Text = $"✓ Найдена компания: {foundCompany.Title}";

                    // Очищаем поле поиска
                    SearchText = string.Empty;
                }
                else
                {
                    // Компания не найдена
                    CompanyInfo = $"Компания не найдена по запросу: '{SearchText}'";
                    txtCompanyStatus.Text = $"❌ Не найдена компания по запросу: '{SearchText}'";
                }
            }
            catch (Exception ex)
            {
                txtCompanyStatus.Text = $"Ошибка поиска: {ex.Message}";
                MessageBox.Show($"Ошибка при поиске компании: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
        //SSSSSS
        private async Task ProcessSuccessfulInvoiceAsync(int invoiceId,
    List<(string Name, int Id)> foundProducts, List<string> notFoundProducts)
        {
            txtCompanyStatus.Text = $"✅ Счет создан! ID: {invoiceId}";

            // ГЕНЕРАЦИЯ ДОКУМЕНТА WORD СРАЗУ ПОСЛЕ СОЗДАНИЯ СЧЕТА
            await GenerateAndDownloadDocumentAsync(invoiceId, foundProducts, notFoundProducts);

        }

        private async Task GenerateAndDownloadDocumentAsync(int invoiceId,
            List<(string Name, int Id)> foundProducts, List<string> notFoundProducts)
        {
            try
            {
                txtCompanyStatus.Text = "Генерация документа Word...";

                // ГЕНЕРАЦИЯ И СКАЧИВАНИЕ ДОКУМЕНТА
                string downloadUrl = await _bitrixService.GenerateInvoiceDocument(invoiceId, 2, "docx");

                string successMessage = $"Счет #{invoiceId} создан!\n" +
                                       $"Товаров: {foundProducts.Count}\n" +
                                       $"Итого: {GrandTotal:#,##0.00} ₽";

                if (!string.IsNullOrEmpty(downloadUrl))
                {
                    successMessage += $"\n\n📄 Документ Word готов!";

                    // Автоматическое скачивание файла в несколько мест
                    string mainPath = await DownloadDocumentFileAsync(
                        downloadUrl,
                        invoiceId,
                        SelectedCompany?.Title ?? "БезНазвания");

                    if (!string.IsNullOrEmpty(mainPath))
                    {
                        successMessage += $"\n\nФайл сохранен в:\n1. {mainPath}";
                    }
                }
                else
                {
                    successMessage += $"\n\n⚠️ Документ не сгенерирован";
                }

                if (notFoundProducts.Any())
                {
                    successMessage += $"\n\nПропущено: {notFoundProducts.Count} товаров";
                }

                MessageBox.Show(successMessage, "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка генерации документа: {ex.Message}");
                MessageBox.Show($"Счет создан, но документ не сгенерирован:\n{ex.Message}",
                    "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        private void LoadInvoiceItems(List<InvoiceItem> items)
        {
            InvoiceItems.Clear();

            foreach (var item in items)
            {
                var invoiceItem = new InvoiceItemViewModel
                {
                    BitrixProductId = item.BitrixProductId, // ДОБАВЬТЕ ЭТУ СТРОЧКУ
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
        private async Task CreateInvoiceAsync()
        {
            try
            {
                // ПРОВЕРКА ВЫБРАННЫХ КОМПАНИЙ
                if (SelectedCompany == null || SelectedMyCompany == null)
                {
                    MessageBox.Show("Выберите обе компании!", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // ПРОВЕРКА ТОВАРОВ
                if (!InvoiceItems.Any())
                {
                    MessageBox.Show("Добавьте товары в счет!", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // ПОДТВЕРЖДЕНИЕ
                var confirmResult = MessageBox.Show(
                    $"Создать счет для {SelectedCompany.Title}?\n" +
                    $"Итого: {GrandTotal:#,##0.00} ₽", "Подтверждение",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (confirmResult != MessageBoxResult.Yes) return;

                // БЛОКИРОВКА КНОПОК
                DisableButtons();

                // ПОДГОТОВКА ТОВАРОВ
                var (invoiceProducts, foundProducts, notFoundProducts) = await PrepareProductsAsync();

                // ПРОВЕРКА РЕЗУЛЬТАТОВ
                if (!invoiceProducts.Any(p => p.ProductId > 0))
                {
                    ShowNoProductsError(notFoundProducts);
                    return;
                }

                if (notFoundProducts.Any()) ShowNotFoundWarning(notFoundProducts);

                // СОЗДАНИЕ СЧЕТА
                int invoiceId = await CreateBitrixInvoiceAsync(invoiceProducts);

                if (invoiceId > 0)
                {
                    await ProcessSuccessfulInvoiceAsync(invoiceId, foundProducts, notFoundProducts);
                }
                else
                {
                    ShowInvoiceError();
                }
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
            finally
            {

            }
        }

        // === ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ===

        private void DisableButtons()
        {
            txtCompanyStatus.Text = "Подготовка товаров...";
         
        }

        private async Task<(List<InvoiceProduct> products, List<(string Name, int Id)> found, List<string> notFound)>
            PrepareProductsAsync()
        {
            var invoiceProducts = new List<InvoiceProduct>();
            var notFoundProducts = new List<string>();
            var foundProducts = new List<(string Name, int Id)>();

            foreach (var item in InvoiceItems)
            {
                try
                {
                    txtCompanyStatus.Text = $"Обработка: {item.ProductName}";

                    int productId = item.BitrixProductId;

                    if (productId == 0)
                    {
                        productId = await _bitrixService.GetProductIdByName(item.OriginalProductName ?? item.ProductName);

                        if (productId == 0)
                        {
                            var result = MessageBox.Show(
                                $"Товар '{item.ProductName}' не найден.\nСоздать?",
                                "Внимание", MessageBoxButton.YesNo, MessageBoxImage.Question);

                            if (result == MessageBoxResult.Yes)
                            {
                                productId = await _bitrixService.CreateProductIfNotExists(
                                    item.OriginalProductName ?? item.ProductName, item.Price);

                                if (productId > 0)
                                {
                                    await BitrixCache.RefreshCacheAsync();
                                    foundProducts.Add((item.ProductName, productId));
                                }
                                else
                                {
                                    notFoundProducts.Add($"{item.ProductName} (не создан)");
                                    continue;
                                }
                            }
                            else
                            {
                                notFoundProducts.Add($"{item.ProductName} (пропущен)");
                                continue;
                            }
                        }
                    }

                    if (productId > 0)
                    {
                        foundProducts.Add((item.ProductName, productId));
                        invoiceProducts.Add(new InvoiceProduct
                        {
                            ProductId = productId,
                            ProductName = item.ProductName,
                            Quantity = item.Quantity,
                            Price = item.Price
                        });
                    }
                }
                catch (Exception ex)
                {
                    notFoundProducts.Add($"{item.ProductName} (ошибка: {ex.Message})");
                }
            }

            return (invoiceProducts, foundProducts, notFoundProducts);
        }

        private void ShowNoProductsError(List<string> notFoundProducts)
        {
            MessageBox.Show($"Нет товаров с реальными ID!\n\n" +
                           $"Не найдено:\n{string.Join("\n", notFoundProducts)}",
                           "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            txtCompanyStatus.Text = "❌ Нет товаров для счета";
        }

        private void ShowNotFoundWarning(List<string> notFoundProducts)
        {
            string warning = $"Следующие товары не добавлены:\n" +
                            string.Join("\n", notFoundProducts.Take(3));
            if (notFoundProducts.Count > 3) warning += $"\n...и еще {notFoundProducts.Count - 3}";

            MessageBox.Show(warning, "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private async Task<int> CreateBitrixInvoiceAsync(List<InvoiceProduct> invoiceProducts)
        {
            txtCompanyStatus.Text = "Создание счета...";

            // Получаем вебхук из настроек
            string webhook = WebhookManager.GetWebhookFromFile();

            if (string.IsNullOrWhiteSpace(webhook))
            {
                MessageBox.Show("Вебхук Bitrix24 не настроен. Пожалуйста, укажите его в настройках.",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return -1;
            }

            string orderTopic = $"Счет для {SelectedCompany.Title} от {DateTime.Now:dd.MM.yyyy}";

            return await _bitrixService.CreateSmartInvoice(
                webhook, // Используем вебхук из настроек
                SelectedCompany.Id,
                SelectedMyCompany.Id,
                orderTopic,
                invoiceProducts.Where(p => p.ProductId > 0).ToList());
        }


        


        private async Task<string> DownloadDocumentFileAsync(string documentUrl, int invoiceId, string companyName)
        {
            try
            {
                byte[] fileBytes = await _bitrixService.DownloadDocumentBytes(documentUrl);

                if (fileBytes == null || fileBytes.Length == 0)
                {
                    Console.WriteLine("❌ Не удалось загрузить документ");
                    return null;
                }

                List<string> savedPaths = new List<string>();

                // 1. Сохраняем в папку ManagerApp\History
                string appHistoryPath = GetAppHistoryFilePath(invoiceId, companyName);
                await SaveToFileAsync(fileBytes, appHistoryPath);
                savedPaths.Add(appHistoryPath);

                // 2. Сохраняем в папку Downloads текущего пользователя
                string downloadsPath = GetDownloadsFilePath(invoiceId, companyName);
                await SaveToFileAsync(fileBytes, downloadsPath);
                savedPaths.Add(downloadsPath);

                // 3. Предлагаем пользователю выбрать дополнительную папку
                string userSelectedPath = await SaveWithUserDialogAsync(fileBytes, invoiceId, companyName);
                if (!string.IsNullOrEmpty(userSelectedPath))
                {
                    savedPaths.Add(userSelectedPath);
                }

                Console.WriteLine($"✅ Документ сохранен в {savedPaths.Count} местах:");
                foreach (var path in savedPaths)
                {
                    Console.WriteLine($"   📁 {path}");
                }

                // Открываем папку History для просмотра
                OpenFolderInExplorer(appHistoryPath);

                return appHistoryPath; // Возвращаем основной путь к файлу
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка скачивания: {ex.Message}");

                // Если не удалось сохранить, открываем URL напрямую
                Process.Start(new ProcessStartInfo
                {
                    FileName = documentUrl,
                    UseShellExecute = true
                });

                return null;
            }
        }

        // Получаем путь к файлу в папке History приложения
        private string GetAppHistoryFilePath(int invoiceId, string companyName)
        {
            try
            {
                // Папка History относительно папки приложения
                string appBasePath = AppDomain.CurrentDomain.BaseDirectory;
                string historyPath = Path.Combine(appBasePath, "History");

                // Создаем папку History, если её нет
                if (!Directory.Exists(historyPath))
                {
                    Directory.CreateDirectory(historyPath);
                }

                // Создаем подпапку по году и месяцу для организации
                string yearMonthPath = Path.Combine(historyPath, DateTime.Now.ToString("yyyy-MM"));
                if (!Directory.Exists(yearMonthPath))
                {
                    Directory.CreateDirectory(yearMonthPath);
                }

                // Очищаем имя компании от недопустимых символов
                string safeCompanyName = RemoveInvalidFileNameChars(companyName);

                // Формируем имя файла
                string fileName = $"Счет_{invoiceId}_{safeCompanyName}_{DateTime.Now:yyyyMMdd_HHmmss}.docx";

                return Path.Combine(yearMonthPath, fileName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка формирования пути History: {ex.Message}");
                return null;
            }
        }

        // Получаем путь к файлу в папке Downloads
        private string GetDownloadsFilePath(int invoiceId, string companyName)
        {
            try
            {
                string downloadsPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Downloads");

                string safeCompanyName = RemoveInvalidFileNameChars(companyName);
                string fileName = $"Счет_{invoiceId}_{safeCompanyName}_{DateTime.Now:yyyyMMdd}.docx";

                return Path.Combine(downloadsPath, fileName);
            }
            catch
            {
                return null;
            }
        }

        // Предлагаем пользователю выбрать папку для сохранения
        private async Task<string> SaveWithUserDialogAsync(byte[] fileBytes, int invoiceId, string companyName)
        {
            try
            {
                return await Task.Run(() =>
                {
                    // Используем диалог сохранения файла WPF
                    var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                    {
                        Title = "Сохранить документ счета",
                        Filter = "Документ Word (*.docx)|*.docx|Все файлы (*.*)|*.*",
                        FileName = $"Счет_{invoiceId}_{RemoveInvalidFileNameChars(companyName)}.docx",
                        DefaultExt = ".docx",
                        AddExtension = true
                    };

                    bool? result = saveFileDialog.ShowDialog();

                    if (result == true && !string.IsNullOrEmpty(saveFileDialog.FileName))
                    {
                        // Сохраняем файл в выбранное место
                        File.WriteAllBytes(saveFileDialog.FileName, fileBytes);
                        return saveFileDialog.FileName;
                    }

                    return null;
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка диалога сохранения: {ex.Message}");
                return null;
            }
        }

        // Утилита для сохранения байтов в файл
        private async Task SaveToFileAsync(byte[] fileBytes, string filePath)
        {
            try
            {
                if (fileBytes == null || string.IsNullOrEmpty(filePath))
                    return;

                // Создаем директорию, если её нет
                string directory = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Сохраняем файл (синхронно)
                File.WriteAllBytes(filePath, fileBytes);
                Console.WriteLine($"✅ Сохранено: {filePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка сохранения в {filePath}: {ex.Message}");
            }
        }

        // Удаляем недопустимые символы из имени файла
        private string RemoveInvalidFileNameChars(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return "БезНазвания";

            string invalidChars = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());

            foreach (char c in invalidChars)
            {
                fileName = fileName.Replace(c.ToString(), "");
            }

            // Ограничиваем длину имени файла
            if (fileName.Length > 50)
            {
                fileName = fileName.Substring(0, 50);
            }

            return fileName.Trim();
        }

        // Открываем папку с файлом в проводнике
        private void OpenFolderInExplorer(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    string directory = Path.GetDirectoryName(filePath);

                    // Открываем проводник и выделяем файл
                    Process.Start("explorer.exe", $"/select,\"{filePath}\"");
                }
                else if (Directory.Exists(Path.GetDirectoryName(filePath)))
                {
                    // Открываем только папку
                    Process.Start("explorer.exe", $"\"{Path.GetDirectoryName(filePath)}\"");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка открытия папки: {ex.Message}");
            }
        }
    
    

    private void ShowInvoiceError()
        {
            txtCompanyStatus.Text = "❌ Ошибка создания счета";
            MessageBox.Show("Не удалось создать счет.\nПроверьте консоль для деталей.",
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void HandleException(Exception ex)
        {
            txtCompanyStatus.Text = $"❌ Ошибка: {ex.Message}";
            MessageBox.Show($"Ошибка при создании счета:\n{ex.Message}",
                "Критическая ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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

                // Возвращаемся на страницу DombPage
                var dombPage = new DombPage();
                NavigationService.Navigate(dombPage);
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
        private int _bitrixProductId;
        public int BitrixProductId
        {
            get => _bitrixProductId;
            set
            {
                _bitrixProductId = value;
                OnPropertyChanged(nameof(BitrixProductId));
            }
        }
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