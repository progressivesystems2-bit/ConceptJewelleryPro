using System.Globalization;
using System.IO;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Xml;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.Infrastructure.Services;
using ConceptJewelleryPro.WPF.ViewModels;

namespace ConceptJewelleryPro.WPF.Services;

public static class SalesRegisterFlowDocumentBuilder
{
    private static readonly CultureInfo IndianCulture = new("en-IN");

    /// <summary>
    /// Calculates the correct rupee tax amount and taxable amount.
    /// If TaxAmt in the database was storing the tax rate % (e.g. 0, 3, 5, 18) instead of the actual amount,
    /// this properly computes the rupee amounts based on NetAmt and Tax Rate.
    /// </summary>
    public static (decimal TaxableAmt, decimal TaxAmt, decimal Cgst, decimal Sgst) GetTaxBreakup(InvoiceListItem inv)
    {
        decimal taxable = inv.TaxableAmt;
        decimal tax = inv.TaxAmt;

        if (taxable <= 0 && inv.NetAmt > 0)
        {
            decimal rate = inv.Tax > 0 ? inv.Tax : 3.00m;
            taxable = Math.Round(inv.NetAmt / (1m + (rate / 100m)), 2);
            tax = inv.NetAmt - taxable;
        }
        else if (tax <= 0 && taxable > 0 && inv.NetAmt > taxable)
        {
            tax = inv.NetAmt - taxable;
        }
        else if (taxable <= 0 && tax > 0 && inv.NetAmt > tax)
        {
            taxable = inv.NetAmt - tax;
        }

        var cgst = Math.Round(tax / 2m, 2);
        var sgst = tax - cgst;

        return (taxable, tax, cgst, sgst);
    }

    public static FlowDocument CreateReportDocument(InvoiceListViewModel vm)
    {
        return vm.SelectedPrintOption switch
        {
            "DayWisePrint" => CreateDaywisePrintDocument(vm),
            "DayWiseSummaryPrint" => CreateDaywiseSummaryDocument(vm),
            "VoucherWisePrint" => CreateVoucherWisePrintDocument(vm),
            "VoucherWiseSummary" => CreateVoucherWiseSummaryDocument(vm),
            "PurityWiseSales" => CreatePurityWiseSalesDocument(vm),
            "MonthwiseSummary" => CreateMonthwiseSummaryDocument(vm),
            _ => CreateDaywisePrintDocument(vm)
        };
    }

    public static FlowDocument CreateDaywisePrintDocument(InvoiceListViewModel vm)
    {
        var templatePath = ResolveTemplatePath("DayWisePrintTemplate.xaml");
        if (File.Exists(templatePath))
        {
            try
            {
                var template = File.ReadAllText(templatePath);
                var rowMatch = Regex.Match(template, @"<!--\s*<DataRows>\s*-->([\s\S]*?)<!--\s*</DataRows>\s*-->");
                var dateTotalMatch = Regex.Match(template, @"<!--\s*<DateTotalRow>\s*-->([\s\S]*?)<!--\s*</DateTotalRow>\s*-->");
                var grandTotalMatch = Regex.Match(template, @"<!--\s*<GrandTotalRow>\s*-->([\s\S]*?)<!--\s*</GrandTotalRow>\s*-->");

                if (rowMatch.Success && dateTotalMatch.Success && grandTotalMatch.Success)
                {
                    var rowTemplate = rowMatch.Groups[1].Value;
                    var dateTotalTemplate = dateTotalMatch.Groups[1].Value;
                    var grandTotalTemplate = grandTotalMatch.Groups[1].Value;

                    var sb = new StringBuilder();
                    var groupedByDate = vm.Invoices
                        .OrderBy(i => i.BillDate)
                        .ThenBy(i => i.VouNo)
                        .GroupBy(i => i.BillDate.Date);

                    decimal grandTaxable = 0m, grandGst = 0m, grandTotal = 0m, grandNetWt = 0m;

                    foreach (var group in groupedByDate)
                    {
                        decimal dayTaxable = 0m, dayGst = 0m, dayTotal = 0m, dayNetWt = 0m;

                        foreach (var inv in group)
                        {
                            var (taxableAmt, taxAmt, _, _) = GetTaxBreakup(inv);

                            dayTaxable += taxableAmt;
                            dayGst += taxAmt;
                            dayTotal += inv.NetAmt;
                            dayNetWt += inv.NetWt;

                            grandTaxable += taxableAmt;
                            grandGst += taxAmt;
                            grandTotal += inv.NetAmt;
                            grandNetWt += inv.NetWt;

                            var row = rowTemplate
                                .Replace("{{BillDate}}", inv.BillDate.ToString("dd/MM/yyyy"))
                                .Replace("{{VouType}}", EscapeXml(inv.VouType))
                                .Replace("{{BillNo}}", EscapeXml(inv.VouNo))
                                .Replace("{{NetWt}}", inv.NetWt > 0 ? inv.NetWt.ToString("N3", CultureInfo.InvariantCulture) : "-")
                                .Replace("{{TaxableAmt}}", taxableAmt.ToString("N2", IndianCulture))
                                .Replace("{{TaxAmt}}", taxAmt.ToString("N2", IndianCulture))
                                .Replace("{{Total}}", inv.NetAmt.ToString("N2", IndianCulture));

                            sb.Append(row);
                        }

                        var dateTotalRow = dateTotalTemplate
                            .Replace("{{DayTaxableAmt}}", dayTaxable.ToString("N2", IndianCulture))
                            .Replace("{{DayTaxAmt}}", dayGst.ToString("N2", IndianCulture))
                            .Replace("{{DayTotal}}", dayTotal.ToString("N2", IndianCulture));

                        sb.Append(dateTotalRow);
                    }

                    var grandTotalRow = grandTotalTemplate
                        .Replace("{{GrandNetWt}}", grandNetWt > 0 ? grandNetWt.ToString("N3", CultureInfo.InvariantCulture) : "-")
                        .Replace("{{GrandTaxableAmt}}", grandTaxable.ToString("N2", IndianCulture))
                        .Replace("{{GrandTaxAmt}}", grandGst.ToString("N2", IndianCulture))
                        .Replace("{{GrandTotal}}", grandTotal.ToString("N2", IndianCulture));

                    sb.Append(grandTotalRow);

                    var fullTableRegex = new Regex(@"<!--\s*<DataRows>\s*-->[\s\S]*?<!--\s*</GrandTotalRow>\s*-->");
                    var populated = fullTableRegex.Replace(template, sb.ToString());

                    populated = ReplaceHeaderPlaceholders(populated, vm, "Day Wise Sales Register");

                    using var stringReader = new StringReader(populated);
                    using var xmlReader = XmlReader.Create(stringReader);
                    return (FlowDocument)XamlReader.Load(xmlReader);
                }
            }
            catch { }
        }

        // Programmatic Fallback
        return BuildDaywisePrintDocumentCode(vm);
    }

