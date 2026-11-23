using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ConceptJewelleryPro.Domain.Entities;

public class BarcodeSubformItem : INotifyPropertyChanged
{
    private string _barcode = string.Empty;
    private int _barcodeAutoNo;
    private int _nos;
    private string _vouNo = "9";
    private string _bType = "WT";
    private string _itemName = string.Empty;
    private string _itemCode = string.Empty;
    private string _purity = string.Empty;
    private string _description = string.Empty;
    private string _huid = string.Empty; // Description2 in DB
    private string _size = "-";
    private decimal _qty = 1m;
    private decimal _grossWt;
    private decimal _stoneWt; // Less weight
    private decimal _netWt;
    private decimal _making;
    private string _makingType = "GRAM";
    private decimal _mPerGr;
    private decimal _stoneValue;
    private decimal _costTounch;
    private decimal _costPg;
    private decimal _costMaking;
    private decimal _costPrice;
    private decimal _costRate;
    private decimal _mrp;
    private decimal _profit = 1.65m;
    private string _supplierName = "OPN";
    private string _partyCode = "OPN";
    private string _suppInvNo = string.Empty;
    private DateTime? _suppDate;
    private DateTime _pDate = DateTime.Today;
    private DateTime _punchedOn = DateTime.Now;
    private string _photo = string.Empty;
    private string _sti = "StickerWt";
    private string _status = "Active";
    private bool _sale;
    private bool _open;
    private string _entryUser = "admin";
    private string _modifyUser = string.Empty;
    private DateTime? _modifyDate;
    private DateTime? _modifyTime;
    private int _printStickerNo;
    private string _boxNo = string.Empty;
    private string _brand = string.Empty;
    private string _remarks1 = string.Empty;
    private string _note = string.Empty;
    private string _godown = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Barcode
    {
        get => _barcode;
        set => SetField(ref _barcode, value);
    }

    public int BarcodeAutoNo
    {
        get => _barcodeAutoNo;
        set => SetField(ref _barcodeAutoNo, value);
    }

    public int Nos
    {
        get => _nos;
        set => SetField(ref _nos, value);
    }

    public string VouNo
    {
        get => _vouNo;
        set => SetField(ref _vouNo, value);
    }

    public string BType
    {
        get => _bType;
        set
        {
            if (SetField(ref _bType, value))
            {
                if (string.Equals(value, "PC", StringComparison.OrdinalIgnoreCase))
                {
                    Sti = "StickerPC";
                }
                else if (string.Equals(value, "WT", StringComparison.OrdinalIgnoreCase) || value == "-")
                {
                    Sti = "StickerWt";
                }
            }
        }
    }

    public string ItemName
    {
        get => _itemName;
        set => SetField(ref _itemName, value);
    }

    public string ItemCode
    {
        get => _itemCode;
        set => SetField(ref _itemCode, value);
    }

    public string Purity
    {
        get => _purity;
        set => SetField(ref _purity, value);
    }

    public string Description
    {
        get => _description;
        set => SetField(ref _description, value);
    }

    /// <summary>
    /// HUID number mapped to Description2 column in BarcodeSubform
    /// </summary>
    public string HUID
    {
        get => _huid;
        set => SetField(ref _huid, value);
    }

    public string Size
    {
        get => _size;
        set => SetField(ref _size, value);
    }

    public decimal Qty
    {
        get => _qty;
        set => SetField(ref _qty, value);
    }

    public decimal GrossWt
    {
        get => _grossWt;
        set
        {
            if (SetField(ref _grossWt, value))
            {
                RecalculateNetWeight();
            }
        }
    }

    /// <summary>
    /// Less Weight (StoneWt)
    /// </summary>
    public decimal StoneWt
    {
        get => _stoneWt;
        set
        {
            if (SetField(ref _stoneWt, value))
            {
                RecalculateNetWeight();
            }
        }
    }

    public decimal NetWt
    {
        get => _netWt;
        set => SetField(ref _netWt, value);
    }

    public decimal Making
    {
        get => _making;
        set => SetField(ref _making, value);
    }

    public string MakingType
    {
        get => _makingType;
        set => SetField(ref _makingType, value);
    }

    public decimal MPerGr
    {
        get => _mPerGr;
        set => SetField(ref _mPerGr, value);
    }

    public decimal StoneValue
    {
        get => _stoneValue;
        set => SetField(ref _stoneValue, value);
    }

    public decimal CostTounch
    {
        get => _costTounch;
        set => SetField(ref _costTounch, value);
    }

