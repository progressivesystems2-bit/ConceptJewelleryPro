using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Printing;
using System.Windows.Input;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.Infrastructure.Printing;
using ConceptJewelleryPro.Infrastructure.Settings;
using ConceptJewelleryPro.Shared;
using ConceptJewelleryPro.WPF.Controls;

namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class PrinterSettingsViewModel : ViewModelBase
{
    private readonly LocalPrinterSettingsStore _store;
    private readonly ISettingsRepository? _settingsRepository;
    private readonly string _currentUser;

    private string _defaultBillPrinter = string.Empty;
    private bool _billPrintPreviewBeforePrint = true;
    private string _defaultBarcodePrinter = string.Empty;
    private bool _barcodePrintPreviewBeforePrint;
    private string _barcodePrintEngine = "PRN";
    private bool _hideWtInPcItem = true;
    private bool _isBusy;
    private string? _toastMessage;
    private ToastType _toastType = ToastType.Info;
    private string _statusMessage = string.Empty;

    public PrinterSettingsViewModel(
        LocalPrinterSettingsStore store,
        string currentUser = "admin",
        ISettingsRepository? settingsRepository = null)
    {
        _store = store;
        _currentUser = currentUser;
        _settingsRepository = settingsRepository;

        AvailablePrinters = new ObservableCollection<string>();
        BarcodePrintEngineOptions = new ObservableCollection<string> { "PRN", "FRX" };

        SaveCommand = new RelayCommand(SaveSettings);
        RefreshCommand = new RelayCommand(LoadSettings);
        RefreshPrintersCommand = new RelayCommand(PopulatePrinters);
        ClearBillPrinterCommand = new RelayCommand(() => DefaultBillPrinter = string.Empty);
        ClearBarcodePrinterCommand = new RelayCommand(() => DefaultBarcodePrinter = string.Empty);
        SelectPrnEngineCommand = new RelayCommand(() => BarcodePrintEngine = "PRN");
        SelectFrxEngineCommand = new RelayCommand(() => BarcodePrintEngine = "FRX");
        OpenTemplatesFolderCommand = new RelayCommand(OpenTemplatesFolder);

        LoadSettings();
    }

    public ObservableCollection<string> AvailablePrinters { get; }

    public ObservableCollection<string> BarcodePrintEngineOptions { get; }

    public ICommand SaveCommand { get; }

    public ICommand RefreshCommand { get; }

    public ICommand RefreshPrintersCommand { get; }

    public ICommand ClearBillPrinterCommand { get; }

    public ICommand ClearBarcodePrinterCommand { get; }

    public ICommand SelectPrnEngineCommand { get; }

    public ICommand SelectFrxEngineCommand { get; }

    public ICommand OpenTemplatesFolderCommand { get; }

    public string DefaultBillPrinter
    {
        get => _defaultBillPrinter;
        set => SetProperty(ref _defaultBillPrinter, value);
    }

    public bool BillPrintPreviewBeforePrint
    {
        get => _billPrintPreviewBeforePrint;
        set => SetProperty(ref _billPrintPreviewBeforePrint, value);
    }

    public string DefaultBarcodePrinter
    {
        get => _defaultBarcodePrinter;
        set => SetProperty(ref _defaultBarcodePrinter, value);
    }

    public bool BarcodePrintPreviewBeforePrint
    {
        get => _barcodePrintPreviewBeforePrint;
        set => SetProperty(ref _barcodePrintPreviewBeforePrint, value);
    }

    public string BarcodePrintEngine
    {
        get => _barcodePrintEngine;
        set
        {
            var normalized = BarcodePrintService.NormalizeEngine(value);
            if (SetProperty(ref _barcodePrintEngine, normalized))
            {
                OnPropertyChanged(nameof(IsPrnEngineSelected));
                OnPropertyChanged(nameof(IsFrxEngineSelected));
            }
        }
    }

    public bool IsPrnEngineSelected
    {
        get => string.Equals(_barcodePrintEngine, "PRN", StringComparison.OrdinalIgnoreCase);
        set
        {
            if (value) BarcodePrintEngine = "PRN";
        }
    }

    public bool IsFrxEngineSelected
    {
        get => string.Equals(_barcodePrintEngine, "FRX", StringComparison.OrdinalIgnoreCase);
        set
        {
            if (value) BarcodePrintEngine = "FRX";
        }
    }

    public bool HideWtInPcItem
    {
        get => _hideWtInPcItem;
        set => SetProperty(ref _hideWtInPcItem, value);
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

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public void LoadSettings()
    {
        IsBusy = true;
        try
        {
            PopulatePrinters();

            var loaded = _store.Load();
            DefaultBillPrinter = loaded.DefaultBillPrinter ?? string.Empty;
            BillPrintPreviewBeforePrint = loaded.BillPrintPreviewBeforePrint;
            DefaultBarcodePrinter = loaded.DefaultBarcodePrinter ?? string.Empty;
            BarcodePrintPreviewBeforePrint = loaded.BarcodePrintPreviewBeforePrint;
            BarcodePrintEngine = BarcodePrintService.NormalizeEngine(loaded.BarcodePrintEngine);
            HideWtInPcItem = loaded.HideWtInPcItem;

            if (!string.IsNullOrWhiteSpace(DefaultBillPrinter) && !AvailablePrinters.Contains(DefaultBillPrinter))
            {
                AvailablePrinters.Add(DefaultBillPrinter);
            }

            if (!string.IsNullOrWhiteSpace(DefaultBarcodePrinter) && !AvailablePrinters.Contains(DefaultBarcodePrinter))
            {
                AvailablePrinters.Add(DefaultBarcodePrinter);
            }

            StatusMessage = "Printer settings loaded.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Failed to load printer settings.";
            ShowToast($"Failed to load printer settings: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void SaveSettings()
    {
        IsBusy = true;
        try
        {
            var existing = _store.Load();
            var normalizedEngine = BarcodePrintService.NormalizeEngine(BarcodePrintEngine);

            var settings = new LocalPrinterSettings
            {
                DefaultBillPrinter = DefaultBillPrinter?.Trim() ?? string.Empty,
                BillPrintPreviewBeforePrint = BillPrintPreviewBeforePrint,
                DefaultBarcodePrinter = DefaultBarcodePrinter?.Trim() ?? string.Empty,
                BarcodePrintPreviewBeforePrint = BarcodePrintPreviewBeforePrint,
                BarcodePrintEngine = normalizedEngine,
                HideWtInPcItem = HideWtInPcItem,
                SoftwarePath = existing.SoftwarePath ?? string.Empty,
                PrintFiles = string.IsNullOrWhiteSpace(existing.PrintFiles) ? FastReportPrintService.DefaultPrintFilesPath : existing.PrintFiles,
                SavePath = existing.SavePath ?? string.Empty
            };

            _store.Save(settings);

            if (_settingsRepository != null)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _settingsRepository.SaveAsync(AppSetting.BarcodePrintEngineKey, normalizedEngine);
                        if (!string.IsNullOrWhiteSpace(settings.DefaultBarcodePrinter))
                        {
                            await _settingsRepository.SaveAsync(AppSetting.DefaultBarcodePrinterKey, settings.DefaultBarcodePrinter);
                        }
                    }
                    catch { }
                });
            }

            StatusMessage = $"Printer settings saved locally (Barcode Mode: {normalizedEngine}).";
            ShowToast($"Printer settings saved! Barcode printing mode set to {normalizedEngine} File.", ToastType.Success);
        }
        catch (Exception ex)
        {
            StatusMessage = "Error saving printer settings.";
            ShowToast($"Error saving printer settings: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OpenTemplatesFolder()
    {
        try
        {
            var subFolder = IsFrxEngineSelected ? "BarcodeFRX" : "BarcodePRN";
            var folder = Path.Combine(AppContext.BaseDirectory, "PrintTemplates", subFolder);
            if (!Directory.Exists(folder))
            {
                folder = Path.Combine(AppContext.BaseDirectory, "PrintTemplates");
            }
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ShowToast($"Could not open templates folder: {ex.Message}", ToastType.Error);
        }
    }

    private void PopulatePrinters()
    {
        var existingSelectedBill = DefaultBillPrinter;
        var existingSelectedBarcode = DefaultBarcodePrinter;

        AvailablePrinters.Clear();

        try
        {
            using var printServer = new LocalPrintServer();
            var printQueues = printServer.GetPrintQueues(new[]
            {
                EnumeratedPrintQueueTypes.Local,
                EnumeratedPrintQueueTypes.Connections
            });

            foreach (var pq in printQueues.OrderBy(p => p.FullName))
            {
                if (!string.IsNullOrWhiteSpace(pq.FullName) && !AvailablePrinters.Contains(pq.FullName))
                {
                    AvailablePrinters.Add(pq.FullName);
                }
            }
        }
        catch
        {
            // Fallback
        }

        if (!string.IsNullOrWhiteSpace(existingSelectedBill) && !AvailablePrinters.Contains(existingSelectedBill))
        {
            AvailablePrinters.Add(existingSelectedBill);
        }

        if (!string.IsNullOrWhiteSpace(existingSelectedBarcode) && !AvailablePrinters.Contains(existingSelectedBarcode))
        {
            AvailablePrinters.Add(existingSelectedBarcode);
        }
    }

    private void ShowToast(string message, ToastType type = ToastType.Info)
    {
        ToastType = type;
        ToastMessage = message;
    }
}
