using ManagerApp.Classes.Read;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ManagerApp.Pages
{
    /// <summary>
    /// Логика взаимодействия для DombPage.xaml
    /// </summary>
    public partial class DombPage : Page
    {
        public DombPage()
        {
            InitializeComponent();
            SetupFileUploadHandlers();
        }

        private void SetupFileUploadHandlers()
        {
            // Настройка обработчиков drag-and-drop
            DropArea.Drop += DropArea_Drop;
            DropArea.DragEnter += DropArea_DragEnter;
            DropArea.DragLeave += DropArea_DragLeave;
            DropArea.AllowDrop = true;
        }

        // Обработчик клика по кнопке "Выберите файл"
        private void SelectFileButton_Click(object sender, RoutedEventArgs e)
        {
            LoadFile();
        }

        // Обработчик события DragEnter
        private void DropArea_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                DropArea.Opacity = 0.8;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        // Обработчик события DragLeave
        private void DropArea_DragLeave(object sender, DragEventArgs e)
        {
            DropArea.Opacity = 1.0;
        }

        // Обработчик события Drop
        private void DropArea_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0)
                {
                    ProcessFile(files[0]);
                }
            }
            DropArea.Opacity = 1.0;
        }

        // Метод загрузки файла через диалог
        private void LoadFile()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = CreateFileFilter(),
                Title = "Выберите файл",
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() == true)
            {
                ProcessFile(openFileDialog.FileName);
            }
        }

        // Создание фильтра для диалога выбора файла
        private string CreateFileFilter()
        {
            string filter = "Все поддерживаемые файлы (";

            // Добавляем Word форматы
            foreach (var format in FormatLists.WordFormatList)
            {
                filter += $"*{format};";
            }

            // Добавляем PDF формат
            filter += $"*{FormatLists.PdfFormatList[0]};";

            // Добавляем Excel форматы
            foreach (var format in FormatLists.ExcelFormatList)
            {
                filter += $"*{format};";
            }

            // Добавляем форматы изображений
            foreach (var format in FormatLists.ImageFormatList)
            {
                filter += $"*{format};";
            }

            filter = filter.TrimEnd(';') + ")|";

            // Формируем полный фильтр
            foreach (var format in FormatLists.WordFormatList)
            {
                filter += $"*{format};";
            }
            foreach (var format in FormatLists.PdfFormatList)
            {
                filter += $"*{format};";
            }
            foreach (var format in FormatLists.ExcelFormatList)
            {
                filter += $"*{format};";
            }
            foreach (var format in FormatLists.ImageFormatList)
            {
                filter += $"*{format};";
            }

            filter = filter.TrimEnd(';') + "|";

            // Отдельные фильтры для каждого типа
            filter += "Word документы (*.docx, *.dotx, *.docm, *.dotm)|*.docx;*.dotx;*.docm;*.dotm|";
            filter += "PDF документы (*.pdf)|*.pdf|";
            filter += "Excel файлы (*.xlsx, *.xls, *.xlsm, *.xlsb, *.csv)|*.xlsx;*.xls;*.xlsm;*.xlsb;*.csv|";
            filter += "Изображения (*.png, *.jpg, *.jpeg, *.bmp, *.gif, *.tiff, *.ico, *.webp)|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tiff;*.ico;*.webp|";
            filter += "Все файлы (*.*)|*.*";

            return filter;
        }

        private void ProcessFile(string filePath)
        {
            // Получаем расширение файла
            string extension = System.IO.Path.GetExtension(filePath)?.ToLower();

            // Определяем, какой тип файла и открываем соответствующую страницу
            if (FormatLists.ExcelFormatList.Contains(extension))
            {
                // Excel файлы
                ExcelFile excelFilePage = new ExcelFile(filePath);
                this.NavigationService.Navigate(excelFilePage);
            }
            else if (FormatLists.PdfFormatList.Contains(extension) ||
                     FormatLists.WordFormatList.Contains(extension))
            {
                // PDF или Word файлы
                WorldPdfFile worldPdfFilePage = new WorldPdfFile();
                worldPdfFilePage.LoadFile(filePath);
                this.NavigationService.Navigate(worldPdfFilePage);
            }
            else if (FormatLists.ImageFormatList.Contains(extension))
            {
                // Файлы изображений
                ProcessImageFile(filePath);
            }
            else
            {
                MessageBox.Show($"Неподдерживаемый формат файла: {extension}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Обработка файла изображения
        // Обработка файла изображения
        private void ProcessImageFile(string filePath)
        {
            try
            {
                // ПРОСТО Создаем и открываем страницу для обработки изображения
                PngPage pngPage = new PngPage(filePath);
                this.NavigationService.Navigate(pngPage);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии изображения: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Метод для открытия изображения (если нужно предпросмотр)
        private void OpenImageForViewing(string filePath)
        {
            try
            {
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(filePath);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();

                // Создаем окно для просмотра изображения
                Window imageWindow = new Window
                {
                    Title = System.IO.Path.GetFileName(filePath),
                    Width = 800,
                    Height = 600,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };

                Image imageControl = new Image
                {
                    Source = bitmap,
                    Stretch = Stretch.Uniform
                };

                ScrollViewer scrollViewer = new ScrollViewer
                {
                    Content = imageControl,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
                };

                imageWindow.Content = scrollViewer;
                imageWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось открыть изображение: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Проверка поддерживаемого формата
        private bool IsSupportedFormat(string extension)
        {
            return FormatLists.WordFormatList.Contains(extension) ||
                   FormatLists.PdfFormatList.Contains(extension) ||
                   FormatLists.ExcelFormatList.Contains(extension) ||
                   FormatLists.ImageFormatList.Contains(extension);
        }

        // Удалены старые методы меню (оставлены только те, что нужны для загрузки файлов)
    }
}