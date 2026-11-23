using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using ConceptJewelleryPro.WPF.ViewModels;

namespace ConceptJewelleryPro.WPF.Services;

public static class TrialBalanceFlowDocumentBuilder
{
    private static readonly CultureInfo IndianCulture = new("en-IN");

    public static FlowDocument CreateDocument(TrialBalanceViewModel vm)
    {
        var doc = new FlowDocument
        {
            PagePadding = new Thickness(36),
            ColumnWidth = double.PositiveInfinity,
            FontFamily = new FontFamily("Segoe UI, Arial, sans-serif"),
            FontSize = 12
        };

        // Header Title
        var titlePara = new Paragraph(new Run("TRIAL BALANCE"))
        {
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(15, 118, 110)),
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 4)
        };
        doc.Blocks.Add(titlePara);

        var subTitlePara = new Paragraph(new Run($"For Period: {vm.FromDate:dd/MM/yyyy} to {vm.ToDate:dd/MM/yyyy}"))
        {
            FontSize = 12,
            Foreground = Brushes.Gray,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 14)
        };
        doc.Blocks.Add(subTitlePara);

        // Main Table
        var table = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 14) };
        table.Columns.Add(new TableColumn { Width = new GridLength(1.8, GridUnitType.Star) }); // Particulars
        table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });   // Group
        table.Columns.Add(new TableColumn { Width = new GridLength(110) }); // Op Dr
        table.Columns.Add(new TableColumn { Width = new GridLength(110) }); // Op Cr
        table.Columns.Add(new TableColumn { Width = new GridLength(110) }); // Period Dr
        table.Columns.Add(new TableColumn { Width = new GridLength(110) }); // Period Cr
        table.Columns.Add(new TableColumn { Width = new GridLength(120) }); // Close Dr
        table.Columns.Add(new TableColumn { Width = new GridLength(120) }); // Close Cr

        var rowGroup = new TableRowGroup();

        // Header Row
        var headerRow = new TableRow { Background = new SolidColorBrush(Color.FromRgb(241, 245, 249)) };
        AddHeaderCell(headerRow, "Particulars / Account", TextAlignment.Left);
        AddHeaderCell(headerRow, "Category", TextAlignment.Left);
        AddHeaderCell(headerRow, "Opening Dr (₹)", TextAlignment.Right);
        AddHeaderCell(headerRow, "Opening Cr (₹)", TextAlignment.Right);
        AddHeaderCell(headerRow, "Debit Tx (₹)", TextAlignment.Right);
        AddHeaderCell(headerRow, "Credit Tx (₹)", TextAlignment.Right);
        AddHeaderCell(headerRow, "Closing Dr (₹)", TextAlignment.Right);
        AddHeaderCell(headerRow, "Closing Cr (₹)", TextAlignment.Right);
        rowGroup.Rows.Add(headerRow);

        bool alternate = false;
        foreach (var item in vm.Items)
        {
            var row = new TableRow();
            if (alternate) row.Background = new SolidColorBrush(Color.FromRgb(248, 250, 252));
            alternate = !alternate;

            AddDataCell(row, item.AccountName, TextAlignment.Left, isBold: true);
            AddDataCell(row, item.HeadCategory, TextAlignment.Left);
            AddDataCell(row, item.FormattedOpeningDebit, TextAlignment.Right);
            AddDataCell(row, item.FormattedOpeningCredit, TextAlignment.Right);
            AddDataCell(row, item.FormattedPeriodDebit, TextAlignment.Right);
            AddDataCell(row, item.FormattedPeriodCredit, TextAlignment.Right);
            AddDataCell(row, item.FormattedClosingDebit, TextAlignment.Right, isBold: item.ClosingDebit > 0);
            AddDataCell(row, item.FormattedClosingCredit, TextAlignment.Right, isBold: item.ClosingCredit > 0);

            rowGroup.Rows.Add(row);
        }

        // Totals Footer Row
        var totalRow = new TableRow
        {
            Background = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
            FontWeight = FontWeights.Bold
        };
        AddDataCell(totalRow, "TOTAL", TextAlignment.Left, isBold: true);
        AddDataCell(totalRow, $"{vm.Items.Count} Accounts", TextAlignment.Left);
        AddDataCell(totalRow, vm.Summary.FormattedTotalOpeningDebit, TextAlignment.Right, isBold: true);
        AddDataCell(totalRow, vm.Summary.FormattedTotalOpeningCredit, TextAlignment.Right, isBold: true);
        AddDataCell(totalRow, vm.Summary.FormattedTotalPeriodDebit, TextAlignment.Right, isBold: true);
        AddDataCell(totalRow, vm.Summary.FormattedTotalPeriodCredit, TextAlignment.Right, isBold: true);
        AddDataCell(totalRow, vm.Summary.FormattedTotalClosingDebit, TextAlignment.Right, isBold: true);
        AddDataCell(totalRow, vm.Summary.FormattedTotalClosingCredit, TextAlignment.Right, isBold: true);
        rowGroup.Rows.Add(totalRow);

        table.RowGroups.Add(rowGroup);
        doc.Blocks.Add(table);

        // Status banner
        var statusPara = new Paragraph(new Run($"Trial Balance Status: {vm.Summary.BalancingStatusText}"))
        {
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = vm.Summary.IsBalanced ? new SolidColorBrush(Color.FromRgb(16, 185, 129)) : new SolidColorBrush(Color.FromRgb(239, 68, 68)),
            TextAlignment = TextAlignment.Right,
            Margin = new Thickness(0, 4, 0, 0)
        };
        doc.Blocks.Add(statusPara);

        return doc;
    }

    private static void AddHeaderCell(TableRow row, string text, TextAlignment alignment)
    {
        var cell = new TableCell(new Paragraph(new Run(text))
        {
            FontWeight = FontWeights.SemiBold,
            FontSize = 11.5,
            TextAlignment = alignment,
            Margin = new Thickness(0)
        })
        {
            Padding = new Thickness(6, 6, 6, 6),
            BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
            BorderThickness = new Thickness(0, 0, 0, 1.5)
        };
        row.Cells.Add(cell);
    }

    private static void AddDataCell(TableRow row, string text, TextAlignment alignment, bool isBold = false)
    {
        var run = new Run(text);
        if (isBold) run.FontWeight = FontWeights.SemiBold;

        var cell = new TableCell(new Paragraph(run)
        {
            FontSize = 11,
            TextAlignment = alignment,
            Margin = new Thickness(0)
        })
        {
            Padding = new Thickness(6, 4, 6, 4),
            BorderBrush = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
            BorderThickness = new Thickness(0, 0, 0, 1)
        };
        row.Cells.Add(cell);
    }
}
