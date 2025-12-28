using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;

namespace ManagerApp.Classes.Read.ReadPicture
{
    public class SimpleOCRProcessor
    {
        private ImageTextReader _reader;
        private bool _isReady;

        public SimpleOCRProcessor()
        {
            try
            {
                _reader = new ImageTextReader();
                _isReady = _reader.IsReady();

                if (_isReady)
                {
                    Console.WriteLine("✅ OCR готов к работе!");

                    // Показываем какие языки доступны
                    var languages = _reader.GetAvailableLanguages();
                    Console.WriteLine($"Доступные языки: {string.Join(", ", languages)}");
                }
                else
                {
                    Console.WriteLine("❌ OCR не готов. Проблема с файлами.");
                }
            }
            catch (Exception ex)
            {
                _isReady = false;
                Console.WriteLine($"Ошибка: {ex.Message}");
            }
        }

        // Основной метод обработки картинки
        public List<string> ProcessImage(string imagePath)
        {
            if (!_isReady)
            {
                return new List<string>
                {
                    "OCR не готов к работе",
                    "Проверьте подключение к интернету и перезапустите программу"
                };
            }

            // Проверяем формат файла
            string extension = Path.GetExtension(imagePath).ToLower();
            string[] supportedFormats = { ".png", ".jpg", ".jpeg", ".bmp", ".tiff", ".tif" };

            if (!supportedFormats.Contains(extension))
            {
                return new List<string>
                {
                    $"Неподдерживаемый формат: {extension}",
                    "Поддерживаемые форматы: PNG, JPG, BMP, TIFF"
                };
            }

            try
            {
                Console.WriteLine($"\n📷 Обрабатываем: {Path.GetFileName(imagePath)}");

                // Распознаем текст
                List<string> textLines = _reader.ReadTextFromImage(imagePath);

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

            if (lines.Count == 1 && lines[0].StartsWith("OCR не готов") ||
                lines[0].StartsWith("Неподдерживаемый") ||
                lines[0].StartsWith("Ошибка") ||
                lines[0].StartsWith("Текст не найден"))
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

        // Проверка доступности OCR
        public bool IsOCRReady()
        {
            return _isReady;
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
    }
}