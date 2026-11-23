using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.WPF.Controls;

namespace ConceptJewelleryPro.WPF.ViewModels;

public enum StaffFormMode
{
    View,
    Add,
    Modify
}

public sealed class StaffCreationViewModel : ViewModelBase, IConfirmCloseDocument
{
    private readonly IStaffRepository _repository;
    private readonly IAuditRepository? _auditRepository;
    private readonly string _currentUser;

    private StaffFormMode _formMode = StaffFormMode.View;
    private StaffMember? _selectedStaff;
    private StaffMember? _originalStaffSnapshot;
    private string _originalStaffName = string.Empty;
    private bool _isBusy;
    private bool _suppressSync;
    private string _searchText = string.Empty;

    // Form fields
    private string _staffName = string.Empty;
    private string _status = "Active";

    private string? _toastMessage;
    private ToastType _toastType = ToastType.Info;

    public StaffCreationViewModel(IStaffRepository repository, string currentUser = "admin", IAuditRepository? auditRepository = null)
    {
        _repository = repository;
        _auditRepository = auditRepository;
        _currentUser = currentUser;

        NewCommand = new AsyncRelayCommand(NewStaffAsync, () => CanNew);
        SaveCommand = new AsyncRelayCommand(SaveStaffAsync, () => CanSave);
        ModifyCommand = new RelayCommand(ModifyStaff, () => CanModify);
        DeleteCommand = new AsyncRelayCommand(DeleteStaffAsync, () => CanDelete);
        CancelCommand = new RelayCommand(CancelStaff, () => CanCancel);
        RefreshCommand = new AsyncRelayCommand(LoadDataAsync);

        _ = InitializeAsync();
    }

    public string Title => "Staff Creation";

    public event EventHandler? RequestFocusName;

    public ObservableCollection<StaffMember> StaffList { get; } = [];
    public ObservableCollection<StaffMember> FilteredStaffList { get; } = [];
    public IReadOnlyList<string> StatusOptions { get; } = ["Active", "Saved", "Inactive"];

