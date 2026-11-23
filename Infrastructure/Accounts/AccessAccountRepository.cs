using System.Data.Common;
using System.Data.OleDb;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class AccessAccountRepository : IAccountRepository
{
    private readonly string _connectionString;

    public AccessAccountRepository(string databasePath)
    {
        _connectionString = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={databasePath};";
    }

    public async Task AddAsync(Account account, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            INSERT INTO [AccountCreation]
                ([HeadCategory], [SubCategory], [AccountName], [Address], [Address2], [City], [PinCode], [State],
                 [PhoneNo], [RefName], [VatType], [VatNo], [Position], [InOut], [Print], [Status], [OpAmt1], [PanNo],
                 [CreditLimit], [CreditDays], [User], [EntryDate], [Time], [Interest], [OpAmt2], [Hide],
                 [Mobile], [BankAc], [Email], [ComputerName], [PartyCode], [NOS], [StdCode], [OpGoldWeight])
            VALUES
                (@HeadCategory, @SubCategory, @AccountName, @Address, @Address2, @City, @PinCode, @State,
                 @PhoneNo, @RefName, @VatType, @VatNo, @Position, @InOut, @Print, @Status, @OpAmt1, @PanNo,
                 @CreditLimit, @CreditDays, @User, @EntryDate, @Time, @Interest, @OpAmt2, @Hide,
                 @Mobile, @BankAc, @Email, @ComputerName, @PartyCode, @NOS, @StdCode, @OpGoldWeight)";

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        AddParameters(command, account);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAsync(Account account, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            UPDATE [AccountCreation] SET
                [HeadCategory] = @HeadCategory, [SubCategory] = @SubCategory, [AccountName] = @AccountName,
                [Address] = @Address, [Address2] = @Address2, [City] = @City, [PinCode] = @PinCode, [State] = @State,
                [PhoneNo] = @PhoneNo, [RefName] = @RefName, [VatType] = @VatType, [VatNo] = @VatNo,
                [Position] = @Position, [InOut] = @InOut, [Print] = @Print, [Status] = @Status, [OpAmt1] = @OpAmt1,
                [PanNo] = @PanNo, [CreditLimit] = @CreditLimit, [CreditDays] = @CreditDays, [User] = @User,
                [EntryDate] = @EntryDate, [Time] = @Time, [Interest] = @Interest, [OpAmt2] = @OpAmt2,
                [Hide] = @Hide, [Mobile] = @Mobile, [BankAc] = @BankAc, [Email] = @Email,
                [ComputerName] = @ComputerName, [NOS] = @NOS, [StdCode] = @StdCode, [OpGoldWeight] = @OpGoldWeight
            WHERE [PartyCode] = @WherePartyCode";

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        AddUpdateParameters(command, account);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(string partyCode, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM AccountCreation WHERE [PartyCode] = @PartyCode";

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        command.Parameters.Add("@PartyCode", OleDbType.VarWChar, 50).Value = partyCode;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<Account?> FindByPartyCodeAsync(string partyCode, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT TOP 1 * FROM AccountCreation WHERE [PartyCode] = @PartyCode";

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        command.Parameters.Add("@PartyCode", OleDbType.VarWChar, 50).Value = partyCode;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return Map(reader);
    }

    public async Task<IReadOnlyList<Account>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM AccountCreation ORDER BY [AccountName]";

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var accounts = new List<Account>();
        while (await reader.ReadAsync(cancellationToken))
        {
            accounts.Add(Map(reader));
        }

        return accounts;
    }

    private static Account Map(DbDataReader reader)
    {
        return new Account
        {
            HeadCategory = GetString(reader, "HeadCategory"),
            SubCategory = GetString(reader, "SubCategory"),
            AccountName = GetString(reader, "AccountName"),
            Address = GetString(reader, "Address"),
            Address2 = GetString(reader, "Address2"),
            City = GetString(reader, "City"),
            PinCode = GetString(reader, "PinCode"),
            State = GetString(reader, "State"),
            PhoneNo = GetString(reader, "PhoneNo"),
            RefName = GetString(reader, "RefName"),
            VatType = GetString(reader, "VatType"),
            VatNo = GetString(reader, "VatNo"),
            Position = GetInt(reader, "Position"),
            InOut = GetString(reader, "InOut"),
            Print = GetBool(reader, "Print"),
            Status = GetString(reader, "Status"),
            OpAmt1 = GetDecimal(reader, "OpAmt1"),
            PanNo = GetString(reader, "PanNo"),
            CreditLimit = GetDecimal(reader, "CreditLimit"),
            CreditDays = GetDecimal(reader, "CreditDays"),
            User = GetString(reader, "User"),
            EntryDate = GetDateTime(reader, "EntryDate"),
            Time = GetTimeOfDay(reader, "Time"),
            Interest = GetDecimal(reader, "Interest"),
            OpAmt2 = GetDecimal(reader, "OpAmt2"),
            Hide = GetBool(reader, "Hide"),
            Mobile = GetString(reader, "Mobile"),
            BankAc = GetString(reader, "BankAc"),
            Email = GetString(reader, "Email"),
            ComputerName = GetString(reader, "ComputerName"),
            PartyCode = GetString(reader, "PartyCode"),
            NOS = GetInt(reader, "NOS"),
            StdCode = GetString(reader, "StdCode"),
            OpGoldWeight = GetDecimal(reader, "OpGoldWeight")
        };
    }

    private static void AddParameters(OleDbCommand command, Account account)
    {
        command.Parameters.Add("@HeadCategory", OleDbType.VarWChar, 50).Value = NullIfEmpty(account.HeadCategory);
        command.Parameters.Add("@SubCategory", OleDbType.VarWChar, 50).Value = NullIfEmpty(account.SubCategory);
        command.Parameters.Add("@AccountName", OleDbType.VarWChar, 100).Value = account.AccountName;
        command.Parameters.Add("@Address", OleDbType.VarWChar, 255).Value = NullIfEmpty(account.Address);
        command.Parameters.Add("@Address2", OleDbType.VarWChar, 50).Value = NullIfEmpty(account.Address2);
        command.Parameters.Add("@City", OleDbType.VarWChar, 50).Value = NullIfEmpty(account.City);
        command.Parameters.Add("@PinCode", OleDbType.VarWChar, 50).Value = NullIfEmpty(account.PinCode);
        command.Parameters.Add("@State", OleDbType.VarWChar, 50).Value = NullIfEmpty(account.State);
        command.Parameters.Add("@PhoneNo", OleDbType.VarWChar, 50).Value = NullIfEmpty(account.PhoneNo);
        command.Parameters.Add("@RefName", OleDbType.VarWChar, 50).Value = NullIfEmpty(account.RefName);
        command.Parameters.Add("@VatType", OleDbType.VarWChar, 11).Value = NullIfEmpty(account.VatType);
        command.Parameters.Add("@VatNo", OleDbType.VarWChar, 50).Value = NullIfEmpty(account.VatNo);
        command.Parameters.Add("@Position", OleDbType.Integer).Value = account.Position;
        command.Parameters.Add("@InOut", OleDbType.VarWChar, 10).Value = NullIfEmpty(account.InOut);
        command.Parameters.Add("@Print", OleDbType.Boolean).Value = account.Print;
        command.Parameters.Add("@Status", OleDbType.VarWChar, 10).Value = NullIfEmpty(account.Status);
        command.Parameters.Add("@OpAmt1", OleDbType.Currency).Value = account.OpAmt1;
        command.Parameters.Add("@PanNo", OleDbType.VarWChar, 25).Value = NullIfEmpty(account.PanNo);
        command.Parameters.Add("@CreditLimit", OleDbType.Currency).Value = account.CreditLimit;
        command.Parameters.Add("@CreditDays", OleDbType.Currency).Value = account.CreditDays;
        command.Parameters.Add("@User", OleDbType.VarWChar, 50).Value = NullIfEmpty(account.User);
        command.Parameters.Add("@EntryDate", OleDbType.Date).Value = account.EntryDate;
        command.Parameters.Add("@Time", OleDbType.Date).Value = new DateTime(1899, 12, 30).Add(account.Time);
        command.Parameters.Add("@Interest", OleDbType.Currency).Value = account.Interest;
        command.Parameters.Add("@OpAmt2", OleDbType.Currency).Value = account.OpAmt2;
        command.Parameters.Add("@Hide", OleDbType.Boolean).Value = account.Hide;
        command.Parameters.Add("@Mobile", OleDbType.VarWChar, 50).Value = NullIfEmpty(account.Mobile);
        command.Parameters.Add("@BankAc", OleDbType.VarWChar, 125).Value = NullIfEmpty(account.BankAc);
        command.Parameters.Add("@Email", OleDbType.VarWChar).Value = NullIfEmpty(account.Email);
        command.Parameters.Add("@ComputerName", OleDbType.VarWChar, 50).Value = NullIfEmpty(account.ComputerName);
        command.Parameters.Add("@PartyCode", OleDbType.VarWChar, 50).Value = account.PartyCode;
        command.Parameters.Add("@NOS", OleDbType.Integer).Value = account.NOS;
        command.Parameters.Add("@StdCode", OleDbType.VarWChar, 50).Value = NullIfEmpty(account.StdCode);
        command.Parameters.Add("@OpGoldWeight", OleDbType.Currency).Value = account.OpGoldWeight;
    }

    private static void AddUpdateParameters(OleDbCommand command, Account account)
    {
        command.Parameters.Add("@HeadCategory", OleDbType.VarWChar, 50).Value = NullIfEmpty(account.HeadCategory);
        command.Parameters.Add("@SubCategory", OleDbType.VarWChar, 50).Value = NullIfEmpty(account.SubCategory);
        command.Parameters.Add("@AccountName", OleDbType.VarWChar, 100).Value = account.AccountName;
        command.Parameters.Add("@Address", OleDbType.VarWChar, 255).Value = NullIfEmpty(account.Address);
        command.Parameters.Add("@Address2", OleDbType.VarWChar, 50).Value = NullIfEmpty(account.Address2);
        command.Parameters.Add("@City", OleDbType.VarWChar, 50).Value = NullIfEmpty(account.City);
        command.Parameters.Add("@PinCode", OleDbType.VarWChar, 50).Value = NullIfEmpty(account.PinCode);
        command.Parameters.Add("@State", OleDbType.VarWChar, 50).Value = NullIfEmpty(account.State);
        command.Parameters.Add("@PhoneNo", OleDbType.VarWChar, 50).Value = NullIfEmpty(account.PhoneNo);
        command.Parameters.Add("@RefName", OleDbType.VarWChar, 50).Value = NullIfEmpty(account.RefName);
        command.Parameters.Add("@VatType", OleDbType.VarWChar, 11).Value = NullIfEmpty(account.VatType);
        command.Parameters.Add("@VatNo", OleDbType.VarWChar, 50).Value = NullIfEmpty(account.VatNo);
        command.Parameters.Add("@Position", OleDbType.Integer).Value = account.Position;
        command.Parameters.Add("@InOut", OleDbType.VarWChar, 10).Value = NullIfEmpty(account.InOut);
        command.Parameters.Add("@Print", OleDbType.Boolean).Value = account.Print;
        command.Parameters.Add("@Status", OleDbType.VarWChar, 10).Value = NullIfEmpty(account.Status);
        command.Parameters.Add("@OpAmt1", OleDbType.Currency).Value = account.OpAmt1;
        command.Parameters.Add("@PanNo", OleDbType.VarWChar, 25).Value = NullIfEmpty(account.PanNo);
        command.Parameters.Add("@CreditLimit", OleDbType.Currency).Value = account.CreditLimit;
        command.Parameters.Add("@CreditDays", OleDbType.Currency).Value = account.CreditDays;
        command.Parameters.Add("@User", OleDbType.VarWChar, 50).Value = NullIfEmpty(account.User);
        command.Parameters.Add("@EntryDate", OleDbType.Date).Value = account.EntryDate;
        command.Parameters.Add("@Time", OleDbType.Date).Value = new DateTime(1899, 12, 30).Add(account.Time);
        command.Parameters.Add("@Interest", OleDbType.Currency).Value = account.Interest;
        command.Parameters.Add("@OpAmt2", OleDbType.Currency).Value = account.OpAmt2;
        command.Parameters.Add("@Hide", OleDbType.Boolean).Value = account.Hide;
        command.Parameters.Add("@Mobile", OleDbType.VarWChar, 50).Value = NullIfEmpty(account.Mobile);
        command.Parameters.Add("@BankAc", OleDbType.VarWChar, 125).Value = NullIfEmpty(account.BankAc);
        command.Parameters.Add("@Email", OleDbType.VarWChar).Value = NullIfEmpty(account.Email);
        command.Parameters.Add("@ComputerName", OleDbType.VarWChar, 50).Value = NullIfEmpty(account.ComputerName);
        command.Parameters.Add("@NOS", OleDbType.Integer).Value = account.NOS;
        command.Parameters.Add("@StdCode", OleDbType.VarWChar, 50).Value = NullIfEmpty(account.StdCode);
        command.Parameters.Add("@OpGoldWeight", OleDbType.Currency).Value = account.OpGoldWeight;
        command.Parameters.Add("@WherePartyCode", OleDbType.VarWChar, 50).Value = account.PartyCode;
    }

    public async Task<IReadOnlyList<string>> GetAdvanceOptionsAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<string>();
        try
        {
            const string sql = "SELECT DISTINCT [Advance] FROM [Advance] WHERE [Advance] IS NOT NULL";
            await using var connection = new OleDbConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new OleDbCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var val = reader[0]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(val))
                {
                    result.Add(val);
                }
            }
        }
        catch
        {
            // Table or column might be named differently
        }

        if (result.Count == 0)
        {
            try
            {
                const string sql2 = "SELECT * FROM [Advance]";
                await using var connection = new OleDbConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);
                await using var command = new OleDbCommand(sql2, connection);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        var val = reader[i]?.ToString()?.Trim();
                        if (!string.IsNullOrWhiteSpace(val) && !result.Contains(val))
                        {
                            result.Add(val);
                            break;
                        }
                    }
                }
            }
            catch { }
        }

        if (result.Count == 0)
        {
            result = new List<string> { "Order Advance", "Old Gold / Scheme", "General Advance" };
        }

        return result;
    }

    public async Task<IReadOnlyList<string>> GetHeadCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<string>();
        try
        {
            const string sql = "SELECT [HeadCategory] FROM [HeadCategory] WHERE [HeadCategory] IS NOT NULL AND [HeadCategory] <> '' ORDER BY [HeadCategory]";
            await using var connection = new OleDbConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new OleDbCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var val = reader[0]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(val) && !result.Contains(val, StringComparer.OrdinalIgnoreCase))
                {
                    result.Add(val);
                }
            }
        }
        catch
        {
            // HeadCategory table might be named differently or empty
        }

        try
        {
            const string sql2 = "SELECT DISTINCT [HeadCategory] FROM [AccountCreation] WHERE [HeadCategory] IS NOT NULL AND [HeadCategory] <> '' ORDER BY [HeadCategory]";
            await using var connection = new OleDbConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new OleDbCommand(sql2, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var val = reader[0]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(val) && !result.Contains(val, StringComparer.OrdinalIgnoreCase))
                {
                    result.Add(val);
                }
            }
        }
        catch
        {
        }

        var defaultCategories = new[]
        {
            "Advances & Deposits", "Approval Account", "Artisians", "Bank A/c", "Bank OD A/C",
            "Capital", "Cash in Hand", "Closing Stock", "Credit Card", "Creditors",
            "Current Assets", "Current Liabilities", "Debtors", "Deposits(Assets)", "Depreciation",
            "Direct Expenses", "Direct Incomes", "Duties & Taxes", "Equity Shares", "Fixed Assets",
            "Hire Account", "Indirect Expenses", "Indirect Incomes", "Intangible Asset", "Investments",
            "Loans Given", "Loans Taken", "Opening Stock", "OutStanding Liability", "Provisions",
            "Purchase Return", "Purchases", "Reserve & Surplus", "Sales", "Sales Return",
            "Suspense A/c", "Unsecured Loans"
        };

        foreach (var def in defaultCategories)
        {
            if (!result.Contains(def, StringComparer.OrdinalIgnoreCase))
            {
                result.Add(def);
            }
        }

        result.Sort(StringComparer.OrdinalIgnoreCase);
        return result;
    }

    private static string GetString(DbDataReader reader, string column) =>
        reader[column] is DBNull ? string.Empty : Convert.ToString(reader[column]) ?? string.Empty;

    private static int GetInt(DbDataReader reader, string column) =>
        reader[column] is DBNull ? 0 : Convert.ToInt32(reader[column]);

    private static bool GetBool(DbDataReader reader, string column) =>
        reader[column] is DBNull ? false : Convert.ToBoolean(reader[column]);

    private static decimal GetDecimal(DbDataReader reader, string column) =>
        reader[column] is DBNull ? 0m : Convert.ToDecimal(reader[column]);

    private static DateTime GetDateTime(DbDataReader reader, string column) =>
        reader[column] is DBNull ? DateTime.Today : Convert.ToDateTime(reader[column]);

    private static TimeSpan GetTimeOfDay(DbDataReader reader, string column) =>
        reader[column] is DBNull ? TimeSpan.Zero : Convert.ToDateTime(reader[column]).TimeOfDay;

    private static object NullIfEmpty(string value) =>
        string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;
}
