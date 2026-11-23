using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.WPF.ViewModels;

namespace ConceptJewelleryPro.WPF.Services;

public static class TradingAccountFlowDocumentBuilder
{
    private static readonly CultureInfo IndianCulture = new("en-IN");

    public static FlowDocument CreateDocument(TradingAccountViewModel vm)
    {
        var doc = new FlowDocument
        {
            PagePadding = new Thickness(36),
            ColumnWidth = double.PositiveInfinity,
            FontFamily = new FontFamily("Segoe UI, Arial, sans-serif"),
            FontSize = 11.5
        };

        // Header Title
        var titlePara = new Paragraph(new Run("TRADING ACCOUNT STATEMENT"))
        {
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(15, 118, 110)),
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 3)
        };
        doc.Blocks.Add(titlePara);

        var subTitlePara = new Paragraph(new Run($"For the period from {vm.Statement.FromDate:dd/MM/yyyy} to {vm.Statement.ToDate:dd/MM/yyyy}"))
        {
            FontSize = 11.5,
            Foreground = Brushes.Gray,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 14)
        };
        doc.Blocks.Add(subTitlePara);

        // Horizontal T-Format Table
        var table = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 14) };
        table.Columns.Add(new TableColumn { Width = new GridLength(2.2, GridUnitType.Star) }); // Debit Particulars
        table.Columns.Add(new TableColumn { Width = new GridLength(75) });                      // Debit Gold Wt
        table.Columns.Add(new TableColumn { Width = new GridLength(125) });                     // Debit Amount
        table.Columns.Add(new TableColumn { Width = new GridLength(2.2, GridUnitType.Star) }); // Credit Particulars
        table.Columns.Add(new TableColumn { Width = new GridLength(75) });                      // Credit Gold Wt
        table.Columns.Add(new TableColumn { Width = new GridLength(125) });                     // Credit Amount

        var rowGroup = new TableRowGroup();

        // Header Row
        var headerRow = new TableRow { Background = new SolidColorBrush(Color.FromRgb(241, 245, 249)) };
        AddHeaderCell(headerRow, "DEBIT (PURCHASES & DIRECT EXPENSES)", TextAlignment.Left);
        AddHeaderCell(headerRow, "GOLD (g)", TextAlignment.Right);
        AddHeaderCell(headerRow, "AMOUNT (\u20B9)", TextAlignment.Right);
        AddHeaderCell(headerRow, "CREDIT (SALES & CLOSING STOCK)", TextAlignment.Left);
        AddHeaderCell(headerRow, "GOLD (g)", TextAlignment.Right);
        AddHeaderCell(headerRow, "AMOUNT (\u20B9)", TextAlignment.Right);
        rowGroup.Rows.Add(headerRow);

        var debits = vm.Statement.DebitItems;
        var credits = vm.Statement.CreditItems;
        int maxRows = Math.Max(debits.Count, credits.Count);

        for (int i = 0; i < maxRows; i++)
        {
            var row = new TableRow();

            // Left side (Debit)
            if (i < debits.Count)
            {
                var d = debits[i];
                bool isGp = d.ItemType == TradingItemType.GrossProfitTransfer;
                AddDataCell(row, d.Particulars, TextAlignment.Left, isBold: isGp);
                AddDataCell(row, d.GrossWeight != 0 ? $"{d.GrossWeight:N3}" : "-", TextAlignment.Right);
                AddDataCell(row, d.FormattedAmount, TextAlignment.Right, isBold: isGp);
            }
            else
            {
                AddDataCell(row, string.Empty, TextAlignment.Left);
                AddDataCell(row, string.Empty, TextAlignment.Right);
                AddDataCell(row, string.Empty, TextAlignment.Right);
            }

            // Right side (Credit)
            if (i < credits.Count)
            {
                var c = credits[i];
                bool isGl = c.ItemType == TradingItemType.GrossLossTransfer;
                AddDataCell(row, c.Particulars, TextAlignment.Left, isBold: isGl);
                AddDataCell(row, c.GrossWeight != 0 ? $"{c.GrossWeight:N3}" : "-", TextAlignment.Right);
                AddDataCell(row, c.FormattedAmount, TextAlignment.Right, isBold: isGl);
            }
            else
            {
                AddDataCell(row, string.Empty, TextAlignment.Left);
                AddDataCell(row, string.Empty, TextAlignment.Right);
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
        AddDataCell(totalRow, "TOTAL DEBITS", TextAlignment.Left, isBold: true);
        AddDataCell(totalRow, string.Empty, TextAlignment.Right);
        AddDataCell(totalRow, vm.Statement.FormattedTotalDebitsTFormat, TextAlignment.Right, isBold: true);
        AddDataCell(totalRow, "TOTAL CREDITS", TextAlignment.Left, isBold: true);
        AddDataCell(totalRow, string.Empty, TextAlignment.Right);
        AddDataCell(totalRow, vm.Statement.FormattedTotalCreditsTFormat, TextAlignment.Right, isBold: true);
        rowGroup.Rows.Add(totalRow);

        table.RowGroups.Add(rowGroup);
        doc.Blocks.Add(table);

        // Metal Reconciliation & Margin Summary
        var summaryPara = new Paragraph();
        summaryPara.Inlines.Add(new Run($"Gross Profit: \u20B9 {vm.Statement.FormattedGrossProfit} ({vm.Statement.FormattedGrossProfitPercentage})") { FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129)) });
        summaryPara.Inlines.Add(new Run($"   |   Cost of Goods Sold (COGS): \u20B9 {vm.Statement.FormattedCostOfGoodsSold}") { Foreground = Brushes.DimGray });
        summaryPara.Inlines.Add(new Run($"   |   Gold Balance Drift: {vm.Statement.GoldReconciliation.FormattedDifference}") { Foreground = vm.Statement.GoldReconciliation.IsTallied ? Brushes.DimGray : Brushes.Crimson });
        summaryPara.TextAlignment = TextAlignment.Right;
        summaryPara.Margin = new Thickness(0, 4, 0, 0);
        doc.Blocks.Add(summaryPara);

        return doc;
    }

    private static void AddHeaderCell(TableRow row, string text, TextAlignment alignment)
    {
        var cell = new TableCell(new Paragraph(new Run(text))
        {
            FontWeight = FontWeights.Bold,
            FontSize = 10.5,
            TextAlignment = alignment,
            Margin = new Thickness(0)
        })
        {
            Padding = new Thickness(6, 5, 6, 5),
            BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
            BorderThickness = new Thickness(0, 0, 0, 2)
        };
        row.Cells.Add(cell);
    }

    private static void AddDataCell(TableRow row, string text, TextAlignment alignment, bool isBold = false)
    {
        var run = new Run(text);
        if (isBold) run.FontWeight = FontWeights.SemiBold;

        var cell = new TableCell(new Paragraph(run)
        {
            FontSize = 10.5,
            TextAlignment = alignment,
            Margin = new Thickness(0)
        })
        {
            Padding = new Thickness(5, 3, 5, 3),
            BorderBrush = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
            BorderThickness = new Thickness(0, 0, 0, 1)
        };
        row.Cells.Add(cell);
    }
}