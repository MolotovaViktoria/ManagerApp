using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Tesseract;

namespace ManagerApp.Classes.Read.ReadPicture
{

    public class ImageTextReader
    {
        private string _tessDataPath;
        private bool _isInitialized = false;
        private const string OCROptionsFile = "fileresurse.txt";

        public ImageTextReader()
        {
            try
            {
                Console.WriteLine("=== ИНИЦИАЛИЗАЦИЯ TESSERACT ===");

                // 1. Пробуем загрузить путь из файла fileresurse.txt
                string savedPath = LoadPathFromFile();
                if (!string.IsNullOrEmpty(savedPath))
                {
                    _tessDataPath = savedPath;
                    Console.WriteLine($"✅ Используется путь из fileresurse.txt: {_tessDataPath}");

                    // Проверяем существование файла
                    string russianFile = Path.Combine(_tessDataPath, "rus.traineddata");
                    if (!File.Exists(russianFile))
                    {
                        // Если файл не найден по сохраненному пути, ищем в стандартных местах
                        Console.WriteLine($"❌ Файл не найден по сохраненному пути, ищу в стандартных местах...");
                        _tessDataPath = FindRussianFile();
                    }
                }
                else
                {
                    // 2. Если путь не сохранен, ищем файл в стандартных местах
                    _tessDataPath = FindRussianFile();
                }

                // Сохраняем найденный путь для будущего использования
                SavePathToFile(_tessDataPath);

                _isInitialized = true;
                Console.WriteLine($"✅ Найден путь к данным: {_tessDataPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ОШИБКА: {ex.Message}");
                _isInitialized = false;
            }
        }

