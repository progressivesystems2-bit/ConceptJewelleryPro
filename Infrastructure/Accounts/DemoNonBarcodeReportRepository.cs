using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class DemoNonBarcodeReportRepository : INonBarcodeReportRepository
{
    private static readonly List<NonBarcodeStockReportItem> SampleItems =
    [
        new()
        {
            ItemName = "BULLION 99.5",
            HeadGroup = "GOLD",
            SubGroup = "BULLION",
            ItemType = "GOLD",
            OpeningQty = 5,
            OpeningWt = 500.000m,
            InwardQty = 10,
            InwardWt = 1000.000m,
            OutwardQty = 3,
            OutwardWt = 227.300m,
            Rate = 7200m,
            Amount = 1636560m
        },
        new()
        {
            ItemName = "GOLD OLD",
            HeadGroup = "GOLD PRIMARY",
            SubGroup = "OLD PURCHASE",
            ItemType = "GOLD PRIMARY",
            OpeningQty = 2,
            OpeningWt = 34.320m,
            InwardQty = 5,
            InwardWt = 62.230m,
            OutwardQty = 0,
            OutwardWt = 0m,
            Rate = 6800m,
            Amount = 423164m
        },
        new()
        {
            ItemName = "NEW ORNAMENTS",
            HeadGroup = "GOLD",
            SubGroup = "RING",
            ItemType = "GOLD",
            OpeningQty = 20,
            OpeningWt = 250.000m,
            InwardQty = 15,
            InwardWt = 180.500m,
            OutwardQty = 5,
            OutwardWt = 556.410m,
            Rate = 7150m,
            Amount = 3978331.5m
        },
        new()
        {
            ItemName = "FASIA",
            HeadGroup = "GOLD",
            SubGroup = "EXTRA",
            ItemType = "GOLD",
            OpeningQty = 50,
            OpeningWt = 25.000m,
            InwardQty = 100,
            InwardWt = 45.000m,
            OutwardQty = 2,
            OutwardWt = 0.780m,
            Rate = 7300m,
            Amount = 5694m
        },
        new()
        {
            ItemName = "SILVER OLD",
            HeadGroup = "SILVER PRIMARY",
            SubGroup = "OLD PURCHASE",
            ItemType = "SILVER PRIMARY",
            OpeningQty = 1,
            OpeningWt = 150.000m,
            InwardQty = 3,
            InwardWt = 450.000m,
            OutwardQty = 0,
            OutwardWt = 0m,
            Rate = 85m,
            Amount = 38250m
        }
    ];

    public Task<IReadOnlyList<NonBarcodeStockReportItem>> GetItemWiseStockAsync(NonBarcodeStockReportFilter filter, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<NonBarcodeStockReportItem>>(Array.Empty<NonBarcodeStockReportItem>());
    }

    public Task<IReadOnlyList<NonBarcodeGroupSummary>> GetItemTypeWiseStockAsync(NonBarcodeStockReportFilter filter, CancellationToken cancellationToken = default)
    {
        var sampleGroups = new List<NonBarcodeGroupSummary>
        {
            new()
            {
                GroupKey = "GOLD PRIMARY",
                ItemType = "GOLD PRIMARY",
                ItemCount = 1,
                OpeningQty = 2,
                OpeningWt = 682.460m,
                InwardQty = 5,
                InwardWt = 62.230m,
                OutwardQty = 1,
                OutwardWt = 512.200m
            },
            new()
            {
                GroupKey = "GOLD NEW ORNAMENTS",
                ItemType = "GOLD NEW ORNAMENTS",
                ItemCount = 1,
                OpeningQty = 20,
                OpeningWt = 41116.434m,
                InwardQty = 1,
                InwardWt = 1319.460m,
                OutwardQty = 0,
                OutwardWt = 0m
            },
            new()
            {
                GroupKey = "SILVER PRIMARY",
                ItemType = "SILVER PRIMARY",
                ItemCount = 1,
                OpeningQty = 1,
                OpeningWt = 150.000m,
                InwardQty = 3,
                InwardWt = 450.000m,
                OutwardQty = 0,
                OutwardWt = 0m
            }
        };

        var result = sampleGroups.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(filter.ItemHead) && filter.ItemHead != "*")
        {
            result = result.Where(x => string.Equals(x.ItemType, filter.ItemHead, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var s = filter.SearchText.Trim();
            result = result.Where(x => x.ItemType.Contains(s, StringComparison.OrdinalIgnoreCase));
        }

        return Task.FromResult<IReadOnlyList<NonBarcodeGroupSummary>>(result.ToList());
    }

    public Task<IReadOnlyList<NonBarcodeTransactionDetail>> GetTransactionDetailsAsync(NonBarcodeStockReportFilter filter, CancellationToken cancellationToken = default)
    {
        var sampleTransactions = new List<NonBarcodeTransactionDetail>
        {
            new()
            {
                Date = DateTime.Today,
                VouNo = "RE00001",
                TransactionType = "Inward (Karighar Receive)",
                PartyName = "DUTTA ENTERPRISES",
                ItemName = "GOLD NEW ORNAMENTS",
                HeadGroup = "GOLD NEW ORNAMENTS",
                SubGroup = "KARIGHAR RECEIVE",
                ItemType = "GOLD NEW ORNAMENTS",
                InQty = 1,
                InWt = 1319.460m,
                Amount = 296904.76m,
                EntryUser = "admin"
            },
            new()
            {
                Date = DateTime.Today.AddDays(-1),
                VouNo = "OGP/0001/25-26",
                TransactionType = "Inward (Old Gold Purchase)",
                PartyName = "RAJESH SHARMA",
                ItemName = "GOLD OLD",
                HeadGroup = "GOLD PRIMARY",
                SubGroup = "OLD PURCHASE",
                ItemType = "GOLD PRIMARY",
                InQty = 1,
                InWt = 31.840m,
                Rate = 6800m,
                Amount = 216512m,
                Narration = "Old Gold Purchase",
                EntryUser = "admin"
            },
            new()
            {
                Date = DateTime.Today.AddDays(-2),
                VouNo = "IS00001",
                TransactionType = "Outward (Karighar Issue)",
                PartyName = "DUTTA ENTERPRISES",
                ItemName = "GOLD PRIMARY",
                HeadGroup = "GOLD PRIMARY",
                SubGroup = "KARIGHAR ISSUE",
                ItemType = "GOLD PRIMARY",
                OutQty = 1,
                OutWt = 1319.460m,
                Narration = "Purity: 916",
                EntryUser = "admin"
            }
        };

        var result = sampleTransactions.AsEnumerable();

        if (filter.FromDate.HasValue)
        {
            result = result.Where(x => x.Date >= filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            result = result.Where(x => x.Date <= filter.ToDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.ItemHead) && filter.ItemHead != "*")
        {
            result = result.Where(x => string.Equals(x.HeadGroup, filter.ItemHead, StringComparison.OrdinalIgnoreCase) ||
                                       string.Equals(x.ItemType, filter.ItemHead, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var s = filter.SearchText.Trim();
            result = result.Where(x => x.ItemName.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                                       x.PartyName.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                                       x.VouNo.Contains(s, StringComparison.OrdinalIgnoreCase));
        }

        return Task.FromResult<IReadOnlyList<NonBarcodeTransactionDetail>>(result.OrderByDescending(x => x.Date).ToList());
    }

    public Task<IReadOnlyList<NonBarcodeTransactionDetail>> GetItemWiseTransactionsAsync(NonBarcodeStockReportFilter filter, CancellationToken cancellationToken = default)
    {
        return GetTransactionDetailsAsync(filter, cancellationToken);
    }

    public Task<(IReadOnlyList<string> ItemHeads, IReadOnlyList<string> SubGroups, IReadOnlyList<string> ItemNames)> GetFilterMastersAsync(CancellationToken cancellationToken = default)
    {
        return GetItemFilterMastersAsync(cancellationToken);
    }

    public Task<(IReadOnlyList<string> ItemHeads, IReadOnlyList<string> SubGroups, IReadOnlyList<string> ItemNames)> GetItemFilterMastersAsync(CancellationToken cancellationToken = default)
    {
        var heads = new List<string> { "*", "GOLD", "SILVER", "PLATINUM", "DIAMOND" };
        var subs = new List<string> { "*", "RING", "CHAIN", "BANGLE", "EARRING", "PAYAL", "COIN" };
        var items = new List<string> { "*", "LADIES RING", "GENTS RING", "GOLD CHAIN 916", "SILVER PAYAL", "SILVER COIN 10G" };

        return Task.FromResult<(IReadOnlyList<string>, IReadOnlyList<string>, IReadOnlyList<string>)>((heads, subs, items));
    }
}


