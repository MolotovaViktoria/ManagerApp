using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ManagerApp.Data.ScharedData
{
    public class ProductPriceViewModel : INotifyPropertyChanged
    {
        private string _originalProductName;
        public string OriginalProductName
        {
            get => _originalProductName;
            set
            {
                _originalProductName = value;
                OnPropertyChanged(nameof(OriginalProductName));
            }
        }

        private string _bitrixProductName;
        public string BitrixProductName
        {
            get => _bitrixProductName;
            set
            {
                _bitrixProductName = value;
                OnPropertyChanged(nameof(BitrixProductName));
            }
        }

        private decimal _bitrixPrice;
        public decimal BitrixPrice
        {
            get => _bitrixPrice;
            set
            {
                _bitrixPrice = value;
                OnPropertyChanged(nameof(BitrixPrice));
            }
        }

        private decimal _customPrice;
        public decimal CustomPrice
        {
            get => _customPrice;
            set
            {
                _customPrice = value;
                OnPropertyChanged(nameof(CustomPrice));
                CalculatePrices();
            }
        }

        private decimal _quantity = 1;
        public decimal Quantity
        {
            get => _quantity;
            set
            {
                _quantity = value;
                OnPropertyChanged(nameof(Quantity));
                CalculatePrices();
            }
        }

        private string _unit = "шт.";
        public string Unit
        {
            get => _unit;
            set
            {
                _unit = value;
                OnPropertyChanged(nameof(Unit));
            }
        }

        private string _vat;
        public string VAT
        {
            get => _vat;
            set
            {
                _vat = value;
                OnPropertyChanged(nameof(VAT));
                CalculatePrices();
            }
        }

        private decimal _priceWithVAT;
        public decimal PriceWithVAT
        {
            get => _priceWithVAT;
            set
            {
                _priceWithVAT = value;
                OnPropertyChanged(nameof(PriceWithVAT));
            }
        }

        private decimal _totalWithVAT;
        public decimal TotalWithVAT
        {
            get => _totalWithVAT;
            set
            {
                _totalWithVAT = value;
                OnPropertyChanged(nameof(TotalWithVAT));
            }
        }

        private void CalculatePrices()
        {
            if (decimal.TryParse(VAT?.Replace("%", ""), out decimal vatPercent))
            {
                // Преобразуем проценты в коэффициент (20% -> 0.20)
                decimal vatCoefficient = vatPercent / 100m;

                // Цена с НДС = цена без НДС * (1 + НДС)
                PriceWithVAT = CustomPrice * (1 + vatCoefficient);

                // Сумма с НДС = цена с НДС * количество
                TotalWithVAT = PriceWithVAT * Quantity;
            }
            else
            {
                // Если не удалось распарсить НДС, используем значение по умолчанию
                PriceWithVAT = CustomPrice * (1 + 0.20m); // 20% по умолчанию
                TotalWithVAT = PriceWithVAT * Quantity;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}