using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.WPF.Views;

public partial class CustomerHistoryWindow : Window
{
    private readonly string? _customerName;
    private readonly string? _phone;
    private readonly IBillRepository? _billRepository;
    private List<CustomerPurchaseHistoryItem> _allItems = new();

    public CustomerHistoryWindow(string? customerName, string? phone, IBillRepository? billRepository)
    {
        InitializeComponent();
        _customerName = customerName?.Trim();
        _phone = phone?.Trim();
        _billRepository = billRepository;

        CustomerNameTextBlock.Text = !string.IsNullOrWhiteSpace(_customerName) ? _customerName : "Customer";
        CustomerPhoneTextBlock.Text = !string.IsNullOrWhiteSpace(_phone) ? _phone : "No phone number recorded";

        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await LoadHistoryAsync();
    }

    private async Task LoadHistoryAsync()
    {
        BusyIndicator.IsBusy = true;
        try
        {
            if (_billRepository != null)
            {
                var summary = await _billRepository.GetCustomerPurchaseSummaryAsync(_phone, _customerName, null);
                TotalInvoicesTextBlock.Text = summary.TotalBillsCount.ToString();
                TotalValueTextBlock.Text = $"₹ {summary.TotalPurchaseValue:N2}";
                AvgValueTextBlock.Text = $"₹ {summary.AvgPurchaseValue:N2}";
                LastPurchaseTextBlock.Text = summary.LastPurchaseDate.HasValue
                    ? summary.LastPurchaseDate.Value.ToString("dd/MM/yyyy")
                    : "-";

                _allItems = summary.Items?.ToList() ?? new List<CustomerPurchaseHistoryItem>();
                ApplyFilter();
            }
            else
            {
                EmptyStateTextBlock.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load customer history: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BusyIndicator.IsBusy = false;
        }
    }

    private void ApplyFilter()
    {
        var q = SearchBox.Text?.Trim();
        var list = _allItems;
        if (!string.IsNullOrWhiteSpace(q))
        {
            list = _allItems.Where(x =>
                (!string.IsNullOrEmpty(x.ItemName) && x.ItemName.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) ||
                (!string.IsNullOrEmpty(x.VouNo) && x.VouNo.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
            ).ToList();
        }

        HistoryGridView.ItemsSource = list;
        EmptyStateTextBlock.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        RecordCountTextBlock.Text = $"{list.Count} item(s) found";
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void CopyPhoneButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_phone))
        {
            try
            {
                Clipboard.SetText(_phone);
                MessageBox.Show($"Copied mobile number {_phone} to clipboard!", "Copied", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch { }
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
