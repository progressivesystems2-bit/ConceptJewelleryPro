using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using ConceptJewelleryPro.Infrastructure.Settings;
using ConceptJewelleryPro.Shared;

namespace ConceptJewelleryPro.WPF.ViewModels;

public enum DatabaseProvider
{
    MicrosoftAccess,
    SqlServer
}

public sealed class PreviousYearAccessItem : ViewModelBase
{
    private string _financialYear = string.Empty;
    private string _filePath = string.Empty;

    public string FinancialYear
    {
        get => _financialYear;
        set => SetProperty(ref _financialYear, value);
    }

    public string FilePath
    {
        get => _filePath;
        set => SetProperty(ref _filePath, value);
    }
}

public sealed class PreviousYearSqlItem : ViewModelBase
{
    private string _financialYear = string.Empty;
    private string _databaseName = string.Empty;

    public string FinancialYear
    {
        get => _financialYear;
        set => SetProperty(ref _financialYear, value);
    }

    public string DatabaseName
    {
        get => _databaseName;
        set => SetProperty(ref _databaseName, value);
    }
}

public sealed class ConnectionSettingsViewModel : ViewModelBase
{
    private readonly DatabaseSettingsStore _store;
    private bool _isExpanded;
    private int _selectedProviderIndex = (int)DatabaseProvider.SqlServer;
    private string _accessDatabasePath = string.Empty;
    private string _sqlServerName = string.Empty;
    private string _sqlDatabaseName = "ConceptJewelleryPro";
    private string _sqlUsername = string.Empty;
    private string _sqlPassword = string.Empty;
    private string _saveStatusMessage = string.Empty;

    public ConnectionSettingsViewModel(DatabaseSettingsStore store)
    {
        _store = store;
        SaveCommand = new RelayCommand(Save, _ => true);
        AddPreviousYearAccessCommand = new RelayCommand(AddPreviousYearAccess);
        RemovePreviousYearAccessCommand = new RelayCommand(RemovePreviousYearAccess);
        BrowsePreviousYearAccessCommand = new RelayCommand(BrowsePreviousYearAccess);
        BrowseAccessDatabaseCommand = new RelayCommand(BrowseAccessDatabase);
        AddPreviousYearSqlCommand = new RelayCommand(AddPreviousYearSql);
        RemovePreviousYearSqlCommand = new RelayCommand(RemovePreviousYearSql);

        var saved = _store.Load();
        SelectedProviderIndex = saved.Provider == DatabaseProvider.MicrosoftAccess.ToString()
            ? (int)DatabaseProvider.MicrosoftAccess
            : (int)DatabaseProvider.SqlServer;
        AccessDatabasePath = saved.AccessDatabasePath;
        SqlServerName = saved.SqlServerName;
        SqlDatabaseName = !string.IsNullOrWhiteSpace(saved.SqlDatabaseName) ? saved.SqlDatabaseName : "ConceptJewelleryPro";
        SqlUsername = saved.SqlUsername;
        SqlPassword = saved.SqlPassword;

        PreviousYearsAccess.Clear();
        if (saved.PreviousYearsAccess is not null)
        {
            foreach (var item in saved.PreviousYearsAccess)
            {
                PreviousYearsAccess.Add(new PreviousYearAccessItem
                {
                    FinancialYear = item.FinancialYear,
                    FilePath = item.FilePath
                });
            }
        }

        PreviousYearsSql.Clear();
        if (saved.PreviousYearsSql is not null)
        {
            foreach (var item in saved.PreviousYearsSql)
            {
                PreviousYearsSql.Add(new PreviousYearSqlItem
                {
                    FinancialYear = item.FinancialYear,
                    DatabaseName = item.DatabaseName
                });
            }
        }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public int SelectedProviderIndex
    {
        get => _selectedProviderIndex;
        set
        {
            if (SetProperty(ref _selectedProviderIndex, value))
            {
                OnPropertyChanged(nameof(IsAccessSelected));
                OnPropertyChanged(nameof(IsSqlSelected));
            }
        }
    }

    public bool IsAccessSelected => SelectedProviderIndex == (int)DatabaseProvider.MicrosoftAccess;

    public bool IsSqlSelected => SelectedProviderIndex == (int)DatabaseProvider.SqlServer;

    public string AccessDatabasePath
    {
        get => _accessDatabasePath;
        set => SetProperty(ref _accessDatabasePath, value);
    }

    public string SqlServerName
    {
        get => _sqlServerName;
        set => SetProperty(ref _sqlServerName, value);
    }

    public string SqlDatabaseName
    {
        get => _sqlDatabaseName;
        set => SetProperty(ref _sqlDatabaseName, value);
    }

    public string SqlUsername
    {
        get => _sqlUsername;
        set => SetProperty(ref _sqlUsername, value);
    }

    public string SqlPassword
    {
        get => _sqlPassword;
        set => SetProperty(ref _sqlPassword, value);
    }

    public ObservableCollection<PreviousYearAccessItem> PreviousYearsAccess { get; } = new();

    public ObservableCollection<PreviousYearSqlItem> PreviousYearsSql { get; } = new();

    public string SaveStatusMessage
    {
        get => _saveStatusMessage;
        private set => SetProperty(ref _saveStatusMessage, value);
    }

    public RelayCommand SaveCommand { get; }
    public RelayCommand AddPreviousYearAccessCommand { get; }
    public RelayCommand RemovePreviousYearAccessCommand { get; }
    public RelayCommand BrowsePreviousYearAccessCommand { get; }
    public RelayCommand BrowseAccessDatabaseCommand { get; }
    public RelayCommand AddPreviousYearSqlCommand { get; }
    public RelayCommand RemovePreviousYearSqlCommand { get; }

    private void AddPreviousYearAccess(object? parameter)
    {
        PreviousYearsAccess.Add(new PreviousYearAccessItem());
    }

    private void RemovePreviousYearAccess(object? parameter)
    {
        if (parameter is PreviousYearAccessItem item)
        {
            PreviousYearsAccess.Remove(item);
        }
    }

    private void BrowsePreviousYearAccess(object? parameter)
    {
        var item = parameter as PreviousYearAccessItem;
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select Previous Year Microsoft Access Database File",
            Filter = "Access Database (*.accdb;*.mdb)|*.accdb;*.mdb|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            if (item is not null)
            {
                item.FilePath = dialog.FileName;
                if (string.IsNullOrWhiteSpace(item.FinancialYear))
                {
                    var fn = Path.GetFileNameWithoutExtension(dialog.FileName);
                    var match = Regex.Match(fn, @"(\d{2,4})[-_]?(\d{2,4})");
                    if (match.Success)
                    {
                        item.FinancialYear = $"{match.Groups[1].Value}-{match.Groups[2].Value}";
                    }
                }
            }
            else
            {
                var newItem = new PreviousYearAccessItem { FilePath = dialog.FileName };
                var fn = Path.GetFileNameWithoutExtension(dialog.FileName);
                var match = Regex.Match(fn, @"(\d{2,4})[-_]?(\d{2,4})");
                if (match.Success)
                {
                    newItem.FinancialYear = $"{match.Groups[1].Value}-{match.Groups[2].Value}";
                }
                PreviousYearsAccess.Add(newItem);
            }
        }
    }

