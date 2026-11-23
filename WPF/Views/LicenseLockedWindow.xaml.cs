using System.Windows;
using ConceptJewelleryPro.WPF.ViewModels;

namespace ConceptJewelleryPro.WPF.Views;

public partial class LicenseLockedWindow : Window
{
    public LicenseLockedWindow(LicenseLockedViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        viewModel.VerificationSucceeded += () =>
        {
            DialogResult = true;
            Close();
        };

        viewModel.ExitRequested += () =>
        {
            DialogResult = false;
            Close();
        };
    }
}
