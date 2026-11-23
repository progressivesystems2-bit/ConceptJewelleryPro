using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.WPF.Controls;

namespace ConceptJewelleryPro.WPF.ViewModels;

public enum KarigharFormMode
{
    View,
    Add,
    Modify
}

public sealed class KarigharIssueViewModel : ViewModelBase, IConfirmCloseDocument
{
    private readonly IKarigharRepository _repository;
    private readonly IAccountRepository _accountRepository;
    private readonly IPurityRepository _purityRepository;
    private readonly string _currentUser;

    private KarigharFormMode _formMode = KarigharFormMode.View;
    private MetalIssuedItem? _selectedItem;
    private bool _isBusy;
    private bool _suppressSync;
    private int _selectedTabIndex;
    private string _searchText = string.Empty;

    // Form Fields
    private string _issueNo = string.Empty;
    private int _nos;
    private DateTime _issueDate = DateTime.Today;
    private string _artisianName = string.Empty;
    private string _itemType = string.Empty;
    private decimal _weightIssued;
    private string _purity = string.Empty;
    private string _note = string.Empty;

    private string? _toastMessage;
    private ToastType _toastType = ToastType.Info;

    public KarigharIssueViewModel(
        IKarigharRepository repository,
        IAccountRepository accountRepository,
        IPurityRepository purityRepository,
        string currentUser = "admin")
    {
        _repository = repository;
        _accountRepository = accountRepository;
        _purityRepository = purityRepository;
        _currentUser = currentUser;

        NewCommand = new AsyncRelayCommand(NewIssueAsync, () => CanNew);
        SaveCommand = new AsyncRelayCommand(SaveIssueAsync, () => CanSave);
        ModifyCommand = new RelayCommand(ModifyIssue, () => CanModify);
        DeleteCommand = new AsyncRelayCommand(DeleteIssueAsync, () => CanDelete);
        CancelCommand = new RelayCommand(CancelIssue, () => CanCancel);
        RefreshCommand = new AsyncRelayCommand(LoadDataAsync);

        _ = InitializeAsync();
    }

    public string Title => "Karighar Issue";

    public event EventHandler? RequestFocusFirstField;

    public ObservableCollection<Account> Artisans { get; } = [];
    public ObservableCollection<string> ItemTypes { get; } = [];
    public ObservableCollection<PurityMaster> Purities { get; } = [];
    public ObservableCollection<MetalIssuedItem> IssuedList { get; } = [];
    public ObservableCollection<MetalIssuedItem> FilteredIssuedList { get; } = [];

