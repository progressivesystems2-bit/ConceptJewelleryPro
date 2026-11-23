using System.Collections.ObjectModel;
using System.Windows.Input;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.WPF.Controls;

namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class HireReceiveViewModel : ViewModelBase, IConfirmCloseDocument
{
    private readonly IHireRepository _repository;
    private readonly string _currentUser;

    private KarigharFormMode _formMode = KarigharFormMode.View;
    private HireReceivedItem? _selectedItem;
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
    private string _note = string.Empty;

    private string? _toastMessage;
    private ToastType _toastType = ToastType.Info;

    public HireReceiveViewModel(
        IHireRepository repository,
        string currentUser = "admin")
    {
        _repository = repository;
        _currentUser = currentUser;

        NewCommand = new AsyncRelayCommand(NewReceiveAsync, () => CanNew);
        SaveCommand = new AsyncRelayCommand(SaveReceiveAsync, () => CanSave);
        ModifyCommand = new RelayCommand(ModifyReceive, () => CanModify);
        DeleteCommand = new AsyncRelayCommand(DeleteReceiveAsync, () => CanDelete);
        CancelCommand = new RelayCommand(CancelReceive, () => CanCancel);
        RefreshCommand = new AsyncRelayCommand(LoadDataAsync);

        _ = InitializeAsync();
    }

    public string Title => "Hire Receive";
    public string HeaderIcon => AppIcons.KarigharReceived;
    public string CurrentUsername => _currentUser;

    public ObservableCollection<Account> Parties { get; } = [];
    public ObservableCollection<string> ItemTypes { get; } = [];
    public ObservableCollection<HireReceivedItem> ReceivedList { get; } = [];
    public ObservableCollection<HireReceivedItem> FilteredReceivedList { get; } = [];

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
                OnPropertyChanged(nameof(FormStatusText));
            }
        }
    }

    public bool IsFormEnabled => FormMode != KarigharFormMode.View;
    public bool CanNew => FormMode == KarigharFormMode.View;
    public bool CanSave => FormMode != KarigharFormMode.View;
    public bool CanModify => FormMode == KarigharFormMode.View && SelectedItem != null;
    public bool CanDelete => FormMode == KarigharFormMode.View && SelectedItem != null;
    public bool CanCancel => FormMode != KarigharFormMode.View;

    public string FormStatusText => FormMode switch
    {
        KarigharFormMode.Add => "NEW HIRE RECEIVE",
        KarigharFormMode.Modify => "MODIFYING HIRE RECEIVE",
        _ => "VIEW MODE"
    };

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

    public string Note
    {
        get => _note;
        set => SetProperty(ref _note, value);
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

    public HireReceivedItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetProperty(ref _selectedItem, value) && !_suppressSync)
            {
                SyncFromSelectedItem();
                OnPropertyChanged(nameof(CanModify));
                OnPropertyChanged(nameof(CanDelete));
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
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

    public bool HasUnsavedChanges() => FormMode != KarigharFormMode.View;
    public string? CloseConfirmationMessage => "You have unsaved changes in Hire Receive. Are you sure you want to close this tab?";

    private async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
            var parties = await _repository.GetArtisansAsync();
            Parties.Clear();
            foreach (var p in parties) Parties.Add(p);

            var itemTypes = await _repository.GetItemTypesAsync();
            ItemTypes.Clear();
            foreach (var t in itemTypes) ItemTypes.Add(t);

            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to initialize master data: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task LoadDataAsync()
    {
        IsBusy = true;
        try
        {
            var list = await _repository.GetRecentReceivedAsync(100);
            ReceivedList.Clear();
            foreach (var item in list) ReceivedList.Add(item);

            ApplyFilter();

            if (ReceivedList.Count > 0 && SelectedItem is null)
            {
                SelectedItem = ReceivedList[0];
            }
            else if (ReceivedList.Count == 0)
            {
                ClearForm();
            }
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to load hire receive list: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFilter()
    {
        FilteredReceivedList.Clear();
        var q = _searchText?.Trim() ?? string.Empty;
        var query = string.IsNullOrEmpty(q)
            ? ReceivedList
            : ReceivedList.Where(i =>
                i.RecNo.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                i.PartyName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                i.ItemType.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                i.Note.Contains(q, StringComparison.OrdinalIgnoreCase));

        foreach (var item in query)
        {
            FilteredReceivedList.Add(item);
        }
    }

    private async Task NewReceiveAsync()
    {
        ClearForm();
        FormMode = KarigharFormMode.Add;
        SelectedTabIndex = 0;

        try
        {
            RecNo = await _repository.GetNextRecNoAsync();
            Nos = await _repository.GetNextRecNosAsync();
            RecDate = DateTime.Today;
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to generate next Rec No: {ex.Message}", ToastType.Warning);
        }
    }

    private async Task SaveReceiveAsync()
    {
        if (string.IsNullOrWhiteSpace(PartyName))
        {
            ShowToast("Please select or enter a Party / Artisian Name.", ToastType.Warning);
            return;
        }

        if (WeightRecd <= 0)
        {
            ShowToast("Please enter a valid Weight Received greater than 0.", ToastType.Warning);
            return;
        }

        IsBusy = true;
        try
        {
            var item = new HireReceivedItem
            {
                RecNo = RecNo,
                Nos = Nos,
                RecDate = RecDate,
                PartyName = PartyName.Trim(),
                ItemType = ItemType?.Trim() ?? string.Empty,
                WeightRecd = WeightRecd,
                Note = Note?.Trim() ?? string.Empty,
                EntryUser = _currentUser,
                EntryDate = DateTime.Today,
                EntryTime = DateTime.Now,
                ComputerName = Environment.MachineName,
                Status = "Saved"
            };

            await _repository.SaveReceivedAsync(item, isUpdate: FormMode == KarigharFormMode.Modify);

            ShowToast($"Hire Receive {RecNo} saved successfully!", ToastType.Success);
            FormMode = KarigharFormMode.View;

            await LoadDataAsync();

            _suppressSync = true;
            SelectedItem = ReceivedList.FirstOrDefault(i => string.Equals(i.RecNo, item.RecNo, StringComparison.OrdinalIgnoreCase));
            _suppressSync = false;
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to save Hire Receive: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ModifyReceive()
    {
        if (SelectedItem is null) return;
        FormMode = KarigharFormMode.Modify;
        SelectedTabIndex = 0;
    }

    private async Task DeleteReceiveAsync()
    {
        if (SelectedItem is null || string.IsNullOrWhiteSpace(SelectedItem.RecNo)) return;

        var result = System.Windows.MessageBox.Show(
            $"Are you sure you want to delete Hire Receive \"{SelectedItem.RecNo}\" for {SelectedItem.PartyName}?",
            "Confirm Delete",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result != System.Windows.MessageBoxResult.Yes) return;

        IsBusy = true;
        try
        {
            await _repository.DeleteReceivedAsync(SelectedItem.RecNo);
            ShowToast($"Hire Receive {SelectedItem.RecNo} deleted.", ToastType.Info);
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to delete Hire Receive: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void CancelReceive()
    {
        FormMode = KarigharFormMode.View;
        SyncFromSelectedItem();
    }

    private void SyncFromSelectedItem()
    {
        if (SelectedItem is null)
        {
            ClearForm();
            return;
        }

        RecNo = SelectedItem.RecNo;
        Nos = SelectedItem.Nos;
        RecDate = SelectedItem.RecDate;
        PartyName = SelectedItem.PartyName;
        ItemType = SelectedItem.ItemType;
        WeightRecd = SelectedItem.WeightRecd;
        Note = SelectedItem.Note;
    }

    private void ClearForm()
    {
        RecNo = string.Empty;
        Nos = 0;
        RecDate = DateTime.Today;
        PartyName = string.Empty;
        ItemType = string.Empty;
        WeightRecd = 0m;
        Note = string.Empty;
    }

    private void ShowToast(string message, ToastType type)
    {
        ToastMessage = message;
        ToastType = type;
    }
}
