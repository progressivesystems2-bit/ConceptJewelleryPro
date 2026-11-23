using ConceptJewelleryPro.API.Models;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.Infrastructure.Printing;
using ConceptJewelleryPro.Infrastructure.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConceptJewelleryPro.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BarcodeController : ControllerBase
{
    private readonly ISettingsRepository _settingsRepository;
    private readonly IBarcodeCreationRepository _barcodeRepository;
    private readonly IPurityRepository _purityRepository;
    private readonly IBarcodePrintService _barcodePrintService;
    private readonly LocalPrinterSettingsStore _settingsStore;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BarcodeController> _logger;

    public BarcodeController(
        ISettingsRepository settingsRepository,
        IBarcodeCreationRepository barcodeRepository,
        IPurityRepository purityRepository,
        IBarcodePrintService barcodePrintService,
        LocalPrinterSettingsStore settingsStore,
        IConfiguration configuration,
        ILogger<BarcodeController> logger)
    {
        _settingsRepository = settingsRepository;
        _barcodeRepository = barcodeRepository;
        _purityRepository = purityRepository;
        _barcodePrintService = barcodePrintService;
        _settingsStore = settingsStore;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Helper to find the actual image file on disk strictly inside Settings.SavePath/Barcode.
    /// </summary>
    private async Task<string?> FindBarcodeImageFileAsync(string barcode, CancellationToken cancellationToken)
    {
        var savePath = await _settingsRepository.GetValueAsync(AppSetting.SavePathKey, cancellationToken);
        if (string.IsNullOrWhiteSpace(savePath) || !Directory.Exists(savePath))
        {
            return null;
        }

        var candidateDirs = new List<string>
        {
            Path.Combine(savePath, "Barcode"),
            Path.Combine(savePath, "Barcodes"),
            savePath
        };

        var rawCode = string.Join("_", barcode.Trim().Split(Path.GetInvalidFileNameChars()));
        var d6Code = int.TryParse(rawCode, out var n) ? n.ToString("D6") : rawCode;
        var trimmedCode = int.TryParse(rawCode, out var n2) ? n2.ToString() : rawCode;

        var patterns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            $"{rawCode}.*",
            $"{d6Code}.*",
            $"{trimmedCode}.*",
            $"{rawCode}_orig.*",
            $"{d6Code}_orig.*"
        };

        foreach (var dir in candidateDirs)
        {
            if (!Directory.Exists(dir)) continue;

            foreach (var pat in patterns)
            {
                try
                {
                    var files = Directory.GetFiles(dir, pat);
                    var validFile = files.FirstOrDefault(f =>
                    {
                        var ext = Path.GetExtension(f).ToLowerInvariant();
                        return ext is ".jpg" or ".jpeg" or ".png" or ".webp" or ".bmp";
                    });

                    if (validFile is not null)
                    {
                        return validFile;
                    }
                }
                catch
                {
                    // Ignore directory read access exceptions
                }
            }
        }

        return null;
    }

    private async Task<string> GetBarcodeUploadDirectoryAsync(CancellationToken cancellationToken)
    {
        var savePath = await _settingsRepository.GetValueAsync(AppSetting.SavePathKey, cancellationToken);
        if (string.IsNullOrWhiteSpace(savePath))
        {
            throw new InvalidOperationException("SavePath is not configured in application settings. Please set SavePath in the WPF application.");
        }

        var dir = Path.Combine(savePath, "Barcode");
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// Fetch complete barcode details including purity rate and image path.
    /// </summary>
    [HttpGet("{barcode}")]
    [ProducesResponseType(typeof(BarcodeDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBarcodeDetails(string barcode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(barcode))
        {
            return BadRequest(new { message = "Barcode is required." });
        }

        var item = await _barcodeRepository.GetBarcodeByCodeAsync(barcode.Trim(), cancellationToken);
        if (item is null)
        {
            _logger.LogWarning("Barcode not found: {Barcode}", barcode);
            return NotFound(new { message = $"Barcode '{barcode}' not found in database." });
        }

        // Fetch live purity rate from PURITY table
        decimal ratePerGram = 0m;
        if (!string.IsNullOrWhiteSpace(item.Purity))
        {
            var purityMaster = await _purityRepository.GetByPurityAsync(item.Purity.Trim(), cancellationToken);
            if (purityMaster is not null)
            {
                ratePerGram = purityMaster.RatePerGram;
            }
        }

        // Check if image exists in Settings.SavePath/Barcode or other candidate locations
        var imageFile = await FindBarcodeImageFileAsync(barcode, cancellationToken);
        string? imageUrl = imageFile is not null ? $"/api/Barcode/image/{barcode.Trim()}" : null;

        var response = new BarcodeDetailResponse
        {
            Barcode = item.Barcode,
            ItemName = item.ItemName,
            ItemCode = item.ItemCode,
            Purity = item.Purity,
            GrossWt = item.GrossWt,
            StoneWt = item.StoneWt,
            NetWt = item.NetWt,
            Making = item.Making,
            MakingType = item.MakingType,
            MPerGr = item.MPerGr,
            StoneValue = item.StoneValue,
            MRP = item.MRP,
            Size = string.IsNullOrWhiteSpace(item.Size) ? "-" : item.Size,
            Description = item.Description,
            HUID = item.HUID,
            RatePerGram = ratePerGram,
            ImageUrl = imageUrl,
            PrintStickerNo = item.PrintStickerNo,
            Status = item.Status,
            StickerFormat = !string.IsNullOrWhiteSpace(item.Sti) ? item.Sti : (string.Equals(item.BType, "PC", StringComparison.OrdinalIgnoreCase) ? "StickerPC" : "StickerWt"),
            BType = string.IsNullOrWhiteSpace(item.BType) ? "WT" : item.BType.Trim().ToUpperInvariant()
        };

        return Ok(response);
    }

    /// <summary>
    /// Stream the barcode product image directly.
    /// </summary>
    [HttpGet("image/{barcode}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBarcodeImage(string barcode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(barcode))
        {
            return BadRequest();
        }

        var imageFile = await FindBarcodeImageFileAsync(barcode, cancellationToken);
        if (imageFile is null)
        {
            return NotFound();
        }

        var ext = Path.GetExtension(imageFile).ToLowerInvariant();
        var contentType = ext switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            _ => "image/jpeg"
        };

        return PhysicalFile(imageFile, contentType);
    }

    /// <summary>
    /// Upload a barcode product image and link it in the database.
    /// </summary>
    [HttpPost("upload")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Upload(
        [FromForm] string barcodeNo,
        IFormFile image,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(barcodeNo))
        {
            return BadRequest(new { message = "BarcodeNo is required." });
        }

        if (image is null || image.Length == 0)
        {
            return BadRequest(new { message = "Image file is required." });
        }

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".webp" };
        var extension = Path.GetExtension(image.FileName)?.ToLowerInvariant();
        if (string.IsNullOrEmpty(extension) || !allowedExtensions.Contains(extension))
        {
            return BadRequest(new { message = $"Invalid image format. Allowed: {string.Join(", ", allowedExtensions)}" });
        }

        var safeBarcode = string.Join("_", barcodeNo.Trim().Split(Path.GetInvalidFileNameChars()));
        var fileName = $"{safeBarcode}{extension}";

        var savePath = await _settingsRepository.GetValueAsync(AppSetting.SavePathKey, cancellationToken);
        if (string.IsNullOrWhiteSpace(savePath))
        {
            _logger.LogError("SavePath setting is not configured.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "SavePath is not configured. Please set SavePath in the WPF application settings."
            });
        }

        var uploadDir = Path.Combine(savePath, "Barcode");
        Directory.CreateDirectory(uploadDir);

        var targetPath = Path.Combine(uploadDir, fileName);
        try
        {
            await using (var stream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await image.CopyToAsync(stream, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save barcode image to {TargetPath}", targetPath);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = $"Failed to save barcode image to SavePath\\Barcode ({ex.Message}). Please verify folder write permissions."
            });
        }

        var savedFilePath = targetPath;

        // Update photo reference in database table
        try
        {
            await _barcodeRepository.UpdateBarcodePhotoAsync(barcodeNo.Trim(), savedFilePath, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not update Photo column in DB for barcode {BarcodeNo}", barcodeNo);
        }

        return Ok(new
        {
            message = "Barcode image uploaded and saved successfully.",
            barcodeNo,
            filePath = savedFilePath,
            imageUrl = $"/api/Barcode/image/{barcodeNo.Trim()}",
            fileSize = image.Length
        });
    }

    /// <summary>
    /// Get available PRN sticker formats.
    /// </summary>
    [HttpGet("sticker-formats")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStickerFormats(CancellationToken cancellationToken)
    {
        var formats = await _barcodeRepository.GetStickerFormatsAsync(cancellationToken);
        return Ok(formats);
    }

    /// <summary>
    /// Get configured barcode printer and list of all installed printers on the host machine.
    /// </summary>
    [HttpGet("printers")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPrinters(CancellationToken cancellationToken)
    {
        string? configuredPrinter = null;
        try
        {
            configuredPrinter = await _settingsRepository.GetValueAsync(AppSetting.DefaultBarcodePrinterKey, cancellationToken);
            if (string.IsNullOrWhiteSpace(configuredPrinter))
            {
                configuredPrinter = await _settingsRepository.GetValueAsync(AppSetting.BarcodePrinterKey, cancellationToken);
            }
        }
        catch
        {
        }

        if (string.IsNullOrWhiteSpace(configuredPrinter))
        {
            var localSettings = new LocalPrinterSettingsStore().Load();
            configuredPrinter = localSettings.DefaultBarcodePrinter;
        }

        string? configuredPrintEngine = null;
        try
        {
            var localSettings = new LocalPrinterSettingsStore().Load();
            if (!string.IsNullOrWhiteSpace(localSettings.BarcodePrintEngine))
            {
                configuredPrintEngine = BarcodePrintService.NormalizeEngine(localSettings.BarcodePrintEngine);
            }
            if (string.IsNullOrWhiteSpace(configuredPrintEngine))
            {
                var dbEngine = await _settingsRepository.GetValueAsync(AppSetting.BarcodePrintEngineKey, cancellationToken);
                if (!string.IsNullOrWhiteSpace(dbEngine))
                {
                    configuredPrintEngine = BarcodePrintService.NormalizeEngine(dbEngine);
                }
            }
        }
        catch { }

        configuredPrintEngine ??= "PRN";

        var installedPrinters = new List<string>();
        try
        {
            foreach (string p in System.Drawing.Printing.PrinterSettings.InstalledPrinters)
            {
                installedPrinters.Add(p);
            }
        }
        catch
        {
        }

        // Auto-detect fallback if not yet configured
        var autoDetected = BarcodePrintService.AutoDetectBarcodePrinter();

        return Ok(new
        {
            configuredPrinter = configuredPrinter ?? string.Empty,
            autoDetectedPrinter = autoDetected ?? string.Empty,
            configuredPrintEngine,
            installedPrinters
        });
    }

    /// <summary>
    /// Set default barcode printer in system settings and local store.
    /// </summary>
    [HttpPost("default-printer")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetDefaultPrinter([FromBody] SetDefaultPrinterRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PrinterName))
        {
            return BadRequest(new { message = "Printer name is required." });
        }

        var printer = request.PrinterName.Trim();
        var engine = !string.IsNullOrWhiteSpace(request.PrintEngine)
            ? BarcodePrintService.NormalizeEngine(request.PrintEngine)
            : null;

        try
        {
            await _settingsRepository.SaveAsync(AppSetting.DefaultBarcodePrinterKey, printer, cancellationToken);
            if (!string.IsNullOrWhiteSpace(engine))
            {
                await _settingsRepository.SaveAsync(AppSetting.BarcodePrintEngineKey, engine, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not save DefaultBarcodePrinter to database settings");
        }

        try
        {
            var store = new LocalPrinterSettingsStore();
            var settings = store.Load();
            settings.DefaultBarcodePrinter = printer;
            if (!string.IsNullOrWhiteSpace(engine))
            {
                settings.BarcodePrintEngine = engine;
            }
            store.Save(settings);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not save DefaultBarcodePrinter to local printer settings store");
        }

        _logger.LogInformation("Default barcode printer updated to: {Printer} (Engine: {Engine})", printer, engine ?? "unchanged");
        return Ok(new { message = $"Default barcode printer set to '{printer}'{(engine != null ? $" ({engine} mode)" : "")} in settings.", printerName = printer, printEngine = engine });
    }

    /// <summary>
    /// Get configured software installation path (ERP executable folder) and folder status.
    /// </summary>
    [HttpGet("software-path")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSoftwarePath(CancellationToken cancellationToken)
    {
        string? configuredPath = null;
        try
        {
            configuredPath = await _settingsRepository.GetValueAsync(AppSetting.SoftwarePathKey, cancellationToken);
        }
        catch { }

        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            try { configuredPath = _settingsStore.Load().SoftwarePath; } catch { }
        }

        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            configuredPath = _configuration["SoftwarePath"] ?? @"E:\TATTVA JEWELS26-27\net8.0-windows";
        }

        configuredPath = configuredPath?.Trim() ?? string.Empty;
        var exists = !string.IsNullOrWhiteSpace(configuredPath) && Directory.Exists(configuredPath);
        var prnDir = Path.Combine(configuredPath, "PrintTemplates", "BarcodePRN");
        var prnExists = exists && Directory.Exists(prnDir);
        var prnFilesCount = 0;
        if (prnExists)
        {
            try { prnFilesCount = Directory.GetFiles(prnDir, "*.prn").Length; } catch { }
        }

        return Ok(new
        {
            softwarePath = configuredPath,
            exists,
            prnDir,
            prnExists,
            prnFilesCount
        });
    }

    /// <summary>
    /// Set software installation path in database settings and local store.
    /// </summary>
    [HttpPost("software-path")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetSoftwarePath([FromBody] SetSoftwarePathRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.SoftwarePath))
        {
            return BadRequest(new { message = "Software path cannot be empty." });
        }

        var path = request.SoftwarePath.Trim();

        try
        {
            await _settingsRepository.SaveAsync(AppSetting.SoftwarePathKey, path, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not save SoftwarePath to database settings");
        }

        try
        {
            var settings = _settingsStore.Load();
            settings.SoftwarePath = path;
            _settingsStore.Save(settings);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not save SoftwarePath to local printer settings store");
        }

        var exists = Directory.Exists(path);
        var prnDir = Path.Combine(path, "PrintTemplates", "BarcodePRN");
        var prnExists = exists && Directory.Exists(prnDir);
        var prnFilesCount = 0;
        if (prnExists)
        {
            try { prnFilesCount = Directory.GetFiles(prnDir, "*.prn").Length; } catch { }
        }

        _logger.LogInformation("Software path updated to: {Path} (Exists: {Exists}, PRN Dir Exists: {PrnExists}, PRN count: {Count})",
            path, exists, prnExists, prnFilesCount);

        return Ok(new
        {
            message = $"Software path saved: '{path}'.",
            softwarePath = path,
            exists,
            prnDir,
            prnExists,
            prnFilesCount
        });
    }

    /// <summary>
    /// Update MRP, Size, and print barcode sticker using the barcode's PRN template format.
    /// </summary>
    [HttpPost("update-advanced")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAdvanced(
        [FromBody] UpdateBarcodeAdvancedRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Barcode))
        {
            return BadRequest(new { message = "Barcode is required." });
        }

        _logger.LogInformation("Updating advanced barcode details for {Barcode}: BType={BType}, MRP={MRP}, Making={Making}, Size={Size}, Reprint={Reprint}, StickerFormat={Format}, Printer={Printer}",
            request.Barcode, request.BType, request.MRP, request.Making, request.Size, request.ReprintSticker, request.StickerFormat, request.PrinterName);

        try
        {
            // 1. Update database with MRP, Making (MC), Size, BType, reprint flag, and optionally updated Sticker Format
            try
            {
                await _barcodeRepository.UpdateBarcodeAdvancedAsync(
                    request.Barcode.Trim(),
                    request.MRP,
                    request.Size?.Trim(),
                    request.ReprintSticker,
                    request.StickerFormat?.Trim(),
                    request.BType?.Trim(),
                    request.Making,
                    cancellationToken);
            }
            catch (MissingMethodException ex)
            {
                _logger.LogWarning(ex, "7-parameter UpdateBarcodeAdvancedAsync not found in loaded repository assembly. Falling back to 5-parameter overload.");
                await _barcodeRepository.UpdateBarcodeAdvancedAsync(
                    request.Barcode.Trim(),
                    request.MRP,
                    request.Size?.Trim(),
                    request.ReprintSticker,
                    request.StickerFormat?.Trim(),
                    cancellationToken);
            }

            // 2. Fetch the barcode record from database
            var item = await _barcodeRepository.GetBarcodeByCodeAsync(request.Barcode.Trim(), cancellationToken);
            if (item == null)
            {
                return NotFound(new { message = $"Barcode '{request.Barcode}' not found in database." });
            }

            if (!string.IsNullOrWhiteSpace(request.BType)) item.BType = request.BType.Trim().ToUpperInvariant();
            if (request.MRP.HasValue) item.MRP = request.MRP.Value;
            if (request.Making.HasValue) item.Making = request.Making.Value;
            if (!string.IsNullOrWhiteSpace(request.Size)) item.Size = request.Size.Trim();
            if (!string.IsNullOrWhiteSpace(request.StickerFormat)) item.Sti = request.StickerFormat.Trim();

            // 3. Resolve the effective Stickerformat in that barcode:
            var effectiveFormat = !string.IsNullOrWhiteSpace(request.StickerFormat)
                ? request.StickerFormat.Trim()
                : (!string.IsNullOrWhiteSpace(item.Sti)
                    ? item.Sti.Trim()
                    : (string.Equals(item.BType, "PC", StringComparison.OrdinalIgnoreCase) ? "StickerPC" : "StickerWt"));

            // If user explicitly provided a printer or print engine in request, persist it to settings
            var requestedEngine = !string.IsNullOrWhiteSpace(request.PrintEngine)
                ? BarcodePrintService.NormalizeEngine(request.PrintEngine)
                : null;

            if (!string.IsNullOrWhiteSpace(request.PrinterName) || !string.IsNullOrWhiteSpace(requestedEngine))
            {
                var targetPrinter = request.PrinterName?.Trim();
                try
                {
                    if (!string.IsNullOrWhiteSpace(targetPrinter))
                        await _settingsRepository.SaveAsync(AppSetting.DefaultBarcodePrinterKey, targetPrinter, cancellationToken);
                    if (!string.IsNullOrWhiteSpace(requestedEngine))
                        await _settingsRepository.SaveAsync(AppSetting.BarcodePrintEngineKey, requestedEngine, cancellationToken);
                }
                catch { }
                try
                {
                    var store = new LocalPrinterSettingsStore();
                    var s = store.Load();
                    if (!string.IsNullOrWhiteSpace(targetPrinter))
                        s.DefaultBarcodePrinter = targetPrinter;
                    if (!string.IsNullOrWhiteSpace(requestedEngine))
                        s.BarcodePrintEngine = requestedEngine;
                    store.Save(s);
                }
                catch { }
            }

            var activeEngine = requestedEngine
                               ?? BarcodePrintService.NormalizeEngine(new LocalPrinterSettingsStore().Load().BarcodePrintEngine);
            var ext = string.Equals(activeEngine, "FRX", StringComparison.OrdinalIgnoreCase) ? ".frx" : ".prn";

            // 4. Print sticker using PRN or FRX File if reprint or printNow requested
            string printStatus = "Queued";
            string printerName = "";
            if (request.ReprintSticker || request.PrintNow)
            {
                try
                {
                    await _barcodePrintService.PrintBarcodeStickerAsync(item, effectiveFormat, request.PrinterName?.Trim(), activeEngine, cancellationToken);

                    // Resolve the actual printer name used
                    if (!string.IsNullOrWhiteSpace(request.PrinterName))
                    {
                        printerName = request.PrinterName.Trim();
                    }
                    else
                    {
                        printerName = await _settingsRepository.GetValueAsync(AppSetting.DefaultBarcodePrinterKey, cancellationToken)
                                      ?? new LocalPrinterSettingsStore().Load().DefaultBarcodePrinter;
                        if (string.IsNullOrWhiteSpace(printerName))
                        {
                            printerName = BarcodePrintService.AutoDetectBarcodePrinter() ?? "Thermal Barcode Printer";
                        }
                    }

                    printStatus = "Printed";
                    _logger.LogInformation("Barcode #{Barcode} printed successfully using format '{Format}{Ext}' on printer '{Printer}'",
                        item.Barcode, effectiveFormat, ext, printerName);
                }
                catch (Exception printEx)
                {
                    _logger.LogWarning(printEx, "Print sticker error for barcode {Barcode} with format {Format}{Ext}", item.Barcode, effectiveFormat, ext);
                    printStatus = $"Print failed: {printEx.Message}";
                }
            }

            var successMsg = printStatus == "Printed"
                ? (string.Equals(item.BType, "PC", StringComparison.OrdinalIgnoreCase)
                    ? $"Saved (PC: MRP ₹{item.MRP:N0}, Size: {item.Size}) & printed '{effectiveFormat}{ext}' on '{printerName}'! 🏷️🖨️"
                    : $"Saved (WT: MC ₹{item.Making:N0}, Size: {item.Size}) & printed '{effectiveFormat}{ext}' on '{printerName}'! 🏷️🖨️")
                : $"Barcode details updated in database. ({printStatus})";

            return Ok(new
            {
                message = successMsg,
                barcode = request.Barcode.Trim(),
                bType = item.BType,
                mrp = item.MRP,
                making = item.Making,
                size = item.Size,
                stickerFormat = effectiveFormat,
                printEngine = activeEngine,
                printStatus,
                printerName,
                reprintQueued = request.ReprintSticker
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update advanced details for barcode {Barcode}", request.Barcode);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Failed to update barcode details in database." });
        }
    }
}
