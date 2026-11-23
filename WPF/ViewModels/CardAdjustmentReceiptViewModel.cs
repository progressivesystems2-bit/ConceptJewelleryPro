using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using ConceptJewelleryPro.Application.Authentication;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.WPF.Views;
using Microsoft.Win32;

namespace ConceptJewelleryPro.WPF.ViewModels;

public class CardAdjustmentReceiptViewModel : ViewModelBase
{
    private readonly ICardAdjustmentReceiptRepository _receiptRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly string _userName;
    private readonly Func<CardSetupViewModel>? _cardSetupViewModelFactory;
    private bool _isUpdatingSelectAll;

    private int _nos;
    private string _receiptNo = string.Empty;
    private DateTime _receiptDate = DateTime.Today;
    private string _branch = "HO";
    private string _selectedCardAccount = string.Empty;
    private decimal _globalCommissionPercent;
    private DateTime _fromDate = DateTime.Today.AddDays(-30);
    private DateTime _toDate = DateTime.Today;

    private Account? _selectedBankAccount;
    private Account? _selectedReceivableAccount;
    private Account? _selectedCommissionAccount;
    private string _remarks = string.Empty;

    private decimal _totalCardAmount;
    private decimal _totalCommissionAmount;
    private decimal _totalReceivedAmount;
    private decimal _totalAccountReceivedAmount;
    private decimal _taxableAmount;
    private decimal _rofAmount;

    private string _invoiceType = "GST";
    private string _taxType = "-- None --";
    private bool _taxManual;
    private decimal _sgstTaxAmount;
    private decimal _cgstTaxAmount;
    private decimal _igstTaxAmount;

    private bool _isSelectAll = true;
    private bool _isExistingReceipt;
    private string _statusMessage = "Ready. Select a Card Account and Date Range to pull transactions.";
    private CardAdjustmentReceiptItem? _selectedItem;
    private bool _isBusy;

    public int Nos
    {
        get => _nos;
        set => SetProperty(ref _nos, value);
    }

    public string ReceiptNo
    {
        get => _receiptNo;
        set => SetProperty(ref _receiptNo, value);
    }

    public DateTime ReceiptDate
    {
        get => _receiptDate;
        set => SetProperty(ref _receiptDate, value);
    }

    public string Branch
    {
        get => _branch;
        set => SetProperty(ref _branch, value);
    }

