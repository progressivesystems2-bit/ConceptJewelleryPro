using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.WPF.Helpers;
using ConceptJewelleryPro.WPF.ViewModels;

namespace ConceptJewelleryPro.WPF.Views;

public sealed class ColumnChooserItem : INotifyPropertyChanged
{
    private bool _isVisible;
    public string Header { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public Telerik.Windows.Controls.GridViewColumn Column { get; set; } = null!;

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible != value)
            {
                _isVisible = value;
                if (Column != null)
                {
                    Column.IsVisible = value;
                }
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsVisible)));
            }
        }
    }

    public void SetInitialVisibility(bool isVisible)
    {
        _isVisible = isVisible;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class GridColumnLayoutModel
{
    public string Header { get; set; } = string.Empty;
    public double Width { get; set; }
    public bool IsStar { get; set; }
    public bool IsAuto { get; set; }
    public int DisplayIndex { get; set; }
    public bool IsVisible { get; set; } = true;
}

public sealed class InvoiceGridLayoutModel
{
    public List<GridColumnLayoutModel> BillGridColumns { get; set; } = new();
    public List<GridColumnLayoutModel> PaymentGridColumns { get; set; } = new();
}

public partial class InvoiceView : UserControl
{
    private InvoiceViewModel? _currentViewModel;

    public InvoiceView()
    {
        InitializeComponent();
        InitializeColumnChooser();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += OnIsVisibleChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        FocusAddButton();
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(LoadGridLayout));
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            FocusAddButton();
        }
    }

    private void FocusAddButton()
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            AddNewButton?.Focus();
        }));
    }

    private void Root_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (DataContext is not InvoiceViewModel vm) return;

        if (e.Key == System.Windows.Input.Key.F3)
        {
            e.Handled = true;
            OpenCustomerSearchDialog();
            return;
        }

        bool alt = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Alt) == System.Windows.Input.ModifierKeys.Alt;

        if (alt)
        {
            var key = e.SystemKey != System.Windows.Input.Key.None ? e.SystemKey : e.Key;

            if (key == System.Windows.Input.Key.A)
            {
                if (vm.IsNotEditing && vm.AddNewCommand.CanExecute(null))
                {
                    e.Handled = true;
                    vm.AddNewCommand.Execute(null);
                }
            }
            else if (key == System.Windows.Input.Key.S)
            {
                if (vm.IsEditing && vm.SaveCommand.CanExecute(null))
                {
                    e.Handled = true;
                    vm.SaveCommand.Execute(null);
                }
            }
            else if (key == System.Windows.Input.Key.M)
            {
                if (vm.IsNotEditing && vm.ModifyCommand.CanExecute(null))
                {
                    e.Handled = true;
                    vm.ModifyCommand.Execute(null);
                }
            }
            else if (key == System.Windows.Input.Key.D)
            {
                if (vm.IsNotEditing && vm.DeleteCommand.CanExecute(null))
                {
                    e.Handled = true;
                    vm.DeleteCommand.Execute(null);
                }
            }
            else if (key == System.Windows.Input.Key.C)
            {
                if (vm.IsEditing && vm.CancelCommand.CanExecute(null))
                {
                    e.Handled = true;
                    vm.CancelCommand.Execute(null);
                }
            }
            else if (key == System.Windows.Input.Key.P)
            {
                if (vm.IsNotEditing && vm.PrintCommand.CanExecute(null))
                {
                    e.Handled = true;
                    vm.PrintCommand.Execute(null);
                }
            }
            else if (key == System.Windows.Input.Key.W)
            {
                if (vm.IsNotEditing && vm.WhatsAppCommand.CanExecute(null))
                {
                    e.Handled = true;
                    vm.WhatsAppCommand.Execute(null);
                }
            }
            else if (key == System.Windows.Input.Key.E)
            {
                if (vm.IsNotEditing && vm.GenerateEInvoiceCommand.CanExecute(null))
                {
                    e.Handled = true;
                    vm.GenerateEInvoiceCommand.Execute(null);
                }
            }
            else if (key == System.Windows.Input.Key.L)
            {
                if (vm.IsNotEditing && vm.OpenListCommand.CanExecute(null))
                {
                    e.Handled = true;
                    vm.OpenListCommand.Execute(null);
                }
                else if (vm.IsEditing)
                {
                    e.Handled = true;
                    PaymentGrid.CommitEdit();
                    var selectedPay = PaymentGrid.SelectedItem as PaymentSubformItemViewModel ?? PaymentGrid.CurrentItem as PaymentSubformItemViewModel;
                    if (vm.CreateDebtorPaymentCommand.CanExecute(selectedPay))
                    {
                        vm.CreateDebtorPaymentCommand.Execute(selectedPay);
                    }
                }
            }
        }
        else if (e.Key == System.Windows.Input.Key.F10)
        {
            if (vm.IsEditing && vm.SaveCommand.CanExecute(null))
            {
                e.Handled = true;
                vm.SaveCommand.Execute(null);
            }
        }
        else
        {
            bool ctrl = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control;
            bool isMinus = e.Key == System.Windows.Input.Key.OemMinus || e.Key == System.Windows.Input.Key.Subtract;

            if (ctrl && isMinus)
            {
                if (vm.IsEditing)
                {
                    e.Handled = true;
                    RemoveCurrentGridLine();
                    return;
                }
            }
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_currentViewModel is not null)
        {
            _currentViewModel.RequestFocusInvoiceType -= OnRequestFocusInvoiceType;
            _currentViewModel.RequestFocusPaymentAmount -= OnRequestFocusPaymentAmount;
            _currentViewModel.RequestSaveLayout -= OnRequestSaveLayout;
            _currentViewModel.RequestRemoveGridLine -= OnRequestRemoveGridLine;
            _currentViewModel = null;
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_currentViewModel is not null)
        {
            _currentViewModel.RequestFocusInvoiceType -= OnRequestFocusInvoiceType;
            _currentViewModel.RequestFocusPaymentAmount -= OnRequestFocusPaymentAmount;
            _currentViewModel.RequestSaveLayout -= OnRequestSaveLayout;
            _currentViewModel.RequestRemoveGridLine -= OnRequestRemoveGridLine;
        }

        _currentViewModel = e.NewValue as InvoiceViewModel;

        if (_currentViewModel is not null)
        {
            _currentViewModel.RequestFocusInvoiceType += OnRequestFocusInvoiceType;
            _currentViewModel.RequestFocusPaymentAmount += OnRequestFocusPaymentAmount;
            _currentViewModel.RequestSaveLayout += OnRequestSaveLayout;
            _currentViewModel.RequestRemoveGridLine += OnRequestRemoveGridLine;
        }
    }

    private void OnRequestRemoveGridLine(object? sender, EventArgs e)
    {
        RemoveCurrentGridLine();
    }

    private void OnRequestSaveLayout(object? sender, EventArgs e)
    {
        SaveGridLayout();
    }

    private void OnRequestFocusPaymentAmount(object? sender, PaymentSubformItemViewModel pay)
    {
        FocusPaymentAmountForRow(pay);
    }

    private void OnRequestFocusInvoiceType(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            InvoiceTypeComboBox.Focus();
        }));
    }

    private void InvoiceTypeComboBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is InvoiceViewModel vm && sender is Telerik.Windows.Controls.RadComboBox combo)
        {
            var text = !string.IsNullOrWhiteSpace(combo.Text) ? combo.Text : combo.SelectedItem?.ToString();
            if (!string.IsNullOrWhiteSpace(text) && !string.Equals(vm.VouType, text, StringComparison.OrdinalIgnoreCase))
            {
                vm.VouType = text;
            }
        }
    }

    private void InvoiceTypeComboBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key is System.Windows.Input.Key.Enter or System.Windows.Input.Key.Tab)
        {
            if (DataContext is InvoiceViewModel vm && sender is Telerik.Windows.Controls.RadComboBox combo)
            {
                var text = !string.IsNullOrWhiteSpace(combo.Text) ? combo.Text : combo.SelectedItem?.ToString();
                if (!string.IsNullOrWhiteSpace(text) && !string.Equals(vm.VouType, text, StringComparison.OrdinalIgnoreCase))
                {
                    vm.VouType = text;
                }
            }
        }
    }

    private void ItemsGrid_BeginningEdit(object? sender, Telerik.Windows.Controls.GridViewBeginningEditRoutedEventArgs e)
    {
        if (DataContext is InvoiceViewModel vm && !vm.IsEditing)
        {
            e.Cancel = true;
            return;
        }

        var colHeader = e.Cell?.Column?.Header?.ToString();
        if (string.Equals(colHeader, "ItemName", StringComparison.OrdinalIgnoreCase))
        {
            var item = (e.Cell?.DataContext as BillSubformItemViewModel)
                    ?? (ItemsGrid?.SelectedItem as BillSubformItemViewModel);
            if (item != null && !string.IsNullOrWhiteSpace(item.Barcode))
            {
                e.Cancel = true;
                return;
            }
        }

        // Approach 1: Block Rate, MetalValue, MPerGr, Making for PC-rate items
        if (string.Equals(colHeader, "Rate", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(colHeader, "MetalValue", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(colHeader, "MPerGr", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(colHeader, "Making", StringComparison.OrdinalIgnoreCase))
        {
            var item = (e.Cell?.DataContext as BillSubformItemViewModel)
                    ?? (ItemsGrid?.SelectedItem as BillSubformItemViewModel);
            if (item != null && item.PcRate)
            {
                e.Cancel = true;
                return;
            }
        }
    }

    private void ItemsGrid_CellEditEnded(object? sender, Telerik.Windows.Controls.GridViewCellEditEndedEventArgs e)
    {
        if (e.EditAction == Telerik.Windows.Controls.GridView.GridViewEditAction.Commit)
        {
            (DataContext as InvoiceViewModel)?.RecalculateTotals();
        }
    }

    private void ItemsGrid_PreparingCellForEdit(object? sender, Telerik.Windows.Controls.GridViewPreparingCellForEditEventArgs e)
    {
        var colHeader = e.Column?.Header?.ToString();
        var item = (e.Row?.DataContext as BillSubformItemViewModel)
                ?? (ItemsGrid?.SelectedItem as BillSubformItemViewModel);

        if (string.Equals(colHeader, "Barcode", StringComparison.OrdinalIgnoreCase))
        {
            _barcodeUserIsTyping = false;
            var combo = (e.EditingElement as Telerik.Windows.Controls.RadComboBox)
                     ?? FindVisualChild<Telerik.Windows.Controls.RadComboBox>(e.EditingElement);
            if (combo != null)
            {
                combo.IsDropDownOpen = false;
                if (item != null && string.IsNullOrWhiteSpace(item.Barcode))
                {
                    ClearBarcodeComboSelection(combo);
                }
            }
            return;
        }

        if (string.Equals(colHeader, "ItemName", StringComparison.OrdinalIgnoreCase))
        {
            _itemNameUserIsTyping = false;
            var combo = (e.EditingElement as Telerik.Windows.Controls.RadComboBox)
                     ?? FindVisualChild<Telerik.Windows.Controls.RadComboBox>(e.EditingElement);
            if (combo != null)
            {
                if (item != null && string.IsNullOrWhiteSpace(item.ItemName))
                {
                    ClearItemNameComboSelection(combo);
                }
                else
                {
                    Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
                    {
                        var tb = (combo.Template?.FindName("PART_EditableTextBox", combo) as TextBox)
                                 ?? FindVisualChild<TextBox>(combo);
                        tb?.SelectAll();
                    }));
                }
            }
            return;
        }
    }

    private static string GetComboBoxEditorText(Telerik.Windows.Controls.RadComboBox combo, DependencyObject? focusedElem = null)
    {
        if (focusedElem != null)
        {
            var tb = (focusedElem as TextBox) ?? FindVisualParent<TextBox>(focusedElem);
            if (tb != null) return tb.Text;
            var rtb = (focusedElem as Telerik.Windows.Controls.RadWatermarkTextBox) ?? FindVisualParent<Telerik.Windows.Controls.RadWatermarkTextBox>(focusedElem);
            if (rtb != null) return rtb.Text;
        }

        var innerBox = (combo.Template?.FindName("PART_EditableTextBox", combo) as TextBox)
                    ?? FindVisualChild<TextBox>(combo)
                    ?? (FrameworkElement?)FindVisualChild<Telerik.Windows.Controls.RadWatermarkTextBox>(combo);
        if (innerBox is TextBox innerTb)
        {
            return innerTb.Text;
        }
        if (innerBox is Telerik.Windows.Controls.RadWatermarkTextBox innerRtb)
        {
            return innerRtb.Text;
        }

        return combo.Text ?? string.Empty;
    }

    private static string GetActiveComboBoxText(DependencyObject? focusedElem, Telerik.Windows.Controls.RadComboBox? combo)
    {
        if (focusedElem != null)
        {
            var tb = (focusedElem as TextBox) ?? FindVisualParent<TextBox>(focusedElem);
            if (tb != null)
            {
                return tb.Text.Trim();
            }

            var rtb = (focusedElem as Telerik.Windows.Controls.RadWatermarkTextBox)
                   ?? FindVisualParent<Telerik.Windows.Controls.RadWatermarkTextBox>(focusedElem);
            if (rtb != null)
            {
                return rtb.Text.Trim();
            }
        }

        if (combo != null)
        {
            var editorText = GetComboBoxEditorText(combo);
            if (!string.IsNullOrWhiteSpace(editorText))
            {
                return editorText.Trim();
            }

            var innerBox = (combo.Template?.FindName("PART_EditableTextBox", combo) as TextBox)
                        ?? FindVisualChild<TextBox>(combo)
                        ?? (FrameworkElement?)FindVisualChild<Telerik.Windows.Controls.RadWatermarkTextBox>(combo);
            if (innerBox != null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(combo.Text))
            {
                return combo.Text.Trim();
            }

            if (combo.SelectedItem is TempStockItem stockItem && !string.IsNullOrWhiteSpace(stockItem.Barcode))
            {
                return stockItem.Barcode.Trim();
            }

            if (combo.SelectedItem is string str && !string.IsNullOrWhiteSpace(str))
            {
                return str.Trim();
            }
        }

        return string.Empty;
    }

    private static string ResolveChosenBarcode(Telerik.Windows.Controls.RadComboBox combo, DependencyObject? focusedElem)
    {
        var typedText = GetComboBoxEditorText(combo, focusedElem)?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(typedText))
        {
            return string.Empty;
        }

        if (combo.SelectedItem is TempStockItem stockItem && !string.IsNullOrWhiteSpace(stockItem.Barcode))
        {
            if (stockItem.Barcode.Trim().StartsWith(typedText, StringComparison.OrdinalIgnoreCase))
            {
                return stockItem.Barcode.Trim();
            }
        }
        if (combo.SelectedItem is string selectedStr && !string.IsNullOrWhiteSpace(selectedStr))
        {
            if (selectedStr.Trim().StartsWith(typedText, StringComparison.OrdinalIgnoreCase))
            {
                return selectedStr.Trim();
            }
        }

        // Match from beginning digits / characters (StartsWith)
        if (combo.ItemsSource is IEnumerable<TempStockItem> stockItems)
        {
            var match = stockItems.FirstOrDefault(s => s.Barcode.Trim().StartsWith(typedText, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                combo.SelectedItem = match;
                return match.Barcode.Trim();
            }
        }
        else if (combo.Items.Count > 0)
        {
            foreach (var it in combo.Items)
            {
                string? b = it is TempStockItem si ? si.Barcode : it?.ToString();
                if (!string.IsNullOrWhiteSpace(b) && b.Trim().StartsWith(typedText, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedItem = it;
                    return b.Trim();
                }
            }
        }

        return typedText;
    }

    // Track whether user has actively typed in the barcode combo to prevent Telerik auto-selection
    private bool _barcodeUserIsTyping = false;
    private bool _isClearingBarcodeSelection = false;

    private void ClearBarcodeComboSelection(Telerik.Windows.Controls.RadComboBox combo)
    {
        _barcodeUserIsTyping = false;
        try
        {
            _isClearingBarcodeSelection = true;
            combo.SelectedItem = null;
            combo.SelectedIndex = -1;
            combo.Text = string.Empty;

            var innerBox = (combo.Template?.FindName("PART_EditableTextBox", combo) as TextBox)
                        ?? FindVisualChild<TextBox>(combo)
                        ?? (FrameworkElement?)FindVisualChild<Telerik.Windows.Controls.RadWatermarkTextBox>(combo);
            if (innerBox is TextBox tb)
            {
                tb.Text = string.Empty;
            }
            else if (innerBox is Telerik.Windows.Controls.RadWatermarkTextBox rtb)
            {
                rtb.Text = string.Empty;
            }
        }
        finally
        {
            _isClearingBarcodeSelection = false;
        }

        // Deferred clear: Telerik may re-apply selection after Loaded/GotFocus
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
        {
            if (combo.DataContext is BillSubformItemViewModel item && string.IsNullOrWhiteSpace(item.Barcode) && !_barcodeUserIsTyping)
            {
                try
                {
                    _isClearingBarcodeSelection = true;
                    combo.SelectedItem = null;
                    combo.SelectedIndex = -1;
                    combo.Text = string.Empty;

                    var innerBox = (combo.Template?.FindName("PART_EditableTextBox", combo) as TextBox)
                                ?? FindVisualChild<TextBox>(combo)
                                ?? (FrameworkElement?)FindVisualChild<Telerik.Windows.Controls.RadWatermarkTextBox>(combo);
                    if (innerBox is TextBox tb)
                    {
                        tb.Text = string.Empty;
                    }
                    else if (innerBox is Telerik.Windows.Controls.RadWatermarkTextBox rtb)
                    {
                        rtb.Text = string.Empty;
                    }
                }
                finally
                {
                    _isClearingBarcodeSelection = false;
                }
            }
        }));
    }

    private void BarcodeComboBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is Telerik.Windows.Controls.RadComboBox combo)
        {
            App.ApplyComboBoxDropDownChevron(combo);

            if (combo.FilteringBehavior is not ConceptJewelleryPro.WPF.Helpers.BarcodeStartsWithFilteringBehavior)
            {
                combo.FilteringBehavior = new ConceptJewelleryPro.WPF.Helpers.BarcodeStartsWithFilteringBehavior();
            }

            if (combo.DataContext is BillSubformItemViewModel item && string.IsNullOrWhiteSpace(item.Barcode))
            {
                ClearBarcodeComboSelection(combo);
            }
        }
    }

    private void BarcodeComboBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is Telerik.Windows.Controls.RadComboBox combo)
        {
            combo.IsDropDownOpen = false;
            if (combo.DataContext is BillSubformItemViewModel item && string.IsNullOrWhiteSpace(item.Barcode))
            {
                ClearBarcodeComboSelection(combo);
            }
        }
    }

    private void BarcodeComboBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Text))
        {
            _barcodeUserIsTyping = true;
        }
    }

    private void BarcodeComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_isClearingBarcodeSelection) return;

        if (sender is Telerik.Windows.Controls.RadComboBox combo)
        {
            var editorText = GetComboBoxEditorText(combo);
            if (string.IsNullOrWhiteSpace(editorText))
            {
                if (combo.DataContext is BillSubformItemViewModel item)
                {
                    item.Barcode = string.Empty;
                }
                ClearBarcodeComboSelection(combo);
                return;
            }

            // If user hasn't typed anything yet, immediately reject the auto-selection
            if (!_barcodeUserIsTyping
                && combo.DataContext is BillSubformItemViewModel it
                && string.IsNullOrWhiteSpace(it.Barcode))
            {
                ClearBarcodeComboSelection(combo);
            }
        }
    }

    private void BarcodeComboBox_DropDownOpened(object? sender, EventArgs e)
    {
        if (sender is Telerik.Windows.Controls.RadComboBox combo)
        {
            ResetComboBoxScrollToTop(combo);
        }
    }

    private void BarcodeComboBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        bool isShift = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) == System.Windows.Input.ModifierKeys.Shift;
        if (isShift && e.Key == System.Windows.Input.Key.Tab) return;

        if (sender is Telerik.Windows.Controls.RadComboBox combo && combo.DataContext is BillSubformItemViewModel item)
        {
            // Mark that user has started typing — allows SelectionChanged to accept user-driven selections
            if (e.Key >= System.Windows.Input.Key.D0 && e.Key <= System.Windows.Input.Key.D9
                || e.Key >= System.Windows.Input.Key.NumPad0 && e.Key <= System.Windows.Input.Key.NumPad9
                || e.Key >= System.Windows.Input.Key.A && e.Key <= System.Windows.Input.Key.Z
                || e.Key == System.Windows.Input.Key.Back || e.Key == System.Windows.Input.Key.Delete)
            {
                _barcodeUserIsTyping = true;
            }

            if (e.Key == System.Windows.Input.Key.Back || e.Key == System.Windows.Input.Key.Delete)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
                {
                    var currentText = GetComboBoxEditorText(combo);
                    if (string.IsNullOrWhiteSpace(currentText))
                    {
                        item.Barcode = string.Empty;
                        try
                        {
                            _isClearingBarcodeSelection = true;
                            combo.SelectedItem = null;
                            combo.SelectedIndex = -1;
                            combo.Text = string.Empty;
                        }
                        finally
                        {
                            _isClearingBarcodeSelection = false;
                        }
                    }
                }));
            }

            if (e.Key is System.Windows.Input.Key.Enter or System.Windows.Input.Key.Tab)
            {
                combo.IsDropDownOpen = false;

                var activeText = GetComboBoxEditorText(combo, e.OriginalSource as DependencyObject);
                if (string.IsNullOrWhiteSpace(activeText))
                {
                    item.Barcode = string.Empty;
                    ClearBarcodeComboSelection(combo);
                    e.Handled = true;
                    if (DataContext is InvoiceViewModel vmNav)
                    {
                        var rowIndex = vmNav.Items.IndexOf(item);
                        if (rowIndex >= 0)
                        {
                            var itemNameCol = FindColumn("ItemName");
                            if (itemNameCol != null)
                            {
                                Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
                                {
                                    FocusCell(rowIndex, itemNameCol);
                                }));
                            }
                            else
                            {
                                var barcodeCol = FindColumn("Barcode");
                                Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
                                {
                                    AdvanceToNextColumn(rowIndex, barcodeCol, item);
                                }));
                            }
                        }
                    }
                    return;
                }

                var text = ResolveChosenBarcode(combo, e.OriginalSource as DependencyObject);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    combo.Text = text;
                    if (DataContext is InvoiceViewModel vmModel)
                    {
                        _ = vmModel.LookupBarcodeAsync(item, text);
                    }
                }
                else
                {
                    combo.Text = activeText.Trim();
                    item.Barcode = activeText.Trim();
                }

                e.Handled = true;
                if (DataContext is InvoiceViewModel vm)
                {
                    var rowIndex = vm.Items.IndexOf(item);
                    if (rowIndex >= 0)
                    {
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            // After barcode scan, directly go to GrossAmt
                            var grossAmtCol = FindColumn("Gross Amt") ?? FindColumn("GrossAmt");
                            if (grossAmtCol != null)
                            {
                                Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
                                {
                                    FocusCell(rowIndex, grossAmtCol);
                                }));
                                return;
                            }

                            var barcodeCol = FindColumn("Barcode");
                            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
                            {
                                AdvanceToNextColumn(rowIndex, barcodeCol, item);
                            }));
                        }
                        else
                        {
                            // If barcode is not scanned go on ItemName and then continue normally control by control
                            var itemNameCol = FindColumn("ItemName");
                            if (itemNameCol != null)
                            {
                                Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
                                {
                                    FocusCell(rowIndex, itemNameCol);
                                }));
                            }
                            else
                            {
                                var barcodeCol = FindColumn("Barcode");
                                Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
                                {
                                    AdvanceToNextColumn(rowIndex, barcodeCol, item);
                                }));
                            }
                        }
                    }
                }
            }
        }
    }

    private void BarcodeComboBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        // Key down is handled cleanly via BarcodeComboBox_PreviewKeyDown to prevent duplicate execution
    }

    private void BarcodeComboBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is Telerik.Windows.Controls.RadComboBox combo && combo.DataContext is BillSubformItemViewModel item)
        {
            var activeText = GetComboBoxEditorText(combo, e.OriginalSource as DependencyObject);
            if (string.IsNullOrWhiteSpace(activeText))
            {
                item.Barcode = string.Empty;
                ClearBarcodeComboSelection(combo);
                return;
            }

            var text = ResolveChosenBarcode(combo, e.OriginalSource as DependencyObject);
            if (!string.IsNullOrWhiteSpace(text))
            {
                combo.Text = text;
                if (DataContext is InvoiceViewModel vm && (string.IsNullOrWhiteSpace(item.ItemName) || !string.Equals(item.Barcode, text, StringComparison.OrdinalIgnoreCase)))
                {
                    _ = vm.LookupBarcodeAsync(item, text);
                }
            }
            else
            {
                combo.Text = activeText.Trim();
                item.Barcode = activeText.Trim();
            }
        }
    }

    private void BarcodeComboBox_DropDownClosed(object? sender, EventArgs e)
    {
        if (sender is Telerik.Windows.Controls.RadComboBox combo && combo.DataContext is BillSubformItemViewModel item)
        {
            var activeText = GetComboBoxEditorText(combo);
            if (string.IsNullOrWhiteSpace(activeText))
            {
                item.Barcode = string.Empty;
                ClearBarcodeComboSelection(combo);
                return;
            }

            var chosen = ResolveChosenBarcode(combo, null);

            if (!string.IsNullOrWhiteSpace(chosen))
            {
                combo.Text = chosen;
                if (DataContext is InvoiceViewModel vm && (string.IsNullOrWhiteSpace(item.ItemName) || !string.Equals(item.Barcode, chosen, StringComparison.OrdinalIgnoreCase)))
                {
                    _ = vm.LookupBarcodeAsync(item, chosen);
                }
            }
            else
            {
                combo.Text = activeText.Trim();
                item.Barcode = activeText.Trim();
            }
        }
    }

    // Track whether user has actively typed in the item name combo to prevent Telerik auto-selection
    private bool _itemNameUserIsTyping = false;
    private bool _isClearingItemNameSelection = false;

    private void ClearItemNameComboSelection(Telerik.Windows.Controls.RadComboBox combo)
    {
        if (_itemNameUserIsTyping) return;

        try
        {
            _isClearingItemNameSelection = true;
            combo.SelectedItem = null;
            combo.SelectedIndex = -1;
            combo.Text = string.Empty;

            var innerBox = (combo.Template?.FindName("PART_EditableTextBox", combo) as TextBox)
                        ?? FindVisualChild<TextBox>(combo)
                        ?? (FrameworkElement?)FindVisualChild<Telerik.Windows.Controls.RadWatermarkTextBox>(combo);
            if (innerBox is TextBox tb)
            {
                tb.Text = string.Empty;
            }
            else if (innerBox is Telerik.Windows.Controls.RadWatermarkTextBox rtb)
            {
                rtb.Text = string.Empty;
            }
        }
        finally
        {
            _isClearingItemNameSelection = false;
        }

        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
        {
            if (!_itemNameUserIsTyping
                && combo.DataContext is BillSubformItemViewModel item
                && string.IsNullOrWhiteSpace(item.ItemName)
                && string.IsNullOrWhiteSpace(combo.Text))
            {
                try
                {
                    _isClearingItemNameSelection = true;
                    combo.SelectedItem = null;
                    combo.SelectedIndex = -1;
                    combo.Text = string.Empty;

                    var innerBox = (combo.Template?.FindName("PART_EditableTextBox", combo) as TextBox)
                                ?? FindVisualChild<TextBox>(combo)
                                ?? (FrameworkElement?)FindVisualChild<Telerik.Windows.Controls.RadWatermarkTextBox>(combo);
                    if (innerBox is TextBox tb)
                    {
                        tb.Text = string.Empty;
                    }
                    else if (innerBox is Telerik.Windows.Controls.RadWatermarkTextBox rtb)
                    {
                        rtb.Text = string.Empty;
                    }
                }
                finally
                {
                    _isClearingItemNameSelection = false;
                }
            }
        }));
    }

    private void ItemNameComboBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is Telerik.Windows.Controls.RadComboBox combo)
        {
            App.ApplyComboBoxDropDownChevron(combo);

            if (combo.FilteringBehavior is not ConceptJewelleryPro.WPF.Helpers.PrefixPriorityFilteringBehavior)
            {
                combo.FilteringBehavior = new ConceptJewelleryPro.WPF.Helpers.PrefixPriorityFilteringBehavior();
            }

            if (combo.DataContext is BillSubformItemViewModel item && string.IsNullOrWhiteSpace(item.ItemName))
            {
                ClearItemNameComboSelection(combo);
            }
        }
    }

    private void ItemNameComboBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is Telerik.Windows.Controls.RadComboBox combo)
        {
            _itemNameUserIsTyping = false;
            if (combo.DataContext is BillSubformItemViewModel item && string.IsNullOrWhiteSpace(item.ItemName))
            {
                ClearItemNameComboSelection(combo);
            }
        }
    }

    private void ItemNameComboBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Text))
        {
            _itemNameUserIsTyping = true;
        }
    }

    private void ItemNameComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_isClearingItemNameSelection) return;

        if (sender is Telerik.Windows.Controls.RadComboBox combo && combo.DataContext is BillSubformItemViewModel item)
        {
            // If user hasn't typed anything yet and the item name is empty, reject spontaneous background auto-selection (e.g. selecting "Aarti" on focus)
            if (!_itemNameUserIsTyping && string.IsNullOrWhiteSpace(item.ItemName))
            {
                ClearItemNameComboSelection(combo);
                return;
            }

            if (combo.SelectedItem is string selected && !string.IsNullOrWhiteSpace(selected))
            {
                item.ItemName = selected;
                combo.Text = selected;
                return;
            }

            var editorText = GetComboBoxEditorText(combo);
            if (string.IsNullOrWhiteSpace(editorText))
            {
                item.ItemName = string.Empty;
                ClearItemNameComboSelection(combo);
                return;
            }

            if (!combo.IsDropDownOpen && !_itemNameUserIsTyping && string.IsNullOrWhiteSpace(item.ItemName))
            {
                ClearItemNameComboSelection(combo);
            }
        }
    }

    private void ItemNameComboBox_DropDownOpened(object? sender, EventArgs e)
    {
        if (sender is Telerik.Windows.Controls.RadComboBox combo)
        {
            var text = GetComboBoxEditorText(combo)?.Trim();
            if (string.IsNullOrEmpty(text) || !_itemNameUserIsTyping)
            {
                ResetComboBoxScrollToTop(combo);

                if (combo.ItemsSource != null)
                {
                    var view = CollectionViewSource.GetDefaultView(combo.ItemsSource);
                    if (view != null && view.Filter != null)
                    {
                        view.Filter = null;
                    }
                }
            }
        }
    }

    private static void ResetComboBoxScrollToTop(Telerik.Windows.Controls.RadComboBox combo)
    {
        combo.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            var scrollViewer = FindVisualChild<ScrollViewer>(combo);
            scrollViewer?.ScrollToTop();
        }));
    }

    private static string ResolveChosenItemName(Telerik.Windows.Controls.RadComboBox combo, DependencyObject? focusedElem)
    {
        var typedText = GetComboBoxEditorText(combo, focusedElem)?.Trim() ?? string.Empty;

        // 0. If user navigated to or highlighted an item in the open dropdown (e.g. using Down Arrow to "Toe Ring 925")
        if (combo.IsDropDownOpen)
        {
            var highlighted = ComboBoxNavigationHelper.GetRadHighlightedItem(combo);
            if (highlighted is string highStr && !string.IsNullOrWhiteSpace(highStr))
            {
                if (string.IsNullOrWhiteSpace(typedText) || highStr.IndexOf(typedText, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return highStr.Trim();
                }
            }
            else if (highlighted != null)
            {
                var s = highlighted.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(s) && (string.IsNullOrWhiteSpace(typedText) || s.IndexOf(typedText, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    return s;
                }
            }

            var navigated = ComboBoxNavigationHelper.GetNavigatedItem(combo);
            if (navigated is string navStr && !string.IsNullOrWhiteSpace(navStr))
            {
                if (string.IsNullOrWhiteSpace(typedText) || navStr.IndexOf(typedText, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return navStr.Trim();
                }
            }
        }

        // 1. Explicit user selection (if matching typedText, or user arrowed to it)
        if (combo.SelectedItem is string selectedStr && !string.IsNullOrWhiteSpace(selectedStr))
        {
            if (string.IsNullOrWhiteSpace(typedText) || selectedStr.IndexOf(typedText, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return selectedStr.Trim();
            }
        }

        if (string.IsNullOrWhiteSpace(typedText))
        {
            return string.Empty;
        }

        // 2. Exact match check against items source or items
        if (combo.ItemsSource is IEnumerable<string> stringItems)
        {
            var exact = stringItems.FirstOrDefault(s => string.Equals(s?.Trim(), typedText, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact.Trim();
        }
        else if (combo.Items.Count > 0)
        {
            var exact = combo.Items.OfType<object>()
                .Select(o => (o as string) ?? o?.ToString()?.Trim())
                .FirstOrDefault(s => string.Equals(s, typedText, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact;
        }

        // 3. Prefix match
        if (combo.ItemsSource is IEnumerable<string> items)
        {
            var prefix = items.FirstOrDefault(s => s?.Trim().StartsWith(typedText, StringComparison.OrdinalIgnoreCase) == true);
            if (prefix != null) return prefix.Trim();

            var contains = items.FirstOrDefault(s => s?.Trim().IndexOf(typedText, StringComparison.OrdinalIgnoreCase) >= 0);
            if (contains != null) return contains.Trim();
        }
        else if (combo.Items.Count > 0)
        {
            var list = combo.Items.OfType<object>().Select(o => ((o as string) ?? o?.ToString())?.Trim()).Where(s => !string.IsNullOrEmpty(s)).OfType<string>().ToList();

            var prefix = list.FirstOrDefault(s => s.StartsWith(typedText, StringComparison.OrdinalIgnoreCase));
            if (prefix != null) return prefix;

            var contains = list.FirstOrDefault(s => s.IndexOf(typedText, StringComparison.OrdinalIgnoreCase) >= 0);
            if (contains != null) return contains;
        }

        // 4. Multi-word match (e.g. "925 ring" or "ring 925")
        var words = typedText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 1)
        {
            if (combo.ItemsSource is IEnumerable<string> stringItemsList)
            {
                var match = stringItemsList.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s) && words.All(w => s.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0));
                if (match != null) return match.Trim();
            }
            else if (combo.Items.Count > 0)
            {
                var list = combo.Items.OfType<object>().Select(o => ((o as string) ?? o?.ToString())?.Trim()).Where(s => !string.IsNullOrEmpty(s)).OfType<string>().ToList();
                var match = list.FirstOrDefault(s => words.All(w => s.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0));
                if (match != null) return match;
            }
        }

        return typedText;
    }

    private void ItemNameComboBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (sender is Telerik.Windows.Controls.RadComboBox combo && combo.DataContext is BillSubformItemViewModel item)
        {
            if (!string.IsNullOrWhiteSpace(item.Barcode))
            {
                if (e.Key is not (System.Windows.Input.Key.Enter or System.Windows.Input.Key.Tab or System.Windows.Input.Key.Left or System.Windows.Input.Key.Right or System.Windows.Input.Key.Up or System.Windows.Input.Key.Down or System.Windows.Input.Key.Escape))
                {
                    e.Handled = true;
                    return;
                }

                if (e.Key is System.Windows.Input.Key.Enter or System.Windows.Input.Key.Tab)
                {
                    combo.IsDropDownOpen = false;
                    if (e.Key == System.Windows.Input.Key.Enter)
                    {
                        e.Handled = true;
                        if (DataContext is InvoiceViewModel vm)
                        {
                            var rowIndex = vm.Items.IndexOf(item);
                            if (rowIndex >= 0)
                            {
                                var itemNameCol = ItemsGrid.Columns.OfType<Telerik.Windows.Controls.GridViewColumn>().FirstOrDefault(c => string.Equals(c.Header?.ToString(), "ItemName", StringComparison.OrdinalIgnoreCase));
                                Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
                                {
                                    AdvanceToNextColumn(rowIndex, itemNameCol, item);
                                }));
                            }
                        }
                    }
                    return;
                }
            }

            // Mark that user has started typing or navigating — allows SelectionChanged to accept user-driven selections
            if (e.Key >= System.Windows.Input.Key.A && e.Key <= System.Windows.Input.Key.Z
                || e.Key >= System.Windows.Input.Key.D0 && e.Key <= System.Windows.Input.Key.D9
                || e.Key >= System.Windows.Input.Key.NumPad0 && e.Key <= System.Windows.Input.Key.NumPad9
                || e.Key == System.Windows.Input.Key.Back || e.Key == System.Windows.Input.Key.Delete
                || e.Key == System.Windows.Input.Key.Space
                || e.Key == System.Windows.Input.Key.Down || e.Key == System.Windows.Input.Key.Up)
            {
                _itemNameUserIsTyping = true;
            }

            if (e.Key == System.Windows.Input.Key.Back || e.Key == System.Windows.Input.Key.Delete)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
                {
                    var currentText = GetComboBoxEditorText(combo);
                    if (string.IsNullOrWhiteSpace(currentText))
                    {
                        item.ItemName = string.Empty;
                        try
                        {
                            _isClearingItemNameSelection = true;
                            combo.SelectedItem = null;
                            combo.SelectedIndex = -1;
                            combo.Text = string.Empty;
                        }
                        finally
                        {
                            _isClearingItemNameSelection = false;
                        }
                    }
                }));
            }

            if (e.Key is System.Windows.Input.Key.Enter or System.Windows.Input.Key.Tab)
            {
                var activeText = GetComboBoxEditorText(combo, e.OriginalSource as DependencyObject);
                if (string.IsNullOrWhiteSpace(activeText))
                {
                    combo.IsDropDownOpen = false;
                    item.ItemName = string.Empty;
                    ClearItemNameComboSelection(combo);
                    e.Handled = true;
                    if (DataContext is InvoiceViewModel vm)
                    {
                        var rowIndex = vm.Items.IndexOf(item);
                        if (rowIndex >= 0)
                        {
                            var itemNameCol = FindColumn("ItemName");
                            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
                            {
                                AdvanceToNextColumn(rowIndex, itemNameCol, item);
                            }));
                        }
                    }
                    return;
                }

                // 1. Resolve chosen item FIRST while dropdown is open so highlighted item is captured!
                var text = ResolveChosenItemName(combo, e.OriginalSource as DependencyObject);

                // 2. Close dropdown
                combo.IsDropDownOpen = false;

                if (!string.IsNullOrWhiteSpace(text))
                {
                    item.ItemName = text;
                    combo.SelectedItem = text;
                    combo.Text = text;
                }
                else
                {
                    item.ItemName = activeText.Trim();
                    combo.Text = activeText.Trim();
                }

                e.Handled = true;
                if (DataContext is InvoiceViewModel vmModel)
                {
                    var rowIndex = vmModel.Items.IndexOf(item);
                    if (rowIndex >= 0)
                    {
                        var itemNameCol = FindColumn("ItemName");
                        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
                        {
                            AdvanceToNextColumn(rowIndex, itemNameCol, item);
                        }));
                    }
                }
            }
        }
    }

    private void ItemNameComboBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (sender is Telerik.Windows.Controls.RadComboBox combo)
        {
            if (e.Key is System.Windows.Input.Key.Enter or System.Windows.Input.Key.Tab or System.Windows.Input.Key.Escape)
            {
                return;
            }

            if (!combo.IsDropDownOpen && e.Key != System.Windows.Input.Key.Back && e.Key != System.Windows.Input.Key.Delete)
            {
                combo.IsDropDownOpen = true;
            }
        }
    }

    private void ItemNameComboBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is Telerik.Windows.Controls.RadComboBox combo && combo.DataContext is BillSubformItemViewModel item)
        {
            if (!string.IsNullOrWhiteSpace(item.Barcode))
            {
                if (!string.IsNullOrWhiteSpace(item.ItemName))
                {
                    combo.SelectedItem = item.ItemName;
                    combo.Text = item.ItemName;
                }
                return;
            }

            var activeText = GetComboBoxEditorText(combo, e.OriginalSource as DependencyObject);
            if (string.IsNullOrWhiteSpace(activeText))
            {
                item.ItemName = string.Empty;
                ClearItemNameComboSelection(combo);
                return;
            }

            var text = ResolveChosenItemName(combo, e.OriginalSource as DependencyObject);
            if (!string.IsNullOrWhiteSpace(text))
            {
                item.ItemName = text;
                combo.SelectedItem = text;
                combo.Text = text;
            }
            else
            {
                item.ItemName = activeText.Trim();
                combo.Text = activeText.Trim();
            }
        }
    }

    private void ItemNameComboBox_DropDownClosed(object? sender, EventArgs e)
    {
        if (sender is Telerik.Windows.Controls.RadComboBox combo && combo.DataContext is BillSubformItemViewModel item)
        {
            if (!string.IsNullOrWhiteSpace(item.Barcode))
            {
                if (!string.IsNullOrWhiteSpace(item.ItemName))
                {
                    combo.Text = item.ItemName;
                }
                return;
            }

            if (combo.SelectedItem is string selectedStr && !string.IsNullOrWhiteSpace(selectedStr))
            {
                combo.Text = selectedStr;
                item.ItemName = selectedStr;
                return;
            }

            var activeText = GetComboBoxEditorText(combo);
            if (string.IsNullOrWhiteSpace(activeText))
            {
                item.ItemName = string.Empty;
                ClearItemNameComboSelection(combo);
                return;
            }

            var resolved = ResolveChosenItemName(combo, null);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                combo.Text = resolved;
                item.ItemName = resolved;
            }
            else
            {
                combo.Text = activeText.Trim();
                item.ItemName = activeText.Trim();
            }
        }
    }

    private void PaymentGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DataContext is InvoiceViewModel vm && vm.IsEditing && PaymentGrid.SelectedItem is PaymentSubformItemViewModel pay)
        {
            vm.OpenPaymentDetails(pay);
        }
    }

    private void PaymentGrid_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        bool alt = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Alt) == System.Windows.Input.ModifierKeys.Alt;
        var key = e.SystemKey != System.Windows.Input.Key.None ? e.SystemKey : e.Key;
        if (alt && key == System.Windows.Input.Key.L)
        {
            e.Handled = true;
            if (DataContext is InvoiceViewModel vm && vm.IsEditing)
            {
                PaymentGrid.CommitEdit();
                var pay = PaymentGrid.SelectedItem as PaymentSubformItemViewModel ?? PaymentGrid.CurrentItem as PaymentSubformItemViewModel;
                if (vm.CreateDebtorPaymentCommand.CanExecute(pay))
                {
                    vm.CreateDebtorPaymentCommand.Execute(pay);
                }
            }
        }
    }

    private void PaymentAccountComboBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (sender is Telerik.Windows.Controls.RadComboBox combo)
        {
            bool alt = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Alt) == System.Windows.Input.ModifierKeys.Alt;
            var key = e.SystemKey != System.Windows.Input.Key.None ? e.SystemKey : e.Key;
            if (alt && key == System.Windows.Input.Key.L)
            {
                e.Handled = true;
                combo.IsDropDownOpen = false;
                if (DataContext is InvoiceViewModel vm && vm.IsEditing)
                {
                    PaymentGrid.CommitEdit();
                    var pay = combo.DataContext as PaymentSubformItemViewModel ?? PaymentGrid.SelectedItem as PaymentSubformItemViewModel;
                    if (vm.CreateDebtorPaymentCommand.CanExecute(pay))
                    {
                        vm.CreateDebtorPaymentCommand.Execute(pay);
                    }
                }
                return;
            }

            if (e.Key is System.Windows.Input.Key.Enter or System.Windows.Input.Key.Tab)
            {
                e.Handled = true;
                combo.IsDropDownOpen = false;

                var text = !string.IsNullOrWhiteSpace(combo.Text) ? combo.Text : combo.SelectedItem?.ToString();
                if (combo.DataContext is PaymentSubformItemViewModel currentPay)
                {
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        currentPay.AccountName = text;
                    }
                    else if (string.IsNullOrWhiteSpace(currentPay.AccountName) && currentPay.Amount <= 0)
                    {
                        // Pressed Enter on empty payment row -> cleanup and advance straight to Save
                        if (DataContext is InvoiceViewModel vm)
                        {
                            if (vm.Payments.Count > 1)
                            {
                                vm.Payments.Remove(currentPay);
                            }
                            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(FocusSaveButton));
                            return;
                        }
                    }
                }

                Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
                {
                    FocusPaymentAmountForRow(combo.DataContext as PaymentSubformItemViewModel);
                }));
                return;
            }

            if (!combo.IsDropDownOpen && e.Key != System.Windows.Input.Key.Escape)
            {
                combo.IsDropDownOpen = true;
            }
        }
    }

    private void PaymentAmountTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        bool alt = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Alt) == System.Windows.Input.ModifierKeys.Alt;
        var key = e.SystemKey != System.Windows.Input.Key.None ? e.SystemKey : e.Key;
        if (alt && key == System.Windows.Input.Key.L)
        {
            e.Handled = true;
            if (DataContext is InvoiceViewModel vm && vm.IsEditing)
            {
                PaymentGrid.CommitEdit();
                var pay = (sender as FrameworkElement)?.DataContext as PaymentSubformItemViewModel ?? PaymentGrid.SelectedItem as PaymentSubformItemViewModel;
                if (vm.CreateDebtorPaymentCommand.CanExecute(pay))
                {
                    vm.CreateDebtorPaymentCommand.Execute(pay);
                }
            }
            return;
        }

        if (e.Key is System.Windows.Input.Key.Enter or System.Windows.Input.Key.Tab)
        {
            e.Handled = true;
            if (DataContext is not InvoiceViewModel vm) return;

            PaymentSubformItemViewModel? currentPay = null;
            if (sender is FrameworkElement fe && fe.DataContext is PaymentSubformItemViewModel pay)
            {
                currentPay = pay;
            }
            else if (PaymentGrid.SelectedItem is PaymentSubformItemViewModel selectedPay)
            {
                currentPay = selectedPay;
            }

            if (currentPay == null) return;

            // Recalculate totals so balance reflects current payment amount
            vm.RecalculateTotals();

            int currentIndex = vm.Payments.IndexOf(currentPay);
            if (currentIndex >= 0 && currentIndex < vm.Payments.Count - 1)
            {
                // Advance to the existing next payment row
                var nextRow = vm.Payments[currentIndex + 1];
                PaymentGrid.SelectedItem = nextRow;
                PaymentGrid.CurrentItem = nextRow;
                Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
                {
                    FocusPaymentAccountForRow(nextRow);
                }));
                return;
            }

            // If there's still an outstanding balance, add exactly one next blank payment row (no preloaded amount)
            if (vm.BalanceAmt > 0.01m)
            {
                // Check if the last payment row is already an empty row
                var existingBlank = vm.Payments.LastOrDefault(p => p != currentPay && string.IsNullOrWhiteSpace(p.AccountName) && p.Amount <= 0);
                if (existingBlank != null)
                {
                    PaymentGrid.SelectedItem = existingBlank;
                    PaymentGrid.CurrentItem = existingBlank;
                    Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
                    {
                        FocusPaymentAccountForRow(existingBlank);
                    }));
                }
                else
                {
                    vm.AddPaymentRow();
                    var newPay = vm.Payments.LastOrDefault();
                    if (newPay != null)
                    {
                        PaymentGrid.SelectedItem = newPay;
                        PaymentGrid.CurrentItem = newPay;
                        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
                        {
                            FocusPaymentAccountForRow(newPay);
                        }));
                    }
                }
            }
            else
            {
                // Balance is settled or 0 -> remove any trailing blank payment rows and jump straight to Save Button
                for (int i = vm.Payments.Count - 1; i >= 0; i--)
                {
                    if (vm.Payments[i] != currentPay && string.IsNullOrWhiteSpace(vm.Payments[i].AccountName) && vm.Payments[i].Amount <= 0)
                    {
                        vm.Payments.RemoveAt(i);
                    }
                }
                Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(FocusSaveButton));
            }
        }
    }

    public void FocusSaveButton()
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            SaveButton?.Focus();
        }));
    }

    private void AmountTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                tb.SelectAll();
            }));
        }
        else if (sender is Telerik.Windows.Controls.RadWatermarkTextBox rtb)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                rtb.SelectAll();
            }));
        }
    }

    private void AmountTextBox_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is UIElement elem && !elem.IsKeyboardFocusWithin)
        {
            elem.Focus();
            e.Handled = true;
        }
    }

    private void InvDate_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key is System.Windows.Input.Key.Enter or System.Windows.Input.Key.Tab)
        {
            e.Handled = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                PhoneTextBox?.Focus();
            }));
        }
    }

    private bool TryHandleCtrlXConvertRetail(System.Windows.Input.KeyEventArgs e)
    {
        if (System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control
            && e.Key == System.Windows.Input.Key.X
            && DataContext is InvoiceViewModel vm
            && vm.ConvertRetailToInvoice())
        {
            e.Handled = true;
            return true;
        }
        return false;
    }

    private void PhoneTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (TryHandleCtrlXConvertRetail(e)) return;

        if (e.Key is System.Windows.Input.Key.Enter)
        {
            e.Handled = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                CustomerNameTextBox?.Focus();
            }));
        }
    }

    private void SearchCustomerButton_Click(object sender, RoutedEventArgs e)
    {
        OpenCustomerSearchDialog();
    }

    private void OpenCustomerSearchDialog()
    {
        if (DataContext is not InvoiceViewModel vm || !vm.IsEditing) return;

        var initialSearch = !string.IsNullOrWhiteSpace(CustomerNameTextBox?.Text)
            ? CustomerNameTextBox.Text
            : (!string.IsNullOrWhiteSpace(PhoneTextBox?.Text) ? PhoneTextBox.Text : string.Empty);

        var picker = new CustomerPickerWindow(vm.AvailableCustomers, initialSearch)
        {
            Owner = Window.GetWindow(this)
        };

        if (picker.ShowDialog() == true && picker.SelectedCustomer is CustomerLookupItem customer)
        {
            vm.SelectedCustomer = customer;
            vm.ApplyCustomerLookup(customer);
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                CustomerNameTextBox?.Focus();
            }));
        }
    }

    private void CustomerNameTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (TryHandleCtrlXConvertRetail(e)) return;

        if (e.Key is System.Windows.Input.Key.Enter)
        {
            e.Handled = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                AddressTextBox?.Focus();
            }));
        }
    }

    private void DueDate_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key is System.Windows.Input.Key.Enter or System.Windows.Input.Key.Tab)
        {
            e.Handled = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(FocusFirstBillItemCell));
        }
    }

    private void State_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key is System.Windows.Input.Key.Enter)
        {
            e.Handled = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(FocusFirstBillItemCell));
        }
    }

    private void Pincode_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key is System.Windows.Input.Key.Enter or System.Windows.Input.Key.Tab)
        {
            e.Handled = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(FocusFirstBillItemCell));
        }
    }

    public void FocusFirstBillItemCell()
    {
        if (ItemsGrid == null) return;

        if (DataContext is InvoiceViewModel vm && vm.Items.Count == 0)
        {
            vm.Items.Add(new BillSubformItemViewModel());
        }

        if (ItemsGrid.Items.Count > 0 && ItemsGrid.Columns.Count > 0)
        {
            var firstCol = ItemsGrid.Columns
                .OfType<Telerik.Windows.Controls.GridViewColumn>()
                .Where(c => c.IsVisible && !c.IsReadOnly && !string.IsNullOrEmpty(c.Header?.ToString()))
                .OrderBy(c => c.DisplayIndex)
                .FirstOrDefault() ?? (Telerik.Windows.Controls.GridViewColumn)ItemsGrid.Columns[0];

            FocusCell(0, firstCol);
        }
    }

    private Telerik.Windows.Controls.GridViewColumn? FindColumn(string name)
    {
        if (ItemsGrid == null) return null;
        var cleanName = name.Replace(" ", "");
        return ItemsGrid.Columns.OfType<Telerik.Windows.Controls.GridViewColumn>()
            .FirstOrDefault(c => c.IsVisible && (
                string.Equals(c.Header?.ToString(), name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c.Header?.ToString()?.Replace(" ", ""), cleanName, StringComparison.OrdinalIgnoreCase) ||
                (c is Telerik.Windows.Controls.GridViewBoundColumnBase bc && string.Equals(bc.DataMemberBinding?.Path?.Path, cleanName, StringComparison.OrdinalIgnoreCase))
            ));
    }

    private void FocusCell(int rowIndex, Telerik.Windows.Controls.GridViewColumn col)
    {
        if (ItemsGrid == null || rowIndex < 0 || rowIndex >= ItemsGrid.Items.Count || col == null) return;

        var item = ItemsGrid.Items[rowIndex];

        ItemsGrid.SelectedItem = item;
        ItemsGrid.CurrentItem = item;
        ItemsGrid.CurrentColumn = col;
        ItemsGrid.ScrollIntoView(item);

        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            ItemsGrid.BeginEdit();

            var row = ItemsGrid.GetRowForItem(item);
            var cell = row?.Cells.FirstOrDefault(c => c.Column == col);

            if (cell != null)
            {
                var editor = (FrameworkElement?)FindVisualChild<Telerik.Windows.Controls.RadComboBox>(cell)
                          ?? FindVisualChild<TextBox>(cell)
                          ?? (FrameworkElement?)FindVisualChild<Telerik.Windows.Controls.RadWatermarkTextBox>(cell)
                          ?? FindVisualChild<Control>(cell);

                if (editor != null)
                {
                    FocusEditableControl(editor);
                    return;
                }
                cell.Focus();
            }
            else
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    var delayedRow = ItemsGrid.GetRowForItem(item);
                    var delayedCell = delayedRow?.Cells.FirstOrDefault(c => c.Column == col);
                    if (delayedCell != null)
                    {
                        var editor = (FrameworkElement?)FindVisualChild<Telerik.Windows.Controls.RadComboBox>(delayedCell)
                                  ?? FindVisualChild<TextBox>(delayedCell)
                                  ?? (FrameworkElement?)FindVisualChild<Telerik.Windows.Controls.RadWatermarkTextBox>(delayedCell)
                                  ?? FindVisualChild<Control>(delayedCell);

                        if (editor != null)
                        {
                            FocusEditableControl(editor);
                            return;
                        }
                        delayedCell.Focus();
                    }
                }));
            }
        }));
    }

    private void AdvanceToNextColumn(int rowIndex, Telerik.Windows.Controls.GridViewColumn? currentCol, BillSubformItemViewModel focusedItem)
    {
        if (DataContext is not InvoiceViewModel vm || ItemsGrid == null) return;

        var orderedCols = ItemsGrid.Columns
            .OfType<Telerik.Windows.Controls.GridViewColumn>()
            .Where(c => c.IsVisible && !string.IsNullOrEmpty(c.Header?.ToString()))
            .OrderBy(c => c.DisplayIndex)
            .ToList();

        if (orderedCols.Count == 0) return;

        int currentIdx = -1;
        if (currentCol != null)
        {
            currentIdx = orderedCols.IndexOf(currentCol);
            if (currentIdx < 0)
            {
                currentIdx = orderedCols.FindIndex(c =>
                    string.Equals(c.Header?.ToString(), currentCol.Header?.ToString(), StringComparison.OrdinalIgnoreCase) ||
                    (c is Telerik.Windows.Controls.GridViewBoundColumnBase bc1 && currentCol is Telerik.Windows.Controls.GridViewBoundColumnBase bc2 && string.Equals(bc1.DataMemberBinding?.Path?.Path, bc2.DataMemberBinding?.Path?.Path, StringComparison.OrdinalIgnoreCase)));
            }
        }

        bool isLastRow = rowIndex >= vm.Items.Count - 1;

        if (currentIdx >= 0 && currentIdx < orderedCols.Count - 1)
        {
            int nextIdx = currentIdx + 1;
            while (nextIdx < orderedCols.Count)
            {
                var candidateCol = orderedCols[nextIdx];
                if (string.Equals(candidateCol.Header?.ToString(), "ItemName", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(focusedItem.Barcode))
                {
                    nextIdx++;
                    continue;
                }
                if (focusedItem.PcRate && (
                    string.Equals(candidateCol.Header?.ToString(), "Rate", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(candidateCol.Header?.ToString(), "MetalValue", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(candidateCol.Header?.ToString(), "MPerGr", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(candidateCol.Header?.ToString(), "Making", StringComparison.OrdinalIgnoreCase)))
                {
                    nextIdx++;
                    continue;
                }
                break;
            }

            if (nextIdx < orderedCols.Count)
            {
                var nextCol = orderedCols[nextIdx];
                FocusCell(rowIndex, nextCol);
                return;
            }
        }

        // Reached end of row (or currentCol wasn't found, fallback to next column or Purity)
        if (currentIdx < 0)
        {
            var fallbackCol = FindColumn("Purity") ?? FindColumn("Qty") ?? orderedCols[0];
            FocusCell(rowIndex, fallbackCol);
            return;
        }

        if (isLastRow)
        {
            if (InvoiceViewModel.IsItemRowFilled(focusedItem))
            {
                var newItem = new BillSubformItemViewModel();
                vm.Items.Add(newItem);
                ItemsGrid.SelectedItem = newItem;
                ItemsGrid.CurrentItem = newItem;
                ItemsGrid.CurrentColumn = orderedCols[0];

                Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
                {
                    FocusCell(vm.Items.Count - 1, orderedCols[0]);
                }));
            }
            else
            {
                // Blank row remains untouched — never remove rows during column navigation!
                Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(FocusPaymentGrid));
            }
        }
        else
        {
            FocusCell(rowIndex + 1, orderedCols[0]);
        }
    }

    private static void FocusEditableControl(FrameworkElement element)
    {
        if (element is Telerik.Windows.Controls.RadComboBox combo)
        {
            if (combo.IsEditable)
            {
                combo.ApplyTemplate();
                var innerBox = (combo.Template?.FindName("PART_EditableTextBox", combo) as TextBox)
                            ?? FindVisualChild<TextBox>(combo)
                            ?? (FrameworkElement?)FindVisualChild<Telerik.Windows.Controls.RadWatermarkTextBox>(combo);

                if (innerBox != null)
                {
                    if (combo.DataContext is BillSubformItemViewModel rowItem)
                    {
                        if (string.Equals(combo.Name, "BarcodeComboBox", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(rowItem.Barcode))
                        {
                            combo.SelectedItem = null;
                            combo.SelectedIndex = -1;
                            combo.Text = string.Empty;
                            if (innerBox is TextBox tbClear) tbClear.Text = string.Empty;
                            else if (innerBox is Telerik.Windows.Controls.RadWatermarkTextBox rtbClear) rtbClear.Text = string.Empty;
                        }
                        else if (string.Equals(combo.Name, "ItemNameComboBox", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(rowItem.ItemName))
                        {
                            combo.SelectedItem = null;
                            combo.SelectedIndex = -1;
                            combo.Text = string.Empty;
                            if (innerBox is TextBox tbClear) tbClear.Text = string.Empty;
                            else if (innerBox is Telerik.Windows.Controls.RadWatermarkTextBox rtbClear) rtbClear.Text = string.Empty;
                        }
                    }

                    innerBox.Focus();
                    if (innerBox is TextBox tb)
                    {
                        tb.SelectAll();
                    }
                    else if (innerBox is Telerik.Windows.Controls.RadWatermarkTextBox rtb)
                    {
                        rtb.SelectAll();
                    }

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

        if (element is Telerik.Windows.Controls.RadWatermarkTextBox rtbElem)
        {
            rtbElem.Focus();
            rtbElem.SelectAll();
            return;
        }

        element.Focus();
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

    private void ItemsGrid_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (DataContext is not InvoiceViewModel vm || !vm.IsEditing || ItemsGrid == null || vm.Items.Count == 0) return;

        bool isTab = e.Key == System.Windows.Input.Key.Tab && (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) == 0;
        bool isEnter = e.Key == System.Windows.Input.Key.Enter;

        if (!isTab && !isEnter) return;

        var focusedElem = System.Windows.Input.Keyboard.FocusedElement as DependencyObject;
        var cell = (focusedElem as Telerik.Windows.Controls.GridView.GridViewCell)
                ?? FindVisualParent<Telerik.Windows.Controls.GridView.GridViewCell>(focusedElem);

        var colHeader = cell?.Column?.Header?.ToString()
                     ?? ItemsGrid.CurrentColumn?.Header?.ToString()
                     ?? string.Empty;

        // Let BarcodeComboBox_PreviewKeyDown handle its own keys directly
        if (string.Equals(colHeader, "Barcode", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // If ItemNameComboBox is active, let ItemNameComboBox_PreviewKeyDown handle it
        if (string.Equals(colHeader, "ItemName", StringComparison.OrdinalIgnoreCase))
        {
            if (focusedElem is Telerik.Windows.Controls.RadComboBox || FindVisualParent<Telerik.Windows.Controls.RadComboBox>(focusedElem) != null)
            {
                return;
            }
        }

        BillSubformItemViewModel? focusedItem = null;
        if (focusedElem != null)
        {
            var row = FindVisualParent<Telerik.Windows.Controls.GridView.GridViewRow>(focusedElem);
            if (row != null && row.DataContext is BillSubformItemViewModel rowItem)
            {
                focusedItem = rowItem;
            }
        }

        if (focusedItem == null && ItemsGrid.SelectedItem is BillSubformItemViewModel selectedItem)
        {
            focusedItem = selectedItem;
        }

        if (focusedItem == null && vm.Items.Count > 0)
        {
            focusedItem = vm.Items[0];
        }

        if (focusedItem == null) return;

        var selectedIndex = vm.Items.IndexOf(focusedItem);
        if (selectedIndex < 0) selectedIndex = 0;

        if (isEnter)
        {
            e.Handled = true;
            ItemsGrid.CommitEdit();

            var currentColumn = cell?.Column ?? ItemsGrid.CurrentColumn;
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                AdvanceToNextColumn(selectedIndex, currentColumn, focusedItem);
            }));
            return;
        }
    }

    private void FocusPaymentGrid()
    {
        if (PaymentGrid == null) return;

        if (DataContext is InvoiceViewModel vm)
        {
            if (vm.Payments.Count == 0)
            {
                vm.Payments.Add(new PaymentSubformItemViewModel());
            }

            if (PaymentGrid.Items.Count > 0)
            {
                PaymentGrid.SelectedItem = PaymentGrid.Items[0];
                PaymentGrid.CurrentItem = PaymentGrid.Items[0];
                PaymentGrid.Focus();

                Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
                {
                    FocusPaymentAccountForRow(PaymentGrid.Items[0] as PaymentSubformItemViewModel);
                }));
            }
        }
    }

    private void FocusPaymentAccountForRow(PaymentSubformItemViewModel? pay)
    {
        if (PaymentGrid == null) return;

        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            var target = pay ?? PaymentGrid.SelectedItem as PaymentSubformItemViewModel ?? PaymentGrid.Items.OfType<PaymentSubformItemViewModel>().LastOrDefault();
            if (target != null)
            {
                PaymentGrid.SelectedItem = target;
                PaymentGrid.CurrentItem = target;
                PaymentGrid.ScrollIntoView(target);
                PaymentGrid.BeginEdit();

                var row = PaymentGrid.GetRowForItem(target);
                if (row != null)
                {
                    var combo = FindVisualChild<Telerik.Windows.Controls.RadComboBox>(row);
                    if (combo != null)
                    {
                        FocusEditableControl(combo);
                        return;
                    }
                }
            }

            var allCombos = FindVisualChild<Telerik.Windows.Controls.RadComboBox>(PaymentGrid);
            if (allCombos != null)
            {
                FocusEditableControl(allCombos);
            }
        }));
    }

    private void FocusPaymentAmountForRow(PaymentSubformItemViewModel? pay)
    {
        if (PaymentGrid == null) return;

        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            var target = pay ?? PaymentGrid.SelectedItem as PaymentSubformItemViewModel ?? PaymentGrid.Items.OfType<PaymentSubformItemViewModel>().FirstOrDefault();
            if (target != null)
            {
                PaymentGrid.SelectedItem = target;
                PaymentGrid.CurrentItem = target;
                var row = PaymentGrid.GetRowForItem(target);
                if (row != null)
                {
                    var box = (FrameworkElement?)FindVisualChild<Telerik.Windows.Controls.RadWatermarkTextBox>(row)
                           ?? FindVisualChild<TextBox>(row);

                    if (box != null)
                    {
                        FocusEditableControl(box);
                        return;
                    }
                }
            }

            var allBoxes = (FrameworkElement?)FindVisualChild<Telerik.Windows.Controls.RadWatermarkTextBox>(PaymentGrid)
                        ?? FindVisualChild<TextBox>(PaymentGrid);
            if (allBoxes != null)
            {
                FocusEditableControl(allBoxes);
            }
        }));
    }

    private readonly ObservableCollection<ColumnChooserItem> _columnChooserItems = new();

    private void InitializeColumnChooser()
    {
        var view = CollectionViewSource.GetDefaultView(_columnChooserItems);
        view.GroupDescriptions.Clear();
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ColumnChooserItem.Category)));
        ColumnChooserList.ItemsSource = view;
    }

    private void ColumnChooserButton_DropDownOpened(object? sender, RoutedEventArgs e)
    {
        RefreshColumnChooserItems();
    }

    private void RefreshColumnChooserItems()
    {
        _columnChooserItems.Clear();

        if (ItemsGrid != null)
        {
            var billCols = ItemsGrid.Columns
                .OfType<Telerik.Windows.Controls.GridViewColumn>()
                .Where(c => !string.IsNullOrWhiteSpace(c.Header?.ToString()))
                .OrderBy(c => c.DisplayIndex);

            foreach (var col in billCols)
            {
                var item = new ColumnChooserItem
                {
                    Header = col.Header?.ToString() ?? string.Empty,
                    Category = "Bill Items",
                    Column = col
                };
                item.SetInitialVisibility(col.IsVisible);
                _columnChooserItems.Add(item);
            }
        }

        if (PaymentGrid != null)
        {
            var paymentCols = PaymentGrid.Columns
                .OfType<Telerik.Windows.Controls.GridViewColumn>()
                .Where(c => !string.IsNullOrWhiteSpace(c.Header?.ToString()))
                .OrderBy(c => c.DisplayIndex);

            foreach (var col in paymentCols)
            {
                var item = new ColumnChooserItem
                {
                    Header = col.Header?.ToString() ?? string.Empty,
                    Category = "Payment",
                    Column = col
                };
                item.SetInitialVisibility(col.IsVisible);
                _columnChooserItems.Add(item);
            }
        }
    }

    private void SaveSettingsOption_Click(object sender, RoutedEventArgs e)
    {
        SaveGridLayout();
        ColumnChooserButton.IsOpen = false;
    }

    private void RemoveCurrentGridLine()
    {
        if (DataContext is not InvoiceViewModel vm || !vm.IsEditing) return;

        if (PaymentGrid != null && PaymentGrid.IsKeyboardFocusWithin)
        {
            RemovePaymentGridLine(vm);
            return;
        }

        RemoveItemGridLine(vm);
    }

    private void RemoveItemGridLine(InvoiceViewModel vm)
    {
        if (vm.Items.Count == 0) return;

        BillSubformItemViewModel? targetItem = null;
        var focusedElem = System.Windows.Input.Keyboard.FocusedElement as DependencyObject;
        if (focusedElem != null)
        {
            var row = FindVisualParent<Telerik.Windows.Controls.GridView.GridViewRow>(focusedElem);
            if (row != null && row.DataContext is BillSubformItemViewModel rowItem)
            {
                targetItem = rowItem;
            }
        }

        if (targetItem == null && ItemsGrid != null)
        {
            targetItem = ItemsGrid.SelectedItem as BillSubformItemViewModel
                      ?? ItemsGrid.CurrentItem as BillSubformItemViewModel;
        }

        if (targetItem == null)
        {
            targetItem = vm.Items.LastOrDefault();
        }

        if (targetItem == null) return;

        int removedIndex = vm.Items.IndexOf(targetItem);

        ItemsGrid?.CancelEdit();

        if (vm.RemoveItemRowCommand.CanExecute(targetItem))
        {
            vm.RemoveItemRowCommand.Execute(targetItem);
        }

        if (ItemsGrid != null && vm.Items.Count > 0)
        {
            int nextIndex = Math.Clamp(removedIndex, 0, vm.Items.Count - 1);
            var nextItem = vm.Items[nextIndex];
            ItemsGrid.SelectedItem = nextItem;
            ItemsGrid.CurrentItem = nextItem;
            ItemsGrid.ScrollIntoView(nextItem);

            var firstCol = ItemsGrid.Columns
                .OfType<Telerik.Windows.Controls.GridViewColumn>()
                .Where(c => c.IsVisible && !c.IsReadOnly && !string.IsNullOrEmpty(c.Header?.ToString()))
                .OrderBy(c => c.DisplayIndex)
                .FirstOrDefault();

            if (firstCol != null)
            {
                FocusCell(nextIndex, firstCol);
            }
        }
    }

    private void RemovePaymentGridLine(InvoiceViewModel vm)
    {
        if (vm.Payments.Count == 0) return;

        PaymentSubformItemViewModel? targetPay = null;
        var focusedElem = System.Windows.Input.Keyboard.FocusedElement as DependencyObject;
        if (focusedElem != null)
        {
            var row = FindVisualParent<Telerik.Windows.Controls.GridView.GridViewRow>(focusedElem);
            if (row != null && row.DataContext is PaymentSubformItemViewModel rowPay)
            {
                targetPay = rowPay;
            }
        }

        if (targetPay == null && PaymentGrid != null)
        {
            targetPay = PaymentGrid.SelectedItem as PaymentSubformItemViewModel
                     ?? PaymentGrid.CurrentItem as PaymentSubformItemViewModel;
        }

        if (targetPay == null)
        {
            targetPay = vm.Payments.LastOrDefault();
        }

        if (targetPay == null) return;

        int removedIndex = vm.Payments.IndexOf(targetPay);

        PaymentGrid?.CancelEdit();

        if (vm.RemovePaymentRowCommand.CanExecute(targetPay))
        {
            vm.RemovePaymentRowCommand.Execute(targetPay);
        }

        if (PaymentGrid != null && vm.Payments.Count > 0)
        {
            int nextIndex = Math.Clamp(removedIndex, 0, vm.Payments.Count - 1);
            var nextPay = vm.Payments[nextIndex];
            PaymentGrid.SelectedItem = nextPay;
            PaymentGrid.CurrentItem = nextPay;
            PaymentGrid.ScrollIntoView(nextPay);

            FocusPaymentAccountForRow(nextPay);
        }
    }

    private static string GetLayoutFilePath()
    {
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ConceptJewelleryPro");
        if (!Directory.Exists(appData))
        {
            Directory.CreateDirectory(appData);
        }
        return Path.Combine(appData, "invoice_grid_layout.json");
    }

    public void SaveGridLayout()
    {
        try
        {
            var data = new InvoiceGridLayoutModel();

            // Save ItemsGrid (BillGrid) columns
            if (ItemsGrid != null)
            {
                foreach (Telerik.Windows.Controls.GridViewColumn col in ItemsGrid.Columns)
                {
                    var header = col.Header?.ToString() ?? col.UniqueName ?? string.Empty;
                    var width = col.ActualWidth > 0 ? col.ActualWidth : (col.Width.Value > 0 ? col.Width.Value : 60);
                    bool isStar = col.Width.IsStar;
                    bool isAuto = col.Width.IsAuto;

                    data.BillGridColumns.Add(new GridColumnLayoutModel
                    {
                        Header = header,
                        Width = width,
                        IsStar = isStar,
                        IsAuto = isAuto,
                        DisplayIndex = col.DisplayIndex,
                        IsVisible = col.IsVisible
                    });
                }
            }

            // Save PaymentGrid columns
            if (PaymentGrid != null)
            {
                foreach (Telerik.Windows.Controls.GridViewColumn col in PaymentGrid.Columns)
                {
                    var header = col.Header?.ToString() ?? col.UniqueName ?? string.Empty;
                    var width = col.ActualWidth > 0 ? col.ActualWidth : (col.Width.Value > 0 ? col.Width.Value : 60);
                    bool isStar = col.Width.IsStar;
                    bool isAuto = col.Width.IsAuto;

                    data.PaymentGridColumns.Add(new GridColumnLayoutModel
                    {
                        Header = header,
                        Width = width,
                        IsStar = isStar,
                        IsAuto = isAuto,
                        DisplayIndex = col.DisplayIndex,
                        IsVisible = col.IsVisible
                    });
                }
            }

            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(GetLayoutFilePath(), json);

            if (_currentViewModel != null)
            {
                _currentViewModel.StatusMessage = "Bill & Payment grid settings saved successfully!";
            }
        }
        catch (Exception ex)
        {
            if (_currentViewModel != null)
            {
                _currentViewModel.ErrorMessage = "Error saving grid layout: " + ex.Message;
            }
        }
    }

    public void LoadGridLayout()
    {
        try
        {
            var filePath = GetLayoutFilePath();
            if (!File.Exists(filePath)) return;

            var json = File.ReadAllText(filePath);
            var data = JsonSerializer.Deserialize<InvoiceGridLayoutModel>(json);
            if (data == null) return;

            // Restore ItemsGrid (BillGrid)
            if (ItemsGrid != null && data.BillGridColumns != null && data.BillGridColumns.Count > 0)
            {
                // 1. Restore Width and Visibility
                foreach (Telerik.Windows.Controls.GridViewColumn col in ItemsGrid.Columns)
                {
                    var header = col.Header?.ToString() ?? col.UniqueName ?? string.Empty;
                    var saved = data.BillGridColumns.FirstOrDefault(c => string.Equals(c.Header, header, StringComparison.OrdinalIgnoreCase));
                    if (saved != null)
                    {
                        if (saved.Width > 0)
                        {
                            if (saved.IsStar)
                                col.Width = new Telerik.Windows.Controls.GridViewLength(saved.Width, Telerik.Windows.Controls.GridViewLengthUnitType.Star);
                            else if (saved.IsAuto)
                                col.Width = Telerik.Windows.Controls.GridViewLength.Auto;
                            else
                                col.Width = new Telerik.Windows.Controls.GridViewLength(saved.Width, Telerik.Windows.Controls.GridViewLengthUnitType.Pixel);
                        }

                        col.IsVisible = saved.IsVisible;
                    }
                }

                // 2. Restore DisplayIndex in ascending order of target index
                var orderedSaved = data.BillGridColumns
                    .Where(c => c.DisplayIndex >= 0 && c.DisplayIndex < ItemsGrid.Columns.Count)
                    .OrderBy(c => c.DisplayIndex)
                    .ToList();

                foreach (var saved in orderedSaved)
                {
                    var col = ItemsGrid.Columns.OfType<Telerik.Windows.Controls.GridViewColumn>().FirstOrDefault(c => string.Equals(c.Header?.ToString() ?? c.UniqueName, saved.Header, StringComparison.OrdinalIgnoreCase));
                    if (col != null)
                    {
                        col.DisplayIndex = Math.Clamp(saved.DisplayIndex, 0, ItemsGrid.Columns.Count - 1);
                    }
                }
            }

            // Restore PaymentGrid
            if (PaymentGrid != null && data.PaymentGridColumns != null && data.PaymentGridColumns.Count > 0)
            {
                // 1. Restore Width and Visibility
                foreach (Telerik.Windows.Controls.GridViewColumn col in PaymentGrid.Columns)
                {
                    var header = col.Header?.ToString() ?? col.UniqueName ?? string.Empty;
                    var saved = data.PaymentGridColumns.FirstOrDefault(c => string.Equals(c.Header, header, StringComparison.OrdinalIgnoreCase));
                    if (saved != null)
                    {
                        if (saved.Width > 0)
                        {
                            if (saved.IsStar)
                                col.Width = new Telerik.Windows.Controls.GridViewLength(saved.Width, Telerik.Windows.Controls.GridViewLengthUnitType.Star);
                            else if (saved.IsAuto)
                                col.Width = Telerik.Windows.Controls.GridViewLength.Auto;
                            else
                                col.Width = new Telerik.Windows.Controls.GridViewLength(saved.Width, Telerik.Windows.Controls.GridViewLengthUnitType.Pixel);
                        }

                        col.IsVisible = saved.IsVisible;
                    }
                }

                // 2. Restore DisplayIndex
                var orderedSavedPayment = data.PaymentGridColumns
                    .Where(c => c.DisplayIndex >= 0 && c.DisplayIndex < PaymentGrid.Columns.Count)
                    .OrderBy(c => c.DisplayIndex)
                    .ToList();

                foreach (var saved in orderedSavedPayment)
                {
                    var col = PaymentGrid.Columns.OfType<Telerik.Windows.Controls.GridViewColumn>().FirstOrDefault(c => string.Equals(c.Header?.ToString() ?? c.UniqueName, saved.Header, StringComparison.OrdinalIgnoreCase));
                    if (col != null)
                    {
                        col.DisplayIndex = Math.Clamp(saved.DisplayIndex, 0, PaymentGrid.Columns.Count - 1);
                    }
                }
            }
        }
        catch
        {
            // Ignore layout load errors on corrupted or incompatible layout files
        }
    }
}
