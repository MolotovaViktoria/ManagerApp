using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ManagerApp.Data.ScharedData
{
    public class ProductPriceViewModel : INotifyPropertyChanged
    {
        public int BitrixProductId { get; set; }

        private string _originalProductName;
        public string OriginalProductName
        {
            get => _originalProductName;
            set { _originalProductName = value; OnPropertyChanged(); }
        }

        private string _bitrixProductName;
        public string BitrixProductName
        {
            get => _bitrixProductName;
            set { _bitrixProductName = value; OnPropertyChanged(); }
        }

        private decimal _bitrixPrice;
        public decimal BitrixPrice
        {
            get => _bitrixPrice;
            set { _bitrixPrice = value; OnPropertyChanged(); }
        }

        private decimal _purchasingPrice;
        public decimal PurchasingPrice
        {
            get => _purchasingPrice;
            set { _purchasingPrice = value; OnPropertyChanged(); }
        }

        private decimal _customPrice;
        public decimal CustomPrice
        {
            get => _customPrice;
            set
            {
                _customPrice = value;
                OnPropertyChanged();
                CalculatePrices();
            }
        }

        private decimal _quantity = 1;
        public decimal Quantity
        {
            get => _quantity;
            set
            {
                _quantity = value > 0 ? value : 1;
                OnPropertyChanged();
                CalculatePrices();
            }
        }

        private string _unit = "шт.";
        public string Unit
        {
            get => _unit;
            set { _unit = value; OnPropertyChanged(); }
        }

        private string _vat = "20";
        public string VAT
        {
            get => _vat;
            set
            {
                _vat = value;
                OnPropertyChanged();
                CalculatePrices();
            }
        }

        private decimal _priceWithVAT;
        public decimal PriceWithVAT
        {
            get => _priceWithVAT;
            set { _priceWithVAT = value; OnPropertyChanged(); }
        }

        private decimal _totalWithVAT;
        public decimal TotalWithVAT
        {
            get => _totalWithVAT;
            set { _totalWithVAT = value; OnPropertyChanged(); }
        }

        private void CalculatePrices()
        {
            if (decimal.TryParse(VAT?.Replace("%", ""), NumberStyles.Any, CultureInfo.InvariantCulture, out var vatPercent))
            {
                vatPercent = vatPercent / 100;
                PriceWithVAT = CustomPrice * (1 + vatPercent);
                TotalWithVAT = PriceWithVAT * Quantity;
            }
            else
            {
                PriceWithVAT = CustomPrice;
                TotalWithVAT = CustomPrice * Quantity;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}