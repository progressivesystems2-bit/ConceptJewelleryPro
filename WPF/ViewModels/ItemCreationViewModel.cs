using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.Shared;
using ConceptJewelleryPro.WPF.Controls;
using ConceptJewelleryPro.WPF.Helpers;
using ConceptJewelleryPro.WPF.Views;

namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class ItemCreationViewModel : ViewModelBase, IRefreshable
{
    private readonly IItemRepository _itemRepository;
    private readonly string _currentUser;

    private string _searchText = string.Empty;
    private bool _isBusy;
    private string? _statusMessage;
    private string? _errorMessage;
    private string? _toastMessage;
    private ToastType _toastType = ToastType.Info;
    private Item? _selectedItem;

    public ItemCreationViewModel(IItemRepository itemRepository, string currentUser)
    {
        _itemRepository = itemRepository;
        _currentUser = currentUser;

        AddNewCommand = new AsyncRelayCommand(AddNewAsync, () => !IsBusy);
        ModifyCommand = new AsyncRelayCommand(ModifyAsync, () => !IsBusy);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => !IsBusy);
        ExportToExcelCommand = new RelayCommand(ExportToExcel);
        PrintFormatCommand = new RelayCommand(param => PrintFormat(param as string));

        _ = LoadItemsAsync();
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

    public AsyncRelayCommand AddNewCommand { get; }
    public AsyncRelayCommand ModifyCommand { get; }
    public AsyncRelayCommand DeleteCommand { get; }
    public RelayCommand ExportToExcelCommand { get; }
    public RelayCommand PrintFormatCommand { get; }

    public ObservableCollection<Item> Items { get; } = new();
    public ObservableCollection<Item> FilteredItems { get; } = new();

    public Item? SelectedItem
    {
        get => _selectedItem;
        set => SetProperty(ref _selectedItem, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                AddNewCommand.RaiseCanExecuteChanged();
                ModifyCommand.RaiseCanExecuteChanged();
                DeleteCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
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

    public void ResetSelection()
    {
        _searchText = string.Empty;
        OnPropertyChanged(nameof(SearchText));
        SelectedItem = null;
        ApplyFilter();
    }

    public async Task LoadItemsAsync()
    {
        IsBusy = true;
        try
        {
            var list = await _itemRepository.GetAllAsync();
            Items.Clear();
            foreach (var itm in list)
            {
                Items.Add(itm);
            }
            ApplyFilter();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to load items: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task RefreshAsync() => await LoadItemsAsync();

    private void ApplyFilter()
    {
        FilteredItems.Clear();
        var query = SearchText?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(query))
        {
            foreach (var itm in Items)
            {
                FilteredItems.Add(itm);
            }
            return;
        }

        foreach (var itm in Items)
        {
            if ((itm.ItemName?.Contains(query, StringComparison.OrdinalIgnoreCase) == true) ||
                (itm.ItemCode?.Contains(query, StringComparison.OrdinalIgnoreCase) == true) ||
                (itm.Group?.Contains(query, StringComparison.OrdinalIgnoreCase) == true) ||
                (itm.SubGroup?.Contains(query, StringComparison.OrdinalIgnoreCase) == true) ||
                (itm.MetalType?.Contains(query, StringComparison.OrdinalIgnoreCase) == true) ||
                (itm.HSNCode?.Contains(query, StringComparison.OrdinalIgnoreCase) == true) ||
                (itm.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) == true))
            {
                FilteredItems.Add(itm);
            }
        }
    }

    private async Task AddNewAsync()
    {
        var maxCode = Items
            .Select(i => int.TryParse(i.ItemCode, out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max();
        var suggestedCode = maxCode > 0 ? (maxCode + 1).ToString() : "1";

        var existingGroups = Items
            .Select(i => !string.IsNullOrWhiteSpace(i.Group) ? i.Group : i.HeadGroup)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var existingSubGroups = Items
            .Select(i => i.SubGroup)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var existingPurities = Items
            .Select(i => i.RawPurity)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var vm = new ItemEditViewModel(
            _itemRepository,
            _currentUser,
            itemToEdit: null,
            suggestedItemCode: suggestedCode,
            existingGroups: existingGroups,
            existingSubGroups: existingSubGroups,
            existingPurities: existingPurities);

        var win = new ItemEditWindow(vm)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        if (win.ShowDialog() == true)
        {
            await LoadItemsAsync();
            ShowToast("Item created successfully", ToastType.Success);
        }
    }

    private async Task ModifyAsync()
    {
        if (SelectedItem == null)
        {
            ShowToast("Please select an item to modify", ToastType.Warning);
            return;
        }

        var existingGroups = Items
            .Select(i => !string.IsNullOrWhiteSpace(i.Group) ? i.Group : i.HeadGroup)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var existingSubGroups = Items
            .Select(i => i.SubGroup)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var existingPurities = Items
            .Select(i => i.RawPurity)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var vm = new ItemEditViewModel(
            _itemRepository,
            _currentUser,
            itemToEdit: SelectedItem,
            existingGroups: existingGroups,
            existingSubGroups: existingSubGroups,
            existingPurities: existingPurities);

        var win = new ItemEditWindow(vm)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        if (win.ShowDialog() == true)
        {
            await LoadItemsAsync();
            ShowToast("Item updated successfully", ToastType.Success);
        }
    }

    private async Task DeleteAsync()
    {
        if (SelectedItem == null)
        {
            ShowToast("Please select an item to delete", ToastType.Warning);
            return;
        }

        var res = MessageBox.Show(
            $"Are you sure you want to delete item '{SelectedItem.ItemName}' ({SelectedItem.ItemCode})?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (res == MessageBoxResult.Yes)
        {
            IsBusy = true;
            try
            {
                var itemToDelete = SelectedItem;
                await _itemRepository.DeleteAsync(itemToDelete.ItemCode);

                await LoadItemsAsync();
                ShowToast("Item deleted successfully", ToastType.Success);
            }
            catch (Exception ex)
            {
                ShowToast("Failed to delete item: " + ex.Message, ToastType.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }

    public async Task SelectItemByCodeAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return;

        SearchText = code;
        var match = FilteredItems.FirstOrDefault(i => string.Equals(i.ItemCode, code, StringComparison.OrdinalIgnoreCase));
        if (match != null)
        {
            SelectedItem = match;
            await ModifyAsync();
        }
    }

    private void ExportToExcel()
    {
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV File (*.csv)|*.csv|All Files (*.*)|*.*",
                FileName = $"Items_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (dialog.ShowDialog() == true)
            {
                var sb = new StringBuilder();
                sb.AppendLine("ItemCode,ItemName,Group,SubGroup,Unit,MetalType,Purity,StandardWeight,MinWeight,MaxWeight,MRP,SaleRate,PurchaseRate,OpeningStock,ReorderLevel,HSNCode,GSTRate,IsActive,Allowed,Status");
                foreach (var i in FilteredItems)
                {
                    sb.AppendLine($"\"{i.ItemCode}\",\"{i.ItemName}\",\"{i.Group}\",\"{i.SubGroup}\",\"{i.Unit}\",\"{i.MetalType}\",{i.Purity},{i.StandardWeight},{i.MinWeight},{i.MaxWeight},{i.MRP},{i.SaleRate},{i.PurchaseRate},{i.OpeningStock},{i.ReorderLevel},\"{i.HSNCode}\",{i.GSTRate},{i.IsActive},{i.Allowed},\"{i.Status}\"");
                }
                File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
                ShowToast("Exported to CSV successfully", ToastType.Success);
            }
        }
        catch (Exception ex)
        {
            ShowToast("Export failed: " + ex.Message, ToastType.Error);
        }
    }

    private void PrintFormat(string? format)
    {
        var fmt = format ?? "All Items Summary";
        ShowToast($"Printing in format: {fmt}", ToastType.Info);
    }

    private void ShowToast(string message, ToastType type)
    {
        ToastMessage = message;
        ToastType = type;
    }
}