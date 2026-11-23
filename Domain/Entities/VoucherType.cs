namespace ConceptJewelleryPro.Domain.Entities;

public static class VoucherType
{
    public const string Payments = "PAYMENTS";
    public const string Receipts = "RECIEPTS";
    public const string Journal = "JOURNAL";
    public const string SchemeEntry = "SCHEME ENTRY";

    public const string PrefixPayments = "PM";
    public const string PrefixReceipts = "RC";
    public const string PrefixJournal = "JV";
    public const string PrefixSchemeEntry = "SE";

    public static string GetPrefix(string vouType)
    {
        return vouType?.Trim().ToUpperInvariant() switch
        {
            Payments => PrefixPayments,
            Receipts or "RECEIPTS" => PrefixReceipts,
            Journal => PrefixJournal,
            SchemeEntry => PrefixSchemeEntry,
            _ => "VO"
        };
    }

    public static string GetDisplayName(string vouType)
    {
        return vouType?.Trim().ToUpperInvariant() switch
        {
            Payments => "Payments",
            Receipts or "RECEIPTS" => "Receipts",
            Journal => "Journal",
            SchemeEntry => "Scheme Entry",
            _ => vouType ?? "Voucher"
        };
    }
}
