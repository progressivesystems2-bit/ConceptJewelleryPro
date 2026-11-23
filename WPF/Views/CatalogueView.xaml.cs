using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.WPF.ViewModels;

namespace ConceptJewelleryPro.WPF.Views;

public partial class CatalogueView : UserControl
{
    private CatalogueViewModel? _viewModel;

    public CatalogueView()
    {
        InitializeComponent();
        Loaded += CatalogueView_Loaded;
        DataContextChanged += CatalogueView_DataContextChanged;
    }

    private void CatalogueView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        _viewModel = DataContext as CatalogueViewModel;
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CatalogueViewModel.ToastMessage) ||
            e.PropertyName == nameof(CatalogueViewModel.ToastType))
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

    private void CatalogueView_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateToastVisibility();
    }

    private void CatalogueListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel == null) return;

        // Try getting item from sender or SelectedItem
        if (_viewModel.SelectedItem is CatalogueItem item && !string.IsNullOrWhiteSpace(item.Barcode))
        {
            _viewModel.OpenBarcode(item.Barcode);
            e.Handled = true;
        }
    }
}
