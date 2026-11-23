using System.Data.OleDb;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

/// <summary>
/// Logs customer jewellery requirements into the <c>CustomerRequirement</c> Access table.
/// The table is auto-created if it does not exist (schema migration on first use).
/// </summary>
public sealed class AccessCustomerRequirementRepository : ICustomerRequirementRepository
{
    private readonly string _connectionString;

    public AccessCustomerRequirementRepository(string databasePath)
    {
        _connectionString = AccessConnectionProvider.GetConnectionString(databasePath);
    }

    /// <inheritdoc/>
    public async Task<int> LogAsync(CustomerRequirement requirement, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // First attempt inserting with ImagePath and OrderCategory columns
        try
        {
            const string insertWithImageAndCatSql = @"
                INSERT INTO [CustomerRequirement]
                    ([EntryDate], [CustomerName], [CustomerMobile], [ItemName],
                     [Description], [Size], [WeightRange], [Purity], [Remarks], [EntryUser], [ImagePath], [OrderCategory])
                VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)";

            await using var cmd = new OleDbCommand(insertWithImageAndCatSql, connection);
            cmd.Parameters.Add("@EntryDate",       OleDbType.Date).Value          = requirement.EntryDate;
            cmd.Parameters.Add("@CustomerName",    OleDbType.VarWChar, 100).Value = requirement.CustomerName;
            cmd.Parameters.Add("@CustomerMobile",  OleDbType.VarWChar, 20).Value  = NullIfEmpty(requirement.CustomerMobile);
            cmd.Parameters.Add("@ItemName",        OleDbType.VarWChar, 150).Value = requirement.ItemName;
            cmd.Parameters.Add("@Description",     OleDbType.LongVarWChar).Value  = NullIfEmpty(requirement.Description);
            cmd.Parameters.Add("@Size",            OleDbType.VarWChar, 50).Value  = NullIfEmpty(requirement.Size);
            cmd.Parameters.Add("@WeightRange",     OleDbType.VarWChar, 50).Value  = NullIfEmpty(requirement.WeightRange);
            cmd.Parameters.Add("@Purity",          OleDbType.VarWChar, 50).Value  = NullIfEmpty(requirement.Purity);
            cmd.Parameters.Add("@Remarks",         OleDbType.LongVarWChar).Value  = NullIfEmpty(requirement.Remarks);
            cmd.Parameters.Add("@EntryUser",       OleDbType.VarWChar, 50).Value  = requirement.EntryUser;
            cmd.Parameters.Add("@ImagePath",       OleDbType.VarWChar, 255).Value = NullIfEmpty(requirement.ImagePath);
            cmd.Parameters.Add("@OrderCategory",   OleDbType.VarWChar, 50).Value  = NullIfEmpty(requirement.OrderCategory);

            await cmd.ExecuteNonQueryAsync(cancellationToken);

            await using var idCmd = new OleDbCommand("SELECT @@IDENTITY", connection);
            var result = await idCmd.ExecuteScalarAsync(cancellationToken);
            return result is DBNull || result is null ? 0 : Convert.ToInt32(result);
        }
        catch (OleDbException)
        {
            // Fallback for databases without OrderCategory column
            try
            {
                const string insertWithImageSql = @"
                    INSERT INTO [CustomerRequirement]
                        ([EntryDate], [CustomerName], [CustomerMobile], [ItemName],
                         [Description], [Size], [WeightRange], [Purity], [Remarks], [EntryUser], [ImagePath])
                    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)";

                await using var cmd = new OleDbCommand(insertWithImageSql, connection);
                cmd.Parameters.Add("@EntryDate",       OleDbType.Date).Value          = requirement.EntryDate;
                cmd.Parameters.Add("@CustomerName",    OleDbType.VarWChar, 100).Value = requirement.CustomerName;
                cmd.Parameters.Add("@CustomerMobile",  OleDbType.VarWChar, 20).Value  = NullIfEmpty(requirement.CustomerMobile);
                cmd.Parameters.Add("@ItemName",        OleDbType.VarWChar, 150).Value = requirement.ItemName;
                cmd.Parameters.Add("@Description",     OleDbType.LongVarWChar).Value  = NullIfEmpty(requirement.Description);
                cmd.Parameters.Add("@Size",            OleDbType.VarWChar, 50).Value  = NullIfEmpty(requirement.Size);
                cmd.Parameters.Add("@WeightRange",     OleDbType.VarWChar, 50).Value  = NullIfEmpty(requirement.WeightRange);
                cmd.Parameters.Add("@Purity",          OleDbType.VarWChar, 50).Value  = NullIfEmpty(requirement.Purity);
                cmd.Parameters.Add("@Remarks",         OleDbType.LongVarWChar).Value  = NullIfEmpty(requirement.Remarks);
                cmd.Parameters.Add("@EntryUser",       OleDbType.VarWChar, 50).Value  = requirement.EntryUser;
                cmd.Parameters.Add("@ImagePath",       OleDbType.VarWChar, 255).Value = NullIfEmpty(requirement.ImagePath);

                await cmd.ExecuteNonQueryAsync(cancellationToken);

                await using var idCmd = new OleDbCommand("SELECT @@IDENTITY", connection);
                var result = await idCmd.ExecuteScalarAsync(cancellationToken);
                return result is DBNull || result is null ? 0 : Convert.ToInt32(result);
            }
            catch (OleDbException)
            {
                // Fallback for existing databases where neither column exists
                const string fallbackSql = @"
                    INSERT INTO [CustomerRequirement]
                        ([EntryDate], [CustomerName], [CustomerMobile], [ItemName],
                         [Description], [Size], [WeightRange], [Purity], [Remarks], [EntryUser])
                    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)";

