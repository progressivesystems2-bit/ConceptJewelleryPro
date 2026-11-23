using System.Security.Cryptography;
using System.Text;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Services;

public sealed class CouponEngineService : ICouponEngineService
{
    private static readonly char[] CodeChars = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ".ToCharArray();

    private readonly ICouponRepository _couponRepository;
    private readonly IWhatsAppService? _whatsAppService;

    public CouponEngineService(
        ICouponRepository couponRepository,
        IWhatsAppService? whatsAppService = null)
    {
        _couponRepository = couponRepository;
        _whatsAppService = whatsAppService;
    }

    public async Task<CouponValidationResult> ValidateCouponAsync(
        string couponCode,
        string? customerMobile,
        decimal cartGrossAmount,
        DateTime billDate,
        string? enteredOtp = null,
        CancellationToken cancellationToken = default)
    {
        var cleanCode = (couponCode ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(cleanCode))
            return CouponValidationResult.Fail("Coupon code cannot be empty.");

        var coupon = await _couponRepository.GetCouponByCodeAsync(cleanCode, cancellationToken);
        if (coupon == null)
            return CouponValidationResult.Fail($"Coupon code '{cleanCode}' is invalid or does not exist.");

        if (coupon.Status == CouponStatus.Expired || billDate.Date > coupon.ValidTo.Date)
            return CouponValidationResult.Fail($"Coupon expired on {coupon.ValidTo:dd/MM/yyyy}.");

        if (billDate.Date < coupon.ValidFrom.Date)
            return CouponValidationResult.Fail($"Coupon is only valid starting from {coupon.ValidFrom:dd/MM/yyyy}.");

        if (coupon.Status == CouponStatus.Revoked)
            return CouponValidationResult.Fail("This coupon has been revoked and cannot be used.");

        if (coupon.Status == CouponStatus.Redeemed || coupon.UsageCount >= coupon.MaxUsageCount)
            return CouponValidationResult.Fail($"Coupon '{cleanCode}' has already reached its maximum usage limit ({coupon.MaxUsageCount}).");

        if (!string.IsNullOrWhiteSpace(coupon.CustomerMobile))
        {
            var normalizedCouponMobile = NormalizePhone(coupon.CustomerMobile);
            var normalizedCustMobile = NormalizePhone(customerMobile);

            if (string.IsNullOrWhiteSpace(normalizedCustMobile))
                return CouponValidationResult.Fail($"This coupon is assigned to mobile ending in ...{GetMaskedMobile(coupon.CustomerMobile)}. Please enter customer mobile/phone.");

            if (!normalizedCouponMobile.EndsWith(normalizedCustMobile, StringComparison.OrdinalIgnoreCase) &&
                !normalizedCustMobile.EndsWith(normalizedCouponMobile, StringComparison.OrdinalIgnoreCase))
            {
                return CouponValidationResult.Fail($"This coupon is personalized and can only be redeemed by mobile ending in ...{GetMaskedMobile(coupon.CustomerMobile)}.");
            }
        }

        if (coupon.MinBillAmount.HasValue && cartGrossAmount < coupon.MinBillAmount.Value)
        {
            var deficit = coupon.MinBillAmount.Value - cartGrossAmount;
            return CouponValidationResult.Fail($"Min purchase of ₹{coupon.MinBillAmount.Value:N2} required. Add ₹{deficit:N2} more to qualify.");
        }

        if (coupon.RequireOtp)
        {
            var cleanEnteredOtp = (enteredOtp ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(cleanEnteredOtp))
                return CouponValidationResult.Fail("OTP verification is required for this coupon.");

            if (!string.Equals(coupon.OtpCode?.Trim(), cleanEnteredOtp, StringComparison.OrdinalIgnoreCase))
                return CouponValidationResult.Fail("Invalid OTP entered for coupon verification.");
        }

        var discount = CalculateDiscount(coupon, cartGrossAmount);
        if (discount <= 0)
            return CouponValidationResult.Fail("Calculated coupon discount is ₹0.");

        return CouponValidationResult.Success(coupon, discount, $"Coupon applied! ₹{discount:N2} discount granted.");
    }

