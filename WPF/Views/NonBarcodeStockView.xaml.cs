using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ConceptJewelleryPro.Domain.Entities;
using Telerik.Windows.Controls;
using Telerik.Windows.Controls.GridView;

namespace ConceptJewelleryPro.WPF.Views;

public partial class NonBarcodeStockView : UserControl
{
    public NonBarcodeStockView()
    {
        InitializeComponent();
    }

    private void ItemNameCombo_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is RadComboBox combo)
        {
            App.ApplyComboBoxDropDownChevron(combo);

            if (combo.FilteringBehavior is not ConceptJewelleryPro.WPF.Helpers.PrefixPriorityFilteringBehavior)
            {
                combo.FilteringBehavior = new ConceptJewelleryPro.WPF.Helpers.PrefixPriorityFilteringBehavior();
            }

            if (combo.DataContext is NonBarcodeSubform line && !string.IsNullOrWhiteSpace(line.ItemName))
            {
                combo.Text = line.ItemName;
                combo.SelectedItem = line.ItemName;
            }
        }
    }

    private void ItemNameCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is RadComboBox combo && combo.DataContext is NonBarcodeSubform line)
        {
            if (combo.SelectedItem is string selected && !string.IsNullOrWhiteSpace(selected))
            {
                line.ItemName = selected;
                combo.Text = selected;
            }
            else if (combo.SelectedItem is Item itemObj && !string.IsNullOrWhiteSpace(itemObj.ItemName))
            {
                line.ItemName = itemObj.ItemName;
                combo.Text = itemObj.ItemName;
            }
        }
    }

    private void ItemNameCombo_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is RadComboBox combo && combo.DataContext is NonBarcodeSubform line)
        {
            CommitComboValue(combo, line);
        }
    }

    private void ItemNameCombo_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is RadComboBox combo && combo.DataContext is NonBarcodeSubform line)
        {
            if (e.Key is Key.Enter or Key.Tab)
            {
                CommitComboValue(combo, line);
                if (combo.IsDropDownOpen)
                {
                    combo.IsDropDownOpen = false;
                }
            }
        }
    }

    private void StockGrid_CellEditEnded(object? sender, GridViewCellEditEndedEventArgs e)
    {
        if (e.Cell?.DataContext is NonBarcodeSubform line && e.EditingElement is FrameworkElement elem)
        {
            var combo = elem as RadComboBox ?? FindVisualChild<RadComboBox>(elem);
            if (combo != null)
            {
                CommitComboValue(combo, line);
            }
        }
    }

    private static void CommitComboValue(RadComboBox combo, NonBarcodeSubform line)
    {
        var text = combo.Text?.Trim();
        if (combo.SelectedItem is string selected && !string.IsNullOrWhiteSpace(selected))
        {
            line.ItemName = selected;
        }
        else if (combo.SelectedItem is Item itemObj && !string.IsNullOrWhiteSpace(itemObj.ItemName))
        {
            line.ItemName = itemObj.ItemName;
        }
        else if (!string.IsNullOrWhiteSpace(text))
        {
            line.ItemName = text;
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild) return typedChild;
            var nested = FindVisualChild<T>(child);
            if (nested != null) return nested;
        }
        return null;
    }
}
