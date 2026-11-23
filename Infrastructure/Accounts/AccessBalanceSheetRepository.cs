using System.Data;
using System.Data.OleDb;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class AccessBalanceSheetRepository : IBalanceSheetRepository
{
    private readonly string _connectionString;

    public AccessBalanceSheetRepository(string databasePath)
    {
        _connectionString = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={databasePath};";
    }

    public async Task<decimal> GetEstimatedClosingStockValueAsync(DateTime asOnDate, CancellationToken cancellationToken = default)
    {
        // Calculate estimated closing stock from ItemMaster & NonBarcode / Barcode stock
        decimal estimatedValue = 0m;
        try
        {
            await using var connection = new OleDbConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            // Attempt to read from clstk1 or item stock value
            try
            {
                const string sql = "SELECT SUM([Total]) FROM [clstk1]";
                await using var cmd = new OleDbCommand(sql, connection);
                var result = await cmd.ExecuteScalarAsync(cancellationToken);
                if (result != null && result != DBNull.Value)
                {
                    estimatedValue = Convert.ToDecimal(result);
                }
            }
            catch { }

            if (estimatedValue == 0m)
            {
                // Fallback: estimate from NonBarcode stock value
                try
                {
                    const string sql = "SELECT SUM([Gwt] * [Rate]) FROM [ditrn1] WHERE [Type] = 'Stock' OR [Type] = 'Opening'";
                    await using var cmd = new OleDbCommand(sql, connection);
                    var result = await cmd.ExecuteScalarAsync(cancellationToken);
                    if (result != null && result != DBNull.Value)
                    {
                        estimatedValue = Convert.ToDecimal(result);
                    }
                }
                catch { }
            }
        }
        catch { }

        return estimatedValue > 0 ? estimatedValue : 0m;
    }

    public async Task<BalanceSheetStatement> GetBalanceSheetAsync(BalanceSheetFilter filter, CancellationToken cancellationToken = default)
    {
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var fromDate = filter.FromDate ?? new DateTime(DateTime.Today.Month < 4 ? DateTime.Today.Year - 1 : DateTime.Today.Year, 4, 1);
        var toDate = filter.ToDate ?? DateTime.Today;

        // 1. Fetch Accounts from AccountCreation
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
                accounts.Add(new AccountRaw
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
                });
            }
        }

        var accountMap = accounts.GroupBy(a => a.AccountName, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var validAccounts = new HashSet<string>(accounts.Select(a => a.AccountName), StringComparer.OrdinalIgnoreCase);
        validAccounts.Add("GST");

        var activityMap = new Dictionary<string, List<TransactionItem>>(StringComparer.OrdinalIgnoreCase);

        void AddTx(string accountName, DateTime date, decimal debit, decimal credit)
        {
            if (string.IsNullOrWhiteSpace(accountName)) return;
            var key = accountName.Trim();
            if (!validAccounts.Contains(key)) return;

            if (!activityMap.TryGetValue(key, out var list))
            {
                list = new List<TransactionItem>();
                activityMap[key] = list;
            }
            list.Add(new TransactionItem { Date = date, Debit = debit, Credit = credit });
        }

        // SalesVouType mapping
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
                    AddTx(salesAcc, billDate, 0m, itemAmt);
                    if (taxAmt > 0) AddTx("GST", billDate, 0m, taxAmt);
                }
            }
        }
        catch { }

        // 3. Fetch Transactions from PaymentSubform linked with Bill
        try
        {
            const string paySql = @"
                SELECT p.[AccountName], p.[BankName], p.[HeadCategory], p.[Amount], b.[BillDate], b.[VouType]
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

        // 4. Fetch Transactions from PurchaseE
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

        // 5. Fetch Transactions from RECIEPTS
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

        // 6. Calculate Net Balances for each Account as of toDate
        var accountBalances = new List<AccountCalculatedBalance>();

        foreach (var acc in accounts)
        {
            decimal initialOpAmount = 0m;
            if (acc.OpAmt1 < 0) initialOpAmount = acc.OpAmt1;
            else if (acc.OpAmt1 > 0) initialOpAmount = acc.OpAmt1;
            else if (acc.OpAmt2 > 0) initialOpAmount = -acc.OpAmt2;

            DateTime baseDate = new DateTime(2025, 4, 1);
            if (acc.EntryDate.Year >= 2000)
            {
                var accFy = acc.EntryDate.Month >= 4 ? acc.EntryDate.Year : acc.EntryDate.Year - 1;
                baseDate = new DateTime(Math.Min(2025, accFy), 4, 1);
            }

            decimal allDebits = 0m;
            decimal allCredits = 0m;

            if (activityMap.TryGetValue(acc.AccountName, out var txList))
            {
                foreach (var tx in txList)
                {
                    if (tx.Date.Date >= baseDate.Date && tx.Date.Date <= toDate.Date)
                    {
                        allDebits += tx.Debit;
                        allCredits += tx.Credit;
                    }
                }
            }

            decimal netBalance = initialOpAmount + allDebits - allCredits;

            accountBalances.Add(new AccountCalculatedBalance
            {
                AccountName = acc.AccountName,
                HeadCategory = acc.HeadCategory,
                SubCategory = acc.SubCategory,
                NetBalance = netBalance,
                IsDebit = netBalance > 0,
                IsCredit = netBalance < 0,
                AbsoluteAmount = Math.Abs(netBalance)
            });
        }

        // 7. Calculate P&L (Trading & Profit/Loss)
        var pAndL = new ProfitLossStatement();
        decimal closingStockVal = filter.ManualClosingStockAmount ?? await GetEstimatedClosingStockValueAsync(toDate, cancellationToken);
        pAndL.ClosingStockAmount = closingStockVal;

        foreach (var acc in accountBalances)
        {
            var head = acc.HeadCategory.Trim();

            if (IsSalesHead(head))
            {
                // Sales normally Credit (negative NetBalance)
                pAndL.SalesAmount += (acc.NetBalance < 0 ? Math.Abs(acc.NetBalance) : -acc.NetBalance);
            }
            else if (IsPurchaseHead(head))
            {
                // Purchases normally Debit (positive NetBalance)
                pAndL.PurchaseAmount += (acc.NetBalance > 0 ? acc.NetBalance : -Math.Abs(acc.NetBalance));
            }
            else if (IsDirectExpenseHead(head))
            {
                pAndL.DirectExpenses += (acc.NetBalance > 0 ? acc.NetBalance : -Math.Abs(acc.NetBalance));
            }
            else if (IsDirectIncomeHead(head))
            {
                pAndL.DirectIncomes += (acc.NetBalance < 0 ? Math.Abs(acc.NetBalance) : -acc.NetBalance);
            }
            else if (IsIndirectExpenseHead(head))
            {
                pAndL.IndirectExpenses += (acc.NetBalance > 0 ? acc.NetBalance : -Math.Abs(acc.NetBalance));
            }
            else if (IsIndirectIncomeHead(head))
            {
                pAndL.IndirectIncomes += (acc.NetBalance < 0 ? Math.Abs(acc.NetBalance) : -acc.NetBalance);
            }
        }

        // 8. Build Statement Sections
        var statement = new BalanceSheetStatement
        {
            FromDate = fromDate,
            AsOnDate = toDate,
            ProfitLoss = pAndL
        };

        var capitalSection = new BalanceSheetSection { SectionTitle = "Capital Account & Equity", SectionCategory = "Equity", DisplayOrder = 1 };
        var reservesSection = new BalanceSheetSection { SectionTitle = "Reserves & Surplus", SectionCategory = "Equity", DisplayOrder = 2 };
        var securedLoansSection = new BalanceSheetSection { SectionTitle = "Secured Loans (Bank/OD)", SectionCategory = "Liabilities", DisplayOrder = 3 };
        var unsecuredLoansSection = new BalanceSheetSection { SectionTitle = "Unsecured Loans", SectionCategory = "Liabilities", DisplayOrder = 4 };
        var tradePayablesSection = new BalanceSheetSection { SectionTitle = "Sundry Creditors (Trade Payables)", SectionCategory = "Current Liabilities", DisplayOrder = 5 };
        var customerAdvancesSection = new BalanceSheetSection { SectionTitle = "Customer Advances & Schemes", SectionCategory = "Current Liabilities", DisplayOrder = 6 };
        var dutiesTaxesSection = new BalanceSheetSection { SectionTitle = "Duties & Taxes (GST / Statutory)", SectionCategory = "Current Liabilities", DisplayOrder = 7 };
        var otherLiabilitiesSection = new BalanceSheetSection { SectionTitle = "Provisions & Other Liabilities", SectionCategory = "Current Liabilities", DisplayOrder = 8 };

        var fixedAssetsSection = new BalanceSheetSection { SectionTitle = "Fixed Assets", SectionCategory = "Assets", DisplayOrder = 1 };
        var investmentsSection = new BalanceSheetSection { SectionTitle = "Investments & Deposits", SectionCategory = "Assets", DisplayOrder = 2 };
        var inventorySection = new BalanceSheetSection { SectionTitle = "Stock-in-Hand (Inventories)", SectionCategory = "Current Assets", DisplayOrder = 3 };
        var tradeReceivablesSection = new BalanceSheetSection { SectionTitle = "Sundry Debtors (Receivables)", SectionCategory = "Current Assets", DisplayOrder = 4 };
        var cashBankSection = new BalanceSheetSection { SectionTitle = "Cash & Bank Balances", SectionCategory = "Current Assets", DisplayOrder = 5 };
        var loansAdvancesSection = new BalanceSheetSection { SectionTitle = "Loans, Advances & Other Assets", SectionCategory = "Current Assets", DisplayOrder = 6 };

        // Add Closing Stock Item
        if (pAndL.ClosingStockAmount > 0 || !filter.HideZeroBalances)
        {
            inventorySection.Items.Add(new BalanceSheetItem
            {
                AccountName = "Closing Stock-in-Hand",
                HeadCategory = "Inventories",
                ScheduleName = "Stock-in-Hand",
                GroupType = "Current Assets",
                Amount = pAndL.ClosingStockAmount,
                GoldWeight = filter.ManualClosingGoldWeight ?? 0m,
                SilverWeight = filter.ManualClosingSilverWeight ?? 0m,
                IsDebit = true
            });
        }

        // Add Net Profit / Loss to Reserves & Surplus
        if (pAndL.NetProfit != 0 || !filter.HideZeroBalances)
        {
            reservesSection.Items.Add(new BalanceSheetItem
            {
                AccountName = pAndL.NetProfit >= 0 ? "Profit & Loss Account (Current Year Profit)" : "Profit & Loss Account (Current Year Loss)",
                HeadCategory = "Reserves & Surplus",
                ScheduleName = "Reserves & Surplus",
                GroupType = "Equity",
                Amount = pAndL.NetProfit,
                IsCredit = pAndL.NetProfit >= 0,
                IsDebit = pAndL.NetProfit < 0
            });
        }

        foreach (var acc in accountBalances)
        {
            var head = acc.HeadCategory.Trim();
            if (IsPandLHead(head)) continue; // Trading/P&L items roll up into Net Profit

            if (filter.HideZeroBalances && acc.AbsoluteAmount == 0)
                continue;

            var item = new BalanceSheetItem
            {
                AccountName = acc.AccountName,
                HeadCategory = acc.HeadCategory,
                SubCategory = acc.SubCategory,
                Amount = acc.AbsoluteAmount,
                IsDebit = acc.IsDebit,
                IsCredit = acc.IsCredit
            };

            if (IsCapitalHead(head))
            {
                item.ScheduleName = "Capital Account";
                item.GroupType = "Equity";
                capitalSection.Items.Add(item);
            }
            else if (IsReservesHead(head))
            {
                item.ScheduleName = "Reserves & Surplus";
                item.GroupType = "Equity";
                reservesSection.Items.Add(item);
            }
            else if (IsSecuredLoanHead(head))
            {
                item.ScheduleName = "Secured Loans";
                item.GroupType = "Liabilities";
                securedLoansSection.Items.Add(item);
            }
            else if (IsUnsecuredLoanHead(head))
            {
                item.ScheduleName = "Unsecured Loans";
                item.GroupType = "Liabilities";
                unsecuredLoansSection.Items.Add(item);
            }
            else if (IsCreditorHead(head))
            {
                item.ScheduleName = "Sundry Creditors";
                item.GroupType = "Current Liabilities";
                tradePayablesSection.Items.Add(item);
            }
            else if (IsCustomerAdvanceHead(head))
            {
                item.ScheduleName = "Customer Advances";
                item.GroupType = "Current Liabilities";
                customerAdvancesSection.Items.Add(item);
            }
            else if (IsDutiesTaxesHead(head))
            {
                item.ScheduleName = "Duties & Taxes";
                item.GroupType = "Current Liabilities";
                dutiesTaxesSection.Items.Add(item);
            }
            else if (IsFixedAssetHead(head))
            {
                item.ScheduleName = "Fixed Assets";
                item.GroupType = "Assets";
                fixedAssetsSection.Items.Add(item);
            }
            else if (IsInvestmentHead(head))
            {
                item.ScheduleName = "Investments";
                item.GroupType = "Assets";
                investmentsSection.Items.Add(item);
            }
            else if (IsCashBankHead(head))
            {
                item.ScheduleName = "Cash & Bank";
                item.GroupType = "Current Assets";
                cashBankSection.Items.Add(item);
            }
            else if (IsDebtorHead(head))
            {
                item.ScheduleName = "Sundry Debtors";
                item.GroupType = "Current Assets";
                tradeReceivablesSection.Items.Add(item);
            }
            else
            {
                // General classification based on Debit vs Credit balance
                if (acc.IsDebit)
                {
                    item.ScheduleName = "Loans & Advances";
                    item.GroupType = "Current Assets";
                    loansAdvancesSection.Items.Add(item);
                }
                else
                {
                    item.ScheduleName = "Other Liabilities";
                    item.GroupType = "Current Liabilities";
                    otherLiabilitiesSection.Items.Add(item);
                }
            }
        }

        // Add non-empty sections to Statement
        AddSectionIfHasItems(statement.LiabilitiesSections, capitalSection);
        AddSectionIfHasItems(statement.LiabilitiesSections, reservesSection);
        AddSectionIfHasItems(statement.LiabilitiesSections, securedLoansSection);
        AddSectionIfHasItems(statement.LiabilitiesSections, unsecuredLoansSection);
        AddSectionIfHasItems(statement.LiabilitiesSections, tradePayablesSection);
        AddSectionIfHasItems(statement.LiabilitiesSections, customerAdvancesSection);
        AddSectionIfHasItems(statement.LiabilitiesSections, dutiesTaxesSection);
        AddSectionIfHasItems(statement.LiabilitiesSections, otherLiabilitiesSection);

        AddSectionIfHasItems(statement.AssetsSections, fixedAssetsSection);
        AddSectionIfHasItems(statement.AssetsSections, investmentsSection);
        AddSectionIfHasItems(statement.AssetsSections, inventorySection);
        AddSectionIfHasItems(statement.AssetsSections, tradeReceivablesSection);
        AddSectionIfHasItems(statement.AssetsSections, cashBankSection);
        AddSectionIfHasItems(statement.AssetsSections, loansAdvancesSection);

        return statement;
    }

    private static void AddSectionIfHasItems(List<BalanceSheetSection> list, BalanceSheetSection section)
    {
        if (section.Items.Count > 0)
        {
            list.Add(section);
        }
    }

    #region Classification Helpers

    private static bool IsPandLHead(string head) =>
        IsSalesHead(head) || IsPurchaseHead(head) || IsDirectExpenseHead(head) ||
        IsDirectIncomeHead(head) || IsIndirectExpenseHead(head) || IsIndirectIncomeHead(head);

    private static bool IsSalesHead(string head) =>
        head.Contains("Sale", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Job Work Receipt", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Making Charge Received", StringComparison.OrdinalIgnoreCase);

    private static bool IsPurchaseHead(string head) =>
        head.Contains("Purchase", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Karigar Wage", StringComparison.OrdinalIgnoreCase);

    private static bool IsDirectExpenseHead(string head) =>
        head.Contains("Direct Exp", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Making Charge Paid", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Manufacturing", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Labour Paid", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Custom", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Freight", StringComparison.OrdinalIgnoreCase);

    private static bool IsDirectIncomeHead(string head) =>
        head.Contains("Direct Inc", StringComparison.OrdinalIgnoreCase);

    private static bool IsIndirectExpenseHead(string head) =>
        head.Contains("Indirect Exp", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Salary", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Rent", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Electricity", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Depreciation", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Bank Charge", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Audit", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Office Exp", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Interest Paid", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Repair", StringComparison.OrdinalIgnoreCase);

    private static bool IsIndirectIncomeHead(string head) =>
        head.Contains("Indirect Inc", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Discount Rec", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Interest Rec", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Commission Rec", StringComparison.OrdinalIgnoreCase);

    private static bool IsCapitalHead(string head) =>
        head.Contains("Capital", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Drawing", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Partner", StringComparison.OrdinalIgnoreCase);

    private static bool IsReservesHead(string head) =>
        head.Contains("Reserve", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Surplus", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Retained", StringComparison.OrdinalIgnoreCase);

    private static bool IsSecuredLoanHead(string head) =>
        head.Contains("Secured Loan", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Bank Loan", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Gold Loan", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("OD", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("CC A/c", StringComparison.OrdinalIgnoreCase);

    private static bool IsUnsecuredLoanHead(string head) =>
        head.Contains("Unsecured", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Director Loan", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Bullion Loan", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Hand Loan", StringComparison.OrdinalIgnoreCase);

    private static bool IsCreditorHead(string head) =>
        head.Contains("Creditor", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Supplier", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Karighar", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Karigar", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Bullion Dealer", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Trade Payable", StringComparison.OrdinalIgnoreCase);

    private static bool IsCustomerAdvanceHead(string head) =>
        head.Contains("Advance", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Scheme", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Kitty", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Deposit", StringComparison.OrdinalIgnoreCase);

    private static bool IsDutiesTaxesHead(string head) =>
        head.Contains("Duties", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Tax", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("GST", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("TDS", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("TCS", StringComparison.OrdinalIgnoreCase);

    private static bool IsFixedAssetHead(string head) =>
        head.Contains("Fixed Asset", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Furniture", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Machinery", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Equipment", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Computer", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Vehicle", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Building", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Vault", StringComparison.OrdinalIgnoreCase);

    private static bool IsInvestmentHead(string head) =>
        head.Contains("Investment", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Deposit", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Gold Bond", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Fixed Deposit", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("FD", StringComparison.OrdinalIgnoreCase);

    private static bool IsCashBankHead(string head) =>
        head.Contains("Cash", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Bank", StringComparison.OrdinalIgnoreCase);

    private static bool IsDebtorHead(string head) =>
        head.Contains("Debtor", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Customer", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Trade Receivable", StringComparison.OrdinalIgnoreCase);

    #endregion

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

    private sealed class AccountCalculatedBalance
    {
        public string AccountName { get; set; } = string.Empty;
        public string HeadCategory { get; set; } = string.Empty;
        public string SubCategory { get; set; } = string.Empty;
        public decimal NetBalance { get; set; }
        public bool IsDebit { get; set; }
        public bool IsCredit { get; set; }
        public decimal AbsoluteAmount { get; set; }
    }
}
