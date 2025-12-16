using ManagerApp.Classes;

using ManagerApp.Classes.Read;
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

namespace ManagerApp.Pages
{
    public partial class WorldPdfFile : Page
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

        public WorldPdfFile(string filePath = null)
        {
            InitializeComponent();
            _aiAnalyzer = new AIProductAnalyzer();
            _algorithmicAnalyzer = new AlgorithmicProductAnalyzer();

            if (!string.IsNullOrEmpty(filePath))
            {
                LoadFile(filePath);
            }
        }

        public void LoadFile(string filePath)
        {
            try
            {
                _currentFilePath = filePath;
                ClearAllSelections();

                ReadRequst reader = new ReadRequst();
                string fileContent = reader.ReadFileAll(filePath);

                if (fileContent == "Ошибка формата")
                {
                    MessageBox.Show("Неподдерживаемый формат файла", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                _lines = fileContent.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                                    .Where(line => !string.IsNullOrWhiteSpace(line))
                                    .Select(line => line.Trim())
                                    .ToList();

                DisplayText();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка чтения файла: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DisplayText()
        {
            textItemsControl.Items.Clear();

            for (int i = 0; i < _lines.Count; i++)
            {
                string line = _lines[i];
                var border = CreateLineBorder(i, line);
                textItemsControl.Items.Add(border);
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

            string uniqueId = GetLineUniqueId(lineIndex);
            if (_selectedLineIds.Contains(uniqueId))
            {
                border.Background = new SolidColorBrush(Color.FromArgb(255, 255, 235, 200));
            }

            return border;
        }

        private void ToggleLineSelection(int lineIndex)
        {
            string uniqueId = GetLineUniqueId(lineIndex);
            string lineText = _lines[lineIndex];
            string fileName = Path.GetFileName(_currentFilePath);

            if (_selectedLineIds.Contains(uniqueId))
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
            else
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

        private void btnLoadExcel_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Word/PDF файлы|*.docx;*.pdf|Все файлы|*.*",
                Title = "Выберите файл"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                LoadFile(openFileDialog.FileName);
            }
        }

        // ==================== АВТОМАТИЧЕСКИЙ АНАЛИЗ ВСЕГО ТЕКСТА ====================

        private async void btnAI_Click(object sender, RoutedEventArgs e)
        {
            if (_lines.Count == 0)
            {
                MessageBox.Show("Файл не загружен", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Блокируем кнопки и показываем прогресс
                SetProcessingState(true, "AI анализ...");

                // Сбрасываем прогресс
                progressBar.Value = 0;
                statusText.Text = "Запуск AI анализа...";

                MessageBox.Show($"Начинаем AI анализ всех строк ({_lines.Count} строк)",
                    "AI анализ", MessageBoxButton.OK, MessageBoxImage.Information);

                // Создаем прогресс-отчет
                var progress = new Progress<string>(message =>
                {
                    // Обновляем статус
                    statusText.Text = message;

                    // Обновляем прогресс-бар (увеличиваем на 1% каждое сообщение)
                    if (progressBar.Value < 95)
                        progressBar.Value += 0.5;

                    // Принудительное обновление UI
                    Application.Current.Dispatcher.Invoke(() => { },
                        System.Windows.Threading.DispatcherPriority.Background);
                });

                // Анализируем ВСЕ строки через AI
                var foundProducts = await Task.Run(() =>
                {
                    return _aiAnalyzer.AnalyzeViaAIAsync(
                        _lines,
                        message =>
                        {
                            // Передаем сообщение через прогресс
                            ((IProgress<string>)progress).Report(message);
                        });
                });

                // Завершаем прогресс
                progressBar.Value = 100;
                statusText.Text = $"Найдено {foundProducts.Count} товаров";

                // Небольшая задержка для отображения завершения
                await Task.Delay(500);

                // Автоматически добавляем найденные товары в список
                AddFoundProductsToList(foundProducts);

                MessageBox.Show($"AI анализ завершен. Найдено товаров: {foundProducts.Count}",
                    "Результат", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка AI анализа: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Возвращаем нормальное состояние
                SetProcessingState(false, "Готово");
            }
        }

        private async void btnMath_Click(object sender, RoutedEventArgs e)
        {
            if (_lines.Count == 0)
            {
                MessageBox.Show("Файл не загружен", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Блокируем кнопки и показываем прогресс
                SetProcessingState(true, "Алгоритмический анализ...");

                // Сбрасываем прогресс
                progressBar.Value = 0;
                statusText.Text = "Запуск алгоритмического анализа...";

                MessageBox.Show($"Начинаем алгоритмический анализ всех строк ({_lines.Count} строк)",
                    "Алгоритмический анализ", MessageBoxButton.OK, MessageBoxImage.Information);

                // Создаем прогресс-отчет
                var progress = new Progress<string>(message =>
                {
                    // Обновляем статус
                    statusText.Text = message;

                    // Обновляем прогресс-бар
                    if (progressBar.Value < 95)
                        progressBar.Value += 1;

                    // Принудительное обновление UI
                    Application.Current.Dispatcher.Invoke(() => { },
                        System.Windows.Threading.DispatcherPriority.Background);
                });

                // Анализируем ВСЕ строки через алгоритм
                var foundProducts = await Task.Run(() =>
                {
                    return _algorithmicAnalyzer.AnalyzeViaAlgorithmAsync(
                        _lines,
                        message =>
                        {
                            // Передаем сообщение через прогресс
                            ((IProgress<string>)progress).Report(message);
                        });
                });

                // Завершаем прогресс
                progressBar.Value = 100;
                statusText.Text = $"Найдено {foundProducts.Count} товаров";

                // Небольшая задержка для отображения завершения
                await Task.Delay(500);

                // Автоматически добавляем найденные товары в список
                AddFoundProductsToList(foundProducts);

                MessageBox.Show($"Алгоритмический анализ завершен. Найдено товаров: {foundProducts.Count}",
                    "Результат", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка алгоритмического анализа: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Возвращаем нормальное состояние
                SetProcessingState(false, "Готово");
            }
        }

        // Новый метод для управления состоянием UI
        private void SetProcessingState(bool isProcessing, string status = "")
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Управляем видимостью панели прогресса
                progressPanel.Visibility = isProcessing ? Visibility.Visible : Visibility.Collapsed;

                // Блокируем/разблокируем кнопки
                btnAI.IsEnabled = !isProcessing;
                btnMath.IsEnabled = !isProcessing;
                btnLoadExcel.IsEnabled = !isProcessing;

                if (!string.IsNullOrEmpty(status))
                {
                    statusText.Text = status;
                }

                if (!isProcessing)
                {
                    progressBar.Value = 0;
                    btnAI.Content = "Сформировать список товаров с помощью ИИ";
                    btnMath.Content = "Сформировать список товаров с помощью алгоритма";
                }
                else
                {
                    btnAI.Content = "Идет анализ...";
                    btnMath.Content = "Идет анализ...";
                }
            });
        }

        private void AddFoundProductsToList(List<string> foundProducts)
        {
            ClearAllSelections();

            string fileName = Path.GetFileName(_currentFilePath);

            foreach (var product in foundProducts)
            {
                // Находим индекс строки в исходном тексте
                int lineIndex = -1;
                for (int i = 0; i < _lines.Count; i++)
                {
                    if (_lines[i].Contains(product) || product.Contains(_lines[i]))
                    {
                        lineIndex = i;
                        break;
                    }
                }

                if (lineIndex >= 0)
                {
                    string uniqueId = GetLineUniqueId(lineIndex);
                    string lineText = _lines[lineIndex];

                    // Добавляем в выделенные
                    _selectedLineIds.Add(uniqueId);
                    UpdateLineAppearance(lineIndex, true);

                    // Добавляем в ListView
                    lvSelectedCells.Items.Add(new SelectedLineItem
                    {
                        Text = lineText,
                        LineNumber = lineIndex + 1,
                        FileName = fileName,
                        UniqueId = uniqueId
                    });
                }
                else
                {
                    // Если не нашли точного совпадения, добавляем как новый элемент
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

        // ==================== КНОПКИ УПРАВЛЕНИЯ ====================

        private void btnClearList_Click(object sender, RoutedEventArgs e)
        {
            // ТО ЖЕ САМОЕ, что и при клике на строки для отмены выделения
            ClearAllSelections();
        }

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            // ТО ЖЕ САМОЕ, что и при клике кнопки "Назад" на странице
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

                var selectedProducts = new List<string>();

                foreach (SelectedLineItem item in lvSelectedCells.Items)
                {
                    if (!string.IsNullOrWhiteSpace(item.Text))
                    {
                        string cleanText = item.Text.Trim();
                        if (cleanText.Length > 150)
                        {
                            cleanText = cleanText.Substring(0, 150) + "...";
                        }
                        selectedProducts.Add(cleanText);
                    }
                }

                if (selectedProducts.Count == 0)
                {
                    MessageBox.Show("Нет выбранных строк с текстом",
                        "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (typeof(ProductSelectionManager) != null)
                {
                    ProductSelectionManager.SetProducts(selectedProducts);
                }

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