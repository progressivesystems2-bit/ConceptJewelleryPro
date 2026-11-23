using System.Globalization;
using System.IO;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Xml;
using ConceptJewelleryPro.WPF.ViewModels;

namespace ConceptJewelleryPro.WPF.Services;

public static class InvoiceFlowDocumentBuilder
{
    private static readonly CultureInfo IndianCulture = new("en-IN");

    public static FlowDocument CreateInvoiceDocument(InvoiceViewModel invoice, string? customTemplatePath = null)
    {
        var templateNameOrPath = !string.IsNullOrWhiteSpace(customTemplatePath)
            ? customTemplatePath
            : invoice.GetPrintFileForVouType();

        var templatePath = ResolveTemplatePath(templateNameOrPath);
        string templateXaml;

        if (File.Exists(templatePath))
        {
            templateXaml = File.ReadAllText(templatePath);
        }
        else
        {
            throw new FileNotFoundException($"Invoice template not found at {templatePath}");
        }

        var populatedXaml = ProcessTemplate(templateXaml, invoice);

        using var stringReader = new StringReader(populatedXaml);
        using var xmlReader = XmlReader.Create(stringReader);
        return (FlowDocument)XamlReader.Load(xmlReader);
    }

    private static string ProcessTemplate(string template, InvoiceViewModel invoice)
    {
        var localSettings = new ConceptJewelleryPro.Infrastructure.Settings.LocalPrinterSettingsStore().Load();
        bool hideWtInPc = localSettings.HideWtInPcItem;

        // 1. Expand Dynamic Item Rows: <!-- <ItemsRows> --> ... <!-- </ItemsRows> -->
        var processed = Regex.Replace(
            template,
            @"<!--\s*<ItemsRows>\s*-->([\s\S]*?)<!--\s*</ItemsRows>\s*-->",
            match =>
            {
                var rowTemplate = match.Groups[1].Value;
                var sb = new StringBuilder();
                int sno = 1;

                foreach (var item in invoice.Items)
                {
                    var taxableVal = item.Amount > 0 ? item.Amount : item.GrossAmt;
                    var purity = !string.IsNullOrWhiteSpace(item.Purity) ? item.Purity : "22K";
                    var subtext = !string.IsNullOrWhiteSpace(item.OldBarcode) ? item.OldBarcode : item.ENote;

                    bool shouldHideWeight = hideWtInPc && item.PcRate;
                    string grWtStr = (shouldHideWeight || item.GrWt <= 0 || (item.GrWt == item.NtWt && item.NtWt > 0)) ? "-" : item.GrWt.ToString("F3", CultureInfo.InvariantCulture);
                    string ntWtStr = (shouldHideWeight || item.NtWt <= 0) ? "-" : item.NtWt.ToString("F3", CultureInfo.InvariantCulture);

                    var itemHsn = !string.IsNullOrWhiteSpace(item.HsnCode)
                        ? item.HsnCode
                        : invoice.GetHsnForItem(item.ItemName);
                    if (string.IsNullOrWhiteSpace(itemHsn)) itemHsn = "7113";

                    var row = rowTemplate
                        .Replace("{{SNo}}", sno.ToString())
                        .Replace("{{Barcode}}", EscapeXml(item.Barcode))
                        .Replace("{{ItemName}}", EscapeXml(item.ItemName?.ToUpperInvariant()))
                        .Replace("{{OldBarcode}}", EscapeXml(subtext))
                        .Replace("{{HSN}}", EscapeXml(itemHsn))
                        .Replace("{{Hsn}}", EscapeXml(itemHsn))
                        .Replace("{{HsnCode}}", EscapeXml(itemHsn))
                        .Replace("{{Purity}}", EscapeXml(purity))
                        .Replace("{{Qty}}", item.Qty.ToString())
                        .Replace("{{GrWt}}", grWtStr)
                        .Replace("{{NtWt}}", ntWtStr)
                        .Replace("{{Rate}}", item.Rate > 0 ? item.Rate.ToString("N2", IndianCulture) : "-")
                        .Replace("{{MetalValue}}", item.MetalValue > 0 ? item.MetalValue.ToString("N2", IndianCulture) : "-")
                        .Replace("{{Making}}", item.Making > 0 ? item.Making.ToString("N2", IndianCulture) : "-")
                        .Replace("{{GrossAmt}}", item.GrossAmt > 0 ? item.GrossAmt.ToString("N2", IndianCulture) : "-")
                        .Replace("{{Value}}", (item.GrossAmt > 0 ? item.GrossAmt : taxableVal).ToString("N2", IndianCulture))
                        .Replace("{{TaxableValue}}", taxableVal > 0 ? taxableVal.ToString("N2", IndianCulture) : "-")
                        .Replace("{{TaxableAmt}}", taxableVal > 0 ? taxableVal.ToString("N2", IndianCulture) : "-")
                        .Replace("{{DisAmt}}", item.DisAmt.ToString("N2", IndianCulture))
                        .Replace("{{Discount}}", item.DisAmt.ToString("N2", IndianCulture))
                        .Replace("{{Amount}}", taxableVal > 0 ? taxableVal.ToString("N2", IndianCulture) : "-")
                        .Replace("{{SalesMan}}", EscapeXml(item.SalesMan))
                        .Replace("{{ENote}}", EscapeXml(item.ENote));

                    sb.Append(row);
                    sno++;
                }

                // Parse MinRows from template (e.g. <!-- MinRows: 10 -->) or default to 0
                int minRows = 0;
                var minRowsMatch = Regex.Match(template, @"<!--\s*MinRows:\s*(\d+)\s*-->", RegexOptions.IgnoreCase);
                if (minRowsMatch.Success && int.TryParse(minRowsMatch.Groups[1].Value, out var parsedMin))
                {
                    minRows = parsedMin;
                }

                while (sno <= minRows)
                {
                    var fillerRow = rowTemplate
                        .Replace("{{SNo}}", "")
                        .Replace("{{Barcode}}", "")
                        .Replace("{{ItemName}}", "")
                        .Replace("{{OldBarcode}}", "")
                        .Replace("{{HSN}}", "")
                        .Replace("{{Hsn}}", "")
                        .Replace("{{HsnCode}}", "")
                        .Replace("{{Purity}}", "")
                        .Replace("{{Qty}}", "")
                        .Replace("{{GrWt}}", "")
                        .Replace("{{NtWt}}", "")
                        .Replace("{{Rate}}", "")
                        .Replace("{{MetalValue}}", "")
                        .Replace("{{Making}}", "")
                        .Replace("{{GrossAmt}}", "")
                        .Replace("{{Value}}", "")
                        .Replace("{{TaxableValue}}", "")
                        .Replace("{{TaxableAmt}}", "")
                        .Replace("{{DisAmt}}", "")
                        .Replace("{{Discount}}", "")
                        .Replace("{{Amount}}", "")
                        .Replace("{{SalesMan}}", "")
                        .Replace("{{ENote}}", "");

                    sb.Append(fillerRow);
                    sno++;
                }

                return sb.ToString();
            });

        // 2. Expand Dynamic Payment Rows: <!-- <PaymentRows> --> ... <!-- </PaymentRows> -->
        processed = Regex.Replace(
            processed,
            @"<!--\s*<PaymentRows>\s*-->([\s\S]*?)<!--\s*</PaymentRows>\s*-->",
            match =>
            {
                var rowTemplate = match.Groups[1].Value;
                var sb = new StringBuilder();

                var activePayments = invoice.Payments.Where(p => p.Amount > 0).ToList();
                if (activePayments.Count == 0)
                {
                    activePayments = invoice.Payments.ToList();
                }

                foreach (var p in activePayments)
                {
                    string details = p.DetailsSummary;
                    if (string.IsNullOrWhiteSpace(details))
                    {
                        if (p.GrWt > 0 || p.Wt > 0)
                        {
                            details = $"Old Gold Ornaments Gold Weight : {p.GrWt:F2} @ {p.Rate:F2}";
                        }
                        else if (!string.IsNullOrWhiteSpace(p.Narration))
                        {
                            details = p.Narration;
                        }
                    }

                    var row = rowTemplate
                        .Replace("{{AccountName}}", EscapeXml(p.AccountName?.ToUpperInvariant()))
                        .Replace("{{DetailsSummary}}", EscapeXml(details))
                        .Replace("{{Amount}}", p.Amount.ToString("N2", IndianCulture));

                    sb.Append(row);
                }

                return sb.ToString();
            });

        var totalTaxable = invoice.Items.Sum(i => i.Amount > 0 ? i.Amount : i.GrossAmt);
        var totalDiscount = invoice.Discount > 0 ? invoice.Discount : invoice.Items.Sum(i => i.DisAmt);

        var sumVat = invoice.Items.Sum(i => i.VatAmt);
        decimal cgstAmt, sgstAmt;
        if (sumVat > 0)
        {
            cgstAmt = Math.Round(sumVat / 2m, 2);
            sgstAmt = Math.Round(sumVat - cgstAmt, 2);
        }
        else
        {
            cgstAmt = invoice.CgstAmt;
            sgstAmt = invoice.SgstAmt;
        }
        var totalGst = cgstAmt + sgstAmt;

        var taxableBase = invoice.Items.Sum(i => i.AbeforeTax > 0 ? i.AbeforeTax : 0m);
        if (taxableBase <= 0)
        {
            if (totalGst > 0 && invoice.NetAmt > totalGst)
            {
                taxableBase = invoice.NetAmt - totalGst;
            }
            else
            {
                taxableBase = totalTaxable;
            }
        }

        var logoPath = ResolveAssetPath("tattva_logo.png");
        var footerQrPath = ResolveAssetPath("tattva_qr_footer.png");
        var watermarkPath = ResolveAssetPath("tattva_watermark.png");

        // 3. Replace Scalar Header, Totals, and Store Variables
        processed = processed
            .Replace("{{CustomerName}}", EscapeXml(invoice.CustomerName?.ToUpperInvariant()))
            .Replace("{{Address}}", EscapeXml(invoice.Address?.ToUpperInvariant()))
            .Replace("{{Address2}}", EscapeXml(invoice.Address2?.ToUpperInvariant()))
            .Replace("{{Phone}}", EscapeXml(invoice.Phone))
            .Replace("{{City}}", EscapeXml(invoice.City?.ToUpperInvariant()))
            .Replace("{{StoreGstin}}", EscapeXml(invoice.StoreGstin))
            .Replace("{{StoreName}}", EscapeXml(invoice.StoreName))
            .Replace("{{VouNo}}", EscapeXml(invoice.VouNo))
            .Replace("{{BillDate}}", invoice.BillDate.ToString("dd/MM/yy"))
            .Replace("{{BillDateLong}}", invoice.BillDate.ToString("dd/MM/yyyy"))
            .Replace("{{InvoiceDate}}", invoice.BillDate.ToString("dd/MM/yyyy"))
            .Replace("{{TotalQty}}", invoice.TotalQty > 0 ? invoice.TotalQty.ToString() : "")
            .Replace("{{TotalQtyFormatted}}", invoice.TotalQty.ToString("0.00", CultureInfo.InvariantCulture))
            .Replace("{{TotalGrWt}}", invoice.TotalGrWt > 0 ? invoice.TotalGrWt.ToString("F3", CultureInfo.InvariantCulture) : "")
            .Replace("{{TotalNtWt}}", invoice.TotalNtWt > 0 ? invoice.TotalNtWt.ToString("F3", CultureInfo.InvariantCulture) : "")
            .Replace("{{TotalMetalValue}}", invoice.TotalMetalValue > 0 ? invoice.TotalMetalValue.ToString("N2", IndianCulture) : "")
            .Replace("{{TotalMaking}}", invoice.TotalMaking > 0 ? invoice.TotalMaking.ToString("N2", IndianCulture) : "")
            .Replace("{{GrossAmt}}", invoice.GrossAmt > 0 ? invoice.GrossAmt.ToString("N2", IndianCulture) : "")
            .Replace("{{TotalValue}}", (invoice.GrossAmt > 0 ? invoice.GrossAmt : totalTaxable).ToString("N2", IndianCulture))
            .Replace("{{TotalDiscount}}", totalDiscount.ToString("N2", IndianCulture))
            .Replace("{{TaxableBaseAmt}}", taxableBase.ToString("N2", IndianCulture))
            .Replace("{{Hsn}}", EscapeXml(invoice.GetInvoiceHsnSummary()))
            .Replace("{{HsnCode}}", EscapeXml(invoice.GetInvoiceHsnSummary()))
            .Replace("{{GstRate}}", "3%")
            .Replace("{{CgstRate}}", "1.5%")
            .Replace("{{SgstRate}}", "1.5%")
            .Replace("{{TotalGstAmt}}", totalGst.ToString("N2", IndianCulture))
            .Replace("{{IgstAmt}}", "0.00")
            .Replace("{{TotalTaxableValue}}", totalTaxable > 0 ? totalTaxable.ToString("N2", IndianCulture) : "")
            .Replace("{{TotalTaxableAmt}}", totalTaxable > 0 ? totalTaxable.ToString("N2", IndianCulture) : "")
            .Replace("{{TotalAmount}}", (invoice.NetAmt > 0 ? invoice.NetAmt : totalTaxable).ToString("N2", IndianCulture))
            .Replace("{{CgstAmt}}", cgstAmt.ToString("N2", IndianCulture))
            .Replace("{{SgstAmt}}", sgstAmt.ToString("N2", IndianCulture))
            .Replace("{{NetAmt}}", invoice.NetAmt.ToString("N2", IndianCulture))
            .Replace("{{LogoPath}}", logoPath)
            .Replace("{{FooterQrPath}}", footerQrPath)
            .Replace("{{WatermarkPath}}", watermarkPath);

        // Strip any remaining unreplaced {{Tag}} to avoid XAML parse errors on optional placeholders
        processed = Regex.Replace(processed, @"\{\{[A-Za-z0-9_]+\}\}", string.Empty);

        return processed;
    }

