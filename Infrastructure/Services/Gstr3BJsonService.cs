using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ConceptJewelleryPro.Domain.Entities.GST;

namespace ConceptJewelleryPro.Infrastructure.Services;

#region GSTR-3B GSTN Offline Utility JSON DTOs

/// <summary>
/// Root DTO strictly adhering to GSTN GSTR-3B offline utility JSON schema.
/// Uploaded to GST Portal (gst.gov.in) -> Returns Dashboard -> GSTR-3B -> Prepare Offline.
/// </summary>
public sealed class Gstr3BJsonDto
{
    [JsonPropertyName("gstin")]
    [JsonPropertyOrder(1)]
    public string Gstin { get; set; } = string.Empty;

    [JsonPropertyName("ret_period")]
    [JsonPropertyOrder(2)]
    public string RetPeriod { get; set; } = string.Empty;

    [JsonPropertyName("sup_details")]
    [JsonPropertyOrder(3)]
    public Gstr3BSupDetailsDto SupDetails { get; set; } = new();

    [JsonPropertyName("itc_elg")]
    [JsonPropertyOrder(4)]
    public Gstr3BItcElgDto ItcElg { get; set; } = new();

    [JsonPropertyName("inward_sup")]
    [JsonPropertyOrder(5)]
    public Gstr3BInwardSupDto InwardSup { get; set; } = new();

    [JsonPropertyName("intr_ltfee")]
    [JsonPropertyOrder(6)]
    public Gstr3BIntrLtfeeDto IntrLtfee { get; set; } = new();

    [JsonPropertyName("inter_sup")]
    [JsonPropertyOrder(7)]
    public Gstr3BInterSupDto InterSup { get; set; } = new();
}

/// <summary>
/// Table 3.1: Details of Outward Supplies and Inward supplies liable to reverse charge.
/// Each sub-category MUST contain all 5 fields (txval, iamt, camt, samt, csamt) for portal schema validation.
/// </summary>
public sealed class Gstr3BSupDetailsDto
{
    [JsonPropertyName("osup_det")]
    [JsonPropertyOrder(1)]
    public Gstr3BTaxDetailDto OsupDet { get; set; } = new();

    [JsonPropertyName("osup_zero")]
    [JsonPropertyOrder(2)]
    public Gstr3BTaxDetailDto OsupZero { get; set; } = new();

    [JsonPropertyName("osup_nil_exmp")]
    [JsonPropertyOrder(3)]
    public Gstr3BTaxDetailDto OsupNilExmp { get; set; } = new();

    [JsonPropertyName("isup_rev")]
    [JsonPropertyOrder(4)]
    public Gstr3BTaxDetailDto IsupRev { get; set; } = new();

    [JsonPropertyName("osup_nongst")]
    [JsonPropertyOrder(5)]
    public Gstr3BTaxDetailDto OsupNongst { get; set; } = new();
}

public sealed class Gstr3BTaxDetailDto
{
    [JsonPropertyName("txval")]
    [JsonPropertyOrder(1)]
    public decimal Txval { get; set; }

    [JsonPropertyName("iamt")]
    [JsonPropertyOrder(2)]
    public decimal Iamt { get; set; }

    [JsonPropertyName("camt")]
    [JsonPropertyOrder(3)]
    public decimal Camt { get; set; }

    [JsonPropertyName("samt")]
    [JsonPropertyOrder(4)]
    public decimal Samt { get; set; }

    [JsonPropertyName("csamt")]
    [JsonPropertyOrder(5)]
    public decimal Csamt { get; set; }
}

/// <summary>
/// Table 4: Eligible Input Tax Credit (ITC).
/// </summary>
public sealed class Gstr3BItcElgDto
{
    [JsonPropertyName("itc_avl")]
    [JsonPropertyOrder(1)]
    public List<Gstr3BItcItemDto> ItcAvl { get; set; } = new();

    [JsonPropertyName("itc_rev")]
    [JsonPropertyOrder(2)]
    public List<Gstr3BItcItemDto> ItcRev { get; set; } = new();

    [JsonPropertyName("itc_net")]
    [JsonPropertyOrder(3)]
    public Gstr3BTaxAmountDto ItcNet { get; set; } = new();

    [JsonPropertyName("itc_inelg")]
    [JsonPropertyOrder(4)]
    public List<Gstr3BItcItemDto> ItcInelg { get; set; } = new();
}

public sealed class Gstr3BItcItemDto
{
    [JsonPropertyName("ty")]
    [JsonPropertyOrder(1)]
    public string Ty { get; set; } = string.Empty;

    [JsonPropertyName("iamt")]
    [JsonPropertyOrder(2)]
    public decimal Iamt { get; set; }

