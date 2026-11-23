using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Globalization;
using System.Text;
using ConceptJewelleryPro.Infrastructure.Services;
using ConceptJewelleryPro.Infrastructure.Settings;
using ConceptJewelleryPro.Shared;
using FastReport;
using FastReport.Export.Image;
using FrPageRange = FastReport.PageRange;

namespace ConceptJewelleryPro.Infrastructure.Printing;

public static class FastReportPrintService
{
    public const string DefaultPrintFilesPath = @"D:\Concept\FRX";
    private static readonly CultureInfo IndianCulture = new("en-IN");
    private static string? _configuredPrintFilesPath;
    private static string? _configuredSavePath;

    public static void ConfigurePaths(string? printFilesPath, string? savePath)
    {
        _configuredPrintFilesPath = string.IsNullOrWhiteSpace(printFilesPath)
            ? DefaultPrintFilesPath
            : printFilesPath.Trim();
        _configuredSavePath = string.IsNullOrWhiteSpace(savePath)
            ? null
            : savePath.Trim();
    }

    public sealed record PreparedFastReportDocument(
        string Title,
        string DocumentName,
        string TemplateName,
        IReadOnlyList<byte[]> PagePngBytes,
        float PaperWidthMm,
        float PaperHeightMm,
        bool IsLandscape);

    public sealed class VoucherFrxPrintModel
    {
        public string CompanyName { get; set; } = string.Empty;
        public string CompanyAddress { get; set; } = string.Empty;
        public string CompanyPhone { get; set; } = string.Empty;
        public string CompanyGstin { get; set; } = string.Empty;
        public string VoucherTitle { get; set; } = string.Empty;
        public string VouNo { get; set; } = string.Empty;
        public string VouDate { get; set; } = string.Empty;
        public string PaymentMode { get; set; } = string.Empty;
        public string DrAccountLabel { get; set; } = string.Empty;
        public string DrAccount { get; set; } = string.Empty;
        public string CrAccountLabel { get; set; } = string.Empty;
        public string CrAccount { get; set; } = string.Empty;
        public string PartyLabel { get; set; } = string.Empty;
        public string PartyName { get; set; } = string.Empty;
        public string CashBankLabel { get; set; } = string.Empty;
        public string CashBankAccount { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string AmountFormatted { get; set; } = string.Empty;
        public string AmountInWords { get; set; } = string.Empty;
        public string ChequeNo { get; set; } = string.Empty;
        public string ChqDate { get; set; } = string.Empty;
        public string BankName { get; set; } = string.Empty;
        public string CardType { get; set; } = string.Empty;
        public string CardCommText { get; set; } = string.Empty;
        public string ReferenceSummary { get; set; } = string.Empty;
        public string Narration { get; set; } = string.Empty;
        public string PreparedBy { get; set; } = string.Empty;
    }

