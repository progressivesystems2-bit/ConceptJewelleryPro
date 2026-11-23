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

public static class PurchaseRegisterFlowDocumentBuilder
{
    private static readonly CultureInfo IndianCulture = new("en-IN");

    public static FlowDocument CreateReportDocument(PurchaseRegisterViewModel vm)
    {
        return vm.SelectedPrintOption switch
        {
            "ItemwiseDetailedPrint" or "Itemwise Print Detailed" => CreateItemwiseDetailedPrintDocument(vm),
            "DayWisePrint" or "Day Wise Purchase Register" => CreateDaywisePrintDocument(vm),
            "PartyWisePrint" or "Party Wise Purchase Register" => CreatePartyWisePrintDocument(vm),
            "VoucherWisePrint" or "Voucher Wise Purchase Register" => CreateVoucherWisePrintDocument(vm),
            "ItemWiseSummary" or "Item Wise Purchase Summary" => CreateItemWiseSummaryDocument(vm),
            _ => CreateItemwiseDetailedPrintDocument(vm)
        };
    }

    public static FlowDocument CreateItemwiseDetailedPrintDocument(PurchaseRegisterViewModel vm)
    {
        var templatePath = ResolveTemplatePath("PurchaseRegisterDetailedTemplate.xaml");
        if (File.Exists(templatePath))
        {
            try
            {
                var template = File.ReadAllText(templatePath);
                var groupTotalMatch = Regex.Match(template, @"<!--\s*<ItemTotalRow>\s*-->([\s\S]*?)<!--\s*</ItemTotalRow>\s*-->");
                var dataRowMatch = Regex.Match(template, @"<!--\s*<DataRows>\s*-->([\s\S]*?)<!--\s*</DataRows>\s*-->");
                var grandTotalMatch = Regex.Match(template, @"<!--\s*<GrandTotalRow>\s*-->([\s\S]*?)<!--\s*</GrandTotalRow>\s*-->");

                if (groupTotalMatch.Success && dataRowMatch.Success && grandTotalMatch.Success)
                {
                    var groupTotalTemplate = groupTotalMatch.Groups[1].Value;
                    var dataRowTemplate = dataRowMatch.Groups[1].Value;
                    var grandTotalTemplate = grandTotalMatch.Groups[1].Value;

                    var sb = new StringBuilder();

                    // Flatten items with their parent purchase
                    var flatItems = new List<(PurchaseRegisterItem Purchase, PurchaseSubform Subform)>();
                    foreach (var p in vm.Purchases)
                    {
                        if (p.Items.Count > 0)
                        {
                            foreach (var item in p.Items)
                            {
                                flatItems.Add((p, item));
                            }
                        }
                        else
                        {
                            // Purchase without subform items - create dummy subform
                            flatItems.Add((p, new PurchaseSubform
                            {
                                ItemName = "PURCHASE GENERAL",
                                NtWt = p.NtWt,
                                GrWt = p.GrWt,
                                Qty = p.TotalQty > 0 ? p.TotalQty : 1,
                                Rate = p.NtWt > 0 ? Math.Round(p.TaxableAmount / p.NtWt, 2) : 0m,
                                TaxableAmount = p.TaxableAmount,
                                GstAmt = p.TaxAmt,
                                TotalAmt = p.NetAmt
                            }));
                        }
                    }

                    // Group by ItemName
                    var groupedByItem = flatItems
                        .GroupBy(x => string.IsNullOrWhiteSpace(x.Subform.ItemName) ? "GENERAL ORNAMENTS" : x.Subform.ItemName.Trim())
                        .OrderBy(g => g.Key);

                    decimal grandNetWt = 0m, grandTaxable = 0m, grandIgst = 0m, grandCgst = 0m, grandSgst = 0m, grandGst = 0m, grandROff = 0m, grandNetAmt = 0m;

                    foreach (var group in groupedByItem)
                    {
                        var itemGroupName = group.Key;
                        decimal grpNetWt = 0m, grpTaxable = 0m, grpIgst = 0m, grpCgst = 0m, grpSgst = 0m, grpGst = 0m, grpROff = 0m, grpNetAmt = 0m;

                        var detailRowsSb = new StringBuilder();

                        foreach (var (purchase, sub) in group.OrderBy(x => x.Purchase.BillDate).ThenBy(x => x.Purchase.VouNo))
                        {
                            var lineNetWt = sub.NtWt > 0 ? sub.NtWt : (sub.GrWt > 0 ? sub.GrWt : 0m);
                            var lineTaxable = sub.TaxableAmount > 0 ? sub.TaxableAmount : (sub.Amount > 0 ? sub.Amount : sub.GrossAmt);
                            var lineGst = sub.GstAmt > 0 ? sub.GstAmt : (sub.TotalAmt > lineTaxable ? sub.TotalAmt - lineTaxable : 0m);
                            var lineNet = sub.TotalAmt > 0 ? sub.TotalAmt : (lineTaxable + lineGst);

                            decimal lineIgst = 0m, lineCgst = 0m, lineSgst = 0m;
                            if (purchase.IGSTAmt > 0)
                            {
                                lineIgst = lineGst;
                            }
                            else
                            {
                                lineCgst = Math.Round(lineGst / 2m, 2);
                                lineSgst = lineGst - lineCgst;
                            }

                            decimal lineRate = sub.Rate > 0 ? sub.Rate : (lineNetWt > 0 ? Math.Round(lineTaxable / lineNetWt, 2) : 0m);
                            decimal lineROff = 0m;

                            grpNetWt += lineNetWt;
                            grpTaxable += lineTaxable;
                            grpIgst += lineIgst;
                            grpCgst += lineCgst;
                            grpSgst += lineSgst;
                            grpGst += lineGst;
                            grpROff += lineROff;
                            grpNetAmt += lineNet;

                            grandNetWt += lineNetWt;
                            grandTaxable += lineTaxable;
                            grandIgst += lineIgst;
                            grandCgst += lineCgst;
                            grandSgst += lineSgst;
                            grandGst += lineGst;
                            grandROff += lineROff;
                            grandNetAmt += lineNet;

                            var purNoDisplay = !string.IsNullOrWhiteSpace(purchase.SuppBillNo)
                                ? (!string.IsNullOrWhiteSpace(purchase.VouNo) ? $"{purchase.VouNo} / {purchase.SuppBillNo}" : purchase.SuppBillNo)
                                : purchase.VouNo;

                            var row = dataRowTemplate
                                .Replace("{{Date}}", purchase.BillDate.ToString("dd/MM/yy"))
                                .Replace("{{PurNo}}", EscapeXml(purNoDisplay))
                                .Replace("{{PartyName}}", EscapeXml(purchase.PartyDisplayName))
                                .Replace("{{PartyDetails}}", EscapeXml(purchase.PartyIdentification))
                                .Replace("{{NetWeight}}", lineNetWt.ToString("F3", CultureInfo.InvariantCulture))
                                .Replace("{{RatePerGr}}", lineRate.ToString("N2", IndianCulture))
                                .Replace("{{TaxableAmt}}", lineTaxable.ToString("N2", IndianCulture))
                                .Replace("{{IGST}}", lineIgst.ToString("N2", IndianCulture))
                                .Replace("{{CGST}}", lineCgst.ToString("N2", IndianCulture))
                                .Replace("{{SGST}}", lineSgst.ToString("N2", IndianCulture))
                                .Replace("{{GST}}", lineGst.ToString("N2", IndianCulture))
                                .Replace("{{ROff}}", lineROff.ToString("N2", IndianCulture))
                                .Replace("{{NetAmount}}", lineNet.ToString("N2", IndianCulture));

                            detailRowsSb.Append(row);
                        }

                        // Add group total header/summary row
                        var groupTotalRow = groupTotalTemplate
                            .Replace("{{ItemGroupName}}", EscapeXml(itemGroupName))
                            .Replace("{{GroupNetWeight}}", grpNetWt.ToString("F2", CultureInfo.InvariantCulture))
                            .Replace("{{GroupTaxableAmt}}", grpTaxable.ToString("N2", IndianCulture))
                            .Replace("{{GroupIGST}}", grpIgst.ToString("N2", IndianCulture))
                            .Replace("{{GroupCGST}}", grpCgst.ToString("N2", IndianCulture))
                            .Replace("{{GroupSGST}}", grpSgst.ToString("N2", IndianCulture))
                            .Replace("{{GroupGST}}", grpGst.ToString("N2", IndianCulture))
                            .Replace("{{GroupROff}}", grpROff.ToString("N2", IndianCulture))
                            .Replace("{{GroupNetAmount}}", grpNetAmt.ToString("N2", IndianCulture));

                        sb.Append(groupTotalRow);
                        sb.Append(detailRowsSb);
                    }

                    // Grand total row
                    var grandTotalRow = grandTotalTemplate
                        .Replace("{{GrandNetWeight}}", grandNetWt.ToString("F2", CultureInfo.InvariantCulture))
                        .Replace("{{GrandTaxableAmt}}", grandTaxable.ToString("N2", IndianCulture))
                        .Replace("{{GrandIGST}}", grandIgst.ToString("N2", IndianCulture))
                        .Replace("{{GrandCGST}}", grandCgst.ToString("N2", IndianCulture))
                        .Replace("{{GrandSGST}}", grandSgst.ToString("N2", IndianCulture))
                        .Replace("{{GrandGST}}", grandGst.ToString("N2", IndianCulture))
                        .Replace("{{GrandROff}}", grandROff.ToString("N2", IndianCulture))
                        .Replace("{{GrandNetAmount}}", grandNetAmt.ToString("N2", IndianCulture));

                    sb.Append(grandTotalRow);

                    var fullTableRegex = new Regex(@"<!--\s*<ItemTotalRow>\s*-->[\s\S]*?<!--\s*</GrandTotalRow>\s*-->");
                    var populated = fullTableRegex.Replace(template, sb.ToString());

                    populated = ReplaceHeaderPlaceholders(populated, vm, "PURCHASE REGISTER DETAILED");

                    using var reader = XmlReader.Create(new StringReader(populated));
                    return (FlowDocument)XamlReader.Load(reader);
                }
            }
            catch
            {
                // Fallback to programmatic builder
            }
        }

        return BuildItemwiseDetailedDocumentCode(vm);
    }

