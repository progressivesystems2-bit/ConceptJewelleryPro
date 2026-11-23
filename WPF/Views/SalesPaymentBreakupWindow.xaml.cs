using System.Windows;
using System.Windows.Input;
using ConceptJewelleryPro.WPF.ViewModels;

namespace ConceptJewelleryPro.WPF.Views;

public partial class SalesPaymentBreakupWindow : Window
{
    private readonly SalesPaymentBreakupViewModel _viewModel;

    public SalesPaymentBreakupWindow(SalesPaymentBreakupViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        _viewModel.RequestClose += (s, e) => Close();

        PreviewKeyDown += SalesPaymentBreakupWindow_PreviewKeyDown;
        Loaded += SalesPaymentBreakupWindow_Loaded;
    }

    private async void SalesPaymentBreakupWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SummaryItems.Count == 0 && !_viewModel.IsBusy)
        {
            await _viewModel.LoadAsync();
        }
    }

    private void SalesPaymentBreakupWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
        else if (e.Key == Key.F5)
        {
            if (_viewModel.RefreshCommand.CanExecute(null))
            {
                _viewModel.RefreshCommand.Execute(null);
                e.Handled = true;
            }
        }
    }
}
