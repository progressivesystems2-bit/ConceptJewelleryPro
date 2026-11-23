using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Coupons;

public sealed class DemoCouponRepository : ICouponRepository
{
    private readonly List<CouponCampaign> _campaigns = [];
    private readonly List<Coupon> _coupons = [];
    private readonly List<CouponRedemption> _redemptions = [];
    private int _nextCampaignId = 1;
    private int _nextCouponId = 1;
    private int _nextRedemptionId = 1;

    public DemoCouponRepository()
    {
        var fest = new CouponCampaign
        {
            CampaignId = _nextCampaignId++,
            CampaignName = "Akshaya Tritiya Gold Offer",
            Description = "Flat ₹1,000 off on gold jewellery purchases above ₹25,000",
            CouponType = CouponType.FlatAmount,
            DiscountValue = 1000m,
            MinBillAmount = 25000m,
            ValidFrom = DateTime.Today.AddDays(-7),
            ValidTo = DateTime.Today.AddDays(30),
            IsActive = true,
            CodePrefix = "GOLD",
            CreatedBy = "admin"
        };
        _campaigns.Add(fest);

        _coupons.Add(new Coupon
        {
            CouponId = _nextCouponId++,
            CampaignId = fest.CampaignId,
            CampaignName = fest.CampaignName,
            CouponCode = "GOLD2026",
            CouponType = CouponType.FlatAmount,
            DiscountValue = 1000m,
            MinBillAmount = 25000m,
            ValidFrom = fest.ValidFrom,
            ValidTo = fest.ValidTo,
            MaxUsageCount = 50,
            Status = CouponStatus.Active,
            Channel = "Direct",
            CreatedBy = "admin"
        });
    }

    public Task<IReadOnlyList<CouponCampaign>> GetCampaignsAsync(CancellationToken cancellationToken = default)
    {
        var result = _campaigns
            .Select(c =>
            {
                c.TotalGenerated = _coupons.Count(cp => cp.CampaignId == c.CampaignId);
                c.TotalRedeemed = _coupons.Count(cp => cp.CampaignId == c.CampaignId && cp.UsageCount > 0);
                return c;
            })
            .OrderByDescending(c => c.CampaignId)
            .ToList();
        return Task.FromResult<IReadOnlyList<CouponCampaign>>(result);
    }

    public Task<CouponCampaign?> GetCampaignByIdAsync(int campaignId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_campaigns.FirstOrDefault(c => c.CampaignId == campaignId));

    public Task<int> SaveCampaignAsync(CouponCampaign campaign, CancellationToken cancellationToken = default)
    {
        campaign.CampaignId = _nextCampaignId++;
        campaign.CreatedAt = DateTime.Now;
        _campaigns.Add(campaign);
        return Task.FromResult(campaign.CampaignId);
    }

    public Task UpdateCampaignAsync(CouponCampaign campaign, CancellationToken cancellationToken = default)
    {
        var idx = _campaigns.FindIndex(c => c.CampaignId == campaign.CampaignId);
        if (idx >= 0) _campaigns[idx] = campaign;
        return Task.CompletedTask;
    }

    public Task DeleteCampaignAsync(int campaignId, CancellationToken cancellationToken = default)
    {
        _campaigns.RemoveAll(c => c.CampaignId == campaignId);
        return Task.CompletedTask;
    }

    public Task<Coupon?> GetCouponByCodeAsync(string couponCode, CancellationToken cancellationToken = default) =>
        Task.FromResult(_coupons.FirstOrDefault(c => string.Equals(c.CouponCode, couponCode.Trim(), StringComparison.OrdinalIgnoreCase)));

    public Task<IReadOnlyList<Coupon>> GetActiveCouponsForCustomerAsync(string customerMobile, CancellationToken cancellationToken = default)
    {
        var clean = customerMobile.Trim();
        var list = _coupons.Where(c => c.Status == CouponStatus.Active && !string.IsNullOrWhiteSpace(c.CustomerMobile) && c.CustomerMobile.Contains(clean, StringComparison.OrdinalIgnoreCase)).ToList();
        return Task.FromResult<IReadOnlyList<Coupon>>(list);
    }

