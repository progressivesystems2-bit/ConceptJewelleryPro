using System;
using System.Collections.Generic;

namespace ConceptJewelleryPro.Domain.Entities.GST;

public sealed class Gstr1Report
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public List<Gstr1B2BInvoice> B2BInvoices { get; set; } = new();
    public List<Gstr1B2CLargeInvoice> B2CLargeInvoices { get; set; } = new();
    public List<Gstr1B2CSmallSummary> B2CSmallSummaries { get; set; } = new();
    public List<Gstr1CreditDebitNote> CreditDebitNotes { get; set; } = new();
    public List<Gstr1HsnSummary> HsnSummaries { get; set; } = new();
    public List<Gstr1HsnSummary> HsnB2BSummaries { get; set; } = new();
    public List<Gstr1HsnSummary> HsnB2CSummaries { get; set; } = new();
    public List<Gstr1DocumentSummary> DocumentSummaries { get; set; } = new();

    public decimal TotalTaxableAmount { get; set; }
    public decimal TotalIgstAmount { get; set; }
    public decimal TotalCgstAmount { get; set; }
    public decimal TotalSgstAmount { get; set; }
    public decimal TotalTaxAmount { get; set; }
    public decimal TotalInvoiceValue { get; set; }
    public int TotalInvoiceCount { get; set; }
}

public sealed class Gstr1B2BInvoice
{
    public string Gstin { get; set; } = string.Empty;
    public string ReceiverName { get; set; } = string.Empty;
    public string InvoiceNo { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public decimal InvoiceValue { get; set; }
    public string PlaceOfSupply { get; set; } = string.Empty;
    public string ReverseCharge { get; set; } = "N";
    public string InvoiceType { get; set; } = "Regular";
    public decimal GstRate { get; set; }
    public decimal TaxableValue { get; set; }
    public decimal IgstAmount { get; set; }
    public decimal CgstAmount { get; set; }
    public decimal SgstAmount { get; set; }
    public decimal CessAmount { get; set; }
}

public sealed class Gstr1B2CLargeInvoice
{
    public string InvoiceNo { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public decimal InvoiceValue { get; set; }
    public string PlaceOfSupply { get; set; } = string.Empty;
    public decimal GstRate { get; set; }
    public decimal TaxableValue { get; set; }
    public decimal IgstAmount { get; set; }
    public decimal CessAmount { get; set; }
}

public sealed class Gstr1B2CSmallSummary
{
    public string Type { get; set; } = "OE"; // Other than E-Commerce
    public string PlaceOfSupply { get; set; } = string.Empty;
    public decimal GstRate { get; set; }
    public decimal TaxableValue { get; set; }
    public decimal IgstAmount { get; set; }
    public decimal CgstAmount { get; set; }
    public decimal SgstAmount { get; set; }
    public decimal CessAmount { get; set; }
    public decimal TotalAmount { get; set; }
}

public sealed class Gstr1CreditDebitNote
{
    public string Gstin { get; set; } = string.Empty;
    public string ReceiverName { get; set; } = string.Empty;
    public string NoteType { get; set; } = "C"; // C = Credit, D = Debit
    public string NoteNumber { get; set; } = string.Empty;
    public DateTime NoteDate { get; set; }
    public string OriginalInvoiceNo { get; set; } = string.Empty;
    public DateTime OriginalInvoiceDate { get; set; }
    public decimal NoteValue { get; set; }
    public string PlaceOfSupply { get; set; } = string.Empty;
    public string PreGst { get; set; } = "N";
    public decimal GstRate { get; set; }
    public decimal TaxableValue { get; set; }
    public decimal IgstAmount { get; set; }
    public decimal CgstAmount { get; set; }
    public decimal SgstAmount { get; set; }
    public decimal CessAmount { get; set; }
}

public sealed class Gstr1HsnSummary
{
    public string HsnCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Uqc { get; set; } = "GMS"; // Grams / PCS
    public decimal TotalQuantity { get; set; }
    public decimal TotalValue { get; set; }
    public decimal TaxableValue { get; set; }
    public decimal GstRate { get; set; }
    public decimal IgstAmount { get; set; }
    public decimal CgstAmount { get; set; }
    public decimal SgstAmount { get; set; }
    public decimal CessAmount { get; set; }
}

public sealed class Gstr1DocumentSummary
{
    public int NatureOfDoc { get; set; } // 1 = Invoices for outward supply, 2 = Invoices for inward supply from unregistered person, 3 = Revised Invoice, 4 = Debit Note, 5 = Credit Note, etc.
    public string DocDescription { get; set; } = string.Empty;
    public string FromSerialNo { get; set; } = string.Empty;
    public string ToSerialNo { get; set; } = string.Empty;
    public int TotalNumber { get; set; }
    public int CancelledNumber { get; set; }
    public int NetIssuedNumber => Math.Max(0, TotalNumber - CancelledNumber);
}
