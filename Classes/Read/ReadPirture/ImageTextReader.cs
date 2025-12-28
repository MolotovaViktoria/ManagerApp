using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Tesseract;

namespace ManagerApp.Classes.Read.ReadPicture
{
    public class ImageTextReader
    {
        private string _tessDataPath; // Путь к файлам Tesseract

        public ImageTextReader()
        {
            // 1. Находим или создаем папку для файлов Tesseract
            _tessDataPath = GetTessDataPath();
            Console.WriteLine($"Папка с файлами Tesseract: {_tessDataPath}");

            // 2. Создаем папку, если ее нет
            CreateFolderIfNotExists(_tessDataPath);

            // 3. Проверяем и загружаем файлы языков
            CheckAndDownloadLanguageFiles();
        }

        // Метод для получения правильного пути к файлам
        private string GetTessDataPath()
        {
            // Путь 1: Папка рядом с программой
            string programFolder = AppDomain.CurrentDomain.BaseDirectory;
            string path1 = Path.Combine(programFolder, "tessdata");

            // Путь 2: Папка Image рядом с программой
            string path2 = Path.Combine(programFolder, "Image");

            // Путь 3: В документах пользователя
            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string path3 = Path.Combine(documents, "ManagerApp", "tessdata");

            // Проверяем, где есть файлы
            if (HasTessDataFiles(path1)) return path1;
            if (HasTessDataFiles(path2)) return path2;
            if (HasTessDataFiles(path3)) return path3;

            // Если нигде нет файлов, используем первый путь
            return path1;
        }

        // Проверяем, есть ли файлы Tesseract в папке
        private bool HasTessDataFiles(string folderPath)
        {
            if (!Directory.Exists(folderPath)) return false;

            // Ищем файлы с расширением .traineddata
            string[] files = Directory.GetFiles(folderPath, "*.traineddata");
            return files.Length > 0;
        }

