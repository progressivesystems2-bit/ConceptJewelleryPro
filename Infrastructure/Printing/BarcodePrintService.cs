using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.Infrastructure.Settings;
using FastReport;
using FastReport.Export.Image;
using FrPageRange = FastReport.PageRange;

namespace ConceptJewelleryPro.Infrastructure.Printing;

public sealed class BarcodePrintService : IBarcodePrintService
{
    private readonly LocalPrinterSettingsStore _settingsStore;
    private readonly ISettingsRepository? _settingsRepository;
    private readonly Func<string?>? _fallbackSoftwarePathProvider;

    public Func<BarcodeStickerPreviewData, Task<bool>>? PreviewHandler { get; set; }

    public BarcodePrintService(
        LocalPrinterSettingsStore settingsStore,
        ISettingsRepository? settingsRepository = null,
        Func<string?>? fallbackSoftwarePathProvider = null)
    {
        _settingsStore = settingsStore;
        _settingsRepository = settingsRepository;
        _fallbackSoftwarePathProvider = fallbackSoftwarePathProvider;
    }

    public Task PrintBarcodeStickerAsync(BarcodeSubformItem item, string? customStickerFormat = null, CancellationToken cancellationToken = default)
    {
        return PrintBarcodeStickerAsync(item, customStickerFormat, targetPrinter: null, printEngine: null, cancellationToken);
    }

    public Task PrintBarcodeStickerAsync(BarcodeSubformItem item, string? customStickerFormat, string? targetPrinter, CancellationToken cancellationToken = default)
    {
        return PrintBarcodeStickerAsync(item, customStickerFormat, targetPrinter, printEngine: null, cancellationToken);
    }

    public async Task<string> ResolveSoftwarePathAsync(CancellationToken cancellationToken = default)
    {
        // 1. Try database [Settings] table
        if (_settingsRepository != null)
        {
            try
            {
                var dbPath = await _settingsRepository.GetValueAsync(AppSetting.SoftwarePathKey, cancellationToken);
                if (!string.IsNullOrWhiteSpace(dbPath)) return dbPath.Trim();
            }
            catch { }
        }

        // 2. Try local printer settings store (printersettings.json)
        try
        {
            var localSettings = _settingsStore.Load();
            if (!string.IsNullOrWhiteSpace(localSettings.SoftwarePath))
            {
                return localSettings.SoftwarePath.Trim();
            }
        }
        catch { }

        // 3. Try fallback provider (e.g. appsettings.json)
        try
        {
            var providerPath = _fallbackSoftwarePathProvider?.Invoke();
            if (!string.IsNullOrWhiteSpace(providerPath))
            {
                return providerPath.Trim();
            }
        }
        catch { }

        // 4. Default fallback to production executable folder
        return @"E:\TATTVA JEWELS26-27\net8.0-windows";
    }

    public async Task<string> ResolvePrintEngineAsync(
        string? explicitEngine = null,
        string? customStickerFormat = null,
        string? itemSti = null,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(explicitEngine))
        {
            return NormalizeEngine(explicitEngine);
        }

        if (!string.IsNullOrWhiteSpace(customStickerFormat))
        {
            if (customStickerFormat.Trim().EndsWith(".frx", StringComparison.OrdinalIgnoreCase))
                return "FRX";
            if (customStickerFormat.Trim().EndsWith(".prn", StringComparison.OrdinalIgnoreCase))
                return "PRN";
        }

        if (!string.IsNullOrWhiteSpace(itemSti))
        {
            if (itemSti.Trim().EndsWith(".frx", StringComparison.OrdinalIgnoreCase))
                return "FRX";
            if (itemSti.Trim().EndsWith(".prn", StringComparison.OrdinalIgnoreCase))
                return "PRN";
        }

        try
        {
            var localSettings = _settingsStore.Load();
            if (!string.IsNullOrWhiteSpace(localSettings.BarcodePrintEngine))
            {
                return NormalizeEngine(localSettings.BarcodePrintEngine);
            }
        }
        catch { }

        if (_settingsRepository != null)
        {
            try
            {
                var dbEngine = await _settingsRepository.GetValueAsync(AppSetting.BarcodePrintEngineKey, cancellationToken);
                if (!string.IsNullOrWhiteSpace(dbEngine))
                {
                    return NormalizeEngine(dbEngine);
                }
            }
            catch { }
        }

