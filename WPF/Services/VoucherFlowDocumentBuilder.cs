using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using ConceptJewelleryPro.WPF.ViewModels;

namespace ConceptJewelleryPro.WPF.Services;

public static class VoucherFlowDocumentBuilder
{
    private static readonly CultureInfo IndianCulture = new("en-IN");

    public static FlowDocument CreateVoucherDocument(VoucherEntryViewModel vm)
    {
        var doc = new FlowDocument
        {
            PageWidth = 793, // A4 standard width ~ 8.27in * 96dpi
            PageHeight = 560, // A5 landscape / half A4 voucher slip
            PagePadding = new Thickness(40, 30, 40, 30),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 12,
            ColumnWidth = double.PositiveInfinity
        };

        // Title & Store Header
        var titleParagraph = new Paragraph
        {
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 10)
        };
        titleParagraph.Inlines.Add(new Bold(new Run("CONCEPT JEWELLERY")) { FontSize = 18, Foreground = new SolidColorBrush(Color.FromRgb(30, 41, 59)) });
        titleParagraph.Inlines.Add(new LineBreak());
        titleParagraph.Inlines.Add(new Run($"{vm.Title.ToUpperInvariant()} VOUCHER") { FontSize = 14, FontWeight = FontWeights.Bold });
        doc.Blocks.Add(titleParagraph);

