namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class BillSubformItemViewModel : ViewModelBase
{
    private string _barcode = string.Empty;
    private string _oldBarcode = string.Empty;
    private bool _pcRate;
    private decimal _unitPcRate;
    private string _itemName = string.Empty;
    private string _eNote = string.Empty;
    private int _qty = 1;
    private decimal _ntWt;
    private decimal _originalNtWt;
    private decimal _grWt;
    private decimal _rate;
    private decimal _metalValue;
    private decimal _mperGr;
    private decimal _making;
    private decimal _stoneValue;
    private decimal _grossAmt;
    private decimal _disPer;
    private decimal _disAmt;
    private decimal _amount;
    private decimal _tax = 3.00m;
    private decimal _abeforeTax;
    private decimal _vatAmt;
    private string _salesMan = string.Empty;
    private string _purity = string.Empty;
    private string _hsnCode = string.Empty;
    private bool _isRecalculating;

    public bool IsRecalculating => _isRecalculating;

    public decimal UnitPcRate
    {
        get => _unitPcRate;
        set => SetProperty(ref _unitPcRate, value);
    }

    public string Purity
    {
        get => _purity;
        set => SetProperty(ref _purity, value);
    }

    public bool CanEditItemName => string.IsNullOrWhiteSpace(Barcode);
    public bool IsItemNameReadOnly => !string.IsNullOrWhiteSpace(Barcode);

    public string Barcode
    {
        get => _barcode;
        set
        {
            if (SetProperty(ref _barcode, value))
            {
                OnPropertyChanged(nameof(CanEditItemName));
                OnPropertyChanged(nameof(IsItemNameReadOnly));
            }
        }
    }

    public string OldBarcode
    {
        get => _oldBarcode;
        set => SetProperty(ref _oldBarcode, value);
    }

    public string HsnCode
    {
        get => _hsnCode;
        set => SetProperty(ref _hsnCode, value);
    }

    public bool PcRate
    {
        get => _pcRate;
        set
        {
            if (SetProperty(ref _pcRate, value))
            {
                if (_pcRate)
                {
                    if (_grossAmt > 0 && _qty > 0)
                    {
                        _unitPcRate = Math.Round(_grossAmt / _qty, 2);
                    }
                    else if (_rate > 0)
                    {
                        _unitPcRate = _rate;
                    }
                    _rate = 0m;
                    _metalValue = 0m;
                    _making = 0m;
                    _mperGr = 0m;
                }
                else
                {
                    if (_unitPcRate > 0 && _rate == 0m)
                    {
                        _rate = _unitPcRate;
                    }
                }
                Recalculate();
            }
        }
    }

    public string ItemName
    {
        get => _itemName;
        set
        {
            if (!string.IsNullOrWhiteSpace(_barcode) && !string.IsNullOrWhiteSpace(_itemName) && _itemName != value)
            {
                return;
            }
            SetProperty(ref _itemName, value);
        }
    }

    public string ENote
    {
        get => _eNote;
        set => SetProperty(ref _eNote, value);
    }

    public int Qty
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

    public decimal OriginalNtWt
    {
        get => _originalNtWt;
        set => SetProperty(ref _originalNtWt, value);
    }

    public decimal Rate
    {
        get => _rate;
        set
        {
            if (_pcRate)
            {
                if (value > 0)
                {
                    _unitPcRate = value;
                }
                _rate = 0m;
                Recalculate();
                return;
            }
            if (SetProperty(ref _rate, value))
            {
                Recalculate();
            }
        }
    }

    public decimal MetalValue
    {
        get => _metalValue;
        set
        {
            if (_pcRate) return;
            if (SetProperty(ref _metalValue, value))
            {
                RecalculateFromMetalValue();
            }
        }
    }

    public decimal MperGr
    {
        get => _mperGr;
        set
        {
            if (_pcRate) return;
            if (SetProperty(ref _mperGr, value))
            {
                Recalculate();
            }
        }
    }

    public decimal Making
    {
        get => _making;
        set
        {
            if (_pcRate) return;
            if (SetProperty(ref _making, value))
            {
                RecalculateFromMaking();
            }
        }
    }

    public decimal StoneValue
    {
        get => _stoneValue;
        set
        {
            if (SetProperty(ref _stoneValue, value))
            {
                Recalculate();
            }
        }
    }

    public decimal GrossAmt
    {
        get => _grossAmt;
        set
        {
            if (SetProperty(ref _grossAmt, value))
            {
                if (_pcRate && _qty > 0)
                {
                    _unitPcRate = Math.Round(_grossAmt / _qty, 2);
                }
                RecalculateFromGrossAmt();
            }
        }
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
                RecalculateFromDisAmt();
            }
        }
    }

    public decimal Amount
    {
        get => _amount;
        set
        {
            if (SetProperty(ref _amount, value))
            {
                RecalculateFromAmount();
            }
        }
    }

    public decimal Tax
    {
        get => _tax;
        set
        {
            if (SetProperty(ref _tax, value))
            {
                Recalculate();
            }
        }
    }

    public decimal AbeforeTax
    {
        get => _abeforeTax;
        set => SetProperty(ref _abeforeTax, value);
    }

    public decimal Taxable
    {
        get => _abeforeTax;
        set => SetProperty(ref _abeforeTax, value);
    }

    public decimal VatAmt
    {
        get => _vatAmt;
        set => SetProperty(ref _vatAmt, value);
    }

    public string SalesMan
    {
        get => _salesMan;
        set => SetProperty(ref _salesMan, value);
    }

    public event EventHandler? ValuesChanged;

    public void ApplyBarcodeDetails(
        string barcode,
        string itemName,
        bool pcRate,
        decimal grWt,
        decimal ntWt,
        decimal originalNtWt,
        int qty,
        decimal mperGr,
        decimal making,
        decimal stoneValue,
        string eNote,
        string oldBarcode,
        string purity,
        decimal tax,
        decimal rate,
        decimal disPer = 0m,
        string? hsnCode = null)
    {
        _barcode = barcode;
        _itemName = itemName;
        _hsnCode = hsnCode ?? string.Empty;
        _pcRate = pcRate;
        _grWt = grWt;
        _ntWt = ntWt;
        _originalNtWt = originalNtWt;
        _qty = qty;
        _stoneValue = stoneValue;
        _eNote = eNote;
        _oldBarcode = oldBarcode;
        _purity = purity;
        _tax = tax;

        if (pcRate)
        {
            _unitPcRate = rate;
            _rate = 0m;
            _metalValue = 0m;
            _making = 0m;
            _mperGr = 0m;
        }
        else
        {
            _unitPcRate = 0m;
            _rate = rate;
            _mperGr = mperGr;
            _making = making;
        }

        if (disPer > 0) _disPer = disPer;

        RecalculateWithoutNotification();

        // Single atomic notification to refresh all UI bindings at once
        OnPropertyChanged(string.Empty);
        ValuesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RecalculateWithoutNotification()
    {
        if (_pcRate)
        {
            _rate = 0m;
            _metalValue = 0m;
            _making = 0m;
            _mperGr = 0m;
            if (_unitPcRate > 0)
            {
                _grossAmt = Math.Round(_qty * _unitPcRate, 2);
            }
        }
        else
        {
            var wt = _grWt > 0 ? _grWt : (_ntWt > 0 ? _ntWt : _qty);
            _metalValue = Math.Round(wt * _rate, 2);

            if (_mperGr > 0)
            {
                var wtForMaking = _grWt > 0 ? _grWt : _ntWt;
                _making = Math.Round(wtForMaking * _mperGr, 2);
            }

            _grossAmt = Math.Round(_metalValue + _making + _stoneValue, 2);
        }

        // Amount = GrossAmount - Discount
        if (_disPer > 0)
        {
            _disAmt = Math.Round(_grossAmt * (_disPer / 100m), 2);
        }

        _amount = Math.Round(_grossAmt - _disAmt, 2);

        // AbeforeTax = Amount / (1 + Vat% / 100)
        if (_tax > 0)
        {
            _abeforeTax = Math.Round(_amount / (1m + (_tax / 100m)), 2);
            _vatAmt = Math.Round(_amount - _abeforeTax, 2);
        }
        else
        {
            _abeforeTax = _amount;
            _vatAmt = 0.00m;
        }
    }

    public void Recalculate()
    {
        if (_isRecalculating) return;
        try
        {
            _isRecalculating = true;

            if (_pcRate)
            {
                _rate = 0m;
                _metalValue = 0m;
                _making = 0m;
                _mperGr = 0m;
                OnPropertyChanged(nameof(Rate));
                OnPropertyChanged(nameof(MetalValue));
                OnPropertyChanged(nameof(Making));
                OnPropertyChanged(nameof(MperGr));

                if (_unitPcRate > 0)
                {
                    _grossAmt = Math.Round(_qty * _unitPcRate, 2);
                    OnPropertyChanged(nameof(GrossAmt));
                }
            }
            else
            {
                // 1. MetalValue = GrWt * Rate (or Qty * Rate)
                var wt = _grWt > 0 ? _grWt : (_ntWt > 0 ? _ntWt : _qty);
                _metalValue = Math.Round(wt * _rate, 2);
                OnPropertyChanged(nameof(MetalValue));

                // 2. Making = GrWt * MperGr
                if (_mperGr > 0)
                {
                    var wtForMaking = _grWt > 0 ? _grWt : _ntWt;
                    _making = Math.Round(wtForMaking * _mperGr, 2);
                    OnPropertyChanged(nameof(Making));
                }

                // 3. GrossAmount = MetalValue + Making + StoneValue
                _grossAmt = Math.Round(_metalValue + _making + _stoneValue, 2);
                OnPropertyChanged(nameof(GrossAmt));
            }

            // 4. Amount = GrossAmount - Discount
            if (_disPer > 0)
            {
                _disAmt = Math.Round(_grossAmt * (_disPer / 100m), 2);
                OnPropertyChanged(nameof(DisAmt));
            }

            _amount = Math.Round(_grossAmt - _disAmt, 2);
            OnPropertyChanged(nameof(Amount));

            // 5. AbeforeTax = Amount / (1 + Vat% / 100)
            if (_tax > 0)
            {
                _abeforeTax = Math.Round(_amount / (1m + (_tax / 100m)), 2);
                _vatAmt = Math.Round(_amount - _abeforeTax, 2);
            }
            else
            {
                _abeforeTax = _amount;
                _vatAmt = 0.00m;
            }
            OnPropertyChanged(nameof(AbeforeTax));
            OnPropertyChanged(nameof(Taxable));
            OnPropertyChanged(nameof(VatAmt));
        }
        finally
        {
            _isRecalculating = false;
        }

        ValuesChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RecalculateFromGrossAmt()
    {
        if (_isRecalculating) return;
        try
        {
            _isRecalculating = true;

            if (_pcRate)
            {
                _rate = 0m;
                _metalValue = 0m;
                _making = 0m;
                _mperGr = 0m;
                if (_qty > 0)
                {
                    _unitPcRate = Math.Round(_grossAmt / _qty, 2);
                }
                OnPropertyChanged(nameof(Rate));
                OnPropertyChanged(nameof(MetalValue));
                OnPropertyChanged(nameof(Making));
                OnPropertyChanged(nameof(MperGr));
            }
            else
            {
                // 1. Rate is fixed -> MetalValue is fixed
                var wt = _grWt > 0 ? _grWt : (_ntWt > 0 ? _ntWt : _qty);
                _metalValue = Math.Round(wt * _rate, 2);
                OnPropertyChanged(nameof(MetalValue));

                // 2. Making = GrossAmt - StoneValue - MetalValue
                _making = Math.Round(_grossAmt - _stoneValue - _metalValue, 2);
                OnPropertyChanged(nameof(Making));

                // 3. MperGr = Making / NtWt
                var wtForMpergr = _ntWt > 0 ? _ntWt : (_grWt > 0 ? _grWt : (_qty > 0 ? _qty : 1m));
                if (wtForMpergr > 0)
                {
                    _mperGr = Math.Round(_making / wtForMpergr, 2);
                    OnPropertyChanged(nameof(MperGr));
                }
            }

            // 4. Discount & Amount
            if (_disPer > 0)
            {
                _disAmt = Math.Round(_grossAmt * (_disPer / 100m), 2);
                OnPropertyChanged(nameof(DisAmt));
            }

            _amount = Math.Round(_grossAmt - _disAmt, 2);
            OnPropertyChanged(nameof(Amount));

            // 5. AbeforeTax = Amount / (1 + (Vat% / 100))
            if (_tax > 0)
            {
                _abeforeTax = Math.Round(_amount / (1m + (_tax / 100m)), 2);
                _vatAmt = Math.Round(_amount - _abeforeTax, 2);
            }
            else
            {
                _abeforeTax = _amount;
                _vatAmt = 0.00m;
            }
            OnPropertyChanged(nameof(AbeforeTax));
            OnPropertyChanged(nameof(Taxable));
            OnPropertyChanged(nameof(VatAmt));
        }
        finally
        {
            _isRecalculating = false;
        }

        ValuesChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RecalculateFromAmount()
    {
        if (_isRecalculating) return;
        try
        {
            _isRecalculating = true;

            // 1. Reverse calculate GrossAmt from Amount
            if (_disPer > 0 && _disPer < 100m)
            {
                _grossAmt = Math.Round(_amount / (1m - (_disPer / 100m)), 2);
                _disAmt = Math.Round(_grossAmt - _amount, 2);
                OnPropertyChanged(nameof(DisAmt));
            }
            else
            {
                _grossAmt = Math.Round(_amount + _disAmt, 2);
            }
            OnPropertyChanged(nameof(GrossAmt));

            if (_pcRate)
            {
                _rate = 0m;
                _metalValue = 0m;
                _making = 0m;
                _mperGr = 0m;
                if (_qty > 0)
                {
                    _unitPcRate = Math.Round(_grossAmt / _qty, 2);
                }
                OnPropertyChanged(nameof(Rate));
                OnPropertyChanged(nameof(MetalValue));
                OnPropertyChanged(nameof(Making));
                OnPropertyChanged(nameof(MperGr));
            }
            else
            {
                // 2. Rate is fixed -> MetalValue is fixed
                var wt = _grWt > 0 ? _grWt : (_ntWt > 0 ? _ntWt : _qty);
                _metalValue = Math.Round(wt * _rate, 2);
                OnPropertyChanged(nameof(MetalValue));

                // 3. Making = GrossAmt - StoneValue - MetalValue
                _making = Math.Round(_grossAmt - _stoneValue - _metalValue, 2);
                OnPropertyChanged(nameof(Making));

                // 4. MperGr = Making / NtWt
                var wtForMpergr = _ntWt > 0 ? _ntWt : (_grWt > 0 ? _grWt : (_qty > 0 ? _qty : 1m));
                if (wtForMpergr > 0)
                {
                    _mperGr = Math.Round(_making / wtForMpergr, 2);
                    OnPropertyChanged(nameof(MperGr));
                }
            }

            // 5. AbeforeTax = Amount / (1 + (Vat% / 100))
            if (_tax > 0)
            {
                _abeforeTax = Math.Round(_amount / (1m + (_tax / 100m)), 2);
                _vatAmt = Math.Round(_amount - _abeforeTax, 2);
            }
            else
            {
                _abeforeTax = _amount;
                _vatAmt = 0.00m;
            }
            OnPropertyChanged(nameof(AbeforeTax));
            OnPropertyChanged(nameof(Taxable));
            OnPropertyChanged(nameof(VatAmt));
        }
        finally
        {
            _isRecalculating = false;
        }

        ValuesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RecalculateFromDisAmt()
    {
        if (_isRecalculating) return;
        try
        {
            _isRecalculating = true;

            if (_grossAmt > 0 && _disAmt > 0)
            {
                _disPer = Math.Round((_disAmt / _grossAmt) * 100m, 2);
                OnPropertyChanged(nameof(DisPer));
            }
            else if (_disAmt == 0)
            {
                _disPer = 0m;
                OnPropertyChanged(nameof(DisPer));
            }

            _amount = Math.Round(_grossAmt - _disAmt, 2);
            OnPropertyChanged(nameof(Amount));

            if (_tax > 0)
            {
                _abeforeTax = Math.Round(_amount / (1m + (_tax / 100m)), 2);
                _vatAmt = Math.Round(_amount - _abeforeTax, 2);
            }
            else
            {
                _abeforeTax = _amount;
                _vatAmt = 0.00m;
            }
            OnPropertyChanged(nameof(AbeforeTax));
            OnPropertyChanged(nameof(Taxable));
            OnPropertyChanged(nameof(VatAmt));
        }
        finally
        {
            _isRecalculating = false;
        }

        ValuesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RecalculateFromMetalValue()
    {
        if (_isRecalculating || _pcRate) return;
        try
        {
            _isRecalculating = true;

            _grossAmt = Math.Round(_metalValue + _making + _stoneValue, 2);
            OnPropertyChanged(nameof(GrossAmt));

            if (_disPer > 0)
            {
                _disAmt = Math.Round(_grossAmt * (_disPer / 100m), 2);
                OnPropertyChanged(nameof(DisAmt));
            }

            _amount = Math.Round(_grossAmt - _disAmt, 2);
            OnPropertyChanged(nameof(Amount));

            if (_tax > 0)
            {
                _abeforeTax = Math.Round(_amount / (1m + (_tax / 100m)), 2);
                _vatAmt = Math.Round(_amount - _abeforeTax, 2);
            }
            else
            {
                _abeforeTax = _amount;
                _vatAmt = 0.00m;
            }
            OnPropertyChanged(nameof(AbeforeTax));
            OnPropertyChanged(nameof(Taxable));
            OnPropertyChanged(nameof(VatAmt));
        }
        finally
        {
            _isRecalculating = false;
        }

        ValuesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RecalculateFromMaking()
    {
        if (_isRecalculating || _pcRate) return;
        try
        {
            _isRecalculating = true;

            var wtForMaking = _grWt > 0 ? _grWt : (_ntWt > 0 ? _ntWt : (_qty > 0 ? _qty : 1m));
            if (wtForMaking > 0)
            {
                _mperGr = Math.Round(_making / wtForMaking, 2);
                OnPropertyChanged(nameof(MperGr));
            }

            _grossAmt = Math.Round(_metalValue + _making + _stoneValue, 2);
            OnPropertyChanged(nameof(GrossAmt));

            if (_disPer > 0)
            {
                _disAmt = Math.Round(_grossAmt * (_disPer / 100m), 2);
                OnPropertyChanged(nameof(DisAmt));
            }

            _amount = Math.Round(_grossAmt - _disAmt, 2);
            OnPropertyChanged(nameof(Amount));

            if (_tax > 0)
            {
                _abeforeTax = Math.Round(_amount / (1m + (_tax / 100m)), 2);
                _vatAmt = Math.Round(_amount - _abeforeTax, 2);
            }
            else
            {
                _abeforeTax = _amount;
                _vatAmt = 0.00m;
            }
            OnPropertyChanged(nameof(AbeforeTax));
            OnPropertyChanged(nameof(Taxable));
            OnPropertyChanged(nameof(VatAmt));
        }
        finally
        {
            _isRecalculating = false;
        }

        ValuesChanged?.Invoke(this, EventArgs.Empty);
    }
}
