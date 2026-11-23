using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;

namespace ConceptJewelleryPro.WPF.Views;

public partial class InvoicePrintPreviewWindow : Window
{
    private readonly FlowDocument _document;
    private readonly string _documentName;

    public InvoicePrintPreviewWindow(FlowDocument document, string windowTitle = "Tax Invoice Print Preview", string? headerTitle = null, string docName = "Sales Report")
    {
        InitializeComponent();
        _document = document;
        _documentName = docName;
        Title = windowTitle;

        if (!string.IsNullOrWhiteSpace(headerTitle) && HeaderTitleTextBlock != null)
        {
            HeaderTitleTextBlock.Text = headerTitle;
        }

        DocumentScrollViewer.Document = _document;

        KeyDown += Window_KeyDown;
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F10)
        {
            Print_Click(sender, e);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Close_Click(sender, e);
            e.Handled = true;
        }
    }

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        var printDialog = new System.Windows.Controls.PrintDialog();

        // Automatically detect if template is Landscape (PageWidth > PageHeight)
        if (_document.PageWidth > _document.PageHeight && printDialog.PrintTicket != null)
        {
            printDialog.PrintTicket.PageOrientation = System.Printing.PageOrientation.Landscape;
        }

        if (printDialog.ShowDialog() == true)
        {
            _document.PageWidth = printDialog.PrintableAreaWidth > 0 ? printDialog.PrintableAreaWidth : _document.PageWidth;
            _document.PageHeight = printDialog.PrintableAreaHeight > 0 ? printDialog.PrintableAreaHeight : _document.PageHeight;
            _document.ColumnWidth = _document.PageWidth;

            IDocumentPaginatorSource idp = _document;
            printDialog.PrintDocument(idp.DocumentPaginator, _documentName);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
