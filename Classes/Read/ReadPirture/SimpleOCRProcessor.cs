using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace ManagerApp.Classes.Read.ReadPicture
{
    public class SimpleOCRProcessor
    {
        private ImageTextReader _reader;
        private bool _isReady;
        private string _errorMessage;

        public SimpleOCRProcessor()
        {
            try
            {
                Console.WriteLine("=== ИНИЦИАЛИЗАЦИЯ OCR ===");
                Console.WriteLine("=== ПОДРОБНАЯ ДИАГНОСТИКА ===");

                // 1. Показываем все пути
                string exePath = Assembly.GetExecutingAssembly().Location;
                string exeDir = Path.GetDirectoryName(exePath);
                string currentDir = Directory.GetCurrentDirectory();
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;

                Console.WriteLine($"EXE путь: {exePath}");
                Console.WriteLine($"EXE папка: {exeDir}");
                Console.WriteLine($"Текущая папка: {currentDir}");
                Console.WriteLine($"BaseDirectory: {baseDir}");

                // 2. Показываем содержимое папок
                Console.WriteLine($"\n=== СОДЕРЖИМОЕ ПАПКИ С EXE ===");
                ShowDirectoryContents(exeDir);

                Console.WriteLine($"\n=== СОДЕРЖИМОЕ ТЕКУЩЕЙ ПАПКИ ===");
                ShowDirectoryContents(currentDir);

                Console.WriteLine($"\n=== СОДЕРЖИМОЕ BaseDirectory ===");
                ShowDirectoryContents(baseDir);

                // 3. Ищем файл
                Console.WriteLine($"\n=== ПОИСК ФАЙЛА ===");
                string[] checkPaths = {
                    Path.Combine(exeDir, "rus.traineddata"),
                    Path.Combine(currentDir, "rus.traineddata"),
                    Path.Combine(baseDir, "rus.traineddata"),
                    Path.Combine(exeDir, "руководствоПользователя.pdf"),
                    Path.Combine(currentDir, "руководствоПользователя.pdf"),
                    Path.Combine(baseDir, "руководствоПользователя.pdf")
                };

                bool tessdataFound = false;
                bool manualFound = false;

                foreach (var path in checkPaths)
                {
                    Console.Write($"  {Path.GetFileName(path)}: {path} ... ");
                    if (File.Exists(path))
                    {
                        Console.WriteLine("✅ НАЙДЕН");
                        if (path.Contains("rus.traineddata")) tessdataFound = true;
                        if (path.Contains("руководствоПользователя.pdf")) manualFound = true;
                    }
                    else
                    {
                        Console.WriteLine("❌ НЕТ");
                    }
                }

                // 4. Создаем ридер
                if (tessdataFound)
                {
                    Console.WriteLine($"\n✅ Файл rus.traineddata найден!");
                    _reader = new ImageTextReader();
                    _isReady = _reader.IsReady();

                    if (_isReady)
                    {
                        Console.WriteLine("✅ OCR готов к работе!");
                    }
                    else
                    {
                        _errorMessage = "Не удалось инициализировать Tesseract";
                        Console.WriteLine($"❌ {_errorMessage}");
                    }
                }
                else
                {
                    _errorMessage = $"Файл rus.traineddata не найден!\n\n" +
                                  $"Руководство найдено: {(manualFound ? "ДА" : "НЕТ")}\n" +
                                  $"Проверьте папки:\n1. {exeDir}\n2. {currentDir}\n3. {baseDir}";
                    Console.WriteLine($"❌ {_errorMessage}");
                    _isReady = false;
                }
            }
            catch (Exception ex)
            {
                _isReady = false;
                _errorMessage = ex.Message;
                Console.WriteLine($"💥 КРИТИЧЕСКАЯ ОШИБКА: {ex.Message}");
            }
        }

        private void ShowDirectoryContents(string directory)
        {
            try
            {
                if (!Directory.Exists(directory))
                {
                    Console.WriteLine($"  Папка не существует: {directory}");
                    return;
                }

                var files = Directory.GetFiles(directory);
                var directories = Directory.GetDirectories(directory);

                Console.WriteLine($"  Файлы ({files.Length}):");
                foreach (var file in files)
                {
                    var info = new FileInfo(file);
                    Console.WriteLine($"    - {Path.GetFileName(file)} ({info.Length} байт)");
                }

                Console.WriteLine($"  Папки ({directories.Length}):");
                foreach (var dir in directories)
                {
                    Console.WriteLine($"    📁 {Path.GetFileName(dir)}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Ошибка: {ex.Message}");
            }
        }

        public bool IsOCRReady() => _isReady;
        public string GetErrorMessage() => _errorMessage;

        public List<string> ProcessImage(string imagePath)
        {
            if (!_isReady)
            {
                return new List<string>
                {
                    "OCR не готов к работе",
                    "Разместите файл 'rus.traineddata' в папке с программой"
                };
            }

            try
            {
                return _reader.ReadTextFromImage(imagePath);
            }
            catch (Exception ex)
            {
                return new List<string> { $"Ошибка: {ex.Message}" };
            }
        }

        // Метод для получения отладочной информации
        public string GetDebugInfo()
        {
            try
            {
                string exePath = Assembly.GetExecutingAssembly().Location;
                string exeDir = Path.GetDirectoryName(exePath);
                string currentDir = Directory.GetCurrentDirectory();
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("=== ДИАГНОСТИКА OCR ===");
                sb.AppendLine($"1. EXE путь: {exePath}");
                sb.AppendLine($"2. EXE папка: {exeDir}");
                sb.AppendLine($"3. Текущая папка: {currentDir}");
                sb.AppendLine($"4. BaseDirectory: {baseDir}");
                sb.AppendLine($"5. OCR готов: {_isReady}");
                sb.AppendLine($"6. Сообщение об ошибке: {_errorMessage}");

                // Проверяем файлы
                sb.AppendLine("\n=== ПРОВЕРКА ФАЙЛОВ ===");

                string[] filesToCheck = {
                    Path.Combine(exeDir, "rus.traineddata"),
                    Path.Combine(currentDir, "rus.traineddata"),
                    Path.Combine(baseDir, "rus.traineddata"),
                    Path.Combine(exeDir, "руководствоПользователя.pdf"),
                    Path.Combine(currentDir, "руководствоПользователя.pdf"),
                    Path.Combine(baseDir, "руководствоПользователя.pdf")
                };

                foreach (var file in filesToCheck)
                {
                    bool exists = File.Exists(file);
                    sb.AppendLine($"  {Path.GetFileName(file)}: {exists} ({file})");
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"Ошибка получения информации: {ex.Message}";
            }
        }
    }
}