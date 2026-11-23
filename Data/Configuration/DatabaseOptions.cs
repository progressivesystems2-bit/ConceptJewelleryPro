namespace ConceptJewelleryPro.Data.Configuration;

public sealed class DatabaseOptions
{
    public string Provider { get; set; } = "SqlServer";

    public string ConnectionString { get; set; } = string.Empty;
}
