using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.WPF.Controls;

namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class SalesVouTypeEditViewModel : ViewModelBase
{
    private readonly ISalesVouTypeRepository _salesVouTypeRepository;
    private readonly IAccountRepository? _accountRepository;
    private readonly IItemRepository? _itemRepository;
    private readonly IPurityRepository? _purityRepository;
    private readonly string _currentUser;
    private readonly string? _originalVouType;

    private string _vouType = string.Empty;
    private string _shortName = string.Empty;
    private string _headCategory = "Sales";
    private string _salesAccount = string.Empty;
    private string _vatAccount = string.Empty;
    private decimal _vatPercentage;
    private decimal _cardCommission;
    private string _cardBank = string.Empty;
    private string _commAc = string.Empty;
    private string _itemHead = string.Empty;
    private string _printFile = "BillPrint";
    private string _itemPurchased = string.Empty;
    private string _itemConvertedTo = string.Empty;
    private string _counterNo = string.Empty;
    private string _status = "Active";
    private string _note = string.Empty;
    private bool _hide;

    private bool _isBusy;
    private string? _toastMessage;
    private ToastType _toastType = ToastType.Info;
    private string _errorMessage = string.Empty;

    public event EventHandler<bool?>? RequestClose;

    public SalesVouTypeEditViewModel(
        ISalesVouTypeRepository salesVouTypeRepository,
        IAccountRepository? accountRepository = null,
        IItemRepository? itemRepository = null,
        IPurityRepository? purityRepository = null,
        SalesVouType? existing = null,
        string currentUser = "admin")
    {
        _salesVouTypeRepository = salesVouTypeRepository;
        _accountRepository = accountRepository;
        _itemRepository = itemRepository;
        _purityRepository = purityRepository;
        _currentUser = currentUser;

        AvailableHeadCategories = new ObservableCollection<string>();
        AvailableAccounts = new ObservableCollection<string>();
        AvailableCardBanks = new ObservableCollection<string>();
        AvailableCommAccounts = new ObservableCollection<string>();
        AvailableItemHeads = new ObservableCollection<string>();
        AvailableItemTypes = new ObservableCollection<string>();
        AvailablePrintFiles = new ObservableCollection<string>();

        if (existing is not null)
        {
            _originalVouType = existing.VouType;
            _vouType = existing.VouType;
            _shortName = existing.ShortName;
            _headCategory = existing.HeadCategory;
            _salesAccount = existing.SalesAccount;
            _vatAccount = existing.VatAccount;
            _vatPercentage = existing.VatPercentage;
            _cardCommission = existing.CardCommission;
            _cardBank = existing.CardBank;
            _commAc = existing.CommAc;
            _itemHead = existing.ItemHead;
            _printFile = existing.PrintFile;
            _itemPurchased = existing.ItemPurchased;
            _itemConvertedTo = existing.ItemConvertedTo;
            _counterNo = existing.CounterNo;
            _status = string.IsNullOrWhiteSpace(existing.Status) ? "Active" : existing.Status;
            _note = existing.Note;
            _hide = existing.Hide;

            Title = $"Modify Voucher Type - {existing.VouType}";
            IsNew = false;
        }
        else
        {
            Title = "Add New Voucher Type";
            IsNew = true;
            _headCategory = "Sales";
            _status = "Active";
            _printFile = "BillPrint";
            _vatPercentage = 3.00m;
        }

        SaveCommand = new RelayCommand(async () => await SaveAsync());
        CancelCommand = new RelayCommand(Cancel);

        _ = LoadLookupsAsync();
    }

    public string Title { get; }

    public bool IsNew { get; }

    public ObservableCollection<string> AvailableHeadCategories { get; }

    public ObservableCollection<string> AvailableAccounts { get; }

    public ObservableCollection<string> AvailableCardBanks { get; }

    public ObservableCollection<string> AvailableCommAccounts { get; }

    public ObservableCollection<string> AvailableItemHeads { get; }

    public ObservableCollection<string> AvailableItemTypes { get; }

    public ObservableCollection<string> AvailablePrintFiles { get; }

    public ICommand SaveCommand { get; }

    public ICommand CancelCommand { get; }

    public string VouType
    {
        get => _vouType;
        set => SetProperty(ref _vouType, value);
    }

    public string ShortName
    {
        get => _shortName;
        set => SetProperty(ref _shortName, value);
    }

    public string HeadCategory
    {
        get => _headCategory;
        set => SetProperty(ref _headCategory, value);
    }

    public string SalesAccount
    {
        get => _salesAccount;
        set => SetProperty(ref _salesAccount, value);
    }

    public string VatAccount
    {
        get => _vatAccount;
        set => SetProperty(ref _vatAccount, value);
    }

    public decimal VatPercentage
    {
        get => _vatPercentage;
        set => SetProperty(ref _vatPercentage, value);
    }

    public decimal CardCommission
    {
        get => _cardCommission;
        set => SetProperty(ref _cardCommission, value);
    }

    public string CardBank
    {
        get => _cardBank;
        set => SetProperty(ref _cardBank, value);
    }

    public string CommAc
    {
        get => _commAc;
        set => SetProperty(ref _commAc, value);
    }

    public string ItemHead
    {
        get => _itemHead;
        set => SetProperty(ref _itemHead, value);
    }

    public string PrintFile
    {
        get => _printFile;
        set => SetProperty(ref _printFile, value);
    }

    public string ItemPurchased
    {
        get => _itemPurchased;
        set => SetProperty(ref _itemPurchased, value);
    }

    public string ItemConvertedTo
    {
        get => _itemConvertedTo;
        set => SetProperty(ref _itemConvertedTo, value);
    }

    public string CounterNo
    {
        get => _counterNo;
        set => SetProperty(ref _counterNo, value);
    }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public string Note
    {
        get => _note;
        set => SetProperty(ref _note, value);
    }

    public bool Hide
    {
        get => _hide;
        set => SetProperty(ref _hide, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public string? ToastMessage
    {
        get => _toastMessage;
        private set => SetProperty(ref _toastMessage, value);
    }

    public ToastType ToastType
    {
        get => _toastType;
        private set => SetProperty(ref _toastType, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value) && !string.IsNullOrWhiteSpace(value))
            {
                ToastType = ToastType.Error;
                ToastMessage = string.Empty;
                ToastMessage = value;
            }
        }
    }

    private async Task LoadLookupsAsync()
    {
        try
        {
            // 1. Categories
            var cats = await _salesVouTypeRepository.GetHeadCategoriesAsync();
            AvailableHeadCategories.Clear();
            foreach (var c in cats) AvailableHeadCategories.Add(c);

            // 2. Print files
            var prints = await _salesVouTypeRepository.GetPrintFilesAsync();
            AvailablePrintFiles.Clear();
            foreach (var p in prints) AvailablePrintFiles.Add(p);

            // 3. Accounts
            if (_accountRepository is not null)
            {
                var allAccs = await _accountRepository.GetAllAsync();
                AvailableAccounts.Clear();
                AvailableCardBanks.Clear();
                AvailableCommAccounts.Clear();

                foreach (var acc in allAccs.OrderBy(a => a.AccountName))
                {
                    if (!string.IsNullOrWhiteSpace(acc.AccountName))
                    {
                        AvailableAccounts.Add(acc.AccountName);

                        if (acc.HeadCategory.Contains("Bank", StringComparison.OrdinalIgnoreCase) ||
                            acc.AccountName.Contains("Bank", StringComparison.OrdinalIgnoreCase) ||
                            acc.AccountName.Contains("Card", StringComparison.OrdinalIgnoreCase))
                        {
                            AvailableCardBanks.Add(acc.AccountName);
                        }

                        if (acc.HeadCategory.Contains("Exp", StringComparison.OrdinalIgnoreCase) ||
                            acc.HeadCategory.Contains("Indirect", StringComparison.OrdinalIgnoreCase) ||
                            acc.AccountName.Contains("Comm", StringComparison.OrdinalIgnoreCase) ||
                            acc.AccountName.Contains("Charge", StringComparison.OrdinalIgnoreCase))
                        {
                            AvailableCommAccounts.Add(acc.AccountName);
                        }
                    }
                }
            }

            // Fallback accounts if none loaded
            if (AvailableAccounts.Count == 0)
            {
                var defaults = new[] { "SALE GOLD", "SALE SILVER", "DIAMOND PURCHASE", "OLD GOLD PURCHASES", "GST", "GST APPROVAL", "SALES APPROVAL", "BANK CHARGES", "CASH A/C" };
                foreach (var d in defaults) AvailableAccounts.Add(d);
            }
            if (AvailableCardBanks.Count == 0)
            {
                var defaultBanks = new[] { "AXIS BANK CREDIT CARD", "HDFC BANK", "SBI BANK", "ICICI BANK" };
                foreach (var b in defaultBanks) AvailableCardBanks.Add(b);
            }
            if (AvailableCommAccounts.Count == 0)
            {
                var defaultComms = new[] { "BANK CHARGES", "COMMISSION EXPENSES", "DISCOUNT A/C" };
                foreach (var c in defaultComms) AvailableCommAccounts.Add(c);
            }

            // 4. Item Heads
            if (_purityRepository is not null)
            {
                var heads = await _purityRepository.GetItemHeadsAsync();
                AvailableItemHeads.Clear();
                foreach (var h in heads) AvailableItemHeads.Add(h);
            }

            if (AvailableItemHeads.Count == 0)
            {
                var defaultHeads = new[] { "GOLD", "SILVER", "DIAMOND", "DECORATIVES", "PLATINUM", "18ct C.Z" };
                foreach (var dh in defaultHeads) AvailableItemHeads.Add(dh);
            }

            // 5. Item Types
            if (_itemRepository is not null)
            {
                var types = await _itemRepository.GetItemTypesAsync();
                AvailableItemTypes.Clear();
                foreach (var t in types) AvailableItemTypes.Add(t);

                if (AvailableItemTypes.Count == 0)
                {
                    var items = await _itemRepository.GetAllAsync();
                    foreach (var itm in items.Select(i => i.ItemName).Distinct().OrderBy(n => n))
                    {
                        if (!string.IsNullOrWhiteSpace(itm)) AvailableItemTypes.Add(itm);
                    }
                }
            }

            // Ensure current values exist in lists
            EnsurePresent(AvailableHeadCategories, HeadCategory);
            EnsurePresent(AvailableAccounts, SalesAccount);
            EnsurePresent(AvailableAccounts, VatAccount);
            EnsurePresent(AvailableCardBanks, CardBank);
            EnsurePresent(AvailableCommAccounts, CommAc);
            EnsurePresent(AvailableItemHeads, ItemHead);
            EnsurePresent(AvailablePrintFiles, PrintFile);
            EnsurePresent(AvailableItemTypes, ItemPurchased);
            EnsurePresent(AvailableItemTypes, ItemConvertedTo);
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to load lookups: " + ex.Message;
        }
    }

    private static void EnsurePresent(ObservableCollection<string> collection, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) && !collection.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            collection.Insert(0, value);
        }
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(VouType))
        {
            ErrorMessage = "Voucher Type Name is required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(ShortName))
        {
            ErrorMessage = "Short Code / Prefix is required.";
            return;
        }

        var result = MessageBox.Show(
            "Do you want to Save this Voucher Type configuration?",
            "Confirm Save",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.Yes);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;

            var entity = new SalesVouType
            {
                VouType = VouType.Trim(),
                ShortName = ShortName.Trim().ToUpperInvariant(),
                HeadCategory = HeadCategory?.Trim() ?? "Sales",
                SalesAccount = SalesAccount?.Trim() ?? string.Empty,
                VatAccount = VatAccount?.Trim() ?? string.Empty,
                VatPercentage = VatPercentage,
                CardCommission = CardCommission,
                CardBank = CardBank?.Trim() ?? string.Empty,
                CommAc = CommAc?.Trim() ?? string.Empty,
                ItemHead = ItemHead?.Trim() ?? string.Empty,
                PrintFile = PrintFile?.Trim() ?? "BillPrint",
                ItemPurchased = ItemPurchased?.Trim() ?? string.Empty,
                ItemConvertedTo = ItemConvertedTo?.Trim() ?? string.Empty,
                CounterNo = CounterNo?.Trim() ?? string.Empty,
                Status = Status?.Trim() ?? "Active",
                Note = Note?.Trim() ?? string.Empty,
                Hide = Hide
            };

            if (IsNew || string.IsNullOrWhiteSpace(_originalVouType))
            {
                var existing = await _salesVouTypeRepository.GetByVouTypeAsync(entity.VouType);
                if (existing is not null)
                {
                    ErrorMessage = $"Voucher Type '{entity.VouType}' already exists.";
                    return;
                }

                await _salesVouTypeRepository.AddAsync(entity);
            }
            else
            {
                await _salesVouTypeRepository.UpdateAsync(_originalVouType, entity);
            }

            RequestClose?.Invoke(this, true);
        }
        catch (Exception ex)
        {
            ErrorMessage = "Error saving Voucher Type: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Cancel()
    {
        RequestClose?.Invoke(this, false);
    }
}
