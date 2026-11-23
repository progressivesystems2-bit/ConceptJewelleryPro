using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.Shared;
using ConceptJewelleryPro.WPF.Controls;
using Microsoft.Win32;

namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class AuditReportViewModel : ViewModelBase
{
    private readonly IAuditRepository _auditRepository;
    private readonly string _currentUser;

    private DateTime _fromDate = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private DateTime _toDate = DateTime.Today;
    private string _selectedUser = "All Users";
    private string _selectedEntryType = "All Modules";
    private AuditStatusFilter _selectedStatus = AuditStatusFilter.All;
    private string _searchText = string.Empty;

    private bool _isBusy;
    private string? _statusMessage;
    private string? _toastMessage;
    private ToastType _toastType = ToastType.Info;
    private AuditGroupedEntry? _selectedGroup;

    private int _totalRecordsCount;
    private int _totalChangesCount;
    private int _uncheckedCount;
    private int _deletionsCount;
    private int _modificationsCount;

    public AuditReportViewModel(IAuditRepository auditRepository, string currentUser)
    {
        _auditRepository = auditRepository;
        _currentUser = currentUser;

        RefreshCommand = new AsyncRelayCommand(LoadLogsAsync, () => !IsBusy);
        ResetFiltersCommand = new RelayCommand(ResetFilters);
        MarkCheckedCommand = new AsyncRelayCommand(MarkSelectedCheckedAsync, () => !IsBusy && SelectedGroup != null);
        MarkUncheckedCommand = new AsyncRelayCommand(MarkSelectedUncheckedAsync, () => !IsBusy && SelectedGroup != null);
        ExportCsvCommand = new RelayCommand(ExportToCsv, () => GroupedLogs.Count > 0);

        _ = InitializeAsync();
    }

    public string Title => "Audit Report (Modification & Deletion Log)";

    public DateTime FromDate
    {
        get => _fromDate;
        set => SetProperty(ref _fromDate, value);
    }

    public DateTime ToDate
    {
        get => _toDate;
        set => SetProperty(ref _toDate, value);
    }

    public string SelectedUser
    {
        get => _selectedUser;
        set => SetProperty(ref _selectedUser, value);
    }

    public string SelectedEntryType
    {
        get => _selectedEntryType;
        set => SetProperty(ref _selectedEntryType, value);
    }

    public AuditStatusFilter SelectedStatus
    {
        get => _selectedStatus;
        set => SetProperty(ref _selectedStatus, value);
    }

    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RefreshCommand.RaiseCanExecuteChanged();
                MarkCheckedCommand.RaiseCanExecuteChanged();
                MarkUncheckedCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
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

    public AuditGroupedEntry? SelectedGroup
    {
        get => _selectedGroup;
        set
        {
            if (SetProperty(ref _selectedGroup, value))
            {
                MarkCheckedCommand.RaiseCanExecuteChanged();
                MarkUncheckedCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public int TotalRecordsCount { get => _totalRecordsCount; private set => SetProperty(ref _totalRecordsCount, value); }
    public int TotalChangesCount { get => _totalChangesCount; private set => SetProperty(ref _totalChangesCount, value); }
    public int UncheckedCount { get => _uncheckedCount; private set => SetProperty(ref _uncheckedCount, value); }
    public int DeletionsCount { get => _deletionsCount; private set => SetProperty(ref _deletionsCount, value); }
    public int ModificationsCount { get => _modificationsCount; private set => SetProperty(ref _modificationsCount, value); }

    public ObservableCollection<string> Users { get; } = new();
    public ObservableCollection<string> EntryTypes { get; } = new();
    public ObservableCollection<AuditGroupedEntry> GroupedLogs { get; } = new();

    public AsyncRelayCommand RefreshCommand { get; }
    public RelayCommand ResetFiltersCommand { get; }
    public AsyncRelayCommand MarkCheckedCommand { get; }
    public AsyncRelayCommand MarkUncheckedCommand { get; }
    public RelayCommand ExportCsvCommand { get; }

    public async Task InitializeAsync()
    {
        try
        {
            await LoadFilterDropdownsAsync();
            await LoadLogsAsync();
        }
        catch (Exception ex)
        {
            ShowToast("Failed to initialize Audit Report: " + ex.Message, ToastType.Error);
        }
    }

    public async Task LoadFilterDropdownsAsync()
    {
        try
        {
            var dbUsers = await _auditRepository.GetDistinctUsersAsync();
            Users.Clear();
            Users.Add("All Users");
            foreach (var u in dbUsers)
            {
                Users.Add(u);
            }
            SelectedUser = "All Users";

            var dbEntryTypes = await _auditRepository.GetDistinctEntryTypesAsync();
            EntryTypes.Clear();
            EntryTypes.Add("All Modules");
            foreach (var et in dbEntryTypes)
            {
                EntryTypes.Add(et);
            }
            SelectedEntryType = "All Modules";
        }
        catch
        {
            // Dropdown load failure non-blocking
        }
    }

    public async Task LoadLogsAsync()
    {
        IsBusy = true;
        StatusMessage = "Loading audit logs...";

        try
        {
            var filter = new AuditReportFilter
            {
                FromDate = FromDate,
                ToDate = ToDate,
                User = SelectedUser == "All Users" ? null : SelectedUser,
                EntryType = SelectedEntryType == "All Modules" ? null : SelectedEntryType,
                Status = SelectedStatus,
                SearchText = SearchText
            };

            var logs = await _auditRepository.GetAuditLogsAsync(filter);

            // Group into transaction groups
            var grouped = GroupAuditLogs(logs);

            GroupedLogs.Clear();
            foreach (var g in grouped)
            {
                GroupedLogs.Add(g);
            }

            // Compute summary KPI counts
            TotalRecordsCount = GroupedLogs.Count;
            TotalChangesCount = logs.Count;
            UncheckedCount = logs.Count(x => !x.Checked);
            DeletionsCount = GroupedLogs.Count(x => x.IsDeleted);
            ModificationsCount = GroupedLogs.Count(x => !x.IsDeleted);

            StatusMessage = $"Loaded {GroupedLogs.Count} audit action(s) ({logs.Count} field change(s)).";
            ExportCsvCommand.RaiseCanExecuteChanged();
        }
        catch (Exception ex)
        {
            StatusMessage = "Error loading logs: " + ex.Message;
            ShowToast("Failed to load audit logs: " + ex.Message, ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static List<AuditGroupedEntry> GroupAuditLogs(IReadOnlyList<AuditLogEntry> logs)
    {
        var groups = new List<AuditGroupedEntry>();
        
        // Group by VouNo, User, EntryType, Date, and Time rounded to nearest minute
        var groupedDictionary = logs
            .GroupBy(l => new
            {
                l.VouNo,
                l.User,
                l.EntryType,
                DateStr = l.Date.ToString("yyyy-MM-dd"),
                TimeStr = l.Time.ToString("HH:mm"),
                l.IsDeleted
            })
            .ToList();

        foreach (var g in groupedDictionary)
        {
            var first = g.First();
            var item = new AuditGroupedEntry
            {
                GroupKey = $"{g.Key.VouNo}_{g.Key.User}_{g.Key.EntryType}_{g.Key.DateStr}_{g.Key.TimeStr}",
                VouNo = g.Key.VouNo,
                User = g.Key.User,
                EntryType = g.Key.EntryType,
                Timestamp = first.Timestamp,
                IsDeleted = g.Key.IsDeleted,
                AllChecked = g.All(x => x.Checked),
                Changes = g.ToList()
            };
            groups.Add(item);
        }

        return groups;
    }

    private async Task MarkSelectedCheckedAsync()
    {
        if (SelectedGroup == null || SelectedGroup.Changes.Count == 0) return;

        try
        {
            IsBusy = true;
            var ids = SelectedGroup.Changes.Select(x => x.IdNo).ToList();
            await _auditRepository.MarkCheckedAsync(ids, true);

            foreach (var c in SelectedGroup.Changes)
            {
                c.Checked = true;
            }
            SelectedGroup.AllChecked = true;
            OnPropertyChanged(nameof(SelectedGroup));

            UncheckedCount = Math.Max(0, UncheckedCount - ids.Count);
            ShowToast($"Marked {ids.Count} change(s) as Verified/Checked.", ToastType.Success);
        }
        catch (Exception ex)
        {
            ShowToast("Failed to update status: " + ex.Message, ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task MarkSelectedUncheckedAsync()
    {
        if (SelectedGroup == null || SelectedGroup.Changes.Count == 0) return;

        try
        {
            IsBusy = true;
            var ids = SelectedGroup.Changes.Select(x => x.IdNo).ToList();
            await _auditRepository.MarkCheckedAsync(ids, false);

            foreach (var c in SelectedGroup.Changes)
            {
                c.Checked = false;
            }
            SelectedGroup.AllChecked = false;
            OnPropertyChanged(nameof(SelectedGroup));

            UncheckedCount += ids.Count;
            ShowToast($"Marked {ids.Count} change(s) as Unchecked/Pending.", ToastType.Info);
        }
        catch (Exception ex)
        {
            ShowToast("Failed to update status: " + ex.Message, ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ResetFilters()
    {
        FromDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        ToDate = DateTime.Today;
        SelectedUser = "All Users";
        SelectedEntryType = "All Modules";
        SelectedStatus = AuditStatusFilter.All;
        SearchText = string.Empty;
        _ = LoadLogsAsync();
    }

    private void ExportToCsv()
    {
        if (GroupedLogs.Count == 0)
        {
            ShowToast("No audit records to export.", ToastType.Warning);
            return;
        }

        var dlg = new SaveFileDialog
        {
            FileName = $"AuditReport_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            Title = "Export Audit Report to CSV"
        };

        if (dlg.ShowDialog() == true)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("ID,Date,Time,User,Module,Voucher/Record No,Action,Field Name,Old Value,New Value,Checked");

                foreach (var group in GroupedLogs)
                {
                    foreach (var change in group.Changes)
                    {
                        var row = string.Format(
                            "{0},{1:yyyy-MM-dd},{2:HH:mm:ss},\"{3}\",\"{4}\",\"{5}\",\"{6}\",\"{7}\",\"{8}\",\"{9}\",{10}",
                            change.IdNo,
                            change.Date,
                            change.Time,
                            EscapeCsv(change.User),
                            EscapeCsv(change.EntryType),
                            EscapeCsv(change.VouNo),
                            change.ActionDisplay,
                            EscapeCsv(change.FieldName),
                            EscapeCsv(change.OldValue),
                            EscapeCsv(change.NewValue),
                            change.Checked ? "Yes" : "No");
                        sb.AppendLine(row);
                    }
                }

                File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                ShowToast("Audit Report exported successfully to " + Path.GetFileName(dlg.FileName), ToastType.Success);
            }
            catch (Exception ex)
            {
                ShowToast("Failed to export CSV: " + ex.Message, ToastType.Error);
            }
        }
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Replace("\"", "\"\"");
    }

    private void ShowToast(string message, ToastType type)
    {
        ToastMessage = message;
        ToastType = type;
    }
}
