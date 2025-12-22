using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using Tesseract;

namespace ManagerApp.Classes.Read.ReadPirture
{
    public class ImageTextReader
    {
        private string _tessDataPath;

        public ImageTextReader(string tessDataPath = null)
        {
            // Исправленный путь к tessdata
            _tessDataPath = tessDataPath ?? @"C:\Users\vimol\Desktop\managerApp\ManagerApp\Classes\tessdata";

            Console.WriteLine($"Tesseract data path: {_tessDataPath}");

            if (!Directory.Exists(_tessDataPath))
            {
                throw new DirectoryNotFoundException(
                    $"Папка tessdata не найдена по пути: {_tessDataPath}\n" +
                    "Создайте папку 'tessdata' в корне проекта и добавьте файлы rus.traineddata и eng.traineddata");
            }

            // Проверяем наличие языковых файлов
            CheckLanguageFiles();

            // Проверяем версию файлов
            CheckTessDataQuality();
        }

        private void CheckLanguageFiles()
        {
            var requiredLanguages = new[] { "rus", "eng" };
            var missingFiles = new List<string>();

            foreach (var lang in requiredLanguages)
            {
                string filePath = Path.Combine(_tessDataPath, $"{lang}.traineddata");
                if (!File.Exists(filePath))
                {
                    missingFiles.Add($"{lang}.traineddata");
                }
                else
                {
                    long fileSize = new FileInfo(filePath).Length;
                    Console.WriteLine($"Найден языковой файл: {Path.GetFileName(filePath)} (Size: {FormatFileSize(fileSize)})");

                    // Предупреждение если файл слишком маленький
                    if (fileSize < 5 * 1024 * 1024) // меньше 5MB
                    {
                        Console.WriteLine($"ВНИМАНИЕ: Файл {lang}.traineddata слишком мал ({FormatFileSize(fileSize)}). " +
                                          "Скачайте улучшенную версию с https://github.com/tesseract-ocr/tessdata_best");
                    }
                }
            }

            if (missingFiles.Any())
            {
                throw new FileNotFoundException(
                    $"Отсутствуют языковые файлы Tesseract: {string.Join(", ", missingFiles)}\n" +
                    $"Путь к tessdata: {_tessDataPath}\n" +
                    $"Скачайте файлы с: https://github.com/tesseract-ocr/tessdata_best\n" +
                    $"И поместите их в папку: {_tessDataPath}");
            }
        }

        private void CheckTessDataQuality()
        {
            Console.WriteLine("=== СОВЕТЫ ДЛЯ УЛУЧШЕНИЯ КАЧЕСТВА OCR ===");
            Console.WriteLine("1. Скачайте улучшенные файлы tessdata_best с GitHub");
            Console.WriteLine("2. Убедитесь, что изображения имеют минимум 300 DPI");
            Console.WriteLine("3. Используйте черно-белые изображения с контрастом");
            Console.WriteLine("4. Обрежьте лишние поля вокруг текста");
            Console.WriteLine("===========================================");
        }

        public List<string> ReadTextFromImage(string imagePath, string language = "rus+eng")
        {
            var resultLines = new List<string>();

            try
            {
                Console.WriteLine($"\n=== НАЧАЛО РАСПОЗНАВАНИЯ: {Path.GetFileName(imagePath)} ===");

                // Проверяем существование файла
                if (!File.Exists(imagePath))
                {
                    throw new FileNotFoundException($"Файл не найден: {imagePath}");
                }

                // Автоматически предобрабатываем изображение
                string processedImagePath = PreprocessImageForOCR(imagePath);
                bool isProcessed = processedImagePath != imagePath;

                try
                {
                    // Настраиваем OCR в зависимости от типа изображения
                    var ocrResult = PerformOCR(processedImagePath, language, isProcessed);

                    // Постобработка результатов
                    resultLines = PostProcessOCRResults(ocrResult);

                    // Дополнительная попытка если результаты плохие
                    if (resultLines.Count == 0 || resultLines.All(l => l.Length < 3))
                    {
                        Console.WriteLine("Попытка №2 с другими настройками...");
                        ocrResult = PerformOCRAlternative(processedImagePath, language);
                        resultLines = PostProcessOCRResults(ocrResult);
                    }
                }
                finally
                {
                    // Удаляем временный файл если он создавался
                    if (isProcessed && File.Exists(processedImagePath))
                    {
                        try
                        {
                            File.Delete(processedImagePath);
                        }
                        catch { }
                    }
                }

                Console.WriteLine($"=== ЗАВЕРШЕНО. Распознано строк: {resultLines.Count} ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка ReadTextFromImage: {ex}");
                resultLines.Add($"Ошибка распознавания: {ex.Message}");
            }

            return resultLines;
        }

