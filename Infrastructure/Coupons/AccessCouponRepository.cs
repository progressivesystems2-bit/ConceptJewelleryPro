using System.Data;
using System.Data.OleDb;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Coupons;

public sealed class AccessCouponRepository : ICouponRepository
{
    private readonly string _connectionString;

    public AccessCouponRepository(string databasePath)
    {
        _connectionString = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={databasePath};Persist Security Info=False;";
    }

    private async Task<OleDbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        await AccessCouponSchemaMigrator.EnsureSchemaAsync(_connectionString, cancellationToken);
        var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    public async Task<IReadOnlyList<CouponCampaign>> GetCampaignsAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT CampaignId, CampaignName, Description, CouponType, DiscountValue,
                   MinBillAmount, MaxDiscountAmount, ValidFrom, ValidTo, IsActive,
                   AutoTriggerType, TriggerThresholdAmount, CodePrefix, WhatsAppTemplate,
                   CreatedBy, CreatedAt, WhatsAppTemplateName, WhatsAppParameters
            FROM [CouponCampaign]
            ORDER BY CampaignId DESC
            """;

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = new OleDbCommand(sql, connection);

        var list = new List<CouponCampaign>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                list.Add(ReadCampaign(reader));
            }
        }

        // Populate generated / redeemed counts per campaign
        try
        {
            await using var statsCmd = new OleDbCommand(
                "SELECT CampaignId, COUNT(*), SUM(IIF(UsageCount > 0, 1, 0)) FROM [Coupon] WHERE CampaignId IS NOT NULL GROUP BY CampaignId",
                connection);
            await using var statsReader = await statsCmd.ExecuteReaderAsync(cancellationToken);
            var statsMap = new Dictionary<int, (int gen, int red)>();
            while (await statsReader.ReadAsync(cancellationToken))
            {
                if (!statsReader.IsDBNull(0))
                {
                    statsMap[statsReader.GetInt32(0)] = (
                        statsReader.IsDBNull(1) ? 0 : Convert.ToInt32(statsReader.GetValue(1)),
                        statsReader.IsDBNull(2) ? 0 : Convert.ToInt32(statsReader.GetValue(2)));
                }
            }

            foreach (var c in list)
            {
                if (statsMap.TryGetValue(c.CampaignId, out var s))
                {
                    c.TotalGenerated = s.gen;
                    c.TotalRedeemed = s.red;
                }
            }
        }
        catch { }

        return list;
    }

    public async Task<CouponCampaign?> GetCampaignByIdAsync(int campaignId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT CampaignId, CampaignName, Description, CouponType, DiscountValue,
                   MinBillAmount, MaxDiscountAmount, ValidFrom, ValidTo, IsActive,
                   AutoTriggerType, TriggerThresholdAmount, CodePrefix, WhatsAppTemplate,
                   CreatedBy, CreatedAt, WhatsAppTemplateName, WhatsAppParameters
            FROM [CouponCampaign]
            WHERE CampaignId = ?
            """;

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = new OleDbCommand(sql, connection);
        command.Parameters.Add(new OleDbParameter("?", OleDbType.Integer) { Value = campaignId });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadCampaign(reader) : null;
    }

    public async Task<int> SaveCampaignAsync(CouponCampaign campaign, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO [CouponCampaign] (
                CampaignName, Description, CouponType, DiscountValue, MinBillAmount, MaxDiscountAmount,
                ValidFrom, ValidTo, IsActive, AutoTriggerType, TriggerThresholdAmount, CodePrefix,
                WhatsAppTemplate, CreatedBy, CreatedAt, WhatsAppTemplateName, WhatsAppParameters
            )
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            """;

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = new OleDbCommand(sql, connection);
        AddCampaignParameters(command, campaign, includeCreatedAt: true);
        await command.ExecuteNonQueryAsync(cancellationToken);

        await using var idCmd = new OleDbCommand("SELECT @@IDENTITY", connection);
        var id = await idCmd.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(id);
    }

    public async Task UpdateCampaignAsync(CouponCampaign campaign, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE [CouponCampaign] SET
                CampaignName = ?, Description = ?, CouponType = ?, DiscountValue = ?,
                MinBillAmount = ?, MaxDiscountAmount = ?, ValidFrom = ?, ValidTo = ?,
                IsActive = ?, AutoTriggerType = ?, TriggerThresholdAmount = ?, CodePrefix = ?,
                WhatsAppTemplate = ?, WhatsAppTemplateName = ?, WhatsAppParameters = ?
            WHERE CampaignId = ?
            """;

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = new OleDbCommand(sql, connection);
        command.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = campaign.CampaignName ?? string.Empty });
        command.Parameters.Add(new OleDbParameter("?", OleDbType.LongVarWChar) { Value = (object?)campaign.Description ?? DBNull.Value });
        command.Parameters.Add(new OleDbParameter("?", OleDbType.Integer) { Value = (int)campaign.CouponType });
        command.Parameters.Add(new OleDbParameter("?", OleDbType.Currency) { Value = campaign.DiscountValue });
        command.Parameters.Add(new OleDbParameter("?", OleDbType.Currency) { Value = (object?)campaign.MinBillAmount ?? DBNull.Value });
        command.Parameters.Add(new OleDbParameter("?", OleDbType.Currency) { Value = (object?)campaign.MaxDiscountAmount ?? DBNull.Value });
        command.Parameters.Add(new OleDbParameter("?", OleDbType.Date) { Value = campaign.ValidFrom });
        command.Parameters.Add(new OleDbParameter("?", OleDbType.Date) { Value = campaign.ValidTo });
        command.Parameters.Add(new OleDbParameter("?", OleDbType.Boolean) { Value = campaign.IsActive });
        command.Parameters.Add(new OleDbParameter("?", OleDbType.Integer) { Value = (int)campaign.AutoTriggerType });
        command.Parameters.Add(new OleDbParameter("?", OleDbType.Currency) { Value = (object?)campaign.TriggerThresholdAmount ?? DBNull.Value });
        command.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = campaign.CodePrefix ?? "CPN" });
        command.Parameters.Add(new OleDbParameter("?", OleDbType.LongVarWChar) { Value = (object?)campaign.WhatsAppTemplate ?? DBNull.Value });
        command.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = (object?)campaign.WhatsAppTemplateName ?? DBNull.Value });
        command.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = (object?)campaign.WhatsAppParameters ?? DBNull.Value });
        command.Parameters.Add(new OleDbParameter("?", OleDbType.Integer) { Value = campaign.CampaignId });

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteCampaignAsync(int campaignId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var cmd = new OleDbCommand("DELETE FROM [CouponCampaign] WHERE CampaignId = ?", connection);
        cmd.Parameters.Add(new OleDbParameter("?", OleDbType.Integer) { Value = campaignId });
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<Coupon?> GetCouponByCodeAsync(string couponCode, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT CouponId, CampaignId, CampaignName, CouponCode, CustomerMobile, CustomerName,
                   CouponType, DiscountValue, MinBillAmount, MaxDiscountAmount, ValidFrom, ValidTo,
                   MaxUsageCount, UsageCount, Status, Channel, DispatchStatus, DispatchedAt,
                   DispatchError, WhatsAppMessageId, DeliveryStatus, DeliveredAt, ReadAt,
                   RequireOtp, OtpCode, Notes, CreatedBy, CreatedAt
            FROM [Coupon]
            WHERE UCASE(CouponCode) = ?
            """;

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = new OleDbCommand(sql, connection);
        command.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = (couponCode ?? string.Empty).Trim().ToUpperInvariant() });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadCoupon(reader) : null;
    }

    public async Task<IReadOnlyList<Coupon>> GetActiveCouponsForCustomerAsync(string customerMobile, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT CouponId, CampaignId, CampaignName, CouponCode, CustomerMobile, CustomerName,
                   CouponType, DiscountValue, MinBillAmount, MaxDiscountAmount, ValidFrom, ValidTo,
                   MaxUsageCount, UsageCount, Status, Channel, DispatchStatus, DispatchedAt,
                   DispatchError, WhatsAppMessageId, DeliveryStatus, DeliveredAt, ReadAt,
                   RequireOtp, OtpCode, Notes, CreatedBy, CreatedAt
            FROM [Coupon]
            WHERE Status = 0 AND CustomerMobile LIKE ?
            ORDER BY CouponId DESC
            """;

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = new OleDbCommand(sql, connection);
        command.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = $"%{customerMobile.Trim()}%" });

        var list = new List<Coupon>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(ReadCoupon(reader));
        }
        return list;
    }

    public async Task<IReadOnlyList<Coupon>> GetCouponsAsync(int? campaignId, string? status, string? search, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT CouponId, CampaignId, CampaignName, CouponCode, CustomerMobile, CustomerName,
                   CouponType, DiscountValue, MinBillAmount, MaxDiscountAmount, ValidFrom, ValidTo,
                   MaxUsageCount, UsageCount, Status, Channel, DispatchStatus, DispatchedAt,
                   DispatchError, WhatsAppMessageId, DeliveryStatus, DeliveredAt, ReadAt,
                   RequireOtp, OtpCode, Notes, CreatedBy, CreatedAt
            FROM [Coupon]
            ORDER BY CouponId DESC
            """;

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = new OleDbCommand(sql, connection);

        var all = new List<Coupon>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var c = ReadCoupon(reader);
            if (campaignId.HasValue && c.CampaignId != campaignId.Value) continue;
            if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "All", StringComparison.OrdinalIgnoreCase) &&
                Enum.TryParse<CouponStatus>(status, true, out var st) && c.Status != st) continue;
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                bool match = c.CouponCode.Contains(s, StringComparison.OrdinalIgnoreCase)
                          || (c.CustomerName?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false)
                          || (c.CustomerMobile?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false)
                          || c.CampaignName.Contains(s, StringComparison.OrdinalIgnoreCase);
                if (!match) continue;
            }
            all.Add(c);
        }

        return all.Skip((Math.Max(1, page) - 1) * pageSize).Take(pageSize).ToList();
    }

    public async Task<int> GetCouponsCountAsync(int? campaignId, string? status, string? search, CancellationToken cancellationToken = default)
    {
        var list = await GetCouponsAsync(campaignId, status, search, 1, int.MaxValue, cancellationToken);
        return list.Count;
    }

    public async Task SaveCouponsBatchAsync(IEnumerable<Coupon> coupons, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO [Coupon] (
                CampaignId, CampaignName, CouponCode, CustomerMobile, CustomerName,
                CouponType, DiscountValue, MinBillAmount, MaxDiscountAmount, ValidFrom, ValidTo,
                MaxUsageCount, UsageCount, Status, Channel, DispatchStatus, DispatchedAt,
                DispatchError, WhatsAppMessageId, DeliveryStatus, DeliveredAt, ReadAt,
                RequireOtp, OtpCode, Notes, CreatedBy, CreatedAt
            )
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            """;

        await using var connection = await OpenConnectionAsync(cancellationToken);
        foreach (var c in coupons)
        {
            await using var cmd = new OleDbCommand(sql, connection);
            cmd.Parameters.Add(new OleDbParameter("?", OleDbType.Integer) { Value = (object?)c.CampaignId ?? DBNull.Value });
            cmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = c.CampaignName ?? string.Empty });
            cmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = c.CouponCode ?? string.Empty });
            cmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = (object?)c.CustomerMobile ?? DBNull.Value });
            cmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = (object?)c.CustomerName ?? DBNull.Value });
            cmd.Parameters.Add(new OleDbParameter("?", OleDbType.Integer) { Value = (int)c.CouponType });
            cmd.Parameters.Add(new OleDbParameter("?", OleDbType.Currency) { Value = c.DiscountValue });
            cmd.Parameters.Add(new OleDbParameter("?", OleDbType.Currency) { Value = (object?)c.MinBillAmount ?? DBNull.Value });
            cmd.Parameters.Add(new OleDbParameter("?", OleDbType.Currency) { Value = (object?)c.MaxDiscountAmount ?? DBNull.Value });
            cmd.Parameters.Add(new OleDbParameter("?", OleDbType.Date) { Value = c.ValidFrom });
            cmd.Parameters.Add(new OleDbParameter("?", OleDbType.Date) { Value = c.ValidTo });
            cmd.Parameters.Add(new OleDbParameter("?", OleDbType.Integer) { Value = c.MaxUsageCount });
            cmd.Parameters.Add(new OleDbParameter("?", OleDbType.Integer) { Value = c.UsageCount });
            cmd.Parameters.Add(new OleDbParameter("?", OleDbType.Integer) { Value = (int)c.Status });
            cmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = c.Channel ?? "Direct" });
            cmd.Parameters.Add(new OleDbParameter("?", OleDbType.Integer) { Value = (int)c.DispatchStatus });
            cmd.Parameters.Add(new OleDbParameter("?", OleDbType.Date) { Value = (object?)c.DispatchedAt ?? DBNull.Value });
            cmd.Parameters.Add(new OleDbParameter("?", OleDbType.LongVarWChar) { Value = (object?)c.DispatchError ?? DBNull.Value });
            cmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = (object?)c.WhatsAppMessageId ?? DBNull.Value });
            cmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = c.DeliveryStatus ?? "Pending" });
            cmd.Parameters.Add(new OleDbParameter("?", OleDbType.Date) { Value = (object?)c.DeliveredAt ?? DBNull.Value });
            cmd.Parameters.Add(new OleDbParameter("?", OleDbType.Date) { Value = (object?)c.ReadAt ?? DBNull.Value });
            cmd.Parameters.Add(new OleDbParameter("?", OleDbType.Boolean) { Value = c.RequireOtp });
            cmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = (object?)c.OtpCode ?? DBNull.Value });
            cmd.Parameters.Add(new OleDbParameter("?", OleDbType.LongVarWChar) { Value = (object?)c.Notes ?? DBNull.Value });
            cmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = c.CreatedBy ?? string.Empty });
            cmd.Parameters.Add(new OleDbParameter("?", OleDbType.Date) { Value = c.CreatedAt == default ? DateTime.Now : c.CreatedAt });

            await cmd.ExecuteNonQueryAsync(cancellationToken);

            await using var idCmd = new OleDbCommand("SELECT @@IDENTITY", connection);
            c.CouponId = Convert.ToInt32(await idCmd.ExecuteScalarAsync(cancellationToken));
        }
    }

    public async Task UpdateCouponStatusAsync(int couponId, CouponStatus status, int usageIncrement = 0, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE [Coupon] SET Status = ?, UsageCount = UsageCount + ? WHERE CouponId = ?";
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var cmd = new OleDbCommand(sql, connection);
        cmd.Parameters.Add(new OleDbParameter("?", OleDbType.Integer) { Value = (int)status });
        cmd.Parameters.Add(new OleDbParameter("?", OleDbType.Integer) { Value = usageIncrement });
        cmd.Parameters.Add(new OleDbParameter("?", OleDbType.Integer) { Value = couponId });
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateCouponDispatchStatusAsync(int couponId, CouponDispatchStatus status, string? error = null, string? whatsAppMessageId = null, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE [Coupon] SET
                DispatchStatus = ?, DispatchedAt = ?, DispatchError = ?,
                WhatsAppMessageId = IIF(? IS NULL, WhatsAppMessageId, ?),
                DeliveryStatus = IIF(? = 1, 'Sent', IIF(? = 2, 'Failed', DeliveryStatus))
            WHERE CouponId = ?
            """;

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var cmd = new OleDbCommand(sql, connection);
        cmd.Parameters.Add(new OleDbParameter("?", OleDbType.Integer) { Value = (int)status });
        cmd.Parameters.Add(new OleDbParameter("?", OleDbType.Date) { Value = DateTime.Now });
        cmd.Parameters.Add(new OleDbParameter("?", OleDbType.LongVarWChar) { Value = (object?)error ?? DBNull.Value });
        cmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = (object?)whatsAppMessageId ?? DBNull.Value });
        cmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = (object?)whatsAppMessageId ?? DBNull.Value });
        cmd.Parameters.Add(new OleDbParameter("?", OleDbType.Integer) { Value = (int)status });
        cmd.Parameters.Add(new OleDbParameter("?", OleDbType.Integer) { Value = (int)status });
        cmd.Parameters.Add(new OleDbParameter("?", OleDbType.Integer) { Value = couponId });
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<Coupon?> GetCouponByMessageIdAsync(string messageId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT CouponId, CampaignId, CampaignName, CouponCode, CustomerMobile, CustomerName,
                   CouponType, DiscountValue, MinBillAmount, MaxDiscountAmount, ValidFrom, ValidTo,
                   MaxUsageCount, UsageCount, Status, Channel, DispatchStatus, DispatchedAt,
                   DispatchError, WhatsAppMessageId, DeliveryStatus, DeliveredAt, ReadAt,
                   RequireOtp, OtpCode, Notes, CreatedBy, CreatedAt
            FROM [Coupon]
            WHERE WhatsAppMessageId = ?
            """;

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var cmd = new OleDbCommand(sql, connection);
        cmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = messageId });
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadCoupon(reader) : null;
    }

    public async Task<bool> UpdateCouponDeliveryStatusAsync(string messageId, string status, DateTime? timestamp = null, string? error = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(messageId)) return false;

        var normalized = (status ?? string.Empty).Trim().ToLowerInvariant();
        var ts = timestamp ?? DateTime.Now;
        var sql = normalized switch
        {
            "delivered" => "UPDATE [Coupon] SET DeliveryStatus = 'Delivered', DeliveredAt = ? WHERE WhatsAppMessageId = ?",
            "read" => "UPDATE [Coupon] SET DeliveryStatus = 'Read', ReadAt = ?, DeliveredAt = IIF(DeliveredAt IS NULL, ?, DeliveredAt) WHERE WhatsAppMessageId = ?",
            "failed" => "UPDATE [Coupon] SET DeliveryStatus = 'Failed', DispatchStatus = 2, DispatchError = ? WHERE WhatsAppMessageId = ?",
            _ => "UPDATE [Coupon] SET DeliveryStatus = ? WHERE WhatsAppMessageId = ?"
        };

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var cmd = new OleDbCommand(sql, connection);
        switch (normalized)
        {
            case "delivered":
                cmd.Parameters.Add(new OleDbParameter("?", OleDbType.Date) { Value = ts });
                cmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = messageId });
                break;
            case "read":
                cmd.Parameters.Add(new OleDbParameter("?", OleDbType.Date) { Value = ts });
                cmd.Parameters.Add(new OleDbParameter("?", OleDbType.Date) { Value = ts });
                cmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = messageId });
                break;
            case "failed":
                cmd.Parameters.Add(new OleDbParameter("?", OleDbType.LongVarWChar) { Value = (object?)error ?? "Delivery failed" });
                cmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = messageId });
                break;
            default:
                cmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = status ?? "Sent" });
                cmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = messageId });
                break;
        }

        return await cmd.ExecuteNonQueryAsync(cancellationToken) > 0;
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

    public async Task<int> RecordRedemptionAsync(CouponRedemption redemption, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO [CouponRedemption] (
                CouponId, CouponCode, BillNo, CustomerMobile, CustomerName, BillGrossAmount,
                DiscountAmount, BillNetAmount, [Counter], Store, RedeemedBy, RedeemedAt,
                IsReverted, RevertedAt, RevertedReason
            )
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            """;

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var cmd = new OleDbCommand(sql, connection);
        cmd.Parameters.Add(new OleDbParameter("?", OleDbType.Integer) { Value = redemption.CouponId });
        cmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = redemption.CouponCode ?? string.Empty });
        cmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = redemption.BillNo ?? string.Empty });
        cmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = (object?)redemption.CustomerMobile ?? DBNull.Value });
        cmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = (object?)redemption.CustomerName ?? DBNull.Value });
        cmd.Parameters.Add(new OleDbParameter("?", OleDbType.Currency) { Value = redemption.BillGrossAmount });
        cmd.Parameters.Add(new OleDbParameter("?", OleDbType.Currency) { Value = redemption.DiscountAmount });
        cmd.Parameters.Add(new OleDbParameter("?", OleDbType.Currency) { Value = redemption.BillNetAmount });
        cmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = redemption.Counter ?? string.Empty });
        cmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = redemption.Store ?? string.Empty });
        cmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = redemption.RedeemedBy ?? string.Empty });
        cmd.Parameters.Add(new OleDbParameter("?", OleDbType.Date) { Value = DateTime.Now });
        cmd.Parameters.Add(new OleDbParameter("?", OleDbType.Boolean) { Value = false });
        cmd.Parameters.Add(new OleDbParameter("?", OleDbType.Date) { Value = DBNull.Value });
        cmd.Parameters.Add(new OleDbParameter("?", OleDbType.LongVarWChar) { Value = DBNull.Value });

        await cmd.ExecuteNonQueryAsync(cancellationToken);

        await using var idCmd = new OleDbCommand("SELECT @@IDENTITY", connection);
        return Convert.ToInt32(await idCmd.ExecuteScalarAsync(cancellationToken));
    }

    public async Task<bool> RevertRedemptionAsync(string billNo, string? reason, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        var couponIds = new List<int>();
        await using (var findCmd = new OleDbCommand("SELECT CouponId FROM [CouponRedemption] WHERE BillNo = ? AND IsReverted = False", connection))
        {
            findCmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = billNo });
            await using var rdr = await findCmd.ExecuteReaderAsync(cancellationToken);
            while (await rdr.ReadAsync(cancellationToken))
            {
                if (!rdr.IsDBNull(0)) couponIds.Add(rdr.GetInt32(0));
            }
        }

        if (couponIds.Count == 0) return false;

        await using (var updRed = new OleDbCommand("UPDATE [CouponRedemption] SET IsReverted = True, RevertedAt = ?, RevertedReason = ? WHERE BillNo = ? AND IsReverted = False", connection))
        {
            updRed.Parameters.Add(new OleDbParameter("?", OleDbType.Date) { Value = DateTime.Now });
            updRed.Parameters.Add(new OleDbParameter("?", OleDbType.LongVarWChar) { Value = (object?)reason ?? DBNull.Value });
            updRed.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = billNo });
            await updRed.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var cid in couponIds.Distinct())
        {
            await using var updCpn = new OleDbCommand("UPDATE [Coupon] SET UsageCount = IIF(UsageCount > 0, UsageCount - 1, 0), Status = IIF(Status = 1, 0, Status) WHERE CouponId = ?", connection);
            updCpn.Parameters.Add(new OleDbParameter("?", OleDbType.Integer) { Value = cid });
            await updCpn.ExecuteNonQueryAsync(cancellationToken);
        }

        return true;
    }

    public async Task<IReadOnlyList<CouponRedemption>> GetRedemptionsByBillNoAsync(string billNo, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT RedemptionId, CouponId, CouponCode, BillNo, CustomerMobile, CustomerName,
                   BillGrossAmount, DiscountAmount, BillNetAmount, [Counter], Store, RedeemedBy,
                   RedeemedAt, IsReverted, RevertedAt, RevertedReason
            FROM [CouponRedemption]
            WHERE BillNo = ?
            """;

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var cmd = new OleDbCommand(sql, connection);
        cmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = billNo });

        var list = new List<CouponRedemption>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(ReadRedemption(reader));
        }
        return list;
    }

    public async Task<IReadOnlyList<CustomerSegmentationResult>> SearchCustomersForSegmentationAsync(CouponCustomerSegmentFilter filter, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        // Load sales stats from [Bill] by Phone/CustomerName
        var spendByPhone = new Dictionary<string, (decimal spend, int visits, DateTime? lastVisit)>(StringComparer.OrdinalIgnoreCase);
        try
        {
            await using var billCmd = new OleDbCommand(
                "SELECT Phone, SUM(NetAmt), COUNT(*), MAX(BillDate) FROM [Bill] WHERE Phone IS NOT NULL AND Phone <> '' GROUP BY Phone",
                connection);
            await using var billRdr = await billCmd.ExecuteReaderAsync(cancellationToken);
            while (await billRdr.ReadAsync(cancellationToken))
            {
                var ph = billRdr.IsDBNull(0) ? string.Empty : billRdr.GetValue(0)?.ToString()?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(ph)) continue;
                var sp = billRdr.IsDBNull(1) ? 0m : Convert.ToDecimal(billRdr.GetValue(1));
                var vc = billRdr.IsDBNull(2) ? 0 : Convert.ToInt32(billRdr.GetValue(2));
                DateTime? lv = billRdr.IsDBNull(3) ? null : Convert.ToDateTime(billRdr.GetValue(3));
                spendByPhone[ph] = (sp, vc, lv);
            }
        }
        catch { }

        const string sql = "SELECT Code, CompanyName, Mobile, Phone, City, [Group], DOB, DOA FROM [Customers] WHERE Status IS NULL OR Status <> 'Deleted'";
        await using var cmd = new OleDbCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var today = DateTime.Today;
        var list = new List<CustomerSegmentationResult>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var mob = reader.IsDBNull(2) ? string.Empty : reader.GetValue(2)?.ToString()?.Trim() ?? string.Empty;
            var phone = reader.IsDBNull(3) ? string.Empty : reader.GetValue(3)?.ToString()?.Trim() ?? string.Empty;
            var effectiveMobile = !string.IsNullOrWhiteSpace(mob) ? mob : phone;

            if (filter.OnlyWithMobile && string.IsNullOrWhiteSpace(effectiveMobile))
                continue;

            var city = reader.IsDBNull(4) ? string.Empty : reader.GetValue(4)?.ToString()?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(filter.City) && !city.Contains(filter.City.Trim(), StringComparison.OrdinalIgnoreCase))
                continue;

            var group = reader.IsDBNull(5) ? string.Empty : reader.GetValue(5)?.ToString()?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(filter.Group) && !group.Contains(filter.Group.Trim(), StringComparison.OrdinalIgnoreCase))
                continue;

            DateTime? dob = reader.IsDBNull(6) ? null : SafeGetDate(reader.GetValue(6));
            DateTime? doa = reader.IsDBNull(7) ? null : SafeGetDate(reader.GetValue(7));

            if (filter.BirthMonth.HasValue && (dob == null || dob.Value.Month != filter.BirthMonth.Value))
                continue;

            if (filter.UpcomingBirthdayDays.HasValue && !IsUpcomingAnnualEvent(dob, today, filter.UpcomingBirthdayDays.Value))
                continue;

            if (filter.UpcomingAnniversaryDays.HasValue && !IsUpcomingAnnualEvent(doa, today, filter.UpcomingAnniversaryDays.Value))
                continue;

            decimal lifetimeSpend = 0m;
            int visitCount = 0;
            DateTime? lastVisit = null;
            if (!string.IsNullOrWhiteSpace(effectiveMobile) && spendByPhone.TryGetValue(effectiveMobile, out var st))
            {
                lifetimeSpend = st.spend;
                visitCount = st.visits;
                lastVisit = st.lastVisit;
            }
            else if (!string.IsNullOrWhiteSpace(phone) && spendByPhone.TryGetValue(phone, out var st2))
            {
                lifetimeSpend = st2.spend;
                visitCount = st2.visits;
                lastVisit = st2.lastVisit;
            }

            if (filter.MinLifetimeSpend.HasValue && lifetimeSpend < filter.MinLifetimeSpend.Value) continue;
            if (filter.MaxLifetimeSpend.HasValue && lifetimeSpend > filter.MaxLifetimeSpend.Value) continue;
            if (filter.MinVisits.HasValue && visitCount < filter.MinVisits.Value) continue;
            if (filter.MaxVisits.HasValue && visitCount > filter.MaxVisits.Value) continue;

            int? inactiveDays = lastVisit.HasValue ? (int)(today - lastVisit.Value.Date).TotalDays : null;
            if (filter.InactiveDaysMin.HasValue && (!inactiveDays.HasValue || inactiveDays.Value < filter.InactiveDaysMin.Value)) continue;
            if (filter.InactiveDaysMax.HasValue && (!inactiveDays.HasValue || inactiveDays.Value > filter.InactiveDaysMax.Value)) continue;

            list.Add(new CustomerSegmentationResult
            {
                Code = reader.IsDBNull(0) ? string.Empty : reader.GetValue(0)?.ToString()?.Trim() ?? string.Empty,
                CompanyName = reader.IsDBNull(1) ? string.Empty : reader.GetValue(1)?.ToString()?.Trim() ?? string.Empty,
                Mobile = effectiveMobile,
                City = city,
                Group = group,
                LifetimeSpend = lifetimeSpend,
                VisitCount = visitCount,
                LastVisitDate = lastVisit,
                DaysSinceLastVisit = inactiveDays,
                Dob = dob,
                Doa = doa
            });

            if (list.Count >= filter.MaxRecords)
                break;
        }

        return list;
    }

    public async Task<CouponAnalyticsSummary> GetAnalyticsSummaryAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
    {
        var summary = new CouponAnalyticsSummary();
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            await using var cmdCamp = new OleDbCommand("SELECT COUNT(*) FROM [CouponCampaign]", connection);
            summary.TotalCampaigns = Convert.ToInt32(await cmdCamp.ExecuteScalarAsync(cancellationToken));
        }
        catch { }

        try
        {
            await using var cmdCpn = new OleDbCommand("SELECT DispatchStatus, WhatsAppMessageId, DeliveryStatus, DeliveredAt, ReadAt FROM [Coupon]", connection);
            await using var rdrCpn = await cmdCpn.ExecuteReaderAsync(cancellationToken);
            while (await rdrCpn.ReadAsync(cancellationToken))
            {
                summary.TotalCouponsGenerated++;
                var dispatchStatus = rdrCpn.IsDBNull(0) ? 0 : Convert.ToInt32(rdrCpn.GetValue(0));
                var wamid = rdrCpn.IsDBNull(1) ? null : rdrCpn.GetString(1);
                var deliveryStatus = rdrCpn.IsDBNull(2) ? string.Empty : rdrCpn.GetString(2);
                var deliveredAt = rdrCpn.IsDBNull(3) ? (DateTime?)null : rdrCpn.GetDateTime(3);
                var readAt = rdrCpn.IsDBNull(4) ? (DateTime?)null : rdrCpn.GetDateTime(4);

                if (dispatchStatus == 1 || !string.IsNullOrWhiteSpace(wamid))
                    summary.TotalCouponsDispatched++;
                if (deliveryStatus.Equals("delivered", StringComparison.OrdinalIgnoreCase) ||
                    deliveryStatus.Equals("read", StringComparison.OrdinalIgnoreCase) ||
                    deliveredAt.HasValue)
                    summary.TotalCouponsDelivered++;
                if (deliveryStatus.Equals("read", StringComparison.OrdinalIgnoreCase) || readAt.HasValue)
                    summary.TotalCouponsRead++;
            }
        }
        catch { }

        if (summary.TotalCouponsDispatched > 0)
        {
            summary.DeliveryRate = Math.Round((decimal)summary.TotalCouponsDelivered / summary.TotalCouponsDispatched * 100m, 1);
            summary.ReadRate = Math.Round((decimal)summary.TotalCouponsRead / summary.TotalCouponsDispatched * 100m, 1);
        }

        await using var cmdRed = new OleDbCommand("SELECT BillNetAmount, DiscountAmount FROM [CouponRedemption] WHERE IsReverted = False", connection);
        decimal totalSales = 0m, totalDisc = 0m;
        int redeemedCount = 0;

        await using var reader = await cmdRed.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            redeemedCount++;
            totalSales += reader.IsDBNull(0) ? 0m : Convert.ToDecimal(reader.GetValue(0));
            totalDisc += reader.IsDBNull(1) ? 0m : Convert.ToDecimal(reader.GetValue(1));
        }

        summary.TotalCouponsRedeemed = redeemedCount;
        summary.TotalSalesWithCoupons = totalSales;
        summary.TotalDiscountsGiven = totalDisc;
        if (summary.TotalCouponsGenerated > 0)
            summary.OverallRedemptionRate = Math.Round((decimal)redeemedCount / summary.TotalCouponsGenerated * 100m, 2);
        if (redeemedCount > 0)
            summary.AverageBasketValueWithCoupon = Math.Round(totalSales / redeemedCount, 2);
        if (totalDisc > 0)
            summary.RoiRatio = Math.Round(totalSales / totalDisc, 2);

        return summary;
    }

    public async Task<IReadOnlyList<CouponRedemption>> GetRecentRedemptionsAsync(int count = 100, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT RedemptionId, CouponId, CouponCode, BillNo, CustomerMobile, CustomerName,
                   BillGrossAmount, DiscountAmount, BillNetAmount, [Counter], Store, RedeemedBy,
                   RedeemedAt, IsReverted, RevertedAt, RevertedReason
            FROM [CouponRedemption]
            ORDER BY RedemptionId DESC
            """;

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var cmd = new OleDbCommand(sql, connection);

        var list = new List<CouponRedemption>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken) && list.Count < count)
        {
            list.Add(ReadRedemption(reader));
        }
        return list;
    }

    private static DateTime? SafeGetDate(object? value)
    {
        if (value == null || value == DBNull.Value) return null;
        if (value is DateTime dt) return dt;
        return DateTime.TryParse(value.ToString(), out var parsed) ? parsed : null;
    }

    private static bool IsUpcomingAnnualEvent(DateTime? eventDate, DateTime today, int withinDays)
    {
        if (!eventDate.HasValue) return false;
        try
        {
            var next = new DateTime(today.Year, eventDate.Value.Month, Math.Min(eventDate.Value.Day, DateTime.DaysInMonth(today.Year, eventDate.Value.Month)));
            if (next < today)
                next = new DateTime(today.Year + 1, eventDate.Value.Month, Math.Min(eventDate.Value.Day, DateTime.DaysInMonth(today.Year + 1, eventDate.Value.Month)));
            return (next - today).TotalDays <= withinDays;
        }
        catch { return false; }
    }

    private static void AddCampaignParameters(OleDbCommand command, CouponCampaign campaign, bool includeCreatedAt)
    {
        command.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = campaign.CampaignName ?? string.Empty });
        command.Parameters.Add(new OleDbParameter("?", OleDbType.LongVarWChar) { Value = (object?)campaign.Description ?? DBNull.Value });
        command.Parameters.Add(new OleDbParameter("?", OleDbType.Integer) { Value = (int)campaign.CouponType });
        command.Parameters.Add(new OleDbParameter("?", OleDbType.Currency) { Value = campaign.DiscountValue });
        command.Parameters.Add(new OleDbParameter("?", OleDbType.Currency) { Value = (object?)campaign.MinBillAmount ?? DBNull.Value });
        command.Parameters.Add(new OleDbParameter("?", OleDbType.Currency) { Value = (object?)campaign.MaxDiscountAmount ?? DBNull.Value });
        command.Parameters.Add(new OleDbParameter("?", OleDbType.Date) { Value = campaign.ValidFrom });
        command.Parameters.Add(new OleDbParameter("?", OleDbType.Date) { Value = campaign.ValidTo });
        command.Parameters.Add(new OleDbParameter("?", OleDbType.Boolean) { Value = campaign.IsActive });
        command.Parameters.Add(new OleDbParameter("?", OleDbType.Integer) { Value = (int)campaign.AutoTriggerType });
        command.Parameters.Add(new OleDbParameter("?", OleDbType.Currency) { Value = (object?)campaign.TriggerThresholdAmount ?? DBNull.Value });
        command.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = campaign.CodePrefix ?? "CPN" });
        command.Parameters.Add(new OleDbParameter("?", OleDbType.LongVarWChar) { Value = (object?)campaign.WhatsAppTemplate ?? DBNull.Value });
        command.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = campaign.CreatedBy ?? string.Empty });
        if (includeCreatedAt)
            command.Parameters.Add(new OleDbParameter("?", OleDbType.Date) { Value = DateTime.Now });
        command.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = (object?)campaign.WhatsAppTemplateName ?? DBNull.Value });
        command.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = (object?)campaign.WhatsAppParameters ?? DBNull.Value });
    }

    private static CouponCampaign ReadCampaign(IDataRecord r) => new()
    {
        CampaignId = r.GetInt32(0),
        CampaignName = r.IsDBNull(1) ? string.Empty : r.GetString(1),
        Description = r.IsDBNull(2) ? string.Empty : r.GetString(2),
        CouponType = (CouponType)r.GetInt32(3),
        DiscountValue = Convert.ToDecimal(r.GetValue(4)),
        MinBillAmount = r.IsDBNull(5) ? null : Convert.ToDecimal(r.GetValue(5)),
        MaxDiscountAmount = r.IsDBNull(6) ? null : Convert.ToDecimal(r.GetValue(6)),
        ValidFrom = r.GetDateTime(7),
        ValidTo = r.GetDateTime(8),
        IsActive = r.GetBoolean(9),
        AutoTriggerType = (CouponAutoTriggerType)r.GetInt32(10),
        TriggerThresholdAmount = r.IsDBNull(11) ? null : Convert.ToDecimal(r.GetValue(11)),
        CodePrefix = r.IsDBNull(12) ? "CPN" : r.GetString(12),
        WhatsAppTemplate = r.IsDBNull(13) ? string.Empty : r.GetString(13),
        CreatedBy = r.IsDBNull(14) ? string.Empty : r.GetString(14),
        CreatedAt = r.GetDateTime(15),
        WhatsAppTemplateName = r.FieldCount > 16 && !r.IsDBNull(16) ? r.GetString(16) : null,
        WhatsAppParameters = r.FieldCount > 17 && !r.IsDBNull(17) ? r.GetString(17) : null
    };

    private static Coupon ReadCoupon(IDataRecord r) => new()
    {
        CouponId = r.GetInt32(0),
        CampaignId = r.IsDBNull(1) ? null : r.GetInt32(1),
        CampaignName = r.IsDBNull(2) ? string.Empty : r.GetString(2),
        CouponCode = r.GetString(3),
        CustomerMobile = r.IsDBNull(4) ? null : r.GetString(4),
        CustomerName = r.IsDBNull(5) ? null : r.GetString(5),
        CouponType = (CouponType)r.GetInt32(6),
        DiscountValue = Convert.ToDecimal(r.GetValue(7)),
        MinBillAmount = r.IsDBNull(8) ? null : Convert.ToDecimal(r.GetValue(8)),
        MaxDiscountAmount = r.IsDBNull(9) ? null : Convert.ToDecimal(r.GetValue(9)),
        ValidFrom = r.GetDateTime(10),
        ValidTo = r.GetDateTime(11),
        MaxUsageCount = r.GetInt32(12),
        UsageCount = r.GetInt32(13),
        Status = (CouponStatus)r.GetInt32(14),
        Channel = r.IsDBNull(15) ? "Direct" : r.GetString(15),
        DispatchStatus = (CouponDispatchStatus)r.GetInt32(16),
        DispatchedAt = r.IsDBNull(17) ? null : r.GetDateTime(17),
        DispatchError = r.IsDBNull(18) ? null : r.GetString(18),
        WhatsAppMessageId = r.FieldCount > 19 && !r.IsDBNull(19) ? r.GetString(19) : null,
        DeliveryStatus = r.FieldCount > 20 && !r.IsDBNull(20) ? r.GetString(20) : "Pending",
        DeliveredAt = r.FieldCount > 21 && !r.IsDBNull(21) ? r.GetDateTime(21) : null,
        ReadAt = r.FieldCount > 22 && !r.IsDBNull(22) ? r.GetDateTime(22) : null,
        RequireOtp = r.FieldCount > 23 && !r.IsDBNull(23) && r.GetBoolean(23),
        OtpCode = r.FieldCount > 24 && !r.IsDBNull(24) ? r.GetString(24) : null,
        Notes = r.FieldCount > 25 && !r.IsDBNull(25) ? r.GetString(25) : null,
        CreatedBy = r.FieldCount > 26 && !r.IsDBNull(26) ? r.GetString(26) : string.Empty,
        CreatedAt = r.FieldCount > 27 && !r.IsDBNull(27) ? r.GetDateTime(27) : DateTime.Now
    };

    private static CouponRedemption ReadRedemption(IDataRecord r) => new()
    {
        RedemptionId = r.GetInt32(0),
        CouponId = r.GetInt32(1),
        CouponCode = r.GetString(2),
        BillNo = r.GetString(3),
        CustomerMobile = r.IsDBNull(4) ? string.Empty : r.GetString(4),
        CustomerName = r.IsDBNull(5) ? string.Empty : r.GetString(5),
        BillGrossAmount = Convert.ToDecimal(r.GetValue(6)),
        DiscountAmount = Convert.ToDecimal(r.GetValue(7)),
        BillNetAmount = Convert.ToDecimal(r.GetValue(8)),
        Counter = r.IsDBNull(9) ? string.Empty : r.GetString(9),
        Store = r.IsDBNull(10) ? string.Empty : r.GetString(10),
        RedeemedBy = r.IsDBNull(11) ? string.Empty : r.GetString(11),
        RedeemedAt = r.GetDateTime(12),
        IsReverted = !r.IsDBNull(13) && r.GetBoolean(13),
        RevertedAt = r.IsDBNull(14) ? null : r.GetDateTime(14),
        RevertedReason = r.IsDBNull(15) ? null : r.GetString(15)
    };
}
