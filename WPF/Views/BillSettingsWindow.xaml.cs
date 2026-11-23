using System.Collections.ObjectModel;
using System.Printing;
using System.Windows;
using System.Windows.Input;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.Infrastructure.Settings;
using ConceptJewelleryPro.Shared;

namespace ConceptJewelleryPro.WPF.Views;

public partial class BillSettingsWindow : Window
{
    private readonly LocalPrinterSettingsStore _printerStore;
    private readonly ISettingsRepository? _settingsRepository;
    private readonly ObservableCollection<string> _availablePrinters = new();

    public BillSettingsWindow(ISettingsRepository? settingsRepository = null)
    {
        InitializeComponent();
        _printerStore = new LocalPrinterSettingsStore();
        _settingsRepository = settingsRepository;

        BillPrinterComboBox.ItemsSource = _availablePrinters;
        Loaded += OnLoaded;
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        PopulatePrinters();
        await LoadSettingsAsync();
    }

    private void PopulatePrinters()
    {
        var currentSelected = BillPrinterComboBox.SelectedItem?.ToString();
        _availablePrinters.Clear();

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
                if (!string.IsNullOrWhiteSpace(pq.FullName) && !_availablePrinters.Contains(pq.FullName))
                {
                    _availablePrinters.Add(pq.FullName);
                }
            }
        }
        catch
        {
            // Fallback for restricted printer enumeration
        }

        if (!string.IsNullOrWhiteSpace(currentSelected) && !_availablePrinters.Contains(currentSelected))
        {
            _availablePrinters.Add(currentSelected);
            BillPrinterComboBox.SelectedItem = currentSelected;
        }
    }

    private void RefreshPrintersButton_Click(object sender, RoutedEventArgs e)
    {
        PopulatePrinters();
    }

    private async Task LoadSettingsAsync()
    {
        try
        {
            // 1. Load from local printer store
            var localSettings = _printerStore.Load();
            var billPrinter = localSettings.DefaultBillPrinter;
            var hideWt = localSettings.HideWtInPcItem;

            // 2. If settings repo available, load DB settings
            bool autoSendWa = false;
            string allowNtWt = "0.000";
            if (_settingsRepository != null)
            {
                var dbSettings = await _settingsRepository.GetAllAsync();

                if (dbSettings.TryGetValue(AppSetting.WaAutoSendKey, out var autoSendStr))
                {
                    autoSendWa = bool.TryParse(autoSendStr, out var b) && b;
                }

                if (dbSettings.TryGetValue(AppSetting.HideWtInPcItemsKey, out var hideWtStr) && bool.TryParse(hideWtStr, out var hw))
                {
                    hideWt = hw;
                }

                if (dbSettings.TryGetValue(AppSetting.AllowNtWtModUptoKey, out var allowModStr) && !string.IsNullOrWhiteSpace(allowModStr))
                {
                    allowNtWt = allowModStr;
                }

                if (string.IsNullOrWhiteSpace(billPrinter) && dbSettings.TryGetValue(AppSetting.DefaultBillPrinterKey, out var dbPrinter) && !string.IsNullOrWhiteSpace(dbPrinter))
                {
                    billPrinter = dbPrinter;
                }
            }

            if (!string.IsNullOrWhiteSpace(billPrinter))
            {
                if (!_availablePrinters.Contains(billPrinter))
                {
                    _availablePrinters.Add(billPrinter);
                }
                BillPrinterComboBox.SelectedItem = billPrinter;
            }

            AutoSendWhatsAppCheckBox.IsChecked = autoSendWa;
            HideWtInPcItemsCheckBox.IsChecked = hideWt;
            AllowNtWtModUptoTextBox.Text = allowNtWt;
        }
        catch
        {
            // Fallback defaults
        }
    }

    private async void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var selectedPrinter = BillPrinterComboBox.SelectedItem?.ToString() ?? string.Empty;
            bool autoSendWa = AutoSendWhatsAppCheckBox.IsChecked == true;
            bool hideWt = HideWtInPcItemsCheckBox.IsChecked == true;
            var allowNtWt = AllowNtWtModUptoTextBox.Text?.Trim() ?? "0.000";

            // 1. Save to local printer store
            var localSettings = _printerStore.Load();
            localSettings.DefaultBillPrinter = selectedPrinter;
            localSettings.HideWtInPcItem = hideWt;
            _printerStore.Save(localSettings);

            // 2. Save to db.settings if repository is available
            if (_settingsRepository != null)
            {
                var dict = new Dictionary<string, string?>
                {
                    { AppSetting.DefaultBillPrinterKey, selectedPrinter },
                    { AppSetting.WaAutoSendKey, autoSendWa ? "true" : "false" },
                    { AppSetting.HideWtInPcItemsKey, hideWt ? "true" : "false" },
                    { AppSetting.AllowNtWtModUptoKey, allowNtWt }
                };

                await _settingsRepository.SaveManyAsync(dict);
            }

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Error saving settings: {ex.Message}", "Save Settings Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
