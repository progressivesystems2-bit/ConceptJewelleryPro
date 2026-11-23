using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ConceptJewelleryPro.Domain.Entities;

public sealed class VoucherEntry : INotifyPropertyChanged
{
    private string _vouNo = string.Empty;
    private DateTime _vouDate = DateTime.Today;
    private int _nos;
    private string _vouType = VoucherType.Payments;
    private string _narration = string.Empty;
    private string _narr2 = string.Empty;
    private string _chequeNo = string.Empty;
    private DateTime? _chqDt;
    private string _status = "Saved";
    private string _drAccount = string.Empty;
    private decimal _drAmt;
    private string _crAccount = string.Empty;
    private decimal _crAmt;
    private string _user = string.Empty;
    private DateTime _date = DateTime.Today;
    private TimeSpan _time = DateTime.Now.TimeOfDay;
    private bool _locked;
    private bool _original = true;
    private bool _master = true;
    private bool _masterLock;
    private string _masterDr = string.Empty;
    private string _masterCr = string.Empty;
    private string _cno = string.Empty;
    private DateTime? _cDate;
    private bool _disputed;
    private bool _chandanDispute;
    private string _scode = string.Empty;
    private int _installmentNo;
    private decimal _cardComm;
    private decimal _commAmt;
    private decimal _netCredited;
    private string _bankName = string.Empty;
    private string _cardType = string.Empty;
    private bool _cp;
    private bool _check;
    private string _paymentMode = "Cash";

    public string VouNo
    {
        get => _vouNo;
        set => SetField(ref _vouNo, value);
    }

    public DateTime VouDate
    {
        get => _vouDate;
        set => SetField(ref _vouDate, value);
    }

    public int Nos
    {
        get => _nos;
        set => SetField(ref _nos, value);
    }

    public string VouType
    {
        get => _vouType;
        set => SetField(ref _vouType, value);
    }

    public string Narration
    {
        get => _narration;
        set => SetField(ref _narration, value);
    }

    public string Narr2
    {
        get => _narr2;
        set => SetField(ref _narr2, value);
    }

    public string ChequeNo
    {
        get => _chequeNo;
        set => SetField(ref _chequeNo, value);
    }

    public DateTime? ChqDt
    {
        get => _chqDt;
        set => SetField(ref _chqDt, value);
    }

    public string Status
    {
        get => _status;
        set => SetField(ref _status, value);
    }

    public string DrAccount
    {
        get => _drAccount;
        set => SetField(ref _drAccount, value);
    }

    public decimal DrAmt
    {
        get => _drAmt;
        set => SetField(ref _drAmt, value);
    }

    public string CrAccount
    {
        get => _crAccount;
        set => SetField(ref _crAccount, value);
    }

    public decimal CrAmt
    {
        get => _crAmt;
        set => SetField(ref _crAmt, value);
    }

    public string User
    {
        get => _user;
        set => SetField(ref _user, value);
    }

    public DateTime Date
    {
        get => _date;
        set => SetField(ref _date, value);
    }

    public TimeSpan Time
    {
        get => _time;
        set => SetField(ref _time, value);
    }

    public bool Locked
    {
        get => _locked;
        set => SetField(ref _locked, value);
    }

    public bool Original
    {
        get => _original;
        set => SetField(ref _original, value);
    }

    public bool Master
    {
        get => _master;
        set => SetField(ref _master, value);
    }

    public bool MasterLock
    {
        get => _masterLock;
        set => SetField(ref _masterLock, value);
    }

    public string MasterDr
    {
        get => _masterDr;
        set => SetField(ref _masterDr, value);
    }

    public string MasterCr
    {
        get => _masterCr;
        set => SetField(ref _masterCr, value);
    }

    public string Cno
    {
        get => _cno;
        set => SetField(ref _cno, value);
    }

    public DateTime? CDate
    {
        get => _cDate;
        set => SetField(ref _cDate, value);
    }

    public bool Disputed
    {
        get => _disputed;
        set => SetField(ref _disputed, value);
    }

    public bool ChandanDispute
    {
        get => _chandanDispute;
        set => SetField(ref _chandanDispute, value);
    }

    public string Scode
    {
        get => _scode;
        set => SetField(ref _scode, value);
    }

    public int InstallmentNo
    {
        get => _installmentNo;
        set => SetField(ref _installmentNo, value);
    }

    public decimal CardComm
    {
        get => _cardComm;
        set => SetField(ref _cardComm, value);
    }

    public decimal CommAmt
    {
        get => _commAmt;
        set => SetField(ref _commAmt, value);
    }

    public decimal NetCredited
    {
        get => _netCredited;
        set => SetField(ref _netCredited, value);
    }

    public string BankName
    {
        get => _bankName;
        set => SetField(ref _bankName, value);
    }

    public string CardType
    {
        get => _cardType;
        set => SetField(ref _cardType, value);
    }

    public bool CP
    {
        get => _cp;
        set => SetField(ref _cp, value);
    }

    public bool Check
    {
        get => _check;
        set => SetField(ref _check, value);
    }

    public string PaymentMode
    {
        get => _paymentMode;
        set => SetField(ref _paymentMode, value);
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
