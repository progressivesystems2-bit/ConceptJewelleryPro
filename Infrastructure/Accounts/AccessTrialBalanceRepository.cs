using System.Data;
using System.Data.OleDb;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class AccessTrialBalanceRepository : ITrialBalanceRepository
{
    private readonly string _connectionString;

    public AccessTrialBalanceRepository(string databasePath)
    {
        _connectionString = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={databasePath};";
    }

    public async Task<IReadOnlyList<string>> GetHeadCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var categories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // 1. From HeadCategory table
        try
        {
            const string sql = "SELECT [HeadCategory] FROM [HeadCategory] WHERE [HeadCategory] IS NOT NULL ORDER BY [HeadCategory]";
            await using var cmd = new OleDbCommand(sql, connection);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var val = reader[0]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(val))
                    categories.Add(val);
            }
        }
        catch { }

        // 2. From AccountCreation table
        try
        {
            const string sql2 = "SELECT DISTINCT [HeadCategory] FROM [AccountCreation] WHERE [HeadCategory] IS NOT NULL";
            await using var cmd2 = new OleDbCommand(sql2, connection);
            await using var reader2 = await cmd2.ExecuteReaderAsync(cancellationToken);
            while (await reader2.ReadAsync(cancellationToken))
            {
                var val = reader2[0]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(val))
                    categories.Add(val);
            }
        }
        catch { }

        return categories.OrderBy(c => c).ToList();
    }

    public async Task<IReadOnlyList<TrialBalanceItem>> GetTrialBalanceAsync(TrialBalanceFilter filter, CancellationToken cancellationToken = default)
    {
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var fromDate = filter.FromDate ?? new DateTime(2025, 4, 1);
        var toDate = filter.ToDate ?? DateTime.Today;

        // 1. Fetch All Accounts from AccountCreation
        var accounts = new List<AccountRaw>();
        const string accSql = @"
            SELECT [PartyCode], [AccountName], [HeadCategory], [SubCategory], [PhoneNo], [City], [OpAmt1], [OpAmt2], [EntryDate]
            FROM [AccountCreation]
            WHERE [AccountName] IS NOT NULL AND [AccountName] <> ''
            ORDER BY [HeadCategory], [AccountName]";

        await using (var cmd = new OleDbCommand(accSql, connection))
        await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var acc = new AccountRaw
                {
                    PartyCode = reader["PartyCode"]?.ToString()?.Trim() ?? string.Empty,
                    AccountName = reader["AccountName"]?.ToString()?.Trim() ?? string.Empty,
                    HeadCategory = reader["HeadCategory"]?.ToString()?.Trim() ?? "General",
                    SubCategory = reader["SubCategory"]?.ToString()?.Trim() ?? string.Empty,
                    PhoneNo = reader["PhoneNo"]?.ToString()?.Trim() ?? string.Empty,
                    City = reader["City"]?.ToString()?.Trim() ?? string.Empty,
                    OpAmt1 = reader["OpAmt1"] != DBNull.Value ? Convert.ToDecimal(reader["OpAmt1"]) : 0m,
                    OpAmt2 = reader["OpAmt2"] != DBNull.Value ? Convert.ToDecimal(reader["OpAmt2"]) : 0m,
                    EntryDate = reader["EntryDate"] != DBNull.Value ? Convert.ToDateTime(reader["EntryDate"]) : new DateTime(2025, 4, 1),
                    InOut = reader["InOut"]?.ToString()?.Trim() ?? string.Empty
                };
                accounts.Add(acc);
            }
        }

        var accountMap = accounts.GroupBy(a => a.AccountName, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        // Account Activity lookup: key = account name (case-insensitive) -> list of transactions
        var validAccounts = new HashSet<string>(accounts.Select(a => a.AccountName), StringComparer.OrdinalIgnoreCase);
        validAccounts.Add("GST");

        var activityMap = new Dictionary<string, List<TransactionItem>>(StringComparer.OrdinalIgnoreCase);

        void AddTx(string accountName, DateTime date, decimal debit, decimal credit)
        {
            if (string.IsNullOrWhiteSpace(accountName)) return;
            var key = accountName.Trim();
            if (!validAccounts.Contains(key)) return; // DO NOT post to ledgers that don't exist in AccountCreation

            if (!activityMap.TryGetValue(key, out var list))
            {
                list = new List<TransactionItem>();
                activityMap[key] = list;
            }
            list.Add(new TransactionItem { Date = date, Debit = debit, Credit = credit });
        }

        // Load SalesVouType mapping: VouType -> (SalesAccount, HeadCategory)
        var salesVouTypeMap = new Dictionary<string, (string SalesAccount, string HeadCategory)>(StringComparer.OrdinalIgnoreCase);
        try
        {
            const string svtSql = "SELECT [VouType], [SalesAccount], [HeadCategory] FROM [SalesVouType]";
            await using var svtCmd = new OleDbCommand(svtSql, connection);
            await using var svtReader = await svtCmd.ExecuteReaderAsync(cancellationToken);
            while (await svtReader.ReadAsync(cancellationToken))
            {
                var vt = svtReader["VouType"]?.ToString()?.Trim() ?? string.Empty;
                var sa = svtReader["SalesAccount"]?.ToString()?.Trim() ?? string.Empty;
                var hc = svtReader["HeadCategory"]?.ToString()?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(vt))
                {
                    salesVouTypeMap[vt] = (sa, hc);
                }
            }
        }
        catch { }

        // 2. Fetch Bulk Transactions from Bill table
        try
        {
            const string billSql = @"
                SELECT [VouType], [BillDate], [CustomerName], [AccountName], [NetAmt], [TaxAmt]
                FROM [Bill]
                WHERE ([BillCanceled] = 0 OR [BillCanceled] IS NULL)
                  AND ([Status] IS NULL OR [Status] <> 'Deleted')
                  AND [NetAmt] > 0";

            await using var cmd = new OleDbCommand(billSql, connection);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var custName = reader["CustomerName"]?.ToString()?.Trim() ?? string.Empty;
                var accName = reader["AccountName"]?.ToString()?.Trim() ?? string.Empty;
                var vouType = reader["VouType"]?.ToString()?.Trim() ?? string.Empty;
                var netAmt = reader["NetAmt"] != DBNull.Value ? Convert.ToDecimal(reader["NetAmt"]) : 0m;
                var taxAmt = reader["TaxAmt"] != DBNull.Value ? Convert.ToDecimal(reader["TaxAmt"]) : 0m;
                var billDate = reader["BillDate"] != DBNull.Value ? Convert.ToDateTime(reader["BillDate"]) : DateTime.Today;

                var party = !string.IsNullOrEmpty(custName) ? custName : accName;
                decimal itemAmt = (netAmt > taxAmt) ? (netAmt - taxAmt) : netAmt;

                string salesAcc = "Sales";
                string headCat = "Sales";
                if (salesVouTypeMap.TryGetValue(vouType, out var svt))
                {
                    salesAcc = !string.IsNullOrWhiteSpace(svt.SalesAccount) ? svt.SalesAccount : vouType;
                    headCat = !string.IsNullOrWhiteSpace(svt.HeadCategory) ? svt.HeadCategory : string.Empty;
                }

                bool isSalesReturn = headCat.Equals("Sales Return", StringComparison.OrdinalIgnoreCase) || (vouType.Contains("Return", StringComparison.OrdinalIgnoreCase) && vouType.Contains("Sales", StringComparison.OrdinalIgnoreCase));
                bool isPurchase = headCat.Equals("Purchases", StringComparison.OrdinalIgnoreCase) || (vouType.Contains("Purchase", StringComparison.OrdinalIgnoreCase) && !vouType.Contains("Return", StringComparison.OrdinalIgnoreCase));
                bool isPurchaseReturn = headCat.Equals("Purchase Return", StringComparison.OrdinalIgnoreCase) || (vouType.Contains("Purchase", StringComparison.OrdinalIgnoreCase) && vouType.Contains("Return", StringComparison.OrdinalIgnoreCase));

                if (isSalesReturn)
                {
                    AddTx(salesAcc, billDate, itemAmt, 0m);
                    if (taxAmt > 0) AddTx("GST", billDate, taxAmt, 0m);
                }
                else if (isPurchase)
                {
                    AddTx(salesAcc, billDate, itemAmt, 0m);
                    if (taxAmt > 0) AddTx("GST", billDate, taxAmt, 0m);
                }
                else if (isPurchaseReturn)
                {
                    AddTx(salesAcc, billDate, 0m, itemAmt);
                    if (taxAmt > 0) AddTx("GST", billDate, 0m, taxAmt);
                }
                else
                {
                    // Standard Sales Invoice
                    AddTx(salesAcc, billDate, 0m, itemAmt);
                    if (taxAmt > 0) AddTx("GST", billDate, 0m, taxAmt);
                }
            }
        }
        catch { }

        // 3. Fetch Bulk Transactions from PaymentSubform linked with Bill
        try
        {
            const string paySql = @"
                SELECT p.[AccountName], p.[BankName], p.[HeadCategory], p.[Amount], b.[BillDate], b.[VouType], b.[CustomerName]
                FROM [PaymentSubform] p
                INNER JOIN [Bill] b ON p.[VouNo] = b.[VouNo]
                WHERE (b.[BillCanceled] = 0 OR b.[BillCanceled] IS NULL)
                  AND (b.[Status] IS NULL OR b.[Status] <> 'Deleted')
                  AND p.[Amount] <> 0";

            await using var cmd = new OleDbCommand(paySql, connection);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var payAcc = reader["AccountName"]?.ToString()?.Trim() ?? string.Empty;
                var bankName = reader["BankName"]?.ToString()?.Trim() ?? string.Empty;
                var headCategory = reader["HeadCategory"]?.ToString()?.Trim() ?? string.Empty;
                var rawAmount = reader["Amount"] != DBNull.Value ? Convert.ToDecimal(reader["Amount"]) : 0m;
                var billDate = reader["BillDate"] != DBNull.Value ? Convert.ToDateTime(reader["BillDate"]) : DateTime.Today;

                if (rawAmount == 0m) continue;
                decimal absAmount = Math.Abs(rawAmount);

                if ((string.Equals(headCategory, "Credit Card", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(payAcc, "Credit Card", StringComparison.OrdinalIgnoreCase)) &&
                    !string.IsNullOrWhiteSpace(bankName))
                {
                    payAcc = bankName;
                }

                if (rawAmount > 0)
                {
                    AddTx(payAcc, billDate, absAmount, 0m);
                }
                else
                {
                    AddTx(payAcc, billDate, 0m, absAmount);
                }
            }
        }
        catch { }

        // 3b. Fetch Bulk Transactions from Purchases table
        try
        {
            const string purSql = @"
                SELECT [VouNo], [VouType], [BillDate], [AccountName], [CustomerName], [TaxableAmount], [TaxAmt], [CGSTAmt], [SGSTAmt], [IGSTAmt], [NetAmt], [GrossAmt], [DiscountAllowed], [InOut]
                FROM [Purchases]
                WHERE ([BillCanceled] = 0 OR [BillCanceled] IS NULL)
                  AND ([Status] IS NULL OR [Status] <> 'Deleted')
                  AND [NetAmt] > 0";

            await using var purCmd = new OleDbCommand(purSql, connection);
            await using var purReader = await purCmd.ExecuteReaderAsync(cancellationToken);
            while (await purReader.ReadAsync(cancellationToken))
            {
                var vouType = purReader["VouType"]?.ToString()?.Trim() ?? "Purchase";
                var partyAcc = purReader["AccountName"]?.ToString()?.Trim() ?? purReader["CustomerName"]?.ToString()?.Trim() ?? string.Empty;
                var netAmt = purReader["NetAmt"] != DBNull.Value ? Convert.ToDecimal(purReader["NetAmt"]) : 0m;
                var taxableAmt = purReader["TaxableAmount"] != DBNull.Value ? Convert.ToDecimal(purReader["TaxableAmount"]) : 0m;
                var taxAmt = purReader["TaxAmt"] != DBNull.Value ? Convert.ToDecimal(purReader["TaxAmt"]) : 0m;
                var cgstAmt = purReader["CGSTAmt"] != DBNull.Value ? Convert.ToDecimal(purReader["CGSTAmt"]) : 0m;
                var sgstAmt = purReader["SGSTAmt"] != DBNull.Value ? Convert.ToDecimal(purReader["SGSTAmt"]) : 0m;
                var igstAmt = purReader["IGSTAmt"] != DBNull.Value ? Convert.ToDecimal(purReader["IGSTAmt"]) : 0m;
                var grossAmt = purReader["GrossAmt"] != DBNull.Value ? Convert.ToDecimal(purReader["GrossAmt"]) : 0m;
                var disAmt = purReader["DiscountAllowed"] != DBNull.Value ? Convert.ToDecimal(purReader["DiscountAllowed"]) : 0m;
                var billDate = purReader["BillDate"] != DBNull.Value ? Convert.ToDateTime(purReader["BillDate"]) : DateTime.Today;
                var inOut = purReader["InOut"]?.ToString()?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(inOut) && !string.IsNullOrEmpty(partyAcc) && accountMap.TryGetValue(partyAcc, out var accObj) && !string.IsNullOrEmpty(accObj.InOut))
                {
                    inOut = accObj.InOut;
                }

                decimal purchaseAmt = taxableAmt > 0 ? taxableAmt : ((grossAmt - disAmt > 0) ? (grossAmt - disAmt) : ((netAmt > taxAmt) ? (netAmt - taxAmt) : netAmt));
                bool isOutSide = inOut.Contains("OUT", StringComparison.OrdinalIgnoreCase);
                bool isPurchaseReturn = vouType.Contains("Return", StringComparison.OrdinalIgnoreCase);

                string purAcc = "Purchases";
                if (salesVouTypeMap.TryGetValue(vouType, out var svt) && !string.IsNullOrWhiteSpace(svt.SalesAccount))
                {
                    purAcc = svt.SalesAccount;
                }

                if (isPurchaseReturn)
                {
                    AddTx(partyAcc, billDate, netAmt, 0m);
                    AddTx(purAcc, billDate, 0m, purchaseAmt);
                    if (taxAmt > 0)
                    {
                        if (isOutSide)
                        {
                            AddTx("IGST", billDate, 0m, igstAmt > 0 ? igstAmt : taxAmt);
                        }
                        else
                        {
                            AddTx("CGST", billDate, 0m, cgstAmt > 0 ? cgstAmt : (taxAmt / 2m));
                            AddTx("SGST", billDate, 0m, sgstAmt > 0 ? sgstAmt : (taxAmt / 2m));
                        }
                    }
                }
                else
                {
                    AddTx(partyAcc, billDate, 0m, netAmt);
                    AddTx(purAcc, billDate, purchaseAmt, 0m);
                    if (taxAmt > 0)
                    {
                        if (isOutSide)
                        {
                            AddTx("IGST", billDate, igstAmt > 0 ? igstAmt : taxAmt, 0m);
                        }
                        else
                        {
                            AddTx("CGST", billDate, cgstAmt > 0 ? cgstAmt : (taxAmt / 2m), 0m);
                            AddTx("SGST", billDate, sgstAmt > 0 ? sgstAmt : (taxAmt / 2m), 0m);
                        }
                    }
                }
            }
        }
        catch { }

        // 3c. Fetch Bulk Transactions from PaymentSubform linked with Purchases
        try
        {
            const string purPaySql = @"
                SELECT p.[AccountName], p.[BankName], p.[HeadCategory], p.[Amount], pur.[BillDate], pur.[AccountName] AS [PartyName], pur.[CustomerName]
                FROM [PaymentSubform] p
                INNER JOIN [Purchases] pur ON p.[VouNo] = pur.[VouNo]
                WHERE (pur.[BillCanceled] = 0 OR pur.[BillCanceled] IS NULL)
                  AND (pur.[Status] IS NULL OR pur.[Status] <> 'Deleted')
                  AND p.[Amount] <> 0";

            await using var purPayCmd = new OleDbCommand(purPaySql, connection);
            await using var purPayReader = await purPayCmd.ExecuteReaderAsync(cancellationToken);
            while (await purPayReader.ReadAsync(cancellationToken))
            {
                var payAcc = purPayReader["AccountName"]?.ToString()?.Trim() ?? string.Empty;
                var bankName = purPayReader["BankName"]?.ToString()?.Trim() ?? string.Empty;
                var headCategory = purPayReader["HeadCategory"]?.ToString()?.Trim() ?? string.Empty;
                var partyName = purPayReader["PartyName"]?.ToString()?.Trim() ?? purPayReader["CustomerName"]?.ToString()?.Trim() ?? string.Empty;
                var rawAmount = purPayReader["Amount"] != DBNull.Value ? Convert.ToDecimal(purPayReader["Amount"]) : 0m;
                var billDate = purPayReader["BillDate"] != DBNull.Value ? Convert.ToDateTime(purPayReader["BillDate"]) : DateTime.Today;

                if (rawAmount == 0m) continue;
                decimal absAmount = Math.Abs(rawAmount);

                if ((string.Equals(headCategory, "Credit Card", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(payAcc, "Credit Card", StringComparison.OrdinalIgnoreCase)) &&
                    !string.IsNullOrWhiteSpace(bankName))
                {
                    payAcc = bankName;
                }

                AddTx(partyName, billDate, absAmount, 0m);
                AddTx(payAcc, billDate, 0m, absAmount);
            }
        }
        catch { }

        // 4. Fetch Bulk Transactions from PurchaseE (Purchases & Expenses)
        try
        {
            const string peSql = @"
                SELECT [AccountName], [Head], [NetAmt], [BillDate]
                FROM [PurchaseE]
                WHERE ([Status] IS NULL OR [Status] <> 'Deleted')
                  AND [NetAmt] > 0";

            await using var cmd = new OleDbCommand(peSql, connection);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var partyAcc = reader["AccountName"]?.ToString()?.Trim() ?? string.Empty;
                var headAcc = reader["Head"]?.ToString()?.Trim() ?? string.Empty;
                var netAmt = reader["NetAmt"] != DBNull.Value ? Convert.ToDecimal(reader["NetAmt"]) : 0m;
                var billDate = reader["BillDate"] != DBNull.Value ? Convert.ToDateTime(reader["BillDate"]) : DateTime.Today;

                AddTx(partyAcc, billDate, 0m, netAmt);
                AddTx(headAcc, billDate, netAmt, 0m);
            }
        }
        catch { }

        // 5. Fetch Bulk Transactions from RECIEPTS (Receipts & Journals)
        try
        {
            const string recSql = @"
                SELECT [DrAccount], [DrAmt], [CrAccount], [CrAmt], [VouDate]
                FROM [RECIEPTS]
                WHERE ([Status] IS NULL OR [Status] <> 'Deleted')";

            await using var cmd = new OleDbCommand(recSql, connection);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var drAcc = reader["DrAccount"]?.ToString()?.Trim() ?? string.Empty;
                var crAcc = reader["CrAccount"]?.ToString()?.Trim() ?? string.Empty;
                var drAmt = reader["DrAmt"] != DBNull.Value ? Convert.ToDecimal(reader["DrAmt"]) : 0m;
                var crAmt = reader["CrAmt"] != DBNull.Value ? Convert.ToDecimal(reader["CrAmt"]) : 0m;
                var vouDate = reader["VouDate"] != DBNull.Value ? Convert.ToDateTime(reader["VouDate"]) : DateTime.Today;

                var amt = drAmt > 0 ? drAmt : crAmt;
                if (amt > 0)
                {
                    AddTx(drAcc, vouDate, amt, 0m);
                    AddTx(crAcc, vouDate, 0m, amt);
                }
            }
        }
        catch { }

        // If GST had transactions and is not in accounts, include GST in accounts
        if (activityMap.ContainsKey("GST") && !accounts.Any(a => string.Equals(a.AccountName, "GST", StringComparison.OrdinalIgnoreCase)))
        {
            accounts.Add(new AccountRaw
            {
                PartyCode = "-",
                AccountName = "GST",
                HeadCategory = "Duties and Taxes",
                SubCategory = "GST",
                OpAmt1 = 0m,
                OpAmt2 = 0m,
                EntryDate = new DateTime(2025, 4, 1)
            });
        }

        // 6. Build Trial Balance items for each account
        var resultList = new List<TrialBalanceItem>();

        foreach (var acc in accounts)
        {
            // A. Initial Opening from AccountCreation
            // Rule: If OpAmt1 is negative then Cr side, else Dr side
            decimal initialOpAmount = 0m;
            if (acc.OpAmt1 < 0)
            {
                initialOpAmount = acc.OpAmt1; // Credit side
            }
            else if (acc.OpAmt1 > 0)
            {
                initialOpAmount = acc.OpAmt1; // Debit side
            }
            else if (acc.OpAmt2 > 0)
            {
                initialOpAmount = -acc.OpAmt2;
            }

            // Baseline cut-off date: 01/04/2025
            DateTime baseDate = new DateTime(2025, 4, 1);
            if (acc.EntryDate.Year >= 2000)
            {
                var accFy = acc.EntryDate.Month >= 4 ? acc.EntryDate.Year : acc.EntryDate.Year - 1;
                baseDate = new DateTime(Math.Min(2025, accFy), 4, 1);
            }

            decimal preDebits = 0m;
            decimal preCredits = 0m;
            decimal periodDebits = 0m;
            decimal periodCredits = 0m;

            if (activityMap.TryGetValue(acc.AccountName, out var txList))
            {
                foreach (var tx in txList)
                {
                    if (tx.Date.Date >= baseDate.Date && tx.Date.Date < fromDate.Date)
                    {
                        preDebits += tx.Debit;
                        preCredits += tx.Credit;
                    }
                    else if (tx.Date.Date >= fromDate.Date && tx.Date.Date <= toDate.Date)
                    {
                        periodDebits += tx.Debit;
                        periodCredits += tx.Credit;
                    }
                }
            }

            // Opening Balance as of fromDate
            var openingBalanceValue = initialOpAmount + preDebits - preCredits;
            var openingDr = openingBalanceValue > 0 ? openingBalanceValue : 0m;
            var openingCr = openingBalanceValue < 0 ? Math.Abs(openingBalanceValue) : 0m;

            // Closing Balance as of toDate
            var closingBalanceValue = openingBalanceValue + periodDebits - periodCredits;
            var closingDr = closingBalanceValue > 0 ? closingBalanceValue : 0m;
            var closingCr = closingBalanceValue < 0 ? Math.Abs(closingBalanceValue) : 0m;

            var item = new TrialBalanceItem
            {
                AccountName = acc.AccountName,
                HeadCategory = acc.HeadCategory,
                SubCategory = acc.SubCategory,
                PhoneNo = acc.PhoneNo,
                City = acc.City,
                OpeningDebit = openingDr,
                OpeningCredit = openingCr,
                PeriodDebit = periodDebits,
                PeriodCredit = periodCredits,
                ClosingDebit = closingDr,
                ClosingCredit = closingCr
            };

            // Filter conditions
            if (filter.SelectedHeadCategory != "ALL" && !string.Equals(item.HeadCategory, filter.SelectedHeadCategory, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!string.IsNullOrWhiteSpace(filter.SearchText) &&
                !item.AccountName.Contains(filter.SearchText, StringComparison.OrdinalIgnoreCase) &&
                !item.HeadCategory.Contains(filter.SearchText, StringComparison.OrdinalIgnoreCase) &&
                !item.PhoneNo.Contains(filter.SearchText, StringComparison.OrdinalIgnoreCase))
                continue;

            if (filter.HideZeroBalances && !item.HasActivityOrBalance)
                continue;

            resultList.Add(item);
        }

        return resultList;
    }

    public async Task<IReadOnlyList<VoucherAuditItem>> AuditVouchersAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        var auditList = new List<VoucherAuditItem>();
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // Load valid accounts from AccountCreation
        var validAccounts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            const string accSql = "SELECT [AccountName] FROM [AccountCreation] WHERE [AccountName] IS NOT NULL AND [AccountName] <> ''";
            await using var accCmd = new OleDbCommand(accSql, connection);
            await using var accReader = await accCmd.ExecuteReaderAsync(cancellationToken);
            while (await accReader.ReadAsync(cancellationToken))
            {
                var name = accReader["AccountName"]?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(name)) validAccounts.Add(name);
            }
        }
        catch { }
        validAccounts.Add("GST");

        // 1. Load SalesVouType mapping
        var salesVouTypeMap = new Dictionary<string, (string SalesAccount, string HeadCategory)>(StringComparer.OrdinalIgnoreCase);
        try
        {
            const string svtSql = "SELECT [VouType], [SalesAccount], [HeadCategory] FROM [SalesVouType]";
            await using var svtCmd = new OleDbCommand(svtSql, connection);
            await using var svtReader = await svtCmd.ExecuteReaderAsync(cancellationToken);
            while (await svtReader.ReadAsync(cancellationToken))
            {
                var vt = svtReader["VouType"]?.ToString()?.Trim() ?? string.Empty;
                var sa = svtReader["SalesAccount"]?.ToString()?.Trim() ?? string.Empty;
                var hc = svtReader["HeadCategory"]?.ToString()?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(vt))
                {
                    salesVouTypeMap[vt] = (sa, hc);
                }
            }
        }
        catch { }

        // 2. Load PaymentSubform entries grouped by VouNo
        var pmtMap = new Dictionary<string, List<(string PayAcc, decimal Amount)>>(StringComparer.OrdinalIgnoreCase);
        try
        {
            const string paySql = @"
                SELECT p.[VouNo], p.[AccountName], p.[BankName], p.[HeadCategory], p.[Amount]
                FROM [PaymentSubform] p
                WHERE p.[Amount] <> 0";

            await using var payCmd = new OleDbCommand(paySql, connection);
            await using var payReader = await payCmd.ExecuteReaderAsync(cancellationToken);
            while (await payReader.ReadAsync(cancellationToken))
            {
                var vouNo = payReader["VouNo"]?.ToString()?.Trim() ?? string.Empty;
                var pAcc = payReader["AccountName"]?.ToString()?.Trim() ?? string.Empty;
                var bName = payReader["BankName"]?.ToString()?.Trim() ?? string.Empty;
                var hCat = payReader["HeadCategory"]?.ToString()?.Trim() ?? string.Empty;
                var amt = payReader["Amount"] != DBNull.Value ? Convert.ToDecimal(payReader["Amount"]) : 0m;

                if (amt == 0m || string.IsNullOrEmpty(vouNo)) continue;

                var targetAcc = pAcc;
                if ((string.Equals(hCat, "Credit Card", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(pAcc, "Credit Card", StringComparison.OrdinalIgnoreCase)) &&
                    !string.IsNullOrWhiteSpace(bName))
                {
                    targetAcc = bName;
                }

                if (!pmtMap.TryGetValue(vouNo, out var list))
                {
                    list = new List<(string, decimal)>();
                    pmtMap[vouNo] = list;
                }
                list.Add((targetAcc, amt));
            }
        }
        catch { }

        // 3. Audit Bill Vouchers (Sales, Purchases, Returns)
        try
        {
            const string billSql = @"
                SELECT [VouNo], [VouType], [BillDate], [CustomerName], [AccountName], [NetAmt], [TaxAmt]
                FROM [Bill]
                WHERE ([BillCanceled] = 0 OR [BillCanceled] IS NULL)
                  AND ([Status] IS NULL OR [Status] <> 'Deleted')
                  AND [NetAmt] > 0
                  AND [BillDate] >= @FromDate AND [BillDate] <= @ToDate
                ORDER BY [BillDate], [VouNo]";

            await using var cmd = new OleDbCommand(billSql, connection);
            cmd.Parameters.Add("@FromDate", OleDbType.Date).Value = fromDate.Date;
            cmd.Parameters.Add("@ToDate", OleDbType.Date).Value = toDate.Date;

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var vouNo = reader["VouNo"]?.ToString()?.Trim() ?? string.Empty;
                var vouType = reader["VouType"]?.ToString()?.Trim() ?? "Invoice";
                var billDate = reader["BillDate"] != DBNull.Value ? Convert.ToDateTime(reader["BillDate"]) : DateTime.Today;
                var custName = reader["CustomerName"]?.ToString()?.Trim() ?? string.Empty;
                var accName = reader["AccountName"]?.ToString()?.Trim() ?? string.Empty;
                var netAmt = reader["NetAmt"] != DBNull.Value ? Convert.ToDecimal(reader["NetAmt"]) : 0m;
                var taxAmt = reader["TaxAmt"] != DBNull.Value ? Convert.ToDecimal(reader["TaxAmt"]) : 0m;

                var party = !string.IsNullOrEmpty(custName) ? custName : accName;
                decimal itemAmt = (netAmt > taxAmt) ? (netAmt - taxAmt) : netAmt;

                string salesAcc = "Sales";
                string headCat = "Sales";
                if (salesVouTypeMap.TryGetValue(vouType, out var svt))
                {
                    salesAcc = !string.IsNullOrWhiteSpace(svt.SalesAccount) ? svt.SalesAccount : vouType;
                    headCat = !string.IsNullOrWhiteSpace(svt.HeadCategory) ? svt.HeadCategory : string.Empty;
                }

                bool isPurchase = headCat.Equals("Purchases", StringComparison.OrdinalIgnoreCase) || (vouType.Contains("Purchase", StringComparison.OrdinalIgnoreCase) && !vouType.Contains("Return", StringComparison.OrdinalIgnoreCase));
                bool isSalesReturn = headCat.Equals("Sales Return", StringComparison.OrdinalIgnoreCase) || (vouType.Contains("Return", StringComparison.OrdinalIgnoreCase) && vouType.Contains("Sales", StringComparison.OrdinalIgnoreCase));
                bool isPurchaseReturn = headCat.Equals("Purchase Return", StringComparison.OrdinalIgnoreCase) || (vouType.Contains("Purchase", StringComparison.OrdinalIgnoreCase) && vouType.Contains("Return", StringComparison.OrdinalIgnoreCase));

                pmtMap.TryGetValue(vouNo, out var payments);
                payments ??= new List<(string PayAcc, decimal Amount)>();

                decimal totalDr = 0m;
                decimal totalCr = 0m;
                var drParts = new List<string>();
                var crParts = new List<string>();
                var missingNotes = new List<string>();

                bool salesAccExists = validAccounts.Contains(salesAcc);

                if (isPurchase)
                {
                    // Purchase Voucher
                    if (salesAccExists)
                    {
                        totalDr += itemAmt;
                        drParts.Add($"{salesAcc} (₹{itemAmt:N2})");
                    }
                    else
                    {
                        drParts.Add($"[Missing Ledger: {salesAcc}] (₹{itemAmt:N2})");
                        missingNotes.Add($"Purchase ledger '{salesAcc}' not found in AccountCreation");
                    }

                    if (taxAmt > 0)
                    {
                        totalDr += taxAmt;
                        drParts.Add($"GST (₹{taxAmt:N2})");
                    }

                    foreach (var p in payments)
                    {
                        var absVal = Math.Abs(p.Amount);
                        if (validAccounts.Contains(p.PayAcc))
                        {
                            totalCr += absVal;
                            crParts.Add($"{p.PayAcc} (₹{absVal:N2})");
                        }
                        else
                        {
                            crParts.Add($"[Missing Ledger: {p.PayAcc}] (₹{absVal:N2})");
                            missingNotes.Add($"Payment ledger '{p.PayAcc}' not found in AccountCreation");
                        }
                    }
                }
                else if (isPurchaseReturn)
                {
                    // Purchase Return
                    if (salesAccExists)
                    {
                        totalCr += itemAmt;
                        crParts.Add($"{salesAcc} (₹{itemAmt:N2})");
                    }
                    else
                    {
                        crParts.Add($"[Missing Ledger: {salesAcc}] (₹{itemAmt:N2})");
                        missingNotes.Add($"Purchase return ledger '{salesAcc}' not found in AccountCreation");
                    }

                    if (taxAmt > 0)
                    {
                        totalCr += taxAmt;
                        crParts.Add($"GST (₹{taxAmt:N2})");
                    }

                    foreach (var p in payments)
                    {
                        var absVal = Math.Abs(p.Amount);
                        if (validAccounts.Contains(p.PayAcc))
                        {
                            totalDr += absVal;
                            drParts.Add($"{p.PayAcc} (₹{absVal:N2})");
                        }
                        else
                        {
                            drParts.Add($"[Missing Ledger: {p.PayAcc}] (₹{absVal:N2})");
                            missingNotes.Add($"Payment ledger '{p.PayAcc}' not found in AccountCreation");
                        }
                    }
                }
                else if (isSalesReturn)
                {
                    // Sales Return
                    if (salesAccExists)
                    {
                        totalDr += itemAmt;
                        drParts.Add($"{salesAcc} (₹{itemAmt:N2})");
                    }
                    else
                    {
                        drParts.Add($"[Missing Ledger: {salesAcc}] (₹{itemAmt:N2})");
                        missingNotes.Add($"Sales return ledger '{salesAcc}' not found in AccountCreation");
                    }

                    if (taxAmt > 0)
                    {
                        totalDr += taxAmt;
                        drParts.Add($"GST (₹{taxAmt:N2})");
                    }

                    foreach (var p in payments)
                    {
                        var absVal = Math.Abs(p.Amount);
                        if (validAccounts.Contains(p.PayAcc))
                        {
                            totalCr += absVal;
                            crParts.Add($"{p.PayAcc} (₹{absVal:N2})");
                        }
                        else
                        {
                            crParts.Add($"[Missing Ledger: {p.PayAcc}] (₹{absVal:N2})");
                            missingNotes.Add($"Payment ledger '{p.PayAcc}' not found in AccountCreation");
                        }
                    }
                }
                else
                {
                    // Standard Sales Invoice
                    if (salesAccExists)
                    {
                        totalCr += itemAmt;
                        crParts.Add($"{salesAcc} (₹{itemAmt:N2})");
                    }
                    else
                    {
                        crParts.Add($"[Missing Ledger: {salesAcc}] (₹{itemAmt:N2})");
                        missingNotes.Add($"Sales ledger '{salesAcc}' not found in AccountCreation");
                    }

                    if (taxAmt > 0)
                    {
                        totalCr += taxAmt;
                        crParts.Add($"GST (₹{taxAmt:N2})");
                    }

                    foreach (var p in payments)
                    {
                        if (validAccounts.Contains(p.PayAcc))
                        {
                            totalDr += p.Amount;
                            drParts.Add($"{p.PayAcc} (₹{p.Amount:N2})");
                        }
                        else
                        {
                            drParts.Add($"[Missing Ledger: {p.PayAcc}] (₹{p.Amount:N2})");
                            missingNotes.Add($"Payment ledger '{p.PayAcc}' not found in AccountCreation");
                        }
                    }
                }

                string drStr = drParts.Count > 0 ? string.Join(", ", drParts) : "No debit posted";
                string crStr = crParts.Count > 0 ? string.Join(", ", crParts) : "No credit posted";
                string remarks = missingNotes.Count > 0
                    ? string.Join("; ", missingNotes)
                    : (Math.Abs(totalDr - totalCr) > 0.0001m
                        ? $"Payments (₹{totalDr:N2}) differ from Bill total (₹{totalCr:N2}) by ₹{Math.Abs(totalDr - totalCr):N2}"
                        : $"Customer: {party}");

                auditList.Add(new VoucherAuditItem
                {
                    VouNo = vouNo,
                    VouDate = billDate,
                    VouType = vouType,
                    SourceTable = "Bill / Payments",
                    DebitAccounts = drStr,
                    CreditAccounts = crStr,
                    TotalDebit = totalDr,
                    TotalCredit = totalCr,
                    Remarks = remarks
                });
            }
        }
        catch { }

        // 4. Audit PurchaseE (Purchases & Expenses)
        try
        {
            const string peSql = @"
                SELECT [VouNo], [VouType], [AccountName], [Head], [NetAmt], [BillDate]
                FROM [PurchaseE]
                WHERE ([Status] IS NULL OR [Status] <> 'Deleted')
                  AND [NetAmt] > 0
                  AND [BillDate] >= @FromDate AND [BillDate] <= @ToDate
                ORDER BY [BillDate], [VouNo]";

            await using var cmd = new OleDbCommand(peSql, connection);
            cmd.Parameters.Add("@FromDate", OleDbType.Date).Value = fromDate.Date;
            cmd.Parameters.Add("@ToDate", OleDbType.Date).Value = toDate.Date;

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var vouNo = reader["VouNo"]?.ToString()?.Trim() ?? string.Empty;
                var vouType = reader["VouType"]?.ToString()?.Trim() ?? "Purchase/Expense";
                var partyAcc = reader["AccountName"]?.ToString()?.Trim() ?? string.Empty;
                var head = reader["Head"]?.ToString()?.Trim() ?? string.Empty;
                var netAmt = reader["NetAmt"] != DBNull.Value ? Convert.ToDecimal(reader["NetAmt"]) : 0m;
                var billDate = reader["BillDate"] != DBNull.Value ? Convert.ToDateTime(reader["BillDate"]) : DateTime.Today;

                decimal dr = validAccounts.Contains(head) ? netAmt : 0m;
                decimal cr = validAccounts.Contains(partyAcc) ? netAmt : 0m;

                var missing = new List<string>();
                if (!validAccounts.Contains(head)) missing.Add($"Expense head '{head}' missing in AccountCreation");
                if (!validAccounts.Contains(partyAcc)) missing.Add($"Party account '{partyAcc}' missing in AccountCreation");

                auditList.Add(new VoucherAuditItem
                {
                    VouNo = vouNo,
                    VouDate = billDate,
                    VouType = vouType,
                    SourceTable = "PurchaseE",
                    DebitAccounts = validAccounts.Contains(head) ? head : $"[Missing: {head}]",
                    CreditAccounts = validAccounts.Contains(partyAcc) ? partyAcc : $"[Missing: {partyAcc}]",
                    TotalDebit = dr,
                    TotalCredit = cr,
                    Remarks = missing.Count > 0 ? string.Join("; ", missing) : "Expense / Purchase Posting"
                });
            }
        }
        catch { }

        // 5. Audit RECIEPTS (Receipts & Journals)
        try
        {
            const string recSql = @"
                SELECT [VouNo], [VouType], [DrAccount], [DrAmt], [CrAccount], [CrAmt], [VouDate]
                FROM [RECIEPTS]
                WHERE ([Status] IS NULL OR [Status] <> 'Deleted')
                  AND [VouDate] >= @FromDate AND [VouDate] <= @ToDate
                ORDER BY [VouDate], [VouNo]";

            await using var cmd = new OleDbCommand(recSql, connection);
            cmd.Parameters.Add("@FromDate", OleDbType.Date).Value = fromDate.Date;
            cmd.Parameters.Add("@ToDate", OleDbType.Date).Value = toDate.Date;

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var vouNo = reader["VouNo"]?.ToString()?.Trim() ?? string.Empty;
                var vouType = reader["VouType"]?.ToString()?.Trim() ?? "Receipt/Journal";
                var drAcc = reader["DrAccount"]?.ToString()?.Trim() ?? string.Empty;
                var crAcc = reader["CrAccount"]?.ToString()?.Trim() ?? string.Empty;
                var drAmt = reader["DrAmt"] != DBNull.Value ? Convert.ToDecimal(reader["DrAmt"]) : 0m;
                var crAmt = reader["CrAmt"] != DBNull.Value ? Convert.ToDecimal(reader["CrAmt"]) : 0m;
                var vouDate = reader["VouDate"] != DBNull.Value ? Convert.ToDateTime(reader["VouDate"]) : DateTime.Today;

                decimal dr = validAccounts.Contains(drAcc) ? drAmt : 0m;
                decimal cr = validAccounts.Contains(crAcc) ? crAmt : 0m;

                var missing = new List<string>();
                if (!string.IsNullOrEmpty(drAcc) && !validAccounts.Contains(drAcc)) missing.Add($"DrAccount '{drAcc}' missing in AccountCreation");
                if (!string.IsNullOrEmpty(crAcc) && !validAccounts.Contains(crAcc)) missing.Add($"CrAccount '{crAcc}' missing in AccountCreation");
                if (Math.Abs(drAmt - crAmt) > 0.0001m) missing.Add("Discrepancy in Dr vs Cr amount");

                auditList.Add(new VoucherAuditItem
                {
                    VouNo = vouNo,
                    VouDate = vouDate,
                    VouType = vouType,
                    SourceTable = "RECIEPTS",
                    DebitAccounts = validAccounts.Contains(drAcc) ? drAcc : $"[Missing: {drAcc}]",
                    CreditAccounts = validAccounts.Contains(crAcc) ? crAcc : $"[Missing: {crAcc}]",
                    TotalDebit = dr,
                    TotalCredit = cr,
                    Remarks = missing.Count > 0 ? string.Join("; ", missing) : "Journal/Receipt Posting"
                });
            }
        }
        catch { }

        return auditList.OrderBy(x => x.IsBalanced).ThenByDescending(x => Math.Abs(x.Difference)).ThenBy(x => x.VouDate).ToList();
    }

    private sealed class AccountRaw
    {
        public string PartyCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string HeadCategory { get; set; } = string.Empty;
        public string SubCategory { get; set; } = string.Empty;
        public string PhoneNo { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public decimal OpAmt1 { get; set; }
        public decimal OpAmt2 { get; set; }
        public DateTime EntryDate { get; set; }
        public string InOut { get; set; } = string.Empty;
    }

    private sealed class TransactionItem
    {
        public DateTime Date { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
    }
}
