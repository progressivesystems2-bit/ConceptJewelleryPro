using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.WPF.ViewModels;

namespace ConceptJewelleryPro.WPF.Views;

public partial class InvoiceListWindow : Window
{
    private readonly InvoiceListViewModel _viewModel;

    public InvoiceListItem? SelectedInvoice => _viewModel.SelectedInvoice;

    public InvoiceListWindow(InvoiceListViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        _viewModel.RequestClose += (s, result) =>
        {
            DialogResult = result;
            Close();
        };

        PreviewKeyDown += InvoiceListWindow_PreviewKeyDown;
    }

    private void InvoiceListWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && !e.OriginalSource.GetType().IsAssignableFrom(typeof(TextBox)))
        {
            if (_viewModel.SelectedInvoice != null)
            {
                _viewModel.SelectInvoiceCommand.Execute(_viewModel.SelectedInvoice);
                e.Handled = true;
            }
        }
        else if (e.Key == Key.F5)
        {
            if (_viewModel.RefreshCommand.CanExecute(null))
            {
                _viewModel.RefreshCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    private void InvoicesDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source)
        {
            // Ignore double-clicks on buttons/toggles, scrollbars, repeat buttons, thumbs, column headers, or inside the subsheet row details
            if (FindVisualAncestor<System.Windows.Controls.Primitives.ButtonBase>(source) != null ||
                FindVisualAncestor<System.Windows.Controls.Primitives.ScrollBar>(source) != null ||
                FindVisualAncestor<System.Windows.Controls.Primitives.Thumb>(source) != null ||
                FindVisualAncestor<System.Windows.Controls.Primitives.DataGridColumnHeader>(source) != null ||
                FindVisualAncestor<System.Windows.Controls.Primitives.DataGridDetailsPresenter>(source) != null)
            {
                return;
            }

            // Must be a double-click on a valid data cell
            var cell = FindVisualAncestor<DataGridCell>(source);
            var row = FindVisualAncestor<DataGridRow>(source);
            if (cell != null && row != null && row.Item is InvoiceListItem selected)
            {
                _viewModel.SelectInvoiceCommand.Execute(selected);
            }
        }
    }

    private void SubsheetToggle_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Prevent double click on the expander '+' / '-' toggle from bubbling to the DataGrid
        e.Handled = true;
    }

    private static T? FindVisualAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T match)
            {
                return match;
            }
            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private void PrintRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string tag && _viewModel != null)
        {
            _viewModel.SelectedPrintOption = tag;
        }
    }
}
