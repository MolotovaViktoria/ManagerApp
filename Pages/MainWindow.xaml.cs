using ManagerApp.Classes.Read;
using ManagerApp.Data.GetInfo;
using ManagerApp.Data.StructureList;
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
    /// Логика взаимодействия для MainWindows.xaml
    /// </summary>
    public partial class MainWindows : Window
    {
        public MainWindows()
        {
            InitializeComponent();



           


        }

        private void btnLoadRequest_Click(object sender, RoutedEventArgs e)
        {
            Classes.Read.ReadRequst readRequst = new Classes.Read.ReadRequst();
            //txtOutput.Text = readRequst.ReadWorldFile(@"C:\Users\vimol\Desktop\Work\ManagerApi\Заявки\Приложение 1 спецификация_Испр1.docx");
            txtOutput.Text = readRequst.ReadFileAll(@"C:\Users\vimol\Desktop\Work\ManagerApi\Заявки\SupplierPositions (7).xls");
        }
    }
}
