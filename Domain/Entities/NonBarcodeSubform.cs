using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ConceptJewelleryPro.Domain.Entities;

public class NonBarcodeSubform : INotifyPropertyChanged
{
    private string _vouNo = string.Empty;
    private string _itemName = string.Empty;
    private decimal _qty = 1m;
    private decimal _weight;
    private string _boxNo = string.Empty;
    private DateTime _punchTime = DateTime.Now;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string VouNo
    {
        get => _vouNo;
        set => SetField(ref _vouNo, value);
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

    public decimal Weight
    {
        get => _weight;
        set => SetField(ref _weight, value);
    }

    public string BoxNo
    {
        get => _boxNo;
        set => SetField(ref _boxNo, value);
    }

    public DateTime PunchTime
    {
        get => _punchTime;
        set => SetField(ref _punchTime, value);
    }

    public NonBarcodeSubform Clone() => new()
    {
        VouNo = VouNo,
        ItemName = ItemName,
        Qty = Qty,
        Weight = Weight,
        BoxNo = BoxNo,
        PunchTime = PunchTime
    };

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
