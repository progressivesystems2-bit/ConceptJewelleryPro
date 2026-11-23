using System.Security.Claims;
using ConceptJewelleryPro.API.Models;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConceptJewelleryPro.API.Controllers;

/// <summary>
/// Logs customer jewellery requirements (wish-list / inward details)
/// into the <c>CustomerRequirement</c> table for follow-up by the sales team.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CustomerRequirementController : ControllerBase
{
    private readonly ICustomerRequirementRepository _requirementRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly ISettingsRepository _settingsRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CustomerRequirementController> _logger;

    public CustomerRequirementController(
        ICustomerRequirementRepository requirementRepository,
        IOrderRepository orderRepository,
        ICustomerRepository customerRepository,
        ISettingsRepository settingsRepository,
        IConfiguration configuration,
        ILogger<CustomerRequirementController> logger)
    {
        _requirementRepository = requirementRepository;
        _orderRepository       = orderRepository;
        _customerRepository    = customerRepository;
        _settingsRepository    = settingsRepository;
        _configuration         = configuration;
        _logger                = logger;
    }

    /// <summary>
    /// Look up existing customer by mobile or phone number from the database.
    /// Returns customer name and details if found.
    /// </summary>
    [HttpGet("lookup-customer")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> LookupCustomer([FromQuery] string phone, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(phone) || phone.Trim().Length < 5)
        {
            return Ok(new { found = false });
        }

        try
        {
            var customer = await _customerRepository.FindByPhoneOrMobileAsync(phone.Trim(), cancellationToken);
            if (customer != null && !string.IsNullOrWhiteSpace(customer.CompanyName) && customer.CompanyName != "-")
            {
                return Ok(new
                {
                    found = true,
                    customerName = customer.CompanyName.Trim(),
                    code = customer.Code,
                    phone = customer.Phone,
                    mobile = customer.Mobile,
                    address = customer.Address,
                    city = customer.City
                });
            }

            return Ok(new { found = false });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error looking up customer by phone: {Phone}", phone);
            return Ok(new { found = false });
        }
    }

    /// <summary>
    /// Log a new customer requirement / custom order directly into the ERP [Order] table.
    /// The <c>OrderNo</c> and <c>Nos</c> are sequentially generated.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Log(
        [FromBody] CustomerRequirementRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var entryUser = User.FindFirstValue(ClaimTypes.Name) ?? "WebStaff";

        var category = string.Equals(request.OrderCategory, "Order", StringComparison.OrdinalIgnoreCase)
            ? "Order"
            : "Requirement";

        var customerName = request.CustomerName.Trim();
        var customerMobile = request.CustomerMobile?.Trim() ?? string.Empty;
        var itemName = request.ItemName.Trim();

        var finalRemarks = request.Remarks?.Trim();
        if (!string.IsNullOrWhiteSpace(request.ImagePath))
        {
            var imgRef = $"[Photo: {Path.GetFileName(request.ImagePath)}]";
            finalRemarks = string.IsNullOrWhiteSpace(finalRemarks) ? imgRef : $"{finalRemarks} | {imgRef}";
        }

        // Parse approximate numeric weight from WeightRange if present
        decimal parsedWeight = 0m;
        if (!string.IsNullOrWhiteSpace(request.WeightRange))
        {
            var match = System.Text.RegularExpressions.Regex.Match(request.WeightRange, @"[\d.]+");
            if (match.Success && decimal.TryParse(match.Value, out var w))
            {
                parsedWeight = w;
            }
        }

        string orderNo;
        int nos;

        try
        {
            nos = await _orderRepository.GetNextNosAsync();
            orderNo = await _orderRepository.GetNextOrderNoAsync();

            var combinedDesc = string.IsNullOrWhiteSpace(request.Description)
                ? (finalRemarks ?? string.Empty)
                : (string.IsNullOrWhiteSpace(finalRemarks) ? request.Description.Trim() : $"{request.Description.Trim()} | {finalRemarks}");

            if (!string.IsNullOrWhiteSpace(request.WeightRange))
            {
                combinedDesc = $"[Wt: {request.WeightRange.Trim()}] {combinedDesc}".Trim();
            }

            // 1. Primary Save to ERP [Order] Table
            var order = new CustomerOrder
            {
                OrderNo       = orderNo,
                Nos           = nos,
                OrderCategory = category,
                OrderDate     = DateTime.Today,
                DeliveryDate  = DateTime.Today.AddDays(14),
                CustomerName  = customerName,
                Phone         = customerMobile,
                ItemName      = itemName,
                Qty           = 1,
                Size          = request.Size?.Trim() ?? string.Empty,
                Description   = combinedDesc,
                Purity        = string.IsNullOrWhiteSpace(request.Purity) ? "22K" : request.Purity.Trim(),
                AppWeight     = parsedWeight,
                Photo         = request.ImagePath?.Trim() ?? string.Empty,
                EntryUser     = entryUser,
                EntryDate     = DateTime.Today,
                EntryTime     = DateTime.Now,
                Status        = "Saved",
                Closed        = false
            };

            await _orderRepository.SaveAsync(order, isNew: true, currentUser: entryUser);
            _logger.LogInformation("Successfully saved {Category} #{OrderNo} into ERP [Order] table for '{Customer}'",
                category, orderNo, customerName);

            // 2. Also log to [CustomerRequirement] table for consistency
            try
            {
                var requirement = new CustomerRequirement
                {
                    EntryDate      = DateTime.Now,
                    OrderCategory  = category,
                    CustomerName   = customerName,
                    CustomerMobile = customerMobile,
                    ItemName       = itemName,
                    Description    = request.Description?.Trim() ?? string.Empty,
                    Size           = request.Size?.Trim(),
                    WeightRange    = request.WeightRange?.Trim(),
                    Purity         = request.Purity?.Trim(),
                    Remarks        = finalRemarks,
                    ImagePath      = request.ImagePath?.Trim(),
                    EntryUser      = entryUser
                };
                await _requirementRepository.LogAsync(requirement, cancellationToken);
            }
            catch (Exception reqEx)
            {
                _logger.LogWarning(reqEx, "Could not mirror to CustomerRequirement table (non-fatal)");
            }

            // 3. If customer is new, auto-record in [Customers] directory
            if (!string.IsNullOrWhiteSpace(customerMobile))
            {
                try
                {
                    var existingCust = await _customerRepository.FindByPhoneOrMobileAsync(customerMobile, cancellationToken);
                    if (existingCust == null)
                    {
                        var newCust = new Customer
                        {
                            CompanyName = customerName,
                            Mobile      = customerMobile,
                            Phone       = customerMobile,
                            Code        = $"CUST-{nos:D4}",
                            Status      = "Active",
                            User        = entryUser,
                            Date        = DateTime.Today,
                            Time        = DateTime.Now.TimeOfDay
                        };
                        await _customerRepository.AddAsync(newCust, cancellationToken);
                        _logger.LogInformation("Auto-enrolled new customer '{CustomerName}' with mobile {Mobile}", customerName, customerMobile);
                    }
                }
                catch (Exception custEx)
                {
                    _logger.LogWarning(custEx, "Auto-enrolling customer in Customers table failed (non-fatal)");
                }
            }

            return StatusCode(StatusCodes.Status201Created, new
            {
                message = $"{category} #{orderNo} logged successfully into ERP database! 📝",
                orderNo,
                nos,
                orderCategory = category,
                customerName
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log {Category} for '{CustomerName}'", category, customerName);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = $"Failed to save {category.ToLowerInvariant()} to database: {ex.Message}",
                error   = ex.Message
            });
        }
    }

    /// <summary>
    /// Upload a reference design photo or sketch for customer requirement.
    /// Saves to <c>{SavePath}\CustomerRequirements\</c> or local server directory.
    /// </summary>
    [HttpPost("upload-image")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UploadImage(IFormFile image, CancellationToken cancellationToken)
    {
        if (image is null || image.Length == 0)
            return BadRequest(new { message = "Image file is required." });

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".bmp" };
        var extension = Path.GetExtension(image.FileName)?.ToLowerInvariant();
        if (string.IsNullOrEmpty(extension) || !allowedExtensions.Contains(extension))
        {
            return BadRequest(new
            {
                message = $"Invalid image format. Allowed formats: {string.Join(", ", allowedExtensions)}"
            });
        }

        // Determine destination directories strictly inside SavePath
        var savePath = await _settingsRepository.GetValueAsync(AppSetting.SavePathKey, cancellationToken);
        if (string.IsNullOrWhiteSpace(savePath))
        {
            _logger.LogError("SavePath setting is not configured.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "SavePath is not configured. Please set SavePath in the WPF application settings."
            });
        }

        var candidateDirs = new[]
        {
            Path.Combine(savePath, "CustomerRequirements"),
            Path.Combine(savePath, "Order")
        };

        try
        {
            var safeOriginal = string.Join("_", Path.GetFileNameWithoutExtension(image.FileName).Split(Path.GetInvalidFileNameChars())).Trim('_');
            if (string.IsNullOrEmpty(safeOriginal)) safeOriginal = "design";

            var fileName = $"req_{DateTime.Now:yyyyMMdd_HHmmss}_{safeOriginal}{extension}";
            string? primaryFilePath = null;

            foreach (var dir in candidateDirs)
            {
                try
                {
                    Directory.CreateDirectory(dir);
                    var targetFile = Path.Combine(dir, fileName);
                    await using (var stream = new FileStream(targetFile, FileMode.Create))
                    {
                        await image.CopyToAsync(stream, cancellationToken);
                    }
                    primaryFilePath ??= targetFile;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not copy requirement image to {Dir}", dir);
                }
            }

            if (primaryFilePath is null)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    message = "Failed to save requirement image to SavePath directories. Please check folder write permissions."
                });
            }

            _logger.LogInformation("Saved customer requirement design photo as {FileName} in {SavePath}", fileName, savePath);

            return Ok(new
            {
                message  = "Image uploaded successfully.",
                fileName,
                filePath = primaryFilePath,
                imageUrl = $"/api/CustomerRequirement/image/{fileName}",
                fileSize = image.Length
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save customer requirement image.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Failed to save image on server.",
                error   = ex.Message
            });
        }
    }

    /// <summary>
    /// Serves a saved customer requirement design image strictly from SavePath.
    /// </summary>
    [HttpGet("image/{fileName}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetImage(string fileName, CancellationToken cancellationToken)
    {
        var cleanFileName = Path.GetFileName(fileName);
        var savePath = await _settingsRepository.GetValueAsync(AppSetting.SavePathKey, cancellationToken);
        if (string.IsNullOrWhiteSpace(savePath) || !Directory.Exists(savePath))
        {
            return NotFound(new { message = "Requirement image not found (SavePath is not configured or accessible)." });
        }

        var candidateDirs = new[]
        {
            Path.Combine(savePath, "CustomerRequirements"),
            Path.Combine(savePath, "Order"),
            savePath
        };

        foreach (var dir in candidateDirs)
        {
            if (!Directory.Exists(dir)) continue;
            var fullPath = Path.Combine(dir, cleanFileName);
            if (System.IO.File.Exists(fullPath))
            {
                var ext = Path.GetExtension(fullPath).ToLowerInvariant();
                var mimeType = ext switch
                {
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".png"            => "image/png",
                    ".webp"           => "image/webp",
                    ".bmp"            => "image/bmp",
                    _                 => "application/octet-stream"
                };
                return PhysicalFile(fullPath, mimeType);
            }
        }

        return NotFound(new { message = "Requirement image not found." });
    }
}
