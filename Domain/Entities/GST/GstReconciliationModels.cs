using System;
using System.Collections.Generic;

namespace ConceptJewelleryPro.Domain.Entities.GST;

public enum GstMatchStatus
{
    Matched,
    MismatchInTaxable,
    MismatchInTax,
    MismatchInDate,
    MissingIn2B,     // In ERP Books only (Risk of ITC denial)
    MissingInBooks   // In 2B Portal only (Unclaimed/Not booked yet)
}

public sealed class GstPortalInvoice
{
    public string SupplierGstin { get; set; } = string.Empty;
    public string SupplierTradeName { get; set; } = string.Empty;
    public string InvoiceNumber { get; set; } = string.Empty;
    public string InvoiceType { get; set; } = "B2B"; // B2B, CDNR, CDNUR, B2BA
    public DateTime InvoiceDate { get; set; }
    public decimal InvoiceValue { get; set; }
    public string PlaceOfSupply { get; set; } = string.Empty;
    public string ReverseCharge { get; set; } = "N";
    public decimal Rate { get; set; }
    public decimal TaxableValue { get; set; }
    public decimal IntegratedTax { get; set; }
    public decimal CentralTax { get; set; }
    public decimal StateTax { get; set; }
    public decimal Cess { get; set; }
    public decimal TotalTax => IntegratedTax + CentralTax + StateTax + Cess;
    public string Gstr1FilingStatus { get; set; } = "Y";
    public string Gstr1FilingPeriod { get; set; } = string.Empty;
    public string ItcAvailability { get; set; } = "Y";
    public string SourceFile { get; set; } = string.Empty;
}

public sealed class GstReconciliationItem
{
    // Match Classification
    public GstMatchStatus Status { get; set; } = GstMatchStatus.Matched;
    public string StatusText => Status switch
    {
        GstMatchStatus.Matched => "Matched",
        GstMatchStatus.MismatchInTaxable => "Taxable Mismatch",
        GstMatchStatus.MismatchInTax => "Tax Mismatch",
        GstMatchStatus.MismatchInDate => "Date Mismatch",
        GstMatchStatus.MissingIn2B => "In Books Only (Risk)",
        GstMatchStatus.MissingInBooks => "In 2B Only (Unclaimed)",
        _ => "Unknown"
    };

    public string StatusColor => Status switch
    {
        GstMatchStatus.Matched => "#16A34A",         // Green
        GstMatchStatus.MismatchInTaxable => "#D97706", // Amber
        GstMatchStatus.MismatchInTax => "#D97706",     // Amber
        GstMatchStatus.MismatchInDate => "#2563EB",    // Blue
        GstMatchStatus.MissingIn2B => "#DC2626",       // Red
        GstMatchStatus.MissingInBooks => "#7C3AED",    // Purple
        _ => "#6B7280"
    };

    // Books details (ERP)
    public string? BooksVouNo { get; set; }
    public string? BooksBillNo { get; set; }
    public DateTime? BooksDate { get; set; }
    public string? BooksSupplierName { get; set; }
    public string? BooksGstin { get; set; }
    public decimal BooksTaxableValue { get; set; }
    public decimal BooksIgst { get; set; }
    public decimal BooksCgst { get; set; }
    public decimal BooksSgst { get; set; }
    public decimal BooksTotalTax => BooksIgst + BooksCgst + BooksSgst;
    public decimal BooksTotalAmount { get; set; }

    // Portal details (GSTR-2B / 2A)
    public string? PortalBillNo { get; set; }
    public DateTime? PortalDate { get; set; }
    public string? PortalSupplierName { get; set; }
    public string? PortalGstin { get; set; }
    public decimal PortalTaxableValue { get; set; }
    public decimal PortalIgst { get; set; }
    public decimal PortalCgst { get; set; }
    public decimal PortalSgst { get; set; }
    public decimal PortalTotalTax => PortalIgst + PortalCgst + PortalSgst;
    public decimal PortalTotalAmount { get; set; }

    // Variances (Books - Portal)
    public decimal DifferenceTaxable => BooksTaxableValue - PortalTaxableValue;
    public decimal DifferenceTax => BooksTotalTax - PortalTotalTax;
    public decimal DifferenceTotalAmount => BooksTotalAmount - PortalTotalAmount;
    public string Remarks { get; set; } = string.Empty;
}

public sealed class GstReconciliationSummary
{
    public int TotalBooksInvoices { get; set; }
    public int TotalPortalInvoices { get; set; }

    public int MatchedCount { get; set; }
    public int MismatchCount { get; set; }
    public int MissingIn2BCount { get; set; }
    public int MissingInBooksCount { get; set; }

    public decimal BooksTotalTaxable { get; set; }
    public decimal BooksTotalTax { get; set; }

    public decimal PortalTotalTaxable { get; set; }
    public decimal PortalTotalTax { get; set; }

    public decimal MatchedTaxAmount { get; set; }
    public decimal MissingIn2BTaxAmount { get; set; } // Risk ITC
    public decimal MissingInBooksTaxAmount { get; set; } // Unclaimed ITC
}
