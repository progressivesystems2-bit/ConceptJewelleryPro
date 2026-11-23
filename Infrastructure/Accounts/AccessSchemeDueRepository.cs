using System.Data;
using System.Data.OleDb;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class AccessSchemeDueRepository : ISchemeDueRepository
{
    private readonly string _connectionString;

    public AccessSchemeDueRepository(string databasePath)
    {
        _connectionString = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={databasePath};";
    }

    public async Task<IReadOnlyList<SchemeDueListItem>> GetSchemeDueListAsync(DateTime asOnDate, CancellationToken cancellationToken = default)
    {
        await AccessSchemeCreationSchemaMigrator.EnsureSchemaAsync(_connectionString, cancellationToken);
        var result = new List<SchemeDueListItem>();

        // 1. Load active schemes
        const string schemesSql = @"
            SELECT *
            FROM SchemeCreation
            WHERE (Status IS NULL OR Status <> 'Deleted')
            ORDER BY Nos DESC";

        var schemes = new List<SchemeCreationItem>();

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using (var cmd = new OleDbCommand(schemesSql, connection))
        await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                schemes.Add(new SchemeCreationItem
                {
                    Scode = reader["Scode"]?.ToString()?.Trim() ?? string.Empty,
                    Nos = reader["Nos"] != DBNull.Value ? Convert.ToInt32(reader["Nos"]) : 0,
                    CustomerName = reader["CustomerName"]?.ToString()?.Trim() ?? string.Empty,
                    InstallmentAmt = reader["InstallmentAmt"] != DBNull.Value ? Convert.ToDecimal(reader["InstallmentAmt"]) : 0m,
                    Month = reader["Month"] != DBNull.Value ? Convert.ToDecimal(reader["Month"]) : 11m,
                    TotalAmt = reader["TotalAmt"] != DBNull.Value ? Convert.ToDecimal(reader["TotalAmt"]) : 0m,
                    SchemeDiscount = reader["SchemeDiscount"] != DBNull.Value ? Convert.ToDecimal(reader["SchemeDiscount"]) : 0m,
                    TotalValue = reader["TotalValue"] != DBNull.Value ? Convert.ToDecimal(reader["TotalValue"]) : 0m,
                    SDate = reader["SDate"] != DBNull.Value ? Convert.ToDateTime(reader["SDate"]) : DateTime.Today,
                    EDate = reader["EDate"] != DBNull.Value ? Convert.ToDateTime(reader["EDate"]) : DateTime.Today.AddMonths(11),
                    Status = reader["Status"]?.ToString()?.Trim() ?? "Active",
                    Closed = reader["Closed"] != DBNull.Value && Convert.ToBoolean(reader["Closed"]),
                    Staff = HasColumn(reader, "Staff") && reader["Staff"] != DBNull.Value ? reader["Staff"]?.ToString()?.Trim() ?? string.Empty : string.Empty
                });
            }
        }

        // 2. Load customer contact info map
        var customerContacts = new Dictionary<string, (string Phone, string Address, string City)>(StringComparer.OrdinalIgnoreCase);
        try
        {
            const string custSql = "SELECT CustomerName, Phone, Address, City FROM Customers WHERE CustomerName IS NOT NULL";
            await using var custCmd = new OleDbCommand(custSql, connection);
            await using var custReader = await custCmd.ExecuteReaderAsync(cancellationToken);
            while (await custReader.ReadAsync(cancellationToken))
            {
                var cName = custReader["CustomerName"]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(cName) && !customerContacts.ContainsKey(cName))
                {
                    customerContacts[cName] = (
                        custReader["Phone"]?.ToString()?.Trim() ?? string.Empty,
                        custReader["Address"]?.ToString()?.Trim() ?? string.Empty,
                        custReader["City"]?.ToString()?.Trim() ?? string.Empty
                    );
                }
            }
        }
        catch
        {
            // Non-fatal if customers table has different columns
        }

        // Also check AccountCreation for missing customer contacts
        try
        {
            const string accSql = "SELECT AccountName, PhoneNo, Mobile, Address, City FROM AccountCreation WHERE AccountName IS NOT NULL";
            await using var accCmd = new OleDbCommand(accSql, connection);
            await using var accReader = await accCmd.ExecuteReaderAsync(cancellationToken);
            while (await accReader.ReadAsync(cancellationToken))
            {
                var aName = accReader["AccountName"]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(aName))
                {
                    var phone = accReader["PhoneNo"]?.ToString()?.Trim();
                    if (string.IsNullOrWhiteSpace(phone))
                    {
                        phone = accReader["Mobile"]?.ToString()?.Trim() ?? string.Empty;
                    }

                    if (!customerContacts.ContainsKey(aName))
                    {
                        customerContacts[aName] = (
                            phone,
                            accReader["Address"]?.ToString()?.Trim() ?? string.Empty,
                            accReader["City"]?.ToString()?.Trim() ?? string.Empty
                        );
                    }
                    else if (string.IsNullOrWhiteSpace(customerContacts[aName].Phone) && !string.IsNullOrWhiteSpace(phone))
                    {
                        var cur = customerContacts[aName];
                        customerContacts[aName] = (phone, cur.Address, cur.City);
                    }
                }
            }
        }
        catch
        {
            // Non-fatal
        }

        // 3. Load all scheme receipts grouped by Scode
        var receiptsMap = new Dictionary<string, List<(DateTime VouDate, decimal Amount, int InstallmentNo)>>(StringComparer.OrdinalIgnoreCase);
        try
        {
            const string receiptsSql = @"
                SELECT Scode, VouDate, DrAmt, CrAmt, InstallmentNo
                FROM [RECIEPTS]
                WHERE [VouType] = 'Scheme Entry' AND Scode IS NOT NULL AND ([Status] IS NULL OR [Status] <> 'Deleted')
                ORDER BY [InstallmentNo] ASC, [VouDate] ASC";

            await using var recCmd = new OleDbCommand(receiptsSql, connection);
            await using var recReader = await recCmd.ExecuteReaderAsync(cancellationToken);
            while (await recReader.ReadAsync(cancellationToken))
            {
                var scode = recReader["Scode"]?.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(scode)) continue;

                var vouDate = recReader["VouDate"] != DBNull.Value ? Convert.ToDateTime(recReader["VouDate"]) : DateTime.Today;
                var drAmt = recReader["DrAmt"] != DBNull.Value ? Convert.ToDecimal(recReader["DrAmt"]) : 0m;
                var crAmt = recReader["CrAmt"] != DBNull.Value ? Convert.ToDecimal(recReader["CrAmt"]) : 0m;
                var amt = drAmt > 0 ? drAmt : crAmt;
                var instNo = recReader["InstallmentNo"] != DBNull.Value ? Convert.ToInt32(recReader["InstallmentNo"]) : 0;

                if (!receiptsMap.TryGetValue(scode, out var list))
                {
                    list = new List<(DateTime, decimal, int)>();
                    receiptsMap[scode] = list;
                }
                list.Add((vouDate, amt, instNo));
            }
        }
        catch
        {
            // Non-fatal
        }

        // 4. Calculate due and overdue details for each scheme
        foreach (var s in schemes)
        {
            var item = new SchemeDueListItem
            {
                Scode = s.Scode,
                Nos = s.Nos,
                CustomerName = s.CustomerName,
                Staff = s.Staff,
                SDate = s.SDate,
                EDate = s.EDate,
                InstallmentAmt = s.InstallmentAmt,
                TotalMonths = (int)s.Month <= 0 ? 11 : (int)s.Month,
                TotalValue = s.TotalValue > 0 ? s.TotalValue : (s.InstallmentAmt * (s.Month <= 0 ? 11 : s.Month)),
                Closed = s.Closed,
                Status = s.Status
            };

            if (customerContacts.TryGetValue(s.CustomerName, out var contact))
            {
                item.Phone = contact.Phone;
                item.Address = contact.Address;
                item.City = contact.City;
            }

            if (receiptsMap.TryGetValue(s.Scode, out var payments))
            {
                item.PaidCount = payments.Count;
                item.TotalPaidAmt = payments.Sum(p => p.Amount);
                item.LastPaymentDate = payments.Max(p => p.VouDate);
            }
            else
            {
                item.PaidCount = 0;
                item.TotalPaidAmt = 0m;
                item.LastPaymentDate = null;
            }

            // Expected installments up to asOnDate
            if (asOnDate >= s.SDate)
            {
                int monthsElapsed = (asOnDate.Year - s.SDate.Year) * 12 + asOnDate.Month - s.SDate.Month;
                // If today is past or on the start day of the month, the current month installment is due
                if (asOnDate.Day >= s.SDate.Day)
                {
                    monthsElapsed += 1;
                }
                else
                {
                    monthsElapsed = Math.Max(1, monthsElapsed);
                }
                item.ExpectedCount = Math.Clamp(monthsElapsed, 1, item.TotalMonths);
            }
            else
            {
                item.ExpectedCount = 0;
            }

            // Overdue calculation
            item.OverdueCount = Math.Max(0, item.ExpectedCount - item.PaidCount);
            item.OverdueAmount = item.OverdueCount * item.InstallmentAmt;

            // Next due date calculation
            if (item.PaidCount < item.TotalMonths)
            {
                item.NextDueDate = s.SDate.AddMonths(item.PaidCount);
                if (item.NextDueDate.HasValue)
                {
                    var diff = (item.NextDueDate.Value.Date - asOnDate.Date).Days;
                    if (diff < 0)
                    {
                        item.DaysOverdue = Math.Abs(diff);
                        item.DaysRemaining = 0;
                    }
                    else
                    {
                        item.DaysOverdue = 0;
                        item.DaysRemaining = diff;
                    }
                }
            }
            else
            {
                item.NextDueDate = null;
                item.DaysOverdue = 0;
                item.DaysRemaining = 0;
            }

            result.Add(item);
        }

        return result;
    }

    public async Task<IReadOnlyList<VoucherEntry>> GetSchemePaymentHistoryAsync(string scode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(scode)) return Array.Empty<VoucherEntry>();

        const string sql = @"
            SELECT [VouNo], [VouDate], [Nos], [VouType], [Narration], [ChequeNo], [ChqDt],
                   [Status], [DrAccount], [DrAmt], [CrAccount], [CrAmt], [User], [Date],
                   [Time], [Locked], [Original], [Master], [MasterLock], [MasterDr],
                   [MasterCr], [Narr2], [Cno], [CDate], [Disputed], [ChandanDispute],
                   [Scode], [InstallmentNo], [CardComm], [CommAmt], [NetCredited],
                   [BankName], [CardType], [CP], [Check]
            FROM [RECIEPTS]
            WHERE [Scode] = @Scode AND ([Status] IS NULL OR [Status] <> 'Deleted')
            ORDER BY [InstallmentNo] ASC, [VouDate] ASC";

        var list = new List<VoucherEntry>();
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        command.Parameters.Add("@Scode", OleDbType.VarWChar, 50).Value = scode.Trim();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new VoucherEntry
            {
                VouNo = reader["VouNo"]?.ToString() ?? string.Empty,
                VouDate = reader["VouDate"] != DBNull.Value ? Convert.ToDateTime(reader["VouDate"]) : DateTime.Today,
                Nos = reader["Nos"] != DBNull.Value ? Convert.ToInt32(reader["Nos"]) : 0,
                VouType = reader["VouType"]?.ToString() ?? "Scheme Entry",
                Narration = reader["Narration"]?.ToString() ?? string.Empty,
                DrAccount = reader["DrAccount"]?.ToString() ?? string.Empty,
                DrAmt = reader["DrAmt"] != DBNull.Value ? Convert.ToDecimal(reader["DrAmt"]) : 0m,
                CrAccount = reader["CrAccount"]?.ToString() ?? string.Empty,
                CrAmt = reader["CrAmt"] != DBNull.Value ? Convert.ToDecimal(reader["CrAmt"]) : 0m,
                Scode = reader["Scode"]?.ToString() ?? string.Empty,
                InstallmentNo = reader["InstallmentNo"] != DBNull.Value ? Convert.ToInt32(reader["InstallmentNo"]) : 1,
                BankName = reader["BankName"]?.ToString() ?? string.Empty,
                CardType = reader["CardType"]?.ToString() ?? string.Empty,
                Status = reader["Status"]?.ToString() ?? "Saved"
            });
        }

        return list;
    }

    private static bool HasColumn(IDataRecord reader, string columnName)
    {
        for (int i = 0; i < reader.FieldCount; i++)
        {
            if (string.Equals(reader.GetName(i), columnName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}

