using System.Data;
using System.Data.OleDb;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class AccessVoucherRepository : IVoucherRepository
{
    private readonly string _connectionString;

    public AccessVoucherRepository(string databasePath)
    {
        _connectionString = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={databasePath};";
    }

    public async Task<IReadOnlyList<VoucherEntry>> GetRecentAsync(string vouType, int count = 50, CancellationToken cancellationToken = default)
    {
        count = Math.Clamp(count, 1, 500);
        var sql = $@"
            SELECT TOP {count}
                   [VouNo], [VouDate], [Nos], [VouType], [Narration], [ChequeNo], [ChqDt],
                   [Status], [DrAccount], [DrAmt], [CrAccount], [CrAmt], [User], [Date],
                   [Time], [Locked], [Original], [Master], [MasterLock], [MasterDr],
                   [MasterCr], [Narr2], [Cno], [CDate], [Disputed], [ChandanDispute],
                   [Scode], [InstallmentNo], [CardComm], [CommAmt], [NetCredited],
                   [BankName], [CardType], [CP], [Check]
            FROM [RECIEPTS]
            WHERE [VouType] = @VouType AND ([Status] IS NULL OR [Status] <> 'Deleted')
            ORDER BY [Nos] DESC, [VouDate] DESC";

        var list = new List<VoucherEntry>();
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        command.Parameters.Add("@VouType", OleDbType.VarWChar, 50).Value = vouType;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(MapVoucher(reader));
        }

        return list;
    }

    public async Task<VoucherEntry?> GetByVouNoAsync(string vouNo, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT TOP 1
                   [VouNo], [VouDate], [Nos], [VouType], [Narration], [ChequeNo], [ChqDt],
                   [Status], [DrAccount], [DrAmt], [CrAccount], [CrAmt], [User], [Date],
                   [Time], [Locked], [Original], [Master], [MasterLock], [MasterDr],
                   [MasterCr], [Narr2], [Cno], [CDate], [Disputed], [ChandanDispute],
                   [Scode], [InstallmentNo], [CardComm], [CommAmt], [NetCredited],
                   [BankName], [CardType], [CP], [Check]
            FROM [RECIEPTS]
            WHERE [VouNo] = @VouNo AND ([Status] IS NULL OR [Status] <> 'Deleted')";

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        command.Parameters.Add("@VouNo", OleDbType.VarWChar, 50).Value = vouNo;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapVoucher(reader);
        }

        return null;
    }

    public async Task<int> GetNextNosAsync(string vouType, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT IIf(IsNull(Max([Nos])), 0, Max([Nos])) + 1 FROM [RECIEPTS] WHERE [VouType] = @VouType";

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        command.Parameters.Add("@VouType", OleDbType.VarWChar, 50).Value = vouType;

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? 1 : Convert.ToInt32(result);
    }

    public async Task<string> GetNextVouNoAsync(string vouType, CancellationToken cancellationToken = default)
    {
        var nextNos = await GetNextNosAsync(vouType, cancellationToken);
        var prefix = VoucherType.GetPrefix(vouType);
        return $"{prefix}{nextNos:D4}";
    }

    public async Task SaveAsync(VoucherEntry voucher, bool updateExisting = false, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(voucher.VouNo))
        {
            if (voucher.Nos <= 0)
            {
                voucher.Nos = await GetNextNosAsync(voucher.VouType, cancellationToken);
            }
            voucher.VouNo = $"{VoucherType.GetPrefix(voucher.VouType)}{voucher.Nos:D4}";
        }

        if (voucher.Nos <= 0)
        {
            voucher.Nos = await GetNextNosAsync(voucher.VouType, cancellationToken);
        }

        // Keep MasterDr/MasterCr/Narr2 consistent
        voucher.MasterDr = voucher.DrAccount;
        voucher.MasterCr = voucher.CrAccount;
        voucher.Narr2 = voucher.Narration;
        voucher.DrAmt = voucher.CrAmt; // DrAmt and CrAmt match in double entry voucher

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        if (updateExisting)
        {
            const string updateSql = @"
                UPDATE [RECIEPTS] SET
                    [VouDate] = @VouDate,
                    [Nos] = @Nos,
                    [VouType] = @VouType,
                    [Narration] = @Narration,
                    [ChequeNo] = @ChequeNo,
                    [ChqDt] = @ChqDt,
                    [Status] = @Status,
                    [DrAccount] = @DrAccount,
                    [DrAmt] = @DrAmt,
                    [CrAccount] = @CrAccount,
                    [CrAmt] = @CrAmt,
                    [User] = @User,
                    [Date] = @Date,
                    [Time] = @Time,
                    [Locked] = @Locked,
                    [Original] = @Original,
                    [Master] = @Master,
                    [MasterLock] = @MasterLock,
                    [MasterDr] = @MasterDr,
                    [MasterCr] = @MasterCr,
                    [Narr2] = @Narr2,
                    [Cno] = @Cno,
                    [CDate] = @CDate,
                    [Disputed] = @Disputed,
                    [ChandanDispute] = @ChandanDispute,
                    [Scode] = @Scode,
                    [InstallmentNo] = @InstallmentNo,
                    [CardComm] = @CardComm,
                    [CommAmt] = @CommAmt,
                    [NetCredited] = @NetCredited,
                    [BankName] = @BankName,
                    [CardType] = @CardType,
                    [CP] = @CP,
                    [Check] = @Check
                WHERE [VouNo] = @WhereVouNo";

            await using var command = new OleDbCommand(updateSql, connection);
            AddParameters(command, voucher);
            command.Parameters.Add("@WhereVouNo", OleDbType.VarWChar, 50).Value = voucher.VouNo;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        else
        {
            const string insertSql = @"
                INSERT INTO [RECIEPTS] (
                    [VouNo], [VouDate], [Nos], [VouType], [Narration], [ChequeNo], [ChqDt],
                    [Status], [DrAccount], [DrAmt], [CrAccount], [CrAmt], [User], [Date],
                    [Time], [Locked], [Original], [Master], [MasterLock], [MasterDr],
                    [MasterCr], [Narr2], [Cno], [CDate], [Disputed], [ChandanDispute],
                    [Scode], [InstallmentNo], [CardComm], [CommAmt], [NetCredited],
                    [BankName], [CardType], [CP], [Check]
                ) VALUES (
                    @VouNo, @VouDate, @Nos, @VouType, @Narration, @ChequeNo, @ChqDt,
                    @Status, @DrAccount, @DrAmt, @CrAccount, @CrAmt, @User, @Date,
                    @Time, @Locked, @Original, @Master, @MasterLock, @MasterDr,
                    @MasterCr, @Narr2, @Cno, @CDate, @Disputed, @ChandanDispute,
                    @Scode, @InstallmentNo, @CardComm, @CommAmt, @NetCredited,
                    @BankName, @CardType, @CP, @Check
                )";

            await using var command = new OleDbCommand(insertSql, connection);
            command.Parameters.Add("@VouNo", OleDbType.VarWChar, 50).Value = voucher.VouNo;
            AddParameters(command, voucher);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public async Task DeleteAsync(string vouNo, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE [RECIEPTS] SET [Status] = 'Deleted' WHERE [VouNo] = @VouNo";

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        command.Parameters.Add("@VouNo", OleDbType.VarWChar, 50).Value = vouNo;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetBankAccountsAsync(CancellationToken cancellationToken = default)
    {
        var list = new List<string>();
        try
        {
            const string sql = @"
                SELECT DISTINCT [AccountName]
                FROM [AccountCreation]
                WHERE ([HeadCategory] = 'Bank A/c' OR [HeadCategory] = 'Bank A/C' OR [HeadCategory] = 'BANK ACCOUNTS' OR [HeadCategory] = 'BANK' OR [SubCategory] = 'BANK' OR [AccountName] LIKE '%BANK%')
                  AND ([Status] IS NULL OR [Status] <> 'Deleted')
                ORDER BY [AccountName]";

            await using var connection = new OleDbConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new OleDbCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var val = reader[0]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(val))
                {
                    list.Add(val);
                }
            }
        }
        catch
        {
            // Fallback default
        }

        if (list.Count == 0)
        {
            list.AddRange(new[] { "HDFC BANK -NEW 50200040965163", "STATE BANK OF INDIA 33162560995" });
        }

        return list;
    }

    public async Task<IReadOnlyList<string>> GetCashAccountsAsync(CancellationToken cancellationToken = default)
    {
        var list = new List<string>();
        try
        {
            const string sql = @"
                SELECT DISTINCT [AccountName]
                FROM [AccountCreation]
                WHERE ([HeadCategory] = 'CASH IN HAND' OR [HeadCategory] = 'Cash in Hand' OR [HeadCategory] = 'CASH' OR [SubCategory] = 'CASH' OR [AccountName] LIKE '%CASH%')
                  AND ([Status] IS NULL OR [Status] <> 'Deleted')
                ORDER BY [AccountName]";

            await using var connection = new OleDbConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new OleDbCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var val = reader[0]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(val))
                {
                    list.Add(val);
                }
            }
        }
        catch
        {
            // Fallback default
        }

        if (list.Count == 0)
        {
            list.AddRange(new[] { "CASH", "PETTY CASH" });
        }

        return list;
    }

    public async Task<IReadOnlyList<string>> GetCardTypesAsync(CancellationToken cancellationToken = default)
    {
        var list = new List<string>();
        try
        {
            const string sql = "SELECT DISTINCT [BankName] FROM [CardSetup]";

            await using var connection = new OleDbConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new OleDbCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var val = reader[0]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(val))
                {
                    list.Add(val);
                }
            }
        }
        catch
        {
            // Fallback default
        }

        if (list.Count == 0)
        {
            list.AddRange(new[] { "VISA / MASTER", "RUPAY", "AMEX", "CREDIT CARD", "DEBIT CARD" });
        }

        return list;
    }

    public async Task<IReadOnlyList<VoucherEntry>> GetByScodeAsync(string scode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(scode))
        {
            return Array.Empty<VoucherEntry>();
        }

        const string sql = @"
            SELECT [VouNo], [VouDate], [Nos], [VouType], [Narration], [ChequeNo], [ChqDt],
                   [Status], [DrAccount], [DrAmt], [CrAccount], [CrAmt], [User], [Date],
                   [Time], [Locked], [Original], [Master], [MasterLock], [MasterDr],
                   [MasterCr], [Narr2], [Cno], [CDate], [Disputed], [ChandanDispute],
                   [Scode], [InstallmentNo], [CardComm], [CommAmt], [NetCredited],
                   [BankName], [CardType], [CP], [Check]
            FROM [RECIEPTS]
            WHERE [Scode] = @Scode AND ([Status] IS NULL OR [Status] <> 'Deleted')
            ORDER BY [InstallmentNo] ASC, [Nos] ASC";

        var list = new List<VoucherEntry>();
        try
        {
            await using var connection = new OleDbConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new OleDbCommand(sql, connection);
            command.Parameters.Add("@Scode", OleDbType.VarWChar, 50).Value = scode.Trim();

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                list.Add(MapVoucher(reader));
            }
        }
        catch
        {
            // Return any loaded items
        }

        return list;
    }

    public async Task<decimal> GetAccountBalanceAsync(string accountName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accountName))
        {
            return 0m;
        }

        try
        {
            await using var connection = new OleDbConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            // Sum of DrAmt in RECIEPTS
            decimal drSum = 0m;
            const string drSql = "SELECT SUM([DrAmt]) FROM [RECIEPTS] WHERE [DrAccount] = @AccountName AND ([Status] IS NULL OR [Status] <> 'Deleted')";
            await using (var drCmd = new OleDbCommand(drSql, connection))
            {
                drCmd.Parameters.Add("@AccountName", OleDbType.VarWChar, 100).Value = accountName.Trim();
                var drRes = await drCmd.ExecuteScalarAsync(cancellationToken);
                if (drRes is not null and not DBNull)
                {
                    drSum = Convert.ToDecimal(drRes);
                }
            }

            // Sum of CrAmt in RECIEPTS
            decimal crSum = 0m;
            const string crSql = "SELECT SUM([CrAmt]) FROM [RECIEPTS] WHERE [CrAccount] = @AccountName AND ([Status] IS NULL OR [Status] <> 'Deleted')";
            await using (var crCmd = new OleDbCommand(crSql, connection))
            {
                crCmd.Parameters.Add("@AccountName", OleDbType.VarWChar, 100).Value = accountName.Trim();
                var crRes = await crCmd.ExecuteScalarAsync(cancellationToken);
                if (crRes is not null and not DBNull)
                {
                    crSum = Convert.ToDecimal(crRes);
                }
            }

            // Net balance: positive for Dr, negative for Cr
            return drSum - crSum;
        }
        catch
        {
            return 0m;
        }
    }

    private static void AddParameters(OleDbCommand command, VoucherEntry v)
    {
        command.Parameters.Add("@VouDate", OleDbType.Date).Value = v.VouDate;
        command.Parameters.Add("@Nos", OleDbType.Integer).Value = v.Nos;
        command.Parameters.Add("@VouType", OleDbType.VarWChar, 50).Value = v.VouType;
        command.Parameters.Add("@Narration", OleDbType.VarWChar, 255).Value = NullIfEmpty(v.Narration);
        command.Parameters.Add("@ChequeNo", OleDbType.VarWChar, 50).Value = NullIfEmpty(v.ChequeNo);
        command.Parameters.Add("@ChqDt", OleDbType.Date).Value = (object?)v.ChqDt ?? DBNull.Value;
        command.Parameters.Add("@Status", OleDbType.VarWChar, 50).Value = string.IsNullOrWhiteSpace(v.Status) ? "Saved" : v.Status;
        command.Parameters.Add("@DrAccount", OleDbType.VarWChar, 100).Value = v.DrAccount ?? string.Empty;
        command.Parameters.Add("@DrAmt", OleDbType.Currency).Value = v.DrAmt;
        command.Parameters.Add("@CrAccount", OleDbType.VarWChar, 100).Value = v.CrAccount ?? string.Empty;
        command.Parameters.Add("@CrAmt", OleDbType.Currency).Value = v.CrAmt;
        command.Parameters.Add("@User", OleDbType.VarWChar, 50).Value = NullIfEmpty(v.User);
        command.Parameters.Add("@Date", OleDbType.Date).Value = v.Date;
        command.Parameters.Add("@Time", OleDbType.Date).Value = new DateTime(1899, 12, 30).Add(v.Time);
        command.Parameters.Add("@Locked", OleDbType.Boolean).Value = v.Locked;
        command.Parameters.Add("@Original", OleDbType.Boolean).Value = v.Original;
        command.Parameters.Add("@Master", OleDbType.Boolean).Value = v.Master;
        command.Parameters.Add("@MasterLock", OleDbType.Boolean).Value = v.MasterLock;
        command.Parameters.Add("@MasterDr", OleDbType.VarWChar, 100).Value = NullIfEmpty(v.MasterDr);
        command.Parameters.Add("@MasterCr", OleDbType.VarWChar, 100).Value = NullIfEmpty(v.MasterCr);
        command.Parameters.Add("@Narr2", OleDbType.VarWChar, 255).Value = NullIfEmpty(v.Narr2);
        command.Parameters.Add("@Cno", OleDbType.VarWChar, 50).Value = NullIfEmpty(v.Cno);
        command.Parameters.Add("@CDate", OleDbType.Date).Value = (object?)v.CDate ?? DBNull.Value;
        command.Parameters.Add("@Disputed", OleDbType.Boolean).Value = v.Disputed;
        command.Parameters.Add("@ChandanDispute", OleDbType.Boolean).Value = v.ChandanDispute;
        command.Parameters.Add("@Scode", OleDbType.VarWChar, 50).Value = NullIfEmpty(v.Scode);
        command.Parameters.Add("@InstallmentNo", OleDbType.Integer).Value = v.InstallmentNo;
        command.Parameters.Add("@CardComm", OleDbType.Currency).Value = v.CardComm;
        command.Parameters.Add("@CommAmt", OleDbType.Currency).Value = v.CommAmt;
        command.Parameters.Add("@NetCredited", OleDbType.Currency).Value = v.NetCredited;
        command.Parameters.Add("@BankName", OleDbType.VarWChar, 100).Value = NullIfEmpty(v.BankName);
        command.Parameters.Add("@CardType", OleDbType.VarWChar, 50).Value = NullIfEmpty(v.CardType);
        command.Parameters.Add("@CP", OleDbType.Boolean).Value = v.CP;
        command.Parameters.Add("@Check", OleDbType.Boolean).Value = v.Check;
    }

    private static VoucherEntry MapVoucher(IDataRecord r)
    {
        var v = new VoucherEntry
        {
            VouNo = GetString(r, "VouNo"),
            VouDate = GetDateTime(r, "VouDate") ?? DateTime.Today,
            Nos = GetInt(r, "Nos"),
            VouType = GetString(r, "VouType"),
            Narration = GetString(r, "Narration"),
            Narr2 = GetString(r, "Narr2"),
            ChequeNo = GetString(r, "ChequeNo"),
            ChqDt = GetDateTime(r, "ChqDt"),
            Status = GetString(r, "Status", "Saved"),
            DrAccount = GetString(r, "DrAccount"),
            DrAmt = GetDecimal(r, "DrAmt"),
            CrAccount = GetString(r, "CrAccount"),
            CrAmt = GetDecimal(r, "CrAmt"),
            User = GetString(r, "User"),
            Date = GetDateTime(r, "Date") ?? DateTime.Today,
            Locked = GetBool(r, "Locked"),
            Original = GetBool(r, "Original", true),
            Master = GetBool(r, "Master", true),
            MasterLock = GetBool(r, "MasterLock"),
            MasterDr = GetString(r, "MasterDr"),
            MasterCr = GetString(r, "MasterCr"),
            Cno = GetString(r, "Cno"),
            CDate = GetDateTime(r, "CDate"),
            Disputed = GetBool(r, "Disputed"),
            ChandanDispute = GetBool(r, "ChandanDispute"),
            Scode = GetString(r, "Scode"),
            InstallmentNo = GetInt(r, "InstallmentNo"),
            CardComm = GetDecimal(r, "CardComm"),
            CommAmt = GetDecimal(r, "CommAmt"),
            NetCredited = GetDecimal(r, "NetCredited"),
            BankName = GetString(r, "BankName"),
            CardType = GetString(r, "CardType"),
            CP = GetBool(r, "CP"),
            Check = GetBool(r, "Check")
        };

        if (GetDateTime(r, "Time") is { } dt)
        {
            v.Time = dt.TimeOfDay;
        }

        // Determine PaymentMode
        if (!string.IsNullOrWhiteSpace(v.CardType) || v.CardComm > 0 || v.CommAmt > 0)
        {
            v.PaymentMode = "Card";
        }
        else if (!string.IsNullOrWhiteSpace(v.ChequeNo) || v.ChqDt.HasValue || !string.IsNullOrWhiteSpace(v.BankName))
        {
            v.PaymentMode = "Cheque";
        }
        else if (v.DrAccount.Contains("BANK", StringComparison.OrdinalIgnoreCase) || v.CrAccount.Contains("BANK", StringComparison.OrdinalIgnoreCase))
        {
            v.PaymentMode = "Online";
        }
        else
        {
            v.PaymentMode = "Cash";
        }

        return v;
    }

    private static object NullIfEmpty(string? val) => string.IsNullOrWhiteSpace(val) ? DBNull.Value : val.Trim();

    private static string GetString(IDataRecord r, string col, string defaultVal = "")
    {
        try
        {
            var ord = r.GetOrdinal(col);
            return r.IsDBNull(ord) ? defaultVal : r.GetValue(ord)?.ToString()?.Trim() ?? defaultVal;
        }
        catch { return defaultVal; }
    }

    private static int GetInt(IDataRecord r, string col, int defaultVal = 0)
    {
        try
        {
            var ord = r.GetOrdinal(col);
            return r.IsDBNull(ord) ? defaultVal : Convert.ToInt32(r.GetValue(ord));
        }
        catch { return defaultVal; }
    }

    private static decimal GetDecimal(IDataRecord r, string col, decimal defaultVal = 0m)
    {
        try
        {
            var ord = r.GetOrdinal(col);
            return r.IsDBNull(ord) ? defaultVal : Convert.ToDecimal(r.GetValue(ord));
        }
        catch { return defaultVal; }
    }

    private static bool GetBool(IDataRecord r, string col, bool defaultVal = false)
    {
        try
        {
            var ord = r.GetOrdinal(col);
            return r.IsDBNull(ord) ? defaultVal : Convert.ToBoolean(r.GetValue(ord));
        }
        catch { return defaultVal; }
    }

    private static DateTime? GetDateTime(IDataRecord r, string col)
    {
        try
        {
            var ord = r.GetOrdinal(col);
            if (r.IsDBNull(ord)) return null;
            var val = r.GetValue(ord);
            return val is DateTime dt ? dt : DateTime.TryParse(val?.ToString(), out var parsed) ? parsed : null;
        }
        catch { return null; }
    }
}