    public static FlowDocument CreateDaywisePrintDocument(PurchaseRegisterViewModel vm)
    {
        return BuildDaywiseDocumentCode(vm);
    }

    public static FlowDocument CreatePartyWisePrintDocument(PurchaseRegisterViewModel vm)
    {
        return BuildPartyWiseDocumentCode(vm);
    }

    public static FlowDocument CreateVoucherWisePrintDocument(PurchaseRegisterViewModel vm)
    {
        return BuildVoucherWiseDocumentCode(vm);
    }

    public static FlowDocument CreateItemWiseSummaryDocument(PurchaseRegisterViewModel vm)
    {
        var templatePath = ResolveTemplatePath("PurchaseRegisterSummaryTemplate.xaml");
        if (File.Exists(templatePath))
        {
            try
            {
                var template = File.ReadAllText(templatePath);
                var groupTotalMatch = Regex.Match(template, @"<!--\s*<ItemTotalRow>\s*-->([\s\S]*?)<!--\s*</ItemTotalRow>\s*-->");
                var dataRowMatch = Regex.Match(template, @"<!--\s*<DataRows>\s*-->([\s\S]*?)<!--\s*</DataRows>\s*-->");
                var grandTotalMatch = Regex.Match(template, @"<!--\s*<GrandTotalRow>\s*-->([\s\S]*?)<!--\s*</GrandTotalRow>\s*-->");

                if (groupTotalMatch.Success && dataRowMatch.Success && grandTotalMatch.Success)
                {
                    var groupTotalTemplate = groupTotalMatch.Groups[1].Value;
                    var dataRowTemplate = dataRowMatch.Groups[1].Value;
                    var grandTotalTemplate = grandTotalMatch.Groups[1].Value;

                    var sb = new StringBuilder();

                    // Flatten items with their parent purchase
                    var flatItems = new List<(PurchaseRegisterItem Purchase, PurchaseSubform Subform)>();
                    foreach (var p in vm.Purchases)
                    {
                        if (p.Items.Count > 0)
                        {
                            foreach (var item in p.Items)
                            {
                                flatItems.Add((p, item));
                            }
                        }
                        else
                        {
                            flatItems.Add((p, new PurchaseSubform
                            {
                                ItemName = "PURCHASE GENERAL",
                                NtWt = p.NtWt,
                                GrWt = p.GrWt,
                                Qty = p.TotalQty > 0 ? p.TotalQty : 1,
                                Rate = p.NtWt > 0 ? Math.Round(p.TaxableAmount / p.NtWt, 2) : 0m,
                                TaxableAmount = p.TaxableAmount,
                                GstAmt = p.TaxAmt,
                                TotalAmt = p.NetAmt
                            }));
                        }
                    }

                    // Group by ItemName
                    var groupedByItem = flatItems
                        .GroupBy(x => string.IsNullOrWhiteSpace(x.Subform.ItemName) ? "GENERAL ORNAMENTS" : x.Subform.ItemName.Trim())
                        .OrderBy(g => g.Key);

                    decimal grandGrWt = 0m, grandNetWt = 0m, grandTaxable = 0m, grandTaxAmt = 0m, grandROff = 0m, grandNetAmt = 0m;

                    foreach (var group in groupedByItem)
                    {
                        var itemGroupName = group.Key;
                        decimal grpGrWt = 0m, grpNetWt = 0m, grpTaxable = 0m, grpTaxAmt = 0m, grpROff = 0m, grpNetAmt = 0m;

                        var detailRowsSb = new StringBuilder();

                        foreach (var (purchase, sub) in group.OrderBy(x => x.Purchase.BillDate).ThenBy(x => x.Purchase.VouNo))
                        {
                            var lineGrWt = sub.GrWt > 0 ? sub.GrWt : (sub.NtWt > 0 ? sub.NtWt : 0m);
                            var lineNetWt = sub.NtWt > 0 ? sub.NtWt : lineGrWt;
                            var lineTaxable = sub.TaxableAmount > 0 ? sub.TaxableAmount : (sub.Amount > 0 ? sub.Amount : sub.GrossAmt);
                            var lineTax = sub.GstAmt > 0 ? sub.GstAmt : (sub.TotalAmt > lineTaxable ? sub.TotalAmt - lineTaxable : 0m);
                            var lineNet = sub.TotalAmt > 0 ? sub.TotalAmt : (lineTaxable + lineTax);
                            var lineROff = 0m;
                            var insideOutside = purchase.IGSTAmt > 0 ? "Outside" : "Inside";

                            grpGrWt += lineGrWt;
                            grpNetWt += lineNetWt;
                            grpTaxable += lineTaxable;
                            grpTaxAmt += lineTax;
                            grpROff += lineROff;
                            grpNetAmt += lineNet;

                            grandGrWt += lineGrWt;
                            grandNetWt += lineNetWt;
                            grandTaxable += lineTaxable;
                            grandTaxAmt += lineTax;
                            grandROff += lineROff;
                            grandNetAmt += lineNet;

                            var row = dataRowTemplate
                                .Replace("{{Date}}", purchase.BillDate.ToString("dd/MM/yy"))
                                .Replace("{{InsideOutside}}", insideOutside)
                                .Replace("{{ItemName}}", EscapeXml(itemGroupName))
                                .Replace("{{GrWeight}}", lineGrWt.ToString("F3", CultureInfo.InvariantCulture))
                                .Replace("{{NetWeight}}", lineNetWt.ToString("F3", CultureInfo.InvariantCulture))
                                .Replace("{{TaxableAmt}}", lineTaxable.ToString("N2", IndianCulture))
                                .Replace("{{TaxAmt}}", lineTax.ToString("N2", IndianCulture))
                                .Replace("{{ROff}}", lineROff.ToString("N2", IndianCulture))
                                .Replace("{{NetAmount}}", lineNet.ToString("N2", IndianCulture));

                            detailRowsSb.Append(row);
                        }

                        // Add group total header/summary row
                        var groupTotalRow = groupTotalTemplate
                            .Replace("{{GroupGrWeight}}", grpGrWt.ToString("F3", CultureInfo.InvariantCulture))
                            .Replace("{{GroupNetWeight}}", grpNetWt.ToString("F3", CultureInfo.InvariantCulture))
                            .Replace("{{GroupTaxableAmt}}", grpTaxable.ToString("N2", IndianCulture))
                            .Replace("{{GroupTaxAmt}}", grpTaxAmt.ToString("N2", IndianCulture))
                            .Replace("{{GroupROff}}", grpROff.ToString("N2", IndianCulture))
                            .Replace("{{GroupNetAmount}}", grpNetAmt.ToString("N2", IndianCulture));

                        sb.Append(detailRowsSb);
                        sb.Append(groupTotalRow);
                    }

                    // Grand total row
                    var grandTotalRow = grandTotalTemplate
                        .Replace("{{GrandGrWeight}}", grandGrWt.ToString("F3", CultureInfo.InvariantCulture))
                        .Replace("{{GrandNetWeight}}", grandNetWt.ToString("F3", CultureInfo.InvariantCulture))
                        .Replace("{{GrandTaxableAmt}}", grandTaxable.ToString("N2", IndianCulture))
                        .Replace("{{GrandTaxAmt}}", grandTaxAmt.ToString("N2", IndianCulture))
                        .Replace("{{GrandROff}}", grandROff.ToString("N2", IndianCulture))
                        .Replace("{{GrandNetAmount}}", grandNetAmt.ToString("N2", IndianCulture));

                    sb.Append(grandTotalRow);

                    var fullTableRegex = new Regex(@"<!--\s*<DataRows>\s*-->[\s\S]*?<!--\s*</GrandTotalRow>\s*-->");
                    var populated = fullTableRegex.Replace(template, sb.ToString());

                    populated = ReplaceHeaderPlaceholders(populated, vm, "PURCHASE REGISTER SUMMARY");

                    using var reader = XmlReader.Create(new StringReader(populated));
                    return (FlowDocument)XamlReader.Load(reader);
                }
            }
            catch
            {
                // Fallback to programmatic builder
            }
        }

        return BuildItemWiseSummaryDocumentCode(vm);
    }

