using ConceptJewelleryPro.API.Models;
using ConceptJewelleryPro.API.Services;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConceptJewelleryPro.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InvoiceController : ControllerBase
{
    private readonly IInvoiceParsingService _invoiceParsingService;
    private readonly ISettingsRepository _settingsRepository;
    private readonly ILogger<InvoiceController> _logger;

    public InvoiceController(
        IInvoiceParsingService invoiceParsingService,
        ISettingsRepository settingsRepository,
        ILogger<InvoiceController> logger)
    {
        _invoiceParsingService = invoiceParsingService;
        _settingsRepository = settingsRepository;
        _logger = logger;
    }

    /// <summary>
    /// Upload an invoice/bill image, save it to {SavePath}/AiBill/, and parse it using Azure Document Intelligence.
    /// Returns structured invoice data (vendor, items, totals).
    /// </summary>
    [HttpPost("parse")]
    [ProducesResponseType(typeof(InvoiceResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Parse(IFormFile image, CancellationToken cancellationToken)
    {
        if (image is null || image.Length == 0)
        {
            return BadRequest(new { message = "Image file is required." });
        }

        // Validate file is an image or PDF
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".pdf" };
        var extension = Path.GetExtension(image.FileName)?.ToLowerInvariant();
        if (string.IsNullOrEmpty(extension) || !allowedExtensions.Contains(extension))
        {
            return BadRequest(new { message = $"Invalid file format. Allowed: {string.Join(", ", allowedExtensions)}" });
        }

        var savePath = await _settingsRepository.GetValueAsync(AppSetting.SavePathKey, cancellationToken);
        if (string.IsNullOrWhiteSpace(savePath))
        {
            _logger.LogError("SavePath setting is not configured in the database.");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "SavePath is not configured. Please set SavePath in the WPF application settings." });
        }

        var aiBillDir = Path.Combine(savePath, "AiBill");
        Directory.CreateDirectory(aiBillDir);

        // Generate a unique filename to avoid collisions
        var fileName = $"{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(aiBillDir, fileName);

        _logger.LogInformation("Saving invoice image to {FilePath}", filePath);

        await using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await image.CopyToAsync(stream, cancellationToken);
        }

        try
        {
            var invoiceResult = await _invoiceParsingService.ParseInvoiceAsync(filePath, cancellationToken);
            return Ok(invoiceResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse invoice image: {FilePath}", filePath);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "Failed to parse the invoice image.", error = ex.Message });
        }
    }
}
