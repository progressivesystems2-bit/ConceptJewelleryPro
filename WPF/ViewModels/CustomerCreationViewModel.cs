using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.Infrastructure.Services;
using ConceptJewelleryPro.Shared;
using ConceptJewelleryPro.WPF.Controls;
using ConceptJewelleryPro.WPF.Helpers;
using ConceptJewelleryPro.WPF.Services;
using ConceptJewelleryPro.WPF.Views;

namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class CustomerCreationViewModel : ViewModelBase, IRefreshable
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IAuditRepository? _auditRepository;
    private readonly string _currentUser;
    private readonly MastersIndiaGstService _gstService = new();

    private string _searchText = string.Empty;
    private bool _isBusy;
    private string? _statusMessage;
    private string? _errorMessage;
    private string? _toastMessage;
    private ToastType _toastType = ToastType.Info;
    private Customer? _selectedCustomer;

    public CustomerCreationViewModel(ICustomerRepository customerRepository, string currentUser, IAuditRepository? auditRepository = null)
    {
        _customerRepository = customerRepository;
        _auditRepository = auditRepository;
        _currentUser = currentUser;

        AddNewCommand = new RelayCommand(AddNew);
        AddNewFromGstnCommand = new AsyncRelayCommand(AddNewFromGstnAsync, () => !IsBusy);
        ModifyCommand = new AsyncRelayCommand(ModifyAsync, () => !IsBusy);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => !IsBusy);
        ExportToExcelCommand = new RelayCommand(ExportToExcel);
        PrintFormatCommand = new RelayCommand(param => PrintFormat(param as string));
        GetImportFormatCommand = new RelayCommand(GetImportFormat);
        UploadContactsCommand = new AsyncRelayCommand(UploadContactsAsync, () => !IsBusy);

        _ = LoadCustomersAsync();
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilter();
            }
        }
    }

    public RelayCommand AddNewCommand { get; }
    public AsyncRelayCommand AddNewFromGstnCommand { get; }
    public AsyncRelayCommand ModifyCommand { get; }
    public AsyncRelayCommand DeleteCommand { get; }
    public RelayCommand ExportToExcelCommand { get; }
    public RelayCommand PrintFormatCommand { get; }
    public RelayCommand GetImportFormatCommand { get; }
    public AsyncRelayCommand UploadContactsCommand { get; }

    public ObservableCollection<Customer> Customers { get; } = new();
    public ObservableCollection<Customer> FilteredCustomers { get; } = new();

    public Customer? SelectedCustomer
    {
        get => _selectedCustomer;
        set => SetProperty(ref _selectedCustomer, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                ModifyCommand.RaiseCanExecuteChanged();
                DeleteCommand.RaiseCanExecuteChanged();
                AddNewFromGstnCommand.RaiseCanExecuteChanged();
                UploadContactsCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public string? ToastMessage
    {
        get => _toastMessage;
        private set => SetProperty(ref _toastMessage, value);
    }

    public ToastType ToastType
    {
        get => _toastType;
        private set => SetProperty(ref _toastType, value);
    }

    public void ResetSelection()
    {
        _searchText = string.Empty;
        OnPropertyChanged(nameof(SearchText));
        SelectedCustomer = null;
        ApplyFilter();
    }

    public async Task LoadCustomersAsync()
    {
        IsBusy = true;
        try
        {
            var list = await _customerRepository.GetAllAsync();
            Customers.Clear();
            foreach (var cust in list)
            {
                Customers.Add(cust);
            }
            ApplyFilter();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to load customers: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task RefreshAsync() => await LoadCustomersAsync();

    private void ApplyFilter()
    {
        FilteredCustomers.Clear();
        var query = SearchText?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(query))
        {
            foreach (var cust in Customers)
            {
                FilteredCustomers.Add(cust);
            }
            return;
        }

        foreach (var cust in Customers)
        {
            if ((cust.CompanyName?.Contains(query, StringComparison.OrdinalIgnoreCase) == true) ||
                (cust.Code?.Contains(query, StringComparison.OrdinalIgnoreCase) == true) ||
                (cust.City?.Contains(query, StringComparison.OrdinalIgnoreCase) == true) ||
                (cust.Mobile?.Contains(query, StringComparison.OrdinalIgnoreCase) == true) ||
                (cust.Phone?.Contains(query, StringComparison.OrdinalIgnoreCase) == true) ||
                (cust.Group?.Contains(query, StringComparison.OrdinalIgnoreCase) == true) ||
                (cust.Email?.Contains(query, StringComparison.OrdinalIgnoreCase) == true) ||
                (cust.Profession?.Contains(query, StringComparison.OrdinalIgnoreCase) == true))
            {
                FilteredCustomers.Add(cust);
            }
        }
    }

    private void AddNew()
    {
        var vm = new CustomerEditViewModel(_customerRepository, _currentUser, null, _auditRepository);
        var win = new CustomerEditWindow(vm);
        if (win.ShowDialog() == true)
        {
            _ = LoadCustomersAsync();
            ShowToast("Customer created successfully", ToastType.Success);
        }
    }

    private async Task ModifyAsync()
    {
        if (SelectedCustomer == null)
        {
            ShowToast("Please select a customer to modify", ToastType.Warning);
            return;
        }

        var vm = new CustomerEditViewModel(_customerRepository, _currentUser, SelectedCustomer, _auditRepository);
        var win = new CustomerEditWindow(vm);
        if (win.ShowDialog() == true)
        {
            await LoadCustomersAsync();
            ShowToast("Customer updated successfully", ToastType.Success);
        }
    }

    private async Task DeleteAsync()
    {
        if (SelectedCustomer == null)
        {
            ShowToast("Please select a customer to delete", ToastType.Warning);
            return;
        }

        var res = MessageBox.Show(
            $"Are you sure you want to delete customer '{SelectedCustomer.CompanyName}' ({SelectedCustomer.Code})?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (res == MessageBoxResult.Yes)
        {
            IsBusy = true;
            try
            {
                var customerToDelete = SelectedCustomer;
                await _customerRepository.DeleteAsync(customerToDelete.Code);

                if (_auditRepository != null)
                {
                    try
                    {
                        await AuditService.LogCustomerDeletionAsync(_auditRepository, customerToDelete, _currentUser);
                    }
                    catch
                    {
                        // Non-blocking audit logging
                    }
                }

                await LoadCustomersAsync();
                ShowToast("Customer deleted successfully", ToastType.Success);
            }
            catch (Exception ex)
            {
                ShowToast("Failed to delete customer: " + ex.Message, ToastType.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }

    private async Task AddNewFromGstnAsync()
    {
        var inputDlg = new InputDialog("Add From GSTIN", "Enter 15-digit GSTIN Number:");
        if (inputDlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(inputDlg.InputText))
        {
            IsBusy = true;
            try
            {
                var gstResult = await _gstService.SearchGstinAsync(inputDlg.InputText.Trim());
                var vm = new CustomerEditViewModel(_customerRepository, _currentUser, null, _auditRepository);
                if (gstResult != null)
                {
                    vm.PopulateFromGstResult(gstResult);
                }

                var win = new CustomerEditWindow(vm);
                if (win.ShowDialog() == true)
                {
                    await LoadCustomersAsync();
                    ShowToast("Customer created from GSTIN successfully", ToastType.Success);
                }
            }
            catch (Exception ex)
            {
                ShowToast("Failed GSTIN Lookup: " + ex.Message, ToastType.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }

    public async Task SelectCustomerByCodeAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return;

        SearchText = code;
        var match = FilteredCustomers.FirstOrDefault(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase));
        if (match != null)
        {
            SelectedCustomer = match;
            await ModifyAsync();
        }
    }

    private void ExportToExcel()
    {
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV File (*.csv)|*.csv|All Files (*.*)|*.*",
                FileName = $"Customers_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (dialog.ShowDialog() == true)
            {
                var sb = new StringBuilder();
                sb.AppendLine("Code,Initial,CompanyName,Address,Address2,City,Pincode,Group,Nos,Phone,Mobile,Email,DOB,DOA,Profession,Print,Status");
                foreach (var c in FilteredCustomers)
                {
                    sb.AppendLine($"\"{c.Code}\",\"{c.Initial}\",\"{c.CompanyName}\",\"{c.Address}\",\"{c.Address2}\",\"{c.City}\",\"{c.Pincode}\",\"{c.Group}\",{c.Nos},\"{c.Phone}\",\"{c.Mobile}\",\"{c.Email}\",\"{c.DOB:yyyy-MM-dd}\",\"{c.DOA:yyyy-MM-dd}\",\"{c.Profession}\",\"{c.Print}\",\"{c.Status}\"");
                }
                File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
                ShowToast("Exported to CSV successfully", ToastType.Success);
            }
        }
        catch (Exception ex)
        {
            ShowToast("Export failed: " + ex.Message, ToastType.Error);
        }
    }

    private void GetImportFormat()
    {
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV Template (*.csv)|*.csv|All Files (*.*)|*.*",
                FileName = "Customer_Contacts_Template.csv",
                Title = "Save Customer Contacts Template"
            };

            if (dialog.ShowDialog() == true)
            {
                var templateContent = CustomerContactImportService.GenerateCsvTemplate();
                File.WriteAllText(dialog.FileName, templateContent, new UTF8Encoding(true));
                ShowToast("Template downloaded successfully: " + Path.GetFileName(dialog.FileName), ToastType.Success);
            }
        }
        catch (Exception ex)
        {
            ShowToast("Failed to save template: " + ex.Message, ToastType.Error);
        }
    }

    private async Task UploadContactsAsync()
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Customer Contacts File to Import",
                Filter = "Contacts Files (*.xlsx;*.xls;*.csv)|*.xlsx;*.xls;*.csv|Excel Files (*.xlsx;*.xls)|*.xlsx;*.xls|CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                RestoreDirectory = true
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            var filePath = dialog.FileName;
            IsBusy = true;
            StatusMessage = "Importing customer contacts...";

            var result = await Task.Run(() =>
                CustomerContactImportService.ParseFile(filePath, _currentUser, Customers.ToList()));

            if (result.ErrorMessages.Count > 0)
            {
                var err = string.Join(Environment.NewLine, result.ErrorMessages);
                ShowToast(err, ToastType.Error);
                return;
            }

            if (result.AddedCustomers.Count == 0 && result.UpdatedCustomers.Count == 0)
            {
                ShowToast("No valid contacts found to import.", ToastType.Warning);
                return;
            }

            foreach (var cust in result.AddedCustomers)
            {
                await _customerRepository.AddAsync(cust);
            }

            foreach (var cust in result.UpdatedCustomers)
            {
                await _customerRepository.UpdateAsync(cust);
            }

            await LoadCustomersAsync();

            var summary = $"Import completed: {result.AddedCustomers.Count} added, {result.UpdatedCustomers.Count} updated";
            if (result.SkippedCount > 0)
            {
                summary += $", {result.SkippedCount} skipped";
            }
            summary += ".";

            ShowToast(summary, ToastType.Success);
        }
        catch (Exception ex)
        {
            ShowToast("Failed to import contacts: " + ex.Message, ToastType.Error);
        }
        finally
        {
            StatusMessage = null;
            IsBusy = false;
        }
    }

    private void PrintFormat(string? format)
    {
        var fmt = format ?? "All Customers Summary";
        ShowToast($"Printing in format: {fmt}", ToastType.Info);
    }

    private void ShowToast(string message, ToastType type)
    {
        ToastMessage = message;
        ToastType = type;
    }
}
