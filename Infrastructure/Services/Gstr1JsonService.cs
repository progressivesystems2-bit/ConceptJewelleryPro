using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ConceptJewelleryPro.Domain.Entities.GST;

namespace ConceptJewelleryPro.Infrastructure.Services;

#region GSTR-1 Data Contract Schema (Strict GSTN Offline Utility Alignment)

/// <summary>
/// Root document strictly adhering to GSTN GSTR-1 offline utility JSON schema.
/// Uploaded to GST Portal (gst.gov.in) -> Returns Dashboard -> GSTR-1 -> Prepare Offline.
/// Structure matches working reference implementation from ConceptS.
/// </summary>
public sealed class Gstr1Document
{
    [JsonPropertyName("gstin")]
    [JsonPropertyOrder(1)]
    public string gstin { get; set; } = string.Empty;

    [JsonPropertyName("fp")]
    [JsonPropertyOrder(2)]
    public string fp { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    [JsonPropertyOrder(3)]
    public string version { get; set; } = "GST3.2.2";

    [JsonPropertyName("hash")]
    [JsonPropertyOrder(4)]
    public string hash { get; set; } = "hash";

    [JsonPropertyName("b2b")]
    [JsonPropertyOrder(5)]
    public List<Gstr1B2bParty> b2b { get; set; } = new();

    [JsonPropertyName("cdnr")]
    [JsonPropertyOrder(6)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Gstr1NoteParty>? cdnr { get; set; }

    [JsonPropertyName("b2cs")]
    [JsonPropertyOrder(7)]
    public List<Gstr1B2csRow> b2cs { get; set; } = new();

    [JsonPropertyName("hsn")]
    [JsonPropertyOrder(8)]
    public Gstr1HsnSection hsn { get; set; } = new();

    [JsonPropertyName("doc_issue")]
    [JsonPropertyOrder(9)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Gstr1DocIssueSection? doc_issue { get; set; }
}

public sealed class Gstr1B2bParty
{
    [JsonPropertyName("ctin")]
    public string ctin { get; set; } = string.Empty;

    [JsonPropertyName("inv")]
    public List<Gstr1Invoice> inv { get; set; } = new();
}

public sealed class Gstr1Invoice
{
    [JsonPropertyName("inum")]
    public string inum { get; set; } = string.Empty;

    [JsonPropertyName("idt")]
    public string idt { get; set; } = string.Empty; // dd-MM-yyyy

    [JsonPropertyName("val")]
    public decimal val { get; set; } // Rounded to 0 decimals (nearest integer rupee)

    [JsonPropertyName("pos")]
    public string pos { get; set; } = string.Empty;

    [JsonPropertyName("rchrg")]
    public string rchrg { get; set; } = "N";

    [JsonPropertyName("inv_typ")]
    public string inv_typ { get; set; } = "R";

    [JsonPropertyName("itms")]
    public List<Gstr1Item> itms { get; set; } = new();
}

public sealed class Gstr1NoteParty
{
    [JsonPropertyName("ctin")]
    public string ctin { get; set; } = string.Empty;

    [JsonPropertyName("nt")]
    public List<Gstr1Note> nt { get; set; } = new();
}

public sealed class Gstr1Note
{
    [JsonPropertyName("nt_num")]
    public string nt_num { get; set; } = string.Empty;

    [JsonPropertyName("nt_dt")]
    public string nt_dt { get; set; } = string.Empty; // dd-MM-yyyy

    [JsonPropertyName("ntty")]
    public string ntty { get; set; } = "C";

    [JsonPropertyName("val")]
    public decimal val { get; set; } // Rounded to 0 decimals

    [JsonPropertyName("pos")]
    public string pos { get; set; } = string.Empty;

    [JsonPropertyName("rchrg")]
    public string rchrg { get; set; } = "N";

    [JsonPropertyName("inv_typ")]
    public string inv_typ { get; set; } = "R";

    [JsonPropertyName("itms")]
    public List<Gstr1Item> itms { get; set; } = new();
}

public sealed class Gstr1Item
{
    [JsonPropertyName("num")]
    public int num { get; set; }

    [JsonPropertyName("itm_det")]
    public Gstr1ItemDetail itm_det { get; set; } = new();
}

public sealed class Gstr1ItemDetail
{
    [JsonPropertyName("txval")]
    [JsonPropertyOrder(1)]
    public decimal txval { get; set; }

    [JsonPropertyName("rt")]
    [JsonPropertyOrder(2)]
    public decimal rt { get; set; }

    [JsonPropertyName("iamt")]
    [JsonPropertyOrder(3)]
    public decimal iamt { get; set; }

    [JsonPropertyName("camt")]
    [JsonPropertyOrder(4)]
    public decimal camt { get; set; }

    [JsonPropertyName("samt")]
    [JsonPropertyOrder(5)]
    public decimal samt { get; set; }

    [JsonPropertyName("csamt")]
    [JsonPropertyOrder(6)]
    public decimal csamt { get; set; }
}

public sealed class Gstr1B2csRow
{
    [JsonPropertyName("flag")]
    [JsonPropertyOrder(1)]
    public string flag { get; set; } = "N";

    [JsonPropertyName("sply_ty")]
    [JsonPropertyOrder(2)]
    public string sply_ty { get; set; } = "INTRA";

    [JsonPropertyName("typ")]
    [JsonPropertyOrder(3)]
    public string typ { get; set; } = "OE";

    [JsonPropertyName("pos")]
    [JsonPropertyOrder(4)]
    public string pos { get; set; } = string.Empty;

    [JsonPropertyName("rt")]
    [JsonPropertyOrder(5)]
    public decimal rt { get; set; }

    [JsonPropertyName("txval")]
    [JsonPropertyOrder(6)]
    public decimal txval { get; set; }

    [JsonPropertyName("iamt")]
    [JsonPropertyOrder(7)]
    public decimal iamt { get; set; }

    [JsonPropertyName("camt")]
    [JsonPropertyOrder(8)]
    public decimal camt { get; set; }

    [JsonPropertyName("samt")]
    [JsonPropertyOrder(9)]
    public decimal samt { get; set; }

    [JsonPropertyName("csamt")]
    [JsonPropertyOrder(10)]
    public decimal csamt { get; set; }
}

public sealed class Gstr1HsnSection
{
    [JsonPropertyName("hsn_b2b")]
    [JsonPropertyOrder(1)]
    public List<Gstr1HsnRow> hsn_b2b { get; set; } = new();

    [JsonPropertyName("hsn_b2c")]
    [JsonPropertyOrder(2)]
    public List<Gstr1HsnRow> hsn_b2c { get; set; } = new();
}

public sealed class Gstr1HsnRow
{
    [JsonPropertyName("num")]
    [JsonPropertyOrder(1)]
    public int num { get; set; }

    [JsonPropertyName("hsn_sc")]
    [JsonPropertyOrder(2)]
    public string hsn_sc { get; set; } = string.Empty;

    [JsonPropertyName("desc")]
    [JsonPropertyOrder(3)]
    public string desc { get; set; } = string.Empty;

    [JsonPropertyName("uqc")]
    [JsonPropertyOrder(4)]
    public string uqc { get; set; } = string.Empty;

    [JsonPropertyName("qty")]
    [JsonPropertyOrder(5)]
    public decimal qty { get; set; }

    [JsonPropertyName("rt")]
    [JsonPropertyOrder(6)]
    public decimal rt { get; set; }

    [JsonPropertyName("txval")]
    [JsonPropertyOrder(7)]
    public decimal txval { get; set; }

    [JsonPropertyName("iamt")]
    [JsonPropertyOrder(8)]
    public decimal iamt { get; set; }

    [JsonPropertyName("camt")]
    [JsonPropertyOrder(9)]
    public decimal camt { get; set; }

    [JsonPropertyName("samt")]
    [JsonPropertyOrder(10)]
    public decimal samt { get; set; }

    [JsonPropertyName("csamt")]
    [JsonPropertyOrder(11)]
    public decimal csamt { get; set; }
}

public sealed class Gstr1DocIssueSection
{
    [JsonPropertyName("flag")]
    [JsonPropertyOrder(1)]
    public string flag { get; set; } = "N";

    [JsonPropertyName("doc_det")]
    [JsonPropertyOrder(2)]
    public List<Gstr1DocDetail> doc_det { get; set; } = new();
}

public sealed class Gstr1DocDetail
{
    [JsonPropertyName("doc_num")]
    [JsonPropertyOrder(1)]
    public int doc_num { get; set; } = 1;

    [JsonPropertyName("doc_typ")]
    [JsonPropertyOrder(2)]
    public string doc_typ { get; set; } = "Invoices for outward supply";

    [JsonPropertyName("docs")]
    [JsonPropertyOrder(3)]
    public List<Gstr1DocRange> docs { get; set; } = new();
}

public sealed class Gstr1DocRange
{
    [JsonPropertyName("num")]
    [JsonPropertyOrder(1)]
    public int num { get; set; }

    [JsonPropertyName("from")]
    [JsonPropertyOrder(2)]
    public string from { get; set; } = string.Empty;

    [JsonPropertyName("to")]
    [JsonPropertyOrder(3)]
    public string to { get; set; } = string.Empty;

    [JsonPropertyName("totnum")]
    [JsonPropertyOrder(4)]
    public int totnum { get; set; }

    [JsonPropertyName("cancel")]
    [JsonPropertyOrder(5)]
    public int cancel { get; set; }

    [JsonPropertyName("net_issue")]
    [JsonPropertyOrder(6)]
    public int net_issue { get; set; }
}

/// <summary>
/// Formats decimals to always output at least one decimal place (e.g., 7200.0, 3.0, 0.0)
/// matching GST portal and offline tool validator expectations.
/// </summary>
public sealed class GstrDecimalConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.GetDecimal();
    }

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
    {
        var formatted = value.ToString("0.0####", CultureInfo.InvariantCulture);
        writer.WriteRawValue(formatted);
    }
}

#endregion

/// <summary>
/// Service to create and export government-compliant GSTR-1 JSON files for portal upload (gst.gov.in).
/// Schema precisely mirrors ConceptS working reference.
/// </summary>
public static class Gstr1JsonService
{
    private static readonly Regex GstinRegex = new(@"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex RetPeriodRegex = new(@"^(0[1-9]|1[0-2])20[0-9]{2}$", RegexOptions.Compiled);

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions(true);
    private static readonly JsonSerializerOptions CompactJsonOptions = CreateJsonOptions(false);

    private static JsonSerializerOptions CreateJsonOptions(bool indented)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = indented,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        options.Converters.Add(new GstrDecimalConverter());
        return options;
    }

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
            errors.Add($"Return Period '{retPeriod}' must be in MMYYYY format (e.g., 072026 for July 2026).");
        }

