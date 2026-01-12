using System;
using System.Windows;
using System.Windows.Threading;

namespace ManagerApp.Pages
{
    public partial class WaitingWindow : Window
    {
        public WaitingWindow(string title, string message)
        {
            InitializeComponent();
            Title = title;
            txtMessage.Text = message;
        }

        // Метод для обновления сообщения
        public void UpdateMessage(string message)
        {
            Dispatcher.Invoke(() =>
            {
                txtMessage.Text = message;
                // Принудительно обновляем окно
                this.InvalidateVisual();
            }, DispatcherPriority.Send);
        }
    }
}