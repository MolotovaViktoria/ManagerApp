using ManagerApp.Classes.Read;
using ManagerApp.Classes.Read.ReadPirture;
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
    /// <summary>
    /// Логика взаимодействия для PngPage.xaml
    /// </summary>
    public partial class PngPage : Page
    {
        private string _currentFilePath = "";
        private List<string> _lines = new List<string>();
        private HashSet<string> _selectedLineIds = new HashSet<string>();
        private AIProductAnalyzer _aiAnalyzer;
        private AlgorithmicProductAnalyzer _algorithmicAnalyzer;

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
            _aiAnalyzer = new AIProductAnalyzer();
            _algorithmicAnalyzer = new AlgorithmicProductAnalyzer();
        }

        public void LoadFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    MessageBox.Show("Файл не найден", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string extension = Path.GetExtension(filePath)?.ToLower();
                if (!Classes.Read.FormatLists.ImageFormatList.Contains(extension))
                {
                    MessageBox.Show($"Неподдерживаемый формат изображения: {extension}",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show($"Ошибка загрузки файла: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show($"Не удалось загрузить изображение: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                ImagePreview.Visibility = Visibility.Collapsed;
            }
        }

        private async void RecognizeTextFromImage(string filePath)
        {
            try
            {
                SetProcessingState(true, "Распознавание текста с изображения...");

                var textLines = await Task.Run(() =>
                {
                    var ocrProcessor = new ImageOCRProcessor();

                    if (!ocrProcessor.IsTesseractAvailable())
                    {
                        return new List<string>
                {
                    "Tesseract OCR не доступен",
                    "Установите файлы rus.traineddata и eng.traineddata",
                    $"в папку: {GetTessDataPath()}"
                };
                    }

                    return ocrProcessor.ProcessImageFile(filePath);
                });

                if (textLines == null || textLines.Count == 0)
                {
                    MessageBox.Show("Текст на изображении не найден",
                        "Результат", MessageBoxButton.OK, MessageBoxImage.Information);
                    _lines = new List<string>();
                }
                else
                {
                    _lines = textLines;

                    // Отображаем распознанный текст
                    DisplayText();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка распознавания текста: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                _lines = new List<string>();
            }
            finally
            {
                SetProcessingState(false, "Готово");
            }
        }

        private string GetTessDataPath()
        {
            string appPath = AppDomain.CurrentDomain.BaseDirectory;
            string projectPath = Path.GetFullPath(Path.Combine(appPath, @"..\..\.."));
            return Path.Combine(projectPath, "Classes", "tessdata");
        }

        private void DisplayText()
        {
            textItemsControl.Items.Clear();

            for (int i = 0; i < _lines.Count; i++)
            {
                string line = _lines[i];
                if (!string.IsNullOrWhiteSpace(line))
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
                Padding = new Thickness(8, 4, 8, 4),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 0, 1)
            };

            border.Child = new TextBlock
            {
                Text = lineText,
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
        }

        private void UpdateLineAppearance(int lineIndex, bool isSelected)
        {
            foreach (var item in textItemsControl.Items)
            {
                if (item is Border border && border.Tag is int index && index == lineIndex)
                {
                    border.Background = isSelected
                        ? new SolidColorBrush(Color.FromArgb(255, 255, 235, 200))
                        : GetDefaultLineColor(lineIndex);
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
                : new SolidColorBrush(Color.FromArgb(255, 249, 249, 249));
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
        }

        private void btnLoadImage_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = CreateImageFilter(),
                Title = "Выберите изображение"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                LoadFile(openFileDialog.FileName);
            }
        }

        private string CreateImageFilter()
        {
            return "Изображения|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tiff;*.tif;*.ico;*.webp;*.jfif|" +
                   "Все файлы|*.*";
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
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
                ShowError("AI анализа", ex);
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
                ShowError("алгоритмического анализа", ex);
            }
        }

        private bool ValidateTextForAnalysis()
        {
            if (_lines.Count == 0)
            {
                MessageBox.Show("Сначала загрузите изображение и распознайте текст",
                    "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
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

                    MessageBox.Show($"{analysisType} завершен. Найдено товаров: {foundProducts.Count}",
                        "Результат", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"{analysisType} не нашел товаров",
                        "Результат", MessageBoxButton.OK, MessageBoxImage.Information);
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
            for (int i = 0; i < _lines.Count; i++)
            {
                if (_lines[i].Contains(product) || product.Contains(_lines[i]))
                {
                    return i;
                }
            }
            return -1;
        }

        private void UpdateProgress(string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                statusText.Text = message;
                if (progressBar.Value < 95)
                    progressBar.Value += 0.5;
            });
        }

        private void SetProcessingState(bool isProcessing, string status = "")
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                progressPanel.Visibility = isProcessing ? Visibility.Visible : Visibility.Collapsed;

                //btnAI.IsEnabled = !isProcessing;
                //btnMath.IsEnabled = !isProcessing;
                btnLoadImage.IsEnabled = !isProcessing;
                btnClearList.IsEnabled = !isProcessing;
                btnNext.IsEnabled = !isProcessing;
                btnBack.IsEnabled = !isProcessing;

                if (!string.IsNullOrEmpty(status))
                    statusText.Text = status;

                if (!isProcessing)
                {
                    progressBar.Value = 0;
                    //btnAI.Content = "Сформировать список товаров с помощью ИИ";
                    //btnMath.Content = "Сформировать список товаров с помощью алгоритма";
                }
                else
                {
                    //btnAI.Content = "Идет анализ...";
                    //btnMath.Content = "Идет анализ...";
                }
            });
        }

        private void ShowError(string analysisType, Exception ex)
        {
            MessageBox.Show($"Ошибка {analysisType}: {ex.Message}",
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            SetProcessingState(false, "Ошибка");
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
                    MessageBox.Show("Сначала выберите строки для сопоставления",
                        "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var selectedProducts = lvSelectedCells.Items.Cast<SelectedLineItem>()
                    .Where(item => !string.IsNullOrWhiteSpace(item.Text))
                    .Select(item =>
                    {
                        string text = item.Text.Trim();
                        return text.Length > 150 ? text.Substring(0, 150) + "..." : text;
                    })
                    .ToList();

                if (selectedProducts.Count == 0)
                {
                    MessageBox.Show("Нет выбранных строк с текстом",
                        "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                ProductSelectionManager.SetProducts(selectedProducts);

                var comparisonPage = new ComparisonProduct(selectedProducts);
                NavigationService.Navigate(comparisonPage);

                ClearAllSelections();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при переходе: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}