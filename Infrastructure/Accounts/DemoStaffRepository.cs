using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class DemoStaffRepository : IStaffRepository
{
    private readonly List<StaffMember> _staffList = new();

    public DemoStaffRepository()
    {
        _staffList.Add(new StaffMember { Staff = "ASIT KUMAR DAS", Status = "Saved", EntryUser = "SUBHASIS", EntryDate = DateTime.Today });
        _staffList.Add(new StaffMember { Staff = "BHABNA DAS", Status = "Saved", EntryUser = "SUBHASIS", EntryDate = DateTime.Today });
        _staffList.Add(new StaffMember { Staff = "CHITTARANJAN DEBNATH", Status = "Saved", EntryUser = "SUBHASIS", EntryDate = DateTime.Today });
        _staffList.Add(new StaffMember { Staff = "DHARITRI SAHOO", Status = "Saved", EntryUser = "SUBHASIS", EntryDate = DateTime.Today });
        _staffList.Add(new StaffMember { Staff = "KUBER PRADHAN", Status = "Saved", EntryUser = "SUBHASIS", EntryDate = DateTime.Today });
    }

    public Task<IReadOnlyList<StaffMember>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<StaffMember> list = _staffList.Where(x => x.Status != "Deleted").OrderBy(x => x.Staff).ToList();
        return Task.FromResult(list);
    }

    public Task<StaffMember?> GetByNameAsync(string staffName, CancellationToken cancellationToken = default)
    {
        var item = _staffList.FirstOrDefault(x => string.Equals(x.Staff, staffName, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(item);
    }

    public Task SaveAsync(StaffMember member, bool isUpdate = false, string? oldStaffName = null, CancellationToken cancellationToken = default)
    {
        if (isUpdate && !string.IsNullOrWhiteSpace(oldStaffName))
        {
            var existing = _staffList.FirstOrDefault(x => string.Equals(x.Staff, oldStaffName, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                existing.Staff = member.Staff;
                existing.Status = member.Status;
            }
        }
        else
        {
            _staffList.Add(member);
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(string staffName, CancellationToken cancellationToken = default)
    {
        var existing = _staffList.FirstOrDefault(x => string.Equals(x.Staff, staffName, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            existing.Status = "Deleted";
        }

        return Task.CompletedTask;
    }
}