        return errors.Count == 0;
    }

    public static string FormatReturnPeriod(DateTime date)
    {
        return date.ToString("MMyyyy", CultureInfo.InvariantCulture);
    }

    public static Gstr1Document CreatePayload(Gstr1Report report, string gstin, string? retPeriod = null, string sellerStateCode = "21")
    {
        var cleanGstin = NormalizeGstin(gstin);
        var period = !string.IsNullOrWhiteSpace(retPeriod)
            ? retPeriod.Trim()
            : FormatReturnPeriod(report.ToDate != default ? report.ToDate : (report.FromDate != default ? report.FromDate : DateTime.Today));

        var cleanSellerState = FormatPosCode(sellerStateCode);

        var doc = new Gstr1Document
        {
            gstin = cleanGstin,
            fp = period,
            version = "GST3.2.2",
            hash = "hash",
            b2b = new List<Gstr1B2bParty>(),
            cdnr = null,
            b2cs = new List<Gstr1B2csRow>(),
            hsn = new Gstr1HsnSection
            {
                hsn_b2b = new List<Gstr1HsnRow>(),
                hsn_b2c = new List<Gstr1HsnRow>()
            },
            doc_issue = null
        };

        // 1. Table 4: B2B Invoices (Grouped by Buyer CTIN)
        if (report.B2BInvoices != null && report.B2BInvoices.Count > 0)
        {
            var b2bGroups = report.B2BInvoices
                .GroupBy(x => NormalizeGstin(x.Gstin))
                .Where(g => !string.IsNullOrWhiteSpace(g.Key))
                .OrderBy(g => g.Min(inv => inv.InvoiceNo), StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    var party = new Gstr1B2bParty { ctin = g.Key };
                    party.inv = g.GroupBy(inv => inv.InvoiceNo)
                        .OrderBy(invGroup => invGroup.Key, StringComparer.OrdinalIgnoreCase)
                        .Select(invGroup =>
                        {
                            var first = invGroup.First();
                            var pos = FormatPosCode(!string.IsNullOrWhiteSpace(first.PlaceOfSupply) ? first.PlaceOfSupply : (first.Gstin?.Length >= 2 ? first.Gstin.Substring(0, 2) : cleanSellerState));
                            var isSameState = string.Equals(pos, cleanSellerState, StringComparison.OrdinalIgnoreCase);

                            var invDto = new Gstr1Invoice
                            {
                                inum = first.InvoiceNo,
                                idt = first.InvoiceDate.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture),
                                val = Math.Round(invGroup.Sum(i => i.InvoiceValue), 0, MidpointRounding.AwayFromZero),
                                pos = pos,
                                rchrg = "N",
                                inv_typ = "R"
                            };

                            int itemNum = 1;
                            foreach (var rateGroup in invGroup.GroupBy(l => l.GstRate).OrderBy(gRate => gRate.Key))
                            {
                                var rate = R(rateGroup.Key);
                                var taxable = R(rateGroup.Sum(l => l.TaxableValue));
                                var igst = R(rateGroup.Sum(l => l.IgstAmount));
                                var cgst = R(rateGroup.Sum(l => l.CgstAmount));
                                var sgst = R(rateGroup.Sum(l => l.SgstAmount));
                                var cess = R(rateGroup.Sum(l => l.CessAmount));

                                Gstr1ItemDetail itmDet;
                                if (isSameState)
                                {
                                    var equalTax = Math.Round((cgst + sgst) / 2m, 2, MidpointRounding.AwayFromZero);
                                    itmDet = new Gstr1ItemDetail
                                    {
                                        txval = taxable,
                                        rt = rate,
                                        iamt = 0m,
                                        camt = equalTax,
                                        samt = equalTax,
                                        csamt = cess
                                    };
                                }
                                else
                                {
                                    itmDet = new Gstr1ItemDetail
                                    {
                                        txval = taxable,
                                        rt = rate,
                                        iamt = igst,
                                        camt = 0m,
                                        samt = 0m,
                                        csamt = cess
                                    };
                                }

                                invDto.itms.Add(new Gstr1Item
                                {
                                    num = itemNum++,
                                    itm_det = itmDet
                                });
                            }

                            return invDto;
                        }).ToList();

                    return party;
                }).ToList();

            doc.b2b = b2bGroups;
        }

        // 2. Table 9B: Credit / Debit Notes Registered (CDNR)
        if (report.CreditDebitNotes != null && report.CreditDebitNotes.Count > 0)
        {
            var cdnrGroups = report.CreditDebitNotes
                .GroupBy(x => NormalizeGstin(x.Gstin))
                .Where(g => !string.IsNullOrWhiteSpace(g.Key))
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    var party = new Gstr1NoteParty { ctin = g.Key };
                    party.nt = g.Select(note =>
                    {
                        var pos = FormatPosCode(!string.IsNullOrWhiteSpace(note.PlaceOfSupply) ? note.PlaceOfSupply : (note.Gstin?.Length >= 2 ? note.Gstin.Substring(0, 2) : cleanSellerState));
                        var isSameState = string.Equals(pos, cleanSellerState, StringComparison.OrdinalIgnoreCase);

                        var noteDto = new Gstr1Note
                        {
                            nt_num = note.NoteNumber,
                            nt_dt = note.NoteDate.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture),
                            ntty = string.Equals(note.NoteType, "D", StringComparison.OrdinalIgnoreCase) ? "D" : "C",
                            val = Math.Round(note.NoteValue, 0, MidpointRounding.AwayFromZero),
                            pos = pos,
                            rchrg = "N",
                            inv_typ = "R"
                        };

                        var rate = R(note.GstRate);
                        var taxable = R(note.TaxableValue);
                        var igst = R(note.IgstAmount);
                        var cgst = R(note.CgstAmount);
                        var sgst = R(note.SgstAmount);
                        var cess = R(note.CessAmount);

                        Gstr1ItemDetail itmDet;
                        if (isSameState)
                        {
                            var equalTax = Math.Round((cgst + sgst) / 2m, 2, MidpointRounding.AwayFromZero);
                            itmDet = new Gstr1ItemDetail
                            {
                                txval = taxable,
                                rt = rate,
                                iamt = 0m,
                                camt = equalTax,
                                samt = equalTax,
                                csamt = cess
                            };
                        }
                        else
                        {
                            itmDet = new Gstr1ItemDetail
                            {
                                txval = taxable,
                                rt = rate,
                                iamt = igst,
                                camt = 0m,
                                samt = 0m,
                                csamt = cess
                            };
                        }

                        noteDto.itms.Add(new Gstr1Item
                        {
                            num = 1,
                            itm_det = itmDet
                        });

                        return noteDto;
                    }).ToList();

                    return party;
                }).ToList();

            if (cdnrGroups.Count > 0)
            {
                doc.cdnr = cdnrGroups;
            }
        }

        // 3. Table 7: B2C Small (Aggregated by State and Rate)
        var rawB2cs = new List<Gstr1B2csRow>();
        if (report.B2CSmallSummaries != null && report.B2CSmallSummaries.Count > 0)
        {
            foreach (var x in report.B2CSmallSummaries)
            {
                if (x.TaxableValue <= 0 && x.CgstAmount <= 0 && x.IgstAmount <= 0)
                    continue;

                var pos = FormatPosCode(!string.IsNullOrWhiteSpace(x.PlaceOfSupply) ? x.PlaceOfSupply : cleanSellerState);
                var isInter = pos != cleanSellerState;

                rawB2cs.Add(new Gstr1B2csRow
                {
                    flag = "N",
                    sply_ty = isInter ? "INTER" : "INTRA",
                    typ = "OE",
                    pos = pos,
                    rt = R(x.GstRate),
                    txval = R(x.TaxableValue),
                    iamt = isInter ? R(x.IgstAmount) : 0m,
                    camt = isInter ? 0m : R(x.CgstAmount),
                    samt = isInter ? 0m : R(x.SgstAmount),
                    csamt = R(x.CessAmount)
                });
            }
        }

        // Also aggregate B2CLarge into B2CS if present to maintain standard B2C structure
        if (report.B2CLargeInvoices != null && report.B2CLargeInvoices.Count > 0)
        {
            foreach (var l in report.B2CLargeInvoices)
            {
                var pos = FormatPosCode(l.PlaceOfSupply);
                var isInter = pos != cleanSellerState;

                rawB2cs.Add(new Gstr1B2csRow
                {
                    flag = "N",
                    sply_ty = "INTER",
                    typ = "OE",
                    pos = pos,
                    rt = R(l.GstRate),
                    txval = R(l.TaxableValue),
                    iamt = R(l.IgstAmount),
                    camt = 0m,
                    samt = 0m,
                    csamt = R(l.CessAmount)
                });
            }
        }

        if (rawB2cs.Count > 0)
        {
            doc.b2cs = rawB2cs
                .GroupBy(row => new { row.flag, row.sply_ty, row.typ, row.pos, row.rt })
                .Select(g =>
                {
                    var txval = R(g.Sum(r => r.txval));
                    var iamt = R(g.Sum(r => r.iamt));
                    var camt = R(g.Sum(r => r.camt));
                    var samt = R(g.Sum(r => r.samt));
                    var csamt = R(g.Sum(r => r.csamt));

                    // For INTRA supplies, GST portal mandates that camt and samt are strictly equal
                    if (g.Key.sply_ty == "INTRA" || g.Key.pos == cleanSellerState)
                    {
                        var equalTax = Math.Round((camt + samt) / 2m, 2, MidpointRounding.AwayFromZero);
                        camt = equalTax;
                        samt = equalTax;
                        iamt = 0m;
                    }
                    else
                    {
                        camt = 0m;
                        samt = 0m;
                    }

                    return new Gstr1B2csRow
                    {
                        flag = g.Key.flag,
                        sply_ty = g.Key.sply_ty,
                        typ = g.Key.typ,
                        pos = g.Key.pos,
                        rt = g.Key.rt,
                        txval = txval,
                        iamt = iamt,
                        camt = camt,
                        samt = samt,
                        csamt = csamt
                    };
                })
                .OrderBy(r => r.pos)
                .ThenBy(r => r.rt)
                .ToList();
        }

        // 4. Table 12: HSN Summary (Split into hsn_b2c and hsn_b2b)
        var rawHsnB2c = new List<Gstr1HsnRow>();
        var rawHsnB2b = new List<Gstr1HsnRow>();

        if (report.HsnB2CSummaries != null && report.HsnB2CSummaries.Count > 0)
        {
            foreach (var x in report.HsnB2CSummaries)
            {
                if (x.TaxableValue <= 0 && x.CgstAmount <= 0 && x.IgstAmount <= 0) continue;
                rawHsnB2c.Add(new Gstr1HsnRow
                {
                    hsn_sc = CleanHsn(x.HsnCode),
                    uqc = FormatUqc(x.Uqc),
                    qty = R(x.TotalQuantity),
                    rt = R(x.GstRate),
                    txval = R(x.TaxableValue),
                    iamt = R(x.IgstAmount),
                    camt = R(x.CgstAmount),
                    samt = R(x.SgstAmount),
                    csamt = R(x.CessAmount)
                });
            }
        }
        else if (report.HsnSummaries != null && report.HsnSummaries.Count > 0)
        {
            foreach (var x in report.HsnSummaries)
            {
                if (x.TaxableValue <= 0 && x.CgstAmount <= 0 && x.IgstAmount <= 0) continue;
                rawHsnB2c.Add(new Gstr1HsnRow
                {
                    hsn_sc = CleanHsn(x.HsnCode),
                    uqc = FormatUqc(x.Uqc),
                    qty = R(x.TotalQuantity),
                    rt = R(x.GstRate),
                    txval = R(x.TaxableValue),
                    iamt = R(x.IgstAmount),
                    camt = R(x.CgstAmount),
                    samt = R(x.SgstAmount),
                    csamt = R(x.CessAmount)
                });
            }
        }

        if (report.HsnB2BSummaries != null && report.HsnB2BSummaries.Count > 0)
        {
            foreach (var x in report.HsnB2BSummaries)
            {
                if (x.TaxableValue <= 0 && x.CgstAmount <= 0 && x.IgstAmount <= 0) continue;
                rawHsnB2b.Add(new Gstr1HsnRow
                {
                    hsn_sc = CleanHsn(x.HsnCode),
                    uqc = FormatUqc(x.Uqc),
                    qty = R(x.TotalQuantity),
                    rt = R(x.GstRate),
                    txval = R(x.TaxableValue),
                    iamt = R(x.IgstAmount),
                    camt = R(x.CgstAmount),
                    samt = R(x.SgstAmount),
                    csamt = R(x.CessAmount)
                });
            }
        }

        doc.hsn = new Gstr1HsnSection
        {
            hsn_b2b = AggregateHsn(rawHsnB2b),
            hsn_b2c = AggregateHsn(rawHsnB2c)
        };

        // 5. Table 13: Documents Issued (doc_issue)
        if (report.DocumentSummaries != null && report.DocumentSummaries.Count > 0)
        {
            var validDocs = report.DocumentSummaries
                .Where(d => !string.IsNullOrWhiteSpace(d.FromSerialNo) || !string.IsNullOrWhiteSpace(d.ToSerialNo) || d.TotalNumber > 0)
                .ToList();

            if (validDocs.Count > 0)
            {
                int seriesNum = 1;
                var ranges = validDocs.Select(d => new Gstr1DocRange
                {
                    num = seriesNum++,
                    from = !string.IsNullOrWhiteSpace(d.FromSerialNo) ? d.FromSerialNo.Trim() : "1",
                    to = !string.IsNullOrWhiteSpace(d.ToSerialNo) ? d.ToSerialNo.Trim() : d.FromSerialNo?.Trim() ?? "1",
                    totnum = d.TotalNumber > 0 ? d.TotalNumber : 1,
                    cancel = d.CancelledNumber >= 0 ? d.CancelledNumber : 0,
                    net_issue = d.NetIssuedNumber > 0 ? d.NetIssuedNumber : (d.TotalNumber - d.CancelledNumber)
                }).ToList();

                doc.doc_issue = new Gstr1DocIssueSection
                {
                    flag = "N",
                    doc_det = new List<Gstr1DocDetail>
                    {
                        new Gstr1DocDetail
                        {
                            doc_num = 1,
                            doc_typ = "Invoices for outward supply",
                            docs = ranges
                        }
                    }
                };
            }
        }

        return doc;
    }

    public static string GenerateJson(Gstr1Report report, string gstin, string? retPeriod = null, string sellerStateCode = "21", bool indented = true)
    {
        var payload = CreatePayload(report, gstin, retPeriod, sellerStateCode);
        return JsonSerializer.Serialize(payload, indented ? JsonOptions : CompactJsonOptions);
    }

    private static decimal R(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string NormalizeGstin(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var str = value.Trim().ToUpperInvariant();
        if (str.StartsWith("GSTIN:", StringComparison.OrdinalIgnoreCase)) str = str["GSTIN:".Length..].Trim();
        else if (str.StartsWith("GSTIN :", StringComparison.OrdinalIgnoreCase)) str = str["GSTIN :".Length..].Trim();
        else if (str.StartsWith("GSTIN", StringComparison.OrdinalIgnoreCase)) str = str["GSTIN".Length..].Trim();
        return str;
    }

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

    private static string CleanHsn(string hsn)
    {
        if (string.IsNullOrWhiteSpace(hsn)) return "7113";
        var cleaned = Regex.Replace(hsn, @"[^0-9]", "");
        return !string.IsNullOrWhiteSpace(cleaned) ? cleaned : "7113";
    }

    private static string FormatUqc(string? uqc)
    {
        if (string.IsNullOrWhiteSpace(uqc)) return "GMS";
        var trimmed = uqc.Trim().ToUpperInvariant();
        return trimmed switch
        {
            "GRAMS" or "GM" or "G" or "GRM" or "GMS" => "GMS",
            "PIECES" or "PIECE" or "PC" or "PCS" => "PCS",
            "NUMBERS" or "NUMBER" or "NO" or "NOS" => "NOS",
            "KILOGRAMS" or "KG" or "KGS" => "KGS",
            _ => trimmed.Length <= 3 ? trimmed : "GMS"
        };
    }

    private static List<Gstr1HsnRow> AggregateHsn(IEnumerable<Gstr1HsnRow> rows)
    {
        return rows
            .GroupBy(r => new { r.hsn_sc, r.uqc, r.rt })
            .OrderBy(g => g.Key.hsn_sc)
            .ThenBy(g => g.Key.uqc)
            .ThenBy(g => g.Key.rt)
            .Select((g, idx) =>
            {
                var hsn = g.Key.hsn_sc;
                var uqc = g.Key.uqc;
                var rt = g.Key.rt;
                var qty = R(g.Sum(r => r.qty));
                var txval = R(g.Sum(r => r.txval));
                var iamt = R(g.Sum(r => r.iamt));
                var camt = R(g.Sum(r => r.camt));
                var samt = R(g.Sum(r => r.samt));
                var csamt = R(g.Sum(r => r.csamt));

                return new Gstr1HsnRow
                {
                    num = idx + 1,
                    hsn_sc = hsn,
                    desc = GetHsnDescription(hsn),
                    uqc = uqc,
                    qty = qty,
                    rt = rt,
                    txval = txval,
                    iamt = iamt,
                    camt = camt,
                    samt = samt,
                    csamt = csamt
                };
            })
            .ToList();
    }

    private static string GetHsnDescription(string hsn)
    {
        if (string.IsNullOrWhiteSpace(hsn)) return "Gold & Precious Jewellery";
        var prefix = hsn.Trim();
        if (prefix.StartsWith("7113")) return "Gold & Precious Jewellery";
        if (prefix.StartsWith("7114")) return "Silverware & Articles";
        if (prefix.StartsWith("7117")) return "Imitation Jewellery";
        if (prefix.StartsWith("7118")) return "Coin";
        if (prefix.StartsWith("7108")) return "Gold Unwrought / Semi-Manufactured";
        if (prefix.StartsWith("7106")) return "Silver Unwrought / Semi-Manufactured";
        return "Articles of Jewellery";
    }
}