        // Voucher Meta Info (VouNo, Date, Mode)
        var metaTable = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 12) };
        metaTable.Columns.Add(new TableColumn { Width = new GridLength(200) });
        metaTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        metaTable.Columns.Add(new TableColumn { Width = new GridLength(200) });

        var metaRowGroup = new TableRowGroup();
        var metaRow = new TableRow();
        metaRow.Cells.Add(new TableCell(new Paragraph(new Bold(new Run($"Voucher No: {vm.VouNo}")))));
        metaRow.Cells.Add(new TableCell(new Paragraph(new Run(vm.IsJournal ? "" : $"Mode: {vm.PaymentMode}")) { TextAlignment = TextAlignment.Center }));
        metaRow.Cells.Add(new TableCell(new Paragraph(new Bold(new Run($"Date: {vm.VouDate:dd/MM/yyyy}"))) { TextAlignment = TextAlignment.Right }));
        metaRowGroup.Rows.Add(metaRow);
        metaTable.RowGroups.Add(metaRowGroup);
        doc.Blocks.Add(metaTable);

        // Divider
        doc.Blocks.Add(new BlockUIContainer(new System.Windows.Shapes.Rectangle { Height = 1, Fill = Brushes.Gray, Margin = new Thickness(0, 0, 0, 12) }));

        // Transaction Details Table
        var detailsTable = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 12) };
        detailsTable.Columns.Add(new TableColumn { Width = new GridLength(220) });
        detailsTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });

        var detailsRowGroup = new TableRowGroup();

        void AddDetailRow(string label, string value, bool isBold = false)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            var r = new TableRow();
            r.Cells.Add(new TableCell(new Paragraph(new Run(label)) { FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 3, 0, 3) }));
            var valRun = isBold ? (Inline)new Bold(new Run(value)) : new Run(value);
            r.Cells.Add(new TableCell(new Paragraph(valRun) { Margin = new Thickness(0, 3, 0, 3) }));
            detailsRowGroup.Rows.Add(r);
        }

        AddDetailRow(vm.DrAccountLabel + ":", vm.DrAccount);
        AddDetailRow(vm.CrAccountLabel + ":", vm.CrAccount);
        AddDetailRow("Amount:", $"₹ {vm.Amount.ToString("N2", IndianCulture)}", true);

        if (!vm.IsJournal)
        {
            if (!string.IsNullOrWhiteSpace(vm.ChequeNo)) AddDetailRow("Cheque / Ref No:", vm.ChequeNo);
            if (vm.ChqDate.HasValue) AddDetailRow("Cheque Date:", $"{vm.ChqDate.Value:dd/MM/yyyy}");
            if (!string.IsNullOrWhiteSpace(vm.BankName)) AddDetailRow("Bank Name:", vm.BankName);
            if (!string.IsNullOrWhiteSpace(vm.CardType)) AddDetailRow("Card Type:", vm.CardType);
            if (vm.CardComm > 0) AddDetailRow("Card Commission:", $"{vm.CardComm}% (₹ {vm.CommAmt:N2}) - Net: ₹ {vm.NetCredited:N2}");
        }

        AddDetailRow("Narration:", vm.Narration);

        detailsTable.RowGroups.Add(detailsRowGroup);
        doc.Blocks.Add(detailsTable);

        // Signatures Table
        var signTable = new Table { CellSpacing = 0, Margin = new Thickness(0, 25, 0, 0) };
        signTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        signTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        signTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });

        var signGroup = new TableRowGroup();
        var signRow = new TableRow();
        signRow.Cells.Add(new TableCell(new Paragraph(new Run("Prepared By\n\n_________________")) { TextAlignment = TextAlignment.Left }));
        signRow.Cells.Add(new TableCell(new Paragraph(new Run("Receiver's Signature\n\n_________________")) { TextAlignment = TextAlignment.Center }));
        signRow.Cells.Add(new TableCell(new Paragraph(new Run("Authorized Signatory\n\n_________________")) { TextAlignment = TextAlignment.Right }));
        signGroup.Rows.Add(signRow);
        signTable.RowGroups.Add(signGroup);
        doc.Blocks.Add(signTable);

        return doc;
    }

    public static FlowDocument CreateSchemeReceiptDocument(SchemeReceiptViewModel vm)
    {
        var doc = new FlowDocument
        {
            PageWidth = 793,
            PageHeight = 560,
            PagePadding = new Thickness(40, 30, 40, 30),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 12,
            ColumnWidth = double.PositiveInfinity
        };

        // Header
        var titleParagraph = new Paragraph
        {
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 10)
        };
        titleParagraph.Inlines.Add(new Bold(new Run("CONCEPT JEWELLERY")) { FontSize = 18, Foreground = new SolidColorBrush(Color.FromRgb(0, 59, 70)) });
        titleParagraph.Inlines.Add(new LineBreak());
        titleParagraph.Inlines.Add(new Run("SAVINGS SCHEME RECEIPT") { FontSize = 14, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(15, 76, 92)) });
        doc.Blocks.Add(titleParagraph);

        // Meta Info Table
        var metaTable = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 12) };
        metaTable.Columns.Add(new TableColumn { Width = new GridLength(220) });
        metaTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        metaTable.Columns.Add(new TableColumn { Width = new GridLength(200) });

        var metaRowGroup = new TableRowGroup();
        var metaRow = new TableRow();
        metaRow.Cells.Add(new TableCell(new Paragraph(new Bold(new Run($"Receipt No: {vm.VouNo}")))));
        metaRow.Cells.Add(new TableCell(new Paragraph(new Bold(new Run($"Scheme Code: {vm.Scode}"))) { TextAlignment = TextAlignment.Center, Foreground = new SolidColorBrush(Color.FromRgb(0, 59, 70)) }));
        metaRow.Cells.Add(new TableCell(new Paragraph(new Bold(new Run($"Date: {vm.VouDate:dd/MM/yyyy}"))) { TextAlignment = TextAlignment.Right }));
        metaRowGroup.Rows.Add(metaRow);
        metaTable.RowGroups.Add(metaRowGroup);
        doc.Blocks.Add(metaTable);

        // Divider
        doc.Blocks.Add(new BlockUIContainer(new System.Windows.Shapes.Rectangle { Height = 1.5, Fill = new SolidColorBrush(Color.FromRgb(0, 59, 70)), Margin = new Thickness(0, 0, 0, 12) }));

        // Details Table
        var detailsTable = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 12) };
        detailsTable.Columns.Add(new TableColumn { Width = new GridLength(200) });
        detailsTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });

        var detailsRowGroup = new TableRowGroup();

        void AddDetailRow(string label, string value, bool isBold = false, Brush? foreground = null)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            var r = new TableRow();
            r.Cells.Add(new TableCell(new Paragraph(new Run(label)) { FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 3, 0, 3) }));
            var valRun = isBold ? (Inline)new Bold(new Run(value)) : new Run(value);
            if (foreground != null) valRun.Foreground = foreground;
            r.Cells.Add(new TableCell(new Paragraph(valRun) { Margin = new Thickness(0, 3, 0, 3) }));
            detailsRowGroup.Rows.Add(r);
        }

        AddDetailRow("Received From (Customer):", vm.CrAccount, true);
        AddDetailRow("Installment Number:", $"Installment #{vm.InstallmentNo} (Paid {vm.PaidInstallmentsCount} Installments)", true);
        AddDetailRow("Amount Received:", $"₹ {vm.Amount.ToString("N2", IndianCulture)}", true, new SolidColorBrush(Color.FromRgb(0, 59, 70)));
        AddDetailRow("Payment Mode:", vm.PaymentMode);
        AddDetailRow("Received By (Account):", vm.DrAccount);

        if (!string.IsNullOrWhiteSpace(vm.ChequeNo)) AddDetailRow("Cheque / Ref No:", vm.ChequeNo);
        if (vm.ChqDate.HasValue) AddDetailRow("Cheque Date:", $"{vm.ChqDate.Value:dd/MM/yyyy}");
        if (!string.IsNullOrWhiteSpace(vm.BankName)) AddDetailRow("Bank Name:", vm.BankName);
        if (!string.IsNullOrWhiteSpace(vm.CardType)) AddDetailRow("Card Type:", vm.CardType);

        if (vm.SelectedSchemeItem is not null)
        {
            AddDetailRow("Scheme Total Value:", $"₹ {vm.SelectedSchemeItem.TotalValue.ToString("N2", IndianCulture)} ({vm.SelectedSchemeItem.Month:0} Months duration)");
            AddDetailRow("Scheme Maturity Date:", $"{vm.SelectedSchemeItem.EDate:dd/MM/yyyy}");
        }

        AddDetailRow("Narration:", vm.Narration);

        detailsTable.RowGroups.Add(detailsRowGroup);
        doc.Blocks.Add(detailsTable);

        // Signatures Table
        var signTable = new Table { CellSpacing = 0, Margin = new Thickness(0, 20, 0, 0) };
        signTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        signTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        signTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });

        var signGroup = new TableRowGroup();
        var signRow = new TableRow();
        signRow.Cells.Add(new TableCell(new Paragraph(new Run($"Prepared By: {vm.CurrentUsername}\n\n_________________")) { TextAlignment = TextAlignment.Left }));
        signRow.Cells.Add(new TableCell(new Paragraph(new Run("Customer Signature\n\n_________________")) { TextAlignment = TextAlignment.Center }));
        signRow.Cells.Add(new TableCell(new Paragraph(new Run("For CONCEPT JEWELLERY\n\n_________________")) { TextAlignment = TextAlignment.Right }));
        signGroup.Rows.Add(signRow);
        signTable.RowGroups.Add(signGroup);
        doc.Blocks.Add(signTable);

        return doc;
    }
}
