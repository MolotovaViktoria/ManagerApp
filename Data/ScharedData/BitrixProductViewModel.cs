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
        private string _lowerSectionName;
        private string _fullCategoryPath;
        private decimal _price;
        private bool _hasPrice;
        private string _sectionId;
        private decimal _purchasingPrice;
        private bool _hasPurchasingPrice;
        private string _purchasingCurrency;
        public decimal? PurchasingPrice { get; set; } // Добавить
        public bool HasPurchasingPrice { get; set; } // Добавить
                                                     // Для привязки в XAML
        public string PurchasingPriceDisplay =>
            HasPurchasingPrice && PurchasingPrice.HasValue
                ? PurchasingPrice.Value.ToString("##0.00 ₽")
                : "Нет цены";
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

        // НОВОЕ ПОЛЕ: Нижний раздел (непосредственный раздел товара)
        public string LowerSectionName
        {
            get => _lowerSectionName;
            set
            {
                if (_lowerSectionName != value)
                {
                    _lowerSectionName = value;
                    OnPropertyChanged();
                }
            }
        }

        // НОВОЕ ПОЛЕ: Полный путь категории (например: "ЭЛЕКТРОТОВАРЫ → Розетки")
        public string FullCategoryPath
        {
            get => _fullCategoryPath;
            set
            {
                if (_fullCategoryPath != value)
                {
                    _fullCategoryPath = value;
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

        //// НОВОЕ ПОЛЕ: Закупочная цена
        //public decimal PurchasingPrice
        //{
        //    get => _purchasingPrice;
        //    set
        //    {
        //        if (_purchasingPrice != value)
        //        {
        //            _purchasingPrice = value;
        //            OnPropertyChanged();
        //            OnPropertyChanged(nameof(HasPurchasingPrice));
        //        }
        //    }
        //}

        //// НОВОЕ ПОЛЕ: Флаг наличия закупочной цены
        //public bool HasPurchasingPrice
        //{
        //    get => _purchasingPrice > 0;
        //    set
        //    {
        //        if (_hasPurchasingPrice != value)
        //        {
        //            _hasPurchasingPrice = value;
        //            OnPropertyChanged();
        //        }
        //    }
        //}

        // НОВОЕ ПОЛЕ: Валюта закупочной цены
        public string PurchasingCurrency
        {
            get => _purchasingCurrency;
            set
            {
                if (_purchasingCurrency != value)
                {
                    _purchasingCurrency = value;
                    OnPropertyChanged();
                }
            }
        }

        // НОВОЕ СВОЙСТВО: Цена для отображения (сначала обычная, потом закупочная)
        public decimal DisplayPrice => (decimal)(HasPrice ? Price :
                                       (HasPurchasingPrice ? PurchasingPrice : 0));

        // НОВОЕ СВОЙСТВО: Флаг наличия любой цены
        public bool HasAnyPrice => HasPrice || HasPurchasingPrice;

        public string SectionId
        {
            get => _sectionId;
            set
            {
                if (_sectionId != value)
                {
                    _sectionId = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasSectionId));
                }
            }
        }

        public bool HasSectionId => !string.IsNullOrEmpty(SectionId);

        // Свойство для отображения (можно использовать в XAML)
        public string DisplayCategory =>
            !string.IsNullOrEmpty(LowerSectionName) ? LowerSectionName : CategoryName;

        // Свойство для отображения полной информации о категории
        public string CategoryTooltip =>
            !string.IsNullOrEmpty(FullCategoryPath) ? FullCategoryPath : CategoryName;

        // Свойство для отображения информации о ценах
        public string PriceTooltip
        {
            get
            {
                var sb = new StringBuilder();

                if (HasPrice)
                {
                    sb.AppendLine($"Цена: {Price:##0.00} ₽");
                }

                if (HasPurchasingPrice)
                {
                    sb.AppendLine($"Закупочная: {PurchasingPrice:##0.00} ₽");
                }

                if (!HasPrice && !HasPurchasingPrice)
                {
                    sb.Append("Нет цен");
                }

                return sb.ToString();
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