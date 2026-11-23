using System.Data.Common;
using System.Data.OleDb;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.Infrastructure.Services;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class AccessPurchaseRepository : IPurchaseRepository
{
    private readonly string _connectionString;

    public AccessPurchaseRepository(string databasePath)
    {
        _connectionString = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={databasePath};";
    }

    private async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        await AccessPurchasesSchemaMigrator.EnsureSchemaAsync(_connectionString, cancellationToken);
        await AccessPaymentSubformSchemaMigrator.EnsureSchemaAsync(_connectionString, cancellationToken);
    }

    public async Task<IReadOnlyList<Purchase>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        const string sql = "SELECT * FROM Purchases ORDER BY EntryDate DESC, EntryTime DESC";
        var result = new List<Purchase>();

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(MapPurchase(reader));
        }

        return result;
    }

    public async Task<Purchase?> GetByVouNoAsync(string vouNo, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        const string sql = "SELECT * FROM Purchases WHERE [VouNo] = @VouNo";

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        command.Parameters.Add("@VouNo", OleDbType.VarWChar, 50).Value = vouNo;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapPurchase(reader);
        }

        return null;
    }

    public async Task<IReadOnlyList<PurchaseSubform>> GetSubformItemsAsync(string vouNo, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        const string sql = "SELECT * FROM PurchaseSubform WHERE [VouNo] = @VouNo ORDER BY [SlNo], [AutoNo]";
        var result = new List<PurchaseSubform>();

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        command.Parameters.Add("@VouNo", OleDbType.VarWChar, 50).Value = vouNo;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(MapPurchaseSubform(reader));
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
        command.Parameters.Add("@VouNo", OleDbType.VarWChar, 50).Value = vouNo;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(MapPaymentSubform(reader));
        }

        return result;
    }

    public async Task AddAsync(Purchase purchase, IEnumerable<PurchaseSubform> items, IEnumerable<PaymentSubform> payments, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var transaction = connection.BeginTransaction();

        try
        {
            const string insertPurchaseSql = @"
                INSERT INTO Purchases
                    ([VouNo], [VouType], [BillDate], [DueDate], [SuppBillNo], [SuppDate], [Phone],
                     [AccountName], [CustomerName], [CustomerCode], [Address], [Address2], [City], [Pincode],
                     [Email], [VatNo], [VatType], [Reverse], [Check], [BillCanceled], [GrossAmt], [DiscountAllowed],
                     [TaxableAmount], [Tax], [TaxAmt], [CGSTAmt], [SGSTAmt], [IGSTAmt], [AddLess], [AddLessNote],
                     [NetAmt], [Remarks], [Narration], [Note], [EntryUser], [EntryDate], [EntryTime], [ComputerName],
                     [Status], [InOut], [GrWt], [NtWt], [TotalQty])
                VALUES
                    (@VouNo, @VouType, @BillDate, @DueDate, @SuppBillNo, @SuppDate, @Phone,
                     @AccountName, @CustomerName, @CustomerCode, @Address, @Address2, @City, @Pincode,
                     @Email, @VatNo, @VatType, @Reverse, @Check, @BillCanceled, @GrossAmt, @DiscountAllowed,
                     @TaxableAmount, @Tax, @TaxAmt, @CGSTAmt, @SGSTAmt, @IGSTAmt, @AddLess, @AddLessNote,
                     @NetAmt, @Remarks, @Narration, @Note, @EntryUser, @EntryDate, @EntryTime, @ComputerName,
                     @Status, @InOut, @GrWt, @NtWt, @TotalQty)";

            await using (var cmd = new OleDbCommand(insertPurchaseSql, connection, transaction))
            {
                AddPurchaseParameters(cmd, purchase);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            const string insertItemSql = @"
                INSERT INTO PurchaseSubform
                    ([VouNo], [SlNo], [Barcode], [OldBarcode], [PcRate], [ItemName], [Purity], [HSN], [ENote],
                     [Qty], [GrWt], [LessWt], [NtWt], [Rate], [Amount], [MetalValue], [Making], [MperGr],
                     [StoneValue], [DisPer], [DisAmt], [GrossAmt], [TaxableAmount], [GstPer], [GstAmt],
                     [TotalAmt], [IsDiscount])
                VALUES
                    (@VouNo, @SlNo, @Barcode, @OldBarcode, @PcRate, @ItemName, @Purity, @HSN, @ENote,
                     @Qty, @GrWt, @LessWt, @NtWt, @Rate, @Amount, @MetalValue, @Making, @MperGr,
                     @StoneValue, @DisPer, @DisAmt, @GrossAmt, @TaxableAmount, @GstPer, @GstAmt,
                     @TotalAmt, @IsDiscount)";

            int sl = 1;
            foreach (var item in items)
            {
                await using var cmd = new OleDbCommand(insertItemSql, connection, transaction);
                cmd.Parameters.Add("@VouNo", OleDbType.VarWChar, 50).Value = purchase.VouNo;
                cmd.Parameters.Add("@SlNo", OleDbType.Integer).Value = sl++;
                cmd.Parameters.Add("@Barcode", OleDbType.VarWChar, 50).Value = item.Barcode ?? string.Empty;
                cmd.Parameters.Add("@OldBarcode", OleDbType.VarWChar, 50).Value = item.OldBarcode ?? string.Empty;
                cmd.Parameters.Add("@PcRate", OleDbType.Boolean).Value = item.PcRate;
                cmd.Parameters.Add("@ItemName", OleDbType.VarWChar, 150).Value = item.ItemName ?? string.Empty;
                cmd.Parameters.Add("@Purity", OleDbType.VarWChar, 50).Value = item.Purity ?? string.Empty;
                cmd.Parameters.Add("@HSN", OleDbType.VarWChar, 50).Value = item.HSN ?? string.Empty;
                cmd.Parameters.Add("@ENote", OleDbType.VarWChar, 255).Value = item.ENote ?? string.Empty;
                cmd.Parameters.Add("@Qty", OleDbType.Double).Value = (double)item.Qty;
                cmd.Parameters.Add("@GrWt", OleDbType.Currency).Value = item.GrWt;
                cmd.Parameters.Add("@LessWt", OleDbType.Currency).Value = item.LessWt;
                cmd.Parameters.Add("@NtWt", OleDbType.Currency).Value = item.NtWt;
                cmd.Parameters.Add("@Rate", OleDbType.Currency).Value = item.Rate;
                cmd.Parameters.Add("@Amount", OleDbType.Currency).Value = item.Amount;
                cmd.Parameters.Add("@MetalValue", OleDbType.Currency).Value = item.MetalValue;
                cmd.Parameters.Add("@Making", OleDbType.Currency).Value = item.Making;
                cmd.Parameters.Add("@MperGr", OleDbType.Currency).Value = item.MperGr;
                cmd.Parameters.Add("@StoneValue", OleDbType.Currency).Value = item.StoneValue;
                cmd.Parameters.Add("@DisPer", OleDbType.Currency).Value = item.DisPer;
                cmd.Parameters.Add("@DisAmt", OleDbType.Currency).Value = item.DisAmt;
                cmd.Parameters.Add("@GrossAmt", OleDbType.Currency).Value = item.GrossAmt;
                cmd.Parameters.Add("@TaxableAmount", OleDbType.Currency).Value = item.TaxableAmount;
                cmd.Parameters.Add("@GstPer", OleDbType.Currency).Value = item.GstPer;
                cmd.Parameters.Add("@GstAmt", OleDbType.Currency).Value = item.GstAmt;
                cmd.Parameters.Add("@TotalAmt", OleDbType.Currency).Value = item.TotalAmt;
                cmd.Parameters.Add("@IsDiscount", OleDbType.Boolean).Value = item.IsDiscount;

                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await InsertPaymentsAsync(connection, transaction, purchase.VouNo, payments, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task UpdateAsync(Purchase purchase, IEnumerable<PurchaseSubform> items, IEnumerable<PaymentSubform> payments, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var transaction = connection.BeginTransaction();

        try
        {
            const string updatePurchaseSql = @"
                UPDATE Purchases SET
                    [VouType] = @VouType, [BillDate] = @BillDate, [DueDate] = @DueDate, [SuppBillNo] = @SuppBillNo,
                    [SuppDate] = @SuppDate, [Phone] = @Phone, [AccountName] = @AccountName, [CustomerName] = @CustomerName,
                    [CustomerCode] = @CustomerCode, [Address] = @Address, [Address2] = @Address2, [City] = @City,
                    [Pincode] = @Pincode, [Email] = @Email, [VatNo] = @VatNo, [VatType] = @VatType, [Reverse] = @Reverse,
                    [Check] = @Check, [BillCanceled] = @BillCanceled, [GrossAmt] = @GrossAmt,
                    [DiscountAllowed] = @DiscountAllowed, [TaxableAmount] = @TaxableAmount, [Tax] = @Tax,
                    [TaxAmt] = @TaxAmt, [CGSTAmt] = @CGSTAmt, [SGSTAmt] = @SGSTAmt, [IGSTAmt] = @IGSTAmt,
                    [AddLess] = @AddLess, [AddLessNote] = @AddLessNote, [NetAmt] = @NetAmt, [Remarks] = @Remarks,
                    [Narration] = @Narration, [Note] = @Note, [EntryUser] = @EntryUser, [EntryDate] = @EntryDate,
                    [EntryTime] = @EntryTime, [ComputerName] = @ComputerName, [Status] = @Status, [InOut] = @InOut,
                    [GrWt] = @GrWt, [NtWt] = @NtWt, [TotalQty] = @TotalQty
                WHERE [VouNo] = @WhereVouNo";

            await using (var cmd = new OleDbCommand(updatePurchaseSql, connection, transaction))
            {
                cmd.Parameters.Add("@VouType", OleDbType.VarWChar, 50).Value = purchase.VouType ?? string.Empty;
                cmd.Parameters.Add("@BillDate", OleDbType.Date).Value = purchase.BillDate;
                cmd.Parameters.Add("@DueDate", OleDbType.Date).Value = purchase.DueDate;
                cmd.Parameters.Add("@SuppBillNo", OleDbType.VarWChar, 50).Value = purchase.SuppBillNo ?? string.Empty;
                cmd.Parameters.Add("@SuppDate", OleDbType.Date).Value = (object?)purchase.SuppDate ?? DBNull.Value;
                cmd.Parameters.Add("@Phone", OleDbType.VarWChar, 50).Value = purchase.Phone ?? string.Empty;
                cmd.Parameters.Add("@AccountName", OleDbType.VarWChar, 150).Value = purchase.AccountName ?? string.Empty;
                cmd.Parameters.Add("@CustomerName", OleDbType.VarWChar, 150).Value = purchase.CustomerName ?? string.Empty;
                cmd.Parameters.Add("@CustomerCode", OleDbType.VarWChar, 50).Value = purchase.CustomerCode ?? string.Empty;
                cmd.Parameters.Add("@Address", OleDbType.VarWChar, 255).Value = purchase.Address ?? string.Empty;
                cmd.Parameters.Add("@Address2", OleDbType.VarWChar, 255).Value = purchase.Address2 ?? string.Empty;
                cmd.Parameters.Add("@City", OleDbType.VarWChar, 50).Value = purchase.City ?? string.Empty;
                cmd.Parameters.Add("@Pincode", OleDbType.VarWChar, 20).Value = purchase.Pincode ?? string.Empty;
                cmd.Parameters.Add("@Email", OleDbType.VarWChar, 100).Value = purchase.Email ?? string.Empty;
                cmd.Parameters.Add("@VatNo", OleDbType.VarWChar, 50).Value = purchase.VatNo ?? string.Empty;
                cmd.Parameters.Add("@VatType", OleDbType.VarWChar, 50).Value = purchase.VatType ?? string.Empty;
                cmd.Parameters.Add("@Reverse", OleDbType.Boolean).Value = purchase.Reverse;
                cmd.Parameters.Add("@Check", OleDbType.Boolean).Value = purchase.Check;
                cmd.Parameters.Add("@BillCanceled", OleDbType.Boolean).Value = purchase.BillCanceled;
                cmd.Parameters.Add("@GrossAmt", OleDbType.Currency).Value = purchase.GrossAmt;
                cmd.Parameters.Add("@DiscountAllowed", OleDbType.Currency).Value = purchase.DiscountAllowed;
                cmd.Parameters.Add("@TaxableAmount", OleDbType.Currency).Value = purchase.TaxableAmount;
                cmd.Parameters.Add("@Tax", OleDbType.Currency).Value = purchase.Tax;
                cmd.Parameters.Add("@TaxAmt", OleDbType.Currency).Value = purchase.TaxAmt;
                cmd.Parameters.Add("@CGSTAmt", OleDbType.Currency).Value = purchase.CGSTAmt;
                cmd.Parameters.Add("@SGSTAmt", OleDbType.Currency).Value = purchase.SGSTAmt;
                cmd.Parameters.Add("@IGSTAmt", OleDbType.Currency).Value = purchase.IGSTAmt;
                cmd.Parameters.Add("@AddLess", OleDbType.Currency).Value = purchase.AddLess;
                cmd.Parameters.Add("@AddLessNote", OleDbType.VarWChar, 100).Value = purchase.AddLessNote ?? string.Empty;
                cmd.Parameters.Add("@NetAmt", OleDbType.Currency).Value = purchase.NetAmt;
                cmd.Parameters.Add("@Remarks", OleDbType.VarWChar, 255).Value = purchase.Remarks ?? string.Empty;
                cmd.Parameters.Add("@Narration", OleDbType.VarWChar, 255).Value = purchase.Narration ?? string.Empty;
                cmd.Parameters.Add("@Note", OleDbType.VarWChar, 255).Value = purchase.Note ?? string.Empty;
                cmd.Parameters.Add("@EntryUser", OleDbType.VarWChar, 50).Value = purchase.EntryUser ?? string.Empty;
                cmd.Parameters.Add("@EntryDate", OleDbType.Date).Value = purchase.EntryDate;
                cmd.Parameters.Add("@EntryTime", OleDbType.Date).Value = purchase.EntryTime;
                cmd.Parameters.Add("@ComputerName", OleDbType.VarWChar, 50).Value = purchase.ComputerName ?? string.Empty;
                cmd.Parameters.Add("@Status", OleDbType.VarWChar, 50).Value = purchase.Status ?? string.Empty;
                cmd.Parameters.Add("@InOut", OleDbType.VarWChar, 10).Value = purchase.InOut ?? "In";
                cmd.Parameters.Add("@GrWt", OleDbType.Currency).Value = purchase.GrWt;
                cmd.Parameters.Add("@NtWt", OleDbType.Currency).Value = purchase.NtWt;
                cmd.Parameters.Add("@TotalQty", OleDbType.Double).Value = (double)purchase.TotalQty;
                cmd.Parameters.Add("@WhereVouNo", OleDbType.VarWChar, 50).Value = purchase.VouNo;

                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            const string delSub = "DELETE FROM PurchaseSubform WHERE [VouNo] = @VouNo";
            await using (var cmd = new OleDbCommand(delSub, connection, transaction))
            {
                cmd.Parameters.Add("@VouNo", OleDbType.VarWChar, 50).Value = purchase.VouNo;
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            const string delPay = "DELETE FROM PaymentSubform WHERE [VouNo] = @VouNo";
            await using (var cmd = new OleDbCommand(delPay, connection, transaction))
            {
                cmd.Parameters.Add("@VouNo", OleDbType.VarWChar, 50).Value = purchase.VouNo;
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            const string insertItemSql = @"
                INSERT INTO PurchaseSubform
                    ([VouNo], [SlNo], [Barcode], [OldBarcode], [PcRate], [ItemName], [Purity], [HSN], [ENote],
                     [Qty], [GrWt], [LessWt], [NtWt], [Rate], [Amount], [MetalValue], [Making], [MperGr],
                     [StoneValue], [DisPer], [DisAmt], [GrossAmt], [TaxableAmount], [GstPer], [GstAmt],
                     [TotalAmt], [IsDiscount])
                VALUES
                    (@VouNo, @SlNo, @Barcode, @OldBarcode, @PcRate, @ItemName, @Purity, @HSN, @ENote,
                     @Qty, @GrWt, @LessWt, @NtWt, @Rate, @Amount, @MetalValue, @Making, @MperGr,
                     @StoneValue, @DisPer, @DisAmt, @GrossAmt, @TaxableAmount, @GstPer, @GstAmt,
                     @TotalAmt, @IsDiscount)";

            int sl = 1;
            foreach (var item in items)
            {
                await using var cmd = new OleDbCommand(insertItemSql, connection, transaction);
                cmd.Parameters.Add("@VouNo", OleDbType.VarWChar, 50).Value = purchase.VouNo;
                cmd.Parameters.Add("@SlNo", OleDbType.Integer).Value = sl++;
                cmd.Parameters.Add("@Barcode", OleDbType.VarWChar, 50).Value = item.Barcode ?? string.Empty;
                cmd.Parameters.Add("@OldBarcode", OleDbType.VarWChar, 50).Value = item.OldBarcode ?? string.Empty;
                cmd.Parameters.Add("@PcRate", OleDbType.Boolean).Value = item.PcRate;
                cmd.Parameters.Add("@ItemName", OleDbType.VarWChar, 150).Value = item.ItemName ?? string.Empty;
                cmd.Parameters.Add("@Purity", OleDbType.VarWChar, 50).Value = item.Purity ?? string.Empty;
                cmd.Parameters.Add("@HSN", OleDbType.VarWChar, 50).Value = item.HSN ?? string.Empty;
                cmd.Parameters.Add("@ENote", OleDbType.VarWChar, 255).Value = item.ENote ?? string.Empty;
                cmd.Parameters.Add("@Qty", OleDbType.Double).Value = (double)item.Qty;
                cmd.Parameters.Add("@GrWt", OleDbType.Currency).Value = item.GrWt;
                cmd.Parameters.Add("@LessWt", OleDbType.Currency).Value = item.LessWt;
                cmd.Parameters.Add("@NtWt", OleDbType.Currency).Value = item.NtWt;
                cmd.Parameters.Add("@Rate", OleDbType.Currency).Value = item.Rate;
                cmd.Parameters.Add("@Amount", OleDbType.Currency).Value = item.Amount;
                cmd.Parameters.Add("@MetalValue", OleDbType.Currency).Value = item.MetalValue;
                cmd.Parameters.Add("@Making", OleDbType.Currency).Value = item.Making;
                cmd.Parameters.Add("@MperGr", OleDbType.Currency).Value = item.MperGr;
                cmd.Parameters.Add("@StoneValue", OleDbType.Currency).Value = item.StoneValue;
                cmd.Parameters.Add("@DisPer", OleDbType.Currency).Value = item.DisPer;
                cmd.Parameters.Add("@DisAmt", OleDbType.Currency).Value = item.DisAmt;
                cmd.Parameters.Add("@GrossAmt", OleDbType.Currency).Value = item.GrossAmt;
                cmd.Parameters.Add("@TaxableAmount", OleDbType.Currency).Value = item.TaxableAmount;
                cmd.Parameters.Add("@GstPer", OleDbType.Currency).Value = item.GstPer;
                cmd.Parameters.Add("@GstAmt", OleDbType.Currency).Value = item.GstAmt;
                cmd.Parameters.Add("@TotalAmt", OleDbType.Currency).Value = item.TotalAmt;
                cmd.Parameters.Add("@IsDiscount", OleDbType.Boolean).Value = item.IsDiscount;

                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await InsertPaymentsAsync(connection, transaction, purchase.VouNo, payments, cancellationToken);

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
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var transaction = connection.BeginTransaction();

        try
        {
            const string delSub = "DELETE FROM PurchaseSubform WHERE [VouNo] = @VouNo";
            await using (var cmd = new OleDbCommand(delSub, connection, transaction))
            {
                cmd.Parameters.Add("@VouNo", OleDbType.VarWChar, 50).Value = vouNo;
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            const string delPay = "DELETE FROM PaymentSubform WHERE [VouNo] = @VouNo";
            await using (var cmd = new OleDbCommand(delPay, connection, transaction))
            {
                cmd.Parameters.Add("@VouNo", OleDbType.VarWChar, 50).Value = vouNo;
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            const string delPurchase = "DELETE FROM Purchases WHERE [VouNo] = @VouNo";
            await using (var cmd = new OleDbCommand(delPurchase, connection, transaction))
            {
                cmd.Parameters.Add("@VouNo", OleDbType.VarWChar, 50).Value = vouNo;
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<IReadOnlyList<SalesVouType>> GetPurchaseVouTypesAsync(string? counter = null, string? headCategory = "Purchase", CancellationToken cancellationToken = default)
    {
        var result = new List<SalesVouType>();
        const string sql = "SELECT * FROM [SalesVouType] WHERE ([Hide] = False OR [Hide] IS NULL) ORDER BY [VouType]";

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var isPurchase = string.IsNullOrWhiteSpace(headCategory) ||
                         string.Equals(headCategory, "Purchase", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(headCategory, "Purchases", StringComparison.OrdinalIgnoreCase);

        while (await reader.ReadAsync(cancellationToken))
        {
            var item = MapSalesVouType(reader);

            if (isPurchase)
            {
                if (!string.Equals(item.HeadCategory, "Purchase", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(item.HeadCategory, "Purchases", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }
            else if (!string.Equals(item.HeadCategory, headCategory!.Trim(), StringComparison.OrdinalIgnoreCase))
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
        await EnsureSchemaAsync(cancellationToken);
        const string sql = "SELECT [VouNo] FROM Purchases WHERE [VouType] = @VouType";

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        command.Parameters.Add("@VouType", OleDbType.VarWChar, 50).Value = vouType;

        var prefix = !string.IsNullOrWhiteSpace(shortName) ? shortName.Trim() : vouType.Trim();
        long maxNumber = 0;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var vouNo = reader["VouNo"]?.ToString();
            if (string.IsNullOrWhiteSpace(vouNo)) continue;

            var numericPart = new string(vouNo.Where(char.IsDigit).ToArray());
            if (long.TryParse(numericPart, out var num) && num > maxNumber)
            {
                maxNumber = num;
            }
        }

        return $"{prefix}{maxNumber + 1}";
    }

    public async Task<IReadOnlyList<InvoiceListItem>> GetPurchaseListAsync(DateTime? fromDate = null, DateTime? toDate = null, string? vouType = null, CancellationToken cancellationToken = default)
    {
        var filter = new PurchaseRegisterFilter
        {
            FromDate = fromDate,
            ToDate = toDate,
            VouType = vouType
        };

        var register = await GetPurchaseRegisterAsync(filter, cancellationToken);
        return register.Select(p => new InvoiceListItem
        {
            VouNo = p.VouNo,
            VouType = p.VouType,
            BillDate = p.BillDate,
            CustomerName = p.PartyDisplayName,
            Phone = p.Phone,
            Address = p.Address,
            City = p.City,
            GrossAmt = p.GrossAmt,
            TaxableAmt = p.TaxableAmount,
            TaxAmt = p.TaxAmt,
            NetAmt = p.NetAmt,
            Qty = (int)Math.Round(p.TotalQty),
            GrWt = p.GrWt,
            NetWt = p.NtWt,
            Status = p.Status
        }).ToList();
    }

    public async Task<IReadOnlyList<PurchaseRegisterItem>> GetPurchaseRegisterAsync(PurchaseRegisterFilter filter, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        var result = new List<PurchaseRegisterItem>();
        var sql = "SELECT * FROM Purchases WHERE 1=1";

        if (filter.FromDate.HasValue) sql += " AND [BillDate] >= @FromDate";
        if (filter.ToDate.HasValue) sql += " AND [BillDate] <= @ToDate";
        if (!string.IsNullOrWhiteSpace(filter.VouType) && !string.Equals(filter.VouType.Trim(), "ALL", StringComparison.OrdinalIgnoreCase))
        {
            sql += " AND [VouType] = @VouType";
        }

        sql += " ORDER BY [BillDate] ASC, [VouNo] ASC";

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        if (filter.FromDate.HasValue) command.Parameters.Add("@FromDate", OleDbType.Date).Value = filter.FromDate.Value.Date;
        if (filter.ToDate.HasValue) command.Parameters.Add("@ToDate", OleDbType.Date).Value = filter.ToDate.Value.Date.AddDays(1).AddSeconds(-1);
        if (!string.IsNullOrWhiteSpace(filter.VouType) && !string.Equals(filter.VouType.Trim(), "ALL", StringComparison.OrdinalIgnoreCase))
        {
            command.Parameters.Add("@VouType", OleDbType.VarWChar, 50).Value = filter.VouType.Trim();
        }

        var purchases = new List<PurchaseRegisterItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            purchases.Add(new PurchaseRegisterItem
            {
                VouNo = reader["VouNo"]?.ToString() ?? string.Empty,
                VouType = reader["VouType"]?.ToString() ?? "Purchase",
                BillDate = reader["BillDate"] != DBNull.Value ? Convert.ToDateTime(reader["BillDate"]) : DateTime.Today,
                DueDate = reader["DueDate"] != DBNull.Value ? Convert.ToDateTime(reader["DueDate"]) : DateTime.Today,
                SuppBillNo = reader["SuppBillNo"]?.ToString() ?? string.Empty,
                SuppDate = reader["SuppDate"] != DBNull.Value ? Convert.ToDateTime(reader["SuppDate"]) : null,
                Phone = reader["Phone"]?.ToString() ?? string.Empty,
                AccountName = reader["AccountName"]?.ToString() ?? string.Empty,
                CustomerName = reader["CustomerName"]?.ToString() ?? string.Empty,
                CustomerCode = reader["CustomerCode"]?.ToString() ?? string.Empty,
                Address = reader["Address"]?.ToString() ?? string.Empty,
                Address2 = reader["Address2"]?.ToString() ?? string.Empty,
                City = reader["City"]?.ToString() ?? "CUTTACK",
                Pincode = reader["Pincode"]?.ToString() ?? string.Empty,
                Email = reader["Email"]?.ToString() ?? string.Empty,
                VatNo = reader["VatNo"]?.ToString() ?? string.Empty,
                VatType = reader["VatType"]?.ToString() ?? string.Empty,
                BillCanceled = reader["BillCanceled"] != DBNull.Value && Convert.ToBoolean(reader["BillCanceled"]),
                GrossAmt = reader["GrossAmt"] != DBNull.Value ? Convert.ToDecimal(reader["GrossAmt"]) : 0m,
                DiscountAllowed = reader["DiscountAllowed"] != DBNull.Value ? Convert.ToDecimal(reader["DiscountAllowed"]) : 0m,
                TaxableAmount = reader["TaxableAmount"] != DBNull.Value ? Convert.ToDecimal(reader["TaxableAmount"]) : 0m,
                Tax = reader["Tax"] != DBNull.Value ? Convert.ToDecimal(reader["Tax"]) : 0m,
                TaxAmt = reader["TaxAmt"] != DBNull.Value ? Convert.ToDecimal(reader["TaxAmt"]) : 0m,
                CGSTAmt = reader["CGSTAmt"] != DBNull.Value ? Convert.ToDecimal(reader["CGSTAmt"]) : 0m,
                SGSTAmt = reader["SGSTAmt"] != DBNull.Value ? Convert.ToDecimal(reader["SGSTAmt"]) : 0m,
                IGSTAmt = reader["IGSTAmt"] != DBNull.Value ? Convert.ToDecimal(reader["IGSTAmt"]) : 0m,
                AddLess = reader["AddLess"] != DBNull.Value ? Convert.ToDecimal(reader["AddLess"]) : 0m,
                AddLessNote = reader["AddLessNote"]?.ToString() ?? string.Empty,
                NetAmt = reader["NetAmt"] != DBNull.Value ? Convert.ToDecimal(reader["NetAmt"]) : 0m,
                Remarks = reader["Remarks"]?.ToString() ?? string.Empty,
                Narration = reader["Narration"]?.ToString() ?? string.Empty,
                Status = reader["Status"]?.ToString() ?? "Saved",
                GrWt = reader["GrWt"] != DBNull.Value ? Convert.ToDecimal(reader["GrWt"]) : 0m,
                NtWt = reader["NtWt"] != DBNull.Value ? Convert.ToDecimal(reader["NtWt"]) : 0m,
                TotalQty = reader["TotalQty"] != DBNull.Value ? Convert.ToDecimal(reader["TotalQty"]) : 0m
            });
        }

        // Fetch Subform items for all returned vouchers
        if (purchases.Count > 0)
        {
            var vouList = purchases.Select(p => p.VouNo).Distinct().ToList();
            var allSubItems = new Dictionary<string, List<PurchaseSubform>>(StringComparer.OrdinalIgnoreCase);

            // Fetch subform in batches of 50
            for (int i = 0; i < vouList.Count; i += 50)
            {
                var batch = vouList.Skip(i).Take(50).ToList();
                var inClause = string.Join(",", batch.Select(v => $"'{v.Replace("'", "''")}'"));
                var subSql = $"SELECT * FROM PurchaseSubform WHERE [VouNo] IN ({inClause}) ORDER BY [SlNo], [AutoNo]";

                await using var subCmd = new OleDbCommand(subSql, connection);
                await using var subReader = await subCmd.ExecuteReaderAsync(cancellationToken);
                while (await subReader.ReadAsync(cancellationToken))
                {
                    var item = MapPurchaseSubform(subReader);
                    if (!allSubItems.TryGetValue(item.VouNo, out var list))
                    {
                        list = new List<PurchaseSubform>();
                        allSubItems[item.VouNo] = list;
                    }
                    list.Add(item);
                }
            }

            foreach (var p in purchases)
            {
                if (allSubItems.TryGetValue(p.VouNo, out var items))
                {
                    p.Items = items;
                    if (p.TotalQty == 0 && items.Count > 0)
                    {
                        p.TotalQty = items.Sum(x => x.Qty);
                    }
                    if (p.NtWt == 0 && items.Count > 0)
                    {
                        p.NtWt = items.Sum(x => x.NtWt);
                    }
                    if (p.GrWt == 0 && items.Count > 0)
                    {
                        p.GrWt = items.Sum(x => x.GrWt);
                    }
                }
            }
        }

        // ItemType filter
        var itemTypeFilter = filter.ItemType?.Trim();
        if (!string.IsNullOrWhiteSpace(itemTypeFilter) && !string.Equals(itemTypeFilter, "ALL", StringComparison.OrdinalIgnoreCase) && !string.Equals(itemTypeFilter, "*", StringComparison.OrdinalIgnoreCase))
        {
            purchases = purchases.Where(p => p.Items.Any(item =>
                (!string.IsNullOrWhiteSpace(item.ItemName) && item.ItemName.Contains(itemTypeFilter, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(item.Purity) && item.Purity.Contains(itemTypeFilter, StringComparison.OrdinalIgnoreCase))
            )).ToList();
        }

        // Search text filter
        var searchText = filter.SearchText?.Trim();
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            purchases = purchases.Where(p =>
                p.VouNo.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                p.SuppBillNo.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                p.PartyDisplayName.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                p.Phone.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                p.City.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                p.VatNo.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                p.CustomerCode.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                p.Items.Any(it => it.ItemName.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            ).ToList();
        }

        return purchases;
    }

    public async Task<IReadOnlyList<string>> GetAvailableItemTypesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        var types = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // 1. From ItemType table
        try
        {
            await using var cmd = new OleDbCommand("SELECT DISTINCT [ItemType] FROM [ItemType] WHERE [ItemType] IS NOT NULL AND [ItemType] <> ''", connection);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var val = reader[0]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(val)) types.Add(val);
            }
        }
        catch { }

        // 2. From PURITY table
        try
        {
            await using var cmd = new OleDbCommand("SELECT DISTINCT [ItemType] FROM [PURITY] WHERE [ItemType] IS NOT NULL AND [ItemType] <> ''", connection);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var val = reader[0]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(val)) types.Add(val);
            }
        }
        catch { }

        // 3. From ItemCreation table (Group and MetalType)
        try
        {
            await using var cmd = new OleDbCommand("SELECT DISTINCT [Group] FROM [ItemCreation] WHERE [Group] IS NOT NULL AND [Group] <> ''", connection);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var val = reader[0]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(val)) types.Add(val);
            }
        }
        catch { }

        // 4. From PurchaseSubform table (ItemName)
        try
        {
            await using var cmd = new OleDbCommand("SELECT DISTINCT [ItemName] FROM [PurchaseSubform] WHERE [ItemName] IS NOT NULL AND [ItemName] <> ''", connection);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var val = reader[0]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(val)) types.Add(val);
            }
        }
        catch { }

        return types.OrderBy(t => t).ToList();
    }

    private static void AddPurchaseParameters(OleDbCommand cmd, Purchase purchase)
    {
        cmd.Parameters.Add("@VouNo", OleDbType.VarWChar, 50).Value = purchase.VouNo ?? string.Empty;
        cmd.Parameters.Add("@VouType", OleDbType.VarWChar, 50).Value = purchase.VouType ?? string.Empty;
        cmd.Parameters.Add("@BillDate", OleDbType.Date).Value = purchase.BillDate;
        cmd.Parameters.Add("@DueDate", OleDbType.Date).Value = purchase.DueDate;
        cmd.Parameters.Add("@SuppBillNo", OleDbType.VarWChar, 50).Value = purchase.SuppBillNo ?? string.Empty;
        cmd.Parameters.Add("@SuppDate", OleDbType.Date).Value = (object?)purchase.SuppDate ?? DBNull.Value;
        cmd.Parameters.Add("@Phone", OleDbType.VarWChar, 50).Value = purchase.Phone ?? string.Empty;
        cmd.Parameters.Add("@AccountName", OleDbType.VarWChar, 150).Value = purchase.AccountName ?? string.Empty;
        cmd.Parameters.Add("@CustomerName", OleDbType.VarWChar, 150).Value = purchase.CustomerName ?? string.Empty;
        cmd.Parameters.Add("@CustomerCode", OleDbType.VarWChar, 50).Value = purchase.CustomerCode ?? string.Empty;
        cmd.Parameters.Add("@Address", OleDbType.VarWChar, 255).Value = purchase.Address ?? string.Empty;
        cmd.Parameters.Add("@Address2", OleDbType.VarWChar, 255).Value = purchase.Address2 ?? string.Empty;
        cmd.Parameters.Add("@City", OleDbType.VarWChar, 50).Value = purchase.City ?? string.Empty;
        cmd.Parameters.Add("@Pincode", OleDbType.VarWChar, 20).Value = purchase.Pincode ?? string.Empty;
        cmd.Parameters.Add("@Email", OleDbType.VarWChar, 100).Value = purchase.Email ?? string.Empty;
        cmd.Parameters.Add("@VatNo", OleDbType.VarWChar, 50).Value = purchase.VatNo ?? string.Empty;
        cmd.Parameters.Add("@VatType", OleDbType.VarWChar, 50).Value = purchase.VatType ?? string.Empty;
        cmd.Parameters.Add("@Reverse", OleDbType.Boolean).Value = purchase.Reverse;
        cmd.Parameters.Add("@Check", OleDbType.Boolean).Value = purchase.Check;
        cmd.Parameters.Add("@BillCanceled", OleDbType.Boolean).Value = purchase.BillCanceled;
        cmd.Parameters.Add("@GrossAmt", OleDbType.Currency).Value = purchase.GrossAmt;
        cmd.Parameters.Add("@DiscountAllowed", OleDbType.Currency).Value = purchase.DiscountAllowed;
        cmd.Parameters.Add("@TaxableAmount", OleDbType.Currency).Value = purchase.TaxableAmount;
        cmd.Parameters.Add("@Tax", OleDbType.Currency).Value = purchase.Tax;
        cmd.Parameters.Add("@TaxAmt", OleDbType.Currency).Value = purchase.TaxAmt;
        cmd.Parameters.Add("@CGSTAmt", OleDbType.Currency).Value = purchase.CGSTAmt;
        cmd.Parameters.Add("@SGSTAmt", OleDbType.Currency).Value = purchase.SGSTAmt;
        cmd.Parameters.Add("@IGSTAmt", OleDbType.Currency).Value = purchase.IGSTAmt;
        cmd.Parameters.Add("@AddLess", OleDbType.Currency).Value = purchase.AddLess;
        cmd.Parameters.Add("@AddLessNote", OleDbType.VarWChar, 100).Value = purchase.AddLessNote ?? string.Empty;
        cmd.Parameters.Add("@NetAmt", OleDbType.Currency).Value = purchase.NetAmt;
        cmd.Parameters.Add("@Remarks", OleDbType.VarWChar, 255).Value = purchase.Remarks ?? string.Empty;
        cmd.Parameters.Add("@Narration", OleDbType.VarWChar, 255).Value = purchase.Narration ?? string.Empty;
        cmd.Parameters.Add("@Note", OleDbType.VarWChar, 255).Value = purchase.Note ?? string.Empty;
        cmd.Parameters.Add("@EntryUser", OleDbType.VarWChar, 50).Value = purchase.EntryUser ?? string.Empty;
        cmd.Parameters.Add("@EntryDate", OleDbType.Date).Value = purchase.EntryDate;
        cmd.Parameters.Add("@EntryTime", OleDbType.Date).Value = purchase.EntryTime;
        cmd.Parameters.Add("@ComputerName", OleDbType.VarWChar, 50).Value = purchase.ComputerName ?? string.Empty;
        cmd.Parameters.Add("@Status", OleDbType.VarWChar, 50).Value = purchase.Status ?? string.Empty;
        cmd.Parameters.Add("@InOut", OleDbType.VarWChar, 10).Value = purchase.InOut ?? "In";
        cmd.Parameters.Add("@GrWt", OleDbType.Currency).Value = purchase.GrWt;
        cmd.Parameters.Add("@NtWt", OleDbType.Currency).Value = purchase.NtWt;
        cmd.Parameters.Add("@TotalQty", OleDbType.Double).Value = (double)purchase.TotalQty;
    }

    private static HashSet<string>? _paymentSubformColumns;
    private static readonly object _paymentColLock = new();

    private static async Task<HashSet<string>> GetPaymentSubformColumnsAsync(OleDbConnection connection, OleDbTransaction? transaction, CancellationToken cancellationToken)
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
            lock (_paymentColLock)
            {
                _paymentSubformColumns = cols;
            }
        }

        return cols;
    }

    private static async Task InsertPaymentsAsync(OleDbConnection connection, OleDbTransaction transaction, string vouNo, IEnumerable<PaymentSubform> payments, CancellationToken cancellationToken)
    {
        var existingCols = await GetPaymentSubformColumnsAsync(connection, transaction, cancellationToken);

        var payBaseTime = DateTime.Now;
        int payIndex = 0;

        foreach (var pay in payments)
        {
            if (pay.Amount == 0 && string.IsNullOrWhiteSpace(pay.AccountName)) continue;

            var payNumber = !string.IsNullOrWhiteSpace(pay.Number)
                ? pay.Number
                : payBaseTime.AddSeconds(payIndex++).ToString("dd/MM/yyyy HH:mm:ss");

            var colNames = new List<string> { "[VouNo]", "[AccountName]", "[Amount]" };
            var valParams = new List<string> { "@VouNo", "@AccountName", "@Amount" };

            var cmd = new OleDbCommand();
            cmd.Connection = connection;
            cmd.Transaction = transaction;

            cmd.Parameters.Add("@VouNo", OleDbType.VarWChar, 50).Value = vouNo;
            cmd.Parameters.Add("@AccountName", OleDbType.VarWChar, 150).Value = pay.AccountName ?? string.Empty;
            cmd.Parameters.Add("@Amount", OleDbType.Currency).Value = pay.Amount;

            if (existingCols.Contains("Number"))
            {
                colNames.Add("[Number]");
                valParams.Add("@Number");
                cmd.Parameters.Add("@Number", OleDbType.VarWChar, 50).Value = payNumber;
            }

            if (existingCols.Contains("HeadCategory") && !string.IsNullOrWhiteSpace(pay.HeadCategory))
            {
                colNames.Add("[HeadCategory]");
                valParams.Add("@HeadCategory");
                cmd.Parameters.Add("@HeadCategory", OleDbType.VarWChar, 50).Value = pay.HeadCategory;
            }

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
                cmd.Parameters.Add("@ItemName", OleDbType.VarWChar, 150).Value = !string.IsNullOrWhiteSpace(pay.ItemName) ? pay.ItemName : "OLD";
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

            cmd.CommandText = $"INSERT INTO [PaymentSubform] ({string.Join(", ", colNames)}) VALUES ({string.Join(", ", valParams)})";
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static Purchase MapPurchase(DbDataReader reader)
    {
        return new Purchase
        {
            VouNo = reader["VouNo"]?.ToString() ?? string.Empty,
            VouType = reader["VouType"]?.ToString() ?? "Purchase",
            BillDate = reader["BillDate"] != DBNull.Value ? Convert.ToDateTime(reader["BillDate"]) : DateTime.Today,
            DueDate = reader["DueDate"] != DBNull.Value ? Convert.ToDateTime(reader["DueDate"]) : DateTime.Today,
            SuppBillNo = reader["SuppBillNo"]?.ToString() ?? string.Empty,
            SuppDate = reader["SuppDate"] != DBNull.Value ? Convert.ToDateTime(reader["SuppDate"]) : null,
            Phone = reader["Phone"]?.ToString() ?? string.Empty,
            AccountName = reader["AccountName"]?.ToString() ?? string.Empty,
            CustomerName = reader["CustomerName"]?.ToString() ?? string.Empty,
            CustomerCode = reader["CustomerCode"]?.ToString() ?? string.Empty,
            Address = reader["Address"]?.ToString() ?? string.Empty,
            Address2 = reader["Address2"]?.ToString() ?? string.Empty,
            City = reader["City"]?.ToString() ?? "CUTTACK",
            Pincode = reader["Pincode"]?.ToString() ?? string.Empty,
            Email = reader["Email"]?.ToString() ?? string.Empty,
            VatNo = reader["VatNo"]?.ToString() ?? string.Empty,
            VatType = reader["VatType"]?.ToString() ?? string.Empty,
            Reverse = reader["Reverse"] != DBNull.Value && Convert.ToBoolean(reader["Reverse"]),
            Check = reader["Check"] != DBNull.Value && Convert.ToBoolean(reader["Check"]),
            BillCanceled = reader["BillCanceled"] != DBNull.Value && Convert.ToBoolean(reader["BillCanceled"]),
            GrossAmt = reader["GrossAmt"] != DBNull.Value ? Convert.ToDecimal(reader["GrossAmt"]) : 0m,
            DiscountAllowed = reader["DiscountAllowed"] != DBNull.Value ? Convert.ToDecimal(reader["DiscountAllowed"]) : 0m,
            TaxableAmount = reader["TaxableAmount"] != DBNull.Value ? Convert.ToDecimal(reader["TaxableAmount"]) : 0m,
            Tax = reader["Tax"] != DBNull.Value ? Convert.ToDecimal(reader["Tax"]) : 0m,
            TaxAmt = reader["TaxAmt"] != DBNull.Value ? Convert.ToDecimal(reader["TaxAmt"]) : 0m,
            CGSTAmt = reader["CGSTAmt"] != DBNull.Value ? Convert.ToDecimal(reader["CGSTAmt"]) : 0m,
            SGSTAmt = reader["SGSTAmt"] != DBNull.Value ? Convert.ToDecimal(reader["SGSTAmt"]) : 0m,
            IGSTAmt = reader["IGSTAmt"] != DBNull.Value ? Convert.ToDecimal(reader["IGSTAmt"]) : 0m,
            AddLess = reader["AddLess"] != DBNull.Value ? Convert.ToDecimal(reader["AddLess"]) : 0m,
            AddLessNote = reader["AddLessNote"]?.ToString() ?? string.Empty,
            NetAmt = reader["NetAmt"] != DBNull.Value ? Convert.ToDecimal(reader["NetAmt"]) : 0m,
            Remarks = reader["Remarks"]?.ToString() ?? string.Empty,
            Narration = reader["Narration"]?.ToString() ?? string.Empty,
            Note = reader["Note"]?.ToString() ?? string.Empty,
            EntryUser = reader["EntryUser"]?.ToString() ?? string.Empty,
            EntryDate = reader["EntryDate"] != DBNull.Value ? Convert.ToDateTime(reader["EntryDate"]) : DateTime.Today,
            EntryTime = reader["EntryTime"] != DBNull.Value ? Convert.ToDateTime(reader["EntryTime"]) : DateTime.Now,
            ComputerName = reader["ComputerName"]?.ToString() ?? string.Empty,
            Status = reader["Status"]?.ToString() ?? "Saved",
            InOut = reader["InOut"]?.ToString() ?? "In",
            GrWt = reader["GrWt"] != DBNull.Value ? Convert.ToDecimal(reader["GrWt"]) : 0m,
            NtWt = reader["NtWt"] != DBNull.Value ? Convert.ToDecimal(reader["NtWt"]) : 0m,
            TotalQty = reader["TotalQty"] != DBNull.Value ? Convert.ToDecimal(reader["TotalQty"]) : 0m
        };
    }

    private static PurchaseSubform MapPurchaseSubform(DbDataReader reader)
    {
        return new PurchaseSubform
        {
            AutoNo = reader["AutoNo"] != DBNull.Value ? Convert.ToInt32(reader["AutoNo"]) : 0,
            VouNo = reader["VouNo"]?.ToString() ?? string.Empty,
            SlNo = reader["SlNo"] != DBNull.Value ? Convert.ToInt32(reader["SlNo"]) : 1,
            Barcode = reader["Barcode"]?.ToString() ?? string.Empty,
            OldBarcode = reader["OldBarcode"]?.ToString() ?? string.Empty,
            PcRate = reader["PcRate"] != DBNull.Value && Convert.ToBoolean(reader["PcRate"]),
            ItemName = reader["ItemName"]?.ToString() ?? string.Empty,
            Purity = reader["Purity"]?.ToString() ?? string.Empty,
            HSN = reader["HSN"]?.ToString() ?? string.Empty,
            ENote = reader["ENote"]?.ToString() ?? string.Empty,
            Qty = reader["Qty"] != DBNull.Value ? Convert.ToDecimal(reader["Qty"]) : 1m,
            GrWt = reader["GrWt"] != DBNull.Value ? Convert.ToDecimal(reader["GrWt"]) : 0m,
            LessWt = reader["LessWt"] != DBNull.Value ? Convert.ToDecimal(reader["LessWt"]) : 0m,
            NtWt = reader["NtWt"] != DBNull.Value ? Convert.ToDecimal(reader["NtWt"]) : 0m,
            Rate = reader["Rate"] != DBNull.Value ? Convert.ToDecimal(reader["Rate"]) : 0m,
            Amount = reader["Amount"] != DBNull.Value ? Convert.ToDecimal(reader["Amount"]) : 0m,
            MetalValue = reader["MetalValue"] != DBNull.Value ? Convert.ToDecimal(reader["MetalValue"]) : 0m,
            Making = reader["Making"] != DBNull.Value ? Convert.ToDecimal(reader["Making"]) : 0m,
            MperGr = reader["MperGr"] != DBNull.Value ? Convert.ToDecimal(reader["MperGr"]) : 0m,
            StoneValue = reader["StoneValue"] != DBNull.Value ? Convert.ToDecimal(reader["StoneValue"]) : 0m,
            DisPer = reader["DisPer"] != DBNull.Value ? Convert.ToDecimal(reader["DisPer"]) : 0m,
            DisAmt = reader["DisAmt"] != DBNull.Value ? Convert.ToDecimal(reader["DisAmt"]) : 0m,
            GrossAmt = reader["GrossAmt"] != DBNull.Value ? Convert.ToDecimal(reader["GrossAmt"]) : 0m,
            TaxableAmount = reader["TaxableAmount"] != DBNull.Value ? Convert.ToDecimal(reader["TaxableAmount"]) : 0m,
            GstPer = reader["GstPer"] != DBNull.Value ? Convert.ToDecimal(reader["GstPer"]) : 3.00m,
            GstAmt = reader["GstAmt"] != DBNull.Value ? Convert.ToDecimal(reader["GstAmt"]) : 0m,
            TotalAmt = reader["TotalAmt"] != DBNull.Value ? Convert.ToDecimal(reader["TotalAmt"]) : 0m,
            IsDiscount = reader["IsDiscount"] != DBNull.Value && Convert.ToBoolean(reader["IsDiscount"])
        };
    }

    private static PaymentSubform MapPaymentSubform(DbDataReader reader)
    {
        var itemVal = HasColumn(reader, "Item") ? GetString(reader, "Item") : (HasColumn(reader, "ItemType") ? GetString(reader, "ItemType") : string.Empty);
        return new PaymentSubform
        {
            VouNo = GetString(reader, "VouNo"),
            Number = HasColumn(reader, "Number") ? GetString(reader, "Number") : string.Empty,
            AccountName = GetString(reader, "AccountName"),
            Amount = GetDecimal(reader, "Amount"),
            HeadCategory = HasColumn(reader, "HeadCategory") ? GetString(reader, "HeadCategory") : string.Empty,
            Narration = HasColumn(reader, "Narration") ? GetString(reader, "Narration") : string.Empty,
            ItemType = itemVal,
            ItemName = HasColumn(reader, "ItemName") && !string.IsNullOrEmpty(GetString(reader, "ItemName")) ? GetString(reader, "ItemName") : string.Empty,
            Wt = HasColumn(reader, "Wt") ? GetDecimal(reader, "Wt") : 0m,
            LessWt = HasColumn(reader, "LessWt") ? GetDecimal(reader, "LessWt") : 0m,
            GrWt = HasColumn(reader, "GrWt") ? GetDecimal(reader, "GrWt") : 0m,
            Alloy = HasColumn(reader, "Alloy") && reader["Alloy"] != DBNull.Value ? GetDecimal(reader, "Alloy") : 100m,
            NtWt = HasColumn(reader, "NtWt") ? GetDecimal(reader, "NtWt") : 0m,
            Rate = HasColumn(reader, "Rate") ? GetDecimal(reader, "Rate") : 0m
        };
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

    private static bool IsCounterMatched(string? counterNoField, string targetCounter)
    {
        if (string.IsNullOrWhiteSpace(counterNoField)) return true;
        var target = targetCounter.Trim();
        if (string.IsNullOrEmpty(target)) return true;

        var counters = counterNoField.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return counters.Any(c => string.Equals(c, target, StringComparison.OrdinalIgnoreCase));
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

    public async Task<DateRangeCompanyInfo> GetCompanyDetailsAsync(CancellationToken cancellationToken = default)
    {
        return await DateRangeService.LoadFromConnectionStringAsync(_connectionString, cancellationToken);
    }
}
