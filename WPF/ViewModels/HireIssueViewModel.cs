using System.Collections.ObjectModel;
using System.Windows.Input;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.WPF.Controls;

namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class HireIssueViewModel : ViewModelBase, IConfirmCloseDocument
{
    private readonly IHireRepository _repository;
    private readonly string _currentUser;

    private KarigharFormMode _formMode = KarigharFormMode.View;
    private HireIssuedItem? _selectedItem;
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
    private string _note = string.Empty;

    private string? _toastMessage;
    private ToastType _toastType = ToastType.Info;

    public HireIssueViewModel(
        IHireRepository repository,
        string currentUser = "admin")
    {
        _repository = repository;
        _currentUser = currentUser;

        NewCommand = new AsyncRelayCommand(NewIssueAsync, () => CanNew);
        SaveCommand = new AsyncRelayCommand(SaveIssueAsync, () => CanSave);
        ModifyCommand = new RelayCommand(ModifyIssue, () => CanModify);
        DeleteCommand = new AsyncRelayCommand(DeleteIssueAsync, () => CanDelete);
        CancelCommand = new RelayCommand(CancelIssue, () => CanCancel);
        RefreshCommand = new AsyncRelayCommand(LoadDataAsync);

        _ = InitializeAsync();
    }

    public string Title => "Hire Issue";
    public string HeaderIcon => AppIcons.KarigharIssue;
    public string CurrentUsername => _currentUser;

    public ObservableCollection<Account> Artisans { get; } = [];
    public ObservableCollection<string> ItemTypes { get; } = [];
    public ObservableCollection<HireIssuedItem> IssuedList { get; } = [];
    public ObservableCollection<HireIssuedItem> FilteredIssuedList { get; } = [];

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
        KarigharFormMode.Add => "NEW HIRE ISSUE",
        KarigharFormMode.Modify => "MODIFYING HIRE ISSUE",
        _ => "VIEW MODE"
    };

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

    public HireIssuedItem? SelectedItem
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
    public string? CloseConfirmationMessage => "You have unsaved changes in Hire Issue. Are you sure you want to close this tab?";

    private async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
            var artisans = await _repository.GetArtisansAsync();
            Artisans.Clear();
            foreach (var a in artisans) Artisans.Add(a);

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
            var list = await _repository.GetRecentIssuedAsync(100);
            IssuedList.Clear();
            foreach (var item in list) IssuedList.Add(item);

            ApplyFilter();

            if (IssuedList.Count > 0 && SelectedItem is null)
            {
                SelectedItem = IssuedList[0];
            }
            else if (IssuedList.Count == 0)
            {
                ClearForm();
            }
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to load hire issue list: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFilter()
    {
        FilteredIssuedList.Clear();
        var q = _searchText?.Trim() ?? string.Empty;
        var query = string.IsNullOrEmpty(q)
            ? IssuedList
            : IssuedList.Where(i =>
                i.IssueNo.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                i.ArtisianName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                i.ItemType.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                i.Note.Contains(q, StringComparison.OrdinalIgnoreCase));

        foreach (var item in query)
        {
            FilteredIssuedList.Add(item);
        }
    }

    private async Task NewIssueAsync()
    {
        ClearForm();
        FormMode = KarigharFormMode.Add;
        SelectedTabIndex = 0;

        try
        {
            IssueNo = await _repository.GetNextIssueNoAsync();
            Nos = await _repository.GetNextIssueNosAsync();
            IssueDate = DateTime.Today;
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to generate next Issue No: {ex.Message}", ToastType.Warning);
        }
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
            ShowToast("Please enter a valid Weight Issued greater than 0.", ToastType.Warning);
            return;
        }

        IsBusy = true;
        try
        {
            var item = new HireIssuedItem
            {
                IssueNo = IssueNo,
                Nos = Nos,
                IssueDate = IssueDate,
                ArtisianName = ArtisianName.Trim(),
                ItemType = ItemType?.Trim() ?? string.Empty,
                WeightIssued = WeightIssued,
                Note = Note?.Trim() ?? string.Empty,
                EntryUser = _currentUser,
                EntryDate = DateTime.Today,
                EntryTime = DateTime.Now,
                ComputerName = Environment.MachineName,
                Status = "Saved"
            };

            await _repository.SaveIssuedAsync(item, isUpdate: FormMode == KarigharFormMode.Modify);

            ShowToast($"Hire Issue {IssueNo} saved successfully!", ToastType.Success);
            FormMode = KarigharFormMode.View;

            await LoadDataAsync();

            _suppressSync = true;
            SelectedItem = IssuedList.FirstOrDefault(i => string.Equals(i.IssueNo, item.IssueNo, StringComparison.OrdinalIgnoreCase));
            _suppressSync = false;
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to save Hire Issue: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ModifyIssue()
    {
        if (SelectedItem is null) return;
        FormMode = KarigharFormMode.Modify;
        SelectedTabIndex = 0;
    }

    private async Task DeleteIssueAsync()
    {
        if (SelectedItem is null || string.IsNullOrWhiteSpace(SelectedItem.IssueNo)) return;

        var result = System.Windows.MessageBox.Show(
            $"Are you sure you want to delete Hire Issue \"{SelectedItem.IssueNo}\" for {SelectedItem.ArtisianName}?",
            "Confirm Delete",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result != System.Windows.MessageBoxResult.Yes) return;

        IsBusy = true;
        try
        {
            await _repository.DeleteIssuedAsync(SelectedItem.IssueNo);
            ShowToast($"Hire Issue {SelectedItem.IssueNo} deleted.", ToastType.Info);
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to delete Hire Issue: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void CancelIssue()
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

        IssueNo = SelectedItem.IssueNo;
        Nos = SelectedItem.Nos;
        IssueDate = SelectedItem.IssueDate;
        ArtisianName = SelectedItem.ArtisianName;
        ItemType = SelectedItem.ItemType;
        WeightIssued = SelectedItem.WeightIssued;
        Note = SelectedItem.Note;
    }

    private void ClearForm()
    {
        IssueNo = string.Empty;
        Nos = 0;
        IssueDate = DateTime.Today;
        ArtisianName = string.Empty;
        ItemType = string.Empty;
        WeightIssued = 0m;
        Note = string.Empty;
    }

    private void ShowToast(string message, ToastType type)
    {
        ToastMessage = message;
        ToastType = type;
    }
}
