using System.Windows.Controls;
using System.Windows.Input;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.WPF.ViewModels;
using Telerik.Windows.Controls;

namespace ConceptJewelleryPro.WPF.Views;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
    }

    private void PendingOrdersGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is RadGridView grid && grid.SelectedItem is PendingCustomerOrderItem order)
        {
            if (DataContext is DashboardViewModel vm)
            {
                vm.OpenOrder(order);
            }
        }
    }
}
