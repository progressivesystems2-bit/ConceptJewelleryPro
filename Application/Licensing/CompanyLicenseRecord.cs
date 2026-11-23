using System.Text.Json.Serialization;

namespace ConceptJewelleryPro.Application.Licensing;

public sealed class CompanyLicenseRecord
{
    [JsonPropertyName("id")]
    public Guid? Id { get; set; }

    [JsonPropertyName("company_name")]
    public string CompanyName { get; set; } = string.Empty;

    [JsonPropertyName("product_name")]
    public string ProductName { get; set; } = "ConceptJewelleryPro";

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; } = true;

    [JsonPropertyName("valid_from")]
    public DateTime ValidFrom { get; set; } = DateTime.Today;

    [JsonPropertyName("valid_to")]
    public DateTime? ValidTo { get; set; }

    [JsonPropertyName("license_type")]
    public string LicenseType { get; set; } = "Subscription";

    [JsonPropertyName("edition")]
    public string Edition { get; set; } = "SingleHO";

    [JsonPropertyName("max_branches")]
    public int MaxBranches { get; set; } = 1;

    [JsonPropertyName("max_nodes")]
    public int MaxNodes { get; set; } = 1;

    [JsonPropertyName("contact_person")]
    public string? ContactPerson { get; set; }

    [JsonPropertyName("contact_phone")]
    public string? ContactPhone { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("signature")]
    public string Signature { get; set; } = string.Empty;
}

public sealed class LicenseNodeRecord
{
    [JsonPropertyName("id")]
    public Guid? Id { get; set; }

    [JsonPropertyName("license_id")]
    public Guid? LicenseId { get; set; }

    [JsonPropertyName("company_name")]
    public string CompanyName { get; set; } = string.Empty;

    [JsonPropertyName("machine_id")]
    public string MachineId { get; set; } = string.Empty;

    [JsonPropertyName("machine_name")]
    public string MachineName { get; set; } = string.Empty;

    [JsonPropertyName("os_user")]
    public string? OsUser { get; set; }

    [JsonPropertyName("app_version")]
    public string? AppVersion { get; set; }

    [JsonPropertyName("first_registered_at")]
    public DateTime? FirstRegisteredAt { get; set; }

    [JsonPropertyName("last_active_at")]
    public DateTime? LastActiveAt { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; } = true;

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}