    public string SelectedCardAccount
    {
        get => _selectedCardAccount;
        set
        {
            if (SetProperty(ref _selectedCardAccount, value))
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    SelectedReceivableAccount = ReceivableAccounts.FirstOrDefault(a =>
                        string.Equals(a.AccountName, value, StringComparison.OrdinalIgnoreCase));
                }
            }
        }
    }

    public decimal GlobalCommissionPercent
    {
        get => _globalCommissionPercent;
        set => SetProperty(ref _globalCommissionPercent, value);
    }

    public DateTime FromDate
    {
        get => _fromDate;
        set => SetProperty(ref _fromDate, value);
    }

    public DateTime ToDate
    {
        get => _toDate;
        set => SetProperty(ref _toDate, value);
    }

    public Account? SelectedBankAccount
    {
        get => _selectedBankAccount;
        set => SetProperty(ref _selectedBankAccount, value);
    }

    public Account? SelectedReceivableAccount
    {
        get => _selectedReceivableAccount;
        set => SetProperty(ref _selectedReceivableAccount, value);
    }

    public Account? SelectedCommissionAccount
    {
        get => _selectedCommissionAccount;
        set => SetProperty(ref _selectedCommissionAccount, value);
    }

    public string Remarks
    {
        get => _remarks;
        set => SetProperty(ref _remarks, value);
    }

    public decimal TotalCardAmount
    {
        get => _totalCardAmount;
        set => SetProperty(ref _totalCardAmount, value);
    }

    public decimal TotalCommissionAmount
    {
        get => _totalCommissionAmount;
        set
        {
            if (SetProperty(ref _totalCommissionAmount, value))
            {
                if (!_isCalculatingTotals)
                {
                    ApplyTotalCommissionAmount(value);
                }
            }
        }
    }

    public decimal TotalReceivedAmount
    {
        get => _totalReceivedAmount;
        set => SetProperty(ref _totalReceivedAmount, value);
    }

    public decimal TotalAccountReceivedAmount
    {
        get => _totalAccountReceivedAmount;
        set
        {
            if (SetProperty(ref _totalAccountReceivedAmount, value))
            {
                if (!_isCalculatingTotals)
                {
                    ApplyTotalReceivedAmount(value);
                }
            }
        }
    }

    public decimal TaxableAmount
    {
        get => _taxableAmount;
        set => SetProperty(ref _taxableAmount, value);
    }

    public decimal RofAmount
    {
        get => _rofAmount;
        set => SetProperty(ref _rofAmount, value);
    }

    public string InvoiceType
    {
        get => _invoiceType;
        set => SetProperty(ref _invoiceType, value);
    }

    public string TaxType
    {
        get => _taxType;
        set
        {
            if (SetProperty(ref _taxType, value))
            {
                RecalculateTax();
            }
        }
    }

    public bool TaxManual
    {
        get => _taxManual;
        set
        {
            if (SetProperty(ref _taxManual, value))
            {
                if (!value) RecalculateTax();
            }
        }
    }

    public decimal SgstTaxAmount
    {
        get => _sgstTaxAmount;
        set => SetProperty(ref _sgstTaxAmount, value);
    }

    public decimal CgstTaxAmount
    {
        get => _cgstTaxAmount;
        set => SetProperty(ref _cgstTaxAmount, value);
    }

    public decimal IgstTaxAmount
    {
        get => _igstTaxAmount;
        set => SetProperty(ref _igstTaxAmount, value);
    }

    public bool IsSelectAll
    {
        get => _isSelectAll;
        set
        {
            if (SetProperty(ref _isSelectAll, value))
            {
                if (_isUpdatingSelectAll) return;
                _isUpdatingSelectAll = true;
                try
                {
                    foreach (var item in Items)
                    {
                        item.IsSelected = value;
                    }
                    RecalculateTotals();
                }
                finally
                {
                    _isUpdatingSelectAll = false;
                }
            }
        }
    }

    public bool IsExistingReceipt
    {
        get => _isExistingReceipt;
        set => SetProperty(ref _isExistingReceipt, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public CardAdjustmentReceiptItem? SelectedItem
    {
        get => _selectedItem;
        set => SetProperty(ref _selectedItem, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public string Title { get; set; } = "Card Adjustment Receipt";

    public ObservableCollection<string> CardAccounts { get; } = [];
    public ObservableCollection<Account> BankAccounts { get; } = [];
    public ObservableCollection<Account> ReceivableAccounts { get; } = [];
    public ObservableCollection<Account> CommissionAccounts { get; } = [];
    public ObservableCollection<CardAdjustmentReceiptItem> Items { get; } = [];

    public IReadOnlyList<string> InvoiceTypeOptions { get; } = ["GST", "Non-GST", "SEZ", "Export"];
    public IReadOnlyList<string> TaxTypeOptions { get; } = ["-- None --", "18% GST (9% CGST + 9% SGST)", "18% IGST", "12% GST", "5% GST"];

    public string CurrentDayTime => $"{ReceiptDate:dddd} {DateTime.Now:hh:mm tt}";
    public int ItemCount => Items.Count;
    public int SelectedCount => Items.Count(i => i.IsSelected);

    public AsyncRelayCommand NewReceiptCommand { get; }
    public AsyncRelayCommand SaveReceiptCommand { get; }
    public AsyncRelayCommand DeleteReceiptCommand { get; }
    public AsyncRelayCommand FetchTransactionsCommand { get; }
    public RelayCommand ApplyGlobalCommissionCommand { get; }
    public AsyncRelayCommand NavigateFirstCommand { get; }
    public AsyncRelayCommand NavigatePreviousCommand { get; }
    public AsyncRelayCommand NavigateNextCommand { get; }
    public AsyncRelayCommand NavigateLastCommand { get; }
    public RelayCommand ExportToExcelCommand { get; }
    public RelayCommand PrintReceiptCommand { get; }
    public RelayCommand OpenCardSetupCommand { get; }

    public CardAdjustmentReceiptViewModel(
        ICardAdjustmentReceiptRepository receiptRepository,
        IAccountRepository accountRepository,
        string userName = "Admin",
        Func<CardSetupViewModel>? cardSetupViewModelFactory = null)
    {
        _receiptRepository = receiptRepository;
        _accountRepository = accountRepository;
        _userName = userName;
        _cardSetupViewModelFactory = cardSetupViewModelFactory;

        NewReceiptCommand = new AsyncRelayCommand(NewReceiptAsync);
        SaveReceiptCommand = new AsyncRelayCommand(SaveReceiptAsync);
        DeleteReceiptCommand = new AsyncRelayCommand(DeleteReceiptAsync);
        FetchTransactionsCommand = new AsyncRelayCommand(FetchTransactionsAsync);
        ApplyGlobalCommissionCommand = new RelayCommand(ApplyGlobalCommission);
        NavigateFirstCommand = new AsyncRelayCommand(NavigateFirstAsync);
        NavigatePreviousCommand = new AsyncRelayCommand(NavigatePreviousAsync);
        NavigateNextCommand = new AsyncRelayCommand(NavigateNextAsync);
        NavigateLastCommand = new AsyncRelayCommand(NavigateLastAsync);
        ExportToExcelCommand = new RelayCommand(ExportToExcel);
        PrintReceiptCommand = new RelayCommand(PrintReceipt);
        OpenCardSetupCommand = new RelayCommand(OpenCardSetup);
    }

    public async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
            await LoadAccountsAsync();
            await NewReceiptAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadAccountsAsync()
    {
        CardAccounts.Clear();
        BankAccounts.Clear();
        ReceivableAccounts.Clear();
        CommissionAccounts.Clear();

        var cards = await _receiptRepository.GetCardBanksAsync();
        foreach (var c in cards)
        {
            CardAccounts.Add(c);
        }

        var allAccounts = await _accountRepository.GetAllAsync();
        foreach (var acc in allAccounts)
        {
            ReceivableAccounts.Add(acc);

            var group = acc.HeadCategory ?? string.Empty;
            if (group.Contains("Bank", StringComparison.OrdinalIgnoreCase))
            {
                BankAccounts.Add(acc);
            }

            if (group.Contains("Expense", StringComparison.OrdinalIgnoreCase) ||
                acc.AccountName.Contains("Charges", StringComparison.OrdinalIgnoreCase) ||
                acc.AccountName.Contains("Comm", StringComparison.OrdinalIgnoreCase))
            {
                CommissionAccounts.Add(acc);
            }
        }

        if (string.IsNullOrWhiteSpace(SelectedCardAccount) && CardAccounts.Count > 0)
        {
            SelectedCardAccount = CardAccounts[0];
        }

        if (SelectedBankAccount is null && BankAccounts.Count > 0)
        {
            SelectedBankAccount = BankAccounts.FirstOrDefault(b =>
                !b.AccountName.Contains("Card", StringComparison.OrdinalIgnoreCase) &&
                !b.AccountName.Contains("UPI", StringComparison.OrdinalIgnoreCase)) ?? BankAccounts[0];
        }

        if (SelectedCommissionAccount is null && CommissionAccounts.Count > 0)
        {
            SelectedCommissionAccount = CommissionAccounts.FirstOrDefault(c =>
                c.AccountName.Contains("Credit Card Charges", StringComparison.OrdinalIgnoreCase) ||
                c.AccountName.Contains("Bank Charges", StringComparison.OrdinalIgnoreCase)) ?? CommissionAccounts[0];
        }
    }

    public async Task FetchTransactionsAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedCardAccount))
        {
            StatusMessage = "Please select a Card Account first.";
            return;
        }

        IsBusy = true;
        StatusMessage = $"Fetching transactions for {SelectedCardAccount}...";
        try
        {
            Items.Clear();
            var rows = await _receiptRepository.GetPendingCardTransactionsAsync(SelectedCardAccount, FromDate, ToDate);

            foreach (var row in rows)
            {
                row.CommissionPercent = GlobalCommissionPercent;
                row.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName is nameof(CardAdjustmentReceiptItem.IsSelected) or
                        nameof(CardAdjustmentReceiptItem.CommissionAmount) or
                        nameof(CardAdjustmentReceiptItem.CommissionPercent) or
                        nameof(CardAdjustmentReceiptItem.ReceivedAmount) or
                        nameof(CardAdjustmentReceiptItem.CardAmount))
                    {
                        RecalculateTotals();
                    }
                };
                Items.Add(row);
            }

            IsSelectAll = true;
            RecalculateTotals();
            StatusMessage = $"Found {Items.Count} card transactions for {SelectedCardAccount}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error fetching transactions: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void ApplyGlobalCommission()
    {
        foreach (var item in Items)
        {
            item.CommissionPercent = GlobalCommissionPercent;
        }
        RecalculateTotals();
        StatusMessage = $"Applied {GlobalCommissionPercent:F2}% commission to all items.";
    }

    private bool _isCalculatingTotals;

    public void RecalculateTotals()
    {
        if (_isCalculatingTotals) return;
        _isCalculatingTotals = true;
        try
        {
            var selected = Items.Where(i => i.IsSelected).ToList();
            TotalCardAmount = selected.Sum(i => i.CardAmount);
            TotalCommissionAmount = selected.Sum(i => i.CommissionAmount);
            TotalReceivedAmount = selected.Sum(i => i.ReceivedAmount);
            _totalAccountReceivedAmount = TotalReceivedAmount;
            OnPropertyChanged(nameof(TotalAccountReceivedAmount));

            RecalculateTax();
            OnPropertyChanged(nameof(ItemCount));
            OnPropertyChanged(nameof(SelectedCount));
        }
        finally
        {
            _isCalculatingTotals = false;
        }
    }

    public void ApplyTotalReceivedAmount(decimal totalReceived)
    {
        if (_isCalculatingTotals) return;
        var selected = Items.Where(i => i.IsSelected).ToList();
        if (selected.Count == 0 || TotalCardAmount <= 0)
        {
            TotalReceivedAmount = totalReceived;
            TotalCommissionAmount = Math.Max(0m, TotalCardAmount - totalReceived);
            RecalculateTax();
            return;
        }

        _isCalculatingTotals = true;
        try
        {
            var totalComm = Math.Max(0m, TotalCardAmount - totalReceived);
            TotalCommissionAmount = totalComm;
            TotalReceivedAmount = totalReceived;
            _totalAccountReceivedAmount = totalReceived;
            OnPropertyChanged(nameof(TotalAccountReceivedAmount));

            if (selected.Count == 1)
            {
                selected[0].ReceivedAmount = totalReceived;
                selected[0].CommissionAmount = totalComm;
                selected[0].CommissionPercent = selected[0].CardAmount > 0
                    ? Math.Round((totalComm / selected[0].CardAmount) * 100m, 4)
                    : 0m;
            }
            else
            {
                decimal allocatedRec = 0m;
                for (int i = 0; i < selected.Count; i++)
                {
                    var item = selected[i];
                    if (i == selected.Count - 1)
                    {
                        item.ReceivedAmount = Math.Max(0m, totalReceived - allocatedRec);
                    }
                    else
                    {
                        var ratio = item.CardAmount / TotalCardAmount;
                        var itemRec = Math.Round(totalReceived * ratio, 2);
                        item.ReceivedAmount = itemRec;
                        allocatedRec += itemRec;
                    }
                    item.CommissionAmount = Math.Max(0m, item.CardAmount - item.ReceivedAmount);
                    item.CommissionPercent = item.CardAmount > 0
                        ? Math.Round((item.CommissionAmount / item.CardAmount) * 100m, 4)
                        : 0m;
                }
            }

            RecalculateTax();
            OnPropertyChanged(nameof(ItemCount));
            OnPropertyChanged(nameof(SelectedCount));
        }
        finally
        {
            _isCalculatingTotals = false;
        }
    }

    public void ApplyTotalCommissionAmount(decimal totalCommission)
    {
        if (_isCalculatingTotals) return;
        var selected = Items.Where(i => i.IsSelected).ToList();
        if (selected.Count == 0 || TotalCardAmount <= 0)
        {
            TotalReceivedAmount = Math.Max(0m, TotalCardAmount - totalCommission);
            _totalAccountReceivedAmount = TotalReceivedAmount;
            OnPropertyChanged(nameof(TotalAccountReceivedAmount));
            RecalculateTax();
            return;
        }

        _isCalculatingTotals = true;
        try
        {
            TotalCommissionAmount = totalCommission;
            var netReceived = Math.Max(0m, TotalCardAmount - totalCommission);
            TotalReceivedAmount = netReceived;
            _totalAccountReceivedAmount = netReceived;
            OnPropertyChanged(nameof(TotalAccountReceivedAmount));

            if (selected.Count == 1)
            {
                selected[0].CommissionAmount = totalCommission;
                selected[0].ReceivedAmount = netReceived;
                selected[0].CommissionPercent = selected[0].CardAmount > 0
                    ? Math.Round((totalCommission / selected[0].CardAmount) * 100m, 4)
                    : 0m;
            }
            else
            {
                decimal allocatedComm = 0m;
                for (int i = 0; i < selected.Count; i++)
                {
                    var item = selected[i];
                    if (i == selected.Count - 1)
                    {
                        item.CommissionAmount = Math.Max(0m, totalCommission - allocatedComm);
                    }
                    else
                    {
                        var ratio = item.CardAmount / TotalCardAmount;
                        var itemComm = Math.Round(totalCommission * ratio, 2);
                        item.CommissionAmount = itemComm;
                        allocatedComm += itemComm;
                    }
                    item.ReceivedAmount = Math.Max(0m, item.CardAmount - item.CommissionAmount);
                    item.CommissionPercent = item.CardAmount > 0
                        ? Math.Round((item.CommissionAmount / item.CardAmount) * 100m, 4)
                        : 0m;
                }
            }

            RecalculateTax();
            OnPropertyChanged(nameof(ItemCount));
            OnPropertyChanged(nameof(SelectedCount));
        }
        finally
        {
            _isCalculatingTotals = false;
        }
    }

    private void RecalculateTax()
    {
        if (TaxManual) return;

        TaxableAmount = TotalCommissionAmount;
        if (TaxType.Contains("18% GST", StringComparison.OrdinalIgnoreCase))
        {
            var totalTax = Math.Round(TaxableAmount * 0.18m, 2);
            CgstTaxAmount = Math.Round(totalTax / 2m, 2);
            SgstTaxAmount = totalTax - CgstTaxAmount;
            IgstTaxAmount = 0m;
        }
        else if (TaxType.Contains("18% IGST", StringComparison.OrdinalIgnoreCase))
        {
            IgstTaxAmount = Math.Round(TaxableAmount * 0.18m, 2);
            CgstTaxAmount = 0m;
            SgstTaxAmount = 0m;
        }
        else if (TaxType.Contains("12%", StringComparison.OrdinalIgnoreCase))
        {
            var totalTax = Math.Round(TaxableAmount * 0.12m, 2);
            CgstTaxAmount = Math.Round(totalTax / 2m, 2);
            SgstTaxAmount = totalTax - CgstTaxAmount;
            IgstTaxAmount = 0m;
        }
        else if (TaxType.Contains("5%", StringComparison.OrdinalIgnoreCase))
        {
            var totalTax = Math.Round(TaxableAmount * 0.05m, 2);
            CgstTaxAmount = Math.Round(totalTax / 2m, 2);
            SgstTaxAmount = totalTax - CgstTaxAmount;
            IgstTaxAmount = 0m;
        }
        else
        {
            CgstTaxAmount = 0m;
            SgstTaxAmount = 0m;
            IgstTaxAmount = 0m;
        }

        var totalNet = TotalReceivedAmount;
        var rounded = Math.Round(totalNet, 0);
        RofAmount = rounded - totalNet;
    }

    public async Task NewReceiptAsync()
    {
        IsBusy = true;
        try
        {
            Items.Clear();
            ReceiptNo = await _receiptRepository.GetNextReceiptNoAsync();
            ReceiptDate = DateTime.Today;
            FromDate = DateTime.Today.AddDays(-30);
            ToDate = DateTime.Today;
            GlobalCommissionPercent = 0m;
            Remarks = string.Empty;
            IsExistingReceipt = false;
            TotalCardAmount = 0m;
            TotalCommissionAmount = 0m;
            TotalReceivedAmount = 0m;
            _totalAccountReceivedAmount = 0m;
            OnPropertyChanged(nameof(TotalAccountReceivedAmount));
            TaxableAmount = 0m;
            SgstTaxAmount = 0m;
            CgstTaxAmount = 0m;
            IgstTaxAmount = 0m;
            RofAmount = 0m;
            InvoiceType = "GST";
            TaxType = "-- None --";
            TaxManual = false;
            StatusMessage = $"New Receipt {ReceiptNo} initialized.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SaveReceiptAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedCardAccount))
        {
            MessageBox.Show("Please select a Card Account.", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (SelectedBankAccount is null)
        {
            MessageBox.Show("Please select a destination Bank Account.", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var selectedItems = Items.Where(i => i.IsSelected).ToList();
        if (selectedItems.Count == 0)
        {
            MessageBox.Show("Please select at least one transaction to adjust.", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (TotalReceivedAmount <= 0 && TotalCardAmount <= 0)
        {
            MessageBox.Show("Selected transactions have 0 amount.", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsBusy = true;
        try
        {
            var user = !string.IsNullOrWhiteSpace(_userName) ? _userName : Environment.UserName;

            var receipt = new CardAdjustmentReceipt
            {
                Nos = Nos,
                ReceiptNo = ReceiptNo.Trim(),
                ReceiptDate = ReceiptDate,
                Branch = Branch,
                CardAccount = SelectedCardAccount.Trim(),
                BankAccount = SelectedBankAccount.AccountName.Trim(),
                ReceivableAccount = SelectedReceivableAccount?.AccountName?.Trim() ?? SelectedCardAccount.Trim(),
                CommissionAccount = SelectedCommissionAccount?.AccountName?.Trim() ?? "CREDIT CARD CHARGES",
                CommissionPercent = GlobalCommissionPercent,
                FromDate = FromDate,
                ToDate = ToDate,
                TotalCardAmount = TotalCardAmount,
                TotalCommissionAmount = TotalCommissionAmount,
                TotalReceivedAmount = TotalReceivedAmount,
                TaxableAmount = TaxableAmount,
                SgstTaxAmount = SgstTaxAmount,
                CgstTaxAmount = CgstTaxAmount,
                IgstTaxAmount = IgstTaxAmount,
                RofAmount = RofAmount,
                InvoiceType = InvoiceType,
                TaxType = TaxType,
                TaxManual = TaxManual,
                Remarks = Remarks,
                Status = "Active",
                CreatedBy = user,
                Items = selectedItems
            };

            await _receiptRepository.SaveAsync(receipt, IsExistingReceipt);
            IsExistingReceipt = true;
            StatusMessage = $"Receipt {ReceiptNo} saved successfully. Cumulative voucher posted to Bank & Card ledger.";

            MessageBox.Show(
                $"Card Adjustment Receipt {ReceiptNo} saved successfully!\n\n" +
                $"Bank Ledger Debited: {SelectedBankAccount.AccountName} ({TotalReceivedAmount:C})\n" +
                (TotalCommissionAmount > 0 ? $"Commission Debited: {receipt.CommissionAccount} ({TotalCommissionAmount:C})\n" : "") +
                $"Card Ledger Credited: {SelectedCardAccount} ({TotalCardAmount:C})",
                Title,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save receipt: {ex.Message}", Title, MessageBoxButton.OK, MessageBoxImage.Error);
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task DeleteReceiptAsync()
    {
        if (!IsExistingReceipt || string.IsNullOrWhiteSpace(ReceiptNo))
        {
            StatusMessage = "Cannot delete a receipt that has not been saved.";
            return;
        }

        var confirm = MessageBox.Show(
            $"Are you sure you want to delete Card Adjustment Receipt {ReceiptNo}?\n" +
            "This will revert the adjusted bills and delete the bank ledger voucher.",
            Title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        IsBusy = true;
        try
        {
            await _receiptRepository.DeleteAsync(ReceiptNo);
            StatusMessage = $"Receipt {ReceiptNo} deleted and ledger postings reverted.";
            await NewReceiptAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to delete: {ex.Message}", Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task LoadReceiptAsync(string receiptNo)
    {
        if (string.IsNullOrWhiteSpace(receiptNo)) return;

        IsBusy = true;
        try
        {
            var receipt = await _receiptRepository.GetByReceiptNoAsync(receiptNo);
            if (receipt is null)
            {
                StatusMessage = $"Receipt {receiptNo} not found.";
                return;
            }

            Nos = receipt.Nos;
            ReceiptNo = receipt.ReceiptNo;
            ReceiptDate = receipt.ReceiptDate;
            Branch = receipt.Branch;
            SelectedCardAccount = receipt.CardAccount;
            GlobalCommissionPercent = receipt.CommissionPercent;
            FromDate = receipt.FromDate;
            ToDate = receipt.ToDate;
            Remarks = receipt.Remarks;
            InvoiceType = receipt.InvoiceType;
            TaxType = receipt.TaxType;
            TaxManual = receipt.TaxManual;
            SgstTaxAmount = receipt.SgstTaxAmount;
            CgstTaxAmount = receipt.CgstTaxAmount;
            IgstTaxAmount = receipt.IgstTaxAmount;
            RofAmount = receipt.RofAmount;

            SelectedBankAccount = BankAccounts.FirstOrDefault(a =>
                string.Equals(a.AccountName, receipt.BankAccount, StringComparison.OrdinalIgnoreCase));
            SelectedReceivableAccount = ReceivableAccounts.FirstOrDefault(a =>
                string.Equals(a.AccountName, receipt.ReceivableAccount, StringComparison.OrdinalIgnoreCase));
            SelectedCommissionAccount = CommissionAccounts.FirstOrDefault(a =>
                string.Equals(a.AccountName, receipt.CommissionAccount, StringComparison.OrdinalIgnoreCase));

            Items.Clear();
            foreach (var item in receipt.Items)
            {
                item.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName is nameof(CardAdjustmentReceiptItem.IsSelected) or
                        nameof(CardAdjustmentReceiptItem.CommissionAmount) or
                        nameof(CardAdjustmentReceiptItem.CommissionPercent) or
                        nameof(CardAdjustmentReceiptItem.ReceivedAmount) or
                        nameof(CardAdjustmentReceiptItem.CardAmount))
                    {
                        RecalculateTotals();
                    }
                };
                Items.Add(item);
            }

            IsExistingReceipt = true;
            RecalculateTotals();
            StatusMessage = $"Loaded Receipt {ReceiptNo}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading receipt: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task NavigateFirstAsync() => await NavigateAsync(ReceiptNavigationDirection.First);
    public async Task NavigatePreviousAsync() => await NavigateAsync(ReceiptNavigationDirection.Previous);
    public async Task NavigateNextAsync() => await NavigateAsync(ReceiptNavigationDirection.Next);
    public async Task NavigateLastAsync() => await NavigateAsync(ReceiptNavigationDirection.Last);

    private async Task NavigateAsync(ReceiptNavigationDirection direction)
    {
        var targetNo = await _receiptRepository.NavigateReceiptNoAsync(ReceiptNo, direction);
        if (!string.IsNullOrWhiteSpace(targetNo) && !string.Equals(targetNo, ReceiptNo, StringComparison.OrdinalIgnoreCase))
        {
            await LoadReceiptAsync(targetNo);
        }
    }

    public void ExportToExcel()
    {
        if (Items.Count == 0)
        {
            MessageBox.Show("No transaction data to export.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var saveDialog = new SaveFileDialog
        {
            Title = "Export Card Adjustment Receipt",
            Filter = "CSV File (*.csv)|*.csv|All Files (*.*)|*.*",
            FileName = $"CardAdjustment_{ReceiptNo}_{DateTime.Now:yyyyMMdd}.csv"
        };

        if (saveDialog.ShowDialog() != true) return;

        try
        {
            using var sw = new StreamWriter(saveDialog.FileName);
            sw.WriteLine($"CARD ADJUSTMENT RECEIPT: {ReceiptNo}");
            sw.WriteLine($"Card Account: {SelectedCardAccount}, Bank Account: {SelectedBankAccount?.AccountName}");
            sw.WriteLine($"Date: {ReceiptDate:dd/MM/yyyy}, Period: {FromDate:dd/MM/yyyy} to {ToDate:dd/MM/yyyy}");
            sw.WriteLine($"Total Card Amount: {TotalCardAmount}, Total Commission: {TotalCommissionAmount}, Net Received: {TotalReceivedAmount}");
            sw.WriteLine();
            sw.WriteLine("Sr,Selected,Doc Type,Bill No,Bill Date,Account,Card No,Card Bank,Card Amt,Commission %,Commission Amt,Received Amt");

            foreach (var i in Items)
            {
                sw.WriteLine($"{i.SrNo},{(i.IsSelected ? "Yes" : "No")},{i.DbName},{i.BillNo},{i.BillDate:dd/MM/yyyy},\"{i.AccountName.Replace("\"", "\"\"")}\",\"{i.CardNo}\",\"{i.CardBank}\",{i.CardAmount:F2},{i.CommissionPercent:F2},{i.CommissionAmount:F2},{i.ReceivedAmount:F2}");
            }

            StatusMessage = $"Exported to {Path.GetFileName(saveDialog.FileName)}.";
            MessageBox.Show($"Export completed successfully to {saveDialog.FileName}", Title, MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Export failed: {ex.Message}", Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void PrintReceipt()
    {
        try
        {
            var doc = CreateReceiptFlowDocument();
            var previewWindow = new InvoicePrintPreviewWindow(doc, $"{Title} Print Preview - {ReceiptNo}", "CARD ADJUSTMENT RECEIPT", $"CardAdjustment_{ReceiptNo}")
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            previewWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Print preview error: {ex.Message}", Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private FlowDocument CreateReceiptFlowDocument()
    {
        var doc = new FlowDocument
        {
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 12,
            PagePadding = new Thickness(40),
            ColumnWidth = double.PositiveInfinity
        };

        var titlePara = new Paragraph(new Run("CARD ADJUSTMENT RECEIPT"))
        {
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 10)
        };
        doc.Blocks.Add(titlePara);

        var infoTable = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 15) };
        infoTable.Columns.Add(new TableColumn { Width = new GridLength(140) });
        infoTable.Columns.Add(new TableColumn { Width = new GridLength(220) });
        infoTable.Columns.Add(new TableColumn { Width = new GridLength(140) });
        infoTable.Columns.Add(new TableColumn { Width = new GridLength(220) });

        var infoGroup = new TableRowGroup();
        var r1 = new TableRow();
        r1.Cells.Add(new TableCell(new Paragraph(new Run("Receipt No: ") { FontWeight = FontWeights.Bold })));
        r1.Cells.Add(new TableCell(new Paragraph(new Run(ReceiptNo))));
        r1.Cells.Add(new TableCell(new Paragraph(new Run("Receipt Date: ") { FontWeight = FontWeights.Bold })));
        r1.Cells.Add(new TableCell(new Paragraph(new Run(ReceiptDate.ToString("dd/MM/yyyy")))));
        infoGroup.Rows.Add(r1);

        var r2 = new TableRow();
        r2.Cells.Add(new TableCell(new Paragraph(new Run("Card Account: ") { FontWeight = FontWeights.Bold })));
        r2.Cells.Add(new TableCell(new Paragraph(new Run(SelectedCardAccount))));
        r2.Cells.Add(new TableCell(new Paragraph(new Run("Deposit Bank: ") { FontWeight = FontWeights.Bold })));
        r2.Cells.Add(new TableCell(new Paragraph(new Run(SelectedBankAccount?.AccountName ?? ""))));
        infoGroup.Rows.Add(r2);

        var r3 = new TableRow();
        r3.Cells.Add(new TableCell(new Paragraph(new Run("Date Range: ") { FontWeight = FontWeights.Bold })));
        r3.Cells.Add(new TableCell(new Paragraph(new Run($"{FromDate:dd/MM/yyyy} to {ToDate:dd/MM/yyyy}"))));
        r3.Cells.Add(new TableCell(new Paragraph(new Run("Status: ") { FontWeight = FontWeights.Bold })));
        r3.Cells.Add(new TableCell(new Paragraph(new Run(IsExistingReceipt ? "Saved" : "New"))));
        infoGroup.Rows.Add(r3);

        infoTable.RowGroups.Add(infoGroup);
        doc.Blocks.Add(infoTable);

        // Transaction items table
        var itemTable = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 15), BorderThickness = new Thickness(1), BorderBrush = Brushes.Gray };
        itemTable.Columns.Add(new TableColumn { Width = new GridLength(40) });
        itemTable.Columns.Add(new TableColumn { Width = new GridLength(90) });
        itemTable.Columns.Add(new TableColumn { Width = new GridLength(80) });
        itemTable.Columns.Add(new TableColumn { Width = new GridLength(160) });
        itemTable.Columns.Add(new TableColumn { Width = new GridLength(90) });
        itemTable.Columns.Add(new TableColumn { Width = new GridLength(70) });
        itemTable.Columns.Add(new TableColumn { Width = new GridLength(80) });
        itemTable.Columns.Add(new TableColumn { Width = new GridLength(90) });

        var headerGroup = new TableRowGroup();
        var hRow = new TableRow { Background = Brushes.LightGray };
        hRow.Cells.Add(new TableCell(new Paragraph(new Run("Sr") { FontWeight = FontWeights.Bold })));
        hRow.Cells.Add(new TableCell(new Paragraph(new Run("Bill No") { FontWeight = FontWeights.Bold })));
        hRow.Cells.Add(new TableCell(new Paragraph(new Run("Date") { FontWeight = FontWeights.Bold })));
        hRow.Cells.Add(new TableCell(new Paragraph(new Run("Customer") { FontWeight = FontWeights.Bold })));
        hRow.Cells.Add(new TableCell(new Paragraph(new Run("Card Amt") { FontWeight = FontWeights.Bold }) { TextAlignment = TextAlignment.Right }));
        hRow.Cells.Add(new TableCell(new Paragraph(new Run("Comm %") { FontWeight = FontWeights.Bold }) { TextAlignment = TextAlignment.Right }));
        hRow.Cells.Add(new TableCell(new Paragraph(new Run("Comm Amt") { FontWeight = FontWeights.Bold }) { TextAlignment = TextAlignment.Right }));
        hRow.Cells.Add(new TableCell(new Paragraph(new Run("Received") { FontWeight = FontWeights.Bold }) { TextAlignment = TextAlignment.Right }));
        headerGroup.Rows.Add(hRow);
        itemTable.RowGroups.Add(headerGroup);

        var bodyGroup = new TableRowGroup();
        var selected = Items.Where(i => i.IsSelected).ToList();
        foreach (var item in selected)
        {
            var row = new TableRow();
            row.Cells.Add(new TableCell(new Paragraph(new Run(item.SrNo.ToString()))));
            row.Cells.Add(new TableCell(new Paragraph(new Run(item.BillNo))));
            row.Cells.Add(new TableCell(new Paragraph(new Run(item.BillDate.ToString("dd/MM/yy")))));
            row.Cells.Add(new TableCell(new Paragraph(new Run(item.AccountName))));
            row.Cells.Add(new TableCell(new Paragraph(new Run(item.CardAmount.ToString("N2"))) { TextAlignment = TextAlignment.Right }));
            row.Cells.Add(new TableCell(new Paragraph(new Run(item.CommissionPercent.ToString("F2"))) { TextAlignment = TextAlignment.Right }));
            row.Cells.Add(new TableCell(new Paragraph(new Run(item.CommissionAmount.ToString("N2"))) { TextAlignment = TextAlignment.Right }));
            row.Cells.Add(new TableCell(new Paragraph(new Run(item.ReceivedAmount.ToString("N2"))) { TextAlignment = TextAlignment.Right }));
            bodyGroup.Rows.Add(row);
        }

        // Totals Row
        var totRow = new TableRow { Background = Brushes.WhiteSmoke };
        totRow.Cells.Add(new TableCell(new Paragraph(new Run("Total") { FontWeight = FontWeights.Bold })) { ColumnSpan = 4 });
        totRow.Cells.Add(new TableCell(new Paragraph(new Run(TotalCardAmount.ToString("N2")) { FontWeight = FontWeights.Bold }) { TextAlignment = TextAlignment.Right }));
        totRow.Cells.Add(new TableCell(new Paragraph(new Run(""))));
        totRow.Cells.Add(new TableCell(new Paragraph(new Run(TotalCommissionAmount.ToString("N2")) { FontWeight = FontWeights.Bold }) { TextAlignment = TextAlignment.Right }));
        totRow.Cells.Add(new TableCell(new Paragraph(new Run(TotalReceivedAmount.ToString("N2")) { FontWeight = FontWeights.Bold }) { TextAlignment = TextAlignment.Right }));
        bodyGroup.Rows.Add(totRow);

        itemTable.RowGroups.Add(bodyGroup);
        doc.Blocks.Add(itemTable);

        // Summary footer
        var summaryPara = new Paragraph();
        summaryPara.Inlines.Add(new Run($"Tax Type: {TaxType} | Taxable Amount: {TaxableAmount:C2} | CGST: {CgstTaxAmount:C2} | SGST: {SgstTaxAmount:C2} | IGST: {IgstTaxAmount:C2} | Round Off: {RofAmount:C2}\n") { FontSize = 10 });
        if (!string.IsNullOrWhiteSpace(Remarks))
        {
            summaryPara.Inlines.Add(new Run($"Remarks: {Remarks}\n") { FontStyle = FontStyles.Italic });
        }
        doc.Blocks.Add(summaryPara);

        return doc;
    }

    public void OpenCardSetup()
    {
        if (_cardSetupViewModelFactory == null) return;

        var setupVm = _cardSetupViewModelFactory();
        var setupWindow = new CardSetupWindow(setupVm)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        setupWindow.ShowDialog();

        // Refresh card accounts after dialog closes
        _ = LoadAccountsAsync();
    }
}
