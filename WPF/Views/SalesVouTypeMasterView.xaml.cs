using System.Windows.Controls;
using System.Windows.Input;
using ConceptJewelleryPro.WPF.ViewModels;

namespace ConceptJewelleryPro.WPF.Views;

public partial class SalesVouTypeMasterView : UserControl
{
    public SalesVouTypeMasterView()
    {
        InitializeComponent();
    }

    private void VouTypesGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is SalesVouTypeMasterViewModel vm && vm.EditCommand.CanExecute(null))
        {
            vm.EditCommand.Execute(null);
        }
    }
}
