namespace ConceptJewelleryPro.Data.SqlServer.Configuration;

public sealed class SqlServerOptions
{
    public string ConnectionString { get; set; } = string.Empty;

    public int ConnectTimeoutSeconds { get; set; } = 15;
}
