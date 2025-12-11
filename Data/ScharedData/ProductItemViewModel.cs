using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using static ManagerApp.Pages.ComparisonProduct;

namespace ManagerApp.Data.ScharedData
{
    public class ProductItemViewModel : INotifyPropertyChanged
    {
        private string _originalProduct;
        private BitrixProductViewModel _selectedBitrixProduct;

        public string OriginalProduct
        {
            get => _originalProduct;
            set
            {
                if (_originalProduct != value)
                {
                    _originalProduct = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<BitrixProductViewModel> BitrixProducts { get; set; }

        public BitrixProductViewModel SelectedBitrixProduct
        {
            get => _selectedBitrixProduct;
            set
            {
                if (_selectedBitrixProduct != value)
                {
                    _selectedBitrixProduct = value;
                    OnPropertyChanged();
                }
            }
        }

        public ICommand AddCommand { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        public virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
