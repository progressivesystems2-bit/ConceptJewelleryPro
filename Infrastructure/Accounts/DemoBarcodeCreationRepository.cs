using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class DemoBarcodeCreationRepository : IBarcodeCreationRepository
{
    private readonly List<BarcodeSubformItem> _subformItems = [];
    private readonly List<BarcodeCreationHeader> _headers = [];

    public DemoBarcodeCreationRepository()
    {
        _headers.Add(new BarcodeCreationHeader
        {
            VouNo = "9",
            Nos = 0,
            VouDate = DateTime.Today,
            ArtisianName = "OPN",
            Status = "Saved"
        });

        _subformItems.Add(new BarcodeSubformItem
        {
            Barcode = "000001",
            BarcodeAutoNo = 1,
            Nos = 1,
            VouNo = "9",
            BType = "WT",
            ItemName = "GOLD RING",
            ItemCode = "RNG01",
            Purity = "916",
            Description = "22K Yellow Gold Casting Ring",
            HUID = "HUID-916-101",
            Size = "14",
            Qty = 1,
            GrossWt = 4.500m,
            StoneWt = 0.200m,
            NetWt = 4.300m,
            Making = 450m,
            MakingType = "GRAM",
            MPerGr = 450m,
            CostRate = 6850m,
            MRP = 34500m,
            SupplierName = "OPN",
            PDate = DateTime.Today,
            PunchedOn = DateTime.Now
        });

        _subformItems.Add(new BarcodeSubformItem
        {
            Barcode = "BC1002",
            BarcodeAutoNo = 2,
            Nos = 2,
            VouNo = "9",
            BType = "WT",
            ItemName = "GOLD NECKLACE 22K",
            ItemCode = "NCK02",
            Purity = "22K",
            Description = "Traditional Floral Choker Necklace",
            HUID = "HUID-22K-482",
            Size = "16 Inch",
            Qty = 1,
            GrossWt = 15.200m,
            StoneWt = 0.400m,
            NetWt = 14.800m,
            Making = 12m,
            MakingType = "PERCENT",
            MPerGr = 500m,
            StoneValue = 1500m,
            CostRate = 6850m,
            MRP = 115000m,
            SupplierName = "OPN",
            PDate = DateTime.Today,
            PunchedOn = DateTime.Now
        });

        _subformItems.Add(new BarcodeSubformItem
        {
            Barcode = "BC1003",
            BarcodeAutoNo = 3,
            Nos = 3,
            VouNo = "9",
            BType = "WT",
            ItemName = "DIAMOND BRACELET",
            ItemCode = "BRC03",
            Purity = "18K",
            Description = "18K White Gold Diamond Solitaire Bracelet",
            HUID = "HUID-18K-991",
            Size = "2.4",
            Qty = 1,
            GrossWt = 8.600m,
            StoneWt = 1.100m,
            NetWt = 7.500m,
            Making = 15m,
            MakingType = "PERCENT",
            MPerGr = 600m,
            StoneValue = 28000m,
            CostRate = 5600m,
            MRP = 89000m,
            SupplierName = "OPN",
            PDate = DateTime.Today,
            PunchedOn = DateTime.Now
        });
    }

    public Task<IReadOnlyList<BarcodeSubformItem>> GetOpeningBarcodesAsync(int count = 100, CancellationToken cancellationToken = default)
    {
        var items = _subformItems
            .Where(x => x.VouNo == "9")
            .OrderByDescending(x => x.BarcodeAutoNo)
            .Take(count)
            .Select(x => x.Clone())
            .ToList();
        return Task.FromResult<IReadOnlyList<BarcodeSubformItem>>(items);
    }

    public Task<BarcodeSubformItem?> GetBarcodeByCodeAsync(string barcode, CancellationToken cancellationToken = default)
    {
        var raw = barcode?.Trim() ?? string.Empty;
        var d6 = int.TryParse(raw, out var n) ? n.ToString("D6") : raw;
        var found = _subformItems.FirstOrDefault(x =>
            string.Equals(x.Barcode, raw, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(x.Barcode, d6, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(found?.Clone());
    }

    public Task<string> GetNextBarcodeNumberAsync(CancellationToken cancellationToken = default)
    {
        var maxNos = _subformItems.Count == 0 ? 0 : _subformItems.Max(x => x.Nos);
        return Task.FromResult((maxNos + 1).ToString("D6"));
    }

    public Task<int> GetNextBarcodeAutoNoAsync(CancellationToken cancellationToken = default)
    {
        var maxAuto = _subformItems.Count == 0 ? 0 : _subformItems.Max(x => x.BarcodeAutoNo);
        return Task.FromResult(maxAuto + 1);
    }

    public Task SaveSingleOpeningBarcodeAsync(BarcodeSubformItem item, bool isUpdate = false, CancellationToken cancellationToken = default)
    {
        item.VouNo = "9";
        if (isUpdate)
        {
            var idx = _subformItems.FindIndex(x => string.Equals(x.Barcode, item.Barcode, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
            {
                _subformItems[idx] = item.Clone();
            }
        }
        else
        {
            if (item.Nos <= 0)
            {
                var maxNos = _subformItems.Count == 0 ? 0 : _subformItems.Max(x => x.Nos);
                item.Nos = maxNos + 1;
            }
            if (string.IsNullOrWhiteSpace(item.Barcode))
            {
                item.Barcode = item.Nos.ToString("D6");
            }
            if (item.BarcodeAutoNo <= 0)
            {
                var maxAuto = _subformItems.Count == 0 ? 0 : _subformItems.Max(x => x.BarcodeAutoNo);
                item.BarcodeAutoNo = maxAuto + 1;
            }
            _subformItems.Add(item.Clone());
        }

        return Task.CompletedTask;
    }

    public Task DeleteBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
    {
        _subformItems.RemoveAll(x => string.Equals(x.Barcode, barcode, StringComparison.OrdinalIgnoreCase));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<BarcodeCreationHeader>> GetRecentVouchersAsync(int count = 50, CancellationToken cancellationToken = default)
    {
        var headers = _headers
            .Where(x => x.VouNo != "9" && x.Status != "Deleted")
            .OrderByDescending(x => x.VouDate)
            .Take(count)
            .ToList();
        return Task.FromResult<IReadOnlyList<BarcodeCreationHeader>>(headers);
    }

    public Task<BarcodeCreationHeader?> GetVoucherByVouNoAsync(string vouNo, CancellationToken cancellationToken = default)
    {
        var header = _headers.FirstOrDefault(x => string.Equals(x.VouNo, vouNo, StringComparison.OrdinalIgnoreCase));
        if (header is null) return Task.FromResult<BarcodeCreationHeader?>(null);

        var copy = new BarcodeCreationHeader
        {
            VouNo = header.VouNo,
            Nos = header.Nos,
            VouDate = header.VouDate,
            ArtisianName = header.ArtisianName,
            SuppBillNo = header.SuppBillNo,
            GrossWts = header.GrossWts,
            NetWts = header.NetWts,
            StoneWts = header.StoneWts,
            StoneValues = header.StoneValues,
            Qty = header.Qty,
            MetalRate = header.MetalRate,
            Makings = header.Makings,
            Status = header.Status,
            ComputerName = header.ComputerName,
            EntryUser = header.EntryUser,
            EntryDate = header.EntryDate,
            EntryTime = header.EntryTime,
            Items = _subformItems.Where(i => string.Equals(i.VouNo, vouNo, StringComparison.OrdinalIgnoreCase)).Select(i => i.Clone()).ToList()
        };

        return Task.FromResult<BarcodeCreationHeader?>(copy);
    }

    public Task<int> GetNextVoucherNosAsync(CancellationToken cancellationToken = default)
    {
        var maxNos = _headers.Where(x => x.VouNo != "9").Select(x => x.Nos).DefaultIfEmpty(0).Max();
        return Task.FromResult(maxNos + 1);
    }

    public Task SaveBulkVoucherAsync(BarcodeCreationHeader voucher, bool updateExisting = false, CancellationToken cancellationToken = default)
    {
        if (updateExisting)
        {
            var idx = _headers.FindIndex(x => string.Equals(x.VouNo, voucher.VouNo, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0) _headers[idx] = voucher;
            _subformItems.RemoveAll(x => string.Equals(x.VouNo, voucher.VouNo, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            if (voucher.Nos <= 0)
            {
                var maxNos = _headers.Where(x => x.VouNo != "9").Select(x => x.Nos).DefaultIfEmpty(0).Max();
                voucher.Nos = maxNos + 1;
            }
            _headers.Add(voucher);
        }

        foreach (var item in voucher.Items)
        {
            item.VouNo = voucher.VouNo;
            if (item.Nos <= 0)
            {
                var maxNos = _subformItems.Count == 0 ? 0 : _subformItems.Max(x => x.Nos);
                item.Nos = maxNos + 1;
            }
            if (string.IsNullOrWhiteSpace(item.Barcode))
            {
                item.Barcode = item.Nos.ToString("D6");
            }
            if (item.BarcodeAutoNo <= 0)
            {
                var maxAuto = _subformItems.Count == 0 ? 0 : _subformItems.Max(x => x.BarcodeAutoNo);
                item.BarcodeAutoNo = maxAuto + 1;
            }
            _subformItems.Add(item.Clone());
        }

        return Task.CompletedTask;
    }

    public Task DeleteVoucherAsync(string vouNo, CancellationToken cancellationToken = default)
    {
        _headers.RemoveAll(x => string.Equals(x.VouNo, vouNo, StringComparison.OrdinalIgnoreCase));
        _subformItems.RemoveAll(x => string.Equals(x.VouNo, vouNo, StringComparison.OrdinalIgnoreCase));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> GetStickerFormatsAsync(CancellationToken cancellationToken = default)
    {
        var list = new List<string> { "StickerWt", "StickerPC", "No Making", "With Making", "Diamond" };
        return Task.FromResult<IReadOnlyList<string>>(list);
    }

    public Task UpdateBarcodePhotoAsync(string barcode, string photoPath, CancellationToken cancellationToken = default)
    {
        var raw = barcode?.Trim() ?? string.Empty;
        var d6 = int.TryParse(raw, out var n) ? n.ToString("D6") : raw;
        var found = _subformItems.FirstOrDefault(x =>
            string.Equals(x.Barcode, raw, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(x.Barcode, d6, StringComparison.OrdinalIgnoreCase));

        if (found is not null)
        {
            found.Photo = photoPath ?? string.Empty;
        }

        return Task.CompletedTask;
    }

    public Task UpdateBarcodeAdvancedAsync(string barcode, decimal? mrp, string? size, bool reprintSticker, CancellationToken cancellationToken = default)
    {
        return UpdateBarcodeAdvancedAsync(barcode, mrp, size, reprintSticker, null, null, null, cancellationToken);
    }

    public Task UpdateBarcodeAdvancedAsync(string barcode, decimal? mrp, string? size, bool reprintSticker, string? stickerFormat, CancellationToken cancellationToken = default)
    {
        return UpdateBarcodeAdvancedAsync(barcode, mrp, size, reprintSticker, stickerFormat, null, null, cancellationToken);
    }

    public Task UpdateBarcodeAdvancedAsync(string barcode, decimal? mrp, string? size, bool reprintSticker, string? stickerFormat, string? bType, decimal? making, CancellationToken cancellationToken = default)
    {
        var raw = barcode?.Trim() ?? string.Empty;
        var d6 = int.TryParse(raw, out var n) ? n.ToString("D6") : raw;
        var found = _subformItems.FirstOrDefault(x =>
            string.Equals(x.Barcode, raw, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(x.Barcode, d6, StringComparison.OrdinalIgnoreCase));

        if (found is not null)
        {
            if (!string.IsNullOrWhiteSpace(bType)) found.BType = bType.Trim().ToUpperInvariant();
            if (mrp.HasValue) found.MRP = mrp.Value;
            if (making.HasValue) found.Making = making.Value;
            if (!string.IsNullOrEmpty(size)) found.Size = size;
            if (reprintSticker) found.PrintStickerNo++;
            if (!string.IsNullOrWhiteSpace(stickerFormat)) found.Sti = stickerFormat.Trim();
        }

        return Task.CompletedTask;
    }
}