        // Метод для загрузки пути из файла fileresurse.txt
        private string LoadPathFromFile()
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
                            var path = line.Substring(8).Trim();
                            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                            {
                                Console.WriteLine($"📁 Найден сохраненный путь: {path}");
                                return path;
                            }
                        }
                    }
                    Console.WriteLine("Файл fileresurse.txt есть, но путь OCRPATH не найден или невалиден");
                }
                else
                {
                    Console.WriteLine("Файл fileresurse.txt не найден");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка чтения файла {OCROptionsFile}: {ex.Message}");
            }

            return null;
        }

        // Метод для сохранения пути в файл
        private void SavePathToFile(string path)
        {
            try
            {
                var lines = new List<string>();

                if (File.Exists(OCROptionsFile))
                {
                    lines = File.ReadAllLines(OCROptionsFile).ToList();

                    // Удаляем старую запись OCRPATH если есть
                    lines.RemoveAll(line => line.StartsWith("OCRPATH="));
                }

                // Добавляем новую запись
                lines.Add($"OCRPATH={path}");

                File.WriteAllLines(OCROptionsFile, lines);
                Console.WriteLine($"💾 Путь сохранен в {OCROptionsFile}: {path}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка сохранения пути в файл: {ex.Message}");
            }
        }

        // Метод для ручного указания пути (из настроек)
        public static bool SetTessDataPath(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    // Проверяем есть ли файл в этой папке
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
                            break;
                        }
                    }

                    if (fileFound)
                    {
                        // Сохраняем путь в файл
                        SavePathToFileStatic(Path.GetDirectoryName(foundFile));
                        return true;
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        // Статический метод для сохранения пути
        private static void SavePathToFileStatic(string path)
        {
            try
            {
                const string fileName = "fileresurse.txt";
                var lines = new List<string>();

                if (File.Exists(fileName))
                {
                    lines = File.ReadAllLines(fileName).ToList();
                    lines.RemoveAll(line => line.StartsWith("OCRPATH="));
                }

                lines.Add($"OCRPATH={path}");
                File.WriteAllLines(fileName, lines);

                Console.WriteLine($"✅ Путь сохранен в {fileName}: {path}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка сохранения пути: {ex.Message}");
            }
        }

        // Остальные методы остаются без изменений...
        private string FindRussianFile()
        {
            string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string exeDir = Path.GetDirectoryName(exePath);

            Console.WriteLine($"=== ПОИСК ФАЙЛА (ЕДИНЫЙ ДЛЯ ВСЕЙ ПРОГРАММЫ) ===");
            Console.WriteLine($"EXE путь: {exePath}");

            // ТОЛЬКО 3 основных места - такие же как в настройках!
            List<string> searchPaths = new List<string>
        {
            // 1. Рядом с EXE (самый простой)
            Path.Combine(exeDir, "rus.traineddata"),
            
            // 2. В папке tessdata рядом с EXE
            Path.Combine(exeDir, "tessdata", "rus.traineddata"),
            
            // 3. В папке Image рядом с EXE
            Path.Combine(exeDir, "Image", "rus.traineddata"),
        };

            foreach (string path in searchPaths)
            {
                Console.WriteLine($"  Проверяю: {path}");
                if (File.Exists(path))
                {
                    FileInfo info = new FileInfo(path);
                    Console.WriteLine($"✅ НАЙДЕН: {path} ({info.Length / 1024 / 1024} MB)");

                    // Возвращаем папку, где лежит файл
                    return Path.GetDirectoryName(path);
                }
            }

            throw new FileNotFoundException(
                $"Файл rus.traineddata не найден!\n\n" +
                $"Положите файл в одну из папок:\n" +
                $"1. {exeDir}\\rus.traineddata\n" +
                $"2. {exeDir}\\tessdata\\rus.traineddata\n" +
                $"3. {exeDir}\\Image\\rus.traineddata");
        }






        //// Метод для ручного указания пути
        //public static bool SetTessDataPath(string path)
        //{
        //    try
        //    {
        //        if (Directory.Exists(path))
        //        {
        //            // Проверяем есть ли файл в этой папке
        //            string russianFile = Path.Combine(path, "rus.traineddata");
        //            if (File.Exists(russianFile))
        //            {
        //                CustomTessDataPath = path;
        //                return true;
        //            }
        //        }
        //        return false;
        //    }
        //    catch
        //    {
        //        return false;
        //    }
        //}

        // ДОБАВЬТЕ ЭТИ МЕТОДЫ:

        public bool IsReady()
        {
            try
            {
                if (!_isInitialized)
                {
                    Console.WriteLine("❌ Tesseract не инициализирован");
                    return false;
                }

                string russianFile = Path.Combine(_tessDataPath, "rus.traineddata");
                if (string.IsNullOrEmpty(_tessDataPath))
                {
                    russianFile = "rus.traineddata";
                }

                if (!File.Exists(russianFile))
                {
                    Console.WriteLine($"❌ Файл не найден: {russianFile}");
                    return false;
                }

                // Простая проверка движка
                using (var engine = new TesseractEngine(_tessDataPath, "rus", EngineMode.Default))
                {
                    Console.WriteLine("✅ Tesseract работает");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка проверки готовности: {ex.Message}");
                return false;
            }
        }

        // ОСНОВНОЙ МЕТОД - максимально упрощенный
        public List<string> ReadTextFromImage(string imagePath)
        {
            var result = new List<string>();

            try
            {
                Console.WriteLine($"\n=== ЧТЕНИЕ ИЗОБРАЖЕНИЯ ===");
                Console.WriteLine($"Изображение: {imagePath}");

                // 1. Проверяем изображение
                if (!File.Exists(imagePath))
                {
                    result.Add($"Файл не найден: {imagePath}");
                    return result;
                }

                // 2. Проверяем языковой файл
                string russianFile = Path.Combine(_tessDataPath, "rus.traineddata");
                if (!File.Exists(russianFile))
                {
                    result.Add("Ошибка: Файл rus.traineddata не найден!");
                    Console.WriteLine("❌ rus.traineddata не найден!");
                    return result;
                }

                // 3. Распознаем текст
                Console.WriteLine("Запускаю Tesseract...");
                using (var engine = new TesseractEngine(_tessDataPath, "rus", EngineMode.Default))
                {
                    // Простые настройки
                    try
                    {
                        engine.SetVariable("preserve_interword_spaces", "1");
                    }
                    catch { }

                    using (var img = Pix.LoadFromFile(imagePath))
                    {
                        using (var page = engine.Process(img))
                        {
                            string text = page.GetText();
                            float confidence = page.GetMeanConfidence();

                            Console.WriteLine($"Уверенность распознавания: {confidence:P}");

                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                result = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                                    .Select(line => line.Trim())
                                    .Where(line => line.Length > 0)
                                    .ToList();

                                Console.WriteLine($"Найдено строк: {result.Count}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ОШИБКА РАСПОЗНАВАНИЯ:");
                Console.WriteLine($"Сообщение: {ex.Message}");
                Console.WriteLine($"Тип: {ex.GetType().Name}");

                // Добавляем понятное сообщение об ошибке
                if (ex.Message.Contains("Failed to init"))
                {
                    result.Add("Ошибка инициализации Tesseract. Проверьте файл rus.traineddata");
                }
                else if (ex.Message.Contains("language"))
                {
                    result.Add("Ошибка загрузки языкового пакета");
                }
                else
                {
                    result.Add($"Ошибка: {ex.Message}");
                }
            }

            if (result.Count == 0)
            {
                result.Add("Текст на изображении не найден");
            }

            return result;
        }

        // Для отладки - показываем доступные языки
        public List<string> GetAvailableLanguages()
        {
            var languages = new List<string>();

            try
            {
                if (Directory.Exists(_tessDataPath))
                {
                    var files = Directory.GetFiles(_tessDataPath, "*.traineddata");
                    foreach (var file in files)
                    {
                        string lang = Path.GetFileNameWithoutExtension(file);
                        languages.Add(lang);
                    }
                }

                if (languages.Count == 0)
                {
                    languages.Add("rus (требуется файл rus.traineddata)");
                }
            }
            catch { }

            return languages;
        }

        // Добавьте метод для отладки
        public void DebugPaths()
        {
            Console.WriteLine("\n=== ОТЛАДКА ПУТЕЙ ===");

            string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string exeDir = Path.GetDirectoryName(exePath);

            Console.WriteLine($"1. EXE путь: {exePath}");
            Console.WriteLine($"2. EXE папка: {exeDir}");
            Console.WriteLine($"3. Текущая директория: {Directory.GetCurrentDirectory()}");
            Console.WriteLine($"4. BaseDirectory: {AppDomain.CurrentDomain.BaseDirectory}");
            Console.WriteLine($"5. Tesseract путь: {_tessDataPath}");

            // Проверяем Image папку
            string imagePath = Path.Combine(exeDir, "Image");
            Console.WriteLine($"6. Image папка: {imagePath}");
            Console.WriteLine($"7. Image папка существует: {Directory.Exists(imagePath)}");

            if (Directory.Exists(imagePath))
            {
                Console.WriteLine("Содержимое Image папки:");
                foreach (string file in Directory.GetFiles(imagePath))
                {
                    FileInfo info = new FileInfo(file);
                    Console.WriteLine($"  - {Path.GetFileName(file)} ({info.Length} байт)");
                }
            }
        }
    }
}