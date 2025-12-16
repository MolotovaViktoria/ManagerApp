using ClosedXML.Excel;
using ManagerApp.Data.ScharedData;
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
        public string UniqueId { get; set; }

        // Для отображения в ListView
        public string DisplayText => $"{SheetName} - Строка {Row}, Столбец {ColumnName}: {Value}";

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
        private Dictionary<int, SolidColorBrush> _originalRowColors = new Dictionary<int, SolidColorBrush>();
        private bool _isMouseDragging = false;
        private Point _mouseDragStartPoint;

        public ExcelFile(string filePath = null)
        {
            InitializeComponent();

            if (!string.IsNullOrEmpty(filePath))
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    LoadExcelFile(filePath);
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e) { }

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
                UpdateFileInfo(filePath);
                LoadWorksheets(filePath);

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
                    LoadWorksheetsWithNpoi(filePath);
                }
                else if (extension == ".xlsx" || extension == ".xlsm" || extension == ".xlsb")
                {
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

            for (int col = headerRow.FirstCellNum; col < headerRow.LastCellNum; col++)
            {
                ICell cell = headerRow.GetCell(col);
                string columnName = cell != null ? cell.ToString() : $"Column {col + 1}";

                string uniqueColumnName = columnName;
                int counter = 1;
                while (dataTable.Columns.Contains(uniqueColumnName))
                {
                    uniqueColumnName = $"{columnName}_{counter}";
                    counter++;
                }

                dataTable.Columns.Add(uniqueColumnName, typeof(string));
            }

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

            var firstRow = range.FirstRow();
            for (int col = 1; col <= range.ColumnCount(); col++)
            {
                string columnName = firstRow.Cell(col).GetString();
                if (string.IsNullOrEmpty(columnName))
                {
                    columnName = $"Column {col}";
                }

                string uniqueColumnName = columnName;
                int counter = 1;
                while (dataTable.Columns.Contains(uniqueColumnName))
                {
                    uniqueColumnName = $"{columnName}_{counter}";
                    counter++;
                }

                dataTable.Columns.Add(uniqueColumnName, typeof(string));
            }

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
                _currentDataTable = selectedSheet.Value;
                dgExcelData.ItemsSource = _currentDataTable.DefaultView;
                UpdateTableInfo();
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

        private void dgExcelData_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _mouseDragStartPoint = e.GetPosition(dgExcelData);
            _isMouseDragging = false;
        }

        private void dgExcelData_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var currentPosition = e.GetPosition(dgExcelData);
                if (Math.Abs(currentPosition.X - _mouseDragStartPoint.X) > 5 ||
                    Math.Abs(currentPosition.Y - _mouseDragStartPoint.Y) > 5)
                {
                    _isMouseDragging = true;
                }
            }
        }

        private void dgExcelData_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (_isMouseDragging)
                {
                    ProcessAllSelectedCells();
                    _isMouseDragging = false;
                    dgExcelData.UnselectAllCells();
                }
                else
                {
                    ProcessSingleCellClick(e);
                    dgExcelData.UnselectAllCells();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ProcessSingleCellClick(MouseButtonEventArgs e)
        {
            if (!(cmbSheets.SelectedItem is KeyValuePair<string, DataTable>))
                return;

            var selectedSheet = (KeyValuePair<string, DataTable>)cmbSheets.SelectedItem;

            var hitTestResult = VisualTreeHelper.HitTest(dgExcelData, e.GetPosition(dgExcelData));
            if (hitTestResult?.VisualHit == null) return;

            DependencyObject cell = hitTestResult.VisualHit;
            while (cell != null && !(cell is DataGridCell))
            {
                cell = VisualTreeHelper.GetParent(cell);
            }

            if (cell is DataGridCell dataGridCell)
            {
                var row = FindParent<DataGridRow>(dataGridCell);
                if (row != null)
                {
                    int rowIndex = row.GetIndex();
                    string columnName = dataGridCell.Column?.Header?.ToString() ?? "Unknown";
                    string value = GetCellValue(dataGridCell);
                    string currentSheet = selectedSheet.Key;
                    string cellId = $"{currentSheet}|{rowIndex}|{columnName}";

                    var existingCell = _selectedCells.FirstOrDefault(c => c.UniqueId == cellId);

                    if (existingCell != null)
                    {
                        _selectedCells.Remove(existingCell);
                        lvSelectedCells.Items.Remove(existingCell);
                        CheckAndRemoveRowHighlight(rowIndex, currentSheet);
                    }
                    else
                    {
                        var cellInfo = new SelectedCellInfo(currentSheet, rowIndex, columnName, value);
                        _selectedCells.Add(cellInfo);
                        lvSelectedCells.Items.Add(cellInfo);
                        HighlightRow(rowIndex);
                    }

                    UpdateListViewFromSelectedCells();
                }
            }
        }

        private void ProcessAllSelectedCells()
        {
            if (dgExcelData == null || lvSelectedCells == null) return;
            if (!(cmbSheets.SelectedItem is KeyValuePair<string, DataTable>)) return;

            var selectedSheet = (KeyValuePair<string, DataTable>)cmbSheets.SelectedItem;
            string currentSheet = selectedSheet.Key;

            foreach (DataGridCellInfo cellInfo in dgExcelData.SelectedCells)
            {
                if (cellInfo.Item != null && cellInfo.Column?.Header != null)
                {
                    int rowIndex = dgExcelData.Items.IndexOf(cellInfo.Item);
                    string columnName = cellInfo.Column.Header.ToString();
                    string cellValue = GetCellValueFromCellInfo(cellInfo);

                    if (!string.IsNullOrWhiteSpace(cellValue))
                    {
                        string cellId = $"{currentSheet}|{rowIndex}|{columnName}";

                        if (!_selectedCells.Any(c => c.UniqueId == cellId))
                        {
                            var newCell = new SelectedCellInfo(currentSheet, rowIndex, columnName, cellValue);
                            _selectedCells.Add(newCell);
                            HighlightRow(rowIndex);
                        }
                    }
                }
            }

            UpdateListViewFromSelectedCells();
        }

        private void HighlightRow(int rowIndex)
        {
            if (!_originalRowColors.ContainsKey(rowIndex))
            {
                var row = GetDataGridRow(rowIndex);
                if (row != null)
                {
                    _originalRowColors[rowIndex] = row.Background as SolidColorBrush ?? Brushes.White;
                    row.Background = new SolidColorBrush(Color.FromArgb(255, 255, 235, 200)); // Светло-оранжевый
                }
            }
        }

        private void RemoveRowHighlight(int rowIndex)
        {
            if (_originalRowColors.ContainsKey(rowIndex))
            {
                var row = GetDataGridRow(rowIndex);
                if (row != null)
                {
                    row.Background = _originalRowColors[rowIndex];
                    _originalRowColors.Remove(rowIndex);
                }
            }
        }

        private void CheckAndRemoveRowHighlight(int rowIndex, string sheetName)
        {
            // Проверяем, есть ли еще ячейки в этой строке для текущего листа
            bool hasOtherCellsInRow = _selectedCells.Any(c =>
                c.SheetName == sheetName && (c.Row - 1) == rowIndex);

            if (!hasOtherCellsInRow)
            {
                RemoveRowHighlight(rowIndex);
            }
        }

        private void UpdateRowHighlighting()
        {
            // Очищаем старые цвета
            _originalRowColors.Clear();

            if (cmbSheets.SelectedItem is KeyValuePair<string, DataTable> selectedSheet)
            {
                string currentSheet = selectedSheet.Key;
                var rowsToHighlight = _selectedCells
                    .Where(c => c.SheetName == currentSheet)
                    .Select(c => c.Row - 1)
                    .Distinct();

                foreach (int rowIndex in rowsToHighlight)
                {
                    HighlightRow(rowIndex);
                }
            }
        }

        private DataGridRow GetDataGridRow(int index)
        {
            return dgExcelData.ItemContainerGenerator.ContainerFromIndex(index) as DataGridRow;
        }

        private void UpdateListViewFromSelectedCells()
        {
            lvSelectedCells.Items.Clear();

            if (cmbSheets.SelectedItem is KeyValuePair<string, DataTable> selectedSheet)
            {
                string currentSheet = selectedSheet.Key;
                var cellsForCurrentSheet = _selectedCells
                    .Where(c => c.SheetName == currentSheet)
                    .OrderBy(c => c.Row)
                    .ThenBy(c => c.ColumnName);

                foreach (var cell in cellsForCurrentSheet)
                {
                    lvSelectedCells.Items.Add(cell);
                }
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

        private string GetCellValueFromCellInfo(DataGridCellInfo cellInfo)
        {
            if (cellInfo.Column?.GetCellContent(cellInfo.Item) is TextBlock textBlock)
                return textBlock.Text?.Trim();

            if (cellInfo.Item is DataRowView rowView && cellInfo.Column != null)
            {
                string columnName = cellInfo.Column.Header?.ToString();
                if (!string.IsNullOrEmpty(columnName) && rowView.Row.Table.Columns.Contains(columnName))
                {
                    return rowView[columnName]?.ToString()?.Trim();
                }
            }

            return string.Empty;
        }



        private void ClearAllSelections()
        {
            // Восстанавливаем цвета всех строк
            foreach (var kvp in _originalRowColors)
            {
                var row = GetDataGridRow(kvp.Key);
                if (row != null)
                {
                    row.Background = kvp.Value;
                }
            }

            _selectedCells.Clear();
            lvSelectedCells.Items.Clear();
            _originalRowColors.Clear();
        }

        private void btnBack_Click(object sender, RoutedEventArgs e) 
        {
            if (NavigationService.CanGoBack)
            {
                NavigationService.GoBack();
            }
        }

        private void btnNext_Click(object sender, RoutedEventArgs e) {



            try
            {
                // Получаем список значений из выбранных ячеек
                var selectedProducts = new List<string>();

                foreach (SelectedCellInfo cell in lvSelectedCells.Items)
                {
                    if (!string.IsNullOrWhiteSpace(cell.Value))
                    {
                        selectedProducts.Add(cell.Value);
                    }
                }

                if (selectedProducts.Count == 0)
                {
                    MessageBox.Show("Нет выбранных товаров для сопоставления",
                        "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Сохраняем выбранные товары
                ProductSelectionManager.SetProducts(selectedProducts);

                // Открываем страницу сопоставления
                var comparisonPage = new ComparisonProduct(selectedProducts);

                // Получаем родительское окно для навигации
                var window = Window.GetWindow(this);
                if (window is HomeWindows homeWindow)
                {
                    homeWindow.NavigateToPage(comparisonPage);
                }
                else if (window != null)
                {
                    // Если это другое окно, используем Frame
                    var frame = FindParent<Frame>(this);
                    if (frame != null)
                    {
                        frame.Navigate(comparisonPage);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка перехода к сопоставлению: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }

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

        private void dgExcelData_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            int rowIndex = e.Row.GetIndex();
            e.Row.Tag = rowIndex;

            // Если строка не подсвечена, устанавливаем стандартный цвет
            if (!_originalRowColors.ContainsKey(rowIndex))
            {
                e.Row.Background = rowIndex % 2 == 0
                    ? Brushes.White
                    : new SolidColorBrush(Color.FromArgb(255, 249, 249, 249));
            }
        }

        public void LoadFile(string filePath)
        {
            if (File.Exists(filePath) &&
                (filePath.EndsWith(".xls", StringComparison.OrdinalIgnoreCase) ||
                 filePath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) ||
                 filePath.EndsWith(".xlsm", StringComparison.OrdinalIgnoreCase) ||
                 filePath.EndsWith(".xlsb", StringComparison.OrdinalIgnoreCase)))
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    LoadExcelFile(filePath);
                }), System.Windows.Threading.DispatcherPriority.Normal);
            }
            else
            {
                MessageBox.Show("Выбранный файл не является Excel файлом", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void btnClearList_Click(object sender, RoutedEventArgs e)
        {
            ClearAllSelections();
        }





      
    }
}