    public AsyncRelayCommand NewCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public RelayCommand ModifyCommand { get; }
    public AsyncRelayCommand DeleteCommand { get; }
    public RelayCommand CancelCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }

    public StaffFormMode FormMode
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

    public bool IsFormEnabled => FormMode != StaffFormMode.View;
    public bool CanNew => FormMode == StaffFormMode.View && !IsBusy;
    public bool CanSave => FormMode != StaffFormMode.View && !IsBusy;
    public bool CanModify => FormMode == StaffFormMode.View && !string.IsNullOrWhiteSpace(StaffName) && !IsBusy;
    public bool CanDelete => FormMode == StaffFormMode.View && !string.IsNullOrWhiteSpace(StaffName) && !IsBusy;
    public bool CanCancel => FormMode != StaffFormMode.View && !IsBusy;

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

    public StaffMember? SelectedStaff
    {
        get => _selectedStaff;
        set
        {
            if (SetProperty(ref _selectedStaff, value))
            {
                OnPropertyChanged(nameof(CanModify));
                OnPropertyChanged(nameof(CanDelete));
                RefreshCommands();

                if (!_suppressSync && value is not null)
                {
                    PopulateFromItem(value);
                    FormMode = StaffFormMode.View;
                }
            }
        }
    }

    public string StaffName
    {
        get => _staffName;
        set => SetProperty(ref _staffName, value);
    }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
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

    public async Task InitializeAsync()
    {
        try
        {
            IsBusy = true;
            await LoadDataAsync();

            if (StaffList.Count > 0)
            {
                SelectedStaff = StaffList.First();
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
            var list = await _repository.GetAllAsync();
            _suppressSync = true;
            StaffList.Clear();
            foreach (var item in list)
            {
                StaffList.Add(item);
            }
            _suppressSync = false;
            ApplyFilter();
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to load staff list: {ex.Message}", ToastType.Error);
        }
    }

    private Task NewStaffAsync()
    {
        PrepareBlankForm();
        FormMode = StaffFormMode.Add;

        _suppressSync = true;
        SelectedStaff = null;
        _suppressSync = false;

        ShowToast("Ready to add a new staff member.", ToastType.Info);
        RequestFocusName?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    private void ModifyStaff()
    {
        if (string.IsNullOrWhiteSpace(StaffName))
        {
            ShowToast("Select a staff member to modify.", ToastType.Warning);
            return;
        }

        _originalStaffName = StaffName;
        _originalStaffSnapshot = SelectedStaff != null ? new StaffMember { Staff = SelectedStaff.Staff, Status = SelectedStaff.Status } : new StaffMember { Staff = StaffName, Status = Status };
        FormMode = StaffFormMode.Modify;
        ShowToast($"Editing staff member: {StaffName}", ToastType.Info);
        RequestFocusName?.Invoke(this, EventArgs.Empty);
    }

    private async Task SaveStaffAsync()
    {
        if (string.IsNullOrWhiteSpace(StaffName))
        {
            ShowToast("Please enter a Staff Name.", ToastType.Warning);
            return;
        }

        var result = MessageBox.Show(
            $"Do you want to save Staff '{StaffName.Trim()}'?",
            "Staff Creation",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.Yes);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            IsBusy = true;
            var isUpdate = FormMode == StaffFormMode.Modify;

            var member = new StaffMember
            {
                Staff = StaffName.Trim(),
                Status = string.IsNullOrWhiteSpace(Status) ? "Active" : Status.Trim(),
                EntryUser = string.IsNullOrWhiteSpace(_currentUser) ? "admin" : _currentUser,
                EntryDate = DateTime.Today,
                EntryTime = DateTime.Now,
                ComputerName = Environment.MachineName
            };

            await _repository.SaveAsync(member, isUpdate, _originalStaffName);

            if (isUpdate && _auditRepository != null)
            {
                try
                {
                    await Infrastructure.Services.AuditService.LogStaffModificationAsync(_auditRepository, _originalStaffSnapshot, member, _currentUser);
                }
                catch
                {
                    // Non-blocking audit logging
                }
            }

            ShowToast($"Staff '{member.Staff}' saved successfully!", ToastType.Success);

            await LoadDataAsync();

            _suppressSync = true;
            SelectedStaff = StaffList.FirstOrDefault(x => string.Equals(x.Staff, member.Staff, StringComparison.OrdinalIgnoreCase));
            _suppressSync = false;

            FormMode = StaffFormMode.View;
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to save staff: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DeleteStaffAsync()
    {
        if (string.IsNullOrWhiteSpace(StaffName))
        {
            ShowToast("Select a staff member to delete.", ToastType.Warning);
            return;
        }

        var staffToDelete = StaffName;
        var result = MessageBox.Show(
            $"Are you sure you want to delete Staff member '{staffToDelete}'?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            IsBusy = true;
            await _repository.DeleteAsync(staffToDelete);

            if (_auditRepository != null)
            {
                try
                {
                    await Infrastructure.Services.AuditService.LogStaffDeletionAsync(_auditRepository, staffToDelete, _currentUser);
                }
                catch
                {
                    // Non-blocking audit logging
                }
            }

            ShowToast($"Staff member '{staffToDelete}' deleted.", ToastType.Success);

            await LoadDataAsync();
            PrepareBlankForm();
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to delete staff: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void CancelStaff()
    {
        if (SelectedStaff is not null)
        {
            PopulateFromItem(SelectedStaff);
        }
        else
        {
            PrepareBlankForm();
        }

        FormMode = StaffFormMode.View;
        ShowToast("Operation cancelled.", ToastType.Info);
    }

    private void PopulateFromItem(StaffMember member)
    {
        StaffName = member.Staff;
        Status = member.Status;
        _originalStaffName = member.Staff;
    }

    private void PrepareBlankForm()
    {
        StaffName = string.Empty;
        Status = "Active";
        _originalStaffName = string.Empty;
        FormMode = StaffFormMode.View;
    }

    private void ApplyFilter()
    {
        FilteredStaffList.Clear();
        var query = SearchText?.Trim();

        foreach (var item in StaffList)
        {
            if (string.IsNullOrWhiteSpace(query) ||
                item.Staff.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.Status.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.EntryUser.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                FilteredStaffList.Add(item);
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
        return FormMode != StaffFormMode.View ||
               !string.IsNullOrWhiteSpace(StaffName);
    }
}