    public decimal CalculateDiscount(Coupon coupon, decimal cartGrossAmount)
    {
        if (cartGrossAmount <= 0) return 0m;

        decimal discount = coupon.CouponType == CouponType.Percentage
            ? cartGrossAmount * (coupon.DiscountValue / 100m)
            : coupon.DiscountValue;

        if (coupon.CouponType == CouponType.Percentage && coupon.MaxDiscountAmount.HasValue && discount > coupon.MaxDiscountAmount.Value)
            discount = coupon.MaxDiscountAmount.Value;

        discount = Math.Min(discount, cartGrossAmount);
        return Math.Round(Math.Max(0m, discount), 2, MidpointRounding.AwayFromZero);
    }

    public async Task<CouponRedemption?> RedeemCouponForBillAsync(
        string couponCode,
        string billNo,
        string customerMobile,
        string customerName,
        decimal billGross,
        decimal discount,
        decimal netAmount,
        string counter,
        string store,
        string user,
        CancellationToken cancellationToken = default)
    {
        var cleanCode = (couponCode ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(cleanCode)) return null;

        var coupon = await _couponRepository.GetCouponByCodeAsync(cleanCode, cancellationToken);
        if (coupon == null) return null;

        var newUsage = coupon.UsageCount + 1;
        var newStatus = newUsage >= coupon.MaxUsageCount ? CouponStatus.Redeemed : CouponStatus.Active;
        await _couponRepository.UpdateCouponStatusAsync(coupon.CouponId, newStatus, usageIncrement: 1, cancellationToken);

        var redemption = new CouponRedemption
        {
            CouponId = coupon.CouponId,
            CouponCode = coupon.CouponCode,
            BillNo = billNo,
            CustomerMobile = customerMobile,
            CustomerName = customerName,
            BillGrossAmount = billGross,
            DiscountAmount = discount,
            BillNetAmount = netAmount,
            Counter = counter,
            Store = store,
            RedeemedBy = user,
            RedeemedAt = DateTime.Now
        };

        redemption.RedemptionId = await _couponRepository.RecordRedemptionAsync(redemption, cancellationToken);
        return redemption;
    }