    #region Code-Based Fallback Builders

    private static FlowDocument BuildItemwiseDetailedDocumentCode(PurchaseRegisterViewModel vm)
    {
        var doc = new FlowDocument
        {
            Background = Brushes.White,
            Foreground = Brushes.Black,
            PageWidth = 1050,
            PageHeight = 750,
            PagePadding = new Thickness(24, 20, 24, 20),
            FontFamily = new FontFamily("Segoe UI, Arial, sans-serif"),
            FontSize = 9.5,
            ColumnWidth = double.PositiveInfinity
        };

        AppendCompanyHeader(doc, vm, "PURCHASE REGISTER DETAILED");

        var table = new Table { CellSpacing = 0, Margin = new Thickness(0, 4, 0, 12) };
        table.Columns.Add(new TableColumn { Width = new GridLength(60) });
        table.Columns.Add(new TableColumn { Width = new GridLength(100) });
        table.Columns.Add(new TableColumn { Width = new GridLength(220) });
        table.Columns.Add(new TableColumn { Width = new GridLength(65) });
        table.Columns.Add(new TableColumn { Width = new GridLength(70) });
        table.Columns.Add(new TableColumn { Width = new GridLength(85) });
        table.Columns.Add(new TableColumn { Width = new GridLength(55) });
        table.Columns.Add(new TableColumn { Width = new GridLength(55) });
        table.Columns.Add(new TableColumn { Width = new GridLength(55) });
        table.Columns.Add(new TableColumn { Width = new GridLength(55) });
        table.Columns.Add(new TableColumn { Width = new GridLength(45) });
        table.Columns.Add(new TableColumn { Width = new GridLength(90) });

        var rowGroup = new TableRowGroup();
        var headerRow = new TableRow();
        AddHeaderCell(headerRow, "Date", TextAlignment.Left);
        AddHeaderCell(headerRow, "Pur No/ Supp Bill No", TextAlignment.Left);
        AddHeaderCell(headerRow, "Party", TextAlignment.Left);
        AddHeaderCell(headerRow, "Net Weight", TextAlignment.Right);
        AddHeaderCell(headerRow, "Rate / Gr", TextAlignment.Right);
        AddHeaderCell(headerRow, "Taxable Amt", TextAlignment.Right);
        AddHeaderCell(headerRow, "IGST", TextAlignment.Right);
        AddHeaderCell(headerRow, "CGST", TextAlignment.Right);
        AddHeaderCell(headerRow, "SGST", TextAlignment.Right);
        AddHeaderCell(headerRow, "GST", TextAlignment.Right);
        AddHeaderCell(headerRow, "R.Off", TextAlignment.Right);
        AddHeaderCell(headerRow, "Net Amount", TextAlignment.Right);
        rowGroup.Rows.Add(headerRow);

        var flatItems = new List<(PurchaseRegisterItem Purchase, PurchaseSubform Subform)>();
        foreach (var p in vm.Purchases)
        {
            if (p.Items.Count > 0)
            {
                foreach (var item in p.Items)
                {
                    flatItems.Add((p, item));
                }
            }
            else
            {
                flatItems.Add((p, new PurchaseSubform
                {
                    ItemName = "PURCHASE GENERAL",
                    NtWt = p.NtWt,
                    GrWt = p.GrWt,
                    Qty = p.TotalQty > 0 ? p.TotalQty : 1,
                    Rate = p.NtWt > 0 ? Math.Round(p.TaxableAmount / p.NtWt, 2) : 0m,
                    TaxableAmount = p.TaxableAmount,
                    GstAmt = p.TaxAmt,
                    TotalAmt = p.NetAmt
                }));
            }
        }

        var groupedByItem = flatItems
            .GroupBy(x => string.IsNullOrWhiteSpace(x.Subform.ItemName) ? "GENERAL ORNAMENTS" : x.Subform.ItemName.Trim())
            .OrderBy(g => g.Key);

        decimal grandNetWt = 0m, grandTaxable = 0m, grandIgst = 0m, grandCgst = 0m, grandSgst = 0m, grandGst = 0m, grandROff = 0m, grandNetAmt = 0m;

        foreach (var group in groupedByItem)
        {
            var itemGroupName = group.Key;
            decimal grpNetWt = 0m, grpTaxable = 0m, grpIgst = 0m, grpCgst = 0m, grpSgst = 0m, grpGst = 0m, grpROff = 0m, grpNetAmt = 0m;

            var itemRows = new List<TableRow>();

            foreach (var (purchase, sub) in group.OrderBy(x => x.Purchase.BillDate).ThenBy(x => x.Purchase.VouNo))
            {
                var lineNetWt = sub.NtWt > 0 ? sub.NtWt : (sub.GrWt > 0 ? sub.GrWt : 0m);
                var lineTaxable = sub.TaxableAmount > 0 ? sub.TaxableAmount : (sub.Amount > 0 ? sub.Amount : sub.GrossAmt);
                var lineGst = sub.GstAmt > 0 ? sub.GstAmt : (sub.TotalAmt > lineTaxable ? sub.TotalAmt - lineTaxable : 0m);
                var lineNet = sub.TotalAmt > 0 ? sub.TotalAmt : (lineTaxable + lineGst);

                decimal lineIgst = 0m, lineCgst = 0m, lineSgst = 0m;
                if (purchase.IGSTAmt > 0)
                {
                    lineIgst = lineGst;
                }
                else
                {
                    lineCgst = Math.Round(lineGst / 2m, 2);
                    lineSgst = lineGst - lineCgst;
                }

                decimal lineRate = sub.Rate > 0 ? sub.Rate : (lineNetWt > 0 ? Math.Round(lineTaxable / lineNetWt, 2) : 0m);
                decimal lineROff = 0m;

                grpNetWt += lineNetWt;
                grpTaxable += lineTaxable;
                grpIgst += lineIgst;
                grpCgst += lineCgst;
                grpSgst += lineSgst;
                grpGst += lineGst;
                grpROff += lineROff;
                grpNetAmt += lineNet;

                grandNetWt += lineNetWt;
                grandTaxable += lineTaxable;
                grandIgst += lineIgst;
                grandCgst += lineCgst;
                grandSgst += lineSgst;
                grandGst += lineGst;
                grandROff += lineROff;
                grandNetAmt += lineNet;

                var purNoDisplay = !string.IsNullOrWhiteSpace(purchase.SuppBillNo)
                    ? (!string.IsNullOrWhiteSpace(purchase.VouNo) ? $"{purchase.VouNo} / {purchase.SuppBillNo}" : purchase.SuppBillNo)
                    : purchase.VouNo;

                var row = new TableRow();
                AddDataCell(row, purchase.BillDate.ToString("dd/MM/yy"), TextAlignment.Left);
                AddDataCell(row, purNoDisplay, TextAlignment.Left);
                AddPartyCell(row, purchase.PartyDisplayName, purchase.PartyIdentification);
                AddDataCell(row, lineNetWt.ToString("F3", CultureInfo.InvariantCulture), TextAlignment.Right);
                AddDataCell(row, lineRate.ToString("N2", IndianCulture), TextAlignment.Right);
                AddDataCell(row, lineTaxable.ToString("N2", IndianCulture), TextAlignment.Right);
                AddDataCell(row, lineIgst.ToString("N2", IndianCulture), TextAlignment.Right);
                AddDataCell(row, lineCgst.ToString("N2", IndianCulture), TextAlignment.Right);
                AddDataCell(row, lineSgst.ToString("N2", IndianCulture), TextAlignment.Right);
                AddDataCell(row, lineGst.ToString("N2", IndianCulture), TextAlignment.Right);
                AddDataCell(row, lineROff.ToString("N2", IndianCulture), TextAlignment.Right);
                AddDataCell(row, lineNet.ToString("N2", IndianCulture), TextAlignment.Right);
                itemRows.Add(row);
            }

            // Group Total Row
            var groupTotalRow = new TableRow();
            var totalHeaderCell = new TableCell(new Paragraph(new Bold(new Run("ITEM TOTAL : "))) { Margin = new Thickness(0), Foreground = new SolidColorBrush(Color.FromRgb(192, 0, 0)) })
            {
                ColumnSpan = 2,
                Padding = new Thickness(3, 3, 3, 2),
                BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
                BorderThickness = new Thickness(0, 1, 0, 0)
            };
            groupTotalRow.Cells.Add(totalHeaderCell);

            var itemNameCell = new TableCell(new Paragraph(new Bold(new Run(itemGroupName))) { Margin = new Thickness(0) })
            {
                Padding = new Thickness(3, 3, 3, 2),
                BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
                BorderThickness = new Thickness(0, 1, 0, 0)
            };
            groupTotalRow.Cells.Add(itemNameCell);

            AddColoredTotalCell(groupTotalRow, grpNetWt.ToString("F2", CultureInfo.InvariantCulture), TextAlignment.Right, Color.FromRgb(192, 0, 0));
            AddColoredTotalCell(groupTotalRow, "", TextAlignment.Right, Color.FromRgb(192, 0, 0));
            AddColoredTotalCell(groupTotalRow, grpTaxable.ToString("N2", IndianCulture), TextAlignment.Right, Color.FromRgb(192, 0, 0));
            AddColoredTotalCell(groupTotalRow, grpIgst.ToString("N2", IndianCulture), TextAlignment.Right, Color.FromRgb(192, 0, 0));
            AddColoredTotalCell(groupTotalRow, grpCgst.ToString("N2", IndianCulture), TextAlignment.Right, Color.FromRgb(192, 0, 0));
            AddColoredTotalCell(groupTotalRow, grpSgst.ToString("N2", IndianCulture), TextAlignment.Right, Color.FromRgb(192, 0, 0));
            AddColoredTotalCell(groupTotalRow, grpGst.ToString("N2", IndianCulture), TextAlignment.Right, Color.FromRgb(192, 0, 0));
            AddColoredTotalCell(groupTotalRow, grpROff.ToString("N2", IndianCulture), TextAlignment.Right, Color.FromRgb(192, 0, 0));
            AddColoredTotalCell(groupTotalRow, grpNetAmt.ToString("N2", IndianCulture), TextAlignment.Right, Color.FromRgb(192, 0, 0));

            rowGroup.Rows.Add(groupTotalRow);
            foreach (var r in itemRows)
            {
                rowGroup.Rows.Add(r);
            }
        }

        // Grand Total Row
        var grandRow = new TableRow();
        var grandHeaderCell = new TableCell(new Paragraph(new Bold(new Run("GRAND TOTAL"))) { Margin = new Thickness(0), FontSize = 10 })
        {
            ColumnSpan = 3,
            Padding = new Thickness(3, 4, 3, 4),
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(0, 1.5, 0, 2.0)
        };
        grandRow.Cells.Add(grandHeaderCell);

        AddGrandTotalCell(grandRow, grandNetWt.ToString("F2", CultureInfo.InvariantCulture), TextAlignment.Right);
        AddGrandTotalCell(grandRow, "", TextAlignment.Right);
        AddGrandTotalCell(grandRow, grandTaxable.ToString("N2", IndianCulture), TextAlignment.Right);
        AddGrandTotalCell(grandRow, grandIgst.ToString("N2", IndianCulture), TextAlignment.Right);
        AddGrandTotalCell(grandRow, grandCgst.ToString("N2", IndianCulture), TextAlignment.Right);
        AddGrandTotalCell(grandRow, grandSgst.ToString("N2", IndianCulture), TextAlignment.Right);
        AddGrandTotalCell(grandRow, grandGst.ToString("N2", IndianCulture), TextAlignment.Right);
        AddGrandTotalCell(grandRow, grandROff.ToString("N2", IndianCulture), TextAlignment.Right);
        AddGrandTotalCell(grandRow, grandNetAmt.ToString("N2", IndianCulture), TextAlignment.Right);
        rowGroup.Rows.Add(grandRow);

        table.RowGroups.Add(rowGroup);
        doc.Blocks.Add(table);
        return doc;
    }

