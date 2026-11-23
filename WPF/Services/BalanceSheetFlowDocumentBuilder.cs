using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.WPF.ViewModels;

namespace ConceptJewelleryPro.WPF.Services;

public static class BalanceSheetFlowDocumentBuilder
{
    private static readonly CultureInfo IndianCulture = new("en-IN");

    public static FlowDocument CreateDocument(BalanceSheetViewModel vm)
    {
        var doc = new FlowDocument
        {
            PagePadding = new Thickness(36),
            ColumnWidth = double.PositiveInfinity,
            FontFamily = new FontFamily("Segoe UI, Arial, sans-serif"),
            FontSize = 12
        };

        // Header Title
        var titlePara = new Paragraph(new Run("BALANCE SHEET"))
        {
            FontSize = 22,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(15, 118, 110)),
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 4)
        };
        doc.Blocks.Add(titlePara);

        var subTitlePara = new Paragraph(new Run($"As on {vm.Statement.AsOnDate:dd/MM/yyyy}  (Period: {vm.Statement.FromDate:dd/MM/yyyy} to {vm.Statement.AsOnDate:dd/MM/yyyy})"))
        {
            FontSize = 12,
            Foreground = Brushes.Gray,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 16)
        };
        doc.Blocks.Add(subTitlePara);

        // Horizontal T-Format Table
        var table = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 14) };
        table.Columns.Add(new TableColumn { Width = new GridLength(2.2, GridUnitType.Star) }); // Liabilities Particulars
        table.Columns.Add(new TableColumn { Width = new GridLength(140) });                     // Liabilities Amount
        table.Columns.Add(new TableColumn { Width = new GridLength(2.2, GridUnitType.Star) }); // Assets Particulars
        table.Columns.Add(new TableColumn { Width = new GridLength(140) });                     // Assets Amount

        var rowGroup = new TableRowGroup();

        // Main Header Row
        var headerRow = new TableRow { Background = new SolidColorBrush(Color.FromRgb(241, 245, 249)) };
        AddHeaderCell(headerRow, "CAPITAL & LIABILITIES", TextAlignment.Left);
        AddHeaderCell(headerRow, "AMOUNT (₹)", TextAlignment.Right);
        AddHeaderCell(headerRow, "PROPERTY & ASSETS", TextAlignment.Left);
        AddHeaderCell(headerRow, "AMOUNT (₹)", TextAlignment.Right);
        rowGroup.Rows.Add(headerRow);

        // Flatten sections into rows side-by-side
        var leftRows = FlattenSections(vm.Statement.LiabilitiesSections);
        var rightRows = FlattenSections(vm.Statement.AssetsSections);

        int maxRows = Math.Max(leftRows.Count, rightRows.Count);

        for (int i = 0; i < maxRows; i++)
        {
            var row = new TableRow();

            // Left side (Liabilities)
            if (i < leftRows.Count)
            {
                var l = leftRows[i];
                AddDataCell(row, l.Text, TextAlignment.Left, isHeader: l.IsHeader, isBold: l.IsBold, indent: l.Indent);
                AddDataCell(row, l.AmountText, TextAlignment.Right, isHeader: l.IsHeader, isBold: l.IsBold);
            }
            else
            {
                AddDataCell(row, string.Empty, TextAlignment.Left);
                AddDataCell(row, string.Empty, TextAlignment.Right);
            }

            // Right side (Assets)
            if (i < rightRows.Count)
            {
                var r = rightRows[i];
                AddDataCell(row, r.Text, TextAlignment.Left, isHeader: r.IsHeader, isBold: r.IsBold, indent: r.Indent);
                AddDataCell(row, r.AmountText, TextAlignment.Right, isHeader: r.IsHeader, isBold: r.IsBold);
            }
            else
            {
                AddDataCell(row, string.Empty, TextAlignment.Left);
                AddDataCell(row, string.Empty, TextAlignment.Right);
            }

            rowGroup.Rows.Add(row);
        }

        // Grand Totals Footer Row
        var totalRow = new TableRow
        {
            Background = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
            FontWeight = FontWeights.Bold
        };
        AddDataCell(totalRow, "TOTAL LIABILITIES & EQUITY", TextAlignment.Left, isBold: true);
        AddDataCell(totalRow, vm.Statement.FormattedTotalLiabilities, TextAlignment.Right, isBold: true);
        AddDataCell(totalRow, "TOTAL ASSETS & PROPERTIES", TextAlignment.Left, isBold: true);
        AddDataCell(totalRow, vm.Statement.FormattedTotalAssets, TextAlignment.Right, isBold: true);
        rowGroup.Rows.Add(totalRow);

        table.RowGroups.Add(rowGroup);
        doc.Blocks.Add(table);

        // Status & Equilibrium Summary
        var summaryPara = new Paragraph();
        if (vm.Statement.IsBalanced)
        {
            summaryPara.Inlines.Add(new Run("✓ Balance Sheet is fully tallied.") { FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129)) });
        }
        else
        {
            summaryPara.Inlines.Add(new Run($"⚠ Discrepancy: ₹ {vm.Statement.FormattedDifference}") { FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68)) });
        }

        summaryPara.Inlines.Add(new Run($"   |   Net Profit: ₹ {vm.Statement.ProfitLoss.FormattedNetProfit}   |   Working Capital: ₹ {vm.Statement.FormattedWorkingCapital}") { Foreground = Brushes.DimGray });
        summaryPara.TextAlignment = TextAlignment.Right;
        summaryPara.Margin = new Thickness(0, 6, 0, 0);
        doc.Blocks.Add(summaryPara);

        return doc;
    }

    private static List<ReportLine> FlattenSections(List<BalanceSheetSection> sections)
    {
        var list = new List<ReportLine>();
        foreach (var sec in sections)
        {
            // Section Header
            list.Add(new ReportLine
            {
                Text = sec.SectionTitle.ToUpperInvariant(),
                AmountText = sec.FormattedTotalAmount,
                IsHeader = true,
                IsBold = true
            });

            // Section Items
            foreach (var item in sec.Items)
            {
                var extraInfo = string.Empty;
                if (item.GoldWeight != 0) extraInfo += $" [Gold: {item.GoldWeight:N3}g]";
                if (item.SilverWeight != 0) extraInfo += $" [Silver: {item.SilverWeight:N3}g]";

                list.Add(new ReportLine
                {
                    Text = item.AccountName + extraInfo,
                    AmountText = item.FormattedAmount,
                    IsHeader = false,
                    IsBold = false,
                    Indent = 12
                });
            }

            // Spacer line
            list.Add(new ReportLine { Text = string.Empty, AmountText = string.Empty });
        }
        return list;
    }

    private static void AddHeaderCell(TableRow row, string text, TextAlignment alignment)
    {
        var cell = new TableCell(new Paragraph(new Run(text))
        {
            FontWeight = FontWeights.Bold,
            FontSize = 11.5,
            TextAlignment = alignment,
            Margin = new Thickness(0)
        })
        {
            Padding = new Thickness(8, 6, 8, 6),
            BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
            BorderThickness = new Thickness(0, 0, 0, 2)
        };
        row.Cells.Add(cell);
    }

    private static void AddDataCell(TableRow row, string text, TextAlignment alignment, bool isHeader = false, bool isBold = false, double indent = 0)
    {
        var run = new Run(text);
        if (isBold || isHeader) run.FontWeight = FontWeights.SemiBold;
        if (isHeader) run.Foreground = new SolidColorBrush(Color.FromRgb(15, 118, 110));

        var cell = new TableCell(new Paragraph(run)
        {
            FontSize = isHeader ? 11.5 : 11,
            TextAlignment = alignment,
            Margin = new Thickness(indent, 0, 0, 0)
        })
        {
            Padding = new Thickness(6, 3, 6, 3),
            BorderBrush = isHeader ? new SolidColorBrush(Color.FromRgb(226, 232, 240)) : new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            BorderThickness = isHeader ? new Thickness(0, 1, 0, 1) : new Thickness(0, 0, 0, 1)
        };
        row.Cells.Add(cell);
    }

    private sealed class ReportLine
    {
        public string Text { get; set; } = string.Empty;
        public string AmountText { get; set; } = string.Empty;
        public bool IsHeader { get; set; }
        public bool IsBold { get; set; }
        public double Indent { get; set; }
    }
}
