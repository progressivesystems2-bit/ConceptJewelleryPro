using System.Data;
using System.Data.OleDb;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class AccessTradingAccountRepository : ITradingAccountRepository
{
    private readonly string _connectionString;

    public AccessTradingAccountRepository(string databasePath)
    {
        _connectionString = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={databasePath};";
    }

    public async Task<(decimal GoldWeight, decimal SilverWeight, decimal Value)> GetEstimatedStockValuationAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        decimal goldWt = 0m;
        decimal silverWt = 0m;
        decimal totalVal = 0m;

        try
        {
            await using var connection = new OleDbConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            // 1. Try clstk1 table
            try
            {
                const string sql = "SELECT SUM([Total]), SUM([Gwt]) FROM [clstk1];";
                await using var cmd = new OleDbCommand(sql, connection);
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                {
                    if (reader[0] != DBNull.Value) totalVal = Convert.ToDecimal(reader[0]);
                    if (reader[1] != DBNull.Value) goldWt = Convert.ToDecimal(reader[1]);
                }
            }
            catch { }

            // 2. Fallback to OpeningStock table
            if (totalVal == 0m || goldWt == 0m)
            {
                try
                {
                    const string sql = "SELECT [MetalType], SUM([Weight]), SUM([Amount]) FROM [OpeningStock] GROUP BY [MetalType];";
                    await using var cmd = new OleDbCommand(sql, connection);
                    await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        var metalType = reader[0]?.ToString()?.ToUpperInvariant() ?? string.Empty;
                        var wt = reader[1] != DBNull.Value ? Convert.ToDecimal(reader[1]) : 0m;
                        var amt = reader[2] != DBNull.Value ? Convert.ToDecimal(reader[2]) : 0m;

                        if (metalType.Contains("SILVER"))
                        {
                            silverWt += wt;
                        }
                        else
                        {
                            goldWt += wt;
                        }
                        totalVal += amt;
                    }
                }
                catch { }
            }
        }
        catch { }

        return (goldWt, silverWt, totalVal);
    }

    public async Task<TradingAccountStatement> GetTradingAccountAsync(TradingAccountFilter filter, CancellationToken cancellationToken = default)
    {
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var fromDate = filter.FromDate ?? new DateTime(DateTime.Today.Month < 4 ? DateTime.Today.Year - 1 : DateTime.Today.Year, 4, 1);
        var toDate = filter.ToDate ?? DateTime.Today;

        var statement = new TradingAccountStatement
        {
            FromDate = fromDate,
            ToDate = toDate
        };

        // 1. Fetch Accounts from AccountCreation for Opening Ledger Balances
        var accounts = new List<AccountRaw>();
        const string accSql = @"
 SELECT [PartyCode], [AccountName], [HeadCategory], [SubCategory], [OpAmt1], [OpAmt2], [EntryDate]
 FROM [AccountCreation]
 WHERE [AccountName] IS NOT NULL AND [AccountName] <> ''
 ORDER BY [HeadCategory], [AccountName];";

        try
        {
            await using var cmd = new OleDbCommand(accSql, connection);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                accounts.Add(new AccountRaw
                {
                    PartyCode = reader["PartyCode"]?.ToString()?.Trim() ?? string.Empty,
                    AccountName = reader["AccountName"]?.ToString()?.Trim() ?? string.Empty,
                    HeadCategory = reader["HeadCategory"]?.ToString()?.Trim() ?? "General",
                    SubCategory = reader["SubCategory"]?.ToString()?.Trim() ?? string.Empty,
                    OpAmt1 = reader["OpAmt1"] != DBNull.Value ? Convert.ToDecimal(reader["OpAmt1"]) : 0m,
                    OpAmt2 = reader["OpAmt2"] != DBNull.Value ? Convert.ToDecimal(reader["OpAmt2"]) : 0m,
                    EntryDate = reader["EntryDate"] != DBNull.Value ? Convert.ToDateTime(reader["EntryDate"]) : new DateTime(2025, 4, 1)
                });
            }
        }
        catch { }

        // 2. Base Opening Stock
        decimal baseOpeningVal = 0m;
        decimal baseOpeningGoldWt = 0m;
        decimal baseOpeningSilverWt = 0m;

        try
        {
            const string opSql = "SELECT [MetalType], [Weight], [Amount] FROM [OpeningStock] WHERE [MetalType] IS NOT NULL;";
            await using var cmd = new OleDbCommand(opSql, connection);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var mType = reader["MetalType"]?.ToString()?.ToUpperInvariant() ?? string.Empty;
                var wt = reader["Weight"] != DBNull.Value ? Convert.ToDecimal(reader["Weight"]) : 0m;
                var amt = reader["Amount"] != DBNull.Value ? Convert.ToDecimal(reader["Amount"]) : 0m;

                if (mType.Contains("SILVER"))
                {
                    baseOpeningSilverWt += wt;
                }
                else
                {
                    baseOpeningGoldWt += wt;
                }
                baseOpeningVal += amt;
            }
        }
        catch { }

        var openingStockAmount = filter.ManualOpeningStockAmount ?? (baseOpeningVal > 0 ? baseOpeningVal : 0m);
        var openingGoldWeight = filter.ManualOpeningGoldWeight ?? baseOpeningGoldWt;
        var openingSilverWeight = filter.ManualOpeningSilverWeight ?? baseOpeningSilverWt;

        if (openingStockAmount > 0 || openingGoldWeight > 0 || openingSilverWeight > 0 || !filter.HideZeroBalances)
        {
            statement.DebitItems.Add(new TradingAccountItem
            {
                Particulars = "To Opening Stock-in-Hand",
                AccountName = "Opening Stock",
                HeadCategory = "Stock-in-Hand",
                ScheduleName = "Opening Stock",
                ItemType = TradingItemType.OpeningStock,
                GrossWeight = openingGoldWeight,
                NetWeight = openingGoldWeight,
                SilverWeight = openingSilverWeight,
                Amount = openingStockAmount,
                IsDebit = true,
                Notes = "Stock valuation as on opening period"
            });
        }

        // 3. Sales from Bill table
        var salesMap = new Dictionary<string, TradingAccountItem>(StringComparer.OrdinalIgnoreCase);
        decimal totalSoldGoldWt = 0m;
        decimal totalSoldSilverWt = 0m;

        try
        {
            const string billSql = @"
 SELECT [VouType], [BillDate], [CustomerName], [AccountName], [Item], [GrWt], [NtWt], [NetAmt], [TaxAmt]
 FROM [Bill]
 WHERE ([BillCanceled] = 0 OR [BillCanceled] IS NULL)
 AND ([Status] IS NULL OR [Status] <> 'Deleted');";

            await using var cmd = new OleDbCommand(billSql, connection);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var bDate = reader["BillDate"] != DBNull.Value ? Convert.ToDateTime(reader["BillDate"]) : DateTime.Today;
                if (bDate.Date < fromDate.Date || bDate.Date > toDate.Date) continue;

                var vouType = reader["VouType"]?.ToString()?.Trim() ?? "Sales";
                var accName = reader["AccountName"]?.ToString()?.Trim() ?? "Sales A/c";
                var itemType = reader["Item"]?.ToString()?.ToUpperInvariant() ?? string.Empty;
                var netAmt = reader["NetAmt"] != DBNull.Value ? Convert.ToDecimal(reader["NetAmt"]) : 0m;
                var taxAmt = reader["TaxAmt"] != DBNull.Value ? Convert.ToDecimal(reader["TaxAmt"]) : 0m;
                var grWt = reader["GrWt"] != DBNull.Value ? Convert.ToDecimal(reader["GrWt"]) : 0m;
                var ntWt = reader["NtWt"] != DBNull.Value ? Convert.ToDecimal(reader["NtWt"]) : 0m;

                var taxableAmt = netAmt > taxAmt ? (netAmt - taxAmt) : netAmt;
                var wt = ntWt > 0 ? ntWt : grWt;

                bool isOldGoldPur = vouType.Equals("OLD GOLD PURCHASE", StringComparison.OrdinalIgnoreCase);
                bool isSalesReturn = vouType.Contains("Return", StringComparison.OrdinalIgnoreCase) && vouType.Contains("Sales", StringComparison.OrdinalIgnoreCase);
                bool isPurchase = vouType.Contains("Purchase", StringComparison.OrdinalIgnoreCase) && !isSalesReturn;

                if (isOldGoldPur)
                {
                    // Track as Purchase Old Gold
                    var key = "Old Gold Purchase";
                    if (!salesMap.TryGetValue(key, out var oldGoldItem))
                    {
                        oldGoldItem = new TradingAccountItem
                        {
                            Particulars = "To Old Gold Exchange / Urdhu Purchases",
                            AccountName = "Old Gold Purchase",
                            HeadCategory = "Purchases",
                            ScheduleName = "Purchases",
                            ItemType = TradingItemType.PurchaseOldGold,
                            IsDebit = true
                        };
                        salesMap[key] = oldGoldItem;
                    }
                    oldGoldItem.Amount += taxableAmt;
                    if (itemType.Contains("SILVER")) oldGoldItem.SilverWeight += wt;
                    else oldGoldItem.GrossWeight += wt;
                }
                else if (isSalesReturn)
                {
                    // Sales return reduces sales or goes to Debit
                    var key = $"Sales Return - {vouType}";
                    if (!salesMap.TryGetValue(key, out var retItem))
                    {
                        retItem = new TradingAccountItem
                        {
                            Particulars = $"Less: Sales Return ({vouType})",
                            AccountName = accName,
                            HeadCategory = "Sales",
                            ScheduleName = "Sales",
                            ItemType = TradingItemType.SalesGeneral,
                            IsCredit = true
                        };
                        salesMap[key] = retItem;
                    }
                    retItem.Amount -= taxableAmt;
                    if (itemType.Contains("SILVER")) retItem.SilverWeight -= wt;
                    else retItem.GrossWeight -= wt;
                }
                else if (!isPurchase)
                {
                    // Normal Sales
                    var key = string.IsNullOrWhiteSpace(accName) ? "Sales A/c" : accName;
                    if (!salesMap.TryGetValue(key, out var saleItem))
                    {
                        saleItem = new TradingAccountItem
                        {
                            Particulars = $"By {key} (Vou: {vouType})",
                            AccountName = key,
                            HeadCategory = "Sales",
                            ScheduleName = "Sales",
                            ItemType = TradingItemType.SalesRetail,
                            IsCredit = true
                        };
                        salesMap[key] = saleItem;
                    }
                    saleItem.Amount += taxableAmt;
                    if (itemType.Contains("SILVER"))
                    {
                        saleItem.SilverWeight += wt;
                        totalSoldSilverWt += wt;
                    }
                    else
                    {
                        saleItem.GrossWeight += wt;
                        totalSoldGoldWt += wt;
                    }
                }
            }
        }
        catch { }

        // Add sales and old gold purchases
        foreach (var kvp in salesMap.Values)
        {
            if (filter.HideZeroBalances && kvp.Amount == 0 && kvp.GrossWeight == 0 && kvp.SilverWeight == 0) continue;
            if (kvp.IsDebit) statement.DebitItems.Add(kvp);
            else statement.CreditItems.Add(kvp);
        }

        // 4. Purchases & Direct Expenses from PurchaseE table
        var purchaseMap = new Dictionary<string, TradingAccountItem>(StringComparer.OrdinalIgnoreCase);
        decimal totalPurchasedGoldWt = 0m;
        decimal totalPurchasedSilverWt = 0m;

        try
        {
            const string peSql = @"
 SELECT [AccountName], [Head], [NetAmt], [TaxAmt], [BillDate]
 FROM [PurchaseE]
 WHERE ([Status] IS NULL OR [Status] <> 'Deleted');";

            await using var cmd = new OleDbCommand(peSql, connection);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var bDate = reader["BillDate"] != DBNull.Value ? Convert.ToDateTime(reader["BillDate"]) : DateTime.Today;
                if (bDate.Date < fromDate.Date || bDate.Date > toDate.Date) continue;

                var partyName = reader["AccountName"]?.ToString()?.Trim() ?? string.Empty;
                var headName = reader["Head"]?.ToString()?.Trim() ?? "Purchases";
                var netAmt = reader["NetAmt"] != DBNull.Value ? Convert.ToDecimal(reader["NetAmt"]) : 0m;
                var taxAmt = reader["TaxAmt"] != DBNull.Value ? Convert.ToDecimal(reader["TaxAmt"]) : 0m;
                var itemAmt = netAmt > taxAmt ? (netAmt - taxAmt) : netAmt;

                bool isDirectExp = IsDirectExpenseHead(headName);
                var key = isDirectExp ? $"Exp_{headName}" : $"Pur_{headName}";

                if (!purchaseMap.TryGetValue(key, out var item))
                {
                    item = new TradingAccountItem
                    {
                        Particulars = isDirectExp ? $"To Direct Exp: {headName}" : $"To Purchases ({headName})",
                        AccountName = headName,
                        HeadCategory = isDirectExp ? "Direct Expenses" : "Purchases",
                        ScheduleName = isDirectExp ? "Direct Expenses" : "Purchases",
                        ItemType = isDirectExp ? TradingItemType.DirectExpenseGeneral : TradingItemType.PurchaseGeneral,
                        IsDebit = true
                    };
                    purchaseMap[key] = item;
                }
                item.Amount += itemAmt;
            }
        }
        catch { }

        // 5. Direct Expenses: Karigar Mazdoori / Making Charges from MetalReceived
        try
        {
            const string mrSql = @"
 SELECT SUM([MakingCharges]), SUM([WeightRecd])
 FROM [MetalReceived]
 WHERE ([Status] IS NULL OR [Status] <> 'Deleted')
 AND [RecDate] >= ? AND [RecDate] <= ?;";

            await using var cmd = new OleDbCommand(mrSql, connection);
            cmd.Parameters.AddWithValue("?", fromDate.Date);
            cmd.Parameters.AddWithValue("?", toDate.Date);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                var making = reader[0] != DBNull.Value ? Convert.ToDecimal(reader[0]) : 0m;
                var wtRecd = reader[1] != DBNull.Value ? Convert.ToDecimal(reader[1]) : 0m;
                if (making > 0)
                {
                    purchaseMap["KarigarMazdoori"] = new TradingAccountItem
                    {
                        Particulars = "To Karigar Making Charges / Mazdoori",
                        AccountName = "Karigar Wages",
                        HeadCategory = "Direct Expenses",
                        ScheduleName = "Direct Expenses",
                        ItemType = TradingItemType.DirectExpenseKarigarWages,
                        GrossWeight = wtRecd,
                        Amount = making,
                        IsDebit = true,
                        Notes = "Labour charges on jewellery receipts"
                    };
                }
            }
        }
        catch { }

        foreach (var pItem in purchaseMap.Values)
        {
            if (filter.HideZeroBalances && pItem.Amount == 0 && pItem.GrossWeight == 0 && pItem.SilverWeight == 0) continue;
            statement.DebitItems.Add(pItem);
            if (pItem.ItemType is TradingItemType.PurchaseBullion or TradingItemType.PurchaseJewellery or TradingItemType.PurchaseOldGold or TradingItemType.PurchaseGeneral)
            {
                totalPurchasedGoldWt += pItem.GrossWeight;
                totalPurchasedSilverWt += pItem.SilverWeight;
            }
        }

        // 6. Closing Stock
        var estValuation = await GetEstimatedStockValuationAsync(toDate, cancellationToken);
        var closingStockVal = filter.ManualClosingStockAmount ?? (estValuation.Value > 0 ? estValuation.Value : 0m);
        var closingGoldWt = filter.ManualClosingGoldWeight ?? estValuation.GoldWeight;
        var closingSilverWt = filter.ManualClosingSilverWeight ?? estValuation.SilverWeight;

        if (closingStockVal > 0 || closingGoldWt > 0 || closingSilverWt > 0 || !filter.HideZeroBalances)
        {
            statement.CreditItems.Add(new TradingAccountItem
            {
                Particulars = "By Closing Stock-in-Hand",
                AccountName = "Closing Stock",
                HeadCategory = "Stock-in-Hand",
                ScheduleName = "Closing Stock",
                ItemType = TradingItemType.ClosingStock,
                GrossWeight = closingGoldWt,
                NetWeight = closingGoldWt,
                SilverWeight = closingSilverWt,
                Amount = closingStockVal,
                IsCredit = true,
                Notes = "Physical inventory valuation at cost / NRV"
            });
        }

        // 7. All Items
        statement.AllItems.AddRange(statement.DebitItems);
        statement.AllItems.AddRange(statement.CreditItems);

        // 8. Gross Profit / Loss balancing row
        if (statement.IsGrossProfit)
        {
            statement.DebitItems.Add(new TradingAccountItem
            {
                Particulars = "To Gross Profit c/d (Transferred to Profit & Loss A/c)",
                AccountName = "Gross Profit Transfer",
                HeadCategory = "Gross Profit",
                ScheduleName = "Profit Margin",
                ItemType = TradingItemType.GrossProfitTransfer,
                Amount = statement.GrossProfit,
                IsDebit = true
            });
        }
        else
        {
            statement.CreditItems.Add(new TradingAccountItem
            {
                Particulars = "By Gross Loss c/d (Transferred to Profit & Loss A/c)",
                AccountName = "Gross Loss Transfer",
                HeadCategory = "Gross Loss",
                ScheduleName = "Profit Margin",
                ItemType = TradingItemType.GrossLossTransfer,
                Amount = Math.Abs(statement.GrossProfit),
                IsCredit = true
            });
        }

        // 9. Metal Reconciliation
        statement.GoldReconciliation = new MetalReconciliationSummary
        {
            MetalType = "GOLD (in Grams)",
            OpeningWeight = openingGoldWeight,
            InwardWeight = totalPurchasedGoldWt + statement.DebitItems.Where(x => x.ItemType == TradingItemType.PurchaseOldGold).Sum(x => x.GrossWeight),
            OutwardWeight = totalSoldGoldWt,
            ClosingWeight = closingGoldWt
        };

        statement.SilverReconciliation = new MetalReconciliationSummary
        {
            MetalType = "SILVER (in Grams)",
            OpeningWeight = openingSilverWeight,
            InwardWeight = totalPurchasedSilverWt,
            OutwardWeight = totalSoldSilverWt,
            ClosingWeight = closingSilverWt
        };

        return statement;
    }

    private static bool IsDirectExpenseHead(string head) =>
        head.Contains("Direct Exp", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Making Charge Paid", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Karigar Wage", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Manufacturing", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Labour Paid", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Hallmark", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Melting", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Freight", StringComparison.OrdinalIgnoreCase) ||
        head.Contains("Custom", StringComparison.OrdinalIgnoreCase);

    private sealed class AccountRaw
    {
        public string PartyCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string HeadCategory { get; set; } = string.Empty;
        public string SubCategory { get; set; } = string.Empty;
        public decimal OpAmt1 { get; set; }
        public decimal OpAmt2 { get; set; }
        public DateTime EntryDate { get; set; }
    }
}