    public static FlowDocument CreateDaywiseSummaryDocument(InvoiceListViewModel vm)
    {
        var templatePath = ResolveTemplatePath("DayWiseSummaryTemplate.xaml");
        if (File.Exists(templatePath))
        {
            try
            {
                var template = File.ReadAllText(templatePath);
                var rowMatch = Regex.Match(template, @"<!--\s*<DataRows>\s*-->([\s\S]*?)<!--\s*</DataRows>\s*-->");
                var dateTotalMatch = Regex.Match(template, @"<!--\s*<DateTotalRow>\s*-->([\s\S]*?)<!--\s*</DateTotalRow>\s*-->");
                var grandTotalMatch = Regex.Match(template, @"<!--\s*<GrandTotalRow>\s*-->([\s\S]*?)<!--\s*</GrandTotalRow>\s*-->");

                if (rowMatch.Success && dateTotalMatch.Success && grandTotalMatch.Success)
                {
                    var rowTemplate = rowMatch.Groups[1].Value;
                    var dateTotalTemplate = dateTotalMatch.Groups[1].Value;
                    var grandTotalTemplate = grandTotalMatch.Groups[1].Value;

                    var sb = new StringBuilder();
                    var groupedByDate = vm.Invoices
                        .OrderBy(i => i.BillDate)
                        .GroupBy(i => i.BillDate.Date);

                    decimal grandQty = 0m, grandNetWt = 0m, grandTaxable = 0m, grandTax = 0m, grandTotal = 0m;

                    foreach (var dateGroup in groupedByDate)
                    {
                        var date = dateGroup.Key;
                        decimal dayQty = 0m, dayNetWt = 0m, dayTaxable = 0m, dayTax = 0m, dayTotal = 0m;

                        var vouTypeGroups = dateGroup
                            .GroupBy(i => i.VouType)
                            .OrderBy(g => g.Key);

                        foreach (var typeGroup in vouTypeGroups)
                        {
                            var sortedBills = typeGroup.OrderBy(i => i.VouNo).ToList();
                            var firstBill = sortedBills.FirstOrDefault()?.VouNo ?? "";
                            var lastBill = sortedBills.LastOrDefault()?.VouNo ?? "";
                            var billRange = string.Equals(firstBill, lastBill, StringComparison.OrdinalIgnoreCase)
                                ? firstBill
                                : $"{firstBill}-{lastBill}";

                            decimal typeQty = typeGroup.Sum(i => (decimal)i.Qty);
                            decimal typeNetWt = typeGroup.Sum(i => i.NetWt);
                            decimal typeTaxable = typeGroup.Sum(i => GetTaxBreakup(i).TaxableAmt);
                            decimal typeTax = typeGroup.Sum(i => GetTaxBreakup(i).TaxAmt);
                            decimal typeTotal = typeGroup.Sum(i => i.NetAmt);

                            dayQty += typeQty;
                            dayNetWt += typeNetWt;
                            dayTaxable += typeTaxable;
                            dayTax += typeTax;
                            dayTotal += typeTotal;

                            grandQty += typeQty;
                            grandNetWt += typeNetWt;
                            grandTaxable += typeTaxable;
                            grandTax += typeTax;
                            grandTotal += typeTotal;

                            var row = rowTemplate
                                .Replace("{{BillDate}}", date.ToString("dd/MM/yyyy"))
                                .Replace("{{VouType}}", EscapeXml(typeGroup.Key))
                                .Replace("{{BillNoToBillNo}}", EscapeXml(billRange))
                                .Replace("{{Qty}}", typeQty.ToString("F2", CultureInfo.InvariantCulture))
                                .Replace("{{NetWeight}}", typeNetWt > 0 ? typeNetWt.ToString("N3", CultureInfo.InvariantCulture) : "-")
                                .Replace("{{TaxableAmt}}", typeTaxable.ToString("N2", IndianCulture))
                                .Replace("{{TaxAmt}}", typeTax.ToString("N2", IndianCulture))
                                .Replace("{{Total}}", typeTotal.ToString("N2", IndianCulture));

                            sb.Append(row);
                        }

                        var dateTotalRow = dateTotalTemplate
                            .Replace("{{DayQty}}", dayQty.ToString("F2", CultureInfo.InvariantCulture))
                            .Replace("{{DayNetWeight}}", dayNetWt > 0 ? dayNetWt.ToString("N3", CultureInfo.InvariantCulture) : "-")
                            .Replace("{{DayTaxableAmt}}", dayTaxable.ToString("N2", IndianCulture))
                            .Replace("{{DayTaxAmt}}", dayTax.ToString("N2", IndianCulture))
                            .Replace("{{DayTotal}}", dayTotal.ToString("N2", IndianCulture));

                        sb.Append(dateTotalRow);
                    }

                    var grandTotalRow = grandTotalTemplate
                        .Replace("{{GrandQty}}", grandQty.ToString("F2", CultureInfo.InvariantCulture))
                        .Replace("{{GrandNetWeight}}", grandNetWt > 0 ? grandNetWt.ToString("N3", CultureInfo.InvariantCulture) : "-")
                        .Replace("{{GrandTaxableAmt}}", grandTaxable.ToString("N2", IndianCulture))
                        .Replace("{{GrandTaxAmt}}", grandTax.ToString("N2", IndianCulture))
                        .Replace("{{GrandTotal}}", grandTotal.ToString("N2", IndianCulture));

                    sb.Append(grandTotalRow);

                    var fullTableRegex = new Regex(@"<!--\s*<DataRows>\s*-->[\s\S]*?<!--\s*</GrandTotalRow>\s*-->");
                    var populated = fullTableRegex.Replace(template, sb.ToString());

                    populated = ReplaceHeaderPlaceholders(populated, vm, "Day Wise Sales Summary");

                    using var stringReader = new StringReader(populated);
                    using var xmlReader = XmlReader.Create(stringReader);
                    return (FlowDocument)XamlReader.Load(xmlReader);
                }
            }
            catch { }
        }

        // Programmatic Fallback
        return BuildDaywiseSummaryDocumentCode(vm);
    }

    public static FlowDocument CreateVoucherWisePrintDocument(InvoiceListViewModel vm)
    {
        var templatePath = ResolveTemplatePath("VoucherWisePrintTemplate.xaml");
        if (File.Exists(templatePath))
        {
            try
            {
                var template = File.ReadAllText(templatePath);
                var headerMatch = Regex.Match(template, @"<!--\s*<VoucherTypeHeader>\s*-->([\s\S]*?)<!--\s*</VoucherTypeHeader>\s*-->");
                var rowMatch = Regex.Match(template, @"<!--\s*<DataRows>\s*-->([\s\S]*?)<!--\s*</DataRows>\s*-->");
                var totalMatch = Regex.Match(template, @"<!--\s*<VoucherTypeTotalRow>\s*-->([\s\S]*?)<!--\s*</VoucherTypeTotalRow>\s*-->");
                var grandTotalMatch = Regex.Match(template, @"<!--\s*<GrandTotalRow>\s*-->([\s\S]*?)<!--\s*</GrandTotalRow>\s*-->");

                if (headerMatch.Success && rowMatch.Success && totalMatch.Success && grandTotalMatch.Success)
                {
                    var headerTemplate = headerMatch.Groups[1].Value;
                    var rowTemplate = rowMatch.Groups[1].Value;
                    var totalTemplate = totalMatch.Groups[1].Value;
                    var grandTotalTemplate = grandTotalMatch.Groups[1].Value;

                    var sb = new StringBuilder();
                    var groupedByType = vm.Invoices
                        .OrderBy(i => i.VouType)
                        .ThenBy(i => i.BillDate)
                        .ThenBy(i => i.VouNo)
                        .GroupBy(i => i.VouType);

                    decimal grandQty = 0m, grandNetWt = 0m, grandTaxable = 0m, grandCgst = 0m, grandSgst = 0m, grandTotal = 0m;

                    foreach (var group in groupedByType)
                    {
                        var vHeader = headerTemplate.Replace("{{VouTypeHeader}}", EscapeXml(group.Key.ToUpperInvariant()));
                        sb.Append(vHeader);

                        decimal typeQty = 0m, typeNetWt = 0m, typeTaxable = 0m, typeCgst = 0m, typeSgst = 0m, typeTotal = 0m;

                        foreach (var inv in group)
                        {
                            var (taxableAmt, _, cgst, sgst) = GetTaxBreakup(inv);
                            var qty = (decimal)inv.Qty;

                            typeQty += qty;
                            typeNetWt += inv.NetWt;
                            typeTaxable += taxableAmt;
                            typeCgst += cgst;
                            typeSgst += sgst;
                            typeTotal += inv.NetAmt;

                            grandQty += qty;
                            grandNetWt += inv.NetWt;
                            grandTaxable += taxableAmt;
                            grandCgst += cgst;
                            grandSgst += sgst;
                            grandTotal += inv.NetAmt;

                            var detailsList = new List<string>();
                            if (!string.IsNullOrWhiteSpace(inv.CustomerName)) detailsList.Add(inv.CustomerName.Trim());
                            if (!string.IsNullOrWhiteSpace(inv.Address)) detailsList.Add(inv.Address.Trim());
                            if (!string.IsNullOrWhiteSpace(inv.City)) detailsList.Add(inv.City.Trim());
                            var custDetails = detailsList.Count > 0 ? string.Join(", ", detailsList) : "-";

                            var row = rowTemplate
                                .Replace("{{BillDate}}", inv.BillDate.ToString("dd/MM/yyyy"))
                                .Replace("{{BillNo}}", EscapeXml(inv.VouNo))
                                .Replace("{{CustomerDetails}}", EscapeXml(custDetails))
                                .Replace("{{Qty}}", qty.ToString("F2", CultureInfo.InvariantCulture))
                                .Replace("{{NetWeight}}", inv.NetWt > 0 ? inv.NetWt.ToString("N3", CultureInfo.InvariantCulture) : "-")
                                .Replace("{{TaxableAmt}}", taxableAmt.ToString("N2", IndianCulture))
                                .Replace("{{Cgst}}", cgst.ToString("N2", IndianCulture))
                                .Replace("{{Sgst}}", sgst.ToString("N2", IndianCulture))
                                .Replace("{{Total}}", inv.NetAmt.ToString("N2", IndianCulture));

                            sb.Append(row);
                        }

                        var typeTotalRow = totalTemplate
                            .Replace("{{VouTypeHeader}}", EscapeXml(group.Key))
                            .Replace("{{TypeQty}}", typeQty.ToString("F2", CultureInfo.InvariantCulture))
                            .Replace("{{TypeNetWeight}}", typeNetWt > 0 ? typeNetWt.ToString("N3", CultureInfo.InvariantCulture) : "-")
                            .Replace("{{TypeTaxableAmt}}", typeTaxable.ToString("N2", IndianCulture))
                            .Replace("{{TypeCgst}}", typeCgst.ToString("N2", IndianCulture))
                            .Replace("{{TypeSgst}}", typeSgst.ToString("N2", IndianCulture))
                            .Replace("{{TypeTotal}}", typeTotal.ToString("N2", IndianCulture));

                        sb.Append(typeTotalRow);
                    }

                    var grandTotalRow = grandTotalTemplate
                        .Replace("{{GrandQty}}", grandQty.ToString("F2", CultureInfo.InvariantCulture))
                        .Replace("{{GrandNetWeight}}", grandNetWt > 0 ? grandNetWt.ToString("N3", CultureInfo.InvariantCulture) : "-")
                        .Replace("{{GrandTaxableAmt}}", grandTaxable.ToString("N2", IndianCulture))
                        .Replace("{{GrandCgst}}", grandCgst.ToString("N2", IndianCulture))
                        .Replace("{{GrandSgst}}", grandSgst.ToString("N2", IndianCulture))
                        .Replace("{{GrandTotal}}", grandTotal.ToString("N2", IndianCulture));

                    sb.Append(grandTotalRow);

                    var fullTableRegex = new Regex(@"<!--\s*<VoucherTypeHeader>\s*-->[\s\S]*?<!--\s*</GrandTotalRow>\s*-->");
                    var populated = fullTableRegex.Replace(template, sb.ToString());

                    populated = ReplaceHeaderPlaceholders(populated, vm, "Voucher Wise Sales Register");

                    using var stringReader = new StringReader(populated);
                    using var xmlReader = XmlReader.Create(stringReader);
                    return (FlowDocument)XamlReader.Load(xmlReader);
                }
            }
            catch { }
        }

        // Programmatic Fallback
        return BuildVoucherWisePrintDocumentCode(vm);
    }

