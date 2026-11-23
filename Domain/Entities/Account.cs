namespace ConceptJewelleryPro.Domain.Entities;

public class Account
{
    public string HeadCategory { get; set; } = string.Empty;

    public string SubCategory { get; set; } = string.Empty;

    public string AccountName { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string Address2 { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string PinCode { get; set; } = string.Empty;

    public string State { get; set; } = string.Empty;

    public string PhoneNo { get; set; } = string.Empty;

    public string RefName { get; set; } = string.Empty;

    public string VatType { get; set; } = string.Empty;

    public string VatNo { get; set; } = string.Empty;

    public int Position { get; set; }

    public string InOut { get; set; } = string.Empty;

    public bool Print { get; set; }

    public string Status { get; set; } = "Saved";

    public decimal OpAmt1 { get; set; }

    public string PanNo { get; set; } = string.Empty;

    public decimal CreditLimit { get; set; }

    public decimal CreditDays { get; set; }

    public string User { get; set; } = string.Empty;

    public DateTime EntryDate { get; set; } = DateTime.Today;

    public TimeSpan Time { get; set; }

    public decimal Interest { get; set; }

    public decimal OpAmt2 { get; set; }

    public bool Hide { get; set; }

    public string Mobile { get; set; } = string.Empty;

    public string BankAc { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string ComputerName { get; set; } = string.Empty;

    public string PartyCode { get; set; } = string.Empty;

    public int NOS { get; set; }

    public string StdCode { get; set; } = string.Empty;

    public decimal OpGoldWeight { get; set; }

    public override string ToString() => AccountName;
}
