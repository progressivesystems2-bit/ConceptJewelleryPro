namespace ConceptJewelleryPro.Shared;

public sealed class PreviousYearAccessSetting
{
    public string FinancialYear { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
}

public sealed class PreviousYearSqlSetting
{
    public string FinancialYear { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
}

public sealed class DatabaseSettings
{
    public string Provider { get; set; } = "SqlServer";

    public string AccessDatabasePath { get; set; } = string.Empty;

    public string SqlServerName { get; set; } = string.Empty;

    public string SqlDatabaseName { get; set; } = "ConceptJewelleryPro";

    public string SqlUsername { get; set; } = string.Empty;

    public string SqlPassword { get; set; } = string.Empty;

    public List<PreviousYearAccessSetting> PreviousYearsAccess { get; set; } = new();

    public List<PreviousYearSqlSetting> PreviousYearsSql { get; set; } = new();
}