    public static FlowDocument CreateVoucherWiseSummaryDocument(InvoiceListViewModel vm)
    {
        var templatePath = ResolveTemplatePath("VoucherWiseSummaryTemplate.xaml");
        if (File.Exists(templatePath))
        {
            try
            {
                var template = File.ReadAllText(templatePath);
                var headerMatch = Regex.Match(template, @"<!--\s*<VoucherTypeHeader>\s*-->([\s\S]*?)<!--\s*</VoucherTypeHeader>\s*-->");
                var rowMatch = Regex.Match(template, @"<!--\s*<DataRows>\s*-->([\s\S]*?)<!--\s*</DataRows>\s*-->");
                var totalMatch = Regex.Match(template, @"<!--\s*<VoucherTypeTotalRow>\s*-->([\s\S]*?)<!--\s*</VoucherTypeTotalRow>\s*-->");
                var grandTotalMatch = Regex.Match(template, @"<!--\s*<GrandTotalRow>\s*-->([\s\S]*?)<!--\s*</GrandTotalRow>\s*-->");

                if (headerMatch.Success && rowMatch.Success && totalMatch.Success && grandTotalMatch.Success)
                {
                    var headerTemplate = headerMatch.Groups[1].Value;
                    var rowTemplate = rowMatch.Groups[1].Value;
                    var totalTemplate = totalMatch.Groups[1].Value;
                    var grandTotalTemplate = grandTotalMatch.Groups[1].Value;

                    var sb = new StringBuilder();
                    var groupedByType = vm.Invoices
                        .GroupBy(i => i.VouType)
                        .OrderBy(g => g.Key);

                    decimal grandQty = 0m, grandNetWt = 0m, grandTaxable = 0m, grandTax = 0m, grandTotal = 0m;

                    foreach (var typeGroup in groupedByType)
                    {
                        var vHeader = headerTemplate.Replace("{{VouTypeHeader}}", EscapeXml(typeGroup.Key.ToUpperInvariant()));
                        sb.Append(vHeader);

                        decimal typeQty = 0m, typeNetWt = 0m, typeTaxable = 0m, typeTax = 0m, typeTotal = 0m;

                        var groupedByDate = typeGroup
                            .OrderBy(i => i.BillDate)
                            .GroupBy(i => i.BillDate.Date);

                        foreach (var dateGroup in groupedByDate)
                        {
                            var sortedBills = dateGroup.OrderBy(i => i.VouNo).ToList();
                            var firstBill = sortedBills.FirstOrDefault()?.VouNo ?? "";
                            var lastBill = sortedBills.LastOrDefault()?.VouNo ?? "";
                            var billRange = string.Equals(firstBill, lastBill, StringComparison.OrdinalIgnoreCase)
                                ? firstBill
                                : $"{firstBill}-{lastBill}";

                            decimal dayQty = dateGroup.Sum(i => (decimal)i.Qty);
                            decimal dayNetWt = dateGroup.Sum(i => i.NetWt);
                            decimal dayTaxable = dateGroup.Sum(i => GetTaxBreakup(i).TaxableAmt);
                            decimal dayTax = dateGroup.Sum(i => GetTaxBreakup(i).TaxAmt);
                            decimal dayTotal = dateGroup.Sum(i => i.NetAmt);

                            typeQty += dayQty;
                            typeNetWt += dayNetWt;
                            typeTaxable += dayTaxable;
                            typeTax += dayTax;
                            typeTotal += dayTotal;

                            grandQty += dayQty;
                            grandNetWt += dayNetWt;
                            grandTaxable += dayTaxable;
                            grandTax += dayTax;
                            grandTotal += dayTotal;

                            var row = rowTemplate
                                .Replace("{{BillDate}}", dateGroup.Key.ToString("dd/MM/yyyy"))
                                .Replace("{{BillNoToBillNo}}", EscapeXml(billRange))
                                .Replace("{{Qty}}", dayQty.ToString("F2", CultureInfo.InvariantCulture))
                                .Replace("{{NetWeight}}", dayNetWt > 0 ? dayNetWt.ToString("N3", CultureInfo.InvariantCulture) : "-")
                                .Replace("{{TaxableAmt}}", dayTaxable.ToString("N2", IndianCulture))
                                .Replace("{{VatAmt}}", dayTax.ToString("N2", IndianCulture))
                                .Replace("{{Total}}", dayTotal.ToString("N2", IndianCulture));

                            sb.Append(row);
                        }

                        var typeTotalRow = totalTemplate
                            .Replace("{{VouTypeHeader}}", EscapeXml(typeGroup.Key))
                            .Replace("{{TypeQty}}", typeQty.ToString("F2", CultureInfo.InvariantCulture))
                            .Replace("{{TypeNetWeight}}", typeNetWt > 0 ? typeNetWt.ToString("N3", CultureInfo.InvariantCulture) : "-")
                            .Replace("{{TypeTaxableAmt}}", typeTaxable.ToString("N2", IndianCulture))
                            .Replace("{{TypeVatAmt}}", typeTax.ToString("N2", IndianCulture))
                            .Replace("{{TypeTotal}}", typeTotal.ToString("N2", IndianCulture));

                        sb.Append(typeTotalRow);
                    }

                    var grandTotalRow = grandTotalTemplate
                        .Replace("{{GrandQty}}", grandQty.ToString("F2", CultureInfo.InvariantCulture))
                        .Replace("{{GrandNetWeight}}", grandNetWt > 0 ? grandNetWt.ToString("N3", CultureInfo.InvariantCulture) : "-")
                        .Replace("{{GrandTaxableAmt}}", grandTaxable.ToString("N2", IndianCulture))
                        .Replace("{{GrandVatAmt}}", grandTax.ToString("N2", IndianCulture))
                        .Replace("{{GrandTotal}}", grandTotal.ToString("N2", IndianCulture));

                    sb.Append(grandTotalRow);

                    var fullTableRegex = new Regex(@"<!--\s*<VoucherTypeHeader>\s*-->[\s\S]*?<!--\s*</GrandTotalRow>\s*-->");
                    var populated = fullTableRegex.Replace(template, sb.ToString());

                    populated = ReplaceHeaderPlaceholders(populated, vm, "Voucher Wise Sales Summary");

                    using var stringReader = new StringReader(populated);
                    using var xmlReader = XmlReader.Create(stringReader);
                    return (FlowDocument)XamlReader.Load(xmlReader);
                }
            }
            catch { }
        }

        // Programmatic Fallback
        return BuildVoucherWiseSummaryDocumentCode(vm);
    }

