using System.Data;
using System.Data.OleDb;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class AccessNonBarcodeReportRepository : INonBarcodeReportRepository
{
    private readonly string _connectionString;

    public AccessNonBarcodeReportRepository(string databasePath)
    {
        _connectionString = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={databasePath};";
    }

    public async Task<IReadOnlyList<NonBarcodeStockReportItem>> GetItemWiseStockAsync(NonBarcodeStockReportFilter filter, CancellationToken cancellationToken = default)
    {
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var fromDate = filter.FromDate?.Date;
        var toDate = filter.ToDate?.Date;

        // 1. Load item metadata & base opening stock from ItemCreation
        var itemMetas = await LoadItemMetasAsync(connection, cancellationToken);

        var itemsMap = new Dictionary<string, NonBarcodeStockReportItem>(StringComparer.OrdinalIgnoreCase);

        // Pre-populate with items having base opening stock
        foreach (var meta in itemMetas.Values)
        {
            if (meta.OpeningQty != 0 || meta.OpeningWt != 0)
            {
                itemsMap[meta.ItemName] = new NonBarcodeStockReportItem
                {
                    ItemName = meta.ItemName,
                    HeadGroup = meta.HeadGroup,
                    SubGroup = meta.SubGroup,
                    ItemType = meta.ItemType,
                    Purity = meta.Purity,
                    OpeningQty = meta.OpeningQty,
                    OpeningWt = meta.OpeningWt
                };
            }
        }

        NonBarcodeStockReportItem GetOrCreateItem(string rawName, string defaultPurity = "")
        {
            var name = string.IsNullOrWhiteSpace(rawName) ? "UNNAMED ITEM" : rawName.Trim();
            if (!itemsMap.TryGetValue(name, out var item))
            {
                if (itemMetas.TryGetValue(name, out var meta))
                {
                    item = new NonBarcodeStockReportItem
                    {
                        ItemName = meta.ItemName,
                        HeadGroup = meta.HeadGroup,
                        SubGroup = meta.SubGroup,
                        ItemType = meta.ItemType,
                        Purity = !string.IsNullOrWhiteSpace(meta.Purity) ? meta.Purity : defaultPurity,
                        OpeningQty = 0,
                        OpeningWt = 0
                    };
                }
                else
                {
                    item = new NonBarcodeStockReportItem
                    {
                        ItemName = name,
                        Purity = defaultPurity,
                        OpeningQty = 0,
                        OpeningWt = 0
                    };
                }
                itemsMap[name] = item;
            }
            return item;
        }

        // 2. Load Inward from Purchases (where Barcode is null or empty)
        try
        {
            const string sql = @"
                SELECT p.[BillDate], ps.[ItemName], ps.[Qty], ps.[NtWt], ps.[GrWt], ps.[Rate], ps.[Amount], ps.[Purity]
                FROM [PurchaseSubform] ps
                INNER JOIN [Purchases] p ON ps.[VouNo] = p.[VouNo]
                WHERE (ps.[Barcode] IS NULL OR ps.[Barcode] = '')
                  AND (p.[Status] IS NULL OR p.[Status] <> 'Deleted')";

            await using var cmd = new OleDbCommand(sql, connection);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var date = GetDateTime(reader, "BillDate");
                var itemName = GetString(reader, "ItemName");
                if (string.IsNullOrWhiteSpace(itemName)) continue;

                var qty = GetInt(reader, "Qty", 1);
                var ntWt = GetDecimal(reader, "NtWt");
                var grWt = GetDecimal(reader, "GrWt");
                var wt = ntWt > 0 ? ntWt : grWt;
                var rate = GetDecimal(reader, "Rate");
                var amount = GetDecimal(reader, "Amount");
                var purity = GetString(reader, "Purity");

                if (toDate.HasValue && date.HasValue && date.Value.Date > toDate.Value)
                    continue;

                var item = GetOrCreateItem(itemName, purity);
                if (fromDate.HasValue && date.HasValue && date.Value.Date < fromDate.Value)
                {
                    item.OpeningQty += qty;
                    item.OpeningWt += wt;
                }
                else
                {
                    item.InwardQty += qty;
                    item.InwardWt += wt;
                    if (rate > 0) item.Rate = rate;
                    item.Amount += amount;
                }
            }
        }
        catch { }

        // 3. Load Inward from NonBarcode stock entry vouchers
        try
        {
            const string sql = @"
                SELECT n.[VouDate], ns.[ItemName], ns.[Qty], ns.[Weight]
                FROM [NonBarcodeSubform] ns
                INNER JOIN [NonBarcode] n ON ns.[VouNo] = n.[VouNo]
                WHERE (n.[Status] IS NULL OR n.[Status] <> 'Deleted')";

            await using var cmd = new OleDbCommand(sql, connection);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var date = GetDateTime(reader, "VouDate");
                var itemName = GetString(reader, "ItemName");
                if (string.IsNullOrWhiteSpace(itemName)) continue;

                var qty = GetInt(reader, "Qty", 1);
                var wt = GetDecimal(reader, "Weight");

                if (toDate.HasValue && date.HasValue && date.Value.Date > toDate.Value)
                    continue;

                var item = GetOrCreateItem(itemName);
                if (fromDate.HasValue && date.HasValue && date.Value.Date < fromDate.Value)
                {
                    item.OpeningQty += qty;
                    item.OpeningWt += wt;
                }
                else
                {
                    item.InwardQty += qty;
                    item.InwardWt += wt;
                }
            }
        }
        catch { }

        // 4. Load Outward from Sales BillSubform (where Barcode is null or empty)
        try
        {
            const string sql = @"
                SELECT b.[BillDate], bs.[ItemName], bs.[Qty], bs.[NtWt], bs.[GrWt], bs.[Rate], bs.[Amount], bs.[Purity]
                FROM [BillSubform] bs
                INNER JOIN [Bill] b ON bs.[VouNo] = b.[VouNo]
                WHERE (bs.[Barcode] IS NULL OR bs.[Barcode] = '')
                  AND (b.[BillCanceled] = False OR b.[BillCanceled] IS NULL)";

            await using var cmd = new OleDbCommand(sql, connection);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var date = GetDateTime(reader, "BillDate");
                var itemName = GetString(reader, "ItemName");
                if (string.IsNullOrWhiteSpace(itemName)) continue;

                var qty = GetInt(reader, "Qty", 1);
                var ntWt = GetDecimal(reader, "NtWt");
                var grWt = GetDecimal(reader, "GrWt");
                var wt = ntWt > 0 ? ntWt : grWt;
                var purity = GetString(reader, "Purity");

                if (toDate.HasValue && date.HasValue && date.Value.Date > toDate.Value)
                    continue;

                var item = GetOrCreateItem(itemName, purity);
                if (fromDate.HasValue && date.HasValue && date.Value.Date < fromDate.Value)
                {
                    item.OpeningQty -= qty;
                    item.OpeningWt -= wt;
                }
                else
                {
                    item.OutwardQty += qty;
                    item.OutwardWt += wt;
                }
            }
        }
        catch { }

        // 5. Apply filters
        var result = itemsMap.Values.AsEnumerable();

        var headFilter = filter.ItemHead?.Trim();
        if (!string.IsNullOrWhiteSpace(headFilter) && headFilter != "*")
        {
            result = result.Where(x =>
                string.Equals(x.HeadGroup, headFilter, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.ItemType, headFilter, StringComparison.OrdinalIgnoreCase));
        }

        var subFilter = filter.SubGroup?.Trim();
        if (!string.IsNullOrWhiteSpace(subFilter) && subFilter != "*")
        {
            result = result.Where(x => string.Equals(x.SubGroup, subFilter, StringComparison.OrdinalIgnoreCase));
        }

        var itemFilter = filter.ItemName?.Trim();
        if (!string.IsNullOrWhiteSpace(itemFilter) && itemFilter != "*")
        {
            result = result.Where(x => string.Equals(x.ItemName, itemFilter, StringComparison.OrdinalIgnoreCase));
        }

        var search = filter.SearchText?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            result = result.Where(x =>
                x.ItemName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                x.HeadGroup.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                x.SubGroup.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                x.Purity.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        // Keep items that have some movement or balance
        result = result.Where(x =>
            x.OpeningQty != 0 || x.OpeningWt != 0 ||
            x.InwardQty != 0 || x.InwardWt != 0 ||
            x.OutwardQty != 0 || x.OutwardWt != 0 ||
            x.ClosingQty != 0 || x.ClosingWt != 0);

        return result.OrderBy(x => x.ItemName).ToList();
    }

    public async Task<IReadOnlyList<NonBarcodeTransactionDetail>> GetItemWiseTransactionsAsync(NonBarcodeStockReportFilter filter, CancellationToken cancellationToken = default)
    {
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var fromDate = filter.FromDate?.Date;
        var toDate = filter.ToDate?.Date;
        var itemMetas = await LoadItemMetasAsync(connection, cancellationToken);
        var list = new List<NonBarcodeTransactionDetail>();

        // 1. Non-barcoded Purchases
        try
        {
            const string sql = @"
                SELECT p.[BillDate], p.[VouNo], p.[CustomerName], p.[AccountName], p.[EntryUser], p.[Narration],
                       ps.[ItemName], ps.[Qty], ps.[NtWt], ps.[GrWt], ps.[Rate], ps.[Amount], ps.[Purity]
                FROM [PurchaseSubform] ps
                INNER JOIN [Purchases] p ON ps.[VouNo] = p.[VouNo]
                WHERE (ps.[Barcode] IS NULL OR ps.[Barcode] = '')
                  AND (p.[Status] IS NULL OR p.[Status] <> 'Deleted')";

            await using var cmd = new OleDbCommand(sql, connection);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var date = GetDateTime(reader, "BillDate");
                if (fromDate.HasValue && date.HasValue && date.Value.Date < fromDate.Value) continue;
                if (toDate.HasValue && date.HasValue && date.Value.Date > toDate.Value) continue;

                var itemName = GetString(reader, "ItemName");
                if (string.IsNullOrWhiteSpace(itemName)) continue;

                itemMetas.TryGetValue(itemName, out var meta);
                var party = GetString(reader, "CustomerName");
                if (string.IsNullOrWhiteSpace(party)) party = GetString(reader, "AccountName");

                var ntWt = GetDecimal(reader, "NtWt");
                var grWt = GetDecimal(reader, "GrWt");

                list.Add(new NonBarcodeTransactionDetail
                {
                    Date = date,
                    VouNo = GetString(reader, "VouNo"),
                    TransactionType = "Purchase",
                    PartyName = party,
                    ItemName = itemName,
                    HeadGroup = meta?.HeadGroup ?? string.Empty,
                    SubGroup = meta?.SubGroup ?? string.Empty,
                    ItemType = meta?.ItemType ?? string.Empty,
                    Purity = GetString(reader, "Purity", meta?.Purity ?? string.Empty),
                    InQty = GetInt(reader, "Qty", 1),
                    InWt = ntWt > 0 ? ntWt : grWt,
                    Rate = GetDecimal(reader, "Rate"),
                    Amount = GetDecimal(reader, "Amount"),
                    Narration = GetString(reader, "Narration"),
                    EntryUser = GetString(reader, "EntryUser")
                });
            }
        }
        catch { }

        // 2. NonBarcode Stock Entry Vouchers
        try
        {
            const string sql = @"
                SELECT n.[VouDate], n.[VouNo], n.[SupplierName], n.[EntryUser],
                       ns.[ItemName], ns.[Qty], ns.[Weight], ns.[BoxNo]
                FROM [NonBarcodeSubform] ns
                INNER JOIN [NonBarcode] n ON ns.[VouNo] = n.[VouNo]
                WHERE (n.[Status] IS NULL OR n.[Status] <> 'Deleted')";

            await using var cmd = new OleDbCommand(sql, connection);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var date = GetDateTime(reader, "VouDate");
                if (fromDate.HasValue && date.HasValue && date.Value.Date < fromDate.Value) continue;
                if (toDate.HasValue && date.HasValue && date.Value.Date > toDate.Value) continue;

                var itemName = GetString(reader, "ItemName");
                if (string.IsNullOrWhiteSpace(itemName)) continue;

                itemMetas.TryGetValue(itemName, out var meta);
                var boxNo = GetString(reader, "BoxNo");
                var narr = string.IsNullOrWhiteSpace(boxNo) ? "Non-Barcode Entry" : $"Non-Barcode Entry (Box: {boxNo})";

                list.Add(new NonBarcodeTransactionDetail
                {
                    Date = date,
                    VouNo = GetString(reader, "VouNo"),
                    TransactionType = "Non-Barcode Stock Inward",
                    PartyName = GetString(reader, "SupplierName"),
                    ItemName = itemName,
                    HeadGroup = meta?.HeadGroup ?? string.Empty,
                    SubGroup = meta?.SubGroup ?? string.Empty,
                    ItemType = meta?.ItemType ?? string.Empty,
                    Purity = meta?.Purity ?? string.Empty,
                    InQty = GetInt(reader, "Qty", 1),
                    InWt = GetDecimal(reader, "Weight"),
                    Narration = narr,
                    EntryUser = GetString(reader, "EntryUser")
                });
            }
        }
        catch { }

        // 3. Non-barcoded Sales Invoices
        try
        {
            const string sql = @"
                SELECT b.[BillDate], b.[VouNo], b.[CustomerName], b.[EntryUser], b.[Narration],
                       bs.[ItemName], bs.[Qty], bs.[NtWt], bs.[GrWt], bs.[Rate], bs.[Amount], bs.[Purity]
                FROM [BillSubform] bs
                INNER JOIN [Bill] b ON bs.[VouNo] = b.[VouNo]
                WHERE (bs.[Barcode] IS NULL OR bs.[Barcode] = '')
                  AND (b.[BillCanceled] = False OR b.[BillCanceled] IS NULL)";

            await using var cmd = new OleDbCommand(sql, connection);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var date = GetDateTime(reader, "BillDate");
                if (fromDate.HasValue && date.HasValue && date.Value.Date < fromDate.Value) continue;
                if (toDate.HasValue && date.HasValue && date.Value.Date > toDate.Value) continue;

                var itemName = GetString(reader, "ItemName");
                if (string.IsNullOrWhiteSpace(itemName)) continue;

                itemMetas.TryGetValue(itemName, out var meta);
                var ntWt = GetDecimal(reader, "NtWt");
                var grWt = GetDecimal(reader, "GrWt");

                list.Add(new NonBarcodeTransactionDetail
                {
                    Date = date,
                    VouNo = GetString(reader, "VouNo"),
                    TransactionType = "Sales Invoice",
                    PartyName = GetString(reader, "CustomerName"),
                    ItemName = itemName,
                    HeadGroup = meta?.HeadGroup ?? string.Empty,
                    SubGroup = meta?.SubGroup ?? string.Empty,
                    ItemType = meta?.ItemType ?? string.Empty,
                    Purity = GetString(reader, "Purity", meta?.Purity ?? string.Empty),
                    OutQty = GetInt(reader, "Qty", 1),
                    OutWt = ntWt > 0 ? ntWt : grWt,
                    Rate = GetDecimal(reader, "Rate"),
                    Amount = GetDecimal(reader, "Amount"),
                    Narration = GetString(reader, "Narration"),
                    EntryUser = GetString(reader, "EntryUser")
                });
            }
        }
        catch { }

        // Apply filters
        var result = list.AsEnumerable();

        var headFilter = filter.ItemHead?.Trim();
        if (!string.IsNullOrWhiteSpace(headFilter) && headFilter != "*")
        {
            result = result.Where(x =>
                string.Equals(x.HeadGroup, headFilter, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.ItemType, headFilter, StringComparison.OrdinalIgnoreCase));
        }

        var subFilter = filter.SubGroup?.Trim();
        if (!string.IsNullOrWhiteSpace(subFilter) && subFilter != "*")
        {
            result = result.Where(x => string.Equals(x.SubGroup, subFilter, StringComparison.OrdinalIgnoreCase));
        }

        var itemFilter = filter.ItemName?.Trim();
        if (!string.IsNullOrWhiteSpace(itemFilter) && itemFilter != "*")
        {
            result = result.Where(x => string.Equals(x.ItemName, itemFilter, StringComparison.OrdinalIgnoreCase));
        }

        var search = filter.SearchText?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            result = result.Where(x =>
                x.ItemName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                x.VouNo.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                x.PartyName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                x.TransactionType.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        return result.OrderByDescending(x => x.Date).ThenByDescending(x => x.VouNo).ToList();
    }

    private sealed class ItemMeta
    {
        public string ItemName { get; set; } = string.Empty;
        public string HeadGroup { get; set; } = string.Empty;
        public string SubGroup { get; set; } = string.Empty;
        public string ItemType { get; set; } = string.Empty;
        public string Purity { get; set; } = string.Empty;
        public decimal OpeningQty { get; set; }
        public decimal OpeningWt { get; set; }
    }

    private static async Task<Dictionary<string, ItemMeta>> LoadItemMetasAsync(OleDbConnection connection, CancellationToken cancellationToken)
    {
        var dict = new Dictionary<string, ItemMeta>(StringComparer.OrdinalIgnoreCase);
        try
        {
            const string sql = "SELECT * FROM [ItemCreation]";
            await using var cmd = new OleDbCommand(sql, connection);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var name = GetString(reader, "ItemName");
                if (string.IsNullOrWhiteSpace(name)) continue;

                var head = GetString(reader, "HeadGroup");
                if (string.IsNullOrWhiteSpace(head)) head = GetString(reader, "Group");

                var sub = GetString(reader, "SubGroup");
                var metal = GetString(reader, "MetalType");
                var purity = GetString(reader, "Purity");
                var opStock = GetDecimal(reader, "OpeningStock", GetDecimal(reader, "Opening"));
                var stdWt = GetDecimal(reader, "StandardWeight");

                dict[name] = new ItemMeta
                {
                    ItemName = name,
                    HeadGroup = head,
                    SubGroup = sub,
                    ItemType = metal,
                    Purity = purity,
                    OpeningQty = opStock,
                    OpeningWt = opStock * (stdWt > 0 ? stdWt : 0m)
                };
            }
        }
        catch { }
        return dict;
    }

    public async Task<IReadOnlyList<NonBarcodeGroupSummary>> GetItemTypeWiseStockAsync(NonBarcodeStockReportFilter filter, CancellationToken cancellationToken = default)
    {
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var fromDate = filter.FromDate?.Date;
        var toDate = filter.ToDate?.Date;

        var groupsMap = new Dictionary<string, NonBarcodeGroupSummary>(StringComparer.OrdinalIgnoreCase);

        NonBarcodeGroupSummary GetOrCreateGroup(string rawType)
        {
            var type = string.IsNullOrWhiteSpace(rawType) ? "UNASSIGNED" : rawType.Trim();
            if (!groupsMap.TryGetValue(type, out var summary))
            {
                summary = new NonBarcodeGroupSummary
                {
                    GroupKey = type,
                    ItemType = type,
                    ItemCount = 1,
                    OpeningQty = 0,
                    OpeningWt = 0,
                    InwardQty = 0,
                    InwardWt = 0,
                    OutwardQty = 0,
                    OutwardWt = 0
                };
                groupsMap[type] = summary;
            }
            return summary;
        }

        // 1. Seed with distinct ItemTypes from ItemType table, MetalIssued, MetalReceived
        try
        {
            await using var cmd = new OleDbCommand("SELECT DISTINCT [ItemType] FROM [ItemType] WHERE [ItemType] IS NOT NULL AND [ItemType] <> ''", connection);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var t = reader[0]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(t)) GetOrCreateGroup(t);
            }
        }
        catch { }

        try
        {
            await using var cmd = new OleDbCommand("SELECT DISTINCT [ItemType] FROM [MetalIssued] WHERE [ItemType] IS NOT NULL AND [ItemType] <> ''", connection);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var t = reader[0]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(t)) GetOrCreateGroup(t);
            }
        }
        catch { }

        try
        {
            await using var cmd = new OleDbCommand("SELECT DISTINCT [ItemType] FROM [MetalReceived] WHERE [ItemType] IS NOT NULL AND [ItemType] <> ''", connection);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var t = reader[0]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(t)) GetOrCreateGroup(t);
            }
        }
        catch { }

        // 2. Load Base Opening Stock from OpeningStock table where OpeningStock.MetalType = ItemType.ItemType
        try
        {
            await using var cmd = new OleDbCommand("SELECT [MetalType], [Weight] FROM [OpeningStock] WHERE [MetalType] IS NOT NULL", connection);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var metalType = GetString(reader, "MetalType");
                var weight = GetDecimal(reader, "Weight");
                if (string.IsNullOrWhiteSpace(metalType)) continue;

                var grp = GetOrCreateGroup(metalType);
                grp.OpeningWt += weight;
            }
        }
        catch { }

        // 3. Load Inward from MetalReceived (Karighar Receive)
        try
        {
            const string sql = @"
                SELECT [RecDate], [ItemType], [WeightRecd], [MakingCharges]
                FROM [MetalReceived]
                WHERE ([Status] IS NULL OR [Status] <> 'Deleted')
                  AND [WeightRecd] > 0";
            await using var cmd = new OleDbCommand(sql, connection);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var rDate = GetDateTime(reader, "RecDate");
                var itemType = GetString(reader, "ItemType");
                var wt = GetDecimal(reader, "WeightRecd");

                if (wt <= 0) continue;
                var resolvedType = !string.IsNullOrWhiteSpace(itemType) ? itemType : "GOLD NEW ORNAMENTS";

                if (toDate.HasValue && rDate.HasValue && rDate.Value.Date > toDate.Value)
                    continue;

                var grp = GetOrCreateGroup(resolvedType);
                if (fromDate.HasValue && rDate.HasValue && rDate.Value.Date < fromDate.Value)
                {
                    grp.OpeningQty += 1;
                    grp.OpeningWt += wt;
                }
                else
                {
                    grp.InwardQty += 1;
                    grp.InwardWt += wt;
                }
            }
        }
        catch { }

        // 4. Load Inward from Bill (OLD GOLD PURCHASE)
        try
        {
            const string sql = @"
                SELECT b.[BillDate], b.[Item], b.[GrWt], b.[NtWt], b.[NetAmt]
                FROM [Bill] b
                WHERE (b.[BillCanceled] = False OR b.[BillCanceled] IS NULL)
                  AND (b.[VouType] = 'OLD GOLD PURCHASE')";
            await using var cmd = new OleDbCommand(sql, connection);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var bDate = GetDateTime(reader, "BillDate");
                var itemType = GetString(reader, "Item");
                var ntWt = GetDecimal(reader, "NtWt");
                var grWt = GetDecimal(reader, "GrWt");
                var wt = ntWt > 0 ? ntWt : grWt;

                if (wt <= 0) continue;
                var resolvedType = !string.IsNullOrWhiteSpace(itemType) ? itemType : "GOLD PRIMARY";

                if (toDate.HasValue && bDate.HasValue && bDate.Value.Date > toDate.Value)
                    continue;

                var grp = GetOrCreateGroup(resolvedType);
                if (fromDate.HasValue && bDate.HasValue && bDate.Value.Date < fromDate.Value)
                {
                    grp.OpeningQty += 1;
                    grp.OpeningWt += wt;
                }
                else
                {
                    grp.InwardQty += 1;
                    grp.InwardWt += wt;
                }
            }
        }
        catch { }

        // 5. Load Inward from PaymentSubform (Old Gold exchange in invoices)
        try
        {
            const string sql = @"
                SELECT b.[BillDate], ps.[Item], ps.[GrWt], ps.[NtWt], ps.[Amount], ps.[AccountName]
                FROM [PaymentSubform] ps
                INNER JOIN [Bill] b ON ps.[VouNo] = b.[VouNo]
                WHERE (b.[BillCanceled] = False OR b.[BillCanceled] IS NULL)
                  AND (b.[VouType] <> 'OLD GOLD PURCHASE' OR b.[VouType] IS NULL)
                  AND (ps.[AccountName] IS NOT NULL AND ps.[AccountName] <> '')
                  AND (ps.[NtWt] > 0 OR ps.[GrWt] > 0 OR ps.[ItemName] LIKE '%OLD%' OR ps.[Item] LIKE '%OLD%')";
            await using var cmd = new OleDbCommand(sql, connection);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var bDate = GetDateTime(reader, "BillDate");
                var accountName = GetString(reader, "AccountName");
                if (string.IsNullOrWhiteSpace(accountName)) continue;

                var itemType = GetString(reader, "Item");
                var ntWt = GetDecimal(reader, "NtWt");
                var grWt = GetDecimal(reader, "GrWt");
                var wt = ntWt > 0 ? ntWt : grWt;

                if (wt <= 0) continue;
                var resolvedType = !string.IsNullOrWhiteSpace(itemType) ? itemType : "OLD GOLD";

                if (toDate.HasValue && bDate.HasValue && bDate.Value.Date > toDate.Value)
                    continue;

                var grp = GetOrCreateGroup(resolvedType);
                if (fromDate.HasValue && bDate.HasValue && bDate.Value.Date < fromDate.Value)
                {
                    grp.OpeningQty += 1;
                    grp.OpeningWt += wt;
                }
                else
                {
                    grp.InwardQty += 1;
                    grp.InwardWt += wt;
                }
            }
        }
        catch { }

        // 6. Load Outward from MetalIssued (Karighar Issue)
        try
        {
            const string sql = @"
                SELECT [IssueDate], [ItemType], [WeightIssued]
                FROM [MetalIssued]
                WHERE ([Status] IS NULL OR [Status] <> 'Deleted')
                  AND [WeightIssued] > 0";
            await using var cmd = new OleDbCommand(sql, connection);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var iDate = GetDateTime(reader, "IssueDate");
                var itemType = GetString(reader, "ItemType");
                var wt = GetDecimal(reader, "WeightIssued");

                if (wt <= 0) continue;
                var resolvedType = !string.IsNullOrWhiteSpace(itemType) ? itemType : "GOLD PRIMARY";

                if (toDate.HasValue && iDate.HasValue && iDate.Value.Date > toDate.Value)
                    continue;

                var grp = GetOrCreateGroup(resolvedType);
                if (fromDate.HasValue && iDate.HasValue && iDate.Value.Date < fromDate.Value)
                {
                    grp.OpeningQty -= 1;
                    grp.OpeningWt -= wt;
                }
                else
                {
                    grp.OutwardQty += 1;
                    grp.OutwardWt += wt;
                }
            }
        }
        catch { }

        // 7. Apply filters
        var resultList = groupsMap.Values.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(filter.ItemHead) && filter.ItemHead != "*")
        {
            resultList = resultList.Where(x => string.Equals(x.ItemType, filter.ItemHead.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var search = filter.SearchText.Trim();
            resultList = resultList.Where(x => x.ItemType.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        return resultList
            .Where(x => x.OpeningQty != 0 || x.OpeningWt != 0 || x.InwardQty != 0 || x.InwardWt != 0 || x.OutwardQty != 0 || x.OutwardWt != 0)
            .OrderBy(x => x.ItemType)
            .ToList();
    }

    public async Task<IReadOnlyList<NonBarcodeTransactionDetail>> GetTransactionDetailsAsync(NonBarcodeStockReportFilter filter, CancellationToken cancellationToken = default)
    {
        var list = new List<NonBarcodeTransactionDetail>();

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var fromDate = filter.FromDate?.Date;
        var toDate = filter.ToDate?.Date;

        // 1. Load Inward Stock from MetalReceived (Karighar Receive)
        const string metalRecSql = @"
            SELECT [RecNo], [RecDate], [PartyName], [ItemType], [WeightRecd], [MakingCharges], [Note], [EntryUser]
            FROM [MetalReceived]
            WHERE ([Status] IS NULL OR [Status] <> 'Deleted')
              AND [WeightRecd] > 0";

        try
        {
            await using var mrCmd = new OleDbCommand(metalRecSql, connection);
            await using var mrReader = await mrCmd.ExecuteReaderAsync(cancellationToken);
            while (await mrReader.ReadAsync(cancellationToken))
            {
                var rDate = GetDateTime(mrReader, "RecDate");
                var recNo = GetString(mrReader, "RecNo");
                var party = GetString(mrReader, "PartyName");
                var itemType = GetString(mrReader, "ItemType");
                var wt = GetDecimal(mrReader, "WeightRecd");
                var making = GetDecimal(mrReader, "MakingCharges");
                var note = GetString(mrReader, "Note");
                var user = GetString(mrReader, "EntryUser");

                if (wt <= 0) continue;

                var resolvedType = !string.IsNullOrWhiteSpace(itemType) ? itemType : "GOLD NEW ORNAMENTS";

                list.Add(new NonBarcodeTransactionDetail
                {
                    Date = rDate,
                    VouNo = recNo,
                    TransactionType = "Inward (Karighar Receive)",
                    PartyName = party,
                    ItemName = resolvedType,
                    HeadGroup = resolvedType,
                    SubGroup = "KARIGHAR RECEIVE",
                    ItemType = resolvedType,
                    InQty = 1,
                    InWt = wt,
                    Amount = making,
                    Narration = note,
                    EntryUser = user
                });
            }
        }
        catch { }

        // 2. Load Inward Stock from Bill (OLD GOLD PURCHASE vouchers) based on bill.Item
        const string billOldGoldSql = @"
            SELECT b.[VouNo], b.[BillDate], b.[CustomerName], b.[EntryUser], b.[ItemName], b.[Item], b.[GrWt], b.[NtWt], b.[Rate], b.[NetAmt], b.[Remarks]
            FROM [Bill] b
            WHERE (b.[BillCanceled] = False OR b.[BillCanceled] IS NULL)
              AND (b.[VouType] = 'OLD GOLD PURCHASE')";

        try
        {
            await using var bogCmd = new OleDbCommand(billOldGoldSql, connection);
            await using var bogReader = await bogCmd.ExecuteReaderAsync(cancellationToken);
            while (await bogReader.ReadAsync(cancellationToken))
            {
                var bDate = GetDateTime(bogReader, "BillDate");
                var vouNo = GetString(bogReader, "VouNo");
                var customer = GetString(bogReader, "CustomerName");
                var user = GetString(bogReader, "EntryUser");
                var itemName = GetString(bogReader, "ItemName");
                var itemType = GetString(bogReader, "Item");
                var ntWt = GetDecimal(bogReader, "NtWt");
                var grWt = GetDecimal(bogReader, "GrWt");
                var wt = ntWt > 0 ? ntWt : grWt;
                var rate = GetDecimal(bogReader, "Rate");
                var amount = GetDecimal(bogReader, "NetAmt");
                var remarks = GetString(bogReader, "Remarks");

                if (wt <= 0 && amount <= 0) continue;

                var resolvedName = !string.IsNullOrWhiteSpace(itemName) ? itemName : (!string.IsNullOrWhiteSpace(itemType) ? itemType : "GOLD OLD");
                var resolvedType = !string.IsNullOrWhiteSpace(itemType) ? itemType : "GOLD PRIMARY";

                list.Add(new NonBarcodeTransactionDetail
                {
                    Date = bDate,
                    VouNo = vouNo,
                    TransactionType = "Inward (Old Gold Purchase)",
                    PartyName = customer,
                    ItemName = resolvedName,
                    HeadGroup = resolvedType,
                    SubGroup = "OLD PURCHASE",
                    ItemType = resolvedType,
                    InQty = 1,
                    InWt = wt,
                    Rate = rate,
                    Amount = amount,
                    Narration = remarks,
                    EntryUser = user
                });
            }
        }
        catch { }

        // 3. Load Inward Stock from PaymentSubform (Old Gold Purchases from retail invoices)
        const string oldGoldSql = @"
            SELECT b.[VouNo], b.[BillDate], b.[CustomerName], b.[EntryUser], ps.[ItemName], ps.[Item], ps.[GrWt], ps.[NtWt], ps.[Amount], ps.[Rate], ps.[Narration], ps.[AccountName]
            FROM [PaymentSubform] ps
            INNER JOIN [Bill] b ON ps.[VouNo] = b.[VouNo]
            WHERE (b.[BillCanceled] = False OR b.[BillCanceled] IS NULL)
              AND (b.[VouType] <> 'OLD GOLD PURCHASE' OR b.[VouType] IS NULL)
              AND (ps.[AccountName] IS NOT NULL AND ps.[AccountName] <> '')
              AND (ps.[NtWt] > 0 OR ps.[GrWt] > 0 OR ps.[ItemName] LIKE '%OLD%' OR ps.[Item] LIKE '%OLD%')";

        try
        {
            await using var ogCmd = new OleDbCommand(oldGoldSql, connection);
            await using var ogReader = await ogCmd.ExecuteReaderAsync(cancellationToken);
            while (await ogReader.ReadAsync(cancellationToken))
            {
                var bDate = GetDateTime(ogReader, "BillDate");
                var accountName = GetString(ogReader, "AccountName");
                if (string.IsNullOrWhiteSpace(accountName)) continue;

                var vouNo = GetString(ogReader, "VouNo");
                var customer = GetString(ogReader, "CustomerName");
                var user = GetString(ogReader, "EntryUser");
                var itemName = GetString(ogReader, "ItemName");
                var itemType = GetString(ogReader, "Item");
                var ntWt = GetDecimal(ogReader, "NtWt");
                var grWt = GetDecimal(ogReader, "GrWt");
                var wt = ntWt > 0 ? ntWt : grWt;
                var rate = GetDecimal(ogReader, "Rate");
                var amount = GetDecimal(ogReader, "Amount");
                var narration = GetString(ogReader, "Narration");

                if (wt <= 0 && amount <= 0) continue;

                var resolvedName = !string.IsNullOrWhiteSpace(itemName) ? itemName : (!string.IsNullOrWhiteSpace(itemType) ? itemType : "OLD GOLD");
                var resolvedType = !string.IsNullOrWhiteSpace(itemType) ? itemType : "OLD GOLD";

                list.Add(new NonBarcodeTransactionDetail
                {
                    Date = bDate,
                    VouNo = vouNo,
                    TransactionType = "Inward (Old Gold)",
                    PartyName = customer,
                    ItemName = resolvedName,
                    HeadGroup = resolvedType,
                    SubGroup = "OLD PURCHASE",
                    ItemType = resolvedType,
                    InQty = 1,
                    InWt = wt,
                    Rate = rate,
                    Amount = amount,
                    Narration = narration,
                    EntryUser = user
                });
            }
        }
        catch { }

        // 4. Load Outward Stock from MetalIssued (Karighar Issue)
        const string metalIssueSql = @"
            SELECT [IssueNo], [IssueDate], [ArtisianName], [ItemType], [WeightIssued], [Purity], [Note], [EntryUser]
            FROM [MetalIssued]
            WHERE ([Status] IS NULL OR [Status] <> 'Deleted')
              AND [WeightIssued] > 0";

        try
        {
            await using var miCmd = new OleDbCommand(metalIssueSql, connection);
            await using var miReader = await miCmd.ExecuteReaderAsync(cancellationToken);
            while (await miReader.ReadAsync(cancellationToken))
            {
                var iDate = GetDateTime(miReader, "IssueDate");
                var issueNo = GetString(miReader, "IssueNo");
                var artisan = GetString(miReader, "ArtisianName");
                var itemType = GetString(miReader, "ItemType");
                var wt = GetDecimal(miReader, "WeightIssued");
                var purity = GetString(miReader, "Purity");
                var note = GetString(miReader, "Note");
                var user = GetString(miReader, "EntryUser");

                if (wt <= 0) continue;

                var resolvedType = !string.IsNullOrWhiteSpace(itemType) ? itemType : "GOLD PRIMARY";
                var narr = !string.IsNullOrWhiteSpace(purity) ? $"Purity: {purity}. {note}".Trim() : note;

                list.Add(new NonBarcodeTransactionDetail
                {
                    Date = iDate,
                    VouNo = issueNo,
                    TransactionType = "Outward (Karighar Issue)",
                    PartyName = artisan,
                    ItemName = resolvedType,
                    HeadGroup = resolvedType,
                    SubGroup = "KARIGHAR ISSUE",
                    ItemType = resolvedType,
                    OutQty = 1,
                    OutWt = wt,
                    Narration = narr,
                    EntryUser = user
                });
            }
        }
        catch { }

        // 5. Apply filters
        var resultList = list.AsEnumerable();

        if (fromDate.HasValue)
        {
            resultList = resultList.Where(x => x.Date.HasValue && x.Date.Value.Date >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            resultList = resultList.Where(x => x.Date.HasValue && x.Date.Value.Date <= toDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.ItemHead) && filter.ItemHead != "*")
        {
            resultList = resultList.Where(x => string.Equals(x.HeadGroup, filter.ItemHead.Trim(), StringComparison.OrdinalIgnoreCase) ||
                                               string.Equals(x.ItemType, filter.ItemHead.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(filter.SubGroup) && filter.SubGroup != "*")
        {
            resultList = resultList.Where(x => string.Equals(x.SubGroup, filter.SubGroup.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(filter.ItemName) && filter.ItemName != "*")
        {
            resultList = resultList.Where(x => string.Equals(x.ItemName, filter.ItemName.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var s = filter.SearchText.Trim();
            resultList = resultList.Where(x =>
                x.VouNo.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                x.PartyName.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                x.ItemName.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                x.HeadGroup.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                x.TransactionType.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                x.Narration.Contains(s, StringComparison.OrdinalIgnoreCase));
        }

        return resultList
            .OrderByDescending(x => x.Date)
            .ThenByDescending(x => x.VouNo)
            .ToList();
    }

    public async Task<(IReadOnlyList<string> ItemHeads, IReadOnlyList<string> SubGroups, IReadOnlyList<string> ItemNames)> GetFilterMastersAsync(CancellationToken cancellationToken = default)
    {
        var itemHeads = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "*" };
        var subGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "*", "KARIGHAR RECEIVE", "KARIGHAR ISSUE", "OLD PURCHASE" };
        var itemNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "*" };

        try
        {
            await using var connection = new OleDbConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            // 1. ItemType table
            await using (var cmd = new OleDbCommand("SELECT DISTINCT [ItemType] FROM [ItemType] WHERE [ItemType] IS NOT NULL AND [ItemType] <> ''", connection))
            await using (var r = await cmd.ExecuteReaderAsync(cancellationToken))
            {
                while (await r.ReadAsync(cancellationToken))
                {
                    var t = r[0]?.ToString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(t))
                    {
                        itemHeads.Add(t);
                        itemNames.Add(t);
                    }
                }
            }

            // 2. OpeningStock MetalTypes
            await using (var cmd = new OleDbCommand("SELECT DISTINCT [MetalType] FROM [OpeningStock] WHERE [MetalType] IS NOT NULL AND [MetalType] <> ''", connection))
            await using (var r = await cmd.ExecuteReaderAsync(cancellationToken))
            {
                while (await r.ReadAsync(cancellationToken))
                {
                    var t = r[0]?.ToString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(t))
                    {
                        itemHeads.Add(t);
                        itemNames.Add(t);
                    }
                }
            }

            // 3. MetalIssued & MetalReceived ItemTypes
            await using (var cmd = new OleDbCommand("SELECT DISTINCT [ItemType] FROM [MetalIssued] WHERE [ItemType] IS NOT NULL AND [ItemType] <> ''", connection))
            await using (var r = await cmd.ExecuteReaderAsync(cancellationToken))
            {
                while (await r.ReadAsync(cancellationToken))
                {
                    var t = r[0]?.ToString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(t))
                    {
                        itemHeads.Add(t);
                        itemNames.Add(t);
                    }
                }
            }

            // 4. Bill table items
            await using (var cmd = new OleDbCommand("SELECT DISTINCT [Item] FROM [Bill] WHERE [Item] IS NOT NULL AND [Item] <> ''", connection))
            await using (var r = await cmd.ExecuteReaderAsync(cancellationToken))
            {
                while (await r.ReadAsync(cancellationToken))
                {
                    var t = r[0]?.ToString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(t))
                    {
                        itemHeads.Add(t);
                        itemNames.Add(t);
                    }
                }
            }
        }
        catch
        {
            itemHeads.UnionWith(new[] { "GOLD PRIMARY", "GOLD NEW ORNAMENTS", "GOLD OLD", "SILVER PRIMARY", "SILVER OLD" });
        }

        return (itemHeads.OrderBy(x => x == "*" ? " " : x).ToList(),
                subGroups.OrderBy(x => x == "*" ? " " : x).ToList(),
                itemNames.OrderBy(x => x == "*" ? " " : x).ToList());
    }

    public async Task<(IReadOnlyList<string> ItemHeads, IReadOnlyList<string> SubGroups, IReadOnlyList<string> ItemNames)> GetItemFilterMastersAsync(CancellationToken cancellationToken = default)
    {
        var itemHeads = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "*" };
        var subGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "*" };
        var itemNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "*" };

        try
        {
            await using var connection = new OleDbConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            // From ItemCreation
            try
            {
                await using var cmd = new OleDbCommand("SELECT DISTINCT [HeadGroup], [Group], [SubGroup], [ItemName] FROM [ItemCreation]", connection);
                await using var r = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await r.ReadAsync(cancellationToken))
                {
                    var h = GetString(r, "HeadGroup");
                    var g = GetString(r, "Group");
                    var s = GetString(r, "SubGroup");
                    var i = GetString(r, "ItemName");

                    if (!string.IsNullOrWhiteSpace(h)) itemHeads.Add(h);
                    if (!string.IsNullOrWhiteSpace(g)) itemHeads.Add(g);
                    if (!string.IsNullOrWhiteSpace(s)) subGroups.Add(s);
                    if (!string.IsNullOrWhiteSpace(i)) itemNames.Add(i);
                }
            }
            catch { }

            // Also from NonBarcodeSubform
            try
            {
                await using var cmd = new OleDbCommand("SELECT DISTINCT [ItemName] FROM [NonBarcodeSubform] WHERE [ItemName] IS NOT NULL AND [ItemName] <> ''", connection);
                await using var r = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await r.ReadAsync(cancellationToken))
                {
                    var i = r[0]?.ToString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(i)) itemNames.Add(i);
                }
            }
            catch { }

            // Also from PurchaseSubform
            try
            {
                await using var cmd = new OleDbCommand("SELECT DISTINCT [ItemName] FROM [PurchaseSubform] WHERE [ItemName] IS NOT NULL AND [ItemName] <> ''", connection);
                await using var r = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await r.ReadAsync(cancellationToken))
                {
                    var i = r[0]?.ToString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(i)) itemNames.Add(i);
                }
            }
            catch { }

            // Also from BillSubform
            try
            {
                await using var cmd = new OleDbCommand("SELECT DISTINCT [ItemName] FROM [BillSubform] WHERE [ItemName] IS NOT NULL AND [ItemName] <> ''", connection);
                await using var r = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await r.ReadAsync(cancellationToken))
                {
                    var i = r[0]?.ToString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(i)) itemNames.Add(i);
                }
            }
            catch { }
        }
        catch { }

        return (itemHeads.OrderBy(x => x == "*" ? " " : x).ToList(),
                subGroups.OrderBy(x => x == "*" ? " " : x).ToList(),
                itemNames.OrderBy(x => x == "*" ? " " : x).ToList());
    }

    private static string GetString(IDataRecord r, string col, string defaultVal = "")
    {
        try
        {
            var ord = r.GetOrdinal(col);
            return r.IsDBNull(ord) ? defaultVal : r.GetValue(ord)?.ToString()?.Trim() ?? defaultVal;
        }
        catch { return defaultVal; }
    }

    private static int GetInt(IDataRecord r, string col, int defaultVal = 0)
    {
        try
        {
            var ord = r.GetOrdinal(col);
            return r.IsDBNull(ord) ? defaultVal : Convert.ToInt32(r.GetValue(ord));
        }
        catch { return defaultVal; }
    }

    private static decimal GetDecimal(IDataRecord r, string col, decimal defaultVal = 0m)
    {
        try
        {
            var ord = r.GetOrdinal(col);
            return r.IsDBNull(ord) ? defaultVal : Convert.ToDecimal(r.GetValue(ord));
        }
        catch { return defaultVal; }
    }

    private static DateTime? GetDateTime(IDataRecord r, string col)
    {
        try
        {
            var ord = r.GetOrdinal(col);
            if (r.IsDBNull(ord)) return null;
            var val = r.GetValue(ord);
            return val is DateTime dt ? dt : DateTime.TryParse(val?.ToString(), out var parsed) ? parsed : null;
        }
        catch { return null; }
    }
}
