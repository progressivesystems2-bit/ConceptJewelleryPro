using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.Infrastructure.Services;
using ConceptJewelleryPro.Shared;
using ConceptJewelleryPro.WPF.Controls;
using ConceptJewelleryPro.WPF.Helpers;
using ConceptJewelleryPro.WPF.Views;

namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class AccountCreationViewModel : ViewModelBase, IRefreshable
{
    private readonly IAccountRepository _accountRepository;
    private readonly IAuditRepository? _auditRepository;
    private readonly string _currentUser;
    private readonly MastersIndiaGstService _gstService = new();

    private string _searchText = string.Empty;
    private bool _isBusy;
    private string? _statusMessage;
    private string? _errorMessage;
    private string? _toastMessage;
    private ToastType _toastType = ToastType.Info;
    private Account? _selectedAccount;

    public AccountCreationViewModel(IAccountRepository accountRepository, string currentUser, IAuditRepository? auditRepository = null)
    {
        _accountRepository = accountRepository;
        _auditRepository = auditRepository;
        _currentUser = currentUser;

        AddNewCommand = new RelayCommand(AddNew);
        AddNewFromGstnCommand = new AsyncRelayCommand(AddNewFromGstnAsync, () => !IsBusy);
        ModifyCommand = new AsyncRelayCommand(ModifyAsync, () => !IsBusy);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => !IsBusy);
        ExportToExcelCommand = new RelayCommand(ExportToExcel);
        PrintFormatCommand = new RelayCommand(param => PrintFormat(param as string));

        _ = LoadAccountsAsync();
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

    public ObservableCollection<Account> Accounts { get; } = new();
    public ObservableCollection<Account> FilteredAccounts { get; } = new();

    public Account? SelectedAccount
    {
        get => _selectedAccount;
        set => SetProperty(ref _selectedAccount, value);
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
        SelectedAccount = null;
        ApplyFilter();
    }

    public async Task LoadAccountsAsync()
    {
        IsBusy = true;
        try
        {
            var list = await _accountRepository.GetAllAsync();
            Accounts.Clear();
            foreach (var acc in list)
            {
                Accounts.Add(acc);
            }
            ApplyFilter();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to load accounts: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task RefreshAsync() => await LoadAccountsAsync();

    private void ApplyFilter()
    {
        FilteredAccounts.Clear();
        var query = SearchText?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(query))
        {
            foreach (var acc in Accounts)
            {
                FilteredAccounts.Add(acc);
            }
            return;
        }

        foreach (var acc in Accounts)
        {
            if ((acc.AccountName?.Contains(query, StringComparison.OrdinalIgnoreCase) == true) ||
                (acc.PartyCode?.Contains(query, StringComparison.OrdinalIgnoreCase) == true) ||
                (acc.HeadCategory?.Contains(query, StringComparison.OrdinalIgnoreCase) == true) ||
                (acc.City?.Contains(query, StringComparison.OrdinalIgnoreCase) == true) ||
                (acc.Mobile?.Contains(query, StringComparison.OrdinalIgnoreCase) == true) ||
                (acc.VatNo?.Contains(query, StringComparison.OrdinalIgnoreCase) == true))
            {
                FilteredAccounts.Add(acc);
            }
        }
    }

    private void AddNew()
    {
        var vm = new AccountEditViewModel(_accountRepository, _currentUser, null, _auditRepository);
        var win = new AccountEditWindow(vm);
        if (win.ShowDialog() == true)
        {
            _ = LoadAccountsAsync();
            ShowToast("Account created successfully", ToastType.Success);
        }
    }

    private async Task ModifyAsync()
    {
        if (SelectedAccount == null)
        {
            ShowToast("Please select an account to modify", ToastType.Warning);
            return;
        }

        var vm = new AccountEditViewModel(_accountRepository, _currentUser, SelectedAccount, _auditRepository);
        var win = new AccountEditWindow(vm);
        if (win.ShowDialog() == true)
        {
            await LoadAccountsAsync();
            ShowToast("Account updated successfully", ToastType.Success);
        }
    }

    private async Task DeleteAsync()
    {
        if (SelectedAccount == null)
        {
            ShowToast("Please select an account to delete", ToastType.Warning);
            return;
        }

        var res = MessageBox.Show(
            $"Are you sure you want to delete account '{SelectedAccount.AccountName}' ({SelectedAccount.PartyCode})?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (res == MessageBoxResult.Yes)
        {
            IsBusy = true;
            try
            {
                var accountToDelete = SelectedAccount;
                await _accountRepository.DeleteAsync(accountToDelete.PartyCode);

                if (_auditRepository != null)
                {
                    try
                    {
                        await Infrastructure.Services.AuditService.LogAccountDeletionAsync(_auditRepository, accountToDelete, _currentUser);
                    }
                    catch
                    {
                        // Non-blocking audit logging
                    }
                }

                await LoadAccountsAsync();
                ShowToast("Account deleted successfully", ToastType.Success);
            }
            catch (Exception ex)
            {
                ShowToast("Failed to delete account: " + ex.Message, ToastType.Error);
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
                var vm = new AccountEditViewModel(_accountRepository, _currentUser, null, _auditRepository);
                if (gstResult != null)
                {
                    vm.PopulateFromGstResult(gstResult);
                }
                else
                {
                    vm.VatNo = inputDlg.InputText.Trim();
                }

                var win = new AccountEditWindow(vm);
                if (win.ShowDialog() == true)
                {
                    await LoadAccountsAsync();
                    ShowToast("Account created from GSTIN successfully", ToastType.Success);
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

    private void ExportToExcel()
    {
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV File (*.csv)|*.csv|All Files (*.*)|*.*",
                FileName = $"LedgerAccounts_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (dialog.ShowDialog() == true)
            {
                var sb = new StringBuilder();
                sb.AppendLine("PartyCode,AccountName,HeadCategory,SubCategory,City,State,Mobile,GSTIN,OpAmt1");
                foreach (var acc in FilteredAccounts)
                {
                    sb.AppendLine($"\"{acc.PartyCode}\",\"{acc.AccountName}\",\"{acc.HeadCategory}\",\"{acc.SubCategory}\",\"{acc.City}\",\"{acc.State}\",\"{acc.Mobile}\",\"{acc.VatNo}\",{acc.OpAmt1}");
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

    public static string MapStateName(string? stateName) => PincodeService.MapStateName(stateName);

    private void PrintFormat(string? format)
    {
        var fmt = format ?? "All Accounts Summary";
        ShowToast($"Printing in format: {fmt}", ToastType.Info);
    }

    private void ShowToast(string message, ToastType type)
    {
        ToastMessage = message;
        ToastType = type;
    }
}
