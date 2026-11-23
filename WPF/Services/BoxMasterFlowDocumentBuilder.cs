using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.WPF.Helpers;

namespace ConceptJewelleryPro.WPF.Services;

/// <summary>
/// Generates standard A4 FlowDocuments for Box Master labels.
/// </summary>
public static class BoxMasterFlowDocumentBuilder
{
    // A4 dimensions at 96 DPI: 210mm x 297mm -> ~793.7 x 1122.5 DIP
    private const double A4Width = 793.7;
    private const double A4Height = 1122.5;

    public static FlowDocument CreateDocument(IReadOnlyList<BoxMaster> boxes)
    {
        // 80% width of A4 paper centered horizontally: 793.7 * 0.80 = ~635.0 DIP
        double targetLabelWidth = Math.Round(A4Width * 0.80, 1); // ~635.0 DIP
        double sidePadding = Math.Round((A4Width - targetLabelWidth) / 2.0, 1); // ~79.4 DIP
        const double verticalPadding = 25.0;

        var doc = new FlowDocument
        {
            PageWidth = A4Width,
            PageHeight = A4Height,
            ColumnWidth = A4Width,
            PagePadding = new Thickness(sidePadding, verticalPadding, sidePadding, verticalPadding),
            Background = Brushes.White,
            FontFamily = new FontFamily("Segoe UI, Arial, sans-serif")
        };

        if (boxes.Count == 0)
        {
            var emptyPara = new Paragraph(new Run("No boxes selected for printing."))
            {
                FontSize = 14,
                Foreground = Brushes.Gray,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 50, 0, 0)
            };
            doc.Blocks.Add(emptyPara);
            return doc;
        }

        var table = new Table
        {
            CellSpacing = 0,
            Margin = new Thickness(0)
        };

        // Single column spanning 80% of A4 width
        table.Columns.Add(new TableColumn { Width = new GridLength(targetLabelWidth) });

        var rowGroup = new TableRowGroup();

        foreach (var box in boxes)
        {
            var row = new TableRow();
            var cell = new TableCell(CreateLabelBlock(box, targetLabelWidth));
            row.Cells.Add(cell);
            rowGroup.Rows.Add(row);
        }

        table.RowGroups.Add(rowGroup);
        doc.Blocks.Add(table);

        return doc;
    }

    private static BlockUIContainer CreateLabelBlock(BoxMaster box, double labelWidth)
    {
        var border = new Border
        {
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(1.5),
            Background = Brushes.White,
            Padding = new Thickness(14, 8, 14, 8),
            Margin = new Thickness(0, 5, 0, 5),
            Height = 95,
            Width = labelWidth,
            HorizontalAlignment = HorizontalAlignment.Center,
            SnapsToDevicePixels = true
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Left Section: Barcode + BoxNo + Rack
        var leftPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 140,
            Margin = new Thickness(0, 0, 16, 0)
        };

        // 1. Barcode Image
        var barcodeSource = Code128BarcodeGenerator.GenerateBarcodeImage(box.BoxNo, height: 28, moduleWidth: 1.25);
        var barcodeImage = new Image
        {
            Source = barcodeSource,
            Height = 28,
            HorizontalAlignment = HorizontalAlignment.Left,
            Stretch = Stretch.Uniform,
            SnapsToDevicePixels = true
        };
        RenderOptions.SetBitmapScalingMode(barcodeImage, BitmapScalingMode.NearestNeighbor);
        leftPanel.Children.Add(barcodeImage);

        // 2. Box No
        var boxNoText = new TextBlock
        {
            Text = box.BoxNo?.Trim() ?? string.Empty,
            FontFamily = new FontFamily("Segoe UI, Arial, sans-serif"),
            FontSize = 13.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.Black,
            Margin = new Thickness(2, 2, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        leftPanel.Children.Add(boxNoText);

        // 3. Rack Position
        if (!string.IsNullOrWhiteSpace(box.Rack))
        {
            var rackText = new TextBlock
            {
                Text = box.Rack.Trim(),
                FontFamily = new FontFamily("Segoe UI, Arial, sans-serif"),
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Black,
                Margin = new Thickness(2, 1, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            leftPanel.Children.Add(rackText);
        }

        Grid.SetColumn(leftPanel, 0);
        grid.Children.Add(leftPanel);

        // Right Section: Box Name in prominent bold uppercase
        string name = box.BoxName?.Trim().ToUpperInvariant() ?? string.Empty;
        double fontSize = name.Length > 30 ? 15 : (name.Length > 20 ? 18 : 22);

        var nameBlock = new TextBlock
        {
            Text = name,
            FontFamily = new FontFamily("Segoe UI, Arial, sans-serif"),
            FontSize = fontSize,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.Black,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(8, 0, 8, 0)
        };

        Grid.SetColumn(nameBlock, 1);
        grid.Children.Add(nameBlock);

        border.Child = grid;

        return new BlockUIContainer(border)
        {
            Margin = new Thickness(0)
        };
    }
}
