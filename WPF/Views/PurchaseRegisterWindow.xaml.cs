using System.Windows;
using System.Windows.Input;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.WPF.ViewModels;

namespace ConceptJewelleryPro.WPF.Views;

public partial class PurchaseRegisterWindow : Window
{
    private readonly PurchaseRegisterViewModel _viewModel;

    public PurchaseRegisterItem? SelectedPurchase => _viewModel.SelectedPurchase;

    public PurchaseRegisterWindow(PurchaseRegisterViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        _viewModel.RequestClose += (s, result) =>
        {
            DialogResult = result;
            Close();
        };

        PreviewKeyDown += PurchaseRegisterWindow_PreviewKeyDown;
    }

    private void PurchaseRegisterWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && !(e.OriginalSource is System.Windows.Controls.TextBox))
        {
            if (_viewModel.SelectedPurchase != null)
            {
                _viewModel.SelectPurchaseCommand.Execute(_viewModel.SelectedPurchase);
                e.Handled = true;
            }
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
