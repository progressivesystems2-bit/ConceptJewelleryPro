using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.WPF.ViewModels;

namespace ConceptJewelleryPro.WPF.Views;

public partial class BarcodeStockReportView : UserControl
{
    private BarcodeStockReportViewModel? _viewModel;
    private bool _isScanning;
    private bool _isFilterEventsSubscribed;

    public BarcodeStockReportView()
    {
        InitializeComponent();

        Loaded += BarcodeStockReportView_Loaded;
        DataContextChanged += BarcodeStockReportView_DataContextChanged;
    }

    private void BarcodeStockReportView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            _viewModel.ScannedStockItems.CollectionChanged -= ScannedStockItems_CollectionChanged;
            _viewModel.PendingStockItems.CollectionChanged -= PendingStockItems_CollectionChanged;
        }

        _viewModel = DataContext as BarcodeStockReportViewModel;
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            _viewModel.ScannedStockItems.CollectionChanged += ScannedStockItems_CollectionChanged;
            _viewModel.PendingStockItems.CollectionChanged += PendingStockItems_CollectionChanged;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BarcodeStockReportViewModel.ToastMessage) ||
            e.PropertyName == nameof(BarcodeStockReportViewModel.ToastType))
        {
            UpdateToastVisibility();
        }
        else if (e.PropertyName == nameof(BarcodeStockReportViewModel.IsStockCheckActive))
        {
            if (_viewModel?.IsStockCheckActive == true)
            {
                RefocusScanBox();
                UpdateFilteredTotals();
            }
        }
        else if (e.PropertyName == nameof(BarcodeStockReportViewModel.SelectedScannedStockItem))
        {
            if (_viewModel?.SelectedScannedStockItem != null)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
                {
                    ScannedStockGridView?.ScrollIntoView(_viewModel.SelectedScannedStockItem);
                });
            }
        }
    }

    private void StockCheckScanTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Return)
        {
            e.Handled = true;
            SubmitScan();
        }
    }

    private void StockCheckScanTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Return)
        {
            e.Handled = true;
            SubmitScan();
        }
    }

    private void BarcodeStockReportView_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_viewModel?.IsStockCheckActive != true) return;

        if (StockCheckScanTextBox != null && StockCheckScanTextBox.IsKeyboardFocusWithin)
        {
            return;
        }

        // Do not steal focus if user is actively in another text input, dropdown, datepicker, or checkbox
        if (Keyboard.FocusedElement is TextBox or ComboBox or DatePicker or CheckBox)
        {
            return;
        }

        if (Keyboard.FocusedElement is DependencyObject focusedDo &&
            (FindParent<Telerik.Windows.Controls.RadDatePicker>(focusedDo) != null ||
             FindParent<Telerik.Windows.Controls.RadComboBox>(focusedDo) != null ||
             FindParent<CheckBox>(focusedDo) != null ||
             focusedDo is Telerik.Windows.Controls.RadDatePicker ||
             focusedDo is Telerik.Windows.Controls.RadComboBox ||
             focusedDo is CheckBox))
        {
            return;
        }

        if (e.Key is Key.Enter or Key.Return)
        {
            e.Handled = true;
            SubmitScan();
            return;
        }

        // Direct scanner or typed alphanumeric input directly into the scan textbox
        if ((e.Key >= Key.A && e.Key <= Key.Z) ||
            (e.Key >= Key.D0 && e.Key <= Key.D9) ||
            (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9))
        {
            StockCheckScanTextBox?.Focus();
            Keyboard.Focus(StockCheckScanTextBox);
        }
    }

    private void StockCheckGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        BarcodeStockReportView_PreviewKeyDown(sender, e);
    }

    private void SubmitScan()
    {
        if (_isScanning) return;

        var text = StockCheckScanTextBox?.Text?.Trim();
        if (StockCheckScanTextBox != null)
        {
            StockCheckScanTextBox.Text = string.Empty;
        }

        if (string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(_viewModel?.ScanBarcodeText))
        {
            text = _viewModel.ScanBarcodeText.Trim();
        }

        if (string.IsNullOrWhiteSpace(text) || _viewModel == null)
        {
            RefocusScanBox();
            return;
        }

        try
        {
            _isScanning = true;
            _viewModel.ScanBarcodeText = string.Empty;
            if (_viewModel.ScanBarcodeCommand.CanExecute(text))
            {
                _viewModel.ScanBarcodeCommand.Execute(text);
            }
        }
        finally
        {
            _isScanning = false;
            RefocusScanBox();
        }
    }

    private void RefocusScanBox()
    {
        if (StockCheckScanTextBox == null) return;

        void FocusAction()
        {
            if (_viewModel?.IsStockCheckActive != true) return;

            // Don't steal focus if user is actively in a combobox, datepicker, or checkbox
            if (Keyboard.FocusedElement is CheckBox ||
                (Keyboard.FocusedElement is DependencyObject focusedDo &&
                (FindParent<Telerik.Windows.Controls.RadDatePicker>(focusedDo) != null ||
                 FindParent<Telerik.Windows.Controls.RadComboBox>(focusedDo) != null ||
                 FindParent<CheckBox>(focusedDo) != null ||
                 focusedDo is Telerik.Windows.Controls.RadDatePicker ||
                 focusedDo is Telerik.Windows.Controls.RadComboBox ||
                 focusedDo is CheckBox)))
            {
                return;
            }

            if (StockCheckScanTextBox.IsEnabled && StockCheckScanTextBox.IsVisible)
            {
                StockCheckScanTextBox.Focus();
                Keyboard.Focus(StockCheckScanTextBox);
                StockCheckScanTextBox.SelectAll();
            }
        }

        Dispatcher.BeginInvoke(DispatcherPriority.Input, (Action)FocusAction);
        Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, (Action)FocusAction);
    }

    private static T? FindParent<T>(DependencyObject? element) where T : DependencyObject
    {
        var current = element;
        while (current != null)
        {
            if (current is T match) return match;
            current = current is System.Windows.Media.Visual
                ? System.Windows.Media.VisualTreeHelper.GetParent(current)
                : System.Windows.LogicalTreeHelper.GetParent(current);
        }
        return null;
    }

    private void UpdateToastVisibility()
    {
        if (_viewModel == null) return;

        if (!string.IsNullOrEmpty(_viewModel.ToastMessage))
        {
            ToastNotification.Message = _viewModel.ToastMessage;
            ToastNotification.ToastType = _viewModel.ToastType;
            ToastNotification.Show();
        }
        else
        {
            ToastNotification.Hide();
        }
    }

    private void BarcodeStockReportView_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateToastVisibility();

        if (!_isFilterEventsSubscribed)
        {
            _isFilterEventsSubscribed = true;
            if (ScannedStockGridView != null)
            {
                ScannedStockGridView.FilterDescriptors.CollectionChanged += (s, ev) => UpdateFilteredTotals();
            }
            if (PendingStockGridView != null)
            {
                PendingStockGridView.FilterDescriptors.CollectionChanged += (s, ev) => UpdateFilteredTotals();
            }
        }

        if (_viewModel?.IsStockCheckActive == true)
        {
            RefocusScanBox();
            UpdateFilteredTotals();
        }
    }

    private void ScannedStockGridView_Filtered(object? sender, Telerik.Windows.Controls.GridView.GridViewFilteredEventArgs e)
    {
        UpdateFilteredTotals();
    }

    private void PendingStockGridView_Filtered(object? sender, Telerik.Windows.Controls.GridView.GridViewFilteredEventArgs e)
    {
        UpdateFilteredTotals();
    }

    private void ScannedStockItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Normal, () =>
        {
            UpdateFilteredTotals();
        });
    }

    private void PendingStockItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Normal, () =>
        {
            UpdateFilteredTotals();
        });
    }

    private void UpdateFilteredTotals()
    {
        if (_viewModel == null) return;

        IEnumerable<BarcodeStockReportItem>? scannedItems = null;
        if (ScannedStockGridView != null && ScannedStockGridView.FilterDescriptors.Count > 0)
        {
            scannedItems = ScannedStockGridView.Items.OfType<BarcodeStockReportItem>();
        }

        IEnumerable<BarcodeStockReportItem>? pendingItems = null;
        if (PendingStockGridView != null && PendingStockGridView.FilterDescriptors.Count > 0)
        {
            pendingItems = PendingStockGridView.Items.OfType<BarcodeStockReportItem>();
        }

        _viewModel.RecalculateStockCheckTotals(scannedItems, pendingItems);
    }
}
