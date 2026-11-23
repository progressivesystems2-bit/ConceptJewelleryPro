using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.WPF.ViewModels;
using Telerik.Windows.Controls;
using Telerik.Windows.Controls.GridView;

namespace ConceptJewelleryPro.WPF.Views;

public partial class UserPermissionView : UserControl
{
    public UserPermissionView()
    {
        InitializeComponent();
    }

    private void UsersGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var element = e.OriginalSource as DependencyObject;
        var row = element?.ParentOfType<GridViewRow>();
        if (row is null || row.DataContext is not UserPermission)
        {
            return;
        }

        e.Handled = true;
        if (DataContext is UserPermissionViewModel vm && vm.SelectedUser is not null)
        {
            vm.EditSelected();
        }
    }
}
