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

public class AccessCardAdjustmentReceiptRepository : ICardAdjustmentReceiptRepository
{
    private readonly string _databasePath;
    private readonly string _connectionString;

    public AccessCardAdjustmentReceiptRepository(string databasePath)
    {
        _databasePath = databasePath;
        _connectionString = AccessConnectionProvider.GetConnectionString(databasePath);
    }

    public async Task<string> GetNextReceiptNoAsync(string? counter = null, CancellationToken cancellationToken = default)
    {
        await AccessCardAdjustmentSchemaMigrator.EnsureSchemaAsync(_connectionString, cancellationToken);

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = "SELECT IIf(IsNull(Max(Nos)), 0, Max(Nos)) FROM CardAdjustmentReceipt";
        int maxNos = 0;
        try
        {
            await using var cmd = new OleDbCommand(sql, connection);
            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            if (result != null && result != DBNull.Value)
            {
                maxNos = Convert.ToInt32(result);
            }
        }
        catch
        {
            maxNos = 0;
        }

        var counterStr = string.IsNullOrWhiteSpace(counter) ? "" : counter.Trim();
        return $"CAR{counterStr}{(maxNos + 1):D4}";
    }

    public async Task<CardAdjustmentReceipt?> GetByReceiptNoAsync(string receiptNo, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(receiptNo)) return null;

        await AccessCardAdjustmentSchemaMigrator.EnsureSchemaAsync(_connectionString, cancellationToken);

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string headerSql = @"
            SELECT Nos, ReceiptNo, ReceiptDate, Branch, CardAccount, BankAccount,
                   ReceivableAccount, CommissionAccount, CommissionPercent, FromDate, ToDate,
                   TotalCardAmount, TotalCommissionAmount, TotalReceivedAmount,
                   TaxableAmount, SgstTaxAmount, CgstTaxAmount, IgstTaxAmount, RofAmount,
                   InvoiceType, TaxType, TaxManual, Remarks, VouNo, Status, CreatedBy, CreatedDate
            FROM CardAdjustmentReceipt
            WHERE ReceiptNo = ?";