    public async Task<bool> RevertCouponForBillAsync(string billNo, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(billNo)) return false;
        return await _couponRepository.RevertRedemptionAsync(billNo, reason, cancellationToken);
    }

    public async Task<IReadOnlyList<Coupon>> GetAvailableCouponsForCustomerAsync(
        string customerMobile,
        decimal currentBillGross,
        DateTime billDate,
        CancellationToken cancellationToken = default)
    {
        var cleanPhone = NormalizePhone(customerMobile);
        if (string.IsNullOrWhiteSpace(cleanPhone)) return [];

        var coupons = await _couponRepository.GetActiveCouponsForCustomerAsync(cleanPhone, cancellationToken);
        return coupons.Where(c => c.IsUsableNow(billDate)).ToList();
    }

    public async Task<IReadOnlyList<Coupon>> GenerateBulkCouponsAsync(
        CouponCampaign campaign,
        IEnumerable<CustomerSegmentationResult> targetCustomers,
        string createdBy,
        bool dispatchWhatsApp = false,
        CancellationToken cancellationToken = default)
    {
        var prefix = string.IsNullOrWhiteSpace(campaign.CodePrefix) ? "CPN" : campaign.CodePrefix.Trim().ToUpperInvariant();
        var list = new List<Coupon>();

        foreach (var cust in targetCustomers)
        {
            if (string.IsNullOrWhiteSpace(cust.Mobile)) continue;

            list.Add(new Coupon
            {
                CampaignId = campaign.CampaignId,
                CampaignName = campaign.CampaignName,
                CouponCode = GenerateUniqueCode(prefix),
                CustomerMobile = cust.Mobile.Trim(),
                CustomerName = cust.CompanyName?.Trim(),
                CouponType = campaign.CouponType,
                DiscountValue = campaign.DiscountValue,
                MinBillAmount = campaign.MinBillAmount,
                MaxDiscountAmount = campaign.MaxDiscountAmount,
                ValidFrom = campaign.ValidFrom,
                ValidTo = campaign.ValidTo,
                MaxUsageCount = 1,
                UsageCount = 0,
                Status = CouponStatus.Active,
                Channel = "WhatsApp",
                DispatchStatus = CouponDispatchStatus.Pending,
                CreatedBy = createdBy,
                CreatedAt = DateTime.Now
            });
        }

        if (list.Count == 0) return list;

        await _couponRepository.SaveCouponsBatchAsync(list, cancellationToken);

        if (dispatchWhatsApp && _whatsAppService != null)
        {
            await DispatchCouponsWhatsAppAsync(list.Select(c => c.CouponId), campaign.WhatsAppTemplate, cancellationToken);
        }

        return list;
    }

    public async Task<Coupon> GenerateSingleCouponAsync(Coupon coupon, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(coupon.CouponCode))
        {
            coupon.CouponCode = GenerateUniqueCode("CPN");
        }
        else
        {
            coupon.CouponCode = coupon.CouponCode.Trim().ToUpperInvariant();
        }

        await _couponRepository.SaveCouponsBatchAsync([coupon], cancellationToken);
        return coupon;
    }

    public async Task<(int sentCount, int failedCount)> DispatchCouponsWhatsAppAsync(
        IEnumerable<int> couponIds,
        string? customTemplate = null,
        CancellationToken cancellationToken = default)
    {
        if (_whatsAppService == null)
            return (0, 0);

        var idSet = couponIds.ToHashSet();
        if (idSet.Count == 0) return (0, 0);

        var allCoupons = await _couponRepository.GetCouponsAsync(null, null, null, 1, 5000, cancellationToken);
        var targets = allCoupons.Where(c => idSet.Contains(c.CouponId)).ToList();
        var campaigns = (await _couponRepository.GetCampaignsAsync(cancellationToken)).ToDictionary(c => c.CampaignId);

        var storeName = "Concept Jewellery";
        try
        {
            var info = DateRangeService.GetCompanyDetails();
            if (!string.IsNullOrWhiteSpace(info.CompanyName)) storeName = info.CompanyName.Trim();
        }
        catch { }

        int sent = 0, failed = 0;
        foreach (var c in targets)
        {
            if (string.IsNullOrWhiteSpace(c.CustomerMobile))
            {
                await _couponRepository.UpdateCouponDispatchStatusAsync(c.CouponId, CouponDispatchStatus.Failed, "Missing customer mobile", null, cancellationToken);
                failed++;
                continue;
            }

            string template = customTemplate ?? string.Empty;
            if (string.IsNullOrWhiteSpace(template) && c.CampaignId.HasValue && campaigns.TryGetValue(c.CampaignId.Value, out var camp))
            {
                template = camp.WhatsAppTemplate;
            }
            if (string.IsNullOrWhiteSpace(template))
            {
                template = "Dear {CustomerName}, here is your exclusive coupon {CouponCode} for {DiscountText} at {Store}! Valid till {ValidTo}. T&C apply.";
            }

            var message = template
                .Replace("{CustomerName}", string.IsNullOrWhiteSpace(c.CustomerName) ? "Valued Customer" : c.CustomerName)
                .Replace("{CouponCode}", c.CouponCode)
                .Replace("{DiscountText}", c.FormattedDiscount)
                .Replace("{MinBill}", c.MinBillAmount.HasValue ? $"₹{c.MinBillAmount.Value:N0}" : "No Minimum")
                .Replace("{Store}", storeName)
                .Replace("{StoreName}", storeName)
                .Replace("{ValidTo}", c.ValidTo.ToString("dd/MM/yyyy"))
                .Replace("{ValidFrom}", c.ValidFrom.ToString("dd/MM/yyyy"))
                .Replace("{CampaignName}", c.CampaignName);

            try
            {
                var res = await _whatsAppService.SendInvoiceMessageAsync(new WhatsAppMessageRequest
                {
                    RecipientPhone = c.CustomerMobile,
                    CustomerName = c.CustomerName ?? "Customer",
                    InvoiceNo = c.CouponCode,
                    BillDate = c.ValidTo,
                    NetAmount = c.DiscountValue,
                    StoreName = storeName,
                    CustomMessage = message,
                    AttachPdf = false
                }, cancellationToken);

                if (res.Success)
                {
                    await _couponRepository.UpdateCouponDispatchStatusAsync(c.CouponId, CouponDispatchStatus.Sent, null, res.ExternalId, cancellationToken);
                    sent++;
                }
                else
                {
                    await _couponRepository.UpdateCouponDispatchStatusAsync(c.CouponId, CouponDispatchStatus.Failed, res.Message, null, cancellationToken);
                    failed++;
                }
            }
            catch (Exception ex)
            {
                await _couponRepository.UpdateCouponDispatchStatusAsync(c.CouponId, CouponDispatchStatus.Failed, ex.Message, null, cancellationToken);
                failed++;
            }
        }

        return (sent, failed);
    }

    public async Task<int> ProcessAutoTriggersAsync(CancellationToken cancellationToken = default)
    {
        var campaigns = await _couponRepository.GetCampaignsAsync(cancellationToken);
        var activeTriggers = campaigns
            .Where(c => c.IsActive && c.AutoTriggerType != CouponAutoTriggerType.None && c.AutoTriggerType != CouponAutoTriggerType.BounceBackVoucher && c.ValidTo.Date >= DateTime.Today)
            .ToList();

        if (activeTriggers.Count == 0) return 0;

        int totalGenerated = 0;
        var existingCoupons = await _couponRepository.GetCouponsAsync(null, null, null, 1, 5000, cancellationToken);

        foreach (var camp in activeTriggers)
        {
            var filter = camp.AutoTriggerType switch
            {
                CouponAutoTriggerType.Birthday => new CouponCustomerSegmentFilter { UpcomingBirthdayDays = 7, OnlyWithMobile = true },
                CouponAutoTriggerType.Anniversary => new CouponCustomerSegmentFilter { UpcomingAnniversaryDays = 7, OnlyWithMobile = true },
                CouponAutoTriggerType.WinBack60Days => new CouponCustomerSegmentFilter { InactiveDaysMin = 60, OnlyWithMobile = true },
                _ => null
            };

            if (filter == null) continue;

            var customers = await _couponRepository.SearchCustomersForSegmentationAsync(filter, cancellationToken);
            var eligible = customers.Where(cust =>
                !existingCoupons.Any(ec =>
                    ec.CampaignId == camp.CampaignId &&
                    !string.IsNullOrWhiteSpace(ec.CustomerMobile) &&
                    NormalizePhone(ec.CustomerMobile) == NormalizePhone(cust.Mobile) &&
                    ec.CreatedAt.Date >= DateTime.Today.AddDays(-30)))
                .ToList();

            if (eligible.Count > 0)
            {
                var created = await GenerateBulkCouponsAsync(camp, eligible, "AutoPilot", dispatchWhatsApp: true, cancellationToken);
                totalGenerated += created.Count;
            }
        }

        return totalGenerated;
    }

    public async Task<Coupon?> ProcessBounceBackVoucherOnBillAsync(
        string billNo,
        string customerMobile,
        string customerName,
        decimal billGrossTotal,
        string createdBy,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customerMobile)) return null;

        var campaigns = await _couponRepository.GetCampaignsAsync(cancellationToken);
        var bbCampaign = campaigns
            .Where(c => c.IsActive && c.AutoTriggerType == CouponAutoTriggerType.BounceBackVoucher && DateTime.Today >= c.ValidFrom.Date && DateTime.Today <= c.ValidTo.Date)
            .OrderByDescending(c => c.TriggerThresholdAmount ?? 0m)
            .FirstOrDefault(c => !c.TriggerThresholdAmount.HasValue || billGrossTotal >= c.TriggerThresholdAmount.Value);

        if (bbCampaign == null) return null;

        var coupon = new Coupon
        {
            CampaignId = bbCampaign.CampaignId,
            CampaignName = bbCampaign.CampaignName,
            CouponCode = GenerateUniqueCode(string.IsNullOrWhiteSpace(bbCampaign.CodePrefix) ? "BBV" : bbCampaign.CodePrefix),
            CustomerMobile = customerMobile.Trim(),
            CustomerName = customerName?.Trim(),
            CouponType = bbCampaign.CouponType,
            DiscountValue = bbCampaign.DiscountValue,
            MinBillAmount = bbCampaign.MinBillAmount,
            MaxDiscountAmount = bbCampaign.MaxDiscountAmount,
            ValidFrom = DateTime.Today.AddDays(1),
            ValidTo = DateTime.Today.AddDays(30),
            MaxUsageCount = 1,
            Status = CouponStatus.Active,
            Channel = "BounceBack",
            Notes = $"Auto-issued on Bill #{billNo} (₹{billGrossTotal:N0})",
            CreatedBy = createdBy,
            CreatedAt = DateTime.Now
        };

        await _couponRepository.SaveCouponsBatchAsync([coupon], cancellationToken);

        if (_whatsAppService != null)
        {
            _ = DispatchCouponsWhatsAppAsync([coupon.CouponId], bbCampaign.WhatsAppTemplate, CancellationToken.None);
        }

        return coupon;
    }

    public async Task<(int updatedCount, int checkedCount)> SyncCouponStatusesAsync(
        IEnumerable<int>? couponIds = null,
        CancellationToken cancellationToken = default)
    {
        if (_whatsAppService == null) return (0, 0);

        var all = await _couponRepository.GetCouponsAsync(null, null, null, 1, 2000, cancellationToken);
        var idSet = couponIds?.ToHashSet();
        var targets = all.Where(c =>
            !string.IsNullOrWhiteSpace(c.WhatsAppMessageId) &&
            (idSet == null || idSet.Contains(c.CouponId)) &&
            !string.Equals(c.DeliveryStatus, "Read", StringComparison.OrdinalIgnoreCase))
            .ToList();

        int updated = 0;
        foreach (var c in targets)
        {
            try
            {
                var status = await _whatsAppService.FetchMessageStatusAsync(c.WhatsAppMessageId!, cancellationToken);
                if (!string.IsNullOrWhiteSpace(status) &&
                    !string.Equals(status, c.DeliveryStatus, StringComparison.OrdinalIgnoreCase))
                {
                    if (await _couponRepository.UpdateCouponDeliveryStatusAsync(c.WhatsAppMessageId!, status, DateTime.Now, null, cancellationToken))
                        updated++;
                }
            }
            catch { }
        }

        return (updated, targets.Count);
    }

    private static string GenerateUniqueCode(string prefix)
    {
        var sb = new StringBuilder(prefix.Trim().ToUpperInvariant());
        if (sb.Length > 0) sb.Append('-');
        Span<byte> bytes = stackalloc byte[6];
        RandomNumberGenerator.Fill(bytes);
        foreach (var b in bytes)
        {
            sb.Append(CodeChars[b % CodeChars.Length]);
        }
        return sb.ToString();
    }

    private static string NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        return digits.Length > 10 ? digits[^10..] : digits;
    }

    private static string GetMaskedMobile(string? phone)
    {
        var d = NormalizePhone(phone);
        return d.Length >= 4 ? d[^4..] : d;
    }
}
