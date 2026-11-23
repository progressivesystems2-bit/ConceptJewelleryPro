using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ConceptJewelleryPro.WPF.ViewModels;

namespace ConceptJewelleryPro.WPF.Views;

public partial class StockTransferView : UserControl
{
    private bool _isScanning;
    private StockTransferViewModel? _viewModel;

    public StockTransferView()
    {
        InitializeComponent();

        DataContextChanged += StockTransferView_DataContextChanged;
        Loaded += StockTransferView_Loaded;
    }

    private void StockTransferView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        _viewModel = DataContext as StockTransferViewModel;

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(StockTransferViewModel.ToastMessage) or nameof(StockTransferViewModel.ToastType))
        {
            UpdateToastVisibility();
        }
        else if (e.PropertyName == nameof(StockTransferViewModel.FormMode))
        {
            if (_viewModel?.IsFormEnabled == true)
            {
                RefocusScanBox();
            }
        }
    }

    private void StockTransferView_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateToastVisibility();
        RefocusScanBox();
    }

    private void ScanTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Return)
        {
            e.Handled = true;
            SubmitScan();
        }
    }

    private void ScanTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Return)
        {
            e.Handled = true;
            SubmitScan();
        }
    }

    private void ScanButton_Click(object sender, RoutedEventArgs e)
    {
        SubmitScan();
    }

    private void StockTransferView_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (ScanTextBox != null && ScanTextBox.IsKeyboardFocusWithin)
        {
            return;
        }

        // If user is intentionally typing into Remarks, Date, Combobox or clicking buttons, don't steal focus
        if (Keyboard.FocusedElement is TextBox or ComboBox or DatePicker)
        {
            return;
        }

        if (Keyboard.FocusedElement is DependencyObject focusedDo &&
            (FindParent<Telerik.Windows.Controls.RadDatePicker>(focusedDo) != null ||
             FindParent<Telerik.Windows.Controls.RadComboBox>(focusedDo) != null ||
             FindParent<System.Windows.Controls.Primitives.ButtonBase>(focusedDo) != null ||
             focusedDo is Telerik.Windows.Controls.RadDatePicker ||
             focusedDo is Telerik.Windows.Controls.RadComboBox ||
             focusedDo is System.Windows.Controls.Primitives.ButtonBase))
        {
            return;
        }

        if (e.Key is Key.Enter or Key.Return)
        {
            e.Handled = true;
            RefocusScanBox();
            return;
        }

        // Capture incoming scanner characters from anywhere in the window
        if ((e.Key >= Key.A && e.Key <= Key.Z) ||
            (e.Key >= Key.D0 && e.Key <= Key.D9) ||
            (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9))
        {
            ScanTextBox?.Focus();
            Keyboard.Focus(ScanTextBox);
        }
    }

    private void SubmitScan()
    {
        if (_isScanning) return;

        var text = ScanTextBox?.Text?.Trim();
        if (ScanTextBox != null)
        {
            ScanTextBox.Text = string.Empty;
        }

        if (string.IsNullOrWhiteSpace(text) || _viewModel == null)
        {
            RefocusScanBox();
            return;
        }

        try
        {
            _isScanning = true;
            _viewModel.ScanBarcodeText = text;
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
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            if (Keyboard.FocusedElement is DependencyObject focusedDo &&
                (FindParent<Telerik.Windows.Controls.RadDatePicker>(focusedDo) != null ||
                 FindParent<Telerik.Windows.Controls.RadComboBox>(focusedDo) != null ||
                 FindParent<System.Windows.Controls.Primitives.ButtonBase>(focusedDo) != null ||
                 focusedDo is Telerik.Windows.Controls.RadDatePicker ||
                 focusedDo is Telerik.Windows.Controls.RadComboBox ||
                 focusedDo is System.Windows.Controls.Primitives.ButtonBase))
            {
                return;
            }

            if (ScanTextBox != null && ScanTextBox.IsEnabled)
            {
                ScanTextBox.Focus();
                Keyboard.Focus(ScanTextBox);
                ScanTextBox.SelectAll();
            }
        });
    }

    private void UpdateToastVisibility()
    {
        if (_viewModel == null || ToastNotification == null) return;

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

    private void RegisterGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel != null && _viewModel.SelectedTransfer != null)
        {
            _viewModel.SelectedTabIndex = 0;
            RefocusScanBox();
        }
    }
}
