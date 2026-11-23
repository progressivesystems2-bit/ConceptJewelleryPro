using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.WPF.Controls;
using ConceptJewelleryPro.WPF.Helpers;

namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class SalesVouTypeMasterViewModel : ViewModelBase
{
    private readonly ISalesVouTypeRepository _salesVouTypeRepository;
    private readonly IAccountRepository? _accountRepository;
    private readonly IItemRepository? _itemRepository;
    private readonly IPurityRepository? _purityRepository;
    private readonly string _currentUser;
    private readonly List<SalesVouType> _allVouTypes = new();

    private SalesVouType? _selectedVouType;
    private string _selectedCategory = "ALL";
    private string _searchText = string.Empty;
    private bool _showHidden = true;
    private bool _isBusy;
    private string? _toastMessage;
    private ToastType _toastType = ToastType.Info;
    private string _errorMessage = string.Empty;
    private string _statusMessage = string.Empty;

    public SalesVouTypeMasterViewModel(
        ISalesVouTypeRepository salesVouTypeRepository,
        IAccountRepository? accountRepository = null,
        IItemRepository? itemRepository = null,
        IPurityRepository? purityRepository = null,
        string currentUser = "admin")
    {
        _salesVouTypeRepository = salesVouTypeRepository;
        _accountRepository = accountRepository;
        _itemRepository = itemRepository;
        _purityRepository = purityRepository;
        _currentUser = currentUser;

        VouTypes = new ObservableCollection<SalesVouType>();
        AvailableCategories = new ObservableCollection<string> { "ALL" };

        AddCommand = new RelayCommand(AddNew);
        EditCommand = new RelayCommand(EditSelected, () => SelectedVouType is not null);
        DeleteCommand = new RelayCommand(async () => await DeleteSelectedAsync(), () => SelectedVouType is not null);
        RefreshCommand = new RelayCommand(async () => await LoadDataAsync());

        _ = LoadDataAsync();
    }

    public ObservableCollection<SalesVouType> VouTypes { get; }

    public ObservableCollection<string> AvailableCategories { get; }

    public ICommand AddCommand { get; }

    public ICommand EditCommand { get; }

    public ICommand DeleteCommand { get; }

    public ICommand RefreshCommand { get; }

    public SalesVouType? SelectedVouType
    {
        get => _selectedVouType;
        set
        {
            if (SetProperty(ref _selectedVouType, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
            {
                ApplyFilter();
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

    public bool ShowHidden
    {
        get => _showHidden;
        set
        {
            if (SetProperty(ref _showHidden, value))
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

            var list = await _salesVouTypeRepository.GetAllAsync(null, true);
            _allVouTypes.Clear();
            _allVouTypes.AddRange(list);

            var categories = await _salesVouTypeRepository.GetHeadCategoriesAsync();
            AvailableCategories.Clear();
            AvailableCategories.Add("ALL");
            foreach (var cat in categories)
            {
                if (!AvailableCategories.Contains(cat))
                {
                    AvailableCategories.Add(cat);
                }
            }

            ApplyFilter();
            StatusMessage = $"Loaded {_allVouTypes.Count} voucher types.";
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to load voucher types: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFilter()
    {
        var filter = SearchText?.Trim() ?? string.Empty;
        var selectedCat = SelectedCategory?.Trim() ?? "ALL";

        var filtered = _allVouTypes.AsEnumerable();

        if (!ShowHidden)
        {
            filtered = filtered.Where(v => !v.Hide);
        }

        if (!string.Equals(selectedCat, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(v => string.Equals(v.HeadCategory, selectedCat, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(filter))
        {
            filtered = filtered.Where(v =>
                v.VouType.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                v.ShortName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                v.SalesAccount.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                v.VatAccount.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                v.ItemHead.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                v.PrintFile.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                v.HeadCategory.Contains(filter, StringComparison.OrdinalIgnoreCase));
        }

        VouTypes.Clear();
        foreach (var v in filtered)
        {
            VouTypes.Add(v);
        }

        if (SelectedVouType is not null && !VouTypes.Contains(SelectedVouType))
        {
            SelectedVouType = VouTypes.FirstOrDefault();
        }
    }

    public void AddNew()
    {
        var editVm = new SalesVouTypeEditViewModel(_salesVouTypeRepository, _accountRepository, _itemRepository, _purityRepository, null, _currentUser);
        var win = new Views.SalesVouTypeEditWindow(editVm);
        win.SetOwnerSafely();
        if (win.ShowDialog() == true)
        {
            _ = LoadDataAsync();
        }
    }

    public void EditSelected()
    {
        if (SelectedVouType is null) return;
        var editVm = new SalesVouTypeEditViewModel(_salesVouTypeRepository, _accountRepository, _itemRepository, _purityRepository, SelectedVouType, _currentUser);
        var win = new Views.SalesVouTypeEditWindow(editVm);
        win.SetOwnerSafely();
        if (win.ShowDialog() == true)
        {
            _ = LoadDataAsync();
        }
    }

    public async Task DeleteSelectedAsync()
    {
        if (SelectedVouType is null)
        {
            ErrorMessage = "Please select a voucher type to delete.";
            return;
        }

        var vouName = SelectedVouType.VouType;
        var result = MessageBox.Show(
            $"Are you sure you want to delete Voucher Type '{vouName}'?\n\nWarning: Invoices or entries created with this type may require it for reporting.",
            "Confirm Delete Voucher Type",
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
            await _salesVouTypeRepository.DeleteAsync(vouName);
            StatusMessage = $"Voucher Type '{vouName}' deleted successfully.";
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to delete voucher type: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