        // Создаем папку, если ее нет
        private void CreateFolderIfNotExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                Console.WriteLine($"Создана папка: {path}");
            }
        }

        // Проверяем и загружаем файлы языков
        private void CheckAndDownloadLanguageFiles()
        {
            // Какие языки нам нужны
            string[] neededLanguages = { "rus", "eng" };

            foreach (string language in neededLanguages)
            {
                string filePath = Path.Combine(_tessDataPath, $"{language}.traineddata");

                // Если файла нет, скачиваем его
                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"Файл {language}.traineddata не найден. Скачиваем...");
                    DownloadLanguageFile(language, filePath);
                }
                else
                {
                    // Проверяем размер файла
                    FileInfo info = new FileInfo(filePath);
                    if (info.Length < 2 * 1024 * 1024) // Меньше 2 МБ
                    {
                        Console.WriteLine($"Файл {language}.traineddata слишком маленький. Скачиваем заново...");
                        DownloadLanguageFile(language, filePath);
                    }
                    else
                    {
                        Console.WriteLine($"Файл {language}.traineddata найден ({info.Length / 1024 / 1024} МБ)");
                    }
                }
            }
        }

        // Скачиваем файл языка
        private void DownloadLanguageFile(string language, string savePath)
        {
            try
            {
                // URL для скачивания
                string url = $"https://github.com/tesseract-ocr/tessdata/raw/main/{language}.traineddata";

                Console.WriteLine($"Скачиваем с: {url}");

                using (WebClient client = new WebClient())
                {
                    // Устанавливаем User-Agent, чтобы GitHub не блокировал
                    client.Headers.Add("User-Agent", "ManagerApp/1.0");

                    // Скачиваем файл
                    client.DownloadFile(url, savePath);
                }

                Console.WriteLine($"Файл скачан: {savePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при скачивании: {ex.Message}");
                throw new Exception($"Не удалось скачать файл {language}.traineddata. Проверьте интернет соединение.");
            }
        }

        // Основной метод для чтения текста с картинки
        public List<string> ReadTextFromImage(string imagePath, string language = "rus")
        {
            List<string> result = new List<string>();

            try
            {
                Console.WriteLine($"Читаем текст с: {Path.GetFileName(imagePath)}");

                // 1. Предварительная обработка картинки
                string processedImage = PrepareImage(imagePath);

                // 2. Распознавание текста
                string text = RecognizeText(processedImage, language);

                // 3. Обработка результата
                result = CleanText(text);

                // 4. Удаляем временный файл
                if (processedImage != imagePath && File.Exists(processedImage))
                {
                    File.Delete(processedImage);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
                result.Add($"Ошибка: {ex.Message}");
            }

            return result;
        }

        // Подготовка картинки для распознавания
        private string PrepareImage(string imagePath)
        {
            // Простая проверка: если картинка слишком маленькая, обрабатываем
            using (Bitmap image = new Bitmap(imagePath))
            {
                if (image.Width < 100 || image.Height < 100)
                {
                    // Создаем временный файл
                    string tempPath = Path.GetTempFileName() + ".png";

                    // Увеличиваем размер и улучшаем качество
                    using (Bitmap processed = ImproveImageQuality(image))
                    {
                        processed.Save(tempPath, System.Drawing.Imaging.ImageFormat.Png);
                        return tempPath;
                    }
                }
            }

            return imagePath; // Картинка хорошая, не обрабатываем
        }

        // Улучшаем качество картинки
        private Bitmap ImproveImageQuality(Bitmap original)
        {
            // 1. Увеличиваем разрешение
            Bitmap result = new Bitmap(original.Width * 2, original.Height * 2);

            using (Graphics g = Graphics.FromImage(result))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(original, 0, 0, result.Width, result.Height);
            }

            // 2. Делаем черно-белой
            result = MakeBlackAndWhite(result);

            return result;
        }

        // Преобразуем картинку в черно-белую
        private Bitmap MakeBlackAndWhite(Bitmap original)
        {
            Bitmap result = new Bitmap(original.Width, original.Height);

            for (int x = 0; x < original.Width; x++)
            {
                for (int y = 0; y < original.Height; y++)
                {
                    Color pixel = original.GetPixel(x, y);

                    // Вычисляем яркость
                    int brightness = (pixel.R + pixel.G + pixel.B) / 3;

                    // Если яркость больше 128 - белый, иначе черный
                    if (brightness > 128)
                        result.SetPixel(x, y, Color.White);
                    else
                        result.SetPixel(x, y, Color.Black);
                }
            }

            return result;
        }

        // Распознаем текст с картинки
        private string RecognizeText(string imagePath, string language)
        {
            using (TesseractEngine engine = new TesseractEngine(_tessDataPath, language, EngineMode.Default))
            {
                using (Pix image = Pix.LoadFromFile(imagePath))
                {
                    using (Page page = engine.Process(image))
                    {
                        string text = page.GetText();
                        float confidence = page.GetMeanConfidence();

                        Console.WriteLine($"Уверенность распознавания: {confidence:P0}");

                        return text;
                    }
                }
            }
        }

        // Очищаем и форматируем текст
        private List<string> CleanText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new List<string> { "Текст не найден" };

            List<string> lines = new List<string>();

            // Разделяем на строки
            string[] rawLines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string line in rawLines)
            {
                string cleaned = line.Trim();

                // Убираем слишком короткие строки (меньше 2 символов)
                if (cleaned.Length >= 2)
                {
                    // Исправляем распространенные ошибки
                    cleaned = FixCommonErrors(cleaned);
                    lines.Add(cleaned);
                }
            }

            return lines;
        }

        // Исправляем частые ошибки распознавания
        private string FixCommonErrors(string text)
        {
            string result = text;

            // Заменяем английские буквы на русские в русском контексте
            Dictionary<string, string> replacements = new Dictionary<string, string>
            {
                { "o", "о" }, { "O", "О" }, { "c", "с" }, { "C", "С" },
                { "p", "р" }, { "P", "Р" }, { "y", "у" }, { "Y", "У" },
                { "a", "а" }, { "A", "А" }, { "e", "е" }, { "E", "Е" }
            };

            foreach (var replacement in replacements)
            {
                result = result.Replace(replacement.Key, replacement.Value);
            }

            // Убираем лишние пробелы
            while (result.Contains("  "))
                result = result.Replace("  ", " ");

            return result;
        }

        // Получаем список доступных языков
        public List<string> GetAvailableLanguages()
        {
            List<string> languages = new List<string>();

            if (Directory.Exists(_tessDataPath))
            {
                string[] files = Directory.GetFiles(_tessDataPath, "*.traineddata");

                foreach (string file in files)
                {
                    string language = Path.GetFileNameWithoutExtension(file);
                    languages.Add(language);
                }
            }

            return languages;
        }

        // Проверяем, готов ли Tesseract к работе
        public bool IsReady()
        {
            string rusFile = Path.Combine(_tessDataPath, "rus.traineddata");
            return File.Exists(rusFile) && new FileInfo(rusFile).Length > 1024 * 1024;
        }
    }
}