using System.Data;
using System.Data.OleDb;
using System.Text;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class AccessBarcodeReportRepository : IBarcodeReportRepository
{
    private readonly string _connectionString;

    public AccessBarcodeReportRepository(string databasePath)
    {
        _connectionString = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={databasePath};";
    }

    public async Task<IReadOnlyList<BarcodeStockReportItem>> GetStockInHandAsync(BarcodeStockReportFilter filter, CancellationToken cancellationToken = default)
    {
        var list = new List<BarcodeStockReportItem>();

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // Fetch sold and converted barcodes in lightning-fast distinct lookups
        var (soldBarcodes, convertedBarcodes) = await GetExcludedBarcodesAsync(connection, cancellationToken);

        var (whereSql, parameters) = BuildStockWhereClause(filter);

        var sql = $@"
            SELECT 
                b.[Barcode], 
                b.[ItemName], 
                b.[Qty], 
                b.[GrossWt], 
                b.[StoneWt], 
                b.[NetWt], 
                b.[StoneValue], 
                b.[Making], 
                b.[Purity], 
                i.[SubGroup], 
                i.[HeadGroup],
                b.[SupplierName],
                b.[CostPrice],
                b.[MRP],
                b.[PDate],
                b.[Status],
                b.[Size],
                b.[OldBarcode],
                b.[BType],
                b.[Description],
                b.[Photo],
                b.[BoxNo],
                b.[Godown]
            FROM [BarcodeSubform] b 
            LEFT JOIN [ItemCreation] i ON b.[ItemName] = i.[ItemName]
            WHERE (b.[Status] IS NULL OR b.[Status] <> 'Deleted')
              AND (b.[Godown] IS NULL OR b.[Godown] <> 'Damage')
              {whereSql}
            ORDER BY b.[BarcodeAutoNo] DESC, b.[Barcode] DESC";

        await using var command = new OleDbCommand(sql, connection);
        foreach (var p in parameters)
        {
            command.Parameters.Add(p);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var item = MapStockItem(reader);
            if (!string.IsNullOrEmpty(item.Barcode))
            {
                if (soldBarcodes.Contains(item.Barcode) || convertedBarcodes.Contains(item.Barcode))
                {
                    continue; // Exclude sold or converted items
                }
            }
            list.Add(item);
        }

        return list;
    }

    private static async Task<(HashSet<string> Sold, HashSet<string> Converted)> GetExcludedBarcodesAsync(OleDbConnection connection, CancellationToken cancellationToken)
    {
        var sold = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var converted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Sold Barcodes from BillSubform
        try
        {
            await using var cmdSold = new OleDbCommand("SELECT DISTINCT [Barcode] FROM [BillSubform] WHERE [Barcode] IS NOT NULL AND [Barcode] <> ''", connection);
            await using var rSold = await cmdSold.ExecuteReaderAsync(cancellationToken);
            while (await rSold.ReadAsync(cancellationToken))
            {
                var bc = rSold[0]?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(bc)) sold.Add(bc);
            }
        }
        catch { }

        // 2. Converted Barcodes from BarcodeConversion
        try
        {
            await using var cmdConv = new OleDbCommand("SELECT DISTINCT [Barcode] FROM [BarcodeConversion] WHERE [Barcode] IS NOT NULL AND [Barcode] <> ''", connection);
            await using var rConv = await cmdConv.ExecuteReaderAsync(cancellationToken);
            while (await rConv.ReadAsync(cancellationToken))
            {
                var bc = rConv[0]?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(bc)) converted.Add(bc);
            }
        }
        catch { }

        return (sold, converted);
    }

    public async Task<IReadOnlyList<BarcodeGroupSummary>> GetStockHeadGroupWiseAsync(BarcodeStockReportFilter filter, CancellationToken cancellationToken = default)
    {
        return await GetHeadGroupSummariesAsync(filter, cancellationToken);
    }

    public async Task<IReadOnlyList<BarcodeGroupSummary>> GetHeadGroupSummariesAsync(BarcodeStockReportFilter filter, CancellationToken cancellationToken = default)
    {
        var stock = await GetStockInHandAsync(filter, cancellationToken);
        return stock
            .GroupBy(s => string.IsNullOrWhiteSpace(s.HeadGroup) ? "UNASSIGNED" : s.HeadGroup.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => new BarcodeGroupSummary
            {
                GroupKey = g.Key,
                HeadGroup = g.Key,
                ItemCount = g.Count(),
                TotalQty = g.Sum(x => x.Qty),
                TotalGrossWt = g.Sum(x => x.GrossWt),
                TotalStoneWt = g.Sum(x => x.StoneWt),
                TotalNetWt = g.Sum(x => x.NetWt),
                TotalStoneValue = g.Sum(x => x.StoneValue),
                TotalMaking = g.Sum(x => x.Making)
            })
            .OrderBy(g => g.HeadGroup)
            .ToList();
    }

    public async Task<IReadOnlyList<BarcodeGroupSummary>> GetSubGroupSummariesAsync(string headGroup, BarcodeStockReportFilter filter, CancellationToken cancellationToken = default)
    {
        var stock = await GetStockInHandAsync(filter, cancellationToken);
        return stock
            .Where(s => string.Equals(s.HeadGroup ?? "UNASSIGNED", headGroup, StringComparison.OrdinalIgnoreCase))
            .GroupBy(s => string.IsNullOrWhiteSpace(s.SubGroup) ? "UNASSIGNED" : s.SubGroup.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => new BarcodeGroupSummary
            {
                GroupKey = g.Key,
                HeadGroup = headGroup,
                SubGroup = g.Key,
                ItemCount = g.Count(),
                TotalQty = g.Sum(x => x.Qty),
                TotalGrossWt = g.Sum(x => x.GrossWt),
                TotalStoneWt = g.Sum(x => x.StoneWt),
                TotalNetWt = g.Sum(x => x.NetWt),
                TotalStoneValue = g.Sum(x => x.StoneValue),
                TotalMaking = g.Sum(x => x.Making)
            })
            .OrderBy(g => g.SubGroup)
            .ToList();
    }

    public async Task<IReadOnlyList<BarcodeStockReportItem>> GetHeadGroupBarcodeDetailsAsync(string headGroup, string subGroup, BarcodeStockReportFilter filter, CancellationToken cancellationToken = default)
    {
        var stock = await GetStockInHandAsync(filter, cancellationToken);
        return stock
            .Where(s => string.Equals(s.HeadGroup ?? "UNASSIGNED", headGroup, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(s.SubGroup ?? "UNASSIGNED", subGroup, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public async Task<IReadOnlyList<BarcodeGroupSummary>> GetStockSupplierWiseAsync(BarcodeStockReportFilter filter, CancellationToken cancellationToken = default)
    {
        var stock = await GetStockInHandAsync(filter, cancellationToken);
        return stock
            .GroupBy(s => string.IsNullOrWhiteSpace(s.SupplierName) ? "UNSPECIFIED" : s.SupplierName.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => new BarcodeGroupSummary
            {
                GroupKey = g.Key,
                ItemCount = g.Count(),
                TotalQty = g.Sum(x => x.Qty),
                TotalGrossWt = g.Sum(x => x.GrossWt),
                TotalStoneWt = g.Sum(x => x.StoneWt),
                TotalNetWt = g.Sum(x => x.NetWt),
                TotalStoneValue = g.Sum(x => x.StoneValue),
                TotalMaking = g.Sum(x => x.Making)
            })
            .OrderBy(g => g.GroupKey)
            .ToList();
    }

    public async Task<IReadOnlyList<BarcodeGroupSummary>> GetItemWiseStockAsync(BarcodeStockReportFilter filter, CancellationToken cancellationToken = default)
    {
        var stock = await GetStockInHandAsync(filter, cancellationToken);
        return stock
            .GroupBy(s => new {
                ItemName = string.IsNullOrWhiteSpace(s.ItemName) ? "UNNAMED" : s.ItemName.Trim(),
                SubGroup = string.IsNullOrWhiteSpace(s.SubGroup) ? "UNASSIGNED" : s.SubGroup.Trim(),
                Purity = string.IsNullOrWhiteSpace(s.Purity) ? "" : s.Purity.Trim()
            })
            .Select(g => new BarcodeGroupSummary
            {
                GroupKey = g.Key.ItemName,
                SubGroup = g.Key.SubGroup,
                Purity = g.Key.Purity,
                ItemCount = g.Count(),
                TotalQty = g.Sum(x => x.Qty),
                TotalGrossWt = g.Sum(x => x.GrossWt),
                TotalStoneWt = g.Sum(x => x.StoneWt),
                TotalNetWt = g.Sum(x => x.NetWt),
                TotalStoneValue = g.Sum(x => x.StoneValue),
                TotalMaking = g.Sum(x => x.Making)
            })
            .OrderBy(g => g.GroupKey)
            .ToList();
    }

    public async Task<IReadOnlyList<BarcodeStockReportItem>> GetItemWiseSalesAsync(BarcodeStockReportFilter filter, CancellationToken cancellationToken = default)
    {
        var list = new List<BarcodeStockReportItem>();
        var sb = new StringBuilder();
        var parameters = new List<OleDbParameter>();

        if (!string.IsNullOrWhiteSpace(filter.ItemHead) && filter.ItemHead != "*")
        {
            sb.Append(" AND i.[HeadGroup] = ?");
            parameters.Add(new OleDbParameter("?", OleDbType.VarWChar, 100) { Value = filter.ItemHead });
        }
        if (!string.IsNullOrWhiteSpace(filter.SubGroup) && filter.SubGroup != "*")
        {
            sb.Append(" AND i.[SubGroup] = ?");
            parameters.Add(new OleDbParameter("?", OleDbType.VarWChar, 100) { Value = filter.SubGroup });
        }
        if (!string.IsNullOrWhiteSpace(filter.Purity) && filter.Purity != "*")
        {
            sb.Append(" AND bs.[Purity] = ?");
            parameters.Add(new OleDbParameter("?", OleDbType.VarWChar, 50) { Value = filter.Purity });
        }
        if (!string.IsNullOrWhiteSpace(filter.ItemName) && filter.ItemName != "*")
        {
            sb.Append(" AND bs.[ItemName] = ?");
            parameters.Add(new OleDbParameter("?", OleDbType.VarWChar, 100) { Value = filter.ItemName });
        }
        if (filter.FromDate.HasValue)
        {
            sb.Append(" AND b.[BillDate] >= ?");
            parameters.Add(new OleDbParameter("?", OleDbType.Date) { Value = filter.FromDate.Value });
        }
        if (filter.ToDate.HasValue)
        {
            sb.Append(" AND b.[BillDate] <= ?");
            parameters.Add(new OleDbParameter("?", OleDbType.Date) { Value = filter.ToDate.Value });
        }
        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var st = $"%{filter.SearchText.Trim()}%";
            sb.Append(" AND (bs.[Barcode] LIKE ? OR bs.[ItemName] LIKE ? OR b.[VouNo] LIKE ? OR b.[CustomerName] LIKE ? OR b.[AccountName] LIKE ? OR b.[Phone] LIKE ?)");
            parameters.Add(new OleDbParameter("?", OleDbType.VarWChar, 100) { Value = st });
            parameters.Add(new OleDbParameter("?", OleDbType.VarWChar, 100) { Value = st });
            parameters.Add(new OleDbParameter("?", OleDbType.VarWChar, 100) { Value = st });
            parameters.Add(new OleDbParameter("?", OleDbType.VarWChar, 100) { Value = st });
            parameters.Add(new OleDbParameter("?", OleDbType.VarWChar, 100) { Value = st });
            parameters.Add(new OleDbParameter("?", OleDbType.VarWChar, 100) { Value = st });
        }

        var sql = $@"
            SELECT 
                b.[VouNo],
                b.[BillDate],
                b.[CustomerName],
                b.[AccountName],
                b.[Phone],
                bs.[Barcode],
                bs.[ItemName],
                bs.[Purity],
                bs.[Qty],
                bs.[GrWt],
                bs.[ExtraWt],
                bs.[NtWt],
                bs.[Rate],
                bs.[Amount],
                bs.[Making],
                bs.[StoneValue],
                i.[SubGroup],
                i.[HeadGroup]
            FROM (([BillSubform] bs
            INNER JOIN [Bill] b ON bs.[VouNo] = b.[VouNo])
            LEFT JOIN [ItemCreation] i ON bs.[ItemName] = i.[ItemName])
            WHERE (b.[BillCanceled] = False OR b.[BillCanceled] IS NULL)
              {sb}
            ORDER BY b.[BillDate] DESC, bs.[Barcode]";

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        foreach (var p in parameters)
        {
            command.Parameters.Add(p);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var custName = GetString(reader, "CustomerName");
            if (string.IsNullOrWhiteSpace(custName))
            {
                custName = GetString(reader, "AccountName");
            }

            list.Add(new BarcodeStockReportItem
            {
                VouNo = GetString(reader, "VouNo"),
                BillDate = GetDateTime(reader, "BillDate"),
                CustomerName = custName,
                Phone = GetString(reader, "Phone"),
                Barcode = GetString(reader, "Barcode"),
                ItemName = GetString(reader, "ItemName"),
                Purity = GetString(reader, "Purity"),
                Qty = GetDecimal(reader, "Qty"),
                GrossWt = GetDecimal(reader, "GrWt"),
                StoneWt = GetDecimal(reader, "ExtraWt"),
                NetWt = GetDecimal(reader, "NtWt"),
                Rate = GetDecimal(reader, "Rate"),
                Amount = GetDecimal(reader, "Amount"),
                Making = GetDecimal(reader, "Making"),
                StoneValue = GetDecimal(reader, "StoneValue"),
                SubGroup = GetString(reader, "SubGroup"),
                HeadGroup = GetString(reader, "HeadGroup")
            });
        }

        return list;
    }

    public async Task<IReadOnlyList<BarcodeStockReportItem>> GetItemWiseSalesReturnAsync(BarcodeStockReportFilter filter, CancellationToken cancellationToken = default)
    {
        var list = new List<BarcodeStockReportItem>();
        var sb = new StringBuilder();
        var parameters = new List<OleDbParameter>();

        if (!string.IsNullOrWhiteSpace(filter.ItemName) && filter.ItemName != "*")
        {
            sb.Append(" AND sr.[ItemName] = ?");
            parameters.Add(new OleDbParameter("?", OleDbType.VarWChar, 100) { Value = filter.ItemName });
        }
        if (!string.IsNullOrWhiteSpace(filter.Purity) && filter.Purity != "*")
        {
            sb.Append(" AND sr.[Purity] = ?");
            parameters.Add(new OleDbParameter("?", OleDbType.VarWChar, 50) { Value = filter.Purity });
        }

        var sql = $@"
            SELECT 
                sr.[Number] as VouNo,
                sr.[Barcode],
                sr.[ItemName],
                sr.[Purity],
                sr.[Qty],
                sr.[GrWt],
                sr.[ExtraWt],
                sr.[NtWt],
                sr.[Rate],
                sr.[Amount],
                sr.[Making],
                i.[SubGroup],
                i.[HeadGroup]
            FROM ([SalesReturnSubform] sr
            LEFT JOIN [ItemCreation] i ON sr.[ItemName] = i.[ItemName])
            WHERE 1=1 {sb}
            ORDER BY sr.[Number] DESC";

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        foreach (var p in parameters)
        {
            command.Parameters.Add(p);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new BarcodeStockReportItem
            {
                VouNo = GetString(reader, "VouNo"),
                Barcode = GetString(reader, "Barcode"),
                ItemName = GetString(reader, "ItemName"),
                Purity = GetString(reader, "Purity"),
                Qty = GetDecimal(reader, "Qty"),
                GrossWt = GetDecimal(reader, "GrWt"),
                StoneWt = GetDecimal(reader, "ExtraWt"),
                NetWt = GetDecimal(reader, "NtWt"),
                Rate = GetDecimal(reader, "Rate"),
                Amount = GetDecimal(reader, "Amount"),
                Making = GetDecimal(reader, "Making"),
                SubGroup = GetString(reader, "SubGroup"),
                HeadGroup = GetString(reader, "HeadGroup")
            });
        }

        return list;
    }

    public async Task<IReadOnlyList<BarcodeMovementItem>> GetBarcodeMovementAsync(string? barcode, BarcodeStockReportFilter filter, CancellationToken cancellationToken = default)
    {
        var list = new List<BarcodeMovementItem>();
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // 1. Inward Creation Records
        var inwardSql = @"
            SELECT [PDate], [Barcode], [ItemName], [VouNo], [SupplierName], [Qty], [GrossWt], [NetWt], [MRP], [EntryUser]
            FROM [BarcodeSubform]
            WHERE ([Status] IS NULL OR [Status] <> 'Deleted') " +
            (string.IsNullOrWhiteSpace(barcode) ? "" : " AND [Barcode] = ?");

        await using (var cmd = new OleDbCommand(inwardSql, connection))
        {
            if (!string.IsNullOrWhiteSpace(barcode))
            {
                cmd.Parameters.Add("?", OleDbType.VarWChar, 50).Value = barcode.Trim();
            }
            await using var r = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await r.ReadAsync(cancellationToken))
            {
                list.Add(new BarcodeMovementItem
                {
                    Date = GetDateTime(r, "PDate"),
                    Barcode = GetString(r, "Barcode"),
                    ItemName = GetString(r, "ItemName"),
                    MovementType = "Inward (Stock)",
                    ReferenceVouNo = GetString(r, "VouNo"),
                    PartyOrSupplier = GetString(r, "SupplierName"),
                    Qty = GetDecimal(r, "Qty"),
                    GrossWt = GetDecimal(r, "GrossWt"),
                    NetWt = GetDecimal(r, "NetWt"),
                    Amount = GetDecimal(r, "MRP"),
                    User = GetString(r, "EntryUser")
                });
            }
        }

        // 2. Outward Sales Records
        var saleSql = @"
            SELECT b.[BillDate], bs.[Barcode], bs.[ItemName], b.[VouNo], b.[CustomerName], bs.[Qty], bs.[GrWt], bs.[NtWt], bs.[Rate], bs.[Amount], b.[EntryUser]
            FROM ([BillSubform] bs
            INNER JOIN [Bill] b ON bs.[VouNo] = b.[VouNo])
            WHERE (b.[BillCanceled] = False OR b.[BillCanceled] IS NULL) " +
            (string.IsNullOrWhiteSpace(barcode) ? "" : " AND bs.[Barcode] = ?");

        await using (var cmd = new OleDbCommand(saleSql, connection))
        {
            if (!string.IsNullOrWhiteSpace(barcode))
            {
                cmd.Parameters.Add("?", OleDbType.VarWChar, 50).Value = barcode.Trim();
            }
            await using var r = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await r.ReadAsync(cancellationToken))
            {
                list.Add(new BarcodeMovementItem
                {
                    Date = GetDateTime(r, "BillDate"),
                    Barcode = GetString(r, "Barcode"),
                    ItemName = GetString(r, "ItemName"),
                    MovementType = "Outward (Sale)",
                    ReferenceVouNo = GetString(r, "VouNo"),
                    PartyOrSupplier = GetString(r, "CustomerName"),
                    Qty = GetDecimal(r, "Qty"),
                    GrossWt = GetDecimal(r, "GrWt"),
                    NetWt = GetDecimal(r, "NtWt"),
                    Rate = GetDecimal(r, "Rate"),
                    Amount = GetDecimal(r, "Amount"),
                    User = GetString(r, "EntryUser")
                });
            }
        }

        // 3. Conversion Records
        var convSql = @"
            SELECT [Date], [Barcode], [ItemName], [VouNo], [ConvertedTo], [Qty], [Weight], [NtWt], [Narration], [EntryUser]
            FROM [BarcodeConversion] " +
            (string.IsNullOrWhiteSpace(barcode) ? "" : " WHERE [Barcode] = ?");

        await using (var cmd = new OleDbCommand(convSql, connection))
        {
            if (!string.IsNullOrWhiteSpace(barcode))
            {
                cmd.Parameters.Add("?", OleDbType.VarWChar, 50).Value = barcode.Trim();
            }
            await using var r = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await r.ReadAsync(cancellationToken))
            {
                list.Add(new BarcodeMovementItem
                {
                    Date = GetDateTime(r, "Date"),
                    Barcode = GetString(r, "Barcode"),
                    ItemName = GetString(r, "ItemName"),
                    MovementType = "Conversion",
                    ReferenceVouNo = GetString(r, "VouNo"),
                    PartyOrSupplier = GetString(r, "ConvertedTo"),
                    Qty = GetDecimal(r, "Qty"),
                    GrossWt = GetDecimal(r, "Weight"),
                    NetWt = GetDecimal(r, "NtWt"),
                    Narration = GetString(r, "Narration"),
                    User = GetString(r, "EntryUser")
                });
            }
        }

        return list.OrderByDescending(m => m.Date).ToList();
    }

    public async Task<IReadOnlyList<BarcodeStockReportItem>> GetItemMismatchesAsync(BarcodeStockReportFilter filter, CancellationToken cancellationToken = default)
    {
        var allStock = await GetStockInHandAsync(filter, cancellationToken);
        var mismatches = new List<BarcodeStockReportItem>();

        foreach (var item in allStock)
        {
            var calculatedNet = item.GrossWt - item.StoneWt;
            if (Math.Abs(calculatedNet - item.NetWt) > 0.005m)
            {
                item.IsMismatch = true;
                item.MismatchReason = $"Weight Mismatch: GrWt ({item.GrossWt:F3}) - StnWt ({item.StoneWt:F3}) = {calculatedNet:F3} != NetWt ({item.NetWt:F3})";
                mismatches.Add(item);
            }
            else if (string.IsNullOrWhiteSpace(item.HeadGroup) || item.HeadGroup == "UNASSIGNED")
            {
                item.IsMismatch = true;
                item.MismatchReason = $"Item '{item.ItemName}' has no Head Group assigned in Item Master";
                mismatches.Add(item);
            }
            else if (item.GrossWt <= 0 && item.Qty <= 0)
            {
                item.IsMismatch = true;
                item.MismatchReason = "Zero Quantity and Zero Gross Weight";
                mismatches.Add(item);
            }
        }

        return mismatches;
    }

    private (IReadOnlyList<string> ItemHeads, IReadOnlyList<string> SubGroups, IReadOnlyList<string> Purities, IReadOnlyList<string> ItemNames, IReadOnlyList<string> Suppliers, IReadOnlyList<string> BoxNos)? _cachedMasters;

    public void InvalidateMasterCache()
    {
        _cachedMasters = null;
    }

    public async Task<(IReadOnlyList<string> ItemHeads, IReadOnlyList<string> SubGroups, IReadOnlyList<string> Purities, IReadOnlyList<string> ItemNames, IReadOnlyList<string> Suppliers, IReadOnlyList<string> BoxNos)> GetFilterMastersAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedMasters is not null)
        {
            return _cachedMasters.Value;
        }

        var itemHeads = new List<string> { "*" };
        var subGroups = new List<string> { "*" };
        var purities = new List<string> { "*" };
        var itemNames = new List<string> { "*" };
        var suppliers = new List<string> { "*" };
        var boxNos = new List<string> { "*" };

        try
        {
            await using var connection = new OleDbConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            // 1. Combined ItemCreation Masters (HeadGroup & SubGroup)
            await using (var cmd = new OleDbCommand("SELECT DISTINCT [HeadGroup], [SubGroup] FROM [ItemCreation]", connection))
            await using (var r = await cmd.ExecuteReaderAsync(cancellationToken))
            {
                while (await r.ReadAsync(cancellationToken))
                {
                    var h = r[0]?.ToString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(h) && !itemHeads.Contains(h)) itemHeads.Add(h);

                    var s = r[1]?.ToString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(s) && !subGroups.Contains(s)) subGroups.Add(s);
                }
            }

            // 2. Combined BarcodeSubform Masters (Purity, ItemName, SupplierName, BoxNo)
            await using (var cmd = new OleDbCommand("SELECT DISTINCT [Purity], [ItemName], [SupplierName], [BoxNo] FROM [BarcodeSubform]", connection))
            await using (var r = await cmd.ExecuteReaderAsync(cancellationToken))
            {
                while (await r.ReadAsync(cancellationToken))
                {
                    var p = r[0]?.ToString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(p) && !purities.Contains(p)) purities.Add(p);

                    var i = r[1]?.ToString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(i) && !itemNames.Contains(i)) itemNames.Add(i);

                    var s = r[2]?.ToString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(s) && !suppliers.Contains(s)) suppliers.Add(s);

                    var b = r[3]?.ToString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(b) && !boxNos.Contains(b)) boxNos.Add(b);
                }
            }

            // 3. Boxes from BoxMaster
            try
            {
                await using (var cmd = new OleDbCommand("SELECT DISTINCT [BoxNo] FROM [BoxMaster] WHERE [Status] IS NULL OR [Status] <> 'Deleted' ORDER BY [BoxNo]", connection))
                await using (var r = await cmd.ExecuteReaderAsync(cancellationToken))
                {
                    while (await r.ReadAsync(cancellationToken))
                    {
                        var b = r[0]?.ToString()?.Trim();
                        if (!string.IsNullOrWhiteSpace(b) && !boxNos.Contains(b)) boxNos.Add(b);
                    }
                }
            }
            catch { }
        }
        catch
        {
            // Fallbacks
            itemHeads.AddRange(new[] { "GOLD", "DIAMOND", "SILVER", "PLATINUM" });
            subGroups.AddRange(new[] { "RING", "CHAIN", "EARRING", "BANGLES", "NECKLACE" });
            purities.AddRange(new[] { "24ct", "22ct", "916HALL MARKED", "18ct", "14ct" });
        }

        _cachedMasters = (itemHeads, subGroups, purities, itemNames, suppliers, boxNos);
        return _cachedMasters.Value;
    }

    public async Task<IReadOnlyList<string>> GetBoxNosAsync(CancellationToken cancellationToken = default)
    {
        var masters = await GetFilterMastersAsync(cancellationToken);
        return masters.BoxNos;
    }

    private static (string WhereSql, List<OleDbParameter> Parameters) BuildStockWhereClause(BarcodeStockReportFilter filter)
    {
        var sb = new StringBuilder();
        var parameters = new List<OleDbParameter>();

        if (!string.IsNullOrWhiteSpace(filter.ItemHead) && filter.ItemHead != "*")
        {
            sb.Append(" AND i.[HeadGroup] = ?");
            parameters.Add(new OleDbParameter("?", OleDbType.VarWChar, 100) { Value = filter.ItemHead.Trim() });
        }

        if (!string.IsNullOrWhiteSpace(filter.SubGroup) && filter.SubGroup != "*")
        {
            sb.Append(" AND i.[SubGroup] = ?");
            parameters.Add(new OleDbParameter("?", OleDbType.VarWChar, 100) { Value = filter.SubGroup.Trim() });
        }

        if (!string.IsNullOrWhiteSpace(filter.Purity) && filter.Purity != "*")
        {
            sb.Append(" AND b.[Purity] = ?");
            parameters.Add(new OleDbParameter("?", OleDbType.VarWChar, 50) { Value = filter.Purity.Trim() });
        }

        if (!string.IsNullOrWhiteSpace(filter.ItemName) && filter.ItemName != "*")
        {
            sb.Append(" AND b.[ItemName] = ?");
            parameters.Add(new OleDbParameter("?", OleDbType.VarWChar, 100) { Value = filter.ItemName.Trim() });
        }

        if (!string.IsNullOrWhiteSpace(filter.SupplierName) && filter.SupplierName != "*")
        {
            sb.Append(" AND b.[SupplierName] = ?");
            parameters.Add(new OleDbParameter("?", OleDbType.VarWChar, 100) { Value = filter.SupplierName.Trim() });
        }

        if (!string.IsNullOrWhiteSpace(filter.BoxNo) && filter.BoxNo != "*")
        {
            sb.Append(" AND b.[BoxNo] = ?");
            parameters.Add(new OleDbParameter("?", OleDbType.VarWChar, 100) { Value = filter.BoxNo.Trim() });
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var st = $"%{filter.SearchText.Trim()}%";
            sb.Append(" AND (b.[Barcode] LIKE ? OR b.[ItemName] LIKE ? OR b.[Description] LIKE ? OR b.[SupplierName] LIKE ? OR b.[BoxNo] LIKE ?)");
            parameters.Add(new OleDbParameter("?", OleDbType.VarWChar, 100) { Value = st });
            parameters.Add(new OleDbParameter("?", OleDbType.VarWChar, 100) { Value = st });
            parameters.Add(new OleDbParameter("?", OleDbType.VarWChar, 100) { Value = st });
            parameters.Add(new OleDbParameter("?", OleDbType.VarWChar, 100) { Value = st });
            parameters.Add(new OleDbParameter("?", OleDbType.VarWChar, 100) { Value = st });
        }

        // Stock reports are cumulative as-of EndDate (ToDate) - all existing stock up to ToDate is included
        if (filter.ToDate.HasValue)
        {
            sb.Append(" AND (b.[PDate] IS NULL OR b.[PDate] <= ?)");
            var endOfDay = filter.ToDate.Value.Date.AddDays(1).AddSeconds(-1);
            parameters.Add(new OleDbParameter("?", OleDbType.Date) { Value = endOfDay });
        }

        return (sb.ToString(), parameters);
    }

    private static BarcodeStockReportItem MapStockItem(IDataRecord r) => new()
    {
        Barcode = GetString(r, "Barcode"),
        ItemName = GetString(r, "ItemName"),
        Qty = GetDecimal(r, "Qty", 1m),
        GrossWt = GetDecimal(r, "GrossWt"),
        StoneWt = GetDecimal(r, "StoneWt"),
        NetWt = GetDecimal(r, "NetWt"),
        StoneValue = GetDecimal(r, "StoneValue"),
        Making = GetDecimal(r, "Making"),
        Purity = GetString(r, "Purity"),
        SubGroup = GetString(r, "SubGroup"),
        HeadGroup = GetString(r, "HeadGroup"),
        SupplierName = GetString(r, "SupplierName"),
        CostPrice = GetDecimal(r, "CostPrice"),
        MRP = GetDecimal(r, "MRP"),
        PDate = GetDateTime(r, "PDate"),
        Status = GetString(r, "Status", "Active"),
        HUID = GetString(r, "HUID"),
        Size = GetString(r, "Size"),
        OldBarcode = GetString(r, "OldBarcode"),
        BType = GetString(r, "BType"),
        Description = GetString(r, "Description"),
        BoxNo = GetString(r, "BoxNo"),
        Godown = GetString(r, "Godown"),
        LastScan = GetDateTime(r, "LastScan") ?? GetDateTime(r, "ScanTime")
    };

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

    public async Task UpdateMrpAsync(string barcode, decimal mrp, CancellationToken cancellationToken = default)
    {
        var rawCode = barcode?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(rawCode)) return;

        var d6Code = int.TryParse(rawCode, out var n) ? n.ToString("D6") : rawCode;

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = "UPDATE [BarcodeSubform] SET [MRP] = ? WHERE ([Barcode] = ? OR [Barcode] = ?)";
        await using var cmd = new OleDbCommand(sql, connection);
        cmd.Parameters.Add(new OleDbParameter("?", OleDbType.Currency) { Value = mrp });
        cmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar, 50) { Value = rawCode });
        cmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar, 50) { Value = d6Code });
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public Task UpdateBoxNoAsync(string barcode, string boxNo, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(barcode)) return Task.CompletedTask;
        return UpdateBoxNosAsync([barcode], boxNo, cancellationToken);
    }

    public async Task UpdateBoxNosAsync(IEnumerable<string> barcodes, string boxNo, CancellationToken cancellationToken = default)
    {
        var codeList = barcodes?.Where(b => !string.IsNullOrWhiteSpace(b)).Distinct().ToList();
        if (codeList == null || codeList.Count == 0) return;

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = "UPDATE [BarcodeSubform] SET [BoxNo] = ? WHERE ([Barcode] = ? OR [Barcode] = ?)";
        await using var cmd = new OleDbCommand(sql, connection);
        var pBox = cmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar, 100));
        var pRaw = cmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar, 50));
        var pD6 = cmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar, 50));

        pBox.Value = boxNo?.Trim() ?? string.Empty;

        foreach (var barcode in codeList)
        {
            var rawCode = barcode.Trim();
            var d6Code = int.TryParse(rawCode, out var n) ? n.ToString("D6") : rawCode;
            pRaw.Value = rawCode;
            pD6.Value = d6Code;
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static void EnsureStockCheckTable(OleDbConnection connection)
    {
        try
        {
            using var checkCmd = new OleDbCommand("SELECT COUNT(*) FROM [BarcodeCheck] WHERE 1=0", connection);
            checkCmd.ExecuteNonQuery();
        }
        catch
        {
            try
            {
                using var createCmd = new OleDbCommand(@"CREATE TABLE [BarcodeCheck] (
                    [Barcode] TEXT(50) PRIMARY KEY,
                    [ItemName] TEXT(100),
                    [GrossWt] CURRENCY,
                    [StWt] CURRENCY,
                    [NetWt] CURRENCY,
                    [StVl] CURRENCY,
                    [Making] CURRENCY,
                    [Qty] LONG,
                    [Purity] TEXT(50),
                    [ScanTime] DATETIME,
                    [Size] TEXT(50)
                )", connection);
                createCmd.ExecuteNonQuery();
            }
            catch { }
        }

        try
        {
            using var alterCmd = new OleDbCommand("ALTER TABLE [BarcodeCheck] ADD COLUMN [ScanTime] DATETIME", connection);
            alterCmd.ExecuteNonQuery();
        }
        catch { }

        try
        {
            using var alterCmd = new OleDbCommand("ALTER TABLE [BarcodeCheck] ADD COLUMN [Size] TEXT(50)", connection);
            alterCmd.ExecuteNonQuery();
        }
        catch { }
    }

    public async Task<IReadOnlyList<BarcodeStockReportItem>> GetScannedStockCheckItemsAsync(CancellationToken cancellationToken = default)
    {
        var list = new List<BarcodeStockReportItem>();
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        EnsureStockCheckTable(connection);

        try
        {
            const string sql = @"
                SELECT c.[Barcode], 
                       IIF(b.[ItemName] IS NOT NULL, b.[ItemName], c.[ItemName]) AS [ItemName],
                       IIF(b.[GrossWt] IS NOT NULL AND b.[GrossWt] > 0, b.[GrossWt], c.[GrossWt]) AS [GrossWt],
                       IIF(b.[StoneWt] IS NOT NULL, b.[StoneWt], c.[StWt]) AS [StoneWt],
                       IIF(b.[NetWt] IS NOT NULL AND b.[NetWt] > 0, b.[NetWt], c.[NetWt]) AS [NetWt],
                       IIF(b.[StoneValue] IS NOT NULL, b.[StoneValue], c.[StVl]) AS [StoneValue],
                       IIF(b.[Making] IS NOT NULL AND b.[Making] > 0, b.[Making], c.[Making]) AS [Making],
                       IIF(b.[Qty] IS NOT NULL AND b.[Qty] > 0, b.[Qty], c.[Qty]) AS [Qty],
                       IIF(b.[Purity] IS NOT NULL, b.[Purity], c.[Purity]) AS [Purity],
                       b.[CostPrice], b.[MRP], b.[SupplierName], b.[BoxNo], b.[Description],
                       IIF(b.[Size] IS NOT NULL AND b.[Size] <> '', b.[Size], c.[Size]) AS [Size],
                       b.[Photo], b.[BType], b.[Godown],
                       c.[ScanTime] AS [LastScan],
                       i.[HeadGroup], i.[SubGroup]
                FROM ([BarcodeCheck] c
                LEFT JOIN [BarcodeSubform] b ON c.[Barcode] = b.[Barcode])
                LEFT JOIN [ItemCreation] i ON b.[ItemName] = i.[ItemName]";

            await using var cmd = new OleDbCommand(sql, connection);
            await using var r = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await r.ReadAsync(cancellationToken))
            {
                var item = MapStockItem(r);
                list.Add(item);
            }

            return list;
        }
        catch
        {
            list.Clear();
            const string fallbackSql = @"
                SELECT c.[Barcode], 
                       IIF(b.[ItemName] IS NOT NULL, b.[ItemName], c.[ItemName]) AS [ItemName],
                       IIF(b.[GrossWt] IS NOT NULL AND b.[GrossWt] > 0, b.[GrossWt], c.[GrossWt]) AS [GrossWt],
                       IIF(b.[StoneWt] IS NOT NULL, b.[StoneWt], c.[StWt]) AS [StoneWt],
                       IIF(b.[NetWt] IS NOT NULL AND b.[NetWt] > 0, b.[NetWt], c.[NetWt]) AS [NetWt],
                       IIF(b.[StoneValue] IS NOT NULL, b.[StoneValue], c.[StVl]) AS [StoneValue],
                       IIF(b.[Making] IS NOT NULL AND b.[Making] > 0, b.[Making], c.[Making]) AS [Making],
                       IIF(b.[Qty] IS NOT NULL AND b.[Qty] > 0, b.[Qty], c.[Qty]) AS [Qty],
                       IIF(b.[Purity] IS NOT NULL, b.[Purity], c.[Purity]) AS [Purity],
                       b.[CostPrice], b.[MRP], b.[SupplierName], b.[BoxNo], b.[Description], b.[Size], b.[Photo], b.[BType], b.[Godown],
                       i.[HeadGroup], i.[SubGroup]
                FROM ([BarcodeCheck] c
                LEFT JOIN [BarcodeSubform] b ON c.[Barcode] = b.[Barcode])
                LEFT JOIN [ItemCreation] i ON b.[ItemName] = i.[ItemName]";

            await using var cmd = new OleDbCommand(fallbackSql, connection);
            await using var r = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await r.ReadAsync(cancellationToken))
            {
                var item = MapStockItem(r);
                list.Add(item);
            }

            return list;
        }
    }

    public async Task SaveScannedStockCheckItemAsync(BarcodeStockReportItem item, CancellationToken cancellationToken = default)
    {
        var barcode = item.Barcode?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(barcode)) return;

        var d6Code = int.TryParse(barcode, out var n) ? n.ToString("D6") : barcode;

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        EnsureStockCheckTable(connection);

        try
        {
            const string delSql = "DELETE FROM [BarcodeCheck] WHERE [Barcode] = ? OR [Barcode] = ?";
            await using var delCmd = new OleDbCommand(delSql, connection);
            delCmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar, 50) { Value = barcode });
            delCmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar, 50) { Value = d6Code });
            await delCmd.ExecuteNonQueryAsync(cancellationToken);
        }
        catch { }

        try
        {
            const string insertSql = @"INSERT INTO [BarcodeCheck] 
                ([Barcode], [ItemName], [GrossWt], [StWt], [NetWt], [StVl], [Making], [Qty], [Purity], [ScanTime], [Size])
                VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)";
            await using var insCmd = new OleDbCommand(insertSql, connection);
            insCmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar, 50) { Value = barcode });
            insCmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar, 100) { Value = item.ItemName?.Trim() ?? string.Empty });
            insCmd.Parameters.Add(new OleDbParameter("?", OleDbType.Currency) { Value = item.GrossWt });
            insCmd.Parameters.Add(new OleDbParameter("?", OleDbType.Currency) { Value = item.StoneWt });
            insCmd.Parameters.Add(new OleDbParameter("?", OleDbType.Currency) { Value = item.NetWt });
            insCmd.Parameters.Add(new OleDbParameter("?", OleDbType.Currency) { Value = item.StoneValue });
            insCmd.Parameters.Add(new OleDbParameter("?", OleDbType.Currency) { Value = item.Making });
            insCmd.Parameters.Add(new OleDbParameter("?", OleDbType.Integer) { Value = (int)item.Qty });
            insCmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar, 50) { Value = item.Purity?.Trim() ?? string.Empty });
            insCmd.Parameters.Add(new OleDbParameter("?", OleDbType.Date) { Value = (object?)item.LastScan ?? (object?)item.TimeStamp ?? DateTime.Now });
            insCmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar, 50) { Value = item.Size?.Trim() ?? string.Empty });
            await insCmd.ExecuteNonQueryAsync(cancellationToken);
        }
        catch
        {
            const string insertSql = @"INSERT INTO [BarcodeCheck] 
                ([Barcode], [ItemName], [GrossWt], [StWt], [NetWt], [StVl], [Making], [Qty], [Purity])
                VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)";
            await using var insCmd = new OleDbCommand(insertSql, connection);
            insCmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar, 50) { Value = barcode });
            insCmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar, 100) { Value = item.ItemName?.Trim() ?? string.Empty });
            insCmd.Parameters.Add(new OleDbParameter("?", OleDbType.Currency) { Value = item.GrossWt });
            insCmd.Parameters.Add(new OleDbParameter("?", OleDbType.Currency) { Value = item.StoneWt });
            insCmd.Parameters.Add(new OleDbParameter("?", OleDbType.Currency) { Value = item.NetWt });
            insCmd.Parameters.Add(new OleDbParameter("?", OleDbType.Currency) { Value = item.StoneValue });
            insCmd.Parameters.Add(new OleDbParameter("?", OleDbType.Currency) { Value = item.Making });
            insCmd.Parameters.Add(new OleDbParameter("?", OleDbType.Integer) { Value = (int)item.Qty });
            insCmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar, 50) { Value = item.Purity?.Trim() ?? string.Empty });
            await insCmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public async Task RemoveScannedStockCheckItemAsync(string barcode, CancellationToken cancellationToken = default)
    {
        var rawCode = barcode?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(rawCode)) return;

        var d6Code = int.TryParse(rawCode, out var n) ? n.ToString("D6") : rawCode;

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        EnsureStockCheckTable(connection);

        const string sql = "DELETE FROM [BarcodeCheck] WHERE [Barcode] = ? OR [Barcode] = ?";
        await using var cmd = new OleDbCommand(sql, connection);
        cmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar, 50) { Value = rawCode });
        cmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar, 50) { Value = d6Code });
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task ClearScannedStockCheckItemsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        EnsureStockCheckTable(connection);

        const string sql = "DELETE FROM [BarcodeCheck]";
        await using var cmd = new OleDbCommand(sql, connection);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<BarcodeStockReportItem?> LookupBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(barcode)) return null;

        var code = barcode.Trim();
        var padded = code.PadLeft(6, '0');

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            SELECT 
                b.[Barcode], 
                b.[ItemName], 
                b.[Qty], 
                b.[GrossWt], 
                b.[StoneWt], 
                b.[NetWt], 
                b.[StoneValue], 
                b.[Making], 
                b.[Purity], 
                i.[SubGroup], 
                i.[HeadGroup],
                b.[SupplierName],
                b.[CostPrice],
                b.[MRP],
                b.[PDate],
                b.[Status],
                b.[Size],
                b.[OldBarcode],
                b.[BType],
                b.[Description],
                b.[Photo],
                b.[BoxNo],
                b.[Godown]
            FROM [BarcodeSubform] b 
            LEFT JOIN [ItemCreation] i ON b.[ItemName] = i.[ItemName]
            WHERE (b.[Barcode] = ? OR b.[Barcode] = ?)
              AND (b.[Status] IS NULL OR b.[Status] <> 'Deleted')
              AND (b.[Godown] IS NULL OR b.[Godown] <> 'Damage')";

        await using var cmd = new OleDbCommand(sql, connection);
        cmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar, 50) { Value = code });
        cmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar, 50) { Value = padded });

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapStockItem(reader);
        }

        return null;
    }
}