    private static string EscapeXml(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return SecurityElement.Escape(value);
    }

    private static string ResolveTemplatePath(string? templateNameOrPath)
    {
        if (!string.IsNullOrWhiteSpace(templateNameOrPath) && File.Exists(templateNameOrPath))
            return templateNameOrPath;

        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string? softwarePath = null;
        try
        {
            softwarePath = new ConceptJewelleryPro.Infrastructure.Settings.LocalPrinterSettingsStore().Load().SoftwarePath;
        }
        catch { }

        if (!string.IsNullOrWhiteSpace(templateNameOrPath))
        {
            var fileName = templateNameOrPath.Trim();
            if (!fileName.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
            {
                fileName += ".xaml";
            }

            // 0. Configured SoftwarePath\Templates
            if (!string.IsNullOrWhiteSpace(softwarePath))
            {
                var swPath = Path.Combine(softwarePath, "Templates", fileName);
                if (File.Exists(swPath)) return swPath;
            }

            // 1. Direct output directory Templates folder
            var outPath = Path.Combine(baseDir, "Templates", fileName);
            if (File.Exists(outPath)) return outPath;

            // 2. Development source directory path
            var devPath = Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\Templates", fileName));
            if (File.Exists(devPath)) return devPath;

            // 3. Current Working Directory fallback
            var cwdDev = Path.GetFullPath(Path.Combine(@"WPF\Templates", fileName));
            if (File.Exists(cwdDev)) return cwdDev;

            var localCwd = Path.GetFullPath(Path.Combine(@"Templates", fileName));
            if (File.Exists(localCwd)) return localCwd;
        }

        // Fallback to default TaxInvoiceTemplate.xaml / BillPrint.xaml if specific template not found
        string[] defaultFallbacks = { "TaxInvoiceTemplate.xaml", "BillPrint.xaml" };
        foreach (var defaultFile in defaultFallbacks)
        {
            if (!string.IsNullOrWhiteSpace(softwarePath))
            {
                var swPath = Path.Combine(softwarePath, "Templates", defaultFile);
                if (File.Exists(swPath)) return swPath;
            }

            var outPath = Path.Combine(baseDir, "Templates", defaultFile);
            if (File.Exists(outPath)) return outPath;

            var devPath = Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\Templates", defaultFile));
            if (File.Exists(devPath)) return devPath;

            var cwdDev = Path.GetFullPath(Path.Combine(@"WPF\Templates", defaultFile));
            if (File.Exists(cwdDev)) return cwdDev;

            var localCwd = Path.GetFullPath(Path.Combine(@"Templates", defaultFile));
            if (File.Exists(localCwd)) return localCwd;
        }

        if (!string.IsNullOrWhiteSpace(softwarePath))
        {
            var targetFile = !string.IsNullOrWhiteSpace(templateNameOrPath) ? templateNameOrPath : "TaxInvoiceTemplate.xaml";
            var swPath = Path.Combine(softwarePath, "Templates", targetFile);
            if (File.Exists(swPath)) return swPath;
        }

        return Path.Combine(baseDir, "Templates", !string.IsNullOrWhiteSpace(templateNameOrPath) ? templateNameOrPath : "TaxInvoiceTemplate.xaml");
    }

    private static string ResolveAssetPath(string assetFileName)
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string? softwarePath = null;
        try
        {
            softwarePath = new ConceptJewelleryPro.Infrastructure.Settings.LocalPrinterSettingsStore().Load().SoftwarePath;
        }
        catch { }

        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(softwarePath))
        {
            candidates.Add(Path.Combine(softwarePath, "Templates", "Assets", assetFileName));
            candidates.Add(Path.Combine(softwarePath, "Templates", assetFileName));
        }

        candidates.Add(Path.Combine(baseDir, "Templates", "Assets", assetFileName));
        candidates.Add(Path.Combine(baseDir, "Templates", assetFileName));
        candidates.Add(Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\Templates\Assets", assetFileName)));
        candidates.Add(Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\Templates", assetFileName)));
        candidates.Add(Path.GetFullPath(Path.Combine(@"WPF\Templates\Assets", assetFileName)));
        candidates.Add(Path.GetFullPath(Path.Combine(@"Templates\Assets", assetFileName)));
        candidates.Add(Path.GetFullPath(Path.Combine(@"Templates", assetFileName)));

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate)) return candidate;
        }

        return Path.Combine(baseDir, "Templates", "Assets", assetFileName);
    }
}
