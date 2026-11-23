using System;
using System.IO;
using System.Text;
using System.Windows;
using ConceptJewelleryPro.Domain.Entities.GST;
using ConceptJewelleryPro.Infrastructure.Services;
using Microsoft.Win32;

namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class Gstr1ExportViewModel : ViewModelBase
{
    private readonly Gstr1Report _report;
    private string _gstin;
    private string _returnPeriod;
    private string _jsonPreview = string.Empty;
    private string? _validationMessage;
    private bool _hasErrors;
    private string? _copyStatus;

    private string _companyName;
    private string _sellerStateCode = "21";
    private string _gstSourceInfo = "Auto-detected from DateRange table";

    public event EventHandler? RequestClose;

    public Gstr1ExportViewModel(Gstr1Report report, string defaultGstin, string? defaultReturnPeriod = null, string? companyName = null, string sellerStateCode = "21")
    {
        _report = report ?? throw new ArgumentNullException(nameof(report));
        _companyName = !string.IsNullOrWhiteSpace(companyName) ? companyName : "TATTVA JEWELS";
        _gstin = !string.IsNullOrWhiteSpace(defaultGstin) ? defaultGstin.Trim().ToUpperInvariant() : "21AGVPV8495M1Z5";
        _sellerStateCode = !string.IsNullOrWhiteSpace(sellerStateCode) ? sellerStateCode : "21";

        if (!string.IsNullOrWhiteSpace(defaultReturnPeriod))
        {
            _returnPeriod = defaultReturnPeriod.Trim();
        }
        else if (report.ToDate != default)
        {
            _returnPeriod = Gstr1JsonService.FormatReturnPeriod(report.ToDate);
        }
        else if (report.FromDate != default)
        {
            _returnPeriod = Gstr1JsonService.FormatReturnPeriod(report.FromDate);
        }
        else
        {
            _returnPeriod = Gstr1JsonService.FormatReturnPeriod(DateTime.Today);
        }

        SaveJsonCommand = new RelayCommand(SaveJson, () => !HasErrors);
        ExportExcelCommand = new RelayCommand(ExportExcel, () => !HasErrors);
        CopyJsonCommand = new RelayCommand(CopyJson);
        CloseCommand = new RelayCommand(() => RequestClose?.Invoke(this, EventArgs.Empty));

        UpdateJsonPreview();
    }

    // ── Properties ───────────────────────────────────────────────────────────

    public Gstr1Report Report => _report;

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

    public string PeriodDisplay => $"{_report.FromDate:dd/MM/yyyy} to {_report.ToDate:dd/MM/yyyy}";

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
    public decimal TotalTaxableSales => _report.TotalTaxableAmount;
    public decimal TotalIgst => _report.TotalIgstAmount;
    public decimal TotalCgst => _report.TotalCgstAmount;
    public decimal TotalSgst => _report.TotalSgstAmount;
    public decimal TotalTax => _report.TotalTaxAmount;
    public decimal TotalInvoiceValue => _report.TotalInvoiceValue;
    public int B2BInvoiceCount => _report.B2BInvoices?.Count ?? 0;
    public int B2CSCount => _report.B2CSmallSummaries?.Count ?? 0;
    public int HsnCount => _report.HsnSummaries?.Count ?? 0;

    // ── Commands ─────────────────────────────────────────────────────────────

    public RelayCommand SaveJsonCommand { get; }
    public RelayCommand ExportExcelCommand { get; }
    public RelayCommand CopyJsonCommand { get; }
    public RelayCommand CloseCommand { get; }

    // ── Logic ────────────────────────────────────────────────────────────────

    private void UpdateJsonPreview()
    {
        bool valid = Gstr1JsonService.ValidateParameters(Gstin, ReturnPeriod, out var errors);
        if (!valid)
        {
            ValidationMessage = string.Join(Environment.NewLine, errors);
            HasErrors = true;
            SaveJsonCommand.RaiseCanExecuteChanged();
            ExportExcelCommand.RaiseCanExecuteChanged();
            try
            {
                JsonPreview = Gstr1JsonService.GenerateJson(_report, Gstin, ReturnPeriod, _sellerStateCode);
            }
            catch
            {
                JsonPreview = "// Invalid parameters for GSTR-1 JSON generation.";
            }
            return;
        }

        try
        {
            JsonPreview = Gstr1JsonService.GenerateJson(_report, Gstin, ReturnPeriod, _sellerStateCode);
            ValidationMessage = null;
            HasErrors = false;
            SaveJsonCommand.RaiseCanExecuteChanged();
            ExportExcelCommand.RaiseCanExecuteChanged();
        }
        catch (Exception ex)
        {
            ValidationMessage = "Error generating JSON: " + ex.Message;
            HasErrors = true;
            SaveJsonCommand.RaiseCanExecuteChanged();
            ExportExcelCommand.RaiseCanExecuteChanged();
            JsonPreview = "// " + ex.Message;
        }
    }

    private void SaveJson()
    {
        if (HasErrors)
        {
            MessageBox.Show(ValidationMessage ?? "Please fix validation errors before saving.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var defaultName = $"GSTR1_{Gstin}_{ReturnPeriod}.json";
        var sfd = new SaveFileDialog
        {
            Title = "Save GSTR-1 JSON Return File",
            Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
            FileName = defaultName
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                var json = Gstr1JsonService.GenerateJson(_report, Gstin, ReturnPeriod, _sellerStateCode, indented: false);
                File.WriteAllText(sfd.FileName, json, Encoding.UTF8);
                MessageBox.Show($"GSTR-1 JSON saved successfully to:\n{sfd.FileName}", "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                RequestClose?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to save JSON file: " + ex.Message, "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void ExportExcel()
    {
        if (HasErrors)
        {
            MessageBox.Show(ValidationMessage ?? "Please fix validation errors before exporting.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var defaultName = $"GSTR1_{Gstin}_{ReturnPeriod}.xlsx";
        var sfd = new SaveFileDialog
        {
            Title = "Export GSTR-1 Excel Return (Offline Utility Format)",
            Filter = "Excel Workbook (*.xlsx)|*.xlsx|All Files (*.*)|*.*",
            FileName = defaultName
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                GstrExcelService.ExportGstr1ToFile(_report, sfd.FileName, Gstin, ReturnPeriod, CompanyName, _sellerStateCode);
                MessageBox.Show($"GSTR-1 Excel workbook exported successfully to:\n{sfd.FileName}\n\nWorksheets created:\n• Summary (Executive Audit View)\n• b2b (Table 4 Registered Invoices)\n• b2cl (Table 5 Large Unregistered Invoices)\n• b2cs (Table 7 Retail Small Supplies)\n• cdnr (Table 9B Credit/Debit Notes)\n• hsn (Table 12 HSN Summary)\n• docs (Table 13 Documents Issued)\n\nStrictly aligned with Government GST Offline Utility format.", "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                RequestClose?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to export Excel file: " + ex.Message, "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void CopyJson()
    {
        if (string.IsNullOrWhiteSpace(JsonPreview)) return;
        try
        {
            Clipboard.SetText(JsonPreview);
            CopyStatus = "✓ JSON Copied to clipboard";
        }
        catch (Exception ex)
        {
            CopyStatus = "Failed to copy: " + ex.Message;
        }
    }
}
