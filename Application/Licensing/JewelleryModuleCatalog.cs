namespace ConceptJewelleryPro.Application.Licensing;

public sealed record JewelleryModuleDefinition(string Code, string DisplayName, string Description);

public static class JewelleryModuleCatalog
{
    public const string Core = "CORE";
    public const string Barcode = "BARCODE";
    public const string CatalogueAi = "CATALOGUE_AI";
    public const string Orders = "ORDERS";
    public const string Karighar = "KARIGHAR";
    public const string Schemes = "SCHEMES";
    public const string CouponsWhatsApp = "COUPONS_WHATSAPP";
    public const string Banking = "BANKING";
    public const string GstCompliance = "GST_COMPLIANCE";
    public const string MultiBranch = "MULTIBRANCH";
    public const string Audit = "AUDIT";

    public static readonly IReadOnlyList<JewelleryModuleDefinition> All =
    [
        new(Core, "Core ERP (Base)", "Accounts, Billing, Purchases, Vouchers, Ledgers, Financials & Settings"),
        new(Barcode, "Barcode & Stock Inventory", "Single/Bulk Barcode Creation, Non-Barcode Stock, Box Master & Stock Reports"),
        new(CatalogueAi, "Digital Catalogue & AI Studio", "Jewellery Photo Catalogue, Weight Filters & AI Model Enhancement"),
        new(Orders, "Customer Orders", "Custom Jewellery Orders, Specifications & Delivery Tracking"),
        new(Karighar, "Karighar & Hire Job-Work", "Karighar Issue/Receive & Artisan Hire Management"),
        new(Schemes, "Gold Saving Schemes", "Scheme Creation, Monthly Installment Receipts & Due List"),
        new(CouponsWhatsApp, "Coupon Engine & WhatsApp", "Promotional Coupon Campaigns, Targeting & WhatsApp Dispatch"),
        new(Banking, "Bank & Card Reconciliation", "Bank Statement BRS & Card Swipe Settlement Reconciliation"),
        new(GstCompliance, "GST Returns & E-Invoicing", "GSTR-1, GSTR-3B, GSTR-2 & GSTR-2B Reconciliation"),
        new(MultiBranch, "Multi-Branch & Transfers", "Inter-Store Stock Transfers & Branch Operations"),
        new(Audit, "Audit Trail & Security Logs", "Full Modification & Deletion Audit Reports")
    ];

    public static string GetDisplayName(string code)
    {
        var match = All.FirstOrDefault(m => string.Equals(m.Code, code, StringComparison.OrdinalIgnoreCase));
        return match?.DisplayName ?? code;
    }

    /// <summary>
    /// Maps any MainViewModel menu title or navigation alias to its required license module code.
    /// </summary>
    public static string ResolveModuleForScreen(string? moduleTitle)
    {
        if (string.IsNullOrWhiteSpace(moduleTitle))
            return Core;

        var title = moduleTitle.Trim();

        return title.ToLowerInvariant() switch
        {
            // Barcode & Stock Inventory
            "barcode creation" or "barcodecreation" or
            "barcode creation (bulk)" or "barcodecreationbulk" or "bulk barcode creation" or
            "non-barcode stock" or "non barcode stock" or
            "box master" or "boxmaster" or "boxes" or "box" or
            "barcode reports" or "barcode stock reports" or "barcode stock" or "barcode report" or
            "nonbarcode stock reports" or "non-barcode stock reports" or "non-barcode stock report" or
            "non barcode stock report" or "non barcode stock reports" or "item-wise non-barcode stock" or "itemwise nonbarcode stock" or
            "itemtype stock report" or "itemtype stock reports" or "itemtype stock" or "item type stock" or "item type stock report" or "primary stock report"
                => Barcode,

            // Digital Catalogue & AI Studio
            "catalogue" or "catalogue view" or "catalogueview"
                => CatalogueAi,

            // Customer Orders
            "order/customer requirements" or "order / customer requirements" or
            "order" or "orders" or "customer requirements" or "orderrequirement" or
            "orderrequirements" or "customer orders" or "customers order"
                => Orders,

            // Karighar & Hire Job-Work
            "karighar issue" or "metal issued" or "metalissued" or
            "karighar received" or "karighar receive" or "metal received" or "metalreceived" or
            "hire issue" or "hireissued" or "hire issue entry" or "hireissue" or
            "hire receive" or "hirereceived" or "hire recieve" or "hirerecieve" or "hire receive entry" or "hirereceive"
                => Karighar,

            // Gold Saving Schemes
            "scheme creation" or "schemecreation" or "scheme" or
            "scheme receipts" or "scheme receipt" or "schemereceipts" or "schemereceipt" or "scheme entry" or
            "scheme due list" or "scheme due" or "scheme dues" or "schemeduelist"
                => Schemes,

            // Coupon Engine & WhatsApp
            "coupon engine" or "couponengine" or "coupons"
                => CouponsWhatsApp,

            // Bank & Card Reconciliation
            "bank reconciliation" or "bankreconciliation" or "brs" or "bank statement reconciliation" or
            "card reconciliation" or "cardreconciliation" or "card adjustment" or "cardadjustment" or
            "card adjustment receipt" or "cardadjustmentreceipt"
                => Banking,

            // GST Compliance
            "gst reports & reconciliation" or "gst reports" or "gst report" or "gst reconciliation" or
            "gstr-1" or "gstr-3b" or "gstr-2" or "gst" or "gstreports"
                => GstCompliance,

            // Multi-Branch & Transfers
            "stock transfer" or "stocktransfer" or "stock transfers"
                => MultiBranch,

            // Audit Trail
            "audit report" or "audit reports" or "audit log" or "audit logs" or "auditreport" or "audit"
                => Audit,

            // Everything else is Core ERP
            _ => Core
        };
    }

    public static IReadOnlySet<string> ParseEnabledModules(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            // Empty/null means all modules enabled (backward compatible with existing licenses)
            return All.Select(m => m.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        var set = csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(m => m.ToUpperInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Core ERP is always included
        set.Add(Core);
        return set;
    }

    public static string FormatModulesForDisplay(string? csv)
    {
        var enabled = ParseEnabledModules(csv);
        if (enabled.Count >= All.Count)
            return "All Modules Unlocked";

        var names = All
            .Where(m => enabled.Contains(m.Code))
            .Select(m => m.DisplayName);

        return string.Join("  •  ", names);
    }
}
