using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ConceptJewelleryPro.WPF.ViewModels;

namespace ConceptJewelleryPro.WPF.Views;

public partial class KarigharReceiveView : UserControl
{
    private KarigharReceiveViewModel? _viewModel;

    public KarigharReceiveView()
    {
        InitializeComponent();
        Loaded += KarigharReceiveView_Loaded;
        DataContextChanged += KarigharReceiveView_DataContextChanged;
    }

    private void KarigharReceiveView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.RequestFocusFirstField -= ViewModel_RequestFocusFirstField;
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        _viewModel = DataContext as KarigharReceiveViewModel;
        if (_viewModel != null)
        {
            _viewModel.RequestFocusFirstField += ViewModel_RequestFocusFirstField;
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }
    }

    private void ViewModel_RequestFocusFirstField(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            ArtisianNameComboBox?.Focus();
        }));
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(KarigharReceiveViewModel.ToastMessage) ||
            e.PropertyName == nameof(KarigharReceiveViewModel.ToastType))
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

    private void KarigharReceiveView_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateToastVisibility();
    }
}
