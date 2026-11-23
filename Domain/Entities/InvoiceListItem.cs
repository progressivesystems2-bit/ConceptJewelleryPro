using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ConceptJewelleryPro.Domain.Entities;

public class InvoiceListItem : INotifyPropertyChanged
{
    private bool _isExpanded;

    public string VouNo { get; set; } = string.Empty;

    public DateTime BillDate { get; set; } = DateTime.Today;

    public string VouType { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public decimal NetAmt { get; set; }

    public int Qty { get; set; }

    public decimal NetWt { get; set; }

    public decimal GrWt { get; set; }

    public decimal TaxableAmt { get; set; }

    public decimal TaxAmt { get; set; }

    public decimal Tax { get; set; }

    public decimal GrossAmt { get; set; }

    public decimal DiscountAllowed { get; set; }

    public bool BillCanceled { get; set; }

    public string Status { get; set; } = "Saved";

    private string? _whatsAppStatus;
    public string? WhatsAppStatus
    {
        get => _whatsAppStatus;
        set
        {
            if (_whatsAppStatus != value)
            {
                _whatsAppStatus = value;
                OnPropertyChanged();
            }
        }
    }

    private string? _whatsAppReply;
    public string? WhatsAppReply
    {
        get => _whatsAppReply;
        set
        {
            if (_whatsAppReply != value)
            {
                _whatsAppReply = value;
                OnPropertyChanged();
            }
        }
    }

    public List<BillSubform> Items { get; set; } = new();

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

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
