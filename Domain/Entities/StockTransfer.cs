using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ConceptJewelleryPro.Domain.Entities;

public class StockTransfer : INotifyPropertyChanged
{
    private string _transferNo = string.Empty;
    private DateTime _transferDate = DateTime.Today;
    private string _fromLocation = "Showroom";
    private string _toLocation = "Damage";
    private string _transferType = "Manual Transfer";
    private decimal _totalQty;
    private decimal _totalGrossWt;
    private decimal _totalStoneWt;
    private decimal _totalNetWt;
    private decimal _totalStoneValue;
    private decimal _totalMaking;
    private decimal _totalMRP;
    private string _remarks = string.Empty;
    private string _entryUser = string.Empty;
    private DateTime? _entryDate;
    private string _status = "Saved";

    public event PropertyChangedEventHandler? PropertyChanged;

    public string TransferNo
    {
        get => _transferNo;
        set => SetField(ref _transferNo, value);
    }

    public DateTime TransferDate
    {
        get => _transferDate;
        set => SetField(ref _transferDate, value);
    }

    public string FromLocation
    {
        get => _fromLocation;
        set => SetField(ref _fromLocation, value);
    }

    public string ToLocation
    {
        get => _toLocation;
        set => SetField(ref _toLocation, value);
    }

    public string TransferType
    {
        get => _transferType;
        set => SetField(ref _transferType, value);
    }

    public decimal TotalQty
    {
        get => _totalQty;
        set => SetField(ref _totalQty, value);
    }

    public decimal TotalGrossWt
    {
        get => _totalGrossWt;
        set => SetField(ref _totalGrossWt, value);
    }

    public decimal TotalStoneWt
    {
        get => _totalStoneWt;
        set => SetField(ref _totalStoneWt, value);
    }

    public decimal TotalNetWt
    {
        get => _totalNetWt;
        set => SetField(ref _totalNetWt, value);
    }

    public decimal TotalStoneValue
    {
        get => _totalStoneValue;
        set => SetField(ref _totalStoneValue, value);
    }

    public decimal TotalMaking
    {
        get => _totalMaking;
        set => SetField(ref _totalMaking, value);
    }

    public decimal TotalMRP
    {
        get => _totalMRP;
        set => SetField(ref _totalMRP, value);
    }

    public string Remarks
    {
        get => _remarks;
        set => SetField(ref _remarks, value);
    }

    public string EntryUser
    {
        get => _entryUser;
        set => SetField(ref _entryUser, value);
    }

    public DateTime? EntryDate
    {
        get => _entryDate;
        set => SetField(ref _entryDate, value);
    }

    public string Status
    {
        get => _status;
        set => SetField(ref _status, value);
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