    private void BrowseAccessDatabase(object? parameter)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select Current Microsoft Access Database File",
            Filter = "Access Database (*.accdb;*.mdb)|*.accdb;*.mdb|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            AccessDatabasePath = dialog.FileName;
        }
    }

    private void AddPreviousYearSql(object? parameter)
    {
        PreviousYearsSql.Add(new PreviousYearSqlItem());
    }

    private void RemovePreviousYearSql(object? parameter)
    {
        if (parameter is PreviousYearSqlItem item)
        {
            PreviousYearsSql.Remove(item);
        }
    }

    private void Save(object? parameter)
    {
        _store.Save(new DatabaseSettings
        {
            Provider = SelectedProviderIndex == (int)DatabaseProvider.MicrosoftAccess
                ? DatabaseProvider.MicrosoftAccess.ToString()
                : DatabaseProvider.SqlServer.ToString(),
            AccessDatabasePath = AccessDatabasePath?.Trim() ?? string.Empty,
            SqlServerName = SqlServerName?.Trim() ?? string.Empty,
            SqlDatabaseName = SqlDatabaseName?.Trim() ?? string.Empty,
            SqlUsername = SqlUsername?.Trim() ?? string.Empty,
            SqlPassword = SqlPassword ?? string.Empty,
            PreviousYearsAccess = PreviousYearsAccess
                .Where(x => !string.IsNullOrWhiteSpace(x.FilePath) || !string.IsNullOrWhiteSpace(x.FinancialYear))
                .Select(x => new PreviousYearAccessSetting
                {
                    FinancialYear = x.FinancialYear?.Trim() ?? string.Empty,
                    FilePath = x.FilePath?.Trim() ?? string.Empty
                }).ToList(),
            PreviousYearsSql = PreviousYearsSql
                .Where(x => !string.IsNullOrWhiteSpace(x.DatabaseName) || !string.IsNullOrWhiteSpace(x.FinancialYear))
                .Select(x => new PreviousYearSqlSetting
                {
                    FinancialYear = x.FinancialYear?.Trim() ?? string.Empty,
                    DatabaseName = x.DatabaseName?.Trim() ?? string.Empty
                }).ToList()
        });

        SaveStatusMessage = "Settings saved.";
    }
}
