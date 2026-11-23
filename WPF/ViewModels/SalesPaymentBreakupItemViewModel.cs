namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class PaymentBreakupSummaryItem : ViewModelBase
{
    private string _paymentCategory = string.Empty;
    private string _accountName = string.Empty;
    private int _transactionCount;
    private decimal _weight;
    private decimal _amount;
    private decimal _percentage;
    private string _categoryColor = "#3B82F6";

    public string PaymentCategory
    {
        get => _paymentCategory;
        set => SetProperty(ref _paymentCategory, value);
    }

    public string AccountName
    {
        get => _accountName;
        set => SetProperty(ref _accountName, value);
    }

    public int TransactionCount
    {
        get => _transactionCount;
        set => SetProperty(ref _transactionCount, value);
    }

    public decimal Weight
    {
        get => _weight;
        set => SetProperty(ref _weight, value);
    }

    public decimal Amount
    {
        get => _amount;
        set => SetProperty(ref _amount, value);
    }

    public decimal Percentage
    {
        get => _percentage;
        set => SetProperty(ref _percentage, value);
    }

    public string CategoryColor
    {
        get => _categoryColor;
        set => SetProperty(ref _categoryColor, value);
    }
}

public sealed class InvoicePaymentBreakupItem : ViewModelBase
{
    private string _vouNo = string.Empty;
    private DateTime _billDate = DateTime.Today;
    private string _vouType = string.Empty;
    private string _customerName = string.Empty;
    private string _phone = string.Empty;
    private decimal _netAmt;
    private decimal _cashAmt;
    private decimal _paytmAmt;
    private decimal _oldSilverAmt;
    private decimal _oldSilverWt;
    private decimal _creditAmt;
    private decimal _otherAmt;
    private decimal _totalSettled;
    private string _paymentSummary = string.Empty;

    public string VouNo
    {
        get => _vouNo;
        set => SetProperty(ref _vouNo, value);
    }

    public DateTime BillDate
    {
        get => _billDate;
        set => SetProperty(ref _billDate, value);
    }

    public string VouType
    {
        get => _vouType;
        set => SetProperty(ref _vouType, value);
    }

    public string CustomerName
    {
        get => _customerName;
        set => SetProperty(ref _customerName, value);
    }

    public string Phone
    {
        get => _phone;
        set => SetProperty(ref _phone, value);
    }

    public decimal NetAmt
    {
        get => _netAmt;
        set => SetProperty(ref _netAmt, value);
    }

    public decimal CashAmt
    {
        get => _cashAmt;
        set => SetProperty(ref _cashAmt, value);
    }

    public decimal PaytmAmt
    {
        get => _paytmAmt;
        set => SetProperty(ref _paytmAmt, value);
    }

    public decimal OldSilverAmt
    {
        get => _oldSilverAmt;
        set => SetProperty(ref _oldSilverAmt, value);
    }

    public decimal OldSilverWt
    {
        get => _oldSilverWt;
        set => SetProperty(ref _oldSilverWt, value);
    }

    public decimal CreditAmt
    {
        get => _creditAmt;
        set => SetProperty(ref _creditAmt, value);
    }

    public decimal OtherAmt
    {
        get => _otherAmt;
        set => SetProperty(ref _otherAmt, value);
    }

    public decimal TotalSettled
    {
        get => _totalSettled;
        set => SetProperty(ref _totalSettled, value);
    }

    public decimal Balance => NetAmt - TotalSettled;

    public string PaymentSummary
    {
        get => _paymentSummary;
        set => SetProperty(ref _paymentSummary, value);
    }
}
