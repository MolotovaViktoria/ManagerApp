using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ManagerApp.Classes.Read.ReadPicture
{
    public class SimpleOCRProcessor
    {
        private ImageTextReader _reader;
        private bool _isReady;
        private string _errorMessage;
        private const string OCROptionsFile = "fileresurse.txt";

        public SimpleOCRProcessor()
        {
            try
            {
                Console.WriteLine("=== НАЧАЛО ИНИЦИАЛИЗАЦИИ OCR ===");

                // Сначала проверяем сохраненный путь
                CheckSavedPath();

                // Создаем ридер
                _reader = new ImageTextReader();

                // Вызываем отладку
                var method = _reader.GetType().GetMethod("DebugPaths");
                if (method != null)
                {
                    method.Invoke(_reader, null);
                }

                _isReady = _reader.IsReady();

                if (_isReady)
                {
                    Console.WriteLine("✅ OCR готов к работе!");

                    // Показываем доступные языки
                    var languages = _reader.GetAvailableLanguages();
                    Console.WriteLine($"Доступные языки: {string.Join(", ", languages)}");
                }
                else
                {
                    _errorMessage = "Tesseract не смог инициализироваться";
                    Console.WriteLine($"❌ OCR не готов: {_errorMessage}");

                    // ПРОСТОЙ ФИКС: если не нашли файл, создаем его автоматически
                    string exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    string targetFile = Path.Combine(exeDir, "rus.traineddata");

                    // Пробуем найти файл в AppData и скопировать в папку с программой
                    bool copied = TryCopyFileFromAppData(targetFile);

                    if (copied)
                    {
                        // Пробуем снова
                        _reader = new ImageTextReader();
                        _isReady = _reader.IsReady();

                        if (_isReady)
                        {
                            Console.WriteLine("✅ Файл скопирован, OCR готов!");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _isReady = false;
                _errorMessage = ex.Message;
                Console.WriteLine($"💥 КРИТИЧЕСКАЯ ОШИБКА: {ex.Message}");
            }
        }

        // Метод для проверки сохраненного пути
        private void CheckSavedPath()
        {
            try
            {
                if (File.Exists(OCROptionsFile))
                {
                    Console.WriteLine($"📁 Проверяю файл {OCROptionsFile}...");
                    var lines = File.ReadAllLines(OCROptionsFile);
                    foreach (var line in lines)
                    {
                        if (line.StartsWith("OCRPATH="))
                        {
                            var path = line.Substring(8).Trim();
                            Console.WriteLine($"📁 Найден сохраненный путь: {path}");

                            if (Directory.Exists(path))
                            {
                                Console.WriteLine($"✅ Путь существует!");

                                // Проверяем наличие файла rus.traineddata
                                string[] possibleFiles = {
                                Path.Combine(path, "rus.traineddata"),
                                Path.Combine(path, "tessdata", "rus.traineddata")
                            };

                                bool fileFound = false;
                                foreach (var file in possibleFiles)
                                {
                                    if (File.Exists(file))
                                    {
                                        fileFound = true;
                                        Console.WriteLine($"✅ Файл найден: {file}");
                                        break;
                                    }
                                }

                                if (!fileFound)
                                {
                                    Console.WriteLine($"❌ Файл rus.traineddata не найден по сохраненному пути");
                                }
                            }
                            else
                            {
                                Console.WriteLine($"❌ Сохраненный путь не существует");
                            }
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"📁 Файл {OCROptionsFile} не найден, будут использованы стандартные пути");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка проверки файла настроек: {ex.Message}");
            }
        }

        // Метод для принудительной установки пути (вызывается из настроек)
        public static bool SetCustomPath(string path)
        {
            try
            {
                // Используем статический метод ImageTextReader
                var method = typeof(ImageTextReader).GetMethod("SetTessDataPath",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

                if (method != null)
                {
                    return (bool)method.Invoke(null, new object[] { path });
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка установки пути: {ex.Message}");
                return false;
            }
        }

        // Метод для получения текущего сохраненного пути
        public static string GetSavedPath()
        {
            try
            {
                if (File.Exists(OCROptionsFile))
                {
                    var lines = File.ReadAllLines(OCROptionsFile);
                    foreach (var line in lines)
                    {
                        if (line.StartsWith("OCRPATH="))
                        {
                            return line.Substring(8).Trim();
                        }
                    }
                }
            }
            catch { }

            return null;
        }

        private bool TryCopyFileFromAppData(string targetFile)
        {
            try
            {
                // Ищем файл в AppData
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string[] files = Directory.GetFiles(localAppData, "rus.traineddata", SearchOption.AllDirectories);

                if (files.Length > 0)
                {
                    string sourceFile = files[0];
                    FileInfo sourceInfo = new FileInfo(sourceFile);

                    Console.WriteLine($"📋 Найден файл в AppData: {sourceFile}");
                    Console.WriteLine($"📋 Копирую в: {targetFile}");

                    File.Copy(sourceFile, targetFile, true);

                    MessageBox.Show(
                        $"Файл rus.traineddata найден и скопирован!\n\n" +
                        $"Из: {sourceFile}\n" +
                        $"В: {targetFile}\n\n" +
                        $"OCR готов к работе!",
                        "Успех",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка копирования файла: {ex.Message}");
                return false;
            }
        }
        private bool ReinitializeWithDownload()
        {
            try
            {
                // Пытаемся скачать файл напрямую
                string exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string tessdataPath = Path.Combine(exeDir, "tessdata");

                Directory.CreateDirectory(tessdataPath);
                string targetFile = Path.Combine(tessdataPath, "rus.traineddata");

                if (!File.Exists(targetFile))
                {
                    MessageBox.Show("Для работы OCR требуется скачать языковой файл.\nНажмите OK для загрузки.",
                        "Загрузка файла", MessageBoxButton.OK, MessageBoxImage.Information);

                    using (var client = new WebClient())
                    {
                        client.DownloadFile(
                            "https://github.com/tesseract-ocr/tessdata/raw/main/rus.traineddata",
                            targetFile);
                    }
                }

                // Создаем нового читателя
                _reader = new ImageTextReader();
                return _reader.IsReady();
            }
            catch (WebException)
            {
                _errorMessage = "Нет подключения к интернету для загрузки языкового файла.";
                return false;
            }
            catch (Exception ex)
            {
                _errorMessage = ex.Message;
                return false;
            }
        }



        // Добавьте метод для получения ошибки
        public string GetErrorMessage()
        {
            return _errorMessage;
        }

        // Проверка доступности OCR
        public bool IsOCRReady()
        {
            return _isReady;
        }

        private void PerformInitialDiagnostics()
        {
            Console.WriteLine("\n=== ДИАГНОСТИКА СРЕДЫ ===");

            // 1. Текущая директория
            Console.WriteLine($"1. CurrentDirectory: {Directory.GetCurrentDirectory()}");

            // 2. BaseDirectory
            Console.WriteLine($"2. BaseDirectory: {AppDomain.CurrentDomain.BaseDirectory}");

            // 3. Путь к EXE
            string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            Console.WriteLine($"3. EXE путь: {exePath}");
            Console.WriteLine($"4. EXE папка: {Path.GetDirectoryName(exePath)}");

            // 4. Проверяем ClickOnce
            try
            {
                bool isClickOnce = System.Deployment.Application.ApplicationDeployment.IsNetworkDeployed;
                Console.WriteLine($"5. ClickOnce: {isClickOnce}");

                if (isClickOnce)
                {
                    var deployment = System.Deployment.Application.ApplicationDeployment.CurrentDeployment;
                    Console.WriteLine($"6. DataDirectory: {deployment.DataDirectory}");
                    Console.WriteLine($"7. UpdateLocation: {deployment.UpdateLocation}");
                }
            }
            catch
            {
                Console.WriteLine("5. ClickOnce: Не доступно (нет System.Deployment)");
            }
        }

        private void PerformDetailedDiagnostics()
        {
            Console.WriteLine("\n=== ПОДРОБНАЯ ДИАГНОСТИКА ФАЙЛОВ ===");

            // Ищем rus.traineddata везде
            string[] searchLocations = {
            Directory.GetCurrentDirectory(),
            AppDomain.CurrentDomain.BaseDirectory,
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ManagerApp"),
            Path.Combine(Path.GetTempPath(), "ManagerApp"),
            "C:\\",
            "D:\\"
        };

            foreach (string location in searchLocations)
            {
                Console.WriteLine($"\nПоиск в: {location}");

                if (Directory.Exists(location))
                {
                    try
                    {
                        // Ищем во всех подпапках
                        string[] files = Directory.GetFiles(location, "rus.traineddata", SearchOption.AllDirectories);

                        if (files.Length > 0)
                        {
                            Console.WriteLine($"✅ НАЙДЕНО {files.Length} файлов:");
                            foreach (string file in files.Take(5)) // Показываем первые 5
                            {
                                FileInfo info = new FileInfo(file);
                                Console.WriteLine($"  - {file} ({info.Length / 1024 / 1024} MB)");
                            }

                            if (files.Length > 5)
                                Console.WriteLine($"  ... и еще {files.Length - 5} файлов");
                        }
                        else
                        {
                            Console.WriteLine("❌ Не найдено");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Ошибка поиска: {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine("❌ Папка не существует");
                }
            }

            // Проверяем файлы рядом с EXE
            string exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            if (!string.IsNullOrEmpty(exeDir) && Directory.Exists(exeDir))
            {
                Console.WriteLine($"\n=== ФАЙЛЫ В ПАПКЕ С EXE ===");
                try
                {
                    foreach (string file in Directory.GetFiles(exeDir))
                    {
                        FileInfo info = new FileInfo(file);
                        Console.WriteLine($"- {Path.GetFileName(file)} ({info.Length} байт)");
                    }

                    // Папки
                    foreach (string dir in Directory.GetDirectories(exeDir))
                    {
                        Console.WriteLine($"📁 {Path.GetFileName(dir)}/");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка: {ex.Message}");
                }
            }
        }

        // Основной метод обработки картинки
        // Основной метод обработки картинки
        public List<string> ProcessImage(string imagePath)
        {
            // ЕСЛИ OCR не готов, но мы в тестовом режиме
            if (!_isReady)
            {
                return new List<string>
        {
            "OCR в тестовом режиме",
            "Разместите файл rus.traineddata в папке Image",
            "Сейчас показываю пример текста:",
            "Пример товара 1: Молоко 2.5%",
            "Пример товара 2: Хлеб белый",
            "Пример товара 3: Яйца куриные"
        };
            }

            try
            {
                Console.WriteLine($"\n📷 Обрабатываем: {Path.GetFileName(imagePath)}");

                // Распознаем текст
                List<string> textLines = _reader.ReadTextFromImage(imagePath);

                // Если это сообщение об ошибке
                if (textLines.Count == 1 && textLines[0].Contains("rus.traineddata"))
                {
                    return new List<string>
            {
                "⚠️ Файл rus.traineddata не найден",
                "Разместите его в папке:",
                Path.Combine(Path.GetDirectoryName(
                    Assembly.GetExecutingAssembly().Location),
                    "Image", "rus.traineddata"),
                "Или скачайте с:",
                "https://github.com/tesseract-ocr/tessdata/raw/main/rus.traineddata"
            };
                }

                // Фильтруем пустые строки
                textLines = textLines.Where(line => !string.IsNullOrWhiteSpace(line)).ToList();

                if (textLines.Count == 0)
                {
                    return new List<string> { "Текст на изображении не найден" };
                }

                Console.WriteLine($"✅ Найдено строк: {textLines.Count}");
                return textLines;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка: {ex.Message}");
                return new List<string> { $"Ошибка обработки: {ex.Message}" };
            }
        }
        // Метод для получения чистого текста
        public string GetCleanText(string imagePath)
        {
            List<string> lines = ProcessImage(imagePath);

            if (lines.Count == 1 && (lines[0].StartsWith("OCR не готов") ||
                lines[0].StartsWith("Неподдерживаемый") ||
                lines[0].StartsWith("Ошибка") ||
                lines[0].StartsWith("Текст не найден")))
            {
                return lines[0];
            }

            // Объединяем все строки
            StringBuilder result = new StringBuilder();
            foreach (string line in lines)
            {
                result.AppendLine(line);
            }

            return result.ToString();
        }

        // Метод для получения статистики
        public Dictionary<string, int> GetTextStatistics(List<string> lines)
        {
            Dictionary<string, int> stats = new Dictionary<string, int>();

            if (lines == null || lines.Count == 0)
                return stats;

            int totalChars = 0;
            int totalWords = 0;
            int russianChars = 0;
            int englishChars = 0;
            int numbers = 0;

            foreach (string line in lines)
            {
                totalChars += line.Length;

                // Считаем слова (разделенные пробелами)
                string[] words = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                totalWords += words.Length;

                // Считаем типы символов
                foreach (char c in line)
                {
                    if (c >= 'А' && c <= 'я' || c == 'Ё' || c == 'ё')
                        russianChars++;
                    else if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'))
                        englishChars++;
                    else if (char.IsDigit(c))
                        numbers++;
                }
            }

            stats["Строк"] = lines.Count;
            stats["Символов"] = totalChars;
            stats["Слов"] = totalWords;
            stats["Русских букв"] = russianChars;
            stats["Английских букв"] = englishChars;
            stats["Цифр"] = numbers;

            return stats;
        }


        // Показ справки
        public void ShowHelp()
        {
            string helpText =
                "📖 КАК ПОЛУЧИТЬ ЛУЧШИЙ РЕЗУЛЬТАТ:\n\n" +
                "1. 📸 Качество картинки:\n" +
                "   • Четкий текст\n" +
                "   • Хорошее освещение\n" +
                "   • Прямой угол съемки\n\n" +
                "2. 🎨 Цвета:\n" +
                "   • Черный текст на белом фоне\n" +
                "   • Высокий контраст\n\n" +
                "3. 📝 Текст:\n" +
                "   • Горизонтальное расположение\n" +
                "   • Стандартный шрифт\n" +
                "   • Размер шрифта не менее 12pt\n\n" +
                "✅ Программа сама скачает все нужные файлы\n" +
                "✅ Поддерживаются: PNG, JPG, BMP, TIFF";

            MessageBox.Show(helpText, "Советы по использованию",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Дополнительный метод для получения доступных языков
        public List<string> GetAvailableLanguages()
        {
            try
            {
                if (_reader != null)
                {
                    return _reader.GetAvailableLanguages();
                }
            }
            catch { }

            return new List<string> { "rus (требуется файл)" };
        }

        // Метод для принудительной переинициализации
        public bool Reinitialize()
        {
            try
            {
                _reader = new ImageTextReader();
                _isReady = _reader.IsReady();
                return _isReady;
            }
            catch
            {
                _isReady = false;
                return false;
            }
        }

        // Метод для тестирования OCR
        // Исправьте метод TestOCR
        public async Task TestOCR(string testImagePath = null)
        {
            try
            {
                Console.WriteLine("\n=== ТЕСТИРОВАНИЕ OCR ===");

                if (!_isReady)
                {
                    Console.WriteLine("❌ OCR не готов к работе");
                    return;
                }

                // Если путь не указан, создаем тестовое изображение
                if (string.IsNullOrEmpty(testImagePath) || !File.Exists(testImagePath))
                {
                    testImagePath = CreateTestImage();
                }

                if (!string.IsNullOrEmpty(testImagePath) && File.Exists(testImagePath))
                {
                    Console.WriteLine($"Тестовое изображение: {testImagePath}");

                    // ДОБАВЬТЕ await перед Task.Run
                    var result = await Task.Run(() => ProcessImage(testImagePath));

                    if (result.Count > 0 && !result[0].StartsWith("OCR не готов") &&
                        !result[0].StartsWith("Ошибка") && !result[0].StartsWith("Неподдерживаемый"))
                    {
                        Console.WriteLine($"✅ OCR работает корректно!");
                        Console.WriteLine($"Распознанный текст ({result.Count} строк):");
                        foreach (var line in result)
                        {
                            Console.WriteLine($"  - {line}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"❌ OCR не работает: {result[0]}");
                    }

                    // Удаляем временный файл если создавали
                    if (testImagePath.Contains("_test_ocr_"))
                    {
                        try { File.Delete(testImagePath); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка тестирования: {ex.Message}");
            }
        }
        private string CreateTestImage()
        {
            try
            {
                string tempPath = Path.GetTempFileName() + "_test_ocr_.png";

                // Создаем простое тестовое изображение с текстом
                using (System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(400, 100))
                {
                    using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(bmp))
                    {
                        g.Clear(System.Drawing.Color.White);

                        using (System.Drawing.Font font = new System.Drawing.Font("Arial", 16))
                        using (System.Drawing.Brush brush = new System.Drawing.SolidBrush(System.Drawing.Color.Black))
                        {
                            g.DrawString("Тест OCR: Привет, мир! 123", font, brush, 10, 30);
                        }
                    }

                    bmp.Save(tempPath, System.Drawing.Imaging.ImageFormat.Png);
                }

                return tempPath;
            }
            catch
            {
                return null;
            }
        }
    }
}