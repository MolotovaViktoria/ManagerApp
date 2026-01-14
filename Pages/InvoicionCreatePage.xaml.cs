using DocumentFormat.OpenXml.Packaging;
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
using System.Windows.Media;
using static ManagerApp.Data.GetInfo.BitrixService;

using DocumentFormat.OpenXml.Wordprocessing;
namespace ManagerApp.Pages
{
    /// <summary>
    /// Логика взаимодействия для InvoicionCreatePage.xaml
    /// </summary>
    public partial class InvoicionCreatePage : Page, INotifyPropertyChanged
    {
        DateTime inviteTime;
        // Новые свойства для полей формы
        private string _invoiceNumber;
        public string InvoiceNumber
        {
            get => _invoiceNumber;
            set { _invoiceNumber = value; OnPropertyChanged(nameof(InvoiceNumber)); }
        }

        private string _address = "самовывоз со склада Поставщика г. Екатеринбург, ул. Мартовская, д.8: с 9 ч. 00 мин. до 18 ч. 00 мин. по местному времени в рабочие дни";
        public string Address
        {
            get => _address;
            set { _address = value; OnPropertyChanged(nameof(Address)); }
        }

        private string _deliveryDays;
        public string DeliveryDays
        {
            get => _deliveryDays;
            set { _deliveryDays = value; OnPropertyChanged(nameof(DeliveryDays)); }
        }

        // Способы оплаты
        public ObservableCollection<PaymentMethod> PaymentMethods { get; set; }
        private PaymentMethod _selectedPaymentMethod;
        public PaymentMethod SelectedPaymentMethod
        {
            get => _selectedPaymentMethod;
            set { _selectedPaymentMethod = value; OnPropertyChanged(nameof(SelectedPaymentMethod)); }
        }

        public class PaymentMethod
        {
            public string Name { get; set; }
            public string Value { get; set; }
        }

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
        public PaymentStatus SelectedPaymentStatus
        {
            get => _selectedPaymentStatus;
            set
            {
                _selectedPaymentStatus = value;
                OnPropertyChanged(nameof(SelectedPaymentStatus));
            }
        }

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
            InitializePaymentStatuses();
            InitializePaymentMethods();

            cmbCompanies.AddHandler(TextBox.TextChangedEvent,
                new TextChangedEventHandler(cmbCompanies_TextChanged),
                true);
        }

        // Простое добавление своего способа оплаты
        private void InitializePaymentMethods()
        {
            PaymentMethods = new ObservableCollection<PaymentMethod>
            {
                new PaymentMethod {
                    Name = "50% предоплата, 50% доплата в течение двух рабочих дней с момента извещения о готовности Товара",
                    Value = "50% предоплата, 50% доплата в течение двух рабочих дней с момента извещения о готовности Товара"
                },
                new PaymentMethod {
                    Name = "100% предоплата",
                    Value = "100% предоплата"
                },
                new PaymentMethod {
                    Name = "Отсрочка платежа 10 календарных дней",
                    Value = "Отсрочка платежа 10 календарных дней"
                }
            };

            if (PaymentMethods.Count > 0)
                SelectedPaymentMethod = PaymentMethods[0];
        }

     
        // Метод для обновления статуса с иконкой
        private void UpdateStatus(string message, string icon = "⏳")
        {
            Dispatcher.Invoke(() =>
            {
                txtStatusIcon.Text = icon;
                txtCompanyStatus.Text = message;

                // Меняем цвет иконки в зависимости от статуса
                if (icon == "✅")
                    txtStatusIcon.Foreground = Brushes.Green;
                else if (icon == "❌")
                    txtStatusIcon.Foreground = Brushes.Red;
                else if (icon == "⚠️")
                    txtStatusIcon.Foreground = Brushes.Orange;
                else
                    txtStatusIcon.Foreground = Brushes.Blue;
            });
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
                    UpdateStatus("Введите название или ID компании для поиска", "⚠️");
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
                    UpdateStatus($"✓ Найдена компания: {foundCompany.Title}", "✅");

                    // Очищаем поле поиска
                    SearchText = string.Empty;
                }
                else
                {
                    // Компания не найдена
                    CompanyInfo = $"Компания не найдена по запросу: '{SearchText}'";
                    UpdateStatus($"❌ Не найдена компания по запросу: '{SearchText}'", "❌");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Ошибка поиска: {ex.Message}", "❌");
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

        private async Task ProcessSuccessfulInvoiceAsync(int invoiceId,
            List<(string Name, int Id)> foundProducts, List<string> notFoundProducts)
        {
            UpdateStatus($"✅ Счет создан! ID: {invoiceId}", "✅");

            // ГЕНЕРАЦИЯ ДОКУМЕНТА WORD СРАЗУ ПОСЛЕ СОЗДАНИЯ СЧЕТА
            await GenerateAndDownloadDocumentAsync(invoiceId, foundProducts, notFoundProducts);
        }
        private string RemoveRubleSymbolFromText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            return text
                .Replace(" ₽", "")  // С пробелом
                .Replace("₽", "")   // Без пробела
                .Trim();
        }

