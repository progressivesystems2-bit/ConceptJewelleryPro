using System.Security.Claims;
using ConceptJewelleryPro.API.Models;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConceptJewelleryPro.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BillController : ControllerBase
{
    private readonly IBillRepository _billRepository;
    private readonly ILogger<BillController> _logger;

    public BillController(
        IBillRepository billRepository,
        ILogger<BillController> logger)
    {
        _billRepository = billRepository;
        _logger = logger;
    }

    /// <summary>
    /// Gets active sales voucher types from the ERP configuration (SALESVOUTYPE).
    /// </summary>
    [HttpGet("voucher-types")]
    [ProducesResponseType(typeof(IReadOnlyList<VouTypeOptionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVoucherTypes(CancellationToken cancellationToken)
    {
        try
        {
            var rawList = await _billRepository.GetSalesVouTypesAsync(null, null, cancellationToken);

            var options = rawList
                .Where(v => !v.Hide)
                .Select(v => new VouTypeOptionDto
                {
                    VouType = v.VouType,
                    ShortName = string.IsNullOrWhiteSpace(v.ShortName) ? v.VouType : v.ShortName,
                    VatPercent = v.VatPercentage,
                    HeadCategory = v.HeadCategory
                })
                .ToList();

            if (options.Count == 0)
            {
                options.Add(new VouTypeOptionDto
                {
                    VouType = "GST RETAIL SALE",
                    ShortName = "GST",
                    VatPercent = 3m,
                    HeadCategory = "Sales"
                });
                options.Add(new VouTypeOptionDto
                {
                    VouType = "ESTIMATE",
                    ShortName = "EST",
                    VatPercent = 0m,
                    HeadCategory = "Sales"
                });
            }

            return Ok(options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve sales voucher types");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Error loading voucher types." });
        }
    }

    /// <summary>
    /// Calculates the next sequential VouNo based on the highest existing VouNo in the Bill table.
    /// </summary>
    [HttpGet("next-vouno")]
    [ProducesResponseType(typeof(NextVouNoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetNextVouNo([FromQuery] string vouType, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(vouType))
        {
            return BadRequest(new { message = "Voucher type is required." });
        }

        try
        {
            var nextVouNo = await _billRepository.GetNextVouNoAsync(vouType.Trim(), null, cancellationToken);
            return Ok(new NextVouNoResponse
            {
                VouType = vouType.Trim(),
                VouNo = nextVouNo
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate next VouNo for {VouType}", vouType);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Error calculating next voucher number." });
        }
    }

    /// <summary>
    /// Saves a complete POS Bill to the ERP database, re-generating the latest sequential VouNo,
    /// inserting into Bill, BillSubform, PaymentSubform, and deducting sold stock items.
    /// </summary>
    [HttpPost("save")]
    [ProducesResponseType(typeof(SaveBillResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Save([FromBody] SaveBillRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (request.Items == null || request.Items.Count == 0)
        {
            return BadRequest(new { message = "At least one article is required to save the bill." });
        }

        var vouType = request.VouType.Trim();

        try
        {
            // Concurrency-safe: regenerate latest next VouNo at the instant of saving
            var freshVouNo = await _billRepository.GetNextVouNoAsync(vouType, null, cancellationToken);
            if (string.IsNullOrWhiteSpace(freshVouNo))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Could not generate voucher number." });
            }

            var entryUser = User.FindFirstValue(ClaimTypes.Name) ?? "WebAdmin";

            // Compute totals
            decimal totalGrossAmt = request.Items.Sum(i => i.GrossAmt > 0 ? i.GrossAmt : (i.MetalValue + i.StoneValue + i.Making));
            decimal totalTaxAmt = request.Items.Sum(i => i.VatAmt);
            decimal discount = Math.Max(0, request.DiscountAllowed);
            decimal netAmt = Math.Round(Math.Max(0, totalGrossAmt - discount + totalTaxAmt), 2);

            var bill = new Bill
            {
                VouNo = freshVouNo,
                VouType = vouType,
                BillDate = DateTime.Today,
                DueDate = DateTime.Today,
                Phone = request.Phone?.Trim() ?? string.Empty,
                CustomerName = !string.IsNullOrWhiteSpace(request.CustomerName) ? request.CustomerName.Trim() : "Walk-in Customer",
                CustomerCode = request.CustomerCode?.Trim() ?? string.Empty,
                Address = request.Address?.Trim() ?? string.Empty,
                Address2 = request.Address2?.Trim() ?? string.Empty,
                City = !string.IsNullOrWhiteSpace(request.City) ? request.City.Trim() : "CUTTACK",
                Pincode = request.Pincode?.Trim() ?? string.Empty,
                Email = request.Email?.Trim() ?? string.Empty,
                VatNo = request.VatNo?.Trim() ?? string.Empty,
                Check = false,
                BillCanceled = false,
                GrossAmt = totalGrossAmt,
                DiscountAllowed = discount,
                NetAmt = netAmt,
                Tax = totalTaxAmt > 0 ? 3m : 0m,
                TaxAmt = totalTaxAmt,
                Remarks = request.Remarks?.Trim() ?? string.Empty,
                Narration = $"Web POS Bill generated on {DateTime.Now:dd-MMM-yyyy HH:mm}",
                EntryUser = entryUser,
                EntryDate = DateTime.Today,
                EntryTime = DateTime.Now,
                ComputerName = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "WebClient",
                Status = "Saved"
            };

            var items = request.Items.Select((item, idx) => new BillSubform
            {
                VouNo = freshVouNo,
                AutoNo = idx + 1,
                Barcode = item.Barcode?.Trim() ?? string.Empty,
                ItemName = item.ItemName?.Trim() ?? string.Empty,
                Purity = item.Purity?.Trim() ?? string.Empty,
                Qty = item.Qty > 0 ? item.Qty : 1,
                NtWt = item.NtWt,
                GrWt = item.GrWt > 0 ? item.GrWt : item.NtWt,
                Rate = item.Rate,
                Amount = item.Amount > 0 ? item.Amount : Math.Round(item.NtWt * item.Rate, 2),
                GrossAmt = item.GrossAmt > 0 ? item.GrossAmt : (item.MetalValue + item.StoneValue + item.Making),
                Making = item.Making,
                MperGr = item.MperGr,
                MetalValue = item.MetalValue,
                StoneValue = item.StoneValue,
                VatAmt = item.VatAmt,
                DisAmt = item.DisAmt,
                DisPer = item.DisPer,
                ENote = item.ENote?.Trim() ?? string.Empty,
                SalesMan = item.SalesMan?.Trim() ?? string.Empty
            }).ToList();

            var paymentMode = !string.IsNullOrWhiteSpace(request.PaymentMode) ? request.PaymentMode.Trim() : "Cash";
            var payments = new List<PaymentSubform>
            {
                new PaymentSubform
                {
                    VouNo = freshVouNo,
                    AccountName = paymentMode.ToUpperInvariant(),
                    Amount = netAmt,
                    HeadCategory = "Sales",
                    Narration = $"Settled via {paymentMode}"
                }
            };

            _logger.LogInformation("Saving bill {VouNo} ({VouType}) for customer '{Customer}' with {ItemCount} items, Net: {NetAmt}",
                freshVouNo, vouType, bill.CustomerName, items.Count, netAmt);

            await _billRepository.AddAsync(bill, items, payments, cancellationToken);

            return Ok(new SaveBillResponse
            {
                Success = true,
                Message = $"Bill #{freshVouNo} saved to ERP database successfully!",
                VouNo = freshVouNo,
                VouType = vouType,
                CustomerName = bill.CustomerName,
                GrossAmt = bill.GrossAmt,
                DiscountAllowed = bill.DiscountAllowed,
                TaxAmt = bill.TaxAmt,
                NetAmt = bill.NetAmt,
                BillDate = bill.BillDate
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save bill for voucher type {VouType}", vouType);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "Failed to save bill into the ERP database.", error = ex.Message });
        }
    }
}
