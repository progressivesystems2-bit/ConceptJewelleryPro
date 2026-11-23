using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.WPF.ViewModels;
using Telerik.Windows.Controls;
using Telerik.Windows.Controls.GridView;

namespace ConceptJewelleryPro.WPF.Views;

public partial class PurityMasterView : UserControl
{
    public PurityMasterView()
    {
        InitializeComponent();
    }

    private void PuritiesGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var element = e.OriginalSource as DependencyObject;
        var row = element?.ParentOfType<GridViewRow>();
        if (row is null || row.DataContext is not PurityMaster)
        {
            return;
        }

        e.Handled = true;
        if (DataContext is PurityMasterViewModel vm && vm.SelectedPurity is not null)
        {
            vm.EditSelected();
        }
    }
}
