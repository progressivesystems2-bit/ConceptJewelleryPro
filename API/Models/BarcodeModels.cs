namespace ConceptJewelleryPro.API.Models;

public class BarcodeDetailResponse
{
    public string Barcode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string ItemCode { get; set; } = string.Empty;
    public string Purity { get; set; } = string.Empty;
    public decimal GrossWt { get; set; }
    public decimal StoneWt { get; set; }
    public decimal NetWt { get; set; }
    public decimal Making { get; set; }
    public string MakingType { get; set; } = "GRAM";
    public decimal MPerGr { get; set; }
    public decimal StoneValue { get; set; }
    public decimal MRP { get; set; }
    public string Size { get; set; } = "-";
    public string Description { get; set; } = string.Empty;
    public string HUID { get; set; } = string.Empty;
    public decimal RatePerGram { get; set; }
    public string? ImageUrl { get; set; }
    public int PrintStickerNo { get; set; }
    public string Status { get; set; } = "Active";
    public string StickerFormat { get; set; } = "StickerWt";
    public string BType { get; set; } = "WT";
}

public class UpdateBarcodeAdvancedRequest
{
    public string Barcode { get; set; } = string.Empty;
    public string? BType { get; set; }
    public decimal? MRP { get; set; }
    public decimal? Making { get; set; }
    public string? Size { get; set; }
    public bool ReprintSticker { get; set; } = true;
    public string? StickerFormat { get; set; }
    public string? PrinterName { get; set; }
    public string? PrintEngine { get; set; }
    public bool PrintNow { get; set; } = true;
}

public class SetDefaultPrinterRequest
{
    public string PrinterName { get; set; } = string.Empty;
    public string? PrintEngine { get; set; }
}

public class SetSoftwarePathRequest
{
    public string SoftwarePath { get; set; } = string.Empty;
}
