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

public sealed class AccessWhatsAppBillTrackerRepository : IWhatsAppBillTrackerRepository
{
    private readonly string _connectionString;

    public AccessWhatsAppBillTrackerRepository(string databasePath)
    {
        _connectionString = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={databasePath};";
    }

    public async Task AddAsync(WhatsAppBillTracker tracker, CancellationToken cancellationToken = default)
    {
        await AccessWhatsAppBillTrackerSchemaMigrator.EnsureSchemaAsync(_connectionString, cancellationToken);

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var status = !string.IsNullOrWhiteSpace(tracker.DeliveryStatus) ? tracker.DeliveryStatus : tracker.Rating;

        try
        {
            const string sql = @"
                INSERT INTO [WhatsApp_Bill_Tracker] ([Wamid], [InvoiceNo], [MobileNumber], [SentAt], [DeliveryStatus], [Rating], [Reply], [UpdatedAt])
                VALUES (?, ?, ?, ?, ?, ?, ?, ?)";

            await using var command = new OleDbCommand(sql, connection);
            AddString(command, tracker.Wamid, 255);
            AddString(command, tracker.InvoiceNo, 100);
            AddString(command, tracker.MobileNumber, 50);
            AddDate(command, tracker.SentAt);
            AddNullableString(command, status, 50);
            AddNullableString(command, tracker.Rating ?? status, 50);
            AddNullableString(command, tracker.Reply, 500);
            AddNullableDate(command, tracker.UpdatedAt);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (OleDbException)
        {
            // Fallback if DeliveryStatus column is not present or other error
            const string fallbackSql = @"
                INSERT INTO [WhatsApp_Bill_Tracker] ([Wamid], [InvoiceNo], [MobileNumber], [SentAt], [Rating], [Reply], [UpdatedAt])
                VALUES (?, ?, ?, ?, ?, ?, ?)";

            await using var fallbackCommand = new OleDbCommand(fallbackSql, connection);
            AddString(fallbackCommand, tracker.Wamid, 255);
            AddString(fallbackCommand, tracker.InvoiceNo, 100);
            AddString(fallbackCommand, tracker.MobileNumber, 50);
            AddDate(fallbackCommand, tracker.SentAt);
            AddNullableString(fallbackCommand, tracker.Rating ?? status, 50);
            AddNullableString(fallbackCommand, tracker.Reply, 500);
            AddNullableDate(fallbackCommand, tracker.UpdatedAt);

            try
            {
                await fallbackCommand.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (OleDbException)
            {
                // Fallback if ID column is not AUTOINCREMENT
                const string manualIdSql = @"
                    INSERT INTO [WhatsApp_Bill_Tracker] ([WhatsAppBillTrackerId], [Wamid], [InvoiceNo], [MobileNumber], [SentAt], [Rating], [Reply], [UpdatedAt])
                    VALUES (?, ?, ?, ?, ?, ?, ?, ?)";

                await using var manualIdCommand = new OleDbCommand(manualIdSql, connection);
                AddInt(manualIdCommand, (int)(DateTime.Now.Ticks % 2147483647));
                AddString(manualIdCommand, tracker.Wamid, 255);
                AddString(manualIdCommand, tracker.InvoiceNo, 100);
                AddString(manualIdCommand, tracker.MobileNumber, 50);
                AddDate(manualIdCommand, tracker.SentAt);
                AddNullableString(manualIdCommand, tracker.Rating ?? status, 50);
                AddNullableString(manualIdCommand, tracker.Reply, 500);
                AddNullableDate(manualIdCommand, tracker.UpdatedAt);

                await manualIdCommand.ExecuteNonQueryAsync(cancellationToken);
            }
        }
    }

    public async Task<WhatsAppBillTracker?> GetByWamidAsync(string wamid, CancellationToken cancellationToken = default)
    {
        await AccessWhatsAppBillTrackerSchemaMigrator.EnsureSchemaAsync(_connectionString, cancellationToken);

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            SELECT *
            FROM [WhatsApp_Bill_Tracker]
            WHERE [Wamid] = ?";

        await using var command = new OleDbCommand(sql, connection);
        AddString(command, wamid, 255);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapTracker(reader);
        }

        return null;
    }

    public async Task<WhatsAppBillTracker?> GetByInvoiceNoAsync(string invoiceNo, CancellationToken cancellationToken = default)
    {
        await AccessWhatsAppBillTrackerSchemaMigrator.EnsureSchemaAsync(_connectionString, cancellationToken);

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            SELECT *
            FROM [WhatsApp_Bill_Tracker]
            WHERE [InvoiceNo] = ?";

        await using var command = new OleDbCommand(sql, connection);
        AddString(command, invoiceNo, 100);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapTracker(reader);
        }

        return null;
    }

