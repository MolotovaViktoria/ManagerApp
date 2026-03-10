using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using ManagerApp.Classes.Setting;
using ManagerApp.Data.GetInfo;
using ManagerApp.Data.ScharedData;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
using static CustomPaymentDialog;
using static ManagerApp.Data.GetInfo.BitrixService;
namespace ManagerApp.Pages
{
    /// <summary>
    /// Логика взаимодействия для InvoicionCreatePage.xaml
    /// </summary>
    public partial class InvoicionCreatePage : Page, INotifyPropertyChanged
    {
        private DateTime? inviteTime; // nullable DateTime
        DateTime invoiceDate;


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

            // Инициализация шаблона
            SelectedTemplateName = "СПК (шаблон 32)";
        }
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
        private string _selectedTemplateName;
        public string SelectedTemplateName
        {
            get => _selectedTemplateName;
            set
            {
                _selectedTemplateName = value;
                OnPropertyChanged(nameof(SelectedTemplateName));
                OnPropertyChanged(nameof(SelectedTemplateColor));
            }
        }

        public Brush SelectedTemplateColor
        {
            get => SelectedTemplateName == "СПК (шаблон 32)" ? Brushes.Green : Brushes.Orange;
        }

        private void DisplayMyCompanyDetails(MyLocalCompany company)
        {
            if (company == null) return;

            var details = new System.Text.StringBuilder();
            details.AppendLine($"Компания: {company.DisplayName}");
            details.AppendLine($"Шаблон документа ID: {company.TemplateId}");

            MyCompanyDetails = details.ToString();
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
        public ObservableCollection<MyLocalCompany> MyLocalCompanies { get; set; }
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
        private MyLocalCompany _selectedMyLocalCompany;
        public MyLocalCompany SelectedMyLocalCompany
        {
            get => _selectedMyLocalCompany;
            set
            {
                _selectedMyLocalCompany = value;
                OnPropertyChanged(nameof(SelectedMyLocalCompany));
                if (value != null)
                {
                    DisplayMyCompanyDetails(value);
                    Console.WriteLine($"✅ Выбрана компания: {value.DisplayName}, шаблон ID: {value.TemplateId}");
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

            // ОПРЕДЕЛЯЕМ ID ШАБЛОНА В ЗАВИСИМОСТИ ОТ ВЫБРАННОЙ КОМПАНИИ
            int templateId = GetTemplateIdForMyCompany();

            // ГЕНЕРАЦИЯ ДОКУМЕНТА WORD СРАЗУ ПОСЛЕ СОЗДАНИЯ СЧЕТА
            await GenerateAndDownloadDocumentAsync(invoiceId, templateId, foundProducts, notFoundProducts);
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

        private async Task GenerateAndDownloadDocumentAsync(int invoiceId, int templateId,
      List<(string Name, int Id)> foundProducts, List<string> notFoundProducts)
        {
            try
            {
                string companyType = templateId == 32 ? "СПК" : "НВР";
                UpdateStatus($"Генерация документа Word (шаблон {companyType})...", "⏳");

                // ГЕНЕРАЦИЯ И СКАЧИВАНИЕ ДОКУМЕНТА с указанием шаблона
                string downloadUrl = await _bitrixService.GenerateInvoiceDocument(invoiceId, templateId, "docx");

                string successMessage = $"Счет #{invoiceId} создан!\n" +
                                       $"Товаров: {foundProducts.Count}\n" +
                                       $"Итого: {GrandTotal:#,##0.00} ₽";

                if (!string.IsNullOrEmpty(downloadUrl))
                {
                    successMessage += $"\n\n📄 Документ Word готов (шаблон {companyType})!";

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

                UpdateStatus($"Документ счета #{invoiceId} (шаблон {companyType}) готов", "✅");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка генерации документа: {ex.Message}");
                UpdateStatus($"Ошибка генерации документа: {ex.Message}", "❌");
                MessageBox.Show($"Счет создан, но документ не сгенерирован:\n{ex.Message}",
                    "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// Определяет ID шаблона в зависимости от выбранной "Моей компании"
        /// </summary>
        /// <returns>32 для ООО "СПК", 34 для ООО ТД МК "НВР"</returns>
        /// <summary>
        /// Определяет ID шаблона в зависимости от выбранной компании
        /// </summary>
        private int GetTemplateIdForMyCompany()
        {
            if (SelectedMyLocalCompany == null)
            {
                Console.WriteLine("⚠️ Компания не выбрана, используем шаблон по умолчанию 32 (СПК)");
                return 32;
            }

            Console.WriteLine($"✅ Используем шаблон ID: {SelectedMyLocalCompany.TemplateId} для компании {SelectedMyLocalCompany.DisplayName}");
            return SelectedMyLocalCompany.TemplateId;
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
                    Total = item.Total,
                    MiasureId = item.MeasureId
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
                if (SelectedCompany == null || SelectedMyLocalCompany == null)
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

                // ОБРАБОТКА ДАТЫ
                DateTime invoiceDate;
                if (inviteTime == null || !inviteTime.HasValue)
                {
                    invoiceDate = DateTime.Today;
                    Console.WriteLine($"⚠️ Дата не выбрана, используем сегодняшнюю: {invoiceDate:dd.MM.yyyy}");
                }
                else
                {
                    invoiceDate = inviteTime.Value;
                    Console.WriteLine($"📅 Используем выбранную дату: {invoiceDate:dd.MM.yyyy}");
                }

                //ПОДТВЕРЖДЕНИЕ
               var confirmResult = MessageBox.Show(
                   $"Создание счета для {SelectedCompany.Title}?\n" +
                   $"Дата: {invoiceDate:dd.MM.yyyy}\n" +
                   $"Итого: {GrandTotal:#,##0.00} ₽", "",
                   MessageBoxButton.OK, MessageBoxImage.Information);

                //if (confirmResult != MessageBoxResult.Yes) return;

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

                // СОЗДАНИЕ СЧЕТА - передаем дату!
                int invoiceId = await CreateBitrixInvoiceAsync(invoiceProducts, invoiceDate);

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
                            MeasureId = item.MiasureId
                            
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

        private async Task<int> CreateBitrixInvoiceAsync(List<InvoiceProduct> invoiceProducts, DateTime invoiceDate)
        {
            UpdateStatus("Создание счета...", "⏳");
            Console.WriteLine($"📅 Дата для создания счета: {invoiceDate:dd.MM.yyyy}");

            // Получаем ID выбранного сотрудника из настроек
            int responsibleEmployeeId = IDSetting.GetSelectedEmployeeId();

            if (responsibleEmployeeId <= 0)
            {
                responsibleEmployeeId = 241;
                Console.WriteLine("⚠️ Ответственный сотрудник не выбран, используем 241");
            }
            else
            {
                Console.WriteLine($"✅ Ответственный сотрудник ID: {responsibleEmployeeId}");
            }

            // Используем переданную дату в заголовке
            string orderTopic = $"Счет для {SelectedCompany.Title} от {invoiceDate:dd.MM.yyyy}";

            // ВЫВОД НОВЫХ ПОЛЕЙ В КОНСОЛЬ ПЕРЕД СОЗДАНИЕМ
            Console.WriteLine("=== ПЕРЕДАВАЕМЫЕ ДАННЫЕ ДЛЯ СЧЕТА ===");
            Console.WriteLine($"1. Дата счета: {invoiceDate:dd.MM.yyyy}");
            Console.WriteLine($"2. Номер счета: {InvoiceNumber}");
            Console.WriteLine($"3. Клиент: {SelectedCompany.Title} (ID: {SelectedCompany.Id})");
            //Console.WriteLine($"4. Компания: {SelectedMyLocalCompany.Title} (ID: {SelectedMyLocalCompany.Id})");
            Console.WriteLine($"5. Адрес: {Address}");
            Console.WriteLine($"6. Дней доставки: {DeliveryDays}");
            Console.WriteLine($"7. Способ оплаты: {(SelectedPaymentMethod?.Name ?? "Не выбран")}");
            Console.WriteLine($"8. Ответственный сотрудник ID: {responsibleEmployeeId}");
            Console.WriteLine("=====================================");

            if (string.IsNullOrEmpty(Address))
            {
                Address = "самовывоз со склада Поставщика г. Екатеринбург, ул. Мартовская, д.8: с 9 ч. 00 мин. до 18 ч. 00 мин. по местному времени в рабочие дни";
            }

            return await _bitrixService.CreateSmartInvoice(
                invoiceDate, // передаем дату
                SelectedCompany.Id,
                6,
                orderTopic,
                invoiceProducts.Where(p => p.ProductId > 0).ToList(),
                InvoiceNumber,
                Address,
                DeliveryDays,
                SelectedPaymentMethod?.Value ?? "Не указано",
                responsibleEmployeeId
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
                await SaveToFileAsync(cleanedBytes, appHistoryPath);
                savedPaths.Add(appHistoryPath);

                // 2. Сохраняем в папку Downloads (ОЧИЩЕННЫЙ) И СРАЗУ ОТКРЫВАЕМ
                string downloadsPath = GetDownloadsFilePath(invoiceId, companyName);
                await SaveToFileAsync(cleanedBytes, downloadsPath);
                savedPaths.Add(downloadsPath);

                // ОТКРЫВАЕМ ФАЙЛ СРАЗУ ПОСЛЕ СОХРАНЕНИЯ
                OpenFile(downloadsPath);



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

        private void OpenFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = filePath,
                        UseShellExecute = true
                    });
                    Console.WriteLine($"📂 Открыт файл: {filePath}");
                }
                else
                {
                    Console.WriteLine($"❌ Файл не найден: {filePath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка при открытии файла: {ex.Message}");
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
            MyLocalCompanies = new ObservableCollection<MyLocalCompany>(); // Изменено
            _allCompanies = new List<Company>();
            CompanyContacts = new ObservableCollection<ContactInfo>();
            InvoiceItems = new ObservableCollection<InvoiceItemViewModel>();
            DataContext = this;

            // Добавляем две компании вручную
            MyLocalCompanies.Add(new MyLocalCompany
            {
                DisplayName = "ООО \"СПК\"",
                ShortName = "СПК",
                TemplateId = 32
            });

            MyLocalCompanies.Add(new MyLocalCompany
            {
                DisplayName = "ООО ТД МК «НВР»",
                ShortName = "НВР",
                TemplateId = 34
            });

            // Выбираем первую по умолчанию
            if (MyLocalCompanies.Count > 0)
            {
                SelectedMyLocalCompany = MyLocalCompanies[0];
            }
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
                //SelectedMyLocalCompany.Clear();
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
                //MyCompanies.Clear();
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

                //foreach (var company in myCompanies)
                //{
                //    MyCompanies.Add(company);
                //}

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


        private async Task<bool> CheckInvoiceExistsAsync(string invoiceNumber)
        {
            Console.WriteLine($"=== НАЧАЛО ПРОВЕРКИ НОМЕРА '{invoiceNumber}' ===");

            BitrixService bitrixService = new BitrixService();

            try
            {
                // Пробуем разные варианты фильтра
                var filtersToTry = new List<Dictionary<string, object>>
        {
            new Dictionary<string, object> { ["ACCOUNT_NUMBER"] = invoiceNumber },
            new Dictionary<string, object> { ["=ACCOUNT_NUMBER"] = invoiceNumber }
        };

                foreach (var filter in filtersToTry)
                {
                    Console.WriteLine($"Пробуем фильтр: {JsonConvert.SerializeObject(filter)}");

                    var result = await bitrixService.CallMethodAsync("crm.item.list", new
                    {
                        entityTypeId = 31,
                        filter = filter,
                        select = new[] { "id", "ACCOUNT_NUMBER", "TITLE" }
                    });

                    Console.WriteLine($"Ответ Bitrix: {result.ToString(Newtonsoft.Json.Formatting.None)}");

                    JArray items = null;
                    if (result["result"]?["items"] is JArray r1)
                        items = r1;
                    else if (result["items"] is JArray r2)
                        items = r2;

                    if (items != null && items.Count > 0)
                    {
                        Console.WriteLine($"УСПЕХ! Найден счет: ID={items[0]["id"]}, Номер={items[0]["accountNumber"]}");
                        return true;
                    }
                }

                Console.WriteLine($"Номер '{invoiceNumber}' не найден в системе");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ОШИБКА: {ex.Message}");
                return false;
            }
            finally
            {
                Console.WriteLine($"=== КОНЕЦ ПРОВЕРКИ НОМЕРА '{invoiceNumber}' ===");
            }
        }
        private async void btnFinish_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedCompany == null || SelectedMyLocalCompany == null)
            {
                MessageBox.Show("Выберите компании!", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

          

            try
            {
                btnFinish.IsEnabled = false;
                Console.WriteLine("Начинаем создание счета...");
                await CreateInvoiceAsync();

                //MessageBox.Show($"Счет успешно создан!\n" +
                //              $"Номер: {InvoiceNumber}\n" +
                //              $"Клиент: {SelectedCompany.Title}\n" +
                //              $"Итого: {GrandTotal:#,##0.00} ₽",
                //              "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                NavigationService.Navigate(new DombPage());
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
                if (SelectedCompany == null || SelectedMyLocalCompany == null || !InvoiceItems.Any())
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
                //previewText.AppendLine($"Компания: {SelectedMyCompany.Title}");
                //previewText.AppendLine($"ID: {SelectedMyCompany.Id}");
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

        private string _miasureId;

        public string MiasureId
        {
            get => _miasureId;
            set
            {
                _miasureId = value;
                OnPropertyChanged(nameof(MiasureId));
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


    public class MyLocalCompany
    {
        public string DisplayName { get; set; }
        public string ShortName { get; set; }
        public int TemplateId { get; set; }

        // Для отображения в ComboBox
        public override string ToString()
        {
            return DisplayName;
        }
    }
}