        private OcrResult PerformOCR(string imagePath, string language, bool isProcessed)
        {
            // Пробуем разные режимы сегментации
            var results = new List<OcrResult>();

            // Основной режим - LSTM (лучше всего для русского)
            results.Add(ProcessWithSettings(imagePath, language, EngineMode.LstmOnly, PageSegMode.Auto));

            // Дополнительные режимы для сложных случаев
            if (results[0].Confidence < 60)
            {
                results.Add(ProcessWithSettings(imagePath, language, EngineMode.TesseractAndLstm, PageSegMode.SingleBlock));
                results.Add(ProcessWithSettings(imagePath, language, EngineMode.Default, PageSegMode.SingleColumn));
            }

            // Выбираем лучший результат
            return results.OrderByDescending(r => r.Confidence).First();
        }

        private OcrResult PerformOCRAlternative(string imagePath, string language)
        {
            // Альтернативные настройки для сложных случаев
            return ProcessWithSettings(imagePath, language, EngineMode.TesseractOnly, PageSegMode.SingleWord);
        }

        private OcrResult ProcessWithSettings(string imagePath, string language, EngineMode engineMode, PageSegMode pageSegMode)
        {
            using (var engine = new TesseractEngine(_tessDataPath, language, engineMode))
            {
                // Оптимальные настройки для русского и английского
                engine.SetVariable("tessedit_pageseg_mode", ((int)pageSegMode).ToString());
                engine.SetVariable("preserve_interword_spaces", "1");
                engine.SetVariable("user_defined_dpi", "300");
                engine.SetVariable("textord_min_linesize", "2.5");

                // Для различения кириллицы и латиницы
                engine.SetVariable("tessedit_char_blacklist", "|\\/~`");

                // Если уверенность низкая - пробуем без whitelist
                engine.SetVariable("tessedit_char_whitelist",
                    "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz" +
                    "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя" +
                    "0123456789 .,;:!?\"'()[]{}<>-–—+=*/\\@#$%^&«»„“”‘’…");

                using (var img = Pix.LoadFromFile(imagePath))
                {
                    using (var page = engine.Process(img))
                    {
                        float confidence = page.GetMeanConfidence();
                        string text = page.GetText();

                        Console.WriteLine($"Режим: {engineMode}/{pageSegMode}, Уверенность: {confidence:F1}%");

                        if (confidence > 50 && !string.IsNullOrWhiteSpace(text))
                        {
                            Console.WriteLine($"Текст ({text.Length} символов): {text.Substring(0, Math.Min(100, text.Length))}...");
                        }

                        return new OcrResult
                        {
                            Text = text ?? "",
                            Confidence = confidence,
                            EngineMode = engineMode,
                            PageSegMode = pageSegMode
                        };
                    }
                }
            }
        }

        private List<string> PostProcessOCRResults(OcrResult ocrResult)
        {
            if (string.IsNullOrWhiteSpace(ocrResult.Text))
                return new List<string>();

            // Основная постобработка
            string processedText = PostProcessText(ocrResult.Text);

            // Разделение на строки с фильтрацией
            var lines = processedText
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line) && line.Length > 1)
                .ToList();

            // Если уверенность низкая, добавляем предупреждение
            if (ocrResult.Confidence < 60 && lines.Count > 0)
            {
                lines.Insert(0, $"⚠️ Низкая уверенность распознавания: {ocrResult.Confidence:F1}%");
            }

