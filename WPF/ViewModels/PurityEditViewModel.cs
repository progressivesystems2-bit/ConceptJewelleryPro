using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.WPF.Controls;

namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class PurityEditViewModel : ViewModelBase
{
    private readonly IPurityRepository _purityRepository;
    private readonly string _currentUser;
    private readonly string? _originalPurity;

    private string _purity = string.Empty;
    private decimal _ratePerGram;
    private string _itemHead = string.Empty;
    private string _itemType = string.Empty;
    private bool _hide;
    private bool _isBusy;
    private string? _toastMessage;
    private ToastType _toastType = ToastType.Info;
    private string _errorMessage = string.Empty;

    public event EventHandler<bool?>? RequestClose;

    public PurityEditViewModel(
        IPurityRepository purityRepository,
        PurityMaster? existingPurity = null,
        string currentUser = "admin",
        IReadOnlyList<string>? preloadedItemHeads = null,
        IReadOnlyList<string>? preloadedItemTypes = null)
    {
        _purityRepository = purityRepository;
        _currentUser = currentUser;

        AvailableItemHeads = new ObservableCollection<string>();
        AvailableItemTypes = new ObservableCollection<string>();

        if (preloadedItemHeads is not null)
        {
            foreach (var head in preloadedItemHeads) AvailableItemHeads.Add(head);
        }

        if (preloadedItemTypes is not null)
        {
            foreach (var type in preloadedItemTypes) AvailableItemTypes.Add(type);
        }

        if (existingPurity is not null)
        {
            _originalPurity = existingPurity.Purity;
            _purity = existingPurity.Purity;
            _ratePerGram = existingPurity.RatePerGram;
            _itemHead = existingPurity.ItemHead;
            _itemType = existingPurity.ItemType;
            _hide = existingPurity.Hide;
            Title = $"Modify Purity - {existingPurity.Purity}";
            IsNew = false;
        }
        else
        {
            Title = "Add New Purity";
            IsNew = true;
            _ratePerGram = 0m;
            _hide = false;
        }

        SaveCommand = new RelayCommand(async () => await SaveAsync());
        CancelCommand = new RelayCommand(Cancel);

        _ = LoadLookupsAsync();
    }

    public string Title { get; }

    public bool IsNew { get; }

    public ObservableCollection<string> AvailableItemHeads { get; }

    public ObservableCollection<string> AvailableItemTypes { get; }

    public ICommand SaveCommand { get; }

    public ICommand CancelCommand { get; }

    public string Purity
    {
        get => _purity;
        set => SetProperty(ref _purity, value);
    }

    public decimal RatePerGram
    {
        get => _ratePerGram;
        set => SetProperty(ref _ratePerGram, value);
    }

    public string ItemHead
    {
        get => _itemHead;
        set => SetProperty(ref _itemHead, value);
    }

    public string ItemType
    {
        get => _itemType;
        set => SetProperty(ref _itemType, value);
    }

    public bool Hide
    {
        get => _hide;
        set => SetProperty(ref _hide, value);
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

    private async Task LoadLookupsAsync()
    {
        try
        {
            if (AvailableItemHeads.Count == 0)
            {
                var heads = await _purityRepository.GetItemHeadsAsync();
                AvailableItemHeads.Clear();
                foreach (var h in heads) AvailableItemHeads.Add(h);
            }

            if (AvailableItemTypes.Count == 0)
            {
                var types = await _purityRepository.GetItemTypesAsync();
                AvailableItemTypes.Clear();
                foreach (var t in types) AvailableItemTypes.Add(t);
            }

            // Ensure current values exist in lists
            if (!string.IsNullOrWhiteSpace(ItemHead) && !AvailableItemHeads.Contains(ItemHead))
            {
                AvailableItemHeads.Insert(0, ItemHead);
            }
            if (!string.IsNullOrWhiteSpace(ItemType) && !AvailableItemTypes.Contains(ItemType))
            {
                AvailableItemTypes.Insert(0, ItemType);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to load item heads/types: " + ex.Message;
        }
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Purity))
        {
            ErrorMessage = "Purity name is required.";
            return;
        }

        var result = MessageBox.Show(
            "Do you want to Save?",
            "Confirmation",
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

            var entity = new PurityMaster
            {
                Purity = Purity.Trim(),
                RatePerGram = RatePerGram,
                ItemHead = ItemHead?.Trim() ?? string.Empty,
                ItemType = ItemType?.Trim() ?? string.Empty,
                Hide = Hide
            };

            if (IsNew || string.IsNullOrWhiteSpace(_originalPurity))
            {
                var existing = await _purityRepository.GetByPurityAsync(entity.Purity);
                if (existing is not null)
                {
                    ErrorMessage = $"Purity '{entity.Purity}' already exists.";
                    return;
                }

                await _purityRepository.AddAsync(entity);
            }
            else
            {
                await _purityRepository.UpdateAsync(_originalPurity, entity);
            }

            RequestClose?.Invoke(this, true);
        }
        catch (Exception ex)
        {
            ErrorMessage = "Error saving purity: " + ex.Message;
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
