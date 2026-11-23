using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.WPF.Controls;
using ConceptJewelleryPro.WPF.Helpers;
using ConceptJewelleryPro.WPF.Services;
using ConceptJewelleryPro.WPF.Views;

namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class BoxMasterViewModel : ViewModelBase, IRefreshable
{
    private readonly IBoxMasterRepository _repository;
    private readonly string _currentUser;
    private readonly List<BoxMaster> _allBoxes = new();

    private BoxMaster? _selectedBox;
    private string _searchText = string.Empty;
    private string _selectedRack = "*";
    private bool _isBusy;
    private string? _toastMessage;
    private ToastType _toastType = ToastType.Info;
    private string _statusMessage = string.Empty;
    private bool? _isAllSelected = false;
    private bool _isUpdatingAllSelected;

    public event EventHandler<BoxMaster?>? BoxSelected;

    public BoxMasterViewModel(
        IBoxMasterRepository repository,
        string currentUser = "admin")
    {
        _repository = repository;
        _currentUser = currentUser;

        Boxes = new ObservableCollection<BoxMaster>();
        Racks = new ObservableCollection<string> { "*" };

        AddCommand = new RelayCommand(AddNew);
        EditCommand = new RelayCommand(EditSelected, () => SelectedBox is not null);
        DeleteCommand = new RelayCommand(async () => await DeleteSelectedAsync(), () => SelectedBox is not null);
        RefreshCommand = new RelayCommand(async () => await LoadDataAsync());
        PrintCommand = new RelayCommand(PrintSelected);
        SelectBoxCommand = new RelayCommand(ConfirmSelection, () => SelectedBox is not null);

        _ = LoadDataAsync();
    }

    public ObservableCollection<BoxMaster> Boxes { get; }

    public ObservableCollection<string> Racks { get; }

    public ICommand AddCommand { get; }

    public ICommand EditCommand { get; }

    public ICommand DeleteCommand { get; }

    public ICommand RefreshCommand { get; }

    public ICommand PrintCommand { get; }

    public ICommand SelectBoxCommand { get; }

    public async Task RefreshAsync() => await LoadDataAsync();


    public BoxMaster? SelectedBox
    {
        get => _selectedBox;
        set
        {
            if (SetProperty(ref _selectedBox, value))
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

    public string SelectedRack
    {
        get => _selectedRack;
        set
        {
            if (SetProperty(ref _selectedRack, value))
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
        private set => SetProperty(ref _statusMessage, value);
    }

    public int TotalBoxesCount => _allBoxes.Count;

    public bool? IsAllSelected
    {
        get => _isAllSelected;
        set
        {
            if (SetProperty(ref _isAllSelected, value))
            {
                if (!_isUpdatingAllSelected && value.HasValue)
                {
                    foreach (var box in Boxes)
                    {
                        box.IsSelected = value.Value;
                    }
                    OnPropertyChanged(nameof(SelectedBoxesCount));
                }
            }
        }
    }

    public int SelectedBoxesCount => Boxes.Count(b => b.IsSelected);

    public async Task LoadDataAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Loading box masters...";

            var list = await _repository.GetAllAsync();
            _allBoxes.Clear();
            _allBoxes.AddRange(list);

            var racks = await _repository.GetRacksAsync();
            Racks.Clear();
            Racks.Add("*");
            foreach (var r in racks)
            {
                if (!string.IsNullOrWhiteSpace(r) && !Racks.Contains(r))
                    Racks.Add(r);
            }

            ApplyFilter();
            StatusMessage = $"Loaded {_allBoxes.Count} boxes.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading boxes: {ex.Message}";
            ShowToast(StatusMessage, ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFilter()
    {
        foreach (var b in Boxes)
        {
            b.PropertyChanged -= Box_PropertyChanged;
        }

        Boxes.Clear();

        var query = _allBoxes.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SelectedRack) && SelectedRack != "*")
        {
            query = query.Where(b => string.Equals(b.Rack, SelectedRack, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var term = SearchText.Trim();
            query = query.Where(b =>
                b.BoxNo.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                b.BoxName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                b.Rack.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                b.Remarks.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var b in query.OrderBy(b => b.Serial))
        {
            b.PropertyChanged += Box_PropertyChanged;
            Boxes.Add(b);
        }

        OnPropertyChanged(nameof(TotalBoxesCount));
        OnPropertyChanged(nameof(SelectedBoxesCount));
        UpdateIsAllSelectedState();
    }

    private void Box_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BoxMaster.IsSelected))
        {
            OnPropertyChanged(nameof(SelectedBoxesCount));
            UpdateIsAllSelectedState();
        }
    }

    private void UpdateIsAllSelectedState()
    {
        if (Boxes.Count == 0)
        {
            _isUpdatingAllSelected = true;
            IsAllSelected = false;
            _isUpdatingAllSelected = false;
            return;
        }

        int selectedCount = Boxes.Count(b => b.IsSelected);
        _isUpdatingAllSelected = true;
        if (selectedCount == Boxes.Count)
        {
            IsAllSelected = true;
        }
        else if (selectedCount == 0)
        {
            IsAllSelected = false;
        }
        else
        {
            IsAllSelected = null;
        }
        _isUpdatingAllSelected = false;
    }


    public void AddNew()
    {
        var editVm = new BoxEditViewModel(_repository, null, _currentUser, Racks.Where(r => r != "*").ToList());
        var win = new BoxEditWindow(editVm);
        win.SetOwnerSafely();

        if (win.ShowDialog() == true)
        {
            _ = LoadDataAsync();
            if (editVm.ResultBox != null)
            {
                SelectedBox = editVm.ResultBox;
            }
        }
    }

    public void EditSelected()
    {
        if (SelectedBox is null) return;

        var editVm = new BoxEditViewModel(_repository, SelectedBox, _currentUser, Racks.Where(r => r != "*").ToList());
        var win = new BoxEditWindow(editVm);
        win.SetOwnerSafely();

        if (win.ShowDialog() == true)
        {
            _ = LoadDataAsync();
        }
    }

    public async Task DeleteSelectedAsync()
    {
        if (SelectedBox is null) return;

        var boxToDelete = SelectedBox;
        var confirm = MessageBox.Show(
            $"Are you sure you want to delete box '{boxToDelete.BoxNo} - {boxToDelete.BoxName}'?",
            "Confirm Delete Box",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            IsBusy = true;
            await _repository.DeleteAsync(boxToDelete.BoxNo);
            ShowToast($"Box '{boxToDelete.BoxNo}' deleted.", ToastType.Success);
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            ShowToast($"Error deleting box: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void PrintSelected()

    {
        var selected = Boxes.Where(b => b.IsSelected).ToList();
        if (selected.Count == 0 && SelectedBox != null)
        {
            selected.Add(SelectedBox);
        }

        if (selected.Count == 0)
        {
            ShowToast("Please select at least one box to print.", ToastType.Warning);
            return;
        }

        try
        {
            var doc = BoxMasterFlowDocumentBuilder.CreateDocument(selected);
            var preview = new InvoicePrintPreviewWindow(
                document: doc,
                windowTitle: "Box Master Label Print Preview",
                headerTitle: $"Box Master Labels (A4 - {selected.Count} box{(selected.Count > 1 ? "es" : "")})",
                docName: "BoxMasterLabels");
            preview.SetOwnerSafely();
            preview.ShowDialog();

            ShowToast($"Generated print preview for {selected.Count} box label{(selected.Count > 1 ? "s" : "")}.", ToastType.Success);
        }
        catch (Exception ex)
        {
            ShowToast($"Error generating print preview: {ex.Message}", ToastType.Error);
        }
    }

    private void ConfirmSelection()
    {
        if (SelectedBox is not null)
        {
            BoxSelected?.Invoke(this, SelectedBox);
        }
    }

    private void ShowToast(string message, ToastType type)
    {
        ToastMessage = message;
        ToastType = type;
    }
}