    public sealed class SchemeReceiptFrxPrintModel
    {
        public string CompanyName { get; set; } = string.Empty;
        public string CompanyAddress { get; set; } = string.Empty;
        public string CompanyPhone { get; set; } = string.Empty;
        public string CompanyGstin { get; set; } = string.Empty;
        public string VouNo { get; set; } = string.Empty;
        public string VouDate { get; set; } = string.Empty;
        public string Scode { get; set; } = string.Empty;
        public string PaymentMode { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string ReceivedByAccount { get; set; } = string.Empty;
        public int InstallmentNo { get; set; }
        public int PaidInstallmentsCount { get; set; }
        public string InstallmentSummary { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string AmountFormatted { get; set; } = string.Empty;
        public string AmountInWords { get; set; } = string.Empty;
        public string ChequeNo { get; set; } = string.Empty;
        public string ChqDate { get; set; } = string.Empty;
        public string BankName { get; set; } = string.Empty;
        public string CardType { get; set; } = string.Empty;
        public string ReferenceSummary { get; set; } = string.Empty;
        public string SchemeTotalValue { get; set; } = string.Empty;
        public string SchemeDurationMonths { get; set; } = string.Empty;
        public string SchemeMaturityDate { get; set; } = string.Empty;
        public string SchemeDetailsSummary { get; set; } = string.Empty;
        public string Narration { get; set; } = string.Empty;
        public string PreparedBy { get; set; } = string.Empty;
    }

    public sealed class BillHeaderFrxPrintModel
    {
        public string CompanyName { get; set; } = string.Empty;
        public string CompanyAddress { get; set; } = string.Empty;
        public string CompanyPhone { get; set; } = string.Empty;
        public string CompanyGstin { get; set; } = string.Empty;
        public string StateCode { get; set; } = string.Empty;
        public string VouNo { get; set; } = string.Empty;
        public string BillDate { get; set; } = string.Empty;
        public string BillDateLong { get; set; } = string.Empty;
        public string VouType { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerAddress { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string CustomerCity { get; set; } = string.Empty;
        public string TotalQty { get; set; } = string.Empty;
        public string TotalGrWt { get; set; } = string.Empty;
        public string TotalNtWt { get; set; } = string.Empty;
        public string TotalMetalValue { get; set; } = string.Empty;
        public string TotalMaking { get; set; } = string.Empty;
        public string GrossAmt { get; set; } = string.Empty;
        public string TotalDiscount { get; set; } = string.Empty;
        public string TaxableBaseAmt { get; set; } = string.Empty;
        public string CgstRate { get; set; } = "1.5%";
        public string SgstRate { get; set; } = "1.5%";
        public string CgstAmt { get; set; } = string.Empty;
        public string SgstAmt { get; set; } = string.Empty;
        public string TotalGstAmt { get; set; } = string.Empty;
        public string NetAmt { get; set; } = string.Empty;
        public string TotalAmount { get; set; } = string.Empty;
        public string AmountInWords { get; set; } = string.Empty;
        public string PaymentSummary { get; set; } = string.Empty;
        public string PaymentAccountsText { get; set; } = string.Empty;
        public string PaymentAmountsText { get; set; } = string.Empty;
        public string HsnSummary { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
    }

    public sealed class BillItemFrxPrintModel
    {
        public string SNo { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string Subtext { get; set; } = string.Empty;
        public string HsnCode { get; set; } = string.Empty;
        public string Purity { get; set; } = string.Empty;
        public string Qty { get; set; } = string.Empty;
        public string GrWt { get; set; } = string.Empty;
        public string NtWt { get; set; } = string.Empty;
        public string Rate { get; set; } = string.Empty;
        public string MetalValue { get; set; } = string.Empty;
        public string Making { get; set; } = string.Empty;
        public string DisAmt { get; set; } = string.Empty;
        public string TaxableValue { get; set; } = string.Empty;
    }

    public sealed class BillPaymentFrxPrintModel
    {
        public string AccountName { get; set; } = string.Empty;
        public string DetailsSummary { get; set; } = string.Empty;
        public string Amount { get; set; } = string.Empty;
    }

    public static bool HasFrxTemplate(string? templateNameOrPath)
    {
        if (string.IsNullOrWhiteSpace(templateNameOrPath))
            return false;

        var resolved = ResolveFrxTemplatePath(templateNameOrPath, throwIfMissing: false);
        return !string.IsNullOrWhiteSpace(resolved) && File.Exists(resolved);
    }

    public static string ResolveFrxTemplatePath(string templateNameOrPath, bool throwIfMissing = true)
    {
        if (File.Exists(templateNameOrPath))
            return templateNameOrPath;

        var fileName = templateNameOrPath.Trim();
        if (!fileName.EndsWith(".frx", StringComparison.OrdinalIgnoreCase))
            fileName += ".frx";

        var candidates = new List<string>();
        LocalPrinterSettings? localSettings = null;
        try
        {
            localSettings = new LocalPrinterSettingsStore().Load();
        }
        catch { }

        // 1. Primary: PrintFiles folder (default D:\Concept\FRX)
        var printFilesDir = !string.IsNullOrWhiteSpace(_configuredPrintFilesPath)
            ? _configuredPrintFilesPath
            : !string.IsNullOrWhiteSpace(localSettings?.PrintFiles)
                ? localSettings.PrintFiles.Trim()
                : DefaultPrintFilesPath;

        if (!string.IsNullOrWhiteSpace(printFilesDir))
        {
            candidates.Add(Path.Combine(printFilesDir, fileName));
        }

        // 2. Secondary: SavePath\FRX
        var savePathDir = !string.IsNullOrWhiteSpace(_configuredSavePath)
            ? _configuredSavePath
            : localSettings?.SavePath?.Trim();

        if (!string.IsNullOrWhiteSpace(savePathDir))
        {
            candidates.Add(Path.Combine(savePathDir, "FRX", fileName));
        }

        // 3. Fallback: SoftwarePath\PrintTemplates and Application PrintTemplates folder
        var softwarePath = localSettings?.SoftwarePath;
        if (!string.IsNullOrWhiteSpace(softwarePath))
        {
            candidates.Add(Path.Combine(softwarePath, "PrintTemplates", fileName));
        }

        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        candidates.Add(Path.Combine(baseDir, "PrintTemplates", fileName));
        candidates.Add(Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\PrintTemplates", fileName)));
        candidates.Add(Path.GetFullPath(Path.Combine("WPF", "PrintTemplates", fileName)));
        candidates.Add(Path.GetFullPath(Path.Combine("PrintTemplates", fileName)));

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        if (throwIfMissing)
        {
            throw new FileNotFoundException($"FastReport template '{fileName}' was not found in PrintTemplates.", fileName);
        }

        return string.Empty;
    }

    public static PreparedFastReportDocument PrepareReport(
        string templateNameOrPath,
        string title,
        string documentName,
        Action<Report> registerDataAction,
        int dpi = 200)
    {
        var frxPath = ResolveFrxTemplatePath(templateNameOrPath);
        var frxXml = File.ReadAllText(frxPath, Encoding.UTF8);

        using var report = new Report();
        using (var xmlStream = new MemoryStream(Encoding.UTF8.GetBytes(frxXml)))
        {
            report.Load(xmlStream);
        }

        registerDataAction(report);
        report.Prepare();

        if (report.PreparedPages.Count == 0)
        {
            throw new InvalidOperationException($"No pages were generated from FastReport template '{Path.GetFileName(frxPath)}'.");
        }

        float paperWidthMm = 210f;
        float paperHeightMm = 297f;
        bool isLandscape = false;
        if (report.Pages.Count > 0 && report.Pages[0] is ReportPage reportPage)
        {
            if (reportPage.PaperWidth > 0) paperWidthMm = reportPage.PaperWidth;
            if (reportPage.PaperHeight > 0) paperHeightMm = reportPage.PaperHeight;
            isLandscape = reportPage.Landscape;
        }

        var pageBytes = ExportReportPages(report, dpi);

        return new PreparedFastReportDocument(
            title,
            documentName,
            Path.GetFileNameWithoutExtension(frxPath),
            pageBytes,
            paperWidthMm,
            paperHeightMm,
            isLandscape);
    }

    public static void PrintPreparedDocument(PreparedFastReportDocument prepared, string? printerName = null)
    {
        if (prepared.PagePngBytes.Count == 0)
            throw new InvalidOperationException("No rendered pages are available to print.");

        using var printDoc = new PrintDocument();
        if (!string.IsNullOrWhiteSpace(printerName))
        {
            printDoc.PrinterSettings.PrinterName = printerName.Trim();
        }

        printDoc.PrintController = new StandardPrintController();
        printDoc.DocumentName = prepared.DocumentName;

        int widthHundredths = Math.Max(100, (int)Math.Round((prepared.PaperWidthMm / 25.4f) * 100f));
        int heightHundredths = Math.Max(100, (int)Math.Round((prepared.PaperHeightMm / 25.4f) * 100f));

        printDoc.DefaultPageSettings.Landscape = prepared.IsLandscape;
        printDoc.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
        try
        {
            printDoc.DefaultPageSettings.PaperSize = new PaperSize(
                prepared.TemplateName,
                prepared.IsLandscape ? heightHundredths : widthHundredths,
                prepared.IsLandscape ? widthHundredths : heightHundredths);
        }
        catch
        {
            // Fallback to printer default paper size if driver rejects custom PaperSize
        }

        int currentPageIndex = 0;
        printDoc.PrintPage += (_, e) =>
        {
            if (currentPageIndex < prepared.PagePngBytes.Count)
            {
                using var ms = new MemoryStream(prepared.PagePngBytes[currentPageIndex]);
                using var img = Image.FromStream(ms);

                e.Graphics!.InterpolationMode = InterpolationMode.HighQualityBicubic;
                e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                var bounds = e.PageBounds;
                float scale = Math.Min((float)bounds.Width / img.Width, (float)bounds.Height / img.Height);
                float drawW = img.Width * scale;
                float drawH = img.Height * scale;
                float drawX = bounds.Left + (bounds.Width - drawW) / 2f;
                float drawY = bounds.Top + (bounds.Height - drawH) / 2f;

                e.Graphics.DrawImage(img, drawX, drawY, drawW, drawH);

                currentPageIndex++;
                e.HasMorePages = currentPageIndex < prepared.PagePngBytes.Count;
            }
            else
            {
                e.HasMorePages = false;
            }
        };

        printDoc.Print();
    }

    public static (string Name, string Address, string Phone, string Gstin, string StateCode) GetCompanyHeaderInfo()
    {
        try
        {
            var info = DateRangeService.GetCompanyDetails();
            var name = string.IsNullOrWhiteSpace(info.CompanyName) ? "CONCEPT JEWELLERY" : info.CompanyName.Trim();
            var addressParts = new[] { info.Address, info.Address2, info.City }
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim());
            var address = string.Join(", ", addressParts);
            var phone = info.Phone?.Trim() ?? string.Empty;
            var gstin = info.Gstin?.Trim() ?? string.Empty;
            var stateCode = string.IsNullOrWhiteSpace(info.StateCode) ? "21" : info.StateCode.Trim();
            return (name, address, phone, gstin, stateCode);
        }
        catch
        {
            return ("CONCEPT JEWELLERY", string.Empty, string.Empty, string.Empty, "21");
        }
    }

    public static string FormatInr(decimal amount) => $"₹ {amount.ToString("N2", IndianCulture)}";

    public static string ConvertAmountToWords(decimal amount)
    {
        if (amount <= 0) return "Rupees Zero Only";

        var rounded = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        long rupees = (long)Math.Floor(rounded);
        int paise = (int)Math.Round((rounded - rupees) * 100m, 0, MidpointRounding.AwayFromZero);

        var sb = new StringBuilder("Rupees ");
        sb.Append(NumberToIndianWords(rupees));
        if (paise > 0)
        {
            sb.Append(" and ");
            sb.Append(NumberToIndianWords(paise));
            sb.Append(" Paise");
        }
        sb.Append(" Only");
        return sb.ToString();
    }

    private static string NumberToIndianWords(long number)
    {
        if (number == 0) return "Zero";

        string[] units =
        [
            "", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten",
            "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen"
        ];
        string[] tens = ["", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety"];

        string TwoDigits(long n)
        {
            if (n < 20) return units[n];
            return tens[n / 10] + (n % 10 > 0 ? " " + units[n % 10] : "");
        }

        var parts = new List<string>();
        if (number >= 10000000)
        {
            parts.Add(NumberToIndianWords(number / 10000000) + " Crore");
            number %= 10000000;
        }
        if (number >= 100000)
        {
            parts.Add(TwoDigits(number / 100000) + " Lakh");
            number %= 100000;
        }
        if (number >= 1000)
        {
            parts.Add(TwoDigits(number / 1000) + " Thousand");
            number %= 1000;
        }
        if (number >= 100)
        {
            parts.Add(units[number / 100] + " Hundred");
            number %= 100;
        }
        if (number > 0)
        {
            parts.Add(TwoDigits(number));
        }

        return string.Join(" ", parts);
    }

    private static IReadOnlyList<byte[]> ExportReportPages(Report report, int dpi)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ConceptJewelleryPro_FRDoc_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var baseFilePath = Path.Combine(tempDir, "page.png");
            var export = new ImageExport
            {
                ImageFormat = ImageExportFormat.Png,
                Resolution = dpi,
                SeparateFiles = true,
                PageRange = FrPageRange.All
            };

            report.Export(export, baseFilePath);

            var files = Directory.GetFiles(tempDir, "*.png");
            if (files.Length == 0)
                throw new InvalidOperationException("FastReport did not generate any page images.");

            var orderedFiles = files
                .OrderBy(ExtractPageIndex)
                .ToList();

            var result = new List<byte[]>(orderedFiles.Count);
            foreach (var file in orderedFiles)
            {
                result.Add(File.ReadAllBytes(file));
            }

            return result;
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);
            }
            catch { }
        }
    }

    private static int ExtractPageIndex(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        int i = name.Length - 1;
        while (i >= 0 && char.IsDigit(name[i])) i--;
        var digits = name[(i + 1)..];
        return int.TryParse(digits, out var idx) ? idx : 0;
    }
}
