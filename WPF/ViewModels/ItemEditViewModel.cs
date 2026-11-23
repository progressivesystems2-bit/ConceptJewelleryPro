using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.Shared;
using ConceptJewelleryPro.WPF.Controls;

namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class ItemEditViewModel : ViewModelBase
{
    private readonly IItemRepository _itemRepository;
    private readonly string _currentUser;
    private readonly Item? _originalItem;

    private string _itemCode = string.Empty;
    private string _itemName = string.Empty;
    private string _group = "Gold";
    private string _subGroup = string.Empty;
    private string _headGroup = string.Empty;
    private string _unit = "PCS";
    private string _metalType = "Gold";
    private decimal _purity = 22.0m;
    private string _purityText = "22.00";
    private decimal _standardWeight;
    private decimal _minWeight;
    private decimal _maxWeight;
    private decimal _mrp;
    private decimal _saleRate;
    private decimal _purchaseRate;
    private decimal _openingStock;
    private decimal _reorderLevel;
    private string _hsnCode = "7113";
    private decimal _gstRate = 3.0m;
    private decimal _tax = 3.0m;
    private decimal _disper;
    private string _description = string.Empty;
    private bool _isActive = true;
    private bool _allowed = true;
    private string _status = "Saved";

    private bool _isBusy;
    private string? _errorMessage;
    private string? _statusMessage;
    private string? _toastMessage;
    private ToastType _toastType = ToastType.Info;

    public ItemEditViewModel(
        IItemRepository itemRepository,
        string currentUser,
        Item? itemToEdit = null,
        string? suggestedItemCode = null,
        IEnumerable<string>? existingGroups = null,
        IEnumerable<string>? existingSubGroups = null,
        IEnumerable<string>? existingPurities = null)
    {
        _itemRepository = itemRepository;
        _currentUser = currentUser;
        _originalItem = itemToEdit;

        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsBusy);
        CancelCommand = new RelayCommand(Cancel);

        var defaultGroups = new[] { "Gold", "Silver", "Diamond", "Platinum", "Other" };
        var groupList = new List<string>(defaultGroups);
        if (existingGroups != null)
        {
            foreach (var g in existingGroups)
            {
                if (!string.IsNullOrWhiteSpace(g) && !groupList.Contains(g, StringComparer.OrdinalIgnoreCase))
                {
                    groupList.Add(g.Trim());
                }
            }
        }
        Groups = groupList;

        var defaultSubGroups = new[] { "Ring", "Chain", "Bangle", "Earring", "Necklace", "Pendant", "Bracelet", "Anklet", "Coin", "Bar", "Other" };
        var subGroupList = new List<string>(defaultSubGroups);
        if (existingSubGroups != null)
        {
            foreach (var sg in existingSubGroups)
            {
                if (!string.IsNullOrWhiteSpace(sg) && !subGroupList.Contains(sg, StringComparer.OrdinalIgnoreCase))
                {
                    subGroupList.Add(sg.Trim());
                }
            }
        }
        SubGroups = subGroupList;

        var defaultPurities = new[] { "916HM", "925 Silver", "Silver", "22K", "18K", "750HM", "995", "18ct" };
        var purityList = new List<string>(defaultPurities);
        if (existingPurities != null)
        {
            foreach (var p in existingPurities)
            {
                if (!string.IsNullOrWhiteSpace(p) && !purityList.Contains(p, StringComparer.OrdinalIgnoreCase))
                {
                    purityList.Add(p.Trim());
                }
            }
        }
        Purities = purityList;

        if (_originalItem != null)
        {
            Title = $"Modify Item - {_originalItem.ItemName}";
            PopulateFromItem(_originalItem);
            Status = "Modify";
        }
        else
        {
            Title = "Create New Item";
            Status = "Saved";
            if (!string.IsNullOrWhiteSpace(suggestedItemCode))
            {
                ItemCode = suggestedItemCode.Trim();
            }
        }
    }

    public string Title { get; }
    public bool IsCodeReadOnly => _originalItem != null;

    public IReadOnlyList<string> Groups { get; }
    public IReadOnlyList<string> SubGroups { get; }
    public IReadOnlyList<string> Purities { get; }
    public IReadOnlyList<string> Units { get; } = new[] { "PCS", "GMS", "GRAM", "KG", "PAIR", "SET", "CTS" };
    public IReadOnlyList<string> MetalTypes { get; } = new[] { "Gold", "Silver", "Platinum", "Diamond", "Gemstone", "Other" };
    public IReadOnlyList<string> StatusOptions { get; } = new[] { "Saved", "Modify" };

    public string ItemCode
    {
        get => _itemCode;
        set
        {
            if (SetProperty(ref _itemCode, value))
            {
                ClearError();
            }
        }
    }

    public string ItemName
    {
        get => _itemName;
        set
        {
            if (SetProperty(ref _itemName, value))
            {
                ClearError();
            }
        }
    }

    public string Group
    {
        get => _group;
        set
        {
            if (SetProperty(ref _group, value))
            {
                if (string.IsNullOrWhiteSpace(_headGroup) || _headGroup == _group)
                {
                    HeadGroup = value;
                }
                if (string.IsNullOrWhiteSpace(_metalType) || _metalType == _group)
                {
                    MetalType = value;
                }
                if (!string.IsNullOrWhiteSpace(value) && value.Contains("SILVER", StringComparison.OrdinalIgnoreCase) && (_hsnCode == "7113" || string.IsNullOrWhiteSpace(_hsnCode)))
                {
                    HSNCode = "7114";
                }
            }
        }
    }

    public string SubGroup
    {
        get => _subGroup;
        set => SetProperty(ref _subGroup, value);
    }

    public string HeadGroup
    {
        get => _headGroup;
        set => SetProperty(ref _headGroup, value);
    }

    public string Unit
    {
        get => _unit;
        set => SetProperty(ref _unit, value);
    }

    public string MetalType
    {
        get => _metalType;
        set
        {
            if (SetProperty(ref _metalType, value))
            {
                if (!string.IsNullOrWhiteSpace(value) && value.Contains("SILVER", StringComparison.OrdinalIgnoreCase) && (_hsnCode == "7113" || string.IsNullOrWhiteSpace(_hsnCode)))
                {
                    HSNCode = "7114";
                }
            }
        }
    }

    public string PurityText
    {
        get => _purityText;
        set
        {
            if (SetProperty(ref _purityText, value))
            {
                ClearError();
                if (decimal.TryParse(value, out var d))
                {
                    _purity = d;
                    OnPropertyChanged(nameof(Purity));
                }
                else
                {
                    var digits = new string(value?.Where(c => char.IsDigit(c) || c == '.').ToArray() ?? Array.Empty<char>());
                    if (decimal.TryParse(digits, out var parsed))
                    {
                        _purity = parsed;
                        OnPropertyChanged(nameof(Purity));
                    }
                }
            }
        }
    }

    public decimal Purity
    {
        get => _purity;
        set
        {
            if (SetProperty(ref _purity, value))
            {
                if (string.IsNullOrWhiteSpace(_purityText) || decimal.TryParse(_purityText, out _))
                {
                    _purityText = value > 0 ? value.ToString("0.##") : string.Empty;
                    OnPropertyChanged(nameof(PurityText));
                }
            }
        }
    }

    public decimal StandardWeight
    {
        get => _standardWeight;
        set => SetProperty(ref _standardWeight, value);
    }

    public decimal MinWeight
    {
        get => _minWeight;
        set => SetProperty(ref _minWeight, value);
    }

    public decimal MaxWeight
    {
        get => _maxWeight;
        set => SetProperty(ref _maxWeight, value);
    }

    public decimal MRP
    {
        get => _mrp;
        set => SetProperty(ref _mrp, value);
    }

    public decimal SaleRate
    {
        get => _saleRate;
        set => SetProperty(ref _saleRate, value);
    }

    public decimal PurchaseRate
    {
        get => _purchaseRate;
        set => SetProperty(ref _purchaseRate, value);
    }

    public decimal OpeningStock
    {
        get => _openingStock;
        set => SetProperty(ref _openingStock, value);
    }

    public decimal ReorderLevel
    {
        get => _reorderLevel;
        set => SetProperty(ref _reorderLevel, value);
    }

    public string HSNCode
    {
        get => _hsnCode;
        set => SetProperty(ref _hsnCode, value);
    }

    public decimal GSTRate
    {
        get => _gstRate;
        set
        {
            if (SetProperty(ref _gstRate, value))
            {
                Tax = value;
            }
        }
    }

    public decimal Tax
    {
        get => _tax;
        set => SetProperty(ref _tax, value);
    }

    public decimal Disper
    {
        get => _disper;
        set => SetProperty(ref _disper, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    public bool Allowed
    {
        get => _allowed;
        set => SetProperty(ref _allowed, value);
    }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                SaveCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
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

    public AsyncRelayCommand SaveCommand { get; }
    public RelayCommand CancelCommand { get; }

    public event EventHandler<bool>? RequestClose;

    private void PopulateFromItem(Item item)
    {
        ItemCode = item.ItemCode;
        ItemName = item.ItemName;
        Group = !string.IsNullOrWhiteSpace(item.Group) ? item.Group : item.HeadGroup;
        SubGroup = item.SubGroup;
        HeadGroup = !string.IsNullOrWhiteSpace(item.HeadGroup) ? item.HeadGroup : item.Group;
        Unit = item.Unit;
        MetalType = !string.IsNullOrWhiteSpace(item.MetalType) ? item.MetalType : HeadGroup;
        Purity = item.Purity;
        PurityText = !string.IsNullOrWhiteSpace(item.RawPurity)
            ? item.RawPurity
            : (item.Purity > 0 ? item.Purity.ToString("0.##") : string.Empty);
        StandardWeight = item.StandardWeight;
        MinWeight = item.MinWeight;
        MaxWeight = item.MaxWeight;
        MRP = item.MRP;
        SaleRate = item.SaleRate;
        PurchaseRate = item.PurchaseRate;
        OpeningStock = item.OpeningStock;
        ReorderLevel = item.ReorderLevel;
        HSNCode = !string.IsNullOrWhiteSpace(item.HSNCode) ? item.HSNCode : "7113";
        GSTRate = item.Tax > 0 ? item.Tax : (item.GSTRate > 0 ? item.GSTRate : 3.0m);
        Tax = GSTRate;
        Disper = item.Disper;
        Description = item.Description;
        IsActive = item.IsActive;
        Allowed = item.Allowed;
        Status = !string.IsNullOrWhiteSpace(item.Status) ? item.Status : "Saved";
    }

    private async Task SaveAsync()
    {
        ErrorMessage = null;
        StatusMessage = null;

        if (string.IsNullOrWhiteSpace(ItemCode))
        {
            ErrorMessage = "Item Code is required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(ItemName))
        {
            ErrorMessage = "Item Name is required.";
            return;
        }

        var isNew = _originalItem == null;
        var trimmedCode = ItemCode.Trim();
        var trimmedName = ItemName.Trim();

        IsBusy = true;
        try
        {
            if (isNew)
            {
                var existingWithCode = await _itemRepository.FindByCodeAsync(trimmedCode);
                if (existingWithCode != null)
                {
                    ErrorMessage = $"Item Code '{trimmedCode}' is already in use by '{existingWithCode.ItemName}'. Please enter a unique Item Code.";
                    return;
                }
            }

            var allItems = await _itemRepository.GetAllAsync();
            var duplicateName = allItems.FirstOrDefault(i =>
                string.Equals(i.ItemName?.Trim(), trimmedName, StringComparison.OrdinalIgnoreCase) &&
                (isNew || !string.Equals(i.ItemCode, _originalItem!.ItemCode, StringComparison.OrdinalIgnoreCase)));

            if (duplicateName != null)
            {
                ErrorMessage = $"An item with the name '{trimmedName}' already exists (Item Code: {duplicateName.ItemCode}). Item names must be unique.";
                return;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to validate item: " + ex.Message;
            return;
        }
        finally
        {
            IsBusy = false;
        }

        var result = System.Windows.MessageBox.Show(
            "Do you want to Save?",
            "Confirmation",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question,
            System.Windows.MessageBoxResult.Yes);

        if (result != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var item = _originalItem ?? new Item();

            item.ItemCode = trimmedCode;
            item.ItemName = trimmedName;
            item.Group = !string.IsNullOrWhiteSpace(Group) ? Group : HeadGroup;
            item.SubGroup = SubGroup;
            item.HeadGroup = !string.IsNullOrWhiteSpace(HeadGroup) ? HeadGroup : Group;
            item.Unit = Unit;
            item.MetalType = !string.IsNullOrWhiteSpace(MetalType) ? MetalType : item.HeadGroup;
            item.Purity = Purity;
            item.RawPurity = !string.IsNullOrWhiteSpace(PurityText)
                ? PurityText.Trim()
                : (Purity > 0 ? Purity.ToString("0.##") : string.Empty);
            item.StandardWeight = StandardWeight;
            item.MinWeight = MinWeight;
            item.MaxWeight = MaxWeight;
            item.MRP = MRP;
            item.SaleRate = SaleRate;
            item.PurchaseRate = PurchaseRate;
            item.OpeningStock = OpeningStock;
            item.ReorderLevel = ReorderLevel;
            item.HSNCode = HSNCode;
            item.GSTRate = GSTRate;
            item.Tax = GSTRate;
            item.Disper = Disper;
            item.Description = Description;
            item.IsActive = IsActive;
            item.Allowed = Allowed;
            item.Status = Status;
            item.Saved = "Yes";
            item.User = _currentUser;
            item.Date = DateTime.Today;
            item.Time = DateTime.Now.TimeOfDay;

            if (isNew)
            {
                await _itemRepository.AddAsync(item);
            }
            else
            {
                await _itemRepository.UpdateAsync(item);
            }

            RequestClose?.Invoke(this, true);
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to save item: " + ex.Message;
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

    private void ClearError()
    {
        ErrorMessage = null;
    }
}