    private static FlowDocument BuildDaywiseDocumentCode(PurchaseRegisterViewModel vm)
    {
        var doc = new FlowDocument
        {
            Background = Brushes.White,
            Foreground = Brushes.Black,
            PageWidth = 842,
            PageHeight = 1190,
            PagePadding = new Thickness(30, 28, 30, 28),
            FontFamily = new FontFamily("Segoe UI, Arial, sans-serif"),
            FontSize = 10,
            ColumnWidth = double.PositiveInfinity
        };

        AppendCompanyHeader(doc, vm, "DAY WISE PURCHASE REGISTER");

        var table = new Table { CellSpacing = 0, Margin = new Thickness(0, 8, 0, 16) };
        table.Columns.Add(new TableColumn { Width = new GridLength(75) });
        table.Columns.Add(new TableColumn { Width = new GridLength(85) });
        table.Columns.Add(new TableColumn { Width = new GridLength(180) });
        table.Columns.Add(new TableColumn { Width = new GridLength(65) });
        table.Columns.Add(new TableColumn { Width = new GridLength(95) });
        table.Columns.Add(new TableColumn { Width = new GridLength(80) });
        table.Columns.Add(new TableColumn { Width = new GridLength(100) });

        var rowGroup = new TableRowGroup();
        var headerRow = new TableRow();
        AddHeaderCell(headerRow, "Date", TextAlignment.Left);
        AddHeaderCell(headerRow, "Vou No", TextAlignment.Left);
        AddHeaderCell(headerRow, "Supplier / Party", TextAlignment.Left);
        AddHeaderCell(headerRow, "Net Wt", TextAlignment.Right);
        AddHeaderCell(headerRow, "Taxable Amt", TextAlignment.Right);
        AddHeaderCell(headerRow, "GST Amt", TextAlignment.Right);
        AddHeaderCell(headerRow, "Net Amount", TextAlignment.Right);
        rowGroup.Rows.Add(headerRow);

        var groupedByDate = vm.Purchases.OrderBy(p => p.BillDate).ThenBy(p => p.VouNo).GroupBy(p => p.BillDate.Date);
        decimal grandNetWt = 0m, grandTaxable = 0m, grandGst = 0m, grandNet = 0m;

        foreach (var grp in groupedByDate)
        {
            decimal dayNetWt = 0m, dayTaxable = 0m, dayGst = 0m, dayNet = 0m;

            foreach (var p in grp)
            {
                dayNetWt += p.NtWt;
                dayTaxable += p.TaxableAmount;
                dayGst += p.TaxAmt;
                dayNet += p.NetAmt;

                grandNetWt += p.NtWt;
                grandTaxable += p.TaxableAmount;
                grandGst += p.TaxAmt;
                grandNet += p.NetAmt;

                var row = new TableRow();
                AddDataCell(row, p.BillDate.ToString("dd/MM/yyyy"), TextAlignment.Left);
                AddDataCell(row, p.VouNo, TextAlignment.Left);
                AddDataCell(row, p.PartyDisplayName, TextAlignment.Left);
                AddDataCell(row, p.NtWt > 0 ? p.NtWt.ToString("N3", CultureInfo.InvariantCulture) : "-", TextAlignment.Right);
                AddDataCell(row, p.TaxableAmount.ToString("N2", IndianCulture), TextAlignment.Right);
                AddDataCell(row, p.TaxAmt.ToString("N2", IndianCulture), TextAlignment.Right);
                AddDataCell(row, p.NetAmt.ToString("N2", IndianCulture), TextAlignment.Right);
                rowGroup.Rows.Add(row);
            }

            var dayTotalRow = new TableRow();
            var dtCell = new TableCell(new Paragraph(new Bold(new Run($"DATE TOTAL ({grp.Key:dd/MM/yyyy})"))) { Margin = new Thickness(0) })
            {
                ColumnSpan = 3,
                Padding = new Thickness(4, 3, 4, 3),
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(0, 1, 0, 1)
            };
            dayTotalRow.Cells.Add(dtCell);
            AddColoredTotalCell(dayTotalRow, dayNetWt.ToString("N3", CultureInfo.InvariantCulture), TextAlignment.Right, Colors.Black);
            AddColoredTotalCell(dayTotalRow, dayTaxable.ToString("N2", IndianCulture), TextAlignment.Right, Colors.Black);
            AddColoredTotalCell(dayTotalRow, dayGst.ToString("N2", IndianCulture), TextAlignment.Right, Colors.Black);
            AddColoredTotalCell(dayTotalRow, dayNet.ToString("N2", IndianCulture), TextAlignment.Right, Colors.Black);
            rowGroup.Rows.Add(dayTotalRow);
        }

        var grandRow = new TableRow();
        var gCell = new TableCell(new Paragraph(new Bold(new Run("GRAND TOTAL"))) { Margin = new Thickness(0) })
        {
            ColumnSpan = 3,
            Padding = new Thickness(4, 4, 4, 4),
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(0, 1.5, 0, 2.0)
        };
        grandRow.Cells.Add(gCell);
        AddGrandTotalCell(grandRow, grandNetWt.ToString("N3", CultureInfo.InvariantCulture), TextAlignment.Right);
        AddGrandTotalCell(grandRow, grandTaxable.ToString("N2", IndianCulture), TextAlignment.Right);
        AddGrandTotalCell(grandRow, grandGst.ToString("N2", IndianCulture), TextAlignment.Right);
        AddGrandTotalCell(grandRow, grandNet.ToString("N2", IndianCulture), TextAlignment.Right);
        rowGroup.Rows.Add(grandRow);

        table.RowGroups.Add(rowGroup);
        doc.Blocks.Add(table);
        return doc;
    }

