using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities.GST;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class DemoGstReportRepository : IGstReportRepository
{
    public Task<Gstr1Report> GetGstr1ReportAsync(DateTime fromDate, DateTime toDate, string? vouType = null, CancellationToken cancellationToken = default)
    {
        var report = new Gstr1Report
        {
            FromDate = fromDate,
            ToDate = toDate,
            B2BInvoices = new List<Gstr1B2BInvoice>
            {
                new()
                {
                    Gstin = "21AAZFR4775J1ZH",
                    ReceiverName = "MAA TARINI JEWELLERS",
                    InvoiceNo = "INV-00101",
                    InvoiceDate = fromDate.AddDays(2),
                    InvoiceValue = 245600m,
                    PlaceOfSupply = "21-Odisha",
                    GstRate = 3.00m,
                    TaxableValue = 238446.60m,
                    CgstAmount = 3576.70m,
                    SgstAmount = 3576.70m
                },
                new()
                {
                    Gstin = "19AABCU9603R1ZM",
                    ReceiverName = "KOLKATA GOLDSMITH CRAFTS",
                    InvoiceNo = "INV-00102",
                    InvoiceDate = fromDate.AddDays(5),
                    InvoiceValue = 512000m,
                    PlaceOfSupply = "19-West Bengal",
                    GstRate = 3.00m,
                    TaxableValue = 497087.38m,
                    IgstAmount = 14912.62m
                }
            },
            B2CSmallSummaries = new List<Gstr1B2CSmallSummary>
            {
                new()
                {
                    PlaceOfSupply = "21-Odisha",
                    GstRate = 3.00m,
                    TaxableValue = 1850000.00m,
                    CgstAmount = 27750.00m,
                    SgstAmount = 27750.00m,
                    TotalAmount = 1905500.00m
                }
            },
            HsnSummaries = new List<Gstr1HsnSummary>
            {
                new()
                {
                    HsnCode = "7113",
                    Description = "Gold & Precious Jewellery",
                    Uqc = "GMS",
                    TotalQuantity = 345.500m,
                    TaxableValue = 2485533.98m,
                    GstRate = 3.00m,
                    CgstAmount = 31326.70m,
                    SgstAmount = 31326.70m,
                    IgstAmount = 14912.62m,
                    TotalValue = 2563100.00m
                },
                new()
                {
                    HsnCode = "7114",
                    Description = "Silverware & Articles",
                    Uqc = "GMS",
                    TotalQuantity = 1250.000m,
                    TaxableValue = 100000.00m,
                    GstRate = 3.00m,
                    CgstAmount = 1500.00m,
                    SgstAmount = 1500.00m,
                    TotalValue = 103000.00m
                },
                new()
                {
                    HsnCode = "7117",
                    Description = "Fashion & Imitation Jewellery (Pieces)",
                    Uqc = "PCS",
                    TotalQuantity = 25.000m,
                    TaxableValue = 50000.00m,
                    GstRate = 3.00m,
                    CgstAmount = 750.00m,
                    SgstAmount = 750.00m,
                    TotalValue = 51500.00m
                }
            },
            HsnB2BSummaries = new List<Gstr1HsnSummary>
            {
                new()
                {
                    HsnCode = "7113",
                    Description = "Gold & Precious Jewellery",
                    Uqc = "GMS",
                    TotalQuantity = 95.500m,
                    TaxableValue = 735533.98m,
                    GstRate = 3.00m,
                    CgstAmount = 3576.70m,
                    SgstAmount = 3576.70m,
                    IgstAmount = 14912.62m,
                    TotalValue = 757600.00m
                },
                new()
                {
                    HsnCode = "7117",
                    Description = "Fashion & Imitation Jewellery (Pieces)",
                    Uqc = "PCS",
                    TotalQuantity = 10.000m,
                    TaxableValue = 20000.00m,
                    GstRate = 3.00m,
                    CgstAmount = 300.00m,
                    SgstAmount = 300.00m,
                    IgstAmount = 0m,
                    TotalValue = 20600.00m
                }
            },
            HsnB2CSummaries = new List<Gstr1HsnSummary>
            {
                new()
                {
                    HsnCode = "7113",
                    Description = "Gold & Precious Jewellery",
                    Uqc = "GMS",
                    TotalQuantity = 250.000m,
                    TaxableValue = 1750000.00m,
                    GstRate = 3.00m,
                    CgstAmount = 26250.00m,
                    SgstAmount = 26250.00m,
                    IgstAmount = 0m,
                    TotalValue = 1802500.00m
                },
                new()
                {
                    HsnCode = "7114",
                    Description = "Silverware & Articles",
                    Uqc = "GMS",
                    TotalQuantity = 1250.000m,
                    TaxableValue = 100000.00m,
                    GstRate = 3.00m,
                    CgstAmount = 1500.00m,
                    SgstAmount = 1500.00m,
                    IgstAmount = 0m,
                    TotalValue = 103000.00m
                },
                new()
                {
                    HsnCode = "7117",
                    Description = "Fashion & Imitation Jewellery (Pieces)",
                    Uqc = "PCS",
                    TotalQuantity = 15.000m,
                    TaxableValue = 30000.00m,
                    GstRate = 3.00m,
                    CgstAmount = 450.00m,
                    SgstAmount = 450.00m,
                    IgstAmount = 0m,
                    TotalValue = 30900.00m
                }
            },
            DocumentSummaries = new List<Gstr1DocumentSummary>
            {
                new()
                {
                    NatureOfDoc = 1,
                    DocDescription = "Invoices for outward supply",
                    FromSerialNo = "101",
                    ToSerialNo = "185",
                    TotalNumber = 85,
                    CancelledNumber = 2
                }
            }
        };

        report.TotalTaxableAmount = 2585533.98m;
        report.TotalCgstAmount = 32826.70m;
        report.TotalSgstAmount = 32826.70m;
        report.TotalIgstAmount = 14912.62m;
        report.TotalTaxAmount = report.TotalCgstAmount + report.TotalSgstAmount + report.TotalIgstAmount;
        report.TotalInvoiceValue = 2666100.00m;
        report.TotalInvoiceCount = 83;

        return Task.FromResult(report);
    }

    public Task<Gstr3BReport> GetGstr3BReportAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        var report = new Gstr3BReport
        {
            FromDate = fromDate,
            ToDate = toDate,
            Period = $"{fromDate:MMM yyyy}",
            OutwardTaxableSupplies = new Gstr3BSupplyRow
            {
                Description = "(a) Outward taxable supplies (other than zero rated, nil rated and exempted)",
                TotalTaxableValue = 2585533.98m,
                CentralTax = 32826.70m,
                StateTax = 32826.70m,
                IntegratedTax = 14912.62m
            },
            InterStateSuppliesToUnregistered = new List<Gstr3BInterStateSupply>
            {
                new()
                {
                    PlaceOfSupply = "19",
                    TaxableValue = 497087.38m,
                    IntegratedTax = 14912.62m
                }
            },
            ItcAllOther = new Gstr3BItcRow
            {
                Description = "(A)(5) All other ITC (Purchases & Expenses)",
                CentralTax = 22450.00m,
                StateTax = 22450.00m,
                IntegratedTax = 8500.00m
            },
            NetItcAvailable = new Gstr3BItcRow
            {
                Description = "(C) Net ITC Available (A) - (B)",
                CentralTax = 22450.00m,
                StateTax = 22450.00m,
                IntegratedTax = 8500.00m
            },
            TotalTaxLiability = new Gstr3BTaxSummary
            {
                Description = "Total Output Tax Liability",
                CentralTax = 32826.70m,
                StateTax = 32826.70m,
                IntegratedTax = 14912.62m
            },
            TotalItcUtilized = new Gstr3BTaxSummary
            {
                Description = "Paid through ITC",
                CentralTax = 22450.00m,
                StateTax = 22450.00m,
                IntegratedTax = 8500.00m
            },
            NetTaxPayableInCash = new Gstr3BTaxSummary
            {
                Description = "Tax Payable in Cash",
                CentralTax = 10376.70m,
                StateTax = 10376.70m,
                IntegratedTax = 6412.62m
            }
        };

        return Task.FromResult(report);
    }

    public Task<IReadOnlyList<GstPurchaseRegisterItem>> GetGstPurchaseRegisterAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<GstPurchaseRegisterItem> list = new List<GstPurchaseRegisterItem>
        {
            new()
            {
                VouNo = "PV-0041",
                SuppBillNo = "MB/2026/89",
                BillDate = fromDate.AddDays(3),
                SupplierName = "MANEKLAL BULLION & CHAINS",
                SupplierGstin = "27AAACM1234F1Z9",
                PlaceOfSupply = "27-Maharashtra",
                VoucherType = "Stock Purchase",
                Head = "GOLD 22K 916",
                GrossWt = 150.000m,
                NetWt = 150.000m,
                TaxableAmount = 1050000.00m,
                GstRate = 3.00m,
                IgstAmount = 31500.00m,
                NetAmount = 1081500.00m
            },
            new()
            {
                VouNo = "PV-0042",
                SuppBillNo = "OR/881",
                BillDate = fromDate.AddDays(7),
                SupplierName = "ORISSA PRECIOUS REFINERS",
                SupplierGstin = "21AAAFO5566K1Z3",
                PlaceOfSupply = "21-Odisha",
                VoucherType = "Stock Purchase",
                Head = "GOLD 24K RAW",
                GrossWt = 85.200m,
                NetWt = 85.200m,
                TaxableAmount = 620000.00m,
                GstRate = 3.00m,
                CgstAmount = 9300.00m,
                SgstAmount = 9300.00m,
                NetAmount = 638600.00m
            },
            new()
            {
                VouNo = "PEX-012",
                SuppBillNo = "HM/245",
                BillDate = fromDate.AddDays(8),
                SupplierName = "BIS HALLMARKING CENTER",
                SupplierGstin = "21AABCB7788P1Z1",
                PlaceOfSupply = "21-Odisha",
                VoucherType = "Purchase Expense",
                Head = "HALLMARKING CHARGES",
                TaxableAmount = 14500.00m,
                GstRate = 18.00m,
                CgstAmount = 1305.00m,
                SgstAmount = 1305.00m,
                NetAmount = 17110.00m
            }
        };

        return Task.FromResult(list);
    }

    public Task<(string Gstin, string CompanyName, string StateCode)> GetCompanyDetailsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(("21AGVPV8495M1Z5", "TATTVA JEWELS", "21"));
    }
}