    public static FlowDocument CreateMonthwiseSummaryDocument(InvoiceListViewModel vm)
    {
        var templatePath = ResolveTemplatePath("MonthwiseSummaryTemplate.xaml");
        if (File.Exists(templatePath))
        {
            try
            {
                var template = File.ReadAllText(templatePath);
                var rowMatch = Regex.Match(template, @"<!--\s*<DataRows>\s*-->([\s\S]*?)<!--\s*</DataRows>\s*-->");
                var grandTotalMatch = Regex.Match(template, @"<!--\s*<GrandTotalRow>\s*-->([\s\S]*?)<!--\s*</GrandTotalRow>\s*-->");

                if (rowMatch.Success && grandTotalMatch.Success)
                {
                    var rowTemplate = rowMatch.Groups[1].Value;
                    var grandTotalTemplate = grandTotalMatch.Groups[1].Value;

                    var sb = new StringBuilder();
                    var groupedByMonth = vm.Invoices
                        .GroupBy(i => new { i.BillDate.Year, i.BillDate.Month })
                        .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month);

                    decimal grandTaxable = 0m, grandGst = 0m, grandTotal = 0m, grandNetWt = 0m;

                    foreach (var monthGroup in groupedByMonth)
                    {
                        var dt = new DateTime(monthGroup.Key.Year, monthGroup.Key.Month, 1);
                        var monthName = dt.ToString("MMMM");

                        var typeGroups = monthGroup
                            .GroupBy(i => i.VouType)
                            .OrderBy(g => g.Key);

                        foreach (var typeGroup in typeGroups)
                        {
                            var sortedBills = typeGroup.OrderBy(i => i.VouNo).ToList();
                            var firstBill = sortedBills.FirstOrDefault()?.VouNo ?? "";
                            var lastBill = sortedBills.LastOrDefault()?.VouNo ?? "";
                            var billRange = string.Equals(firstBill, lastBill, StringComparison.OrdinalIgnoreCase)
                                ? firstBill
                                : $"{firstBill} - {lastBill}";

                            decimal netWt = typeGroup.Sum(i => i.NetWt);
                            decimal taxable = typeGroup.Sum(i => GetTaxBreakup(i).TaxableAmt);
                            decimal gst = typeGroup.Sum(i => GetTaxBreakup(i).TaxAmt);
                            decimal total = typeGroup.Sum(i => i.NetAmt);

                            grandNetWt += netWt;
                            grandTaxable += taxable;
                            grandGst += gst;
                            grandTotal += total;

                            var row = rowTemplate
                                .Replace("{{BillDate}}", monthName)
                                .Replace("{{VouType}}", EscapeXml(typeGroup.Key))
                                .Replace("{{BillNo}}", EscapeXml(billRange))
                                .Replace("{{NetWt}}", netWt.ToString("N2", CultureInfo.InvariantCulture))
                                .Replace("{{TaxableAmt}}", taxable.ToString("N2", IndianCulture))
                                .Replace("{{TaxAmt}}", gst.ToString("N2", IndianCulture))
                                .Replace("{{Total}}", total.ToString("N2", IndianCulture));

                            sb.Append(row);
                        }
                    }

                    var grandTotalRow = grandTotalTemplate
                        .Replace("{{GrandNetWt}}", grandNetWt.ToString("N2", CultureInfo.InvariantCulture))
                        .Replace("{{GrandTaxableAmt}}", grandTaxable.ToString("N2", IndianCulture))
                        .Replace("{{GrandTaxAmt}}", grandGst.ToString("N2", IndianCulture))
                        .Replace("{{GrandTotal}}", grandTotal.ToString("N2", IndianCulture));

                    sb.Append(grandTotalRow);

                    var fullTableRegex = new Regex(@"<!--\s*<DataRows>\s*-->[\s\S]*?<!--\s*</GrandTotalRow>\s*-->");
                    var populated = fullTableRegex.Replace(template, sb.ToString());

                    populated = ReplaceHeaderPlaceholders(populated, vm, "Month Wise Sales Summary");

                    using var stringReader = new StringReader(populated);
                    using var xmlReader = XmlReader.Create(stringReader);
                    return (FlowDocument)XamlReader.Load(xmlReader);
                }
            }
            catch { }
        }

