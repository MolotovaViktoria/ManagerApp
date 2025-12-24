using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;

namespace ManagerApp.Classes.Read.ReadPirture
{
    public class ImageOCRProcessor
    {
        private ImageTextReader _imageReader;
        private bool _tesseractAvailable = false;

        public ImageOCRProcessor()
        {
            try
            {
                _imageReader = new ImageTextReader();
                _tesseractAvailable = true;
                Console.WriteLine("=== TESSERACT OCR ИНИЦИАЛИЗИРОВАН ===");

                // Проверяем и показываем доступные языки
                var languages = _imageReader.GetAvailableLanguages();
                Console.WriteLine($"Доступные языки: {string.Join(", ", languages)}");

                if (!languages.Contains("rus") || !languages.Contains("eng"))
                {
                    //MessageBox.Show($"Не найдены необходимые языковые файлы.\n" +
                    //              $"Найдены: {string.Join(", ", languages)}\n" +
                    //              $"Требуются: rus, eng\n\n" +
                    //              $"Скачайте файлы с: https://github.com/tesseract-ocr/tessdata_best",
                    //    "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                _tesseractAvailable = false;
                Console.WriteLine($"Ошибка инициализации Tesseract: {ex.Message}");
                //MessageBox.Show($"Tesseract OCR не доступен!\n\n{ex.Message}\n\n" +
                //              "Решение:\n" +
                //              "1. Скачайте tessdata_best с GitHub\n" +
                //              "2. Поместите rus.traineddata и eng.traineddata в папку tessdata\n" +
                //              "3. Перезапустите приложение",
                //    "Ошибка OCR", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public List<string> ProcessImageFile(string filePath)
        {
            var resultLines = new List<string>();

            try
            {
                if (!_tesseractAvailable)
                {
                    return new List<string>
            {
                "❌ Tesseract OCR не инициализирован",
                "Установите языковые файлы Tesseract",
                $"Файл: {Path.GetFileName(filePath)}"
            };
                }

                Console.WriteLine($"\n{'='.Repeat(60)}");
                Console.WriteLine($"ОБРАБОТКА: {Path.GetFileName(filePath)}");
                Console.WriteLine($"{'='.Repeat(60)}");

                // Проверка формата файла
                string extension = Path.GetExtension(filePath)?.ToLower();
                if (!FormatLists.ImageFormatList.Contains(extension))
                {
                    throw new ArgumentException($"Неподдерживаемый формат: {extension}");
                }

                // Основное распознавание - получаем только чистый текст
                resultLines = _imageReader.ReadTextFromImage(filePath, "rus");

                // Фильтруем пустые строки и убираем служебные сообщения
                resultLines = resultLines
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .Where(line => !line.Contains("уверенность") &&
                                  !line.Contains("низкая") &&
                                  !line.Contains("предупреждение") &&
                                  !line.Contains("распознавания") &&
                                  !line.StartsWith("⚠️"))
                    .ToList();

                // Если ничего не найдено
                if (resultLines.Count == 0)
                {
                    resultLines = new List<string> { "Текст на изображении не найден" };
                }

                Console.WriteLine($"Обработка завершена. Результат: {resultLines.Count} строк");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка обработки: {ex}");
                resultLines = new List<string> { $"Ошибка обработки: {ex.Message}" };
            }

            return resultLines;
        }
        public List<string> GetCleanTextFromImage(string filePath)
        {
            if (!_tesseractAvailable)
            {
                return new List<string> { "OCR не доступен" };
            }

            try
            {
                // Получаем текст
                var lines = _imageReader.ReadTextFromImage(filePath);

                // Фильтруем служебную информацию
                return lines.Where(line => !IsServiceLine(line)).ToList();
            }
            catch
            {
                return new List<string> { "Ошибка распознавания" };
            }
        }

        private bool IsServiceLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return true;

            // Список ключевых фраз, которые считаются служебными
            string[] servicePhrases =
            {
        "уверенность", "распознавания", "низкая", "предупреждение",
        "РЕЖИМ:", "⚠️", "Обработка", "Файл:", "Размер:", "DPI",
        "Начало распознавания", "Завершено", "Строк:"
    };

            return servicePhrases.Any(phrase =>
                line.IndexOf(phrase, StringComparison.OrdinalIgnoreCase) >= 0);
        }
        public string FormatOCRResult(List<string> textLines)
        {
            if (textLines == null || textLines.Count == 0)
            {
                return "Текст на изображении не найден.";
            }

            var sb = new StringBuilder();
            for (int i = 0; i < textLines.Count; i++)
            {
                sb.AppendLine(textLines[i]);
            }

            return sb.ToString();
        }

        public Dictionary<string, object> GetOCRStatistics(List<string> textLines)
        {
            var stats = new Dictionary<string, object>();

            if (textLines != null && textLines.Count > 0)
            {
                int totalLines = textLines.Count;
                int totalCharacters = 0;
                int totalWords = 0;
                int russianChars = 0;
                int englishChars = 0;
                int digitChars = 0;

                foreach (var line in textLines)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        totalCharacters += line.Length;

                        // Подсчет слов
                        var words = line.Split(new[] { ' ', '\t', '.', ',', ';', ':', '!', '?' },
                            StringSplitOptions.RemoveEmptyEntries);
                        totalWords += words.Length;

                        // Подсчет типов символов
                        foreach (char c in line)
                        {
                            if (c >= 'А' && c <= 'я' || c == 'Ё' || c == 'ё')
                                russianChars++;
                            else if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'))
                                englishChars++;
                            else if (char.IsDigit(c))
                                digitChars++;
                        }
                    }
                }

                stats["Количество строк"] = totalLines;
                stats["Количество символов"] = totalCharacters;
                stats["Количество слов"] = totalWords;
                stats["Русские символы"] = russianChars;
                stats["Английские символы"] = englishChars;
                stats["Цифры"] = digitChars;
                stats["Средняя длина строки"] = totalLines > 0 ?
                    Math.Round((double)totalCharacters / totalLines, 1) : 0;
            }

            return stats;
        }

        public bool IsTesseractAvailable()
        {
            return _tesseractAvailable;
        }

        public void ShowOCRHelp()
        {
            MessageBox.Show(
                "Для улучшения качества распознавания:\n\n" +
                "1. Используйте изображения с разрешением 300+ DPI\n" +
                "2. Убедитесь в хорошем контрасте текста\n" +
                "3. Текст должен быть горизонтальным\n" +
                "4. Используйте стандартные шрифты\n" +
                "5. Скачайте tessdata_best с GitHub\n\n" +
                "Поддерживаемые форматы:\n" +
                "• PNG, JPG/JPEG, BMP, TIFF",
                "Советы по использованию OCR",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
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
    }

    public static class StringExtensions
    {
        public static string Repeat(this char ch, int count)
        {
            return new string(ch, Math.Max(0, count));
        }
    }

    public static class FormatLists
    {
        public static readonly List<string> ImageFormatList = new List<string>
        {
            ".png", ".jpg", ".jpeg", ".bmp", ".tiff", ".tif", ".gif"
        };
    }
}