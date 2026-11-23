using System.Data.Common;
using System.Data.OleDb;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class AccessSalesVouTypeRepository : ISalesVouTypeRepository
{
    private readonly string _connectionString;

    public AccessSalesVouTypeRepository(string databasePath)
    {
        _connectionString = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={databasePath};";
    }

    public async Task<IReadOnlyList<SalesVouType>> GetAllAsync(string? headCategory = null, bool includeHidden = true, CancellationToken cancellationToken = default)
    {
        var sql = "SELECT [VouType], [ShortName], [SalesAccount], [VatAccount], [CardCommision], [CardBank], [CommAc], [Vat%], [Note], [Status], [HeadCategory], [Hide], [ItemHead], [PrintFile], [ItemPurchased], [ItemConvertedTo], [CounterNo] FROM [SALESVOUTYPE]";

        var conditions = new List<string>();
        if (!includeHidden)
        {
            conditions.Add("([Hide] = False OR [Hide] IS NULL)");
        }
        if (!string.IsNullOrWhiteSpace(headCategory) && !string.Equals(headCategory, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            conditions.Add("[HeadCategory] = ?");
        }

        if (conditions.Count > 0)
        {
            sql += " WHERE " + string.Join(" AND ", conditions);
        }

        sql += " ORDER BY [HeadCategory], [VouType]";

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        if (!string.IsNullOrWhiteSpace(headCategory) && !string.Equals(headCategory, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            command.Parameters.Add("@HeadCategory", OleDbType.VarWChar, 50).Value = headCategory.Trim();
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var list = new List<SalesVouType>();
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(Map(reader));
        }

        return list;
    }

    public async Task<SalesVouType?> GetByVouTypeAsync(string vouType, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT TOP 1 [VouType], [ShortName], [SalesAccount], [VatAccount], [CardCommision], [CardBank], [CommAc], [Vat%], [Note], [Status], [HeadCategory], [Hide], [ItemHead], [PrintFile], [ItemPurchased], [ItemConvertedTo], [CounterNo] FROM [SALESVOUTYPE] WHERE [VouType] = ?";

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        command.Parameters.Add("@VouType", OleDbType.VarWChar, 50).Value = vouType.Trim();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return Map(reader);
    }

    public async Task AddAsync(SalesVouType entity, CancellationToken cancellationToken = default)
    {
        const string sql = @"INSERT INTO [SALESVOUTYPE] 
            ([VouType], [ShortName], [SalesAccount], [VatAccount], [CardCommision], [CardBank], [CommAc], [Vat%], [Note], [Status], [HeadCategory], [Hide], [ItemHead], [PrintFile], [ItemPurchased], [ItemConvertedTo], [CounterNo]) 
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)";

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        command.Parameters.Add("@VouType", OleDbType.VarWChar, 50).Value = entity.VouType?.Trim() ?? string.Empty;
        command.Parameters.Add("@ShortName", OleDbType.VarWChar, 4).Value = entity.ShortName?.Trim() ?? string.Empty;
        command.Parameters.Add("@SalesAccount", OleDbType.VarWChar, 50).Value = NullIfEmpty(entity.SalesAccount);
        command.Parameters.Add("@VatAccount", OleDbType.VarWChar, 50).Value = NullIfEmpty(entity.VatAccount);
        command.Parameters.Add("@CardCommision", OleDbType.Currency).Value = entity.CardCommission;
        command.Parameters.Add("@CardBank", OleDbType.VarWChar, 50).Value = NullIfEmpty(entity.CardBank);
        command.Parameters.Add("@CommAc", OleDbType.VarWChar, 50).Value = NullIfEmpty(entity.CommAc);
        command.Parameters.Add("@VatPercentage", OleDbType.Currency).Value = entity.VatPercentage;
        command.Parameters.Add("@Note", OleDbType.VarWChar, 50).Value = NullIfEmpty(entity.Note);
        command.Parameters.Add("@Status", OleDbType.VarWChar, 15).Value = NullIfEmpty(entity.Status);
        command.Parameters.Add("@HeadCategory", OleDbType.VarWChar, 50).Value = NullIfEmpty(entity.HeadCategory);
        command.Parameters.Add("@Hide", OleDbType.Boolean).Value = entity.Hide;
        command.Parameters.Add("@ItemHead", OleDbType.VarWChar, 50).Value = NullIfEmpty(entity.ItemHead);
        command.Parameters.Add("@PrintFile", OleDbType.VarWChar, 100).Value = NullIfEmpty(entity.PrintFile);
        command.Parameters.Add("@ItemPurchased", OleDbType.VarWChar, 50).Value = NullIfEmpty(entity.ItemPurchased);
        command.Parameters.Add("@ItemConvertedTo", OleDbType.VarWChar, 50).Value = NullIfEmpty(entity.ItemConvertedTo);
        command.Parameters.Add("@CounterNo", OleDbType.VarWChar, 50).Value = NullIfEmpty(entity.CounterNo);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAsync(string originalVouType, SalesVouType entity, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(originalVouType, entity.VouType, StringComparison.OrdinalIgnoreCase))
        {
            await DeleteAsync(originalVouType, cancellationToken);
            await AddAsync(entity, cancellationToken);
            return;
        }

        const string sql = @"UPDATE [SALESVOUTYPE] SET 
            [ShortName] = ?, 
            [SalesAccount] = ?, 
            [VatAccount] = ?, 
            [CardCommision] = ?, 
            [CardBank] = ?, 
            [CommAc] = ?, 
            [Vat%] = ?, 
            [Note] = ?, 
            [Status] = ?, 
            [HeadCategory] = ?, 
            [Hide] = ?, 
            [ItemHead] = ?, 
            [PrintFile] = ?, 
            [ItemPurchased] = ?, 
            [ItemConvertedTo] = ?, 
            [CounterNo] = ? 
            WHERE [VouType] = ?";

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        command.Parameters.Add("@ShortName", OleDbType.VarWChar, 4).Value = entity.ShortName?.Trim() ?? string.Empty;
        command.Parameters.Add("@SalesAccount", OleDbType.VarWChar, 50).Value = NullIfEmpty(entity.SalesAccount);
        command.Parameters.Add("@VatAccount", OleDbType.VarWChar, 50).Value = NullIfEmpty(entity.VatAccount);
        command.Parameters.Add("@CardCommision", OleDbType.Currency).Value = entity.CardCommission;
        command.Parameters.Add("@CardBank", OleDbType.VarWChar, 50).Value = NullIfEmpty(entity.CardBank);
        command.Parameters.Add("@CommAc", OleDbType.VarWChar, 50).Value = NullIfEmpty(entity.CommAc);
        command.Parameters.Add("@VatPercentage", OleDbType.Currency).Value = entity.VatPercentage;
        command.Parameters.Add("@Note", OleDbType.VarWChar, 50).Value = NullIfEmpty(entity.Note);
        command.Parameters.Add("@Status", OleDbType.VarWChar, 15).Value = NullIfEmpty(entity.Status);
        command.Parameters.Add("@HeadCategory", OleDbType.VarWChar, 50).Value = NullIfEmpty(entity.HeadCategory);
        command.Parameters.Add("@Hide", OleDbType.Boolean).Value = entity.Hide;
        command.Parameters.Add("@ItemHead", OleDbType.VarWChar, 50).Value = NullIfEmpty(entity.ItemHead);
        command.Parameters.Add("@PrintFile", OleDbType.VarWChar, 100).Value = NullIfEmpty(entity.PrintFile);
        command.Parameters.Add("@ItemPurchased", OleDbType.VarWChar, 50).Value = NullIfEmpty(entity.ItemPurchased);
        command.Parameters.Add("@ItemConvertedTo", OleDbType.VarWChar, 50).Value = NullIfEmpty(entity.ItemConvertedTo);
        command.Parameters.Add("@CounterNo", OleDbType.VarWChar, 50).Value = NullIfEmpty(entity.CounterNo);
        command.Parameters.Add("@OriginalVouType", OleDbType.VarWChar, 50).Value = originalVouType.Trim();

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(string vouType, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM [SALESVOUTYPE] WHERE [VouType] = ?";

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        command.Parameters.Add("@VouType", OleDbType.VarWChar, 50).Value = vouType.Trim();

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetHeadCategoriesAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT DISTINCT [HeadCategory] FROM [SALESVOUTYPE] WHERE [HeadCategory] IS NOT NULL AND [HeadCategory] <> '' ORDER BY [HeadCategory]";

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var list = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var cat = reader[0]?.ToString()?.Trim();
            if (!string.IsNullOrWhiteSpace(cat))
            {
                list.Add(cat);
            }
        }

        var defaultCats = new[] { "Sales", "Purchases", "Sales Return", "Purchase Return", "Estimate", "Approval", "Issue", "Receive", "Others" };
        foreach (var dc in defaultCats)
        {
            if (!list.Contains(dc, StringComparer.OrdinalIgnoreCase))
            {
                list.Add(dc);
            }
        }

        return list.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(c => c).ToList();
    }

    public async Task<IReadOnlyList<string>> GetPrintFilesAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT DISTINCT [PrintFile] FROM [SALESVOUTYPE] WHERE [PrintFile] IS NOT NULL AND [PrintFile] <> '' ORDER BY [PrintFile]";

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var list = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var pf = reader[0]?.ToString()?.Trim();
            if (!string.IsNullOrWhiteSpace(pf))
            {
                list.Add(pf);
            }
        }

        var defaultTemplates = new[] { "BillPrint", "BillPrintSilver", "BillPrintDiamond", "Tattva", "PurchasePrint2", "PurchaseReturnPrint", "SalesReturnPrint" };
        foreach (var dt in defaultTemplates)
        {
            if (!list.Contains(dt, StringComparer.OrdinalIgnoreCase))
            {
                list.Add(dt);
            }
        }

        return list.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p => p).ToList();
    }

    private static SalesVouType Map(DbDataReader reader)
    {
        return new SalesVouType
        {
            VouType = Convert.ToString(reader["VouType"])?.Trim() ?? string.Empty,
            ShortName = Convert.ToString(reader["ShortName"])?.Trim() ?? string.Empty,
            SalesAccount = Convert.ToString(reader["SalesAccount"])?.Trim() ?? string.Empty,
            VatAccount = Convert.ToString(reader["VatAccount"])?.Trim() ?? string.Empty,
            CardCommission = reader["CardCommision"] is DBNull ? 0m : Convert.ToDecimal(reader["CardCommision"]),
            CardBank = Convert.ToString(reader["CardBank"])?.Trim() ?? string.Empty,
            CommAc = Convert.ToString(reader["CommAc"])?.Trim() ?? string.Empty,
            VatPercentage = reader["Vat%"] is DBNull ? 0m : Convert.ToDecimal(reader["Vat%"]),
            Note = Convert.ToString(reader["Note"])?.Trim() ?? string.Empty,
            Status = Convert.ToString(reader["Status"])?.Trim() ?? string.Empty,
            HeadCategory = Convert.ToString(reader["HeadCategory"])?.Trim() ?? string.Empty,
            Hide = reader["Hide"] is not DBNull && Convert.ToBoolean(reader["Hide"]),
            ItemHead = Convert.ToString(reader["ItemHead"])?.Trim() ?? string.Empty,
            PrintFile = Convert.ToString(reader["PrintFile"])?.Trim() ?? string.Empty,
            ItemPurchased = Convert.ToString(reader["ItemPurchased"])?.Trim() ?? string.Empty,
            ItemConvertedTo = Convert.ToString(reader["ItemConvertedTo"])?.Trim() ?? string.Empty,
            CounterNo = Convert.ToString(reader["CounterNo"])?.Trim() ?? string.Empty
        };
    }

    private static object NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();
}
