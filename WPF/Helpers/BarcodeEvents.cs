using System;

namespace ConceptJewelleryPro.WPF.Helpers;

/// <summary>
/// Global event broadcaster for barcode inventory changes across ViewModels to ensure reports and lookups stay fresh.
/// </summary>
public static class BarcodeEvents
{
    public static event Action? BarcodesChanged;

    public static void NotifyBarcodesChanged()
    {
        BarcodesChanged?.Invoke();
    }
}
