using System.Security.Cryptography;
using System.Text;

namespace ConceptJewelleryPro.Application.Licensing;

public static class LicenseSigner
{
    // Shared Vendor Public Key matching E:\Repos\ConceptS License Manager (verify-only)
    public const string PublicKeyXml = "<RSAKeyValue><Modulus>rkYTNibNzL5/IHkVSU0pKUFJy2Iyx/uYPOLCVUzo7jnW/v+EnyPctScuHUDOFeWyT3amoFORg8Nz9V34X6PdiVGsjgnUrb3Ul5PaOI/Qf4x4fOsVvkW4ldVqxbHq22PISihaPfPpbgvrz1ZP0qNnlaT85z3MEmT+Ogz2OZ/12B4c5jWOJFIf98QWS8YpuSDgEu1YB5WAVKdhxtJ7LUWNXwJDENlMO19CegIzDThCgF65yl8j8GdLP3rG03KWu1D8huuXO8EdnPHWraFaAUDXFM78RDh8qH8JFdNaWJE+AXa2/TM1hrDL3bLvlwALCGYlQm7VCVHCbloAoDNC+dzlCQ==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

    public static string NormalizeModules(string? enabledModules)
    {
        if (string.IsNullOrWhiteSpace(enabledModules))
            return string.Empty;

        var parts = enabledModules
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(m => m.ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(m => m, StringComparer.OrdinalIgnoreCase);

        return string.Join(",", parts);
    }

    public static string CreateCanonicalPayload(string companyName, DateTime validFrom, DateTime? validTo, string? licenseType, string? edition = null, string? enabledModules = null)
    {
        var normalizedCompany = (companyName ?? string.Empty).Trim().ToUpperInvariant();
        var fromStr = validFrom.ToString("yyyy-MM-dd");
        var toStr = validTo.HasValue ? validTo.Value.ToString("yyyy-MM-dd") : "PERPETUAL";
        var typeStr = string.IsNullOrWhiteSpace(licenseType) ? "SUBSCRIPTION" : licenseType.Trim().ToUpperInvariant();
        var editionStr = string.IsNullOrWhiteSpace(edition) ? "SINGLEHO" : edition.Trim().ToUpperInvariant();
        var modulesStr = NormalizeModules(enabledModules);

        return string.IsNullOrEmpty(modulesStr)
            ? $"{normalizedCompany}|{fromStr}|{toStr}|{typeStr}|{editionStr}"
            : $"{normalizedCompany}|{fromStr}|{toStr}|{typeStr}|{editionStr}|{modulesStr}";
    }

    public static string CreateLegacyCanonicalPayload(string companyName, DateTime validFrom, DateTime? validTo, string? licenseType)
    {
        var normalizedCompany = (companyName ?? string.Empty).Trim().ToUpperInvariant();
        var fromStr = validFrom.ToString("yyyy-MM-dd");
        var toStr = validTo.HasValue ? validTo.Value.ToString("yyyy-MM-dd") : "PERPETUAL";
        var typeStr = string.IsNullOrWhiteSpace(licenseType) ? "SUBSCRIPTION" : licenseType.Trim().ToUpperInvariant();

        return $"{normalizedCompany}|{fromStr}|{toStr}|{typeStr}";
    }

    public static bool VerifySignature(string companyName, DateTime validFrom, DateTime? validTo, string? licenseType, string? edition, string? base64Signature)
    {
        return VerifySignature(companyName, validFrom, validTo, licenseType, edition, null, base64Signature);
    }

    public static bool VerifySignature(string companyName, DateTime validFrom, DateTime? validTo, string? licenseType, string? edition, string? enabledModules, string? base64Signature)
    {
        if (string.IsNullOrWhiteSpace(base64Signature))
            return false;

        try
        {
            var normalizedModules = NormalizeModules(enabledModules);

            var payload = CreateCanonicalPayload(companyName, validFrom, validTo, licenseType, edition, normalizedModules);
            if (VerifyPayload(payload, base64Signature))
                return true;

            if (string.IsNullOrEmpty(normalizedModules))
            {
                var normEdition = (edition ?? string.Empty).Trim().ToUpperInvariant();
                if (normEdition is "" or "SINGLEHO")
                {
                    var legacyPayload = CreateLegacyCanonicalPayload(companyName, validFrom, validTo, licenseType);
                    if (VerifyPayload(legacyPayload, base64Signature))
                        return true;
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool VerifyPayload(string payload, string base64Signature)
    {
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var signatureBytes = Convert.FromBase64String(base64Signature);

        using var rsa = RSA.Create();
        rsa.FromXmlString(PublicKeyXml);

        return rsa.VerifyData(
            payloadBytes,
            signatureBytes,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
    }
}
