using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConceptJewelleryPro.Domain.Entities.GST;

namespace ConceptJewelleryPro.Data.Access.Repositories;

public interface IGstReportRepository
{
    Task<Gstr1Report> GetGstr1ReportAsync(
        DateTime fromDate,
        DateTime toDate,
        string? vouType = null,
        CancellationToken cancellationToken = default);

    Task<Gstr3BReport> GetGstr3BReportAsync(
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GstPurchaseRegisterItem>> GetGstPurchaseRegisterAsync(
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default);

    Task<(string Gstin, string CompanyName, string StateCode)> GetCompanyDetailsAsync(
        CancellationToken cancellationToken = default);
}
