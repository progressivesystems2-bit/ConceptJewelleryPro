namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class PaymentSubformItemViewModel : ViewModelBase
{
    private string _accountName = string.Empty;
    private decimal _amount;
    private string _headCategory = string.Empty;
    private string _advanceType = string.Empty;
    private string _narration = string.Empty;

    private string _itemType = string.Empty;
    private string _itemName = "OLD";
    private decimal _wt;
    private decimal _lessWt;
    private decimal _grWt;
    private decimal _alloy;
    private decimal _ntWt;
    private decimal _rate;

    public string AccountName
    {
        get => _accountName;
        set => SetProperty(ref _accountName, value);
    }

    public decimal Amount
    {
        get => _amount;
        set
        {
            if (SetProperty(ref _amount, value))
            {
                AmountChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public string HeadCategory
    {
        get => _headCategory;
        set => SetProperty(ref _headCategory, value);
    }

    public string AdvanceType
    {
        get => _advanceType;
        set
        {
            if (SetProperty(ref _advanceType, value))
            {
                OnPropertyChanged(nameof(DetailsSummary));
            }
        }
    }

    public string Narration
    {
        get => _narration;
        set => SetProperty(ref _narration, value);
    }

    public string ItemType
    {
        get => _itemType;
        set
        {
            if (SetProperty(ref _itemType, value))
            {
                OnPropertyChanged(nameof(DetailsSummary));
            }
        }
    }

    public string ItemName
    {
        get => _itemName;
        set => SetProperty(ref _itemName, value);
    }

    public decimal Wt
    {
        get => _wt;
        set
        {
            if (SetProperty(ref _wt, value))
            {
                OnPropertyChanged(nameof(DetailsSummary));
            }
        }
    }

    public decimal LessWt
    {
        get => _lessWt;
        set => SetProperty(ref _lessWt, value);
    }

    public decimal GrWt
    {
        get => _grWt;
        set
        {
            if (SetProperty(ref _grWt, value))
            {
                OnPropertyChanged(nameof(DetailsSummary));
            }
        }
    }

    public decimal Alloy
    {
        get => _alloy;
        set => SetProperty(ref _alloy, value);
    }

    public decimal NtWt
    {
        get => _ntWt;
        set => SetProperty(ref _ntWt, value);
    }

    public decimal Rate
    {
        get => _rate;
        set
        {
            if (SetProperty(ref _rate, value))
            {
                OnPropertyChanged(nameof(DetailsSummary));
            }
        }
    }

    public string DetailsSummary
    {
        get
        {
            if (Wt > 0 || Rate > 0)
            {
                var type = !string.IsNullOrWhiteSpace(ItemType) ? ItemType : "OLD";
                return $"[{type} | Wt: {GrWt:N3}g | Rate: {Rate:N2}]";
            }
            if (!string.IsNullOrWhiteSpace(AdvanceType))
            {
                return $"[Advance: {AdvanceType}]";
            }
            return string.Empty;
        }
    }

    public ConceptJewelleryPro.Domain.Entities.PaymentSubform ToDomainModel(string vouNo)
    {
        return new ConceptJewelleryPro.Domain.Entities.PaymentSubform
        {
            VouNo = vouNo,
            AccountName = _accountName,
            Amount = _amount,
            HeadCategory = _headCategory,
            Narration = _narration,
            ItemType = _itemType,
            ItemName = _itemName,
            Wt = _wt,
            LessWt = _lessWt,
            GrWt = _grWt,
            Alloy = _alloy,
            NtWt = _ntWt,
            Rate = _rate
        };
    }

    public static PaymentSubformItemViewModel FromDomainModel(ConceptJewelleryPro.Domain.Entities.PaymentSubform pay)
    {
        return new PaymentSubformItemViewModel
        {
            AccountName = pay.AccountName,
            Amount = pay.Amount,
            HeadCategory = pay.HeadCategory,
            Narration = pay.Narration,
            ItemType = pay.ItemType,
            ItemName = pay.ItemName,
            Wt = pay.Wt,
            LessWt = pay.LessWt,
            GrWt = pay.GrWt,
            Alloy = pay.Alloy,
            NtWt = pay.NtWt,
            Rate = pay.Rate
        };
    }

    public event EventHandler? AmountChanged;
}
