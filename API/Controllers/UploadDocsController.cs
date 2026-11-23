using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConceptJewelleryPro.API.Controllers;

/// <summary>
/// Saves an uploaded document image to <c>{SavePath}\Docs\</c>.
/// Supports images (JPEG, PNG, BMP, TIFF, WebP) and PDFs.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UploadDocsController : ControllerBase
{
    private static readonly string[] AllowedExtensions =
        { ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".webp", ".pdf" };

    private readonly ISettingsRepository _settingsRepository;
    private readonly ILogger<UploadDocsController> _logger;

    public UploadDocsController(
        ISettingsRepository settingsRepository,
        ILogger<UploadDocsController> logger)
    {
        _settingsRepository = settingsRepository;
        _logger             = logger;
    }

    /// <summary>
    /// Upload a document and save it to <c>{SavePath}\Docs\</c>.
    /// The file is renamed to <c>{yyyyMMdd_HHmmss}_{originalFilename}</c>
    /// to avoid naming collisions.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Upload(IFormFile document, CancellationToken cancellationToken)
    {
        if (document is null || document.Length == 0)
            return BadRequest(new { message = "Document file is required." });

        var extension = Path.GetExtension(document.FileName)?.ToLowerInvariant();
        if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
        {
            return BadRequest(new
            {
                message = $"Invalid file format. Allowed: {string.Join(", ", AllowedExtensions)}"
            });
        }

        // ── Resolve SavePath from settings ───────────────────────────────────
        var savePath = await _settingsRepository.GetValueAsync(AppSetting.SavePathKey, cancellationToken);
        if (string.IsNullOrWhiteSpace(savePath))
        {
            _logger.LogError("SavePath setting is not configured.");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "SavePath is not configured. Please set SavePath in the WPF application settings." });
        }

        // ── Build target path ────────────────────────────────────────────────
        var docsDir = Path.Combine(savePath, "Docs");
        Directory.CreateDirectory(docsDir);

        var safeOriginal = SanitiseFileName(Path.GetFileNameWithoutExtension(document.FileName));
        var fileName = $"{DateTime.Now:yyyyMMdd_HHmmss}_{safeOriginal}{extension}";
        var filePath = Path.Combine(docsDir, fileName);

        _logger.LogInformation("Saving document to {FilePath} ({Bytes} bytes)", filePath, document.Length);

        await using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await document.CopyToAsync(stream, cancellationToken);
        }

        return Ok(new
        {
            message      = "Document uploaded successfully.",
            fileName,
            filePath,
            fileSize     = document.Length
        });
    }

    private static string SanitiseFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", name.Split(invalid)).Trim('_');
    }
}
