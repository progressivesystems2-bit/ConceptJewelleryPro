using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Services;

public interface ICouponEngineService
{
    Task<CouponValidationResult> ValidateCouponAsync(
        string couponCode,
        string? customerMobile,
        decimal cartGrossAmount,
        DateTime billDate,
        string? enteredOtp = null,
        CancellationToken cancellationToken = default);

    decimal CalculateDiscount(Coupon coupon, decimal cartGrossAmount);

    Task<CouponRedemption?> RedeemCouponForBillAsync(
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
        CancellationToken cancellationToken = default);

    Task<bool> RevertCouponForBillAsync(
        string billNo,
        string reason,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Coupon>> GetAvailableCouponsForCustomerAsync(
        string customerMobile,
        decimal currentBillGross,
        DateTime billDate,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Coupon>> GenerateBulkCouponsAsync(
        CouponCampaign campaign,
        IEnumerable<CustomerSegmentationResult> targetCustomers,
        string createdBy,
        bool dispatchWhatsApp = false,
        CancellationToken cancellationToken = default);

    Task<Coupon> GenerateSingleCouponAsync(
        Coupon coupon,
        CancellationToken cancellationToken = default);

    Task<(int sentCount, int failedCount)> DispatchCouponsWhatsAppAsync(
        IEnumerable<int> couponIds,
        string? customTemplate = null,
        CancellationToken cancellationToken = default);

    Task<int> ProcessAutoTriggersAsync(CancellationToken cancellationToken = default);

    Task<Coupon?> ProcessBounceBackVoucherOnBillAsync(
        string billNo,
        string customerMobile,
        string customerName,
        decimal billGrossTotal,
        string createdBy,
        CancellationToken cancellationToken = default);

    Task<(int updatedCount, int checkedCount)> SyncCouponStatusesAsync(
        IEnumerable<int>? couponIds = null,
        CancellationToken cancellationToken = default);
}
