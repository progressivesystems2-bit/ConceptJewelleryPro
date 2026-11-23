using System;
using System.Windows;
using System.Windows.Input;
using ConceptJewelleryPro.WPF.ViewModels;

namespace ConceptJewelleryPro.WPF.Views;

public partial class OrderEditWindow : Window
{
    private readonly OrderEditViewModel _viewModel;

    public OrderEditWindow(OrderEditViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        _viewModel.RequestClose += OnRequestClose;
        PreviewKeyDown += OrderEditWindow_PreviewKeyDown;
    }

    private void OnRequestClose(object? sender, bool? result)
    {
        DialogResult = result;
        Close();
    }

    private void OrderEditWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        bool isAltS = (e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Alt) && (e.Key == Key.S || e.SystemKey == Key.S));
        bool isF10 = (e.Key == Key.F10);

        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
            e.Handled = true;
        }
        else if (isAltS || isF10)
        {
            if (FocusManager.GetFocusedElement(this) is UIElement focusedUi)
            {
                focusedUi.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            }

            if (_viewModel.SaveCommand.CanExecute(null))
            {
                e.Handled = true;
                _viewModel.SaveCommand.Execute(null);
            }
        }
    }
}
