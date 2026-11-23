using System.Windows;
using System.Windows.Input;
using ConceptJewelleryPro.WPF.ViewModels;

namespace ConceptJewelleryPro.WPF.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        PreviewMouseDown += OnPreviewMouseDown;
    }

    private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        if (e.OriginalSource is not DependencyObject source) return;
        if (IsInsideMenuArea(source)) return;
        vm.CloseAllMenusCommand.Execute(null);
    }

    private static bool IsInsideMenuArea(DependencyObject source)
    {
        while (source is not null)
        {
            if (source is System.Windows.Controls.Button or System.Windows.Controls.Primitives.Popup)
                return true;
            source = System.Windows.Media.VisualTreeHelper.GetParent(source)
                     ?? System.Windows.LogicalTreeHelper.GetParent(source);
        }
        return false;
    }

    private void GlobalSearchResultsList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (GlobalSearchResultsList.SelectedItem is not null)
        {
            GlobalSearchResultsList.ScrollIntoView(GlobalSearchResultsList.SelectedItem);
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        var key = e.SystemKey != Key.None ? e.SystemKey : e.Key;

        if (vm.IsGlobalSearchOpen)
        {
            if (key == Key.Down)
            {
                vm.SelectNextSearchResult();
                e.Handled = true;
                return;
            }
            if (key == Key.Up)
            {
                vm.SelectPreviousSearchResult();
                e.Handled = true;
                return;
            }
            if (key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
            {
                if (vm.SelectedSearchResult is not null)
                {
                    vm.OpenGlobalSearchResultCommand.Execute(vm.SelectedSearchResult);
                    e.Handled = true;
                    return;
                }
            }
            if (key == Key.Escape)
            {
                vm.IsGlobalSearchOpen = false;
                e.Handled = true;
                return;
            }
        }

        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            if (key == Key.W)
            {
                if (vm.CloseCurrentDocumentCommand.CanExecute(null))
                {
                    e.Handled = true;
                    vm.CloseCurrentDocumentCommand.Execute(null);
                    return;
                }
            }
            else if (key is Key.F or Key.Enter)
            {
                GlobalSearchTextBox.Focus();
                GlobalSearchTextBox.SelectAll();
                Keyboard.Focus(GlobalSearchTextBox);
                e.Handled = true;
                return;
            }
        }

        switch (key)
        {
            case Key.F1:
                e.Handled = true;
                vm.SelectModuleCommand.Execute("Account Creation");
                break;
            case Key.F3:
                e.Handled = true;
                vm.SelectModuleCommand.Execute("Barcode Creation");
                break;
            case Key.F4:
                e.Handled = true;
                vm.SelectModuleCommand.Execute("Journal");
                break;
            case Key.F5:
                e.Handled = true;
                vm.SelectModuleCommand.Execute("Receipts");
                break;
            case Key.F6:
                e.Handled = true;
                vm.SelectModuleCommand.Execute("Payments");
                break;
            case Key.F7:
                e.Handled = true;
                vm.SelectModuleCommand.Execute("Invoice");
                break;
            case Key.F8:
                e.Handled = true;
                vm.SelectModuleCommand.Execute("Purchases Expenses");
                break;
            case Key.F9:
                e.Handled = true;
                vm.RefreshCommand.Execute(null);
                break;
        }
    }
}
