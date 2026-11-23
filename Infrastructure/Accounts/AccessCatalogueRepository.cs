using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.OleDb;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

/// <summary>
/// Queries the <c>BarcodeSubform</c> table to serve the catalogue.
/// Returns active unsold items.
/// </summary>
public sealed class AccessCatalogueRepository : ICatalogueRepository
{
    private readonly string _connectionString;
    private readonly DemoCatalogueRepository _demoRepo = new();

    public AccessCatalogueRepository(string databasePath)
    {
        _connectionString = AccessConnectionProvider.GetConnectionString(databasePath);
    }

    public async Task<IReadOnlyList<CatalogueItem>> GetCatalogueAsync(
        CatalogueFilter filter,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new OleDbConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            // Fetch sold and converted barcodes to only show in-stock items
            var (soldBarcodes, convertedBarcodes) = await GetExcludedBarcodesAsync(connection, cancellationToken);

            var (sql, parameters) = BuildQuery(filter);

            await using var command = new OleDbCommand(sql, connection);
            foreach (var (name, type, size, value) in parameters)
            {
                var p = size > 0 ? command.Parameters.Add(name, type, size) : command.Parameters.Add(name, type);
                p.Value = value ?? DBNull.Value;
            }

            var list = new List<CatalogueItem>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var item = Map(reader);
                if (!string.IsNullOrEmpty(item.Barcode))
                {
                    if (convertedBarcodes.Contains(item.Barcode))
                    {
                        continue;
                    }

                    var isSold = soldBarcodes.Contains(item.Barcode);
                    item.IsSold = isSold;

                    if (!filter.IncludeSold && isSold)
                    {
                        continue;
                    }
                }
                list.Add(item);
            }

