using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using ManagerApp.Classes.Read;
using ManagerApp.Data.ScharedData;
using Microsoft.Win32;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using Table = DocumentFormat.OpenXml.Wordprocessing.Table;
using TableRow = DocumentFormat.OpenXml.Wordprocessing.TableRow;
using TableCell = DocumentFormat.OpenXml.Wordprocessing.TableCell;
using Paragraph = DocumentFormat.OpenXml.Wordprocessing.Paragraph;
using Run = DocumentFormat.OpenXml.Wordprocessing.Run;
using Text = DocumentFormat.OpenXml.Wordprocessing.Text;
using Body = DocumentFormat.OpenXml.Wordprocessing.Body;

namespace ManagerApp.Pages
{
    public partial class ConvertFilePage : Page
    {
        // Информация для доступа к AI - ОСНОВНОЙ
        private const string PrimaryApiToken = "eyJhbGciOiJSUzUxMiIsInR5cCI6IkpXVCIsImtpZCI6IjFrYnhacFJNQGJSI0tSbE1xS1lqIn0.eyJ1c2VyIjoibXoxNjUxODMiLCJ0eXBlIjoiYXBpX2tleSIsImFwaV9rZXlfaWQiOiIxZmI0YWQ0NS0zYjBjLTRiMGQtODJjZS02NzQ0NzFkYWVhYTkiLCJpYXQiOjE3NzIzNzg2Mzl9.K0nQulksfXGzqPYOeudwSVbS2cv0ryHqRsVbElmNg87FuA8BOdUeBq5mPQCr-H0h3cXgg62CJNTfa1ULBd3yCC0y4POj2KbIe_gX_y1May08SC0YP9dQyFEhBgmtcIgOBAg-PvGwlOkkFnjKPxCjOsEkYe2Uf2NaSFqn3yjfZYydxrLTSk4DNlro0zZi7AbAEJvlrefj3fwDdSV3IJIQMApffWhlFpxiqQhmGURMlWdvREadoGY-rtmaZYFVOuZccJeKznQ5bmlZ4KgfRKViacAfVL6zDMP3jLQlWY7aw0ujOG13DUfrwAHSGWXXM-t6CaMQI4DHGjUzHHlZrXZ8h7565am41xxsaE0Alxi7y5vLQrkQvyhEXlWC9Ris3jIaKcIUCMvVrYQCIzPxUurEoKrnEZ8GlZM29mJXexFX_5BP0god0fY08sVZ2IgEu2kTiUJpthO3WyDxQLk3ALAQySYgrkx4YRM-h1YqK8nKnM6vy_E1sA0Jh3mcuVYHyZkS";
        private const string PrimaryApiUrl = "https://agent.timeweb.cloud/api/v1/cloud-ai/agents/7ed67ebb-f658-4716-ac81-35422c12cb21/v1/chat/completions";

        // Информация для доступа к AI - ЗАПАСНОЙ
        private const string BackupApiToken = "eyJhbGciOiJSUzUxMiIsInR5cCI6IkpXVCIsImtpZCI6IjFrYnhacFJNQGJSI0tSbE1xS1lqIn0.eyJ1c2VyIjoibXoxNjUxODMiLCJ0eXBlIjoiYXBpX2tleSIsImFwaV9rZXlfaWQiOiI1ZjVmOGU0OC00ZjAyLTQwOTEtYTlkNC0zZmZjYmZiNGU3NGMiLCJpYXQiOjE3NzI3MTg4OTB9.zj0oLSbBNc6ZZgNfcJn8zsdtpSuYHvsdI7IwKjZqUa-9DGLfSq0cyGUxJmi-YcS2zpDal09gpkOhRLMssMJ29WWCNLWjt8mG92B5-MG9T8y6pAcg2s-wDOusO07jYWomR7eXWml8CvODB3hahArcOM6R_FqUNj1PUGnFNFz0JiIl1yu1idT0SfUO-iGam1aclujeX09PMKXUPcN33gOGXrdWTiFAG7vywZCv-vOaSDEsHH7tlVPggns0BbdgOOzrBqR8f1CiGFlvoo_xNr14sf1hvyDvAK5zf5qQl_JL2M7weSK9KUt3vpCqFoaLzgRp4ikQEX_08nKqf3I-cD_SgpDUhCAR8TgCTvsfjD3ABEgfGDfmuTld_MY2wqhLfPyWi1fr4TYiThcoyeGwL_U6mNnPz_lQUvm-zHem2oYAU3FyvKy2W-wO4YFu-DcZKjz_OY59aGM3l62SOxjh96n2eCSjdv9FF6Gu3344G9RumRzftDaTBUOJULVuu-_W4SiE";
        private const string BackupApiUrl = "https://agent.timeweb.cloud/api/v1/cloud-ai/agents/db9009b9-7858-4e0a-8568-d2bc975dbbe8/v1/chat/completions";

