namespace ConceptJewelleryPro.Domain.Entities;

public enum CouponType
{
    FlatAmount,
    Percentage
}

public enum CouponStatus
{
    Active,
    Redeemed,
    Expired,
    Revoked
}

public enum CouponAutoTriggerType
{
    None,
    Birthday,
    Anniversary,
    WinBack60Days,
    BounceBackVoucher
}

public enum CouponDispatchStatus
{
    Pending,
    Sent,
    Failed
}

public class CouponCampaign
{
    public int CampaignId { get; set; }
    public string CampaignName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public CouponType CouponType { get; set; } = CouponType.FlatAmount;
    public decimal DiscountValue { get; set; }
    public decimal? MinBillAmount { get; set; }
    public decimal? MaxDiscountAmount { get; set; }
    public DateTime ValidFrom { get; set; } = DateTime.Today;
    public DateTime ValidTo { get; set; } = DateTime.Today.AddDays(30);
    public bool IsActive { get; set; } = true;
    public CouponAutoTriggerType AutoTriggerType { get; set; } = CouponAutoTriggerType.None;
    public decimal? TriggerThresholdAmount { get; set; }
    public string CodePrefix { get; set; } = "CPN";
    public string WhatsAppTemplate { get; set; } = "Dear {CustomerName}, here is an exclusive coupon {CouponCode} for {DiscountText} on your next purchase at {Store}! Valid till {ValidTo}. T&C apply.";
    public string? WhatsAppTemplateName { get; set; }
    public string? WhatsAppParameters { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public int TotalGenerated { get; set; }
    public int TotalRedeemed { get; set; }
}

public class Coupon
{
    public int CouponId { get; set; }
    public int? CampaignId { get; set; }
    public string CampaignName { get; set; } = string.Empty;
    public string CouponCode { get; set; } = string.Empty;
    public string? CustomerMobile { get; set; }
    public string? CustomerName { get; set; }
    public CouponType CouponType { get; set; } = CouponType.FlatAmount;
    public decimal DiscountValue { get; set; }
    public decimal? MinBillAmount { get; set; }
    public decimal? MaxDiscountAmount { get; set; }
    public DateTime ValidFrom { get; set; } = DateTime.Today;
    public DateTime ValidTo { get; set; } = DateTime.Today.AddDays(30);
    public int MaxUsageCount { get; set; } = 1;
    public int UsageCount { get; set; }
    public CouponStatus Status { get; set; } = CouponStatus.Active;
    public string Channel { get; set; } = "Direct";
    public CouponDispatchStatus DispatchStatus { get; set; } = CouponDispatchStatus.Pending;
    public DateTime? DispatchedAt { get; set; }
    public string? DispatchError { get; set; }
    public string? WhatsAppMessageId { get; set; }
    public string DeliveryStatus { get; set; } = "Pending";
    public DateTime? DeliveredAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public bool RequireOtp { get; set; }
    public string? OtpCode { get; set; }
    public string? Notes { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public bool IsSelected { get; set; }

    public string FormattedDiscount => CouponType == CouponType.Percentage
        ? $"{DiscountValue:G29}% OFF{(MaxDiscountAmount.HasValue ? $" (Max ₹{MaxDiscountAmount.Value:N0})" : "")}"
        : $"₹{DiscountValue:N0} OFF";

    public bool IsUsableNow(DateTime date) =>
        Status == CouponStatus.Active &&
        UsageCount < MaxUsageCount &&
        date.Date >= ValidFrom.Date &&
        date.Date <= ValidTo.Date;
}

public class CouponRedemption
{
    public int RedemptionId { get; set; }
    public int CouponId { get; set; }
    public string CouponCode { get; set; } = string.Empty;
    public string BillNo { get; set; } = string.Empty;
    public string CustomerMobile { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public decimal BillGrossAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal BillNetAmount { get; set; }
    public string Counter { get; set; } = string.Empty;
    public string Store { get; set; } = string.Empty;
    public string RedeemedBy { get; set; } = string.Empty;
    public DateTime RedeemedAt { get; set; } = DateTime.Now;
    public bool IsReverted { get; set; }
    public DateTime? RevertedAt { get; set; }
    public string? RevertedReason { get; set; }
}

public class CouponValidationResult
{
    public bool IsValid { get; set; }
    public string Message { get; set; } = string.Empty;
    public Coupon? Coupon { get; set; }
    public decimal DiscountAmount { get; set; }

    public static CouponValidationResult Fail(string message) =>
        new() { IsValid = false, Message = message };

    public static CouponValidationResult Success(Coupon coupon, decimal discountAmount, string message = "Coupon applied successfully.") =>
        new() { IsValid = true, Coupon = coupon, DiscountAmount = discountAmount, Message = message };
}

public class CouponCustomerSegmentFilter
{
    public decimal? MinLifetimeSpend { get; set; }
    public decimal? MaxLifetimeSpend { get; set; }
    public int? MinVisits { get; set; }
    public int? MaxVisits { get; set; }
    public int? InactiveDaysMin { get; set; }
    public int? InactiveDaysMax { get; set; }
    public int? UpcomingBirthdayDays { get; set; }
    public int? UpcomingAnniversaryDays { get; set; }
    public int? BirthMonth { get; set; }
    public string? City { get; set; }
    public string? Group { get; set; }
    public bool ExcludeDnd { get; set; } = true;
    public bool OnlyWithMobile { get; set; } = true;
    public int MaxRecords { get; set; } = 1000;
}

public class CustomerSegmentationResult
{
    public string Code { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string Mobile { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public decimal LifetimeSpend { get; set; }
    public int VisitCount { get; set; }
    public DateTime? LastVisitDate { get; set; }
    public int? DaysSinceLastVisit { get; set; }
    public DateTime? Dob { get; set; }
    public DateTime? Doa { get; set; }
    public bool Dnd { get; set; }
    public bool IsSelected { get; set; } = true;
}

public class CouponAnalyticsSummary
{
    public int TotalCampaigns { get; set; }
    public int TotalCouponsGenerated { get; set; }
    public int TotalCouponsRedeemed { get; set; }
    public decimal OverallRedemptionRate { get; set; }
    public decimal TotalSalesWithCoupons { get; set; }
    public decimal TotalDiscountsGiven { get; set; }
    public decimal AverageBasketValueWithCoupon { get; set; }
    public decimal RoiRatio { get; set; }
    public int TotalCouponsDispatched { get; set; }
    public int TotalCouponsDelivered { get; set; }
    public int TotalCouponsRead { get; set; }
    public decimal DeliveryRate { get; set; }
    public decimal ReadRate { get; set; }
}

public class CouponDeliveryStatusUpdate
{
    public string MessageId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? Timestamp { get; set; }
    public string? ErrorMessage { get; set; }
}
