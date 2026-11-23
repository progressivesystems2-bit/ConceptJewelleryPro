using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.Infrastructure.Services;
using ConceptJewelleryPro.WPF.Controls;

namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class RateUpdateViewModel : ViewModelBase
{
    private readonly IPurityRepository _purityRepository;
    private readonly IAuditRepository? _auditRepository;
    private readonly string _currentUser;
    private readonly List<RateUpdateRowViewModel> _allRows = new();

    private string _selectedItemHead = "ALL";
    private string _searchText = string.Empty;
    private DateTime _effectiveDate = DateTime.Today;
    private string _effectiveTimeString = DateTime.Now.ToString("hh:mm:ss tt");
    private decimal _quickOffsetAmount;
    private bool _isBusy;
    private string? _toastMessage;
    private ToastType _toastType = ToastType.Info;
    private string _errorMessage = string.Empty;
    private string _statusMessage = string.Empty;

    public event EventHandler<bool?>? RequestClose;

    public RateUpdateViewModel(
        IPurityRepository purityRepository,
        string currentUser = "admin",
        IAuditRepository? auditRepository = null)
    {
        _purityRepository = purityRepository;
        _currentUser = currentUser;
        _auditRepository = auditRepository;

        Rows = new ObservableCollection<RateUpdateRowViewModel>();
        ItemHeads = new ObservableCollection<string> { "ALL" };

        SaveCommand = new RelayCommand(async () => await SaveAsync(), () => HasChanges && !IsBusy);
        CancelCommand = new RelayCommand(Cancel);
        ResetCommand = new RelayCommand(ResetAll);
        RefreshCommand = new RelayCommand(async () => await LoadDataAsync());
        ApplyQuickOffsetCommand = new RelayCommand(ApplyQuickOffset, () => QuickOffsetAmount != 0);

        _ = LoadDataAsync();
    }

    public string Title => "Daily Metal Rate Update (Rate Master)";

    public ObservableCollection<RateUpdateRowViewModel> Rows { get; }

    public ObservableCollection<string> ItemHeads { get; }

    public ICommand SaveCommand { get; }

    public ICommand CancelCommand { get; }

    public ICommand ResetCommand { get; }

    public ICommand RefreshCommand { get; }

    public ICommand ApplyQuickOffsetCommand { get; }

    public string SelectedItemHead
    {
        get => _selectedItemHead;
        set
        {
            if (SetProperty(ref _selectedItemHead, value))
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

    public DateTime EffectiveDate
    {
        get => _effectiveDate;
        set => SetProperty(ref _effectiveDate, value);
    }

    public string EffectiveTimeString
    {
        get => _effectiveTimeString;
        set => SetProperty(ref _effectiveTimeString, value);
    }

    public decimal QuickOffsetAmount
    {
        get => _quickOffsetAmount;
        set => SetProperty(ref _quickOffsetAmount, value);
    }

    public bool HasChanges => _allRows.Any(r => r.IsChanged);

    public int ChangedCount => _allRows.Count(r => r.IsChanged);

    public int TotalCount => _allRows.Count;

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
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
        private set => SetProperty(ref _statusMessage, value);
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

    public async Task LoadDataAsync()
    {
        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;

            var purities = await _purityRepository.GetAllAsync();

            foreach (var row in _allRows)
            {
                row.PropertyChanged -= OnRowPropertyChanged;
            }
            _allRows.Clear();

            var headSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var p in purities)
            {
                var row = new RateUpdateRowViewModel(p.Purity, p.RatePerGram, p.ItemHead, p.ItemType);
                row.PropertyChanged += OnRowPropertyChanged;
                _allRows.Add(row);

                if (!string.IsNullOrWhiteSpace(p.ItemHead))
                {
                    headSet.Add(p.ItemHead.Trim());
                }
            }

            ItemHeads.Clear();
            ItemHeads.Add("ALL");
            foreach (var h in headSet.OrderBy(x => x))
            {
                ItemHeads.Add(h);
            }

            EffectiveDate = DateTime.Today;
            EffectiveTimeString = DateTime.Now.ToString("hh:mm:ss tt");

            ApplyFilter();
            NotifyChangeStats();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to load purity rates: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(RateUpdateRowViewModel.NewRate) or nameof(RateUpdateRowViewModel.IsChanged))
        {
            NotifyChangeStats();
        }
    }

    private void NotifyChangeStats()
    {
        OnPropertyChanged(nameof(HasChanges));
        OnPropertyChanged(nameof(ChangedCount));
        OnPropertyChanged(nameof(TotalCount));
        StatusMessage = HasChanges
            ? $"{ChangedCount} of {TotalCount} rates modified."
            : $"All {TotalCount} rates up-to-date.";
        CommandManager.InvalidateRequerySuggested();
    }

    private void ApplyFilter()
    {
        var filter = SearchText?.Trim() ?? string.Empty;
        var head = SelectedItemHead;

        var query = _allRows.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(head) && !string.Equals(head, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(r => string.Equals(r.ItemHead, head, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(filter))
        {
            query = query.Where(r =>
                r.Purity.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                r.ItemHead.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                r.ItemType.Contains(filter, StringComparison.OrdinalIgnoreCase));
        }

        Rows.Clear();
        foreach (var r in query)
        {
            Rows.Add(r);
        }
    }

    private void ApplyQuickOffset()
    {
        if (QuickOffsetAmount == 0) return;

        // Apply offset to currently visible filtered rows
        foreach (var row in Rows)
        {
            var updated = row.NewRate + QuickOffsetAmount;
            if (updated >= 0)
            {
                row.NewRate = updated;
            }
        }

        QuickOffsetAmount = 0;
    }

    private void ResetAll()
    {
        foreach (var row in _allRows)
        {
            row.Reset();
        }
        NotifyChangeStats();
    }

    private async Task SaveAsync()
    {
        var changedRows = _allRows.Where(r => r.IsChanged).ToList();
        if (changedRows.Count == 0)
        {
            ErrorMessage = "No rate changes to save.";
            return;
        }

        var result = MessageBox.Show(
            $"Do you want to update rates for {changedRows.Count} purity items?\n\nThis will record new rates in RateMaster log and update Purity table.",
            "Confirm Rate Update",
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

            var rateUpdates = changedRows.Select(r => new RateUpdateItem
            {
                Purity = r.Purity,
                OldRate = r.CurrentRate,
                NewRate = r.NewRate,
                ItemHead = r.ItemHead,
                ItemType = r.ItemType
            }).ToList();

            // 1. Update [PURITY] and insert [RateStored]
            await _purityRepository.UpdateRatesBatchAsync(rateUpdates, _currentUser);

            // 2. Audit logging
            if (_auditRepository != null)
            {
                await AuditService.LogRateUpdatesAsync(_auditRepository, rateUpdates, _currentUser);
            }

            RequestClose?.Invoke(this, true);
        }
        catch (Exception ex)
        {
            ErrorMessage = "Error updating rates: " + ex.Message;
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
