using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.WPF.ViewModels;

namespace ConceptJewelleryPro.WPF.Views;

public partial class PurchaseRegisterView : UserControl
{
    public PurchaseRegisterView()
    {
        InitializeComponent();
    }

    private void PurchasesDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is PurchaseRegisterViewModel vm && sender is DataGrid grid && grid.SelectedItem is PurchaseRegisterItem selected)
        {
            vm.SelectPurchaseCommand.Execute(selected);
        }
    }
}
