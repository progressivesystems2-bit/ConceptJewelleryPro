using System.Data.OleDb;

namespace ConceptJewelleryPro.Infrastructure.Coupons;

public static class AccessCouponSchemaMigrator
{
    private static bool _migrated;
    private static readonly object _lock = new();

    public static async Task EnsureSchemaAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        if (_migrated || string.IsNullOrWhiteSpace(connectionString))
            return;

        try
        {
            await using var connection = new OleDbConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await EnsureTablesAsync(connection, cancellationToken);

            lock (_lock)
            {
                _migrated = true;
            }
        }
        catch
        {
            // Non-fatal if database is locked or read-only
        }
    }

    private static async Task EnsureTablesAsync(OleDbConnection connection, CancellationToken cancellationToken)
    {
        const string createCampaignTable = """
            CREATE TABLE [CouponCampaign]
            (
                [CampaignId] AUTOINCREMENT CONSTRAINT [PK_CouponCampaign] PRIMARY KEY,
                [CampaignName] TEXT(150) NOT NULL,
                [Description] LONGTEXT,
                [CouponType] INTEGER NOT NULL,
                [DiscountValue] CURRENCY NOT NULL,
                [MinBillAmount] CURRENCY,
                [MaxDiscountAmount] CURRENCY,
                [ValidFrom] DATETIME NOT NULL,
                [ValidTo] DATETIME NOT NULL,
                [IsActive] BIT NOT NULL,
                [AutoTriggerType] INTEGER NOT NULL,
                [TriggerThresholdAmount] CURRENCY,
                [CodePrefix] TEXT(20),
                [WhatsAppTemplate] LONGTEXT,
                [WhatsAppTemplateName] TEXT(150),
                [WhatsAppParameters] TEXT(255),
                [CreatedBy] TEXT(100),
                [CreatedAt] DATETIME NOT NULL
            )
            """;

        const string createCouponTable = """
            CREATE TABLE [Coupon]
            (
                [CouponId] AUTOINCREMENT CONSTRAINT [PK_Coupon] PRIMARY KEY,
                [CampaignId] INTEGER,
                [CampaignName] TEXT(150),
                [CouponCode] TEXT(50) NOT NULL,
                [CustomerMobile] TEXT(30),
                [CustomerName] TEXT(150),
                [CouponType] INTEGER NOT NULL,
                [DiscountValue] CURRENCY NOT NULL,
                [MinBillAmount] CURRENCY,
                [MaxDiscountAmount] CURRENCY,
                [ValidFrom] DATETIME NOT NULL,
                [ValidTo] DATETIME NOT NULL,
                [MaxUsageCount] INTEGER NOT NULL,
                [UsageCount] INTEGER NOT NULL,
                [Status] INTEGER NOT NULL,
                [Channel] TEXT(50),
                [DispatchStatus] INTEGER NOT NULL,
                [DispatchedAt] DATETIME,
                [DispatchError] LONGTEXT,
                [WhatsAppMessageId] TEXT(255),
                [DeliveryStatus] TEXT(50),
                [DeliveredAt] DATETIME,
                [ReadAt] DATETIME,
                [RequireOtp] BIT NOT NULL,
                [OtpCode] TEXT(20),
                [Notes] LONGTEXT,
                [CreatedBy] TEXT(100),
                [CreatedAt] DATETIME NOT NULL
            )
            """;

        const string createRedemptionTable = """
            CREATE TABLE [CouponRedemption]
            (
                [RedemptionId] AUTOINCREMENT CONSTRAINT [PK_CouponRedemption] PRIMARY KEY,
                [CouponId] INTEGER NOT NULL,
                [CouponCode] TEXT(50) NOT NULL,
                [BillNo] TEXT(50) NOT NULL,
                [CustomerMobile] TEXT(30),
                [CustomerName] TEXT(150),
                [BillGrossAmount] CURRENCY NOT NULL,
                [DiscountAmount] CURRENCY NOT NULL,
                [BillNetAmount] CURRENCY NOT NULL,
                [Counter] TEXT(50),
                [Store] TEXT(100),
                [RedeemedBy] TEXT(100),
                [RedeemedAt] DATETIME NOT NULL,
                [IsReverted] BIT NOT NULL,
                [RevertedAt] DATETIME,
                [RevertedReason] LONGTEXT
            )
            """;

        foreach (var ddl in new[] { createCampaignTable, createCouponTable, createRedemptionTable })
        {
            try
            {
                await using var cmd = new OleDbCommand(ddl, connection);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
            catch
            {
                // Table already exists
            }
        }

        foreach (var indexSql in new[]
        {
            "CREATE UNIQUE INDEX UX_Coupon_Code ON [Coupon] ([CouponCode])",
            "CREATE INDEX IX_Coupon_Mobile ON [Coupon] ([CustomerMobile])",
            "CREATE INDEX IX_Coupon_CampaignId ON [Coupon] ([CampaignId])",
            "CREATE INDEX IX_Coupon_Wamid ON [Coupon] ([WhatsAppMessageId])",
            "CREATE INDEX IX_CouponRedemption_BillNo ON [CouponRedemption] ([BillNo])",
            "CREATE INDEX IX_CouponRedemption_CouponId ON [CouponRedemption] ([CouponId])"
        })
        {
            try
            {
                await using var cmd = new OleDbCommand(indexSql, connection);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
            catch
            {
                // Index already exists
            }
        }
    }
}
