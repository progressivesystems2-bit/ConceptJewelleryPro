using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class DemoCatalogueRepository : ICatalogueRepository
{
    private readonly List<CatalogueItem> _items = new();

    public DemoCatalogueRepository()
    {
        _items.Add(new CatalogueItem
        {
            Barcode = "000101",
            ItemName = "22K Gold Antique Ring",
            GrossWt = 4.850m,
            NetWt = 4.600m,
            StoneWt = 0.250m,
            Description = "Antique finish Floral carved design with ruby center stone",
            Size = "14",
            Purity = "22K 916",
            Photo = "000101.jpg"
        });

        _items.Add(new CatalogueItem
        {
            Barcode = "000102",
            ItemName = "22K Gold Fancy Bangle",
            GrossWt = 16.420m,
            NetWt = 16.420m,
            StoneWt = 0.000m,
            Description = "Casting Rhodium polished light-weight daily wear bangle",
            Size = "2.4",
            Purity = "22K 916",
            Photo = "000102.jpg"
        });

        _items.Add(new CatalogueItem
        {
            Barcode = "000103",
            ItemName = "18K Diamond Solitaire Pendant",
            GrossWt = 2.150m,
            NetWt = 1.950m,
            StoneWt = 0.200m,
            Description = "VVS EF Certified brilliant round cut diamond pendant",
            Size = "-",
            Purity = "18K 750",
            Photo = "000103.jpg"
        });

        _items.Add(new CatalogueItem
        {
            Barcode = "000104",
            ItemName = "22K Gold Traditional Necklace",
            GrossWt = 32.500m,
            NetWt = 31.800m,
            StoneWt = 0.700m,
            Description = "Temple wedding choker set with emerald drops and meenakari",
            Size = "16",
            Purity = "22K 916",
            Photo = "000104.jpg"
        });

        _items.Add(new CatalogueItem
        {
            Barcode = "000105",
            ItemName = "22K Gold Gents Kada",
            GrossWt = 24.100m,
            NetWt = 24.100m,
            StoneWt = 0.000m,
            Description = "Solid heavy designer pipe kada with screw lock",
            Size = "2.8",
            Purity = "22K 916",
            Photo = "000105.jpg"
        });

        _items.Add(new CatalogueItem
        {
            Barcode = "000106",
            ItemName = "22K Gold Jhumka Earrings",
            GrossWt = 8.750m,
            NetWt = 8.500m,
            StoneWt = 0.250m,
            Description = "3-step hanging filigree traditional South Indian jhumkas",
            Size = "-",
            Purity = "22K 916",
            Photo = "000106.jpg"
        });

        _items.Add(new CatalogueItem
        {
            Barcode = "000107",
            ItemName = "22K Gold Lightweight Chain",
            GrossWt = 6.200m,
            NetWt = 6.200m,
            StoneWt = 0.000m,
            Description = "Machine hollow rope link chain with strong spring ring hook",
            Size = "18",
            Purity = "22K 916",
            Photo = "000107.jpg"
        });

        _items.Add(new CatalogueItem
        {
            Barcode = "000108",
            ItemName = "18K Diamond Stud Earrings",
            GrossWt = 3.400m,
            NetWt = 3.100m,
            StoneWt = 0.300m,
            Description = "Cluster floral halo diamond daily studs",
            Size = "-",
            Purity = "18K 750",
            Photo = "000108.jpg"
        });
    }

    public Task<IReadOnlyList<CatalogueItem>> GetCatalogueAsync(CatalogueFilter filter, CancellationToken cancellationToken = default)
    {
        IEnumerable<CatalogueItem> query = _items;

        if (!string.IsNullOrWhiteSpace(filter.Barcode))
        {
            var raw = filter.Barcode.Trim();
            var padded = int.TryParse(raw, out var n) ? n.ToString("D6") : raw;
            query = query.Where(x => string.Equals(x.Barcode, raw, StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(x.Barcode, padded, StringComparison.OrdinalIgnoreCase) ||
                                     x.Barcode.Contains(raw, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(filter.ItemName))
        {
            query = query.Where(x => x.ItemName.Contains(filter.ItemName.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(filter.Size))
        {
            query = query.Where(x => string.Equals(x.Size, filter.Size.Trim(), StringComparison.OrdinalIgnoreCase) ||
                                     x.Size.Contains(filter.Size.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(filter.Description))
        {
            query = query.Where(x => x.Description.Contains(filter.Description.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (filter.FromWt.HasValue && filter.FromWt.Value > 0)
        {
            query = query.Where(x => x.GrossWt >= filter.FromWt.Value);
        }

        if (filter.ToWt.HasValue && filter.ToWt.Value > 0)
        {
            query = query.Where(x => x.GrossWt <= filter.ToWt.Value);
        }

        IReadOnlyList<CatalogueItem> result = query.ToList();
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<string>> GetAvailableItemNamesAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> result = _items.Select(x => x.ItemName).Distinct().OrderBy(x => x).ToList();
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<string>> GetAvailableSizesAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> result = _items.Select(x => x.Size).Where(x => !string.IsNullOrWhiteSpace(x) && x != "-").Distinct().OrderBy(x => x).ToList();
        return Task.FromResult(result);
    }
}
