using System.Data.Common;
using System.Data.OleDb;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class AccessCustomerRepository : ICustomerRepository
{
    private readonly string _connectionString;

    public AccessCustomerRepository(string databasePath)
    {
        _connectionString = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={databasePath};";
    }

    public async Task AddAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            INSERT INTO [Customers]
                ([Initial], [CompanyName], [Address], [Address2], [City], [Pincode], [Group], [Nos],
                 [Phone], [Code], [Print], [Status], [Saved], [DOB], [DOA], [Profession],
                 [Email], [Mobile], [User], [Date], [Time])
            VALUES
                (@Initial, @CompanyName, @Address, @Address2, @City, @Pincode, @Group, @Nos,
                 @Phone, @Code, @Print, @Status, @Saved, @DOB, @DOA, @Profession,
                 @Email, @Mobile, @User, @Date, @Time)";

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        AddParameters(command, customer);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            UPDATE [Customers] SET
                [Initial] = @Initial, [CompanyName] = @CompanyName, [Address] = @Address,
                [Address2] = @Address2, [City] = @City, [Pincode] = @Pincode, [Group] = @Group,
                [Nos] = @Nos, [Phone] = @Phone, [Print] = @Print, [Status] = @Status, [Saved] = @Saved,
                [DOB] = @DOB, [DOA] = @DOA, [Profession] = @Profession, [Email] = @Email,
                [Mobile] = @Mobile, [User] = @User, [Date] = @Date, [Time] = @Time
            WHERE [Code] = @WhereCode";

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        AddUpdateParameters(command, customer);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(string code, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM Customers WHERE [Code] = @Code";

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        command.Parameters.Add("@Code", OleDbType.VarWChar, 50).Value = code;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<Customer?> FindByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT TOP 1 * FROM Customers WHERE [Code] = @Code";

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        command.Parameters.Add("@Code", OleDbType.VarWChar, 50).Value = code;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return Map(reader);
    }

    public async Task<Customer?> FindByPhoneOrMobileAsync(string phoneOrMobile, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(phoneOrMobile))
        {
            return null;
        }

        var raw = phoneOrMobile.Trim();
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        var core10 = digits.Length >= 10 ? digits.Substring(digits.Length - 10) : digits;

        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { raw };
        if (!string.IsNullOrEmpty(digits))
        {
            candidates.Add(digits);
        }
        if (!string.IsNullOrEmpty(core10))
        {
            candidates.Add(core10);
            candidates.Add("91" + core10);
            candidates.Add("+91" + core10);
            candidates.Add("0" + core10);
        }

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var clauses = new List<string>();
        var parameters = new List<(string name, OleDbType type, object value)>();
        var idx = 0;

        foreach (var c in candidates)
        {
            var pName1 = $"@P{idx++}";
            var pName2 = $"@P{idx++}";
            clauses.Add($"([Phone] = {pName1} OR [Mobile] = {pName2})");
            parameters.Add((pName1, OleDbType.VarWChar, c));
            parameters.Add((pName2, OleDbType.VarWChar, c));
        }

        if (!string.IsNullOrEmpty(core10) && core10.Length >= 5)
        {
            var pLike1 = $"@PLike{idx++}";
            var pLike2 = $"@PLike{idx++}";
            clauses.Add($"([Phone] LIKE {pLike1} OR [Mobile] LIKE {pLike2})");
            parameters.Add((pLike1, OleDbType.VarWChar, $"%{core10}%"));
            parameters.Add((pLike2, OleDbType.VarWChar, $"%{core10}%"));
        }

        var sql = $"SELECT * FROM Customers WHERE {string.Join(" OR ", clauses)}";

        await using var command = new OleDbCommand(sql, connection);
        foreach (var p in parameters)
        {
            command.Parameters.Add(p.name, p.type, 50).Value = p.value;
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var matches = new List<Customer>();
        while (await reader.ReadAsync(cancellationToken))
        {
            matches.Add(Map(reader));
        }

        if (matches.Count == 0)
        {
            return null;
        }

        return matches
            .OrderByDescending(c => ScoreMatch(c, raw, digits, core10))
            .FirstOrDefault();
    }

    private static int ScoreMatch(Customer customer, string raw, string digits, string core10)
    {
        var custPhone = customer.Phone?.Trim() ?? string.Empty;
        var custMobile = customer.Mobile?.Trim() ?? string.Empty;

        if (string.Equals(custMobile, raw, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(custPhone, raw, StringComparison.OrdinalIgnoreCase))
        {
            return 100;
        }

        var custMobileDigits = new string(custMobile.Where(char.IsDigit).ToArray());
        var custPhoneDigits = new string(custPhone.Where(char.IsDigit).ToArray());

        if (!string.IsNullOrEmpty(digits) && (custMobileDigits == digits || custPhoneDigits == digits))
        {
            return 90;
        }

        if (!string.IsNullOrEmpty(core10))
        {
            var custMobileCore = custMobileDigits.Length >= 10 ? custMobileDigits.Substring(custMobileDigits.Length - 10) : custMobileDigits;
            var custPhoneCore = custPhoneDigits.Length >= 10 ? custPhoneDigits.Substring(custPhoneDigits.Length - 10) : custPhoneDigits;

            if (custMobileCore == core10 || custPhoneCore == core10)
            {
                return 80;
            }

            if (custMobileDigits.Contains(core10) || custPhoneDigits.Contains(core10))
            {
                return 50;
            }
        }

        return 10;
    }

    public async Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM Customers ORDER BY [CompanyName]";

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var customers = new List<Customer>();
        while (await reader.ReadAsync(cancellationToken))
        {
            customers.Add(Map(reader));
        }

        return customers;
    }

    private static Customer Map(DbDataReader reader)
    {
        return new Customer
        {
            Initial = GetString(reader, "Initial"),
            CompanyName = GetString(reader, "CompanyName"),
            Address = GetString(reader, "Address"),
            Address2 = GetString(reader, "Address2"),
            City = GetString(reader, "City"),
            Pincode = GetString(reader, "Pincode"),
            Group = GetString(reader, "Group"),
            Nos = GetInt(reader, "Nos"),
            Phone = GetString(reader, "Phone"),
            Code = GetString(reader, "Code"),
            Print = GetBool(reader, "Print"),
            Status = GetString(reader, "Status"),
            Saved = GetString(reader, "Saved"),
            DOB = GetNullableDateTime(reader, "DOB"),
            DOA = GetNullableDateTime(reader, "DOA"),
            Profession = GetString(reader, "Profession"),
            Email = GetString(reader, "Email"),
            Mobile = GetString(reader, "Mobile"),
            User = GetString(reader, "User"),
            Date = GetDateTime(reader, "Date"),
            Time = GetTimeOfDay(reader, "Time")
        };
    }

    private static void AddParameters(OleDbCommand command, Customer customer)
    {
        command.Parameters.Add("@Initial", OleDbType.VarWChar, 10).Value = NullIfEmpty(customer.Initial);
        command.Parameters.Add("@CompanyName", OleDbType.VarWChar, 100).Value = customer.CompanyName;
        command.Parameters.Add("@Address", OleDbType.VarWChar, 255).Value = NullIfEmpty(customer.Address);
        command.Parameters.Add("@Address2", OleDbType.VarWChar, 100).Value = NullIfEmpty(customer.Address2);
        command.Parameters.Add("@City", OleDbType.VarWChar, 50).Value = NullIfEmpty(customer.City);
        command.Parameters.Add("@Pincode", OleDbType.VarWChar, 20).Value = NullIfEmpty(customer.Pincode);
        command.Parameters.Add("@Group", OleDbType.VarWChar, 50).Value = NullIfEmpty(customer.Group);
        command.Parameters.Add("@Nos", OleDbType.Integer).Value = customer.Nos;
        command.Parameters.Add("@Phone", OleDbType.VarWChar, 50).Value = NullIfEmpty(customer.Phone);
        command.Parameters.Add("@Code", OleDbType.VarWChar, 50).Value = customer.Code;
        command.Parameters.Add("@Print", OleDbType.Boolean).Value = customer.Print;
        command.Parameters.Add("@Status", OleDbType.VarWChar, 10).Value = NullIfEmpty(customer.Status);
        command.Parameters.Add("@Saved", OleDbType.VarWChar, 10).Value = NullIfEmpty(customer.Saved);
        command.Parameters.Add("@DOB", OleDbType.Date).Value = (object?)customer.DOB ?? DBNull.Value;
        command.Parameters.Add("@DOA", OleDbType.Date).Value = (object?)customer.DOA ?? DBNull.Value;
        command.Parameters.Add("@Profession", OleDbType.VarWChar, 50).Value = NullIfEmpty(customer.Profession);
        command.Parameters.Add("@Email", OleDbType.VarWChar, 255).Value = NullIfEmpty(customer.Email);
        command.Parameters.Add("@Mobile", OleDbType.VarWChar, 50).Value = NullIfEmpty(customer.Mobile);
        command.Parameters.Add("@User", OleDbType.VarWChar, 50).Value = NullIfEmpty(customer.User);
        command.Parameters.Add("@Date", OleDbType.Date).Value = customer.Date;
        command.Parameters.Add("@Time", OleDbType.Date).Value = new DateTime(1899, 12, 30).Add(customer.Time);
    }

    private static void AddUpdateParameters(OleDbCommand command, Customer customer)
    {
        command.Parameters.Add("@Initial", OleDbType.VarWChar, 10).Value = NullIfEmpty(customer.Initial);
        command.Parameters.Add("@CompanyName", OleDbType.VarWChar, 100).Value = customer.CompanyName;
        command.Parameters.Add("@Address", OleDbType.VarWChar, 255).Value = NullIfEmpty(customer.Address);
        command.Parameters.Add("@Address2", OleDbType.VarWChar, 100).Value = NullIfEmpty(customer.Address2);
        command.Parameters.Add("@City", OleDbType.VarWChar, 50).Value = NullIfEmpty(customer.City);
        command.Parameters.Add("@Pincode", OleDbType.VarWChar, 20).Value = NullIfEmpty(customer.Pincode);
        command.Parameters.Add("@Group", OleDbType.VarWChar, 50).Value = NullIfEmpty(customer.Group);
        command.Parameters.Add("@Nos", OleDbType.Integer).Value = customer.Nos;
        command.Parameters.Add("@Phone", OleDbType.VarWChar, 50).Value = NullIfEmpty(customer.Phone);
        command.Parameters.Add("@Print", OleDbType.Boolean).Value = customer.Print;
        command.Parameters.Add("@Status", OleDbType.VarWChar, 10).Value = NullIfEmpty(customer.Status);
        command.Parameters.Add("@Saved", OleDbType.VarWChar, 10).Value = NullIfEmpty(customer.Saved);
        command.Parameters.Add("@DOB", OleDbType.Date).Value = (object?)customer.DOB ?? DBNull.Value;
        command.Parameters.Add("@DOA", OleDbType.Date).Value = (object?)customer.DOA ?? DBNull.Value;
        command.Parameters.Add("@Profession", OleDbType.VarWChar, 50).Value = NullIfEmpty(customer.Profession);
        command.Parameters.Add("@Email", OleDbType.VarWChar, 255).Value = NullIfEmpty(customer.Email);
        command.Parameters.Add("@Mobile", OleDbType.VarWChar, 50).Value = NullIfEmpty(customer.Mobile);
        command.Parameters.Add("@User", OleDbType.VarWChar, 50).Value = NullIfEmpty(customer.User);
        command.Parameters.Add("@Date", OleDbType.Date).Value = customer.Date;
        command.Parameters.Add("@Time", OleDbType.Date).Value = new DateTime(1899, 12, 30).Add(customer.Time);
        command.Parameters.Add("@WhereCode", OleDbType.VarWChar, 50).Value = customer.Code;
    }

    private static string GetString(DbDataReader reader, string column) =>
        reader[column] is DBNull ? string.Empty : Convert.ToString(reader[column]) ?? string.Empty;

    private static int GetInt(DbDataReader reader, string column) =>
        reader[column] is DBNull ? 0 : Convert.ToInt32(reader[column]);

    private static bool GetBool(DbDataReader reader, string column) =>
        reader[column] is DBNull ? false : Convert.ToBoolean(reader[column]);

    private static DateTime GetDateTime(DbDataReader reader, string column) =>
        reader[column] is DBNull ? DateTime.Today : Convert.ToDateTime(reader[column]);

    private static DateTime? GetNullableDateTime(DbDataReader reader, string column) =>
        reader[column] is DBNull ? null : Convert.ToDateTime(reader[column]);

    private static TimeSpan GetTimeOfDay(DbDataReader reader, string column) =>
        reader[column] is DBNull ? TimeSpan.Zero : Convert.ToDateTime(reader[column]).TimeOfDay;

    private static object NullIfEmpty(string value) =>
        string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;
}