    public AsyncRelayCommand NewCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public RelayCommand ModifyCommand { get; }
    public AsyncRelayCommand DeleteCommand { get; }
    public RelayCommand CancelCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetProperty(ref _selectedTabIndex, value);
    }

    public KarigharFormMode FormMode
    {
        get => _formMode;
        set
        {
            if (SetProperty(ref _formMode, value))
            {
                OnPropertyChanged(nameof(IsFormEnabled));
                OnPropertyChanged(nameof(CanNew));
                OnPropertyChanged(nameof(CanSave));
                OnPropertyChanged(nameof(CanModify));
                OnPropertyChanged(nameof(CanDelete));
                OnPropertyChanged(nameof(CanCancel));
                RefreshCommands();
            }
        }
    }

    public bool IsFormEnabled => FormMode != KarigharFormMode.View;
    public bool CanNew => FormMode == KarigharFormMode.View && !IsBusy;
    public bool CanSave => FormMode != KarigharFormMode.View && !IsBusy;
    public bool CanModify => FormMode == KarigharFormMode.View && !string.IsNullOrWhiteSpace(IssueNo) && !IsBusy;
    public bool CanDelete => FormMode == KarigharFormMode.View && !string.IsNullOrWhiteSpace(IssueNo) && !IsBusy;
    public bool CanCancel => FormMode != KarigharFormMode.View && !IsBusy;

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CanNew));
                OnPropertyChanged(nameof(CanSave));
                OnPropertyChanged(nameof(CanModify));
                OnPropertyChanged(nameof(CanDelete));
                OnPropertyChanged(nameof(CanCancel));
                RefreshCommands();
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilter();
            }
        }
    }

    public MetalIssuedItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetProperty(ref _selectedItem, value))
            {
                OnPropertyChanged(nameof(CanModify));
                OnPropertyChanged(nameof(CanDelete));
                RefreshCommands();

                if (!_suppressSync && value is not null)
                {
                    PopulateFromItem(value);
                    FormMode = KarigharFormMode.View;
                }
            }
        }
    }

    #region Form Properties

    public string IssueNo
    {
        get => _issueNo;
        set => SetProperty(ref _issueNo, value);
    }

    public int Nos
    {
        get => _nos;
        set => SetProperty(ref _nos, value);
    }

    public DateTime IssueDate
    {
        get => _issueDate;
        set => SetProperty(ref _issueDate, value);
    }

    public string ArtisianName
    {
        get => _artisianName;
        set => SetProperty(ref _artisianName, value);
    }

    public string ItemType
    {
        get => _itemType;
        set => SetProperty(ref _itemType, value);
    }

    public decimal WeightIssued
    {
        get => _weightIssued;
        set => SetProperty(ref _weightIssued, value);
    }

    public string Purity
    {
        get => _purity;
        set => SetProperty(ref _purity, value);
    }

    public string Note
    {
        get => _note;
        set => SetProperty(ref _note, value);
    }

    public string? ToastMessage
    {
        get => _toastMessage;
        set => SetProperty(ref _toastMessage, value);
    }

    public ToastType ToastType
    {
        get => _toastType;
        set => SetProperty(ref _toastType, value);
    }

    #endregion

    public async Task InitializeAsync()
    {
        try
        {
            IsBusy = true;

            var accountsTask = _accountRepository.GetAllAsync();
            var puritiesTask = _purityRepository.GetAllAsync();
            var itemTypesTask = _repository.GetItemTypesAsync();
            var issuedListTask = _repository.GetAllMetalIssuedAsync(100);

            await Task.WhenAll(accountsTask, puritiesTask, itemTypesTask, issuedListTask);

            var accounts = await accountsTask;
            Artisans.Clear();
            var sortedAccounts = accounts
                .Where(acc => !string.IsNullOrWhiteSpace(acc.AccountName))
                .OrderBy(acc => acc.AccountName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var acc in sortedAccounts)
            {
                var cat = acc.HeadCategory?.Trim() ?? string.Empty;
                var inOut = acc.InOut?.Trim() ?? string.Empty;

                if (cat.Contains("Creditor", StringComparison.OrdinalIgnoreCase) ||
                    cat.Contains("Artis", StringComparison.OrdinalIgnoreCase) ||
                    cat.Contains("Karighar", StringComparison.OrdinalIgnoreCase) ||
                    cat.Contains("Debtor", StringComparison.OrdinalIgnoreCase) ||
                    cat.Contains("Job", StringComparison.OrdinalIgnoreCase) ||
                    inOut.StartsWith("Out", StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(cat))
                {
                    Artisans.Add(acc);
                }
            }

            // Fallback: If no accounts matched, add all non-system accounts so the dropdown is never empty
            if (Artisans.Count == 0)
            {
                foreach (var acc in sortedAccounts)
                {
                    var cat = acc.HeadCategory?.Trim() ?? string.Empty;
                    if (!cat.Contains("Bank", StringComparison.OrdinalIgnoreCase) &&
                        !cat.Contains("Cash", StringComparison.OrdinalIgnoreCase) &&
                        !cat.Contains("Tax", StringComparison.OrdinalIgnoreCase) &&
                        !cat.Contains("Capital", StringComparison.OrdinalIgnoreCase))
                    {
                        Artisans.Add(acc);
                    }
                }
            }

            var purities = await puritiesTask;
            Purities.Clear();
            foreach (var p in purities)
            {
                Purities.Add(p);
            }
            if (Purities.Count == 0)
            {
                Purities.Add(new PurityMaster { Purity = "22K" });
                Purities.Add(new PurityMaster { Purity = "916HM" });
                Purities.Add(new PurityMaster { Purity = "18K" });
                Purities.Add(new PurityMaster { Purity = "750HM" });
                Purities.Add(new PurityMaster { Purity = "925 Silver" });
                Purities.Add(new PurityMaster { Purity = "Silver" });
            }

            var itemTypes = await itemTypesTask;
            ItemTypes.Clear();
            foreach (var it in itemTypes)
            {
                ItemTypes.Add(it);
            }

            var issued = await issuedListTask;
            _suppressSync = true;
            IssuedList.Clear();
            foreach (var item in issued)
            {
                IssuedList.Add(item);
            }
            _suppressSync = false;

            ApplyFilter();

            if (IssuedList.Count > 0)
            {
                SelectedItem = IssuedList.First();
            }
            else
            {
                PrepareBlankForm();
            }
        }
        catch (Exception ex)
        {
            ShowToast($"Initialization error: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task LoadDataAsync()
    {
        try
        {
            var issued = await _repository.GetAllMetalIssuedAsync(100);
            _suppressSync = true;
            IssuedList.Clear();
            foreach (var item in issued)
            {
                IssuedList.Add(item);
            }
            _suppressSync = false;
            ApplyFilter();
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to load data: {ex.Message}", ToastType.Error);
        }
    }

    private async Task NewIssueAsync()
    {
        try
        {
            IsBusy = true;
            var nextIssueNo = await _repository.GetNextIssueNoAsync();
            var nextNos = await _repository.GetNextIssueNosAsync();

            PrepareBlankForm();
            IssueNo = nextIssueNo;
            Nos = nextNos;
            IssueDate = DateTime.Today;
            SelectedTabIndex = 0;

            _suppressSync = true;
            SelectedItem = null;
            _suppressSync = false;

            FormMode = KarigharFormMode.Add;
            ShowToast($"Ready to enter Issue: {IssueNo}", ToastType.Info);
            RequestFocusFirstField?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ShowToast($"Error preparing new issue: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SelectIssueByVouNoAsync(string vouNo)
    {
        if (string.IsNullOrWhiteSpace(vouNo)) return;
        if (IssuedList.Count == 0)
        {
            await LoadDataAsync();
        }
        var found = IssuedList.FirstOrDefault(x => string.Equals(x.IssueNo, vouNo, StringComparison.OrdinalIgnoreCase));
        if (found != null)
        {
            SelectedItem = found;
        }
    }

    private void ModifyIssue()
    {
        if (string.IsNullOrWhiteSpace(IssueNo))
        {
            ShowToast("Select an issue record to modify.", ToastType.Warning);
            return;
        }

        FormMode = KarigharFormMode.Modify;
        SelectedTabIndex = 0;
        ShowToast($"Editing Issue #{IssueNo}", ToastType.Info);
        RequestFocusFirstField?.Invoke(this, EventArgs.Empty);
    }

    private async Task SaveIssueAsync()
    {
        if (string.IsNullOrWhiteSpace(ArtisianName))
        {
            ShowToast("Please select or enter an Artisian Name.", ToastType.Warning);
            return;
        }

        if (WeightIssued <= 0)
        {
            ShowToast("Please enter a valid Weight Issued.", ToastType.Warning);
            return;
        }

        var result = MessageBox.Show(
            $"Do you want to save Metal Issue #{IssueNo}?",
            "Karighar Issue",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.Yes);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            IsBusy = true;
            var isUpdate = FormMode == KarigharFormMode.Modify;

            var item = new MetalIssuedItem
            {
                IssueNo = IssueNo.Trim(),
                Nos = Nos,
                IssueDate = IssueDate,
                ArtisianName = ArtisianName.Trim(),
                ItemType = ItemType?.Trim() ?? string.Empty,
                WeightIssued = WeightIssued,
                Purity = Purity?.Trim() ?? string.Empty,
                Note = Note?.Trim() ?? string.Empty,
                EntryUser = string.IsNullOrWhiteSpace(_currentUser) ? "admin" : _currentUser,
                EntryDate = DateTime.Today,
                EntryTime = DateTime.Now,
                ComputerName = Environment.MachineName,
                Status = "Saved"
            };

            await _repository.SaveMetalIssuedAsync(item, isUpdate);
            ShowToast($"Metal Issue #{item.IssueNo} saved successfully!", ToastType.Success);

            await LoadDataAsync();

            _suppressSync = true;
            SelectedItem = IssuedList.FirstOrDefault(x => x.IssueNo == item.IssueNo);
            _suppressSync = false;

            FormMode = KarigharFormMode.View;
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to save: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DeleteIssueAsync()
    {
        if (string.IsNullOrWhiteSpace(IssueNo))
        {
            ShowToast("Select an issue record to delete.", ToastType.Warning);
            return;
        }

        var result = MessageBox.Show(
            $"Are you sure you want to delete Metal Issue #{IssueNo}?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            IsBusy = true;
            await _repository.DeleteMetalIssuedAsync(IssueNo);
            ShowToast($"Issue #{IssueNo} deleted.", ToastType.Success);

            await LoadDataAsync();
            PrepareBlankForm();
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to delete: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void CancelIssue()
    {
        if (SelectedItem is not null)
        {
            PopulateFromItem(SelectedItem);
        }
        else
        {
            PrepareBlankForm();
        }

        FormMode = KarigharFormMode.View;
        ShowToast("Cancelled.", ToastType.Info);
    }

    private void PopulateFromItem(MetalIssuedItem item)
    {
        IssueNo = item.IssueNo;
        Nos = item.Nos;
        IssueDate = item.IssueDate;
        ArtisianName = item.ArtisianName;
        ItemType = item.ItemType;
        WeightIssued = item.WeightIssued;
        Purity = item.Purity;
        Note = item.Note;
    }

    private void PrepareBlankForm()
    {
        IssueNo = string.Empty;
        Nos = 0;
        IssueDate = DateTime.Today;
        ArtisianName = string.Empty;
        ItemType = ItemTypes.FirstOrDefault() ?? string.Empty;
        WeightIssued = 0m;
        Purity = string.Empty;
        Note = string.Empty;
        FormMode = KarigharFormMode.View;
    }

    private void ApplyFilter()
    {
        FilteredIssuedList.Clear();
        var query = SearchText?.Trim();

        foreach (var item in IssuedList)
        {
            if (string.IsNullOrWhiteSpace(query) ||
                item.IssueNo.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.ArtisianName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.ItemType.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.Purity.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.Note.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                FilteredIssuedList.Add(item);
            }
        }
    }

    private void RefreshCommands()
    {
        NewCommand.RaiseCanExecuteChanged();
        SaveCommand.RaiseCanExecuteChanged();
        DeleteCommand.RaiseCanExecuteChanged();
        CommandManager.InvalidateRequerySuggested();
    }

    private void ShowToast(string message, ToastType type = ToastType.Info)
    {
        ToastMessage = message;
        ToastType = type;
    }

    public bool HasUnsavedChanges()
    {
        return FormMode != KarigharFormMode.View ||
               !string.IsNullOrWhiteSpace(ArtisianName) ||
               !string.IsNullOrWhiteSpace(ItemType) ||
               WeightIssued > 0;
    }
}
