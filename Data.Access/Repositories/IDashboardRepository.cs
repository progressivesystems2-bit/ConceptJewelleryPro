using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Data.Access.Repositories;

public interface IDashboardRepository
{
    Task<DashboardSummary> GetDashboardSummaryAsync(DateTime targetDate, string currentUser, string role, CancellationToken cancellationToken = default);
    Task<TodayGlanceMetrics> GetTodayGlanceMetricsAsync(DateTime targetDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PendingCustomerOrderItem>> GetPendingOrdersAsync(DateTime targetDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomerCelebrationItem>> GetCelebrationsAsync(DateTime targetDate, int lookaheadDays = 0, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SchemeDueSummaryItem>> GetSchemeDuesWindowAsync(DateTime targetDate, int pastDays = 3, int nextDays = 7, CancellationToken cancellationToken = default);
    Task<SystemStatusInfo> GetSystemStatusAsync(string currentUser, string role, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DashboardMetalRateItem>> GetMetalRatesAsync(CancellationToken cancellationToken = default);
}
