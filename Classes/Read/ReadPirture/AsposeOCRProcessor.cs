using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ManagerApp.Classes.Read.ReadPicture
{
    public class AsposeOCRProcessor : IOCRProcessor
    {
        private dynamic _ocr; // Используем dynamic из-за проблем со сборкой
        private bool _isReady;

        public AsposeOCRProcessor()
        {
            try
            {
                // Проверяем доступность Aspose.OCR
                var asposeType = Type.GetType("Aspose.OCR.AsposeOcr, Aspose.OCR");
                if (asposeType != null)
                {
                    _ocr = Activator.CreateInstance(asposeType);
                    _isReady = true;
                    Console.WriteLine("✅ Aspose.OCR готов к работе");
                }
                else
                {
                    _isReady = false;
                    Console.WriteLine("❌ Aspose.OCR не найден");
                }
            }
            catch (Exception ex)
            {
                _isReady = false;
                Console.WriteLine($"❌ Ошибка Aspose.OCR: {ex.Message}");
            }
        }

        public bool IsReady() => _isReady;

        public List<string> ProcessImage(string imagePath)
        {
            if (!_isReady)
            {
                return new List<string> { "Aspose.OCR не инициализирован" };
            }

            try
            {
                if (!File.Exists(imagePath))
                {
                    return new List<string> { $"Файл не найден: {imagePath}" };
                }

                Console.WriteLine($"📷 Aspose.OCR обработка: {Path.GetFileName(imagePath)}");

                // Используем рефлексию для вызова методов
                var settingsType = Type.GetType("Aspose.OCR.RecognitionSettings, Aspose.OCR");
                if (settingsType != null)
                {
                    var settings = Activator.CreateInstance(settingsType);

                    // Устанавливаем язык
                    var languageProperty = settingsType.GetProperty("Language");
                    if (languageProperty != null)
                    {
                        var languageEnum = Type.GetType("Aspose.OCR.Language, Aspose.OCR");
                        if (languageEnum != null)
                        {
                            var rusValue = Enum.Parse(languageEnum, "Rus");
                            languageProperty.SetValue(settings, rusValue);
                        }
                    }

                    // Вызываем RecognizeImage
                    var recognizeMethod = _ocr.GetType().GetMethod("RecognizeImage",
                        new Type[] { typeof(string), settingsType });

                    if (recognizeMethod != null)
                    {
                        var result = recognizeMethod.Invoke(_ocr, new object[] { imagePath, settings });

                        if (result != null)
                        {
                            var textProperty = result.GetType().GetProperty("RecognitionText");
                            if (textProperty != null)
                            {
                                string recognitionText = (string)textProperty.GetValue(result);

                                if (!string.IsNullOrWhiteSpace(recognitionText))
                                {
                                    var lines = recognitionText
                                        .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                                        .Where(line => !string.IsNullOrWhiteSpace(line.Trim()))
                                        .Select(line => line.Trim())
                                        .ToList();

                                    Console.WriteLine($"✅ Найдено строк: {lines.Count}");
                                    return lines;
                                }
                            }
                        }
                    }
                }

                return new List<string> { "Текст на изображении не найден" };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка обработки: {ex.Message}");
                return new List<string> { $"Ошибка: {ex.Message}" };
            }
        }
    }
}