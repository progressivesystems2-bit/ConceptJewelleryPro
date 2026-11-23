using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class DemoAuditRepository : IAuditRepository
{
    private readonly List<AuditLogEntry> _logs = new();
    private int _nextId = 1;

    public DemoAuditRepository()
    {
        // Seed demo audit entries
        _logs.Add(new AuditLogEntry
        {
            IdNo = _nextId++,
            User = "ADMIN",
            EntryType = "Invoice Entry",
            VouNo = "SVN00101",
            FieldName = "Amount",
            OldValue = "45000",
            NewValue = "48500",
            Date = DateTime.Today.AddDays(-2),
            Time = DateTime.Now.AddHours(-3),
            Checked = false
        });
        _logs.Add(new AuditLogEntry
        {
            IdNo = _nextId++,
            User = "ADMIN",
            EntryType = "Invoice Entry",
            VouNo = "SVN00101",
            FieldName = "Gross Wt",
            OldValue = "12.500",
            NewValue = "13.200",
            Date = DateTime.Today.AddDays(-2),
            Time = DateTime.Now.AddHours(-3),
            Checked = false
        });
        _logs.Add(new AuditLogEntry
        {
            IdNo = _nextId++,
            User = "ADMIN",
            EntryType = "Ledger Creation",
            VouNo = "SUNIL JEWELLERS",
            FieldName = "Credit Limit",
            OldValue = "100000",
            NewValue = "250000",
            Date = DateTime.Today.AddDays(-1),
            Time = DateTime.Now.AddHours(-5),
            Checked = true
        });
        _logs.Add(new AuditLogEntry
        {
            IdNo = _nextId++,
            User = "STAFF1",
            EntryType = "Customer Creation",
            VouNo = "RAMESH KUMAR",
            FieldName = "Phone",
            OldValue = "9876543210",
            NewValue = "9123456780",
            Date = DateTime.Today,
            Time = DateTime.Now.AddMinutes(-45),
            Checked = false
        });
        _logs.Add(new AuditLogEntry
        {
            IdNo = _nextId++,
            User = "ADMIN",
            EntryType = "Staff Creation",
            VouNo = "ROHIT VERMA",
            FieldName = "Staff",
            OldValue = "ROHIT VERMA",
            NewValue = "#Deleted#",
            Date = DateTime.Today,
            Time = DateTime.Now.AddMinutes(-20),
            Checked = false
        });
    }

    public Task LogAuditAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
    {
        lock (_logs)
        {
            entry.IdNo = _nextId++;
            _logs.Add(entry);
        }
        return Task.CompletedTask;
    }

    public Task LogAuditBatchAsync(IEnumerable<AuditLogEntry> entries, CancellationToken cancellationToken = default)
    {
        lock (_logs)
        {
            foreach (var e in entries)
            {
                e.IdNo = _nextId++;
                _logs.Add(e);
            }
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AuditLogEntry>> GetAuditLogsAsync(AuditReportFilter filter, CancellationToken cancellationToken = default)
    {
        lock (_logs)
        {
            var query = _logs.AsEnumerable();

            if (filter.FromDate.HasValue)
            {
                query = query.Where(x => x.Date.Date >= filter.FromDate.Value.Date);
            }

            if (filter.ToDate.HasValue)
            {
                query = query.Where(x => x.Date.Date <= filter.ToDate.Value.Date);
            }

            if (!string.IsNullOrWhiteSpace(filter.User) && !string.Equals(filter.User, "All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x => string.Equals(x.User, filter.User.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(filter.EntryType) && !string.Equals(filter.EntryType, "All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x => string.Equals(x.EntryType, filter.EntryType.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            if (filter.Status == AuditStatusFilter.UncheckedOnly)
            {
                query = query.Where(x => !x.Checked);
            }
            else if (filter.Status == AuditStatusFilter.CheckedOnly)
            {
                query = query.Where(x => x.Checked);
            }

            if (!string.IsNullOrWhiteSpace(filter.SearchText))
            {
                var s = filter.SearchText.Trim();
                query = query.Where(x =>
                    x.VouNo.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                    x.FieldName.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                    x.OldValue.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                    x.NewValue.Contains(s, StringComparison.OrdinalIgnoreCase));
            }

            return Task.FromResult<IReadOnlyList<AuditLogEntry>>(query.OrderByDescending(x => x.Date).ThenByDescending(x => x.Time).ToList());
        }
    }

    public Task<IReadOnlyList<string>> GetDistinctUsersAsync(CancellationToken cancellationToken = default)
    {
        lock (_logs)
        {
            var list = _logs.Select(x => x.User).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().OrderBy(x => x).ToList();
            return Task.FromResult<IReadOnlyList<string>>(list);
        }
    }

    public Task<IReadOnlyList<string>> GetDistinctEntryTypesAsync(CancellationToken cancellationToken = default)
    {
        lock (_logs)
        {
            var list = _logs.Select(x => x.EntryType).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().OrderBy(x => x).ToList();
            return Task.FromResult<IReadOnlyList<string>>(list);
        }
    }

    public Task MarkCheckedAsync(IEnumerable<int> idNos, bool isChecked, CancellationToken cancellationToken = default)
    {
        lock (_logs)
        {
            var set = new HashSet<int>(idNos);
            foreach (var log in _logs.Where(x => set.Contains(x.IdNo)))
            {
                log.Checked = isChecked;
            }
        }
        return Task.CompletedTask;
    }

    public Task DeleteAuditLogsAsync(IEnumerable<int> idNos, CancellationToken cancellationToken = default)
    {
        lock (_logs)
        {
            var set = new HashSet<int>(idNos);
            _logs.RemoveAll(x => set.Contains(x.IdNo));
        }
        return Task.CompletedTask;
    }
}
