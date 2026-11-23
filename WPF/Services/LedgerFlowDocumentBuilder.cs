using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.WPF.ViewModels;

namespace ConceptJewelleryPro.WPF.Services;

public static class LedgerFlowDocumentBuilder
{
    private static readonly CultureInfo IndianCulture = new("en-IN");

    public static FlowDocument CreateLedgerDocument(LedgerReportViewModel vm)
    {
        var doc = new FlowDocument
        {
            PageWidth = 842, // A4 Portrait / Standard Ledger Width
            PageHeight = 1190, // A4 Height
            PagePadding = new Thickness(36, 28, 36, 28),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 11,
            ColumnWidth = double.PositiveInfinity
        };

        // Header Title
        var titleParagraph = new Paragraph
        {
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 8)
        };
        titleParagraph.Inlines.Add(new Bold(new Run("CONCEPT JEWELLERY")) { FontSize = 17, Foreground = new SolidColorBrush(Color.FromRgb(30, 41, 59)) });
        titleParagraph.Inlines.Add(new LineBreak());
        titleParagraph.Inlines.Add(new Run("STATEMENT OF ACCOUNT (LEDGER)") { FontSize = 13, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85)) });
        doc.Blocks.Add(titleParagraph);

        // Account Details and Date Range Meta Box
        var metaTable = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 10) };
        metaTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        metaTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });

        var metaRowGroup = new TableRowGroup();
        var row1 = new TableRow();
        row1.Cells.Add(new TableCell(new Paragraph(new Bold(new Run($"Account: {vm.SelectedAccountName}")))));
        row1.Cells.Add(new TableCell(new Paragraph(new Run($"Period: {vm.FromDate:dd/MM/yyyy} to {vm.ToDate:dd/MM/yyyy}")) { TextAlignment = TextAlignment.Right }));
        metaRowGroup.Rows.Add(row1);

        var row2 = new TableRow();
        row2.Cells.Add(new TableCell(new Paragraph(new Run($"Category: {vm.AccountCategory}  |  Ref: {vm.SelectedRefName}")) { Foreground = Brushes.Gray }));
        row2.Cells.Add(new TableCell(new Paragraph(new Run($"Phone: {vm.SelectedPhoneNo}")) { TextAlignment = TextAlignment.Right, Foreground = Brushes.Gray }));
        metaRowGroup.Rows.Add(row2);

        metaTable.RowGroups.Add(metaRowGroup);
        doc.Blocks.Add(metaTable);

        // Section Divider
        doc.Blocks.Add(new BlockUIContainer(new System.Windows.Shapes.Rectangle
        {
            Height = 1.5,
            Fill = new SolidColorBrush(Color.FromRgb(15, 118, 110)),
            Margin = new Thickness(0, 0, 0, 10)
        }));

        if (vm.SelectedTab == LedgerReportTab.LedgerInWeight)
        {
            AppendWeightLedgerTable(doc, vm);
        }
        else
        {
            AppendRupeeLedgerTable(doc, vm);
        }

        // Summary Box
        AppendSummaryBox(doc, vm);

        // Acknowledgment / Signatures if requested
        if (vm.SelectedPrintOption == LedgerPrintOption.WithAcknowledgment)
        {
            AppendAcknowledgmentSection(doc, vm);
        }

        return doc;
    }

    private static void AppendRupeeLedgerTable(FlowDocument doc, LedgerReportViewModel vm)
    {
        var table = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 14) };
        table.Columns.Add(new TableColumn { Width = new GridLength(80) });  // Date
        table.Columns.Add(new TableColumn { Width = new GridLength(105) }); // Vou No
        table.Columns.Add(new TableColumn { Width = new GridLength(100) }); // Vou Type
        table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) }); // Particulars
        table.Columns.Add(new TableColumn { Width = new GridLength(90) });  // Debits
        table.Columns.Add(new TableColumn { Width = new GridLength(90) });  // Credits
        table.Columns.Add(new TableColumn { Width = new GridLength(105) }); // Balance

        var rowGroup = new TableRowGroup();

        // Header
        var headerRow = new TableRow { Background = new SolidColorBrush(Color.FromRgb(241, 245, 249)) };
        AddHeaderCell(headerRow, "Date", TextAlignment.Left);
        AddHeaderCell(headerRow, "Vou No", TextAlignment.Left);
        AddHeaderCell(headerRow, "Type", TextAlignment.Left);
        AddHeaderCell(headerRow, "Particulars / Head", TextAlignment.Left);
        AddHeaderCell(headerRow, "Debits (₹)", TextAlignment.Right);
        AddHeaderCell(headerRow, "Credits (₹)", TextAlignment.Right);
        AddHeaderCell(headerRow, "Balance (₹)", TextAlignment.Right);
        rowGroup.Rows.Add(headerRow);

        bool alternate = false;
        foreach (var entry in vm.LedgerEntries)
        {
            var row = new TableRow();
            if (alternate) row.Background = new SolidColorBrush(Color.FromRgb(248, 250, 252));
            alternate = !alternate;

            AddDataCell(row, entry.FormattedDate, TextAlignment.Left);
            AddDataCell(row, entry.VouNo, TextAlignment.Left);
            AddDataCell(row, entry.VouType, TextAlignment.Left);
            
            var partPara = new Paragraph(new Run(entry.Head));
            if (vm.SelectedPrintOption == LedgerPrintOption.WithNarration && !string.IsNullOrWhiteSpace(entry.Narration))
            {
                partPara.Inlines.Add(new LineBreak());
                partPara.Inlines.Add(new Run($"({entry.Narration})") { FontSize = 9.5, Foreground = Brushes.DimGray, FontStyle = FontStyles.Italic });
            }
            row.Cells.Add(new TableCell(partPara) { Padding = new Thickness(4, 3, 4, 3), BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(0, 0, 0, 0.5) });

            AddDataCell(row, entry.FormattedDebit, TextAlignment.Right);
            AddDataCell(row, entry.FormattedCredit, TextAlignment.Right);
            AddDataCell(row, entry.FormattedBalance, TextAlignment.Right, isBold: true);

            rowGroup.Rows.Add(row);
        }

        table.RowGroups.Add(rowGroup);
        doc.Blocks.Add(table);
    }

    private static void AppendWeightLedgerTable(FlowDocument doc, LedgerReportViewModel vm)
    {
        var table = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 14) };
        table.Columns.Add(new TableColumn { Width = new GridLength(80) });  // Date
        table.Columns.Add(new TableColumn { Width = new GridLength(110) }); // Vou No
        table.Columns.Add(new TableColumn { Width = new GridLength(110) }); // Vou Type
        table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) }); // Item Type / Particulars
        table.Columns.Add(new TableColumn { Width = new GridLength(90) });  // Issue Wt (Dr)
        table.Columns.Add(new TableColumn { Width = new GridLength(90) });  // Recd Wt (Cr)
        table.Columns.Add(new TableColumn { Width = new GridLength(105) }); // Balance (g)

        var rowGroup = new TableRowGroup();

        var headerRow = new TableRow { Background = new SolidColorBrush(Color.FromRgb(241, 245, 249)) };
        AddHeaderCell(headerRow, "Date", TextAlignment.Left);
        AddHeaderCell(headerRow, "Vou No", TextAlignment.Left);
        AddHeaderCell(headerRow, "Type", TextAlignment.Left);
        AddHeaderCell(headerRow, "Item / Particulars", TextAlignment.Left);
        AddHeaderCell(headerRow, "Issued Wt (g)", TextAlignment.Right);
        AddHeaderCell(headerRow, "Recd Wt (g)", TextAlignment.Right);
        AddHeaderCell(headerRow, "Balance Wt (g)", TextAlignment.Right);
        rowGroup.Rows.Add(headerRow);

        bool alternate = false;
        foreach (var entry in vm.WeightEntries)
        {
            var row = new TableRow();
            if (alternate) row.Background = new SolidColorBrush(Color.FromRgb(248, 250, 252));
            alternate = !alternate;

            AddDataCell(row, entry.FormattedDate, TextAlignment.Left);
            AddDataCell(row, entry.VouNo, TextAlignment.Left);
            AddDataCell(row, entry.VouType, TextAlignment.Left);
            AddDataCell(row, entry.Head, TextAlignment.Left);
            AddDataCell(row, entry.FormattedWeightDebit, TextAlignment.Right);
            AddDataCell(row, entry.FormattedWeightCredit, TextAlignment.Right);
            AddDataCell(row, entry.FormattedWeightBalance, TextAlignment.Right, isBold: true);

            rowGroup.Rows.Add(row);
        }

        table.RowGroups.Add(rowGroup);
        doc.Blocks.Add(table);
    }

    private static void AppendSummaryBox(FlowDocument doc, LedgerReportViewModel vm)
    {
        var summaryTable = new Table
        {
            CellSpacing = 0,
            Margin = new Thickness(0, 8, 0, 16),
            Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
            BorderThickness = new Thickness(1)
        };

        summaryTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        summaryTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        summaryTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        summaryTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });

        var rowGroup = new TableRowGroup();
        var row = new TableRow();

        if (vm.SelectedTab == LedgerReportTab.LedgerInWeight)
        {
            AddSummaryCell(row, "Opening Wt", vm.Summary.FormattedOpeningWeight);
            AddSummaryCell(row, "Total Issued Wt", vm.Summary.FormattedTotalWeightIssued + " g");
            AddSummaryCell(row, "Total Recd Wt", vm.Summary.FormattedTotalWeightReceived + " g");
            AddSummaryCell(row, "Closing Wt", vm.Summary.FormattedClosingWeight);
        }
        else
        {
            AddSummaryCell(row, "Opening Balance", "₹ " + vm.Summary.FormattedOpening);
            AddSummaryCell(row, "Total Debits", "₹ " + vm.Summary.FormattedTotalDebits);
            AddSummaryCell(row, "Total Credits", "₹ " + vm.Summary.FormattedTotalCredits);
            AddSummaryCell(row, "Closing Balance", "₹ " + vm.Summary.FormattedClosingBalance);
        }

        rowGroup.Rows.Add(row);
        summaryTable.RowGroups.Add(rowGroup);
        doc.Blocks.Add(summaryTable);
    }

    private static void AppendAcknowledgmentSection(FlowDocument doc, LedgerReportViewModel vm)
    {
        var ackPara = new Paragraph { Margin = new Thickness(0, 25, 0, 0) };
        ackPara.Inlines.Add(new Run("I/We hereby confirm and acknowledge the correctness of the above statement and closing balance of ") { FontSize = 10.5 });
        ackPara.Inlines.Add(new Bold(new Run($"₹ {vm.Summary.FormattedClosingBalance}")) { FontSize = 11 });
        ackPara.Inlines.Add(new Run($" as on {vm.ToDate:dd/MM/yyyy}."));
        doc.Blocks.Add(ackPara);

        var signTable = new Table { CellSpacing = 0, Margin = new Thickness(0, 35, 0, 0) };
        signTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        signTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });

        var signGroup = new TableRowGroup();
        var signRow = new TableRow();

        signRow.Cells.Add(new TableCell(new Paragraph(new Run("Party / Customer Signature\n\n_________________________")) { TextAlignment = TextAlignment.Left }));
        signRow.Cells.Add(new TableCell(new Paragraph(new Run("For CONCEPT JEWELLERY\n\n_________________________\nAuthorized Signatory")) { TextAlignment = TextAlignment.Right }));

        signGroup.Rows.Add(signRow);
        signTable.RowGroups.Add(signGroup);
        doc.Blocks.Add(signTable);
    }

    private static void AddHeaderCell(TableRow row, string text, TextAlignment alignment)
    {
        var p = new Paragraph(new Bold(new Run(text))) { TextAlignment = alignment };
        row.Cells.Add(new TableCell(p)
        {
            Padding = new Thickness(5, 6, 5, 6),
            BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
            BorderThickness = new Thickness(0, 0, 0, 1)
        });
    }

    private static void AddDataCell(TableRow row, string text, TextAlignment alignment, bool isBold = false)
    {
        Inline inline = new Run(text);
        if (isBold) inline = new Bold(inline);
        var p = new Paragraph(inline) { TextAlignment = alignment };
        row.Cells.Add(new TableCell(p)
        {
            Padding = new Thickness(4, 4, 4, 4),
            BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
            BorderThickness = new Thickness(0, 0, 0, 0.5)
        });
    }

    private static void AddSummaryCell(TableRow row, string label, string value)
    {
        var p = new Paragraph
        {
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0)
        };
        p.Inlines.Add(new Run(label) { FontSize = 10, Foreground = Brushes.Gray });
        p.Inlines.Add(new LineBreak());
        p.Inlines.Add(new Bold(new Run(value)) { FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(15, 118, 110)) });

        row.Cells.Add(new TableCell(p)
        {
            Padding = new Thickness(6, 8, 6, 8)
        });
    }
}
