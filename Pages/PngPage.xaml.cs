using ManagerApp.Classes.Read.ReadPicture;
using ManagerApp.Classes.Search;
using ManagerApp.Data.ScharedData;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ManagerApp.Pages
{
    public partial class PngPage : Page
    {
        private string _currentFilePath = "";
        private List<string> _lines = new List<string>();
        private HashSet<string> _selectedLineIds = new HashSet<string>();
        private AIProductAnalyzer _aiAnalyzer;
        private AlgorithmicProductAnalyzer _algorithmicAnalyzer;
        private SimpleOCRProcessor _ocrProcessor;

        public class SelectedLineItem
        {
            public string Text { get; set; }
            public int LineNumber { get; set; }
            public string FileName { get; set; }
            public string UniqueId { get; set; }

            public string DisplayText => $"{FileName} - Строка {LineNumber}: {Text}";
        }

        public PngPage(string filePath = null)
        {
            InitializeComponent();
            InitializeAnalyzers();

            if (!string.IsNullOrEmpty(filePath))
            {
                LoadFile(filePath);
            }
        }

        private void InitializeAnalyzers()
        {
            try
            {
                _ocrProcessor = new SimpleOCRProcessor();
                _aiAnalyzer = new AIProductAnalyzer();
                _algorithmicAnalyzer = new AlgorithmicProductAnalyzer();

                if (_ocrProcessor.IsOCRReady())
                {
                    statusText.Text = "OCR готов к работе";
                    statusText.Foreground = Brushes.Green;
                }
                else
                {
                    statusText.Text = "OCR не готов. Проверьте интернет соединение";
                    statusText.Foreground = Brushes.Red;
                }
            }
            catch (Exception ex)
            {
                statusText.Text = $"Ошибка инициализации: {ex.Message}";
                statusText.Foreground = Brushes.Red;
            }
        }

        public void LoadFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    ShowError("Файл не найден");
                    return;
                }

                string extension = Path.GetExtension(filePath)?.ToLower();
                if (!IsSupportedFormat(extension))
                {
                    ShowError($"Неподдерживаемый формат: {extension}\n" +
                             "Поддерживаемые: PNG, JPG, BMP, TIFF");
                    return;
                }

                _currentFilePath = filePath;

                // Загружаем и отображаем изображение
                LoadAndDisplayImage(filePath);

                // Распознаем текст с изображения
                RecognizeTextFromImage(filePath);
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка загрузки: {ex.Message}");
            }
        }

        private void LoadAndDisplayImage(string filePath)
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(filePath);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();

                ImagePreview.Source = bitmap;
                ImagePreview.Visibility = Visibility.Visible;

                // Показываем информацию о файле
                var fileInfo = new FileInfo(filePath);
                FileInfoTextBlock.Text =
                    $"Файл: {Path.GetFileName(filePath)}\n" +
                    $"Размер: {FormatFileSize(fileInfo.Length)}\n" +
                    $"Разрешение: {bitmap.PixelWidth} × {bitmap.PixelHeight}";
            }
            catch (Exception ex)
            {
                ShowError($"Не удалось загрузить изображение: {ex.Message}");
                ImagePreview.Visibility = Visibility.Collapsed;
            }
        }

        private async void RecognizeTextFromImage(string filePath)
        {
            try
            {
                if (!_ocrProcessor.IsOCRReady())
                {
                    ShowError("OCR не готов к работе.\nПроверьте интернет соединение и перезапустите программу.");
                    return;
                }

                SetProcessingState(true, "📷 Распознавание текста...");

                // Распознаем текст с картинки
                var textLines = await Task.Run(() => _ocrProcessor.ProcessImage(filePath));

                if (textLines == null || textLines.Count == 0)
                {
                    ShowInfo("Текст на изображении не найден");
                    _lines = new List<string>();
                }
                else if (textLines.Count == 1 && IsErrorMessage(textLines[0]))
                {
                    ShowError(textLines[0]);
                    _lines = new List<string>();
                }
                else
                {
                    _lines = textLines;
                    DisplayText();
                    ShowInfo($"✅ Найдено {textLines.Count} строк текста");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка распознавания: {ex.Message}");
                _lines = new List<string>();
            }
            finally
            {
                SetProcessingState(false, "Готово");
            }
        }

        private void DisplayText()
        {
            textItemsControl.Items.Clear();

            for (int i = 0; i < _lines.Count; i++)
            {
                string line = _lines[i];
                if (!string.IsNullOrWhiteSpace(line) && line.Length > 1)
                {
                    var border = CreateLineBorder(i, line);
                    textItemsControl.Items.Add(border);
                }
            }
        }

        private Border CreateLineBorder(int lineIndex, string lineText)
        {
            var border = new Border
            {
                Tag = lineIndex,
                Background = GetDefaultLineColor(lineIndex),
                Padding = new Thickness(8, 6, 8, 6),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 0, 2),
                CornerRadius = new CornerRadius(4)
            };

            border.Child = new TextBlock
            {
                Text = $"{lineIndex + 1}. {lineText}",
                TextWrapping = TextWrapping.Wrap,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center
            };

            border.MouseLeftButtonDown += (sender, e) =>
            {
                if (sender is Border b && b.Tag is int index)
                {
                    ToggleLineSelection(index);
                    e.Handled = true;
                }
            };

            border.MouseEnter += (sender, e) =>
            {
                if (sender is Border b)
                {
                    b.Background = new SolidColorBrush(Color.FromArgb(255, 245, 245, 245));
                }
            };

            border.MouseLeave += (sender, e) =>
            {
                if (sender is Border b && b.Tag is int index)
                {
                    bool isSelected = _selectedLineIds.Contains(GetLineUniqueId(index));
                    b.Background = isSelected ? GetSelectedColor() : GetDefaultLineColor(index);
                }
            };

            return border;
        }

        private void ToggleLineSelection(int lineIndex)
        {
            if (lineIndex < 0 || lineIndex >= _lines.Count) return;

            string uniqueId = GetLineUniqueId(lineIndex);
            string lineText = _lines[lineIndex];
            string fileName = Path.GetFileName(_currentFilePath);

            if (_selectedLineIds.Contains(uniqueId))
            {
                RemoveSelection(lineIndex, uniqueId);
            }
            else
            {
                AddSelection(lineIndex, uniqueId, lineText, fileName);
            }
        }

        private void AddSelection(int lineIndex, string uniqueId, string lineText, string fileName)
        {
            _selectedLineIds.Add(uniqueId);
            UpdateLineAppearance(lineIndex, true);

            lvSelectedCells.Items.Add(new SelectedLineItem
            {
                Text = lineText,
                LineNumber = lineIndex + 1,
                FileName = fileName,
                UniqueId = uniqueId
            });

            statusText.Text = $"Выбрано строк: {_selectedLineIds.Count}";
        }

        private void RemoveSelection(int lineIndex, string uniqueId)
        {
            _selectedLineIds.Remove(uniqueId);
            UpdateLineAppearance(lineIndex, false);

            var itemToRemove = lvSelectedCells.Items.Cast<SelectedLineItem>()
                .FirstOrDefault(item => item.UniqueId == uniqueId);
            if (itemToRemove != null)
            {
                lvSelectedCells.Items.Remove(itemToRemove);
            }

            statusText.Text = $"Выбрано строк: {_selectedLineIds.Count}";
        }

        private void UpdateLineAppearance(int lineIndex, bool isSelected)
        {
            foreach (var item in textItemsControl.Items)
            {
                if (item is Border border && border.Tag is int index && index == lineIndex)
                {
                    border.Background = isSelected ? GetSelectedColor() : GetDefaultLineColor(lineIndex);
                    break;
                }
            }
        }

        private string GetLineUniqueId(int lineIndex)
        {
            return $"{Path.GetFileName(_currentFilePath)}|{lineIndex}";
        }

        private SolidColorBrush GetDefaultLineColor(int lineIndex)
        {
            return lineIndex % 2 == 0
                ? Brushes.White
                : new SolidColorBrush(Color.FromArgb(20, 0, 0, 0));
        }

        private SolidColorBrush GetSelectedColor()
        {
            return new SolidColorBrush(Color.FromArgb(255, 255, 235, 200));
        }

        private void ClearAllSelections()
        {
            _selectedLineIds.Clear();
            lvSelectedCells.Items.Clear();

            foreach (var item in textItemsControl.Items)
            {
                if (item is Border border && border.Tag is int index)
                {
                    border.Background = GetDefaultLineColor(index);
                }
            }

            statusText.Text = "Готово";
        }

        private void btnLoadImage_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Изображения (*.png;*.jpg;*.jpeg;*.bmp;*.tiff;*.tif)|*.png;*.jpg;*.jpeg;*.bmp;*.tiff;*.tif|Все файлы (*.*)|*.*",
                Title = "Выберите изображение",
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() == true)
            {
                LoadFile(openFileDialog.FileName);
            }
        }

        // ==================== АНАЛИЗ ТЕКСТА ====================

        private async void btnAI_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateTextForAnalysis()) return;

            try
            {
                await RunAnalysisAsync(
                    async () => await _aiAnalyzer.AnalyzeViaAIAsync(_lines, UpdateProgress),
                    "AI анализ"
                );
            }
            catch (Exception ex)
            {
                ShowError($"AI анализа: {ex.Message}");
            }
        }

        private async void btnMath_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateTextForAnalysis()) return;

            try
            {
                await RunAnalysisAsync(
                    async () => await _algorithmicAnalyzer.AnalyzeViaAlgorithmAsync(_lines, UpdateProgress),
                    "Алгоритмический анализ"
                );
            }
            catch (Exception ex)
            {
                ShowError($"алгоритмического анализа: {ex.Message}");
            }
        }

        private bool ValidateTextForAnalysis()
        {
            if (_lines.Count == 0)
            {
                ShowInfo("Сначала загрузите изображение и распознайте текст");
                return false;
            }
            return true;
        }

        private async Task RunAnalysisAsync(Func<Task<List<string>>> analysisFunc, string analysisType)
        {
            SetProcessingState(true, $"{analysisType}...");

            try
            {
                var foundProducts = await Task.Run(analysisFunc);
                ProcessAnalysisResults(foundProducts, analysisType);
            }
            finally
            {
                SetProcessingState(false, "Готово");
            }
        }

        private void ProcessAnalysisResults(List<string> foundProducts, string analysisType)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (foundProducts.Count > 0)
                {
                    AddFoundProductsToList(foundProducts);

                    ShowInfo($"{analysisType} завершен. Найдено товаров: {foundProducts.Count}");
                }
                else
                {
                    ShowInfo($"{analysisType} не нашел товаров");
                }
            });
        }

        private void AddFoundProductsToList(List<string> foundProducts)
        {
            ClearAllSelections();

            string fileName = Path.GetFileName(_currentFilePath);

            foreach (var product in foundProducts)
            {
                if (string.IsNullOrWhiteSpace(product)) continue;

                int lineIndex = FindMatchingLine(product);

                if (lineIndex >= 0)
                {
                    AddSelection(lineIndex, GetLineUniqueId(lineIndex), _lines[lineIndex], fileName);
                }
                else
                {
                    lvSelectedCells.Items.Add(new SelectedLineItem
                    {
                        Text = product,
                        LineNumber = 0,
                        FileName = fileName,
                        UniqueId = $"auto|{Guid.NewGuid()}"
                    });
                }
            }
        }

        private int FindMatchingLine(string product)
        {
            string productLower = product.ToLower();

            for (int i = 0; i < _lines.Count; i++)
            {
                string lineLower = _lines[i].ToLower();

                // Ищем частичное совпадение
                if (lineLower.Contains(productLower) || productLower.Contains(lineLower) ||
                    CalculateSimilarity(lineLower, productLower) > 0.6)
                {
                    return i;
                }
            }
            return -1;
        }

        private double CalculateSimilarity(string s1, string s2)
        {
            // Простая проверка схожести строк
            if (s1 == s2) return 1.0;

            int commonChars = s1.Intersect(s2).Count();
            int maxLength = Math.Max(s1.Length, s2.Length);

            return maxLength > 0 ? (double)commonChars / maxLength : 0;
        }

        private void UpdateProgress(string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                statusText.Text = message;
                if (progressBar.Value < 95)
                    progressBar.Value += 1;
            });
        }

        private void SetProcessingState(bool isProcessing, string status = "")
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                progressPanel.Visibility = isProcessing ? Visibility.Visible : Visibility.Collapsed;

                btnLoadImage.IsEnabled = !isProcessing;
                btnClearList.IsEnabled = !isProcessing && _selectedLineIds.Count > 0;
                btnNext.IsEnabled = !isProcessing && _selectedLineIds.Count > 0;
                btnBack.IsEnabled = !isProcessing;

                if (!string.IsNullOrEmpty(status))
                    statusText.Text = status;

                if (!isProcessing)
                {
                    progressBar.Value = 0;
                }
            });
        }

        // ==================== КНОПКИ УПРАВЛЕНИЯ ====================

        private void btnClearList_Click(object sender, RoutedEventArgs e)
        {
            ClearAllSelections();
        }

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService.CanGoBack)
                NavigationService.GoBack();
        }

        private void btnNext_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedLineIds.Count == 0)
                {
                    ShowInfo("Сначала выберите строки для сопоставления");
                    return;
                }

                var selectedProducts = lvSelectedCells.Items.Cast<SelectedLineItem>()
                    .Where(item => !string.IsNullOrWhiteSpace(item.Text))
                    .Select(item => item.Text.Trim())
                    .ToList();

                if (selectedProducts.Count == 0)
                {
                    ShowInfo("Нет выбранных строк с текстом");
                    return;
                }

                // Переход на страницу сравнения
                NavigateToComparisonPage(selectedProducts);
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка при переходе: {ex.Message}");
            }
        }

        private void NavigateToComparisonPage(List<string> selectedProducts)
        {
            try
            {
                // Сохраняем выбранные товары
                if (typeof(ProductSelectionManager).IsClass)
                {
                    var method = typeof(ProductSelectionManager).GetMethod("SetProducts");
                    if (method != null)
                    {
                        method.Invoke(null, new object[] { selectedProducts });
                    }
                }

                // Создаем и переходим на страницу сравнения
                var comparisonPageType = Type.GetType("ManagerApp.Pages.ComparisonProduct");
                if (comparisonPageType != null)
                {
                    var comparisonPage = Activator.CreateInstance(comparisonPageType, selectedProducts);
                    NavigationService.Navigate(comparisonPage);
                }
                else
                {
                    // Если страница сравнения не существует, показываем результат
                    ShowInfo($"Выбрано {selectedProducts.Count} товаров:\n" +
                            string.Join("\n", selectedProducts.Take(5)));
                    if (selectedProducts.Count > 5)
                        ShowInfo("... и еще " + (selectedProducts.Count - 5) + " товаров");
                }

                ClearAllSelections();
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка навигации: {ex.Message}");
            }
        }

        private void btnOCRHelp_Click(object sender, RoutedEventArgs e)
        {
            if (_ocrProcessor != null)
            {
                _ocrProcessor.ShowHelp();
            }
        }

        // ==================== ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ====================

        private bool IsSupportedFormat(string extension)
        {
            string[] supported = { ".png", ".jpg", ".jpeg", ".bmp", ".tiff", ".tif", ".gif" };
            return supported.Contains(extension);
        }

        private bool IsErrorMessage(string text)
        {
            string[] errorKeywords = { "ошибка", "error", "не найден", "не готов", "неподдерживаемый" };
            return errorKeywords.Any(keyword => text.ToLower().Contains(keyword));
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

        private void ShowInfo(string message)
        {
            MessageBox.Show(message, "Информация",
                MessageBoxButton.OK, MessageBoxImage.Information);
            statusText.Text = message;
        }

        private void ShowError(string message)
        {
            MessageBox.Show(message, "Ошибка",
                MessageBoxButton.OK, MessageBoxImage.Error);
            statusText.Text = message;
            statusText.Foreground = Brushes.Red;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            // Обновляем статус OCR при загрузке страницы
            if (_ocrProcessor != null)
            {
                if (_ocrProcessor.IsOCRReady())
                {
                    statusText.Text = "✅ OCR готов к работе";
                    statusText.Foreground = Brushes.Green;
                }
                else
                {
                    statusText.Text = "⚠️ OCR требует настройки. Нажмите 'Помощь OCR'";
                    statusText.Foreground = Brushes.Orange;
                }
            }
        }
    }
}