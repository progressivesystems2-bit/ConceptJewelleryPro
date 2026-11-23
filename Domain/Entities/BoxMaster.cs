using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ConceptJewelleryPro.Domain.Entities;

public class BoxMaster : INotifyPropertyChanged
{
    private bool _isSelected;

    public string BoxNo { get; set; } = string.Empty;
    public string BoxName { get; set; } = string.Empty;
    public string Rack { get; set; } = string.Empty;
    public int NumberOfBoxes { get; set; } = 1;
    public decimal BoxWt { get; set; }
    public int Serial { get; set; }
    public string Status { get; set; } = "Active";
    public string Remarks { get; set; } = string.Empty;
    public DateTime? CreatedDate { get; set; } = DateTime.Now;
    public DateTime? ModifiedDate { get; set; } = DateTime.Now;

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

    public string DisplayText => string.IsNullOrWhiteSpace(BoxName)
        ? (string.IsNullOrWhiteSpace(Rack) ? BoxNo : $"{BoxNo} (Rack: {Rack})")
        : (string.IsNullOrWhiteSpace(Rack) ? $"{BoxNo} - {BoxName}" : $"{BoxNo} - {BoxName} (Rack: {Rack})");

    public override string ToString() => DisplayText;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

