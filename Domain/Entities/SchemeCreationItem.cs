using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ConceptJewelleryPro.Domain.Entities;

public sealed class SchemeCreationItem : INotifyPropertyChanged
{
    private string _scode = string.Empty;
    private int _nos;
    private string _customerName = string.Empty;
    private decimal _installmentAmt;
    private decimal _month = 11m;
    private decimal _totalAmt;
    private decimal _schemeDiscount;
    private decimal _totalValue;
    private DateTime _sdate = DateTime.Today;
    private DateTime _edate = DateTime.Today.AddMonths(12).AddDays(-5);
    private string _status = "Saved";
    private bool _closed;
    private string _staff = string.Empty;
    private string _entryUser = string.Empty;
    private DateTime _entryDate = DateTime.Today;
    private DateTime _entryTime = DateTime.Now;
    private string _computerName = string.Empty;
    private DateTime? _dob;
    private DateTime? _doa;

    public string Scode
    {
        get => _scode;
        set => SetField(ref _scode, value);
    }

    public int Nos
    {
        get => _nos;
        set => SetField(ref _nos, value);
    }

    public string CustomerName
    {
        get => _customerName;
        set => SetField(ref _customerName, value);
    }

    public decimal InstallmentAmt
    {
        get => _installmentAmt;
        set => SetField(ref _installmentAmt, value);
    }

    public decimal Month
    {
        get => _month;
        set => SetField(ref _month, value);
    }

    public decimal TotalAmt
    {
        get => _totalAmt;
        set => SetField(ref _totalAmt, value);
    }

    public decimal SchemeDiscount
    {
        get => _schemeDiscount;
        set => SetField(ref _schemeDiscount, value);
    }

    public decimal TotalValue
    {
        get => _totalValue;
        set => SetField(ref _totalValue, value);
    }

    public DateTime SDate
    {
        get => _sdate;
        set => SetField(ref _sdate, value);
    }

    public DateTime EDate
    {
        get => _edate;
        set => SetField(ref _edate, value);
    }

    public string Status
    {
        get => _status;
        set => SetField(ref _status, value);
    }

    public bool Closed
    {
        get => _closed;
        set => SetField(ref _closed, value);
    }

    public string Staff
    {
        get => _staff;
        set => SetField(ref _staff, value);
    }

    public string EntryUser
    {
        get => _entryUser;
        set => SetField(ref _entryUser, value);
    }

    public DateTime EntryDate
    {
        get => _entryDate;
        set => SetField(ref _entryDate, value);
    }

    public DateTime EntryTime
    {
        get => _entryTime;
        set => SetField(ref _entryTime, value);
    }

    public string ComputerName
    {
        get => _computerName;
        set => SetField(ref _computerName, value);
    }

    public DateTime? DOB
    {
        get => _dob;
        set => SetField(ref _dob, value);
    }

    public DateTime? DOA
    {
        get => _doa;
        set => SetField(ref _doa, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (!EqualityComparer<T>.Default.Equals(field, value))
        {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
