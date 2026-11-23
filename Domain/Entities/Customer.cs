namespace ConceptJewelleryPro.Domain.Entities;

public class Customer
{
    public string Initial { get; set; } = string.Empty;

    public string CompanyName { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string Address2 { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string Pincode { get; set; } = string.Empty;

    public string Group { get; set; } = string.Empty;

    public int Nos { get; set; }

    public string Phone { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public bool Print { get; set; }

    public string Status { get; set; } = "Saved";

    public string Saved { get; set; } = "Yes";

    public DateTime? DOB { get; set; }

    public DateTime? DOA { get; set; }

    public string Profession { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Mobile { get; set; } = string.Empty;

    public string User { get; set; } = string.Empty;

    public DateTime Date { get; set; } = DateTime.Today;

    public TimeSpan Time { get; set; }
}
