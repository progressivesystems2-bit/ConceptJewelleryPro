using System.Windows;
using System.Windows.Input;
using ConceptJewelleryPro.WPF.ViewModels;

namespace ConceptJewelleryPro.WPF.Views;

public partial class PurityEditWindow : Window
{
    public PurityEditWindow(PurityEditViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        viewModel.RequestClose += (_, result) =>
        {
            DialogResult = result;
            Close();
        };

        Loaded += (_, _) =>
        {
            PurityTextBox?.Focus();
        };
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        bool isAltS = (e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Alt) && (e.Key == Key.S || e.SystemKey == Key.S));
        bool isF10 = (e.Key == Key.F10);

        if (isAltS || isF10)
        {
            if (FocusManager.GetFocusedElement(this) is UIElement focusedUi)
            {
                focusedUi.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            }

            if (DataContext is PurityEditViewModel vm && vm.SaveCommand.CanExecute(null))
            {
                vm.SaveCommand.Execute(null);
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }
}
