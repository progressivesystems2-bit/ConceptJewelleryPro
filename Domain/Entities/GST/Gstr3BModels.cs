using System;

namespace ConceptJewelleryPro.Domain.Entities.GST;

public sealed class Gstr3BReport
{
    public string Gstin { get; set; } = string.Empty;
    public string LegalName { get; set; } = string.Empty;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string Period { get; set; } = string.Empty;

    // 3.1 Details of Outward Supplies and inward supplies liable to reverse charge
    public Gstr3BSupplyRow OutwardTaxableSupplies { get; set; } = new() { Description = "(a) Outward taxable supplies (other than zero rated, nil rated and exempted)" };
    public Gstr3BSupplyRow OutwardZeroRatedSupplies { get; set; } = new() { Description = "(b) Outward taxable supplies (zero rated)" };
    public Gstr3BSupplyRow OtherOutwardSuppliesNilExempt { get; set; } = new() { Description = "(c) Other outward supplies (Nil rated, exempted)" };
    public Gstr3BSupplyRow InwardSuppliesReverseCharge { get; set; } = new() { Description = "(d) Inward supplies (liable to reverse charge)" };
    public Gstr3BSupplyRow NonGstOutwardSupplies { get; set; } = new() { Description = "(e) Non-GST outward supplies" };

    // 4. Eligible ITC
    public Gstr3BItcRow ItcImportGoods { get; set; } = new() { Description = "(A)(1) Import of goods" };
    public Gstr3BItcRow ItcImportServices { get; set; } = new() { Description = "(A)(2) Import of services" };
    public Gstr3BItcRow ItcInwardReverseCharge { get; set; } = new() { Description = "(A)(3) Inward supplies liable to reverse charge" };
    public Gstr3BItcRow ItcInwardIsd { get; set; } = new() { Description = "(A)(4) Inward supplies from ISD" };
    public Gstr3BItcRow ItcAllOther { get; set; } = new() { Description = "(A)(5) All other ITC (Purchases & Expenses)" };

    public Gstr3BItcRow ItcReversedRule4243 { get; set; } = new() { Description = "(B)(1) As per rules 42 & 43 of CGST Rules" };
    public Gstr3BItcRow ItcReversedOthers { get; set; } = new() { Description = "(B)(2) Others" };

    public Gstr3BItcRow NetItcAvailable { get; set; } = new() { Description = "(C) Net ITC Available (A) - (B)" };
    public Gstr3BItcRow IneligibleItcSection17_5 { get; set; } = new() { Description = "(D)(1) As per section 17(5) (Blocked Credit)" };

    // 3.2 Inter-state supplies made to unregistered persons
    public System.Collections.Generic.List<Gstr3BInterStateSupply> InterStateSuppliesToUnregistered { get; set; } = new();

    // 5. Values of exempt, nil-rated and non-GST inward supplies
    public decimal InwardSuppliesCompositionExemptNilInterState { get; set; }
    public decimal InwardSuppliesCompositionExemptNilIntraState { get; set; }

    // 6. Payment of Tax Summary
    public Gstr3BTaxSummary TotalTaxLiability { get; set; } = new() { Description = "Total Output Tax Liability" };
    public Gstr3BTaxSummary TotalItcUtilized { get; set; } = new() { Description = "Paid through ITC" };
    public Gstr3BTaxSummary NetTaxPayableInCash { get; set; } = new() { Description = "Tax Payable in Cash" };
}

public sealed class Gstr3BInterStateSupply
{
    public string PlaceOfSupply { get; set; } = string.Empty;
    public decimal TaxableValue { get; set; }
    public decimal IntegratedTax { get; set; }
}

public sealed class Gstr3BSupplyRow
{
    public string Description { get; set; } = string.Empty;
    public decimal TotalTaxableValue { get; set; }
    public decimal IntegratedTax { get; set; }
    public decimal CentralTax { get; set; }
    public decimal StateTax { get; set; }
    public decimal Cess { get; set; }
    public decimal TotalTax => IntegratedTax + CentralTax + StateTax + Cess;
}

public sealed class Gstr3BItcRow
{
    public string Description { get; set; } = string.Empty;
    public decimal IntegratedTax { get; set; }
    public decimal CentralTax { get; set; }
    public decimal StateTax { get; set; }
    public decimal Cess { get; set; }
    public decimal TotalItc => IntegratedTax + CentralTax + StateTax + Cess;
}

public sealed class Gstr3BTaxSummary
{
    public string Description { get; set; } = string.Empty;
    public decimal IntegratedTax { get; set; }
    public decimal CentralTax { get; set; }
    public decimal StateTax { get; set; }
    public decimal Cess { get; set; }
    public decimal Total => IntegratedTax + CentralTax + StateTax + Cess;
}
