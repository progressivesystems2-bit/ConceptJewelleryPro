using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ConceptJewelleryPro.Domain.Entities;

public class CardAdjustmentReceipt : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private int _nos;
    private string _receiptNo = string.Empty;
    private DateTime _receiptDate = DateTime.Today;
    private string _branch = "HO";
    private string _cardAccount = string.Empty;
    private string _bankAccount = string.Empty;
    private string _receivableAccount = string.Empty;
    private string _commissionAccount = string.Empty;
    private decimal _commissionPercent;
    private DateTime _fromDate = DateTime.Today.AddDays(-30);
    private DateTime _toDate = DateTime.Today;
    private decimal _totalCardAmount;
    private decimal _totalCommissionAmount;
    private decimal _totalReceivedAmount;
    private decimal _taxableAmount;
    private decimal _sgstTaxAmount;
    private decimal _cgstTaxAmount;
    private decimal _igstTaxAmount;
    private decimal _rofAmount;
    private string _invoiceType = "GST";
    private string _taxType = "-- None --";
    private bool _taxManual;
    private string _remarks = string.Empty;
    private string _vouNo = string.Empty;
    private string _status = "Active";
    private string _createdBy = string.Empty;
    private DateTime _createdDate = DateTime.Now;

    public int Nos { get => _nos; set => SetProperty(ref _nos, value); }
    public string ReceiptNo { get => _receiptNo; set => SetProperty(ref _receiptNo, value); }
    public DateTime ReceiptDate { get => _receiptDate; set => SetProperty(ref _receiptDate, value); }
    public string Branch { get => _branch; set => SetProperty(ref _branch, value); }
    public string CardAccount { get => _cardAccount; set => SetProperty(ref _cardAccount, value); }
    public string BankAccount { get => _bankAccount; set => SetProperty(ref _bankAccount, value); }
    public string ReceivableAccount { get => _receivableAccount; set => SetProperty(ref _receivableAccount, value); }
    public string CommissionAccount { get => _commissionAccount; set => SetProperty(ref _commissionAccount, value); }
    public decimal CommissionPercent { get => _commissionPercent; set => SetProperty(ref _commissionPercent, value); }
    public DateTime FromDate { get => _fromDate; set => SetProperty(ref _fromDate, value); }
    public DateTime ToDate { get => _toDate; set => SetProperty(ref _toDate, value); }
    public decimal TotalCardAmount { get => _totalCardAmount; set => SetProperty(ref _totalCardAmount, value); }
    public decimal TotalCommissionAmount { get => _totalCommissionAmount; set => SetProperty(ref _totalCommissionAmount, value); }
    public decimal TotalReceivedAmount { get => _totalReceivedAmount; set => SetProperty(ref _totalReceivedAmount, value); }
    public decimal TaxableAmount { get => _taxableAmount; set => SetProperty(ref _taxableAmount, value); }
    public decimal SgstTaxAmount { get => _sgstTaxAmount; set => SetProperty(ref _sgstTaxAmount, value); }
    public decimal CgstTaxAmount { get => _cgstTaxAmount; set => SetProperty(ref _cgstTaxAmount, value); }
    public decimal IgstTaxAmount { get => _igstTaxAmount; set => SetProperty(ref _igstTaxAmount, value); }
    public decimal RofAmount { get => _rofAmount; set => SetProperty(ref _rofAmount, value); }
    public string InvoiceType { get => _invoiceType; set => SetProperty(ref _invoiceType, value); }
    public string TaxType { get => _taxType; set => SetProperty(ref _taxType, value); }
    public bool TaxManual { get => _taxManual; set => SetProperty(ref _taxManual, value); }
    public string Remarks { get => _remarks; set => SetProperty(ref _remarks, value); }
    public string VouNo { get => _vouNo; set => SetProperty(ref _vouNo, value); }
    public string Status { get => _status; set => SetProperty(ref _status, value); }
    public string CreatedBy { get => _createdBy; set => SetProperty(ref _createdBy, value); }
    public DateTime CreatedDate { get => _createdDate; set => SetProperty(ref _createdDate, value); }

    public List<CardAdjustmentReceiptItem> Items { get; set; } = [];
}

public class CardAdjustmentReceiptItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private long _id;
    private int _srNo;
    private string _receiptNo = string.Empty;
    private string _dbName = "SALE";
    private string _billNo = string.Empty;
    private DateTime _billDate = DateTime.Today;
    private string _accountName = string.Empty;
    private string _cardNo = string.Empty;
    private string _cardBank = string.Empty;
    private decimal _cardAmount;
    private bool _isSelected = true;
    private decimal _commissionAmount;
    private decimal _commissionPercent;
    private decimal _receivedAmount;
    private bool _isUpdating;

    public long Id { get => _id; set => SetProperty(ref _id, value); }
    public int SrNo { get => _srNo; set => SetProperty(ref _srNo, value); }
    public string ReceiptNo { get => _receiptNo; set => SetProperty(ref _receiptNo, value); }
    public string DbName { get => _dbName; set => SetProperty(ref _dbName, value); }
    public string BillNo { get => _billNo; set => SetProperty(ref _billNo, value); }
    public DateTime BillDate { get => _billDate; set => SetProperty(ref _billDate, value); }
    public string AccountName { get => _accountName; set => SetProperty(ref _accountName, value); }
    public string CardNo { get => _cardNo; set => SetProperty(ref _cardNo, value); }
    public string CardBank { get => _cardBank; set => SetProperty(ref _cardBank, value); }
    public decimal CardAmount
    {
        get => _cardAmount;
        set
        {
            if (SetProperty(ref _cardAmount, value))
            {
                RecalculateNet();
            }
        }
    }
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }

    public decimal CommissionAmount
    {
        get => _commissionAmount;
        set
        {
            if (SetProperty(ref _commissionAmount, value))
            {
                if (_isUpdating) return;
                _isUpdating = true;
                try
                {
                    _receivedAmount = Math.Max(0m, CardAmount - _commissionAmount);
                    OnPropertyChanged(nameof(ReceivedAmount));
                    _commissionPercent = CardAmount > 0 ? Math.Round((_commissionAmount / CardAmount) * 100m, 4) : 0m;
                    OnPropertyChanged(nameof(CommissionPercent));
                }
                finally
                {
                    _isUpdating = false;
                }
            }
        }
    }

    public decimal CommissionPercent
    {
        get => _commissionPercent;
        set
        {
            if (SetProperty(ref _commissionPercent, value))
            {
                if (_isUpdating) return;
                _isUpdating = true;
                try
                {
                    _commissionAmount = Math.Round(CardAmount * (_commissionPercent / 100m), 2);
                    OnPropertyChanged(nameof(CommissionAmount));
                    _receivedAmount = Math.Max(0m, CardAmount - _commissionAmount);
                    OnPropertyChanged(nameof(ReceivedAmount));
                }
                finally
                {
                    _isUpdating = false;
                }
            }
        }
    }

    public decimal ReceivedAmount
    {
        get => _receivedAmount;
        set
        {
            if (SetProperty(ref _receivedAmount, value))
            {
                if (_isUpdating) return;
                _isUpdating = true;
                try
                {
                    _commissionAmount = Math.Max(0m, CardAmount - _receivedAmount);
                    OnPropertyChanged(nameof(CommissionAmount));
                    _commissionPercent = CardAmount > 0 ? Math.Round((_commissionAmount / CardAmount) * 100m, 4) : 0m;
                    OnPropertyChanged(nameof(CommissionPercent));
                }
                finally
                {
                    _isUpdating = false;
                }
            }
        }
    }

    public void RecalculateNet()
    {
        if (_isUpdating) return;
        _isUpdating = true;
        try
        {
            _commissionAmount = Math.Round(CardAmount * (CommissionPercent / 100m), 2);
            OnPropertyChanged(nameof(CommissionAmount));
            _receivedAmount = Math.Max(0m, CardAmount - _commissionAmount);
            OnPropertyChanged(nameof(ReceivedAmount));
        }
        finally
        {
            _isUpdating = false;
        }
    }
}
