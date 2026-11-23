using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public class AccessCardSetupRepository : ICardSetupRepository
{
    private readonly string _databasePath;
    private readonly string _connectionString;

    public AccessCardSetupRepository(string databasePath)
    {
        _databasePath = databasePath;
        _connectionString = AccessConnectionProvider.GetConnectionString(databasePath);
    }

    public async Task<IReadOnlyList<CardSetupItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await AccessCardAdjustmentSchemaMigrator.EnsureSchemaAsync(_connectionString, cancellationToken);

        var list = new List<CardSetupItem>();
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = "SELECT CardBank, CardComm, Sbi, [Order] FROM CardSetup ORDER BY [Order] ASC, CardBank ASC";
        await using var cmd = new OleDbCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new CardSetupItem
            {
                CardBank = reader["CardBank"]?.ToString() ?? string.Empty,
                CardComm = reader["CardComm"] is DBNull ? 0m : Convert.ToDecimal(reader["CardComm"]),
                Sbi = reader["Sbi"] is DBNull ? 0m : Convert.ToDecimal(reader["Sbi"]),
                Order = reader["Order"] is DBNull ? 0 : Convert.ToInt32(reader["Order"])
            });
        }

        return list;
    }

    public async Task SaveAllAsync(IEnumerable<CardSetupItem> items, CancellationToken cancellationToken = default)
    {
        await AccessCardAdjustmentSchemaMigrator.EnsureSchemaAsync(_connectionString, cancellationToken);

        var itemList = items
            .Where(x => !string.IsNullOrWhiteSpace(x.CardBank))
            .ToList();

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var transaction = connection.BeginTransaction();
        try
        {
            var currentCards = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using (var cmdCur = connection.CreateCommand())
            {
                cmdCur.Transaction = transaction;
                cmdCur.CommandText = "SELECT CardBank FROM CardSetup";
                using var reader = await cmdCur.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var cb = reader["CardBank"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(cb)) currentCards.Add(cb.Trim());
                }
            }

            var targetCards = new HashSet<string>(itemList.Select(x => x.CardBank.Trim()), StringComparer.OrdinalIgnoreCase);

            foreach (var existing in currentCards)
            {
                if (!targetCards.Contains(existing))
                {
                    using var cmdDel = connection.CreateCommand();
                    cmdDel.Transaction = transaction;
                    cmdDel.CommandText = "DELETE FROM CardSetup WHERE CardBank = ?";
                    cmdDel.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = existing });
                    await cmdDel.ExecuteNonQueryAsync(cancellationToken);
                }
            }

            foreach (var item in itemList)
            {
                var cardBank = item.CardBank.Trim();
                if (currentCards.Contains(cardBank))
                {
                    using var cmdUpd = connection.CreateCommand();
                    cmdUpd.Transaction = transaction;
                    cmdUpd.CommandText = "UPDATE CardSetup SET CardComm = ?, Sbi = ?, [Order] = ? WHERE CardBank = ?";
                    cmdUpd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Currency, Value = item.CardComm });
                    cmdUpd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Currency, Value = item.Sbi });
                    cmdUpd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Integer, Value = item.Order });
                    cmdUpd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = cardBank });
                    await cmdUpd.ExecuteNonQueryAsync(cancellationToken);
                }
                else
                {
                    using var cmdIns = connection.CreateCommand();
                    cmdIns.Transaction = transaction;
                    cmdIns.CommandText = "INSERT INTO CardSetup (CardBank, CardComm, Sbi, [Order]) VALUES (?, ?, ?, ?)";
                    cmdIns.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = cardBank });
                    cmdIns.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Currency, Value = item.CardComm });
                    cmdIns.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Currency, Value = item.Sbi });
                    cmdIns.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Integer, Value = item.Order });
                    await cmdIns.ExecuteNonQueryAsync(cancellationToken);
                }
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}
