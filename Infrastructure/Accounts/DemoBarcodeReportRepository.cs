using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class DemoBarcodeReportRepository : IBarcodeReportRepository
{
    private readonly List<BarcodeStockReportItem> _stock = new();
    private readonly List<BarcodeStockReportItem> _sales = new();
    private readonly List<BarcodeMovementItem> _movements = new();
    private readonly IBarcodeCreationRepository? _creationRepo;

    public DemoBarcodeReportRepository(IBarcodeCreationRepository? creationRepo = null)
    {
        _creationRepo = creationRepo;
        // Seed sample stock items matching screenshot
        _stock.Add(new BarcodeStockReportItem
        {
            Barcode = "560016",
            ItemName = "24 Ct Gold Coin",
            Qty = 1,
            GrossWt = 0.200m,
            StoneWt = 0.000m,
            NetWt = 0.200m,
            StoneValue = 0.00m,
            Making = 338.00m,
            Purity = "24ct",
            SubGroup = "24ct",
            HeadGroup = "24ct",
            SupplierName = "MMTC-PAMP",
            MRP = 1800m
        });
        _stock.Add(new BarcodeStockReportItem
        {
            Barcode = "563683",
            ItemName = "24 Ct Gold Coin",
            Qty = 1,
            GrossWt = 1.000m,
            StoneWt = 0.000m,
            NetWt = 1.000m,
            StoneValue = 0.00m,
            Making = 375.00m,
            Purity = "24ct",
            SubGroup = "24ct",
            HeadGroup = "24ct",
            SupplierName = "MMTC-PAMP",
            MRP = 8500m
        });
        _stock.Add(new BarcodeStockReportItem
        {
            Barcode = "560398",
            ItemName = "24 Ct Gold Coin",
            Qty = 1,
            GrossWt = 0.200m,
            StoneWt = 0.000m,
            NetWt = 0.200m,
            StoneValue = 0.00m,
            Making = 338.00m,
            Purity = "24ct",
            SubGroup = "24ct",
            HeadGroup = "24ct",
            SupplierName = "MMTC-PAMP",
            MRP = 1800m
        });
        _stock.Add(new BarcodeStockReportItem
        {
            Barcode = "004416",
            ItemName = "CHAIN",
            Qty = 1,
            GrossWt = 9.120m,
            StoneWt = 0.000m,
            NetWt = 9.120m,
            StoneValue = 0.00m,
            Making = 540.00m,
            Purity = "916HALL MARKED",
            SubGroup = "CHAIN",
            HeadGroup = "GOLD",
            SupplierName = "ROYAL CHAINS PVT. LTD.",
            MRP = 68000m
        });
        _stock.Add(new BarcodeStockReportItem
        {
            Barcode = "004510",
            ItemName = "EARRING",
            Qty = 2,
            GrossWt = 12.170m,
            StoneWt = 0.500m,
            NetWt = 11.670m,
            StoneValue = 1500.00m,
            Making = 890.00m,
            Purity = "916HALL MARKED",
            SubGroup = "KAN",
            HeadGroup = "GOLD",
            SupplierName = "PALAKJEWELLERS PVT.LTD",
            MRP = 92000m
        });

        // Seed sample items matching Stock Check reference screenshot
        _stock.Add(new BarcodeStockReportItem
        {
            Barcode = "004891",
            ItemName = "BRACELET (GENTS)",
            Qty = 1,
            GrossWt = 31.900m,
            StoneWt = 0.000m,
            NetWt = 31.900m,
            StoneValue = 0.00m,
            Making = 701.80m,
            Purity = "Silver",
            SubGroup = "BRACELET (GENTS)",
            HeadGroup = "SILVER",
            Size = "8.5",
            LastScan = new DateTime(2026, 5, 5, 13, 30, 0)
        });
        _stock.Add(new BarcodeStockReportItem
        {
            Barcode = "004892",
            ItemName = "BRACELET (GENTS)",
            Qty = 1,
            GrossWt = 33.880m,
            StoneWt = 0.000m,
            NetWt = 33.880m,
            StoneValue = 0.00m,
            Making = 745.36m,
            Purity = "Silver",
            SubGroup = "BRACELET (GENTS)",
            HeadGroup = "SILVER",
            Size = "8.5",
            LastScan = new DateTime(2026, 5, 5, 13, 31, 8)
        });
        _stock.Add(new BarcodeStockReportItem
        {
            Barcode = "006202",
            ItemName = "BRACELET (GENTS)",
            Qty = 1,
            GrossWt = 71.900m,
            StoneWt = 0.000m,
            NetWt = 71.900m,
            StoneValue = 0.00m,
            Making = 1797.50m,
            Purity = "Silver",
            SubGroup = "BRACELET (GENTS)",
            HeadGroup = "SILVER",
            Size = "9.0",
            LastScan = new DateTime(2026, 5, 5, 13, 31, 18)
        });
        _stock.Add(new BarcodeStockReportItem
        {
            Barcode = "006205",
            ItemName = "BRACELET (GENTS)",
            Qty = 1,
            GrossWt = 44.900m,
            StoneWt = 0.000m,
            NetWt = 44.900m,
            StoneValue = 0.00m,
            Making = 1122.50m,
            Purity = "Silver",
            SubGroup = "BRACELET (GENTS)",
            HeadGroup = "SILVER",
            LastScan = new DateTime(2026, 5, 5, 13, 31, 32)
        });
        _stock.Add(new BarcodeStockReportItem
        {
            Barcode = "006206",
            ItemName = "BRACELET (GENTS)",
            Qty = 1,
            GrossWt = 38.000m,
            StoneWt = 0.000m,
            NetWt = 38.000m,
            StoneValue = 0.00m,
            Making = 950.00m,
            Purity = "Silver",
            SubGroup = "BRACELET (GENTS)",
            HeadGroup = "SILVER",
            LastScan = new DateTime(2026, 5, 5, 13, 31, 41)
        });
        _stock.Add(new BarcodeStockReportItem
        {
            Barcode = "007245",
            ItemName = "BRACELET (GENTS)",
            Qty = 1,
            GrossWt = 35.000m,
            StoneWt = 0.000m,
            NetWt = 35.000m,
            StoneValue = 0.00m,
            Making = 1050.00m,
            Purity = "Silver",
            SubGroup = "BRACELET (GENTS)",
            HeadGroup = "SILVER",
            LastScan = new DateTime(2026, 5, 5, 13, 31, 48)
        });
        _stock.Add(new BarcodeStockReportItem
        {
            Barcode = "007404",
            ItemName = "BRACELET (GENTS)",
            Qty = 1,
            GrossWt = 29.500m,
            StoneWt = 0.000m,
            NetWt = 29.500m,
            StoneValue = 0.00m,
            Making = 885.00m,
            Purity = "Silver",
            SubGroup = "BRACELET (GENTS)",
            HeadGroup = "SILVER",
            LastScan = new DateTime(2026, 5, 5, 13, 31, 24)
        });
        _stock.Add(new BarcodeStockReportItem
        {
            Barcode = "008755",
            ItemName = "BRACELET (GENTS)",
            Qty = 1,
            GrossWt = 24.060m,
            StoneWt = 0.000m,
            NetWt = 24.060m,
            StoneValue = 0.00m,
            Making = 962.40m,
            Purity = "Silver",
            SubGroup = "BRACELET (GENTS)",
            HeadGroup = "SILVER",
            LastScan = new DateTime(2026, 5, 5, 13, 32, 6)
        });

        _sales.Add(new BarcodeStockReportItem
        {
            VouNo = "AR00378",
            BillDate = new DateTime(2026, 7, 19),
            CustomerName = "Binod Ku Agarwal",
            Phone = "919338408031",
            Barcode = "007929",
            ItemName = "Locket 925",
            Qty = 1,
            GrossWt = 1.110m,
            StoneWt = 0.000m,
            NetWt = 1.110m,
            StoneValue = 0.00m,
            Making = 0.00m,
            Purity = "Silver",
            SubGroup = "Locket",
            HeadGroup = "SILVER"
        });
        _sales.Add(new BarcodeStockReportItem
        {
            VouNo = "AR00381",
            BillDate = new DateTime(2026, 7, 19),
            CustomerName = "Rajesh Sharma",
            Phone = "918249161620",
            Barcode = "002903",
            ItemName = "Locket 925",
            Qty = 1,
            GrossWt = 2.310m,
            StoneWt = 0.000m,
            NetWt = 2.310m,
            StoneValue = 0.00m,
            Making = 0.00m,
            Purity = "Silver",
            SubGroup = "Locket",
            HeadGroup = "SILVER"
        });
        _sales.Add(new BarcodeStockReportItem
        {
            VouNo = "AR00385",
            BillDate = new DateTime(2026, 7, 20),
            CustomerName = "Pooja Hegde",
            Phone = "917377568177",
            Barcode = "007935",
            ItemName = "Locket 925",
            Qty = 1,
            GrossWt = 2.040m,
            StoneWt = 0.000m,
            NetWt = 2.040m,
            StoneValue = 0.00m,
            Making = 0.00m,
            Purity = "Silver",
            SubGroup = "Locket",
            HeadGroup = "SILVER"
        });
        _sales.Add(new BarcodeStockReportItem
        {
            VouNo = "AR00392",
            BillDate = new DateTime(2026, 7, 22),
            CustomerName = "Kavita Deshmukh",
            Phone = "917750854620",
            Barcode = "007863",
            ItemName = "Locket 925",
            Qty = 1,
            GrossWt = 2.440m,
            StoneWt = 0.000m,
            NetWt = 2.440m,
            StoneValue = 0.00m,
            Making = 0.00m,
            Purity = "Silver",
            SubGroup = "Locket",
            HeadGroup = "SILVER"
        });
        _sales.Add(new BarcodeStockReportItem
        {
            VouNo = "AR00401",
            BillDate = new DateTime(2026, 7, 25),
            CustomerName = "Anil Mehta",
            Phone = "919040798230",
            Barcode = "008733",
            ItemName = "Locket 925",
            Qty = 1,
            GrossWt = 3.370m,
            StoneWt = 0.000m,
            NetWt = 3.370m,
            StoneValue = 0.00m,
            Making = 0.00m,
            Purity = "Silver",
            SubGroup = "Locket",
            HeadGroup = "SILVER"
        });
    }

    public async Task<IReadOnlyList<BarcodeStockReportItem>> GetStockInHandAsync(BarcodeStockReportFilter filter, CancellationToken cancellationToken = default)
    {
        if (_creationRepo != null)
        {
            try
            {
                var createdItems = await _creationRepo.GetOpeningBarcodesAsync(100, cancellationToken);
                foreach (var ci in createdItems)
                {
                    if (!string.IsNullOrEmpty(ci.Barcode) && !_stock.Any(s => string.Equals(s.Barcode, ci.Barcode, StringComparison.OrdinalIgnoreCase)))
                    {
                        _stock.Add(new BarcodeStockReportItem
                        {
                            Barcode = ci.Barcode,
                            ItemName = ci.ItemName,
                            Qty = ci.Qty,
                            GrossWt = ci.GrossWt,
                            StoneWt = ci.StoneWt,
                            NetWt = ci.NetWt,
                            StoneValue = ci.StoneValue,
                            Making = ci.Making,
                            Purity = ci.Purity,
                            SubGroup = ci.ItemName,
                            HeadGroup = ci.Purity?.Contains("Silver", StringComparison.OrdinalIgnoreCase) == true ? "SILVER" : "GOLD",
                            SupplierName = ci.SupplierName,
                            CostPrice = ci.CostPrice,
                            MRP = ci.MRP,
                            PDate = ci.PDate,
                            Status = ci.Status ?? "Active",
                            Size = ci.Size,
                            BType = ci.BType,
                            Description = ci.Description,
                            BoxNo = ci.BoxNo,
                            Godown = ci.Godown
                        });
                    }
                }
            }
            catch { }
        }

        var result = ApplyFilter(_stock, filter).OrderByDescending(x => x.Barcode).ToList();
        return result;
    }

    public Task<IReadOnlyList<BarcodeGroupSummary>> GetStockHeadGroupWiseAsync(BarcodeStockReportFilter filter, CancellationToken cancellationToken = default)
    {
        var filtered = ApplyFilter(_stock, filter);
        var grouped = filtered
            .GroupBy(s => new { s.HeadGroup, s.Purity })
            .Select(g => new BarcodeGroupSummary
            {
                GroupKey = g.Key.HeadGroup,
                Purity = g.Key.Purity,
                ItemCount = g.Count(),
                TotalQty = g.Sum(x => x.Qty),
                TotalGrossWt = g.Sum(x => x.GrossWt),
                TotalStoneWt = g.Sum(x => x.StoneWt),
                TotalNetWt = g.Sum(x => x.NetWt),
                TotalStoneValue = g.Sum(x => x.StoneValue),
                TotalMaking = g.Sum(x => x.Making)
            })
            .ToList();
        return Task.FromResult<IReadOnlyList<BarcodeGroupSummary>>(grouped);
    }

    public Task<IReadOnlyList<BarcodeGroupSummary>> GetStockSupplierWiseAsync(BarcodeStockReportFilter filter, CancellationToken cancellationToken = default)
    {
        var filtered = ApplyFilter(_stock, filter);
        var grouped = filtered
            .GroupBy(s => s.SupplierName)
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
            .ToList();
        return Task.FromResult<IReadOnlyList<BarcodeGroupSummary>>(grouped);
    }

    public Task<IReadOnlyList<BarcodeGroupSummary>> GetItemWiseStockAsync(BarcodeStockReportFilter filter, CancellationToken cancellationToken = default)
    {
        var filtered = ApplyFilter(_stock, filter);
        var grouped = filtered
            .GroupBy(s => new { s.ItemName, s.SubGroup, s.HeadGroup, s.Purity })
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
            .ToList();
        return Task.FromResult<IReadOnlyList<BarcodeGroupSummary>>(grouped);
    }

    public Task<IReadOnlyList<BarcodeStockReportItem>> GetItemWiseSalesAsync(BarcodeStockReportFilter filter, CancellationToken cancellationToken = default)
    {
        var query = _sales.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(filter.ItemHead) && filter.ItemHead != "*")
            query = query.Where(x => string.Equals(x.HeadGroup, filter.ItemHead, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(filter.SubGroup) && filter.SubGroup != "*")
            query = query.Where(x => string.Equals(x.SubGroup, filter.SubGroup, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(filter.Purity) && filter.Purity != "*")
            query = query.Where(x => string.Equals(x.Purity, filter.Purity, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(filter.ItemName) && filter.ItemName != "*")
            query = query.Where(x => string.Equals(x.ItemName, filter.ItemName, StringComparison.OrdinalIgnoreCase));
        if (filter.FromDate.HasValue)
            query = query.Where(x => x.BillDate >= filter.FromDate.Value);
        if (filter.ToDate.HasValue)
            query = query.Where(x => x.BillDate <= filter.ToDate.Value);
        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var st = filter.SearchText.Trim();
            query = query.Where(x =>
                x.Barcode.Contains(st, StringComparison.OrdinalIgnoreCase) ||
                x.ItemName.Contains(st, StringComparison.OrdinalIgnoreCase) ||
                x.VouNo.Contains(st, StringComparison.OrdinalIgnoreCase) ||
                x.CustomerName.Contains(st, StringComparison.OrdinalIgnoreCase) ||
                x.Phone.Contains(st, StringComparison.OrdinalIgnoreCase));
        }

        return Task.FromResult<IReadOnlyList<BarcodeStockReportItem>>(query.ToList());
    }

    public Task<IReadOnlyList<BarcodeStockReportItem>> GetItemWiseSalesReturnAsync(BarcodeStockReportFilter filter, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<BarcodeStockReportItem>>(new List<BarcodeStockReportItem>());
    }

    public Task<IReadOnlyList<BarcodeMovementItem>> GetBarcodeMovementAsync(string? barcode, BarcodeStockReportFilter filter, CancellationToken cancellationToken = default)
    {
        var result = string.IsNullOrWhiteSpace(barcode)
            ? _movements
            : _movements.Where(m => string.Equals(m.Barcode, barcode, StringComparison.OrdinalIgnoreCase)).ToList();
        return Task.FromResult<IReadOnlyList<BarcodeMovementItem>>(result);
    }

    public Task<IReadOnlyList<BarcodeGroupSummary>> GetHeadGroupSummariesAsync(BarcodeStockReportFilter filter, CancellationToken cancellationToken = default)
    {
        var filtered = ApplyFilter(_stock, filter);
        var grouped = filtered
            .GroupBy(s => s.HeadGroup)
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
            .ToList();
        return Task.FromResult<IReadOnlyList<BarcodeGroupSummary>>(grouped);
    }

    public Task<IReadOnlyList<BarcodeGroupSummary>> GetSubGroupSummariesAsync(string headGroup, BarcodeStockReportFilter filter, CancellationToken cancellationToken = default)
    {
        var filtered = ApplyFilter(_stock, filter).Where(s => string.Equals(s.HeadGroup, headGroup, StringComparison.OrdinalIgnoreCase));
        var grouped = filtered
            .GroupBy(s => new { s.HeadGroup, s.SubGroup })
            .Select(g => new BarcodeGroupSummary
            {
                GroupKey = g.Key.SubGroup,
                HeadGroup = g.Key.HeadGroup,
                SubGroup = g.Key.SubGroup,
                ItemCount = g.Count(),
                TotalQty = g.Sum(x => x.Qty),
                TotalGrossWt = g.Sum(x => x.GrossWt),
                TotalStoneWt = g.Sum(x => x.StoneWt),
                TotalNetWt = g.Sum(x => x.NetWt),
                TotalStoneValue = g.Sum(x => x.StoneValue),
                TotalMaking = g.Sum(x => x.Making)
            })
            .ToList();
        return Task.FromResult<IReadOnlyList<BarcodeGroupSummary>>(grouped);
    }

    public Task<IReadOnlyList<BarcodeStockReportItem>> GetHeadGroupBarcodeDetailsAsync(string headGroup, string subGroup, BarcodeStockReportFilter filter, CancellationToken cancellationToken = default)
    {
        var filtered = ApplyFilter(_stock, filter)
            .Where(s => string.Equals(s.HeadGroup, headGroup, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(s.SubGroup, subGroup, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return Task.FromResult<IReadOnlyList<BarcodeStockReportItem>>(filtered);
    }

    public Task<IReadOnlyList<BarcodeStockReportItem>> GetItemMismatchesAsync(BarcodeStockReportFilter filter, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<BarcodeStockReportItem>>(new List<BarcodeStockReportItem>());
    }

    public Task<(IReadOnlyList<string> ItemHeads, IReadOnlyList<string> SubGroups, IReadOnlyList<string> Purities, IReadOnlyList<string> ItemNames, IReadOnlyList<string> Suppliers, IReadOnlyList<string> BoxNos)> GetFilterMastersAsync(CancellationToken cancellationToken = default)
    {
        var heads = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "*", "24ct", "GOLD", "DIAMOND", "SILVER" };
        var subs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "*", "24ct", "CHAIN", "KAN", "RING", "BRACELET (GENTS)" };
        var purs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "*", "24ct", "916HALL MARKED", "18ct", "Silver" };
        var items = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "*", "24 Ct Gold Coin", "CHAIN", "EARRING", "RING", "BRACELET (GENTS)" };
        var supps = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "*", "MMTC-PAMP", "ROYAL CHAINS PVT. LTD.", "PALAKJEWELLERS PVT.LTD" };
        var boxes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "*", "CHA01", "CHA02", "RNG01", "RNG02", "BNG01", "EAR01" };

        foreach (var s in _stock)
        {
            if (!string.IsNullOrWhiteSpace(s.HeadGroup)) heads.Add(s.HeadGroup);
            if (!string.IsNullOrWhiteSpace(s.SubGroup)) subs.Add(s.SubGroup);
            if (!string.IsNullOrWhiteSpace(s.Purity)) purs.Add(s.Purity);
            if (!string.IsNullOrWhiteSpace(s.ItemName)) items.Add(s.ItemName);
            if (!string.IsNullOrWhiteSpace(s.SupplierName)) supps.Add(s.SupplierName);
            if (!string.IsNullOrWhiteSpace(s.BoxNo)) boxes.Add(s.BoxNo);
        }

        return Task.FromResult< (IReadOnlyList<string>, IReadOnlyList<string>, IReadOnlyList<string>, IReadOnlyList<string>, IReadOnlyList<string>, IReadOnlyList<string>) >(
            (heads.ToList(), subs.ToList(), purs.ToList(), items.ToList(), supps.ToList(), boxes.ToList()));
    }

    public Task<IReadOnlyList<string>> GetBoxNosAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> boxes = new[] { "*", "CHA01", "CHA02", "RNG01", "RNG02", "BNG01", "EAR01" };
        return Task.FromResult(boxes);
    }

    private static IEnumerable<BarcodeStockReportItem> ApplyFilter(IEnumerable<BarcodeStockReportItem> source, BarcodeStockReportFilter filter)
    {
        var q = source;
        if (!string.IsNullOrWhiteSpace(filter.ItemHead) && filter.ItemHead != "*")
            q = q.Where(x => string.Equals(x.HeadGroup, filter.ItemHead, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(filter.SubGroup) && filter.SubGroup != "*")
            q = q.Where(x => string.Equals(x.SubGroup, filter.SubGroup, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(filter.Purity) && filter.Purity != "*")
            q = q.Where(x => string.Equals(x.Purity, filter.Purity, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(filter.ItemName) && filter.ItemName != "*")
            q = q.Where(x => string.Equals(x.ItemName, filter.ItemName, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(filter.BoxNo) && filter.BoxNo != "*")
            q = q.Where(x => string.Equals(x.BoxNo, filter.BoxNo, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var st = filter.SearchText.Trim();
            q = q.Where(x => (x.Barcode != null && x.Barcode.Contains(st, StringComparison.OrdinalIgnoreCase)) ||
                             (x.ItemName != null && x.ItemName.Contains(st, StringComparison.OrdinalIgnoreCase)) ||
                             (x.Description != null && x.Description.Contains(st, StringComparison.OrdinalIgnoreCase)) ||
                             (x.SupplierName != null && x.SupplierName.Contains(st, StringComparison.OrdinalIgnoreCase)) ||
                             (x.BoxNo != null && x.BoxNo.Contains(st, StringComparison.OrdinalIgnoreCase)));
        }
        return q;
    }

    public void InvalidateMasterCache()
    {
    }

    public Task UpdateMrpAsync(string barcode, decimal mrp, CancellationToken cancellationToken = default)
    {
        var item = _stock.FirstOrDefault(x => string.Equals(x.Barcode, barcode, StringComparison.OrdinalIgnoreCase));
        if (item != null)
        {
            item.MRP = mrp;
        }
        return Task.CompletedTask;
    }

    public Task UpdateBoxNoAsync(string barcode, string boxNo, CancellationToken cancellationToken = default)
    {
        return UpdateBoxNosAsync([barcode], boxNo, cancellationToken);
    }

    public Task UpdateBoxNosAsync(IEnumerable<string> barcodes, string boxNo, CancellationToken cancellationToken = default)
    {
        var set = new HashSet<string>(barcodes ?? [], StringComparer.OrdinalIgnoreCase);
        foreach (var item in _stock.Where(x => set.Contains(x.Barcode)))
        {
            item.BoxNo = boxNo;
        }
        foreach (var item in _demoScannedStockCheck.Where(x => set.Contains(x.Barcode)))
        {
            item.BoxNo = boxNo;
        }
        return Task.CompletedTask;
    }

    private readonly List<BarcodeStockReportItem> _demoScannedStockCheck = new();

    public Task<IReadOnlyList<BarcodeStockReportItem>> GetScannedStockCheckItemsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<BarcodeStockReportItem>>(_demoScannedStockCheck.ToList());
    }

    public Task SaveScannedStockCheckItemAsync(BarcodeStockReportItem item, CancellationToken cancellationToken = default)
    {
        _demoScannedStockCheck.RemoveAll(x => string.Equals(x.Barcode, item.Barcode, StringComparison.OrdinalIgnoreCase));
        _demoScannedStockCheck.Insert(0, item);
        return Task.CompletedTask;
    }

    public Task RemoveScannedStockCheckItemAsync(string barcode, CancellationToken cancellationToken = default)
    {
        _demoScannedStockCheck.RemoveAll(x => string.Equals(x.Barcode, barcode, StringComparison.OrdinalIgnoreCase));
        return Task.CompletedTask;
    }

    public Task ClearScannedStockCheckItemsAsync(CancellationToken cancellationToken = default)
    {
        _demoScannedStockCheck.Clear();
        return Task.CompletedTask;
    }

    public Task<BarcodeStockReportItem?> LookupBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(barcode)) return Task.FromResult<BarcodeStockReportItem?>(null);
        var code = barcode.Trim();
        var padded = code.PadLeft(6, '0');
        var item = _stock.FirstOrDefault(x => string.Equals(x.Barcode, code, StringComparison.OrdinalIgnoreCase) ||
                                              string.Equals(x.Barcode, padded, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(item);
    }
}
