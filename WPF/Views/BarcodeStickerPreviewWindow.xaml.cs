using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ConceptJewelleryPro.Infrastructure.Printing;
using ConceptJewelleryPro.WPF.Helpers;

namespace ConceptJewelleryPro.WPF.Views;

public partial class BarcodeStickerPreviewWindow : Window
{
    public BarcodeStickerPreviewWindow(BarcodePrintService.BarcodeStickerPreviewData data)
    {
        InitializeComponent();

        HeaderTitleText.Text = $"Sticker Preview — #{data.Barcode} ({data.ItemName})";
        HeaderSubtitleText.Text = $"Format: {data.FormatName}  •  Printer: {data.PrinterName}";
        EngineBadgeText.Text = $"{data.PrintEngine} FILE";

        if (data.PreviewImageBytes is { Length: > 0 })
        {
            var bitmap = new BitmapImage();
            using (var ms = new MemoryStream(data.PreviewImageBytes))
            {
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = ms;
                bitmap.EndInit();
            }
            bitmap.Freeze();

            FrxPreviewImage.Source = bitmap;
            FrxImagePanel.Visibility = Visibility.Visible;
        }
        else
        {
            var item = data.Item;
            if (string.Equals(item.BType, "PC", StringComparison.OrdinalIgnoreCase))
            {
                PrnLeftLine1.Text = $"{item.ItemName} {item.Purity}".Trim();
                PrnLeftLine2.Text = $"MRP: {item.MRP:N0}";
                PrnLeftLine3.Text = $"SZ:{(string.IsNullOrWhiteSpace(item.Size) ? "-" : item.Size)}  Q:{item.Qty:N0} PC";
                PrnLeftLine4.Text = item.Description ?? string.Empty;
            }
            else
            {
                PrnLeftLine1.Text = item.ItemName ?? string.Empty;
                PrnLeftLine2.Text = $"Nt Wt: {item.NetWt:F3}g";
                PrnLeftLine3.Text = $"{item.Purity} {item.Description}".Trim();
                PrnLeftLine4.Text = $"MC: {Math.Ceiling(item.Making):N0} {(string.IsNullOrWhiteSpace(item.MakingType) ? "GRAM" : item.MakingType)}";
            }

            PrnBarcodeImage.Source = Code128BarcodeGenerator.GenerateBarcodeImage(data.Barcode, height: 34, moduleWidth: 1.2);
            PrnBarcodeText.Text = data.Barcode;
            PrnRawCommandBox.Text = data.RawPrnCommand ?? string.Empty;
            PrnPreviewPanel.Visibility = Visibility.Visible;
        }

        KeyDown += OnWindowKeyDown;
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F10 || e.Key == Key.Enter)
        {
            DialogResult = true;
            Close();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
            e.Handled = true;
        }
    }

    private void PrintButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