    [JsonPropertyName("camt")]
    [JsonPropertyOrder(3)]
    public decimal Camt { get; set; }

    [JsonPropertyName("samt")]
    [JsonPropertyOrder(4)]
    public decimal Samt { get; set; }

    [JsonPropertyName("csamt")]
    [JsonPropertyOrder(5)]
    public decimal Csamt { get; set; }
}

/// <summary>
/// Table 5: Values of exempt, nil-rated and non-GST inward supplies.
/// </summary>
public sealed class Gstr3BInwardSupDto
{
    [JsonPropertyName("isup_details")]
    public List<Gstr3BInwardDetailDto> IsupDetails { get; set; } = new();
}

public sealed class Gstr3BInwardDetailDto
{
    [JsonPropertyName("ty")]
    [JsonPropertyOrder(1)]
    public string Ty { get; set; } = string.Empty;

    [JsonPropertyName("inter")]
    [JsonPropertyOrder(2)]
    public decimal Inter { get; set; }

    [JsonPropertyName("intra")]
    [JsonPropertyOrder(3)]
    public decimal Intra { get; set; }
}

/// <summary>
/// Table 5.1: Interest & Late Fee details.
/// </summary>
public sealed class Gstr3BIntrLtfeeDto
{
    [JsonPropertyName("intr_details")]
    [JsonPropertyOrder(1)]
    public Gstr3BTaxAmountDto IntrDetails { get; set; } = new();

    [JsonPropertyName("ltfee_details")]
    [JsonPropertyOrder(2)]
    public Gstr3BLtfeeDetailsDto LtfeeDetails { get; set; } = new();
}

public sealed class Gstr3BTaxAmountDto
{
    [JsonPropertyName("iamt")]
    [JsonPropertyOrder(1)]
    public decimal Iamt { get; set; }

    [JsonPropertyName("camt")]
    [JsonPropertyOrder(2)]
    public decimal Camt { get; set; }

    [JsonPropertyName("samt")]
    [JsonPropertyOrder(3)]
    public decimal Samt { get; set; }

    [JsonPropertyName("csamt")]
    [JsonPropertyOrder(4)]
    public decimal Csamt { get; set; }
}

/// <summary>
/// Late fee details object. Serializes to {} when empty.
/// </summary>
public sealed class Gstr3BLtfeeDetailsDto
{
}

/// <summary>
/// Table 3.2: Details of Inter-State supplies made to unregistered persons, composition persons, UIN holders.
/// </summary>
public sealed class Gstr3BInterSupDto
{
    [JsonPropertyName("unreg_details")]
    [JsonPropertyOrder(1)]
    public List<Gstr3BPosTaxDto> UnregDetails { get; set; } = new();

    [JsonPropertyName("comp_details")]
    [JsonPropertyOrder(2)]
    public List<Gstr3BPosTaxDto> CompDetails { get; set; } = new();

    [JsonPropertyName("uin_details")]
    [JsonPropertyOrder(3)]
    public List<Gstr3BPosTaxDto> UinDetails { get; set; } = new();
}

public sealed class Gstr3BPosTaxDto
{
    [JsonPropertyName("pos")]
    [JsonPropertyOrder(1)]
    public string Pos { get; set; } = string.Empty;

    [JsonPropertyName("txval")]
    [JsonPropertyOrder(2)]
    public decimal Txval { get; set; }

    [JsonPropertyName("iamt")]
    [JsonPropertyOrder(3)]
    public decimal Iamt { get; set; }
}

#endregion

/// <summary>
/// Service to create and export government-compliant GSTR-3B JSON files for portal upload (gst.gov.in).
/// </summary>
public static class Gstr3BJsonService
{
    private static readonly Regex GstinRegex = new(@"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex RetPeriodRegex = new(@"^(0[1-9]|1[0-2])20[0-9]{2}$", RegexOptions.Compiled);

    public static bool ValidateParameters(string gstin, string retPeriod, out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(gstin))
        {
            errors.Add("GSTIN cannot be empty.");
        }
        else if (gstin.Trim().Length != 15 || !GstinRegex.IsMatch(gstin.Trim()))
        {
            errors.Add($"GSTIN '{gstin}' is not in valid 15-character GST format (e.g., 21AAIFC0436E1Z6).");
        }

        if (string.IsNullOrWhiteSpace(retPeriod))
        {
            errors.Add("Return Period cannot be empty.");
        }
        else if (!RetPeriodRegex.IsMatch(retPeriod.Trim()))
        {
            errors.Add($"Return Period '{retPeriod}' must be in MMYYYY format (e.g., 092026 for September 2026).");
        }

        return errors.Count == 0;
    }

