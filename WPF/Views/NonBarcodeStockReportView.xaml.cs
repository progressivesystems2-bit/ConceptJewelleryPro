using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using ConceptJewelleryPro.WPF.ViewModels;

namespace ConceptJewelleryPro.WPF.Views;

public partial class NonBarcodeStockReportView : UserControl
{
    private NonBarcodeStockReportViewModel? _viewModel;

    public NonBarcodeStockReportView()
    {
        InitializeComponent();
        Loaded += NonBarcodeStockReportView_Loaded;
        DataContextChanged += NonBarcodeStockReportView_DataContextChanged;
    }

    private void NonBarcodeStockReportView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        _viewModel = DataContext as NonBarcodeStockReportViewModel;

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NonBarcodeStockReportViewModel.ToastMessage) ||
            e.PropertyName == nameof(NonBarcodeStockReportViewModel.ToastType))
        {
            UpdateToastVisibility();
        }
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

    private void NonBarcodeStockReportView_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateToastVisibility();
    }
}
