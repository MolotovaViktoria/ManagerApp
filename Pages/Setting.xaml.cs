using ManagerApp.Classes.Read.ReadPicture;
using ManagerApp.Classes.Setting;
using ManagerApp.Data.GetInfo;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ManagerApp.Pages
{
    public partial class Setting : Page, INotifyPropertyChanged
    {
        private const string SettingsFileName = "settings.txt";
        private const string OCRSettingsFileName = "fileresurse.txt"; // Добавьте эту константу
        private const string DefaultVAT = "20";

        private string _vat = DefaultVAT;
        private ObservableCollection<BitrixUser> _employees = new ObservableCollection<BitrixUser>();
        private BitrixUser _selectedEmployee;
        private string _ocrPath = "";



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
            LoadOCRPathFromFile(); // Загружаем путь при создании
        }

       
        private void SetButtonsEnabled(bool enabled)
        {
            btnRefreshCache.IsEnabled = enabled;
            btnReadManual.IsEnabled = enabled;
            btnSave.IsEnabled = enabled;
            btnClose.IsEnabled = enabled;
        }

        private void btnRefreshCache_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Вы уверены, что хотите обновить кеш?\n\n" +
                "Приложение приостановит работу на время обновления.\n" +
                "Это может занять несколько минут в зависимости от объема данных.\n\n" +
                "Продолжить?",
                "Обновление кеша",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            // Отключаем кнопку
            btnRefreshCache.IsEnabled = false;

            // Создаем окно ожидания
            var waitingWindow = new WaitingWindow("Обновление кеша", "Начало обновления...");
            waitingWindow.Owner = Window.GetWindow(this);

            // Используем BackgroundWorker
            var worker = new System.ComponentModel.BackgroundWorker
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = false
            };

            worker.DoWork += (s, args) =>
            {
                try
                {
                    worker.ReportProgress(0, "Подготовка к обновлению...");
                    System.Threading.Thread.Sleep(500);

                    worker.ReportProgress(10, "Загрузка данных из Bitrix...");

                    // Синхронный вызов вместо асинхронного
                    var task = Task.Run(async () => await BitrixCache.ForceUpdateCacheAsync());
                    task.Wait();

                    worker.ReportProgress(90, "Завершение обновления...");
                    System.Threading.Thread.Sleep(500);

                    var cacheStats = BitrixCache.GetCacheStats();
                    args.Result = new CacheUpdateResult
                    {
                        Success = true,
                        Message = cacheStats,
                        Error = null
                    };
                }
                catch (Exception ex)
                {
                    args.Result = new CacheUpdateResult
                    {
                        Success = false,
                        Message = ex.Message,
                        Error = ex
                    };
                }
            };

            worker.ProgressChanged += (s, args) =>
            {
                waitingWindow.UpdateMessage(args.UserState?.ToString() ?? "Обработка...");
            };

            worker.RunWorkerCompleted += (s, args) =>
            {
                // Закрываем окно ожидания
                waitingWindow.Close();

                // Включаем кнопку
                btnRefreshCache.IsEnabled = true;

                if (args.Result is CacheUpdateResult resultData)
                {
                    if (resultData.Success)
                    {
                        MessageBox.Show(
                            $"✅ Кеш успешно обновлен!\n\n" +
                            $"{resultData.Message}\n\n" +
                            "Приложение продолжит работу с обновленными данными.",
                            "Обновление завершено",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show(
                            $"❌ Ошибка при обновлении кеша:\n{resultData.Message}\n\n" +
                            "Приложение продолжит работу со старым кешем.",
                            "Ошибка обновления",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
                else
                {
                    MessageBox.Show(
                        "❌ Неизвестная ошибка при обновлении кеша",
                        "Ошибка обновления",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            };

            // Показываем окно и запускаем worker
            waitingWindow.Show();
            worker.RunWorkerAsync();
        }

        // Класс для хранения результата обновления кеша
        private class CacheUpdateResult
        {
            public bool Success { get; set; }
            public string Message { get; set; }
            public Exception Error { get; set; }
        }


        private void btnReadManual_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Получаем путь к исполняемому файлу
                string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string exeDir = Path.GetDirectoryName(exePath);

                // Путь к вашему PDF файлу
                string manualPath = Path.Combine(exeDir, "руководствоПользователя.pdf");

                // Проверяем существует ли файл
                if (File.Exists(manualPath))
                {
                    // Открываем файл
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = manualPath,
                        UseShellExecute = true
                    });
                }
                else
                {
                    // Если файл не найден, показываем где его нужно разместить
                    MessageBox.Show(
                        $"Файл руководства не найден.\n\n" +
                        $"Разместите файл 'руководствоПользователя.pdf' в папке:\n{exeDir}\n\n" +
                        $"Или перетащите его прямо в эту папку.",
                        "Руководство не найдено",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    // Можно дополнительно открыть папку для пользователя
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = exeDir,
                            UseShellExecute = true
                        });
                    }
                    catch
                    {
                        // Игнорируем ошибку открытия папки
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка открытия руководства: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CreateTemplateManual(string filePath)
        {
            try
            {
                string templateContent = @"РУКОВОДСТВО ПОЛЬЗОВАТЕЛЯ
Приложение для управления товарами

1. ОСНОВНЫЕ ВОЗМОЖНОСТИ
   - Загрузка и обновление данных из Bitrix24
   - Управление товарами и категориями
   - Настройка параметров приложения
   - Работа с чеками и документами

2. НАСТРОЙКИ
   2.1. НДС
     - Установите процент НДС для расчетов
     - Значение сохраняется автоматически

   2.2. Сотрудник
     - Выберите текущего сотрудника из списка
     - ID сохраняется в файл setting_id_sotrudmik.txt

   2.3. OCR (оптическое распознавание текста)
     - Укажите путь к языковому пакету rus.traineddata
     - Для работы с распознаванием текста из изображений

3. КЕШИРОВАНИЕ
   - Данные кешируются для ускорения работы
   - Кеш автоматически обновляется раз в 24 часа
   - Можно принудительно обновить через кнопку ""Обновить кеш""

4. КЛАВИШИ БЫСТРОГО ДОСТУПА
   - F1 - Справка
   - Ctrl+S - Сохранить
   - Ctrl+Q - Выход

5. УСТРАНЕНИЕ НЕПОЛАДОК
   5.1. Проблемы с загрузкой данных
     - Проверьте подключение к интернету
     - Обновите кеш вручную

   5.2. OCR не работает
     - Убедитесь, что файл rus.traineddata находится в указанной папке
     - Проверьте путь в настройках

Для дополнительной помощи обратитесь к администратору системы.";

                // Создаем текстовый файл как временное решение
                string txtFilePath = Path.ChangeExtension(filePath, ".txt");
                File.WriteAllText(txtFilePath, templateContent, Encoding.UTF8);

                MessageBox.Show(
                    $"Шаблон руководства создан:\n{txtFilePath}\n\n" +
                    "Вы можете открыть его в любом текстовом редакторе.",
                    "Шаблон создан",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // Открываем созданный файл
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = txtFilePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка создания шаблона: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }



        //private void Page_Loaded(object sender, RoutedEventArgs e)
        //{
        //    LoadVATFromFile();
        //    LoadEmployees();
        //    CheckOCRFile(); // Проверяем OCR файл при загрузке
        //}
        private void btnFixOCR_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string exeDir = Path.GetDirectoryName(exePath);
                string targetFile = Path.Combine(exeDir, "rus.traineddata");

                // 1. Проверяем есть ли файл уже в папке с программой
                if (File.Exists(targetFile))
                {
                    MessageBox.Show(
                        $"Файл уже есть в папке программы:\n{targetFile}\n\n" +
                        $"Перезапустите программу.",
                        "Файл найден",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                // 2. Ищем файл в AppData
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string[] files = Directory.GetFiles(localAppData, "rus.traineddata", SearchOption.AllDirectories);

                if (files.Length > 0)
                {
                    string sourceFile = files[0];

                    // Копируем
                    File.Copy(sourceFile, targetFile, false);

                    MessageBox.Show(
                        $"✅ Файл скопирован!\n\n" +
                        $"Из: {sourceFile}\n" +
                        $"В: {targetFile}\n\n" +
                        $"Теперь OCR будет работать!",
                        "Успех",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    // Обновляем статус
                    //CheckOCRFile();
                }
                else
                {
                    MessageBox.Show(
                        $"Файл не найден в AppData.\n\n" +
                        $"1. Скачайте файл по кнопке 'Скачать'\n" +
                        $"2. Или положите в папку: {targetFile}",
                        "Файл не найден",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadVATFromFile();
            LoadEmployees();

            // Загружаем сохраненный путь OCR из fileresurse.txt
            LoadOCRPathFromFile();
        }

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

        private void LoadOCRPathFromFile()
        {
            //try
            //{
            //    if (File.Exists(OCRSettingsFileName))
            //    {
            //        var lines = File.ReadAllLines(OCRSettingsFileName);
            //        foreach (var line in lines)
            //        {
            //            if (line.StartsWith("OCRPATH="))
            //            {
            //                var path = line.Substring(8).Trim();
            //                if (!string.IsNullOrEmpty(path))
            //                {
            //                    OCRPath = path;

            //                    // Если путь существует, сразу применяем его
            //                    if (Directory.Exists(path))
            //                    {
            //                        ApplyOCRPath(path);
            //                    }
            //                }
            //            }
            //        }
            //    }
            //}
            //catch (Exception ex)
            //{
            //    MessageBox.Show($"Ошибка загрузки пути OCR: {ex.Message}", "Ошибка",
            //        MessageBoxButton.OK, MessageBoxImage.Warning);
            //}
        }
        // Обновите метод LoadOCRPathFromFile:
        private void SaveOCRPathToFile(string path)
        {
            //try
            //{
            //    var lines = new List<string>();

            //    if (File.Exists(OCRSettingsFileName))
            //    {
            //        lines = File.ReadAllLines(OCRSettingsFileName).ToList();
            //    }

            //    // Удаляем старую запись OCRPATH если есть
            //    lines.RemoveAll(line => line.StartsWith("OCRPATH="));

            //    // Добавляем новую запись
            //    lines.Add($"OCRPATH={path}");

            //    File.WriteAllLines(OCRSettingsFileName, lines);
            //    OCRPath = path;

            //    MessageBox.Show($"Путь к OCR сохранен в файл: {OCRSettingsFileName}", "Сохранено",
            //        MessageBoxButton.OK, MessageBoxImage.Information);
            //}
            //catch (Exception ex)
            //{
            //    MessageBox.Show($"Ошибка сохранения пути OCR: {ex.Message}", "Ошибка",
            //        MessageBoxButton.OK, MessageBoxImage.Error);
            //}
        }

        // Обновите метод SaveOCRPathToFile:
        private void ApplyOCRPath(string path)
        {
            try
            {
                // Проверяем наличие файла rus.traineddata в указанном пути
                string tessdataPath = path;
                string[] possibleFiles = {
                Path.Combine(path, "rus.traineddata"),
                Path.Combine(path, "tessdata", "rus.traineddata")
            };

                bool fileFound = false;
                string foundFile = "";

                foreach (var file in possibleFiles)
                {
                    if (File.Exists(file))
                    {
                        fileFound = true;
                        foundFile = file;
                        tessdataPath = Path.GetDirectoryName(file);
                        break;
                    }
                }

                if (fileFound)
                {
                    // Устанавливаем путь через статический метод SimpleOCRProcessor
                    //bool success = SimpleOCRProcessor.SetCustomPath(tessdataPath);

                    //if (success)
                    //{
                    //    MessageBox.Show($"Путь к OCR успешно применен!\nФайл найден: {foundFile}", "Успех",
                    //        MessageBoxButton.OK, MessageBoxImage.Information);
                    //}
                    //else
                    //{
                    //    MessageBox.Show($"Не удалось установить путь OCR", "Ошибка",
                    //        MessageBoxButton.OK, MessageBoxImage.Warning);
                    //}
                }
                else
                {
                    MessageBox.Show($"Файл rus.traineddata не найден в указанном пути!\nПроверьте правильность пути.", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка применения пути OCR: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        // Добавьте метод для сохранения в settings.txt (для обратной совместимости)
        private void SaveOCRPathToSettingsFile(string path)
        {
            try
            {
                var lines = new List<string>();

                if (File.Exists(SettingsFileName))
                {
                    lines = File.ReadAllLines(SettingsFileName).ToList();
                }

                // Удаляем старую запись OCRPATH если есть
                lines.RemoveAll(line => line.StartsWith("OCRPATH="));

                // Добавляем новую запись
                lines.Add($"OCRPATH={path}");

                File.WriteAllLines(SettingsFileName, lines);
            }
            catch (Exception ex)
            {
                // Не блокируем основной поток если ошибка в дополнительном файле
                Console.WriteLine($"Ошибка сохранения OCR пути в settings.txt: {ex.Message}");
            }
        }

        // Обновите метод ApplyOCRPath (добавьте вызов SaveOCRPathToFile):


        // Обновите метод btnSave_Click:
        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            SaveVATToFile();

            // Сохраняем путь OCR если он указан
            if (!string.IsNullOrEmpty(OCRPath))
            {
                if (Directory.Exists(OCRPath))
                {
                    SaveOCRPathToFile(OCRPath);
                    ApplyOCRPath(OCRPath);
                }
                else
                {
                    MessageBoxResult result = MessageBox.Show(
                        $"Указанный путь не существует:\n{OCRPath}\n\n" +
                        $"Все равно сохранить путь в файл {OCRSettingsFileName}?",
                        "Путь не существует",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Yes)
                    {
                        SaveOCRPathToFile(OCRPath);
                    }
                }
            }

            MessageBox.Show("Настройки успешно сохранены!", "Успех",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Добавьте статический метод для получения пути OCR из других частей программы:
        public static string GetOCRPath()
        {
            try
            {
                string[] filesToCheck = { "fileresurse.txt", "settings.txt" };

                foreach (var fileName in filesToCheck)
                {
                    if (File.Exists(fileName))
                    {
                        var lines = File.ReadAllLines(fileName);
                        foreach (var line in lines)
                        {
                            if (line.StartsWith("OCRPATH="))
                            {
                                var path = line.Substring(8).Trim();
                                if (!string.IsNullOrEmpty(path))
                                {
                                    return path;
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // Игнорируем ошибки
            }

            return null;
        }

        // Обновите статический метод GetVAT для работы только с VAT:
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

      
        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService.CanGoBack)
            {
                NavigationService.GoBack();
            }
        }



        //private void CheckOCRFile()
        //{
        //    try
        //    {
        //        Console.WriteLine("\n=== ПРОВЕРКА OCR ФАЙЛА (ТОТ ЖЕ ПУТЬ ЧТО И В ImageTextReader) ===");

        //        // Получаем тот же самый путь что используется в ImageTextReader
        //        string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
        //        string exeDir = Path.GetDirectoryName(exePath);

        //        // ТОЧНО ТЕ ЖЕ ПУТИ что в ImageTextReader.FindRussianFile()!
        //        string[] possiblePaths = {
        //    Path.Combine(exeDir, "rus.traineddata"),
        //    Path.Combine(exeDir, "tessdata", "rus.traineddata"),
        //    Path.Combine(exeDir, "Image", "rus.traineddata")
        //};

        //        Console.WriteLine($"Проверяем пути:");
        //        foreach (var path in possiblePaths)
        //        {
        //            Console.WriteLine($"  - {path}");
        //        }

        //        bool found = false;
        //        string foundPath = "";

        //        foreach (var path in possiblePaths)
        //        {
        //            if (File.Exists(path))
        //            {
        //                found = true;
        //                foundPath = path;
        //                break;
        //            }
        //        }

        //        if (found)
        //        {
        //            FileInfo info = new FileInfo(foundPath);
        //            txtOCRStatus.Text = $"✅ Файл найден:\n{foundPath}\nРазмер: {info.Length / 1024 / 1024} МБ";
        //            //txtOCRStatus.Foreground = Brushes.Green;

        //            // Сохраняем путь в общие настройки
        //            OCRSettings.SetPath(Path.GetDirectoryName(foundPath));
        //        }
        //        else
        //        {
        //            txtOCRStatus.Text = $"❌ Файл не найден\nРазместите rus.traineddata в:\n{exeDir}\\rus.traineddata";
        //            //txtOCRStatus.Foreground = Brushes.Red;
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        txtOCRStatus.Text = $"Ошибка проверки: {ex.Message}";
        //        //txtOCRStatus.Foreground = Brushes.Orange;
        //    }
        //}
        //private void btnUseThisPath_Click(object sender, RoutedEventArgs e)
        //{
        //    try
        //    {
        //        // Получаем путь из найденного файла
        //        string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
        //        string exeDir = Path.GetDirectoryName(exePath);

        //        string[] possiblePaths = {
        //    Path.Combine(exeDir, "rus.traineddata"),
        //    Path.Combine(exeDir, "tessdata", "rus.traineddata"),
        //    Path.Combine(exeDir, "Image", "rus.traineddata")
        //};

        //        foreach (var path in possiblePaths)
        //        {
        //            if (File.Exists(path))
        //            {
        //                string folder = Path.GetDirectoryName(path);

        //                // Сохраняем путь
        //                OCRSettings.SetPath(folder);

        //                MessageBox.Show(
        //                    $"Путь сохранен: {folder}\n\n" +
        //                    $"Перезапустите программу для применения.",
        //                    "Успех",
        //                    MessageBoxButton.OK,
        //                    MessageBoxImage.Information);

        //                CheckOCRFile();
        //                return;
        //            }
        //        }

        //        MessageBox.Show("Сначала найдите файл с помощью кнопки 'Проверить'",
        //            "Файл не найден",
        //            MessageBoxButton.OK,
        //            MessageBoxImage.Warning);
        //    }
        //    catch (Exception ex)
        //    {
        //        MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
        //            MessageBoxButton.OK, MessageBoxImage.Error);
        //    }
        //}
        //private void btnCheckOCR_Click(object sender, RoutedEventArgs e)
        //{
        //    CheckOCRFile();
        //}

        //private async void btnDownloadOCR_Click(object sender, RoutedEventArgs e)
        //{
        //    try
        //    {
        //        MessageBoxResult result = MessageBox.Show(
        //            "Скачать языковой файл rus.traineddata (≈40 МБ)?\n" +
        //            "После скачивания файл будет помещен в папку с программой.",
        //            "Скачивание файла OCR",
        //            MessageBoxButton.YesNo,
        //            MessageBoxImage.Question);

        //        if (result != MessageBoxResult.Yes)
        //            return;

        //        // Показываем прогресс
        //        btnDownloadOCR.Content = "Скачивание...";
        //        btnDownloadOCR.IsEnabled = false;

        //        // Создаем папку если нет
        //        string exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        //        string targetDir = Path.Combine(exeDir, "tessdata");
        //        Directory.CreateDirectory(targetDir);

        //        string targetFile = Path.Combine(targetDir, "rus.traineddata");
        //        string downloadUrl = "https://github.com/tesseract-ocr/tessdata/raw/main/rus.traineddata";

        //        // Скачиваем файл
        //        using (var client = new System.Net.WebClient())
        //        {
        //            // Обработка прогресса
        //            client.DownloadProgressChanged += (s, args) =>
        //            {
        //                Dispatcher.Invoke(() =>
        //                {
        //                    btnDownloadOCR.Content = $"Скачивание... {args.ProgressPercentage}%";
        //                });
        //            };

        //            await client.DownloadFileTaskAsync(new Uri(downloadUrl), targetFile);
        //        }

        //        MessageBox.Show($"Файл успешно скачан!\n{targetFile}\nПерезапустите программу для применения.",
        //            "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

        //        CheckOCRFile();
        //    }
        //    catch (System.Net.WebException)
        //    {
        //        MessageBox.Show("Ошибка подключения к интернету.\nСкачайте файл вручную:\n" +
        //                       "https://github.com/tesseract-ocr/tessdata/raw/main/rus.traineddata\n" +
        //                       "и разместите в папке с программой.",
        //            "Ошибка загрузки", MessageBoxButton.OK, MessageBoxImage.Error);
        //    }
        //    catch (Exception ex)
        //    {
        //        MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
        //            MessageBoxButton.OK, MessageBoxImage.Error);
        //    }
        //    finally
        //    {
        //        btnDownloadOCR.Content = "Скачать";
        //        btnDownloadOCR.IsEnabled = true;
        //    }
        //}
        // В начале класса добавьте поля и свойство:

        private string _ocrFileName = "fileresurse.txt";

        public string OCRPath
        {
            get { return _ocrPath; }
            set
            {
                if (_ocrPath != value)
                {
                    _ocrPath = value;
                    OnPropertyChanged(nameof(OCRPath));
                }
            }
        }

        // В конструкторе добавьте инициализацию:




   

    

        // Добавьте обработчики кнопок:
        private void btnBrowseOCR_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Используем OpenFileDialog с трюком для выбора папки
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    ValidateNames = false,
                    CheckFileExists = false,
                    CheckPathExists = true,
                    FileName = "Выберите папку",
                    Title = "Выберите папку с файлом rus.traineddata"
                };

                if (dialog.ShowDialog() == true)
                {
                    // Получаем путь к папке из выбранного файла
                    string selectedPath = Path.GetDirectoryName(dialog.FileName);

                    if (!string.IsNullOrEmpty(selectedPath))
                    {
                        OCRPath = selectedPath;

                        // Проверяем наличие файла
                        string[] possibleFiles = {
                    Path.Combine(OCRPath, "rus.traineddata"),
                    Path.Combine(OCRPath, "tessdata", "rus.traineddata")
                };

                        bool fileFound = false;
                        foreach (var file in possibleFiles)
                        {
                            if (File.Exists(file))
                            {
                                fileFound = true;
                                break;
                            }
                        }

                        if (!fileFound)
                        {
                            MessageBoxResult result = MessageBox.Show(
                                $"В выбранной папке не найден файл rus.traineddata.\n\n" +
                                $"Выберите папку tessdata или папку содержащую rus.traineddata.\n\n" +
                                $"Все равно сохранить этот путь?",
                                "Файл не найден",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Warning);

                            if (result != MessageBoxResult.Yes)
                            {
                                return;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка выбора папки: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        

        private void btnTestOCRPath_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(OCRPath))
            {
                MessageBox.Show("Сначала укажите путь к папке с OCR файлами", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!Directory.Exists(OCRPath))
            {
                MessageBox.Show($"Указанная папка не существует:\n{OCRPath}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Проверяем наличие файла
            string[] possibleFiles = {
        Path.Combine(OCRPath, "rus.traineddata"),
        Path.Combine(OCRPath, "tessdata", "rus.traineddata")
    };

            bool fileFound = false;
            string foundFile = "";

            foreach (var file in possibleFiles)
            {
                if (File.Exists(file))
                {
                    fileFound = true;
                    foundFile = file;
                    break;
                }
            }

            if (fileFound)
            {
                FileInfo info = new FileInfo(foundFile);
                MessageBox.Show($"✅ Файл найден!\n\n" +
                               $"Путь: {foundFile}\n" +
                               $"Размер: {info.Length / 1024 / 1024} МБ\n" +
                               $"Дата изменения: {info.LastWriteTime}",
                               "Проверка успешна",
                               MessageBoxButton.OK,
                               MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show($"❌ Файл rus.traineddata не найден!\n\n" +
                               $"Искали в:\n" +
                               $"{possibleFiles[0]}\n" +
                               $"{possibleFiles[1]}\n\n" +
                               $"Убедитесь что файл находится в одной из этих папок.",
                               "Файл не найден",
                               MessageBoxButton.OK,
                               MessageBoxImage.Error);
            }
        }

        // Обновите метод btnSave_Click:
      
        private void btnSelectOCRFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Языковые файлы Tesseract (*.traineddata)|*.traineddata|Все файлы (*.*)|*.*",
                    Title = "Выберите файл rus.traineddata",
                    Multiselect = false
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    // Спрашиваем куда сохранить
                    MessageBoxResult result = MessageBox.Show(
                        "Куда сохранить файл?\n\n" +
                        "Да - в папку с программой (рекомендуется)\n" +
                        "Нет - оставить в выбранном месте",
                        "Сохранение файла",
                        MessageBoxButton.YesNoCancel,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        // Копируем в папку с программой
                        string exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                        string targetDir = Path.Combine(exeDir, "tessdata");
                        Directory.CreateDirectory(targetDir);

                        string targetFile = Path.Combine(targetDir, "rus.traineddata");
                        File.Copy(openFileDialog.FileName, targetFile, true);

                        MessageBox.Show($"Файл скопирован в:\n{targetFile}\nПерезапустите программу.",
                            "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else if (result == MessageBoxResult.No)
                    {
                        MessageBox.Show($"Файл выбран:\n{openFileDialog.FileName}\n" +
                                       "Убедитесь что программа имеет доступ к этому файлу.",
                            "Файл выбран", MessageBoxButton.OK, MessageBoxImage.Information);
                    }

                    //CheckOCRFile();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
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