        return "PRN";
    }

    public static string NormalizeEngine(string? engine)
    {
        if (string.IsNullOrWhiteSpace(engine))
            return "PRN";

        var val = engine.Trim();
        if (val.Contains("FRX", StringComparison.OrdinalIgnoreCase) ||
            val.Contains("FastReport", StringComparison.OrdinalIgnoreCase))
        {
            return "FRX";
        }

        return "PRN";
    }

    public async Task PrintBarcodeStickerAsync(
        BarcodeSubformItem item,
        string? customStickerFormat,
        string? targetPrinter,
        string? printEngine,
        CancellationToken cancellationToken = default)
    {
        string? printerName = targetPrinter?.Trim();

        // 1. Try explicit target printer passed in call
        // 2. Try local workstation printer settings store (printersettings.json) FIRST
        if (string.IsNullOrWhiteSpace(printerName))
        {
            try
            {
                var localSettings = _settingsStore.Load();
                if (!string.IsNullOrWhiteSpace(localSettings.DefaultBarcodePrinter))
                {
                    printerName = localSettings.DefaultBarcodePrinter.Trim();
                }
            }
            catch { }
        }

        // 3. Try database [Settings] table (DefaultBarcodePrinter or BarcodePrinter)
        if (string.IsNullOrWhiteSpace(printerName) && _settingsRepository != null)
        {
            try
            {
                var dbPrinter = await _settingsRepository.GetValueAsync(AppSetting.DefaultBarcodePrinterKey, cancellationToken);
                if (string.IsNullOrWhiteSpace(dbPrinter))
                {
                    dbPrinter = await _settingsRepository.GetValueAsync(AppSetting.BarcodePrinterKey, cancellationToken);
                }
                if (!string.IsNullOrWhiteSpace(dbPrinter))
                {
                    printerName = dbPrinter.Trim();
                }
            }
            catch
            {
            }
        }

        // 4. Match against installed printers and prefer online queue over offline duplicate queue
        printerName = ResolveBestInstalledPrinter(printerName);

        // 5. Fallback to auto-detection only if no printer is defined in settings
        if (string.IsNullOrWhiteSpace(printerName))
        {
            printerName = AutoDetectBarcodePrinter();
            if (string.IsNullOrWhiteSpace(printerName))
            {
                throw new InvalidOperationException("Barcode Printer is not configured in settings and no installed thermal printer was detected in Windows.");
            }
        }

        // Resolve SoftwarePath and Engine (PRN or FRX)
        var softwarePath = await ResolveSoftwarePathAsync(cancellationToken);
        var effectiveEngine = await ResolvePrintEngineAsync(printEngine, customStickerFormat, item.Sti, cancellationToken);

        // Strip .prn / .frx extension when resolving base format name
        var cleanCustomFormat = string.IsNullOrWhiteSpace(customStickerFormat)
            ? null
            : Path.GetFileNameWithoutExtension(customStickerFormat.Trim());
        var cleanItemSti = string.IsNullOrWhiteSpace(item.Sti)
            ? null
            : Path.GetFileNameWithoutExtension(item.Sti.Trim());

        // Determine format name with type-aware resolution:
        string formatName;
        if (!string.IsNullOrWhiteSpace(cleanCustomFormat) &&
            (string.Equals(cleanCustomFormat, "StickerPC", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(cleanCustomFormat, "StickerWt", StringComparison.OrdinalIgnoreCase)))
        {
            formatName = string.Equals(cleanCustomFormat, "StickerPC", StringComparison.OrdinalIgnoreCase) ? "StickerPC" : "StickerWt";
        }
        else if (!string.IsNullOrWhiteSpace(cleanItemSti) &&
            (string.Equals(cleanItemSti, "StickerPC", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(cleanItemSti, "StickerWt", StringComparison.OrdinalIgnoreCase)))
        {
            formatName = string.Equals(cleanItemSti, "StickerPC", StringComparison.OrdinalIgnoreCase) ? "StickerPC" : "StickerWt";
        }
        else if (!string.IsNullOrWhiteSpace(cleanCustomFormat) && TemplateExists(cleanCustomFormat, softwarePath, effectiveEngine))
        {
            formatName = cleanCustomFormat;
        }
        else if (!string.IsNullOrWhiteSpace(cleanItemSti) && TemplateExists(cleanItemSti, softwarePath, effectiveEngine))
        {
            formatName = cleanItemSti;
        }
        else
        {
            // Default based on item Type PC/WT:
            formatName = string.Equals(item.BType, "PC", StringComparison.OrdinalIgnoreCase)
                ? "StickerPC"
                : "StickerWt";
        }

        var showPreview = false;
        try { showPreview = _settingsStore.Load().BarcodePrintPreviewBeforePrint; } catch { }

        if (string.Equals(effectiveEngine, "FRX", StringComparison.OrdinalIgnoreCase))
        {
            var frxXml = LoadFrxTemplate(formatName, item.BType, softwarePath);
            var prepared = await Task.Run(() => PrepareFrxSticker(printerName, frxXml, formatName, item), cancellationToken);

            if (showPreview && PreviewHandler != null)
            {
                var shouldPrint = await PreviewHandler(new BarcodeStickerPreviewData
                {
                    Barcode = item.Barcode?.Trim() ?? string.Empty,
                    ItemName = item.ItemName?.Trim() ?? string.Empty,
                    FormatName = formatName,
                    PrintEngine = "FRX",
                    PrinterName = printerName,
                    PreviewImageBytes = prepared.PageBytesList.FirstOrDefault(),
                    Item = item
                });
                if (!shouldPrint)
                    return;
            }

            await Task.Run(() =>
            {
                SendPreparedFrxToPrinter(printerName, formatName, item, prepared);
            }, cancellationToken);
        }
        else
        {
            var templateContent = LoadTemplate(formatName, item.BType, softwarePath);
            var prnCommand = PopulateTemplate(templateContent, item);

            if (showPreview && PreviewHandler != null)
            {
                var shouldPrint = await PreviewHandler(new BarcodeStickerPreviewData
                {
                    Barcode = item.Barcode?.Trim() ?? string.Empty,
                    ItemName = item.ItemName?.Trim() ?? string.Empty,
                    FormatName = formatName,
                    PrintEngine = "PRN",
                    PrinterName = printerName,
                    RawPrnCommand = prnCommand,
                    Item = item
                });
                if (!shouldPrint)
                    return;
            }

            await Task.Run(() =>
            {
                RawPrinterHelper.SendStringToPrinter(printerName, prnCommand, $"Sticker-{item.Barcode}");
            }, cancellationToken);
        }
    }

    [DllImport("winspool.drv", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool OpenPrinter(string pPrinterName, out IntPtr phPrinter, IntPtr pDefault);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GetPrinter(IntPtr hPrinter, int level, IntPtr pPrinter, int cbBuf, out int pcbNeeded);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct PRINTER_INFO_2
    {
        public string? pServerName;
        public string? pPrinterName;
        public string? pShareName;
        public string? pPortName;
        public string? pDriverName;
        public string? pComment;
        public string? pLocation;
        public IntPtr pDevMode;
        public string? pSepFile;
        public string? pPrintProcessor;
        public string? pDatatype;
        public string? pParameters;
        public IntPtr pSecurityDescriptor;
        public uint Attributes;
        public uint Priority;
        public uint DefaultPriority;
        public uint StartTime;
        public uint UntilTime;
        public uint Status;
        public uint cJobs;
        public uint AveragePPM;
    }

    public static bool IsPrinterWorkOffline(string? printerName)
    {
        if (string.IsNullOrWhiteSpace(printerName))
            return true;

        try
        {
            if (!OpenPrinter(printerName.Trim(), out var hPrinter, IntPtr.Zero))
                return true;

            try
            {
                GetPrinter(hPrinter, 2, IntPtr.Zero, 0, out var needed);
                if (needed <= 0)
                    return false;

                var buf = Marshal.AllocHGlobal(needed);
                try
                {
                    if (GetPrinter(hPrinter, 2, buf, needed, out _))
                    {
                        var info = Marshal.PtrToStructure<PRINTER_INFO_2>(buf);
                        const uint PRINTER_ATTRIBUTE_WORK_OFFLINE = 0x00000400u;
                        return (info.Attributes & PRINTER_ATTRIBUTE_WORK_OFFLINE) != 0;
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buf);
                }
            }
            finally
            {
                ClosePrinter(hPrinter);
            }
        }
        catch
        {
        }

        return false;
    }

    public static string? ResolveBestInstalledPrinter(string? configuredPrinter)
    {
        if (string.IsNullOrWhiteSpace(configuredPrinter))
            return null;

        var requested = configuredPrinter.Trim();
        try
        {
            var installed = PrinterSettings.InstalledPrinters.Cast<string>().Select(p => p.Trim()).ToList();
            if (installed.Count == 0)
                return requested;

            var shortName = ExtractShortQueueName(requested);
            var suffix = @"\" + shortName;

            // Find all installed printers that match either exact name or share/queue short name
            var matchingQueues = installed
                .Where(p => string.Equals(p, requested, StringComparison.OrdinalIgnoreCase)
                            || (!string.IsNullOrWhiteSpace(shortName) &&
                                (string.Equals(p, shortName, StringComparison.OrdinalIgnoreCase) ||
                                 p.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))))
                .ToList();

            if (matchingQueues.Count > 0)
            {
                // 1. If exact match exists and is ONLINE, use it
                var exactMatch = matchingQueues.FirstOrDefault(p => string.Equals(p, requested, StringComparison.OrdinalIgnoreCase));
                if (exactMatch != null && !IsPrinterWorkOffline(exactMatch))
                {
                    return exactMatch;
                }

                // 2. Otherwise pick any matching queue that is ONLINE (e.g. \\LAPTOP-BATU73FO\Godex EZ520 when local Godex EZ520 is offline)
                var onlineMatch = matchingQueues.FirstOrDefault(p => !IsPrinterWorkOffline(p));
                if (onlineMatch != null)
                {
                    Console.WriteLine($"[BarcodePrintService] Switched offline printer '{requested}' to online queue '{onlineMatch}'.");
                    return onlineMatch;
                }

                if (exactMatch != null)
                    return exactMatch;

                return matchingQueues[0];
            }
        }
        catch
        {
        }

        return requested;
    }

    private static string ExtractShortQueueName(string printerName)
    {
        var trimmed = printerName.Trim();
        if (!trimmed.StartsWith(@"\\", StringComparison.Ordinal))
            return trimmed;

        var lastSlash = trimmed.LastIndexOf('\\');
        return lastSlash >= 0 && lastSlash + 1 < trimmed.Length
            ? trimmed[(lastSlash + 1)..].Trim()
            : trimmed;
    }

    public static string? AutoDetectBarcodePrinter()
    {
        try
        {
            var installedPrinters = PrinterSettings.InstalledPrinters.Cast<string>().ToList();
            if (installedPrinters.Count == 0) return null;

            var onlinePrinters = installedPrinters.Where(p => !IsPrinterWorkOffline(p)).ToList();
            var candidates = onlinePrinters.Count > 0 ? onlinePrinters : installedPrinters;

            var preferredKeywords = new[] { "Godex", "EZ520", "TSC", "Barcode", "Label", "ZDesigner", "ZD230", "Zebra" };
            foreach (var kw in preferredKeywords)
            {
                var match = candidates.FirstOrDefault(p => p.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0);
                if (!string.IsNullOrEmpty(match)) return match;
            }

            var defaultPrinter = new PrinterSettings().PrinterName;
            if (!string.IsNullOrWhiteSpace(defaultPrinter) && candidates.Contains(defaultPrinter))
            {
                return defaultPrinter;
            }

            return candidates[0];
        }
        catch
        {
            return null;
        }
    }

    public static IEnumerable<string> GetCandidateDirectories(string? softwarePath = null)
    {
        var list = new List<string>();

        // 1. Explicitly configured Software Installation Path (top priority):
        if (!string.IsNullOrWhiteSpace(softwarePath))
        {
            var sw = softwarePath.Trim();
            list.Add(Path.Combine(sw, "PrintTemplates", "BarcodeFRX"));
            list.Add(Path.Combine(sw, "PrintTemplates", "BarcodePRN"));
            list.Add(Path.Combine(sw, "PrintTemplates"));
            list.Add(Path.Combine(sw, "BarcodeFRX"));
            list.Add(Path.Combine(sw, "BarcodePRN"));
            list.Add(Path.Combine(sw, "Printfrx"));
            list.Add(sw);
        }

        // 2. Production software executable folder defaults:
        list.Add(@"E:\TATTVA JEWELS26-27\net8.0-windows\PrintTemplates\BarcodeFRX");
        list.Add(@"E:\TATTVA JEWELS26-27\net8.0-windows\PrintTemplates\BarcodePRN");
        list.Add(@"E:\TATTVA JEWELS26-27\net8.0-windows\PrintTemplates");
        list.Add(@"E:\TATTVA JEWELS25-26\net8.0-windows\PrintTemplates\BarcodeFRX");
        list.Add(@"E:\TATTVA JEWELS25-26\net8.0-windows\PrintTemplates\BarcodePRN");
        list.Add(@"E:\TATTVA JEWELS25-26\net8.0-windows\PrintTemplates");
        list.Add(@"D:\TATTVA JEWELS26-27\net8.0-windows\PrintTemplates\BarcodeFRX");
        list.Add(@"D:\TATTVA JEWELS26-27\net8.0-windows\PrintTemplates\BarcodePRN");
        list.Add(@"D:\TATTVA JEWELS26-27\net8.0-windows\PrintTemplates");
        list.Add(@"C:\TATTVA JEWELS26-27\net8.0-windows\PrintTemplates\BarcodeFRX");
        list.Add(@"C:\TATTVA JEWELS26-27\net8.0-windows\PrintTemplates\BarcodePRN");
        list.Add(@"C:\Concept\Printfrx");
        list.Add(@"E:\BarcodeFRX");
        list.Add(@"E:\BarcodePRN");

        // 3. Local machine CommonApplicationData, BaseDirectory, and CurrentDirectory:
        var baseDir = AppContext.BaseDirectory;
        var currentDir = Directory.GetCurrentDirectory();
        var commonAppData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        list.Add(Path.Combine(commonAppData, "ConceptJewelleryPro", "PrintTemplates", "BarcodeFRX"));
        list.Add(Path.Combine(commonAppData, "ConceptJewelleryPro", "PrintTemplates", "BarcodePRN"));
        list.Add(Path.Combine(commonAppData, "ConceptJewelleryPro", "PrintTemplates"));
        list.Add(Path.Combine(commonAppData, "ConceptJewelleryPro", "BarcodeFRX"));
        list.Add(Path.Combine(commonAppData, "ConceptJewelleryPro", "BarcodePRN"));
        list.Add(Path.Combine(baseDir, "PrintTemplates", "BarcodeFRX"));
        list.Add(Path.Combine(baseDir, "PrintTemplates", "BarcodePRN"));
        list.Add(Path.Combine(baseDir, "PrintTemplates"));
        list.Add(Path.Combine(baseDir, "Printfrx"));
        list.Add(baseDir);
        list.Add(Path.Combine(currentDir, "PrintTemplates", "BarcodeFRX"));
        list.Add(Path.Combine(currentDir, "PrintTemplates", "BarcodePRN"));
        list.Add(Path.Combine(currentDir, "PrintTemplates"));
        list.Add(currentDir);
        list.Add(Path.Combine(baseDir, "..", "PrintTemplates", "BarcodeFRX"));
        list.Add(Path.Combine(baseDir, "..", "PrintTemplates", "BarcodePRN"));
        list.Add(Path.Combine(baseDir, "..", "PrintTemplates"));
        list.Add(Path.Combine(baseDir, "..", "..", "..", "PrintTemplates", "BarcodeFRX"));
        list.Add(Path.Combine(baseDir, "..", "..", "..", "PrintTemplates", "BarcodePRN"));
        list.Add(Path.Combine(baseDir, "..", "..", "..", "PrintTemplates"));
        list.Add(Path.Combine(baseDir, "..", "..", "..", "WPF", "PrintTemplates", "BarcodeFRX"));
        list.Add(Path.Combine(baseDir, "..", "..", "..", "WPF", "PrintTemplates", "BarcodePRN"));
        list.Add(Path.Combine(baseDir, "..", "..", "..", "WPF", "PrintTemplates"));
        list.Add(Path.Combine(baseDir, "..", "..", "..", "..", "WPF", "PrintTemplates", "BarcodeFRX"));
        list.Add(Path.Combine(baseDir, "..", "..", "..", "..", "WPF", "PrintTemplates", "BarcodePRN"));
        list.Add(Path.Combine(baseDir, "..", "..", "..", "..", "WPF", "PrintTemplates"));
        list.Add(Path.Combine(currentDir, "..", "WPF", "PrintTemplates", "BarcodeFRX"));
        list.Add(Path.Combine(currentDir, "..", "WPF", "PrintTemplates", "BarcodePRN"));
        list.Add(Path.Combine(currentDir, "..", "WPF", "PrintTemplates"));

        // Dynamically probe for any active TATTVA JEWELS software executable folders on available drives:
        try
        {
            foreach (var drive in new[] { @"E:\", @"D:\", @"C:\" })
            {
                if (!Directory.Exists(drive)) continue;
                foreach (var dir in Directory.GetDirectories(drive, "*TATTVA*"))
                {
                    var frx1 = Path.Combine(dir, "net8.0-windows", "PrintTemplates", "BarcodeFRX");
                    if (Directory.Exists(frx1)) list.Insert(0, frx1);
                    var prn1 = Path.Combine(dir, "net8.0-windows", "PrintTemplates", "BarcodePRN");
                    if (Directory.Exists(prn1)) list.Insert(0, prn1);
                    var prn2 = Path.Combine(dir, "net8.0-windows", "PrintTemplates");
                    if (Directory.Exists(prn2)) list.Insert(0, prn2);
                    var frx3 = Path.Combine(dir, "PrintTemplates", "BarcodeFRX");
                    if (Directory.Exists(frx3)) list.Insert(0, frx3);
                    var prn3 = Path.Combine(dir, "PrintTemplates", "BarcodePRN");
                    if (Directory.Exists(prn3)) list.Insert(0, prn3);
                }
            }
        }
        catch { }

        return list.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static bool TemplateExists(string formatName, string? softwarePath = null, string engine = "PRN")
    {
        if (string.IsNullOrWhiteSpace(formatName)) return false;

        var ext = string.Equals(engine, "FRX", StringComparison.OrdinalIgnoreCase) ? ".frx" : ".prn";
        var pattern = "*" + ext;

        foreach (var dir in GetCandidateDirectories(softwarePath))
        {
            if (!Directory.Exists(dir)) continue;
            if (File.Exists(Path.Combine(dir, $"{formatName}{ext}"))) return true;
            try
            {
                if (Directory.GetFiles(dir, pattern).Any(f => string.Equals(Path.GetFileNameWithoutExtension(f), formatName, StringComparison.OrdinalIgnoreCase)))
                    return true;
            }
            catch { }
        }

        return false;
    }

    private static string LoadTemplate(string formatName, string? bType = null, string? softwarePath = null)
    {
        var cleanFormat = Path.GetFileNameWithoutExtension(formatName);

        foreach (var dir in GetCandidateDirectories(softwarePath))
        {
            if (!Directory.Exists(dir)) continue;

            var exactPath = Path.Combine(dir, $"{cleanFormat}.prn");
            if (File.Exists(exactPath))
            {
                Console.WriteLine($"[BarcodePrintService] Loaded PRN template '{cleanFormat}' from file: {exactPath}");
                return File.ReadAllText(exactPath);
            }

            try
            {
                var matchedFile = Directory.GetFiles(dir, "*.prn")
                    .FirstOrDefault(f => string.Equals(Path.GetFileNameWithoutExtension(f), cleanFormat, StringComparison.OrdinalIgnoreCase));
                if (matchedFile != null && File.Exists(matchedFile))
                {
                    Console.WriteLine($"[BarcodePrintService] Loaded PRN template '{cleanFormat}' from matched file: {matchedFile}");
                    return File.ReadAllText(matchedFile);
                }
            }
            catch { }
        }

        // Respective fallback based on Type: StickerPC or StickerWt
        var fallbackName = string.Equals(bType, "PC", StringComparison.OrdinalIgnoreCase) ? "StickerPC" : "StickerWt";
        foreach (var dir in GetCandidateDirectories(softwarePath))
        {
            if (!Directory.Exists(dir)) continue;
            var fallbackPath = Path.Combine(dir, $"{fallbackName}.prn");
            if (File.Exists(fallbackPath))
            {
                Console.WriteLine($"[BarcodePrintService] Loaded type fallback PRN template '{fallbackName}' from file: {fallbackPath}");
                return File.ReadAllText(fallbackPath);
            }
        }

        Console.WriteLine($"[BarcodePrintService] WARNING: No PRN file found on disk for '{formatName}'. Using built-in fallback.");

        // Built-in fallback 55mm printable bat at 203 DPI
        if (string.Equals(bType, "PC", StringComparison.OrdinalIgnoreCase))
        {
            return @"^XA
^CI28
^PW440
^LL96
^LH0,0
^FO10,8^A0N,22,22^FB200,1,0,L,0^FD{{ITEM_NAME}} {{PURITY}}^FS
^FO10,34^A0N,24,24^FB200,1,0,L,0^FDMRP: {{MRP}}^FS
^FO10,66^A0N,18,18^FB200,1,0,L,0^FDSZ:{{SIZE}} Q:{{QTY}} PC^FS
^FO230,6^A0N,18,18^FB200,1,0,C,0^FD{{COMPANY_NAME}}^FS
^FO240,26^BY1,2,42^BCN,42,N,N,N^FD{{BARCODE}}^FS
^FO230,74^A0N,16,16^FB200,1,0,C,0^FD{{BARCODE}}^FS
^XZ";
        }

        return @"^XA
^CI28
^PW440
^LL96
^LH0,0
^FO10,6^A0N,20,20^FB200,1,0,L,0^FD{{ITEM_NAME}}^FS
^FO10,28^A0N,18,18^FB200,1,0,L,0^FDNt Wt: {{NET_WT}}g^FS
^FO10,50^A0N,16,16^FB200,1,0,L,0^FD{{PURITY}} {{DESCRIPTION}}^FS
^FO10,72^A0N,16,16^FB200,1,0,L,0^FDMC: {{MAKING}} {{MAKING_TYPE}}^FS
^FO230,6^A0N,18,18^FB200,1,0,C,0^FD{{COMPANY_NAME}}^FS
^FO240,26^BY1,2,42^BCN,42,N,N,N^FD{{BARCODE}}^FS
^FO230,74^A0N,16,16^FB200,1,0,C,0^FD{{BARCODE}}^FS
^XZ";
    }

    private static string LoadFrxTemplate(string formatName, string? bType = null, string? softwarePath = null)
    {
        var cleanFormat = Path.GetFileNameWithoutExtension(formatName);

        foreach (var dir in GetCandidateDirectories(softwarePath))
        {
            if (!Directory.Exists(dir)) continue;

            var exactPath = Path.Combine(dir, $"{cleanFormat}.frx");
            if (File.Exists(exactPath))
            {
                Console.WriteLine($"[BarcodePrintService] Loaded FRX template '{cleanFormat}' from file: {exactPath}");
                return File.ReadAllText(exactPath);
            }

            try
            {
                var matchedFile = Directory.GetFiles(dir, "*.frx")
                    .FirstOrDefault(f => string.Equals(Path.GetFileNameWithoutExtension(f), cleanFormat, StringComparison.OrdinalIgnoreCase));
                if (matchedFile != null && File.Exists(matchedFile))
                {
                    Console.WriteLine($"[BarcodePrintService] Loaded FRX template '{cleanFormat}' from matched file: {matchedFile}");
                    return File.ReadAllText(matchedFile);
                }
            }
            catch { }
        }

        // Respective fallback based on Type: StickerPC or StickerWt
        var fallbackName = string.Equals(bType, "PC", StringComparison.OrdinalIgnoreCase) ? "StickerPC" : "StickerWt";
        foreach (var dir in GetCandidateDirectories(softwarePath))
        {
            if (!Directory.Exists(dir)) continue;
            var fallbackPath = Path.Combine(dir, $"{fallbackName}.frx");
            if (File.Exists(fallbackPath))
            {
                Console.WriteLine($"[BarcodePrintService] Loaded type fallback FRX template '{fallbackName}' from file: {fallbackPath}");
                return File.ReadAllText(fallbackPath);
            }

            try
            {
                var matchedFallback = Directory.GetFiles(dir, "*.frx")
                    .FirstOrDefault(f => string.Equals(Path.GetFileNameWithoutExtension(f), fallbackName, StringComparison.OrdinalIgnoreCase));
                if (matchedFallback != null && File.Exists(matchedFallback))
                {
                    Console.WriteLine($"[BarcodePrintService] Loaded type fallback FRX template '{fallbackName}' from matched file: {matchedFallback}");
                    return File.ReadAllText(matchedFallback);
                }
            }
            catch { }
        }

        Console.WriteLine($"[BarcodePrintService] WARNING: No FRX file found on disk for '{formatName}'. Using built-in 81x12 FRX fallback.");
        return GetBuiltInFrxTemplate(bType);
    }

    private static string GetBuiltInFrxTemplate(string? bType)
    {
        if (string.Equals(bType, "PC", StringComparison.OrdinalIgnoreCase))
        {
            return """
<?xml version="1.0" encoding="utf-8"?>
<Report ScriptLanguage="CSharp" ReportInfo.Created="2026-11-17" ReportInfo.Modified="2026-11-17" ReportInfo.CreatorVersion="2027.1.0.0">
  <Dictionary>
    <BusinessObjectDataSource Name="Items" ReferenceName="Items" DataType="null" Enabled="true">
      <Column Name="Barcode" DataType="System.String"/>
      <Column Name="ItemName" DataType="System.String"/>
      <Column Name="Purity" DataType="System.String"/>
      <Column Name="MRP" DataType="System.String"/>
      <Column Name="Size" DataType="System.String"/>
      <Column Name="Qty" DataType="System.String"/>
      <Column Name="CompanyName" DataType="System.String"/>
    </BusinessObjectDataSource>
  </Dictionary>
  <ReportPage Name="Page1" PaperWidth="81" PaperHeight="12" LeftMargin="0" TopMargin="0" RightMargin="0" BottomMargin="0" FirstPageSource="15" OtherPagesSource="15">
    <DataBand Name="Data1" Width="306.18" Height="45.36" DataSource="Items">
      <TextObject Name="txtItemPurity" Left="4.72" Top="3.78" Width="94.5" Height="11.34" Padding="0,0,0,0" Text="[Items.ItemName] [Items.Purity]" Font="Arial, 6.5pt, style=Bold" HorzAlign="Left" VertAlign="Center" WordWrap="false"/>
      <TextObject Name="txtMrp" Left="4.72" Top="16.06" Width="94.5" Height="13.23" Padding="0,0,0,0" Text="MRP: [Items.MRP]" Font="Arial, 7.5pt, style=Bold" HorzAlign="Left" VertAlign="Center" WordWrap="false"/>
      <TextObject Name="txtSizeQty" Left="4.72" Top="31.18" Width="94.5" Height="11.34" Padding="0,0,0,0" Text="SZ:[Items.Size] Q:[Items.Qty] PC" Font="Arial, 6pt, style=Bold" HorzAlign="Left" VertAlign="Center" WordWrap="false"/>
      <TextObject Name="txtCompany" Left="108.68" Top="2.84" Width="94.5" Height="9.45" Padding="0,0,0,0" Text="[Items.CompanyName]" Font="Arial, 6pt, style=Bold" HorzAlign="Center" VertAlign="Center" WordWrap="false"/>
      <BarcodeObject Name="Barcode1" Left="113.4" Top="12.28" Width="85.05" Height="19.85" AutoSize="false" DataColumn="Items.Barcode" Expression="[Items.Barcode]" Text="000001" ShowText="false" Barcode="Code128" Barcode.AutoEncode="true"/>
      <TextObject Name="txtBarcode" Left="108.68" Top="34.02" Width="94.5" Height="9.45" Padding="0,0,0,0" Text="[Items.Barcode]" Font="Arial, 6pt, style=Bold" HorzAlign="Center" VertAlign="Center" WordWrap="false"/>
    </DataBand>
  </ReportPage>
</Report>
""";
        }

        return """
<?xml version="1.0" encoding="utf-8"?>
<Report ScriptLanguage="CSharp" ReportInfo.Created="2026-11-17" ReportInfo.Modified="2026-11-17" ReportInfo.CreatorVersion="2027.1.0.0">
  <Dictionary>
    <BusinessObjectDataSource Name="Items" ReferenceName="Items" DataType="null" Enabled="true">
      <Column Name="Barcode" DataType="System.String"/>
      <Column Name="ItemName" DataType="System.String"/>
      <Column Name="NetWt" DataType="System.String"/>
      <Column Name="Purity" DataType="System.String"/>
      <Column Name="Description" DataType="System.String"/>
      <Column Name="Making" DataType="System.String"/>
      <Column Name="MakingType" DataType="System.String"/>
      <Column Name="CompanyName" DataType="System.String"/>
    </BusinessObjectDataSource>
  </Dictionary>
  <ReportPage Name="Page1" PaperWidth="81" PaperHeight="12" LeftMargin="0" TopMargin="0" RightMargin="0" BottomMargin="0" FirstPageSource="15" OtherPagesSource="15">
    <DataBand Name="Data1" Width="306.18" Height="45.36" DataSource="Items">
      <TextObject Name="txtItemName" Left="4.72" Top="2.84" Width="94.5" Height="10.4" Padding="0,0,0,0" Text="[Items.ItemName]" Font="Arial, 6.5pt, style=Bold" HorzAlign="Left" VertAlign="Center" WordWrap="false"/>
      <TextObject Name="txtNetWt" Left="4.72" Top="13.23" Width="94.5" Height="10.4" Padding="0,0,0,0" Text="Nt Wt: [Items.NetWt]g" Font="Arial, 6pt, style=Bold" HorzAlign="Left" VertAlign="Center" WordWrap="false"/>
      <TextObject Name="txtPurityDesc" Left="4.72" Top="23.62" Width="94.5" Height="10.4" Padding="0,0,0,0" Text="[Items.Purity] [Items.Description]" Font="Arial, 5.5pt, style=Bold" HorzAlign="Left" VertAlign="Center" WordWrap="false"/>
      <TextObject Name="txtMaking" Left="4.72" Top="34.02" Width="94.5" Height="10.4" Padding="0,0,0,0" Text="MC: [Items.Making] [Items.MakingType]" Font="Arial, 5.5pt, style=Bold" HorzAlign="Left" VertAlign="Center" WordWrap="false"/>
      <TextObject Name="txtCompany" Left="108.68" Top="2.84" Width="94.5" Height="9.45" Padding="0,0,0,0" Text="[Items.CompanyName]" Font="Arial, 6pt, style=Bold" HorzAlign="Center" VertAlign="Center" WordWrap="false"/>
      <BarcodeObject Name="Barcode1" Left="113.4" Top="12.28" Width="85.05" Height="19.85" AutoSize="false" DataColumn="Items.Barcode" Expression="[Items.Barcode]" Text="000001" ShowText="false" Barcode="Code128" Barcode.AutoEncode="true"/>
      <TextObject Name="txtBarcode" Left="108.68" Top="34.02" Width="94.5" Height="9.45" Padding="0,0,0,0" Text="[Items.Barcode]" Font="Arial, 6pt, style=Bold" HorzAlign="Center" VertAlign="Center" WordWrap="false"/>
    </DataBand>
  </ReportPage>
</Report>
""";
    }

    private sealed record PreparedFrxSticker(
        IReadOnlyList<byte[]> PageBytesList,
        int PaperWidthHundredths,
        int PaperHeightHundredths,
        bool IsLandscape,
        int TargetDpi,
        bool MonochromeThermal);

    private static PreparedFrxSticker PrepareFrxSticker(string printerName, string frxXml, string formatName, BarcodeSubformItem item)
    {
        var populatedXml = frxXml.Contains("{{", StringComparison.Ordinal)
            ? PopulateTemplate(frxXml, item, xmlEscape: true)
            : frxXml;

        var frxItem = BarcodeFrxPrintItem.FromSubformItem(item);
        var itemsList = new List<BarcodeFrxPrintItem> { frxItem };

        using var report = new Report();
        using (var xmlStream = new MemoryStream(Encoding.UTF8.GetBytes(populatedXml)))
        {
            report.Load(xmlStream);
        }

        report.RegisterData(itemsList, "Items");
        report.Prepare();

        if (report.PreparedPages.Count == 0)
        {
            throw new InvalidOperationException($"No pages were generated from the FastReport template '{formatName}.frx'.");
        }

        float reportWidthMm = 81f;
        float reportHeightMm = 12f;
        bool isLandscape = false;
        if (report.Pages.Count > 0 && report.Pages[0] is ReportPage reportPage)
        {
            if (reportPage.PaperWidth > 0)
                reportWidthMm = reportPage.PaperWidth;
            if (reportPage.PaperHeight > 0)
                reportHeightMm = reportPage.PaperHeight;
            isLandscape = reportPage.Landscape;
        }

        int paperWidthHundredths = Math.Max(1, (int)Math.Round((reportWidthMm / 25.4f) * 100f));
        int paperHeightHundredths = Math.Max(1, (int)Math.Round((reportHeightMm / 25.4f) * 100f));

        using var printDoc = new PrintDocument();
        printDoc.PrinterSettings.PrinterName = printerName;
        ConfigurePageSettings(printDoc, paperWidthHundredths, paperHeightHundredths, isLandscape);

        int targetDpi = ResolvePrinterDpi(printDoc);
        bool monochromeThermal = !TryPrinterSupportsColor(printDoc.PrinterSettings);

        var pageBytesList = ExportReportPages(report, targetDpi, snapMonochrome: true);
        if (pageBytesList.Count == 0)
        {
            throw new InvalidOperationException($"No page images were generated from the FastReport template '{formatName}.frx'.");
        }

        return new PreparedFrxSticker(
            pageBytesList,
            paperWidthHundredths,
            paperHeightHundredths,
            isLandscape,
            targetDpi,
            monochromeThermal);
    }

    private static void SendPreparedFrxToPrinter(string printerName, string formatName, BarcodeSubformItem item, PreparedFrxSticker prepared)
    {
        // For thermal barcode printers (Godex, Zebra, ZDesigner, TSC, etc.), send the rendered .frx bitmap
        // as a native ZPL ^GFA graphic command via RawPrinterHelper (RAW spooler bypass).
        // This avoids Windows Point-and-Print GDI Server-Side Rendering drops on shared network thermal printers.
        if (IsThermalLabelPrinter(printerName, prepared.MonochromeThermal))
        {
            var zplBuilder = new StringBuilder();
            foreach (var pagePng in prepared.PageBytesList)
            {
                zplBuilder.AppendLine(ConvertMonochromePngToZplGfa(pagePng));
            }

            RawPrinterHelper.SendStringToPrinter(
                printerName,
                zplBuilder.ToString(),
                $"Sticker-{item.Barcode} ({formatName}.frx)");
            return;
        }

        using var printDoc = new PrintDocument();
        printDoc.PrinterSettings.PrinterName = printerName;
        printDoc.PrintController = new StandardPrintController();
        printDoc.DocumentName = $"Sticker-{item.Barcode} ({formatName}.frx)";

        ConfigurePageSettings(printDoc, prepared.PaperWidthHundredths, prepared.PaperHeightHundredths, prepared.IsLandscape);

        int currentPageIndex = 0;
        printDoc.PrintPage += (_, e) =>
        {
            if (currentPageIndex < prepared.PageBytesList.Count)
            {
                using var ms = new MemoryStream(prepared.PageBytesList[currentPageIndex]);
                using var img = Image.FromStream(ms);

                DrawBarcodePageImage(e, img, prepared.TargetDpi);
                currentPageIndex++;
                e.HasMorePages = currentPageIndex < prepared.PageBytesList.Count;
            }
            else
            {
                e.HasMorePages = false;
            }
        };

        printDoc.Print();
    }

    private static bool IsThermalLabelPrinter(string printerName, bool isMonochrome)
    {
        if (string.IsNullOrWhiteSpace(printerName))
            return false;

        var nonThermalKeywords = new[] { "PDF", "OneNote", "XPS", "Fax", "HP ", "Smart Tank", "LaserJet", "DeskJet", "OfficeJet", "Canon", "Epson", "Brother" };
        if (nonThermalKeywords.Any(kw => printerName.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0))
            return false;

        var thermalKeywords = new[] { "Godex", "EZ520", "ZDesigner", "ZD230", "Zebra", "TSC", "Barcode", "Label", "Thermal", "TVS", "SNBC", "Citizen", "Bixolon", "Honeywell", "Sato" };
        if (thermalKeywords.Any(kw => printerName.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0))
            return true;

        return isMonochrome;
    }

    public static string ConvertMonochromePngToZplGfa(byte[] pngBytes)
    {
        using var ms = new MemoryStream(pngBytes);
        using var bmp = new Bitmap(ms);

        int width = bmp.Width;
        int height = bmp.Height;
        int bytesPerRow = (width + 7) / 8;
        int totalBytes = bytesPerRow * height;

        var rect = new Rectangle(0, 0, width, height);
        int pixelCount = width * height;
        var pixels = new int[pixelCount];

        var bmpData = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            Marshal.Copy(bmpData.Scan0, pixels, 0, pixelCount);
        }
        finally
        {
            bmp.UnlockBits(bmpData);
        }

        var hex = new StringBuilder(totalBytes * 2);
        for (int y = 0; y < height; y++)
        {
            int rowOffset = y * width;
            for (int byteIdx = 0; byteIdx < bytesPerRow; byteIdx++)
            {
                byte b = 0;
                int baseX = byteIdx * 8;
                for (int bit = 0; bit < 8; bit++)
                {
                    int x = baseX + bit;
                    if (x < width)
                    {
                        int argb = pixels[rowOffset + x];
                        int a = (argb >> 24) & 0xFF;
                        int r = (argb >> 16) & 0xFF;
                        int g = (argb >> 8) & 0xFF;
                        int bl = argb & 0xFF;

                        if (a >= 128 && (r * 299 + g * 587 + bl * 114) / 1000 < 160)
                        {
                            b |= (byte)(1 << (7 - bit));
                        }
                    }
                }
                hex.Append(b.ToString("X2"));
            }
        }

        return $"^XA\r\n^CI28\r\n^PW{width}\r\n^LL{height}\r\n^LH0,0\r\n^FO0,0^GFA,{totalBytes},{totalBytes},{bytesPerRow},{hex}^FS\r\n^XZ";
    }

    private static bool TryPrinterSupportsColor(PrinterSettings settings)
    {
        try
        {
            return settings.SupportsColor;
        }
        catch
        {
            return false;
        }
    }

    private static IReadOnlyList<byte[]> ExportReportPages(Report report, int dpi, bool snapMonochrome = false)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ConceptJewelleryPro_FR_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var baseFilePath = Path.Combine(tempDir, "label.png");
            var export = new ImageExport
            {
                ImageFormat = ImageExportFormat.Png,
                Resolution = dpi,
                SeparateFiles = true,
                PageRange = FrPageRange.All
            };

            report.Export(export, baseFilePath);

            var files = Directory.GetFiles(tempDir, "*.png");
            if (files.Length == 0)
                throw new InvalidOperationException("FastReport did not generate any page images.");

            var orderedFiles = files
                .OrderBy(ExtractPageIndex)
                .ToList();

            var result = new List<byte[]>(orderedFiles.Count);
            foreach (var file in orderedFiles)
            {
                var bytes = File.ReadAllBytes(file);
                result.Add(snapMonochrome ? SnapPngToMonochrome(bytes, dpi) : bytes);
            }

            return result;
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    private static int ExtractPageIndex(string filePath)
    {
        var name = Path.GetFileNameWithoutExtension(filePath);
        var match = Regex.Match(name, @"\d+$");
        if (match.Success && int.TryParse(match.Value, out int idx))
            return idx;
        return 0;
    }

    private static void ConfigurePageSettings(
        PrintDocument printDoc,
        int paperWidthHundredths,
        int paperHeightHundredths,
        bool isLandscape)
    {
        PaperSize? targetPaperSize = null;
        PaperSize? userSize = null;
        try
        {
            var sizes = printDoc.PrinterSettings.PaperSizes.Cast<PaperSize>().ToList();

            // 1. Exact dimension match (within 1mm = 4 hundredths of an inch)
            targetPaperSize = sizes.FirstOrDefault(ps =>
                Math.Abs(ps.Width - paperWidthHundredths) <= 4 &&
                Math.Abs(ps.Height - paperHeightHundredths) <= 4);

            // 2. Match custom label stock where width includes 2-4mm backing liner (e.g. Godex 'Jewellery' Width=329, Height=47 for 81x12mm)
            if (targetPaperSize == null)
            {
                targetPaperSize = sizes
                    .Where(ps => Math.Abs(ps.Height - paperHeightHundredths) <= 6 &&
                                 Math.Abs(ps.Width - paperWidthHundredths) <= 20)
                    .OrderBy(ps => string.Equals(ps.PaperName, "Jewellery", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                    .ThenBy(ps => Math.Abs(ps.Height - paperHeightHundredths) + Math.Abs(ps.Width - paperWidthHundredths))
                    .FirstOrDefault();
            }

            userSize = sizes.FirstOrDefault(ps =>
                ps.PaperName.Equals("USER", StringComparison.OrdinalIgnoreCase) || ps.RawKind == 256);
        }
        catch { }

        if (targetPaperSize == null)
        {
            targetPaperSize = userSize != null
                ? new PaperSize(userSize.PaperName, paperWidthHundredths, paperHeightHundredths) { RawKind = userSize.RawKind }
                : new PaperSize("BarcodeSticker81x12", paperWidthHundredths, paperHeightHundredths);
        }

        printDoc.DefaultPageSettings.PaperSize = targetPaperSize;
        printDoc.PrinterSettings.DefaultPageSettings.PaperSize = targetPaperSize;
        printDoc.DefaultPageSettings.Landscape = isLandscape;
        printDoc.PrinterSettings.DefaultPageSettings.Landscape = isLandscape;
        printDoc.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
        printDoc.OriginAtMargins = false;
    }

    private static int ResolvePrinterDpi(PrintDocument printDoc)
    {
        try
        {
            using var measureGraphics = printDoc.PrinterSettings.CreateMeasurementGraphics(printDoc.DefaultPageSettings);
            var measuredDpi = (int)Math.Round(Math.Max(measureGraphics.DpiX, measureGraphics.DpiY));
            if (measuredDpi is >= 96 and <= 1200)
                return measuredDpi;
        }
        catch
        {
        }

        var resolution = printDoc.DefaultPageSettings.PrinterResolution;
        if (resolution.X is >= 96 and <= 1200)
            return resolution.X;
        if (resolution.Y is >= 96 and <= 1200)
            return resolution.Y;

        return 203;
    }

    private static void DrawBarcodePageImage(PrintPageEventArgs e, Image img, int sourceDpi)
    {
        var g = e.Graphics!;
        float targetDpiX = g.DpiX > 0 ? g.DpiX : sourceDpi;
        float targetDpiY = g.DpiY > 0 ? g.DpiY : sourceDpi;

        g.PageUnit = GraphicsUnit.Pixel;
        g.SmoothingMode = SmoothingMode.None;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.None;

        float hardMarginXPx = e.PageSettings.HardMarginX / 100f * targetDpiX;
        float hardMarginYPx = e.PageSettings.HardMarginY / 100f * targetDpiY;
        float pageWidthPx = e.PageBounds.Width / 100f * targetDpiX;
        float pageHeightPx = e.PageBounds.Height / 100f * targetDpiY;

        float expectedWidthPx = img.Width * (targetDpiX / Math.Max(1f, sourceDpi));
        float expectedHeightPx = img.Height * (targetDpiY / Math.Max(1f, sourceDpi));

        float fitScale = 1f;
        if (pageWidthPx > 0 && pageHeightPx > 0
            && (expectedWidthPx > pageWidthPx + 2f || expectedHeightPx > pageHeightPx + 2f))
        {
            fitScale = Math.Min(pageWidthPx / expectedWidthPx, pageHeightPx / expectedHeightPx);
        }

        // If the printer paper size includes the backing liner width (e.g. 329 vs 319 hundredths of an inch),
        // e.Graphics origin (0,0) at HardMarginX already aligns with the left edge of the physical sticker.
        bool paperIncludesLinerMargin = (pageWidthPx - expectedWidthPx) >= (hardMarginXPx * 1.5f);
        float destX = paperIncludesLinerMargin ? 0f : -MathF.Round(hardMarginXPx);
        float destY = -MathF.Round(hardMarginYPx);
        float destWidth = MathF.Round(expectedWidthPx * fitScale);
        float destHeight = MathF.Round(expectedHeightPx * fitScale);

        g.DrawImage(
            img,
            new RectangleF(destX, destY, destWidth, destHeight),
            new RectangleF(0, 0, img.Width, img.Height),
            GraphicsUnit.Pixel);
    }

    private static byte[] SnapPngToMonochrome(byte[] pngBytes, int dpi)
    {
        using var inputStream = new MemoryStream(pngBytes);
        using var srcBitmap = new Bitmap(inputStream);
        int width = srcBitmap.Width;
        int height = srcBitmap.Height;
        var rect = new Rectangle(0, 0, width, height);

        int pixelCount = width * height;
        var pixels = new int[pixelCount];

        var srcData = srcBitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            System.Runtime.InteropServices.Marshal.Copy(srcData.Scan0, pixels, 0, pixelCount);
        }
        finally
        {
            srcBitmap.UnlockBits(srcData);
        }

        const int blackArgb = unchecked((int)0xFF000000);
        const int whiteArgb = unchecked((int)0xFFFFFFFF);

        for (int i = 0; i < pixels.Length; i++)
        {
            int argb = pixels[i];
            int a = (argb >> 24) & 0xFF;
            int r = (argb >> 16) & 0xFF;
            int g = (argb >> 8) & 0xFF;
            int b = argb & 0xFF;

            if (a < 255)
            {
                int invA = 255 - a;
                r = (r * a + 255 * invA) / 255;
                g = (g * a + 255 * invA) / 255;
                b = (b * a + 255 * invA) / 255;
            }

            int luminance = (r * 299 + g * 587 + b * 114) / 1000;
            pixels[i] = luminance < 160 ? blackArgb : whiteArgb;
        }

        using var destBitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        destBitmap.SetResolution(dpi, dpi);

        var destData = destBitmap.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            System.Runtime.InteropServices.Marshal.Copy(pixels, 0, destData.Scan0, pixelCount);
        }
        finally
        {
            destBitmap.UnlockBits(destData);
        }

        using var outputStream = new MemoryStream();
        destBitmap.Save(outputStream, ImageFormat.Png);
        return outputStream.ToArray();
    }

    private static string PopulateTemplate(string template, BarcodeSubformItem item, bool xmlEscape = false)
    {
        var result = template;

        string barcode = item.Barcode?.Trim() ?? string.Empty;
        string barcode6 = int.TryParse(barcode, out var bInt) ? bInt.ToString("D6") : barcode;
        string size = string.IsNullOrWhiteSpace(item.Size) || item.Size == "-" ? string.Empty : item.Size.Trim();
        string makingType = string.IsNullOrWhiteSpace(item.MakingType) ? "GRAM" : item.MakingType.Trim();
        string companyName = "TATTVA JEWELS";

        string Esc(string val) => xmlEscape ? (SecurityElement.Escape(val) ?? string.Empty) : val;

        string Rep(string text, string key, string val) =>
            text.Replace("{{" + key + "}}", Esc(val), StringComparison.OrdinalIgnoreCase);

        // Core Barcode & Identification
        result = Rep(result, "BARCODE", barcode);
        result = Rep(result, "BARCODE_6DIGIT", barcode6);
        result = Rep(result, "BARCODE_AUTONO", item.BarcodeAutoNo.ToString());
        result = Rep(result, "NOS", item.Nos.ToString());
        result = Rep(result, "VOU_NO", item.VouNo ?? string.Empty);

        // Product Details
        result = Rep(result, "ITEM_NAME", item.ItemName ?? string.Empty);
        result = Rep(result, "ITEM", item.ItemName ?? string.Empty);
        result = Rep(result, "ITEM_CODE", item.ItemCode ?? string.Empty);
        result = Rep(result, "PURITY", item.Purity ?? string.Empty);
        result = Rep(result, "HUID", item.HUID ?? string.Empty);
        result = Rep(result, "DESCRIPTION", item.Description ?? string.Empty);
        result = Rep(result, "DESC", item.Description ?? string.Empty);
        result = Rep(result, "SIZE", size);
        result = Rep(result, "QTY", item.Qty.ToString("N0"));
        result = Rep(result, "TYPE", item.BType ?? "WT");
        result = Rep(result, "BTYPE", item.BType ?? "WT");
        result = Rep(result, "TYPE_PC_WT", item.BType ?? "WT");

        // Weights & Coded Weights
        long netWtMg = (long)Math.Round(item.NetWt * 1000m);
        string codedNetWt = $"A{netWtMg}";

        result = Rep(result, "GROSS_WT", item.GrossWt.ToString("F3"));
        result = Rep(result, "GROSS_WEIGHT", item.GrossWt.ToString("F3"));
        result = Rep(result, "GW", item.GrossWt.ToString("F3"));
        result = Rep(result, "STONE_WT", item.StoneWt.ToString("F3"));
        result = Rep(result, "LESS_WT", item.StoneWt.ToString("F3"));
        result = Rep(result, "LW", item.StoneWt.ToString("F3"));
        result = Rep(result, "NET_WT", item.NetWt.ToString("F3"));
        result = Rep(result, "NET_WEIGHT", item.NetWt.ToString("F3"));
        result = Rep(result, "NW", codedNetWt);
        result = Rep(result, "CODED_NW", codedNetWt);
        result = Rep(result, "CODED_NET_WT", codedNetWt);

        // Charges & Making (MC sent rounded up without decimals)
        long makingRounded = (long)Math.Ceiling(item.Making);
        string makingRoundedStr = makingRounded.ToString();
        result = Rep(result, "MAKING", makingRoundedStr);
        result = Rep(result, "MC", makingRoundedStr);
        result = Rep(result, "MAKING_ROUND", makingRoundedStr);
        result = Rep(result, "MC_ROUND", makingRoundedStr);
        result = Rep(result, "MAKING_RAW", item.Making.ToString("F2"));
        result = Rep(result, "MC_RAW", item.Making.ToString("F2"));
        result = Rep(result, "MAKING_TYPE", makingType);
        result = Rep(result, "MC_TYPE", makingType);
        result = Rep(result, "M_PER_GR", item.MPerGr.ToString("F2"));
        result = Rep(result, "STONE_VALUE", item.StoneValue.ToString("N0"));
        result = Rep(result, "ST_VAL", item.StoneValue.ToString("N0"));

        // Cost & Pricing Details (Cost MC sent rounded up without decimals)
        long costMakingRounded = (long)Math.Ceiling(item.CostMaking);
        string costMakingRoundedStr = costMakingRounded.ToString();
        result = Rep(result, "COST_TOUNCH", item.CostTounch.ToString("F2"));
        result = Rep(result, "TOUNCH", item.CostTounch.ToString("F2"));
        result = Rep(result, "TOUCH", item.CostTounch.ToString("F2"));
        result = Rep(result, "COST_PG", item.CostPg.ToString("F2"));
        result = Rep(result, "COST_PER_GRAM", item.CostPg.ToString("F2"));
        result = Rep(result, "COST_MAKING", costMakingRoundedStr);
        result = Rep(result, "COST_MC", costMakingRoundedStr);
        result = Rep(result, "COST_MAKING_ROUND", costMakingRoundedStr);
        result = Rep(result, "COST_MC_ROUND", costMakingRoundedStr);
        result = Rep(result, "COST_MAKING_RAW", item.CostMaking.ToString("F2"));
        result = Rep(result, "COST_MC_RAW", item.CostMaking.ToString("F2"));
        result = Rep(result, "COST_PRICE", item.CostPrice.ToString("F2"));
        result = Rep(result, "CP", item.CostPrice.ToString("F2"));
        result = Rep(result, "COST_RATE", item.CostRate.ToString("F2"));
        result = Rep(result, "MRP", item.MRP.ToString("N0"));
        result = Rep(result, "PRICE", item.MRP.ToString("N0"));
        result = Rep(result, "PROFIT", item.Profit.ToString("F2"));
        result = Rep(result, "MULTIPLIER", item.Profit.ToString("F2"));
        result = Rep(result, "MARGIN_MULTIPLIER", item.Profit.ToString("F2"));

        // Supplier & Invoices (SupplierName sent via PartyCode on barcode stickers)
        string partyCode = !string.IsNullOrWhiteSpace(item.PartyCode)
            ? item.PartyCode.Trim()
            : (string.IsNullOrWhiteSpace(item.SupplierName) ? "OPN" : item.SupplierName.Trim());

        result = Rep(result, "SUPPLIER", partyCode);
        result = Rep(result, "SUPPLIER_NAME", partyCode);
        result = Rep(result, "PARTY_CODE", partyCode);
        result = Rep(result, "PARTY", partyCode);
        result = Rep(result, "SUPPLIER_FULL_NAME", item.SupplierName ?? string.Empty);
        result = Rep(result, "SUPP_INV_NO", item.SuppInvNo ?? string.Empty);
        result = Rep(result, "INVOICE_NO", item.SuppInvNo ?? string.Empty);
        result = Rep(result, "SUPP_DATE", item.SuppDate?.ToString("dd/MM/yyyy") ?? string.Empty);
        result = Rep(result, "INVOICE_DATE", item.SuppDate?.ToString("dd/MM/yyyy") ?? string.Empty);
        result = Rep(result, "P_DATE", item.PDate.ToString("dd/MM/yyyy"));
        result = Rep(result, "PURCHASE_DATE", item.PDate.ToString("dd/MM/yyyy"));
        result = Rep(result, "PUNCHED_ON", item.PunchedOn.ToString("dd/MM/yyyy HH:mm"));
        result = Rep(result, "DATE", DateTime.Today.ToString("dd/MM/yyyy"));

        // Storage & Metadata
        result = Rep(result, "BOX_NO", item.BoxNo ?? string.Empty);
        result = Rep(result, "BOX", item.BoxNo ?? string.Empty);
        result = Rep(result, "GODOWN", item.Godown ?? string.Empty);
        result = Rep(result, "BRAND", item.Brand ?? string.Empty);
        result = Rep(result, "REMARKS", item.Remarks1 ?? string.Empty);
        result = Rep(result, "NOTE", item.Note ?? string.Empty);
        result = Rep(result, "STATUS", item.Status ?? string.Empty);
        result = Rep(result, "ENTRY_USER", item.EntryUser ?? string.Empty);
        result = Rep(result, "STICKER_FORMAT", item.Sti ?? string.Empty);
        result = Rep(result, "STI", item.Sti ?? string.Empty);

        // Store / Firm Name
        result = Rep(result, "COMPANY_NAME", companyName);
        result = Rep(result, "STORE_NAME", companyName);
        result = Rep(result, "FIRM_NAME", companyName);

        return result;
    }

    public sealed class BarcodeStickerPreviewData
    {
        public string Barcode { get; init; } = string.Empty;
        public string ItemName { get; init; } = string.Empty;
        public string FormatName { get; init; } = string.Empty;
        public string PrintEngine { get; init; } = "PRN";
        public string PrinterName { get; init; } = string.Empty;
        public byte[]? PreviewImageBytes { get; init; }
        public string? RawPrnCommand { get; init; }
        public BarcodeSubformItem Item { get; init; } = new();
    }

    public sealed class BarcodeFrxPrintItem
    {
        public string Barcode { get; set; } = string.Empty;
        public string Barcode6Digit { get; set; } = string.Empty;
        public int BarcodeAutoNo { get; set; }
        public int Nos { get; set; }
        public string VouNo { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string Item { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public string Purity { get; set; } = string.Empty;
        public string HUID { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Desc { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
        public string Qty { get; set; } = "1";
        public string Type { get; set; } = "WT";
        public string BType { get; set; } = "WT";
        public string GrossWt { get; set; } = "0.000";
        public string GW { get; set; } = "0.000";
        public string StoneWt { get; set; } = "0.000";
        public string LessWt { get; set; } = "0.000";
        public string LW { get; set; } = "0.000";
        public string NetWt { get; set; } = "0.000";
        public string NW { get; set; } = "A0";
        public string CodedNw { get; set; } = "A0";
        public string CodedNetWt { get; set; } = "A0";
        public string Making { get; set; } = "0";
        public string MC { get; set; } = "0";
        public string MakingRaw { get; set; } = "0.00";
        public string MakingType { get; set; } = "GRAM";
        public string McType { get; set; } = "GRAM";
        public string MPerGr { get; set; } = "0.00";
        public string StoneValue { get; set; } = "0";
        public string CostTounch { get; set; } = "0.00";
        public string CostPg { get; set; } = "0.00";
        public string CostMaking { get; set; } = "0";
        public string CostMc { get; set; } = "0";
        public string CostMakingRaw { get; set; } = "0.00";
        public string CostPrice { get; set; } = "0.00";
        public string CP { get; set; } = "0.00";
        public string CostRate { get; set; } = "0.00";
        public string MRP { get; set; } = "0";
        public string Price { get; set; } = "0";
        public string Profit { get; set; } = "0.00";
        public string Supplier { get; set; } = "OPN";
        public string SupplierName { get; set; } = "OPN";
        public string PartyCode { get; set; } = "OPN";
        public string SupplierFullName { get; set; } = string.Empty;
        public string SuppInvNo { get; set; } = string.Empty;
        public string SuppDate { get; set; } = string.Empty;
        public string PDate { get; set; } = string.Empty;
        public string PunchedOn { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string BoxNo { get; set; } = string.Empty;
        public string Godown { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string EntryUser { get; set; } = string.Empty;
        public string StickerFormat { get; set; } = string.Empty;
        public string CompanyName { get; set; } = "TATTVA JEWELS";
        public string StoreName { get; set; } = "TATTVA JEWELS";
        public string FirmName { get; set; } = "TATTVA JEWELS";

        public static BarcodeFrxPrintItem FromSubformItem(BarcodeSubformItem item)
        {
            string barcode = item.Barcode?.Trim() ?? string.Empty;
            string barcode6 = int.TryParse(barcode, out var bInt) ? bInt.ToString("D6") : barcode;
            string size = string.IsNullOrWhiteSpace(item.Size) || item.Size == "-" ? string.Empty : item.Size.Trim();
            string makingType = string.IsNullOrWhiteSpace(item.MakingType) ? "GRAM" : item.MakingType.Trim();
            long netWtMg = (long)Math.Round(item.NetWt * 1000m);
            string codedNetWt = $"A{netWtMg}";
            string makingRoundedStr = ((long)Math.Ceiling(item.Making)).ToString();
            string costMakingRoundedStr = ((long)Math.Ceiling(item.CostMaking)).ToString();
            string partyCode = !string.IsNullOrWhiteSpace(item.PartyCode)
                ? item.PartyCode.Trim()
                : (string.IsNullOrWhiteSpace(item.SupplierName) ? "OPN" : item.SupplierName.Trim());

            return new BarcodeFrxPrintItem
            {
                Barcode = barcode,
                Barcode6Digit = barcode6,
                BarcodeAutoNo = item.BarcodeAutoNo,
                Nos = item.Nos,
                VouNo = item.VouNo ?? string.Empty,
                ItemName = item.ItemName ?? string.Empty,
                Item = item.ItemName ?? string.Empty,
                ItemCode = item.ItemCode ?? string.Empty,
                Purity = item.Purity ?? string.Empty,
                HUID = item.HUID ?? string.Empty,
                Description = item.Description ?? string.Empty,
                Desc = item.Description ?? string.Empty,
                Size = size,
                Qty = item.Qty.ToString("N0"),
                Type = item.BType ?? "WT",
                BType = item.BType ?? "WT",
                GrossWt = item.GrossWt.ToString("F3"),
                GW = item.GrossWt.ToString("F3"),
                StoneWt = item.StoneWt.ToString("F3"),
                LessWt = item.StoneWt.ToString("F3"),
                LW = item.StoneWt.ToString("F3"),
                NetWt = item.NetWt.ToString("F3"),
                NW = codedNetWt,
                CodedNw = codedNetWt,
                CodedNetWt = codedNetWt,
                Making = makingRoundedStr,
                MC = makingRoundedStr,
                MakingRaw = item.Making.ToString("F2"),
                MakingType = makingType,
                McType = makingType,
                MPerGr = item.MPerGr.ToString("F2"),
                StoneValue = item.StoneValue.ToString("N0"),
                CostTounch = item.CostTounch.ToString("F2"),
                CostPg = item.CostPg.ToString("F2"),
                CostMaking = costMakingRoundedStr,
                CostMc = costMakingRoundedStr,
                CostMakingRaw = item.CostMaking.ToString("F2"),
                CostPrice = item.CostPrice.ToString("F2"),
                CP = item.CostPrice.ToString("F2"),
                CostRate = item.CostRate.ToString("F2"),
                MRP = item.MRP.ToString("N0"),
                Price = item.MRP.ToString("N0"),
                Profit = item.Profit.ToString("F2"),
                Supplier = partyCode,
                SupplierName = partyCode,
                PartyCode = partyCode,
                SupplierFullName = item.SupplierName ?? string.Empty,
                SuppInvNo = item.SuppInvNo ?? string.Empty,
                SuppDate = item.SuppDate?.ToString("dd/MM/yyyy") ?? string.Empty,
                PDate = item.PDate.ToString("dd/MM/yyyy"),
                PunchedOn = item.PunchedOn.ToString("dd/MM/yyyy HH:mm"),
                Date = DateTime.Today.ToString("dd/MM/yyyy"),
                BoxNo = item.BoxNo ?? string.Empty,
                Godown = item.Godown ?? string.Empty,
                Brand = item.Brand ?? string.Empty,
                Remarks = item.Remarks1 ?? string.Empty,
                Note = item.Note ?? string.Empty,
                Status = item.Status ?? string.Empty,
                EntryUser = item.EntryUser ?? string.Empty,
                StickerFormat = item.Sti ?? string.Empty,
                CompanyName = "TATTVA JEWELS",
                StoreName = "TATTVA JEWELS",
                FirmName = "TATTVA JEWELS"
            };
        }
    }
}
