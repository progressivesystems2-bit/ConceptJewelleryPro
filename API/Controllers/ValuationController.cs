using ConceptJewelleryPro.API.Models;
using ConceptJewelleryPro.Data.Access.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConceptJewelleryPro.API.Controllers;

/// <summary>
/// Calculates the current market price of a jewellery article
/// based on its barcode, the live rate from RateMaster (PURITY table),
/// and the caller-supplied making charge and GST preference.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ValuationController : ControllerBase
{
    /// <summary>GST rate applied to jewellery (3% as per Indian GST law).</summary>
    private const decimal GstRate = 3m;

    private readonly IBarcodeCreationRepository _barcodeRepository;
    private readonly IPurityRepository _purityRepository;
    private readonly ILogger<ValuationController> _logger;

    public ValuationController(
        IBarcodeCreationRepository barcodeRepository,
        IPurityRepository purityRepository,
        ILogger<ValuationController> logger)
    {
        _barcodeRepository = barcodeRepository;
        _purityRepository  = purityRepository;
        _logger            = logger;
    }

    /// <summary>
    /// Calculate the current selling price of a jewellery article.
    /// </summary>
    /// <remarks>
    /// **Making charge rules:**
    /// - If `makingPerGram` is provided → Making = NetWt × MakingPerGram
    /// - Else if `makingPercent` is provided → Making = MetalValue × MakingPercent / 100
    /// - If neither is provided → Making = 0
    ///
    /// **GST rules:**
    /// - `ExclusiveGST` (default) → FinalPrice = SubTotal + (SubTotal × 3%)
    /// - `InclusiveGST` → FinalPrice = SubTotal (GST back-calculated for display)
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(typeof(ValuationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Calculate(
        [FromBody] ValuationRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Barcode))
            return BadRequest(new { message = "Barcode is required." });

        // ── 1. Fetch barcode details ────────────────────────────────────────
        var barcode = await _barcodeRepository.GetBarcodeByCodeAsync(
            request.Barcode.Trim(), cancellationToken);

        if (barcode is null)
        {
            _logger.LogWarning("Valuation requested for unknown barcode: {Barcode}", request.Barcode);
            return NotFound(new { message = $"Barcode '{request.Barcode}' not found." });
        }

        // ── 2. Fetch rate from PURITY (RateMaster) ──────────────────────────
        var purity = await _purityRepository.GetByPurityAsync(barcode.Purity, cancellationToken);
        if (purity is null)
        {
            _logger.LogWarning("No purity rate found for purity: {Purity}", barcode.Purity);
            return BadRequest(new { message = $"Rate not configured for purity '{barcode.Purity}'." });
        }

        var ratePerGram = purity.RatePerGram;

        // ── 3. Calculate metal value ─────────────────────────────────────────
        var netWt      = barcode.NetWt;
        var grossWt    = barcode.GrossWt;
        var stoneWt    = barcode.StoneWt;
        var stoneValue = barcode.StoneValue;
        var metalValue = Math.Round(netWt * ratePerGram, 2);

        // ── 4. Calculate making amount ───────────────────────────────────────
        decimal makingAmount;
        if (request.MakingPerGram.HasValue && request.MakingPerGram.Value > 0)
        {
            makingAmount = Math.Round(netWt * request.MakingPerGram.Value, 2);
        }
        else if (request.MakingPercent.HasValue && request.MakingPercent.Value > 0)
        {
            makingAmount = Math.Round(metalValue * request.MakingPercent.Value / 100m, 2);
        }
        else
        {
            // Fall back to making stored on the barcode itself
            makingAmount = barcode.Making;
        }

        // ── 5. SubTotal ──────────────────────────────────────────────────────
        var subTotal = metalValue + stoneValue + makingAmount;

        // ── 6. GST calculation ───────────────────────────────────────────────
        decimal gstAmount;
        decimal finalPrice;

        if (request.GstMode == GstMode.ExclusiveGST)
        {
            gstAmount  = Math.Round(subTotal * GstRate / 100m, 2);
            finalPrice = Math.Round(subTotal + gstAmount, 2);
        }
        else // InclusiveGST — GST is already inside SubTotal
        {
            // Back-calculate: SubTotal = FinalPrice (GST included)
            // GstAmount = SubTotal - (SubTotal / (1 + GstRate/100))
            gstAmount  = Math.Round(subTotal - (subTotal / (1 + GstRate / 100m)), 2);
            finalPrice = Math.Round(subTotal, 2);
        }

        _logger.LogInformation(
            "Valuation for barcode {Barcode}: MetalValue={MetalValue}, Making={Making}, FinalPrice={FinalPrice}",
            barcode.Barcode, metalValue, makingAmount, finalPrice);

        return Ok(new ValuationResult
        {
            Barcode      = barcode.Barcode,
            ItemName     = barcode.ItemName,
            Purity       = barcode.Purity,
            GrossWt      = grossWt,
            StoneWt      = stoneWt,
            NetWt        = netWt,
            RatePerGram  = ratePerGram,
            MetalValue   = metalValue,
            StoneValue   = stoneValue,
            MakingAmount = makingAmount,
            SubTotal     = subTotal,
            GstMode      = request.GstMode.ToString(),
            GstPercent   = GstRate,
            GstAmount    = gstAmount,
            FinalPrice   = finalPrice
        });
    }
}