    public static string FormatReturnPeriod(DateTime date)
    {
        return date.ToString("MMyyyy");
    }

    public static Gstr3BJsonDto CreatePayload(Gstr3BReport report, string gstin, string? retPeriod = null)
    {
        var cleanGstin = (gstin ?? string.Empty).Trim().ToUpperInvariant();
        var period = !string.IsNullOrWhiteSpace(retPeriod)
            ? retPeriod.Trim()
            : FormatReturnPeriod(report.FromDate != default ? report.FromDate : DateTime.Today);

        var dto = new Gstr3BJsonDto
        {
            Gstin = cleanGstin,
            RetPeriod = period
        };

        // Table 3.1: Details of Outward Supplies and Inward supplies liable to reverse charge
        dto.SupDetails = new Gstr3BSupDetailsDto
        {
            // 3.1(a) Outward taxable supplies (other than zero rated, nil rated and exempted)
            OsupDet = new Gstr3BTaxDetailDto
            {
                Txval = R(report.OutwardTaxableSupplies.TotalTaxableValue),
                Iamt = R(report.OutwardTaxableSupplies.IntegratedTax),
                Camt = R(report.OutwardTaxableSupplies.CentralTax),
                Samt = R(report.OutwardTaxableSupplies.StateTax),
                Csamt = R(report.OutwardTaxableSupplies.Cess)
            },
            // 3.1(b) Outward taxable supplies (zero rated)
            OsupZero = new Gstr3BTaxDetailDto
            {
                Txval = R(report.OutwardZeroRatedSupplies.TotalTaxableValue),
                Iamt = R(report.OutwardZeroRatedSupplies.IntegratedTax),
                Camt = 0,
                Samt = 0,
                Csamt = R(report.OutwardZeroRatedSupplies.Cess)
            },
            // 3.1(c) Other outward supplies (Nil rated, exempted)
            OsupNilExmp = new Gstr3BTaxDetailDto
            {
                Txval = R(report.OtherOutwardSuppliesNilExempt.TotalTaxableValue),
                Iamt = 0,
                Camt = 0,
                Samt = 0,
                Csamt = 0
            },
            // 3.1(d) Inward supplies (liable to reverse charge)
            IsupRev = new Gstr3BTaxDetailDto
            {
                Txval = R(report.InwardSuppliesReverseCharge.TotalTaxableValue),
                Iamt = R(report.InwardSuppliesReverseCharge.IntegratedTax),
                Camt = R(report.InwardSuppliesReverseCharge.CentralTax),
                Samt = R(report.InwardSuppliesReverseCharge.StateTax),
                Csamt = R(report.InwardSuppliesReverseCharge.Cess)
            },
            // 3.1(e) Non-GST outward supplies
            OsupNongst = new Gstr3BTaxDetailDto
            {
                Txval = R(report.NonGstOutwardSupplies.TotalTaxableValue),
                Iamt = 0,
                Camt = 0,
                Samt = 0,
                Csamt = 0
            }
        };

        // Table 4: Eligible Input Tax Credit (ITC)
        dto.ItcElg = new Gstr3BItcElgDto
        {
            // 4(A) ITC Available (whether in full or part)
            ItcAvl = new List<Gstr3BItcItemDto>
            {
                // (1) Import of Goods
                new()
                {
                    Ty = "IMPG",
                    Iamt = R(report.ItcImportGoods.IntegratedTax),
                    Camt = 0,
                    Samt = 0,
                    Csamt = R(report.ItcImportGoods.Cess)
                },
                // (2) Import of Services
                new()
                {
                    Ty = "IMPS",
                    Iamt = R(report.ItcImportServices.IntegratedTax),
                    Camt = 0,
                    Samt = 0,
                    Csamt = R(report.ItcImportServices.Cess)
                },
                // (3) Inward supplies liable to reverse charge
                new()
                {
                    Ty = "ISRC",
                    Iamt = R(report.ItcInwardReverseCharge.IntegratedTax),
                    Camt = R(report.ItcInwardReverseCharge.CentralTax),
                    Samt = R(report.ItcInwardReverseCharge.StateTax),
                    Csamt = R(report.ItcInwardReverseCharge.Cess)
                },
                // (4) Inward supplies from ISD
                new()
                {
                    Ty = "ISD",
                    Iamt = R(report.ItcInwardIsd.IntegratedTax),
                    Camt = R(report.ItcInwardIsd.CentralTax),
                    Samt = R(report.ItcInwardIsd.StateTax),
                    Csamt = R(report.ItcInwardIsd.Cess)
                },
                // (5) All other ITC (Purchases & Expenses)
                new()
                {
                    Ty = "OTH",
                    Iamt = R(report.ItcAllOther.IntegratedTax),
                    Camt = R(report.ItcAllOther.CentralTax),
                    Samt = R(report.ItcAllOther.StateTax),
                    Csamt = R(report.ItcAllOther.Cess)
                }
            },

            // 4(B) ITC Reversed
            ItcRev = new List<Gstr3BItcItemDto>
            {
                // (1) As per rules 42 & 43 of CGST Rules
                new()
                {
                    Ty = "RUL",
                    Iamt = R(report.ItcReversedRule4243.IntegratedTax),
                    Camt = R(report.ItcReversedRule4243.CentralTax),
                    Samt = R(report.ItcReversedRule4243.StateTax),
                    Csamt = R(report.ItcReversedRule4243.Cess)
                },
                // (2) Others
                new()
                {
                    Ty = "OTH",
                    Iamt = R(report.ItcReversedOthers.IntegratedTax),
                    Camt = R(report.ItcReversedOthers.CentralTax),
                    Samt = R(report.ItcReversedOthers.StateTax),
                    Csamt = R(report.ItcReversedOthers.Cess)
                }
            },

            // 4(C) Net ITC Available (A) - (B)
            ItcNet = new Gstr3BTaxAmountDto
            {
                Iamt = R(report.NetItcAvailable.IntegratedTax),
                Camt = R(report.NetItcAvailable.CentralTax),
                Samt = R(report.NetItcAvailable.StateTax),
                Csamt = R(report.NetItcAvailable.Cess)
            },

            // 4(D) Ineligible ITC
            ItcInelg = new List<Gstr3BItcItemDto>
            {
                // (1) As per section 17(5)
                new()
                {
                    Ty = "RUL",
                    Iamt = R(report.IneligibleItcSection17_5.IntegratedTax),
                    Camt = R(report.IneligibleItcSection17_5.CentralTax),
                    Samt = R(report.IneligibleItcSection17_5.StateTax),
                    Csamt = R(report.IneligibleItcSection17_5.Cess)
                },
                // (2) Others
                new()
                {
                    Ty = "OTH",
                    Iamt = 0,
                    Camt = 0,
                    Samt = 0,
                    Csamt = 0
                }
            }
        };

        // Table 5: Values of exempt, nil-rated and non-GST inward supplies
        dto.InwardSup = new Gstr3BInwardSupDto
        {
            IsupDetails = new List<Gstr3BInwardDetailDto>
            {
                new()
                {
                    Ty = "GST",
                    Inter = R(report.InwardSuppliesCompositionExemptNilInterState),
                    Intra = R(report.InwardSuppliesCompositionExemptNilIntraState)
                },
                new()
                {
                    Ty = "NONGST",
                    Inter = 0,
                    Intra = 0
                }
            }
        };

        // Table 5.1: Interest & Late Fee (defaults to 0)
        dto.IntrLtfee = new Gstr3BIntrLtfeeDto
        {
            IntrDetails = new Gstr3BTaxAmountDto
            {
                Iamt = 0,
                Camt = 0,
                Samt = 0,
                Csamt = 0
            },
            LtfeeDetails = new Gstr3BLtfeeDetailsDto()
        };

        // Table 3.2: Details of Inter-State supplies made to unregistered persons
        dto.InterSup = new Gstr3BInterSupDto
        {
            UnregDetails = report.InterStateSuppliesToUnregistered
                .Where(x => x.TaxableValue > 0 || x.IntegratedTax > 0)
                .Select(x => new Gstr3BPosTaxDto
                {
                    Pos = FormatPosCode(x.PlaceOfSupply),
                    Txval = R(x.TaxableValue),
                    Iamt = R(x.IntegratedTax)
                })
                .ToList(),
            CompDetails = new List<Gstr3BPosTaxDto>(),
            UinDetails = new List<Gstr3BPosTaxDto>()
        };

        return dto;
    }

    public static string GenerateJson(Gstr3BReport report, string gstin, string? retPeriod = null, bool indented = true)
    {
        var payload = CreatePayload(report, gstin, retPeriod);
        var options = new JsonSerializerOptions
        {
            WriteIndented = indented,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        };
        return JsonSerializer.Serialize(payload, options);
    }

    private static decimal R(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string FormatPosCode(string pos)
    {
        if (string.IsNullOrWhiteSpace(pos)) return "21";
        var trimmed = pos.Trim();
        if (trimmed.Length >= 2 && char.IsDigit(trimmed[0]) && char.IsDigit(trimmed[1]))
        {
            return trimmed.Substring(0, 2);
        }
        return trimmed.PadLeft(2, '0');
    }
}
