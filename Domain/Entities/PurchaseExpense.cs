using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ConceptJewelleryPro.Domain.Entities;

public class PurchaseExpense
{
    public int Nos { get; set; }
    public string VouType { get; set; } = "PurchaseE";
    public string VouNo { get; set; } = string.Empty;
    public DateTime BillDate { get; set; } = DateTime.Today;
    public string AccountName { get; set; } = string.Empty;
    public string Head { get; set; } = string.Empty;
    public string PartyName { get; set; } = string.Empty;
    public string Gstin { get; set; } = string.Empty;
    public string SuppBillNo { get; set; } = string.Empty;
    public DateTime? SuppBillDate { get; set; } = DateTime.Today;
    public bool Reverse { get; set; }
    public decimal ReverseTaxRate { get; set; }
    public decimal TotalQuantity { get; set; }
    public decimal GrossTotal { get; set; }
    public decimal TaxableAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public string ExclusiveAccount { get; set; } = string.Empty;
    public decimal Exclusive { get; set; }
    public decimal Igst { get; set; }
    public decimal Cgst { get; set; }
    public decimal Sgst { get; set; }
    public decimal RoundedOff { get; set; }
    public decimal NetAmount { get; set; }
    public string Narration { get; set; } = string.Empty;
    public string Unique { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public List<PurchaseExpenseLine> Lines { get; set; } = [];

    public string DisplayText => $"{VouNo} | {AccountName} | {BillDate:dd/MM/yyyy}";

    public override string ToString() => DisplayText;
}

public class PurchaseExpenseLine : INotifyPropertyChanged
{
    private int _autoNo;
    private string _vouNo = string.Empty;
    private string _slNo = string.Empty;
    private string _itemName = string.Empty;
    private string _hsn = string.Empty;
    private decimal _qty;
    private decimal _rate;
    private decimal _rateAmount;
    private decimal _grossAmount;
    private decimal _taxRate;
    private decimal _taxAmount;

    public event PropertyChangedEventHandler? PropertyChanged;

    public int AutoNo
    {
        get => _autoNo;
        set => SetField(ref _autoNo, value);
    }

    public string VouNo
    {
        get => _vouNo;
        set => SetField(ref _vouNo, value);
    }

    public string SlNo
    {
        get => _slNo;
        set => SetField(ref _slNo, value);
    }

    public string ItemName
    {
        get => _itemName;
        set => SetField(ref _itemName, value);
    }

    public string Hsn
    {
        get => _hsn;
        set => SetField(ref _hsn, value);
    }

    public decimal Qty
    {
        get => _qty;
        set => SetField(ref _qty, value);
    }

    public decimal Rate
    {
        get => _rate;
        set => SetField(ref _rate, value);
    }

    public decimal RateAmount
    {
        get => _rateAmount;
        set => SetField(ref _rateAmount, value);
    }

    public decimal GrossAmount
    {
        get => _grossAmount;
        set => SetField(ref _grossAmount, value);
    }

    public decimal TaxRate
    {
        get => _taxRate;
        set => SetField(ref _taxRate, value);
    }

    public decimal TaxAmount
    {
        get => _taxAmount;
        set => SetField(ref _taxAmount, value);
    }

    public PurchaseExpenseLine Clone() => new()
    {
        AutoNo = AutoNo,
        VouNo = VouNo,
        SlNo = SlNo,
        ItemName = ItemName,
        Hsn = Hsn,
        Qty = Qty,
        Rate = Rate,
        RateAmount = RateAmount,
        GrossAmount = GrossAmount,
        TaxRate = TaxRate,
        TaxAmount = TaxAmount
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