            return lines;
        }

        private string PostProcessText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // 1. Заменяем часто путаемые символы
            var result = new StringBuilder(text);

            // Словарь замен для русско-английской путаницы
            var replacements = new Dictionary<string, string>
            {
                { "o", "о" }, { "O", "О" }, { "c", "с" }, { "C", "С" },
                { "p", "р" }, { "P", "Р" }, { "y", "у" }, { "Y", "У" },
                { "x", "х" }, { "X", "Х" }, { "a", "а" }, { "A", "А" },
                { "e", "е" }, { "E", "Е" }, { "B", "В" }, { "H", "Н" },
                { "K", "К" }, { "M", "М" }, { "T", "Т" }
            };

            // 2. Исправляем очевидные ошибки
            string processed = text
                .Replace("|", "I").Replace("[", "I").Replace("]", "I")
                .Replace("1", "I").Replace("0", "O").Replace("l", "I")
                .Replace("  ", " ").Replace("   ", " ") // Убираем лишние пробелы
                .Trim();

            // 3. Автоматическая замена символов в русском контексте
            foreach (var replacement in replacements)
            {
                if (processed.Contains(replacement.Key))
                {
                    // Заменяем только если контекст преимущественно русский
                    int englishIdx = processed.IndexOf(replacement.Key);
                    if (englishIdx >= 0)
                    {
                        // Проверяем окрестность символа
                        string context = GetContext(processed, englishIdx, 3);
                        if (IsMostlyRussian(context))
                        {
                            processed = processed.Replace(replacement.Key, replacement.Value);
                        }
                    }
                }
            }

            // 4. Исправляем соединенные слова
            processed = FixMergedWords(processed);

            return processed;
        }

        private string GetContext(string text, int position, int radius)
        {
            int start = Math.Max(0, position - radius);
            int end = Math.Min(text.Length - 1, position + radius);
            return text.Substring(start, end - start + 1);
        }

        private bool IsMostlyRussian(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;

            int russianCount = text.Count(c => (c >= 'А' && c <= 'я') || c == 'Ё' || c == 'ё');
            int englishCount = text.Count(c => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'));

            return russianCount > englishCount;
        }

        private string FixMergedWords(string text)
        {
            // Простая эвристика: если слово слишком длинное и содержит буквы разного регистра,
            // возможно это два слова, слипшиеся вместе
            var words = text.Split(' ');
            var fixedWords = new List<string>();

            foreach (var word in words)
            {
                if (word.Length > 10)
                {
                    // Ищем границу между строчной и прописной буквой
                    for (int i = 1; i < word.Length - 3; i++)
                    {
                        if (char.IsLower(word[i]) && char.IsUpper(word[i + 1]))
                        {
                            string fixedWord = word.Insert(i + 1, " ");
                            fixedWords.Add(fixedWord);
                            break;
                        }
                    }

                    if (fixedWords.Count == 0 || fixedWords.Last() != word)
                    {
                        fixedWords.Add(word);
                    }
                }
                else
                {
                    fixedWords.Add(word);
                }
            }

            return string.Join(" ", fixedWords);
        }

        // НОВЫЙ УЛУЧШЕННЫЙ МЕТОД ПРЕДОБРАБОТКИ
        public string PreprocessImageForOCR(string inputPath, string outputPath = null)
        {
            if (outputPath == null)
            {
                outputPath = Path.Combine(Path.GetTempPath(), $"ocr_preprocessed_{Guid.NewGuid():N}.png");
            }

            try
            {
                Console.WriteLine($"Предобработка изображения...");

                using (var original = new Bitmap(inputPath))
                {
                    // 1. Увеличиваем DPI до 300
                    original.SetResolution(300, 300);

                    // 2. Определяем, нужно ли предобрабатывать
                    bool needsPreprocessing = CheckIfNeedsPreprocessing(original);

                    if (!needsPreprocessing)
                    {
                        Console.WriteLine("Изображение не требует предобработки");
                        return inputPath;
                    }

                    // 3. Конвертируем в черно-белое с адаптивным порогом
                    var bwBitmap = ConvertToBlackAndWhiteAdaptive(original);

                    // 4. Убираем шум
                    var denoised = RemoveNoise(bwBitmap);

                    // 5. Увеличиваем контраст
                    var contrasted = EnhanceContrast(denoised, 2.0f);

                    // 6. Увеличиваем резкость
                    var sharpened = SharpenImage(contrasted);

                    // 7. Сохраняем в PNG
                    sharpened.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);

                    Console.WriteLine($"Изображение предобработано: {outputPath}");
                    return outputPath;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при предобработке: {ex.Message}");
                return inputPath;
            }
        }

        private bool CheckIfNeedsPreprocessing(Bitmap image)
        {
            // Простая проверка: если изображение уже черно-белое или имеет низкий контраст
            int colorPixelCount = 0;
            int totalPixels = image.Width * image.Height;

            for (int y = 0; y < Math.Min(100, image.Height); y += 10)
            {
                for (int x = 0; x < Math.Min(100, image.Width); x += 10)
                {
                    Color color = image.GetPixel(x, y);
                    // Проверяем, является ли пиксель цветным (не оттенком серого)
                    if (Math.Abs(color.R - color.G) > 10 || Math.Abs(color.R - color.B) > 10)
                    {
                        colorPixelCount++;
                    }
                }
            }

            return colorPixelCount > 10 || totalPixels < 100000; // Маленькие изображения всегда обрабатываем
        }

        private Bitmap ConvertToBlackAndWhiteAdaptive(Bitmap original)
        {
            var result = new Bitmap(original.Width, original.Height);

            // Вычисляем среднюю яркость для адаптивного порога
            long totalBrightness = 0;
            int sampleCount = 0;

            for (int y = 0; y < original.Height; y += 10)
            {
                for (int x = 0; x < original.Width; x += 10)
                {
                    Color color = original.GetPixel(x, y);
                    totalBrightness += (int)(color.R * 0.299 + color.G * 0.587 + color.B * 0.114);
                    sampleCount++;
                }
            }

            int threshold = sampleCount > 0 ? (int)(totalBrightness / sampleCount) : 128;
            threshold = Math.Max(100, Math.Min(180, threshold)); // Ограничиваем диапазон

            Console.WriteLine($"Адаптивный порог: {threshold}");

            // Применяем пороговое преобразование
            for (int y = 0; y < original.Height; y++)
            {
                for (int x = 0; x < original.Width; x++)
                {
                    Color color = original.GetPixel(x, y);
                    int brightness = (int)(color.R * 0.299 + color.G * 0.587 + color.B * 0.114);
                    Color newColor = brightness > threshold ? Color.White : Color.Black;
                    result.SetPixel(x, y, newColor);
                }
            }

            return result;
        }

        private Bitmap RemoveNoise(Bitmap image)
        {
            var result = new Bitmap(image.Width, image.Height);

            for (int y = 1; y < image.Height - 1; y++)
            {
                for (int x = 1; x < image.Width - 1; x++)
                {
                    int blackCount = 0;
                    int whiteCount = 0;

                    // Проверяем окрестность 3x3
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            Color neighbor = image.GetPixel(x + dx, y + dy);
                            if (neighbor.R < 128) // Черный
                                blackCount++;
                            else
                                whiteCount++;
                        }
                    }

                    // Если пиксель одинокий (окружен противоположными пикселями), исправляем
                    Color current = image.GetPixel(x, y);
                    if (current.R < 128 && blackCount <= 2) // Одинокий черный пиксель
                    {
                        result.SetPixel(x, y, Color.White);
                    }
                    else if (current.R >= 128 && whiteCount <= 2) // Одинокий белый пиксель
                    {
                        result.SetPixel(x, y, Color.Black);
                    }
                    else
                    {
                        result.SetPixel(x, y, current);
                    }
                }
            }

            return result;
        }

        private Bitmap EnhanceContrast(Bitmap image, float factor)
        {
            var result = new Bitmap(image.Width, image.Height);

            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    Color color = image.GetPixel(x, y);
                    int value = color.R; // В ч/б все каналы одинаковые

                    // Усиливаем контраст
                    int newValue = (int)((value - 128) * factor + 128);
                    newValue = Math.Max(0, Math.Min(255, newValue));

                    result.SetPixel(x, y, Color.FromArgb(newValue, newValue, newValue));
                }
            }

            return result;
        }

        private Bitmap SharpenImage(Bitmap image)
        {
            var result = new Bitmap(image.Width, image.Height);

            // Простая матрица резкости 3x3
            int[,] kernel = { { 0, -1, 0 }, { -1, 5, -1 }, { 0, -1, 0 } };
            int kernelSize = 3;
            int kernelRadius = kernelSize / 2;

            for (int y = kernelRadius; y < image.Height - kernelRadius; y++)
            {
                for (int x = kernelRadius; x < image.Width - kernelRadius; x++)
                {
                    int sum = 0;

                    for (int ky = -kernelRadius; ky <= kernelRadius; ky++)
                    {
                        for (int kx = -kernelRadius; kx <= kernelRadius; kx++)
                        {
                            Color pixel = image.GetPixel(x + kx, y + ky);
                            int value = pixel.R;
                            sum += value * kernel[ky + kernelRadius, kx + kernelRadius];
                        }
                    }

                    sum = Math.Max(0, Math.Min(255, sum));
                    result.SetPixel(x, y, Color.FromArgb(sum, sum, sum));
                }
            }

            return result;
        }

        public List<string> GetAvailableLanguages()
        {
            var languages = new List<string>();

            try
            {
                if (Directory.Exists(_tessDataPath))
                {
                    var languageFiles = Directory.GetFiles(_tessDataPath, "*.traineddata");

                    foreach (var file in languageFiles)
                    {
                        string language = Path.GetFileNameWithoutExtension(file);
                        languages.Add(language);
                        Console.WriteLine($"Найден язык: {language}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при получении списка языков: {ex.Message}");
            }

            return languages;
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        private class OcrResult
        {
            public string Text { get; set; }
            public float Confidence { get; set; }
            public EngineMode EngineMode { get; set; }
            public PageSegMode PageSegMode { get; set; }
        }
    }
}