using ClosedXML.Excel;
using Microsoft.Win32;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ManagerApp.Pages
{
    /// <summary>
    /// Класс для хранения информации о выбранной ячейке
    /// </summary>
    public class SelectedCellInfo
    {
        public string SheetName { get; set; }
        public int Row { get; set; }
        public string ColumnName { get; set; }
        public string Value { get; set; }
        public string UniqueId { get; set; } // Уникальный идентификатор ячейки

        public SelectedCellInfo(string sheetName, int row, string columnName, string value)
        {
            SheetName = sheetName;
            Row = row + 1; // Excel использует 1-индексацию
            ColumnName = columnName;
            Value = value;
            UniqueId = $"{sheetName}|{row}|{columnName}";
        }
    }

    public partial class ExcelFile : Page
    {
        private string _currentFilePath = "";
        private DataTable _currentDataTable;
        private Dictionary<string, DataTable> _worksheets;
        private List<SelectedCellInfo> _selectedCells = new List<SelectedCellInfo>();
        private Dictionary<string, SolidColorBrush> _highlightedRows = new Dictionary<string, SolidColorBrush>();

        public ExcelFile(string filePath = null)
        {
            InitializeComponent();

            // Если передан путь к файлу, загружаем его
            if (!string.IsNullOrEmpty(filePath))
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    LoadExcelFile(filePath);
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            // Инициализация при загрузке страницы
        }

        private void btnLoadExcel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OpenFileDialog openFileDialog = new OpenFileDialog
                {
                    Filter = "Excel Files|*.xls;*.xlsx;*.xlsm;*.xlsb|All Files|*.*",
                    Title = "Выберите Excel файл"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    LoadExcelFile(openFileDialog.FileName);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки файла: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadExcelFile(string filePath)
        {
            try
            {
                _currentFilePath = filePath;
                ClearAllSelections();

                // Обновляем информацию о файле
                UpdateFileInfo(filePath);

                // Загружаем все листы из Excel
                LoadWorksheets(filePath);

                // Загружаем первый лист по умолчанию
                if (cmbSheets.Items.Count > 0)
                {
                    cmbSheets.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка чтения Excel файла: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateFileInfo(string filePath)
        {
            FileInfo fileInfo = new FileInfo(filePath);
            //txtFileName.Text = fileInfo.Name;
            //txtFileSize.Text = $"{fileInfo.Length / 1024} KB";
        }

        private void LoadWorksheets(string filePath)
        {
            try
            {
                _worksheets = new Dictionary<string, DataTable>();
                cmbSheets.Items.Clear();

                string extension = Path.GetExtension(filePath).ToLower();

                if (extension == ".xls")
                {
                    // Для .xls файлов используем NPOI
                    LoadWorksheetsWithNpoi(filePath);
                }
                else if (extension == ".xlsx" || extension == ".xlsm" || extension == ".xlsb")
                {
                    // Для новых форматов используем ClosedXML
                    LoadWorksheetsWithClosedXml(filePath);
                }
                else
                {
                    MessageBox.Show($"Неподдерживаемый формат файла: {extension}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки листов: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadWorksheetsWithNpoi(string filePath)
        {
            IWorkbook workbook;

            using (FileStream file = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                if (Path.GetExtension(filePath).ToLower() == ".xls")
                {
                    workbook = new HSSFWorkbook(file);
                }
                else
                {
                    workbook = new XSSFWorkbook(file);
                }
            }

            for (int i = 0; i < workbook.NumberOfSheets; i++)
            {
                ISheet sheet = workbook.GetSheetAt(i);

                if (sheet.PhysicalNumberOfRows > 0)
                {
                    DataTable dataTable = ConvertSheetToDataTableNpoi(sheet);
                    _worksheets[sheet.SheetName] = dataTable;
                    cmbSheets.Items.Add(new KeyValuePair<string, DataTable>(sheet.SheetName, dataTable));
                }
            }
        }

        private DataTable ConvertSheetToDataTableNpoi(ISheet sheet)
        {
            DataTable dataTable = new DataTable(sheet.SheetName);

            IRow headerRow = sheet.GetRow(0);
            if (headerRow == null) return dataTable;

            // Добавляем столбцы из первой строки
            for (int col = headerRow.FirstCellNum; col < headerRow.LastCellNum; col++)
            {
                ICell cell = headerRow.GetCell(col);
                string columnName = cell != null ? cell.ToString() : $"Column {col + 1}";

                // Убеждаемся, что имена столбцов уникальны
                string uniqueColumnName = columnName;
                int counter = 1;
                while (dataTable.Columns.Contains(uniqueColumnName))
                {
                    uniqueColumnName = $"{columnName}_{counter}";
                    counter++;
                }

                dataTable.Columns.Add(uniqueColumnName, typeof(string));
            }

            // Добавляем данные (начиная со второй строки)
            for (int row = 1; row <= sheet.LastRowNum; row++)
            {
                IRow dataRow = sheet.GetRow(row);
                if (dataRow == null) continue;

                DataRow dr = dataTable.NewRow();
                for (int col = dataRow.FirstCellNum; col < dataRow.LastCellNum; col++)
                {
                    ICell cell = dataRow.GetCell(col);
                    dr[col] = cell != null ? cell.ToString() : "";
                }
                dataTable.Rows.Add(dr);
            }

            return dataTable;
        }

        private void LoadWorksheetsWithClosedXml(string filePath)
        {
            using (var workbook = new XLWorkbook(filePath))
            {
                foreach (var worksheet in workbook.Worksheets)
                {
                    if (worksheet.RangeUsed() != null)
                    {
                        DataTable dataTable = ConvertWorksheetToDataTable(worksheet);
                        _worksheets[worksheet.Name] = dataTable;
                        cmbSheets.Items.Add(new KeyValuePair<string, DataTable>(worksheet.Name, dataTable));
                    }
                }
            }
        }

        private DataTable ConvertWorksheetToDataTable(IXLWorksheet worksheet)
        {
            DataTable dataTable = new DataTable();
            var range = worksheet.RangeUsed();

            if (range == null)
                return dataTable;

            // Добавляем столбцы из первой строки
            var firstRow = range.FirstRow();
            for (int col = 1; col <= range.ColumnCount(); col++)
            {
                string columnName = firstRow.Cell(col).GetString();
                if (string.IsNullOrEmpty(columnName))
                {
                    columnName = $"Column {col}";
                }

                // Убеждаемся, что имена столбцов уникальны
                string uniqueColumnName = columnName;
                int counter = 1;
                while (dataTable.Columns.Contains(uniqueColumnName))
                {
                    uniqueColumnName = $"{columnName}_{counter}";
                    counter++;
                }

                dataTable.Columns.Add(uniqueColumnName, typeof(string));
            }

            // Добавляем данные (начиная со второй строки)
            for (int row = 2; row <= range.RowCount(); row++)
            {
                DataRow dataRow = dataTable.NewRow();
                for (int col = 1; col <= range.ColumnCount(); col++)
                {
                    dataRow[col - 1] = worksheet.Cell(row, col).GetString();
                }
                dataTable.Rows.Add(dataRow);
            }

            return dataTable;
        }

        private void cmbSheets_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbSheets.SelectedItem is KeyValuePair<string, DataTable> selectedSheet)
            {
                //txtFileName.Text = selectedSheet.Key;
                _currentDataTable = selectedSheet.Value;

                // Обновляем DataGrid
                dgExcelData.ItemsSource = _currentDataTable.DefaultView;

                // Обновляем информацию о таблице
                UpdateTableInfo();

                // Обновляем подсветку строк
                UpdateRowHighlighting();
            }
        }

        private void UpdateTableInfo()
        {
            if (_currentDataTable != null)
            {
                txtTableInfo.Text = $"Строк: {_currentDataTable.Rows.Count}, Столбцов: {_currentDataTable.Columns.Count}";
            }
        }

        // Обработчик клика по таблице
        private void dgExcelData_MouseUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                // Исправление для C# 7.3: замена "is not" на старый синтаксис
                if (!(cmbSheets.SelectedItem is KeyValuePair<string, DataTable>))
                    return;

                var selectedSheet = (KeyValuePair<string, DataTable>)cmbSheets.SelectedItem;

                var hitTestResult = VisualTreeHelper.HitTest(dgExcelData, e.GetPosition(dgExcelData));
                if (hitTestResult == null || hitTestResult.VisualHit == null) return;

                DependencyObject cell = VisualTreeHelper.GetParent(hitTestResult.VisualHit);

                // Ищем DataGridCell
                while (cell != null && !(cell is DataGridCell))
                {
                    cell = VisualTreeHelper.GetParent(cell);
                }

                if (cell is DataGridCell dataGridCell)
                {
                    DataGridRow row = FindParent<DataGridRow>(dataGridCell);
                    if (row != null)
                    {
                        int rowIndex = row.GetIndex();
                        string columnName = dataGridCell.Column?.Header?.ToString() ?? "Unknown";
                        string value = GetCellValue(dataGridCell);

                        string currentSheet = selectedSheet.Key;
                        string cellId = $"{currentSheet}|{rowIndex}|{columnName}";

                        // Проверяем, есть ли уже эта ячейка в списке
                        var existingCell = _selectedCells.FirstOrDefault(c => c.UniqueId == cellId);

                        if (existingCell != null)
                        {
                            // Удаляем ячейку из списка
                            _selectedCells.Remove(existingCell);
                            lvSelectedCells.Items.Remove(existingCell);

                            // Убираем подсветку строки
                            RemoveRowHighlight(rowIndex);
                        }
                        else
                        {
                            // Добавляем новую ячейку
                            var cellInfo = new SelectedCellInfo(currentSheet, rowIndex, columnName, value);
                            _selectedCells.Add(cellInfo);
                            lvSelectedCells.Items.Add(cellInfo);

                            // Добавляем подсветку строки
                            HighlightRow(rowIndex);
                        }

                        UpdateSelectionStats();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при выборе ячейки: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetCellValue(DataGridCell cell)
        {
            if (cell.Content is TextBlock textBlock)
                return textBlock.Text;

            if (cell.Content is string str)
                return str;

            return cell.Content?.ToString() ?? "";
        }

        private T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            if (child == null) return null;

            DependencyObject parent = VisualTreeHelper.GetParent(child);
            if (parent == null) return null;

            if (parent is T tParent)
                return tParent;

            return FindParent<T>(parent);
        }

        private void HighlightRow(int rowIndex)
        {
            string rowKey = rowIndex.ToString();

            // Сохраняем исходный цвет строки
            if (!_highlightedRows.ContainsKey(rowKey))
            {
                // Получаем строку из DataGrid
                var row = GetDataGridRow(rowIndex);
                if (row != null)
                {
                    // Сохраняем текущий цвет
                    _highlightedRows[rowKey] = row.Background as SolidColorBrush ?? Brushes.White;

                    // Устанавливаем новый цвет (светло-оранжевый)
                    row.Background = new SolidColorBrush(Color.FromArgb(255, 255, 235, 200));
                }
            }
        }

        private void RemoveRowHighlight(int rowIndex)
        {
            string rowKey = rowIndex.ToString();

            if (_highlightedRows.ContainsKey(rowKey))
            {
                // Восстанавливаем исходный цвет
                var row = GetDataGridRow(rowIndex);
                if (row != null)
                {
                    row.Background = _highlightedRows[rowKey];
                    _highlightedRows.Remove(rowKey);
                }
            }
        }

        private void UpdateRowHighlighting()
        {
            // Очищаем подсветку при смене листа
            _highlightedRows.Clear();

            // Подсвечиваем строки с выбранными ячейками для текущего листа
            if (cmbSheets.SelectedItem is KeyValuePair<string, DataTable> selectedSheet)
            {
                string currentSheet = selectedSheet.Key;
                var cellsForCurrentSheet = _selectedCells.Where(c => c.SheetName == currentSheet).ToList();

                foreach (var cell in cellsForCurrentSheet)
                {
                    HighlightRow(cell.Row - 1); // Excel row -> 0-based index
                }
            }
        }

        private DataGridRow GetDataGridRow(int index)
        {
            return dgExcelData.ItemContainerGenerator.ContainerFromIndex(index) as DataGridRow;
        }

        private void UpdateSelectionStats()
        {
            txtSelectionStats.Text = $"Выбрано: {_selectedCells.Count} ячеек";
        }

        // Кнопка "Очистить всё"
        private void btnClearAllSelections_Click(object sender, RoutedEventArgs e)
        {
            ClearAllSelections();
        }

        private void ClearAllSelections()
        {
            _selectedCells.Clear();
            lvSelectedCells.Items.Clear();
            _highlightedRows.Clear();
            UpdateSelectionStats();
            UpdateRowHighlighting();
        }

        // Кнопка "Удалить выбранное" (из списка справа)
        private void btnRemoveSelected_Click(object sender, RoutedEventArgs e)
        {
            if (lvSelectedCells.SelectedItem is SelectedCellInfo selectedCell)
            {
                _selectedCells.Remove(selectedCell);
                lvSelectedCells.Items.Remove(selectedCell);

                // Убираем подсветку строки
                if (cmbSheets.SelectedItem is KeyValuePair<string, DataTable> selectedSheet &&
                    selectedCell.SheetName == selectedSheet.Key)
                {
                    RemoveRowHighlight(selectedCell.Row - 1);
                }

                UpdateSelectionStats();
            }
        }

        // Кнопка "Экспорт" выбранных ячеек
        private void btnExportSelections_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedCells.Count == 0)
                {
                    MessageBox.Show("Нет выбранных ячеек для экспорта", "Информация",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Filter = "CSV файл|*.csv|Текстовый файл|*.txt",
                    DefaultExt = ".csv",
                    FileName = $"Выбранные_ячейки_{DateTime.Now:yyyyMMdd_HHmmss}"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    ExportSelectedCells(saveFileDialog.FileName);
                    MessageBox.Show("Выбранные ячейки успешно экспортированы!", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка экспорта: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportSelectedCells(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLower();

            if (extension == ".csv")
            {
                using (var writer = new StreamWriter(filePath, false, System.Text.Encoding.UTF8))
                {
                    // Заголовки
                    writer.WriteLine("Лист;Строка;Столбец;Значение");

                    // Данные
                    foreach (var cell in _selectedCells)
                    {
                        writer.WriteLine($"{cell.SheetName};{cell.Row};{cell.ColumnName};{cell.Value}");
                    }
                }
            }
            else if (extension == ".txt")
            {
                using (var writer = new StreamWriter(filePath, false, System.Text.Encoding.UTF8))
                {
                    writer.WriteLine("=== ВЫБРАННЫЕ ЯЧЕЙКИ ===");
                    writer.WriteLine($"Всего ячеек: {_selectedCells.Count}");
                    writer.WriteLine("=".PadRight(50, '='));

                    foreach (var cell in _selectedCells)
                    {
                        writer.WriteLine($"Лист: {cell.SheetName}");
                        writer.WriteLine($"Строка: {cell.Row}, Столбец: {cell.ColumnName}");
                        writer.WriteLine($"Значение: {cell.Value}");
                        writer.WriteLine("-".PadRight(50, '-'));
                    }
                }
            }
        }

        // Методы, которые больше не используются (оставлены для совместимости)
        private void dgExcelData_SelectionChanged(object sender, SelectionChangedEventArgs e) { }
        private void UpdateSelectionInfo() { }
        private void btnSelectAll_Click(object sender, RoutedEventArgs e) { }
        private void btnClearSelection_Click(object sender, RoutedEventArgs e) { }
        private void btnExportSelection_Click(object sender, RoutedEventArgs e) { }

        private void dgExcelData_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            int rowIndex = e.Row.GetIndex();
            string rowKey = rowIndex.ToString();

            // Устанавливаем фон в зависимости от четности строки
            if (!_highlightedRows.ContainsKey(rowKey))
            {
                e.Row.Background = rowIndex % 2 == 0
                    ? Brushes.White
                    : new SolidColorBrush(Color.FromArgb(255, 249, 249, 249));
            }

            // Сохраняем ссылку на строку для быстрого доступа
            e.Row.Tag = rowIndex;
        }

        // Метод для загрузки файла из другого места программы
        public void LoadFile(string filePath)
        {
            if (File.Exists(filePath) &&
                (filePath.EndsWith(".xls") || filePath.EndsWith(".xlsx") ||
                 filePath.EndsWith(".xlsm") || filePath.EndsWith(".xlsb")))
            {
                LoadExcelFile(filePath);
            }
            else
            {
                MessageBox.Show("Выбранный файл не является Excel файлом", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}