    public decimal CostPg
    {
        get => _costPg;
        set => SetField(ref _costPg, value);
    }

    public decimal CostMaking
    {
        get => _costMaking;
        set => SetField(ref _costMaking, value);
    }

    public decimal CostPrice
    {
        get => _costPrice;
        set => SetField(ref _costPrice, value);
    }

    public decimal CostRate
    {
        get => _costRate;
        set => SetField(ref _costRate, value);
    }

    public decimal MRP
    {
        get => _mrp;
        set => SetField(ref _mrp, value);
    }

    public decimal Profit
    {
        get => _profit;
        set => SetField(ref _profit, value);
    }

    public string SupplierName
    {
        get => _supplierName;
        set => SetField(ref _supplierName, value);
    }

    public string PartyCode
    {
        get => _partyCode;
        set => SetField(ref _partyCode, value);
    }

    public string SuppInvNo
    {
        get => _suppInvNo;
        set => SetField(ref _suppInvNo, value);
    }

    public DateTime? SuppDate
    {
        get => _suppDate;
        set => SetField(ref _suppDate, value);
    }

    public DateTime PDate
    {
        get => _pDate;
        set => SetField(ref _pDate, value);
    }

    public DateTime PunchedOn
    {
        get => _punchedOn;
        set => SetField(ref _punchedOn, value);
    }

    public string Photo
    {
        get => _photo;
        set => SetField(ref _photo, value);
    }

    public string Sti
    {
        get => _sti;
        set => SetField(ref _sti, value);
    }

    public string Status
    {
        get => _status;
        set => SetField(ref _status, value);
    }

    public bool Sale
    {
        get => _sale;
        set => SetField(ref _sale, value);
    }

    public bool Open
    {
        get => _open;
        set => SetField(ref _open, value);
    }

    public string EntryUser
    {
        get => _entryUser;
        set => SetField(ref _entryUser, value);
    }

    public string ModifyUser
    {
        get => _modifyUser;
        set => SetField(ref _modifyUser, value);
    }

    public DateTime? ModifyDate
    {
        get => _modifyDate;
        set => SetField(ref _modifyDate, value);
    }

    public DateTime? ModifyTime
    {
        get => _modifyTime;
        set => SetField(ref _modifyTime, value);
    }

    public int PrintStickerNo
    {
        get => _printStickerNo;
        set => SetField(ref _printStickerNo, value);
    }

    public string BoxNo
    {
        get => _boxNo;
        set => SetField(ref _boxNo, value);
    }

    public string Brand
    {
        get => _brand;
        set => SetField(ref _brand, value);
    }

    public string Remarks1
    {
        get => _remarks1;
        set => SetField(ref _remarks1, value);
    }

    public string Note
    {
        get => _note;
        set => SetField(ref _note, value);
    }

    public string Godown
    {
        get => _godown;
        set => SetField(ref _godown, value);
    }

    public void RecalculateNetWeight()
    {
        var net = GrossWt - StoneWt;
        NetWt = net < 0 ? 0m : net;
    }

    public BarcodeSubformItem Clone() => new()
    {
        Barcode = Barcode,
        BarcodeAutoNo = BarcodeAutoNo,
        Nos = Nos,
        VouNo = VouNo,
        BType = BType,
        ItemName = ItemName,
        ItemCode = ItemCode,
        Purity = Purity,
        Description = Description,
        HUID = HUID,
        Size = Size,
        Qty = Qty,
        GrossWt = GrossWt,
        StoneWt = StoneWt,
        NetWt = NetWt,
        Making = Making,
        MakingType = MakingType,
        MPerGr = MPerGr,
        StoneValue = StoneValue,
        CostTounch = CostTounch,
        CostPg = CostPg,
        CostMaking = CostMaking,
        CostPrice = CostPrice,
        CostRate = CostRate,
        MRP = MRP,
        Profit = Profit,
        SupplierName = SupplierName,
        PartyCode = PartyCode,
        SuppInvNo = SuppInvNo,
        SuppDate = SuppDate,
        PDate = PDate,
        PunchedOn = PunchedOn,
        Photo = Photo,
        Sti = Sti,
        Status = Status,
        Sale = Sale,
        Open = Open,
        EntryUser = EntryUser,
        ModifyUser = ModifyUser,
        ModifyDate = ModifyDate,
        ModifyTime = ModifyTime,
        PrintStickerNo = PrintStickerNo,
        BoxNo = BoxNo,
        Brand = Brand,
        Remarks1 = Remarks1,
        Note = Note,
        Godown = Godown
    };

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