                await using var cmd = new OleDbCommand(fallbackSql, connection);
                cmd.Parameters.Add("@EntryDate",       OleDbType.Date).Value          = requirement.EntryDate;
                cmd.Parameters.Add("@CustomerName",    OleDbType.VarWChar, 100).Value = requirement.CustomerName;
                cmd.Parameters.Add("@CustomerMobile",  OleDbType.VarWChar, 20).Value  = NullIfEmpty(requirement.CustomerMobile);
                cmd.Parameters.Add("@ItemName",        OleDbType.VarWChar, 150).Value = requirement.ItemName;
                cmd.Parameters.Add("@Description",     OleDbType.LongVarWChar).Value  = NullIfEmpty(requirement.Description);
                cmd.Parameters.Add("@Size",            OleDbType.VarWChar, 50).Value  = NullIfEmpty(requirement.Size);
                cmd.Parameters.Add("@WeightRange",     OleDbType.VarWChar, 50).Value  = NullIfEmpty(requirement.WeightRange);
                cmd.Parameters.Add("@Purity",          OleDbType.VarWChar, 50).Value  = NullIfEmpty(requirement.Purity);
                cmd.Parameters.Add("@Remarks",         OleDbType.LongVarWChar).Value  = NullIfEmpty(requirement.Remarks);
                cmd.Parameters.Add("@EntryUser",       OleDbType.VarWChar, 50).Value  = requirement.EntryUser;

                await cmd.ExecuteNonQueryAsync(cancellationToken);

                await using var idCmd = new OleDbCommand("SELECT @@IDENTITY", connection);
                var result = await idCmd.ExecuteScalarAsync(cancellationToken);
                return result is DBNull || result is null ? 0 : Convert.ToInt32(result);
            }
        }
    }

    // ── Schema migration ─────────────────────────────────────────────────────

    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        bool tableExists = await TableExistsBySelectAsync(connection, cancellationToken);

        if (tableExists)
        {
            // Attempt adding ImagePath column if it doesn't already exist
            try
            {
                await using var alterCmd = new OleDbCommand("ALTER TABLE [CustomerRequirement] ADD COLUMN [ImagePath] TEXT(255)", connection);
                await alterCmd.ExecuteNonQueryAsync(cancellationToken);
            }
            catch { }

            // Attempt adding OrderCategory column if it doesn't already exist
            try
            {
                await using var alterCatCmd = new OleDbCommand("ALTER TABLE [CustomerRequirement] ADD COLUMN [OrderCategory] TEXT(50)", connection);
                await alterCatCmd.ExecuteNonQueryAsync(cancellationToken);
            }
            catch { }

            return;
        }

        const string createSql = @"
            CREATE TABLE [CustomerRequirement] (
                [Id]             AUTOINCREMENT PRIMARY KEY,
                [EntryDate]      DATETIME,
                [CustomerName]   TEXT(100),
                [CustomerMobile] TEXT(20),
                [ItemName]       TEXT(150),
                [Description]    MEMO,
                [Size]           TEXT(50),
                [WeightRange]    TEXT(50),
                [Purity]         TEXT(50),
                [Remarks]        MEMO,
                [EntryUser]      TEXT(50),
                [ImagePath]      TEXT(255),
                [OrderCategory]  TEXT(50)
            )";

        await using var createCmd = new OleDbCommand(createSql, connection);
        await createCmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<bool> TableExistsBySelectAsync(OleDbConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            await using var cmd = new OleDbCommand("SELECT TOP 1 [Id] FROM [CustomerRequirement]", connection);
            await cmd.ExecuteScalarAsync(cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static object NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();
}
