using System.Windows;
using ConceptJewelleryPro.WPF.ViewModels;

namespace ConceptJewelleryPro.WPF.Views;

public partial class AiImageEnhanceSettingsWindow : Window
{
    public AiImageEnhanceSettingsWindow(AiImageEnhanceSettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        viewModel.RequestClose += (sender, dialogResult) =>
        {
            DialogResult = dialogResult;
            Close();
        };
    }
}
