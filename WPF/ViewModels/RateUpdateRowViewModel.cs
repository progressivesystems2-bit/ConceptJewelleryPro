namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class RateUpdateRowViewModel : ViewModelBase
{
    private readonly decimal _currentRate;
    private decimal _newRate;

    public RateUpdateRowViewModel(string purity, decimal currentRate, string itemHead, string itemType)
    {
        Purity = purity;
        _currentRate = currentRate;
        _newRate = currentRate;
        ItemHead = itemHead;
        ItemType = itemType;
    }

    public string Purity { get; }

    public decimal CurrentRate => _currentRate;

    public string ItemHead { get; }

    public string ItemType { get; }

    public decimal NewRate
    {
        get => _newRate;
        set
        {
            if (SetProperty(ref _newRate, value))
            {
                OnPropertyChanged(nameof(IsChanged));
                OnPropertyChanged(nameof(Difference));
                OnPropertyChanged(nameof(DifferenceText));
                OnPropertyChanged(nameof(IsPositiveDifference));
                OnPropertyChanged(nameof(IsNegativeDifference));
            }
        }
    }

    public bool IsChanged => _newRate != _currentRate;

    public decimal Difference => _newRate - _currentRate;

    public bool IsPositiveDifference => Difference > 0;

    public bool IsNegativeDifference => Difference < 0;

    public string DifferenceText => Difference switch
    {
        > 0 => $"+{Difference:N2}",
        < 0 => $"{Difference:N2}",
        _ => "0.00"
    };

    public void Reset()
    {
        NewRate = _currentRate;
    }
}
