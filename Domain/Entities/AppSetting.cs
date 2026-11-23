namespace ConceptJewelleryPro.Domain.Entities;

public sealed class AppSetting
{
    public const string SavePathKey = "SavePath";
    public const string PrintFilesKey = "PrintFiles";
    public const string WaSendMethodKey = "WaSendMethod";
    public const string WaAPIKeyKey = "WaAPIKey";
    public const string WaTemplateKey = "WaTemplate";
    public const string WaApiTemplateNameKey = "WaApiTemplateName";
    public const string WaApiLanguageKey = "WaApiLanguage";
    public const string WaApiParametersKey = "WaApiParameters";
    public const string WaAttachPdfKey = "WaAttachPdf";
    public const string WaApiDocumentUrlKey = "WaApiDocumentUrl";
    public const string WaBusinessNoKey = "WaBusinessNo";
    public const string WaAutoSendKey = "WaAutoSend";
    public const string EinvoiceModeKey = "EinvoiceMode";
    public const string DefaultBillPrinterKey = "DefaultBillPrinter";
    public const string DefaultBarcodePrinterKey = "DefaultBarcodePrinter";
    public const string BarcodePrinterKey = "BarcodePrinter";
    public const string BarcodePrintEngineKey = "BarcodePrintEngine";
    public const string HideWtInPcItemsKey = "HideWtInPcItems";
    public const string AllowNtWtModUptoKey = "AllowNtWtModUpto";
    public const string AnniversaryMessageKey = "AnniversaryMessage";
    public const string BirthdayMessageKey = "BirthdayMessage";
    public const string WishesModeKey = "WishesMode";
    public const string SoftwarePathKey = "SoftwarePath";
    public const string WebServerPortKey = "WebServerPort";
    public const string WebServerHostKey = "WebServerHost";
    public const string WebServerExePathKey = "WebServerExePath";
    public const string WebServerAutoStartKey = "WebServerAutoStart";

    // ── Weight Machine (Serial Port) ─────────────────────────────────
    /// <summary>Whether the weight machine integration is active (true/false).</summary>
    public const string WeightEnabledKey = "weight_enabled";

    /// <summary>COM port used by the weighing scale, e.g. "COM1".</summary>
    public const string WeightCommPortKey = "weight_commport";

    /// <summary>Baud rate for the serial connection, e.g. "9600".</summary>
    public const string WeightBaudRateKey = "weight_baudrate";

    /// <summary>Data bits (5 / 6 / 7 / 8). Default: "8".</summary>
    public const string WeightDataBitsKey = "weight_databits";

    /// <summary>Stop bits (1 / 1.5 / 2). Default: "1".</summary>
    public const string WeightStopBitsKey = "weight_stopbits";

    /// <summary>Parity (None / Even / Odd / Mark / Space). Default: "None".</summary>
    public const string WeightParityKey = "weight_parity";

    /// <summary>Flow control / handshake (None / RtsCtsCom / XOnXOff). Default: "None".</summary>
    public const string WeightHandshakeKey = "weight_handshake";

    /// <summary>Prefix/pattern that marks a stable weight reading, e.g. "ST,GS".</summary>
    public const string WeightStablePrefixKey = "weight_stable_prefix";

    /// <summary>Read timeout in milliseconds, e.g. "500".</summary>
    public const string WeightTimeoutMsKey = "weight_timeout_ms";
    // ────────────────────────────────────────────────────────────────

    public string SettingsName { get; set; } = string.Empty;

    public string? SettingsValue { get; set; }

    public AppSetting()
    {
    }

    public AppSetting(string settingsName, string? settingsValue)
    {
        SettingsName = settingsName;
        SettingsValue = settingsValue;
    }
}
