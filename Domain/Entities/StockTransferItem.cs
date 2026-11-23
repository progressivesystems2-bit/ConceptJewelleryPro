using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ConceptJewelleryPro.Domain.Entities;

public class StockTransferItem : INotifyPropertyChanged
{
    private long _autoNo;
    private string _transferNo = string.Empty;
    private string _barcode = string.Empty;
    private string _itemName = string.Empty;
    private decimal _qty = 1m;
    private decimal _grossWt;
    private decimal _stoneWt;
    private decimal _netWt;
    private decimal _stoneValue;
    private decimal _making;
    private decimal _mrp;
    private string _purity = string.Empty;
    private string _boxNo = string.Empty;
    private string _description = string.Empty;
    private decimal _costPrice;

    public event PropertyChangedEventHandler? PropertyChanged;

    public long AutoNo
    {
        get => _autoNo;
        set => SetField(ref _autoNo, value);
    }

    public string TransferNo
    {
        get => _transferNo;
        set => SetField(ref _transferNo, value);
    }

    public string Barcode
    {
        get => _barcode;
        set => SetField(ref _barcode, value);
    }

    public string ItemName
    {
        get => _itemName;
        set => SetField(ref _itemName, value);
    }

    public decimal Qty
    {
        get => _qty;
        set => SetField(ref _qty, value);
    }

    public decimal GrossWt
    {
        get => _grossWt;
        set => SetField(ref _grossWt, value);
    }

    public decimal StoneWt
    {
        get => _stoneWt;
        set => SetField(ref _stoneWt, value);
    }

    public decimal NetWt
    {
        get => _netWt;
        set
        {
            if (SetField(ref _netWt, value))
            {
                OnPropertyChanged(nameof(MakingPerGram));
            }
        }
    }

    public decimal StoneValue
    {
        get => _stoneValue;
        set => SetField(ref _stoneValue, value);
    }

    public decimal Making
    {
        get => _making;
        set
        {
            if (SetField(ref _making, value))
            {
                OnPropertyChanged(nameof(MakingPerGram));
            }
        }
    }

    public decimal MakingPerGram => NetWt > 0 ? Math.Round(Making / NetWt, 2) : 0m;

    public decimal MRP
    {
        get => _mrp;
        set => SetField(ref _mrp, value);
    }

    public string Purity
    {
        get => _purity;
        set => SetField(ref _purity, value);
    }

    public string BoxNo
    {
        get => _boxNo;
        set => SetField(ref _boxNo, value);
    }

    public string Description
    {
        get => _description;
        set => SetField(ref _description, value);
    }

    public decimal CostPrice
    {
        get => _costPrice;
        set => SetField(ref _costPrice, value);
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
