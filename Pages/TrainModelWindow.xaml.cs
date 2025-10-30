using ManagerApp.Classes.ModelsStudy;
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
    /// Логика взаимодействия для TrainModelWindow.xaml
    /// </summary>
    public partial class TrainModelWindow : Window
    {
        public TrainModelWindow(List<string> textsToClassify)
        {
            InitializeComponent();
            DataContext = new TrainModelViewModel(textsToClassify);

            // Обработка закрытия окна
            this.Closing += (s, e) =>
            {
                MessageBox.Show("Модель обучена и сохранена!\nВы можете продолжить обучение в любой момент.",
                              "Обучение завершено",
                              MessageBoxButton.OK,
                              MessageBoxImage.Information);
            };
        }
    }
}

