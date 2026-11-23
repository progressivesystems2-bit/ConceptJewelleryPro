using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ConceptJewelleryPro.WPF.Helpers;
using ConceptJewelleryPro.WPF.ViewModels;
using Telerik.Windows.Controls;
using Telerik.Windows.Controls.GridView;

namespace ConceptJewelleryPro.WPF.Views;

public partial class PurchaseView : UserControl
{
    private static readonly string[] _itemColumnsOrder =
    {
        "Item Name *",
        "Purity",
        "HSN Code",
        "Qty",
        "Rate",
        "GST %"
    };

    private static readonly string[] _paymentColumnsOrder =
    {
        "Payment A/c",
        "Amount",
        "Narration / Ref"
    };

    public PurchaseView()
    {
        InitializeComponent();
    }

    private void Root_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not PurchaseViewModel vm) return;

        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
        bool isMinus = e.Key == Key.OemMinus || e.Key == Key.Subtract;

        if (ctrl && isMinus && vm.CanEdit)
        {
            e.Handled = true;
            RemoveCurrentGridLine(vm);
        }
    }

    private void DueDateDatePicker_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        if (sender is RadDatePicker rdp)
        {
            rdp.IsDropDownOpen = false;
        }

        e.Handled = true;
        FocusFirstItemCell();
    }

    public void FocusFirstItemCell()
    {
        if (DataContext is not PurchaseViewModel vm || PurchaseItemsGrid == null) return;

        if (vm.Items.Count == 0 && vm.AddItemRowCommand.CanExecute(null))
        {
            vm.AddItemRowCommand.Execute(null);
        }

        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            if (vm.Items.Count > 0)
            {
                FocusItemColumn(0, _itemColumnsOrder[0]);
            }
        }));
    }

    #region Item Combobox & Grid Navigation

    private void ItemNameCombo_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is RadComboBox combo)
        {
            if (combo.FilteringBehavior is not PrefixPriorityFilteringBehavior)
            {
                combo.FilteringBehavior = new PrefixPriorityFilteringBehavior();
            }

            combo.ApplyTemplate();
            var innerBox = (combo.Template?.FindName("PART_EditableTextBox", combo) as TextBox)
                        ?? FindVisualChild<TextBox>(combo);
            if (innerBox != null)
            {
                innerBox.Focus();
                innerBox.SelectAll();
            }
        }
    }

    private void ItemNameCombo_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        if (sender is RadComboBox combo && combo.DataContext is PurchaseSubformItemViewModel item)
        {
            if (combo.IsDropDownOpen)
            {
                var chosen = combo.SelectedItem?.ToString()
                          ?? (combo.Template?.FindName("PART_EditableTextBox", combo) as TextBox)?.Text
                          ?? combo.Text;
                combo.IsDropDownOpen = false;
                if (!string.IsNullOrWhiteSpace(chosen))
                {
                    item.ItemName = chosen.Trim();
                    combo.Text = chosen.Trim();
                }
            }
            else
            {
                var txt = (combo.Template?.FindName("PART_EditableTextBox", combo) as TextBox)?.Text ?? combo.Text;
                if (!string.IsNullOrWhiteSpace(txt))
                {
                    item.ItemName = txt.Trim();
                    combo.Text = txt.Trim();
                }
            }

            e.Handled = true;
            PurchaseItemsGrid?.CommitEdit();

            if (DataContext is PurchaseViewModel vm)
            {
                int rowIndex = vm.Items.IndexOf(item);
                if (rowIndex >= 0)
                {
                    Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
                    {
                        AdvanceItemColumn(rowIndex, "Item Name *", item);
                    }));
                }
            }
        }
    }

    private void PurityCombo_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is RadComboBox combo)
        {
            if (combo.FilteringBehavior is not PrefixPriorityFilteringBehavior)
            {
                combo.FilteringBehavior = new PrefixPriorityFilteringBehavior();
            }

            combo.ApplyTemplate();
            var innerBox = (combo.Template?.FindName("PART_EditableTextBox", combo) as TextBox)
                        ?? FindVisualChild<TextBox>(combo);
            if (innerBox != null)
            {
                innerBox.Focus();
                innerBox.SelectAll();
            }
        }
    }

    private void PurityCombo_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        if (sender is RadComboBox combo && combo.DataContext is PurchaseSubformItemViewModel item)
        {
            if (combo.IsDropDownOpen)
            {
                var chosen = combo.SelectedItem?.ToString()
                          ?? (combo.Template?.FindName("PART_EditableTextBox", combo) as TextBox)?.Text
                          ?? combo.Text;
                combo.IsDropDownOpen = false;
                if (!string.IsNullOrWhiteSpace(chosen))
                {
                    item.Purity = chosen.Trim();
                    combo.Text = chosen.Trim();
                }
            }
            else
            {
                var txt = (combo.Template?.FindName("PART_EditableTextBox", combo) as TextBox)?.Text ?? combo.Text;
                if (!string.IsNullOrWhiteSpace(txt))
                {
                    item.Purity = txt.Trim();
                    combo.Text = txt.Trim();
                }
            }

            e.Handled = true;
            PurchaseItemsGrid?.CommitEdit();

            if (DataContext is PurchaseViewModel vm)
            {
                int rowIndex = vm.Items.IndexOf(item);
                if (rowIndex >= 0)
                {
                    Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
                    {
                        AdvanceItemColumn(rowIndex, "Purity", item);
                    }));
                }
            }
        }
    }

    private void PurchaseItemsGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not PurchaseViewModel vm || !vm.CanEdit || PurchaseItemsGrid == null || vm.Items.Count == 0) return;

        if (e.Key != Key.Enter) return;

        var focusedElem = Keyboard.FocusedElement as DependencyObject;

        // RadComboBox handles its own preview key down directly
        if (focusedElem is RadComboBox || FindVisualParent<RadComboBox>(focusedElem) != null)
        {
            return;
        }

        var cell = (focusedElem as GridViewCell) ?? FindVisualParent<GridViewCell>(focusedElem);
        var colHeader = cell?.Column?.Header?.ToString()
                     ?? PurchaseItemsGrid.CurrentColumn?.Header?.ToString()
                     ?? string.Empty;

        PurchaseSubformItemViewModel? focusedItem = null;
        if (focusedElem != null)
        {
            var row = FindVisualParent<GridViewRow>(focusedElem);
            if (row != null && row.DataContext is PurchaseSubformItemViewModel rowItem)
            {
                focusedItem = rowItem;
            }
        }

        focusedItem ??= PurchaseItemsGrid.SelectedItem as PurchaseSubformItemViewModel
                    ?? PurchaseItemsGrid.CurrentItem as PurchaseSubformItemViewModel
                    ?? vm.Items.FirstOrDefault();

        if (focusedItem == null) return;

        int rowIndex = vm.Items.IndexOf(focusedItem);
        if (rowIndex < 0) rowIndex = 0;

        e.Handled = true;
        PurchaseItemsGrid.CommitEdit();

        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            AdvanceItemColumn(rowIndex, colHeader, focusedItem);
        }));
    }

    private void AdvanceItemColumn(int rowIndex, string currentHeader, PurchaseSubformItemViewModel focusedItem)
    {
        if (DataContext is not PurchaseViewModel vm || PurchaseItemsGrid == null) return;

        int colIdx = Array.FindIndex(_itemColumnsOrder, h => string.Equals(h, currentHeader, StringComparison.OrdinalIgnoreCase));

        if (colIdx >= 0 && colIdx < _itemColumnsOrder.Length - 1)
        {
            string nextHeader = _itemColumnsOrder[colIdx + 1];
            FocusItemColumn(rowIndex, nextHeader);
            return;
        }

        // On last column (GST %) or unknown column
        bool isLastRow = rowIndex >= vm.Items.Count - 1;
        if (isLastRow)
        {
            if (!string.IsNullOrWhiteSpace(focusedItem.ItemName))
            {
                if (vm.AddItemRowCommand.CanExecute(null))
                {
                    vm.AddItemRowCommand.Execute(null);
                }

                Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
                {
                    int newRowIndex = vm.Items.Count - 1;
                    FocusItemColumn(newRowIndex, _itemColumnsOrder[0]);
                }));
            }
            else
            {
                FocusPaymentGrid();
            }
        }
        else
        {
            FocusItemColumn(rowIndex + 1, _itemColumnsOrder[0]);
        }
    }

    private void FocusItemColumn(int rowIndex, string header)
    {
        if (PurchaseItemsGrid == null || rowIndex < 0 || rowIndex >= PurchaseItemsGrid.Items.Count) return;

        var column = PurchaseItemsGrid.Columns
            .OfType<Telerik.Windows.Controls.GridViewColumn>()
            .FirstOrDefault(c => string.Equals(c.Header?.ToString(), header, StringComparison.OrdinalIgnoreCase));

        if (column == null) return;

        var item = PurchaseItemsGrid.Items[rowIndex] as PurchaseSubformItemViewModel;
        if (item == null) return;

        PurchaseItemsGrid.SelectedItem = item;
        PurchaseItemsGrid.CurrentItem = item;
        PurchaseItemsGrid.CurrentColumn = column;
        PurchaseItemsGrid.ScrollIntoView(item);

        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            PurchaseItemsGrid.BeginEdit();
            var row = PurchaseItemsGrid.GetRowForItem(item);
            var cell = row?.Cells.FirstOrDefault(c => c.Column == column);
            if (cell != null)
            {
                var combo = FindVisualChild<RadComboBox>(cell);
                if (combo != null)
                {
                    FocusEditableControl(combo);
                    return;
                }

                var tb = (FrameworkElement?)FindVisualChild<TextBox>(cell)
                      ?? FindVisualChild<RadWatermarkTextBox>(cell);
                if (tb != null)
                {
                    FocusEditableControl(tb);
                    return;
                }

                cell.Focus();
            }
        }));
    }

    #endregion

    #region Payment Combobox & Grid Navigation

    public void FocusPaymentGrid()
    {
        if (DataContext is not PurchaseViewModel vm || PurchasePaymentGrid == null) return;

        if (vm.Payments.Count == 0 && vm.AddPaymentRowCommand.CanExecute(null))
        {
            vm.AddPaymentRowCommand.Execute(null);
        }

        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            if (vm.Payments.Count > 0)
            {
                FocusPaymentColumn(0, _paymentColumnsOrder[0]);
            }
        }));
    }

    private void PaymentAccountCombo_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is RadComboBox combo)
        {
            if (combo.FilteringBehavior is not PrefixPriorityFilteringBehavior)
            {
                combo.FilteringBehavior = new PrefixPriorityFilteringBehavior();
            }

            combo.ApplyTemplate();
            var innerBox = (combo.Template?.FindName("PART_EditableTextBox", combo) as TextBox)
                        ?? FindVisualChild<TextBox>(combo);
            if (innerBox != null)
            {
                innerBox.Focus();
                innerBox.SelectAll();
            }
        }
    }

    private void PaymentAccountCombo_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        if (sender is RadComboBox combo && combo.DataContext is PaymentSubformItemViewModel pay)
        {
            if (combo.IsDropDownOpen)
            {
                var chosen = combo.SelectedItem?.ToString()
                          ?? (combo.Template?.FindName("PART_EditableTextBox", combo) as TextBox)?.Text
                          ?? combo.Text;
                combo.IsDropDownOpen = false;
                if (!string.IsNullOrWhiteSpace(chosen))
                {
                    pay.AccountName = chosen.Trim();
                    combo.Text = chosen.Trim();
                }
            }
            else
            {
                var txt = (combo.Template?.FindName("PART_EditableTextBox", combo) as TextBox)?.Text ?? combo.Text;
                if (!string.IsNullOrWhiteSpace(txt))
                {
                    pay.AccountName = txt.Trim();
                    combo.Text = txt.Trim();
                }
            }

            e.Handled = true;
            PurchasePaymentGrid?.CommitEdit();

            if (DataContext is PurchaseViewModel vm)
            {
                int rowIndex = vm.Payments.IndexOf(pay);
                if (rowIndex >= 0)
                {
                    Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
                    {
                        AdvancePaymentColumn(rowIndex, "Payment A/c", pay);
                    }));
                }
            }
        }
    }

    private void PurchasePaymentGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not PurchaseViewModel vm || !vm.CanEdit || PurchasePaymentGrid == null || vm.Payments.Count == 0) return;

        if (e.Key != Key.Enter) return;

        var focusedElem = Keyboard.FocusedElement as DependencyObject;
        if (focusedElem is RadComboBox || FindVisualParent<RadComboBox>(focusedElem) != null)
        {
            return;
        }

        var cell = (focusedElem as GridViewCell) ?? FindVisualParent<GridViewCell>(focusedElem);
        var colHeader = cell?.Column?.Header?.ToString()
                     ?? PurchasePaymentGrid.CurrentColumn?.Header?.ToString()
                     ?? string.Empty;

        PaymentSubformItemViewModel? focusedPay = null;
        if (focusedElem != null)
        {
            var row = FindVisualParent<GridViewRow>(focusedElem);
            if (row != null && row.DataContext is PaymentSubformItemViewModel rowPay)
            {
                focusedPay = rowPay;
            }
        }

        focusedPay ??= PurchasePaymentGrid.SelectedItem as PaymentSubformItemViewModel
                   ?? PurchasePaymentGrid.CurrentItem as PaymentSubformItemViewModel
                   ?? vm.Payments.FirstOrDefault();

        if (focusedPay == null) return;

        int rowIndex = vm.Payments.IndexOf(focusedPay);
        if (rowIndex < 0) rowIndex = 0;

        e.Handled = true;
        PurchasePaymentGrid.CommitEdit();

        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            AdvancePaymentColumn(rowIndex, colHeader, focusedPay);
        }));
    }

    private void AdvancePaymentColumn(int rowIndex, string currentHeader, PaymentSubformItemViewModel focusedPay)
    {
        if (DataContext is not PurchaseViewModel vm || PurchasePaymentGrid == null) return;

        int colIdx = Array.FindIndex(_paymentColumnsOrder, h => string.Equals(h, currentHeader, StringComparison.OrdinalIgnoreCase));

        if (colIdx >= 0 && colIdx < _paymentColumnsOrder.Length - 1)
        {
            string nextHeader = _paymentColumnsOrder[colIdx + 1];
            FocusPaymentColumn(rowIndex, nextHeader);
            return;
        }

        // On last column (Narration / Ref)
        bool isLastRow = rowIndex >= vm.Payments.Count - 1;
        if (isLastRow)
        {
            if (focusedPay.Amount > 0)
            {
                if (vm.AddPaymentRowCommand.CanExecute(null))
                {
                    vm.AddPaymentRowCommand.Execute(null);
                }

                Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
                {
                    int newRowIndex = vm.Payments.Count - 1;
                    FocusPaymentColumn(newRowIndex, _paymentColumnsOrder[0]);
                }));
            }
        }
        else
        {
            FocusPaymentColumn(rowIndex + 1, _paymentColumnsOrder[0]);
        }
    }

    private void FocusPaymentColumn(int rowIndex, string header)
    {
        if (PurchasePaymentGrid == null || rowIndex < 0 || rowIndex >= PurchasePaymentGrid.Items.Count) return;

        var column = PurchasePaymentGrid.Columns
            .OfType<Telerik.Windows.Controls.GridViewColumn>()
            .FirstOrDefault(c => string.Equals(c.Header?.ToString(), header, StringComparison.OrdinalIgnoreCase));

        if (column == null) return;

        var pay = PurchasePaymentGrid.Items[rowIndex] as PaymentSubformItemViewModel;
        if (pay == null) return;

        PurchasePaymentGrid.SelectedItem = pay;
        PurchasePaymentGrid.CurrentItem = pay;
        PurchasePaymentGrid.CurrentColumn = column;
        PurchasePaymentGrid.ScrollIntoView(pay);

        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            PurchasePaymentGrid.BeginEdit();
            var row = PurchasePaymentGrid.GetRowForItem(pay);
            var cell = row?.Cells.FirstOrDefault(c => c.Column == column);
            if (cell != null)
            {
                var combo = FindVisualChild<RadComboBox>(cell);
                if (combo != null)
                {
                    FocusEditableControl(combo);
                    return;
                }

                var tb = (FrameworkElement?)FindVisualChild<TextBox>(cell)
                      ?? FindVisualChild<RadWatermarkTextBox>(cell);
                if (tb != null)
                {
                    FocusEditableControl(tb);
                    return;
                }

                cell.Focus();
            }
        }));
    }

    #endregion

    #region Helper Methods

    private static void FocusEditableControl(FrameworkElement element)
    {
        if (element is RadComboBox combo)
        {
            if (combo.IsEditable)
            {
                combo.ApplyTemplate();
                var innerBox = (combo.Template?.FindName("PART_EditableTextBox", combo) as TextBox)
                            ?? FindVisualChild<TextBox>(combo)
                            ?? (FrameworkElement?)FindVisualChild<RadWatermarkTextBox>(combo);

                if (innerBox != null)
                {
                    innerBox.Focus();
                    if (innerBox is TextBox tb) tb.SelectAll();
                    else if (innerBox is RadWatermarkTextBox rtb) rtb.SelectAll();
                    return;
                }
            }

            combo.Focus();
            return;
        }

        if (element is TextBox tbElem)
        {
            tbElem.Focus();
            tbElem.SelectAll();
            return;
        }

        if (element is RadWatermarkTextBox rtbElem)
        {
            rtbElem.Focus();
            rtbElem.SelectAll();
            return;
        }

        element.Focus();
    }

    private void RemoveCurrentGridLine(PurchaseViewModel vm)
    {
        if (PurchasePaymentGrid != null && PurchasePaymentGrid.IsKeyboardFocusWithin)
        {
            RemovePaymentGridLine(vm);
            return;
        }

        RemoveItemGridLine(vm);
    }

    private void RemoveItemGridLine(PurchaseViewModel vm)
    {
        if (vm.Items.Count == 0) return;

        PurchaseSubformItemViewModel? targetItem = null;
        var focusedElem = Keyboard.FocusedElement as DependencyObject;
        if (focusedElem != null)
        {
            var row = FindVisualParent<GridViewRow>(focusedElem);
            if (row != null && row.DataContext is PurchaseSubformItemViewModel rowItem)
            {
                targetItem = rowItem;
            }
        }

        if (targetItem == null && PurchaseItemsGrid != null)
        {
            targetItem = PurchaseItemsGrid.SelectedItem as PurchaseSubformItemViewModel
                      ?? PurchaseItemsGrid.CurrentItem as PurchaseSubformItemViewModel;
        }

        targetItem ??= vm.Items.LastOrDefault();

        if (targetItem != null && vm.RemoveItemRowCommand.CanExecute(targetItem))
        {
            PurchaseItemsGrid?.CancelEdit();
            vm.RemoveItemRowCommand.Execute(targetItem);
        }
    }

    private void RemovePaymentGridLine(PurchaseViewModel vm)
    {
        if (vm.Payments.Count == 0) return;

        PaymentSubformItemViewModel? targetPay = null;
        var focusedElem = Keyboard.FocusedElement as DependencyObject;
        if (focusedElem != null)
        {
            var row = FindVisualParent<GridViewRow>(focusedElem);
            if (row != null && row.DataContext is PaymentSubformItemViewModel rowPay)
            {
                targetPay = rowPay;
            }
        }

        if (targetPay == null && PurchasePaymentGrid != null)
        {
            targetPay = PurchasePaymentGrid.SelectedItem as PaymentSubformItemViewModel
                     ?? PurchasePaymentGrid.CurrentItem as PaymentSubformItemViewModel;
        }

        targetPay ??= vm.Payments.LastOrDefault();

        if (targetPay != null && vm.RemovePaymentRowCommand.CanExecute(targetPay))
        {
            PurchasePaymentGrid?.CancelEdit();
            vm.RemovePaymentRowCommand.Execute(targetPay);
        }
    }

    private static T? FindVisualChild<T>(DependencyObject? parent) where T : DependencyObject
    {
        if (parent == null) return null;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typed) return typed;
            var desc = FindVisualChild<T>(child);
            if (desc != null) return desc;
        }
        return null;
    }

    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T typed) return typed;
            child = VisualTreeHelper.GetParent(child);
        }
        return null;
    }

    #endregion
}