        // Programmatic Fallback
        return BuildMonthwiseSummaryDocumentCode(vm);
    }

    public static FlowDocument CreatePurityWiseSalesDocument(InvoiceListViewModel vm)
    {
        return CreateDaywisePrintDocument(vm);
    }

    private static (string CompanyName, string Address, string Gstin) GetCompanyHeaderDetails(InvoiceListViewModel vm)
    {
        var companyName = !string.IsNullOrWhiteSpace(vm.CompanyName) ? vm.CompanyName : null;
        var address = !string.IsNullOrWhiteSpace(vm.Address) ? vm.Address : null;
        var gstin = !string.IsNullOrWhiteSpace(vm.Gstin) ? vm.Gstin : null;

        if (companyName == null || address == null || gstin == null)
        {
            var dbCompany = DateRangeService.GetCompanyDetails();
            companyName ??= dbCompany.CompanyName;
            address ??= dbCompany.Address;
            gstin ??= dbCompany.Gstin;
        }

        return (companyName ?? string.Empty, address ?? string.Empty, gstin ?? string.Empty);
    }

    private static string ReplaceHeaderPlaceholders(string template, InvoiceListViewModel vm, string reportTitle)
    {
        var (companyName, address, gstin) = GetCompanyHeaderDetails(vm);

        return template
            .Replace("{{CompanyName}}", EscapeXml(companyName))
            .Replace("{{Address}}", EscapeXml(address))
            .Replace("{{Gstin}}", EscapeXml(gstin))
            .Replace("{{FromDate}}", vm.FromDate.ToString("dd/MM/yyyy"))
            .Replace("{{ToDate}}", vm.ToDate.ToString("dd/MM/yyyy"))
            .Replace("{{ReportTitle}}", EscapeXml(reportTitle));
    }

    private static string ResolveTemplatePath(string templateFileName)
    {
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        var direct = Path.Combine(appDir, "Templates", templateFileName);
        if (File.Exists(direct)) return direct;

        var local = Path.Combine(Directory.GetCurrentDirectory(), "Templates", templateFileName);
        if (File.Exists(local)) return local;

        var sourcePath = Path.GetFullPath(Path.Combine(appDir, @"..\..\..\Templates", templateFileName));
        if (File.Exists(sourcePath)) return sourcePath;

        return direct;
    }

    private static string EscapeXml(string? input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        return SecurityElement.Escape(input);
    }

    #region Code-Level Fallback Builders

    private static FlowDocument BuildDaywisePrintDocumentCode(InvoiceListViewModel vm)
    {
        var doc = new FlowDocument
        {
            Background = Brushes.White,
            Foreground = Brushes.Black,
            PageWidth = 842,
            PageHeight = 1190,
            PagePadding = new Thickness(36, 28, 36, 28),
            FontFamily = new FontFamily("Segoe UI, Arial, sans-serif"),
            FontSize = 11.5,
            ColumnWidth = double.PositiveInfinity
        };

        AppendCompanyHeader(doc, vm, "Day Wise Sales Register");

        var table = new Table { CellSpacing = 0, Margin = new Thickness(0, 8, 0, 16) };
        table.Columns.Add(new TableColumn { Width = new GridLength(85) });
        table.Columns.Add(new TableColumn { Width = new GridLength(175) });
        table.Columns.Add(new TableColumn { Width = new GridLength(95) });
        table.Columns.Add(new TableColumn { Width = new GridLength(75) });
        table.Columns.Add(new TableColumn { Width = new GridLength(115) });
        table.Columns.Add(new TableColumn { Width = new GridLength(95) });
        table.Columns.Add(new TableColumn { Width = new GridLength(110) });

        var rowGroup = new TableRowGroup();
        var headerRow = new TableRow();
        AddHeaderCell(headerRow, "Bill Date", TextAlignment.Left);
        AddHeaderCell(headerRow, "Voucher Type", TextAlignment.Left);
        AddHeaderCell(headerRow, "Bill No", TextAlignment.Left);
        AddHeaderCell(headerRow, "Net Wt", TextAlignment.Right);
        AddHeaderCell(headerRow, "Taxable Amount", TextAlignment.Right);
        AddHeaderCell(headerRow, "GST Amount", TextAlignment.Right);
        AddHeaderCell(headerRow, "Total", TextAlignment.Right);
        rowGroup.Rows.Add(headerRow);

        var groupedByDate = vm.Invoices.OrderBy(i => i.BillDate).ThenBy(i => i.VouNo).GroupBy(i => i.BillDate.Date);
        decimal grandTaxable = 0m, grandGst = 0m, grandTotal = 0m, grandNetWt = 0m;

        foreach (var group in groupedByDate)
        {
            decimal dayTaxable = 0m, dayGst = 0m, dayTotal = 0m;

            foreach (var inv in group)
            {
                var (taxableAmt, taxAmt, _, _) = GetTaxBreakup(inv);
                dayTaxable += taxableAmt;
                dayGst += taxAmt;
                dayTotal += inv.NetAmt;
                grandTaxable += taxableAmt;
                grandGst += taxAmt;
                grandTotal += inv.NetAmt;
                grandNetWt += inv.NetWt;

                var row = new TableRow();
                AddDataCell(row, inv.BillDate.ToString("dd/MM/yyyy"), TextAlignment.Left);
                AddDataCell(row, inv.VouType, TextAlignment.Left);
                AddDataCell(row, inv.VouNo, TextAlignment.Left);
                AddDataCell(row, inv.NetWt > 0 ? inv.NetWt.ToString("N3", CultureInfo.InvariantCulture) : "-", TextAlignment.Right);
                AddDataCell(row, taxableAmt.ToString("N2", IndianCulture), TextAlignment.Right);
                AddDataCell(row, taxAmt.ToString("N2", IndianCulture), TextAlignment.Right);
                AddDataCell(row, inv.NetAmt.ToString("N2", IndianCulture), TextAlignment.Right);
                rowGroup.Rows.Add(row);
            }

            var totalRow = new TableRow();
            AddTotalCell(totalRow, "DATE TOTAL", TextAlignment.Left, isSpan: true, spanCount: 3);
            AddTotalCell(totalRow, "", TextAlignment.Right);
            AddTotalCell(totalRow, dayTaxable.ToString("N2", IndianCulture), TextAlignment.Right);
            AddTotalCell(totalRow, dayGst.ToString("N2", IndianCulture), TextAlignment.Right);
            AddTotalCell(totalRow, dayTotal.ToString("N2", IndianCulture), TextAlignment.Right);
            rowGroup.Rows.Add(totalRow);
        }

        var grandRow = new TableRow();
        AddGrandTotalCell(grandRow, "GRAND TOTAL", TextAlignment.Left, isSpan: true, spanCount: 3);
        AddGrandTotalCell(grandRow, grandNetWt > 0 ? grandNetWt.ToString("N3", CultureInfo.InvariantCulture) : "-", TextAlignment.Right);
        AddGrandTotalCell(grandRow, grandTaxable.ToString("N2", IndianCulture), TextAlignment.Right);
        AddGrandTotalCell(grandRow, grandGst.ToString("N2", IndianCulture), TextAlignment.Right);
        AddGrandTotalCell(grandRow, grandTotal.ToString("N2", IndianCulture), TextAlignment.Right);
        rowGroup.Rows.Add(grandRow);

        table.RowGroups.Add(rowGroup);
        doc.Blocks.Add(table);
        return doc;
    }

    private static FlowDocument BuildDaywiseSummaryDocumentCode(InvoiceListViewModel vm)
    {
        var doc = new FlowDocument
        {
            Background = Brushes.White,
            Foreground = Brushes.Black,
            PageWidth = 842,
            PageHeight = 1190,
            PagePadding = new Thickness(30, 28, 30, 28),
            FontFamily = new FontFamily("Segoe UI, Arial, sans-serif"),
            FontSize = 11.5,
            ColumnWidth = double.PositiveInfinity
        };

        AppendCompanyHeader(doc, vm, "Day Wise Sales Summary");

        var table = new Table { CellSpacing = 0, Margin = new Thickness(0, 8, 0, 16) };
        table.Columns.Add(new TableColumn { Width = new GridLength(80) });
        table.Columns.Add(new TableColumn { Width = new GridLength(165) });
        table.Columns.Add(new TableColumn { Width = new GridLength(155) });
        table.Columns.Add(new TableColumn { Width = new GridLength(55) });
        table.Columns.Add(new TableColumn { Width = new GridLength(75) });
        table.Columns.Add(new TableColumn { Width = new GridLength(110) });
        table.Columns.Add(new TableColumn { Width = new GridLength(90) });
        table.Columns.Add(new TableColumn { Width = new GridLength(105) });

        var rowGroup = new TableRowGroup();
        var headerRow = new TableRow();
        AddHeaderCell(headerRow, "Bill Date", TextAlignment.Left);
        AddHeaderCell(headerRow, "Voucher Type", TextAlignment.Left);
        AddHeaderCell(headerRow, "Bill No To Bill No", TextAlignment.Left);
        AddHeaderCell(headerRow, "Qty", TextAlignment.Right);
        AddHeaderCell(headerRow, "Net Weight", TextAlignment.Right);
        AddHeaderCell(headerRow, "Taxable Amount", TextAlignment.Right);
        AddHeaderCell(headerRow, "Tax Amount", TextAlignment.Right);
        AddHeaderCell(headerRow, "Total", TextAlignment.Right);
        rowGroup.Rows.Add(headerRow);

        var groupedByDate = vm.Invoices.OrderBy(i => i.BillDate).GroupBy(i => i.BillDate.Date);
        decimal grandQty = 0m, grandNetWt = 0m, grandTaxable = 0m, grandTax = 0m, grandTotal = 0m;

        foreach (var dateGroup in groupedByDate)
        {
            var date = dateGroup.Key;
            decimal dayQty = 0m, dayNetWt = 0m, dayTaxable = 0m, dayTax = 0m, dayTotal = 0m;

            var vouTypeGroups = dateGroup.GroupBy(i => i.VouType).OrderBy(g => g.Key);
            foreach (var typeGroup in vouTypeGroups)
            {
                var sortedBills = typeGroup.OrderBy(i => i.VouNo).ToList();
                var firstBill = sortedBills.FirstOrDefault()?.VouNo ?? "";
                var lastBill = sortedBills.LastOrDefault()?.VouNo ?? "";
                var billRange = string.Equals(firstBill, lastBill, StringComparison.OrdinalIgnoreCase) ? firstBill : $"{firstBill}-{lastBill}";

                decimal typeQty = typeGroup.Sum(i => (decimal)i.Qty);
                decimal typeNetWt = typeGroup.Sum(i => i.NetWt);
                decimal typeTaxable = typeGroup.Sum(i => GetTaxBreakup(i).TaxableAmt);
                decimal typeTax = typeGroup.Sum(i => GetTaxBreakup(i).TaxAmt);
                decimal typeTotal = typeGroup.Sum(i => i.NetAmt);

                dayQty += typeQty; dayNetWt += typeNetWt; dayTaxable += typeTaxable; dayTax += typeTax; dayTotal += typeTotal;
                grandQty += typeQty; grandNetWt += typeNetWt; grandTaxable += typeTaxable; grandTax += typeTax; grandTotal += typeTotal;

                var row = new TableRow();
                AddDataCell(row, date.ToString("dd/MM/yyyy"), TextAlignment.Left);
                AddDataCell(row, typeGroup.Key, TextAlignment.Left);
                AddDataCell(row, billRange, TextAlignment.Left);
                AddDataCell(row, typeQty.ToString("F2", CultureInfo.InvariantCulture), TextAlignment.Right);
                AddDataCell(row, typeNetWt > 0 ? typeNetWt.ToString("N3", CultureInfo.InvariantCulture) : "-", TextAlignment.Right);
                AddDataCell(row, typeTaxable.ToString("N2", IndianCulture), TextAlignment.Right);
                AddDataCell(row, typeTax.ToString("N2", IndianCulture), TextAlignment.Right);
                AddDataCell(row, typeTotal.ToString("N2", IndianCulture), TextAlignment.Right);
                rowGroup.Rows.Add(row);
            }

            var totalRow = new TableRow();
            AddTotalCell(totalRow, "DATE TOTAL", TextAlignment.Left, isSpan: true, spanCount: 3);
            AddTotalCell(totalRow, dayQty.ToString("F2", CultureInfo.InvariantCulture), TextAlignment.Right);
            AddTotalCell(totalRow, dayNetWt > 0 ? dayNetWt.ToString("N3", CultureInfo.InvariantCulture) : "-", TextAlignment.Right);
            AddTotalCell(totalRow, dayTaxable.ToString("N2", IndianCulture), TextAlignment.Right);
            AddTotalCell(totalRow, dayTax.ToString("N2", IndianCulture), TextAlignment.Right);
            AddTotalCell(totalRow, dayTotal.ToString("N2", IndianCulture), TextAlignment.Right);
            rowGroup.Rows.Add(totalRow);
        }

        var grandRow = new TableRow();
        AddGrandTotalCell(grandRow, "GRAND TOTAL", TextAlignment.Left, isSpan: true, spanCount: 3);
        AddGrandTotalCell(grandRow, grandQty.ToString("F2", CultureInfo.InvariantCulture), TextAlignment.Right);
        AddGrandTotalCell(grandRow, grandNetWt > 0 ? grandNetWt.ToString("N3", CultureInfo.InvariantCulture) : "-", TextAlignment.Right);
        AddGrandTotalCell(grandRow, grandTaxable.ToString("N2", IndianCulture), TextAlignment.Right);
        AddGrandTotalCell(grandRow, grandTax.ToString("N2", IndianCulture), TextAlignment.Right);
        AddGrandTotalCell(grandRow, grandTotal.ToString("N2", IndianCulture), TextAlignment.Right);
        rowGroup.Rows.Add(grandRow);

        table.RowGroups.Add(rowGroup);
        doc.Blocks.Add(table);
        return doc;
    }

    private static FlowDocument BuildVoucherWisePrintDocumentCode(InvoiceListViewModel vm)
    {
        var doc = new FlowDocument
        {
            Background = Brushes.White,
            Foreground = Brushes.Black,
            PageWidth = 842,
            PageHeight = 1190,
            PagePadding = new Thickness(24, 28, 24, 28),
            FontFamily = new FontFamily("Segoe UI, Arial, sans-serif"),
            FontSize = 11,
            ColumnWidth = double.PositiveInfinity
        };

        AppendCompanyHeader(doc, vm, "Voucher Wise Sales Register");

        var table = new Table { CellSpacing = 0, Margin = new Thickness(0, 6, 0, 16) };
        table.Columns.Add(new TableColumn { Width = new GridLength(72) });
        table.Columns.Add(new TableColumn { Width = new GridLength(72) });
        table.Columns.Add(new TableColumn { Width = new GridLength(220) });
        table.Columns.Add(new TableColumn { Width = new GridLength(45) });
        table.Columns.Add(new TableColumn { Width = new GridLength(65) });
        table.Columns.Add(new TableColumn { Width = new GridLength(95) });
        table.Columns.Add(new TableColumn { Width = new GridLength(72) });
        table.Columns.Add(new TableColumn { Width = new GridLength(72) });
        table.Columns.Add(new TableColumn { Width = new GridLength(85) });

        var rowGroup = new TableRowGroup();
        var headerRow = new TableRow();
        AddHeaderCell(headerRow, "Bill Date", TextAlignment.Left);
        AddHeaderCell(headerRow, "Bill No", TextAlignment.Left);
        AddHeaderCell(headerRow, "Customer Details", TextAlignment.Left);
        AddHeaderCell(headerRow, "Qty", TextAlignment.Right);
        AddHeaderCell(headerRow, "Net Weight", TextAlignment.Right);
        AddHeaderCell(headerRow, "Taxable Amount", TextAlignment.Right);
        AddHeaderCell(headerRow, "CGST", TextAlignment.Right);
        AddHeaderCell(headerRow, "SGST", TextAlignment.Right);
        AddHeaderCell(headerRow, "Total", TextAlignment.Right);
        rowGroup.Rows.Add(headerRow);

        var groupedByType = vm.Invoices.OrderBy(i => i.VouType).ThenBy(i => i.BillDate).ThenBy(i => i.VouNo).GroupBy(i => i.VouType);
        decimal grandQty = 0m, grandNetWt = 0m, grandTaxable = 0m, grandCgst = 0m, grandSgst = 0m, grandTotal = 0m;

        foreach (var group in groupedByType)
        {
            var typeHeader = new TableRow();
            var typePara = new Paragraph(new Bold(new Run(group.Key.ToUpperInvariant())) { Foreground = new SolidColorBrush(Color.FromRgb(220, 38, 38)), FontSize = 12 }) { Margin = new Thickness(0, 8, 0, 4) };
            typeHeader.Cells.Add(new TableCell(typePara) { ColumnSpan = 9, Padding = new Thickness(2, 4, 2, 4), BorderThickness = new Thickness(0) });
            rowGroup.Rows.Add(typeHeader);

            decimal typeQty = 0m, typeNetWt = 0m, typeTaxable = 0m, typeCgst = 0m, typeSgst = 0m, typeTotal = 0m;

            foreach (var inv in group)
            {
                var (taxableAmt, _, cgst, sgst) = GetTaxBreakup(inv);
                var qty = (decimal)inv.Qty;

                typeQty += qty; typeNetWt += inv.NetWt; typeTaxable += taxableAmt; typeCgst += cgst; typeSgst += sgst; typeTotal += inv.NetAmt;
                grandQty += qty; grandNetWt += inv.NetWt; grandTaxable += taxableAmt; grandCgst += cgst; grandSgst += sgst; grandTotal += inv.NetAmt;

                var detailsList = new List<string>();
                if (!string.IsNullOrWhiteSpace(inv.CustomerName)) detailsList.Add(inv.CustomerName.Trim());
                if (!string.IsNullOrWhiteSpace(inv.Address)) detailsList.Add(inv.Address.Trim());
                if (!string.IsNullOrWhiteSpace(inv.City)) detailsList.Add(inv.City.Trim());
                var custDetails = detailsList.Count > 0 ? string.Join(", ", detailsList) : "-";

                var row = new TableRow();
                AddDataCell(row, inv.BillDate.ToString("dd/MM/yyyy"), TextAlignment.Left);
                AddDataCell(row, inv.VouNo, TextAlignment.Left);
                AddDataCell(row, custDetails, TextAlignment.Left);
                AddDataCell(row, qty.ToString("F2", CultureInfo.InvariantCulture), TextAlignment.Right);
                AddDataCell(row, inv.NetWt > 0 ? inv.NetWt.ToString("N3", CultureInfo.InvariantCulture) : "-", TextAlignment.Right);
                AddDataCell(row, taxableAmt.ToString("N2", IndianCulture), TextAlignment.Right);
                AddDataCell(row, cgst.ToString("N2", IndianCulture), TextAlignment.Right);
                AddDataCell(row, sgst.ToString("N2", IndianCulture), TextAlignment.Right);
                AddDataCell(row, inv.NetAmt.ToString("N2", IndianCulture), TextAlignment.Right);
                rowGroup.Rows.Add(row);
            }

            var typeTotalRow = new TableRow();
            AddTotalCell(typeTotalRow, $"TOTAL ({group.Key})", TextAlignment.Left, isSpan: true, spanCount: 3);
            AddTotalCell(typeTotalRow, typeQty.ToString("F2", CultureInfo.InvariantCulture), TextAlignment.Right);
            AddTotalCell(typeTotalRow, typeNetWt > 0 ? typeNetWt.ToString("N3", CultureInfo.InvariantCulture) : "-", TextAlignment.Right);
            AddTotalCell(typeTotalRow, typeTaxable.ToString("N2", IndianCulture), TextAlignment.Right);
            AddTotalCell(typeTotalRow, typeCgst.ToString("N2", IndianCulture), TextAlignment.Right);
            AddTotalCell(typeTotalRow, typeSgst.ToString("N2", IndianCulture), TextAlignment.Right);
            AddTotalCell(typeTotalRow, typeTotal.ToString("N2", IndianCulture), TextAlignment.Right);
            rowGroup.Rows.Add(typeTotalRow);
        }

        var grandRow = new TableRow();
        AddGrandTotalCell(grandRow, "GRAND TOTAL", TextAlignment.Left, isSpan: true, spanCount: 3);
        AddGrandTotalCell(grandRow, grandQty.ToString("F2", CultureInfo.InvariantCulture), TextAlignment.Right);
        AddGrandTotalCell(grandRow, grandNetWt > 0 ? grandNetWt.ToString("N3", CultureInfo.InvariantCulture) : "-", TextAlignment.Right);
        AddGrandTotalCell(grandRow, grandTaxable.ToString("N2", IndianCulture), TextAlignment.Right);
        AddGrandTotalCell(grandRow, grandCgst.ToString("N2", IndianCulture), TextAlignment.Right);
        AddGrandTotalCell(grandRow, grandSgst.ToString("N2", IndianCulture), TextAlignment.Right);
        AddGrandTotalCell(grandRow, grandTotal.ToString("N2", IndianCulture), TextAlignment.Right);
        rowGroup.Rows.Add(grandRow);

        table.RowGroups.Add(rowGroup);
        doc.Blocks.Add(table);
        return doc;
    }

    private static FlowDocument BuildVoucherWiseSummaryDocumentCode(InvoiceListViewModel vm)
    {
        var doc = new FlowDocument
        {
            Background = Brushes.White,
            Foreground = Brushes.Black,
            PageWidth = 842,
            PageHeight = 1190,
            PagePadding = new Thickness(30, 28, 30, 28),
            FontFamily = new FontFamily("Segoe UI, Arial, sans-serif"),
            FontSize = 11,
            ColumnWidth = double.PositiveInfinity
        };

        AppendCompanyHeader(doc, vm, "Voucher Wise Sales Summary");

        var table = new Table { CellSpacing = 0, Margin = new Thickness(0, 6, 0, 16) };
        table.Columns.Add(new TableColumn { Width = new GridLength(85) });
        table.Columns.Add(new TableColumn { Width = new GridLength(200) });
        table.Columns.Add(new TableColumn { Width = new GridLength(60) });
        table.Columns.Add(new TableColumn { Width = new GridLength(80) });
        table.Columns.Add(new TableColumn { Width = new GridLength(115) });
        table.Columns.Add(new TableColumn { Width = new GridLength(95) });
        table.Columns.Add(new TableColumn { Width = new GridLength(110) });

        var rowGroup = new TableRowGroup();
        var headerRow = new TableRow();
        AddHeaderCell(headerRow, "Bill Date", TextAlignment.Left);
        AddHeaderCell(headerRow, "Bill No To Bill No", TextAlignment.Left);
        AddHeaderCell(headerRow, "Qty", TextAlignment.Right);
        AddHeaderCell(headerRow, "Net Weight", TextAlignment.Right);
        AddHeaderCell(headerRow, "Taxable Amount", TextAlignment.Right);
        AddHeaderCell(headerRow, "Vat Amount", TextAlignment.Right);
        AddHeaderCell(headerRow, "Total", TextAlignment.Right);
        rowGroup.Rows.Add(headerRow);

        var groupedByType = vm.Invoices.GroupBy(i => i.VouType).OrderBy(g => g.Key);
        decimal grandQty = 0m, grandNetWt = 0m, grandTaxable = 0m, grandTax = 0m, grandTotal = 0m;

        foreach (var typeGroup in groupedByType)
        {
            var typeHeader = new TableRow();
            var typePara = new Paragraph(new Bold(new Run(typeGroup.Key.ToUpperInvariant())) { Foreground = new SolidColorBrush(Color.FromRgb(220, 38, 38)), FontSize = 12 }) { Margin = new Thickness(0, 8, 0, 4) };
            typeHeader.Cells.Add(new TableCell(typePara) { ColumnSpan = 7, Padding = new Thickness(2, 4, 2, 4), BorderThickness = new Thickness(0) });
            rowGroup.Rows.Add(typeHeader);

            decimal typeQty = 0m, typeNetWt = 0m, typeTaxable = 0m, typeTax = 0m, typeTotal = 0m;

            var groupedByDate = typeGroup.OrderBy(i => i.BillDate).GroupBy(i => i.BillDate.Date);
            foreach (var dateGroup in groupedByDate)
            {
                var sortedBills = dateGroup.OrderBy(i => i.VouNo).ToList();
                var firstBill = sortedBills.FirstOrDefault()?.VouNo ?? "";
                var lastBill = sortedBills.LastOrDefault()?.VouNo ?? "";
                var billRange = string.Equals(firstBill, lastBill, StringComparison.OrdinalIgnoreCase) ? firstBill : $"{firstBill}-{lastBill}";

                decimal dayQty = dateGroup.Sum(i => (decimal)i.Qty);
                decimal dayNetWt = dateGroup.Sum(i => i.NetWt);
                decimal dayTaxable = dateGroup.Sum(i => GetTaxBreakup(i).TaxableAmt);
                decimal dayTax = dateGroup.Sum(i => GetTaxBreakup(i).TaxAmt);
                decimal dayTotal = dateGroup.Sum(i => i.NetAmt);

                typeQty += dayQty; typeNetWt += dayNetWt; typeTaxable += dayTaxable; typeTax += dayTax; typeTotal += dayTotal;
                grandQty += dayQty; grandNetWt += dayNetWt; grandTaxable += dayTaxable; grandTax += dayTax; grandTotal += dayTotal;

                var row = new TableRow();
                AddDataCell(row, dateGroup.Key.ToString("dd/MM/yyyy"), TextAlignment.Left);
                AddDataCell(row, billRange, TextAlignment.Left);
                AddDataCell(row, dayQty.ToString("F2", CultureInfo.InvariantCulture), TextAlignment.Right);
                AddDataCell(row, dayNetWt > 0 ? dayNetWt.ToString("N3", CultureInfo.InvariantCulture) : "-", TextAlignment.Right);
                AddDataCell(row, dayTaxable.ToString("N2", IndianCulture), TextAlignment.Right);
                AddDataCell(row, dayTax.ToString("N2", IndianCulture), TextAlignment.Right);
                AddDataCell(row, dayTotal.ToString("N2", IndianCulture), TextAlignment.Right);
                rowGroup.Rows.Add(row);
            }

            var typeTotalRow = new TableRow();
            AddTotalCell(typeTotalRow, $"TOTAL ({typeGroup.Key})", TextAlignment.Left, isSpan: true, spanCount: 2);
            AddTotalCell(typeTotalRow, typeQty.ToString("F2", CultureInfo.InvariantCulture), TextAlignment.Right);
            AddTotalCell(typeTotalRow, typeNetWt > 0 ? typeNetWt.ToString("N3", CultureInfo.InvariantCulture) : "-", TextAlignment.Right);
            AddTotalCell(typeTotalRow, typeTaxable.ToString("N2", IndianCulture), TextAlignment.Right);
            AddTotalCell(typeTotalRow, typeTax.ToString("N2", IndianCulture), TextAlignment.Right);
            AddTotalCell(typeTotalRow, typeTotal.ToString("N2", IndianCulture), TextAlignment.Right);
            rowGroup.Rows.Add(typeTotalRow);
        }

        var grandRow = new TableRow();
        AddGrandTotalCell(grandRow, "GRAND TOTAL", TextAlignment.Left, isSpan: true, spanCount: 2);
        AddGrandTotalCell(grandRow, grandQty.ToString("F2", CultureInfo.InvariantCulture), TextAlignment.Right);
        AddGrandTotalCell(grandRow, grandNetWt > 0 ? grandNetWt.ToString("N3", CultureInfo.InvariantCulture) : "-", TextAlignment.Right);
        AddGrandTotalCell(grandRow, grandTaxable.ToString("N2", IndianCulture), TextAlignment.Right);
        AddGrandTotalCell(grandRow, grandTax.ToString("N2", IndianCulture), TextAlignment.Right);
        AddGrandTotalCell(grandRow, grandTotal.ToString("N2", IndianCulture), TextAlignment.Right);
        rowGroup.Rows.Add(grandRow);

        table.RowGroups.Add(rowGroup);
        doc.Blocks.Add(table);
        return doc;
    }

    private static FlowDocument BuildMonthwiseSummaryDocumentCode(InvoiceListViewModel vm)
    {
        var doc = new FlowDocument
        {
            Background = Brushes.White,
            Foreground = Brushes.Black,
            PageWidth = 842,
            PageHeight = 1190,
            PagePadding = new Thickness(30, 28, 30, 28),
            FontFamily = new FontFamily("Segoe UI, Arial, sans-serif"),
            FontSize = 11,
            ColumnWidth = double.PositiveInfinity
        };

        AppendCompanyHeader(doc, vm, "Month Wise Sales Summary");

        var table = new Table { CellSpacing = 0, Margin = new Thickness(0, 6, 0, 16) };
        table.Columns.Add(new TableColumn { Width = new GridLength(80) });
        table.Columns.Add(new TableColumn { Width = new GridLength(165) });
        table.Columns.Add(new TableColumn { Width = new GridLength(160) });
        table.Columns.Add(new TableColumn { Width = new GridLength(80) });
        table.Columns.Add(new TableColumn { Width = new GridLength(120) });
        table.Columns.Add(new TableColumn { Width = new GridLength(95) });
        table.Columns.Add(new TableColumn { Width = new GridLength(110) });

        var rowGroup = new TableRowGroup();
        var headerRow = new TableRow();
        AddHeaderCell(headerRow, "Bill Date", TextAlignment.Left);
        AddHeaderCell(headerRow, "Voucher Type", TextAlignment.Left);
        AddHeaderCell(headerRow, "Bill No", TextAlignment.Left);
        AddHeaderCell(headerRow, "Net Wt", TextAlignment.Right);
        AddHeaderCell(headerRow, "Taxable Amount", TextAlignment.Right);
        AddHeaderCell(headerRow, "GST Amount", TextAlignment.Right);
        AddHeaderCell(headerRow, "Total", TextAlignment.Right);
        rowGroup.Rows.Add(headerRow);

        var groupedByMonth = vm.Invoices.GroupBy(i => new { i.BillDate.Year, i.BillDate.Month }).OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month);
        decimal grandTaxable = 0m, grandGst = 0m, grandTotal = 0m, grandNetWt = 0m;

        foreach (var monthGroup in groupedByMonth)
        {
            var dt = new DateTime(monthGroup.Key.Year, monthGroup.Key.Month, 1);
            var monthName = dt.ToString("MMMM");

            var typeGroups = monthGroup.GroupBy(i => i.VouType).OrderBy(g => g.Key);
            foreach (var typeGroup in typeGroups)
            {
                var sortedBills = typeGroup.OrderBy(i => i.VouNo).ToList();
                var firstBill = sortedBills.FirstOrDefault()?.VouNo ?? "";
                var lastBill = sortedBills.LastOrDefault()?.VouNo ?? "";
                var billRange = string.Equals(firstBill, lastBill, StringComparison.OrdinalIgnoreCase) ? firstBill : $"{firstBill} - {lastBill}";

                decimal netWt = typeGroup.Sum(i => i.NetWt);
                decimal taxable = typeGroup.Sum(i => GetTaxBreakup(i).TaxableAmt);
                decimal gst = typeGroup.Sum(i => GetTaxBreakup(i).TaxAmt);
                decimal total = typeGroup.Sum(i => i.NetAmt);

                grandNetWt += netWt; grandTaxable += taxable; grandGst += gst; grandTotal += total;

                var row = new TableRow();
                AddDataCell(row, monthName, TextAlignment.Left);
                AddDataCell(row, typeGroup.Key, TextAlignment.Left);
                AddDataCell(row, billRange, TextAlignment.Left);
                AddDataCell(row, netWt.ToString("N2", CultureInfo.InvariantCulture), TextAlignment.Right);
                AddDataCell(row, taxable.ToString("N2", IndianCulture), TextAlignment.Right);
                AddDataCell(row, gst.ToString("N2", IndianCulture), TextAlignment.Right);
                AddDataCell(row, total.ToString("N2", IndianCulture), TextAlignment.Right);
                rowGroup.Rows.Add(row);
            }
        }

        var grandRow = new TableRow();
        AddGrandTotalCell(grandRow, "GRAND TOTAL", TextAlignment.Left, isSpan: true, spanCount: 3, isRed: true);
        AddGrandTotalCell(grandRow, grandNetWt.ToString("N2", CultureInfo.InvariantCulture), TextAlignment.Right, isRed: true);
        AddGrandTotalCell(grandRow, grandTaxable.ToString("N2", IndianCulture), TextAlignment.Right, isRed: true);
        AddGrandTotalCell(grandRow, grandGst.ToString("N2", IndianCulture), TextAlignment.Right, isRed: true);
        AddGrandTotalCell(grandRow, grandTotal.ToString("N2", IndianCulture), TextAlignment.Right, isRed: true);
        rowGroup.Rows.Add(grandRow);

        table.RowGroups.Add(rowGroup);
        doc.Blocks.Add(table);
        return doc;
    }

    private static void AppendCompanyHeader(FlowDocument doc, InvoiceListViewModel vm, string reportTitle)
    {
        var (companyName, address, gstin) = GetCompanyHeaderDetails(vm);

        var headerTable = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 6) };
        headerTable.Columns.Add(new TableColumn { Width = new GridLength(1.3, GridUnitType.Star) });
        headerTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });

        var rowGroup = new TableRowGroup();
        var row1 = new TableRow();
        row1.Cells.Add(new TableCell(new Paragraph(new Bold(new Run(companyName)) { FontSize = 14.5 }) { Margin = new Thickness(0, 0, 0, 2) }));
        row1.Cells.Add(new TableCell(new Paragraph(new Run($"From Date :  {vm.FromDate:dd/MM/yyyy}      To Date :  {vm.ToDate:dd/MM/yyyy}"))
        {
            FontSize = 11.5,
            FontWeight = FontWeights.SemiBold,
            TextAlignment = TextAlignment.Right,
            Margin = new Thickness(0, 2, 0, 2)
        }));
        rowGroup.Rows.Add(row1);

        var row2 = new TableRow();
        row2.Cells.Add(new TableCell(new Paragraph(new Run(address)) { FontSize = 10.5, Margin = new Thickness(0, 0, 0, 2) }));
        row2.Cells.Add(new TableCell(new Paragraph { Margin = new Thickness(0) }));
        rowGroup.Rows.Add(row2);

        var row3 = new TableRow();
        var tinText = !string.IsNullOrWhiteSpace(gstin) ? $"TIN: GSTIN: {gstin}" : string.Empty;
        row3.Cells.Add(new TableCell(new Paragraph(new Run(tinText)) { FontSize = 10.5, Margin = new Thickness(0, 0, 0, 2) }));
        row3.Cells.Add(new TableCell(new Paragraph(new Run(reportTitle)) { FontSize = 10.5, Foreground = Brushes.Gray, TextAlignment = TextAlignment.Right, Margin = new Thickness(0, 0, 0, 2) }));
        rowGroup.Rows.Add(row3);

        headerTable.RowGroups.Add(rowGroup);
        doc.Blocks.Add(headerTable);
    }

    private static void AddHeaderCell(TableRow row, string text, TextAlignment alignment)
    {
        var p = new Paragraph(new Bold(new Run(text))) { TextAlignment = alignment, FontSize = 11, Margin = new Thickness(0) };
        row.Cells.Add(new TableCell(p) { Padding = new Thickness(4, 5, 4, 5), BorderBrush = Brushes.Black, BorderThickness = new Thickness(0, 2.0, 0, 1.0) });
    }

    private static void AddDataCell(TableRow row, string text, TextAlignment alignment, bool isBold = false)
    {
        Inline inline = new Run(text);
        if (isBold) inline = new Bold(inline);
        var p = new Paragraph(inline) { TextAlignment = alignment, FontSize = 10.5, Margin = new Thickness(0) };
        row.Cells.Add(new TableCell(p) { Padding = new Thickness(4, 2.5, 4, 2.5), BorderBrush = Brushes.Transparent, BorderThickness = new Thickness(0) });
    }

    private static void AddTotalCell(TableRow row, string text, TextAlignment alignment, bool isSpan = false, int spanCount = 1)
    {
        var p = new Paragraph(new Bold(new Run(text))) { TextAlignment = alignment, FontSize = 11, Margin = new Thickness(0) };
        var cell = new TableCell(p) { Padding = new Thickness(4, 4, 4, 4), BorderBrush = Brushes.Black, BorderThickness = new Thickness(0, 1.0, 0, 1.0) };
        if (isSpan && spanCount > 1) cell.ColumnSpan = spanCount;
        row.Cells.Add(cell);
    }

    private static void AddGrandTotalCell(TableRow row, string text, TextAlignment alignment, bool isSpan = false, int spanCount = 1, bool isRed = false)
    {
        var run = new Run(text);
        if (isRed) run.Foreground = new SolidColorBrush(Color.FromRgb(220, 38, 38));
        var p = new Paragraph(new Bold(run)) { TextAlignment = alignment, FontSize = 11.5, Margin = new Thickness(0) };
        var cell = new TableCell(p) { Padding = new Thickness(4, 5, 4, 5), BorderBrush = Brushes.Black, BorderThickness = new Thickness(0, 1.5, 0, 2.5) };
        if (isSpan && spanCount > 1) cell.ColumnSpan = spanCount;
        row.Cells.Add(cell);
    }

    #endregion
}
