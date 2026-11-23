namespace ConceptJewelleryPro.Shared;

public sealed class LocalPrinterSettings
{
    public string DefaultBillPrinter { get; set; } = string.Empty;

    public bool BillPrintPreviewBeforePrint { get; set; } = true;

    public string DefaultBarcodePrinter { get; set; } = string.Empty;

    public bool BarcodePrintPreviewBeforePrint { get; set; } = false;

    public string BarcodePrintEngine { get; set; } = "PRN";

    public bool HideWtInPcItem { get; set; } = true;

    public string SoftwarePath { get; set; } = string.Empty;

    public string PrintFiles { get; set; } = @"D:\Concept\FRX";

    public string SavePath { get; set; } = string.Empty;
}
