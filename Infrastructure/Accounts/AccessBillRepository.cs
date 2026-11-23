using System.Data.Common;
using System.Data.OleDb;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.Infrastructure.Services;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class AccessBillRepository : IBillRepository
{
    private readonly string _connectionString;

    public AccessBillRepository(string databasePath)
    {
        _connectionString = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={databasePath};";
    }

    public async Task<IReadOnlyList<Bill>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM Bill ORDER BY EntryDate DESC, EntryTime DESC";
        var result = new List<Bill>();

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(MapBill(reader));
        }

        return result;
    }

    public async Task<Bill?> GetByVouNoAsync(string vouNo, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM Bill WHERE [VouNo] = @VouNo";

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        command.Parameters.Add("@VouNo", OleDbType.VarWChar, 25).Value = vouNo;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapBill(reader);
        }

        return null;
    }

    public async Task<IReadOnlyList<BillSubform>> GetSubformItemsAsync(string vouNo, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM BillSubform WHERE [VouNo] = @VouNo";
        var result = new List<BillSubform>();

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        command.Parameters.Add("@VouNo", OleDbType.VarWChar, 25).Value = vouNo;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(MapBillSubform(reader));
        }

        return result;
    }

    public async Task<IReadOnlyList<PaymentSubform>> GetPaymentItemsAsync(string vouNo, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM PaymentSubform WHERE [VouNo] = @VouNo";
        var result = new List<PaymentSubform>();

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        command.Parameters.Add("@VouNo", OleDbType.VarWChar, 25).Value = vouNo;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(MapPaymentSubform(reader));
        }

        return result;
    }

    public async Task AddAsync(Bill bill, IEnumerable<BillSubform> items, IEnumerable<PaymentSubform> payments, CancellationToken cancellationToken = default)
    {
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var transaction = connection.BeginTransaction();

        try
        {
            // Insert Bill Header
            const string insertBillSql = @"
                INSERT INTO [Bill]
                    ([VouNo], [VouType], [BillDate], [DueDate], [Phone], [CustomerName], [CustomerCode],
                     [Address], [Address2], [City], [Pincode], [Email], [VatNo], [Check], [BillCanceled],
                     [GrossAmt], [DiscountAllowed], [NetAmt], [ABeforeTax], [Tax], [TaxAmt], [Q1], [Q2], [Q3],
                     [Remarks], [Narration], [EntryUser], [EntryDate], [EntryTime], [ComputerName], [Status])
                VALUES
                    (@VouNo, @VouType, @BillDate, @DueDate, @Phone, @CustomerName, @CustomerCode,
                     @Address, @Address2, @City, @Pincode, @Email, @VatNo, @Check, @BillCanceled,
                     @GrossAmt, @DiscountAllowed, @NetAmt, @ABeforeTax, @Tax, @TaxAmt, @Q1, @Q2, @Q3,
                     @Remarks, @Narration, @EntryUser, @EntryDate, @EntryTime, @ComputerName, @Status)";

            await using (var cmd = new OleDbCommand(insertBillSql, connection, transaction))
            {
                AddBillParameters(cmd, bill);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // Determine max AutoNo for BillSubform
            int currentAutoNo = 0;
            const string getMaxAutoNoSql = "SELECT MAX(AutoNo) FROM BillSubform";
            await using (var maxCmd = new OleDbCommand(getMaxAutoNoSql, connection, transaction))
            {
                var maxVal = await maxCmd.ExecuteScalarAsync(cancellationToken);
                if (maxVal != null && maxVal != DBNull.Value)
                {
                    currentAutoNo = Convert.ToInt32(maxVal);
                }
            }

            // Insert Items
            const string insertItemSql = @"
                INSERT INTO [BillSubform]
                    ([AutoNo], [VouNo], [Barcode], [OldBarcode], [PcRate], [ItemName], [ENote], [Qty], [NtWt], [Rate],
                     [Amount], [GrossAmt], [DisPer], [DisAmt], [GrWt], [MperGr], [Making],
                     [MetalValue], [SalesMan], [Vat%], [VatAmt], [Taxable], [StoneValue], [Purity], [isDiscount])
                VALUES
                    (@AutoNo, @VouNo, @Barcode, @OldBarcode, @PcRate, @ItemName, @ENote, @Qty, @NtWt, @Rate,
                     @Amount, @GrossAmt, @DisPer, @DisAmt, @GrWt, @MperGr, @Making,
                     @MetalValue, @SalesMan, @VatPercent, @VatAmt, @Taxable, @StoneValue, @Purity, @isDiscount)";

            foreach (var item in items)
            {
                await using var cmd = new OleDbCommand(insertItemSql, connection, transaction);
                cmd.Parameters.Add("@AutoNo", OleDbType.Integer).Value = item.AutoNo > 0 ? item.AutoNo : ++currentAutoNo;
                cmd.Parameters.Add("@VouNo", OleDbType.VarWChar, 25).Value = bill.VouNo;
                cmd.Parameters.Add("@Barcode", OleDbType.VarWChar, 10).Value = item.Barcode ?? string.Empty;
                cmd.Parameters.Add("@OldBarcode", OleDbType.VarWChar, 50).Value = item.OldBarcode ?? string.Empty;
                cmd.Parameters.Add("@PcRate", OleDbType.Boolean).Value = item.PcRate;
                cmd.Parameters.Add("@ItemName", OleDbType.VarWChar, 50).Value = item.ItemName ?? string.Empty;
                cmd.Parameters.Add("@ENote", OleDbType.VarWChar, 50).Value = item.ENote ?? string.Empty;
                cmd.Parameters.Add("@Qty", OleDbType.Integer).Value = item.Qty;
                cmd.Parameters.Add("@NtWt", OleDbType.Currency).Value = item.NtWt;
                cmd.Parameters.Add("@Rate", OleDbType.Currency).Value = item.Rate;
                cmd.Parameters.Add("@Amount", OleDbType.Currency).Value = item.Amount;
                cmd.Parameters.Add("@GrossAmt", OleDbType.Currency).Value = item.GrossAmt;
                cmd.Parameters.Add("@DisPer", OleDbType.Currency).Value = item.DisPer;
                cmd.Parameters.Add("@DisAmt", OleDbType.Currency).Value = item.DisAmt;
                cmd.Parameters.Add("@GrWt", OleDbType.Currency).Value = item.GrWt;
                cmd.Parameters.Add("@MperGr", OleDbType.Currency).Value = item.MperGr;
                cmd.Parameters.Add("@Making", OleDbType.Currency).Value = item.Making;
                cmd.Parameters.Add("@MetalValue", OleDbType.Currency).Value = item.MetalValue;
                cmd.Parameters.Add("@SalesMan", OleDbType.VarWChar, 50).Value = string.IsNullOrWhiteSpace(item.SalesMan) ? (object)DBNull.Value : item.SalesMan;

                decimal subTaxRate = item.VatPercent > 0 ? item.VatPercent : 3.00m;
                decimal subTaxable = item.Taxable > 0 ? item.Taxable : (item.Amount > 0 ? Math.Round(item.Amount / (1m + (subTaxRate / 100m)), 2) : 0m);
                decimal subVatAmt = item.VatAmt > 0 && (item.VatAmt > 28m || Math.Abs(item.VatAmt - (item.Amount - subTaxable)) <= 1.0m)
                    ? item.VatAmt
                    : (item.Amount > 0 ? Math.Round(item.Amount - subTaxable, 2) : 0m);

                cmd.Parameters.Add("@VatPercent", OleDbType.Currency).Value = subTaxRate;
                cmd.Parameters.Add("@VatAmt", OleDbType.Currency).Value = subVatAmt;
                cmd.Parameters.Add("@Taxable", OleDbType.Currency).Value = subTaxable;
                cmd.Parameters.Add("@StoneValue", OleDbType.Currency).Value = item.StoneValue;
                var resolvedPurity = await ResolvePurityAsync(connection, transaction, item.Purity, cancellationToken);
                cmd.Parameters.Add("@Purity", OleDbType.VarWChar, 50).Value = resolvedPurity;
                cmd.Parameters.Add("@isDiscount", OleDbType.Boolean).Value = false;

                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await InsertPaymentsAsync(connection, transaction, bill.VouNo, payments, cancellationToken);

            // Deduct sold barcodes from TempStock
            await AccessTempStockSchemaMigrator.EnsureSchemaAsync(_connectionString, cancellationToken);
            var soldBarcodes = items.Where(i => !string.IsNullOrWhiteSpace(i.Barcode)).Select(i => i.Barcode!.Trim()).ToList();
            if (soldBarcodes.Count > 0)
            {
                await TempStockManager.RemoveSoldAsync(connection, transaction, soldBarcodes, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task UpdateAsync(Bill bill, IEnumerable<BillSubform> items, IEnumerable<PaymentSubform> payments, CancellationToken cancellationToken = default)
    {
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var transaction = connection.BeginTransaction();

        try
        {
            // Collect existing old barcodes before deleting subform
            var oldBarcodes = new List<string>();
            const string getOldBcSql = "SELECT [Barcode] FROM BillSubform WHERE [VouNo] = @VouNo AND [Barcode] IS NOT NULL AND [Barcode] <> ''";
            await using (var getCmd = new OleDbCommand(getOldBcSql, connection, transaction))
            {
                getCmd.Parameters.Add("@VouNo", OleDbType.VarWChar, 25).Value = bill.VouNo;
                await using var r = await getCmd.ExecuteReaderAsync(cancellationToken);
                while (await r.ReadAsync(cancellationToken))
                {
                    var bc = r[0]?.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(bc)) oldBarcodes.Add(bc);
                }
            }

            const string updateBillSql = @"
                UPDATE [Bill] SET
                    [VouType] = @VouType, [BillDate] = @BillDate, [DueDate] = @DueDate, [Phone] = @Phone,
                    [CustomerName] = @CustomerName, [CustomerCode] = @CustomerCode, [Address] = @Address,
                    [Address2] = @Address2, [City] = @City, [Pincode] = @Pincode, [Email] = @Email,
                    [VatNo] = @VatNo, [Check] = @Check, [BillCanceled] = @BillCanceled, [GrossAmt] = @GrossAmt,
                    [DiscountAllowed] = @DiscountAllowed, [NetAmt] = @NetAmt, [ABeforeTax] = @ABeforeTax,
                    [Tax] = @Tax, [TaxAmt] = @TaxAmt,
                    [Q1] = @Q1, [Q2] = @Q2, [Q3] = @Q3, [Remarks] = @Remarks, [Narration] = @Narration,
                    [EntryUser] = @EntryUser, [EntryDate] = @EntryDate, [EntryTime] = @EntryTime,
                    [ComputerName] = @ComputerName, [Status] = @Status
                WHERE [VouNo] = @WhereVouNo";

            await using (var cmd = new OleDbCommand(updateBillSql, connection, transaction))
            {
                cmd.Parameters.Add("@VouType", OleDbType.VarWChar, 50).Value = bill.VouType ?? string.Empty;
                cmd.Parameters.Add("@BillDate", OleDbType.Date).Value = bill.BillDate;
                cmd.Parameters.Add("@DueDate", OleDbType.Date).Value = bill.DueDate;
                cmd.Parameters.Add("@Phone", OleDbType.VarWChar, 50).Value = bill.Phone ?? string.Empty;
                cmd.Parameters.Add("@CustomerName", OleDbType.VarWChar, 50).Value = bill.CustomerName ?? string.Empty;
                cmd.Parameters.Add("@CustomerCode", OleDbType.VarWChar, 50).Value = bill.CustomerCode ?? string.Empty;
                cmd.Parameters.Add("@Address", OleDbType.VarWChar, 50).Value = bill.Address ?? string.Empty;
                cmd.Parameters.Add("@Address2", OleDbType.VarWChar, 50).Value = bill.Address2 ?? string.Empty;
                cmd.Parameters.Add("@City", OleDbType.VarWChar, 50).Value = bill.City ?? string.Empty;
                cmd.Parameters.Add("@Pincode", OleDbType.VarWChar, 15).Value = bill.Pincode ?? string.Empty;
                cmd.Parameters.Add("@Email", OleDbType.VarWChar, 100).Value = bill.Email ?? string.Empty;
                cmd.Parameters.Add("@VatNo", OleDbType.VarWChar, 50).Value = bill.VatNo ?? string.Empty;
                cmd.Parameters.Add("@Check", OleDbType.Boolean).Value = bill.Check;
                cmd.Parameters.Add("@BillCanceled", OleDbType.Boolean).Value = bill.BillCanceled;
                cmd.Parameters.Add("@GrossAmt", OleDbType.Currency).Value = bill.GrossAmt;
                cmd.Parameters.Add("@DiscountAllowed", OleDbType.Currency).Value = bill.DiscountAllowed;
                cmd.Parameters.Add("@NetAmt", OleDbType.Currency).Value = bill.NetAmt;

                decimal aBeforeTax = bill.ABeforeTax;
                decimal taxAmt = bill.TaxAmt;
                if (taxAmt == 0 && bill.NetAmt > 0)
                {
                    aBeforeTax = Math.Round(bill.NetAmt / 1.03m, 2);
                    taxAmt = bill.NetAmt - aBeforeTax;
                }
                else if (aBeforeTax == 0 && bill.NetAmt > taxAmt && taxAmt > 0)
                {
                    aBeforeTax = bill.NetAmt - taxAmt;
                }

                cmd.Parameters.Add("@ABeforeTax", OleDbType.Currency).Value = aBeforeTax;
                cmd.Parameters.Add("@Tax", OleDbType.Currency).Value = bill.Tax;
                cmd.Parameters.Add("@TaxAmt", OleDbType.Currency).Value = taxAmt;
                cmd.Parameters.Add("@Q1", OleDbType.Integer).Value = bill.Q1;
                cmd.Parameters.Add("@Q2", OleDbType.Integer).Value = bill.Q2;
                cmd.Parameters.Add("@Q3", OleDbType.Integer).Value = bill.Q3;
                cmd.Parameters.Add("@Remarks", OleDbType.VarWChar, 100).Value = bill.Remarks ?? string.Empty;
                cmd.Parameters.Add("@Narration", OleDbType.VarWChar, 150).Value = bill.Narration ?? string.Empty;
                cmd.Parameters.Add("@EntryUser", OleDbType.VarWChar, 50).Value = bill.EntryUser ?? string.Empty;
                cmd.Parameters.Add("@EntryDate", OleDbType.Date).Value = bill.EntryDate;
                cmd.Parameters.Add("@EntryTime", OleDbType.Date).Value = bill.EntryTime;
                cmd.Parameters.Add("@ComputerName", OleDbType.VarWChar, 50).Value = bill.ComputerName ?? string.Empty;
                cmd.Parameters.Add("@Status", OleDbType.VarWChar, 10).Value = bill.Status ?? string.Empty;
                cmd.Parameters.Add("@WhereVouNo", OleDbType.VarWChar, 25).Value = bill.VouNo;

                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // Delete existing subform records and re-insert
            const string delSub = "DELETE FROM BillSubform WHERE [VouNo] = @VouNo";
            await using (var cmd = new OleDbCommand(delSub, connection, transaction))
            {
                cmd.Parameters.Add("@VouNo", OleDbType.VarWChar, 25).Value = bill.VouNo;
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            const string delPay = "DELETE FROM PaymentSubform WHERE [VouNo] = @VouNo";
            await using (var cmd = new OleDbCommand(delPay, connection, transaction))
            {
                cmd.Parameters.Add("@VouNo", OleDbType.VarWChar, 50).Value = bill.VouNo;
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // Determine max AutoNo for BillSubform
            int updateAutoNo = 0;
            const string getMaxAutoNoSql = "SELECT MAX(AutoNo) FROM BillSubform";
            await using (var maxCmd = new OleDbCommand(getMaxAutoNoSql, connection, transaction))
            {
                var maxVal = await maxCmd.ExecuteScalarAsync(cancellationToken);
                if (maxVal != null && maxVal != DBNull.Value)
                {
                    updateAutoNo = Convert.ToInt32(maxVal);
                }
            }

            // Re-insert Items
            const string insertItemSql = @"
                INSERT INTO [BillSubform]
                    ([AutoNo], [VouNo], [Barcode], [OldBarcode], [PcRate], [ItemName], [ENote], [Qty], [NtWt], [Rate],
                     [Amount], [GrossAmt], [DisPer], [DisAmt], [GrWt], [MperGr], [Making],
                     [MetalValue], [SalesMan], [Vat%], [VatAmt], [Taxable], [StoneValue], [Purity], [isDiscount])
                VALUES
                    (@AutoNo, @VouNo, @Barcode, @OldBarcode, @PcRate, @ItemName, @ENote, @Qty, @NtWt, @Rate,
                     @Amount, @GrossAmt, @DisPer, @DisAmt, @GrWt, @MperGr, @Making,
                     @MetalValue, @SalesMan, @VatPercent, @VatAmt, @Taxable, @StoneValue, @Purity, @isDiscount)";

            foreach (var item in items)
            {
                await using var cmd = new OleDbCommand(insertItemSql, connection, transaction);
                cmd.Parameters.Add("@AutoNo", OleDbType.Integer).Value = item.AutoNo > 0 ? item.AutoNo : ++updateAutoNo;
                cmd.Parameters.Add("@VouNo", OleDbType.VarWChar, 25).Value = bill.VouNo;
                cmd.Parameters.Add("@Barcode", OleDbType.VarWChar, 10).Value = item.Barcode ?? string.Empty;
                cmd.Parameters.Add("@OldBarcode", OleDbType.VarWChar, 50).Value = item.OldBarcode ?? string.Empty;
                cmd.Parameters.Add("@PcRate", OleDbType.Boolean).Value = item.PcRate;
                cmd.Parameters.Add("@ItemName", OleDbType.VarWChar, 50).Value = item.ItemName ?? string.Empty;
                cmd.Parameters.Add("@ENote", OleDbType.VarWChar, 50).Value = item.ENote ?? string.Empty;
                cmd.Parameters.Add("@Qty", OleDbType.Integer).Value = item.Qty;
                cmd.Parameters.Add("@NtWt", OleDbType.Currency).Value = item.NtWt;
                cmd.Parameters.Add("@Rate", OleDbType.Currency).Value = item.Rate;
                cmd.Parameters.Add("@Amount", OleDbType.Currency).Value = item.Amount;
                cmd.Parameters.Add("@GrossAmt", OleDbType.Currency).Value = item.GrossAmt;
                cmd.Parameters.Add("@DisPer", OleDbType.Currency).Value = item.DisPer;
                cmd.Parameters.Add("@DisAmt", OleDbType.Currency).Value = item.DisAmt;
                cmd.Parameters.Add("@GrWt", OleDbType.Currency).Value = item.GrWt;
                cmd.Parameters.Add("@MperGr", OleDbType.Currency).Value = item.MperGr;
                cmd.Parameters.Add("@Making", OleDbType.Currency).Value = item.Making;
                cmd.Parameters.Add("@MetalValue", OleDbType.Currency).Value = item.MetalValue;
                cmd.Parameters.Add("@SalesMan", OleDbType.VarWChar, 50).Value = string.IsNullOrWhiteSpace(item.SalesMan) ? (object)DBNull.Value : item.SalesMan;

                decimal subTaxRate = item.VatPercent > 0 ? item.VatPercent : 3.00m;
                decimal subTaxable = item.Taxable > 0 ? item.Taxable : (item.Amount > 0 ? Math.Round(item.Amount / (1m + (subTaxRate / 100m)), 2) : 0m);
                decimal subVatAmt = item.VatAmt > 0 && (item.VatAmt > 28m || Math.Abs(item.VatAmt - (item.Amount - subTaxable)) <= 1.0m)
                    ? item.VatAmt
                    : (item.Amount > 0 ? Math.Round(item.Amount - subTaxable, 2) : 0m);

                cmd.Parameters.Add("@VatPercent", OleDbType.Currency).Value = subTaxRate;
                cmd.Parameters.Add("@VatAmt", OleDbType.Currency).Value = subVatAmt;
                cmd.Parameters.Add("@Taxable", OleDbType.Currency).Value = subTaxable;
                cmd.Parameters.Add("@StoneValue", OleDbType.Currency).Value = item.StoneValue;
                var resolvedPurity = await ResolvePurityAsync(connection, transaction, item.Purity, cancellationToken);
                cmd.Parameters.Add("@Purity", OleDbType.VarWChar, 50).Value = resolvedPurity;
                cmd.Parameters.Add("@isDiscount", OleDbType.Boolean).Value = false;

                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await InsertPaymentsAsync(connection, transaction, bill.VouNo, payments, cancellationToken);

            // Reconcile TempStock: restore removed items and remove newly sold items
            await AccessTempStockSchemaMigrator.EnsureSchemaAsync(_connectionString, cancellationToken);
            var newBarcodes = items.Where(i => !string.IsNullOrWhiteSpace(i.Barcode)).Select(i => i.Barcode!.Trim()).ToList();
            var restoredBarcodes = oldBarcodes.Except(newBarcodes, StringComparer.OrdinalIgnoreCase).ToList();
            var newlySoldBarcodes = newBarcodes.Except(oldBarcodes, StringComparer.OrdinalIgnoreCase).ToList();

            if (restoredBarcodes.Count > 0)
            {
                await TempStockManager.RestoreBarcodesAsync(connection, transaction, restoredBarcodes, cancellationToken);
            }
            if (newlySoldBarcodes.Count > 0)
            {
                await TempStockManager.RemoveSoldAsync(connection, transaction, newlySoldBarcodes, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task DeleteAsync(string vouNo, CancellationToken cancellationToken = default)
    {
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var transaction = connection.BeginTransaction();

        try
        {
            // Collect barcodes to restore to TempStock
            var deletedBarcodes = new List<string>();
            const string getOldBcSql = "SELECT [Barcode] FROM BillSubform WHERE [VouNo] = @VouNo AND [Barcode] IS NOT NULL AND [Barcode] <> ''";
            await using (var getCmd = new OleDbCommand(getOldBcSql, connection, transaction))
            {
                getCmd.Parameters.Add("@VouNo", OleDbType.VarWChar, 25).Value = vouNo;
                await using var r = await getCmd.ExecuteReaderAsync(cancellationToken);
                while (await r.ReadAsync(cancellationToken))
                {
                    var bc = r[0]?.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(bc)) deletedBarcodes.Add(bc);
                }
            }

            const string sqlBill = "DELETE FROM Bill WHERE [VouNo] = @VouNo";
            await using (var cmd = new OleDbCommand(sqlBill, connection, transaction))
            {
                cmd.Parameters.Add("@VouNo", OleDbType.VarWChar, 25).Value = vouNo;
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            const string sqlSub = "DELETE FROM BillSubform WHERE [VouNo] = @VouNo";
            await using (var cmd = new OleDbCommand(sqlSub, connection, transaction))
            {
                cmd.Parameters.Add("@VouNo", OleDbType.VarWChar, 25).Value = vouNo;
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            const string sqlPay = "DELETE FROM PaymentSubform WHERE [VouNo] = @VouNo";
            await using (var cmd = new OleDbCommand(sqlPay, connection, transaction))
            {
                cmd.Parameters.Add("@VouNo", OleDbType.VarWChar, 50).Value = vouNo;
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            if (deletedBarcodes.Count > 0)
            {
                await AccessTempStockSchemaMigrator.EnsureSchemaAsync(_connectionString, cancellationToken);
                await TempStockManager.RestoreBarcodesAsync(connection, transaction, deletedBarcodes, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static void AddBillParameters(OleDbCommand cmd, Bill bill)
    {
        cmd.Parameters.Add("@VouNo", OleDbType.VarWChar, 25).Value = bill.VouNo ?? string.Empty;
        cmd.Parameters.Add("@VouType", OleDbType.VarWChar, 50).Value = bill.VouType ?? string.Empty;
        cmd.Parameters.Add("@BillDate", OleDbType.Date).Value = bill.BillDate;
        cmd.Parameters.Add("@DueDate", OleDbType.Date).Value = bill.DueDate;
        cmd.Parameters.Add("@Phone", OleDbType.VarWChar, 50).Value = bill.Phone ?? string.Empty;
        cmd.Parameters.Add("@CustomerName", OleDbType.VarWChar, 50).Value = bill.CustomerName ?? string.Empty;
        cmd.Parameters.Add("@CustomerCode", OleDbType.VarWChar, 50).Value = bill.CustomerCode ?? string.Empty;
        cmd.Parameters.Add("@Address", OleDbType.VarWChar, 50).Value = bill.Address ?? string.Empty;
        cmd.Parameters.Add("@Address2", OleDbType.VarWChar, 50).Value = bill.Address2 ?? string.Empty;
        cmd.Parameters.Add("@City", OleDbType.VarWChar, 50).Value = bill.City ?? string.Empty;
        cmd.Parameters.Add("@Pincode", OleDbType.VarWChar, 15).Value = bill.Pincode ?? string.Empty;
        cmd.Parameters.Add("@Email", OleDbType.VarWChar, 100).Value = bill.Email ?? string.Empty;
        cmd.Parameters.Add("@VatNo", OleDbType.VarWChar, 50).Value = bill.VatNo ?? string.Empty;
        cmd.Parameters.Add("@Check", OleDbType.Boolean).Value = bill.Check;
        cmd.Parameters.Add("@BillCanceled", OleDbType.Boolean).Value = bill.BillCanceled;
        cmd.Parameters.Add("@GrossAmt", OleDbType.Currency).Value = bill.GrossAmt;
        cmd.Parameters.Add("@DiscountAllowed", OleDbType.Currency).Value = bill.DiscountAllowed;
        cmd.Parameters.Add("@NetAmt", OleDbType.Currency).Value = bill.NetAmt;

        decimal aBeforeTax = bill.ABeforeTax;
        decimal taxAmt = bill.TaxAmt;
        if (taxAmt == 0 && bill.NetAmt > 0)
        {
            aBeforeTax = Math.Round(bill.NetAmt / 1.03m, 2);
            taxAmt = bill.NetAmt - aBeforeTax;
        }
        else if (aBeforeTax == 0 && bill.NetAmt > taxAmt && taxAmt > 0)
        {
            aBeforeTax = bill.NetAmt - taxAmt;
        }

        cmd.Parameters.Add("@ABeforeTax", OleDbType.Currency).Value = aBeforeTax;
        cmd.Parameters.Add("@Tax", OleDbType.Currency).Value = bill.Tax;
        cmd.Parameters.Add("@TaxAmt", OleDbType.Currency).Value = taxAmt;
        cmd.Parameters.Add("@Q1", OleDbType.Integer).Value = bill.Q1;
        cmd.Parameters.Add("@Q2", OleDbType.Integer).Value = bill.Q2;
        cmd.Parameters.Add("@Q3", OleDbType.Integer).Value = bill.Q3;
        cmd.Parameters.Add("@Remarks", OleDbType.VarWChar, 100).Value = bill.Remarks ?? string.Empty;
        cmd.Parameters.Add("@Narration", OleDbType.VarWChar, 150).Value = bill.Narration ?? string.Empty;
        cmd.Parameters.Add("@EntryUser", OleDbType.VarWChar, 50).Value = bill.EntryUser ?? string.Empty;
        cmd.Parameters.Add("@EntryDate", OleDbType.Date).Value = bill.EntryDate;
        cmd.Parameters.Add("@EntryTime", OleDbType.Date).Value = bill.EntryTime;
        cmd.Parameters.Add("@ComputerName", OleDbType.VarWChar, 50).Value = bill.ComputerName ?? string.Empty;
        cmd.Parameters.Add("@Status", OleDbType.VarWChar, 10).Value = bill.Status ?? string.Empty;
    }

    private static Bill MapBill(DbDataReader reader)
    {
        return new Bill
        {
            VouNo = GetString(reader, "VouNo"),
            VouType = GetString(reader, "VouType"),
            BillDate = GetDateTime(reader, "BillDate"),
            DueDate = GetDateTime(reader, "DueDate"),
            Phone = GetString(reader, "Phone"),
            CustomerName = GetString(reader, "CustomerName"),
            CustomerCode = GetString(reader, "CustomerCode"),
            Address = GetString(reader, "Address"),
            Address2 = GetString(reader, "Address2"),
            City = GetString(reader, "City"),
            Pincode = GetString(reader, "Pincode"),
            Email = GetString(reader, "Email"),
            VatNo = GetString(reader, "VatNo"),
            Check = GetBoolean(reader, "Check"),
            BillCanceled = GetBoolean(reader, "BillCanceled"),
            GrossAmt = GetDecimal(reader, "GrossAmt"),
            DiscountAllowed = GetDecimal(reader, "DiscountAllowed"),
            NetAmt = GetDecimal(reader, "NetAmt"),
            ABeforeTax = HasColumn(reader, "ABeforeTax") ? GetDecimal(reader, "ABeforeTax") : 0m,
            Tax = GetDecimal(reader, "Tax"),
            TaxAmt = GetDecimal(reader, "TaxAmt"),
            Q1 = GetInt(reader, "Q1"),
            Q2 = GetInt(reader, "Q2"),
            Q3 = GetInt(reader, "Q3"),
            Remarks = GetString(reader, "Remarks"),
            Narration = GetString(reader, "Narration"),
            EntryUser = GetString(reader, "EntryUser"),
            EntryDate = GetDateTime(reader, "EntryDate"),
            EntryTime = GetDateTime(reader, "EntryTime"),
            ComputerName = GetString(reader, "ComputerName"),
            Status = GetString(reader, "Status")
        };
    }

    private static BillSubform MapBillSubform(DbDataReader reader)
    {
        return new BillSubform
        {
            AutoNo = HasColumn(reader, "AutoNo") ? GetInt(reader, "AutoNo") : 0,
            VouNo = GetString(reader, "VouNo"),
            Barcode = GetString(reader, "Barcode"),
            OldBarcode = HasColumn(reader, "OldBarcode") ? GetString(reader, "OldBarcode") : string.Empty,
            PcRate = GetBoolean(reader, "PcRate"),
            ItemName = GetString(reader, "ItemName"),
            ENote = GetString(reader, "ENote"),
            Purity = HasColumn(reader, "Purity") ? GetString(reader, "Purity") : string.Empty,
            Qty = GetInt(reader, "Qty"),
            NtWt = GetDecimal(reader, "NtWt"),
            Rate = GetDecimal(reader, "Rate"),
            Amount = GetDecimal(reader, "Amount"),
            GrossAmt = GetDecimal(reader, "GrossAmt"),
            DisPer = GetDecimal(reader, "DisPer"),
            DisAmt = GetDecimal(reader, "DisAmt"),
            GrWt = GetDecimal(reader, "GrWt"),
            MperGr = GetDecimal(reader, "MperGr"),
            Making = GetDecimal(reader, "Making"),
            MetalValue = GetDecimal(reader, "MetalValue"),
            SalesMan = GetString(reader, "SalesMan"),
            VatPercent = HasColumn(reader, "Vat%") ? GetDecimal(reader, "Vat%") : (HasColumn(reader, "VatPer") ? GetDecimal(reader, "VatPer") : 0m),
            Taxable = HasColumn(reader, "Taxable") ? GetDecimal(reader, "Taxable") : 0m,
            VatAmt = GetDecimal(reader, "VatAmt"),
            StoneValue = GetDecimal(reader, "StoneValue")
        };
    }

    private static PaymentSubform MapPaymentSubform(DbDataReader reader)
    {
        var itemVal = HasColumn(reader, "Item") ? GetString(reader, "Item") : (HasColumn(reader, "ItemType") ? GetString(reader, "ItemType") : string.Empty);
        return new PaymentSubform
        {
            VouNo = GetString(reader, "VouNo"),
            Number = GetString(reader, "Number"),
            AccountName = GetString(reader, "AccountName"),
            Amount = GetDecimal(reader, "Amount"),
            Narration = GetString(reader, "Narration"),
            ItemType = itemVal,
            ItemName = HasColumn(reader, "ItemName") && !string.IsNullOrEmpty(GetString(reader, "ItemName")) ? GetString(reader, "ItemName") : "OLD",
            Wt = HasColumn(reader, "Wt") ? GetDecimal(reader, "Wt") : 0m,
            LessWt = HasColumn(reader, "LessWt") ? GetDecimal(reader, "LessWt") : 0m,
            GrWt = HasColumn(reader, "GrWt") ? GetDecimal(reader, "GrWt") : 0m,
            Alloy = HasColumn(reader, "Alloy") && reader["Alloy"] != DBNull.Value ? GetDecimal(reader, "Alloy") : 100m,
            NtWt = HasColumn(reader, "NtWt") ? GetDecimal(reader, "NtWt") : 0m,
            Rate = HasColumn(reader, "Rate") ? GetDecimal(reader, "Rate") : 0m,
            HeadCategory = HasColumn(reader, "HeadCategory") ? GetString(reader, "HeadCategory") : string.Empty
        };
    }

    private static HashSet<string>? _paymentSubformColumns;
    private static readonly object _colLock = new();

    private async Task<HashSet<string>> GetPaymentSubformColumnsAsync(OleDbConnection connection, OleDbTransaction? transaction, CancellationToken cancellationToken)
    {
        if (_paymentSubformColumns != null)
            return _paymentSubformColumns;

        var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            const string sql = "SELECT TOP 1 * FROM [PaymentSubform]";
            await using var cmd = new OleDbCommand(sql, connection, transaction);
            await using var reader = await cmd.ExecuteReaderAsync(System.Data.CommandBehavior.SchemaOnly, cancellationToken);
            var table = reader.GetSchemaTable();
            if (table != null)
            {
                foreach (System.Data.DataRow row in table.Rows)
                {
                    var colName = row["ColumnName"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(colName))
                    {
                        cols.Add(colName);
                    }
                }
            }
        }
        catch { }

        if (cols.Count > 0)
        {
            lock (_colLock)
            {
                _paymentSubformColumns = cols;
            }
        }

        return cols;
    }

    private static async Task<string> ResolvePurityAsync(OleDbConnection connection, OleDbTransaction transaction, string? inputPurity, CancellationToken cancellationToken)
    {
        var clean = inputPurity?.Trim();
        if (string.IsNullOrEmpty(clean))
        {
            clean = "916HM";
        }

        const string sql = "SELECT [Purity] FROM [Purity]";
        var availablePurities = new List<string>();
        try
        {
            await using var cmd = new OleDbCommand(sql, connection, transaction);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var p = reader[0]?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(p)) availablePurities.Add(p);
            }
        }
        catch
        {
            return clean;
        }

        if (availablePurities.Count == 0)
        {
            return clean;
        }

        var exact = availablePurities.FirstOrDefault(p => string.Equals(p, clean, StringComparison.OrdinalIgnoreCase));
        if (exact != null) return exact;

        var partial = availablePurities.FirstOrDefault(p => p.StartsWith(clean, StringComparison.OrdinalIgnoreCase) || clean.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                   ?? availablePurities.FirstOrDefault(p => p.Contains(clean, StringComparison.OrdinalIgnoreCase) || clean.Contains(p, StringComparison.OrdinalIgnoreCase));
        if (partial != null) return partial;

        return availablePurities[0];
    }

    private async Task InsertPaymentsAsync(OleDbConnection connection, OleDbTransaction transaction, string vouNo, IEnumerable<PaymentSubform> payments, CancellationToken cancellationToken)
    {
        var existingCols = await GetPaymentSubformColumnsAsync(connection, transaction, cancellationToken);

        var payBaseTime = DateTime.Now;
        int payIndex = 0;

        foreach (var pay in payments)
        {
            var payNumber = !string.IsNullOrWhiteSpace(pay.Number)
                ? pay.Number
                : payBaseTime.AddSeconds(payIndex++).ToString("dd/MM/yyyy HH:mm:ss");

            var colNames = new List<string> { "[VouNo]", "[Number]", "[AccountName]", "[Amount]" };
            var valParams = new List<string> { "@VouNo", "@Number", "@AccountName", "@Amount" };

            var cmd = new OleDbCommand();
            cmd.Connection = connection;
            cmd.Transaction = transaction;

            cmd.Parameters.Add("@VouNo", OleDbType.VarWChar, 50).Value = vouNo;
            cmd.Parameters.Add("@Number", OleDbType.VarWChar, 50).Value = payNumber;
            cmd.Parameters.Add("@AccountName", OleDbType.VarWChar, 100).Value = pay.AccountName ?? string.Empty;
            cmd.Parameters.Add("@Amount", OleDbType.Currency).Value = pay.Amount;

            if (existingCols.Contains("Narration"))
            {
                colNames.Add("[Narration]");
                valParams.Add("@Narration");
                cmd.Parameters.Add("@Narration", OleDbType.VarWChar, 255).Value = pay.Narration ?? string.Empty;
            }

            if (existingCols.Contains("Item"))
            {
                colNames.Add("[Item]");
                valParams.Add("@Item");
                cmd.Parameters.Add("@Item", OleDbType.VarWChar, 50).Value = pay.ItemType ?? string.Empty;
            }
            else if (existingCols.Contains("ItemType"))
            {
                colNames.Add("[ItemType]");
                valParams.Add("@ItemType");
                cmd.Parameters.Add("@ItemType", OleDbType.VarWChar, 50).Value = pay.ItemType ?? string.Empty;
            }

            if (existingCols.Contains("ItemName"))
            {
                colNames.Add("[ItemName]");
                valParams.Add("@ItemName");
                cmd.Parameters.Add("@ItemName", OleDbType.VarWChar, 50).Value = !string.IsNullOrWhiteSpace(pay.ItemName) ? pay.ItemName : "OLD";
            }

            if (existingCols.Contains("Wt"))
            {
                colNames.Add("[Wt]");
                valParams.Add("@Wt");
                cmd.Parameters.Add("@Wt", OleDbType.Currency).Value = pay.Wt;
            }

            if (existingCols.Contains("LessWt"))
            {
                colNames.Add("[LessWt]");
                valParams.Add("@LessWt");
                cmd.Parameters.Add("@LessWt", OleDbType.Currency).Value = pay.LessWt;
            }

            if (existingCols.Contains("GrWt"))
            {
                colNames.Add("[GrWt]");
                valParams.Add("@GrWt");
                cmd.Parameters.Add("@GrWt", OleDbType.Currency).Value = pay.GrWt;
            }

            if (existingCols.Contains("Alloy"))
            {
                colNames.Add("[Alloy]");
                valParams.Add("@Alloy");
                cmd.Parameters.Add("@Alloy", OleDbType.Currency).Value = pay.Alloy;
            }

            if (existingCols.Contains("NtWt"))
            {
                colNames.Add("[NtWt]");
                valParams.Add("@NtWt");
                cmd.Parameters.Add("@NtWt", OleDbType.Currency).Value = pay.NtWt;
            }

            if (existingCols.Contains("Rate"))
            {
                colNames.Add("[Rate]");
                valParams.Add("@Rate");
                cmd.Parameters.Add("@Rate", OleDbType.Currency).Value = pay.Rate;
            }

            if (existingCols.Contains("HeadCategory") && !string.IsNullOrWhiteSpace(pay.HeadCategory))
            {
                colNames.Add("[HeadCategory]");
                valParams.Add("@HeadCategory");
                cmd.Parameters.Add("@HeadCategory", OleDbType.VarWChar, 50).Value = pay.HeadCategory;
            }

            cmd.CommandText = $"INSERT INTO [PaymentSubform] ({string.Join(", ", colNames)}) VALUES ({string.Join(", ", valParams)})";
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<SalesVouType>> GetSalesVouTypesAsync(string? counter = null, string? headCategory = "Sales", CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM SALESVOUTYPE WHERE (Hide = False OR Hide IS NULL) ORDER BY VouType";
        var result = new List<SalesVouType>();

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var item = MapSalesVouType(reader);
            if (!string.IsNullOrWhiteSpace(headCategory) && !string.Equals(item.HeadCategory, headCategory.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(counter) || IsCounterMatched(item.CounterNo, counter))
            {
                result.Add(item);
            }
        }

        return result;
    }

    public async Task<string> GetNextVouNoAsync(string vouType, string? shortName = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(shortName))
        {
            var vouTypes = await GetSalesVouTypesAsync(null, null, cancellationToken);
            var matching = vouTypes.FirstOrDefault(v => string.Equals(v.VouType, vouType, StringComparison.OrdinalIgnoreCase));
            shortName = matching?.ShortName ?? vouType;
        }

        const string sql = "SELECT VouNo FROM Bill WHERE VouType = @VouType";
        var maxNumber = 0;
        var digitsCount = 5;

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        command.Parameters.Add("@VouType", OleDbType.VarWChar, 50).Value = vouType;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var regex = new System.Text.RegularExpressions.Regex(@"\d+");

        while (await reader.ReadAsync(cancellationToken))
        {
            var vouNo = reader["VouNo"]?.ToString();
            if (!string.IsNullOrWhiteSpace(vouNo))
            {
                var match = regex.Match(vouNo);
                if (match.Success && int.TryParse(match.Value, out var num))
                {
                    if (num > maxNumber)
                    {
                        maxNumber = num;
                        if (match.Value.Length > digitsCount)
                        {
                            digitsCount = match.Value.Length;
                        }
                    }
                }
            }
        }

        var nextNum = maxNumber + 1;
        var padding = new string('0', digitsCount);
        return $"{shortName.Trim()}{nextNum.ToString(padding)}";
    }

    public async Task<BarcodeDetails?> GetBarcodeDetailsAsync(string barcode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(barcode))
        {
            return null;
        }

        var clean = barcode.Trim();

        try
        {
            await AccessTempStockSchemaMigrator.EnsureSchemaAsync(_connectionString, cancellationToken);
            await using var connection = new OleDbConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            // 1. Fast path: check live TempStock table
            try
            {
                var tempStockDetails = await TempStockManager.GetBarcodeDetailsAsync(connection, clean, cancellationToken);
                if (tempStockDetails is not null)
                {
                    return tempStockDetails;
                }
            }
            catch { }

            // 2. Fallback: check master BarcodeSubform table
            bool isNumeric = int.TryParse(clean, out var nos) && nos > 0;
            var sql = isNumeric
                ? "SELECT TOP 1 * FROM [BarcodeSubform] WHERE [Barcode] = ? OR [Barcode] = ? OR [Nos] = ?"
                : "SELECT TOP 1 * FROM [BarcodeSubform] WHERE [Barcode] = ? OR [Barcode] = ?";

            await using var command = new OleDbCommand(sql, connection);
            command.Parameters.Add("?", OleDbType.VarWChar, 50).Value = clean;
            command.Parameters.Add("?", OleDbType.VarWChar, 50).Value = clean.PadLeft(6, '0');
            if (isNumeric)
            {
                command.Parameters.Add("?", OleDbType.Integer).Value = nos;
            }

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            var desc2 = GetString(reader, "Description2");
            if (string.IsNullOrEmpty(desc2)) desc2 = GetString(reader, "OldBarcode");
            if (string.IsNullOrEmpty(desc2)) desc2 = GetString(reader, "HUID");

            return new BarcodeDetails
            {
                Barcode = GetString(reader, "Barcode"),
                BType = GetString(reader, "BType"),
                ItemName = GetString(reader, "ItemName"),
                Purity = GetString(reader, "Purity"),
                Qty = GetInt(reader, "Qty", 1),
                GrossWt = GetDecimal(reader, "GrossWt"),
                NetWt = GetDecimal(reader, "NetWt"),
                StoneWt = GetDecimal(reader, "StoneWt"),
                StoneValue = GetDecimal(reader, "StoneValue"),
                Making = GetDecimal(reader, "Making"),
                MPerGr = GetDecimal(reader, "MPerGr"),
                Description = GetString(reader, "Description"),
                Description2 = desc2,
                MRP = GetDecimal(reader, "MRP"),
                ItemCode = GetString(reader, "ItemCode"),
                CostRate = GetDecimal(reader, "Costrate")
            };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error reading barcode '{clean}' from database: {ex.Message}", ex);
        }
    }

    public async Task<IReadOnlyList<string>> GetAvailableStockBarcodesAsync(string? targetItemHead = null, CancellationToken cancellationToken = default)
    {
        await AccessTempStockSchemaMigrator.EnsureSchemaAsync(_connectionString, cancellationToken);
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return await TempStockManager.GetAvailableBarcodesAsync(connection, targetItemHead, cancellationToken);
    }

    public async Task<IReadOnlyList<TempStockItem>> GetTempStockItemsAsync(string? targetItemHead = null, CancellationToken cancellationToken = default)
    {
        await AccessTempStockSchemaMigrator.EnsureSchemaAsync(_connectionString, cancellationToken);
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return await TempStockManager.GetTempStockItemsAsync(connection, targetItemHead, cancellationToken);
    }

    public async Task<IReadOnlyList<InvoiceListItem>> GetInvoiceListAsync(DateTime? fromDate = null, DateTime? toDate = null, string? vouType = null, CancellationToken cancellationToken = default)
    {
        var result = new List<InvoiceListItem>();
        var whereClauses = new List<string>();
        // Filter only Voucher Types that belong to Category 'Sales' and are NOT Hidden
        whereClauses.Add("b.VouType IN (SELECT VouType FROM SALESVOUTYPE WHERE (Hide = False OR Hide IS NULL) AND HeadCategory = 'Sales')");

        if (fromDate.HasValue)
        {
            whereClauses.Add("b.BillDate >= @FromDate");
        }
        if (toDate.HasValue)
        {
            whereClauses.Add("b.BillDate <= @ToDate");
        }
        if (!string.IsNullOrWhiteSpace(vouType) && !string.Equals(vouType.Trim(), "ALL", StringComparison.OrdinalIgnoreCase))
        {
            whereClauses.Add("b.VouType = @VouType");
        }

        var whereSql = "WHERE " + string.Join(" AND ", whereClauses);

        var sql = $@"
            SELECT b.VouNo, b.BillDate, b.VouType, b.CustomerName, b.Phone, b.City, b.Address,
                   b.NetAmt, b.GrossAmt, b.DiscountAllowed, b.Tax, b.TaxAmt, b.ABeforeTax, b.BillCanceled, b.Status,
                   s.TotalQty, s.TotalNtWt, s.TotalGrWt, s.TotalAmount, s.TotalTaxable, s.TotalVatAmt
            FROM Bill b
            LEFT JOIN (
                SELECT VouNo,
                       SUM(Qty) AS TotalQty,
                       SUM(NtWt) AS TotalNtWt,
                       SUM(GrWt) AS TotalGrWt,
                       SUM(Amount) AS TotalAmount,
                       SUM(Taxable) AS TotalTaxable,
                       SUM(VatAmt) AS TotalVatAmt
                FROM BillSubform
                GROUP BY VouNo
            ) s ON b.VouNo = s.VouNo
            {whereSql}
            ORDER BY b.BillDate DESC, b.VouNo DESC";

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);

        if (fromDate.HasValue)
        {
            command.Parameters.Add("@FromDate", OleDbType.Date).Value = fromDate.Value.Date;
        }
        if (toDate.HasValue)
        {
            command.Parameters.Add("@ToDate", OleDbType.Date).Value = toDate.Value.Date;
        }
        if (!string.IsNullOrWhiteSpace(vouType) && !string.Equals(vouType.Trim(), "ALL", StringComparison.OrdinalIgnoreCase))
        {
            command.Parameters.Add("@VouType", OleDbType.VarWChar, 50).Value = vouType.Trim();
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var netAmt = GetDecimal(reader, "NetAmt");
            var rawTaxAmt = GetDecimal(reader, "TaxAmt");
            var rawTaxRate = GetDecimal(reader, "Tax");
            var aBeforeTax = HasColumn(reader, "ABeforeTax") ? GetDecimal(reader, "ABeforeTax") : 0m;
            var subTaxable = HasColumn(reader, "TotalTaxable") ? GetDecimal(reader, "TotalTaxable") : 0m;
            var subVatAmt = HasColumn(reader, "TotalVatAmt") ? GetDecimal(reader, "TotalVatAmt") : 0m;

            if (rawTaxAmt == 0 && subVatAmt > 0)
            {
                rawTaxAmt = subVatAmt;
            }

            decimal taxableAmt = aBeforeTax > 0 ? aBeforeTax : (subTaxable > 0 ? subTaxable : 0m);
            decimal taxAmt = rawTaxAmt;

            if (taxableAmt <= 0 && netAmt > 0)
            {
                decimal rate = rawTaxRate > 0 ? rawTaxRate : 3.00m;
                taxableAmt = Math.Round(netAmt / (1m + (rate / 100m)), 2);
                taxAmt = netAmt - taxableAmt;
            }
            else if (taxAmt <= 0 && taxableAmt > 0 && netAmt > taxableAmt)
            {
                taxAmt = netAmt - taxableAmt;
            }

            var item = new InvoiceListItem
            {
                VouNo = GetString(reader, "VouNo"),
                BillDate = GetDateTime(reader, "BillDate"),
                VouType = GetString(reader, "VouType"),
                CustomerName = GetString(reader, "CustomerName"),
                Phone = GetString(reader, "Phone"),
                City = GetString(reader, "City"),
                Address = GetString(reader, "Address"),
                NetAmt = netAmt,
                GrossAmt = GetDecimal(reader, "GrossAmt"),
                DiscountAllowed = GetDecimal(reader, "DiscountAllowed"),
                Tax = GetDecimal(reader, "Tax"),
                TaxAmt = taxAmt,
                TaxableAmt = taxableAmt,
                Qty = GetInt(reader, "TotalQty"),
                NetWt = GetDecimal(reader, "TotalNtWt"),
                GrWt = GetDecimal(reader, "TotalGrWt"),
                BillCanceled = GetBoolean(reader, "BillCanceled"),
                Status = GetString(reader, "Status")
            };

            result.Add(item);
        }

        // Bulk fetch BillSubform items for the fetched invoices
        if (result.Count > 0)
        {
            try
            {
                var subSql = $@"
                    SELECT s.AutoNo, s.VouNo, s.Barcode, s.ItemName, s.ENote, s.Qty, s.NtWt, s.Rate,
                           s.Amount, s.GrossAmt, s.DisPer, s.DisAmt, s.GrWt, s.MperGr, s.Making,
                           s.MetalValue, s.SalesMan, s.[Vat%] AS VatPer, s.VatAmt, s.Taxable, s.StoneValue, s.Purity
                    FROM BillSubform s
                    INNER JOIN Bill b ON s.VouNo = b.VouNo
                    {whereSql}
                    ORDER BY s.VouNo, s.AutoNo";

                await using var subCommand = new OleDbCommand(subSql, connection);
                if (fromDate.HasValue)
                {
                    subCommand.Parameters.Add("@FromDate", OleDbType.Date).Value = fromDate.Value.Date;
                }
                if (toDate.HasValue)
                {
                    subCommand.Parameters.Add("@ToDate", OleDbType.Date).Value = toDate.Value.Date;
                }
                if (!string.IsNullOrWhiteSpace(vouType) && !string.Equals(vouType.Trim(), "ALL", StringComparison.OrdinalIgnoreCase))
                {
                    subCommand.Parameters.Add("@VouType", OleDbType.VarWChar, 50).Value = vouType.Trim();
                }

                var itemLookup = result.ToDictionary(r => r.VouNo, r => r, StringComparer.OrdinalIgnoreCase);

                await using var subReader = await subCommand.ExecuteReaderAsync(cancellationToken);
                while (await subReader.ReadAsync(cancellationToken))
                {
                    var vNo = GetString(subReader, "VouNo");
                    if (itemLookup.TryGetValue(vNo, out var parentInvoice))
                    {
                        parentInvoice.Items.Add(new BillSubform
                        {
                            AutoNo = GetInt(subReader, "AutoNo"),
                            VouNo = vNo,
                            Barcode = GetString(subReader, "Barcode"),
                            ItemName = GetString(subReader, "ItemName"),
                            ENote = GetString(subReader, "ENote"),
                            Qty = GetInt(subReader, "Qty"),
                            NtWt = GetDecimal(subReader, "NtWt"),
                            GrWt = GetDecimal(subReader, "GrWt"),
                            Rate = GetDecimal(subReader, "Rate"),
                            Amount = GetDecimal(subReader, "Amount"),
                            GrossAmt = GetDecimal(subReader, "GrossAmt"),
                            DisPer = GetDecimal(subReader, "DisPer"),
                            DisAmt = GetDecimal(subReader, "DisAmt"),
                            MperGr = GetDecimal(subReader, "MperGr"),
                            Making = GetDecimal(subReader, "Making"),
                            MetalValue = GetDecimal(subReader, "MetalValue"),
                            SalesMan = GetString(subReader, "SalesMan"),
                            VatPercent = HasColumn(subReader, "VatPer") ? GetDecimal(subReader, "VatPer") : (HasColumn(subReader, "Vat%") ? GetDecimal(subReader, "Vat%") : 0m),
                            Taxable = GetDecimal(subReader, "Taxable"),
                            VatAmt = GetDecimal(subReader, "VatAmt"),
                            StoneValue = GetDecimal(subReader, "StoneValue"),
                            Purity = GetString(subReader, "Purity")
                        });
                    }
                }
            }
            catch { }
        }

        return result;
    }

    public async Task<IReadOnlyList<PaymentSubform>> GetSalesPaymentsAsync(DateTime? fromDate = null, DateTime? toDate = null, string? vouType = null, CancellationToken cancellationToken = default)
    {
        var result = new List<PaymentSubform>();
        var whereClauses = new List<string>();
        whereClauses.Add("b.VouType IN (SELECT VouType FROM SALESVOUTYPE WHERE (Hide = False OR Hide IS NULL) AND HeadCategory = 'Sales')");

        if (fromDate.HasValue)
        {
            whereClauses.Add("b.BillDate >= @FromDate");
        }
        if (toDate.HasValue)
        {
            whereClauses.Add("b.BillDate <= @ToDate");
        }
        if (!string.IsNullOrWhiteSpace(vouType) && !string.Equals(vouType.Trim(), "ALL", StringComparison.OrdinalIgnoreCase))
        {
            whereClauses.Add("b.VouType = @VouType");
        }

        var whereSql = "WHERE " + string.Join(" AND ", whereClauses);

        var sql = $@"
            SELECT p.VouNo, p.Number, p.AccountName, p.Amount, p.HeadCategory, p.Narration,
                   p.Item, p.ItemName, p.Wt, p.LessWt, p.GrWt, p.Alloy, p.NtWt, p.Rate
            FROM [PaymentSubform] p
            INNER JOIN [Bill] b ON p.VouNo = b.VouNo
            {whereSql}
            ORDER BY b.BillDate DESC, p.VouNo DESC";

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);

        if (fromDate.HasValue)
        {
            command.Parameters.Add("@FromDate", OleDbType.Date).Value = fromDate.Value.Date;
        }
        if (toDate.HasValue)
        {
            command.Parameters.Add("@ToDate", OleDbType.Date).Value = toDate.Value.Date;
        }
        if (!string.IsNullOrWhiteSpace(vouType) && !string.Equals(vouType.Trim(), "ALL", StringComparison.OrdinalIgnoreCase))
        {
            command.Parameters.Add("@VouType", OleDbType.VarWChar, 50).Value = vouType.Trim();
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(MapPaymentSubform(reader));
        }

        return result;
    }

    private static bool IsCounterMatched(string? counterNoField, string targetCounter)
    {
        if (string.IsNullOrWhiteSpace(counterNoField)) return true;
        var target = targetCounter.Trim();
        if (string.IsNullOrEmpty(target)) return true;

        var counters = counterNoField.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return counters.Any(c => string.Equals(c, target, StringComparison.OrdinalIgnoreCase));
    }

    private static SalesVouType MapSalesVouType(DbDataReader reader)
    {
        return new SalesVouType
        {
            VouType = GetString(reader, "VouType"),
            ShortName = GetString(reader, "ShortName"),
            SalesAccount = GetString(reader, "SalesAccount"),
            VatAccount = GetString(reader, "VatAccount"),
            CardCommission = GetDecimal(reader, "CardCommision"),
            CardBank = GetString(reader, "CardBank"),
            CommAc = GetString(reader, "CommAc"),
            VatPercentage = GetDecimal(reader, "Vat%"),
            Note = GetString(reader, "Note"),
            Status = GetString(reader, "Status"),
            HeadCategory = GetString(reader, "HeadCategory"),
            Hide = GetBoolean(reader, "Hide"),
            ItemHead = GetString(reader, "ItemHead"),
            PrintFile = GetString(reader, "PrintFile"),
            ItemPurchased = GetString(reader, "ItemPurchased"),
            ItemConvertedTo = GetString(reader, "ItemConvertedTo"),
            CounterNo = GetString(reader, "CounterNo")
        };
    }

    private static bool HasColumn(DbDataReader reader, string columnName)
    {
        for (int i = 0; i < reader.FieldCount; i++)
        {
            if (string.Equals(reader.GetName(i), columnName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static string GetString(DbDataReader reader, string column) =>
        HasColumn(reader, column) && reader[column] != DBNull.Value ? Convert.ToString(reader[column]) ?? string.Empty : string.Empty;

    private static bool GetBoolean(DbDataReader reader, string column) =>
        HasColumn(reader, column) && reader[column] != DBNull.Value && Convert.ToBoolean(reader[column]);

    private static decimal GetDecimal(DbDataReader reader, string column) =>
        HasColumn(reader, column) && reader[column] != DBNull.Value ? Convert.ToDecimal(reader[column]) : 0m;

    private static int GetInt(DbDataReader reader, string column, int defaultValue = 0)
    {
        if (HasColumn(reader, column) && reader[column] != DBNull.Value)
        {
            try { return Convert.ToInt32(Convert.ToDouble(reader[column])); } catch { }
        }
        return defaultValue;
    }

    public async Task<CustomerPurchaseSummary> GetCustomerPurchaseSummaryAsync(
        string? phone,
        string? customerName,
        string? customerCode,
        CancellationToken cancellationToken = default)
    {
        var summary = new CustomerPurchaseSummary();
        var cleanPhone = !string.IsNullOrWhiteSpace(phone) ? new string(phone.Where(char.IsDigit).ToArray()) : string.Empty;
        var cleanName = customerName?.Trim() ?? string.Empty;
        var cleanCode = customerCode?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(cleanPhone) && string.IsNullOrWhiteSpace(cleanName) && string.IsNullOrWhiteSpace(cleanCode))
        {
            return summary;
        }

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var whereClauses = new List<string>();
        var parameters = new List<(string name, OleDbType type, object val)>();

        if (cleanPhone.Length >= 10)
        {
            whereClauses.Add("(b.[Phone] LIKE ?)");
            parameters.Add(("@Phone", OleDbType.VarWChar, "%" + cleanPhone.Substring(cleanPhone.Length - 10) + "%"));
        }
        if (!string.IsNullOrWhiteSpace(cleanName))
        {
            whereClauses.Add("(b.[CustomerName] = ?)");
            parameters.Add(("@CustName", OleDbType.VarWChar, cleanName));
        }
        if (!string.IsNullOrWhiteSpace(cleanCode))
        {
            whereClauses.Add("(b.[CustomerCode] = ?)");
            parameters.Add(("@CustCode", OleDbType.VarWChar, cleanCode));
        }

        if (whereClauses.Count == 0) return summary;

        var filterSql = $"({string.Join(" OR ", whereClauses)})";

        var billSql = $@"
            SELECT b.[VouNo], b.[BillDate], b.[NetAmt]
            FROM [Bill] b
            WHERE (b.[BillCanceled] IS NULL OR b.[BillCanceled] = False)
              AND {filterSql}
            ORDER BY b.[BillDate] DESC, b.[VouNo] DESC";

        var matchedBills = new List<(string VouNo, DateTime BillDate, decimal NetAmt)>();
        await using (var billCmd = new OleDbCommand(billSql, connection))
        {
            foreach (var p in parameters)
            {
                billCmd.Parameters.Add(p.name, p.type).Value = p.val;
            }

            await using var reader = await billCmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var vNo = GetString(reader, "VouNo");
                var d = GetDateTime(reader, "BillDate");
                var amt = GetDecimal(reader, "NetAmt");
                matchedBills.Add((vNo, d, amt));
            }
        }

        if (matchedBills.Count == 0)
        {
            return summary;
        }

        summary.TotalBillsCount = matchedBills.Count;
        summary.TotalPurchaseValue = matchedBills.Sum(x => x.NetAmt);
        summary.AvgPurchaseValue = Math.Round(summary.TotalPurchaseValue / matchedBills.Count, 2);
        summary.LastPurchaseDate = matchedBills[0].BillDate;

        var itemsSql = $@"
            SELECT b.[BillDate], s.[VouNo], s.[ItemName], s.[Qty], s.[Amount], s.[NtWt]
            FROM [BillSubform] s
            INNER JOIN [Bill] b ON s.[VouNo] = b.[VouNo]
            WHERE (b.[BillCanceled] IS NULL OR b.[BillCanceled] = False)
              AND {filterSql}
            ORDER BY b.[BillDate] DESC, s.[VouNo] DESC, s.[AutoNo] ASC";

        var historyItems = new List<CustomerPurchaseHistoryItem>();
        await using (var itemsCmd = new OleDbCommand(itemsSql, connection))
        {
            foreach (var p in parameters)
            {
                itemsCmd.Parameters.Add(p.name, p.type).Value = p.val;
            }

            await using var reader = await itemsCmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                historyItems.Add(new CustomerPurchaseHistoryItem
                {
                    Date = GetDateTime(reader, "BillDate"),
                    VouNo = GetString(reader, "VouNo"),
                    ItemName = GetString(reader, "ItemName"),
                    Qty = GetInt(reader, "Qty"),
                    Amount = GetDecimal(reader, "Amount"),
                    NtWt = GetDecimal(reader, "NtWt")
                });
            }
        }

        summary.Items = historyItems;
        return summary;
    }

    public async Task<DateRangeCompanyInfo> GetCompanyDetailsAsync(CancellationToken cancellationToken = default)
    {
        return await DateRangeService.LoadFromConnectionStringAsync(_connectionString, cancellationToken);
    }

    private static DateTime GetDateTime(DbDataReader reader, string column) =>
        HasColumn(reader, column) && reader[column] != DBNull.Value ? Convert.ToDateTime(reader[column]) : DateTime.Today;
}