    public async Task<IReadOnlyDictionary<string, WhatsAppBillTracker>> GetByInvoiceNosAsync(IEnumerable<string> invoiceNos, CancellationToken cancellationToken = default)
    {
        var list = invoiceNos.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (list.Count == 0)
            return new Dictionary<string, WhatsAppBillTracker>(StringComparer.OrdinalIgnoreCase);

        await AccessWhatsAppBillTrackerSchemaMigrator.EnsureSchemaAsync(_connectionString, cancellationToken);

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var inClause = string.Join(",", list.Select(_ => "?"));
        var sql = $@"
            SELECT *
            FROM [WhatsApp_Bill_Tracker]
            WHERE [InvoiceNo] IN ({inClause})";

        await using var command = new OleDbCommand(sql, connection);
        foreach (var inv in list)
        {
            AddString(command, inv, 100);
        }

        var dict = new Dictionary<string, WhatsAppBillTracker>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var tracker = MapTracker(reader);
            var invKey = tracker.InvoiceNo?.Trim();
            if (!string.IsNullOrWhiteSpace(invKey) && !dict.ContainsKey(invKey))
            {
                dict[invKey] = tracker;
            }
        }

        return dict;
    }

    public async Task UpdateFeedbackAsync(string wamid, string rating, string? reply = null, CancellationToken cancellationToken = default)
    {
        await AccessWhatsAppBillTrackerSchemaMigrator.EnsureSchemaAsync(_connectionString, cancellationToken);

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            UPDATE [WhatsApp_Bill_Tracker]
            SET [Rating] = ?,
                [Reply] = ?,
                [UpdatedAt] = ?
            WHERE [Wamid] = ?";

        await using var command = new OleDbCommand(sql, connection);
        AddNullableString(command, rating, 50);
        AddNullableString(command, reply, 500);
        AddDate(command, DateTime.Now);
        AddString(command, wamid, 255);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateDeliveryStatusAsync(string wamid, string deliveryStatus, CancellationToken cancellationToken = default)
    {
        await AccessWhatsAppBillTrackerSchemaMigrator.EnsureSchemaAsync(_connectionString, cancellationToken);

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        try
        {
            const string sql = @"
                UPDATE [WhatsApp_Bill_Tracker]
                SET [DeliveryStatus] = ?,
                    [Rating] = ?,
                    [UpdatedAt] = ?
                WHERE [Wamid] = ?";

            await using var command = new OleDbCommand(sql, connection);
            AddNullableString(command, deliveryStatus, 50);
            AddNullableString(command, deliveryStatus, 50);
            AddDate(command, DateTime.Now);
            AddString(command, wamid, 255);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (OleDbException)
        {
            const string fallbackSql = @"
                UPDATE [WhatsApp_Bill_Tracker]
                SET [Rating] = ?,
                    [UpdatedAt] = ?
                WHERE [Wamid] = ?";

            await using var fallbackCommand = new OleDbCommand(fallbackSql, connection);
            AddNullableString(fallbackCommand, deliveryStatus, 50);
            AddDate(fallbackCommand, DateTime.Now);
            AddString(fallbackCommand, wamid, 255);

            await fallbackCommand.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<WhatsAppBillTracker>> GetTrackersForStatusSyncAsync(int daysBack = 14, CancellationToken cancellationToken = default)
    {
        await AccessWhatsAppBillTrackerSchemaMigrator.EnsureSchemaAsync(_connectionString, cancellationToken);

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var cutoffDate = DateTime.Today.AddDays(-daysBack);

        const string sql = @"
            SELECT TOP 100 *
            FROM [WhatsApp_Bill_Tracker]
            WHERE [SentAt] >= ?
              AND [Wamid] IS NOT NULL
            ORDER BY [SentAt] DESC";

        var list = new List<WhatsAppBillTracker>();
        try
        {
            await using var command = new OleDbCommand(sql, connection);
            AddDate(command, cutoffDate);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var tracker = MapTracker(reader);
                if (!string.IsNullOrWhiteSpace(tracker.Wamid))
                {
                    var st = tracker.DeliveryStatus ?? tracker.Rating;
                    if (!string.Equals(st, "Read", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(st, "Failed", StringComparison.OrdinalIgnoreCase))
                    {
                        list.Add(tracker);
                    }
                }
            }
        }
        catch
        {
            // Non-fatal if query fails
        }

        return list;
    }

    private static WhatsAppBillTracker MapTracker(IDataRecord reader)
    {
        string? deliveryStatus = null;
        try
        {
            var val = reader["DeliveryStatus"];
            if (val is not DBNull && val != null)
            {
                deliveryStatus = val.ToString();
            }
        }
        catch
        {
            // Column may not be present in old recordset
        }

        var rating = reader["Rating"] is DBNull or null ? null : reader["Rating"].ToString();

        return new WhatsAppBillTracker
        {
            WhatsAppBillTrackerId = reader["WhatsAppBillTrackerId"] is DBNull or null ? 0L : Convert.ToInt64(reader["WhatsAppBillTrackerId"]),
            Wamid = reader["Wamid"] is DBNull or null ? string.Empty : reader["Wamid"].ToString() ?? string.Empty,
            InvoiceNo = reader["InvoiceNo"] is DBNull or null ? string.Empty : reader["InvoiceNo"].ToString() ?? string.Empty,
            MobileNumber = reader["MobileNumber"] is DBNull or null ? string.Empty : reader["MobileNumber"].ToString() ?? string.Empty,
            SentAt = reader["SentAt"] is DBNull or null ? DateTime.MinValue : Convert.ToDateTime(reader["SentAt"]),
            DeliveryStatus = !string.IsNullOrWhiteSpace(deliveryStatus) ? deliveryStatus : rating,
            Rating = rating,
            Reply = reader["Reply"] is DBNull or null ? null : reader["Reply"].ToString(),
            UpdatedAt = reader["UpdatedAt"] is DBNull or null ? null : Convert.ToDateTime(reader["UpdatedAt"])
        };
    }

    private static void AddString(OleDbCommand command, string? value, int size)
    {
        var parameter = command.Parameters.Add("?", OleDbType.VarWChar, size);
        parameter.Value = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static void AddNullableString(OleDbCommand command, string? value, int size)
    {
        var parameter = command.Parameters.Add("?", OleDbType.VarWChar, size);
        parameter.Value = string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();
    }

    private static void AddInt(OleDbCommand command, int value)
    {
        var parameter = command.Parameters.Add("?", OleDbType.Integer);
        parameter.Value = value;
    }

    private static void AddDate(OleDbCommand command, DateTime value)
    {
        var parameter = command.Parameters.Add("?", OleDbType.Date);
        parameter.Value = value == DateTime.MinValue ? DBNull.Value : value;
    }

    private static void AddNullableDate(OleDbCommand command, DateTime? value)
    {
        var parameter = command.Parameters.Add("?", OleDbType.Date);
        parameter.Value = value.HasValue ? value.Value : DBNull.Value;
    }
}