    public Task<IReadOnlyList<Coupon>> GetCouponsAsync(int? campaignId, string? status, string? search, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        IEnumerable<Coupon> q = _coupons;
        if (campaignId.HasValue) q = q.Where(c => c.CampaignId == campaignId.Value);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<CouponStatus>(status, true, out var st)) q = q.Where(c => c.Status == st);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(c => c.CouponCode.Contains(s, StringComparison.OrdinalIgnoreCase)
                          || (c.CustomerName?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false)
                          || (c.CustomerMobile?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false));
        }
        return Task.FromResult<IReadOnlyList<Coupon>>(q.OrderByDescending(c => c.CouponId).Skip((page - 1) * pageSize).Take(pageSize).ToList());
    }

    public Task<int> GetCouponsCountAsync(int? campaignId, string? status, string? search, CancellationToken cancellationToken = default) =>
        Task.FromResult(_coupons.Count);

    public Task SaveCouponsBatchAsync(IEnumerable<Coupon> coupons, CancellationToken cancellationToken = default)
    {
        foreach (var c in coupons)
        {
            c.CouponId = _nextCouponId++;
            c.CreatedAt = DateTime.Now;
            _coupons.Add(c);
        }
        return Task.CompletedTask;
    }

    public Task UpdateCouponStatusAsync(int couponId, CouponStatus status, int usageIncrement = 0, CancellationToken cancellationToken = default)
    {
        var c = _coupons.FirstOrDefault(x => x.CouponId == couponId);
        if (c != null)
        {
            c.Status = status;
            c.UsageCount += usageIncrement;
        }
        return Task.CompletedTask;
    }

    public Task UpdateCouponDispatchStatusAsync(int couponId, CouponDispatchStatus status, string? error = null, string? whatsAppMessageId = null, CancellationToken cancellationToken = default)
    {
        var c = _coupons.FirstOrDefault(x => x.CouponId == couponId);
        if (c != null)
        {
            c.DispatchStatus = status;
            c.DispatchedAt = DateTime.Now;
            c.DispatchError = error;
            if (!string.IsNullOrWhiteSpace(whatsAppMessageId)) c.WhatsAppMessageId = whatsAppMessageId;
        }
        return Task.CompletedTask;
    }

    public Task<Coupon?> GetCouponByMessageIdAsync(string messageId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_coupons.FirstOrDefault(c => string.Equals(c.WhatsAppMessageId, messageId, StringComparison.OrdinalIgnoreCase)));

    public Task<bool> UpdateCouponDeliveryStatusAsync(string messageId, string status, DateTime? timestamp = null, string? error = null, CancellationToken cancellationToken = default)
    {
        var c = _coupons.FirstOrDefault(x => string.Equals(x.WhatsAppMessageId, messageId, StringComparison.OrdinalIgnoreCase));
        if (c == null) return Task.FromResult(false);
        c.DeliveryStatus = status;
        if (status.Equals("delivered", StringComparison.OrdinalIgnoreCase)) c.DeliveredAt ??= timestamp ?? DateTime.Now;
        if (status.Equals("read", StringComparison.OrdinalIgnoreCase)) c.ReadAt ??= timestamp ?? DateTime.Now;
        if (!string.IsNullOrWhiteSpace(error)) c.DispatchError = error;
        return Task.FromResult(true);
    }

    public async Task<int> SyncCouponDeliveryStatusesBatchAsync(IEnumerable<CouponDeliveryStatusUpdate> updates, CancellationToken cancellationToken = default)
    {
        int count = 0;
        foreach (var u in updates)
        {
            if (await UpdateCouponDeliveryStatusAsync(u.MessageId, u.Status, u.Timestamp, u.ErrorMessage, cancellationToken))
                count++;
        }
        return count;
    }

    public Task<int> RecordRedemptionAsync(CouponRedemption redemption, CancellationToken cancellationToken = default)
    {
        redemption.RedemptionId = _nextRedemptionId++;
        _redemptions.Add(redemption);
        return Task.FromResult(redemption.RedemptionId);
    }

    public Task<bool> RevertRedemptionAsync(string billNo, string? reason, CancellationToken cancellationToken = default)
    {
        var hits = _redemptions.Where(r => string.Equals(r.BillNo, billNo, StringComparison.OrdinalIgnoreCase) && !r.IsReverted).ToList();
        foreach (var r in hits)
        {
            r.IsReverted = true;
            r.RevertedAt = DateTime.Now;
            r.RevertedReason = reason;
            var c = _coupons.FirstOrDefault(x => x.CouponId == r.CouponId);
            if (c != null)
            {
                c.UsageCount = Math.Max(0, c.UsageCount - 1);
                if (c.Status == CouponStatus.Redeemed && c.UsageCount < c.MaxUsageCount)
                    c.Status = CouponStatus.Active;
            }
        }
        return Task.FromResult(hits.Count > 0);
    }

    public Task<IReadOnlyList<CouponRedemption>> GetRedemptionsByBillNoAsync(string billNo, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CouponRedemption>>(_redemptions.Where(r => string.Equals(r.BillNo, billNo, StringComparison.OrdinalIgnoreCase)).ToList());

    public Task<IReadOnlyList<CustomerSegmentationResult>> SearchCustomersForSegmentationAsync(CouponCustomerSegmentFilter filter, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CustomerSegmentationResult>>([
            new() { Code = "C001", CompanyName = "Asha Devi", Mobile = "9876543210", City = "Bhubaneswar", Group = "VIP", LifetimeSpend = 125000m, VisitCount = 4, LastVisitDate = DateTime.Today.AddDays(-15), DaysSinceLastVisit = 15 }
        ]);

    public Task<CouponAnalyticsSummary> GetAnalyticsSummaryAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
    {
        var activeRedemptions = _redemptions.Where(r => !r.IsReverted).ToList();
        var totalSales = activeRedemptions.Sum(r => r.BillNetAmount);
        var totalDisc = activeRedemptions.Sum(r => r.DiscountAmount);
        return Task.FromResult(new CouponAnalyticsSummary
        {
            TotalCampaigns = _campaigns.Count,
            TotalCouponsGenerated = _coupons.Count,
            TotalCouponsRedeemed = activeRedemptions.Count,
            OverallRedemptionRate = _coupons.Count > 0 ? Math.Round((decimal)activeRedemptions.Count / _coupons.Count * 100m, 2) : 0m,
            TotalSalesWithCoupons = totalSales,
            TotalDiscountsGiven = totalDisc,
            AverageBasketValueWithCoupon = activeRedemptions.Count > 0 ? Math.Round(totalSales / activeRedemptions.Count, 2) : 0m,
            RoiRatio = totalDisc > 0 ? Math.Round(totalSales / totalDisc, 2) : 0m
        });
    }

    public Task<IReadOnlyList<CouponRedemption>> GetRecentRedemptionsAsync(int count = 100, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CouponRedemption>>(_redemptions.OrderByDescending(r => r.RedemptionId).Take(count).ToList());
}
