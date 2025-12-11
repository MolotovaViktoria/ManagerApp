using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ManagerApp.Data.ScharedData
{
    public class BitrixProductViewModel : INotifyPropertyChanged, IEquatable<BitrixProductViewModel>
    {
        private string _productId;
        private string _productName;
        private string _categoryName;
        private decimal _price;
        private bool _hasPrice;
        private string _sectionId;

        public string ProductId
        {
            get => _productId;
            set
            {
                if (_productId != value)
                {
                    _productId = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ProductName
        {
            get => _productName;
            set
            {
                if (_productName != value)
                {
                    _productName = value;
                    OnPropertyChanged();
                }
            }
        }

        public string CategoryName
        {
            get => _categoryName;
            set
            {
                if (_categoryName != value)
                {
                    _categoryName = value;
                    OnPropertyChanged();
                }
            }
        }

        public decimal Price
        {
            get => _price;
            set
            {
                if (_price != value)
                {
                    _price = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool HasPrice
        {
            get => _hasPrice;
            set
            {
                if (_hasPrice != value)
                {
                    _hasPrice = value;
                    OnPropertyChanged();
                }
            }
        }

        public string SectionId
        {
            get => _sectionId;
            set
            {
                if (_sectionId != value)
                {
                    _sectionId = value;
                    OnPropertyChanged();
                }
            }
        }

        // Для сравнения товаров по ID
        public bool Equals(BitrixProductViewModel other)
        {
            if (other is null) return false;
            return ProductId == other.ProductId;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as BitrixProductViewModel);
        }

        public override int GetHashCode()
        {
            return ProductId?.GetHashCode() ?? 0;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
