using System.Collections;
using System.Collections.Specialized;
using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Xml;

namespace ConceptJewelleryPro.WPF.Services;

public static class FlowDocumentTableBinder
{
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.RegisterAttached(
            "ItemsSource",
            typeof(IEnumerable),
            typeof(FlowDocumentTableBinder),
            new PropertyMetadata(null, OnItemsSourceChanged));

    public static IEnumerable? GetItemsSource(DependencyObject obj) =>
        (IEnumerable?)obj.GetValue(ItemsSourceProperty);

    public static void SetItemsSource(DependencyObject obj, IEnumerable? value) =>
        obj.SetValue(ItemsSourceProperty, value);

    private static readonly DependencyProperty TemplateXamlProperty =
        DependencyProperty.RegisterAttached(
            "TemplateXaml",
            typeof(string),
            typeof(FlowDocumentTableBinder),
            new PropertyMetadata(null));

    public static readonly DependencyProperty RowIndexProperty =
        DependencyProperty.RegisterAttached(
            "RowIndex",
            typeof(int),
            typeof(FlowDocumentTableBinder),
            new PropertyMetadata(0));

    public static int GetRowIndex(DependencyObject obj) => (int)obj.GetValue(RowIndexProperty);
    public static void SetRowIndex(DependencyObject obj, int value) => obj.SetValue(RowIndexProperty, value);

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TableRowGroup group) return;

        if (e.OldValue is INotifyCollectionChanged oldNcc)
        {
            oldNcc.CollectionChanged -= (s, args) => GenerateRows(group);
        }

        if (group.GetValue(TemplateXamlProperty) == null && group.Rows.Count > 0)
        {
            // Save the original template row as XAML string
            var templateRow = group.Rows[0];
            var xaml = XamlWriter.Save(templateRow);
            group.SetValue(TemplateXamlProperty, xaml);
        }

        GenerateRows(group);

        if (e.NewValue is INotifyCollectionChanged newNcc)
        {
            newNcc.CollectionChanged += (s, args) => GenerateRows(group);
        }
    }

    public static void GenerateRows(TableRowGroup group)
    {
        var templateXaml = (string?)group.GetValue(TemplateXamlProperty);
        if (string.IsNullOrEmpty(templateXaml)) return;

        var items = GetItemsSource(group);
        group.Rows.Clear();

        if (items == null) return;

        int index = 1;
        foreach (var item in items)
        {
            if (item == null) continue;

            using var stringReader = new StringReader(templateXaml);
            using var xmlReader = XmlReader.Create(stringReader);
            if (XamlReader.Load(xmlReader) is TableRow row)
            {
                row.DataContext = item;
                SetRowIndex(row, index);
                PropagateDataContextAndIndex(row, item, index);
                group.Rows.Add(row);
                index++;
            }
        }
    }

    private static void PropagateDataContextAndIndex(DependencyObject parent, object dataContext, int index)
    {
        if (parent is FrameworkContentElement fce)
        {
            fce.DataContext = dataContext;
            SetRowIndex(fce, index);
        }
        else if (parent is FrameworkElement fe)
        {
            fe.DataContext = dataContext;
            SetRowIndex(fe, index);
        }

        if (parent is TableRow row)
        {
            foreach (var cell in row.Cells)
            {
                PropagateDataContextAndIndex(cell, dataContext, index);
            }
        }
        else if (parent is TableCell cell)
        {
            foreach (var block in cell.Blocks)
            {
                PropagateDataContextAndIndex(block, dataContext, index);
            }
        }
        else if (parent is Paragraph paragraph)
        {
            foreach (var inline in paragraph.Inlines)
            {
                PropagateDataContextAndIndex(inline, dataContext, index);
            }
        }
        else if (parent is Span span)
        {
            foreach (var inline in span.Inlines)
            {
                PropagateDataContextAndIndex(inline, dataContext, index);
            }
        }
    }
}
