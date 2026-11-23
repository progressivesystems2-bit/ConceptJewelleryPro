using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ConceptJewelleryPro.Domain.Entities;

public class PurchaseRegisterItem : INotifyPropertyChanged
{
    private bool _isExpanded;

    public string VouNo { get; set; } = string.Empty;

    public string SuppBillNo { get; set; } = string.Empty;

    public DateTime BillDate { get; set; } = DateTime.Today;

    public DateTime DueDate { get; set; } = DateTime.Today;

    public DateTime? SuppDate { get; set; }

    public string VouType { get; set; } = "Purchase";

    public string AccountName { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;

    public string CustomerCode { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string Address2 { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string Pincode { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string VatNo { get; set; } = string.Empty;

    public string VatType { get; set; } = string.Empty;

    public decimal TotalQty { get; set; }

    public decimal GrWt { get; set; }

    public decimal NtWt { get; set; }

    public decimal GrossAmt { get; set; }

    public decimal DiscountAllowed { get; set; }

    public decimal TaxableAmount { get; set; }

    public decimal Tax { get; set; }

    public decimal TaxAmt { get; set; }

    public decimal CGSTAmt { get; set; }

    public decimal SGSTAmt { get; set; }

    public decimal IGSTAmt { get; set; }

    public decimal AddLess { get; set; }

    public string AddLessNote { get; set; } = string.Empty;

    public decimal NetAmt { get; set; }

    public bool BillCanceled { get; set; }

    public string Status { get; set; } = "Saved";

    public string Remarks { get; set; } = string.Empty;

    public string Narration { get; set; } = string.Empty;

    public List<PurchaseSubform> Items { get; set; } = new();

    public bool HasItems => Items != null && Items.Count > 0;

    public int ItemsCount => Items?.Count ?? 0;

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded != value)
            {
                _isExpanded = value;
                OnPropertyChanged();
            }
        }
    }

    public string PartyDisplayName => !string.IsNullOrWhiteSpace(CustomerName)
        ? CustomerName
        : (!string.IsNullOrWhiteSpace(AccountName) ? AccountName : "CASH PURCHASE");

    public string PartyIdentification
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(VatNo))
            {
                parts.Add(VatNo.Length == 15 ? $"GSTIN: {VatNo}" : (VatNo.Length == 10 ? $"PAN: {VatNo}" : $"AADHAR: {VatNo}"));
            }
            else if (!string.IsNullOrWhiteSpace(CustomerCode))
            {
                parts.Add($"CODE: {CustomerCode}");
            }

            if (!string.IsNullOrWhiteSpace(Address))
            {
                parts.Add(Address);
            }
            if (!string.IsNullOrWhiteSpace(City))
            {
                parts.Add(City);
            }

            return string.Join(", ", parts);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
