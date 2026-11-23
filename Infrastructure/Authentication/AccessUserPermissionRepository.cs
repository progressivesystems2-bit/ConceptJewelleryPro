using System.Data.Common;
using System.Data.OleDb;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Authentication;

public sealed class AccessUserPermissionRepository : IUserPermissionRepository
{
    private readonly string _connectionString;

    private static readonly string[] ColumnNames = new[]
    {
        "User", "Password", "Toolbar",
        "SaleAdd", "SaleMod", "SaleDel",
        "LedAdd", "LedMod", "LedDel",
        "ItemAdd", "ItemMod", "ItemDel",
        "BarAdd", "BarMod", "BarDel",
        "BcAdd", "BcMod", "BcDel",
        "RetAdd", "RetMod", "RetDel",
        "PurAdd", "PurMod", "PurDel",
        "RecAdd", "RecMod", "RecDel",
        "CusAdd", "CusMod", "CusDel",
        "PayAdd", "PayMod", "PayDel",
        "JouAdd", "JouMod", "JouDel",
        "MIssAdd", "MIssMod", "MIssDel",
        "MRecAdd", "MRecMod", "MRecDel",
        "AIssAdd", "AIssMod", "AIssDel",
        "ARecAdd", "ARecMod", "ARecDel",
        "HIssAdd", "HIssMod", "HIssDel",
        "HRecAdd", "HRecMod", "HRecDel",
        "UserAdd", "UserMod", "UserDel",
        "RepAdd", "RepMod", "RepDel",
        "ScAdd", "ScMod", "ScDel",
        "StaffAdd", "StaffMod", "StaffDel",
        "OrdAdd", "OrdMod", "OrdDel",
        "BhavAdd", "BhavMod", "BhavDel",
        "NoteTransfer", "NoteTransferMod", "NoteTransferDel",
        "SalesRegister", "ItemWiseSales", "VatRegister", "HidePartyDetails",
        "PurchaseRegister", "BarcodeStock", "RateChart", "LabelPrinting",
        "LedgerIndividual", "LedgerGroup", "MetalWiseLedger", "KarigarLedger",
        "TrialBalance", "OpeningTrial", "Trading", "ProfitLoss", "BalanceSheet",
        "CashBook", "CashBookOpening", "AuditView", "AuditDel", "Attendance"
    };

    public AccessUserPermissionRepository(string databasePath)
    {
        _connectionString = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={databasePath};";
    }

