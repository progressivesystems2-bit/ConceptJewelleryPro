using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class DemoStockTransferRepository : IStockTransferRepository
{
    private readonly List<StockTransfer> _transfers = new();
    private readonly List<StockTransferItem> _items = new();
    private int _seqCounter = 0;

    public Task<string> GetNextTransferNoAsync(CancellationToken cancellationToken = default)
    {
        _seqCounter++;
        return Task.FromResult($"ST-{_seqCounter:D5}");
    }

    public Task<StockTransfer?> GetByTransferNoAsync(string transferNo, CancellationToken cancellationToken = default)
    {
        var found = _transfers.FirstOrDefault(x => string.Equals(x.TransferNo, transferNo, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(found);
    }

    public Task<IReadOnlyList<StockTransfer>> GetAllTransfersAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
    {
        var query = _transfers.AsEnumerable();
        if (fromDate.HasValue) query = query.Where(x => x.TransferDate >= fromDate.Value.Date);
        if (toDate.HasValue) query = query.Where(x => x.TransferDate <= toDate.Value.Date);
        return Task.FromResult<IReadOnlyList<StockTransfer>>(query.OrderByDescending(x => x.TransferDate).ThenByDescending(x => x.TransferNo).ToList());
    }

    public Task<IReadOnlyList<StockTransferItem>> GetTransferItemsAsync(string transferNo, CancellationToken cancellationToken = default)
    {
        var list = _items.Where(x => string.Equals(x.TransferNo, transferNo, StringComparison.OrdinalIgnoreCase)).OrderBy(x => x.AutoNo).ToList();
        return Task.FromResult<IReadOnlyList<StockTransferItem>>(list);
    }

    public Task SaveTransferAsync(StockTransfer transfer, IEnumerable<StockTransferItem> items, CancellationToken cancellationToken = default)
    {
        var itemList = items.ToList();

        _transfers.RemoveAll(x => string.Equals(x.TransferNo, transfer.TransferNo, StringComparison.OrdinalIgnoreCase));
        _items.RemoveAll(x => string.Equals(x.TransferNo, transfer.TransferNo, StringComparison.OrdinalIgnoreCase));

        transfer.TotalQty = itemList.Sum(x => x.Qty);
        transfer.TotalGrossWt = Math.Round(itemList.Sum(x => x.GrossWt), 3);
        transfer.TotalStoneWt = Math.Round(itemList.Sum(x => x.StoneWt), 3);
        transfer.TotalNetWt = Math.Round(itemList.Sum(x => x.NetWt), 3);
        transfer.TotalStoneValue = Math.Round(itemList.Sum(x => x.StoneValue), 2);
        transfer.TotalMaking = Math.Round(itemList.Sum(x => x.Making), 2);
        transfer.TotalMRP = Math.Round(itemList.Sum(x => x.MRP), 2);
        transfer.EntryDate ??= DateTime.Now;

        _transfers.Add(transfer);
        long autoNo = 1;
        foreach (var item in itemList)
        {
            item.AutoNo = autoNo++;
            item.TransferNo = transfer.TransferNo;
            _items.Add(item);
        }

        return Task.CompletedTask;
    }

    public Task DeleteTransferAsync(string transferNo, CancellationToken cancellationToken = default)
    {
        _transfers.RemoveAll(x => string.Equals(x.TransferNo, transferNo, StringComparison.OrdinalIgnoreCase));
        _items.RemoveAll(x => string.Equals(x.TransferNo, transferNo, StringComparison.OrdinalIgnoreCase));
        return Task.CompletedTask;
    }

    public Task<StockTransferItem?> LookupBarcodeForTransferAsync(string barcode, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<StockTransferItem?>(new StockTransferItem
        {
            Barcode = barcode,
            ItemName = "Demo Jewellery Item",
            Qty = 1m,
            GrossWt = 5.250m,
            NetWt = 5.100m,
            StoneWt = 0.150m,
            Making = 500m,
            MRP = 25000m,
            Purity = "22K"
        });
    }

    public Task<IReadOnlyList<string>> GetLocationsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> locations = new List<string>
        {
            "Showroom",
            "Damage",
            "Counter 1",
            "Counter 2",
            "Safe",
            "Workshop",
            "Branch",
            "Repair"
        };
        return Task.FromResult(locations);
    }
}
