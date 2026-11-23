using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class DemoBoxMasterRepository : IBoxMasterRepository
{
    private readonly List<BoxMaster> _boxes = new()
    {
        new BoxMaster { BoxNo = "CHA01", BoxName = "CHAIN CB", Rack = "B2", NumberOfBoxes = 1, BoxWt = 150.000m, Serial = 1, Status = "Active" },
        new BoxMaster { BoxNo = "CHA02", BoxName = "CHAIN ROPE", Rack = "B2", NumberOfBoxes = 1, BoxWt = 160.000m, Serial = 2, Status = "Active" },
        new BoxMaster { BoxNo = "RNG01", BoxName = "GENTS RING", Rack = "A1", NumberOfBoxes = 2, BoxWt = 220.500m, Serial = 3, Status = "Active" },
        new BoxMaster { BoxNo = "RNG02", BoxName = "LADIES RING", Rack = "A1", NumberOfBoxes = 2, BoxWt = 210.000m, Serial = 4, Status = "Active" },
        new BoxMaster { BoxNo = "BNG01", BoxName = "BANGLES 2.4", Rack = "C3", NumberOfBoxes = 1, BoxWt = 350.000m, Serial = 5, Status = "Active" },
        new BoxMaster { BoxNo = "EAR01", BoxName = "JHUMKA / TOPS", Rack = "B1", NumberOfBoxes = 1, BoxWt = 180.000m, Serial = 6, Status = "Active" }
    };

    public Task<IReadOnlyList<BoxMaster>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<BoxMaster> result = _boxes
            .Where(b => b.Status != "Deleted")
            .OrderBy(b => b.Serial)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<BoxMaster?> GetByBoxNoAsync(string boxNo, CancellationToken cancellationToken = default)
    {
        var found = _boxes.FirstOrDefault(b => string.Equals(b.BoxNo, boxNo, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(found);
    }

    public Task AddAsync(BoxMaster box, CancellationToken cancellationToken = default)
    {
        if (box.Serial <= 0)
        {
            box.Serial = _boxes.Count == 0 ? 1 : _boxes.Max(b => b.Serial) + 1;
        }
        _boxes.RemoveAll(b => string.Equals(b.BoxNo, box.BoxNo, StringComparison.OrdinalIgnoreCase));
        _boxes.Add(box);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(string originalBoxNo, BoxMaster box, CancellationToken cancellationToken = default)
    {
        var existing = _boxes.FirstOrDefault(b => string.Equals(b.BoxNo, originalBoxNo, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            _boxes.Remove(existing);
        }
        _boxes.Add(box);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string boxNo, CancellationToken cancellationToken = default)
    {
        _boxes.RemoveAll(b => string.Equals(b.BoxNo, boxNo, StringComparison.OrdinalIgnoreCase));
        return Task.CompletedTask;
    }

    public Task<int> GetNextSerialAsync(CancellationToken cancellationToken = default)
    {
        var next = _boxes.Count == 0 ? 1 : _boxes.Max(b => b.Serial) + 1;
        return Task.FromResult(next);
    }

    public Task<IReadOnlyList<string>> GetRacksAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> racks = _boxes
            .Select(b => b.Rack)
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(r => r)
            .ToList();
        return Task.FromResult(racks);
    }
}