    private static FlowDocument BuildPartyWiseDocumentCode(PurchaseRegisterViewModel vm)
    {
        var doc = new FlowDocument
        {
            Background = Brushes.White,
            Foreground = Brushes.Black,
            PageWidth = 842,
            PageHeight = 1190,
            PagePadding = new Thickness(30, 28, 30, 28),
            FontFamily = new FontFamily("Segoe UI, Arial, sans-serif"),
            FontSize = 10,
            ColumnWidth = double.PositiveInfinity
        };

        AppendCompanyHeader(doc, vm, "PARTY WISE PURCHASE REGISTER");

        var table = new Table { CellSpacing = 0, Margin = new Thickness(0, 8, 0, 16) };
        table.Columns.Add(new TableColumn { Width = new GridLength(200) });
        table.Columns.Add(new TableColumn { Width = new GridLength(80) });
        table.Columns.Add(new TableColumn { Width = new GridLength(85) });
        table.Columns.Add(new TableColumn { Width = new GridLength(65) });
        table.Columns.Add(new TableColumn { Width = new GridLength(95) });
        table.Columns.Add(new TableColumn { Width = new GridLength(80) });
        table.Columns.Add(new TableColumn { Width = new GridLength(100) });

        var rowGroup = new TableRowGroup();
        var headerRow = new TableRow();
        AddHeaderCell(headerRow, "Supplier / Party", TextAlignment.Left);
        AddHeaderCell(headerRow, "Date", TextAlignment.Left);
        AddHeaderCell(headerRow, "Vou No", TextAlignment.Left);
        AddHeaderCell(headerRow, "Net Wt", TextAlignment.Right);
        AddHeaderCell(headerRow, "Taxable Amt", TextAlignment.Right);
        AddHeaderCell(headerRow, "GST Amt", TextAlignment.Right);
        AddHeaderCell(headerRow, "Net Amount", TextAlignment.Right);
        rowGroup.Rows.Add(headerRow);

        var groupedByParty = vm.Purchases.GroupBy(p => p.PartyDisplayName).OrderBy(g => g.Key);
        decimal grandNetWt = 0m, grandTaxable = 0m, grandGst = 0m, grandNet = 0m;

        foreach (var grp in groupedByParty)
        {
            decimal pNetWt = 0m, pTaxable = 0m, pGst = 0m, pNet = 0m;

            foreach (var p in grp.OrderBy(x => x.BillDate).ThenBy(x => x.VouNo))
            {
                pNetWt += p.NtWt;
                pTaxable += p.TaxableAmount;
                pGst += p.TaxAmt;
                pNet += p.NetAmt;

                grandNetWt += p.NtWt;
                grandTaxable += p.TaxableAmount;
                grandGst += p.TaxAmt;
                grandNet += p.NetAmt;

                var row = new TableRow();
                AddDataCell(row, p.PartyDisplayName, TextAlignment.Left);
                AddDataCell(row, p.BillDate.ToString("dd/MM/yyyy"), TextAlignment.Left);
                AddDataCell(row, p.VouNo, TextAlignment.Left);
                AddDataCell(row, p.NtWt > 0 ? p.NtWt.ToString("N3", CultureInfo.InvariantCulture) : "-", TextAlignment.Right);
                AddDataCell(row, p.TaxableAmount.ToString("N2", IndianCulture), TextAlignment.Right);
                AddDataCell(row, p.TaxAmt.ToString("N2", IndianCulture), TextAlignment.Right);
                AddDataCell(row, p.NetAmt.ToString("N2", IndianCulture), TextAlignment.Right);
                rowGroup.Rows.Add(row);
            }

            var partyTotalRow = new TableRow();
            var ptCell = new TableCell(new Paragraph(new Bold(new Run($"PARTY TOTAL: {grp.Key}"))) { Margin = new Thickness(0) })
            {
                ColumnSpan = 3,
                Padding = new Thickness(4, 3, 4, 3),
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(0, 1, 0, 1)
            };
            partyTotalRow.Cells.Add(ptCell);
            AddColoredTotalCell(partyTotalRow, pNetWt.ToString("N3", CultureInfo.InvariantCulture), TextAlignment.Right, Color.FromRgb(192, 0, 0));
            AddColoredTotalCell(partyTotalRow, pTaxable.ToString("N2", IndianCulture), TextAlignment.Right, Color.FromRgb(192, 0, 0));
            AddColoredTotalCell(partyTotalRow, pGst.ToString("N2", IndianCulture), TextAlignment.Right, Color.FromRgb(192, 0, 0));
            AddColoredTotalCell(partyTotalRow, pNet.ToString("N2", IndianCulture), TextAlignment.Right, Color.FromRgb(192, 0, 0));
            rowGroup.Rows.Add(partyTotalRow);
        }

        var grandRow = new TableRow();
        var gCell = new TableCell(new Paragraph(new Bold(new Run("GRAND TOTAL"))) { Margin = new Thickness(0) })
        {
            ColumnSpan = 3,
            Padding = new Thickness(4, 4, 4, 4),
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(0, 1.5, 0, 2.0)
        };
        grandRow.Cells.Add(gCell);
        AddGrandTotalCell(grandRow, grandNetWt.ToString("N3", CultureInfo.InvariantCulture), TextAlignment.Right);
        AddGrandTotalCell(grandRow, grandTaxable.ToString("N2", IndianCulture), TextAlignment.Right);
        AddGrandTotalCell(grandRow, grandGst.ToString("N2", IndianCulture), TextAlignment.Right);
        AddGrandTotalCell(grandRow, grandNet.ToString("N2", IndianCulture), TextAlignment.Right);
        rowGroup.Rows.Add(grandRow);

        table.RowGroups.Add(rowGroup);
        doc.Blocks.Add(table);
        return doc;
    }