        CardAdjustmentReceipt? receipt = null;
        await using (var cmd = new OleDbCommand(headerSql, connection))
        {
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = receiptNo });
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                receipt = new CardAdjustmentReceipt
                {
                    Nos = reader["Nos"] is DBNull ? 0 : Convert.ToInt32(reader["Nos"]),
                    ReceiptNo = reader["ReceiptNo"]?.ToString() ?? string.Empty,
                    ReceiptDate = reader["ReceiptDate"] is DBNull ? DateTime.Today : Convert.ToDateTime(reader["ReceiptDate"]),
                    Branch = reader["Branch"]?.ToString() ?? "HO",
                    CardAccount = reader["CardAccount"]?.ToString() ?? string.Empty,
                    BankAccount = reader["BankAccount"]?.ToString() ?? string.Empty,
                    ReceivableAccount = reader["ReceivableAccount"]?.ToString() ?? string.Empty,
                    CommissionAccount = reader["CommissionAccount"]?.ToString() ?? string.Empty,
                    CommissionPercent = reader["CommissionPercent"] is DBNull ? 0m : Convert.ToDecimal(reader["CommissionPercent"]),
                    FromDate = reader["FromDate"] is DBNull ? DateTime.Today.AddDays(-30) : Convert.ToDateTime(reader["FromDate"]),
                    ToDate = reader["ToDate"] is DBNull ? DateTime.Today : Convert.ToDateTime(reader["ToDate"]),
                    TotalCardAmount = reader["TotalCardAmount"] is DBNull ? 0m : Convert.ToDecimal(reader["TotalCardAmount"]),
                    TotalCommissionAmount = reader["TotalCommissionAmount"] is DBNull ? 0m : Convert.ToDecimal(reader["TotalCommissionAmount"]),
                    TotalReceivedAmount = reader["TotalReceivedAmount"] is DBNull ? 0m : Convert.ToDecimal(reader["TotalReceivedAmount"]),
                    TaxableAmount = reader["TaxableAmount"] is DBNull ? 0m : Convert.ToDecimal(reader["TaxableAmount"]),
                    SgstTaxAmount = reader["SgstTaxAmount"] is DBNull ? 0m : Convert.ToDecimal(reader["SgstTaxAmount"]),
                    CgstTaxAmount = reader["CgstTaxAmount"] is DBNull ? 0m : Convert.ToDecimal(reader["CgstTaxAmount"]),
                    IgstTaxAmount = reader["IgstTaxAmount"] is DBNull ? 0m : Convert.ToDecimal(reader["IgstTaxAmount"]),
                    RofAmount = reader["RofAmount"] is DBNull ? 0m : Convert.ToDecimal(reader["RofAmount"]),
                    InvoiceType = reader["InvoiceType"]?.ToString() ?? "GST",
                    TaxType = reader["TaxType"]?.ToString() ?? "-- None --",
                    TaxManual = reader["TaxManual"] is not DBNull && Convert.ToBoolean(reader["TaxManual"]),
                    Remarks = reader["Remarks"]?.ToString() ?? string.Empty,
                    VouNo = reader["VouNo"]?.ToString() ?? string.Empty,
                    Status = reader["Status"]?.ToString() ?? "Active",
                    CreatedBy = reader["CreatedBy"]?.ToString() ?? string.Empty,
                    CreatedDate = reader["CreatedDate"] is DBNull ? DateTime.Now : Convert.ToDateTime(reader["CreatedDate"])
                };
            }
        }

        if (receipt == null) return null;

        const string itemsSql = @"
            SELECT Id, ReceiptNo, SrNo, DbName, BillNo, BillDate, AccountName,
                   CardNo, CardBank, CardAmount, CommissionPercent, CommissionAmount, ReceivedAmount
            FROM CardAdjustmentReceiptItem
            WHERE ReceiptNo = ?
            ORDER BY SrNo, Id";

        var items = new List<CardAdjustmentReceiptItem>();
        await using (var cmd = new OleDbCommand(itemsSql, connection))
        {
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = receiptNo });
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(new CardAdjustmentReceiptItem
                {
                    Id = reader["Id"] is DBNull ? 0 : Convert.ToInt64(reader["Id"]),
                    ReceiptNo = reader["ReceiptNo"]?.ToString() ?? string.Empty,
                    SrNo = reader["SrNo"] is DBNull ? 0 : Convert.ToInt32(reader["SrNo"]),
                    DbName = reader["DbName"]?.ToString() ?? "SALE",
                    BillNo = reader["BillNo"]?.ToString() ?? string.Empty,
                    BillDate = reader["BillDate"] is DBNull ? DateTime.Today : Convert.ToDateTime(reader["BillDate"]),
                    AccountName = reader["AccountName"]?.ToString() ?? string.Empty,
                    CardNo = reader["CardNo"]?.ToString() ?? string.Empty,
                    CardBank = reader["CardBank"]?.ToString() ?? string.Empty,
                    CardAmount = reader["CardAmount"] is DBNull ? 0m : Convert.ToDecimal(reader["CardAmount"]),
                    CommissionPercent = reader["CommissionPercent"] is DBNull ? 0m : Convert.ToDecimal(reader["CommissionPercent"]),
                    CommissionAmount = reader["CommissionAmount"] is DBNull ? 0m : Convert.ToDecimal(reader["CommissionAmount"]),
                    ReceivedAmount = reader["ReceivedAmount"] is DBNull ? 0m : Convert.ToDecimal(reader["ReceivedAmount"]),
                    IsSelected = true
                });
            }
        }

        receipt.Items = items;
        return receipt;
    }

    public async Task<IReadOnlyList<CardAdjustmentReceipt>> GetAllAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
    {
        await AccessCardAdjustmentSchemaMigrator.EnsureSchemaAsync(_connectionString, cancellationToken);

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        string sql = @"
            SELECT Nos, ReceiptNo, ReceiptDate, Branch, CardAccount, BankAccount,
                   ReceivableAccount, CommissionAccount, CommissionPercent, FromDate, ToDate,
                   TotalCardAmount, TotalCommissionAmount, TotalReceivedAmount,
                   TaxableAmount, SgstTaxAmount, CgstTaxAmount, IgstTaxAmount, RofAmount,
                   InvoiceType, TaxType, TaxManual, Remarks, VouNo, Status, CreatedBy, CreatedDate
            FROM CardAdjustmentReceipt
            WHERE (Status IS NULL OR Status <> 'Deleted')";

        if (fromDate.HasValue && toDate.HasValue)
        {
            sql += " AND ReceiptDate >= ? AND ReceiptDate < ?";
        }
        sql += " ORDER BY ReceiptDate DESC, Nos DESC";

        var list = new List<CardAdjustmentReceipt>();
        await using (var cmd = new OleDbCommand(sql, connection))
        {
            if (fromDate.HasValue && toDate.HasValue)
            {
                cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Date, Value = fromDate.Value.Date });
                cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Date, Value = toDate.Value.Date.AddDays(1) });
            }

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                list.Add(new CardAdjustmentReceipt
                {
                    Nos = reader["Nos"] is DBNull ? 0 : Convert.ToInt32(reader["Nos"]),
                    ReceiptNo = reader["ReceiptNo"]?.ToString() ?? string.Empty,
                    ReceiptDate = reader["ReceiptDate"] is DBNull ? DateTime.Today : Convert.ToDateTime(reader["ReceiptDate"]),
                    Branch = reader["Branch"]?.ToString() ?? "HO",
                    CardAccount = reader["CardAccount"]?.ToString() ?? string.Empty,
                    BankAccount = reader["BankAccount"]?.ToString() ?? string.Empty,
                    ReceivableAccount = reader["ReceivableAccount"]?.ToString() ?? string.Empty,
                    CommissionAccount = reader["CommissionAccount"]?.ToString() ?? string.Empty,
                    CommissionPercent = reader["CommissionPercent"] is DBNull ? 0m : Convert.ToDecimal(reader["CommissionPercent"]),
                    FromDate = reader["FromDate"] is DBNull ? DateTime.Today.AddDays(-30) : Convert.ToDateTime(reader["FromDate"]),
                    ToDate = reader["ToDate"] is DBNull ? DateTime.Today : Convert.ToDateTime(reader["ToDate"]),
                    TotalCardAmount = reader["TotalCardAmount"] is DBNull ? 0m : Convert.ToDecimal(reader["TotalCardAmount"]),
                    TotalCommissionAmount = reader["TotalCommissionAmount"] is DBNull ? 0m : Convert.ToDecimal(reader["TotalCommissionAmount"]),
                    TotalReceivedAmount = reader["TotalReceivedAmount"] is DBNull ? 0m : Convert.ToDecimal(reader["TotalReceivedAmount"]),
                    TaxableAmount = reader["TaxableAmount"] is DBNull ? 0m : Convert.ToDecimal(reader["TaxableAmount"]),
                    SgstTaxAmount = reader["SgstTaxAmount"] is DBNull ? 0m : Convert.ToDecimal(reader["SgstTaxAmount"]),
                    CgstTaxAmount = reader["CgstTaxAmount"] is DBNull ? 0m : Convert.ToDecimal(reader["CgstTaxAmount"]),
                    IgstTaxAmount = reader["IgstTaxAmount"] is DBNull ? 0m : Convert.ToDecimal(reader["IgstTaxAmount"]),
                    RofAmount = reader["RofAmount"] is DBNull ? 0m : Convert.ToDecimal(reader["RofAmount"]),
                    InvoiceType = reader["InvoiceType"]?.ToString() ?? "GST",
                    TaxType = reader["TaxType"]?.ToString() ?? "-- None --",
                    TaxManual = reader["TaxManual"] is not DBNull && Convert.ToBoolean(reader["TaxManual"]),
                    Remarks = reader["Remarks"]?.ToString() ?? string.Empty,
                    VouNo = reader["VouNo"]?.ToString() ?? string.Empty,
                    Status = reader["Status"]?.ToString() ?? "Active",
                    CreatedBy = reader["CreatedBy"]?.ToString() ?? string.Empty,
                    CreatedDate = reader["CreatedDate"] is DBNull ? DateTime.Now : Convert.ToDateTime(reader["CreatedDate"])
                });
            }
        }

        return list;
    }

    public async Task<IReadOnlyList<string>> GetCardBanksAsync(CancellationToken cancellationToken = default)
    {
        await AccessCardAdjustmentSchemaMigrator.EnsureSchemaAsync(_connectionString, cancellationToken);

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // 1. From CardSetup
        try
        {
            await using var cmd = new OleDbCommand("SELECT CardBank FROM CardSetup WHERE CardBank IS NOT NULL AND CardBank <> ''", connection);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var val = reader[0]?.ToString();
                if (!string.IsNullOrWhiteSpace(val)) set.Add(val.Trim());
            }
        }
        catch { }

        // 2. From AccountCreation (Bank/Card ledgers)
        try
        {
            await using var cmd = new OleDbCommand("SELECT AccountName FROM AccountCreation WHERE HeadCategory = 'Bank A/c' OR HeadCategory LIKE '%Card%' OR AccountName LIKE '%Card%' OR AccountName LIKE '%UPI%'", connection);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var val = reader[0]?.ToString();
                if (!string.IsNullOrWhiteSpace(val)) set.Add(val.Trim());
            }
        }
        catch { }

        // 3. From PaymentSubform (Cards/Banks used in actual transactions)
        try
        {
            await using var cmd = new OleDbCommand("SELECT DISTINCT AccountName FROM PaymentSubform WHERE HeadCategory = 'Bank A/c' OR HeadCategory LIKE '%Card%' OR AccountName LIKE '%Card%' OR AccountName LIKE '%UPI%'", connection);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var val = reader[0]?.ToString();
                if (!string.IsNullOrWhiteSpace(val)) set.Add(val.Trim());
            }
        }
        catch { }

        try
        {
            await using var cmd = new OleDbCommand("SELECT DISTINCT BankName FROM PaymentSubform WHERE BankName IS NOT NULL AND BankName <> ''", connection);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var val = reader[0]?.ToString();
                if (!string.IsNullOrWhiteSpace(val)) set.Add(val.Trim());
            }
        }
        catch { }

        // 4. From Bill (if CardBank column exists)
        try
        {
            await using var cmd = new OleDbCommand("SELECT DISTINCT CardBank FROM Bill WHERE CardBank IS NOT NULL AND CardBank <> ''", connection);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var val = reader[0]?.ToString();
                if (!string.IsNullOrWhiteSpace(val)) set.Add(val.Trim());
            }
        }
        catch { }

        return set.OrderBy(s => s).ToList();
    }

    public async Task<IReadOnlyList<CardAdjustmentReceiptItem>> GetPendingCardTransactionsAsync(
        string cardBank, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(cardBank)) return Array.Empty<CardAdjustmentReceiptItem>();

        await AccessCardAdjustmentSchemaMigrator.EnsureSchemaAsync(_connectionString, cancellationToken);

        var normalizedCard = cardBank.Trim();
        var from = fromDate.Date;
        var to = toDate.Date.AddDays(1);

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var items = new List<CardAdjustmentReceiptItem>();
        var seenBills = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int sr = 1;

        // Query 1: From PaymentSubform joined with Bill
        const string paySql = @"
            SELECT p.VouNo, b.BillDate, b.CustomerName, b.AccountName, p.Amount AS CardAmount,
                   p.AccountName AS CardAccount, p.BankName, p.ChequeNo AS TranNo
            FROM PaymentSubform p INNER JOIN Bill b ON p.VouNo = b.VouNo
            WHERE (p.AccountName = ? OR p.BankName = ?)
              AND p.Amount > 0
              AND b.BillDate >= ? AND b.BillDate < ?
              AND (b.Status IS NULL OR b.Status <> 'Deleted')
              AND (p.Reconcilation IS NULL OR p.Reconcilation = False)
              AND (b.Reconcilation IS NULL OR b.Reconcilation = False)
            ORDER BY b.BillDate, p.VouNo";

        try
        {
            await using var cmd = new OleDbCommand(paySql, connection);
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = normalizedCard });
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = normalizedCard });
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Date, Value = from });
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Date, Value = to });

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var billNo = reader["VouNo"]?.ToString() ?? string.Empty;
                var custName = reader["CustomerName"]?.ToString();
                var accName = reader["AccountName"]?.ToString();
                var displayName = !string.IsNullOrWhiteSpace(custName) ? custName : (accName ?? string.Empty);
                var cardAmt = reader["CardAmount"] is DBNull ? 0m : Convert.ToDecimal(reader["CardAmount"]);
                var tranNo = reader["TranNo"]?.ToString() ?? string.Empty;
                var bDate = reader["BillDate"] is DBNull ? DateTime.Today : Convert.ToDateTime(reader["BillDate"]);

                items.Add(new CardAdjustmentReceiptItem
                {
                    SrNo = sr++,
                    DbName = "SALE",
                    BillNo = billNo,
                    BillDate = bDate,
                    AccountName = displayName,
                    CardNo = tranNo,
                    CardBank = normalizedCard,
                    CardAmount = cardAmt,
                    CommissionPercent = 0,
                    CommissionAmount = 0,
                    ReceivedAmount = cardAmt,
                    IsSelected = true
                });

                if (!string.IsNullOrWhiteSpace(billNo))
                {
                    seenBills.Add(billNo);
                }
            }
        }
        catch { }

        // Query 2: Direct Bill columns (for backward compatibility if Bill has CreditCard / CardBank)
        const string billSql = @"
            SELECT BillNo, BillDate, CustomerName, AccountName, CreditCard AS CardAmount, CardBank, TranNo
            FROM Bill
            WHERE CardBank = ? AND CreditCard > 0 AND BillDate >= ? AND BillDate < ?
              AND (Status IS NULL OR Status <> 'Deleted')
              AND (Reconcilation IS NULL OR Reconcilation = False)";

        try
        {
            await using var cmd = new OleDbCommand(billSql, connection);
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = normalizedCard });
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Date, Value = from });
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Date, Value = to });

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var billNo = reader["BillNo"]?.ToString() ?? string.Empty;
                if (seenBills.Contains(billNo)) continue;

                var custName = reader["CustomerName"]?.ToString();
                var accName = reader["AccountName"]?.ToString();
                var displayName = !string.IsNullOrWhiteSpace(custName) ? custName : (accName ?? string.Empty);
                var cardAmt = reader["CardAmount"] is DBNull ? 0m : Convert.ToDecimal(reader["CardAmount"]);
                var tranNo = reader["TranNo"]?.ToString() ?? string.Empty;
                var bDate = reader["BillDate"] is DBNull ? DateTime.Today : Convert.ToDateTime(reader["BillDate"]);

                items.Add(new CardAdjustmentReceiptItem
                {
                    SrNo = sr++,
                    DbName = "SALE",
                    BillNo = billNo,
                    BillDate = bDate,
                    AccountName = displayName,
                    CardNo = tranNo,
                    CardBank = normalizedCard,
                    CardAmount = cardAmt,
                    CommissionPercent = 0,
                    CommissionAmount = 0,
                    ReceivedAmount = cardAmt,
                    IsSelected = true
                });

                seenBills.Add(billNo);
            }
        }
        catch { }

        return items;
    }

    public async Task SaveAsync(CardAdjustmentReceipt receipt, bool updateExisting = false, CancellationToken cancellationToken = default)
    {
        await AccessCardAdjustmentSchemaMigrator.EnsureSchemaAsync(_connectionString, cancellationToken);

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        try
        {
            if (updateExisting)
            {
                // Revert previous reconciliation flags
                const string getOldBillsSql = "SELECT BillNo FROM CardAdjustmentReceiptItem WHERE ReceiptNo = ?";
                var oldBills = new List<string>();
                using (var cmdOld = connection.CreateCommand())
                {
                    cmdOld.Transaction = transaction;
                    cmdOld.CommandText = getOldBillsSql;
                    cmdOld.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = receipt.ReceiptNo });
                    using var reader = await cmdOld.ExecuteReaderAsync(cancellationToken);
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        var b = reader[0]?.ToString();
                        if (!string.IsNullOrWhiteSpace(b)) oldBills.Add(b);
                    }
                }

                foreach (var b in oldBills)
                {
                    using var cmdRev1 = connection.CreateCommand();
                    cmdRev1.Transaction = transaction;
                    cmdRev1.CommandText = "UPDATE PaymentSubform SET Reconcilation = False WHERE VouNo = ?";
                    cmdRev1.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = b });
                    await cmdRev1.ExecuteNonQueryAsync(cancellationToken);

                    using var cmdRev2 = connection.CreateCommand();
                    cmdRev2.Transaction = transaction;
                    cmdRev2.CommandText = "UPDATE Bill SET Reconcilation = False WHERE VouNo = ?";
                    cmdRev2.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = b });
                    try { await cmdRev2.ExecuteNonQueryAsync(cancellationToken); } catch { }
                }

                // Delete old vouchers if any
                if (!string.IsNullOrWhiteSpace(receipt.VouNo))
                {
                    using var cmdDelSub = connection.CreateCommand();
                    cmdDelSub.Transaction = transaction;
                    cmdDelSub.CommandText = "DELETE FROM RecSubform WHERE VouNo = ? OR VouNo = ?";
                    cmdDelSub.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = receipt.VouNo });
                    cmdDelSub.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = $"{receipt.VouNo}-COMM" });
                    await cmdDelSub.ExecuteNonQueryAsync(cancellationToken);

                    using var cmdDelRec = connection.CreateCommand();
                    cmdDelRec.Transaction = transaction;
                    cmdDelRec.CommandText = "DELETE FROM RECIEPTS WHERE VouNo = ? OR VouNo = ?";
                    cmdDelRec.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = receipt.VouNo });
                    cmdDelRec.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = $"{receipt.VouNo}-COMM" });
                    await cmdDelRec.ExecuteNonQueryAsync(cancellationToken);
                }

                using var cmdDelItems = connection.CreateCommand();
                cmdDelItems.Transaction = transaction;
                cmdDelItems.CommandText = "DELETE FROM CardAdjustmentReceiptItem WHERE ReceiptNo = ?";
                cmdDelItems.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = receipt.ReceiptNo });
                await cmdDelItems.ExecuteNonQueryAsync(cancellationToken);

                using var cmdDelHead = connection.CreateCommand();
                cmdDelHead.Transaction = transaction;
                cmdDelHead.CommandText = "DELETE FROM CardAdjustmentReceipt WHERE ReceiptNo = ?";
                cmdDelHead.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = receipt.ReceiptNo });
                await cmdDelHead.ExecuteNonQueryAsync(cancellationToken);
            }

            if (receipt.Nos <= 0)
            {
                const string maxSql = "SELECT IIf(IsNull(Max([Nos])), 0, Max([Nos])) + 1 FROM CardAdjustmentReceipt";
                using var cmdMax = connection.CreateCommand();
                cmdMax.Transaction = transaction;
                cmdMax.CommandText = maxSql;
                var res = await cmdMax.ExecuteScalarAsync(cancellationToken);
                receipt.Nos = (res != null && res != DBNull.Value) ? Convert.ToInt32(res) : 1;
            }

            if (string.IsNullOrWhiteSpace(receipt.ReceiptNo))
            {
                receipt.ReceiptNo = $"CAR{receipt.Nos:D4}";
            }

            if (string.IsNullOrWhiteSpace(receipt.VouNo))
            {
                const string maxVouSql = "SELECT IIf(IsNull(Max([Nos])), 0, Max([Nos])) + 1 FROM RECIEPTS WHERE VouType IN ('Receipt', 'RECEIPT', 'Receipts', 'RECEIPTS')";
                using var cmdVouMax = connection.CreateCommand();
                cmdVouMax.Transaction = transaction;
                cmdVouMax.CommandText = maxVouSql;
                var resVou = await cmdVouMax.ExecuteScalarAsync(cancellationToken);
                var nextVouNos = (resVou != null && resVou != DBNull.Value) ? Convert.ToInt32(resVou) : 1;
                receipt.VouNo = $"RC{nextVouNos:D4}";
            }

            // 1. Insert header
            const string insertHeaderSql = @"
                INSERT INTO CardAdjustmentReceipt
                (Nos, ReceiptNo, ReceiptDate, Branch, CardAccount, BankAccount, ReceivableAccount,
                 CommissionAccount, CommissionPercent, FromDate, ToDate, TotalCardAmount,
                 TotalCommissionAmount, TotalReceivedAmount, TaxableAmount, SgstTaxAmount,
                 CgstTaxAmount, IgstTaxAmount, RofAmount, InvoiceType, TaxType, TaxManual,
                 Remarks, VouNo, Status, CreatedBy, CreatedDate)
                VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)";

            using (var cmdInsHead = connection.CreateCommand())
            {
                cmdInsHead.Transaction = transaction;
                cmdInsHead.CommandText = insertHeaderSql;
                cmdInsHead.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Integer, Value = receipt.Nos });
                cmdInsHead.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = receipt.ReceiptNo });
                cmdInsHead.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Date, Value = receipt.ReceiptDate });
                cmdInsHead.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = receipt.Branch ?? "HO" });
                cmdInsHead.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = receipt.CardAccount ?? string.Empty });
                cmdInsHead.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = receipt.BankAccount ?? string.Empty });
                cmdInsHead.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = receipt.ReceivableAccount ?? string.Empty });
                cmdInsHead.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = receipt.CommissionAccount ?? string.Empty });
                cmdInsHead.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Currency, Value = receipt.CommissionPercent });
                cmdInsHead.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Date, Value = receipt.FromDate });
                cmdInsHead.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Date, Value = receipt.ToDate });
                cmdInsHead.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Currency, Value = receipt.TotalCardAmount });
                cmdInsHead.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Currency, Value = receipt.TotalCommissionAmount });
                cmdInsHead.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Currency, Value = receipt.TotalReceivedAmount });
                cmdInsHead.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Currency, Value = receipt.TaxableAmount });
                cmdInsHead.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Currency, Value = receipt.SgstTaxAmount });
                cmdInsHead.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Currency, Value = receipt.CgstTaxAmount });
                cmdInsHead.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Currency, Value = receipt.IgstTaxAmount });
                cmdInsHead.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Currency, Value = receipt.RofAmount });
                cmdInsHead.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = receipt.InvoiceType ?? "GST" });
                cmdInsHead.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = receipt.TaxType ?? "-- None --" });
                cmdInsHead.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Boolean, Value = receipt.TaxManual });
                cmdInsHead.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.LongVarWChar, Value = receipt.Remarks ?? string.Empty });
                cmdInsHead.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = receipt.VouNo ?? string.Empty });
                cmdInsHead.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = receipt.Status ?? "Active" });
                cmdInsHead.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = receipt.CreatedBy ?? string.Empty });
                cmdInsHead.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Date, Value = DateTime.Now });

                await cmdInsHead.ExecuteNonQueryAsync(cancellationToken);
            }

            // 2. Insert items and update Reconcilation flags
            const string insertItemSql = @"
                INSERT INTO CardAdjustmentReceiptItem
                (ReceiptNo, SrNo, DbName, BillNo, BillDate, AccountName, CardNo, CardBank,
                 CardAmount, CommissionPercent, CommissionAmount, ReceivedAmount)
                VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)";

            int sr = 1;
            var selectedItems = receipt.Items.Where(i => i.IsSelected).ToList();
            foreach (var item in selectedItems)
            {
                using (var cmdInsItem = connection.CreateCommand())
                {
                    cmdInsItem.Transaction = transaction;
                    cmdInsItem.CommandText = insertItemSql;
                    cmdInsItem.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = receipt.ReceiptNo });
                    cmdInsItem.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Integer, Value = sr++ });
                    cmdInsItem.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = item.DbName ?? "SALE" });
                    cmdInsItem.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = item.BillNo ?? string.Empty });
                    cmdInsItem.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Date, Value = item.BillDate });
                    cmdInsItem.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = item.AccountName ?? string.Empty });
                    cmdInsItem.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = item.CardNo ?? string.Empty });
                    cmdInsItem.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = item.CardBank ?? string.Empty });
                    cmdInsItem.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Currency, Value = item.CardAmount });
                    cmdInsItem.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Currency, Value = item.CommissionPercent });
                    cmdInsItem.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Currency, Value = item.CommissionAmount });
                    cmdInsItem.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Currency, Value = item.ReceivedAmount });

                    await cmdInsItem.ExecuteNonQueryAsync(cancellationToken);
                }

                if (!string.IsNullOrWhiteSpace(item.BillNo))
                {
                    using var cmdRec1 = connection.CreateCommand();
                    cmdRec1.Transaction = transaction;
                    cmdRec1.CommandText = "UPDATE PaymentSubform SET Reconcilation = True WHERE VouNo = ?";
                    cmdRec1.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = item.BillNo });
                    await cmdRec1.ExecuteNonQueryAsync(cancellationToken);

                    using var cmdRec2 = connection.CreateCommand();
                    cmdRec2.Transaction = transaction;
                    cmdRec2.CommandText = "UPDATE Bill SET Reconcilation = True WHERE VouNo = ?";
                    cmdRec2.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = item.BillNo });
                    try { await cmdRec2.ExecuteNonQueryAsync(cancellationToken); } catch { }
                }
            }

            // 3. Post to RECIEPTS & RecSubform
            var now = DateTime.Now;
            var entryTime = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second);

            const string insertVoucherSql = @"
                INSERT INTO RECIEPTS
                (Nos, VouNo, VouType, VouDate, Narration, DrAccount, DrAmt, CrAccount, CrAmt,
                 [User], Status, [Date], [Time])
                VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)";

            const string maxVouNosSql = "SELECT IIf(IsNull(Max([Nos])), 0, Max([Nos])) + 1 FROM RECIEPTS WHERE VouType IN ('Receipt', 'RECEIPT', 'Receipts', 'RECEIPTS')";
            int vouNos = 1;
            using (var cmdNos = connection.CreateCommand())
            {
                cmdNos.Transaction = transaction;
                cmdNos.CommandText = maxVouNosSql;
                var res = await cmdNos.ExecuteScalarAsync(cancellationToken);
                if (res != null && res != DBNull.Value) vouNos = Convert.ToInt32(res);
            }

            var cardAccountCr = !string.IsNullOrWhiteSpace(receipt.ReceivableAccount) ? receipt.ReceivableAccount : receipt.CardAccount;
            var narration = !string.IsNullOrWhiteSpace(receipt.Remarks)
                ? receipt.Remarks
                : $"Card adjustment {receipt.ReceiptNo}: {receipt.CardAccount} settled to {receipt.BankAccount}";

            using (var cmdVou = connection.CreateCommand())
            {
                cmdVou.Transaction = transaction;
                cmdVou.CommandText = insertVoucherSql;
                cmdVou.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Integer, Value = vouNos });
                cmdVou.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = receipt.VouNo });
                cmdVou.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = "Receipt" });
                cmdVou.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Date, Value = receipt.ReceiptDate });
                cmdVou.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = narration });
                cmdVou.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = receipt.BankAccount });
                cmdVou.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Currency, Value = receipt.TotalReceivedAmount });
                cmdVou.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = cardAccountCr });
                cmdVou.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Currency, Value = receipt.TotalReceivedAmount });
                cmdVou.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = receipt.CreatedBy ?? string.Empty });
                cmdVou.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = "Active" });
                cmdVou.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Date, Value = DateTime.Today });
                cmdVou.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Date, Value = entryTime });

                await cmdVou.ExecuteNonQueryAsync(cancellationToken);
            }

            const string insertLineSql = "INSERT INTO RecSubform (VouNo, Description, Symbol, Amount) VALUES (?, ?, ?, ?)";
            using (var cmdLine1 = connection.CreateCommand())
            {
                cmdLine1.Transaction = transaction;
                cmdLine1.CommandText = insertLineSql;
                cmdLine1.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = receipt.VouNo });
                cmdLine1.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = $"Net card swipe deposit: {receipt.CardAccount}" });
                cmdLine1.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = "Dr" });
                cmdLine1.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Currency, Value = receipt.TotalReceivedAmount });
                await cmdLine1.ExecuteNonQueryAsync(cancellationToken);
            }

            using (var cmdLine2 = connection.CreateCommand())
            {
                cmdLine2.Transaction = transaction;
                cmdLine2.CommandText = insertLineSql;
                cmdLine2.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = receipt.VouNo });
                cmdLine2.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = $"Card clearing settlement {receipt.ReceiptNo}" });
                cmdLine2.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = "Cr" });
                cmdLine2.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Currency, Value = receipt.TotalReceivedAmount });
                await cmdLine2.ExecuteNonQueryAsync(cancellationToken);
            }

            // 4. Commission Journal Voucher
            if (receipt.TotalCommissionAmount > 0)
            {
                var commVouNo = $"{receipt.VouNo}-COMM";
                var commAccount = !string.IsNullOrWhiteSpace(receipt.CommissionAccount)
                    ? receipt.CommissionAccount
                    : "CREDIT CARD CHARGES";

                const string maxJvNosSql = "SELECT IIf(IsNull(Max([Nos])), 0, Max([Nos])) + 1 FROM RECIEPTS WHERE VouType IN ('Journal', 'JOURNAL', 'Journals', 'JOURNALS')";
                int jvNos = 1;
                using (var cmdJv = connection.CreateCommand())
                {
                    cmdJv.Transaction = transaction;
                    cmdJv.CommandText = maxJvNosSql;
                    var res = await cmdJv.ExecuteScalarAsync(cancellationToken);
                    if (res != null && res != DBNull.Value) jvNos = Convert.ToInt32(res);
                }

                using (var cmdVouJv = connection.CreateCommand())
                {
                    cmdVouJv.Transaction = transaction;
                    cmdVouJv.CommandText = insertVoucherSql;
                    cmdVouJv.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Integer, Value = jvNos });
                    cmdVouJv.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = commVouNo });
                    cmdVouJv.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = "Journal" });
                    cmdVouJv.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Date, Value = receipt.ReceiptDate });
                    cmdVouJv.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = $"Commission & bank charges on card adjustment {receipt.ReceiptNo} ({receipt.CardAccount})" });
                    cmdVouJv.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = commAccount });
                    cmdVouJv.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Currency, Value = receipt.TotalCommissionAmount });
                    cmdVouJv.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = cardAccountCr });
                    cmdVouJv.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Currency, Value = receipt.TotalCommissionAmount });
                    cmdVouJv.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = receipt.CreatedBy ?? string.Empty });
                    cmdVouJv.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = "Active" });
                    cmdVouJv.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Date, Value = DateTime.Today });
                    cmdVouJv.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Date, Value = entryTime });

                    await cmdVouJv.ExecuteNonQueryAsync(cancellationToken);
                }

                using (var cmdLine3 = connection.CreateCommand())
                {
                    cmdLine3.Transaction = transaction;
                    cmdLine3.CommandText = insertLineSql;
                    cmdLine3.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = commVouNo });
                    cmdLine3.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = "Credit card swipe commission fee" });
                    cmdLine3.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = "Dr" });
                    cmdLine3.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Currency, Value = receipt.TotalCommissionAmount });
                    await cmdLine3.ExecuteNonQueryAsync(cancellationToken);
                }

                using (var cmdLine4 = connection.CreateCommand())
                {
                    cmdLine4.Transaction = transaction;
                    cmdLine4.CommandText = insertLineSql;
                    cmdLine4.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = commVouNo });
                    cmdLine4.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = "Commission deducted from card clearing" });
                    cmdLine4.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = "Cr" });
                    cmdLine4.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Currency, Value = receipt.TotalCommissionAmount });
                    await cmdLine4.ExecuteNonQueryAsync(cancellationToken);
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

    public async Task DeleteAsync(string receiptNo, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(receiptNo)) return;

        await AccessCardAdjustmentSchemaMigrator.EnsureSchemaAsync(_connectionString, cancellationToken);

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        try
        {
            // 1. Revert reconciliation on bills
            const string getBillsSql = "SELECT BillNo FROM CardAdjustmentReceiptItem WHERE ReceiptNo = ?";
            var bills = new List<string>();
            using (var cmdBills = connection.CreateCommand())
            {
                cmdBills.Transaction = transaction;
                cmdBills.CommandText = getBillsSql;
                cmdBills.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = receiptNo });
                using var reader = await cmdBills.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var b = reader[0]?.ToString();
                    if (!string.IsNullOrWhiteSpace(b)) bills.Add(b);
                }
            }

            foreach (var b in bills)
            {
                using var cmd1 = connection.CreateCommand();
                cmd1.Transaction = transaction;
                cmd1.CommandText = "UPDATE PaymentSubform SET Reconcilation = False WHERE VouNo = ?";
                cmd1.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = b });
                await cmd1.ExecuteNonQueryAsync(cancellationToken);

                using var cmd2 = connection.CreateCommand();
                cmd2.Transaction = transaction;
                cmd2.CommandText = "UPDATE Bill SET Reconcilation = False WHERE VouNo = ?";
                cmd2.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = b });
                try { await cmd2.ExecuteNonQueryAsync(cancellationToken); } catch { }
            }

            // 2. Fetch header to get VouNo
            string vouNo = string.Empty;
            const string getVouSql = "SELECT VouNo FROM CardAdjustmentReceipt WHERE ReceiptNo = ?";
            using (var cmdVou = connection.CreateCommand())
            {
                cmdVou.Transaction = transaction;
                cmdVou.CommandText = getVouSql;
                cmdVou.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = receiptNo });
                var res = await cmdVou.ExecuteScalarAsync(cancellationToken);
                if (res != null && res != DBNull.Value) vouNo = res.ToString() ?? string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(vouNo))
            {
                using var cmdSub = connection.CreateCommand();
                cmdSub.Transaction = transaction;
                cmdSub.CommandText = "DELETE FROM RecSubform WHERE VouNo = ? OR VouNo = ?";
                cmdSub.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = vouNo });
                cmdSub.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = $"{vouNo}-COMM" });
                await cmdSub.ExecuteNonQueryAsync(cancellationToken);

                using var cmdRec = connection.CreateCommand();
                cmdRec.Transaction = transaction;
                cmdRec.CommandText = "DELETE FROM RECIEPTS WHERE VouNo = ? OR VouNo = ?";
                cmdRec.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = vouNo });
                cmdRec.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = $"{vouNo}-COMM" });
                await cmdRec.ExecuteNonQueryAsync(cancellationToken);
            }

            using var cmdDelItems = connection.CreateCommand();
            cmdDelItems.Transaction = transaction;
            cmdDelItems.CommandText = "DELETE FROM CardAdjustmentReceiptItem WHERE ReceiptNo = ?";
            cmdDelItems.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = receiptNo });
            await cmdDelItems.ExecuteNonQueryAsync(cancellationToken);

            using var cmdDelHead = connection.CreateCommand();
            cmdDelHead.Transaction = transaction;
            cmdDelHead.CommandText = "UPDATE CardAdjustmentReceipt SET Status = 'Deleted' WHERE ReceiptNo = ?";
            cmdDelHead.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = receiptNo });
            await cmdDelHead.ExecuteNonQueryAsync(cancellationToken);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<string?> NavigateReceiptNoAsync(string? currentReceiptNo, ReceiptNavigationDirection direction, CancellationToken cancellationToken = default)
    {
        await AccessCardAdjustmentSchemaMigrator.EnsureSchemaAsync(_connectionString, cancellationToken);

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        if (direction == ReceiptNavigationDirection.First)
        {
            const string sql = "SELECT TOP 1 ReceiptNo FROM CardAdjustmentReceipt WHERE (Status IS NULL OR Status <> 'Deleted') ORDER BY ReceiptDate ASC, Nos ASC";
            await using var cmd = new OleDbCommand(sql, connection);
            var res = await cmd.ExecuteScalarAsync(cancellationToken);
            return res?.ToString();
        }

        if (direction == ReceiptNavigationDirection.Last)
        {
            const string sql = "SELECT TOP 1 ReceiptNo FROM CardAdjustmentReceipt WHERE (Status IS NULL OR Status <> 'Deleted') ORDER BY ReceiptDate DESC, Nos DESC";
            await using var cmd = new OleDbCommand(sql, connection);
            var res = await cmd.ExecuteScalarAsync(cancellationToken);
            return res?.ToString();
        }

        if (string.IsNullOrWhiteSpace(currentReceiptNo))
        {
            return await NavigateReceiptNoAsync(null, ReceiptNavigationDirection.Last, cancellationToken);
        }

        const string currentSql = "SELECT Nos, ReceiptDate FROM CardAdjustmentReceipt WHERE ReceiptNo = ?";
        int nos = 0;
        DateTime date = DateTime.MinValue;
        await using (var cmd = new OleDbCommand(currentSql, connection))
        {
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = currentReceiptNo });
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                nos = reader["Nos"] is DBNull ? 0 : Convert.ToInt32(reader["Nos"]);
                date = reader["ReceiptDate"] is DBNull ? DateTime.Today : Convert.ToDateTime(reader["ReceiptDate"]);
            }
            else
            {
                return await NavigateReceiptNoAsync(null, ReceiptNavigationDirection.Last, cancellationToken);
            }
        }

        if (direction == ReceiptNavigationDirection.Previous)
        {
            const string sql = @"
                SELECT TOP 1 ReceiptNo
                FROM CardAdjustmentReceipt
                WHERE (Status IS NULL OR Status <> 'Deleted')
                  AND (ReceiptDate < ? OR (ReceiptDate = ? AND Nos < ?))
                ORDER BY ReceiptDate DESC, Nos DESC";

            await using var cmd = new OleDbCommand(sql, connection);
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Date, Value = date });
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Date, Value = date });
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Integer, Value = nos });
            var prevNo = (await cmd.ExecuteScalarAsync(cancellationToken))?.ToString();
            return prevNo ?? currentReceiptNo;
        }

        if (direction == ReceiptNavigationDirection.Next)
        {
            const string sql = @"
                SELECT TOP 1 ReceiptNo
                FROM CardAdjustmentReceipt
                WHERE (Status IS NULL OR Status <> 'Deleted')
                  AND (ReceiptDate > ? OR (ReceiptDate = ? AND Nos > ?))
                ORDER BY ReceiptDate ASC, Nos ASC";

            await using var cmd = new OleDbCommand(sql, connection);
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Date, Value = date });
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Date, Value = date });
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Integer, Value = nos });
            var nextNo = (await cmd.ExecuteScalarAsync(cancellationToken))?.ToString();
            return nextNo ?? currentReceiptNo;
        }

        return currentReceiptNo;
    }
}
