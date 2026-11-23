using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ConceptJewelleryPro.WPF.ViewModels;

namespace ConceptJewelleryPro.WPF.Views;

public partial class KarigharIssueView : UserControl
{
    private KarigharIssueViewModel? _viewModel;

    public KarigharIssueView()
    {
        InitializeComponent();
        Loaded += KarigharIssueView_Loaded;
        DataContextChanged += KarigharIssueView_DataContextChanged;
    }

    private void KarigharIssueView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.RequestFocusFirstField -= ViewModel_RequestFocusFirstField;
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        _viewModel = DataContext as KarigharIssueViewModel;
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
        if (e.PropertyName == nameof(KarigharIssueViewModel.ToastMessage) ||
            e.PropertyName == nameof(KarigharIssueViewModel.ToastType))
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

    private void KarigharIssueView_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateToastVisibility();
    }
}
