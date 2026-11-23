using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ConceptJewelleryPro.Domain.Entities;

public class BarcodeStockReportItem : INotifyPropertyChanged
{
    private decimal _mrp;
    private decimal _netWt;
    private decimal _making;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Barcode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal Qty { get; set; } = 1m;
    public decimal GrossWt { get; set; }
    public decimal StoneWt { get; set; }

    public decimal NetWt
    {
        get => _netWt;
        set
        {
            if (_netWt != value)
            {
                _netWt = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(MakingPerGram));
            }
        }
    }

    public decimal StoneValue { get; set; }

    public decimal Making
    {
        get => _making;
        set
        {
            if (_making != value)
            {
                _making = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(MakingPerGram));
            }
        }
    }

    public decimal MakingPerGram => NetWt > 0 ? Math.Round(Making / NetWt, 2) : 0m;

    public string Purity { get; set; } = string.Empty;
    public string SubGroup { get; set; } = string.Empty;
    public string HeadGroup { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public decimal CostPrice { get; set; }

    public decimal MRP
    {
        get => _mrp;
        set
        {
            if (_mrp != value)
            {
                _mrp = value;
                OnPropertyChanged();
            }
        }
    }

    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
    public DateTime? PDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string HUID { get; set; } = string.Empty;
    private string _size = string.Empty;
    public string Size
    {
        get => _size;
        set
        {
            if (_size != value)
            {
                _size = value;
                OnPropertyChanged();
            }
        }
    }
    public string OldBarcode { get; set; } = string.Empty;
    public string BType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Photo { get; set; } = string.Empty;
    public string VouNo { get; set; } = string.Empty;
    public string BillNo
    {
        get => VouNo;
        set => VouNo = value;
    }
    public DateTime? BillDate { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string MobileNo
    {
        get => Phone;
        set => Phone = value;
    }
    public bool IsMismatch { get; set; }
    public string MismatchReason { get; set; } = string.Empty;
    public string? ImagePath { get; set; }
    public bool HasImage => !string.IsNullOrWhiteSpace(ImagePath);
    private string _boxNo = string.Empty;
    public string BoxNo
    {
        get => _boxNo;
        set
        {
            if (_boxNo != value)
            {
                _boxNo = value;
                OnPropertyChanged();
            }
        }
    }
    public string Godown { get; set; } = string.Empty;
    public string Rack { get; set; } = string.Empty;

    private DateTime? _lastScan;
    public DateTime? LastScan
    {
        get => _lastScan;
        set
        {
            if (_lastScan != value)
            {
                _lastScan = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TimeStamp));
            }
        }
    }

    public DateTime? TimeStamp
    {
        get => _lastScan;
        set
        {
            if (_lastScan != value)
            {
                _lastScan = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LastScan));
            }
        }
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _isNewlyScanned;
    public bool IsNewlyScanned
    {
        get => _isNewlyScanned;
        set
        {
            if (_isNewlyScanned != value)
            {
                _isNewlyScanned = value;
                OnPropertyChanged();
            }
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
