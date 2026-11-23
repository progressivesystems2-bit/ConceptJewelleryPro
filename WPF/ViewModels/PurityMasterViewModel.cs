using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.WPF.Controls;
using ConceptJewelleryPro.WPF.Helpers;

namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class PurityMasterViewModel : ViewModelBase
{
    private readonly IPurityRepository _purityRepository;
    private readonly IAuditRepository? _auditRepository;
    private readonly string _currentUser;
    private readonly List<PurityMaster> _allPurities = new();

    private PurityMaster? _selectedPurity;
    private string _searchText = string.Empty;
    private bool _isBusy;
    private string? _toastMessage;
    private ToastType _toastType = ToastType.Info;
    private string _errorMessage = string.Empty;
    private string _statusMessage = string.Empty;

    public PurityMasterViewModel(
        IPurityRepository purityRepository,
        string currentUser = "admin",
        IAuditRepository? auditRepository = null)
    {
        _purityRepository = purityRepository;
        _currentUser = currentUser;
        _auditRepository = auditRepository;

        Purities = new ObservableCollection<PurityMaster>();

        AddCommand = new RelayCommand(AddNew);
        EditCommand = new RelayCommand(EditSelected, () => SelectedPurity is not null);
        DeleteCommand = new RelayCommand(async () => await DeleteSelectedAsync(), () => SelectedPurity is not null);
        RefreshCommand = new RelayCommand(async () => await LoadDataAsync());
        UpdateRatesCommand = new RelayCommand(OpenRateUpdate);

        _ = LoadDataAsync();
    }

    public ObservableCollection<PurityMaster> Purities { get; }

    public ICommand AddCommand { get; }

    public ICommand EditCommand { get; }

    public ICommand DeleteCommand { get; }

    public ICommand RefreshCommand { get; }

    public ICommand UpdateRatesCommand { get; }

    public PurityMaster? SelectedPurity
    {
        get => _selectedPurity;
        set
        {
            if (SetProperty(ref _selectedPurity, value))
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

            var list = await _purityRepository.GetAllAsync();
            _allPurities.Clear();
            _allPurities.AddRange(list);

            ApplyFilter();
            StatusMessage = $"Loaded {_allPurities.Count} purity records.";
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to load purity records: " + ex.Message;
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
            ? _allPurities
            : _allPurities.Where(p =>
                p.Purity.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                p.ItemHead.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                p.ItemType.Contains(filter, StringComparison.OrdinalIgnoreCase));

        Purities.Clear();
        foreach (var p in filtered)
        {
            Purities.Add(p);
        }

        if (SelectedPurity is not null && !Purities.Contains(SelectedPurity))
        {
            SelectedPurity = Purities.FirstOrDefault();
        }
    }

    public void AddNew()
    {
        var editVm = new PurityEditViewModel(_purityRepository, null, _currentUser);
        var win = new Views.PurityEditWindow(editVm);
        win.SetOwnerSafely();
        if (win.ShowDialog() == true)
        {
            _ = LoadDataAsync();
        }
    }

    public void EditSelected()
    {
        if (SelectedPurity is null) return;
        var editVm = new PurityEditViewModel(_purityRepository, SelectedPurity, _currentUser);
        var win = new Views.PurityEditWindow(editVm);
        win.SetOwnerSafely();
        if (win.ShowDialog() == true)
        {
            _ = LoadDataAsync();
        }
    }

    public async Task DeleteSelectedAsync()
    {
        if (SelectedPurity is null)
        {
            ErrorMessage = "Please select a purity record to delete.";
            return;
        }

        var purityName = SelectedPurity.Purity;
        var result = MessageBox.Show(
            $"Are you sure you want to delete Purity '{purityName}'?",
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
            await _purityRepository.DeleteAsync(purityName);
            StatusMessage = $"Purity '{purityName}' deleted successfully.";
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to delete purity: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void OpenRateUpdate()
    {
        var vm = new RateUpdateViewModel(_purityRepository, _currentUser, _auditRepository);
        var win = new Views.RateUpdateWindow(vm);
        win.SetOwnerSafely();
        if (win.ShowDialog() == true)
        {
            _ = LoadDataAsync();
        }
    }
}
