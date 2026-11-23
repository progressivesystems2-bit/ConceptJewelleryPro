using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class PurchaseSubformItemViewModel : ViewModelBase
{
    private int _slNo = 1;
    private string _barcode = string.Empty;
    private string _oldBarcode = string.Empty;
    private bool _pcRate;
    private string _itemName = string.Empty;
    private string _purity = string.Empty;
    private string _hsn = string.Empty;
    private string _eNote = string.Empty;
    private decimal _qty = 1m;
    private decimal _grWt;
    private decimal _lessWt;
    private decimal _ntWt;
    private decimal _rate;
    private decimal _amount;
    private decimal _metalValue;
    private decimal _mperGr;
    private decimal _making;
    private decimal _stoneValue;
    private decimal _disPer;
    private decimal _disAmt;
    private decimal _grossAmt;
    private decimal _taxableAmount;
    private decimal _gstPer = 3.00m;
    private decimal _gstAmt;
    private decimal _totalAmt;
    private bool _isDiscount;
    private bool _isRecalculating;

    public int SlNo
    {
        get => _slNo;
        set => SetProperty(ref _slNo, value);
    }

    public string Barcode
    {
        get => _barcode;
        set => SetProperty(ref _barcode, value);
    }

    public string OldBarcode
    {
        get => _oldBarcode;
        set => SetProperty(ref _oldBarcode, value);
    }

    public bool PcRate
    {
        get => _pcRate;
        set
        {
            if (SetProperty(ref _pcRate, value))
            {
                Recalculate();
            }
        }
    }

    public string ItemName
    {
        get => _itemName;
        set => SetProperty(ref _itemName, value);
    }

    public string Purity
    {
        get => _purity;
        set => SetProperty(ref _purity, value);
    }

    public string HSN
    {
        get => _hsn;
        set
        {
            if (SetProperty(ref _hsn, value))
            {
                OnPropertyChanged(nameof(Hsncode));
            }
        }
    }

    public string Hsncode
    {
        get => _hsn;
        set => HSN = value;
    }

    public string ENote
    {
        get => _eNote;
        set => SetProperty(ref _eNote, value);
    }

    public decimal Qty
    {
        get => _qty;
        set
        {
            if (SetProperty(ref _qty, value))
            {
                Recalculate();
            }
        }
    }

    public decimal GrWt
    {
        get => _grWt;
        set
        {
            if (SetProperty(ref _grWt, value))
            {
                if (_lessWt > 0 || _grWt > 0)
                {
                    _ntWt = Math.Max(0m, _grWt - _lessWt);
                    OnPropertyChanged(nameof(NtWt));
                }
                Recalculate();
            }
        }
    }

    public decimal LessWt
    {
        get => _lessWt;
        set
        {
            if (SetProperty(ref _lessWt, value))
            {
                _ntWt = Math.Max(0m, _grWt - _lessWt);
                OnPropertyChanged(nameof(NtWt));
                Recalculate();
            }
        }
    }

    public decimal NtWt
    {
        get => _ntWt;
        set
        {
            if (SetProperty(ref _ntWt, value))
            {
                Recalculate();
            }
        }
    }

    public decimal Rate
    {
        get => _rate;
        set
        {
            if (SetProperty(ref _rate, value))
            {
                Recalculate();
            }
        }
    }

    public decimal Amount
    {
        get => _amount;
        set => SetProperty(ref _amount, value);
    }

    public decimal MetalValue
    {
        get => _metalValue;
        set => SetProperty(ref _metalValue, value);
    }

    public decimal MperGr
    {
        get => _mperGr;
        set => SetProperty(ref _mperGr, value);
    }

    public decimal Making
    {
        get => _making;
        set => SetProperty(ref _making, value);
    }

    public decimal StoneValue
    {
        get => _stoneValue;
        set => SetProperty(ref _stoneValue, value);
    }

    public decimal DisPer
    {
        get => _disPer;
        set
        {
            if (SetProperty(ref _disPer, value))
            {
                Recalculate();
            }
        }
    }

    public decimal DisAmt
    {
        get => _disAmt;
        set
        {
            if (SetProperty(ref _disAmt, value))
            {
                Recalculate();
            }
        }
    }

    public decimal GrossAmt
    {
        get => _grossAmt;
        set => SetProperty(ref _grossAmt, value);
    }

    public decimal TaxableAmount
    {
        get => _taxableAmount;
        set
        {
            if (SetProperty(ref _taxableAmount, value))
            {
                OnPropertyChanged(nameof(TaxableValue));
            }
        }
    }

    public decimal TaxableValue
    {
        get => _taxableAmount;
        set => TaxableAmount = value;
    }

    public decimal GstPer
    {
        get => _gstPer;
        set
        {
            if (SetProperty(ref _gstPer, value))
            {
                Recalculate();
            }
        }
    }

    public decimal GstAmt
    {
        get => _gstAmt;
        set => SetProperty(ref _gstAmt, value);
    }

    public decimal TotalAmt
    {
        get => _totalAmt;
        set => SetProperty(ref _totalAmt, value);
    }

    public bool IsDiscount
    {
        get => _isDiscount;
        set => SetProperty(ref _isDiscount, value);
    }

    public void Recalculate()
    {
        if (_isRecalculating) return;
        _isRecalculating = true;

        try
        {
            // In Purchase Entry, TaxableValue is simply Qty * Rate
            _taxableAmount = Math.Round(_qty * _rate, 2);
            _amount = _taxableAmount;
            _grossAmt = _taxableAmount;
            _metalValue = 0m;
            _making = 0m;
            _mperGr = 0m;
            _stoneValue = 0m;
            _disAmt = 0m;

            _gstAmt = Math.Round(_taxableAmount * (_gstPer / 100m), 2);
            _totalAmt = _taxableAmount + _gstAmt;

            OnPropertyChanged(nameof(Amount));
            OnPropertyChanged(nameof(MetalValue));
            OnPropertyChanged(nameof(Making));
            OnPropertyChanged(nameof(DisAmt));
            OnPropertyChanged(nameof(GrossAmt));
            OnPropertyChanged(nameof(TaxableAmount));
            OnPropertyChanged(nameof(TaxableValue));
            OnPropertyChanged(nameof(GstAmt));
            OnPropertyChanged(nameof(TotalAmt));
        }
        finally
        {
            _isRecalculating = false;
        }
    }

    public PurchaseSubform ToDomainModel(string vouNo, int slNo)
    {
        return new PurchaseSubform
        {
            VouNo = vouNo,
            SlNo = slNo,
            Barcode = _barcode,
            OldBarcode = _oldBarcode,
            PcRate = _pcRate,
            ItemName = _itemName,
            Purity = _purity,
            HSN = _hsn,
            ENote = _eNote,
            Qty = _qty,
            GrWt = _grWt,
            LessWt = _lessWt,
            NtWt = _ntWt,
            Rate = _rate,
            Amount = _amount,
            MetalValue = _metalValue,
            Making = _making,
            MperGr = _mperGr,
            StoneValue = _stoneValue,
            DisPer = _disPer,
            DisAmt = _disAmt,
            GrossAmt = _grossAmt,
            TaxableAmount = _taxableAmount,
            GstPer = _gstPer,
            GstAmt = _gstAmt,
            TotalAmt = _totalAmt,
            IsDiscount = _isDiscount
        };
    }

    public static PurchaseSubformItemViewModel FromDomainModel(PurchaseSubform item)
    {
        var vm = new PurchaseSubformItemViewModel
        {
            SlNo = item.SlNo,
            Barcode = item.Barcode,
            OldBarcode = item.OldBarcode,
            PcRate = item.PcRate,
            ItemName = item.ItemName,
            Purity = item.Purity,
            HSN = item.HSN,
            ENote = item.ENote,
            Qty = item.Qty,
            GrWt = item.GrWt,
            LessWt = item.LessWt,
            NtWt = item.NtWt,
            Rate = item.Rate,
            Amount = item.Amount != 0 ? item.Amount : (item.Qty * item.Rate),
            MetalValue = item.MetalValue,
            Making = item.Making,
            MperGr = item.MperGr,
            StoneValue = item.StoneValue,
            DisPer = item.DisPer,
            DisAmt = item.DisAmt,
            GrossAmt = item.GrossAmt != 0 ? item.GrossAmt : (item.Qty * item.Rate),
            TaxableAmount = item.TaxableAmount != 0 ? item.TaxableAmount : (item.Qty * item.Rate),
            GstPer = item.GstPer != 0 ? item.GstPer : 3.00m,
            GstAmt = item.GstAmt,
            TotalAmt = item.TotalAmt != 0 ? item.TotalAmt : ((item.TaxableAmount != 0 ? item.TaxableAmount : (item.Qty * item.Rate)) + item.GstAmt),
            IsDiscount = item.IsDiscount
        };
        return vm;
    }
}
