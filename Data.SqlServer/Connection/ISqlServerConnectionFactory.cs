using System.Data.Common;

namespace ConceptJewelleryPro.Data.SqlServer.Connection;

public interface ISqlServerConnectionFactory
{
    DbConnection CreateConnection();

    Task<DbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default);
}