    public async Task<IReadOnlyList<UserPermission>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var cols = string.Join(", ", ColumnNames.Select(c => $"[{c}]"));
        var sql = $"SELECT {cols} FROM [User] ORDER BY [User]";

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var list = new List<UserPermission>();
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(Map(reader));
        }

        return list;
    }

    public async Task<UserPermission?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        var cols = string.Join(", ", ColumnNames.Select(c => $"[{c}]"));
        var sql = $"SELECT TOP 1 {cols} FROM [User] WHERE [User] = ?";

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        command.Parameters.Add("@User", OleDbType.VarWChar, 50).Value = username;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return Map(reader);
    }

    public async Task AddAsync(UserPermission u, CancellationToken cancellationToken = default)
    {
        var colNames = string.Join(", ", ColumnNames.Select(c => $"[{c}]"));
        var placeholders = string.Join(", ", ColumnNames.Select(_ => "?"));
        var sql = $"INSERT INTO [User] ({colNames}) VALUES ({placeholders})";

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        AddParameters(command, u);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAsync(string originalUsername, UserPermission u, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(originalUsername, u.User, StringComparison.OrdinalIgnoreCase))
        {
            await DeleteAsync(originalUsername, cancellationToken);
            await AddAsync(u, cancellationToken);
            return;
        }

        // UPDATE all columns except PK in SET clause, with PK in WHERE
        var nonPkCols = ColumnNames.Where(c => c != "User").ToArray();
        var setClause = string.Join(", ", nonPkCols.Select(c => $"[{c}] = ?"));
        var sql = $"UPDATE [User] SET {setClause} WHERE [User] = ?";

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        AddParameters(command, u, includePrimaryKeyInValues: false);
        command.Parameters.Add("@OriginalUser", OleDbType.VarWChar, 50).Value = originalUsername;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(string username, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM [User] WHERE [User] = ?";

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        command.Parameters.Add("@User", OleDbType.VarWChar, 50).Value = username;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddParameters(OleDbCommand cmd, UserPermission u, bool includePrimaryKeyInValues = true)
    {
        if (includePrimaryKeyInValues)
        {
            cmd.Parameters.Add("@User", OleDbType.VarWChar, 50).Value = u.User?.Trim() ?? string.Empty;
        }

        cmd.Parameters.Add("@Password", OleDbType.VarWChar, 50).Value = u.Password ?? string.Empty;
        cmd.Parameters.Add("@Toolbar", OleDbType.VarWChar, 50).Value = string.IsNullOrWhiteSpace(u.Toolbar) ? "DP1" : u.Toolbar.Trim();

        // Entries
        cmd.Parameters.Add("@SaleAdd", OleDbType.Boolean).Value = u.SaleAdd;
        cmd.Parameters.Add("@SaleMod", OleDbType.Boolean).Value = u.SaleMod;
        cmd.Parameters.Add("@SaleDel", OleDbType.Boolean).Value = u.SaleDel;

        cmd.Parameters.Add("@LedAdd", OleDbType.Boolean).Value = u.LedAdd;
        cmd.Parameters.Add("@LedMod", OleDbType.Boolean).Value = u.LedMod;
        cmd.Parameters.Add("@LedDel", OleDbType.Boolean).Value = u.LedDel;

        cmd.Parameters.Add("@ItemAdd", OleDbType.Boolean).Value = u.ItemAdd;
        cmd.Parameters.Add("@ItemMod", OleDbType.Boolean).Value = u.ItemMod;
        cmd.Parameters.Add("@ItemDel", OleDbType.Boolean).Value = u.ItemDel;

        cmd.Parameters.Add("@BarAdd", OleDbType.Boolean).Value = u.BarAdd;
        cmd.Parameters.Add("@BarMod", OleDbType.Boolean).Value = u.BarMod;
        cmd.Parameters.Add("@BarDel", OleDbType.Boolean).Value = u.BarDel;

        cmd.Parameters.Add("@BcAdd", OleDbType.Boolean).Value = u.BcAdd;
        cmd.Parameters.Add("@BcMod", OleDbType.Boolean).Value = u.BcMod;
        cmd.Parameters.Add("@BcDel", OleDbType.Boolean).Value = u.BcDel;

        cmd.Parameters.Add("@RetAdd", OleDbType.Boolean).Value = u.RetAdd;
        cmd.Parameters.Add("@RetMod", OleDbType.Boolean).Value = u.RetMod;
        cmd.Parameters.Add("@RetDel", OleDbType.Boolean).Value = u.RetDel;

        cmd.Parameters.Add("@PurAdd", OleDbType.Boolean).Value = u.PurAdd;
        cmd.Parameters.Add("@PurMod", OleDbType.Boolean).Value = u.PurMod;
        cmd.Parameters.Add("@PurDel", OleDbType.Boolean).Value = u.PurDel;

        cmd.Parameters.Add("@RecAdd", OleDbType.Boolean).Value = u.RecAdd;
        cmd.Parameters.Add("@RecMod", OleDbType.Boolean).Value = u.RecMod;
        cmd.Parameters.Add("@RecDel", OleDbType.Boolean).Value = u.RecDel;

        cmd.Parameters.Add("@CusAdd", OleDbType.Boolean).Value = u.CusAdd;
        cmd.Parameters.Add("@CusMod", OleDbType.Boolean).Value = u.CusMod;
        cmd.Parameters.Add("@CusDel", OleDbType.Boolean).Value = u.CusDel;

        cmd.Parameters.Add("@PayAdd", OleDbType.Boolean).Value = u.PayAdd;
        cmd.Parameters.Add("@PayMod", OleDbType.Boolean).Value = u.PayMod;
        cmd.Parameters.Add("@PayDel", OleDbType.Boolean).Value = u.PayDel;

        cmd.Parameters.Add("@JouAdd", OleDbType.Boolean).Value = u.JouAdd;
        cmd.Parameters.Add("@JouMod", OleDbType.Boolean).Value = u.JouMod;
        cmd.Parameters.Add("@JouDel", OleDbType.Boolean).Value = u.JouDel;

        cmd.Parameters.Add("@MIssAdd", OleDbType.Boolean).Value = u.MIssAdd;
        cmd.Parameters.Add("@MIssMod", OleDbType.Boolean).Value = u.MIssMod;
        cmd.Parameters.Add("@MIssDel", OleDbType.Boolean).Value = u.MIssDel;

        cmd.Parameters.Add("@MRecAdd", OleDbType.Boolean).Value = u.MRecAdd;
        cmd.Parameters.Add("@MRecMod", OleDbType.Boolean).Value = u.MRecMod;
        cmd.Parameters.Add("@MRecDel", OleDbType.Boolean).Value = u.MRecDel;

        cmd.Parameters.Add("@AIssAdd", OleDbType.Boolean).Value = u.AIssAdd;
        cmd.Parameters.Add("@AIssMod", OleDbType.Boolean).Value = u.AIssMod;
        cmd.Parameters.Add("@AIssDel", OleDbType.Boolean).Value = u.AIssDel;

        cmd.Parameters.Add("@ARecAdd", OleDbType.Boolean).Value = u.ARecAdd;
        cmd.Parameters.Add("@ARecMod", OleDbType.Boolean).Value = u.ARecMod;
        cmd.Parameters.Add("@ARecDel", OleDbType.Boolean).Value = u.ARecDel;

        cmd.Parameters.Add("@HIssAdd", OleDbType.Boolean).Value = u.HIssAdd;
        cmd.Parameters.Add("@HIssMod", OleDbType.Boolean).Value = u.HIssMod;
        cmd.Parameters.Add("@HIssDel", OleDbType.Boolean).Value = u.HIssDel;

        cmd.Parameters.Add("@HRecAdd", OleDbType.Boolean).Value = u.HRecAdd;
        cmd.Parameters.Add("@HRecMod", OleDbType.Boolean).Value = u.HRecMod;
        cmd.Parameters.Add("@HRecDel", OleDbType.Boolean).Value = u.HRecDel;

        cmd.Parameters.Add("@UserAdd", OleDbType.Boolean).Value = u.UserAdd;
        cmd.Parameters.Add("@UserMod", OleDbType.Boolean).Value = u.UserMod;
        cmd.Parameters.Add("@UserDel", OleDbType.Boolean).Value = u.UserDel;

        cmd.Parameters.Add("@RepAdd", OleDbType.Boolean).Value = u.RepAdd;
        cmd.Parameters.Add("@RepMod", OleDbType.Boolean).Value = u.RepMod;
        cmd.Parameters.Add("@RepDel", OleDbType.Boolean).Value = u.RepDel;

        cmd.Parameters.Add("@ScAdd", OleDbType.Boolean).Value = u.ScAdd;
        cmd.Parameters.Add("@ScMod", OleDbType.Boolean).Value = u.ScMod;
        cmd.Parameters.Add("@ScDel", OleDbType.Boolean).Value = u.ScDel;

        cmd.Parameters.Add("@StaffAdd", OleDbType.Boolean).Value = u.StaffAdd;
        cmd.Parameters.Add("@StaffMod", OleDbType.Boolean).Value = u.StaffMod;
        cmd.Parameters.Add("@StaffDel", OleDbType.Boolean).Value = u.StaffDel;

        cmd.Parameters.Add("@OrdAdd", OleDbType.Boolean).Value = u.OrdAdd;
        cmd.Parameters.Add("@OrdMod", OleDbType.Boolean).Value = u.OrdMod;
        cmd.Parameters.Add("@OrdDel", OleDbType.Boolean).Value = u.OrdDel;

        cmd.Parameters.Add("@BhavAdd", OleDbType.Boolean).Value = u.BhavAdd;
        cmd.Parameters.Add("@BhavMod", OleDbType.Boolean).Value = u.BhavMod;
        cmd.Parameters.Add("@BhavDel", OleDbType.Boolean).Value = u.BhavDel;

        cmd.Parameters.Add("@NoteTransfer", OleDbType.Boolean).Value = u.NoteTransfer;
        cmd.Parameters.Add("@NoteTransferMod", OleDbType.Boolean).Value = u.NoteTransferMod;
        cmd.Parameters.Add("@NoteTransferDel", OleDbType.Boolean).Value = u.NoteTransferDel;

        // Reports
        cmd.Parameters.Add("@SalesRegister", OleDbType.Boolean).Value = u.SalesRegister;
        cmd.Parameters.Add("@ItemWiseSales", OleDbType.Boolean).Value = u.ItemWiseSales;
        cmd.Parameters.Add("@VatRegister", OleDbType.Boolean).Value = u.VatRegister;
        cmd.Parameters.Add("@HidePartyDetails", OleDbType.Boolean).Value = u.HidePartyDetails;

        cmd.Parameters.Add("@PurchaseRegister", OleDbType.Boolean).Value = u.PurchaseRegister;
        cmd.Parameters.Add("@BarcodeStock", OleDbType.Boolean).Value = u.BarcodeStock;
        cmd.Parameters.Add("@RateChart", OleDbType.Boolean).Value = u.RateChart;
        cmd.Parameters.Add("@LabelPrinting", OleDbType.Boolean).Value = u.LabelPrinting;

        cmd.Parameters.Add("@LedgerIndividual", OleDbType.Boolean).Value = u.LedgerIndividual;
        cmd.Parameters.Add("@LedgerGroup", OleDbType.Boolean).Value = u.LedgerGroup;
        cmd.Parameters.Add("@MetalWiseLedger", OleDbType.Boolean).Value = u.MetalWiseLedger;
        cmd.Parameters.Add("@KarigarLedger", OleDbType.Boolean).Value = u.KarigarLedger;

        cmd.Parameters.Add("@TrialBalance", OleDbType.Boolean).Value = u.TrialBalance;
        cmd.Parameters.Add("@OpeningTrial", OleDbType.Boolean).Value = u.OpeningTrial;
        cmd.Parameters.Add("@Trading", OleDbType.Boolean).Value = u.Trading;
        cmd.Parameters.Add("@ProfitLoss", OleDbType.Boolean).Value = u.ProfitLoss;
        cmd.Parameters.Add("@BalanceSheet", OleDbType.Boolean).Value = u.BalanceSheet;

        cmd.Parameters.Add("@CashBook", OleDbType.Boolean).Value = u.CashBook;
        cmd.Parameters.Add("@CashBookOpening", OleDbType.Boolean).Value = u.CashBookOpening;
        cmd.Parameters.Add("@AuditView", OleDbType.Boolean).Value = u.AuditView;
        cmd.Parameters.Add("@AuditDel", OleDbType.Boolean).Value = u.AuditDel;
        cmd.Parameters.Add("@Attendance", OleDbType.Boolean).Value = u.Attendance;
    }

    private static UserPermission Map(DbDataReader reader)
    {
        return new UserPermission
        {
            User = Convert.ToString(reader["User"])?.Trim() ?? string.Empty,
            Password = Convert.ToString(reader["Password"]) ?? string.Empty,
            Toolbar = Convert.ToString(reader["Toolbar"])?.Trim() ?? "DP1",

            SaleAdd = GetBool(reader, "SaleAdd"),
            SaleMod = GetBool(reader, "SaleMod"),
            SaleDel = GetBool(reader, "SaleDel"),

            LedAdd = GetBool(reader, "LedAdd"),
            LedMod = GetBool(reader, "LedMod"),
            LedDel = GetBool(reader, "LedDel"),

            ItemAdd = GetBool(reader, "ItemAdd"),
            ItemMod = GetBool(reader, "ItemMod"),
            ItemDel = GetBool(reader, "ItemDel"),

            BarAdd = GetBool(reader, "BarAdd"),
            BarMod = GetBool(reader, "BarMod"),
            BarDel = GetBool(reader, "BarDel"),

            BcAdd = GetBool(reader, "BcAdd"),
            BcMod = GetBool(reader, "BcMod"),
            BcDel = GetBool(reader, "BcDel"),

            RetAdd = GetBool(reader, "RetAdd"),
            RetMod = GetBool(reader, "RetMod"),
            RetDel = GetBool(reader, "RetDel"),

            PurAdd = GetBool(reader, "PurAdd"),
            PurMod = GetBool(reader, "PurMod"),
            PurDel = GetBool(reader, "PurDel"),

            RecAdd = GetBool(reader, "RecAdd"),
            RecMod = GetBool(reader, "RecMod"),
            RecDel = GetBool(reader, "RecDel"),

            CusAdd = GetBool(reader, "CusAdd"),
            CusMod = GetBool(reader, "CusMod"),
            CusDel = GetBool(reader, "CusDel"),

            PayAdd = GetBool(reader, "PayAdd"),
            PayMod = GetBool(reader, "PayMod"),
            PayDel = GetBool(reader, "PayDel"),

            JouAdd = GetBool(reader, "JouAdd"),
            JouMod = GetBool(reader, "JouMod"),
            JouDel = GetBool(reader, "JouDel"),

            MIssAdd = GetBool(reader, "MIssAdd"),
            MIssMod = GetBool(reader, "MIssMod"),
            MIssDel = GetBool(reader, "MIssDel"),

            MRecAdd = GetBool(reader, "MRecAdd"),
            MRecMod = GetBool(reader, "MRecMod"),
            MRecDel = GetBool(reader, "MRecDel"),

            AIssAdd = GetBool(reader, "AIssAdd"),
            AIssMod = GetBool(reader, "AIssMod"),
            AIssDel = GetBool(reader, "AIssDel"),

            ARecAdd = GetBool(reader, "ARecAdd"),
            ARecMod = GetBool(reader, "ARecMod"),
            ARecDel = GetBool(reader, "ARecDel"),

            HIssAdd = GetBool(reader, "HIssAdd"),
            HIssMod = GetBool(reader, "HIssMod"),
            HIssDel = GetBool(reader, "HIssDel"),

            HRecAdd = GetBool(reader, "HRecAdd"),
            HRecMod = GetBool(reader, "HRecMod"),
            HRecDel = GetBool(reader, "HRecDel"),

            UserAdd = GetBool(reader, "UserAdd"),
            UserMod = GetBool(reader, "UserMod"),
            UserDel = GetBool(reader, "UserDel"),

            RepAdd = GetBool(reader, "RepAdd"),
            RepMod = GetBool(reader, "RepMod"),
            RepDel = GetBool(reader, "RepDel"),

            ScAdd = GetBool(reader, "ScAdd"),
            ScMod = GetBool(reader, "ScMod"),
            ScDel = GetBool(reader, "ScDel"),

            StaffAdd = GetBool(reader, "StaffAdd"),
            StaffMod = GetBool(reader, "StaffMod"),
            StaffDel = GetBool(reader, "StaffDel"),

            OrdAdd = GetBool(reader, "OrdAdd"),
            OrdMod = GetBool(reader, "OrdMod"),
            OrdDel = GetBool(reader, "OrdDel"),

            BhavAdd = GetBool(reader, "BhavAdd"),
            BhavMod = GetBool(reader, "BhavMod"),
            BhavDel = GetBool(reader, "BhavDel"),

            NoteTransfer = GetBool(reader, "NoteTransfer"),
            NoteTransferMod = GetBool(reader, "NoteTransferMod"),
            NoteTransferDel = GetBool(reader, "NoteTransferDel"),

            SalesRegister = GetBool(reader, "SalesRegister"),
            ItemWiseSales = GetBool(reader, "ItemWiseSales"),
            VatRegister = GetBool(reader, "VatRegister"),
            HidePartyDetails = GetBool(reader, "HidePartyDetails"),

            PurchaseRegister = GetBool(reader, "PurchaseRegister"),
            BarcodeStock = GetBool(reader, "BarcodeStock"),
            RateChart = GetBool(reader, "RateChart"),
            LabelPrinting = GetBool(reader, "LabelPrinting"),

            LedgerIndividual = GetBool(reader, "LedgerIndividual"),
            LedgerGroup = GetBool(reader, "LedgerGroup"),
            MetalWiseLedger = GetBool(reader, "MetalWiseLedger"),
            KarigarLedger = GetBool(reader, "KarigarLedger"),

            TrialBalance = GetBool(reader, "TrialBalance"),
            OpeningTrial = GetBool(reader, "OpeningTrial"),
            Trading = GetBool(reader, "Trading"),
            ProfitLoss = GetBool(reader, "ProfitLoss"),
            BalanceSheet = GetBool(reader, "BalanceSheet"),

            CashBook = GetBool(reader, "CashBook"),
            CashBookOpening = GetBool(reader, "CashBookOpening"),
            AuditView = GetBool(reader, "AuditView"),
            AuditDel = GetBool(reader, "AuditDel"),
            Attendance = GetBool(reader, "Attendance")
        };
    }

    private static bool GetBool(DbDataReader reader, string columnName)
    {
        var val = reader[columnName];
        if (val is null || val is DBNull) return false;
        return Convert.ToBoolean(val);
    }
}
