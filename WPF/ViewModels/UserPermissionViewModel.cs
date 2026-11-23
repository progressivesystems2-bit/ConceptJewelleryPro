using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.WPF.Controls;
using ConceptJewelleryPro.WPF.Helpers;

namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class UserPermissionViewModel : ViewModelBase
{
    private readonly IUserPermissionRepository _userPermissionRepository;
    private readonly string _currentUser;
    private readonly List<UserPermission> _allUsers = new();

    private UserPermission? _selectedUser;
    private string _searchText = string.Empty;
    private bool _isBusy;
    private string? _toastMessage;
    private ToastType _toastType = ToastType.Info;
    private string _errorMessage = string.Empty;
    private string _statusMessage = string.Empty;

    public UserPermissionViewModel(IUserPermissionRepository userPermissionRepository, string currentUser = "admin")
    {
        _userPermissionRepository = userPermissionRepository;
        _currentUser = currentUser;

        Users = new ObservableCollection<UserPermission>();

        AddCommand = new RelayCommand(AddNew);
        EditCommand = new RelayCommand(EditSelected, () => SelectedUser is not null);
        DeleteCommand = new RelayCommand(async () => await DeleteSelectedAsync(), () => SelectedUser is not null);
        RefreshCommand = new RelayCommand(async () => await LoadDataAsync());

        _ = LoadDataAsync();
    }

    public ObservableCollection<UserPermission> Users { get; }

    public ICommand AddCommand { get; }

    public ICommand EditCommand { get; }

    public ICommand DeleteCommand { get; }

    public ICommand RefreshCommand { get; }

    public UserPermission? SelectedUser
    {
        get => _selectedUser;
        set
        {
            if (SetProperty(ref _selectedUser, value))
            {
                CommandManager.InvalidateRequerySuggested();
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

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (SetProperty(ref _statusMessage, value) && !string.IsNullOrWhiteSpace(value))
            {
                _errorMessage = string.Empty;
                OnPropertyChanged(nameof(ErrorMessage));
                ToastType = ToastType.Success;
                ToastMessage = string.Empty;
                ToastMessage = value;
            }
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value) && !string.IsNullOrWhiteSpace(value))
            {
                _statusMessage = string.Empty;
                OnPropertyChanged(nameof(StatusMessage));
                ToastType = ToastType.Error;
                ToastMessage = string.Empty;
                ToastMessage = value;
            }
        }
    }

    public async Task LoadDataAsync()
    {
        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;

            var list = await _userPermissionRepository.GetAllAsync();
            _allUsers.Clear();
            _allUsers.AddRange(list);

            ApplyFilter();
            StatusMessage = $"Loaded {_allUsers.Count} user accounts.";
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to load user permissions: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFilter()
    {
        var filter = SearchText?.Trim() ?? string.Empty;
        var filtered = string.IsNullOrWhiteSpace(filter)
            ? _allUsers
            : _allUsers.Where(u =>
                u.User.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                u.Toolbar.Contains(filter, StringComparison.OrdinalIgnoreCase));

        Users.Clear();
        foreach (var u in filtered)
        {
            Users.Add(u);
        }

        if (SelectedUser is not null && !Users.Contains(SelectedUser))
        {
            SelectedUser = Users.FirstOrDefault();
        }
    }

    public void AddNew()
    {
        var editVm = new UserPermissionEditViewModel(_userPermissionRepository, null, _currentUser);
        var win = new Views.UserPermissionEditWindow(editVm);
        win.SetOwnerSafely();
        if (win.ShowDialog() == true)
        {
            _ = LoadDataAsync();
        }
    }

    public void EditSelected()
    {
        if (SelectedUser is null) return;
        var editVm = new UserPermissionEditViewModel(_userPermissionRepository, SelectedUser, _currentUser);
        var win = new Views.UserPermissionEditWindow(editVm);
        win.SetOwnerSafely();
        if (win.ShowDialog() == true)
        {
            _ = LoadDataAsync();
        }
    }

    public async Task DeleteSelectedAsync()
    {
        if (SelectedUser is null)
        {
            ErrorMessage = "Please select a user to delete.";
            return;
        }

        var username = SelectedUser.User;

        if (string.Equals(username, "admin", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show("The primary admin user cannot be deleted.", "Action Restricted", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var result = MessageBox.Show(
            $"Are you sure you want to delete user '{username}' and all associated permissions?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            IsBusy = true;
            await _userPermissionRepository.DeleteAsync(username);
            StatusMessage = $"User '{username}' deleted successfully.";
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to delete user: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
