using System.Windows;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.WPF.ViewModels;

namespace ConceptJewelleryPro.WPF.Views;

public partial class BoxPickerWindow : Window
{
    public BoxMaster? SelectedBox { get; private set; }

    public BoxPickerWindow(BoxMasterViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        viewModel.BoxSelected += (_, box) =>
        {
            if (box is not null)
            {
                SelectedBox = box;
                DialogResult = true;
                Close();
            }
        };
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