    private static FlowDocument BuildVoucherWiseDocumentCode(PurchaseRegisterViewModel vm)
    {
        return BuildDaywiseDocumentCode(vm);
    }

    private static FlowDocument BuildItemWiseSummaryDocumentCode(PurchaseRegisterViewModel vm)
    {
        var doc = new FlowDocument
        {
            Background = Brushes.White,
            Foreground = Brushes.Black,
            PageWidth = 842,
            PageHeight = 1190,
            PagePadding = new Thickness(30, 24, 30, 24),
            FontFamily = new FontFamily("Segoe UI, Arial, sans-serif"),
            FontSize = 9.5,
            ColumnWidth = double.PositiveInfinity
        };

        AppendCompanyHeader(doc, vm, "PURCHASE REGISTER SUMMARY");

        var table = new Table { CellSpacing = 0, Margin = new Thickness(0, 4, 0, 12) };
        table.Columns.Add(new TableColumn { Width = new GridLength(65) });
        table.Columns.Add(new TableColumn { Width = new GridLength(65) });
        table.Columns.Add(new TableColumn { Width = new GridLength(180) });
        table.Columns.Add(new TableColumn { Width = new GridLength(75) });
        table.Columns.Add(new TableColumn { Width = new GridLength(75) });
        table.Columns.Add(new TableColumn { Width = new GridLength(85) });
        table.Columns.Add(new TableColumn { Width = new GridLength(65) });
        table.Columns.Add(new TableColumn { Width = new GridLength(50) });
        table.Columns.Add(new TableColumn { Width = new GridLength(90) });

        var rowGroup = new TableRowGroup();
        var headerRow = new TableRow();
        AddHeaderCell(headerRow, "Date", TextAlignment.Left);
        AddHeaderCell(headerRow, "Inside/\nOutside", TextAlignment.Center);
        AddHeaderCell(headerRow, "Item Name", TextAlignment.Center);
        AddHeaderCell(headerRow, "Gr .Weight", TextAlignment.Right);
        AddHeaderCell(headerRow, "Net Weight", TextAlignment.Right);
        AddHeaderCell(headerRow, "Taxable Amt", TextAlignment.Right);
        AddHeaderCell(headerRow, "Tax Amt", TextAlignment.Right);
        AddHeaderCell(headerRow, "R.Off", TextAlignment.Right);
        AddHeaderCell(headerRow, "Net Amount", TextAlignment.Right);
        rowGroup.Rows.Add(headerRow);

        var flatItems = new List<(PurchaseRegisterItem Purchase, PurchaseSubform Subform)>();
        foreach (var p in vm.Purchases)
        {
            if (p.Items.Count > 0)
            {
                foreach (var item in p.Items)
                {
                    flatItems.Add((p, item));
                }
            }
            else
            {
                flatItems.Add((p, new PurchaseSubform
                {
                    ItemName = "PURCHASE GENERAL",
                    NtWt = p.NtWt,
                    GrWt = p.GrWt,
                    Qty = p.TotalQty > 0 ? p.TotalQty : 1,
                    Rate = p.NtWt > 0 ? Math.Round(p.TaxableAmount / p.NtWt, 2) : 0m,
                    TaxableAmount = p.TaxableAmount,
                    GstAmt = p.TaxAmt,
                    TotalAmt = p.NetAmt
                }));
            }
        }

        var groupedByItem = flatItems
            .GroupBy(x => string.IsNullOrWhiteSpace(x.Subform.ItemName) ? "GENERAL ORNAMENTS" : x.Subform.ItemName.Trim())
            .OrderBy(g => g.Key);

        decimal grandGrWt = 0m, grandNetWt = 0m, grandTaxable = 0m, grandTaxAmt = 0m, grandROff = 0m, grandNetAmt = 0m;

        foreach (var group in groupedByItem)
        {
            var itemGroupName = group.Key;
            decimal grpGrWt = 0m, grpNetWt = 0m, grpTaxable = 0m, grpTaxAmt = 0m, grpROff = 0m, grpNetAmt = 0m;

            var itemRows = new List<TableRow>();

            foreach (var (purchase, sub) in group.OrderBy(x => x.Purchase.BillDate).ThenBy(x => x.Purchase.VouNo))
            {
                var lineGrWt = sub.GrWt > 0 ? sub.GrWt : (sub.NtWt > 0 ? sub.NtWt : 0m);
                var lineNetWt = sub.NtWt > 0 ? sub.NtWt : lineGrWt;
                var lineTaxable = sub.TaxableAmount > 0 ? sub.TaxableAmount : (sub.Amount > 0 ? sub.Amount : sub.GrossAmt);
                var lineTax = sub.GstAmt > 0 ? sub.GstAmt : (sub.TotalAmt > lineTaxable ? sub.TotalAmt - lineTaxable : 0m);
                var lineNet = sub.TotalAmt > 0 ? sub.TotalAmt : (lineTaxable + lineTax);
                var lineROff = 0m;
                var insideOutside = purchase.IGSTAmt > 0 ? "Outside" : "Inside";

                grpGrWt += lineGrWt;
                grpNetWt += lineNetWt;
                grpTaxable += lineTaxable;
                grpTaxAmt += lineTax;
                grpROff += lineROff;
                grpNetAmt += lineNet;

                grandGrWt += lineGrWt;
                grandNetWt += lineNetWt;
                grandTaxable += lineTaxable;
                grandTaxAmt += lineTax;
                grandROff += lineROff;
                grandNetAmt += lineNet;

                var row = new TableRow();
                AddDataCell(row, purchase.BillDate.ToString("dd/MM/yy"), TextAlignment.Left);
                AddDataCell(row, insideOutside, TextAlignment.Center);
                AddDataCell(row, itemGroupName, TextAlignment.Left);
                AddDataCell(row, lineGrWt.ToString("F3", CultureInfo.InvariantCulture), TextAlignment.Right);
                AddDataCell(row, lineNetWt.ToString("F3", CultureInfo.InvariantCulture), TextAlignment.Right);
                AddDataCell(row, lineTaxable.ToString("N2", IndianCulture), TextAlignment.Right);
                AddDataCell(row, lineTax.ToString("N2", IndianCulture), TextAlignment.Right);
                AddDataCell(row, lineROff.ToString("N2", IndianCulture), TextAlignment.Right);
                AddDataCell(row, lineNet.ToString("N2", IndianCulture), TextAlignment.Right);
                itemRows.Add(row);
            }

            // Group Total Row
            var groupTotalRow = new TableRow();
            var totalHeaderCell = new TableCell(new Paragraph(new Bold(new Run("ITEM TOTAL : "))) { Margin = new Thickness(0), Foreground = new SolidColorBrush(Color.FromRgb(192, 0, 0)) })
            {
                ColumnSpan = 3,
                Padding = new Thickness(3, 3, 3, 2),
                BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
                BorderThickness = new Thickness(0, 1, 0, 0)
            };
            groupTotalRow.Cells.Add(totalHeaderCell);

            AddColoredTotalCell(groupTotalRow, grpGrWt.ToString("F3", CultureInfo.InvariantCulture), TextAlignment.Right, Color.FromRgb(192, 0, 0));
            AddColoredTotalCell(groupTotalRow, grpNetWt.ToString("F3", CultureInfo.InvariantCulture), TextAlignment.Right, Color.FromRgb(192, 0, 0));
            AddColoredTotalCell(groupTotalRow, grpTaxable.ToString("N2", IndianCulture), TextAlignment.Right, Color.FromRgb(192, 0, 0));
            AddColoredTotalCell(groupTotalRow, grpTaxAmt.ToString("N2", IndianCulture), TextAlignment.Right, Color.FromRgb(192, 0, 0));
            AddColoredTotalCell(groupTotalRow, grpROff.ToString("N2", IndianCulture), TextAlignment.Right, Color.FromRgb(192, 0, 0));
            AddColoredTotalCell(groupTotalRow, grpNetAmt.ToString("N2", IndianCulture), TextAlignment.Right, Color.FromRgb(192, 0, 0));

            foreach (var r in itemRows)
            {
                rowGroup.Rows.Add(r);
            }
            rowGroup.Rows.Add(groupTotalRow);
        }

        // Grand Total Row
        var grandRow = new TableRow();
        var grandHeaderCell = new TableCell(new Paragraph(new Bold(new Run("GRAND TOTAL"))) { Margin = new Thickness(0), FontSize = 10 })
        {
            ColumnSpan = 3,
            Padding = new Thickness(3, 4, 3, 4),
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(0, 1.5, 0, 2.0)
        };
        grandRow.Cells.Add(grandHeaderCell);

        AddGrandTotalCell(grandRow, grandGrWt.ToString("F3", CultureInfo.InvariantCulture), TextAlignment.Right);
        AddGrandTotalCell(grandRow, grandNetWt.ToString("F3", CultureInfo.InvariantCulture), TextAlignment.Right);
        AddGrandTotalCell(grandRow, grandTaxable.ToString("N2", IndianCulture), TextAlignment.Right);
        AddGrandTotalCell(grandRow, grandTaxAmt.ToString("N2", IndianCulture), TextAlignment.Right);
        AddGrandTotalCell(grandRow, grandROff.ToString("N2", IndianCulture), TextAlignment.Right);
        AddGrandTotalCell(grandRow, grandNetAmt.ToString("N2", IndianCulture), TextAlignment.Right);
        rowGroup.Rows.Add(grandRow);

        table.RowGroups.Add(rowGroup);
        doc.Blocks.Add(table);
        return doc;
    }

