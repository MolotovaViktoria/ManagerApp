using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ManagerApp.Pages
{
    public partial class History : Page
    {
        private string historyFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "History");
        private ObservableCollection<InvoiceFile> invoiceFiles = new ObservableCollection<InvoiceFile>();

        public History()
        {
            InitializeComponent();
            Loaded += History_Loaded;
        }

        private void History_Loaded(object sender, RoutedEventArgs e)
        {
            LoadHistoryFiles();
        }

        private void LoadHistoryFiles()
        {
            try
            {
                invoiceFiles.Clear();

                if (!Directory.Exists(historyFolderPath))
                {
                    Directory.CreateDirectory(historyFolderPath);
                    ShowNoFilesMessage();
                    return;
                }

                // Получаем все папки с датами
                var dateFolders = Directory.GetDirectories(historyFolderPath)
                    .OrderByDescending(f => f);

                if (!dateFolders.Any())
                {
                    ShowNoFilesMessage();
                    return;
                }

                foreach (var dateFolder in dateFolders)
                {
                    string folderName = Path.GetFileName(dateFolder);

                    // Получаем все Word файлы в папке
                    var wordFiles = Directory.GetFiles(dateFolder, "*.docx")
                        .OrderByDescending(f => File.GetCreationTime(f));

                    foreach (var file in wordFiles)
                    {
                        var fileInfo = new FileInfo(file);
                        invoiceFiles.Add(new InvoiceFile
                        {
                            FileName = Path.GetFileNameWithoutExtension(file),
                            FullPath = file,
                            Date = folderName,
                            CreatedTime = fileInfo.CreationTime,
                            FileSize = fileInfo.Length
                        });
                    }
                }

                if (invoiceFiles.Count == 0)
                {
                    ShowNoFilesMessage();
                }
                else
                {
                    // Привязываем данные к ListBox
                    listInvoices.ItemsSource = invoiceFiles;
                    listInvoices.Visibility = Visibility.Visible;
                    txtNoFiles.Visibility = Visibility.Collapsed;
                    txtFilesCount.Text = $"Найдено счетов: {invoiceFiles.Count}";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки истории: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowNoFilesMessage()
        {
            txtNoFiles.Visibility = Visibility.Visible;
            listInvoices.Visibility = Visibility.Collapsed;
            txtFilesCount.Text = "Счетов не найдено";
        }

        // Этот метод будет вызываться при двойном клике
        private void listInvoices_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            OpenSelectedInvoice();
        }

        // Метод для открытия выбранного счета
        private void OpenSelectedInvoice()
        {
            var selectedFile = listInvoices.SelectedItem as InvoiceFile;
            if (selectedFile != null)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = selectedFile.FullPath,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Не удалось открыть файл: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("Выберите счет для открытия", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadHistoryFiles();
        }

        private void btnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!Directory.Exists(historyFolderPath))
                {
                    Directory.CreateDirectory(historyFolderPath);
                }

                Process.Start("explorer.exe", historyFolderPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось открыть папку: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = txtSearch.Text.ToLower();

            if (string.IsNullOrWhiteSpace(searchText))
            {
                listInvoices.ItemsSource = invoiceFiles;
                return;
            }

            var filtered = invoiceFiles.Where(f =>
                f.FileName.ToLower().Contains(searchText) ||
                f.Date.ToLower().Contains(searchText)).ToList();

            listInvoices.ItemsSource = filtered;
            txtFilesCount.Text = $"Найдено: {filtered.Count}";
        }

        // Обработчик выделения в списке
        private void listInvoices_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Можно добавить логику при выделении, если нужно
        }

        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            var selectedFile = listInvoices.SelectedItem as InvoiceFile;
            if (selectedFile != null)
            {
                var result = MessageBox.Show($"Удалить счет '{selectedFile.FileName}'?",
                    "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        File.Delete(selectedFile.FullPath);
                        invoiceFiles.Remove(selectedFile);

                        // Проверяем, пуста ли теперь папка с датой
                        string dateFolder = Path.GetDirectoryName(selectedFile.FullPath);
                        if (Directory.Exists(dateFolder) && !Directory.GetFiles(dateFolder).Any())
                        {
                            Directory.Delete(dateFolder);
                        }

                        MessageBox.Show("Счет удален", "Успех",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка удаления: {ex.Message}", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        // Класс для отображения информации о файле
        public class InvoiceFile
        {
            public string FileName { get; set; }
            public string FullPath { get; set; }
            public string Date { get; set; }
            public DateTime CreatedTime { get; set; }
            public long FileSize { get; set; }

            public string FileSizeFormatted
            {
                get
                {
                    if (FileSize < 1024) return $"{FileSize} Б";
                    if (FileSize < 1024 * 1024) return $"{(FileSize / 1024.0):0.0} КБ";
                    return $"{(FileSize / (1024.0 * 1024.0)):0.0} МБ";
                }
            }

            public string CreatedTimeFormatted => CreatedTime.ToString("HH:mm:ss");
        }
    }
}