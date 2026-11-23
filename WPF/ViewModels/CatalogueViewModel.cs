using System;
using System.Collections.Generic;
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

namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class CatalogueViewModel : ViewModelBase
{
    private readonly ICatalogueRepository _catalogueRepository;
    private readonly IItemRepository? _itemRepository;
    private readonly ISettingsRepository? _settingsRepository;
    private readonly string _currentUser;

    private string _itemName = string.Empty;
    private string _size = string.Empty;
    private string _barcode = string.Empty;
    private string _description = string.Empty;
    private decimal? _fromWt;
    private decimal? _toWt;
    private bool _includeSold;

    private bool _isBusy;
    private string? _statusMessage;
    private string? _toastMessage;
    private ToastType _toastType = ToastType.Info;
    private CatalogueItem? _selectedItem;

    private decimal _totalGrossWt;
    private decimal _totalNetWt;
    private int _totalCount;
    private Dictionary<string, string>? _cachedImageIndex;

    public CatalogueViewModel(
        ICatalogueRepository catalogueRepository,
        IItemRepository? itemRepository,
        ISettingsRepository? settingsRepository,
        string currentUser)
    {
        _catalogueRepository = catalogueRepository;
        _itemRepository = itemRepository;
        _settingsRepository = settingsRepository;
        _currentUser = currentUser;

        SearchCommand = new AsyncRelayCommand(LoadCatalogueAsync, () => !IsBusy);
        RefreshCommand = SearchCommand;
        ClearFilterCommand = new RelayCommand(ClearFilters);
        ExportToExcelCommand = new RelayCommand(ExportToExcel);
        PrintCommand = new RelayCommand(PrintCatalogue);
        OpenBarcodeCommand = new RelayCommand(param =>
        {
            var item = param as CatalogueItem ?? SelectedItem;
            if (item != null && !string.IsNullOrWhiteSpace(item.Barcode))
            {
                OpenBarcode(item.Barcode);
            }
        });

        StatusMessage = "Select filter parameters (optional) and click 'Refresh' to load catalogue items.";
        _ = InitializeAsync();
    }

    public string ItemName
    {
        get => _itemName;
        set
        {
            if (SetProperty(ref _itemName, value))
            {
                OnPropertyChanged(nameof(SelectedItemName));
            }
        }
    }

    public string? SelectedItemName
    {
        get => _itemName;
        set
        {
            ItemName = value ?? string.Empty;
            OnPropertyChanged(nameof(SelectedItemName));
        }
    }

    public string Size
    {
        get => _size;
        set
        {
            if (SetProperty(ref _size, value))
            {
                OnPropertyChanged(nameof(SelectedSize));
            }
        }
    }

    public string? SelectedSize
    {
        get => _size;
        set
        {
            Size = value ?? string.Empty;
            OnPropertyChanged(nameof(SelectedSize));
        }
    }

    public string Barcode
    {
        get => _barcode;
        set => SetProperty(ref _barcode, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public decimal? FromWt
    {
        get => _fromWt;
        set => SetProperty(ref _fromWt, value);
    }

    public decimal? ToWt
    {
        get => _toWt;
        set => SetProperty(ref _toWt, value);
    }

    public bool IncludeSold
    {
        get => _includeSold;
        set => SetProperty(ref _includeSold, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                SearchCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
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

    public CatalogueItem? SelectedItem
    {
        get => _selectedItem;
        set => SetProperty(ref _selectedItem, value);
    }

    public decimal TotalGrossWt
    {
        get => _totalGrossWt;
        private set => SetProperty(ref _totalGrossWt, value);
    }

    public decimal TotalNetWt
    {
        get => _totalNetWt;
        private set => SetProperty(ref _totalNetWt, value);
    }

    public int TotalCount
    {
        get => _totalCount;
        private set => SetProperty(ref _totalCount, value);
    }

    public ObservableCollection<CatalogueItem> Items { get; } = new();
    public ObservableCollection<string> AvailableItemNames { get; } = new();
    public ObservableCollection<string> AvailableSizes { get; } = new();

    public AsyncRelayCommand SearchCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }
    public RelayCommand ClearFilterCommand { get; }
    public RelayCommand ExportToExcelCommand { get; }
    public RelayCommand PrintCommand { get; }
    public RelayCommand OpenBarcodeCommand { get; }

    public event EventHandler<string>? OpenBarcodeRequested;

    private async Task InitializeAsync()
    {
        await LoadAvailableFilterOptionsAsync();
    }

    public async Task LoadAvailableFilterOptionsAsync()
    {
        try
        {
            var itemNames = await _catalogueRepository.GetAvailableItemNamesAsync();
            AvailableItemNames.Clear();
            AvailableItemNames.Add(string.Empty);
            foreach (var n in itemNames)
            {
                if (!string.IsNullOrWhiteSpace(n))
                    AvailableItemNames.Add(n);
            }

            var sizes = await _catalogueRepository.GetAvailableSizesAsync();
            AvailableSizes.Clear();
            AvailableSizes.Add(string.Empty);
            foreach (var s in sizes)
            {
                if (!string.IsNullOrWhiteSpace(s))
                    AvailableSizes.Add(s);
            }
        }
        catch
        {
            // Non-critical background loading
        }
    }

    public async Task LoadCatalogueAsync()
    {
        IsBusy = true;
        StatusMessage = "Loading catalogue items...";
        try
        {
            var filter = new CatalogueFilter
            {
                ItemName = string.IsNullOrWhiteSpace(ItemName) ? null : ItemName.Trim(),
                Size = string.IsNullOrWhiteSpace(Size) ? null : Size.Trim(),
                Barcode = string.IsNullOrWhiteSpace(Barcode) ? null : Barcode.Trim(),
                Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
                FromWt = FromWt,
                ToWt = ToWt,
                IncludeSold = IncludeSold
            };

            var savePath = await GetSavePathAsync();
            var rawItems = await _catalogueRepository.GetCatalogueAsync(filter);

            await Task.Run(() =>
            {
                var imageIndex = GetBarcodeImageIndex(savePath);
                foreach (var itm in rawItems)
                {
                    itm.ImagePath = ResolveBarcodeImagePath(itm.Barcode, savePath, imageIndex);
                }
            });

            Items.Clear();
            decimal sumGr = 0;
            decimal sumNt = 0;

            foreach (var itm in rawItems)
            {
                Items.Add(itm);
                sumGr += itm.GrossWt;
                sumNt += itm.NetWt;
            }

            TotalCount = Items.Count;
            TotalGrossWt = sumGr;
            TotalNetWt = sumNt;

            if (Items.Count > 0)
            {
                SelectedItem = Items[0];
                StatusMessage = $"Showing {TotalCount} items | Gr Wt: {TotalGrossWt:F3}g | Nt Wt: {TotalNetWt:F3}g";
            }
            else
            {
                SelectedItem = null;
                StatusMessage = "No in-stock items found matching the selected filter parameters.";
            }
        }
        catch (Exception ex)
        {
            ShowToast("Failed to load catalogue: " + ex.Message, ToastType.Error);
            StatusMessage = "Error loading catalogue: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void ClearFilters()
    {
        _itemName = string.Empty;
        _size = string.Empty;
        _barcode = string.Empty;
        _description = string.Empty;
        _fromWt = null;
        _toWt = null;
        _includeSold = false;

        OnPropertyChanged(nameof(ItemName));
        OnPropertyChanged(nameof(SelectedItemName));
        OnPropertyChanged(nameof(Size));
        OnPropertyChanged(nameof(SelectedSize));
        OnPropertyChanged(nameof(Barcode));
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(FromWt));
        OnPropertyChanged(nameof(ToWt));
        OnPropertyChanged(nameof(IncludeSold));

        Items.Clear();
        TotalCount = 0;
        TotalGrossWt = 0;
        TotalNetWt = 0;
        SelectedItem = null;
        StatusMessage = "Filters cleared. Select filter parameters (optional) and click 'Refresh' to load catalogue items.";
    }

    public void OpenBarcode(string barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode)) return;
        OpenBarcodeRequested?.Invoke(this, barcode.Trim());
    }

    private async Task<string> GetSavePathAsync()
    {
        try
        {
            if (_settingsRepository is not null)
            {
                var val = await _settingsRepository.GetValueAsync(AppSetting.SavePathKey);
                if (!string.IsNullOrWhiteSpace(val))
                {
                    return val;
                }
            }
        }
        catch
        {
            // fallback
        }
        return string.Empty;
    }

    private Dictionary<string, string> GetBarcodeImageIndex(string? savePath)
    {
        if (_cachedImageIndex is not null) return _cachedImageIndex;
        _cachedImageIndex = BuildBarcodeImageIndex(savePath);
        return _cachedImageIndex;
    }

    private static Dictionary<string, string> BuildBarcodeImageIndex(string? savePath)
    {
        var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(savePath)) return index;

        var barcodeDir = Path.Combine(savePath, "Barcode");
        if (!Directory.Exists(barcodeDir))
        {
            barcodeDir = Path.Combine(savePath, "barcode");
            if (!Directory.Exists(barcodeDir))
            {
                barcodeDir = Path.Combine(savePath, "barcodes");
                if (!Directory.Exists(barcodeDir))
                {
                    barcodeDir = savePath;
                }
            }
        }

        if (!Directory.Exists(barcodeDir)) return index;

        try
        {
            foreach (var file in Directory.EnumerateFiles(barcodeDir))
            {
                var nameWithoutExt = Path.GetFileNameWithoutExtension(file);
                if (!index.ContainsKey(nameWithoutExt))
                {
                    index[nameWithoutExt] = file;
                }

                if (int.TryParse(nameWithoutExt, out var num))
                {
                    var padded = num.ToString("D6");
                    var unpadded = num.ToString();
                    if (!index.ContainsKey(padded)) index[padded] = file;
                    if (!index.ContainsKey(unpadded)) index[unpadded] = file;
                }

                var nameWithExt = Path.GetFileName(file);
                if (!index.ContainsKey(nameWithExt))
                {
                    index[nameWithExt] = file;
                }
            }
        }
        catch
        {
            // Ignore directory read exceptions
        }

        return index;
    }

    private static string? ResolveBarcodeImagePath(string barcode, string? savePath, Dictionary<string, string> imageIndex)
    {
        if (string.IsNullOrWhiteSpace(barcode)) return null;

        var code = barcode.Trim();
        var padded = int.TryParse(code, out var n) ? n.ToString("D6") : code;
        var unpadded = n.ToString();

        // 1. Direct match from prebuilt index of SavePath\Barcode
        if (imageIndex.Count > 0)
        {
            if (imageIndex.TryGetValue(code, out var foundBarcode)) return foundBarcode;
            if (imageIndex.TryGetValue(padded, out var foundPadded)) return foundPadded;
            if (imageIndex.TryGetValue(unpadded, out var foundUnpadded)) return foundUnpadded;
        }

        // 2. Direct disk check in SavePath\Barcode
        if (!string.IsNullOrWhiteSpace(savePath))
        {
            string[] dirs =
            {
                Path.Combine(savePath, "Barcode"),
                Path.Combine(savePath, "barcode"),
                Path.Combine(savePath, "barcodes"),
                savePath
            };

            string[] extensions = { ".jpg", ".jpeg", ".png", ".bmp", ".webp" };

            foreach (var dir in dirs)
            {
                if (!Directory.Exists(dir)) continue;

                foreach (var ext in extensions)
                {
                    var f1 = Path.Combine(dir, $"{code}{ext}");
                    if (File.Exists(f1)) return f1;

                    var f2 = Path.Combine(dir, $"{padded}{ext}");
                    if (File.Exists(f2)) return f2;

                    var f3 = Path.Combine(dir, $"{unpadded}{ext}");
                    if (File.Exists(f3)) return f3;
                }
            }
        }

        return null;
    }

    private void ExportToExcel()
    {
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV File (*.csv)|*.csv|All Files (*.*)|*.*",
                FileName = $"Catalogue_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (dialog.ShowDialog() == true)
            {
                var sb = new StringBuilder();
                sb.AppendLine("Barcode,ItemName,GrossWt,NetWt,StoneWt,Purity,Size,Description,Photo");
                foreach (var i in Items)
                {
                    sb.AppendLine($"\"{i.Barcode}\",\"{i.ItemName}\",{i.GrossWt:F3},{i.NetWt:F3},{i.StoneWt:F3},\"{i.Purity}\",\"{i.Size}\",\"{i.Description}\",\"{i.Photo}\"");
                }
                File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
                ShowToast("Catalogue exported to CSV successfully", ToastType.Success);
            }
        }
        catch (Exception ex)
        {
            ShowToast("Export failed: " + ex.Message, ToastType.Error);
        }
    }

    private void PrintCatalogue()
    {
        ShowToast($"Preparing catalogue report for {TotalCount} items...", ToastType.Info);
    }

    private void ShowToast(string message, ToastType type)
    {
        ToastMessage = message;
        ToastType = type;
    }
}
