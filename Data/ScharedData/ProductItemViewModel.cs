using ManagerApp.Data.ScharedData;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

public class ProductItemViewModel : INotifyPropertyChanged
{
    private int _index;
    private string _originalProduct;
    private BitrixProductViewModel _selectedBitrixProduct;
    private decimal _quantity = 1;
    private string _selectedMeasureId;
    private string _measureSymbol;
    private string _measureName;
    private string _description;
    private ObservableCollection<BitrixProductViewModel> _bitrixProducts;

    public string Description
    {
        get => _description;
        set
        {
            if (_description != value)
            {
                _description = value;
                OnPropertyChanged();
                Console.WriteLine($"✅ Описание обновлено для {OriginalProduct}: {_description?.Substring(0, Math.Min(50, _description?.Length ?? 0))}...");
            }
        }
    }

    public int Index
    {
        get => _index;
        set
        {
            _index = value;
            OnPropertyChanged();
        }
    }

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

    public decimal Quantity
    {
        get => _quantity;
        set
        {
            if (_quantity != value)
            {
                _quantity = value;
                OnPropertyChanged();
            }
        }
    }

    public string SelectedMeasureId
    {
        get => _selectedMeasureId;
        set
        {
            if (_selectedMeasureId != value)
            {
                _selectedMeasureId = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(MeasureSymbol));
                OnPropertyChanged(nameof(MeasureName));
            }
        }
    }

    public string MeasureSymbol
    {
        get => _measureSymbol;
        set
        {
            if (_measureSymbol != value)
            {
                _measureSymbol = value;
                OnPropertyChanged();
            }
        }
    }

    public string MeasureName
    {
        get => _measureName;
        set
        {
            if (_measureName != value)
            {
                _measureName = value;
                OnPropertyChanged();
            }
        }
    }

    public ObservableCollection<BitrixProductViewModel> BitrixProducts
    {
        get => _bitrixProducts;
        set
        {
            _bitrixProducts = value;
            OnPropertyChanged();
        }
    }

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
    public ICommand RemoveCommand { get; set; }

    public event PropertyChangedEventHandler PropertyChanged;

    public virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}