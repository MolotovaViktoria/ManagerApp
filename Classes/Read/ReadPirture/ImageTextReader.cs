using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Tesseract;

namespace ManagerApp.Classes.Read.ReadPicture
{
    public class ImageTextReader
    {
        private string _tessDataPath;
        private bool _isInitialized = false;

        public ImageTextReader()
        {
            try
            {
                Console.WriteLine("=== ПОИСК ФАЙЛА rus.traineddata ===");

                // Пробуем найти файл во всех возможных местах
                _tessDataPath = FindTessDataFile();

                if (!string.IsNullOrEmpty(_tessDataPath))
                {
                    Console.WriteLine($"✅ Файл найден в: {_tessDataPath}");
                    _isInitialized = true;
                }
                else
                {
                    Console.WriteLine($"❌ Файл rus.traineddata не найден ни в одном месте!");
                    Console.WriteLine($"Папки где искали: {GetSearchLocationsInfo()}");
                    _isInitialized = false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ОШИБКА ИНИЦИАЛИЗАЦИИ: {ex.Message}");
                _isInitialized = false;
            }
        }

        private string FindTessDataFile()
        {
            try
            {
                // 1. Путь к исполняемому файлу
                string exePath = Assembly.GetExecutingAssembly().Location;
                string exeDir = Path.GetDirectoryName(exePath);

                Console.WriteLine($"Папка EXE: {exeDir}");

                // 2. Текущая рабочая директория
                string currentDir = Directory.GetCurrentDirectory();
                Console.WriteLine($"Текущая папка: {currentDir}");

                // 3. BaseDirectory приложения
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                Console.WriteLine($"BaseDirectory: {baseDir}");

                // Места для поиска (по приоритету)
                List<string> searchPaths = new List<string>
                {
                    // 1. Рядом с EXE файлом (самый надежный)
                    Path.Combine(exeDir, "rus.traineddata"),
                    
                    // 2. В текущей рабочей директории
                    Path.Combine(currentDir, "rus.traineddata"),
                    
                    // 3. В BaseDirectory
                    Path.Combine(baseDir, "rus.traineddata"),
                    
                    // 4. В подпапках рядом с EXE
                    Path.Combine(exeDir, "tessdata", "rus.traineddata"),
                    Path.Combine(exeDir, "Image", "rus.traineddata"),
                    Path.Combine(exeDir, "ocr", "rus.traineddata"),
                    
                    // 5. В папке bin/Debug или bin/Release
                    Path.Combine(baseDir, "..", "rus.traineddata"),
                    Path.Combine(baseDir, "..", "..", "rus.traineddata"),
                    Path.Combine(baseDir, "..", "..", "..", "rus.traineddata"),
                };

                // Убираем дубликаты
                searchPaths = searchPaths.Distinct().ToList();

                Console.WriteLine("\n🔍 Проверяю пути:");
                foreach (var path in searchPaths)
                {
                    Console.Write($"  {path} ... ");
                    if (File.Exists(path))
                    {
                        Console.WriteLine("✅ НАЙДЕН");
                        return Path.GetDirectoryName(path);
                    }
                    Console.WriteLine("❌ нет");
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка поиска: {ex.Message}");
                return null;
            }
        }

        private string GetSearchLocationsInfo()
        {
            try
            {
                string exePath = Assembly.GetExecutingAssembly().Location;
                string exeDir = Path.GetDirectoryName(exePath);
                string currentDir = Directory.GetCurrentDirectory();
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;

                return $"\n1. {exeDir}\n2. {currentDir}\n3. {baseDir}";
            }
            catch
            {
                return "Не удалось получить информацию о путях";
            }
        }

        public bool IsReady()
        {
            return _isInitialized && !string.IsNullOrEmpty(_tessDataPath);
        }

        public List<string> ReadTextFromImage(string imagePath)
        {
            var result = new List<string>();

            try
            {
                if (!File.Exists(imagePath))
                {
                    result.Add($"Файл не найден: {imagePath}");
                    return result;
                }

                if (!IsReady())
                {
                    result.Add("OCR не готов к работе");
                    result.Add("Разместите файл rus.traineddata в папке с программой");
                    return result;
                }

                using (var engine = new TesseractEngine(_tessDataPath, "rus", EngineMode.Default))
                {
                    using (var img = Pix.LoadFromFile(imagePath))
                    {
                        using (var page = engine.Process(img))
                        {
                            string text = page.GetText();

                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                result = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                                    .Select(line => line.Trim())
                                    .Where(line => line.Length > 0)
                                    .ToList();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ОШИБКА РАСПОЗНАВАНИЯ: {ex.Message}");
                result.Add($"Ошибка: {ex.Message}");
            }

            if (result.Count == 0)
            {
                result.Add("Текст на изображении не найден");
            }

            return result;
        }
    }
}