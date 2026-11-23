using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ConceptJewelleryPro.WPF.ViewModels;

namespace ConceptJewelleryPro.WPF.Views;

public partial class StaffCreationView : UserControl
{
    private StaffCreationViewModel? _viewModel;

    public StaffCreationView()
    {
        InitializeComponent();
        Loaded += StaffCreationView_Loaded;
        DataContextChanged += StaffCreationView_DataContextChanged;
    }

    private void StaffCreationView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.RequestFocusName -= ViewModel_RequestFocusName;
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        _viewModel = DataContext as StaffCreationViewModel;
        if (_viewModel != null)
        {
            _viewModel.RequestFocusName += ViewModel_RequestFocusName;
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }
    }

    private void ViewModel_RequestFocusName(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            StaffNameTextBox?.Focus();
            StaffNameTextBox?.SelectAll();
        }));
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(StaffCreationViewModel.ToastMessage) ||
            e.PropertyName == nameof(StaffCreationViewModel.ToastType))
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

    private void StaffCreationView_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateToastVisibility();
    }
}
