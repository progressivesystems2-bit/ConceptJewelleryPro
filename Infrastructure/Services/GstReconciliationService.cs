using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using ConceptJewelleryPro.Domain.Entities.GST;

namespace ConceptJewelleryPro.Infrastructure.Services;

public static class GstReconciliationService
{
    public static List<GstPortalInvoice> ParsePortalFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            throw new FileNotFoundException("GSTR file not found.", filePath);
        }

        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (ext == ".json")
        {
            return ParseGstrJson(filePath);
        }
        else if (ext == ".csv" || ext == ".txt")
        {
            return ParseGstrCsv(filePath);
        }
        else
        {
            // Fallback try JSON then CSV
            try { return ParseGstrJson(filePath); }
            catch { return ParseGstrCsv(filePath); }
        }
    }

    public static (List<GstReconciliationItem> Items, GstReconciliationSummary Summary) Reconcile(
        IReadOnlyList<GstPurchaseRegisterItem> booksPurchases,
        IReadOnlyList<GstPortalInvoice> portalInvoices,
        decimal tolerance = 2.00m)
    {
        var reconciliationItems = new List<GstReconciliationItem>();
        var matchedPortalIndices = new HashSet<int>();

        // 1. For each Books purchase item, find best matching portal invoice
        foreach (var book in booksPurchases)
        {
            var cleanBookGstin = CleanGstin(book.SupplierGstin);
            var normBookBill = NormalizeInvoiceNumber(book.SuppBillNo);

            int bestPortalIdx = -1;
            GstMatchStatus bestStatus = GstMatchStatus.MissingIn2B;
            decimal lowestVariance = decimal.MaxValue;

            for (int i = 0; i < portalInvoices.Count; i++)
            {
                if (matchedPortalIndices.Contains(i)) continue;

                var portal = portalInvoices[i];
                var cleanPortalGstin = CleanGstin(portal.SupplierGstin);
                var normPortalBill = NormalizeInvoiceNumber(portal.InvoiceNumber);

                bool hasBookGstin = !string.IsNullOrWhiteSpace(cleanBookGstin);
                bool hasBookBill = !string.IsNullOrWhiteSpace(normBookBill);
                bool gstinMatch = hasBookGstin && string.Equals(cleanBookGstin, cleanPortalGstin, StringComparison.OrdinalIgnoreCase);
                bool billMatch = hasBookBill && string.Equals(normBookBill, normPortalBill, StringComparison.OrdinalIgnoreCase);

                decimal taxDiff = Math.Abs(book.TotalTax - portal.TotalTax);
                decimal taxableDiff = Math.Abs(book.TaxableAmount - portal.TaxableValue);
                var daysDiff = Math.Abs((book.BillDate - portal.InvoiceDate).TotalDays);

                if (gstinMatch && billMatch)
                {
                    if (taxDiff <= tolerance && taxableDiff <= tolerance)
                    {
                        bestPortalIdx = i;
                        bestStatus = GstMatchStatus.Matched;
                        break;
                    }
                    else if (taxDiff <= tolerance)
                    {
                        bestPortalIdx = i;
                        bestStatus = GstMatchStatus.MismatchInTaxable;
                        lowestVariance = taxableDiff;
                    }
                    else if (taxDiff < lowestVariance)
                    {
                        bestPortalIdx = i;
                        bestStatus = GstMatchStatus.MismatchInTax;
                        lowestVariance = taxDiff;
                    }
                }
                else if (gstinMatch && !billMatch)
                {
                    if (taxDiff <= tolerance && taxableDiff <= tolerance && daysDiff <= 15)
                    {
                        if (bestPortalIdx == -1)
                        {
                            bestPortalIdx = i;
                            bestStatus = GstMatchStatus.MismatchInDate;
                        }
                    }
                }
                else if (!gstinMatch && billMatch)
                {
                    // Bill matches exactly even if GSTIN was omitted from supplier master
                    if (taxableDiff <= tolerance && daysDiff <= 30)
                    {
                        if (bestPortalIdx == -1)
                        {
                            bestPortalIdx = i;
                            bestStatus = taxDiff <= tolerance ? GstMatchStatus.Matched : GstMatchStatus.MismatchInTax;
                        }
                    }
                }
                else if (!hasBookGstin || !hasBookBill)
                {
                    // Fallback: Book has missing GSTIN or missing Bill No, but exact taxable amount and same date window (within +-5 days)
                    if (taxableDiff <= tolerance && daysDiff <= 5)
                    {
                        if (bestPortalIdx == -1)
                        {
                            bestPortalIdx = i;
                            bestStatus = taxDiff <= tolerance ? GstMatchStatus.Matched : GstMatchStatus.MismatchInTax;
                        }
                    }
                }
            }

            if (bestPortalIdx >= 0)
            {
                matchedPortalIndices.Add(bestPortalIdx);
                var matchedPortal = portalInvoices[bestPortalIdx];

                decimal taxDiff = Math.Abs(book.TotalTax - matchedPortal.TotalTax);
                decimal taxableDiff = Math.Abs(book.TaxableAmount - matchedPortal.TaxableValue);
                var notes = new List<string>();

                if (bestStatus == GstMatchStatus.Matched)
                {
                    notes.Add("Matched Successfully");
                    if (string.IsNullOrWhiteSpace(cleanBookGstin))
                        notes.Add($"Note: Add GSTIN {matchedPortal.SupplierGstin} in Books");
                    if (string.IsNullOrWhiteSpace(normBookBill))
                        notes.Add($"Note: Add Bill# {matchedPortal.InvoiceNumber} in Books");
                }
                else
                {
                    if (taxDiff > tolerance)
                        notes.Add($"Tax Diff: ₹{(book.TotalTax - matchedPortal.TotalTax):N2}");
                    if (taxableDiff > tolerance)
                        notes.Add($"Taxable Diff: ₹{(book.TaxableAmount - matchedPortal.TaxableValue):N2}");
                    if (string.IsNullOrWhiteSpace(cleanBookGstin))
                        notes.Add($"Missing GSTIN in Books (Portal has {matchedPortal.SupplierGstin})");
                    if (string.IsNullOrWhiteSpace(normBookBill))
                        notes.Add($"Missing Bill# in Books (Portal has {matchedPortal.InvoiceNumber})");
                }

                reconciliationItems.Add(new GstReconciliationItem
                {
                    Status = bestStatus,
                    BooksVouNo = book.VouNo,
                    BooksBillNo = book.SuppBillNo,
                    BooksDate = book.BillDate,
                    BooksSupplierName = book.SupplierName,
                    BooksGstin = book.SupplierGstin,
                    BooksTaxableValue = book.TaxableAmount,
                    BooksIgst = book.IgstAmount,
                    BooksCgst = book.CgstAmount,
                    BooksSgst = book.SgstAmount,
                    BooksTotalAmount = book.NetAmount,

                    PortalBillNo = matchedPortal.InvoiceNumber,
                    PortalDate = matchedPortal.InvoiceDate,
                    PortalSupplierName = matchedPortal.SupplierTradeName,
                    PortalGstin = matchedPortal.SupplierGstin,
                    PortalTaxableValue = matchedPortal.TaxableValue,
                    PortalIgst = matchedPortal.IntegratedTax,
                    PortalCgst = matchedPortal.CentralTax,
                    PortalSgst = matchedPortal.StateTax,
                    PortalTotalAmount = matchedPortal.InvoiceValue,
                    Remarks = string.Join(" | ", notes)
                });
            }
            else
            {
                // In Books only (Missing in 2B)
                reconciliationItems.Add(new GstReconciliationItem
                {
                    Status = GstMatchStatus.MissingIn2B,
                    BooksVouNo = book.VouNo,
                    BooksBillNo = book.SuppBillNo,
                    BooksDate = book.BillDate,
                    BooksSupplierName = book.SupplierName,
                    BooksGstin = book.SupplierGstin,
                    BooksTaxableValue = book.TaxableAmount,
                    BooksIgst = book.IgstAmount,
                    BooksCgst = book.CgstAmount,
                    BooksSgst = book.SgstAmount,
                    BooksTotalAmount = book.NetAmount,
                    Remarks = "Not found in GST Portal GSTR-2B (Supplier hasn't uploaded)"
                });
            }
        }

        // 2. Add remaining Portal invoices (Missing in Books)
        for (int i = 0; i < portalInvoices.Count; i++)
        {
            if (matchedPortalIndices.Contains(i)) continue;

            var portal = portalInvoices[i];
            reconciliationItems.Add(new GstReconciliationItem
            {
                Status = GstMatchStatus.MissingInBooks,
                PortalBillNo = portal.InvoiceNumber,
                PortalDate = portal.InvoiceDate,
                PortalSupplierName = portal.SupplierTradeName,
                PortalGstin = portal.SupplierGstin,
                PortalTaxableValue = portal.TaxableValue,
                PortalIgst = portal.IntegratedTax,
                PortalCgst = portal.CentralTax,
                PortalSgst = portal.StateTax,
                PortalTotalAmount = portal.InvoiceValue,
                Remarks = "Present in GSTR-2B but not recorded in ERP Purchases"
            });
        }

        // 3. Compile Summary KPIs
        var summary = new GstReconciliationSummary
        {
            TotalBooksInvoices = booksPurchases.Count,
            TotalPortalInvoices = portalInvoices.Count,
            MatchedCount = reconciliationItems.Count(x => x.Status == GstMatchStatus.Matched),
            MismatchCount = reconciliationItems.Count(x => x.Status is GstMatchStatus.MismatchInTaxable or GstMatchStatus.MismatchInTax or GstMatchStatus.MismatchInDate),
            MissingIn2BCount = reconciliationItems.Count(x => x.Status == GstMatchStatus.MissingIn2B),
            MissingInBooksCount = reconciliationItems.Count(x => x.Status == GstMatchStatus.MissingInBooks),

            BooksTotalTaxable = booksPurchases.Sum(x => x.TaxableAmount),
            BooksTotalTax = booksPurchases.Sum(x => x.TotalTax),

            PortalTotalTaxable = portalInvoices.Sum(x => x.TaxableValue),
            PortalTotalTax = portalInvoices.Sum(x => x.TotalTax),

            MatchedTaxAmount = reconciliationItems.Where(x => x.Status == GstMatchStatus.Matched).Sum(x => x.BooksTotalTax),
            MissingIn2BTaxAmount = reconciliationItems.Where(x => x.Status == GstMatchStatus.MissingIn2B).Sum(x => x.BooksTotalTax),
            MissingInBooksTaxAmount = reconciliationItems.Where(x => x.Status == GstMatchStatus.MissingInBooks).Sum(x => x.PortalTotalTax)
        };

        return (reconciliationItems, summary);
    }

    // ── File Parsers ─────────────────────────────────────────────────────────

    private static List<GstPortalInvoice> ParseGstrJson(string filePath)
    {
        var list = new List<GstPortalInvoice>();
        var jsonText = File.ReadAllText(filePath);
        using var doc = JsonDocument.Parse(jsonText);
        var root = doc.RootElement;

        // Standard GSTN JSON (e.g. { "b2b": [ { "ctin": "...", "trdnm": "...", "inv": [...] } ] } or { "data": { "b2b": ... } })
        JsonElement b2bArray;
        if (root.TryGetProperty("b2b", out b2bArray) ||
            (root.TryGetProperty("data", out var dataElem) && dataElem.TryGetProperty("b2b", out b2bArray)))
        {
            if (b2bArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var supplier in b2bArray.EnumerateArray())
                {
                    string ctin = supplier.TryGetProperty("ctin", out var c) ? c.GetString() ?? string.Empty : string.Empty;
                    string trdnm = supplier.TryGetProperty("trdnm", out var t) ? t.GetString() ?? string.Empty : string.Empty;
                    if (string.IsNullOrWhiteSpace(trdnm) && supplier.TryGetProperty("lgnm", out var lg)) trdnm = lg.GetString() ?? string.Empty;

                    if (supplier.TryGetProperty("inv", out var invArray) && invArray.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var inv in invArray.EnumerateArray())
                        {
                            var invoice = new GstPortalInvoice
                            {
                                SupplierGstin = CleanGstin(ctin),
                                SupplierTradeName = trdnm,
                                InvoiceNumber = inv.TryGetProperty("inum", out var inum) ? inum.GetString() ?? string.Empty : string.Empty,
                                InvoiceType = inv.TryGetProperty("inv_typ", out var ityp) ? ityp.GetString() ?? "R" : "R",
                                InvoiceDate = ParseDate(inv.TryGetProperty("idt", out var idt) ? idt.GetString() : null),
                                InvoiceValue = inv.TryGetProperty("val", out var val) ? val.GetDecimal() : 0m,
                                PlaceOfSupply = inv.TryGetProperty("pos", out var pos) ? pos.GetString() ?? string.Empty : string.Empty,
                                ReverseCharge = inv.TryGetProperty("rchrg", out var rchrg) ? rchrg.GetString() ?? "N" : "N",
                                SourceFile = Path.GetFileName(filePath)
                            };

                            if (inv.TryGetProperty("itms", out var itms) && itms.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var item in itms.EnumerateArray())
                                {
                                    JsonElement det;
                                    if (item.TryGetProperty("itm_det", out det) || item.TryGetProperty("det", out det))
                                    {
                                        invoice.Rate = det.TryGetProperty("rt", out var rt) ? rt.GetDecimal() : 0m;
                                        invoice.TaxableValue += det.TryGetProperty("txval", out var tx) ? tx.GetDecimal() : 0m;
                                        invoice.IntegratedTax += det.TryGetProperty("iamt", out var ia) ? ia.GetDecimal() : 0m;
                                        invoice.CentralTax += det.TryGetProperty("camt", out var ca) ? ca.GetDecimal() : 0m;
                                        invoice.StateTax += det.TryGetProperty("samt", out var sa) ? sa.GetDecimal() : 0m;
                                        invoice.Cess += det.TryGetProperty("csamt", out var cs) ? cs.GetDecimal() : 0m;
                                    }
                                }
                            }

                            list.Add(invoice);
                        }
                    }
                }
            }
        }

        return list;
    }

    private static List<GstPortalInvoice> ParseGstrCsv(string filePath)
    {
        var list = new List<GstPortalInvoice>();
        var lines = File.ReadAllLines(filePath);
        if (lines.Length <= 1) return list;

        int headerIndex = -1;
        for (int i = 0; i < Math.Min(10, lines.Length); i++)
        {
            var lineUpper = lines[i].ToUpperInvariant();
            if (lineUpper.Contains("GSTIN") && (lineUpper.Contains("INVOICE") || lineUpper.Contains("INV NO") || lineUpper.Contains("TAXABLE")))
            {
                headerIndex = i;
                break;
            }
        }

        if (headerIndex == -1) headerIndex = 0;

        var headers = ParseCsvLine(lines[headerIndex]).Select(h => h.Trim().ToUpperInvariant()).ToArray();

        int idxGstin = FindHeaderIndex(headers, "GSTIN OF SUPPLIER", "GSTIN", "SUPPLIER GSTIN");
        int idxName = FindHeaderIndex(headers, "TRADE/LEGAL NAME", "TRADE NAME", "LEGAL NAME", "SUPPLIER NAME");
        int idxInvNo = FindHeaderIndex(headers, "INVOICE NUMBER", "INV NO", "INVOICE NO", "DOCUMENT NUMBER");
        int idxDate = FindHeaderIndex(headers, "INVOICE DATE", "INV DATE", "DATE");
        int idxVal = FindHeaderIndex(headers, "INVOICE VALUE", "TOTAL VALUE", "INVOICE AMOUNT");
        int idxTaxable = FindHeaderIndex(headers, "TAXABLE VALUE", "TAXABLE AMOUNT");
        int idxIgst = FindHeaderIndex(headers, "INTEGRATED TAX", "IGST");
        int idxCgst = FindHeaderIndex(headers, "CENTRAL TAX", "CGST");
        int idxSgst = FindHeaderIndex(headers, "STATE/UT TAX", "SGST", "UTGST");

        for (int i = headerIndex + 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            var cols = ParseCsvLine(line);
            if (cols.Count <= Math.Max(idxGstin, idxInvNo)) continue;

            var gstin = idxGstin >= 0 && idxGstin < cols.Count ? cols[idxGstin].Trim() : string.Empty;
            var invNo = idxInvNo >= 0 && idxInvNo < cols.Count ? cols[idxInvNo].Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(gstin) && string.IsNullOrWhiteSpace(invNo)) continue;

            list.Add(new GstPortalInvoice
            {
                SupplierGstin = CleanGstin(gstin),
                SupplierTradeName = idxName >= 0 && idxName < cols.Count ? cols[idxName].Trim() : string.Empty,
                InvoiceNumber = invNo,
                InvoiceDate = idxDate >= 0 && idxDate < cols.Count ? ParseDate(cols[idxDate]) : DateTime.Today,
                InvoiceValue = idxVal >= 0 && idxVal < cols.Count ? ParseDecimal(cols[idxVal]) : 0m,
                TaxableValue = idxTaxable >= 0 && idxTaxable < cols.Count ? ParseDecimal(cols[idxTaxable]) : 0m,
                IntegratedTax = idxIgst >= 0 && idxIgst < cols.Count ? ParseDecimal(cols[idxIgst]) : 0m,
                CentralTax = idxCgst >= 0 && idxCgst < cols.Count ? ParseDecimal(cols[idxCgst]) : 0m,
                StateTax = idxSgst >= 0 && idxSgst < cols.Count ? ParseDecimal(cols[idxSgst]) : 0m,
                SourceFile = Path.GetFileName(filePath)
            });
        }

        return list;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var current = new System.Text.StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '\"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        result.Add(current.ToString());
        return result;
    }

    private static int FindHeaderIndex(string[] headers, params string[] patterns)
    {
        for (int i = 0; i < headers.Length; i++)
        {
            foreach (var p in patterns)
            {
                if (headers[i].Contains(p, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
        }
        return -1;
    }

    public static string NormalizeInvoiceNumber(string? invNo)
    {
        if (string.IsNullOrWhiteSpace(invNo)) return string.Empty;
        var cleaned = Regex.Replace(invNo.Trim().ToUpperInvariant(), @"[^A-Z0-9]", "");
        return cleaned.TrimStart('0');
    }

    private static string CleanGstin(string? gstin)
    {
        if (string.IsNullOrWhiteSpace(gstin)) return string.Empty;
        return Regex.Replace(gstin.Trim().ToUpperInvariant(), @"[^A-Z0-9]", "");
    }

    private static DateTime ParseDate(string? dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr)) return DateTime.Today;

        string[] formats = { "dd-MM-yyyy", "dd/MM/yyyy", "yyyy-MM-dd", "d-M-yyyy", "d/M/yyyy", "dd-MMM-yyyy", "dd-MMM-yy" };
        if (DateTime.TryParseExact(dateStr.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            return dt;
        }

        if (DateTime.TryParse(dateStr, out var parsed))
        {
            return parsed;
        }

        return DateTime.Today;
    }

    private static decimal ParseDecimal(string? valStr)
    {
        if (string.IsNullOrWhiteSpace(valStr)) return 0m;
        var clean = Regex.Replace(valStr.Trim(), @"[^\d.-]", "");
        return decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0m;
    }
}