        private string _currentFilePath;
        private List<ExtractedProductInfo> _extractedProducts;
        private string _generatedFileName;

        public ConvertFilePage()
        {
            InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            SetupFileUploadHandlers();
        }

        private void SetupFileUploadHandlers()
        {
            if (FileUploadArea != null)
            {
                FileUploadArea.AllowDrop = true;
            }
        }

        private void FileUploadArea_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                FileUploadArea.Background = new SolidColorBrush(ColorConverter.ConvertFromString("#FFE8E0") as Color? ?? Colors.Orange);
            }
        }

        private void FileUploadArea_DragLeave(object sender, DragEventArgs e)
        {
            FileUploadArea.Background = new SolidColorBrush(ColorConverter.ConvertFromString("#FFF8F5") as Color? ?? Colors.White);
        }

        private async void FileUploadArea_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    await ProcessFileAsync(files[0]);
                }
            }
            FileUploadArea.Background = new SolidColorBrush(ColorConverter.ConvertFromString("#FFF8F5") as Color? ?? Colors.White);
        }

        private async void SelectFileButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Все поддерживаемые файлы|*.docx;*.doc;*.xlsx;*.xls;*.pdf;*.txt|Word документы (*.docx;*.doc)|*.docx;*.doc|Excel файлы (*.xlsx;*.xls)|*.xlsx;*.xls|PDF файлы (*.pdf)|*.pdf|Текстовые файлы (*.txt)|*.txt",
                Title = "Выберите файл для конвертации"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                await ProcessFileAsync(openFileDialog.FileName);
            }
        }

        // ЗАМЕНИТЕ метод ProcessFileAsync на этот:

        private async Task ProcessFileAsync(string filePath)
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                _currentFilePath = filePath;
                FileNameText.Text = $"Загружен: {Path.GetFileName(filePath)}";
                FileNameText.Visibility = Visibility.Visible;

                ShowProgress("Чтение файла...");

                string extension = Path.GetExtension(filePath).ToLower();
                string fileText = "";

                // ИСПОЛЬЗУЕМ ТОТ ЖЕ ReadRequst, ЧТО И В DombPage
                ReadRequst reader = new ReadRequst();
                fileText = await Task.Run(() => reader.ReadFileAll(filePath));

                if (string.IsNullOrWhiteSpace(fileText))
                {
                    MessageBox.Show("Не удалось извлечь текст из файла", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Логируем первые 500 символов для проверки
                Console.WriteLine($"=== ТЕКСТ ИЗ ФАЙЛА ({fileText.Length} символов) ===");
                Console.WriteLine(fileText.Length > 500 ? fileText.Substring(0, 500) : fileText);
                Console.WriteLine("=== КОНЕЦ ТЕКСТА ===");

                ShowProgress("Анализ с помощью AI...");

                // ИСПОЛЬЗУЕМ ТОТ ЖЕ МЕТОД, ЧТО И В DombPage
                _extractedProducts = await ExtractProductsFromTextWithFallbackAsync(fileText);

                if (_extractedProducts == null || _extractedProducts.Count == 0)
                {
                    MessageBox.Show("Не удалось извлечь товары из файла.", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Показываем результат
                ShowExtractedProducts();

                // Сохраняем в Word файл
                await SaveToWordFile();

                HideProgress();
            }
            catch (Exception ex)
            {
                HideProgress();
                MessageBox.Show($"Ошибка при обработке файла: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateStatus($"Ошибка: {ex.Message}", "❌");
                Console.WriteLine($"ОШИБКА: {ex.Message}");
                Console.WriteLine($"СТЕК: {ex.StackTrace}");
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }
        private void ShowExtractedProducts()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=".PadRight(100, '='));
            sb.AppendLine("СТРУКТУРИРОВАННАЯ ЗАЯВКА");
            sb.AppendLine("=".PadRight(100, '='));
            sb.AppendLine();
            sb.AppendLine($"Дата формирования: {DateTime.Now:dd.MM.yyyy HH:mm:ss}");
            sb.AppendLine($"Количество позиций: {_extractedProducts.Count}");
            sb.AppendLine();
            sb.AppendLine("-".PadRight(100, '-'));
            sb.AppendLine($"{"№",-5} {"НАИМЕНОВАНИЕ",-40} {"КОЛ-ВО",-10} {"ЕД.ИЗМ",-8} {"ОПИСАНИЕ",-30}");
            sb.AppendLine("-".PadRight(100, '-'));

            int number = 1;
            foreach (var product in _extractedProducts)
            {
                string name = product.Name.Length > 37 ? product.Name.Substring(0, 34) + "..." : product.Name;
                string desc = product.Description?.Length > 27 ? product.Description.Substring(0, 24) + "..." : (product.Description ?? "");
                sb.AppendLine($"{number,-5} {name,-40} {product.Quantity,10} {product.MeasureSymbol,-8} {desc,-30}");
                number++;
            }

            sb.AppendLine("-".PadRight(100, '-'));
            sb.AppendLine();
            sb.AppendLine("=".PadRight(100, '='));

            txtResult.Text = sb.ToString();
            txtResult.Visibility = Visibility.Visible;
            btnDownload.IsEnabled = true;
            UpdateStatus($"Готово! Найдено товаров: {_extractedProducts.Count}", "✅");
        }

        private async Task SaveToWordFile()
        {
            try
            {
                _generatedFileName = $"Структурированная_заявка_{DateTime.Now:yyyyMMdd_HHmmss}.docx";
                string tempPath = Path.Combine(Path.GetTempPath(), _generatedFileName);

                await SaveAsWordDocumentAsync(tempPath, _extractedProducts);

                // Открываем диалог сохранения
                SaveFileDialog saveDialog = new SaveFileDialog
                {
                    Title = "Сохранить структурированную заявку",
                    Filter = "Word документы (*.docx)|*.docx",
                    FileName = _generatedFileName
                };

                if (saveDialog.ShowDialog() == true)
                {
                    File.Copy(tempPath, saveDialog.FileName, true);
                    File.Delete(tempPath);

                    // АВТОМАТИЧЕСКИ ОТКРЫВАЕМ ФАЙЛ
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = saveDialog.FileName,
                            UseShellExecute = true
                        });
                        Console.WriteLine($"Файл открыт: {saveDialog.FileName}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Не удалось открыть файл: {ex.Message}");
                    }

                    MessageBox.Show($"Файл сохранен и открыт:\n{saveDialog.FileName}", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);

                    //// Предлагаем открыть папку с файлом (опционально)
                    //var result = MessageBox.Show("Открыть папку с файлом?", "Успех",
                    //    MessageBoxButton.YesNo, MessageBoxImage.Question);

                    //if (result == MessageBoxResult.Yes)
                    //{
                    //    string directory = Path.GetDirectoryName(saveDialog.FileName);
                    //    Process.Start("explorer.exe", directory);
                    //}
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении файла: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        // ============ МЕТОДЫ ИЗ DombPage ============

        private async Task<List<ExtractedProductInfo>> ExtractProductsFromTextWithFallbackAsync(string text)
        {
            List<ExtractedProductInfo> products = null;

            try
            {
                Console.WriteLine("🔄 Пробуем основной AI провайдер...");
                products = await ExtractProductsFromTextAsync(text, PrimaryApiToken, PrimaryApiUrl);
                if (products != null && products.Count > 0)
                {
                    Console.WriteLine($"✅ Основной AI вернул {products.Count} товаров");
                    return products;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Основной AI недоступен: {ex.Message}");
            }

            try
            {
                Console.WriteLine("🔄 Пробуем запасной AI провайдер...");
                products = await ExtractProductsFromTextAsync(text, BackupApiToken, BackupApiUrl);
                if (products != null && products.Count > 0)
                {
                    Console.WriteLine($"✅ Запасной AI вернул {products.Count} товаров");
                    return products;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Запасной AI недоступен: {ex.Message}");
            }

            Console.WriteLine("⚠️ Оба AI недоступны, используем резервный метод извлечения");
            return await ExtractProductsManuallyWithDetailsAsync(text);
        }

        private async Task<List<ExtractedProductInfo>> ExtractProductsFromTextAsync(string text, string apiToken, string apiUrl)
        {
            using (HttpClient client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(180);
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiToken}");

                string prompt = @"Ты - ИИ ассистент. Извлеки ВСЕ товары из этого текста ЛЮБЫМ способом.

ГЛАВНОЕ ПРАВИЛО: НЕ ПРОПУСТИ НИ ОДНОГО ТОВАРА! ДАЖЕ ЕСЛИ ИХ 1000 ШТУК!

Что считается товаром:
- Любое оборудование: розетки, выключатели, кабели, провода, лампы, стартеры, коробки, шины, штанги
- Любой продукт с ценой или количеством
- Любая строка, где есть название + число (количество)

Как найти товары (используй ВСЕ методы сразу):
1. Ищи в тексте слова: ШТ, шт., штука, м, метр, кг, литр, упак
2. Ищи числа рядом с этими словами - это количество
3. Название товара - это текст ДО количества и единицы измерения
4. Если таблица с колонками - бери первый столбец как название
5. Если список с номерами (1., 2.) - бери текст после номера

Формат ответа (ТОЛЬКО JSON, НАЧИНАЙ С [ И ЗАКАНЧИВАЙ ]):
[
  {""name"": ""название товара"", ""quantity"": число, ""measure"": ""шт/м/кг"", ""description"": """"}
]

НЕ ПИШИ НИЧЕГО, КРОМЕ JSON. НЕ ОБРЕЗАЙ ОТВЕТ. НЕ ПРОПУСКАЙ ТОВАРЫ.

Текст для анализа:
" + text + @"

НАЙДИ ВСЕ ТОВАРЫ! ВСЕ! ВЕРНИ JSON МАССИВ СО ВСЕМИ!";

                var requestBody = new
                {
                    model = "gpt-4o-mini",
                    messages = new[] { new { role = "user", content = prompt } },
                    temperature = 0.0,
                    max_tokens = 32768
                };

                string jsonRequest = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await client.PostAsync(apiUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"AI Response длина: {jsonResponse.Length} символов");

                    using (JsonDocument doc = JsonDocument.Parse(jsonResponse))
                    {
                        if (doc.RootElement.TryGetProperty("choices", out JsonElement choices) &&
                            choices.GetArrayLength() > 0)
                        {
                            var firstChoice = choices[0];
                            if (firstChoice.TryGetProperty("message", out JsonElement message) &&
                                message.TryGetProperty("content", out JsonElement contentElement))
                            {
                                string aiResponse = contentElement.GetString();
                                var products = ParseAIResponseToProducts(aiResponse);
                                Console.WriteLine($"Извлечено товаров: {products.Count}");
                                return products;
                            }
                        }
                    }
                }
                else
                {
                    string errorResponse = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Ошибка API: {response.StatusCode} - {errorResponse}");
                }

                return new List<ExtractedProductInfo>();
            }
        }

        private List<ExtractedProductInfo> ParseAIResponseToProducts(string aiResponse)
        {
            var products = new List<ExtractedProductInfo>();

            try
            {
                int startIndex = aiResponse.IndexOf('[');
                int endIndex = aiResponse.LastIndexOf(']');

                if (startIndex >= 0 && endIndex > startIndex)
                {
                    string jsonPart = aiResponse.Substring(startIndex, endIndex - startIndex + 1);
                    var rawProducts = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(jsonPart);

                    foreach (var raw in rawProducts)
                    {
                        var product = new ExtractedProductInfo();

                        if (raw.ContainsKey("name") && raw["name"] != null)
                            product.Name = raw["name"].ToString().Trim();

                        if (raw.ContainsKey("quantity") && raw["quantity"] != null)
                        {
                            if (decimal.TryParse(raw["quantity"].ToString(), out decimal qty))
                                product.Quantity = qty;
                        }

                        if (raw.ContainsKey("measure") && raw["measure"] != null)
                        {
                            string measure = raw["measure"].ToString().ToLower();
                            product.MeasureSymbol = NormalizeMeasureSymbol(measure);
                        }

                        // ⬇️⬇️⬇️ ДОБАВЬТЕ ЭТУ СТРОКУ ⬇️⬇️⬇️
                        if (raw.ContainsKey("description") && raw["description"] != null)
                            product.Description = raw["description"].ToString().Trim();

                        if (!string.IsNullOrWhiteSpace(product.Name))
                            products.Add(product);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка парсинга AI ответа: {ex.Message}");
                products = ParseProductsManually(aiResponse);
            }

            return products;
        }

        private List<ExtractedProductInfo> ParseProductsManually(string text)
        {
            var products = new List<ExtractedProductInfo>();

            try
            {
                var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (trimmed.Length < 5) continue;

                    if (Regex.IsMatch(trimmed, @"\d+[\.\)]\s+[А-Яа-яA-Za-z0-9\s\-\(\)]+") &&
                        !trimmed.Contains("ООО") && !trimmed.Contains("ИНН") &&
                        !trimmed.StartsWith("№") && !trimmed.StartsWith("п/п"))
                    {
                        var product = new ExtractedProductInfo
                        {
                            Name = trimmed,
                            Quantity = 1,
                            MeasureSymbol = "шт",
                            Description = ""
                        };
                        products.Add(product);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в ручном парсинге: {ex.Message}");
            }

            return products;
        }

        private async Task<List<ExtractedProductInfo>> ExtractProductsManuallyWithDetailsAsync(string text)
        {
            return await Task.Run(() =>
            {
                var products = new List<ExtractedProductInfo>();
                var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

                var productRegex = new Regex(
                    @"^(\d+)[\.\)]\s+(.+?)(?:\t|\|)\s*(\d+(?:[.,]\d+)?)\s*(?:[мшткгл]|шт\.?|м\.?|кг\.?|л\.?|упак\.?)",
                    RegexOptions.IgnoreCase | RegexOptions.Multiline
                );

                ExtractedProductInfo currentProduct = null;

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var match = productRegex.Match(line);
                    if (match.Success)
                    {
                        if (currentProduct != null && !string.IsNullOrWhiteSpace(currentProduct.Name))
                            products.Add(currentProduct);

                        currentProduct = new ExtractedProductInfo
                        {
                            Name = match.Groups[2].Value.Trim(),
                            Quantity = 1
                        };

                        if (match.Groups[3].Success && decimal.TryParse(match.Groups[3].Value, out decimal qty))
                            currentProduct.Quantity = qty;

                        string measureMatch = Regex.Match(line, @"(\d+(?:[.,]\d+)?)\s*([мшткгл]|шт\.?|м\.?|кг\.?|л\.?|упак\.?)", RegexOptions.IgnoreCase).Groups[2].Value;
                        if (!string.IsNullOrEmpty(measureMatch))
                            currentProduct.MeasureSymbol = NormalizeMeasureSymbol(measureMatch);
                    }
                    else if (currentProduct != null)
                    {
                        if (line.Contains("ГОСТ") || line.Contains("ТУ") ||
                            line.Contains("характеристик") || line.Contains("параметр") ||
                            line.Contains("Сечение") || line.Contains("Материал") ||
                            line.Contains("Напряжение") || line.Contains("температур"))
                        {
                            if (string.IsNullOrEmpty(currentProduct.Description))
                                currentProduct.Description = line;
                            else
                                currentProduct.Description += "; " + line;
                        }
                    }
                }

                if (currentProduct != null && !string.IsNullOrWhiteSpace(currentProduct.Name))
                    products.Add(currentProduct);

                return products;
            });
        }

        private string NormalizeMeasureSymbol(string measure)
        {
            var measureMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "м", "м" }, { "метр", "м" }, { "m", "м" }, { "metr", "м" },
                { "шт", "шт" }, { "шт.", "шт" }, { "штука", "шт" }, { "pc", "шт" }, { "pcs", "шт" },
                { "кг", "кг" }, { "килограмм", "кг" }, { "kg", "кг" }, { "kilogram", "кг" },
                { "л", "л" }, { "литр", "л" }, { "l", "л" }, { "liter", "л" },
                { "упак", "упак" }, { "упаковка", "упак" }, { "pack", "упак" }
            };
            return measureMap.ContainsKey(measure) ? measureMap[measure] : "шт";
        }

        // ============ МЕТОДЫ ДЛЯ ЧТЕНИЯ ФАЙЛОВ ============

        private async Task<string> ReadWordFileAsync(string filePath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    StringBuilder text = new StringBuilder();
                    using (var doc = WordprocessingDocument.Open(filePath, false))
                    {
                        var body = doc.MainDocumentPart.Document.Body;
                        foreach (var paragraph in body.Elements<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
                        {
                            foreach (var run in paragraph.Elements<DocumentFormat.OpenXml.Wordprocessing.Run>())
                            {
                                foreach (var textElement in run.Elements<DocumentFormat.OpenXml.Wordprocessing.Text>())
                                {
                                    text.Append(textElement.Text);
                                }
                            }
                            text.AppendLine();
                        }
                    }
                    return text.ToString();
                }
                catch
                {
                    return TryReadFileAsText(filePath);
                }
            });
        }

        private async Task<string> ReadExcelFileAsync(string filePath)
        {
            return await Task.Run(() =>
            {
                StringBuilder result = new StringBuilder();
                string extension = Path.GetExtension(filePath).ToLower();

                try
                {
                    if (extension == ".xls")
                    {
                        using (FileStream file = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                        {
                            IWorkbook workbook = new HSSFWorkbook(file);
                            ISheet sheet = workbook.GetSheetAt(0);
                            for (int row = 0; row <= sheet.LastRowNum; row++)
                            {
                                IRow sheetRow = sheet.GetRow(row);
                                if (sheetRow != null)
                                {
                                    for (int col = 0; col < sheetRow.LastCellNum; col++)
                                    {
                                        var cell = sheetRow.GetCell(col);
                                        result.Append(cell?.ToString() + "\t");
                                    }
                                    result.AppendLine();
                                }
                            }
                        }
                    }
                    else
                    {
                        using (var workbook = new XLWorkbook(filePath))
                        {
                            var worksheet = workbook.Worksheets.First();
                            var range = worksheet.RangeUsed();
                            if (range != null)
                            {
                                for (int row = 1; row <= range.RowCount(); row++)
                                {
                                    for (int col = 1; col <= range.ColumnCount(); col++)
                                    {
                                        result.Append(worksheet.Cell(row, col).GetString() + "\t");
                                    }
                                    result.AppendLine();
                                }
                            }
                        }
                    }
                }
                catch
                {
                    return TryReadFileAsText(filePath);
                }

                return result.ToString();
            });
        }

        private async Task<string> ReadPdfFileAsync(string filePath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using (iTextSharp.text.pdf.PdfReader reader = new iTextSharp.text.pdf.PdfReader(filePath))
                    {
                        StringBuilder text = new StringBuilder();
                        for (int i = 1; i <= reader.NumberOfPages; i++)
                        {
                            text.Append(iTextSharp.text.pdf.parser.PdfTextExtractor.GetTextFromPage(reader, i));
                        }
                        return text.ToString();
                    }
                }
                catch
                {
                    return TryReadFileAsText(filePath);
                }
            });
        }

        private string TryReadFileAsText(string filePath)
        {
            try
            {
                return File.ReadAllText(filePath, Encoding.UTF8);
            }
            catch
            {
                return "";
            }
        }
        private async Task SaveAsWordDocumentAsync(string filePath, List<ExtractedProductInfo> products)
        {
            await Task.Run(() =>
            {
                using (var doc = WordprocessingDocument.Create(filePath, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
                {
                    var mainPart = doc.AddMainDocumentPart();
                    mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document();
                    var body = mainPart.Document.AppendChild(new Body());

                    // Заголовок
                    var titlePara = new Paragraph();
                    var titleRun = new Run(new Text("СТРУКТУРИРОВАННАЯ ЗАЯВКА"));
                    titleRun.RunProperties = new RunProperties();
                    titleRun.RunProperties.Bold = new Bold();
                    titlePara.AppendChild(titleRun);
                    body.AppendChild(titlePara);

                    body.AppendChild(new Paragraph(new Run(new Text(""))));
                    body.AppendChild(new Paragraph(new Run(new Text($"Дата формирования: {DateTime.Now:dd.MM.yyyy HH:mm:ss}"))));
                    body.AppendChild(new Paragraph(new Run(new Text($"Всего позиций: {products.Count}"))));
                    body.AppendChild(new Paragraph(new Run(new Text(""))));

                    // Таблица с 5 колонками (добавлено описание)
                    var table = new Table();
                    table.AppendChild(new TableProperties(
                        new TableWidth { Type = TableWidthUnitValues.Dxa, Width = "9000" }
                    ));

                    // Заголовки таблицы
                    var headerRow = new TableRow();
                    headerRow.AppendChild(new TableCell(new Paragraph(new Run(new Text("№")))));
                    headerRow.AppendChild(new TableCell(new Paragraph(new Run(new Text("Наименование товара")))));
                    headerRow.AppendChild(new TableCell(new Paragraph(new Run(new Text("Количество")))));
                    headerRow.AppendChild(new TableCell(new Paragraph(new Run(new Text("Ед.изм")))));
                    headerRow.AppendChild(new TableCell(new Paragraph(new Run(new Text("Описание")))));
                    table.AppendChild(headerRow);

                    // Данные
                    int number = 1;
                    foreach (var product in products)
                    {
                        var row = new TableRow();
                        row.AppendChild(new TableCell(new Paragraph(new Run(new Text(number.ToString())))));
                        row.AppendChild(new TableCell(new Paragraph(new Run(new Text(product.Name)))));
                        row.AppendChild(new TableCell(new Paragraph(new Run(new Text(product.Quantity.ToString())))));
                        row.AppendChild(new TableCell(new Paragraph(new Run(new Text(product.MeasureSymbol)))));
                        row.AppendChild(new TableCell(new Paragraph(new Run(new Text(product.Description ?? "")))));
                        table.AppendChild(row);
                        number++;
                    }

                    body.AppendChild(table);
                }
            });
        }

        // ============ UI МЕТОДЫ ============

        private void ShowProgress(string message)
        {
            Dispatcher.Invoke(() =>
            {
                FileUploadArea.Visibility = Visibility.Collapsed;
                ProgressPanel.Visibility = Visibility.Visible;
                ProgressText.Text = message;
                ProgressBar.Value = 0;
            });
        }

        private void HideProgress()
        {
            Dispatcher.Invoke(() =>
            {
                ProgressPanel.Visibility = Visibility.Collapsed;
                FileUploadArea.Visibility = Visibility.Visible;
            });
        }

        private void UpdateStatus(string message, string icon)
        {
            Dispatcher.Invoke(() =>
            {
                FileNameText.Text = message;
                FileNameText.Foreground = icon == "✅" ? Brushes.Green : Brushes.Red;
            });
        }

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService.CanGoBack)
                NavigationService.GoBack();
        }

        private async void btnDownload_Click(object sender, RoutedEventArgs e)
        {
            if (_extractedProducts == null || _extractedProducts.Count == 0)
                return;

            await SaveToWordFile();
        }
    }
}