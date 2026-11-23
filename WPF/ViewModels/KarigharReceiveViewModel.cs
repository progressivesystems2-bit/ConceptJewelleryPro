using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.WPF.Controls;

namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class KarigharReceiveViewModel : ViewModelBase, IConfirmCloseDocument
{
    private readonly IKarigharRepository _repository;
    private readonly IAccountRepository _accountRepository;
    private readonly string _currentUser;

    private KarigharFormMode _formMode = KarigharFormMode.View;
    private MetalReceivedItem? _selectedItem;
    private bool _isBusy;
    private bool _suppressSync;
    private int _selectedTabIndex;
    private string _searchText = string.Empty;

    // Form Fields
    private string _recNo = string.Empty;
    private int _nos;
    private DateTime _recDate = DateTime.Today;
    private string _partyName = string.Empty;
    private string _itemType = string.Empty;
    private decimal _weightRecd;
    private decimal _makingCharges;
    private string _note = string.Empty;

    private string? _toastMessage;
    private ToastType _toastType = ToastType.Info;

    public KarigharReceiveViewModel(
        IKarigharRepository repository,
        IAccountRepository accountRepository,
        string currentUser = "admin")
    {
        _repository = repository;
        _accountRepository = accountRepository;
        _currentUser = currentUser;

        NewCommand = new AsyncRelayCommand(NewReceiveAsync, () => CanNew);
        SaveCommand = new AsyncRelayCommand(SaveReceiveAsync, () => CanSave);
        ModifyCommand = new RelayCommand(ModifyReceive, () => CanModify);
        DeleteCommand = new AsyncRelayCommand(DeleteReceiveAsync, () => CanDelete);
        CancelCommand = new RelayCommand(CancelReceive, () => CanCancel);
        RefreshCommand = new AsyncRelayCommand(LoadDataAsync);

        _ = InitializeAsync();
    }

    public string Title => "Karighar Receive";

    public event EventHandler? RequestFocusFirstField;

    public ObservableCollection<Account> Artisans { get; } = [];
    public ObservableCollection<string> ItemTypes { get; } = [];
    public ObservableCollection<MetalReceivedItem> ReceivedList { get; } = [];
    public ObservableCollection<MetalReceivedItem> FilteredReceivedList { get; } = [];

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
    public bool CanModify => FormMode == KarigharFormMode.View && !string.IsNullOrWhiteSpace(RecNo) && !IsBusy;
    public bool CanDelete => FormMode == KarigharFormMode.View && !string.IsNullOrWhiteSpace(RecNo) && !IsBusy;
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

    public MetalReceivedItem? SelectedItem
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

    public string RecNo
    {
        get => _recNo;
        set => SetProperty(ref _recNo, value);
    }

    public int Nos
    {
        get => _nos;
        set => SetProperty(ref _nos, value);
    }

    public DateTime RecDate
    {
        get => _recDate;
        set => SetProperty(ref _recDate, value);
    }

    public string PartyName
    {
        get => _partyName;
        set => SetProperty(ref _partyName, value);
    }

    public string ItemType
    {
        get => _itemType;
        set => SetProperty(ref _itemType, value);
    }

    public decimal WeightRecd
    {
        get => _weightRecd;
        set => SetProperty(ref _weightRecd, value);
    }

    public decimal MakingCharges
    {
        get => _makingCharges;
        set => SetProperty(ref _makingCharges, value);
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
            var itemTypesTask = _repository.GetItemTypesAsync();
            var receivedListTask = _repository.GetAllMetalReceivedAsync(100);

            await Task.WhenAll(accountsTask, itemTypesTask, receivedListTask);

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

            var itemTypes = await itemTypesTask;
            ItemTypes.Clear();
            foreach (var it in itemTypes)
            {
                ItemTypes.Add(it);
            }

            var received = await receivedListTask;
            _suppressSync = true;
            ReceivedList.Clear();
            foreach (var item in received)
            {
                ReceivedList.Add(item);
            }
            _suppressSync = false;

            ApplyFilter();

            if (ReceivedList.Count > 0)
            {
                SelectedItem = ReceivedList.First();
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
            var received = await _repository.GetAllMetalReceivedAsync(100);
            _suppressSync = true;
            ReceivedList.Clear();
            foreach (var item in received)
            {
                ReceivedList.Add(item);
            }
            _suppressSync = false;
            ApplyFilter();
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to load data: {ex.Message}", ToastType.Error);
        }
    }

    private async Task NewReceiveAsync()
    {
        try
        {
            IsBusy = true;
            var nextRecNo = await _repository.GetNextRecNoAsync();
            var nextNos = await _repository.GetNextRecNosAsync();

            PrepareBlankForm();
            RecNo = nextRecNo;
            Nos = nextNos;
            RecDate = DateTime.Today;
            SelectedTabIndex = 0;

            _suppressSync = true;
            SelectedItem = null;
            _suppressSync = false;

            FormMode = KarigharFormMode.Add;
            ShowToast($"Ready to enter Receive: {RecNo}", ToastType.Info);
            RequestFocusFirstField?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ShowToast($"Error preparing new receive: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SelectReceiveByVouNoAsync(string vouNo)
    {
        if (string.IsNullOrWhiteSpace(vouNo)) return;
        if (ReceivedList.Count == 0)
        {
            await LoadDataAsync();
        }
        var found = ReceivedList.FirstOrDefault(x => string.Equals(x.RecNo, vouNo, StringComparison.OrdinalIgnoreCase));
        if (found != null)
        {
            SelectedItem = found;
        }
    }

    private void ModifyReceive()
    {
        if (string.IsNullOrWhiteSpace(RecNo))
        {
            ShowToast("Select a receive record to modify.", ToastType.Warning);
            return;
        }

        FormMode = KarigharFormMode.Modify;
        SelectedTabIndex = 0;
        ShowToast($"Editing Receive #{RecNo}", ToastType.Info);
        RequestFocusFirstField?.Invoke(this, EventArgs.Empty);
    }

    private async Task SaveReceiveAsync()
    {
        if (string.IsNullOrWhiteSpace(PartyName))
        {
            ShowToast("Please select or enter an Artisian Name.", ToastType.Warning);
            return;
        }

        if (WeightRecd <= 0 && MakingCharges <= 0)
        {
            ShowToast("Please enter a valid Weight Received or Making Charges.", ToastType.Warning);
            return;
        }

        var result = MessageBox.Show(
            $"Do you want to save Metal Receive #{RecNo}?",
            "Karighar Receive",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.Yes);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            IsBusy = true;
            var isUpdate = FormMode == KarigharFormMode.Modify;

            var item = new MetalReceivedItem
            {
                RecNo = RecNo.Trim(),
                Nos = Nos,
                RecDate = RecDate,
                PartyName = PartyName.Trim(),
                ItemType = ItemType?.Trim() ?? string.Empty,
                WeightRecd = WeightRecd,
                MakingCharges = MakingCharges,
                Note = Note?.Trim() ?? string.Empty,
                EntryUser = string.IsNullOrWhiteSpace(_currentUser) ? "admin" : _currentUser,
                EntryDate = DateTime.Today,
                EntryTime = DateTime.Now,
                ComputerName = Environment.MachineName,
                Status = "Saved"
            };

            await _repository.SaveMetalReceivedAsync(item, isUpdate);
            ShowToast($"Metal Receive #{item.RecNo} saved successfully!", ToastType.Success);

            await LoadDataAsync();

            _suppressSync = true;
            SelectedItem = ReceivedList.FirstOrDefault(x => x.RecNo == item.RecNo);
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

    private async Task DeleteReceiveAsync()
    {
        if (string.IsNullOrWhiteSpace(RecNo))
        {
            ShowToast("Select a receive record to delete.", ToastType.Warning);
            return;
        }

        var result = MessageBox.Show(
            $"Are you sure you want to delete Metal Receive #{RecNo}?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            IsBusy = true;
            await _repository.DeleteMetalReceivedAsync(RecNo);
            ShowToast($"Receive #{RecNo} deleted.", ToastType.Success);

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

    private void CancelReceive()
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

    private void PopulateFromItem(MetalReceivedItem item)
    {
        RecNo = item.RecNo;
        Nos = item.Nos;
        RecDate = item.RecDate;
        PartyName = item.PartyName;
        ItemType = item.ItemType;
        WeightRecd = item.WeightRecd;
        MakingCharges = item.MakingCharges;
        Note = item.Note;
    }

    private void PrepareBlankForm()
    {
        RecNo = string.Empty;
        Nos = 0;
        RecDate = DateTime.Today;
        PartyName = string.Empty;
        ItemType = ItemTypes.FirstOrDefault() ?? string.Empty;
        WeightRecd = 0m;
        MakingCharges = 0m;
        Note = string.Empty;
        FormMode = KarigharFormMode.View;
    }

    private void ApplyFilter()
    {
        FilteredReceivedList.Clear();
        var query = SearchText?.Trim();

        foreach (var item in ReceivedList)
        {
            if (string.IsNullOrWhiteSpace(query) ||
                item.RecNo.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.PartyName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.ItemType.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.Note.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                FilteredReceivedList.Add(item);
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
               !string.IsNullOrWhiteSpace(PartyName) ||
               !string.IsNullOrWhiteSpace(ItemType) ||
               WeightRecd > 0;
    }
}