            return list;
        }
        catch
        {
            return await _demoRepo.GetCatalogueAsync(filter, cancellationToken);
        }
    }

    public async Task<IReadOnlyList<string>> GetAvailableItemNamesAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT DISTINCT [ItemName]
            FROM   [BarcodeSubform]
            WHERE  ([Status] IS NULL OR [Status] <> 'Deleted')
                   AND [ItemName] IS NOT NULL AND [ItemName] <> ''
            ORDER BY [ItemName]";

        var list = new List<string>();
        try
        {
            await using var connection = new OleDbConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new OleDbCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var name = reader["ItemName"] is DBNull ? string.Empty : reader["ItemName"]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(name))
                {
                    list.Add(name);
                }
            }
        }
        catch
        {
            // Non-critical
        }
        return list;
    }

    public async Task<IReadOnlyList<string>> GetAvailableSizesAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT DISTINCT [Size]
            FROM   [BarcodeSubform]
            WHERE  ([Status] IS NULL OR [Status] <> 'Deleted')
                   AND [Size] IS NOT NULL AND [Size] <> '' AND [Size] <> '-'
            ORDER BY [Size]";

        var list = new List<string>();
        try
        {
            await using var connection = new OleDbConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new OleDbCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var size = reader["Size"] is DBNull ? string.Empty : reader["Size"]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(size))
                {
                    list.Add(size);
                }
            }
        }
        catch
        {
            // Non-critical
        }
        return list;
    }

    private static async Task<(HashSet<string> Sold, HashSet<string> Converted)> GetExcludedBarcodesAsync(
        OleDbConnection connection,
        CancellationToken cancellationToken)
    {
        var sold = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var converted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            await using var cmdSold = new OleDbCommand("SELECT DISTINCT [Barcode] FROM [BillSubform] WHERE [Barcode] IS NOT NULL AND [Barcode] <> ''", connection);
            await using var rSold = await cmdSold.ExecuteReaderAsync(cancellationToken);
            while (await rSold.ReadAsync(cancellationToken))
            {
                var bc = rSold[0]?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(bc)) sold.Add(bc);
            }
        }
        catch { }

        try
        {
            await using var cmdConv = new OleDbCommand("SELECT DISTINCT [Barcode] FROM [BarcodeConversion] WHERE [Barcode] IS NOT NULL AND [Barcode] <> ''", connection);
            await using var rConv = await cmdConv.ExecuteReaderAsync(cancellationToken);
            while (await rConv.ReadAsync(cancellationToken))
            {
                var bc = rConv[0]?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(bc)) converted.Add(bc);
            }
        }
        catch { }

        return (sold, converted);
    }

    // ── Query builder ────────────────────────────────────────────────────────

    private static (string Sql, List<(string Name, OleDbType Type, int Size, object Value)> Params)
        BuildQuery(CatalogueFilter filter)
    {
        var sb = new StringBuilder(@"
            SELECT [Barcode], [ItemName], [GrossWt], [NetWt], [StoneWt], [Description],
                   [Size], [Purity], [Photo]
            FROM   [BarcodeSubform]
            WHERE  ([Status] IS NULL OR [Status] <> 'Deleted')");

        var parms = new List<(string, OleDbType, int, object)>();

        if (!string.IsNullOrWhiteSpace(filter.Barcode))
        {
            var raw = filter.Barcode.Trim();
            var padded = int.TryParse(raw, out var n) ? n.ToString("D6") : raw;
            sb.Append(" AND ([Barcode] = ? OR [Barcode] = ? OR [Barcode] LIKE ?)");
            parms.Add(("@Barcode1", OleDbType.VarWChar, 50, raw));
            parms.Add(("@Barcode2", OleDbType.VarWChar, 50, padded));
            parms.Add(("@Barcode3", OleDbType.VarWChar, 50, $"%{raw}%"));
        }

        if (!string.IsNullOrWhiteSpace(filter.ItemName))
        {
            var item = filter.ItemName.Trim();
            var singular = item.EndsWith("s", StringComparison.OrdinalIgnoreCase) && item.Length > 3
                ? item.Substring(0, item.Length - 1)
                : item;

            sb.Append(" AND ([ItemName] = ? OR [ItemName] LIKE ? OR [ItemName] LIKE ?)");
            parms.Add(("@ItemName1", OleDbType.VarWChar, 150, item));
            parms.Add(("@ItemName2", OleDbType.VarWChar, 150, $"%{item}%"));
            parms.Add(("@ItemName3", OleDbType.VarWChar, 150, $"%{singular}%"));
        }

        if (!string.IsNullOrWhiteSpace(filter.Size))
        {
            var size = filter.Size.Trim();
            sb.Append(" AND ([Size] = ? OR [Size] LIKE ?)");
            parms.Add(("@Size1", OleDbType.VarWChar, 50, size));
            parms.Add(("@Size2", OleDbType.VarWChar, 50, $"%{size}%"));
        }

        if (!string.IsNullOrWhiteSpace(filter.Description))
        {
            var desc = filter.Description.Trim();
            sb.Append(" AND ([Description] LIKE ? OR [Description2] LIKE ?)");
            parms.Add(("@Desc1", OleDbType.VarWChar, 255, $"%{desc}%"));
            parms.Add(("@Desc2", OleDbType.VarWChar, 255, $"%{desc}%"));
        }

        if (filter.FromWt.HasValue && filter.FromWt.Value > 0)
        {
            sb.Append(" AND [GrossWt] >= ?");
            parms.Add(("@FromWt", OleDbType.Double, 0, Convert.ToDouble(filter.FromWt.Value)));
        }

        if (filter.ToWt.HasValue && filter.ToWt.Value > 0)
        {
            sb.Append(" AND [GrossWt] <= ?");
            parms.Add(("@ToWt", OleDbType.Double, 0, Convert.ToDouble(filter.ToWt.Value)));
        }

        sb.Append(" ORDER BY [ItemName], [Barcode]");

        return (sb.ToString(), parms);
    }

    // ── Mapping ──────────────────────────────────────────────────────────────

    private static CatalogueItem Map(DbDataReader reader)
    {
        return new CatalogueItem
        {
            Barcode     = GetString(reader, "Barcode"),
            ItemName    = GetString(reader, "ItemName"),
            GrossWt     = GetDecimal(reader, "GrossWt", "GrWt"),
            NetWt       = GetDecimal(reader, "NetWt", "NtWt"),
            StoneWt     = GetDecimal(reader, "StoneWt", "LessWt"),
            Description = GetString(reader, "Description"),
            Size        = GetString(reader, "Size"),
            Purity      = GetString(reader, "Purity"),
            Photo       = GetString(reader, "Photo")
        };
    }

    private static string GetString(DbDataReader r, string col)
    {
        try
        {
            var idx = FindOrdinal(r, col);
            if (idx >= 0 && !r.IsDBNull(idx))
            {
                return Convert.ToString(r.GetValue(idx))?.Trim() ?? string.Empty;
            }
        }
        catch { }
        return string.Empty;
    }

    private static decimal GetDecimal(DbDataReader r, params string[] cols)
    {
        foreach (var col in cols)
        {
            try
            {
                var idx = FindOrdinal(r, col);
                if (idx >= 0 && !r.IsDBNull(idx))
                {
                    return Convert.ToDecimal(r.GetValue(idx));
                }
            }
            catch { }
        }
        return 0m;
    }

    private static int FindOrdinal(DbDataReader r, string columnName)
    {
        for (var i = 0; i < r.FieldCount; i++)
        {
            if (string.Equals(r.GetName(i), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }
        return -1;
    }
}
