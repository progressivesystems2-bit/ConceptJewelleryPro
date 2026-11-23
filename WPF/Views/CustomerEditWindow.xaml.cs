using System.Windows;
using ConceptJewelleryPro.WPF.ViewModels;

namespace ConceptJewelleryPro.WPF.Views;

public partial class CustomerEditWindow : Window
{
    public CustomerEditWindow(CustomerEditViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RequestClose += (s, result) =>
        {
            DialogResult = result;
            Close();
        };
    }

    protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        bool isAltS = (e.KeyboardDevice.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Alt) && (e.Key == System.Windows.Input.Key.S || e.SystemKey == System.Windows.Input.Key.S));
        bool isF10 = (e.Key == System.Windows.Input.Key.F10);

        if (isAltS || isF10)
        {
            if (System.Windows.Input.FocusManager.GetFocusedElement(this) is UIElement focusedUi)
            {
                focusedUi.MoveFocus(new System.Windows.Input.TraversalRequest(System.Windows.Input.FocusNavigationDirection.Next));
            }

            if (DataContext is CustomerEditViewModel vm && vm.SaveCommand.CanExecute(null))
            {
                vm.SaveCommand.Execute(null);
                e.Handled = true;
            }
        }
    }
}
