using System;
using System.IO;
using System.Text;
using System.Windows;
using ConceptJewelleryPro.Domain.Entities.GST;
using ConceptJewelleryPro.Infrastructure.Services;
using Microsoft.Win32;

namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class Gstr3BExportViewModel : ViewModelBase
{
    private readonly Gstr3BReport _report;
    private string _gstin;
    private string _returnPeriod;
    private string _jsonPreview = string.Empty;
    private string? _validationMessage;
    private bool _hasErrors;
    private string? _copyStatus;

    private string _companyName;
    private string _gstSourceInfo = "Auto-detected from DateRange table";

    public event EventHandler? RequestClose;

    public Gstr3BExportViewModel(Gstr3BReport report, string defaultGstin, string? defaultReturnPeriod = null, string? companyName = null)
    {
        _report = report ?? throw new ArgumentNullException(nameof(report));
        _companyName = !string.IsNullOrWhiteSpace(companyName) ? companyName : (!string.IsNullOrWhiteSpace(report.LegalName) ? report.LegalName : "TATTVA JEWELS");
        _gstin = !string.IsNullOrWhiteSpace(defaultGstin) ? defaultGstin.Trim().ToUpperInvariant() : (!string.IsNullOrWhiteSpace(report.Gstin) ? report.Gstin : "21AGVPV8495M1Z5");

        if (!string.IsNullOrWhiteSpace(defaultReturnPeriod))
        {
            _returnPeriod = defaultReturnPeriod.Trim();
        }
        else if (report.FromDate != default)
        {
            _returnPeriod = Gstr3BJsonService.FormatReturnPeriod(report.FromDate);
        }
        else
        {
            _returnPeriod = Gstr3BJsonService.FormatReturnPeriod(DateTime.Today);
        }

        SaveJsonCommand = new RelayCommand(SaveJson, () => !HasErrors);
        CopyJsonCommand = new RelayCommand(CopyJson);
        CloseCommand = new RelayCommand(() => RequestClose?.Invoke(this, EventArgs.Empty));

        UpdateJsonPreview();
    }

    // ── Properties ───────────────────────────────────────────────────────────

    public Gstr3BReport Report => _report;

    public string CompanyName
    {
        get => _companyName;
        set => SetProperty(ref _companyName, value);
    }

    public string GstSourceInfo
    {
        get => _gstSourceInfo;
        set => SetProperty(ref _gstSourceInfo, value);
    }

    public string Gstin
    {
        get => _gstin;
        set
        {
            if (SetProperty(ref _gstin, value?.Trim().ToUpperInvariant() ?? string.Empty))
            {
                UpdateJsonPreview();
            }
        }
    }

    public string ReturnPeriod
    {
        get => _returnPeriod;
        set
        {
            if (SetProperty(ref _returnPeriod, value?.Trim() ?? string.Empty))
            {
                UpdateJsonPreview();
            }
        }
    }

    public string PeriodDisplay => $"{_report.Period} ({_report.FromDate:dd/MM/yyyy} to {_report.ToDate:dd/MM/yyyy})";

    public string JsonPreview
    {
        get => _jsonPreview;
        private set => SetProperty(ref _jsonPreview, value);
    }

    public string? ValidationMessage
    {
        get => _validationMessage;
        private set => SetProperty(ref _validationMessage, value);
    }

    public bool HasErrors
    {
        get => _hasErrors;
        private set
        {
            if (SetProperty(ref _hasErrors, value))
            {
                SaveJsonCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string? CopyStatus
    {
        get => _copyStatus;
        private set => SetProperty(ref _copyStatus, value);
    }

    // Summary Metrics
    public decimal TotalTaxableSales => _report.OutwardTaxableSupplies.TotalTaxableValue;
    public decimal OutwardIgst => _report.OutwardTaxableSupplies.IntegratedTax;
    public decimal OutwardCgst => _report.OutwardTaxableSupplies.CentralTax;
    public decimal OutwardSgst => _report.OutwardTaxableSupplies.StateTax;
    public decimal TotalOutwardTax => _report.OutwardTaxableSupplies.TotalTax;

    public decimal ItcIgst => _report.NetItcAvailable.IntegratedTax;
    public decimal ItcCgst => _report.NetItcAvailable.CentralTax;
    public decimal ItcSgst => _report.NetItcAvailable.StateTax;
    public decimal TotalItc => _report.NetItcAvailable.TotalItc;

    public decimal NetCashIgst => _report.NetTaxPayableInCash.IntegratedTax;
    public decimal NetCashCgst => _report.NetTaxPayableInCash.CentralTax;
    public decimal NetCashSgst => _report.NetTaxPayableInCash.StateTax;
    public decimal TotalNetCash => _report.NetTaxPayableInCash.Total;

    // ── Commands ─────────────────────────────────────────────────────────────

    public RelayCommand SaveJsonCommand { get; }
    public RelayCommand CopyJsonCommand { get; }
    public RelayCommand CloseCommand { get; }

    // ── Logic ────────────────────────────────────────────────────────────────

    private void UpdateJsonPreview()
    {
        bool valid = Gstr3BJsonService.ValidateParameters(Gstin, ReturnPeriod, out var errors);
        if (!valid)
        {
            ValidationMessage = string.Join(Environment.NewLine, errors);
            HasErrors = true;
            try
            {
                JsonPreview = Gstr3BJsonService.GenerateJson(_report, Gstin, ReturnPeriod);
            }
            catch
            {
                JsonPreview = "// Invalid parameters. Please correct GSTIN and Return Period above.";
            }
        }
        else
        {
            ValidationMessage = null;
            HasErrors = false;
            JsonPreview = Gstr3BJsonService.GenerateJson(_report, Gstin, ReturnPeriod);
        }
    }

    private void SaveJson()
    {
        if (HasErrors)
        {
            MessageBox.Show(ValidationMessage, "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var defaultFileName = $"GSTR3B_{Gstin}_{ReturnPeriod}.json";
            var dialog = new SaveFileDialog
            {
                Title = "Save GSTR-3B JSON File",
                Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                FileName = defaultFileName
            };

            if (dialog.ShowDialog() == true)
            {
                var json = Gstr3BJsonService.GenerateJson(_report, Gstin, ReturnPeriod);
                File.WriteAllText(dialog.FileName, json, Encoding.UTF8);

                MessageBox.Show(
                    $"GSTR-3B JSON file successfully generated and saved to:\n\n{dialog.FileName}\n\nYou can now upload this file directly to the GST Portal (gst.gov.in) offline utility.",
                    "Export Successful",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                RequestClose?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Failed to save GSTR-3B JSON file: " + ex.Message, "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CopyJson()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(JsonPreview))
            {
                Clipboard.SetText(JsonPreview);
                CopyStatus = "✓ JSON Copied to clipboard!";
            }
        }
        catch (Exception ex)
        {
            CopyStatus = "Copy failed: " + ex.Message;
        }
    }
}
