using System.Windows;
using ConceptJewelleryPro.WPF.ViewModels;

namespace ConceptJewelleryPro.WPF.Views;

public partial class Gstr3BExportWindow : Window
{
    public Gstr3BExportWindow(Gstr3BExportViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        viewModel.RequestClose += (_, _) =>
        {
            DialogResult = true;
            Close();
        };
    }
}
