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
using Microsoft.Win32;
using System.Linq;
using System.Collections.Generic;
using ManagerApp.Data.StructureList;
using Excel = Microsoft.Office.Interop.Excel;
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

        private async void btnExportPrices_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Проверяем наличие кеша
                if (!BitrixCache.IsCacheReady())
                {
                    var result = MessageBox.Show(
                        "Кеш данных не загружен. Хотите загрузить данные перед выгрузкой?",
                        "Загрузка данных",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        await BitrixCache.InitializeAsync(true);
                    }
                    else
                    {
                        return;
                    }
                }

                // Создаем окно ожидания
                var waitingWindow = new WaitingWindow("Выгрузка прайсов", "Подготовка данных...");
                waitingWindow.Owner = Window.GetWindow(this);

                // Используем BackgroundWorker для асинхронной операции
                var worker = new System.ComponentModel.BackgroundWorker
                {
                    WorkerReportsProgress = true,
                    WorkerSupportsCancellation = false
                };

                List<ProductWithLowerSection> allProducts = null;
                Dictionary<string, List<ProductWithLowerSection>> productsBySupplier = null;

                worker.DoWork += (s, args) =>
                {
                    try
                    {
                        worker.ReportProgress(10, "Загрузка данных из кеша...");

                        // Получаем все товары с нижними разделами
                        var task = Task.Run(async () => await BitrixCache.GetAllProductsWithLowerSections());
                        task.Wait();
                        allProducts = task.Result;

                        if (allProducts == null || !allProducts.Any())
                        {
                            args.Result = new ExportResult
                            {
                                Success = false,
                                Message = "Нет данных для выгрузки"
                            };
                            return;
                        }

                        worker.ReportProgress(40, "Группировка по поставщикам...");

                        // Группируем товары по нижним разделам (поставщикам)
                        productsBySupplier = allProducts
                            .Where(p => !string.IsNullOrEmpty(p.LowerSectionName))
                            .GroupBy(p => p.LowerSectionName)
                            .ToDictionary(g => g.Key, g => g.ToList());

                        worker.ReportProgress(70, "Формирование Excel файла...");

                        // Создаем Excel файл
                        var excelApp = new Microsoft.Office.Interop.Excel.Application();
                        var workbook = excelApp.Workbooks.Add();
                        int sheetIndex = 1;

                        foreach (var supplier in productsBySupplier)
                        {
                            worker.ReportProgress(70 + (sheetIndex * 30 / productsBySupplier.Count),
                                $"Обработка поставщика: {supplier.Key}...");

                            // Добавляем новый лист или используем первый
                            Excel.Worksheet worksheet;
                            if (sheetIndex == 1)
                            {
                                worksheet = (Excel.Worksheet)workbook.Worksheets[1];
                            }
                            else
                            {
                                worksheet = (Excel.Worksheet)workbook.Worksheets.Add(After: workbook.Worksheets[workbook.Worksheets.Count]);
                            }

                            // Называем лист именем поставщика (обрезаем если слишком длинное)
                            string sheetName = supplier.Key.Length > 30 ? supplier.Key.Substring(0, 30) : supplier.Key;
                            worksheet.Name = sheetName;

                            // Заголовки
                            worksheet.Cells[1, 1] = "ID товара";
                            worksheet.Cells[1, 2] = "Наименование";
                            worksheet.Cells[1, 3] = "Код";
                            worksheet.Cells[1, 4] = "Цена";
                            worksheet.Cells[1, 5] = "Категория";
                            worksheet.Cells[1, 6] = "Поставщик";

                            // Выделяем заголовки
                            var headerRange = worksheet.Range[worksheet.Cells[1, 1], worksheet.Cells[1, 6]];
                            headerRange.Font.Bold = true;
                            headerRange.Interior.Color = System.Drawing.Color.LightGray;
                            headerRange.Borders.LineStyle = Microsoft.Office.Interop.Excel.XlLineStyle.xlContinuous;

                            // Заполняем данные
                            int row = 2;
                            foreach (var product in supplier.Value)
                            {
                                worksheet.Cells[row, 1] = product.ProductId;
                                worksheet.Cells[row, 2] = product.ProductName;
                                worksheet.Cells[row, 3] = product.Code;
                                worksheet.Cells[row, 4] = product.HasPrice ? product.Price.ToString("F2") : "Нет цены";
                                worksheet.Cells[row, 5] = product.CategoryPath;
                                worksheet.Cells[row, 6] = product.LowerSectionName;
                                row++;
                            }

                            // Автоподбор ширины колонок
                            var usedRange = worksheet.UsedRange;
                            usedRange.Columns.AutoFit();

                            // Применяем форматирование к ценам
                            var priceRange = worksheet.Range[worksheet.Cells[2, 4], worksheet.Cells[row - 1, 4]];
                            priceRange.NumberFormat = "#,##0.00";

                            sheetIndex++;
                        }

                        worker.ReportProgress(95, "Подготовка к сохранению...");

                        args.Result = new ExportResult
                        {
                            Success = true,
                            Message = "Файл успешно создан",
                            ExcelApp = excelApp,
                            Workbook = workbook,
                            ProductsCount = allProducts.Count,
                            SuppliersCount = productsBySupplier.Count
                        };
                    }
                    catch (Exception ex)
                    {
                        args.Result = new ExportResult
                        {
                            Success = false,
                            Message = ex.Message,
                            Exception = ex
                        };
                    }
                };

                worker.RunWorkerCompleted += (s, args) =>
                {
                    waitingWindow.Close();

                    if (args.Result is ExportResult result)
                    {
                        if (result.Success && result.Workbook != null)
                        {
                            try
                            {
                                // Настраиваем диалог сохранения файла
                                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                                {
                                    Filter = "Excel файлы (*.xlsx)|*.xlsx|Все файлы (*.*)|*.*",
                                    DefaultExt = "xlsx",
                                    FileName = $"Прайсы_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
                                    Title = "Сохранить файл с прайсами"
                                };

                                // Показываем диалог
                                if (saveFileDialog.ShowDialog() == true)
                                {
                                    result.Workbook.SaveAs(saveFileDialog.FileName);
                                    result.Workbook.Close();
                                    result.ExcelApp.Quit();

                                    // Освобождаем COM объекты
                                    System.Runtime.InteropServices.Marshal.ReleaseComObject(result.Workbook);
                                    System.Runtime.InteropServices.Marshal.ReleaseComObject(result.ExcelApp);

                                    MessageBox.Show(
                                        $"✅ Файл успешно сохранен!\n\n" +
                                        $"📊 Всего товаров: {result.ProductsCount}\n" +
                                        $"🏢 Поставщиков: {result.SuppliersCount}\n" +
                                        $"📁 Файл: {saveFileDialog.FileName}",
                                        "Выгрузка завершена",
                                        MessageBoxButton.OK,
                                        MessageBoxImage.Information);

                                    // Предлагаем открыть файл
                                    var openResult = MessageBox.Show(
                                        "Открыть файл?",
                                        "Открыть файл",
                                        MessageBoxButton.YesNo,
                                        MessageBoxImage.Question);

                                    if (openResult == MessageBoxResult.Yes)
                                    {
                                        System.Diagnostics.Process.Start(saveFileDialog.FileName);
                                    }
                                }
                                else
                                {
                                    // Пользователь отменил сохранение
                                    result.Workbook.Close();
                                    result.ExcelApp.Quit();

                                    System.Runtime.InteropServices.Marshal.ReleaseComObject(result.Workbook);
                                    System.Runtime.InteropServices.Marshal.ReleaseComObject(result.ExcelApp);
                                }
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show(
                                    $"❌ Ошибка при сохранении файла:\n{ex.Message}",
                                    "Ошибка",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                            }
                        }
                        else
                        {
                            MessageBox.Show(
                                $"❌ Ошибка при создании файла:\n{result.Message}",
                                "Ошибка",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                        }
                    }
                };

                waitingWindow.Show();
                worker.RunWorkerAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"❌ Ошибка:\n{ex.Message}",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // Вспомогательный класс для результатов экспорта
        public class ExportResult
        {
            public bool Success { get; set; }
            public string Message { get; set; }
            public Exception Exception { get; set; }
            public Microsoft.Office.Interop.Excel.Application ExcelApp { get; set; }
            public Microsoft.Office.Interop.Excel.Workbook Workbook { get; set; }
            public int ProductsCount { get; set; }
            public int SuppliersCount { get; set; }
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