using System.Windows.Controls;
using System.Windows.Input;
using ConceptJewelleryPro.WPF.ViewModels;

namespace ConceptJewelleryPro.WPF.Views;

public partial class BoxMasterView : UserControl
{
    public BoxMasterView()
    {
        InitializeComponent();
    }

    private void BoxesGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is BoxMasterViewModel vm && vm.EditCommand.CanExecute(null))
        {
            vm.EditCommand.Execute(null);
        }
    }
}
