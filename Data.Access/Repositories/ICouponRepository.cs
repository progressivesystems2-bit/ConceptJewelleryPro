using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Data.Access.Repositories;

public interface ICouponRepository
{
    Task<IReadOnlyList<CouponCampaign>> GetCampaignsAsync(CancellationToken cancellationToken = default);
    Task<CouponCampaign?> GetCampaignByIdAsync(int campaignId, CancellationToken cancellationToken = default);
    Task<int> SaveCampaignAsync(CouponCampaign campaign, CancellationToken cancellationToken = default);
    Task UpdateCampaignAsync(CouponCampaign campaign, CancellationToken cancellationToken = default);
    Task DeleteCampaignAsync(int campaignId, CancellationToken cancellationToken = default);

    Task<Coupon?> GetCouponByCodeAsync(string couponCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Coupon>> GetActiveCouponsForCustomerAsync(string customerMobile, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Coupon>> GetCouponsAsync(int? campaignId, string? status, string? search, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default);
    Task<int> GetCouponsCountAsync(int? campaignId, string? status, string? search, CancellationToken cancellationToken = default);
    Task SaveCouponsBatchAsync(IEnumerable<Coupon> coupons, CancellationToken cancellationToken = default);
    Task UpdateCouponStatusAsync(int couponId, CouponStatus status, int usageIncrement = 0, CancellationToken cancellationToken = default);
    Task UpdateCouponDispatchStatusAsync(int couponId, CouponDispatchStatus status, string? error = null, string? whatsAppMessageId = null, CancellationToken cancellationToken = default);
    Task<Coupon?> GetCouponByMessageIdAsync(string messageId, CancellationToken cancellationToken = default);
    Task<bool> UpdateCouponDeliveryStatusAsync(string messageId, string status, DateTime? timestamp = null, string? error = null, CancellationToken cancellationToken = default);
    Task<int> SyncCouponDeliveryStatusesBatchAsync(IEnumerable<CouponDeliveryStatusUpdate> updates, CancellationToken cancellationToken = default);

    Task<int> RecordRedemptionAsync(CouponRedemption redemption, CancellationToken cancellationToken = default);
    Task<bool> RevertRedemptionAsync(string billNo, string? reason, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CouponRedemption>> GetRedemptionsByBillNoAsync(string billNo, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CustomerSegmentationResult>> SearchCustomersForSegmentationAsync(CouponCustomerSegmentFilter filter, CancellationToken cancellationToken = default);
    Task<CouponAnalyticsSummary> GetAnalyticsSummaryAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CouponRedemption>> GetRecentRedemptionsAsync(int count = 100, CancellationToken cancellationToken = default);
}
