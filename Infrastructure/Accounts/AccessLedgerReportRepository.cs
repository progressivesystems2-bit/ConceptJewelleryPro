using System.Data;
using System.Data.OleDb;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class AccessLedgerReportRepository : ILedgerReportRepository
{
    private readonly string _connectionString;

    public AccessLedgerReportRepository(string databasePath)
    {
        _connectionString = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={databasePath};";
    }

    public async Task<IReadOnlyList<Account>> GetLedgerAccountsAsync(CancellationToken cancellationToken = default)
    {
        await AccessStaffCommissionLedgerSchemaMigrator.EnsureSchemaAsync(_connectionString, cancellationToken);

        var list = new List<Account>();
        const string sql = "SELECT * FROM AccountCreation ORDER BY [AccountName]";

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(MapAccount(reader));
        }

        return list;
    }

    public async Task<LedgerAccountSummary> GetAccountSummaryAsync(string accountName, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        await AccessStaffCommissionLedgerSchemaMigrator.EnsureSchemaAsync(_connectionString, cancellationToken);

        var summary = new LedgerAccountSummary
        {
            AccountName = accountName
        };

        if (string.IsNullOrWhiteSpace(accountName))
            return summary;

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // 1. Fetch Account Info
        Account? account = null;
        const string accSql = "SELECT TOP 1 * FROM AccountCreation WHERE [AccountName] = @AccountName";
        await using (var cmd = new OleDbCommand(accSql, connection))
        {
            cmd.Parameters.Add("@AccountName", OleDbType.VarWChar, 100).Value = accountName;
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                account = MapAccount(reader);
            }
        }

        if (account != null)
        {
            summary.Category = account.HeadCategory;
            summary.PhoneNo = account.PhoneNo;
            summary.RefName = account.RefName;
        }

        // 2. Fetch all entries to calculate exact opening, debits, credits, closing
        var filter = new LedgerReportFilter
        {
            AccountName = accountName,
            FromDate = fromDate,
            ToDate = toDate
        };

        var rupeeEntries = await GetLedgerInRupeesInternalAsync(connection, account, filter, cancellationToken);
        var weightEntries = await GetLedgerInWeightInternalAsync(connection, account, filter, cancellationToken);

        // Rupee totals
        var openingEntry = rupeeEntries.FirstOrDefault(e => e.VouType == "Opening Balance");
        if (openingEntry != null)
        {
            summary.OpeningAmount = openingEntry.Balance;
            summary.OpeningType = openingEntry.BalanceType;
        }
        else
        {
            summary.OpeningAmount = 0m;
            summary.OpeningType = "Dr";
        }

        var periodEntries = rupeeEntries.Where(e => e.VouType != "Opening Balance").ToList();
        summary.TotalDebits = periodEntries.Sum(e => e.Debit);
        summary.TotalCredits = periodEntries.Sum(e => e.Credit);

        if (rupeeEntries.Count > 0)
        {
            var last = rupeeEntries.Last();
            summary.ClosingBalance = last.Balance;
            summary.ClosingType = last.BalanceType;
        }
        else
        {
            summary.ClosingBalance = summary.OpeningAmount;
            summary.ClosingType = summary.OpeningType;
        }

        // Weight totals
        var opWeightEntry = weightEntries.FirstOrDefault(e => e.VouType == "Opening Balance");
        if (opWeightEntry != null)
        {
            summary.OpeningGoldWeight = opWeightEntry.WeightBalance;
            summary.OpeningWeightType = opWeightEntry.WeightBalanceType;
        }
        else
        {
            summary.OpeningGoldWeight = 0m;
            summary.OpeningWeightType = "Dr";
        }

        var periodWeightEntries = weightEntries.Where(e => e.VouType != "Opening Balance").ToList();
        summary.TotalWeightIssued = periodWeightEntries.Sum(e => e.WeightDebit);
        summary.TotalWeightReceived = periodWeightEntries.Sum(e => e.WeightCredit);

        if (weightEntries.Count > 0)
        {
            var last = weightEntries.Last();
            summary.ClosingGoldWeight = last.WeightBalance;
            summary.ClosingWeightType = last.WeightBalanceType;
        }
        else
        {
            summary.ClosingGoldWeight = summary.OpeningGoldWeight;
            summary.ClosingWeightType = summary.OpeningWeightType;
        }

        return summary;
    }

    public async Task<IReadOnlyList<LedgerEntry>> GetLedgerInRupeesAsync(LedgerReportFilter filter, CancellationToken cancellationToken = default)
    {
        await AccessStaffCommissionLedgerSchemaMigrator.EnsureSchemaAsync(_connectionString, cancellationToken);

        if (string.IsNullOrWhiteSpace(filter.AccountName))
            return Array.Empty<LedgerEntry>();

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        Account? account = null;
        const string accSql = "SELECT TOP 1 * FROM AccountCreation WHERE [AccountName] = @AccountName";
        await using (var cmd = new OleDbCommand(accSql, connection))
        {
            cmd.Parameters.Add("@AccountName", OleDbType.VarWChar, 100).Value = filter.AccountName;
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                account = MapAccount(reader);
            }
        }

        return await GetLedgerInRupeesInternalAsync(connection, account, filter, cancellationToken);
    }

    private static async Task<List<LedgerEntry>> GetLedgerInRupeesInternalAsync(
        OleDbConnection connection,
        Account? account,
        LedgerReportFilter filter,
        CancellationToken cancellationToken)
    {
        var rawEntries = new List<RawLedgerRecord>();
        var fromDate = filter.FromDate ?? new DateTime(DateTime.Today.Year, 4, 1);
        var toDate = filter.ToDate ?? DateTime.Today;
        var accName = filter.AccountName.Trim();

        // 1. Initial Opening Balance from AccountCreation (starting balance before any transactions)
        // Rule: If OpAmt1 is negative then Cr side, else Dr side
        decimal initialOpAmount = 0m;

        if (account != null)
        {
            if (account.OpAmt1 < 0)
            {
                // Explicitly negative in DB = Credit side
                initialOpAmount = account.OpAmt1;
            }
            else if (account.OpAmt1 > 0)
            {
                // Explicitly positive in DB = Debit side
                initialOpAmount = account.OpAmt1;
            }
            else if (account.OpAmt2 > 0)
            {
                initialOpAmount = -account.OpAmt2;
            }
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

        // Load Account InOut mapping for GST routing
        var accountInOutMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            const string accSql = "SELECT [AccountName], [InOut] FROM [AccountCreation]";
            await using var accCmd = new OleDbCommand(accSql, connection);
            await using var accReader = await accCmd.ExecuteReaderAsync(cancellationToken);
            while (await accReader.ReadAsync(cancellationToken))
            {
                var an = accReader["AccountName"]?.ToString()?.Trim() ?? string.Empty;
                var io = accReader["InOut"]?.ToString()?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(an))
                {
                    accountInOutMap[an] = io;
                }
            }
        }
        catch { }

        // Find matched voucher types for accName
        var matchedSalesVouTypes = new List<(string VouType, string HeadCategory)>();
        foreach (var kvp in salesVouTypeMap)
        {
            var vt = kvp.Key;
            var sa = kvp.Value.SalesAccount;
            var hc = kvp.Value.HeadCategory;

            if (string.Equals(sa, accName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(vt, accName, StringComparison.OrdinalIgnoreCase))
            {
                matchedSalesVouTypes.Add((vt, hc));
            }
            else if (string.Equals(accName, "Purchases", StringComparison.OrdinalIgnoreCase) &&
                     string.Equals(hc, "Purchases", StringComparison.OrdinalIgnoreCase))
            {
                matchedSalesVouTypes.Add((vt, hc));
            }
            else if (string.Equals(accName, "Sales", StringComparison.OrdinalIgnoreCase) &&
                     string.Equals(hc, "Sales", StringComparison.OrdinalIgnoreCase))
            {
                matchedSalesVouTypes.Add((vt, hc));
            }
            else if (string.Equals(accName, "Purchase Return", StringComparison.OrdinalIgnoreCase) &&
                     string.Equals(hc, "Purchase Return", StringComparison.OrdinalIgnoreCase))
            {
                matchedSalesVouTypes.Add((vt, hc));
            }
            else if (string.Equals(accName, "Sales Return", StringComparison.OrdinalIgnoreCase) &&
                     string.Equals(hc, "Sales Return", StringComparison.OrdinalIgnoreCase))
            {
                matchedSalesVouTypes.Add((vt, hc));
            }
        }

        bool isPurchaseAccount = string.Equals(accName, "Purchases", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(accName, "PURCHASE", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(accName, "PURCHASE ACCOUNT", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(accName, "PURCHASE ACCOUNTS", StringComparison.OrdinalIgnoreCase) ||
                                 matchedSalesVouTypes.Any(m => m.HeadCategory.Equals("Purchases", StringComparison.OrdinalIgnoreCase));

        bool isGstAccount = string.Equals(accName, "GST", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(accName, "GST PAYABLE", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(accName, "GST CREDIT LEDGER", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(accName, "VAT", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(accName, "INPUT GST", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(accName, "OUTPUT GST", StringComparison.OrdinalIgnoreCase) ||
                            accName.Contains("Goods & Service Tax", StringComparison.OrdinalIgnoreCase);

        bool isIgstAccount = string.Equals(accName, "IGST", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(accName, "INPUT IGST", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(accName, "OUTPUT IGST", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(accName, "IGST INPUT", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(accName, "IGST OUTPUT", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(accName, "IGST PAYABLE", StringComparison.OrdinalIgnoreCase) ||
                             accName.Contains("Integrated GST", StringComparison.OrdinalIgnoreCase) ||
                             accName.Contains("Integrated Tax", StringComparison.OrdinalIgnoreCase);

        bool isCgstAccount = string.Equals(accName, "CGST", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(accName, "INPUT CGST", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(accName, "OUTPUT CGST", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(accName, "CGST INPUT", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(accName, "CGST OUTPUT", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(accName, "CGST PAYABLE", StringComparison.OrdinalIgnoreCase) ||
                             accName.Contains("Central GST", StringComparison.OrdinalIgnoreCase) ||
                             accName.Contains("Central Tax", StringComparison.OrdinalIgnoreCase);

        bool isSgstAccount = string.Equals(accName, "SGST", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(accName, "INPUT SGST", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(accName, "OUTPUT SGST", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(accName, "SGST INPUT", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(accName, "SGST OUTPUT", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(accName, "SGST PAYABLE", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(accName, "UTGST", StringComparison.OrdinalIgnoreCase) ||
                             accName.Contains("State GST", StringComparison.OrdinalIgnoreCase) ||
                             accName.Contains("State Tax", StringComparison.OrdinalIgnoreCase);

        // 2. Fetch Transactions from BILL table
        try
        {
            if (isGstAccount || isIgstAccount || isCgstAccount || isSgstAccount)
            {
                // When viewing GST ledger, query all bills that have TaxAmt > 0
                const string billTaxSql = @"
                    SELECT [VouNo], [VouType], [BillDate], [CustomerName], [AccountName], [TaxAmt], [NetAmt], [Narration], [Remarks], [BillCanceled], [Status]
                    FROM [Bill]
                    WHERE ([BillCanceled] = 0 OR [BillCanceled] IS NULL)
                      AND ([Status] IS NULL OR [Status] <> 'Deleted')
                      AND [TaxAmt] > 0";

                await using var cmd = new OleDbCommand(billTaxSql, connection);
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var vouNo = reader["VouNo"]?.ToString()?.Trim() ?? string.Empty;
                    var vouType = reader["VouType"]?.ToString()?.Trim() ?? "Invoice";
                    var billDate = reader["BillDate"] != DBNull.Value ? Convert.ToDateTime(reader["BillDate"]) : DateTime.Today;
                    var taxAmt = reader["TaxAmt"] != DBNull.Value ? Convert.ToDecimal(reader["TaxAmt"]) : 0m;
                    var custName = reader["CustomerName"]?.ToString()?.Trim() ?? string.Empty;
                    var narration = reader["Narration"]?.ToString()?.Trim() ?? string.Empty;
                    if (string.IsNullOrEmpty(narration))
                    {
                        narration = reader["Remarks"]?.ToString()?.Trim() ?? string.Empty;
                    }

                    if (taxAmt <= 0) continue;

                    string inOut = string.Empty;
                    if (!string.IsNullOrEmpty(custName) && accountInOutMap.TryGetValue(custName, out var mappedIo))
                    {
                        inOut = mappedIo;
                    }
                    bool isOutSide = inOut.Contains("OUT", StringComparison.OrdinalIgnoreCase);

                    decimal effectiveIgst = isOutSide ? taxAmt : 0m;
                    decimal effectiveCgst = !isOutSide ? (taxAmt / 2m) : 0m;
                    decimal effectiveSgst = !isOutSide ? (taxAmt / 2m) : 0m;

                    string headCat = salesVouTypeMap.TryGetValue(vouType, out var svt) ? svt.HeadCategory : string.Empty;
                    bool isSalesReturn = headCat.Equals("Sales Return", StringComparison.OrdinalIgnoreCase) || (vouType.Contains("Return", StringComparison.OrdinalIgnoreCase) && vouType.Contains("Sales", StringComparison.OrdinalIgnoreCase));
                    bool isPurchase = headCat.Equals("Purchases", StringComparison.OrdinalIgnoreCase) || (vouType.Contains("Purchase", StringComparison.OrdinalIgnoreCase) && !vouType.Contains("Return", StringComparison.OrdinalIgnoreCase));
                    bool isPurchaseReturn = headCat.Equals("Purchase Return", StringComparison.OrdinalIgnoreCase) || (vouType.Contains("Purchase", StringComparison.OrdinalIgnoreCase) && vouType.Contains("Return", StringComparison.OrdinalIgnoreCase));

                    decimal debit = 0m;
                    decimal credit = 0m;
                    string head = string.Empty;

                    if (isGstAccount)
                    {
                        if (isSalesReturn)
                        {
                            debit = taxAmt;
                            head = string.IsNullOrWhiteSpace(custName) ? "To Sales Return" : $"To Sales Return - {custName}";
                        }
                        else if (isPurchase)
                        {
                            debit = taxAmt;
                            head = string.IsNullOrWhiteSpace(custName) ? "To Purchases" : $"To Purchases - {custName}";
                        }
                        else if (isPurchaseReturn)
                        {
                            credit = taxAmt;
                            head = string.IsNullOrWhiteSpace(custName) ? "By Purchase Return" : $"By Purchase Return - {custName}";
                        }
                        else
                        {
                            credit = taxAmt;
                            head = string.IsNullOrWhiteSpace(custName) ? "By Sales A/c" : $"By Sales - {custName}";
                        }
                    }
                    else if (isIgstAccount)
                    {
                        if (effectiveIgst <= 0) continue;
                        if (isSalesReturn || isPurchase)
                        {
                            debit = effectiveIgst;
                            head = string.IsNullOrWhiteSpace(custName) ? "To Sales Return" : $"To Sales Return - {custName}";
                        }
                        else
                        {
                            credit = effectiveIgst;
                            head = string.IsNullOrWhiteSpace(custName) ? "By Sales A/c" : $"By Sales - {custName}";
                        }
                    }
                    else if (isCgstAccount)
                    {
                        if (effectiveCgst <= 0) continue;
                        if (isSalesReturn || isPurchase)
                        {
                            debit = effectiveCgst;
                            head = string.IsNullOrWhiteSpace(custName) ? "To Sales Return" : $"To Sales Return - {custName}";
                        }
                        else
                        {
                            credit = effectiveCgst;
                            head = string.IsNullOrWhiteSpace(custName) ? "By Sales A/c" : $"By Sales - {custName}";
                        }
                    }
                    else if (isSgstAccount)
                    {
                        if (effectiveSgst <= 0) continue;
                        if (isSalesReturn || isPurchase)
                        {
                            debit = effectiveSgst;
                            head = string.IsNullOrWhiteSpace(custName) ? "To Sales Return" : $"To Sales Return - {custName}";
                        }
                        else
                        {
                            credit = effectiveSgst;
                            head = string.IsNullOrWhiteSpace(custName) ? "By Sales A/c" : $"By Sales - {custName}";
                        }
                    }

                    if (debit > 0 || credit > 0)
                    {
                        rawEntries.Add(new RawLedgerRecord
                        {
                            VouDate = billDate,
                            VouNo = vouNo,
                            VouType = vouType,
                            Head = head,
                            Debit = debit,
                            Credit = credit,
                            Narration = narration
                        });
                    }
                }
            }
            else if (matchedSalesVouTypes.Count > 0)
            {
                // When viewing a Purchase / Sales ledger account defined in SalesVouType
                var matchedTypesSet = new HashSet<string>(matchedSalesVouTypes.Select(m => m.VouType), StringComparer.OrdinalIgnoreCase);
                const string billAllSql = @"
                    SELECT [VouNo], [VouType], [BillDate], [CustomerName], [AccountName], [TaxAmt], [NetAmt], [Narration], [Remarks], [BillCanceled], [Status]
                    FROM [Bill]
                    WHERE ([BillCanceled] = 0 OR [BillCanceled] IS NULL)
                      AND ([Status] IS NULL OR [Status] <> 'Deleted')
                      AND [NetAmt] > 0";

                await using var cmd = new OleDbCommand(billAllSql, connection);
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var vouType = reader["VouType"]?.ToString()?.Trim() ?? string.Empty;
                    if (!matchedTypesSet.Contains(vouType)) continue;

                    var vouNo = reader["VouNo"]?.ToString()?.Trim() ?? string.Empty;
                    var billDate = reader["BillDate"] != DBNull.Value ? Convert.ToDateTime(reader["BillDate"]) : DateTime.Today;
                    var netAmt = reader["NetAmt"] != DBNull.Value ? Convert.ToDecimal(reader["NetAmt"]) : 0m;
                    var taxAmt = reader["TaxAmt"] != DBNull.Value ? Convert.ToDecimal(reader["TaxAmt"]) : 0m;
                    var custName = reader["CustomerName"]?.ToString()?.Trim() ?? string.Empty;
                    var narration = reader["Narration"]?.ToString()?.Trim() ?? string.Empty;
                    if (string.IsNullOrEmpty(narration))
                    {
                        narration = reader["Remarks"]?.ToString()?.Trim() ?? string.Empty;
                    }

                    decimal itemAmt = (netAmt > taxAmt) ? (netAmt - taxAmt) : netAmt;
                    string headCat = salesVouTypeMap.TryGetValue(vouType, out var svt) ? svt.HeadCategory : string.Empty;
                    bool isSalesReturn = headCat.Equals("Sales Return", StringComparison.OrdinalIgnoreCase) || (vouType.Contains("Return", StringComparison.OrdinalIgnoreCase) && vouType.Contains("Sales", StringComparison.OrdinalIgnoreCase));
                    bool isPurchase = headCat.Equals("Purchases", StringComparison.OrdinalIgnoreCase) || (vouType.Contains("Purchase", StringComparison.OrdinalIgnoreCase) && !vouType.Contains("Return", StringComparison.OrdinalIgnoreCase));
                    bool isPurchaseReturn = headCat.Equals("Purchase Return", StringComparison.OrdinalIgnoreCase) || (vouType.Contains("Purchase", StringComparison.OrdinalIgnoreCase) && vouType.Contains("Return", StringComparison.OrdinalIgnoreCase));

                    decimal debit = 0m;
                    decimal credit = 0m;
                    string head;

                    if (isPurchase)
                    {
                        debit = itemAmt;
                        credit = 0m;
                        head = string.IsNullOrWhiteSpace(custName) ? "To Cash" : $"To {custName}";
                    }
                    else if (isPurchaseReturn)
                    {
                        debit = 0m;
                        credit = itemAmt;
                        head = string.IsNullOrWhiteSpace(custName) ? "By Cash" : $"By {custName}";
                    }
                    else if (isSalesReturn)
                    {
                        debit = itemAmt;
                        credit = 0m;
                        head = string.IsNullOrWhiteSpace(custName) ? "To Cash" : $"To {custName}";
                    }
                    else
                    {
                        // Sales
                        debit = 0m;
                        credit = itemAmt;
                        head = string.IsNullOrWhiteSpace(custName) ? "By Cash" : $"By {custName}";
                    }

                    rawEntries.Add(new RawLedgerRecord
                    {
                        VouDate = billDate,
                        VouNo = vouNo,
                        VouType = vouType,
                        Head = head,
                        Debit = debit,
                        Credit = credit,
                        Narration = narration
                    });
                }
            }
            else
            {
                // Standard Party / Customer / Account query for Purchase / Return / Non-sales vouchers
                const string billSql = @"
                    SELECT [VouNo], [VouType], [BillDate], [CustomerName], [AccountName], [NetAmt], [Narration], [Remarks], [BillCanceled], [Status]
                    FROM [Bill]
                    WHERE ([BillCanceled] = 0 OR [BillCanceled] IS NULL)
                      AND ([Status] IS NULL OR [Status] <> 'Deleted')
                      AND ([CustomerName] = @CustName OR [AccountName] = @AccName)";

                await using var cmd = new OleDbCommand(billSql, connection);
                cmd.Parameters.Add("@CustName", OleDbType.VarWChar, 100).Value = accName;
                cmd.Parameters.Add("@AccName", OleDbType.VarWChar, 100).Value = accName;

                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var vouNo = reader["VouNo"]?.ToString()?.Trim() ?? string.Empty;
                    var vouType = reader["VouType"]?.ToString()?.Trim() ?? "Invoice";
                    var billDate = reader["BillDate"] != DBNull.Value ? Convert.ToDateTime(reader["BillDate"]) : DateTime.Today;
                    var netAmt = reader["NetAmt"] != DBNull.Value ? Convert.ToDecimal(reader["NetAmt"]) : 0m;
                    var narration = reader["Narration"]?.ToString()?.Trim() ?? string.Empty;
                    if (string.IsNullOrEmpty(narration))
                    {
                        narration = reader["Remarks"]?.ToString()?.Trim() ?? string.Empty;
                    }

                    string headCat = salesVouTypeMap.TryGetValue(vouType, out var svt) ? svt.HeadCategory : string.Empty;
                    string targetSalesAcc = svt.SalesAccount;
                    bool isSalesReturn = headCat.Equals("Sales Return", StringComparison.OrdinalIgnoreCase) || (vouType.Contains("Return", StringComparison.OrdinalIgnoreCase) && vouType.Contains("Sales", StringComparison.OrdinalIgnoreCase));
                    bool isPurchase = headCat.Equals("Purchases", StringComparison.OrdinalIgnoreCase) || (vouType.Contains("Purchase", StringComparison.OrdinalIgnoreCase) && !vouType.Contains("Return", StringComparison.OrdinalIgnoreCase));
                    bool isPurchaseReturn = headCat.Equals("Purchase Return", StringComparison.OrdinalIgnoreCase) || (vouType.Contains("Purchase", StringComparison.OrdinalIgnoreCase) && vouType.Contains("Return", StringComparison.OrdinalIgnoreCase));

                    if (!isPurchase && !isPurchaseReturn && !isSalesReturn)
                    {
                        continue;
                    }

                    decimal debit = 0m;
                    decimal credit = 0m;
                    string head;

                    if (isSalesReturn)
                    {
                        credit = netAmt;
                        head = string.IsNullOrWhiteSpace(targetSalesAcc) ? "Sales Return" : targetSalesAcc;
                    }
                    else if (isPurchase)
                    {
                        credit = netAmt;
                        head = string.IsNullOrWhiteSpace(targetSalesAcc) ? "Purchase A/c" : targetSalesAcc;
                    }
                    else if (isPurchaseReturn)
                    {
                        debit = netAmt;
                        head = string.IsNullOrWhiteSpace(targetSalesAcc) ? "Purchase Return" : targetSalesAcc;
                    }
                    else
                    {
                        debit = netAmt;
                        head = string.IsNullOrWhiteSpace(targetSalesAcc) ? "Sales A/c" : targetSalesAcc;
                    }

                    rawEntries.Add(new RawLedgerRecord
                    {
                        VouDate = billDate,
                        VouNo = vouNo,
                        VouType = vouType,
                        Head = head,
                        Debit = debit,
                        Credit = credit,
                        Narration = narration
                    });
                }
            }
        }
        catch { }

        // 2b. Fetch Transactions from PURCHASES table (Purchase, Party Ledger, GST Ledger)
        try
        {
            if (isGstAccount || isIgstAccount || isCgstAccount || isSgstAccount)
            {
                // When viewing GST/IGST/CGST/SGST ledger, query Purchases with tax
                const string purTaxSql = @"
                    SELECT [VouNo], [VouType], [BillDate], [AccountName], [CustomerName], [TaxAmt], [CGSTAmt], [SGSTAmt], [IGSTAmt], [TaxableAmount], [NetAmt], [Narration], [Remarks], [InOut]
                    FROM [Purchases]
                    WHERE ([BillCanceled] = 0 OR [BillCanceled] IS NULL)
                      AND ([Status] IS NULL OR [Status] <> 'Deleted')
                      AND ([TaxAmt] > 0 OR [CGSTAmt] > 0 OR [SGSTAmt] > 0 OR [IGSTAmt] > 0)";

                await using var purTaxCmd = new OleDbCommand(purTaxSql, connection);
                await using var purTaxReader = await purTaxCmd.ExecuteReaderAsync(cancellationToken);
                while (await purTaxReader.ReadAsync(cancellationToken))
                {
                    var vouNo = purTaxReader["VouNo"]?.ToString()?.Trim() ?? string.Empty;
                    var vouType = purTaxReader["VouType"]?.ToString()?.Trim() ?? "Purchase";
                    var billDate = purTaxReader["BillDate"] != DBNull.Value ? Convert.ToDateTime(purTaxReader["BillDate"]) : DateTime.Today;
                    var taxAmt = purTaxReader["TaxAmt"] != DBNull.Value ? Convert.ToDecimal(purTaxReader["TaxAmt"]) : 0m;
                    var cgstAmt = purTaxReader["CGSTAmt"] != DBNull.Value ? Convert.ToDecimal(purTaxReader["CGSTAmt"]) : 0m;
                    var sgstAmt = purTaxReader["SGSTAmt"] != DBNull.Value ? Convert.ToDecimal(purTaxReader["SGSTAmt"]) : 0m;
                    var igstAmt = purTaxReader["IGSTAmt"] != DBNull.Value ? Convert.ToDecimal(purTaxReader["IGSTAmt"]) : 0m;
                    var partyName = purTaxReader["AccountName"]?.ToString()?.Trim() ?? purTaxReader["CustomerName"]?.ToString()?.Trim() ?? string.Empty;
                    var inOut = purTaxReader["InOut"]?.ToString()?.Trim() ?? string.Empty;
                    if (string.IsNullOrEmpty(inOut) && !string.IsNullOrEmpty(partyName) && accountInOutMap.TryGetValue(partyName, out var mappedIo))
                    {
                        inOut = mappedIo;
                    }
                    var narration = purTaxReader["Narration"]?.ToString()?.Trim() ?? string.Empty;
                    if (string.IsNullOrEmpty(narration))
                    {
                        narration = purTaxReader["Remarks"]?.ToString()?.Trim() ?? string.Empty;
                    }

                    bool isOutSide = inOut.Contains("OUT", StringComparison.OrdinalIgnoreCase);

                    decimal effectiveIgst = 0m;
                    decimal effectiveCgst = 0m;
                    decimal effectiveSgst = 0m;

                    if (isOutSide)
                    {
                        effectiveIgst = igstAmt > 0 ? igstAmt : taxAmt;
                    }
                    else
                    {
                        effectiveCgst = cgstAmt > 0 ? cgstAmt : (taxAmt / 2m);
                        effectiveSgst = sgstAmt > 0 ? sgstAmt : (taxAmt / 2m);
                    }

                    bool isPurchaseReturn = vouType.Contains("Return", StringComparison.OrdinalIgnoreCase);
                    decimal debit = 0m;
                    decimal credit = 0m;
                    string head = string.IsNullOrWhiteSpace(partyName) ? "To Purchases" : $"To Purchases - {partyName}";

                    if (isGstAccount)
                    {
                        if (isPurchaseReturn)
                        {
                            credit = taxAmt;
                            head = string.IsNullOrWhiteSpace(partyName) ? "By Purchase Return" : $"By Purchase Return - {partyName}";
                        }
                        else
                        {
                            debit = taxAmt;
                        }
                    }
                    else if (isIgstAccount)
                    {
                        if (effectiveIgst <= 0) continue;
                        if (isPurchaseReturn)
                        {
                            credit = effectiveIgst;
                            head = string.IsNullOrWhiteSpace(partyName) ? "By Purchase Return" : $"By Purchase Return - {partyName}";
                        }
                        else
                        {
                            debit = effectiveIgst;
                        }
                    }
                    else if (isCgstAccount)
                    {
                        if (effectiveCgst <= 0) continue;
                        if (isPurchaseReturn)
                        {
                            credit = effectiveCgst;
                            head = string.IsNullOrWhiteSpace(partyName) ? "By Purchase Return" : $"By Purchase Return - {partyName}";
                        }
                        else
                        {
                            debit = effectiveCgst;
                        }
                    }
                    else if (isSgstAccount)
                    {
                        if (effectiveSgst <= 0) continue;
                        if (isPurchaseReturn)
                        {
                            credit = effectiveSgst;
                            head = string.IsNullOrWhiteSpace(partyName) ? "By Purchase Return" : $"By Purchase Return - {partyName}";
                        }
                        else
                        {
                            debit = effectiveSgst;
                        }
                    }

                    if (debit > 0 || credit > 0)
                    {
                        rawEntries.Add(new RawLedgerRecord
                        {
                            VouDate = billDate,
                            VouNo = vouNo,
                            VouType = vouType,
                            Head = head,
                            Debit = debit,
                            Credit = credit,
                            Narration = narration
                        });
                    }
                }
            }
            else if (isPurchaseAccount)
            {
                // When viewing Purchase Account (e.g. Purchases, Purchase Account)
                const string purAllSql = @"
                    SELECT [VouNo], [VouType], [BillDate], [AccountName], [CustomerName], [TaxableAmount], [TaxAmt], [NetAmt], [GrossAmt], [DiscountAllowed], [Narration], [Remarks]
                    FROM [Purchases]
                    WHERE ([BillCanceled] = 0 OR [BillCanceled] IS NULL)
                      AND ([Status] IS NULL OR [Status] <> 'Deleted')
                      AND [NetAmt] > 0";

                await using var purCmd = new OleDbCommand(purAllSql, connection);
                await using var purReader = await purCmd.ExecuteReaderAsync(cancellationToken);
                while (await purReader.ReadAsync(cancellationToken))
                {
                    var vouNo = purReader["VouNo"]?.ToString()?.Trim() ?? string.Empty;
                    var vouType = purReader["VouType"]?.ToString()?.Trim() ?? "Purchase";
                    var billDate = purReader["BillDate"] != DBNull.Value ? Convert.ToDateTime(purReader["BillDate"]) : DateTime.Today;
                    var partyName = purReader["AccountName"]?.ToString()?.Trim() ?? purReader["CustomerName"]?.ToString()?.Trim() ?? string.Empty;
                    var taxableAmt = purReader["TaxableAmount"] != DBNull.Value ? Convert.ToDecimal(purReader["TaxableAmount"]) : 0m;
                    var taxAmt = purReader["TaxAmt"] != DBNull.Value ? Convert.ToDecimal(purReader["TaxAmt"]) : 0m;
                    var netAmt = purReader["NetAmt"] != DBNull.Value ? Convert.ToDecimal(purReader["NetAmt"]) : 0m;
                    var grossAmt = purReader["GrossAmt"] != DBNull.Value ? Convert.ToDecimal(purReader["GrossAmt"]) : 0m;
                    var disAmt = purReader["DiscountAllowed"] != DBNull.Value ? Convert.ToDecimal(purReader["DiscountAllowed"]) : 0m;
                    var narration = purReader["Narration"]?.ToString()?.Trim() ?? string.Empty;
                    if (string.IsNullOrEmpty(narration))
                    {
                        narration = purReader["Remarks"]?.ToString()?.Trim() ?? string.Empty;
                    }

                    decimal purchaseAmt = taxableAmt > 0 ? taxableAmt : ((grossAmt - disAmt > 0) ? (grossAmt - disAmt) : ((netAmt > taxAmt) ? (netAmt - taxAmt) : netAmt));
                    bool isPurchaseReturn = vouType.Contains("Return", StringComparison.OrdinalIgnoreCase);

                    decimal debit = 0m;
                    decimal credit = 0m;
                    string head;

                    if (isPurchaseReturn)
                    {
                        credit = purchaseAmt;
                        head = string.IsNullOrWhiteSpace(partyName) ? "By Party" : $"By {partyName}";
                    }
                    else
                    {
                        debit = purchaseAmt;
                        head = string.IsNullOrWhiteSpace(partyName) ? "To Party" : $"To {partyName}";
                    }

                    rawEntries.Add(new RawLedgerRecord
                    {
                        VouDate = billDate,
                        VouNo = vouNo,
                        VouType = vouType,
                        Head = head,
                        Debit = debit,
                        Credit = credit,
                        Narration = narration
                    });
                }
            }
            else
            {
                // When viewing Party / Supplier Ledger
                const string purPartySql = @"
                    SELECT [VouNo], [VouType], [BillDate], [AccountName], [CustomerName], [NetAmt], [Narration], [Remarks]
                    FROM [Purchases]
                    WHERE ([BillCanceled] = 0 OR [BillCanceled] IS NULL)
                      AND ([Status] IS NULL OR [Status] <> 'Deleted')
                      AND ([AccountName] = @AccName OR [CustomerName] = @CustName)";

                await using var purPartyCmd = new OleDbCommand(purPartySql, connection);
                purPartyCmd.Parameters.Add("@AccName", OleDbType.VarWChar, 100).Value = accName;
                purPartyCmd.Parameters.Add("@CustName", OleDbType.VarWChar, 100).Value = accName;

                await using var purPartyReader = await purPartyCmd.ExecuteReaderAsync(cancellationToken);
                while (await purPartyReader.ReadAsync(cancellationToken))
                {
                    var vouNo = purPartyReader["VouNo"]?.ToString()?.Trim() ?? string.Empty;
                    var vouType = purPartyReader["VouType"]?.ToString()?.Trim() ?? "Purchase";
                    var billDate = purPartyReader["BillDate"] != DBNull.Value ? Convert.ToDateTime(purPartyReader["BillDate"]) : DateTime.Today;
                    var netAmt = purPartyReader["NetAmt"] != DBNull.Value ? Convert.ToDecimal(purPartyReader["NetAmt"]) : 0m;
                    var narration = purPartyReader["Narration"]?.ToString()?.Trim() ?? string.Empty;
                    if (string.IsNullOrEmpty(narration))
                    {
                        narration = purPartyReader["Remarks"]?.ToString()?.Trim() ?? string.Empty;
                    }

                    bool isPurchaseReturn = vouType.Contains("Return", StringComparison.OrdinalIgnoreCase);

                    decimal debit = 0m;
                    decimal credit = 0m;
                    string head;

                    if (isPurchaseReturn)
                    {
                        debit = netAmt;
                        head = "To Purchase Return";
                    }
                    else
                    {
                        credit = netAmt;
                        head = "By Purchases";
                    }

                    rawEntries.Add(new RawLedgerRecord
                    {
                        VouDate = billDate,
                        VouNo = vouNo,
                        VouType = vouType,
                        Head = head,
                        Debit = debit,
                        Credit = credit,
                        Narration = narration
                    });
                }
            }
        }
        catch { }

        // 3. Fetch Transactions from PaymentSubform linked with Bill
        try
        {
            const string paySql = @"
                SELECT p.[VouNo], p.[AccountName], p.[BankName], p.[HeadCategory], p.[Amount], p.[Narration], b.[BillDate], b.[VouType], b.[CustomerName], b.[AccountName] AS [BillAccountName]
                FROM [PaymentSubform] p
                INNER JOIN [Bill] b ON p.[VouNo] = b.[VouNo]
                WHERE (b.[BillCanceled] = 0 OR b.[BillCanceled] IS NULL)
                  AND (b.[Status] IS NULL OR b.[Status] <> 'Deleted')
                  AND (p.[AccountName] = @PayAccName OR p.[BankName] = @BankAccName OR b.[CustomerName] = @CustName OR b.[AccountName] = @BillAccName)";

            await using var cmd = new OleDbCommand(paySql, connection);
            cmd.Parameters.Add("@PayAccName", OleDbType.VarWChar, 100).Value = accName;
            cmd.Parameters.Add("@BankAccName", OleDbType.VarWChar, 100).Value = accName;
            cmd.Parameters.Add("@CustName", OleDbType.VarWChar, 100).Value = accName;
            cmd.Parameters.Add("@BillAccName", OleDbType.VarWChar, 100).Value = accName;

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var vouNo = reader["VouNo"]?.ToString()?.Trim() ?? string.Empty;
                var payAcc = reader["AccountName"]?.ToString()?.Trim() ?? string.Empty;
                var bankName = reader["BankName"]?.ToString()?.Trim() ?? string.Empty;
                var headCategory = reader["HeadCategory"]?.ToString()?.Trim() ?? string.Empty;
                var custName = reader["CustomerName"]?.ToString()?.Trim() ?? string.Empty;
                var billAccName = reader["BillAccountName"]?.ToString()?.Trim() ?? string.Empty;
                var rawAmount = reader["Amount"] != DBNull.Value ? Convert.ToDecimal(reader["Amount"]) : 0m;
                var narration = reader["Narration"]?.ToString()?.Trim() ?? string.Empty;
                var billDate = reader["BillDate"] != DBNull.Value ? Convert.ToDateTime(reader["BillDate"]) : DateTime.Today;
                var vouType = reader["VouType"]?.ToString()?.Trim() ?? "Payment";

                if (rawAmount == 0m) continue;

                // In Access/Tauras database, when a bill is created on credit, PaymentSubform contains
                // a record with AccountName = CustomerName (the party itself) and negative Amount = -NetAmt.
                // This represents the bill's credit/unpaid balance, NOT a payment transaction, and the bill
                // is already recorded in the party's ledger from the Bill table.
                // Actual payments made at bill time (Cash, Bank, Card) have positive Amount and payAcc != party.
                var partyName = !string.IsNullOrWhiteSpace(custName) ? custName : billAccName;
                if (rawAmount <= 0 || string.Equals(payAcc, partyName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                decimal absAmount = Math.Abs(rawAmount);

                if ((string.Equals(headCategory, "Credit Card", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(payAcc, "Credit Card", StringComparison.OrdinalIgnoreCase)) &&
                    !string.IsNullOrWhiteSpace(bankName))
                {
                    payAcc = bankName;
                }

                bool isSalesReturn = vouType.Contains("Return", StringComparison.OrdinalIgnoreCase) && vouType.Contains("Sales", StringComparison.OrdinalIgnoreCase);
                bool isPurchase = vouType.Contains("Purchase", StringComparison.OrdinalIgnoreCase) && !vouType.Contains("Return", StringComparison.OrdinalIgnoreCase);
                bool isPurchaseReturn = vouType.Contains("Purchase", StringComparison.OrdinalIgnoreCase) && vouType.Contains("Return", StringComparison.OrdinalIgnoreCase);

                if (string.Equals(payAcc, accName, StringComparison.OrdinalIgnoreCase))
                {
                    // Viewing the payment account (Cash, Bank, Card)
                    decimal debit = 0m;
                    decimal credit = 0m;
                    string head;

                    if (isSalesReturn)
                    {
                        credit = absAmount;
                        head = string.IsNullOrEmpty(partyName) ? "By Cash/Bank" : $"By {partyName}";
                    }
                    else if (isPurchase)
                    {
                        credit = absAmount;
                        head = string.IsNullOrEmpty(partyName) ? "By Party" : $"By {partyName}";
                    }
                    else if (isPurchaseReturn)
                    {
                        debit = absAmount;
                        head = string.IsNullOrEmpty(partyName) ? "To Party" : $"To {partyName}";
                    }
                    else
                    {
                        // Sales payment received into cash/bank
                        debit = absAmount;
                        head = string.IsNullOrEmpty(partyName) ? "To Sales A/c" : $"To {partyName}";
                    }

                    rawEntries.Add(new RawLedgerRecord
                    {
                        VouDate = billDate,
                        VouNo = vouNo,
                        VouType = $"Pmt ({vouType})",
                        Head = head,
                        Debit = debit,
                        Credit = credit,
                        Narration = narration
                    });
                }
                else if (string.Equals(partyName, accName, StringComparison.OrdinalIgnoreCase))
                {
                    // Viewing the party/customer/supplier account when an immediate payment (Cash/Bank) was made on the bill
                    decimal debit = 0m;
                    decimal credit = 0m;
                    string head;

                    if (isPurchase)
                    {
                        // Supplier was paid at time of purchase -> Debits the Supplier ledger
                        debit = absAmount;
                        head = string.IsNullOrEmpty(payAcc) ? "To Cash" : $"To {payAcc}";
                    }
                    else if (isSalesReturn)
                    {
                        // Customer received refund at time of return -> Debits Customer ledger
                        debit = absAmount;
                        head = string.IsNullOrEmpty(payAcc) ? "To Cash" : $"To {payAcc}";
                    }
                    else if (isPurchaseReturn)
                    {
                        // Supplier refunded money -> Credits Supplier ledger
                        credit = absAmount;
                        head = string.IsNullOrEmpty(payAcc) ? "By Cash" : $"By {payAcc}";
                    }
                    else
                    {
                        // Customer paid at time of sales invoice -> Credits Customer ledger
                        credit = absAmount;
                        head = string.IsNullOrEmpty(payAcc) ? "By Cash" : $"By {payAcc}";
                    }

                    rawEntries.Add(new RawLedgerRecord
                    {
                        VouDate = billDate,
                        VouNo = vouNo,
                        VouType = $"Pmt ({vouType})",
                        Head = head,
                        Debit = debit,
                        Credit = credit,
                        Narration = narration
                    });
                }
            }
        }
        catch { }

        // 3b. Fetch Transactions from PaymentSubform linked with Purchases
        try
        {
            const string purPaySql = @"
                SELECT p.[VouNo], p.[AccountName], p.[BankName], p.[HeadCategory], p.[Amount], p.[Narration], pur.[BillDate], pur.[VouType], pur.[AccountName] AS [PartyName], pur.[CustomerName]
                FROM [PaymentSubform] p
                INNER JOIN [Purchases] pur ON p.[VouNo] = pur.[VouNo]
                WHERE (pur.[BillCanceled] = 0 OR pur.[BillCanceled] IS NULL)
                  AND (pur.[Status] IS NULL OR pur.[Status] <> 'Deleted')
                  AND (p.[AccountName] = @PayAccName OR p.[BankName] = @BankAccName OR pur.[AccountName] = @PartyAccName OR pur.[CustomerName] = @CustAccName)";

            await using var purPayCmd = new OleDbCommand(purPaySql, connection);
            purPayCmd.Parameters.Add("@PayAccName", OleDbType.VarWChar, 100).Value = accName;
            purPayCmd.Parameters.Add("@BankAccName", OleDbType.VarWChar, 100).Value = accName;
            purPayCmd.Parameters.Add("@PartyAccName", OleDbType.VarWChar, 100).Value = accName;
            purPayCmd.Parameters.Add("@CustAccName", OleDbType.VarWChar, 100).Value = accName;

            await using var purPayReader = await purPayCmd.ExecuteReaderAsync(cancellationToken);
            while (await purPayReader.ReadAsync(cancellationToken))
            {
                var vouNo = purPayReader["VouNo"]?.ToString()?.Trim() ?? string.Empty;
                var payAcc = purPayReader["AccountName"]?.ToString()?.Trim() ?? string.Empty;
                var bankName = purPayReader["BankName"]?.ToString()?.Trim() ?? string.Empty;
                var headCategory = purPayReader["HeadCategory"]?.ToString()?.Trim() ?? string.Empty;
                var partyName = purPayReader["PartyName"]?.ToString()?.Trim() ?? purPayReader["CustomerName"]?.ToString()?.Trim() ?? string.Empty;
                var rawAmount = purPayReader["Amount"] != DBNull.Value ? Convert.ToDecimal(purPayReader["Amount"]) : 0m;
                var narration = purPayReader["Narration"]?.ToString()?.Trim() ?? string.Empty;
                var billDate = purPayReader["BillDate"] != DBNull.Value ? Convert.ToDateTime(purPayReader["BillDate"]) : DateTime.Today;
                var vouType = purPayReader["VouType"]?.ToString()?.Trim() ?? "Purchase";

                if (rawAmount == 0m) continue;

                if (rawAmount <= 0 || string.Equals(payAcc, partyName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                decimal absAmount = Math.Abs(rawAmount);

                if ((string.Equals(headCategory, "Credit Card", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(payAcc, "Credit Card", StringComparison.OrdinalIgnoreCase)) &&
                    !string.IsNullOrWhiteSpace(bankName))
                {
                    payAcc = bankName;
                }

                if (string.Equals(payAcc, accName, StringComparison.OrdinalIgnoreCase))
                {
                    // Payment account (Cash/Bank) paid out to supplier
                    rawEntries.Add(new RawLedgerRecord
                    {
                        VouDate = billDate,
                        VouNo = vouNo,
                        VouType = $"Pmt ({vouType})",
                        Head = string.IsNullOrEmpty(partyName) ? "By Party" : $"By {partyName}",
                        Debit = 0m,
                        Credit = absAmount,
                        Narration = narration
                    });
                }
                else if (string.Equals(partyName, accName, StringComparison.OrdinalIgnoreCase))
                {
                    // Supplier was paid at time of purchase -> Debits the Supplier ledger
                    rawEntries.Add(new RawLedgerRecord
                    {
                        VouDate = billDate,
                        VouNo = vouNo,
                        VouType = $"Pmt ({vouType})",
                        Head = string.IsNullOrEmpty(payAcc) ? "To Cash" : $"To {payAcc}",
                        Debit = absAmount,
                        Credit = 0m,
                        Narration = narration
                    });
                }
            }
        }
        catch { }

        // 4. Fetch Transactions from PurchaseE (Purchases & Expenses)
        try
        {
            const string purSql = @"
                SELECT [VouNo], [VouType], [BillDate], [AccountName], [Head], [NetAmt], [Narration], [Status]
                FROM [PurchaseE]
                WHERE ([Status] IS NULL OR [Status] <> 'Deleted')
                  AND ([AccountName] = @AccName OR [Head] = @HeadName)";

            await using var cmd = new OleDbCommand(purSql, connection);
            cmd.Parameters.Add("@AccName", OleDbType.VarWChar, 100).Value = accName;
            cmd.Parameters.Add("@HeadName", OleDbType.VarWChar, 100).Value = accName;

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var vouNo = reader["VouNo"]?.ToString()?.Trim() ?? string.Empty;
                var vouType = reader["VouType"]?.ToString()?.Trim() ?? "PurchaseE";
                var billDate = reader["BillDate"] != DBNull.Value ? Convert.ToDateTime(reader["BillDate"]) : DateTime.Today;
                var partyAcc = reader["AccountName"]?.ToString()?.Trim() ?? string.Empty;
                var headAcc = reader["Head"]?.ToString()?.Trim() ?? string.Empty;
                var netAmt = reader["NetAmt"] != DBNull.Value ? Convert.ToDecimal(reader["NetAmt"]) : 0m;
                var narration = reader["Narration"]?.ToString()?.Trim() ?? string.Empty;

                if (string.Equals(partyAcc, accName, StringComparison.OrdinalIgnoreCase))
                {
                    // Party credited with purchase / expense amount
                    rawEntries.Add(new RawLedgerRecord
                    {
                        VouDate = billDate,
                        VouNo = vouNo,
                        VouType = vouType,
                        Head = string.IsNullOrEmpty(headAcc) ? "Purchases Expenses" : headAcc,
                        Debit = 0m,
                        Credit = netAmt,
                        Narration = narration
                    });
                }
                else if (string.Equals(headAcc, accName, StringComparison.OrdinalIgnoreCase))
                {
                    // Expense head debited with purchase / expense amount
                    rawEntries.Add(new RawLedgerRecord
                    {
                        VouDate = billDate,
                        VouNo = vouNo,
                        VouType = vouType,
                        Head = partyAcc,
                        Debit = netAmt,
                        Credit = 0m,
                        Narration = narration
                    });
                }
            }
        }
        catch { }

        // 5. Fetch Transactions from RECIEPTS (Vouchers: Payments, Receipts, Journal, Scheme Entry)
        try
        {
            const string recSql = @"
                SELECT [VouNo], [VouDate], [VouType], [Narration], [DrAccount], [DrAmt], [CrAccount], [CrAmt], [ChequeNo], [Status]
                FROM [RECIEPTS]
                WHERE ([Status] IS NULL OR [Status] <> 'Deleted')
                  AND ([DrAccount] = @DrAcc OR [CrAccount] = @CrAcc)";

            await using var cmd = new OleDbCommand(recSql, connection);
            cmd.Parameters.Add("@DrAcc", OleDbType.VarWChar, 100).Value = accName;
            cmd.Parameters.Add("@CrAcc", OleDbType.VarWChar, 100).Value = accName;

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var vouNo = reader["VouNo"]?.ToString()?.Trim() ?? string.Empty;
                var vouDate = reader["VouDate"] != DBNull.Value ? Convert.ToDateTime(reader["VouDate"]) : DateTime.Today;
                var vouType = reader["VouType"]?.ToString()?.Trim() ?? "Voucher";
                var drAcc = reader["DrAccount"]?.ToString()?.Trim() ?? string.Empty;
                var crAcc = reader["CrAccount"]?.ToString()?.Trim() ?? string.Empty;
                var drAmt = reader["DrAmt"] != DBNull.Value ? Convert.ToDecimal(reader["DrAmt"]) : 0m;
                var crAmt = reader["CrAmt"] != DBNull.Value ? Convert.ToDecimal(reader["CrAmt"]) : 0m;
                var narration = reader["Narration"]?.ToString()?.Trim() ?? string.Empty;
                var chequeNo = reader["ChequeNo"]?.ToString()?.Trim() ?? string.Empty;

                if (string.Equals(drAcc, accName, StringComparison.OrdinalIgnoreCase))
                {
                    rawEntries.Add(new RawLedgerRecord
                    {
                        VouDate = vouDate,
                        VouNo = vouNo,
                        VouType = VoucherType.GetDisplayName(vouType),
                        Head = crAcc,
                        Debit = drAmt > 0 ? drAmt : crAmt,
                        Credit = 0m,
                        Narration = narration,
                        ChequeNo = chequeNo
                    });
                }
                else if (string.Equals(crAcc, accName, StringComparison.OrdinalIgnoreCase))
                {
                    rawEntries.Add(new RawLedgerRecord
                    {
                        VouDate = vouDate,
                        VouNo = vouNo,
                        VouType = VoucherType.GetDisplayName(vouType),
                        Head = drAcc,
                        Debit = 0m,
                        Credit = crAmt > 0 ? crAmt : drAmt,
                        Narration = narration,
                        ChequeNo = chequeNo
                    });
                }
            }
        }
        catch { }

        // Compute Opening Balance at fromDate:
        // Base start date of database opening is 01/04/2025 (or account.EntryDate baseline).
        // Transactions prior to baseDate (such as historical SchemeEntries from 2021-2024) are ignored since they are already incorporated into AccountCreation.OpAmt1.
        // Transactions between baseDate and fromDate are accumulated to calculate the exact opening balance as on fromDate.
        DateTime baseDate = new DateTime(2025, 4, 1);
        if (account != null && account.EntryDate.Year >= 2000)
        {
            var accFyYear = account.EntryDate.Month >= 4 ? account.EntryDate.Year : account.EntryDate.Year - 1;
            baseDate = new DateTime(Math.Min(2025, accFyYear), 4, 1);
        }

        var preDebits = rawEntries.Where(r => r.VouDate.Date >= baseDate.Date && r.VouDate.Date < fromDate.Date).Sum(r => r.Debit);
        var preCredits = rawEntries.Where(r => r.VouDate.Date >= baseDate.Date && r.VouDate.Date < fromDate.Date).Sum(r => r.Credit);
        var openingBalanceValue = initialOpAmount + preDebits - preCredits;

        var result = new List<LedgerEntry>();

        // Add Opening Balance Row
        result.Add(new LedgerEntry
        {
            VouDate = fromDate,
            VouNo = "-",
            VouType = "Opening Balance",
            Head = "By Opening Balance",
            Debit = openingBalanceValue > 0 ? openingBalanceValue : 0m,
            Credit = openingBalanceValue < 0 ? Math.Abs(openingBalanceValue) : 0m,
            Balance = Math.Abs(openingBalanceValue),
            BalanceType = openingBalanceValue >= 0 ? "Dr" : "Cr",
            Narration = "Opening Balance as on " + fromDate.ToString("dd/MM/yyyy")
        });

        // Sort period entries chronologically
        var periodRecords = rawEntries
            .Where(r => r.VouDate.Date >= fromDate.Date && r.VouDate.Date <= toDate.Date)
            .OrderBy(r => r.VouDate)
            .ThenBy(r => r.VouNo)
            .ToList();

        decimal runningBalance = openingBalanceValue;

        foreach (var r in periodRecords)
        {
            runningBalance += r.Debit - r.Credit;

            result.Add(new LedgerEntry
            {
                VouDate = r.VouDate,
                VouNo = r.VouNo,
                VouType = r.VouType,
                Head = r.Head,
                Debit = r.Debit,
                Credit = r.Credit,
                Balance = Math.Abs(runningBalance),
                BalanceType = runningBalance >= 0 ? "Dr" : "Cr",
                Narration = r.Narration,
                ChequeNo = r.ChequeNo
            });
        }

        return result;
    }

    public async Task<IReadOnlyList<LedgerEntry>> GetLedgerInWeightAsync(LedgerReportFilter filter, CancellationToken cancellationToken = default)
    {
        await AccessStaffCommissionLedgerSchemaMigrator.EnsureSchemaAsync(_connectionString, cancellationToken);

        if (string.IsNullOrWhiteSpace(filter.AccountName))
            return Array.Empty<LedgerEntry>();

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        Account? account = null;
        const string accSql = "SELECT TOP 1 * FROM AccountCreation WHERE [AccountName] = @AccountName";
        await using (var cmd = new OleDbCommand(accSql, connection))
        {
            cmd.Parameters.Add("@AccountName", OleDbType.VarWChar, 100).Value = filter.AccountName;
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                account = MapAccount(reader);
            }
        }

        return await GetLedgerInWeightInternalAsync(connection, account, filter, cancellationToken);
    }

    private static async Task<List<LedgerEntry>> GetLedgerInWeightInternalAsync(
        OleDbConnection connection,
        Account? account,
        LedgerReportFilter filter,
        CancellationToken cancellationToken)
    {
        var rawEntries = new List<RawLedgerRecord>();
        var fromDate = filter.FromDate ?? new DateTime(DateTime.Today.Year, 4, 1);
        var toDate = filter.ToDate ?? DateTime.Today;
        var accName = filter.AccountName.Trim();

        decimal initialOpWeight = account?.OpGoldWeight ?? 0m;

        // Load SalesVouType mapping
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

        var matchedSalesVouTypes = new List<(string VouType, string HeadCategory)>();
        foreach (var kvp in salesVouTypeMap)
        {
            var vt = kvp.Key;
            var sa = kvp.Value.SalesAccount;
            var hc = kvp.Value.HeadCategory;

            if (string.Equals(sa, accName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(vt, accName, StringComparison.OrdinalIgnoreCase))
            {
                matchedSalesVouTypes.Add((vt, hc));
            }
            else if (string.Equals(accName, "Purchases", StringComparison.OrdinalIgnoreCase) &&
                     string.Equals(hc, "Purchases", StringComparison.OrdinalIgnoreCase))
            {
                matchedSalesVouTypes.Add((vt, hc));
            }
            else if (string.Equals(accName, "Sales", StringComparison.OrdinalIgnoreCase) &&
                     string.Equals(hc, "Sales", StringComparison.OrdinalIgnoreCase))
            {
                matchedSalesVouTypes.Add((vt, hc));
            }
        }

        // 1. Fetch Weight transactions from Karighar Issue (MetalIssued)
        try
        {
            const string issueSql = @"
                SELECT [IssueNo], [IssueDate], [ArtisianName], [ItemType], [WeightIssued], [Purity], [Note], [Status]
                FROM [MetalIssued]
                WHERE ([Status] IS NULL OR [Status] <> 'Deleted')
                  AND [ArtisianName] = @ArtName";

            await using var cmd = new OleDbCommand(issueSql, connection);
            cmd.Parameters.Add("@ArtName", OleDbType.VarWChar, 100).Value = accName;

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var issueNo = reader["IssueNo"]?.ToString()?.Trim() ?? string.Empty;
                var issueDate = reader["IssueDate"] != DBNull.Value ? Convert.ToDateTime(reader["IssueDate"]) : DateTime.Today;
                var itemType = reader["ItemType"]?.ToString()?.Trim() ?? "GOLD";
                var purity = reader["Purity"]?.ToString()?.Trim() ?? string.Empty;
                var weightIssued = reader["WeightIssued"] != DBNull.Value ? Convert.ToDecimal(reader["WeightIssued"]) : 0m;
                var note = reader["Note"]?.ToString()?.Trim() ?? string.Empty;

                rawEntries.Add(new RawLedgerRecord
                {
                    VouDate = issueDate,
                    VouNo = issueNo,
                    VouType = "Karighar Issue",
                    Head = itemType,
                    ItemType = itemType,
                    Purity = purity,
                    WeightDebit = weightIssued,
                    WeightCredit = 0m,
                    Narration = note
                });
            }
        }
        catch { }

        // 2. Fetch Weight transactions from Karighar Receive (MetalReceived)
        try
        {
            const string recSql = @"
                SELECT [RecNo], [RecDate], [PartyName], [ItemType], [WeightRecd], [MakingCharges], [Note], [Status]
                FROM [MetalReceived]
                WHERE ([Status] IS NULL OR [Status] <> 'Deleted')
                  AND [PartyName] = @PartyName";

            await using var cmd = new OleDbCommand(recSql, connection);
            cmd.Parameters.Add("@PartyName", OleDbType.VarWChar, 100).Value = accName;

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var recNo = reader["RecNo"]?.ToString()?.Trim() ?? string.Empty;
                var recDate = reader["RecDate"] != DBNull.Value ? Convert.ToDateTime(reader["RecDate"]) : DateTime.Today;
                var itemType = reader["ItemType"]?.ToString()?.Trim() ?? "GOLD";
                var weightRecd = reader["WeightRecd"] != DBNull.Value ? Convert.ToDecimal(reader["WeightRecd"]) : 0m;
                var note = reader["Note"]?.ToString()?.Trim() ?? string.Empty;

                rawEntries.Add(new RawLedgerRecord
                {
                    VouDate = recDate,
                    VouNo = recNo,
                    VouType = "Karighar Receive",
                    Head = itemType,
                    ItemType = itemType,
                    WeightDebit = 0m,
                    WeightCredit = weightRecd,
                    Narration = note
                });
            }
        }
        catch { }

        // 3. Fetch Old Gold Weight payments from PaymentSubform linked with Bill
        try
        {
            const string oldGoldSql = @"
                SELECT p.[VouNo], p.[AccountName], p.[ItemType], p.[NtWt], p.[GrWt], p.[Narration], b.[BillDate], b.[CustomerName]
                FROM [PaymentSubform] p
                INNER JOIN [Bill] b ON p.[VouNo] = b.[VouNo]
                WHERE (b.[BillCanceled] = 0 OR b.[BillCanceled] IS NULL)
                  AND (b.[Status] IS NULL OR b.[Status] <> 'Deleted')
                  AND (b.[CustomerName] = @CustName OR p.[AccountName] = @PayAcc)
                  AND (p.[NtWt] > 0 OR p.[GrWt] > 0)";

            await using var cmd = new OleDbCommand(oldGoldSql, connection);
            cmd.Parameters.Add("@CustName", OleDbType.VarWChar, 100).Value = accName;
            cmd.Parameters.Add("@PayAcc", OleDbType.VarWChar, 100).Value = accName;

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var vouNo = reader["VouNo"]?.ToString()?.Trim() ?? string.Empty;
                var custName = reader["CustomerName"]?.ToString()?.Trim() ?? string.Empty;
                var payAcc = reader["AccountName"]?.ToString()?.Trim() ?? string.Empty;
                var itemType = reader["ItemType"]?.ToString()?.Trim() ?? "OLD GOLD";
                var ntWt = reader["NtWt"] != DBNull.Value ? Convert.ToDecimal(reader["NtWt"]) : 0m;
                if (ntWt == 0m && reader["GrWt"] != DBNull.Value)
                {
                    ntWt = Convert.ToDecimal(reader["GrWt"]);
                }
                var narration = reader["Narration"]?.ToString()?.Trim() ?? string.Empty;
                var billDate = reader["BillDate"] != DBNull.Value ? Convert.ToDateTime(reader["BillDate"]) : DateTime.Today;

                if (string.Equals(custName, accName, StringComparison.OrdinalIgnoreCase))
                {
                    // Party provided old metal (Credit to party weight)
                    rawEntries.Add(new RawLedgerRecord
                    {
                        VouDate = billDate,
                        VouNo = vouNo,
                        VouType = "Old Metal Received",
                        Head = string.IsNullOrEmpty(itemType) ? "Old Gold" : itemType,
                        ItemType = itemType,
                        WeightDebit = 0m,
                        WeightCredit = ntWt,
                        Narration = narration
                    });
                }
                else if (string.Equals(payAcc, accName, StringComparison.OrdinalIgnoreCase))
                {
                    // Old gold metal account debited with received metal
                    rawEntries.Add(new RawLedgerRecord
                    {
                        VouDate = billDate,
                        VouNo = vouNo,
                        VouType = "Old Metal Stock",
                        Head = custName,
                        ItemType = itemType,
                        WeightDebit = ntWt,
                        WeightCredit = 0m,
                        Narration = narration
                    });
                }
            }
        }
        catch { }

        // 4. Fetch weight entries from Bill for Purchases/Sales ledgers (e.g. OLD GOLD PURCHASES, NEW GOLD ORNAMENTS PURCHASE, SALE GOLD)
        try
        {
            if (matchedSalesVouTypes.Count > 0)
            {
                var matchedTypesSet = new HashSet<string>(matchedSalesVouTypes.Select(m => m.VouType), StringComparer.OrdinalIgnoreCase);
                const string billWtSql = @"
                    SELECT [VouNo], [VouType], [BillDate], [CustomerName], [Item], [ItemName], [GrWt], [NtWt], [Narration], [Remarks], [BillCanceled], [Status]
                    FROM [Bill]
                    WHERE ([BillCanceled] = 0 OR [BillCanceled] IS NULL)
                      AND ([Status] IS NULL OR [Status] <> 'Deleted')
                      AND ([NtWt] > 0 OR [GrWt] > 0)";

                await using var cmd = new OleDbCommand(billWtSql, connection);
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var vouType = reader["VouType"]?.ToString()?.Trim() ?? string.Empty;
                    if (!matchedTypesSet.Contains(vouType)) continue;

                    var vouNo = reader["VouNo"]?.ToString()?.Trim() ?? string.Empty;
                    var billDate = reader["BillDate"] != DBNull.Value ? Convert.ToDateTime(reader["BillDate"]) : DateTime.Today;
                    var custName = reader["CustomerName"]?.ToString()?.Trim() ?? string.Empty;
                    var item = reader["ItemName"]?.ToString()?.Trim() ?? reader["Item"]?.ToString()?.Trim() ?? "GOLD";
                    var ntWt = reader["NtWt"] != DBNull.Value ? Convert.ToDecimal(reader["NtWt"]) : 0m;
                    if (ntWt == 0m && reader["GrWt"] != DBNull.Value)
                    {
                        ntWt = Convert.ToDecimal(reader["GrWt"]);
                    }
                    if (ntWt <= 0) continue;

                    var narration = reader["Narration"]?.ToString()?.Trim() ?? string.Empty;
                    if (string.IsNullOrEmpty(narration))
                    {
                        narration = reader["Remarks"]?.ToString()?.Trim() ?? string.Empty;
                    }

                    string headCat = salesVouTypeMap.TryGetValue(vouType, out var svt) ? svt.HeadCategory : string.Empty;
                    bool isPurchase = headCat.Equals("Purchases", StringComparison.OrdinalIgnoreCase) || (vouType.Contains("Purchase", StringComparison.OrdinalIgnoreCase) && !vouType.Contains("Return", StringComparison.OrdinalIgnoreCase));
                    bool isPurchaseReturn = headCat.Equals("Purchase Return", StringComparison.OrdinalIgnoreCase) || (vouType.Contains("Purchase", StringComparison.OrdinalIgnoreCase) && vouType.Contains("Return", StringComparison.OrdinalIgnoreCase));
                    bool isSalesReturn = headCat.Equals("Sales Return", StringComparison.OrdinalIgnoreCase) || (vouType.Contains("Return", StringComparison.OrdinalIgnoreCase) && vouType.Contains("Sales", StringComparison.OrdinalIgnoreCase));

                    decimal wtDebit = 0m;
                    decimal wtCredit = 0m;
                    string head;

                    if (isPurchase || isSalesReturn)
                    {
                        wtDebit = ntWt;
                        head = string.IsNullOrWhiteSpace(custName) ? "To Stock" : $"To {custName}";
                    }
                    else
                    {
                        wtCredit = ntWt;
                        head = string.IsNullOrWhiteSpace(custName) ? "By Stock" : $"By {custName}";
                    }

                    rawEntries.Add(new RawLedgerRecord
                    {
                        VouDate = billDate,
                        VouNo = vouNo,
                        VouType = vouType,
                        Head = head,
                        ItemType = item,
                        WeightDebit = wtDebit,
                        WeightCredit = wtCredit,
                        Narration = narration
                    });
                }
            }
        }
        catch { }

        // 4b. Fetch weight entries from Purchases table
        try
        {
            bool isPurchaseAccount = string.Equals(accName, "Purchases", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(accName, "PURCHASE", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(accName, "PURCHASE ACCOUNT", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(accName, "PURCHASE ACCOUNTS", StringComparison.OrdinalIgnoreCase) ||
                                     matchedSalesVouTypes.Any(m => m.HeadCategory.Equals("Purchases", StringComparison.OrdinalIgnoreCase));

            const string purWtSql = @"
                SELECT [VouNo], [VouType], [BillDate], [AccountName], [CustomerName], [GrWt], [NtWt], [Narration], [Remarks]
                FROM [Purchases]
                WHERE ([BillCanceled] = 0 OR [BillCanceled] IS NULL)
                  AND ([Status] IS NULL OR [Status] <> 'Deleted')
                  AND ([NtWt] > 0 OR [GrWt] > 0)";

            await using var purWtCmd = new OleDbCommand(purWtSql, connection);
            await using var purWtReader = await purWtCmd.ExecuteReaderAsync(cancellationToken);
            while (await purWtReader.ReadAsync(cancellationToken))
            {
                var vouNo = purWtReader["VouNo"]?.ToString()?.Trim() ?? string.Empty;
                var vouType = purWtReader["VouType"]?.ToString()?.Trim() ?? "Purchase";
                var billDate = purWtReader["BillDate"] != DBNull.Value ? Convert.ToDateTime(purWtReader["BillDate"]) : DateTime.Today;
                var partyName = purWtReader["AccountName"]?.ToString()?.Trim() ?? purWtReader["CustomerName"]?.ToString()?.Trim() ?? string.Empty;
                var ntWt = purWtReader["NtWt"] != DBNull.Value ? Convert.ToDecimal(purWtReader["NtWt"]) : 0m;
                if (ntWt == 0m && purWtReader["GrWt"] != DBNull.Value)
                {
                    ntWt = Convert.ToDecimal(purWtReader["GrWt"]);
                }
                if (ntWt <= 0) continue;

                var narration = purWtReader["Narration"]?.ToString()?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(narration))
                {
                    narration = purWtReader["Remarks"]?.ToString()?.Trim() ?? string.Empty;
                }

                bool isPurchaseReturn = vouType.Contains("Return", StringComparison.OrdinalIgnoreCase);

                if (isPurchaseAccount)
                {
                    rawEntries.Add(new RawLedgerRecord
                    {
                        VouDate = billDate,
                        VouNo = vouNo,
                        VouType = vouType,
                        Head = string.IsNullOrWhiteSpace(partyName) ? "To Stock" : $"To {partyName}",
                        ItemType = "GOLD",
                        WeightDebit = isPurchaseReturn ? 0m : ntWt,
                        WeightCredit = isPurchaseReturn ? ntWt : 0m,
                        Narration = narration
                    });
                }
                else if (string.Equals(partyName, accName, StringComparison.OrdinalIgnoreCase))
                {
                    rawEntries.Add(new RawLedgerRecord
                    {
                        VouDate = billDate,
                        VouNo = vouNo,
                        VouType = vouType,
                        Head = isPurchaseReturn ? "To Purchase Return" : "By Purchases",
                        ItemType = "GOLD",
                        WeightDebit = isPurchaseReturn ? ntWt : 0m,
                        WeightCredit = isPurchaseReturn ? 0m : ntWt,
                        Narration = narration
                    });
                }
            }
        }
        catch { }

        // Compute pre-date opening weight:
        DateTime baseDate = new DateTime(2025, 4, 1);
        if (account != null && account.EntryDate.Year >= 2000)
        {
            var accFyYear = account.EntryDate.Month >= 4 ? account.EntryDate.Year : account.EntryDate.Year - 1;
            baseDate = new DateTime(Math.Min(2025, accFyYear), 4, 1);
        }

        var preDebits = rawEntries.Where(r => r.VouDate.Date >= baseDate.Date && r.VouDate.Date < fromDate.Date).Sum(r => r.WeightDebit);
        var preCredits = rawEntries.Where(r => r.VouDate.Date >= baseDate.Date && r.VouDate.Date < fromDate.Date).Sum(r => r.WeightCredit);
        var openingWeightValue = initialOpWeight + preDebits - preCredits;

        var result = new List<LedgerEntry>();

        // Opening Weight Row
        result.Add(new LedgerEntry
        {
            VouDate = fromDate,
            VouNo = "-",
            VouType = "Opening Balance",
            Head = "Opening Weight",
            WeightDebit = openingWeightValue > 0 ? openingWeightValue : 0m,
            WeightCredit = openingWeightValue < 0 ? Math.Abs(openingWeightValue) : 0m,
            WeightBalance = Math.Abs(openingWeightValue),
            WeightBalanceType = openingWeightValue >= 0 ? "Dr" : "Cr",
            Narration = "Opening Weight as on " + fromDate.ToString("dd/MM/yyyy")
        });

        var periodRecords = rawEntries
            .Where(r => r.VouDate.Date >= fromDate.Date && r.VouDate.Date <= toDate.Date)
            .OrderBy(r => r.VouDate)
            .ThenBy(r => r.VouNo)
            .ToList();

        decimal runningWeight = openingWeightValue;

        foreach (var r in periodRecords)
        {
            runningWeight += r.WeightDebit - r.WeightCredit;

            result.Add(new LedgerEntry
            {
                VouDate = r.VouDate,
                VouNo = r.VouNo,
                VouType = r.VouType,
                Head = r.Head,
                ItemType = r.ItemType,
                Purity = r.Purity,
                WeightDebit = r.WeightDebit,
                WeightCredit = r.WeightCredit,
                WeightBalance = Math.Abs(runningWeight),
                WeightBalanceType = runningWeight >= 0 ? "Dr" : "Cr",
                Narration = r.Narration
            });
        }

        return result;
    }

    public async Task<IReadOnlyList<LedgerNoteBalanceItem>> GetNoteWiseBalanceAsync(LedgerReportFilter filter, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filter.AccountName))
            return Array.Empty<LedgerNoteBalanceItem>();

        var list = new List<LedgerNoteBalanceItem>();
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var accName = filter.AccountName.Trim();

        try
        {
            // 1. Fetch Bills for Customer
            const string billSql = @"
                SELECT [VouNo], [VouType], [BillDate], [NetAmt], [Narration]
                FROM [Bill]
                WHERE ([BillCanceled] = 0 OR [BillCanceled] IS NULL)
                  AND ([Status] IS NULL OR [Status] <> 'Deleted')
                  AND ([CustomerName] = @CustName OR [AccountName] = @AccName)
                ORDER BY [BillDate] DESC, [VouNo] DESC";

            var bills = new List<(string VouNo, string VouType, DateTime BillDate, decimal NetAmt, string Narration)>();

            await using (var cmd = new OleDbCommand(billSql, connection))
            {
                cmd.Parameters.Add("@CustName", OleDbType.VarWChar, 100).Value = accName;
                cmd.Parameters.Add("@AccName", OleDbType.VarWChar, 100).Value = accName;

                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    bills.Add((
                        reader["VouNo"]?.ToString()?.Trim() ?? string.Empty,
                        reader["VouType"]?.ToString()?.Trim() ?? "Invoice",
                        reader["BillDate"] != DBNull.Value ? Convert.ToDateTime(reader["BillDate"]) : DateTime.Today,
                        reader["NetAmt"] != DBNull.Value ? Convert.ToDecimal(reader["NetAmt"]) : 0m,
                        reader["Narration"]?.ToString()?.Trim() ?? string.Empty
                    ));
                }
            }

            // 1b. Fetch Purchases for this account
            try
            {
                const string purSql = @"
                    SELECT [VouNo], [VouType], [BillDate], [NetAmt], [Narration], [Remarks]
                    FROM [Purchases]
                    WHERE ([BillCanceled] = 0 OR [BillCanceled] IS NULL)
                      AND ([Status] IS NULL OR [Status] <> 'Deleted')
                      AND ([CustomerName] = @CustName OR [AccountName] = @AccName)
                    ORDER BY [BillDate] DESC, [VouNo] DESC";

                await using var purCmd = new OleDbCommand(purSql, connection);
                purCmd.Parameters.Add("@CustName", OleDbType.VarWChar, 100).Value = accName;
                purCmd.Parameters.Add("@AccName", OleDbType.VarWChar, 100).Value = accName;

                await using var purReader = await purCmd.ExecuteReaderAsync(cancellationToken);
                while (await purReader.ReadAsync(cancellationToken))
                {
                    var nar = purReader["Narration"]?.ToString()?.Trim() ?? string.Empty;
                    if (string.IsNullOrEmpty(nar))
                    {
                        nar = purReader["Remarks"]?.ToString()?.Trim() ?? string.Empty;
                    }

                    bills.Add((
                        purReader["VouNo"]?.ToString()?.Trim() ?? string.Empty,
                        purReader["VouType"]?.ToString()?.Trim() ?? "Purchase",
                        purReader["BillDate"] != DBNull.Value ? Convert.ToDateTime(purReader["BillDate"]) : DateTime.Today,
                        purReader["NetAmt"] != DBNull.Value ? Convert.ToDecimal(purReader["NetAmt"]) : 0m,
                        nar
                    ));
                }
            }
            catch { }

            // 2. Fetch Payments grouped by VouNo
            var paymentsByVou = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            const string paySql = @"
                SELECT p.[VouNo], SUM(p.[Amount]) AS TotalPaid
                FROM [PaymentSubform] p
                WHERE p.[Amount] > 0
                GROUP BY p.[VouNo]";

            try
            {
                await using var cmd = new OleDbCommand(paySql, connection);
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var vNo = reader["VouNo"]?.ToString()?.Trim() ?? string.Empty;
                    var paid = reader["TotalPaid"] != DBNull.Value ? Convert.ToDecimal(reader["TotalPaid"]) : 0m;
                    paymentsByVou[vNo] = paid;
                }
            }
            catch { }

            // 3. Build NoteWise balance items
            foreach (var b in bills)
            {
                paymentsByVou.TryGetValue(b.VouNo, out var paidAmount);
                var pending = b.NetAmt - paidAmount;
                var days = (int)(DateTime.Today - b.BillDate.Date).TotalDays;

                string status;
                if (pending <= 0)
                {
                    status = "Paid";
                }
                else if (paidAmount > 0)
                {
                    status = "Partial";
                }
                else
                {
                    status = days > 30 ? "Overdue" : "Pending";
                }

                list.Add(new LedgerNoteBalanceItem
                {
                    VouNo = b.VouNo,
                    VouType = b.VouType,
                    BillDate = b.BillDate,
                    BillAmount = b.NetAmt,
                    PaidAmount = paidAmount,
                    PendingAmount = pending,
                    OverdueDays = Math.Max(0, days),
                    Status = status,
                    Narration = b.Narration
                });
            }
        }
        catch { }

        return list;
    }

    public async Task<IReadOnlyList<string>> GetHeadCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var list = new List<string>();
        try
        {
            const string sql = "SELECT DISTINCT [HeadCategory] FROM AccountCreation WHERE [HeadCategory] IS NOT NULL AND [HeadCategory] <> '' ORDER BY [HeadCategory]";
            await using var connection = new OleDbConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new OleDbCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var cat = reader[0]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(cat) && !list.Contains(cat, StringComparer.OrdinalIgnoreCase))
                {
                    list.Add(cat);
                }
            }
        }
        catch { }

        if (list.Count == 0)
        {
            list.AddRange(new[] { "SUNDRY DEBTORS", "SUNDRY CREDITORS", "BANK ACCOUNTS", "CASH IN HAND", "DIRECT EXPENSES", "INDIRECT EXPENSES", "KARIGHAR", "DUTIES & TAXES", "SALES ACCOUNTS", "PURCHASE ACCOUNTS" });
        }

        return list;
    }

    public async Task<IReadOnlyList<LedgerGroupAccountItem>> GetLedgerGroupSummaryAsync(string headCategory, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        var accounts = await GetLedgerAccountsAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(headCategory) && !headCategory.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            accounts = accounts.Where(a => string.Equals(a.HeadCategory, headCategory.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var result = new List<LedgerGroupAccountItem>();
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        foreach (var acc in accounts)
        {
            var filter = new LedgerReportFilter
            {
                AccountName = acc.AccountName,
                FromDate = fromDate,
                ToDate = toDate
            };

            var rupeeEntries = await GetLedgerInRupeesInternalAsync(connection, acc, filter, cancellationToken);
            var weightEntries = await GetLedgerInWeightInternalAsync(connection, acc, filter, cancellationToken);

            var item = new LedgerGroupAccountItem
            {
                AccountName = acc.AccountName,
                HeadCategory = acc.HeadCategory,
                SubCategory = acc.SubCategory,
                PhoneNo = !string.IsNullOrWhiteSpace(acc.PhoneNo) ? acc.PhoneNo : acc.Mobile,
                City = acc.City,
                RefName = acc.RefName
            };

            // Rupees
            var opRupee = rupeeEntries.FirstOrDefault(e => e.VouType == "Opening Balance");
            if (opRupee != null)
            {
                item.OpeningAmount = opRupee.Balance;
                item.OpeningType = opRupee.BalanceType;
            }

            var periodRupee = rupeeEntries.Where(e => e.VouType != "Opening Balance").ToList();
            item.TotalDebits = periodRupee.Sum(e => e.Debit);
            item.TotalCredits = periodRupee.Sum(e => e.Credit);

            if (rupeeEntries.Count > 0)
            {
                var last = rupeeEntries.Last();
                item.ClosingBalance = last.Balance;
                item.ClosingType = last.BalanceType;
            }
            else
            {
                item.ClosingBalance = item.OpeningAmount;
                item.ClosingType = item.OpeningType;
            }

            // Weights
            var opWeight = weightEntries.FirstOrDefault(e => e.VouType == "Opening Balance");
            if (opWeight != null)
            {
                item.OpeningWeight = opWeight.WeightBalance;
                item.OpeningWeightType = opWeight.WeightBalanceType;
            }

            var periodWeight = weightEntries.Where(e => e.VouType != "Opening Balance").ToList();
            item.TotalWeightDebit = periodWeight.Sum(e => e.WeightDebit);
            item.TotalWeightCredit = periodWeight.Sum(e => e.WeightCredit);

            if (weightEntries.Count > 0)
            {
                var lastW = weightEntries.Last();
                item.ClosingWeight = lastW.WeightBalance;
                item.ClosingWeightType = lastW.WeightBalanceType;
            }
            else
            {
                item.ClosingWeight = item.OpeningWeight;
                item.ClosingWeightType = item.OpeningWeightType;
            }

            result.Add(item);
        }

        return result;
    }

    public async Task<IReadOnlyList<LedgerEntry>> GetLedgerGroupEntriesAsync(string headCategory, DateTime fromDate, DateTime toDate, bool isWeight, CancellationToken cancellationToken = default)
    {
        var accounts = await GetLedgerAccountsAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(headCategory) && !headCategory.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            accounts = accounts.Where(a => string.Equals(a.HeadCategory, headCategory.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var list = new List<LedgerEntry>();
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        foreach (var acc in accounts)
        {
            var filter = new LedgerReportFilter
            {
                AccountName = acc.AccountName,
                FromDate = fromDate,
                ToDate = toDate
            };

            var entries = isWeight
                ? await GetLedgerInWeightInternalAsync(connection, acc, filter, cancellationToken)
                : await GetLedgerInRupeesInternalAsync(connection, acc, filter, cancellationToken);

            foreach (var entry in entries)
            {
                entry.AccountName = acc.AccountName;
                entry.HeadCategory = acc.HeadCategory;
                list.Add(entry);
            }
        }

        return list;
    }

    private static Account MapAccount(System.Data.Common.DbDataReader reader)
    {
        return new Account
        {
            PartyCode = reader["PartyCode"]?.ToString()?.Trim() ?? string.Empty,
            AccountName = reader["AccountName"]?.ToString()?.Trim() ?? string.Empty,
            HeadCategory = reader["HeadCategory"]?.ToString()?.Trim() ?? string.Empty,
            SubCategory = reader["SubCategory"]?.ToString()?.Trim() ?? string.Empty,
            PhoneNo = reader["PhoneNo"]?.ToString()?.Trim() ?? string.Empty,
            Mobile = reader["Mobile"]?.ToString()?.Trim() ?? string.Empty,
            RefName = reader["RefName"]?.ToString()?.Trim() ?? string.Empty,
            City = reader["City"]?.ToString()?.Trim() ?? string.Empty,
            State = reader["State"]?.ToString()?.Trim() ?? string.Empty,
            Address = reader["Address"]?.ToString()?.Trim() ?? string.Empty,
            Address2 = reader["Address2"]?.ToString()?.Trim() ?? string.Empty,
            PinCode = reader["PinCode"]?.ToString()?.Trim() ?? string.Empty,
            VatType = reader["VatType"]?.ToString()?.Trim() ?? string.Empty,
            VatNo = reader["VatNo"]?.ToString()?.Trim() ?? string.Empty,
            PanNo = reader["PanNo"]?.ToString()?.Trim() ?? string.Empty,
            InOut = reader["InOut"]?.ToString()?.Trim() ?? string.Empty,
            OpAmt1 = reader["OpAmt1"] != DBNull.Value ? Convert.ToDecimal(reader["OpAmt1"]) : 0m,
            OpAmt2 = reader["OpAmt2"] != DBNull.Value ? Convert.ToDecimal(reader["OpAmt2"]) : 0m,
            OpGoldWeight = reader["OpGoldWeight"] != DBNull.Value ? Convert.ToDecimal(reader["OpGoldWeight"]) : 0m,
            CreditLimit = reader["CreditLimit"] != DBNull.Value ? Convert.ToDecimal(reader["CreditLimit"]) : 0m,
            CreditDays = reader["CreditDays"] != DBNull.Value ? Convert.ToDecimal(reader["CreditDays"]) : 0m
        };
    }

    private sealed class RawLedgerRecord
    {
        public DateTime VouDate { get; set; }
        public string VouNo { get; set; } = string.Empty;
        public string VouType { get; set; } = string.Empty;
        public string Head { get; set; } = string.Empty;
        public string ItemType { get; set; } = string.Empty;
        public string Purity { get; set; } = string.Empty;
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public decimal WeightDebit { get; set; }
        public decimal WeightCredit { get; set; }
        public string Narration { get; set; } = string.Empty;
        public string ChequeNo { get; set; } = string.Empty;
    }
}
