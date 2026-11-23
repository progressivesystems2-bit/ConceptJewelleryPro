using System;
using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ConceptJewelleryPro.WPF.ViewModels;
using Telerik.Windows.Documents.Fixed.FormatProviders.Pdf;
using Telerik.Windows.Documents.Fixed.Model;
using Telerik.Windows.Documents.Fixed.Model.Editing;

namespace ConceptJewelleryPro.WPF.Services;

public static class InvoicePdfGenerator
{
    /// <summary>
    /// Generates PDF bytes using the exact FlowDocument template configured for the invoice's voucher type (SalesVouType.PrintFile).
    /// </summary>
    public static byte[] GenerateInvoicePdfBytes(InvoiceViewModel invoice, string? customTemplatePath = null)
    {
        var app = System.Windows.Application.Current;
        if (app?.Dispatcher != null && !app.Dispatcher.CheckAccess())
        {
            return app.Dispatcher.Invoke(() => GenerateInvoicePdfBytesCore(invoice, customTemplatePath));
        }

        return GenerateInvoicePdfBytesCore(invoice, customTemplatePath);
    }

    private static byte[] GenerateInvoicePdfBytesCore(InvoiceViewModel invoice, string? customTemplatePath = null)
    {
        if (FastReportDocumentService.HasBillFrxTemplate(invoice, customTemplatePath))
        {
            var prepared = FastReportDocumentService.PrepareBillReport(invoice, customTemplatePath);
            return Views.FastReportPreviewWindow.RenderPreparedToPdfBytes(prepared);
        }

        // 1. Create the exact same FlowDocument used for printing, as defined in SalesVouType.PrintFile
        FlowDocument flowDoc;
        try
        {
            flowDoc = InvoiceFlowDocumentBuilder.CreateInvoiceDocument(invoice, customTemplatePath);
        }
        catch (Exception ex)
        {
            // If custom voucher template fails, fallback to default template
            try
            {
                flowDoc = InvoiceFlowDocumentBuilder.CreateInvoiceDocument(invoice, "TaxInvoiceTemplate.xaml");
            }
            catch
            {
                throw new InvalidOperationException($"Failed to create invoice document from template: {ex.Message}", ex);
            }
        }

        return RenderFlowDocumentToPdfBytes(flowDoc);
    }

    /// <summary>
    /// Renders a FlowDocument to PDF format with high fidelity, preserving all layouts, fonts, barcodes, and styling.
    /// </summary>
    public static byte[] RenderFlowDocumentToPdfBytes(FlowDocument flowDoc, int dpi = 200)
    {
        double pageWidth = flowDoc.PageWidth > 0 ? flowDoc.PageWidth : 793.7;
        double pageHeight = flowDoc.PageHeight > 0 ? flowDoc.PageHeight : 1100.5;
        flowDoc.ColumnWidth = pageWidth;

        var paginator = ((IDocumentPaginatorSource)flowDoc).DocumentPaginator;
        paginator.PageSize = new Size(pageWidth, pageHeight);
        paginator.ComputePageCount();

        var radFixedDoc = new RadFixedDocument();
        int pageCount = paginator.PageCount;
        if (pageCount <= 0)
        {
            pageCount = 1;
        }

        for (int i = 0; i < pageCount; i++)
        {
            var docPage = paginator.GetPage(i);
            if (docPage?.Visual == null) continue;

            double scale = dpi / 96.0;
            int pxW = (int)Math.Ceiling(pageWidth * scale);
            int pxH = (int)Math.Ceiling(pageHeight * scale);

            var rtb = new RenderTargetBitmap(pxW, pxH, dpi, dpi, PixelFormats.Pbgra32);
            rtb.Render(docPage.Visual);

            var encoder = new JpegBitmapEncoder { QualityLevel = 92 };
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using var ms = new MemoryStream();
            encoder.Save(ms);
            ms.Position = 0;

            double ptW = pageWidth * 0.75;
            double ptH = pageHeight * 0.75;

            var radPage = radFixedDoc.Pages.AddPage();
            radPage.Size = new Size(ptW, ptH);

            var editor = new FixedContentEditor(radPage);
            editor.DrawImage(ms, new Size(ptW, ptH));
        }

        var provider = new PdfFormatProvider();
        return provider.Export(radFixedDoc, TimeSpan.FromSeconds(15));
    }

    /// <summary>
    /// Saves the invoice PDF to the target directory using the template configured in SalesVouType.PrintFile.
    /// </summary>
    public static string SaveInvoicePdf(InvoiceViewModel invoice, string targetDirectory, string? customTemplatePath = null)
    {
        if (!Directory.Exists(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        var fileName = $"{invoice.VouNo.Trim()}.pdf";
        var fullPath = Path.Combine(targetDirectory, fileName);
        var bytes = GenerateInvoicePdfBytes(invoice, customTemplatePath);
        File.WriteAllBytes(fullPath, bytes);
        return fullPath;
    }
}
