using System.Windows.Controls;
using System.Windows.Input;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.WPF.ViewModels;
using Telerik.Windows.Controls;

namespace ConceptJewelleryPro.WPF.Views;

public partial class LedgerReportView : UserControl
{
    public LedgerReportView()
    {
        InitializeComponent();
    }

    private void Grid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is LedgerReportViewModel vm)
        {
            if (sender is RadGridView grid && grid.SelectedItem is LedgerEntry entry)
            {
                vm.OpenVoucher(entry);
            }
            else if (vm.SelectedEntry != null)
            {
                vm.OpenVoucher(vm.SelectedEntry);
            }
        }
    }

    private void Grid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (DataContext is LedgerReportViewModel vm)
            {
                if (sender is RadGridView grid && grid.SelectedItem is LedgerEntry entry)
                {
                    vm.OpenVoucher(entry);
                    e.Handled = true;
                }
                else if (vm.SelectedEntry != null)
                {
                    vm.OpenVoucher(vm.SelectedEntry);
                    e.Handled = true;
                }
            }
        }
    }
}
