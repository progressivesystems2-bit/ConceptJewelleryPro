using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using Telerik.Windows.Controls;

namespace ConceptJewelleryPro.WPF.Views;

public partial class LedgerGroupView : UserControl
{
    public LedgerGroupView()
    {
        InitializeComponent();
    }

    private void HeadGroupComboBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is RadComboBox combo)
        {
            var text = combo.Text?.Trim();
            if (!string.IsNullOrEmpty(text) && combo.ItemsSource is IEnumerable<string> items)
            {
                var itemList = items.ToList();
                var matched = itemList.FirstOrDefault(i => string.Equals(i, text, StringComparison.OrdinalIgnoreCase))
                           ?? itemList.FirstOrDefault(i => i.Contains(text, StringComparison.OrdinalIgnoreCase));
                if (matched != null)
                {
                    combo.SelectedItem = matched;
                }
            }
            combo.IsDropDownOpen = false;
            e.Handled = true;
        }
    }
}
