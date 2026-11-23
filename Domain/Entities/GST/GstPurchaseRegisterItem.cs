using System;

namespace ConceptJewelleryPro.Domain.Entities.GST;

public sealed class GstPurchaseRegisterItem
{
    public string VouNo { get; set; } = string.Empty;
    public string SuppBillNo { get; set; } = string.Empty;
    public DateTime BillDate { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string SupplierGstin { get; set; } = string.Empty;
    public string PlaceOfSupply { get; set; } = string.Empty;
    public string VoucherType { get; set; } = "Purchase"; // Purchase or Purchase Expense
    public string Head { get; set; } = string.Empty;
    public decimal GrossWt { get; set; }
    public decimal NetWt { get; set; }
    public decimal TaxableAmount { get; set; }
    public decimal GstRate { get; set; }
    public decimal IgstAmount { get; set; }
    public decimal CgstAmount { get; set; }
    public decimal SgstAmount { get; set; }
    public decimal TotalTax => IgstAmount + CgstAmount + SgstAmount;
    public decimal RoundOff { get; set; }
    public decimal NetAmount { get; set; }
    public string ReverseCharge { get; set; } = "N";
    public string ItcEligibility { get; set; } = "Inputs";
    public string Status { get; set; } = "Active";
}
