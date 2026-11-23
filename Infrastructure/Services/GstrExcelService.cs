using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using ConceptJewelleryPro.Domain.Entities.GST;

namespace ConceptJewelleryPro.Infrastructure.Services;

/// <summary>
/// Service to export GST returns (GSTR-1, GSTR-3B, Purchase Register, Reconciliation) to Excel (.xlsx) format.
/// Uses a built-in, zero-dependency OpenXML (.xlsx) generator so it never requires external ClosedXML/OpenXml DLLs at runtime.
/// The GSTR-1 workbook strictly mirrors the official GST Offline Utility template (b2b, b2cl, b2cs, cdnr, hsn, docs)
/// plus an executive summary sheet for business auditing.
/// </summary>
public static class GstrExcelService
{
    private static readonly Dictionary<string, string> StateNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["01"] = "01-Jammu and Kashmir",
        ["02"] = "02-Himachal Pradesh",
        ["03"] = "03-Punjab",
        ["04"] = "04-Chandigarh",
        ["05"] = "05-Uttarakhand",
        ["06"] = "06-Haryana",
        ["07"] = "07-Delhi",
        ["08"] = "08-Rajasthan",
        ["09"] = "09-Uttar Pradesh",
        ["10"] = "10-Bihar",
        ["11"] = "11-Sikkim",
        ["12"] = "12-Arunachal Pradesh",
        ["13"] = "13-Nagaland",
        ["14"] = "14-Manipur",
        ["15"] = "15-Mizoram",
        ["16"] = "16-Tripura",
        ["17"] = "17-Meghalaya",
        ["18"] = "18-Assam",
        ["19"] = "19-West Bengal",
        ["20"] = "20-Jharkhand",
        ["21"] = "21-Odisha",
        ["22"] = "22-Chhattisgarh",
        ["23"] = "23-Madhya Pradesh",
        ["24"] = "24-Gujarat",
        ["26"] = "26-Dadra and Nagar Haveli and Daman and Diu",
        ["27"] = "27-Maharashtra",
        ["29"] = "29-Karnataka",
        ["30"] = "30-Goa",
        ["31"] = "31-Lakshadweep",
        ["32"] = "32-Kerala",
        ["33"] = "33-Tamil Nadu",
        ["34"] = "34-Puducherry",
        ["35"] = "35-Andaman and Nicobar Islands",
        ["36"] = "36-Telangana",
        ["37"] = "37-Andhra Pradesh",
        ["38"] = "38-Ladakh",
        ["97"] = "97-Other Territory"
    };

    // ─────────────────────────────────────────────────────────────────────────
    // 1. GSTR-1 EXCEL EXPORT (Strict GST Offline Utility Schema)
    // ─────────────────────────────────────────────────────────────────────────

    public static void ExportGstr1ToFile(Gstr1Report report, string filePath, string gstin, string returnPeriod, string companyName, string sellerStateCode = "21")
    {
        using var workbook = CreateGstr1Workbook(report, gstin, returnPeriod, companyName, sellerStateCode);
        workbook.SaveAs(filePath);
    }

    internal static XLWorkbook CreateGstr1Workbook(Gstr1Report report, string gstin, string returnPeriod, string companyName, string sellerStateCode = "21")
    {
        var wb = new XLWorkbook();

        // 1. Executive Summary Sheet
        BuildGstr1SummarySheet(wb, report, gstin, returnPeriod, companyName);

        // 2. Official Offline Tool Worksheets (lowercase names matching GST offline tool)
        BuildB2BSheet(wb, report, sellerStateCode);
        BuildB2CLSheet(wb, report, sellerStateCode);
        BuildB2CSSheet(wb, report, sellerStateCode);
        BuildCdnrSheet(wb, report, sellerStateCode);

        // HSN Summary split into separate B2B and B2C tabs (+ consolidated hsn)
        var b2bHsn = report.HsnB2BSummaries != null && report.HsnB2BSummaries.Count > 0
            ? report.HsnB2BSummaries
            : new List<Gstr1HsnSummary>();

        var b2cHsn = report.HsnB2CSummaries != null && report.HsnB2CSummaries.Count > 0
            ? report.HsnB2CSummaries
            : (report.B2BInvoices.Count == 0 && report.HsnSummaries != null ? report.HsnSummaries : new List<Gstr1HsnSummary>());

        var allHsn = report.HsnSummaries != null && report.HsnSummaries.Count > 0
            ? report.HsnSummaries
            : b2cHsn.Concat(b2bHsn).ToList();

        BuildHsnSheet(wb, "hsn_b2b", b2bHsn);
        BuildHsnSheet(wb, "hsn_b2c", b2cHsn);
        BuildHsnSheet(wb, "hsn", allHsn);
        BuildDocsSheet(wb, report);

        return wb;
    }

    private static void BuildGstr1SummarySheet(XLWorkbook wb, Gstr1Report report, string gstin, string returnPeriod, string companyName)
    {
        var ws = wb.Worksheets.Add("Summary");
        ws.ShowGridLines = true;

        // Title Header Block
        ws.Cell("A1").Value = "FORM GSTR-1 : DETAILS OF OUTWARD SUPPLIES";
        ws.Cell("A1").Style.Font.Bold = true;
        ws.Cell("A1").Style.Font.FontSize = 14;
        ws.Cell("A1").Style.Font.FontColor = XLColor.FromArgb(30, 58, 138);

        ws.Cell("A2").Value = $"Company Name: {companyName}";
        ws.Cell("A2").Style.Font.Bold = true;
        ws.Cell("A2").Style.Font.FontSize = 11;

        ws.Cell("A3").Value = $"GSTIN: {gstin}  |  Return Period: {returnPeriod}  |  Period Range: {report.FromDate:dd/MM/yyyy} to {report.ToDate:dd/MM/yyyy}";
        ws.Cell("A3").Style.Font.FontSize = 10;
        ws.Cell("A3").Style.Font.FontColor = XLColor.FromArgb(100, 116, 139);

        // Summary Table Header
        int row = 5;
        string[] headers =
        {
            "Table", "Section Description", "Count", "Taxable Value (₹)",
            "Integrated Tax (₹)", "Central Tax (₹)", "State/UT Tax (₹)", "Cess (₹)",
            "Total Tax (₹)", "Total Invoice Value (₹)"
        };

        for (int c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cell(row, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(30, 58, 138); // Navy
            cell.Style.Alignment.Horizontal = c >= 2 ? XLAlignmentHorizontalValues.Right : XLAlignmentHorizontalValues.Left;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }
        ws.Row(row).Height = 24;

        // Section Data Rows
        row++;
        var b2bTaxable = report.B2BInvoices.Sum(x => x.TaxableValue);
        var b2bIgst = report.B2BInvoices.Sum(x => x.IgstAmount);
        var b2bCgst = report.B2BInvoices.Sum(x => x.CgstAmount);
        var b2bSgst = report.B2BInvoices.Sum(x => x.SgstAmount);
        var b2bCess = report.B2BInvoices.Sum(x => x.CessAmount);
        var b2bTax = b2bIgst + b2bCgst + b2bSgst + b2bCess;
        var b2bVal = report.B2BInvoices.Sum(x => x.InvoiceValue);
        WriteSummaryRow(ws, ref row, "4A, 4B", "B2B Taxable Invoices", report.B2BInvoices.Select(x => x.InvoiceNo).Distinct().Count(), b2bTaxable, b2bIgst, b2bCgst, b2bSgst, b2bCess, b2bTax, b2bVal);

        var b2clTaxable = report.B2CLargeInvoices.Sum(x => x.TaxableValue);
        var b2clIgst = report.B2CLargeInvoices.Sum(x => x.IgstAmount);
        var b2clCess = report.B2CLargeInvoices.Sum(x => x.CessAmount);
        var b2clTax = b2clIgst + b2clCess;
        var b2clVal = report.B2CLargeInvoices.Sum(x => x.InvoiceValue);
        WriteSummaryRow(ws, ref row, "5", "B2C Large Invoices (> ₹1/2.5L)", report.B2CLargeInvoices.Count, b2clTaxable, b2clIgst, 0, 0, b2clCess, b2clTax, b2clVal);

        var b2csTaxable = report.B2CSmallSummaries.Sum(x => x.TaxableValue);
        var b2csIgst = report.B2CSmallSummaries.Sum(x => x.IgstAmount);
        var b2csCgst = report.B2CSmallSummaries.Sum(x => x.CgstAmount);
        var b2csSgst = report.B2CSmallSummaries.Sum(x => x.SgstAmount);
        var b2csCess = report.B2CSmallSummaries.Sum(x => x.CessAmount);
        var b2csTax = b2csIgst + b2csCgst + b2csSgst + b2csCess;
        var b2csVal = report.B2CSmallSummaries.Sum(x => x.TotalAmount);
        WriteSummaryRow(ws, ref row, "7", "B2C Small Retail Supplies", report.B2CSmallSummaries.Count, b2csTaxable, b2csIgst, b2csCgst, b2csSgst, b2csCess, b2csTax, b2csVal);

        var cdnrTaxable = report.CreditDebitNotes.Sum(x => x.TaxableValue);
        var cdnrIgst = report.CreditDebitNotes.Sum(x => x.IgstAmount);
        var cdnrCgst = report.CreditDebitNotes.Sum(x => x.CgstAmount);
        var cdnrSgst = report.CreditDebitNotes.Sum(x => x.SgstAmount);
        var cdnrCess = report.CreditDebitNotes.Sum(x => x.CessAmount);
        var cdnrTax = cdnrIgst + cdnrCgst + cdnrSgst + cdnrCess;
        var cdnrVal = report.CreditDebitNotes.Sum(x => x.NoteValue);
        WriteSummaryRow(ws, ref row, "9B", "Credit / Debit Notes (Registered)", report.CreditDebitNotes.Count, cdnrTaxable, cdnrIgst, cdnrCgst, cdnrSgst, cdnrCess, cdnrTax, cdnrVal);

        // Grand Total Row
        int totalRow = row;
        ws.Cell(totalRow, 1).Value = "TOTAL";
        ws.Cell(totalRow, 2).Value = "Total Outward Supplies Liability";
        ws.Range(totalRow, 1, totalRow, 2).Style.Font.Bold = true;

        for (int c = 3; c <= 10; c++)
        {
            string colLetter = XLHelper.GetColumnLetterFromNumber(c);
            ws.Cell(totalRow, c).FormulaA1 = $"SUM({colLetter}6:{colLetter}{totalRow - 1})";
            ws.Cell(totalRow, c).Style.Font.Bold = true;
            ws.Cell(totalRow, c).Style.NumberFormat.Format = c == 3 ? "#,##0" : "₹#,##0.00";
            ws.Cell(totalRow, c).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        }

        var totalRange = ws.Range(totalRow, 1, totalRow, 10);
        totalRange.Style.Fill.BackgroundColor = XLColor.FromArgb(241, 245, 249);
        totalRange.Style.Border.TopBorder = XLBorderStyleValues.Thin;
        totalRange.Style.Border.BottomBorder = XLBorderStyleValues.Double;

        // Border around table
        ws.Range(5, 1, totalRow, 10).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        ws.Range(5, 1, totalRow, 10).Style.Border.OutsideBorder = XLBorderStyleValues.Medium;

        // Table 12 & 13 quick count notes below
        row = totalRow + 2;
        int b2bHsnCount = report.HsnB2BSummaries?.Count ?? 0;
        int b2cHsnCount = report.HsnB2CSummaries?.Count ?? 0;
        int totHsnCount = (report.HsnSummaries != null && report.HsnSummaries.Count > 0) ? report.HsnSummaries.Count : (b2bHsnCount + b2cHsnCount);
        ws.Cell(row, 1).Value = $"Table 12 (HSN Summary): {totHsnCount} items total (B2B: {b2bHsnCount} items in 'hsn_b2b', B2C: {b2cHsnCount} items in 'hsn_b2c')";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Font.FontColor = XLColor.FromArgb(71, 85, 105);

        row++;
        int netDocs = report.DocumentSummaries.Sum(d => d.NetIssuedNumber);
        ws.Cell(row, 1).Value = $"Table 13 (Documents Issued): {netDocs} documents issued ({report.DocumentSummaries.Sum(d => d.CancelledNumber)} cancelled)";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Font.FontColor = XLColor.FromArgb(71, 85, 105);

        ws.Columns(1, 10).AdjustToContents();
    }

    private static void WriteSummaryRow(IXLWorksheet ws, ref int row, string table, string desc, int count, decimal taxable, decimal igst, decimal cgst, decimal sgst, decimal cess, decimal totalTax, decimal val)
    {
        ws.Cell(row, 1).Value = table;
        ws.Cell(row, 2).Value = desc;
        ws.Cell(row, 3).Value = count;
        ws.Cell(row, 4).Value = taxable;
        ws.Cell(row, 5).Value = igst;
        ws.Cell(row, 6).Value = cgst;
        ws.Cell(row, 7).Value = sgst;
        ws.Cell(row, 8).Value = cess;
        ws.Cell(row, 9).Value = totalTax;
        ws.Cell(row, 10).Value = val;

        ws.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Cell(row, 3).Style.NumberFormat.Format = "#,##0";
        for (int c = 4; c <= 10; c++)
        {
            ws.Cell(row, c).Style.NumberFormat.Format = "₹#,##0.00";
            ws.Cell(row, c).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        }

        row++;
    }

    // ── Table 4: b2b sheet ───────────────────────────────────────────────────
    private static void BuildB2BSheet(XLWorkbook wb, Gstr1Report report, string sellerStateCode)
    {
        var ws = wb.Worksheets.Add("b2b");
        ws.ShowGridLines = true;
        ws.SheetView.FreezeRows(1);

        string[] headers =
        {
            "GSTIN/UIN of Recipient",
            "Receiver Name",
            "Invoice Number",
            "Invoice date",
            "Invoice Value",
            "Place Of Supply",
            "Reverse Charge",
            "Applicable % of Tax Rate",
            "Invoice Type",
            "E-Commerce GSTIN",
            "Rate",
            "Taxable Value",
            "Cess Amount"
        };

        ApplySheetHeaders(ws, headers);

        int row = 2;
        foreach (var b in report.B2BInvoices)
        {
            var pos = FormatPos(b.PlaceOfSupply, b.Gstin, sellerStateCode);

            ws.Cell(row, 1).SetValue(b.Gstin?.Trim().ToUpperInvariant() ?? string.Empty);
            ws.Cell(row, 2).SetValue(b.ReceiverName?.Trim() ?? string.Empty);
            ws.Cell(row, 3).SetValue(b.InvoiceNo?.Trim() ?? string.Empty);
            ws.Cell(row, 4).SetValue(b.InvoiceDate.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture));
            ws.Cell(row, 5).SetValue(b.InvoiceValue);
            ws.Cell(row, 6).SetValue(pos);
            ws.Cell(row, 7).SetValue(string.Equals(b.ReverseCharge, "Y", StringComparison.OrdinalIgnoreCase) ? "Y" : "N");
            ws.Cell(row, 8).SetValue(string.Empty); // Applicable % of tax rate (blank for standard)
            ws.Cell(row, 9).SetValue(!string.IsNullOrWhiteSpace(b.InvoiceType) ? b.InvoiceType : "Regular");
            ws.Cell(row, 10).SetValue(string.Empty); // E-Commerce GSTIN
            ws.Cell(row, 11).SetValue(b.GstRate);
            ws.Cell(row, 12).SetValue(b.TaxableValue);
            ws.Cell(row, 13).SetValue(b.CessAmount);

            FormatDataRow(ws, row, new[] { 5, 11, 12, 13 }, new[] { 4 });
            row++;
        }

        ws.Columns(1, headers.Length).AdjustToContents(10.0, 45.0);
    }

    // ── Table 5: b2cl sheet ──────────────────────────────────────────────────
    private static void BuildB2CLSheet(XLWorkbook wb, Gstr1Report report, string sellerStateCode)
    {
        var ws = wb.Worksheets.Add("b2cl");
        ws.ShowGridLines = true;
        ws.SheetView.FreezeRows(1);

        string[] headers =
        {
            "Invoice Number",
            "Invoice date",
            "Invoice Value",
            "Place Of Supply",
            "Applicable % of Tax Rate",
            "Rate",
            "Taxable Value",
            "Cess Amount",
            "E-Commerce GSTIN"
        };

        ApplySheetHeaders(ws, headers);

        int row = 2;
        foreach (var l in report.B2CLargeInvoices)
        {
            var pos = FormatPos(l.PlaceOfSupply, string.Empty, sellerStateCode);

            ws.Cell(row, 1).SetValue(l.InvoiceNo?.Trim() ?? string.Empty);
            ws.Cell(row, 2).SetValue(l.InvoiceDate.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture));
            ws.Cell(row, 3).SetValue(l.InvoiceValue);
            ws.Cell(row, 4).SetValue(pos);
            ws.Cell(row, 5).SetValue(string.Empty);
            ws.Cell(row, 6).SetValue(l.GstRate);
            ws.Cell(row, 7).SetValue(l.TaxableValue);
            ws.Cell(row, 8).SetValue(l.CessAmount);
            ws.Cell(row, 9).SetValue(string.Empty);

            FormatDataRow(ws, row, new[] { 3, 6, 7, 8 }, new[] { 2 });
            row++;
        }

        ws.Columns(1, headers.Length).AdjustToContents(10.0, 40.0);
    }

    // ── Table 7: b2cs sheet ──────────────────────────────────────────────────
    private static void BuildB2CSSheet(XLWorkbook wb, Gstr1Report report, string sellerStateCode)
    {
        var ws = wb.Worksheets.Add("b2cs");
        ws.ShowGridLines = true;
        ws.SheetView.FreezeRows(1);

        string[] headers =
        {
            "Type",
            "Place Of Supply",
            "Applicable % of Tax Rate",
            "Rate",
            "Taxable Value",
            "Cess Amount",
            "E-Commerce GSTIN"
        };

        ApplySheetHeaders(ws, headers);

        int row = 2;
        foreach (var s in report.B2CSmallSummaries)
        {
            if (s.TaxableValue <= 0 && s.CgstAmount <= 0 && s.IgstAmount <= 0)
                continue;

            var pos = FormatPos(s.PlaceOfSupply, string.Empty, sellerStateCode);

            ws.Cell(row, 1).SetValue(!string.IsNullOrWhiteSpace(s.Type) ? s.Type : "OE");
            ws.Cell(row, 2).SetValue(pos);
            ws.Cell(row, 3).SetValue(string.Empty);
            ws.Cell(row, 4).SetValue(s.GstRate);
            ws.Cell(row, 5).SetValue(s.TaxableValue);
            ws.Cell(row, 6).SetValue(s.CessAmount);
            ws.Cell(row, 7).SetValue(string.Empty);

            FormatDataRow(ws, row, new[] { 4, 5, 6 }, Array.Empty<int>());
            row++;
        }

        ws.Columns(1, headers.Length).AdjustToContents(10.0, 35.0);
    }

    // ── Table 9B: cdnr sheet ─────────────────────────────────────────────────
    private static void BuildCdnrSheet(XLWorkbook wb, Gstr1Report report, string sellerStateCode)
    {
        var ws = wb.Worksheets.Add("cdnr");
        ws.ShowGridLines = true;
        ws.SheetView.FreezeRows(1);

        string[] headers =
        {
            "GSTIN/UIN of Recipient",
            "Receiver Name",
            "Note/Refund Voucher Number",
            "Note/Refund Voucher date",
            "Document Type",
            "Place Of Supply",
            "Note/Refund Voucher Value",
            "Applicable % of Tax Rate",
            "Rate",
            "Taxable Value",
            "Cess Amount",
            "Pre GST"
        };

        ApplySheetHeaders(ws, headers);

        int row = 2;
        foreach (var c in report.CreditDebitNotes)
        {
            var pos = FormatPos(c.PlaceOfSupply, c.Gstin, sellerStateCode);

            ws.Cell(row, 1).SetValue(c.Gstin?.Trim().ToUpperInvariant() ?? string.Empty);
            ws.Cell(row, 2).SetValue(c.ReceiverName?.Trim() ?? string.Empty);
            ws.Cell(row, 3).SetValue(c.NoteNumber?.Trim() ?? string.Empty);
            ws.Cell(row, 4).SetValue(c.NoteDate.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture));
            ws.Cell(row, 5).SetValue(string.Equals(c.NoteType, "D", StringComparison.OrdinalIgnoreCase) ? "D" : "C");
            ws.Cell(row, 6).SetValue(pos);
            ws.Cell(row, 7).SetValue(c.NoteValue);
            ws.Cell(row, 8).SetValue(string.Empty);
            ws.Cell(row, 9).SetValue(c.GstRate);
            ws.Cell(row, 10).SetValue(c.TaxableValue);
            ws.Cell(row, 11).SetValue(c.CessAmount);
            ws.Cell(row, 12).SetValue(string.Equals(c.PreGst, "Y", StringComparison.OrdinalIgnoreCase) ? "Y" : "N");

            FormatDataRow(ws, row, new[] { 7, 9, 10, 11 }, new[] { 4 });
            row++;
        }

        ws.Columns(1, headers.Length).AdjustToContents(10.0, 40.0);
    }

    // ── Table 12: HSN Sheets (hsn_b2b, hsn_b2c, hsn) ───────────────────────────
    private static void BuildHsnSheet(XLWorkbook wb, string sheetName, IEnumerable<Gstr1HsnSummary> hsnList)
    {
        var ws = wb.Worksheets.Add(sheetName);
        ws.ShowGridLines = true;
        ws.SheetView.FreezeRows(1);

        string[] headers =
        {
            "HSN",
            "Description",
            "UQC",
            "Total Quantity",
            "Total Value",
            "Taxable Value",
            "Integrated Tax Amount",
            "Central Tax Amount",
            "State/UT Tax Amount",
            "Cess Amount",
            "Rate"
        };

        ApplySheetHeaders(ws, headers);

        int row = 2;
        foreach (var h in hsnList)
        {
            if (h.TaxableValue <= 0 && h.CgstAmount <= 0 && h.IgstAmount <= 0)
                continue;

            ws.Cell(row, 1).SetValue(h.HsnCode?.Trim() ?? string.Empty);
            ws.Cell(row, 2).SetValue(h.Description?.Trim() ?? string.Empty);
            ws.Cell(row, 3).SetValue(!string.IsNullOrWhiteSpace(h.Uqc) ? h.Uqc.Trim().ToUpperInvariant() : "GMS");
            ws.Cell(row, 4).SetValue(h.TotalQuantity);
            ws.Cell(row, 5).SetValue(h.TotalValue);
            ws.Cell(row, 6).SetValue(h.TaxableValue);
            ws.Cell(row, 7).SetValue(h.IgstAmount);
            ws.Cell(row, 8).SetValue(h.CgstAmount);
            ws.Cell(row, 9).SetValue(h.SgstAmount);
            ws.Cell(row, 10).SetValue(h.CessAmount);
            ws.Cell(row, 11).SetValue(h.GstRate);

            // Format Qty (col 4): if PCS integer format, if GMS/weight 3 decimal places
            bool isPcs = string.Equals(h.Uqc?.Trim(), "PCS", StringComparison.OrdinalIgnoreCase);
            ws.Cell(row, 4).Style.NumberFormat.Format = isPcs ? "#,##0" : "#,##0.000";
            ws.Cell(row, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            for (int c = 5; c <= 10; c++)
            {
                ws.Cell(row, c).Style.NumberFormat.Format = "#,##0.00";
                ws.Cell(row, c).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            }

            ws.Cell(row, 11).Style.NumberFormat.Format = "0.00";
            ws.Cell(row, 11).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            ws.Row(row).Height = 20;
            row++;
        }

        ws.Columns(1, headers.Length).AdjustToContents(10.0, 45.0);
    }

    // ── Table 13: docs sheet ─────────────────────────────────────────────────
    private static void BuildDocsSheet(XLWorkbook wb, Gstr1Report report)
    {
        var ws = wb.Worksheets.Add("docs");
        ws.ShowGridLines = true;
        ws.SheetView.FreezeRows(1);

        string[] headers =
        {
            "Nature of Document",
            "Sr. No. From",
            "Sr. No. To",
            "Total Number",
            "Cancelled"
        };

        ApplySheetHeaders(ws, headers);

        int row = 2;
        foreach (var d in report.DocumentSummaries)
        {
            ws.Cell(row, 1).SetValue(!string.IsNullOrWhiteSpace(d.DocDescription) ? d.DocDescription : "Invoices for outward supply");
            ws.Cell(row, 2).SetValue(d.FromSerialNo?.Trim() ?? string.Empty);
            ws.Cell(row, 3).SetValue(d.ToSerialNo?.Trim() ?? string.Empty);
            ws.Cell(row, 4).SetValue(d.TotalNumber);
            ws.Cell(row, 5).SetValue(d.CancelledNumber);

            ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            ws.Row(row).Height = 20;
            row++;
        }

        ws.Columns(1, headers.Length).AdjustToContents(12.0, 40.0);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 2. GSTR-3B EXCEL EXPORT
    // ─────────────────────────────────────────────────────────────────────────

    public static void ExportGstr3BToFile(Gstr3BReport report, string filePath, string gstin, string returnPeriod, string companyName)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("GSTR-3B");
        ws.ShowGridLines = true;

        // Title Header
        ws.Cell("A1").Value = "FORM GSTR-3B : MONTHLY SUMMARY RETURN";
        ws.Cell("A1").Style.Font.Bold = true;
        ws.Cell("A1").Style.Font.FontSize = 14;
        ws.Cell("A1").Style.Font.FontColor = XLColor.FromArgb(30, 58, 138);

        ws.Cell("A2").Value = $"Company: {companyName}  |  GSTIN: {gstin}  |  Period: {returnPeriod}";
        ws.Cell("A2").Style.Font.Bold = true;
        ws.Cell("A2").Style.Font.FontSize = 10;
        ws.Cell("A2").Style.Font.FontColor = XLColor.FromArgb(100, 116, 139);

        int row = 4;

        // Table 3.1
        ws.Cell(row, 1).Value = "3.1 Details of Outward Supplies and inward supplies liable to reverse charge";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Font.FontSize = 11;
        row++;

        string[] col31 = { "Nature of Supplies", "Total Taxable Value (₹)", "Integrated Tax (₹)", "Central Tax (₹)", "State/UT Tax (₹)", "Cess (₹)" };
        for (int c = 0; c < col31.Length; c++)
        {
            var cell = ws.Cell(row, c + 1);
            cell.Value = col31[c];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(30, 58, 138);
            cell.Style.Alignment.Horizontal = c == 0 ? XLAlignmentHorizontalValues.Left : XLAlignmentHorizontalValues.Right;
        }
        ws.Row(row).Height = 22;
        row++;

        var ow = report.OutwardTaxableSupplies;
        WriteGstr3BRow(ws, ref row, "(a) Outward taxable supplies (other than zero rated, nil rated and exempted)", ow.TotalTaxableValue, ow.IntegratedTax, ow.CentralTax, ow.StateTax, ow.Cess);
        WriteGstr3BRow(ws, ref row, "(b) Outward taxable supplies (zero rated)", 0, 0, 0, 0, 0);
        WriteGstr3BRow(ws, ref row, "(c) Other outward supplies (Nil rated, exempted)", 0, 0, 0, 0, 0);
        WriteGstr3BRow(ws, ref row, "(d) Inward supplies liable to reverse charge", 0, 0, 0, 0, 0);
        WriteGstr3BRow(ws, ref row, "(e) Non-GST outward supplies", 0, 0, 0, 0, 0);

        row++; // Space

        // Table 4
        ws.Cell(row, 1).Value = "4. Eligible Input Tax Credit (ITC)";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Font.FontSize = 11;
        row++;

        string[] col4 = { "Details", "Total Taxable Value (₹)", "Integrated Tax (₹)", "Central Tax (₹)", "State/UT Tax (₹)", "Cess (₹)" };
        for (int c = 0; c < col4.Length; c++)
        {
            var cell = ws.Cell(row, c + 1);
            cell.Value = col4[c];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(5, 150, 105); // Green
            cell.Style.Alignment.Horizontal = c == 0 ? XLAlignmentHorizontalValues.Left : XLAlignmentHorizontalValues.Right;
        }
        ws.Row(row).Height = 22;
        row++;

        var itc = report.NetItcAvailable;
        WriteGstr3BRow(ws, ref row, "(A) (5) All other ITC (Purchases / Expenses)", 0, itc.IntegratedTax, itc.CentralTax, itc.StateTax, itc.Cess);
        WriteGstr3BRow(ws, ref row, "(C) Net ITC Available (A - B)", 0, itc.IntegratedTax, itc.CentralTax, itc.StateTax, itc.Cess);

        row++; // Space

        // Table 6.1
        ws.Cell(row, 1).Value = "6.1 Payment of Tax (Cash Liability)";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Font.FontSize = 11;
        row++;

        for (int c = 0; c < col31.Length; c++)
        {
            var cell = ws.Cell(row, c + 1);
            cell.Value = col31[c];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(124, 58, 237); // Purple
            cell.Style.Alignment.Horizontal = c == 0 ? XLAlignmentHorizontalValues.Left : XLAlignmentHorizontalValues.Right;
        }
        ws.Row(row).Height = 22;
        row++;

        var cash = report.NetTaxPayableInCash;
        WriteGstr3BRow(ws, ref row, "Tax Payable in Cash", 0, cash.IntegratedTax, cash.CentralTax, cash.StateTax, cash.Cess);

        ws.Columns(1, 6).AdjustToContents(15.0, 50.0);
        wb.SaveAs(filePath);
    }

    private static void WriteGstr3BRow(IXLWorksheet ws, ref int row, string desc, decimal txval, decimal igst, decimal cgst, decimal sgst, decimal cess)
    {
        ws.Cell(row, 1).Value = desc;
        ws.Cell(row, 2).Value = txval;
        ws.Cell(row, 3).Value = igst;
        ws.Cell(row, 4).Value = cgst;
        ws.Cell(row, 5).Value = sgst;
        ws.Cell(row, 6).Value = cess;

        for (int c = 2; c <= 6; c++)
        {
            ws.Cell(row, c).Style.NumberFormat.Format = "₹#,##0.00";
            ws.Cell(row, c).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        }
        ws.Row(row).Height = 20;
        row++;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 3. PURCHASE REGISTER EXCEL EXPORT
    // ─────────────────────────────────────────────────────────────────────────

    public static void ExportPurchaseRegisterToFile(IEnumerable<GstPurchaseRegisterItem> items, string filePath, string companyName, DateTime fromDate, DateTime toDate)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Purchase Register");
        ws.ShowGridLines = true;
        ws.SheetView.FreezeRows(4);

        // Header block
        ws.Cell("A1").Value = $"{companyName} - GST PURCHASE REGISTER";
        ws.Cell("A1").Style.Font.Bold = true;
        ws.Cell("A1").Style.Font.FontSize = 13;
        ws.Cell("A1").Style.Font.FontColor = XLColor.FromArgb(30, 58, 138);

        ws.Cell("A2").Value = $"Period: {fromDate:dd/MM/yyyy} to {toDate:dd/MM/yyyy}  |  Exported: {DateTime.Now:dd/MM/yyyy HH:mm}";
        ws.Cell("A2").Style.Font.FontSize = 10;
        ws.Cell("A2").Style.Font.FontColor = XLColor.FromArgb(100, 116, 139);

        int row = 4;
        string[] headers =
        {
            "Vou No", "Supplier Bill No", "Bill Date", "Supplier Name", "Supplier GSTIN",
            "Voucher Type", "Gross Wt (g)", "Net Wt (g)", "Taxable Amount (₹)", "GST Rate (%)",
            "IGST (₹)", "CGST (₹)", "SGST (₹)", "Total Tax (₹)", "Net Amount (₹)"
        };

        for (int c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cell(row, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(30, 58, 138);
            cell.Style.Alignment.Horizontal = c is >= 6 and <= 14 ? XLAlignmentHorizontalValues.Right : XLAlignmentHorizontalValues.Left;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }
        ws.Row(row).Height = 24;

        int startDataRow = 5;
        row = startDataRow;
        foreach (var p in items)
        {
            ws.Cell(row, 1).SetValue(p.VouNo);
            ws.Cell(row, 2).SetValue(p.SuppBillNo);
            ws.Cell(row, 3).SetValue(p.BillDate.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture));
            ws.Cell(row, 4).SetValue(p.SupplierName);
            ws.Cell(row, 5).SetValue(p.SupplierGstin);
            ws.Cell(row, 6).SetValue(p.VoucherType);
            ws.Cell(row, 7).SetValue(p.GrossWt);
            ws.Cell(row, 8).SetValue(p.NetWt);
            ws.Cell(row, 9).SetValue(p.TaxableAmount);
            ws.Cell(row, 10).SetValue(p.GstRate);
            ws.Cell(row, 11).SetValue(p.IgstAmount);
            ws.Cell(row, 12).SetValue(p.CgstAmount);
            ws.Cell(row, 13).SetValue(p.SgstAmount);
            ws.Cell(row, 14).SetValue(p.TotalTax);
            ws.Cell(row, 15).SetValue(p.NetAmount);

            ws.Cell(row, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(row, 7).Style.NumberFormat.Format = "#,##0.000";
            ws.Cell(row, 8).Style.NumberFormat.Format = "#,##0.000";
            ws.Cell(row, 9).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 10).Style.NumberFormat.Format = "0.00";
            ws.Cell(row, 11).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 12).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 13).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 14).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 15).Style.NumberFormat.Format = "#,##0.00";

            for (int c = 7; c <= 15; c++)
            {
                ws.Cell(row, c).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            }

            ws.Row(row).Height = 20;
            row++;
        }

        if (row > startDataRow)
        {
            // Totals Row
            ws.Cell(row, 1).Value = "TOTAL";
            ws.Range(row, 1, row, 6).Merge();
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            for (int c = 7; c <= 15; c++)
            {
                if (c == 10) continue; // Rate column doesn't sum
                string colLetter = XLHelper.GetColumnLetterFromNumber(c);
                ws.Cell(row, c).FormulaA1 = $"SUM({colLetter}{startDataRow}:{colLetter}{row - 1})";
                ws.Cell(row, c).Style.Font.Bold = true;
                ws.Cell(row, c).Style.NumberFormat.Format = (c is 7 or 8) ? "#,##0.000" : "#,##0.00";
                ws.Cell(row, c).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            }

            var totRange = ws.Range(row, 1, row, 15);
            totRange.Style.Fill.BackgroundColor = XLColor.FromArgb(241, 245, 249);
            totRange.Style.Border.TopBorder = XLBorderStyleValues.Thin;
            totRange.Style.Border.BottomBorder = XLBorderStyleValues.Double;
        }

        ws.Columns(1, headers.Length).AdjustToContents(10.0, 40.0);
        wb.SaveAs(filePath);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 4. RECONCILIATION EXCEL EXPORT
    // ─────────────────────────────────────────────────────────────────────────

    public static void ExportReconciliationToFile(IEnumerable<GstReconciliationItem> items, GstReconciliationSummary summary, string filePath, string companyName, DateTime fromDate, DateTime toDate)
    {
        using var wb = new XLWorkbook();

        // Sheet 1: Summary KPI Sheet
        var wsSumm = wb.Worksheets.Add("Summary");
        wsSumm.ShowGridLines = true;

        wsSumm.Cell("A1").Value = $"{companyName} - GSTR-2B / ERP RECONCILIATION REPORT";
        wsSumm.Cell("A1").Style.Font.Bold = true;
        wsSumm.Cell("A1").Style.Font.FontSize = 13;
        wsSumm.Cell("A1").Style.Font.FontColor = XLColor.FromArgb(30, 58, 138);

        wsSumm.Cell("A2").Value = $"Period: {fromDate:dd/MM/yyyy} to {toDate:dd/MM/yyyy}  |  Generated: {DateTime.Now:dd/MM/yyyy HH:mm}";
        wsSumm.Cell("A2").Style.Font.FontSize = 10;
        wsSumm.Cell("A2").Style.Font.FontColor = XLColor.FromArgb(100, 116, 139);

        int r = 4;
        string[] kpiHeaders = { "Status Category", "Count", "Taxable Value (₹)", "Total Tax (₹)" };
        for (int c = 0; c < kpiHeaders.Length; c++)
        {
            var cell = wsSumm.Cell(r, c + 1);
            cell.Value = kpiHeaders[c];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(30, 58, 138);
            cell.Style.Alignment.Horizontal = c == 0 ? XLAlignmentHorizontalValues.Left : XLAlignmentHorizontalValues.Right;
        }
        wsSumm.Row(r).Height = 22;
        r++;

        var itemList = items.ToList();
        var matchedTaxable = itemList.Where(x => x.Status == GstMatchStatus.Matched).Sum(x => x.BooksTaxableValue);
        var matchedTax = summary.MatchedTaxAmount > 0 ? summary.MatchedTaxAmount : itemList.Where(x => x.Status == GstMatchStatus.Matched).Sum(x => x.BooksTotalTax);

        var mismatchTaxable = itemList.Where(x => x.Status is GstMatchStatus.MismatchInTaxable or GstMatchStatus.MismatchInTax or GstMatchStatus.MismatchInDate).Sum(x => x.BooksTaxableValue);
        var mismatchTax = itemList.Where(x => x.Status is GstMatchStatus.MismatchInTaxable or GstMatchStatus.MismatchInTax or GstMatchStatus.MismatchInDate).Sum(x => x.BooksTotalTax);

        var missing2BTaxable = itemList.Where(x => x.Status == GstMatchStatus.MissingIn2B).Sum(x => x.BooksTaxableValue);
        var missing2BTax = summary.MissingIn2BTaxAmount > 0 ? summary.MissingIn2BTaxAmount : itemList.Where(x => x.Status == GstMatchStatus.MissingIn2B).Sum(x => x.BooksTotalTax);

        var missingBooksTaxable = itemList.Where(x => x.Status == GstMatchStatus.MissingInBooks).Sum(x => x.PortalTaxableValue);
        var missingBooksTax = summary.MissingInBooksTaxAmount > 0 ? summary.MissingInBooksTaxAmount : itemList.Where(x => x.Status == GstMatchStatus.MissingInBooks).Sum(x => x.PortalTotalTax);

        WriteKpiRow(wsSumm, ref r, "Matched Invoices (Safe ITC)", summary.MatchedCount, matchedTaxable, matchedTax, XLColor.FromArgb(220, 252, 231));
        WriteKpiRow(wsSumm, ref r, "Tax / Amount Mismatches", summary.MismatchCount, mismatchTaxable, mismatchTax, XLColor.FromArgb(254, 243, 199));
        WriteKpiRow(wsSumm, ref r, "Missing in 2B (In Books Only - Risk ITC)", summary.MissingIn2BCount, missing2BTaxable, missing2BTax, XLColor.FromArgb(254, 226, 226));
        WriteKpiRow(wsSumm, ref r, "Missing in Books (In 2B Only - Unclaimed)", summary.MissingInBooksCount, missingBooksTaxable, missingBooksTax, XLColor.FromArgb(224, 231, 255));

        wsSumm.Columns(1, 4).AdjustToContents();

        // Sheet 2: Detailed Line Items
        var wsDet = wb.Worksheets.Add("Reconciliation Details");
        wsDet.ShowGridLines = true;
        wsDet.SheetView.FreezeRows(1);

        string[] detHeaders =
        {
            "Status", "Books Bill No", "Books Date", "Books Supplier", "Books GSTIN",
            "Books Taxable (₹)", "Books Tax (₹)", "Portal Bill No", "Portal Date",
            "Portal Supplier", "Portal GSTIN", "Portal Taxable (₹)", "Portal Tax (₹)",
            "Tax Diff (₹)", "Remarks"
        };

        ApplySheetHeaders(wsDet, detHeaders);

        int detRow = 2;
        foreach (var item in itemList)
        {
            wsDet.Cell(detRow, 1).SetValue(item.StatusText);
            wsDet.Cell(detRow, 2).SetValue(item.BooksBillNo ?? string.Empty);
            wsDet.Cell(detRow, 3).SetValue(item.BooksDate?.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture) ?? string.Empty);
            wsDet.Cell(detRow, 4).SetValue(item.BooksSupplierName ?? string.Empty);
            wsDet.Cell(detRow, 5).SetValue(item.BooksGstin ?? string.Empty);
            wsDet.Cell(detRow, 6).SetValue(item.BooksTaxableValue);
            wsDet.Cell(detRow, 7).SetValue(item.BooksTotalTax);
            wsDet.Cell(detRow, 8).SetValue(item.PortalBillNo ?? string.Empty);
            wsDet.Cell(detRow, 9).SetValue(item.PortalDate?.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture) ?? string.Empty);
            wsDet.Cell(detRow, 10).SetValue(item.PortalSupplierName ?? string.Empty);
            wsDet.Cell(detRow, 11).SetValue(item.PortalGstin ?? string.Empty);
            wsDet.Cell(detRow, 12).SetValue(item.PortalTaxableValue);
            wsDet.Cell(detRow, 13).SetValue(item.PortalTotalTax);
            wsDet.Cell(detRow, 14).SetValue(item.DifferenceTax);
            wsDet.Cell(detRow, 15).SetValue(item.Remarks ?? string.Empty);

            // Conditional Row Coloring based on Status
            var rowColor = item.Status switch
            {
                GstMatchStatus.Matched => XLColor.FromArgb(240, 253, 244),
                GstMatchStatus.MissingIn2B => XLColor.FromArgb(254, 242, 242),
                GstMatchStatus.MissingInBooks => XLColor.FromArgb(245, 243, 255),
                _ => XLColor.FromArgb(254, 252, 232)
            };
            wsDet.Range(detRow, 1, detRow, 15).Style.Fill.BackgroundColor = rowColor;

            foreach (var c in new[] { 6, 7, 12, 13, 14 })
            {
                wsDet.Cell(detRow, c).Style.NumberFormat.Format = "#,##0.00";
                wsDet.Cell(detRow, c).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            }

            wsDet.Cell(detRow, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            wsDet.Cell(detRow, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            wsDet.Row(detRow).Height = 20;

            detRow++;
        }

        wsDet.Columns(1, detHeaders.Length).AdjustToContents(10.0, 35.0);
        wb.SaveAs(filePath);
    }

    private static void WriteKpiRow(IXLWorksheet ws, ref int r, string desc, int count, decimal taxable, decimal tax, XLColor bg)
    {
        ws.Cell(r, 1).Value = desc;
        ws.Cell(r, 2).Value = count;
        ws.Cell(r, 3).Value = taxable;
        ws.Cell(r, 4).Value = tax;

        ws.Cell(r, 2).Style.NumberFormat.Format = "#,##0";
        ws.Cell(r, 3).Style.NumberFormat.Format = "₹#,##0.00";
        ws.Cell(r, 4).Style.NumberFormat.Format = "₹#,##0.00";

        ws.Cell(r, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        ws.Cell(r, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        ws.Cell(r, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        ws.Range(r, 1, r, 4).Style.Fill.BackgroundColor = bg;
        ws.Row(r).Height = 22;
        r++;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helper Formatting Methods
    // ─────────────────────────────────────────────────────────────────────────

    private static void ApplySheetHeaders(IXLWorksheet ws, string[] headers)
    {
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontSize = 10;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(30, 58, 138); // Official Header Blue
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }
        ws.Row(1).Height = 26;
    }

    private static void FormatDataRow(IXLWorksheet ws, int row, int[] numericCols, int[] dateCols)
    {
        foreach (var col in numericCols)
        {
            ws.Cell(row, col).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, col).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        }

        foreach (var col in dateCols)
        {
            ws.Cell(row, col).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        ws.Row(row).Height = 20;
    }

    private static string FormatPos(string? pos, string? gstin, string defaultState = "21")
    {
        string code = string.Empty;
        if (!string.IsNullOrWhiteSpace(pos))
        {
            var trimmed = pos.Trim();
            if (trimmed.Length >= 2 && char.IsDigit(trimmed[0]) && char.IsDigit(trimmed[1]))
            {
                code = trimmed.Substring(0, 2);
            }
        }

        if (string.IsNullOrEmpty(code) && !string.IsNullOrWhiteSpace(gstin) && gstin.Trim().Length >= 2 && char.IsDigit(gstin.Trim()[0]) && char.IsDigit(gstin.Trim()[1]))
        {
            code = gstin.Trim().Substring(0, 2);
        }

        if (string.IsNullOrEmpty(code))
        {
            code = defaultState;
        }

        if (StateNames.TryGetValue(code, out var fullState))
        {
            return fullState;
        }

        return code.PadLeft(2, '0');
    }

    // ═════════════════════════════════════════════════════════════════════════
    // BUILT-IN ZERO-DEPENDENCY OPENXML (.XLSX) ENGINE
    // ═════════════════════════════════════════════════════════════════════════

    internal enum XLAlignmentHorizontalValues { General, Left, Center, Right }
    internal enum XLAlignmentVerticalValues { Bottom, Center, Top }
    internal enum XLBorderStyleValues { None, Thin, Medium, Double }

    internal readonly struct XLColor : IEquatable<XLColor>
    {
        public string? ArgbHex { get; }
        private XLColor(string? argbHex) => ArgbHex = argbHex;
        public static XLColor Empty => new(null);
        public static XLColor White => FromArgb(255, 255, 255);
        public static XLColor FromArgb(int r, int g, int b) =>
            new($"FF{Clamp(r):X2}{Clamp(g):X2}{Clamp(b):X2}");
        private static int Clamp(int v) => v < 0 ? 0 : (v > 255 ? 255 : v);
        public bool Equals(XLColor other) => string.Equals(ArgbHex, other.ArgbHex, StringComparison.OrdinalIgnoreCase);
        public override bool Equals(object? obj) => obj is XLColor c && Equals(c);
        public override int GetHashCode() => ArgbHex?.ToUpperInvariant().GetHashCode() ?? 0;
    }

    internal static class XLHelper
    {
        public static string GetColumnLetterFromNumber(int columnNumber)
        {
            var dividend = columnNumber;
            var columnName = string.Empty;
            while (dividend > 0)
            {
                var modulo = (dividend - 1) % 26;
                columnName = Convert.ToChar('A' + modulo) + columnName;
                dividend = (dividend - modulo) / 26;
            }
            return columnName;
        }

        public static (int Row, int Col) ParseCellAddress(string address)
        {
            int i = 0;
            int col = 0;
            while (i < address.Length && char.IsLetter(address[i]))
            {
                col = col * 26 + (char.ToUpperInvariant(address[i]) - 'A' + 1);
                i++;
            }
            int row = int.Parse(address.Substring(i), CultureInfo.InvariantCulture);
            return (row, col);
        }
    }

    internal interface IXLStyle
    {
        IXLFont Font { get; }
        IXLFill Fill { get; }
        IXLAlignment Alignment { get; }
        IXLNumberFormat NumberFormat { get; }
        IXLBorder Border { get; }
    }

    internal interface IXLFont
    {
        bool Bold { get; set; }
        double FontSize { get; set; }
        XLColor FontColor { get; set; }
    }

    internal interface IXLFill
    {
        XLColor BackgroundColor { get; set; }
    }

    internal interface IXLAlignment
    {
        XLAlignmentHorizontalValues Horizontal { get; set; }
        XLAlignmentVerticalValues Vertical { get; set; }
    }

    internal interface IXLNumberFormat
    {
        string Format { get; set; }
    }

    internal interface IXLBorder
    {
        XLBorderStyleValues TopBorder { get; set; }
        XLBorderStyleValues BottomBorder { get; set; }
        XLBorderStyleValues LeftBorder { get; set; }
        XLBorderStyleValues RightBorder { get; set; }
        XLBorderStyleValues InsideBorder { set; }
        XLBorderStyleValues OutsideBorder { set; }
    }

    internal sealed class CellStyleData : IXLStyle, IXLFont, IXLFill, IXLAlignment, IXLNumberFormat, IXLBorder
    {
        public bool Bold { get; set; }
        public double FontSize { get; set; } = 11;
        public XLColor FontColor { get; set; } = XLColor.Empty;
        public XLColor BackgroundColor { get; set; } = XLColor.Empty;
        public XLAlignmentHorizontalValues Horizontal { get; set; } = XLAlignmentHorizontalValues.General;
        public XLAlignmentVerticalValues Vertical { get; set; } = XLAlignmentVerticalValues.Bottom;
        public string Format { get; set; } = string.Empty;
        public XLBorderStyleValues TopBorder { get; set; }
        public XLBorderStyleValues BottomBorder { get; set; }
        public XLBorderStyleValues LeftBorder { get; set; }
        public XLBorderStyleValues RightBorder { get; set; }

        public XLBorderStyleValues InsideBorder
        {
            set
            {
                TopBorder = value;
                BottomBorder = value;
                LeftBorder = value;
                RightBorder = value;
            }
        }

        public XLBorderStyleValues OutsideBorder
        {
            set
            {
                TopBorder = value;
                BottomBorder = value;
                LeftBorder = value;
                RightBorder = value;
            }
        }

        public IXLFont Font => this;
        public IXLFill Fill => this;
        public IXLAlignment Alignment => this;
        public IXLNumberFormat NumberFormat => this;
        public IXLBorder Border => this;

        public bool IsDefault =>
            !Bold &&
            Math.Abs(FontSize - 11) < 0.01 &&
            FontColor.ArgbHex == null &&
            BackgroundColor.ArgbHex == null &&
            Horizontal == XLAlignmentHorizontalValues.General &&
            Vertical == XLAlignmentVerticalValues.Bottom &&
            string.IsNullOrEmpty(Format) &&
            TopBorder == XLBorderStyleValues.None &&
            BottomBorder == XLBorderStyleValues.None &&
            LeftBorder == XLBorderStyleValues.None &&
            RightBorder == XLBorderStyleValues.None;
    }

    internal sealed class XLCell
    {
        public int Row { get; }
        public int Col { get; }
        public object? Value { get; set; }
        public string? FormulaA1 { get; set; }
        public CellStyleData StyleData { get; } = new();
        public IXLStyle Style => StyleData;

        public XLCell(int row, int col)
        {
            Row = row;
            Col = col;
        }

        public XLCell SetValue<T>(T value)
        {
            Value = value;
            return this;
        }
    }

    internal sealed class XLRow
    {
        public double? Height { get; set; }
    }

    internal sealed class XLSheetView
    {
        public int FrozenRows { get; private set; }
        public void FreezeRows(int rows) => FrozenRows = rows;
    }

    internal sealed class XLColumns
    {
        private readonly XLWorksheet _ws;
        private readonly int _fromCol;
        private readonly int _toCol;

        public XLColumns(XLWorksheet ws, int fromCol, int toCol)
        {
            _ws = ws;
            _fromCol = fromCol;
            _toCol = toCol;
        }

        public void AdjustToContents(double minWidth = 8.0, double maxWidth = 60.0)
        {
            _ws.AdjustColumns(_fromCol, _toCol, minWidth, maxWidth);
        }
    }

    internal sealed class XLRange : IXLStyle, IXLFont, IXLFill, IXLAlignment, IXLNumberFormat, IXLBorder
    {
        private readonly XLWorksheet _ws;
        private readonly int _r1, _c1, _r2, _c2;

        public XLRange(XLWorksheet ws, int r1, int c1, int r2, int c2)
        {
            _ws = ws;
            _r1 = Math.Min(r1, r2);
            _c1 = Math.Min(c1, c2);
            _r2 = Math.Max(r1, r2);
            _c2 = Math.Max(c1, c2);
        }

        public IXLStyle Style => this;
        public IXLFont Font => this;
        public IXLFill Fill => this;
        public IXLAlignment Alignment => this;
        public IXLNumberFormat NumberFormat => this;
        public IXLBorder Border => this;

        public void Merge() => _ws.AddMergedRange(_r1, _c1, _r2, _c2);

        private IEnumerable<XLCell> Cells()
        {
            for (int r = _r1; r <= _r2; r++)
                for (int c = _c1; c <= _c2; c++)
                    yield return _ws.Cell(r, c);
        }

        public bool Bold { get => _ws.Cell(_r1, _c1).StyleData.Bold; set { foreach (var c in Cells()) c.StyleData.Bold = value; } }
        public double FontSize { get => _ws.Cell(_r1, _c1).StyleData.FontSize; set { foreach (var c in Cells()) c.StyleData.FontSize = value; } }
        public XLColor FontColor { get => _ws.Cell(_r1, _c1).StyleData.FontColor; set { foreach (var c in Cells()) c.StyleData.FontColor = value; } }
        public XLColor BackgroundColor { get => _ws.Cell(_r1, _c1).StyleData.BackgroundColor; set { foreach (var c in Cells()) c.StyleData.BackgroundColor = value; } }
        public XLAlignmentHorizontalValues Horizontal { get => _ws.Cell(_r1, _c1).StyleData.Horizontal; set { foreach (var c in Cells()) c.StyleData.Horizontal = value; } }
        public XLAlignmentVerticalValues Vertical { get => _ws.Cell(_r1, _c1).StyleData.Vertical; set { foreach (var c in Cells()) c.StyleData.Vertical = value; } }
        public string Format { get => _ws.Cell(_r1, _c1).StyleData.Format; set { foreach (var c in Cells()) c.StyleData.Format = value; } }

        public XLBorderStyleValues TopBorder
        {
            get => _ws.Cell(_r1, _c1).StyleData.TopBorder;
            set { for (int c = _c1; c <= _c2; c++) _ws.Cell(_r1, c).StyleData.TopBorder = value; }
        }

        public XLBorderStyleValues BottomBorder
        {
            get => _ws.Cell(_r2, _c1).StyleData.BottomBorder;
            set { for (int c = _c1; c <= _c2; c++) _ws.Cell(_r2, c).StyleData.BottomBorder = value; }
        }

        public XLBorderStyleValues LeftBorder
        {
            get => _ws.Cell(_r1, _c1).StyleData.LeftBorder;
            set { for (int r = _r1; r <= _r2; r++) _ws.Cell(r, _c1).StyleData.LeftBorder = value; }
        }

        public XLBorderStyleValues RightBorder
        {
            get => _ws.Cell(_r1, _c2).StyleData.RightBorder;
            set { for (int r = _r1; r <= _r2; r++) _ws.Cell(r, _c2).StyleData.RightBorder = value; }
        }

        public XLBorderStyleValues InsideBorder
        {
            set
            {
                for (int r = _r1; r <= _r2; r++)
                {
                    for (int c = _c1; c <= _c2; c++)
                    {
                        var cell = _ws.Cell(r, c).StyleData;
                        if (r > _r1) cell.TopBorder = value;
                        if (r < _r2) cell.BottomBorder = value;
                        if (c > _c1) cell.LeftBorder = value;
                        if (c < _c2) cell.RightBorder = value;
                    }
                }
            }
        }

        public XLBorderStyleValues OutsideBorder
        {
            set
            {
                TopBorder = value;
                BottomBorder = value;
                LeftBorder = value;
                RightBorder = value;
            }
        }
    }

    internal interface IXLWorksheet
    {
        string Name { get; }
        bool ShowGridLines { get; set; }
        XLSheetView SheetView { get; }
        XLCell Cell(int row, int column);
        XLCell Cell(string address);
        XLRow Row(int row);
        XLRange Range(int firstRow, int firstColumn, int lastRow, int lastColumn);
        XLColumns Columns(int firstColumn, int lastColumn);
    }

    internal sealed class XLWorksheet : IXLWorksheet
    {
        public string Name { get; }
        public bool ShowGridLines { get; set; } = true;
        public XLSheetView SheetView { get; } = new();

        internal readonly SortedDictionary<int, SortedDictionary<int, XLCell>> RowsData = new();
        internal readonly Dictionary<int, XLRow> RowProps = new();
        internal readonly SortedDictionary<int, double> ColumnWidths = new();
        internal readonly List<string> MergedRanges = new();

        public XLWorksheet(string name) => Name = name;

        public XLCell Cell(int row, int column)
        {
            if (!RowsData.TryGetValue(row, out var colMap))
            {
                colMap = new SortedDictionary<int, XLCell>();
                RowsData[row] = colMap;
            }
            if (!colMap.TryGetValue(column, out var cell))
            {
                cell = new XLCell(row, column);
                colMap[column] = cell;
            }
            return cell;
        }

        public XLCell Cell(string address)
        {
            var (r, c) = XLHelper.ParseCellAddress(address);
            return Cell(r, c);
        }

        public XLRow Row(int row)
        {
            if (!RowProps.TryGetValue(row, out var r))
            {
                r = new XLRow();
                RowProps[row] = r;
            }
            return r;
        }

        public XLRange Range(int firstRow, int firstColumn, int lastRow, int lastColumn) =>
            new(this, firstRow, firstColumn, lastRow, lastColumn);

        public XLColumns Columns(int firstColumn, int lastColumn) =>
            new(this, firstColumn, lastColumn);

        internal void AddMergedRange(int r1, int c1, int r2, int c2)
        {
            var refStr = $"{XLHelper.GetColumnLetterFromNumber(c1)}{r1}:{XLHelper.GetColumnLetterFromNumber(c2)}{r2}";
            if (!MergedRanges.Contains(refStr))
                MergedRanges.Add(refStr);
        }

        internal void AdjustColumns(int fromCol, int toCol, double minWidth, double maxWidth)
        {
            for (int c = fromCol; c <= toCol; c++)
            {
                int maxLen = 8;
                foreach (var kvp in RowsData)
                {
                    // Skip title banner rows (1..3) in column 1 when auto-fitting table columns
                    if (c == 1 && kvp.Key <= 3 && kvp.Value.Count == 1)
                        continue;

                    if (kvp.Value.TryGetValue(c, out var cell) && cell.Value != null)
                    {
                        var text = FormatCellTextForWidth(cell.Value, cell.StyleData.Format);
                        if (text.Length > maxLen)
                            maxLen = text.Length;
                    }
                }
                double width = Math.Clamp(maxLen + 4.0, minWidth, maxWidth);
                ColumnWidths[c] = width;
            }
        }

        private static string FormatCellTextForWidth(object val, string format)
        {
            if (val is decimal d)
                return d.ToString("N2", CultureInfo.InvariantCulture) + (format.Contains('₹') ? "  " : "");
            if (val is double db)
                return db.ToString("N2", CultureInfo.InvariantCulture);
            return val.ToString() ?? string.Empty;
        }
    }

    internal sealed class XLWorksheets
    {
        internal readonly List<XLWorksheet> List = new();
        public IXLWorksheet Add(string sheetName)
        {
            var ws = new XLWorksheet(sheetName);
            List.Add(ws);
            return ws;
        }
    }

    internal sealed class XLWorkbook : IDisposable
    {
        public XLWorksheets Worksheets { get; } = new();

        public void Dispose() { }

        public void SaveAs(string filePath)
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var archive = new ZipArchive(fs, ZipArchiveMode.Create, leaveOpen: false);

            var sharedStrings = new List<string>();
            var sharedStringMap = new Dictionary<string, int>(StringComparer.Ordinal);

            int GetSharedStringIndex(string s)
            {
                if (!sharedStringMap.TryGetValue(s, out int idx))
                {
                    idx = sharedStrings.Count;
                    sharedStrings.Add(s);
                    sharedStringMap[s] = idx;
                }
                return idx;
            }

            var styleBuilder = new OpenXmlStyleBuilder();

            // Pre-register styles and shared strings across all worksheets
            var sheetXmlList = new List<(XLWorksheet Sheet, XDocument Doc)>();
            for (int i = 0; i < Worksheets.List.Count; i++)
            {
                var ws = Worksheets.List[i];
                var doc = BuildWorksheetXml(ws, i == 0, styleBuilder, GetSharedStringIndex);
                sheetXmlList.Add((ws, doc));
            }

            // 1. [Content_Types].xml
            WriteXmlEntry(archive, "[Content_Types].xml", BuildContentTypesXml(Worksheets.List.Count));

            // 2. _rels/.rels
            WriteXmlEntry(archive, "_rels/.rels", BuildRootRelsXml());

            // 3. xl/workbook.xml
            WriteXmlEntry(archive, "xl/workbook.xml", BuildWorkbookXml(Worksheets.List));

            // 4. xl/_rels/workbook.xml.rels
            WriteXmlEntry(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRelsXml(Worksheets.List.Count));

            // 5. xl/styles.xml
            WriteXmlEntry(archive, "xl/styles.xml", styleBuilder.BuildStylesXml());

            // 6. xl/sharedStrings.xml
            WriteXmlEntry(archive, "xl/sharedStrings.xml", BuildSharedStringsXml(sharedStrings));

            // 7. xl/worksheets/sheet{N}.xml
            for (int i = 0; i < sheetXmlList.Count; i++)
            {
                WriteXmlEntry(archive, $"xl/worksheets/sheet{i + 1}.xml", sheetXmlList[i].Doc);
            }
        }

        private static void WriteXmlEntry(ZipArchive archive, string entryPath, XDocument doc)
        {
            var entry = archive.CreateEntry(entryPath, CompressionLevel.Optimal);
            using var stream = entry.Open();
            var settings = new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                Indent = false,
                OmitXmlDeclaration = false
            };
            using var writer = XmlWriter.Create(stream, settings);
            doc.Save(writer);
        }

        private static XDocument BuildContentTypesXml(int sheetCount)
        {
            XNamespace ns = "http://schemas.openxmlformats.org/package/2006/content-types";
            var types = new XElement(ns + "Types",
                new XElement(ns + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                new XElement(ns + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")),
                new XElement(ns + "Override", new XAttribute("PartName", "/xl/workbook.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")),
                new XElement(ns + "Override", new XAttribute("PartName", "/xl/styles.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml")),
                new XElement(ns + "Override", new XAttribute("PartName", "/xl/sharedStrings.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml"))
            );

            for (int i = 1; i <= sheetCount; i++)
            {
                types.Add(new XElement(ns + "Override",
                    new XAttribute("PartName", $"/xl/worksheets/sheet{i}.xml"),
                    new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")));
            }

            return new XDocument(new XDeclaration("1.0", "utf-8", "yes"), types);
        }

        private static XDocument BuildRootRelsXml()
        {
            XNamespace ns = "http://schemas.openxmlformats.org/package/2006/relationships";
            return new XDocument(
                new XDeclaration("1.0", "utf-8", "yes"),
                new XElement(ns + "Relationships",
                    new XElement(ns + "Relationship",
                        new XAttribute("Id", "rId1"),
                        new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"),
                        new XAttribute("Target", "xl/workbook.xml"))));
        }

        private static XDocument BuildWorkbookXml(List<XLWorksheet> sheets)
        {
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            XNamespace rNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

            var sheetsEl = new XElement(ns + "sheets");
            for (int i = 0; i < sheets.Count; i++)
            {
                sheetsEl.Add(new XElement(ns + "sheet",
                    new XAttribute("name", sheets[i].Name),
                    new XAttribute("sheetId", i + 1),
                    new XAttribute(rNs + "id", $"rId{i + 1}")));
            }

            return new XDocument(
                new XDeclaration("1.0", "utf-8", "yes"),
                new XElement(ns + "workbook",
                    new XAttribute(XNamespace.Xmlns + "r", rNs.NamespaceName),
                    sheetsEl));
        }

        private static XDocument BuildWorkbookRelsXml(int sheetCount)
        {
            XNamespace ns = "http://schemas.openxmlformats.org/package/2006/relationships";
            var rels = new XElement(ns + "Relationships");

            for (int i = 1; i <= sheetCount; i++)
            {
                rels.Add(new XElement(ns + "Relationship",
                    new XAttribute("Id", $"rId{i}"),
                    new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"),
                    new XAttribute("Target", $"worksheets/sheet{i}.xml")));
            }

            rels.Add(new XElement(ns + "Relationship",
                new XAttribute("Id", $"rId{sheetCount + 1}"),
                new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles"),
                new XAttribute("Target", "styles.xml")));

            rels.Add(new XElement(ns + "Relationship",
                new XAttribute("Id", $"rId{sheetCount + 2}"),
                new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings"),
                new XAttribute("Target", "sharedStrings.xml")));

            return new XDocument(new XDeclaration("1.0", "utf-8", "yes"), rels);
        }

        private static XDocument BuildSharedStringsXml(List<string> strings)
        {
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            var sst = new XElement(ns + "sst",
                new XAttribute("count", strings.Count),
                new XAttribute("uniqueCount", strings.Count));

            foreach (var s in strings)
            {
                var t = new XElement(ns + "t", SanitizeXmlString(s));
                if (s.Length > 0 && (char.IsWhiteSpace(s[0]) || char.IsWhiteSpace(s[^1]) || s.Contains('\n') || s.Contains('\r')))
                {
                    t.SetAttributeValue(XNamespace.Xml + "space", "preserve");
                }
                sst.Add(new XElement(ns + "si", t));
            }

            return new XDocument(new XDeclaration("1.0", "utf-8", "yes"), sst);
        }

        private static string SanitizeXmlString(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            var sb = new StringBuilder(input.Length);
            foreach (var ch in input)
            {
                if (XmlConvert.IsXmlChar(ch))
                    sb.Append(ch);
            }
            return sb.ToString();
        }

        private static XDocument BuildWorksheetXml(
            XLWorksheet ws,
            bool isFirstSheet,
            OpenXmlStyleBuilder styleBuilder,
            Func<string, int> getSharedStringIndex)
        {
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

            var worksheet = new XElement(ns + "worksheet");

            // 1. sheetViews
            var sheetView = new XElement(ns + "sheetView",
                new XAttribute("tabSelected", isFirstSheet ? "1" : "0"),
                new XAttribute("showGridLines", ws.ShowGridLines ? "1" : "0"),
                new XAttribute("workbookViewId", "0"));

            if (ws.SheetView.FrozenRows > 0)
            {
                sheetView.Add(new XElement(ns + "pane",
                    new XAttribute("ySplit", ws.SheetView.FrozenRows),
                    new XAttribute("topLeftCell", $"A{ws.SheetView.FrozenRows + 1}"),
                    new XAttribute("activePane", "bottomLeft"),
                    new XAttribute("state", "frozen")));
            }

            worksheet.Add(new XElement(ns + "sheetViews", sheetView));

            // 2. sheetFormatPr
            worksheet.Add(new XElement(ns + "sheetFormatPr", new XAttribute("defaultRowHeight", "15")));

            // 3. cols
            if (ws.ColumnWidths.Count > 0)
            {
                var colsEl = new XElement(ns + "cols");
                foreach (var kvp in ws.ColumnWidths)
                {
                    colsEl.Add(new XElement(ns + "col",
                        new XAttribute("min", kvp.Key),
                        new XAttribute("max", kvp.Key),
                        new XAttribute("width", kvp.Value.ToString("0.##", CultureInfo.InvariantCulture)),
                        new XAttribute("customWidth", "1")));
                }
                worksheet.Add(colsEl);
            }

            // 4. sheetData
            var sheetData = new XElement(ns + "sheetData");
            foreach (var rowKvp in ws.RowsData)
            {
                int rIdx = rowKvp.Key;
                var rowEl = new XElement(ns + "row", new XAttribute("r", rIdx));
                if (ws.RowProps.TryGetValue(rIdx, out var rProp) && rProp.Height.HasValue)
                {
                    rowEl.SetAttributeValue("ht", rProp.Height.Value.ToString("0.##", CultureInfo.InvariantCulture));
                    rowEl.SetAttributeValue("customHeight", "1");
                }

                foreach (var colKvp in rowKvp.Value)
                {
                    var cell = colKvp.Value;
                    string cellRef = $"{XLHelper.GetColumnLetterFromNumber(cell.Col)}{rIdx}";
                    int styleIdx = styleBuilder.GetStyleIndex(cell.StyleData);

                    var cEl = new XElement(ns + "c", new XAttribute("r", cellRef));
                    if (styleIdx > 0)
                    {
                        cEl.SetAttributeValue("s", styleIdx);
                    }

                    if (!string.IsNullOrWhiteSpace(cell.FormulaA1))
                    {
                        cEl.Add(new XElement(ns + "f", cell.FormulaA1));
                        cEl.Add(new XElement(ns + "v", "0"));
                        rowEl.Add(cEl);
                    }
                    else if (cell.Value is null)
                    {
                        if (styleIdx > 0)
                        {
                            rowEl.Add(cEl);
                        }
                    }
                    else if (cell.Value is int or long or short or byte or decimal or double or float)
                    {
                        var numStr = Convert.ToDecimal(cell.Value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
                        cEl.Add(new XElement(ns + "v", numStr));
                        rowEl.Add(cEl);
                    }
                    else
                    {
                        var strVal = cell.Value.ToString() ?? string.Empty;
                        if (strVal.Length == 0)
                        {
                            if (styleIdx > 0)
                                rowEl.Add(cEl);
                        }
                        else
                        {
                            int sIdx = getSharedStringIndex(strVal);
                            cEl.SetAttributeValue("t", "s");
                            cEl.Add(new XElement(ns + "v", sIdx));
                            rowEl.Add(cEl);
                        }
                    }
                }

                if (rowEl.HasElements || (ws.RowProps.TryGetValue(rIdx, out var rp) && rp.Height.HasValue))
                {
                    sheetData.Add(rowEl);
                }
            }
            worksheet.Add(sheetData);

            // 5. mergeCells (must follow sheetData)
            if (ws.MergedRanges.Count > 0)
            {
                var mergeCells = new XElement(ns + "mergeCells", new XAttribute("count", ws.MergedRanges.Count));
                foreach (var mr in ws.MergedRanges)
                {
                    mergeCells.Add(new XElement(ns + "mergeCell", new XAttribute("ref", mr)));
                }
                worksheet.Add(mergeCells);
            }

            return new XDocument(new XDeclaration("1.0", "utf-8", "yes"), worksheet);
        }
    }

    private sealed class OpenXmlStyleBuilder
    {
        private readonly Dictionary<string, int> _numFmtMap = new(StringComparer.Ordinal);
        private int _nextNumFmtId = 164;

        private readonly List<(bool Bold, double Size, string? Color)> _fonts = new()
        {
            (false, 11, null) // 0: Default font
        };

        private readonly List<string?> _fills = new()
        {
            null,       // 0: patternType="none"
            "GRAY125"   // 1: patternType="gray125"
        };

        private readonly List<(XLBorderStyleValues Left, XLBorderStyleValues Right, XLBorderStyleValues Top, XLBorderStyleValues Bottom)> _borders = new()
        {
            (XLBorderStyleValues.None, XLBorderStyleValues.None, XLBorderStyleValues.None, XLBorderStyleValues.None) // 0: Default empty border
        };

        private readonly List<(int NumFmtId, int FontId, int FillId, int BorderId, XLAlignmentHorizontalValues HAlign, XLAlignmentVerticalValues VAlign)> _cellXfs = new()
        {
            (0, 0, 0, 0, XLAlignmentHorizontalValues.General, XLAlignmentVerticalValues.Bottom) // 0: Default xf
        };

        public int GetStyleIndex(CellStyleData style)
        {
            if (style.IsDefault) return 0;

            int numFmtId = GetOrAddNumFmt(style.Format);
            int fontId = GetOrAddFont(style.Bold, style.FontSize, style.FontColor.ArgbHex);
            int fillId = GetOrAddFill(style.BackgroundColor.ArgbHex);
            int borderId = GetOrAddBorder(style.LeftBorder, style.RightBorder, style.TopBorder, style.BottomBorder);

            var tuple = (numFmtId, fontId, fillId, borderId, style.Horizontal, style.Vertical);
            int idx = _cellXfs.IndexOf(tuple);
            if (idx >= 0) return idx;

            idx = _cellXfs.Count;
            _cellXfs.Add(tuple);
            return idx;
        }

        private int GetOrAddNumFmt(string format)
        {
            if (string.IsNullOrEmpty(format)) return 0;
            if (format == "0.00") return 2;
            if (format == "#,##0") return 3;
            if (format == "#,##0.00") return 4;

            if (!_numFmtMap.TryGetValue(format, out int id))
            {
                id = _nextNumFmtId++;
                _numFmtMap[format] = id;
            }
            return id;
        }

        private int GetOrAddFont(bool bold, double size, string? color)
        {
            var item = (bold, size, color);
            int idx = _fonts.IndexOf(item);
            if (idx >= 0) return idx;
            idx = _fonts.Count;
            _fonts.Add(item);
            return idx;
        }

        private int GetOrAddFill(string? argb)
        {
            if (string.IsNullOrEmpty(argb)) return 0;
            int idx = _fills.IndexOf(argb);
            if (idx >= 0) return idx;
            idx = _fills.Count;
            _fills.Add(argb);
            return idx;
        }

        private int GetOrAddBorder(XLBorderStyleValues left, XLBorderStyleValues right, XLBorderStyleValues top, XLBorderStyleValues bottom)
        {
            var item = (left, right, top, bottom);
            int idx = _borders.IndexOf(item);
            if (idx >= 0) return idx;
            idx = _borders.Count;
            _borders.Add(item);
            return idx;
        }

        public XDocument BuildStylesXml()
        {
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            var styleSheet = new XElement(ns + "styleSheet");

            // 1. numFmts
            if (_numFmtMap.Count > 0)
            {
                var numFmts = new XElement(ns + "numFmts", new XAttribute("count", _numFmtMap.Count));
                foreach (var kvp in _numFmtMap.OrderBy(k => k.Value))
                {
                    numFmts.Add(new XElement(ns + "numFmt",
                        new XAttribute("numFmtId", kvp.Value),
                        new XAttribute("formatCode", kvp.Key)));
                }
                styleSheet.Add(numFmts);
            }

            // 2. fonts
            var fontsEl = new XElement(ns + "fonts", new XAttribute("count", _fonts.Count));
            foreach (var f in _fonts)
            {
                var fontEl = new XElement(ns + "font");
                if (f.Bold) fontEl.Add(new XElement(ns + "b"));
                fontEl.Add(new XElement(ns + "sz", new XAttribute("val", f.Size.ToString("0.##", CultureInfo.InvariantCulture))));
                if (!string.IsNullOrEmpty(f.Color))
                    fontEl.Add(new XElement(ns + "color", new XAttribute("rgb", f.Color)));
                else
                    fontEl.Add(new XElement(ns + "color", new XAttribute("theme", "1")));
                fontEl.Add(new XElement(ns + "name", new XAttribute("val", "Calibri")));
                fontEl.Add(new XElement(ns + "family", new XAttribute("val", "2")));
                fontsEl.Add(fontEl);
            }
            styleSheet.Add(fontsEl);

            // 3. fills
            var fillsEl = new XElement(ns + "fills", new XAttribute("count", _fills.Count));
            for (int i = 0; i < _fills.Count; i++)
            {
                if (i == 0)
                {
                    fillsEl.Add(new XElement(ns + "fill", new XElement(ns + "patternFill", new XAttribute("patternType", "none"))));
                }
                else if (i == 1)
                {
                    fillsEl.Add(new XElement(ns + "fill", new XElement(ns + "patternFill", new XAttribute("patternType", "gray125"))));
                }
                else
                {
                    fillsEl.Add(new XElement(ns + "fill",
                        new XElement(ns + "patternFill",
                            new XAttribute("patternType", "solid"),
                            new XElement(ns + "fgColor", new XAttribute("rgb", _fills[i]!)),
                            new XElement(ns + "bgColor", new XAttribute("indexed", "64")))));
                }
            }
            styleSheet.Add(fillsEl);

            // 4. borders
            var bordersEl = new XElement(ns + "borders", new XAttribute("count", _borders.Count));
            foreach (var b in _borders)
            {
                bordersEl.Add(new XElement(ns + "border",
                    BuildBorderSide(ns, "left", b.Left),
                    BuildBorderSide(ns, "right", b.Right),
                    BuildBorderSide(ns, "top", b.Top),
                    BuildBorderSide(ns, "bottom", b.Bottom),
                    new XElement(ns + "diagonal")));
            }
            styleSheet.Add(bordersEl);

            // 5. cellStyleXfs
            styleSheet.Add(new XElement(ns + "cellStyleXfs",
                new XAttribute("count", "1"),
                new XElement(ns + "xf",
                    new XAttribute("numFmtId", "0"),
                    new XAttribute("fontId", "0"),
                    new XAttribute("fillId", "0"),
                    new XAttribute("borderId", "0"))));

            // 6. cellXfs
            var cellXfsEl = new XElement(ns + "cellXfs", new XAttribute("count", _cellXfs.Count));
            for (int i = 0; i < _cellXfs.Count; i++)
            {
                var xf = _cellXfs[i];
                var xfEl = new XElement(ns + "xf",
                    new XAttribute("numFmtId", xf.NumFmtId),
                    new XAttribute("fontId", xf.FontId),
                    new XAttribute("fillId", xf.FillId),
                    new XAttribute("borderId", xf.BorderId),
                    new XAttribute("xfId", "0"));

                if (i > 0)
                {
                    if (xf.NumFmtId > 0) xfEl.SetAttributeValue("applyNumberFormat", "1");
                    if (xf.FontId > 0) xfEl.SetAttributeValue("applyFont", "1");
                    if (xf.FillId > 0) xfEl.SetAttributeValue("applyFill", "1");
                    if (xf.BorderId > 0) xfEl.SetAttributeValue("applyBorder", "1");

                    if (xf.HAlign != XLAlignmentHorizontalValues.General || xf.VAlign != XLAlignmentVerticalValues.Bottom)
                    {
                        xfEl.SetAttributeValue("applyAlignment", "1");
                        var alignEl = new XElement(ns + "alignment");
                        if (xf.HAlign != XLAlignmentHorizontalValues.General)
                        {
                            alignEl.SetAttributeValue("horizontal", xf.HAlign switch
                            {
                                XLAlignmentHorizontalValues.Left => "left",
                                XLAlignmentHorizontalValues.Center => "center",
                                XLAlignmentHorizontalValues.Right => "right",
                                _ => "general"
                            });
                        }
                        if (xf.VAlign != XLAlignmentVerticalValues.Bottom)
                        {
                            alignEl.SetAttributeValue("vertical", xf.VAlign switch
                            {
                                XLAlignmentVerticalValues.Center => "center",
                                XLAlignmentVerticalValues.Top => "top",
                                _ => "bottom"
                            });
                        }
                        xfEl.Add(alignEl);
                    }
                }

                cellXfsEl.Add(xfEl);
            }
            styleSheet.Add(cellXfsEl);

            // 7. cellStyles
            styleSheet.Add(new XElement(ns + "cellStyles",
                new XAttribute("count", "1"),
                new XElement(ns + "cellStyle",
                    new XAttribute("name", "Normal"),
                    new XAttribute("xfId", "0"),
                    new XAttribute("builtinId", "0"))));

            return new XDocument(new XDeclaration("1.0", "utf-8", "yes"), styleSheet);
        }

        private static XElement BuildBorderSide(XNamespace ns, string sideName, XLBorderStyleValues style)
        {
            if (style == XLBorderStyleValues.None)
                return new XElement(ns + sideName);

            string styleVal = style switch
            {
                XLBorderStyleValues.Thin => "thin",
                XLBorderStyleValues.Medium => "medium",
                XLBorderStyleValues.Double => "double",
                _ => "thin"
            };

            return new XElement(ns + sideName,
                new XAttribute("style", styleVal),
                new XElement(ns + "color", new XAttribute("auto", "1")));
        }
    }
}
