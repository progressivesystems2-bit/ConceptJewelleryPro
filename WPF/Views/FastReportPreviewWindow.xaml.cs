using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ConceptJewelleryPro.Infrastructure.Printing;
using Microsoft.Win32;
using Telerik.Windows.Documents.Fixed.FormatProviders.Pdf;
using Telerik.Windows.Documents.Fixed.Model;
using Telerik.Windows.Documents.Fixed.Model.Editing;

namespace ConceptJewelleryPro.WPF.Views;

public partial class FastReportPreviewWindow : Window
{
    private readonly FastReportPrintService.PreparedFastReportDocument _prepared;
    private double _zoom = 1.0;

    public FastReportPreviewWindow(FastReportPrintService.PreparedFastReportDocument prepared)
    {
        InitializeComponent();
        _prepared = prepared;

        Title = prepared.Title;
        HeaderTitleTextBlock.Text = prepared.Title;
        EngineBadgeTextBlock.Text = $"FASTREPORT • {prepared.TemplateName}.frx";
        FooterStatusTextBlock.Text = $"Pages: {prepared.PagePngBytes.Count}  |  Paper: {prepared.PaperWidthMm:0}mm × {prepared.PaperHeightMm:0}mm {(prepared.IsLandscape ? "(Landscape)" : "(Portrait)")}  |  Press F10 or Ctrl+P to Print";

        var images = new List<BitmapImage>(prepared.PagePngBytes.Count);
        foreach (var png in prepared.PagePngBytes)
        {
            var bmp = new BitmapImage();
            using (var ms = new MemoryStream(png))
            {
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = ms;
                bmp.EndInit();
            }
            bmp.Freeze();
            images.Add(bmp);
        }

        PagesItemsControl.ItemsSource = images;
        KeyDown += Window_KeyDown;
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F10 || (e.Key == Key.P && (Keyboard.Modifiers & ModifierKeys.Control) != 0))
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

    private void ZoomIn_Click(object sender, RoutedEventArgs e)
    {
        _zoom = Math.Min(2.5, _zoom + 0.15);
        ApplyZoom();
    }

    private void ZoomOut_Click(object sender, RoutedEventArgs e)
    {
        _zoom = Math.Max(0.4, _zoom - 0.15);
        ApplyZoom();
    }

    private void ApplyZoom()
    {
        PageScaleTransform.ScaleX = _zoom;
        PageScaleTransform.ScaleY = _zoom;
        ZoomPercentTextBlock.Text = $"{Math.Round(_zoom * 100)}%";
    }

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dlg = new System.Windows.Controls.PrintDialog();
            if (_prepared.IsLandscape && dlg.PrintTicket != null)
            {
                dlg.PrintTicket.PageOrientation = System.Printing.PageOrientation.Landscape;
            }

            if (dlg.ShowDialog() == true)
            {
                var printerName = dlg.PrintQueue?.FullName;
                FastReportPrintService.PrintPreparedDocument(_prepared, printerName);
                FooterStatusTextBlock.Text = $"Sent to printer: {printerName ?? "Default Printer"}";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Print failed: {ex.Message}", "Print Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SavePdf_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var safeName = string.Concat(_prepared.DocumentName.Where(c => !Path.GetInvalidFileNameChars().Contains(c)));
            if (string.IsNullOrWhiteSpace(safeName)) safeName = "Document";

            var dlg = new SaveFileDialog
            {
                Filter = "PDF Files (*.pdf)|*.pdf",
                FileName = $"{safeName}.pdf"
            };

            if (dlg.ShowDialog() != true)
                return;

            var pdfBytes = RenderPreparedToPdfBytes(_prepared);
            File.WriteAllBytes(dlg.FileName, pdfBytes);
            FooterStatusTextBlock.Text = $"Saved PDF to {dlg.FileName}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save PDF: {ex.Message}", "PDF Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public static byte[] RenderPreparedToPdfBytes(FastReportPrintService.PreparedFastReportDocument prepared)
    {
        var radFixedDoc = new RadFixedDocument();
        // Convert mm to DIPs (1 inch = 25.4 mm = 96 DIPs, and RadFixedPage.Size expects DIPs in Telerik WPF)
        double widthDip = (prepared.PaperWidthMm / 25.4) * 96.0;
        double heightDip = (prepared.PaperHeightMm / 25.4) * 96.0;
        if (prepared.IsLandscape && widthDip < heightDip)
        {
            (widthDip, heightDip) = (heightDip, widthDip);
        }

        foreach (var png in prepared.PagePngBytes)
        {
            using var ms = new MemoryStream(png);
            var radPage = radFixedDoc.Pages.AddPage();
            radPage.Size = new Size(widthDip, heightDip);
            var editor = new FixedContentEditor(radPage);
            editor.DrawImage(ms, new Size(widthDip, heightDip));
        }

        var provider = new PdfFormatProvider();
        return provider.Export(radFixedDoc, TimeSpan.FromSeconds(15));
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