    #endregion

    #region Helper Methods

    private static (string CompanyName, string Address, string Gstin) GetCompanyHeaderDetails(PurchaseRegisterViewModel vm)
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

    private static void AppendCompanyHeader(FlowDocument doc, PurchaseRegisterViewModel vm, string reportTitle)
    {
        var (companyName, address, gstin) = GetCompanyHeaderDetails(vm);

        var headerTable = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 6) };
        headerTable.Columns.Add(new TableColumn { Width = new GridLength(450) });
        headerTable.Columns.Add(new TableColumn { Width = new GridLength(280) });
        headerTable.Columns.Add(new TableColumn { Width = new GridLength(320) });

        var rowGroup = new TableRowGroup();
        var row = new TableRow();

        var companyCell = new TableCell();
        companyCell.Blocks.Add(new Paragraph(new Bold(new Run(companyName) { FontSize = 14 })) { Margin = new Thickness(0, 0, 0, 1) });
        companyCell.Blocks.Add(new Paragraph(new Run(address)) { Margin = new Thickness(0, 0, 0, 1), FontSize = 10 });
        var gstinText = !string.IsNullOrWhiteSpace(gstin) ? $"GSTIN: {gstin}" : string.Empty;
        companyCell.Blocks.Add(new Paragraph(new Run(gstinText)) { Margin = new Thickness(0, 0, 0, 2), FontSize = 9.5 });
        row.Cells.Add(companyCell);

        var dateCell = new TableCell(new Paragraph(new Run($"From Date : {vm.FromDate:dd/MM/yyyy}   To Date : {vm.ToDate:dd/MM/yyyy}") { FontWeight = FontWeights.Bold })
        {
            Margin = new Thickness(0),
            TextAlignment = TextAlignment.Center,
            FontSize = 10.5
        });
        row.Cells.Add(dateCell);

        var titleCell = new TableCell(new Paragraph(new Bold(new Run(reportTitle) { FontSize = 13, TextDecorations = TextDecorations.Underline }))
        {
            Margin = new Thickness(0, 0, 0, 2),
            TextAlignment = TextAlignment.Right
        });
        row.Cells.Add(titleCell);

        rowGroup.Rows.Add(row);
        headerTable.RowGroups.Add(rowGroup);
        doc.Blocks.Add(headerTable);
    }

    private static string ReplaceHeaderPlaceholders(string template, PurchaseRegisterViewModel vm, string reportTitle)
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

    private static void AddHeaderCell(TableRow row, string text, TextAlignment alignment)
    {
        var p = new Paragraph(new Bold(new Run(text))) { TextAlignment = alignment, Margin = new Thickness(0), FontSize = 9.5 };
        var cell = new TableCell(p)
        {
            Padding = new Thickness(3, 4, 3, 4),
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(0, 2.5, 0, 1.5)
        };
        row.Cells.Add(cell);
    }

    private static void AddDataCell(TableRow row, string text, TextAlignment alignment)
    {
        var p = new Paragraph(new Run(text)) { TextAlignment = alignment, Margin = new Thickness(0), FontSize = 9 };
        var cell = new TableCell(p)
        {
            Padding = new Thickness(3, 2, 3, 2),
            BorderBrush = new SolidColorBrush(Color.FromRgb(234, 234, 234)),
            BorderThickness = new Thickness(0, 0, 0, 0.5)
        };
        row.Cells.Add(cell);
    }

    private static void AddPartyCell(TableRow row, string partyName, string partyDetails)
    {
        var p1 = new Paragraph(new Run(partyName) { FontWeight = FontWeights.SemiBold }) { TextAlignment = TextAlignment.Left, Margin = new Thickness(0), FontSize = 9 };
        var p2 = new Paragraph(new Run(partyDetails)) { TextAlignment = TextAlignment.Left, Margin = new Thickness(0, 1, 0, 0), FontSize = 8, Foreground = new SolidColorBrush(Color.FromRgb(68, 68, 68)) };
        var cell = new TableCell();
        cell.Blocks.Add(p1);
        cell.Blocks.Add(p2);
        cell.Padding = new Thickness(3, 2, 3, 2);
        cell.BorderBrush = new SolidColorBrush(Color.FromRgb(234, 234, 234));
        cell.BorderThickness = new Thickness(0, 0, 0, 0.5);
        row.Cells.Add(cell);
    }

    private static void AddColoredTotalCell(TableRow row, string text, TextAlignment alignment, Color color)
    {
        var p = new Paragraph(new Bold(new Run(text))) { TextAlignment = alignment, Margin = new Thickness(0), FontSize = 9.5, Foreground = new SolidColorBrush(color) };
        var cell = new TableCell(p)
        {
            Padding = new Thickness(3, 3, 3, 2),
            BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
            BorderThickness = new Thickness(0, 1, 0, 0)
        };
        row.Cells.Add(cell);
    }

    private static void AddGrandTotalCell(TableRow row, string text, TextAlignment alignment)
    {
        var p = new Paragraph(new Bold(new Run(text))) { TextAlignment = alignment, Margin = new Thickness(0), FontSize = 10 };
        var cell = new TableCell(p)
        {
            Padding = new Thickness(3, 4, 3, 4),
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(0, 1.5, 0, 2.0)
        };
        row.Cells.Add(cell);
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

    #endregion
}
