using ManagerApp.Classes.Read;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        // Класс для хранения выбранных строк
        public class SelectedLineItem
        {
            public string Text { get; set; }
            public int LineNumber { get; set; }
            public string FileName { get; set; }
            public string UniqueId { get; set; }
        }

        public WorldPdfFile(string filePath = null)
        {
            InitializeComponent();

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

                // Читаем файл
                ReadRequst reader = new ReadRequst();
                string fileContent = reader.ReadFileAll(filePath);

                if (fileContent == "Ошибка формата")
                {
                    MessageBox.Show("Неподдерживаемый формат файла", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                _lines = fileContent.Split(new[] { '\n', '\r' },
                    StringSplitOptions.RemoveEmptyEntries).ToList();

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

            // Обработчики событий
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
                if (sender is Border b && b.Tag is int index)
                {
                    //string uniqueId = GetLineUniqueId(index);
                    //if (!_selectedLineIds.Contains(uniqueId))
                    //{
                    //    b.Background = new SolidColorBrush(Color.FromArgb(255, 240, 240, 240));
                    //}
                }
            };

            border.MouseLeave += (sender, e) =>
            {
                if (sender is Border b && b.Tag is int index)
                {
                    //string uniqueId = GetLineUniqueId(index);
                    //b.Background = _selectedLineIds.Contains(uniqueId)
                    //    ? new SolidColorBrush(Color.FromArgb(255, 255, 235, 200))
                    //    : GetDefaultLineColor(index);
                }
            };

            // Проверяем, выделена ли эта строка
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
                // Удаляем выделение
                _selectedLineIds.Remove(uniqueId);
                UpdateLineAppearance(lineIndex, false);

                // Удаляем из ListView
                var itemToRemove = lvSelectedCells.Items.Cast<SelectedLineItem>()
                    .FirstOrDefault(item => item.UniqueId == uniqueId);
                if (itemToRemove != null)
                {
                    lvSelectedCells.Items.Remove(itemToRemove);
                }
            }
            else
            {
                // Добавляем выделение
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

        // Обработчики событий кнопок
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

        private void btnAI_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedLineIds.Count > 0)
            {
                MessageBox.Show($"Анализ {_selectedLineIds.Count} выбранных строк с помощью ИИ",
                    "ИИ анализ", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void btnMath_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedLineIds.Count > 0)
            {
                MessageBox.Show($"Анализ {_selectedLineIds.Count} выбранных строк с помощью алгоритма",
                    "Алгоритмический анализ", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

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
            if (_selectedLineIds.Count > 0)
            {
                MessageBox.Show($"Переход к следующему шагу с {_selectedLineIds.Count} выбранными строками",
                    "Далее", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Сначала выберите строки для анализа",
                    "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}