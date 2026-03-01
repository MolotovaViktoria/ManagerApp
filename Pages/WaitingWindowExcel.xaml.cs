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
using System.Windows.Shapes;

namespace ManagerApp.Pages
{
    /// <summary>
    /// Логика взаимодействия для WaitingWindowExcel.xaml
    /// </summary>
    public partial class WaitingWindowExcel : Window
    {
        private System.Windows.Controls.ProgressBar progressBar;
        private System.Windows.Controls.TextBlock statusText;
        private System.Windows.Controls.TextBlock progressText;

        public WaitingWindowExcel(string title, string initialMessage)
        {
            this.Title = title;
            this.Width = 400;
            this.Height = 150;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            this.ResizeMode = ResizeMode.NoResize;
            this.WindowStyle = WindowStyle.ToolWindow;

            var grid = new System.Windows.Controls.Grid();
            grid.Margin = new Thickness(10);

            // Status text
            statusText = new System.Windows.Controls.TextBlock();
            statusText.Text = initialMessage;
            statusText.Margin = new Thickness(0, 0, 0, 5);
            statusText.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
            Grid.SetRow(statusText, 0);
            grid.Children.Add(statusText);

            // Progress bar
            progressBar = new System.Windows.Controls.ProgressBar();
            progressBar.Height = 25;
            progressBar.Margin = new Thickness(0, 5, 0, 5);
            progressBar.Minimum = 0;
            progressBar.Maximum = 100;
            Grid.SetRow(progressBar, 1);
            grid.Children.Add(progressBar);

            // Progress text (X из Y)
            progressText = new System.Windows.Controls.TextBlock();
            progressText.Text = "";
            progressText.Margin = new Thickness(0, 5, 0, 0);
            progressText.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
            Grid.SetRow(progressText, 2);
            grid.Children.Add(progressText);

            // Add rows
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            this.Content = grid;
        }

        public void UpdateProgress(int percent, string status)
        {
            Dispatcher.Invoke(() =>
            {
                progressBar.Value = percent;
                statusText.Text = status;

                // Если в статусе есть информация о прогрессе (например, "10 из 100")
                if (status.Contains(" из "))
                {
                    progressText.Text = status;
                }
            });
        }
    }
}
