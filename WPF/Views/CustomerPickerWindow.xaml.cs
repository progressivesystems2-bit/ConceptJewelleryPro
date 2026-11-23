using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.WPF.Views;

public partial class CustomerPickerWindow : Window
{
    private readonly List<CustomerLookupItem> _allCustomers;
    public CustomerLookupItem? SelectedCustomer { get; private set; }

    public CustomerPickerWindow(IEnumerable<CustomerLookupItem> customers, string? initialSearch = null)
    {
        InitializeComponent();
        _allCustomers = customers?.ToList() ?? new List<CustomerLookupItem>();
        Loaded += OnLoaded;
        if (!string.IsNullOrWhiteSpace(initialSearch))
        {
            SearchBox.Text = initialSearch;
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        FilterCustomers(SearchBox.Text);
        SearchBox.Focus();
        if (!string.IsNullOrWhiteSpace(SearchBox.Text))
        {
            SearchBox.SelectAll();
        }
    }

    private void FilterCustomers(string? query)
    {
        var q = query?.Trim() ?? string.Empty;
        List<CustomerLookupItem> filtered;
        if (string.IsNullOrWhiteSpace(q))
        {
            filtered = _allCustomers;
        }
        else
        {
            filtered = _allCustomers.Where(c =>
                (c.Name != null && c.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) ||
                (c.Phone != null && c.Phone.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) ||
                (c.City != null && c.City.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) ||
                (c.Code != null && c.Code.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) ||
                (c.Address != null && c.Address.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
            ).ToList();
        }

        CustomersGrid.ItemsSource = filtered;
        CountTextBlock.Text = $"{filtered.Count} of {_allCustomers.Count} customer(s)";
        if (filtered.Count > 0)
        {
            CustomersGrid.SelectedItem = filtered[0];
        }
    }

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        FilterCustomers(SearchBox.Text);
    }

    private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down && CustomersGrid.Items.Count > 0)
        {
            e.Handled = true;
            CustomersGrid.Focus();
        }
        else if (e.Key == Key.Enter)
        {
            e.Handled = true;
            ConfirmSelection();
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            DialogResult = false;
            Close();
        }
    }

    private void CustomersGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            ConfirmSelection();
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            DialogResult = false;
            Close();
        }
    }

    private void CustomersGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        ConfirmSelection();
    }

    private void SelectButton_Click(object sender, RoutedEventArgs e)
    {
        ConfirmSelection();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ConfirmSelection()
    {
        if (CustomersGrid.SelectedItem is CustomerLookupItem selected)
        {
            SelectedCustomer = selected;
            DialogResult = true;
            Close();
        }
    }
}