        private async Task GenerateAndDownloadDocumentAsync(int invoiceId,
            List<(string Name, int Id)> foundProducts, List<string> notFoundProducts)
        {
            try
            {
                UpdateStatus("Генерация документа Word...", "⏳");

                // ГЕНЕРАЦИЯ И СКАЧИВАНИЕ ДОКУМЕНТА
                string downloadUrl = await _bitrixService.GenerateInvoiceDocument(invoiceId, 32, "docx");

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
                UpdateStatus($"Документ счета #{invoiceId} готов", "✅");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка генерации документа: {ex.Message}");
                UpdateStatus($"Ошибка генерации документа: {ex.Message}", "❌");
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
                    BitrixProductId = item.BitrixProductId,
                    ProductName = item.ProductName,
                    OriginalProductName = item.OriginalProductName,
                    Price = item.Price,
                    Quantity = item.Quantity,
                    Unit = item.UnitFullName,
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


                // ПРОВЕРКА ТОВАРОВ
                if (inviteTime == null)
                {
                    MessageBox.Show("Выберите дату!", "Ошибка",
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
                EnableButtons();
            }
        }

        // === ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ===

        private void DisableButtons()
        {
            UpdateStatus("Подготовка товаров...", "⏳");
            btnFinish.IsEnabled = false;
            //btnPreview.IsEnabled = false;
            //btnCreateInvoice.IsEnabled = false;
        }

        private void EnableButtons()
        {
            btnFinish.IsEnabled = true;
            //btnPreview.IsEnabled = true;
            //btnCreateInvoice.IsEnabled = true;
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
                    UpdateStatus($"Обработка: {item.ProductName}", "⏳");

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
                            Price = item.Price,
                            UnitName = item.Unit,

                            
                        });

                        Console.WriteLine("Наименование количества: " + item.ProductName + " " + item.Unit);
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
            UpdateStatus("❌ Нет товаров для счета", "❌");
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
            UpdateStatus("Создание счета...", "⏳");

            // Получаем ID выбранного сотрудника из настроек
            int responsibleEmployeeId = IDSetting.GetSelectedEmployeeId();

            if (responsibleEmployeeId <= 0)
            {
                // Если сотрудник не выбран, используем значение по умолчанию (241) и показываем предупреждение
                responsibleEmployeeId = 241; // значение по умолчанию
                UpdateStatus("Ответственный сотрудник не выбран, используется значение по умолчанию", "⚠️");
                Console.WriteLine("⚠️ Внимание: ответственный сотрудник не выбран в настройках!");
            }
            else
            {
                //// Получаем информацию о сотруднике для логов
                //int employee = IDSetting.GetSelectedEmployeeId();
                //if (employee != null)
                //{
                //    Console.WriteLine($"✅ Ответственный сотрудник: {employee} ");
                //}
            }

            string orderTopic = $"Счет для {SelectedCompany.Title} от {DateTime.Now:dd.MM.yyyy}";

            // ВЫВОД НОВЫХ ПОЛЕЙ В КОНСОЛЬ ПЕРЕД СОЗДАНИЕМ
            Console.WriteLine("=== ПЕРЕДАВАЕМЫЕ ДАННЫЕ ДЛЯ СЧЕТА ===");
            Console.WriteLine($"1. Номер счета: {InvoiceNumber}");
            Console.WriteLine($"2. Адрес: {Address}");
            Console.WriteLine($"3. Дней доставки: {DeliveryDays}");
            Console.WriteLine($"4. Способ оплаты: {(SelectedPaymentMethod?.Name ?? "Не выбран")}");
            Console.WriteLine($"5. Ответственный сотрудник ID: {responsibleEmployeeId}");

            // Показываем имя сотрудника, если он выбран
            if (responsibleEmployeeId > 0)
            {
                int employeeId = IDSetting.GetSelectedEmployeeId();

            }

            Console.WriteLine("=====================================");

            if (string.IsNullOrEmpty(Address))
                Address = "самовывоз со склада Поставщика г. Екатеринбург, ул. Мартовская, д.8: с 9 ч. 00 мин. до 18 ч. 00 мин. по местному времени в рабочие дни";

            return await _bitrixService.CreateSmartInvoice(
                inviteTime,
                SelectedCompany.Id,
                SelectedMyCompany.Id,
                orderTopic,
                invoiceProducts.Where(p => p.ProductId > 0).ToList(),
                InvoiceNumber,
                Address,
                DeliveryDays,
                SelectedPaymentMethod?.Value ?? "Не указано",

                responsibleEmployeeId // Передаем ID сотрудника вместо жестко закодированного значения
            );
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

                // ОЧИЩАЕМ ДОКУМЕНТ ОТ СИМВОЛА РУБЛЯ
                byte[] cleanedBytes = RemoveRubleSymbolFromDocument(fileBytes);

                List<string> savedPaths = new List<string>();

                // 1. Сохраняем в папку ManagerApp\History (ОЧИЩЕННЫЙ)
                string appHistoryPath = GetAppHistoryFilePath(invoiceId, companyName);
                await SaveToFileAsync(cleanedBytes, appHistoryPath); // Используем очищенный
                savedPaths.Add(appHistoryPath);

                // 2. Сохраняем в папку Downloads (ОЧИЩЕННЫЙ)
                string downloadsPath = GetDownloadsFilePath(invoiceId, companyName);
                await SaveToFileAsync(cleanedBytes, downloadsPath); // Используем очищенный
                savedPaths.Add(downloadsPath);

                // 3. Предлагаем пользователю выбрать дополнительную папку
                string userSelectedPath = await SaveWithUserDialogAsync(cleanedBytes, invoiceId, companyName);
                if (!string.IsNullOrEmpty(userSelectedPath))
                {
                    savedPaths.Add(userSelectedPath);
                }

                Console.WriteLine($"✅ Документ сохранен в {savedPaths.Count} местах (без символа ₽):");
                foreach (var path in savedPaths)
                {
                    Console.WriteLine($"   📁 {path}");
                }

                return appHistoryPath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка скачивания: {ex.Message}");

                Process.Start(new ProcessStartInfo
                {
                    FileName = documentUrl,
                    UseShellExecute = true
                });

                return null;
            }
        }

        private string GetAppHistoryFilePath(int invoiceId, string companyName)
        {
            try
            {
                string appBasePath = AppDomain.CurrentDomain.BaseDirectory;
                string historyPath = Path.Combine(appBasePath, "History");

                if (!Directory.Exists(historyPath))
                {
                    Directory.CreateDirectory(historyPath);
                }

                string yearMonthPath = Path.Combine(historyPath, DateTime.Now.ToString("yyyy-MM"));
                if (!Directory.Exists(yearMonthPath))
                {
                    Directory.CreateDirectory(yearMonthPath);
                }

                // Убираем символ рубля из названия компании
                string safeCompanyName = RemoveRubleSymbolFromText(companyName);
                // Убираем недопустимые символы
                safeCompanyName = RemoveInvalidFileNameChars(safeCompanyName);

                string fileName = $"Счет_{invoiceId}_{safeCompanyName}_{DateTime.Now:yyyyMMdd_HHmmss}.docx";

                return Path.Combine(yearMonthPath, fileName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка формирования пути History: {ex.Message}");
                return null;
            }
        }

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
        private byte[] RemoveRubleSymbolFromDocument(byte[] documentBytes)
        {
            try
            {
                using (MemoryStream stream = new MemoryStream(documentBytes))
                using (WordprocessingDocument doc = WordprocessingDocument.Open(stream, true))
                {
                    // Находим все текстовые элементы в документе
                    foreach (Text text in doc.MainDocumentPart.Document.Descendants<Text>())
                    {
                        // Заменяем символ рубля в тексте
                        text.Text = text.Text.Replace("₽", "").Replace(" ₽", "").Trim();
                    }

                    doc.Save();
                    return stream.ToArray();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка очистки документа: {ex.Message}");
                return documentBytes; // Возвращаем оригинал в случае ошибки
            }
        }

        private async Task<string> SaveWithUserDialogAsync(byte[] fileBytes, int invoiceId, string companyName)
        {
            try
            {
                return await Task.Run(() =>
                {
                    // Убираем символ рубля из названия компании перед созданием имени файла
                    string cleanCompanyName = RemoveRubleSymbolFromText(companyName);
                    cleanCompanyName = RemoveInvalidFileNameChars(cleanCompanyName);

                    var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                    {
                        Title = "Сохранить документ счета",
                        Filter = "Документ Word (*.docx)|*.docx|Все файлы (*.*)|*.*",
                        FileName = $"Счет_{invoiceId}_{cleanCompanyName}.docx", // Используем очищенное имя
                        DefaultExt = ".docx",
                        AddExtension = true
                    };

                    bool? result = saveFileDialog.ShowDialog();

                    if (result == true && !string.IsNullOrEmpty(saveFileDialog.FileName))
                    {
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

        private async Task SaveToFileAsync(byte[] fileBytes, string filePath)
        {
            try
            {
                if (fileBytes == null || string.IsNullOrEmpty(filePath))
                    return;

                string directory = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await Task.Run(() => File.WriteAllBytes(filePath, fileBytes));
                Console.WriteLine($"✅ Сохранено: {filePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка сохранения в {filePath}: {ex.Message}");
            }
        }

        private string RemoveInvalidFileNameChars(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return "БезНазвания";

            string invalidChars = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());

            foreach (char c in invalidChars)
            {
                fileName = fileName.Replace(c.ToString(), "");
            }

            if (fileName.Length > 50)
            {
                fileName = fileName.Substring(0, 50);
            }

            return fileName.Trim();
        }

        private void OpenFolderInExplorer(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    string directory = Path.GetDirectoryName(filePath);
                    Process.Start("explorer.exe", $"/select,\"{filePath}\"");
                }
                else if (Directory.Exists(Path.GetDirectoryName(filePath)))
                {
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
            //UpdateStatus("❌ Ошибка создания счета", "❌");
            //MessageBox.Show("Не удалось создать счет.\nПроверьте консоль для деталей.",
            //    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void HandleException(Exception ex)
        {
            UpdateStatus($"❌ Ошибка: {ex.Message}", "❌");
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
                UpdateStatus("Загрузка компаний...", "⏳");
                btnCreateNewCompany.IsEnabled = false;
                btnRefreshCompanies.IsEnabled = false;

                var allCompanies = await _bitrixService.GetAllCompanies();
                var myCompanies = await _bitrixService.GetMyCompanies();

                Companies.Clear();
                MyCompanies.Clear();
                _allCompanies.Clear();

                var myCompanyIds = myCompanies.Select(c => c.Id).ToHashSet();

                foreach (var company in allCompanies)
                {
                    if (!myCompanyIds.Contains(company.Id))
                    {
                        Companies.Add(company);
                        _allCompanies.Add(company);
                    }
                }

                foreach (var company in myCompanies)
                {
                    MyCompanies.Add(company);
                }

                if (Companies.Count > 0)
                {
                    SelectedCompany = Companies[0];
                }

                if (MyCompanies.Count > 0)
                {
                    SelectedMyCompany = MyCompanies[0];
                }

                UpdateStatus($"Загружено компаний: {Companies.Count}", "✅");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Ошибка: {ex.Message}", "❌");
                MessageBox.Show($"Ошибка загрузки компаний: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnCreateNewCompany.IsEnabled = true;
                btnRefreshCompanies.IsEnabled = true;
            }
        }

        private void btnAddCustomPayment_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Создаем простое окно для ввода способа оплаты
                var dialog = new CustomPaymentDialog();

                // Убираем Owner и просто центрируем на экране
                dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;

                // Можно также установить Topmost, если нужно
                // dialog.Topmost = true;

                bool? result = dialog.ShowDialog();

                if (result == true && !string.IsNullOrEmpty(dialog.PaymentMethodName))
                {
                    string newMethod = dialog.PaymentMethodName.Trim();

                    // Проверяем, нет ли уже такого способа оплаты
                    if (PaymentMethods.Any(p => p.Name == newMethod || p.Value == newMethod))
                    {
                        MessageBox.Show("Такой способ оплаты уже есть в списке!",
                            "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // Просто добавляем новый способ оплаты в коллекцию
                    var newPaymentMethod = new PaymentMethod
                    {
                        Name = newMethod,
                        Value = newMethod
                    };

                    PaymentMethods.Add(newPaymentMethod);

                    // Автоматически выбираем только что добавленный способ
                    SelectedPaymentMethod = newPaymentMethod;

                    MessageBox.Show($"Добавлен новый способ оплаты: {newMethod}",
                        "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Аналогично для создания компании
        private async void btnCreateNewCompany_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var createCompanyWindow = new CreateCompany(_bitrixService);

                // Убираем Owner
                createCompanyWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;

                var result = createCompanyWindow.ShowDialog();

                if (result == true && createCompanyWindow.CreatedCompanyId > 0)
                {
                    int newCompanyId = createCompanyWindow.CreatedCompanyId;
                    UpdateStatus($"Компания создана! ID: {newCompanyId}. Обновление списка...", "✅");

                    await Task.Delay(1000);
                    await RefreshCompaniesList();
                    await SelectNewCompany(newCompanyId);
                }
                else if (result == false)
                {
                    UpdateStatus("Создание компании отменено", "⚠️");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Ошибка: {ex.Message}", "❌");
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
                UpdateStatus("Обновление списка компаний...", "⏳");

                var currentSelectedId = SelectedCompany?.Id;

                var allCompanies = await _bitrixService.GetAllCompanies();
                var myCompanies = await _bitrixService.GetMyCompanies();

                Companies.Clear();
                MyCompanies.Clear();
                _allCompanies.Clear();

                var myCompanyIds = myCompanies.Select(c => c.Id).ToHashSet();

                foreach (var company in allCompanies)
                {
                    if (!myCompanyIds.Contains(company.Id))
                    {
                        Companies.Add(company);
                        _allCompanies.Add(company);
                    }
                }

                foreach (var company in myCompanies)
                {
                    MyCompanies.Add(company);
                }

                if (currentSelectedId.HasValue)
                {
                    var companyToSelect = Companies.FirstOrDefault(c => c.Id == currentSelectedId.Value);
                    SelectedCompany = companyToSelect ?? Companies.FirstOrDefault();
                }
                else if (Companies.Count > 0)
                {
                    SelectedCompany = Companies[0];
                }

                UpdateStatus($"Список обновлен. Компаний: {Companies.Count}", "✅");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Ошибка обновления: {ex.Message}", "❌");
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
                for (int i = 0; i < 3; i++)
                {
                    await Task.Delay(1000);
                    var companyToSelect = Companies.FirstOrDefault(c => c.Id == companyId);
                    if (companyToSelect != null)
                    {
                        SelectedCompany = companyToSelect;
                        UpdateStatus($"Выбрана новая компания: {companyToSelect.Title}", "✅");
                        return;
                    }
                }

                UpdateStatus("Новая компания пока не появилась в списке. Обновите список через некоторое время.", "⚠️");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Ошибка выбора компании: {ex.Message}", "❌");
            }
        }

        private void DisplayCompanyDetails(Company company)
        {
            var details = new System.Text.StringBuilder();
            details.AppendLine($"Компания: {company.Title ?? "Без названия"}");
            details.AppendLine($"ID: {company.Id}");
            details.AppendLine($"Ответственный: ID {company.AssignedById}");
            details.AppendLine($"Создана: {company.CreatedTime:dd.MM.yyyy}");

            CompanyDetails = details.ToString();
            LoadCompanyContacts(company.Id);
        }

        private async void LoadCompanyContacts(int companyId)
        {
            try
            {
                CompanyContacts.Clear();

                // TODO: Реализовать метод для получения контактов компании
                // Временная заглушка
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
            var details = new System.Text.StringBuilder();
            details.AppendLine($"Компания: {company.Title ?? "Без названия"}");
            details.AppendLine($"ID: {company.Id}");
            details.AppendLine($"Ответственный: ID {company.AssignedById}");
            details.AppendLine($"Создана: {company.CreatedTime:dd.MM.yyyy}");

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
                btnFinish.IsEnabled = false;
                await CreateInvoiceAsync();

                MessageBox.Show($"Счет успешно создан для:\n" +
                              $"Клиент: {SelectedCompany.Title}\n" +
                              $"Ваша компания: {SelectedMyCompany.Title}\n" +
                              $"Товаров: {InvoiceItems.Count}\n" +
                              $"Итого: {GrandTotal:#,##0.00} ₽",
                              "Счет создан", MessageBoxButton.OK, MessageBoxImage.Information);

                var dombPage = new DombPage();
                NavigationService.Navigate(dombPage);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при создании счета: {ex.Message}",
                              "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
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

        private void DatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {

                  if (sender is DatePicker datePicker)
            {
                // Проверяем, выбрана ли дата
                if (datePicker.SelectedDate.HasValue)
                {
                    // Получаем дату как DateTime
                    inviteTime = datePicker.SelectedDate.Value;


                }
                else
                {
                    Console.WriteLine("Дата не выбрана (null)");
                }
            }
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
    }
}

// Очень простое диалоговое окно для ввода способа оплаты
public class CustomPaymentDialog : Window
{
    public string PaymentMethodName { get; private set; }

    public CustomPaymentDialog()
    {
        // Настройки окна
        Title = "Добавить свой способ оплаты";
        Width = 400;
        Height = 200;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;

        // Создаем содержимое
        var grid = new Grid();
        grid.Margin = new Thickness(15);

        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Текстовое поле
        var textBox = new TextBox
        {
            Name = "txtPaymentMethod",
            FontSize = 14,
            Margin = new Thickness(0, 0, 0, 10),
            VerticalAlignment = VerticalAlignment.Top,
            Height = 80,
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        // Подсказка
        var placeholder = new TextBlock
        {
            Text = "Введите ваш способ оплаты...",
            Foreground = Brushes.Gray,
            FontStyle = FontStyles.Italic,
            Margin = new Thickness(5, 5, 0, 0),
            Visibility = Visibility.Visible
        };

        textBox.TextChanged += (s, e) =>
        {
            placeholder.Visibility = string.IsNullOrEmpty(textBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        };

        var textBoxContainer = new Grid();
        textBoxContainer.Children.Add(placeholder);
        textBoxContainer.Children.Add(textBox);

        Grid.SetRow(textBoxContainer, 0);
        grid.Children.Add(textBoxContainer);

        // Кнопки
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var btnOk = new Button
        {
            Content = "Добавить",
            Width = 100,
            Height = 35,
            Margin = new Thickness(0, 0, 10, 0),
            Background = Brushes.Orange,
            Foreground = Brushes.White,
            FontWeight = FontWeights.Bold
        };

        var btnCancel = new Button
        {
            Content = "Отмена",
            Width = 100,
            Height = 35,
            Background = Brushes.LightGray,
            Foreground = Brushes.Black
        };

        btnOk.Click += (s, e) =>
        {
            if (!string.IsNullOrWhiteSpace(textBox.Text))
            {
                PaymentMethodName = textBox.Text.Trim();
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("Введите способ оплаты!",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        };

        btnCancel.Click += (s, e) =>
        {
            DialogResult = false;
            Close();
        };

        buttonPanel.Children.Add(btnOk);
        buttonPanel.Children.Add(btnCancel);

        Grid.SetRow(buttonPanel, 1);
        grid.Children.Add(buttonPanel);

        Content = grid;

        // Фокус на текстовом поле при загрузке
        Loaded += (s, e) => textBox.Focus();
    }
}