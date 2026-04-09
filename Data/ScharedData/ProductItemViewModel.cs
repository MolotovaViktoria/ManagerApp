using ManagerApp.Data.ScharedData;
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
            _originalProduct = value;
            OnPropertyChanged();
        }
    }

    public decimal Quantity
    {
        get => _quantity;
        set
        {
            _quantity = value;
            OnPropertyChanged();
        }
    }

    public string SelectedMeasureId
    {
        get => _selectedMeasureId;
        set
        {
            _selectedMeasureId = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(MeasureSymbol));
            OnPropertyChanged(nameof(MeasureName));
        }
    }

    public string MeasureSymbol
    {
        get => _measureSymbol;
        set
        {
            _measureSymbol = value;
            OnPropertyChanged();
        }
    }

    public string MeasureName
    {
        get => _measureName;
        set
        {
            _measureName = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<BitrixProductViewModel> BitrixProducts { get; set; }

    public BitrixProductViewModel SelectedBitrixProduct
    {
        get => _selectedBitrixProduct;
        set
        {
            _selectedBitrixProduct = value;
            OnPropertyChanged();
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