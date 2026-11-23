using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.OleDb;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities.GST;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class AccessGstReportRepository : IGstReportRepository
{
    private readonly string _connectionString;

    public AccessGstReportRepository(string databasePath)
    {
        _connectionString = AccessConnectionProvider.GetConnectionString(databasePath);
    }

    public async Task<Gstr1Report> GetGstr1ReportAsync(
        DateTime fromDate,
        DateTime toDate,
        string? vouType = null,
        CancellationToken cancellationToken = default)
    {
        var report = new Gstr1Report
        {
            FromDate = fromDate.Date,
            ToDate = toDate.Date
        };

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // 1. Cache SALESVOUTYPE classification (Sales, Purchases, Sales Return, etc.)
        var salesVouTypeMap = await GetSalesVouTypesMapAsync(connection, cancellationToken);

        // 2. Cache ItemCreation master HSN codes
        var itemHsnMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            await using var itemCmd = new OleDbCommand("SELECT ItemName, HSNCODE FROM ItemCreation WHERE ItemName IS NOT NULL AND HSNCODE IS NOT NULL", connection);
            await using var itemReader = await itemCmd.ExecuteReaderAsync(cancellationToken);
            while (await itemReader.ReadAsync(cancellationToken))
            {
                var iname = itemReader["ItemName"]?.ToString()?.Trim();
                var ihsn = itemReader["HSNCODE"]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(iname) && !string.IsNullOrWhiteSpace(ihsn) && !itemHsnMap.ContainsKey(iname))
                {
                    itemHsnMap[iname] = ihsn;
                }
            }
        }
        catch
        {
            // Ignore if table or column does not exist
        }

        // 3. Fetch Sales Bills and Subforms
        var whereClauses = new List<string>
        {
            "b.BillDate >= ?",
            "b.BillDate <= ?"
        };

        if (!string.IsNullOrWhiteSpace(vouType) && !string.Equals(vouType.Trim(), "ALL", StringComparison.OrdinalIgnoreCase))
        {
            whereClauses.Add("b.VouType = ?");
        }

        var whereSql = string.Join(" AND ", whereClauses);

        var sql = $@"
            SELECT b.VouNo, b.BillDate, b.VouType, b.CustomerName, b.VatNo, b.Phone, b.City, b.Address,
                   b.NetAmt, b.GrossAmt, b.DiscountAllowed, b.Tax, b.TaxAmt, b.BillCanceled, b.Status,
                   s.ItemName, s.Qty, s.NtWt, s.GrWt, s.Rate, s.Amount, s.VatAmt, s.[Vat%] AS VatPer
            FROM Bill b
            LEFT JOIN BillSubform s ON b.VouNo = s.VouNo
            WHERE {whereSql}
            ORDER BY b.BillDate ASC, b.VouNo ASC";

        await using var command = new OleDbCommand(sql, connection);
        command.Parameters.Add("@FromDate", OleDbType.Date).Value = fromDate.Date;
        command.Parameters.Add("@ToDate", OleDbType.Date).Value = toDate.Date;
        if (!string.IsNullOrWhiteSpace(vouType) && !string.Equals(vouType.Trim(), "ALL", StringComparison.OrdinalIgnoreCase))
        {
            command.Parameters.Add("@VouType", OleDbType.VarWChar, 50).Value = vouType.Trim();
        }

        var rawRows = new List<RawSalesRow>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var rowVouType = GetString(reader, "VouType");
                var rowVouNo = GetString(reader, "VouNo");
                var rowStatus = GetString(reader, "Status");

                // Skip deleted records
                if (string.Equals(rowStatus, "Deleted", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Ignore all bills where SalesType table Hide = -1 (ticked)
                if (salesVouTypeMap.TryGetValue(rowVouType, out var svtInfo) && svtInfo.Hide)
                {
                    continue;
                }

                // GSTR-1 is strictly OUTWARD SUPPLIES (Sales) and Sales Return Credit Notes.
                // Inward purchases (NSP, Expenses, Stock Purchase, etc.) must NEVER be included in GSTR-1.
                if (IsPurchaseOrExpenseVoucher(rowVouType, rowVouNo, salesVouTypeMap))
                {
                    continue;
                }

                // Ensure it is a valid sales or sales return voucher
                if (!IsSalesOrReturnVoucher(rowVouType, rowVouNo, salesVouTypeMap))
                {
                    continue;
                }

                rawRows.Add(new RawSalesRow
                {
                    VouNo = rowVouNo,
                    BillDate = GetDateTime(reader, "BillDate"),
                    VouType = rowVouType,
                    CustomerName = GetString(reader, "CustomerName"),
                    Gstin = GetGstin(reader),
                    Phone = GetString(reader, "Phone"),
                    City = GetString(reader, "City"),
                    NetAmt = GetDecimal(reader, "NetAmt"),
                    GrossAmt = GetDecimal(reader, "GrossAmt"),
                    TaxAmt = GetDecimal(reader, "TaxAmt"),
                    BillCanceled = GetBool(reader, "BillCanceled"),
                    ItemName = GetString(reader, "ItemName"),
                    Qty = GetDecimal(reader, "Qty"),
                    NtWt = GetDecimal(reader, "NtWt"),
                    GrWt = GetDecimal(reader, "GrWt"),
                    Rate = GetDecimal(reader, "Rate"),
                    Amount = GetDecimal(reader, "Amount"),
                    VatAmt = GetDecimal(reader, "VatAmt"),
                    VatPer = GetDecimal(reader, "VatPer", "Vat%"),
                    GstRate = GetDecimal(reader, "Tax"),
                    IgstAmt = 0m,
                    CgstAmt = 0m,
                    SgstAmt = 0m
                });
            }
        }

        // Group rows by Bill
        var bills = rawRows.GroupBy(r => r.VouNo).ToList();

        var b2bList = new List<Gstr1B2BInvoice>();
        var b2cLargeList = new List<Gstr1B2CLargeInvoice>();
        var b2cSmallMap = new Dictionary<string, Gstr1B2CSmallSummary>();
        var cdnrList = new List<Gstr1CreditDebitNote>();
        var hsnMap = new Dictionary<string, Gstr1HsnSummary>();
        var hsnB2BMap = new Dictionary<string, Gstr1HsnSummary>();
        var hsnB2CMap = new Dictionary<string, Gstr1HsnSummary>();

        void AddToHsn(Dictionary<string, Gstr1HsnSummary> targetMap, string code, string desc, string uqc, decimal rate, decimal qty, decimal taxableVal, decimal igstVal, decimal cgstVal, decimal sgstVal)
        {
            var cleanUqc = !string.IsNullOrWhiteSpace(uqc) ? uqc.Trim().ToUpperInvariant() : "GMS";
            var key = $"{code}_{cleanUqc}_{rate:F2}";
            if (!targetMap.TryGetValue(key, out var hsnEntry))
            {
                hsnEntry = new Gstr1HsnSummary
                {
                    HsnCode = code,
                    Description = desc,
                    Uqc = cleanUqc,
                    GstRate = rate
                };
                targetMap[key] = hsnEntry;
            }
            hsnEntry.TotalQuantity += qty;
            hsnEntry.TaxableValue += taxableVal;
            hsnEntry.IgstAmount += igstVal;
            hsnEntry.CgstAmount += cgstVal;
            hsnEntry.SgstAmount += sgstVal;
            hsnEntry.TotalValue += (taxableVal + igstVal + cgstVal + sgstVal);
        }

        int cancelledCount = 0;
        int totalBillsCount = 0;

        foreach (var bg in bills)
        {
            var header = bg.First();
            totalBillsCount++;

            if (header.BillCanceled)
            {
                cancelledCount++;
                continue;
            }

            decimal billNet = header.NetAmt;

            // Skip zero-value or dummy adjustment vouchers
            if (billNet <= 0 && header.GrossAmt <= 0 && !bg.Any(x => x.Amount > 0))
            {
                continue;
            }

            bool isReturn = IsSalesReturnVoucher(header.VouType, salesVouTypeMap);

            var cleanGstin = CleanGstin(header.Gstin);
            bool isB2B = cleanGstin.Length == 15;
            string stateCode = isB2B ? cleanGstin.Substring(0, 2) : GetStateCodeFromCity(header.City);
            string placeOfSupply = $"{stateCode}-{GetStateName(stateCode)}";

            decimal billTaxable = 0;
            decimal billIgst = 0;
            decimal billCgst = 0;
            decimal billSgst = 0;

            foreach (var line in bg)
            {
                decimal lineAmt = line.Amount > 0 ? line.Amount : line.GrossAmt;

                // Determine the applicable GST rate percentage
                decimal lineRate = 3.00m; // Statutory jewellery GST rate default
                if (line.GstRate > 0)
                {
                    lineRate = line.GstRate;
                }
                else if (line.VatPer > 0)
                {
                    lineRate = line.VatPer;
                }
                else if (line.VatAmt > 0 && line.VatAmt <= 28m)
                {
                    // In legacy Access tables, VatAmt is frequently used to store the tax percentage rate (e.g. 3)
                    lineRate = line.VatAmt;
                }

                decimal taxable;
                decimal igst;
                decimal cgst;
                decimal sgst;

                if (lineAmt > 0)
                {
                    decimal expectedInclusiveTax = Math.Round(lineAmt - (lineAmt / (1m + (lineRate / 100m))), 2);

                    // VatAmt is only a genuine rupee tax amount if:
                    // 1) It exceeds 28 (cannot be a tax rate %) AND is less than lineAmt, OR
                    // 2) It closely matches the expected inclusive tax (e.g. 8.74 on 300)
                    bool isRupeeTax = (line.VatAmt > 28m && line.VatAmt < lineAmt)
                                   || (line.VatAmt > 0 && Math.Abs(line.VatAmt - expectedInclusiveTax) <= 0.50m);

                    decimal lineTax;
                    if (isRupeeTax)
                    {
                        lineTax = line.VatAmt;
                        taxable = lineAmt - lineTax;
                    }
                    else
                    {
                        taxable = Math.Round(lineAmt / (1m + (lineRate / 100m)), 2);
                        lineTax = lineAmt - taxable;
                    }

                    if (stateCode != "21")
                    {
                        igst = lineTax;
                        cgst = 0m;
                        sgst = 0m;
                    }
                    else
                    {
                        igst = 0m;
                        cgst = Math.Round(lineTax / 2m, 2);
                        sgst = lineTax - cgst;
                    }
                }
                else
                {
                    taxable = 0m;
                    igst = 0m;
                    cgst = 0m;
                    sgst = 0m;
                }

                billTaxable += taxable;
                billIgst += igst;
                billCgst += cgst;
                billSgst += sgst;

                // HSN Summary accumulation
                if (taxable > 0 || lineAmt > 0)
                {
                    var (hsn, hsnDesc) = ResolveHsn(line.ItemName, itemHsnMap);
                    bool hasNtWt = line.NtWt > 0;
                    string lineUqc = hasNtWt ? "GMS" : "PCS";
                    decimal qty = hasNtWt ? line.NtWt : (line.Qty > 0 ? line.Qty : 1m);

                    AddToHsn(hsnMap, hsn, hsnDesc, lineUqc, lineRate, qty, taxable, igst, cgst, sgst);
                    if (isB2B)
                    {
                        AddToHsn(hsnB2BMap, hsn, hsnDesc, lineUqc, lineRate, qty, taxable, igst, cgst, sgst);
                    }
                    else
                    {
                        AddToHsn(hsnB2CMap, hsn, hsnDesc, lineUqc, lineRate, qty, taxable, igst, cgst, sgst);
                    }
                }
            }

            if (billTaxable == 0 && billNet > 0)
            {
                decimal billRate = header.GstRate > 0 ? header.GstRate : (header.TaxAmt > 0 && header.TaxAmt <= 28m ? header.TaxAmt : 3.00m);
                billTaxable = Math.Round(billNet / (1m + (billRate / 100m)), 2);
                var tax = billNet - billTaxable;
                if (stateCode != "21") billIgst = tax;
                else { billCgst = Math.Round(tax / 2m, 2); billSgst = tax - billCgst; }

                AddToHsn(hsnMap, "7114", "Silverware & Articles", "PCS", billRate, 1m, billTaxable, billIgst, billCgst, billSgst);
                if (isB2B)
                {
                    AddToHsn(hsnB2BMap, "7114", "Silverware & Articles", "PCS", billRate, 1m, billTaxable, billIgst, billCgst, billSgst);
                }
                else
                {
                    AddToHsn(hsnB2CMap, "7114", "Silverware & Articles", "PCS", billRate, 1m, billTaxable, billIgst, billCgst, billSgst);
                }
            }

            if (isReturn)
            {
                cdnrList.Add(new Gstr1CreditDebitNote
                {
                    Gstin = cleanGstin,
                    ReceiverName = header.CustomerName,
                    NoteType = "C",
                    NoteNumber = header.VouNo,
                    NoteDate = header.BillDate,
                    OriginalInvoiceNo = header.VouNo,
                    OriginalInvoiceDate = header.BillDate,
                    NoteValue = billNet,
                    PlaceOfSupply = placeOfSupply,
                    GstRate = 3.00m,
                    TaxableValue = billTaxable,
                    IgstAmount = billIgst,
                    CgstAmount = billCgst,
                    SgstAmount = billSgst
                });
            }
            else if (isB2B)
            {
                b2bList.Add(new Gstr1B2BInvoice
                {
                    Gstin = cleanGstin,
                    ReceiverName = header.CustomerName,
                    InvoiceNo = header.VouNo,
                    InvoiceDate = header.BillDate,
                    InvoiceValue = billNet,
                    PlaceOfSupply = placeOfSupply,
                    GstRate = 3.00m,
                    TaxableValue = billTaxable,
                    IgstAmount = billIgst,
                    CgstAmount = billCgst,
                    SgstAmount = billSgst
                });
            }
            else if (stateCode != "21" && billNet > 100000m) // B2C Large (Inter-state > 1L)
            {
                b2cLargeList.Add(new Gstr1B2CLargeInvoice
                {
                    InvoiceNo = header.VouNo,
                    InvoiceDate = header.BillDate,
                    InvoiceValue = billNet,
                    PlaceOfSupply = placeOfSupply,
                    GstRate = 3.00m,
                    TaxableValue = billTaxable,
                    IgstAmount = billIgst
                });
            }
            else // B2C Small
            {
                var smallKey = $"{placeOfSupply}_3.00";
                if (!b2cSmallMap.TryGetValue(smallKey, out var b2cSmall))
                {
                    b2cSmall = new Gstr1B2CSmallSummary
                    {
                        PlaceOfSupply = placeOfSupply,
                        GstRate = 3.00m
                    };
                    b2cSmallMap[smallKey] = b2cSmall;
                }
                b2cSmall.TaxableValue += billTaxable;
                b2cSmall.IgstAmount += billIgst;
                b2cSmall.CgstAmount += billCgst;
                b2cSmall.SgstAmount += billSgst;
                b2cSmall.TotalAmount += billNet;
            }
        }

        report.B2BInvoices = b2bList;
        report.B2CLargeInvoices = b2cLargeList;
        report.B2CSmallSummaries = b2cSmallMap.Values.OrderBy(x => x.PlaceOfSupply).ToList();
        report.CreditDebitNotes = cdnrList;
        report.HsnSummaries = hsnMap.Values.OrderBy(x => x.HsnCode).ThenBy(x => x.Uqc).ThenBy(x => x.GstRate).ToList();
        report.HsnB2BSummaries = hsnB2BMap.Values.OrderBy(x => x.HsnCode).ThenBy(x => x.Uqc).ThenBy(x => x.GstRate).ToList();
        report.HsnB2CSummaries = hsnB2CMap.Values.OrderBy(x => x.HsnCode).ThenBy(x => x.Uqc).ThenBy(x => x.GstRate).ToList();

        // Document summary: Group outward sales bills by series prefix (e.g. AR, IN, T1)
        var docList = new List<Gstr1DocumentSummary>();
        var validBillsForDoc = bills.Where(bg =>
        {
            var h = bg.First();
            return h.NetAmt > 0 || h.GrossAmt > 0 || bg.Any(x => x.Amount > 0) || h.BillCanceled;
        }).ToList();

        var seriesGroups = validBillsForDoc.GroupBy(bg =>
        {
            var v = bg.First().VouNo;
            var match = Regex.Match(v, @"^[A-Za-z]+");
            return match.Success ? match.Value.ToUpperInvariant() : "INV";
        }).Where(g => !string.Equals(g.Key, "ADJ", StringComparison.OrdinalIgnoreCase)).OrderBy(g => g.Key).ToList();

        foreach (var sg in seriesGroups)
        {
            var sorted = sg.OrderBy(b => b.First().VouNo).ToList();
            var totalCount = sorted.Count;
            var canCount = sorted.Count(b => b.First().BillCanceled);
            var fromSerial = sorted.First().First().VouNo;
            var toSerial = sorted.Last().First().VouNo;

            docList.Add(new Gstr1DocumentSummary
            {
                NatureOfDoc = 1,
                DocDescription = "Invoices for outward supply",
                FromSerialNo = fromSerial,
                ToSerialNo = toSerial,
                TotalNumber = totalCount,
                CancelledNumber = canCount
            });
        }

        if (docList.Count == 0)
        {
            docList.Add(new Gstr1DocumentSummary
            {
                NatureOfDoc = 1,
                DocDescription = "Invoices for outward supply",
                FromSerialNo = "1",
                ToSerialNo = "1",
                TotalNumber = 0,
                CancelledNumber = 0
            });
        }
        report.DocumentSummaries = docList;

        report.TotalTaxableAmount = b2bList.Sum(x => x.TaxableValue) + b2cLargeList.Sum(x => x.TaxableValue) +
                                    report.B2CSmallSummaries.Sum(x => x.TaxableValue) - cdnrList.Sum(x => x.TaxableValue);
        report.TotalIgstAmount = b2bList.Sum(x => x.IgstAmount) + b2cLargeList.Sum(x => x.IgstAmount) +
                                 report.B2CSmallSummaries.Sum(x => x.IgstAmount) - cdnrList.Sum(x => x.IgstAmount);
        report.TotalCgstAmount = b2bList.Sum(x => x.CgstAmount) + report.B2CSmallSummaries.Sum(x => x.CgstAmount) - cdnrList.Sum(x => x.CgstAmount);
        report.TotalSgstAmount = b2bList.Sum(x => x.SgstAmount) + report.B2CSmallSummaries.Sum(x => x.SgstAmount) - cdnrList.Sum(x => x.SgstAmount);
        report.TotalTaxAmount = report.TotalIgstAmount + report.TotalCgstAmount + report.TotalSgstAmount;
        report.TotalInvoiceValue = b2bList.Sum(x => x.InvoiceValue) + b2cLargeList.Sum(x => x.InvoiceValue) +
                                   report.B2CSmallSummaries.Sum(x => x.TotalAmount) - cdnrList.Sum(x => x.NoteValue);
        report.TotalInvoiceCount = totalBillsCount - cancelledCount;

        return report;
    }

    public async Task<Gstr3BReport> GetGstr3BReportAsync(
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default)
    {
        var gstr1 = await GetGstr1ReportAsync(fromDate, toDate, null, cancellationToken);
        var purchases = await GetGstPurchaseRegisterAsync(fromDate, toDate, cancellationToken);

        var companyDetails = await GetCompanyDetailsAsync(cancellationToken);

        var report = new Gstr3BReport
        {
            Gstin = companyDetails.Gstin,
            LegalName = companyDetails.CompanyName,
            FromDate = fromDate.Date,
            ToDate = toDate.Date,
            Period = $"{fromDate:MMM yyyy}"
        };

        // 3.1 Outward Taxable Supplies
        report.OutwardTaxableSupplies.TotalTaxableValue = gstr1.TotalTaxableAmount;
        report.OutwardTaxableSupplies.IntegratedTax = gstr1.TotalIgstAmount;
        report.OutwardTaxableSupplies.CentralTax = gstr1.TotalCgstAmount;
        report.OutwardTaxableSupplies.StateTax = gstr1.TotalSgstAmount;

        // 3.2 Inter-State supplies made to unregistered persons
        var interStateUnregMap = new Dictionary<string, Gstr3BInterStateSupply>(StringComparer.OrdinalIgnoreCase);
        foreach (var b2cl in gstr1.B2CLargeInvoices)
        {
            var posCode = b2cl.PlaceOfSupply.Length >= 2 && char.IsDigit(b2cl.PlaceOfSupply[0]) && char.IsDigit(b2cl.PlaceOfSupply[1])
                ? b2cl.PlaceOfSupply.Substring(0, 2)
                : b2cl.PlaceOfSupply;

            if (!interStateUnregMap.TryGetValue(posCode, out var row))
            {
                row = new Gstr3BInterStateSupply { PlaceOfSupply = posCode };
                interStateUnregMap[posCode] = row;
            }
            row.TaxableValue += b2cl.TaxableValue;
            row.IntegratedTax += b2cl.IgstAmount;
        }

        foreach (var b2cs in gstr1.B2CSmallSummaries)
        {
            if (b2cs.IgstAmount > 0)
            {
                var posCode = b2cs.PlaceOfSupply.Length >= 2 && char.IsDigit(b2cs.PlaceOfSupply[0]) && char.IsDigit(b2cs.PlaceOfSupply[1])
                    ? b2cs.PlaceOfSupply.Substring(0, 2)
                    : b2cs.PlaceOfSupply;

                if (!interStateUnregMap.TryGetValue(posCode, out var row))
                {
                    row = new Gstr3BInterStateSupply { PlaceOfSupply = posCode };
                    interStateUnregMap[posCode] = row;
                }
                row.TaxableValue += b2cs.TaxableValue;
                row.IntegratedTax += b2cs.IgstAmount;
            }
        }
        report.InterStateSuppliesToUnregistered = interStateUnregMap.Values.OrderBy(x => x.PlaceOfSupply).ToList();

        // 4. Eligible ITC from Purchases and Expenses (Inward Supplies)
        var totalPurIgst = purchases.Sum(p => p.IgstAmount);
        var totalPurCgst = purchases.Sum(p => p.CgstAmount);
        var totalPurSgst = purchases.Sum(p => p.SgstAmount);

        report.ItcAllOther.IntegratedTax = totalPurIgst;
        report.ItcAllOther.CentralTax = totalPurCgst;
        report.ItcAllOther.StateTax = totalPurSgst;

        report.NetItcAvailable.IntegratedTax = totalPurIgst;
        report.NetItcAvailable.CentralTax = totalPurCgst;
        report.NetItcAvailable.StateTax = totalPurSgst;

        // 6. Payment of Tax Summary
        report.TotalTaxLiability.IntegratedTax = gstr1.TotalIgstAmount;
        report.TotalTaxLiability.CentralTax = gstr1.TotalCgstAmount;
        report.TotalTaxLiability.StateTax = gstr1.TotalSgstAmount;

        report.TotalItcUtilized.IntegratedTax = Math.Min(gstr1.TotalIgstAmount, totalPurIgst);
        report.TotalItcUtilized.CentralTax = Math.Min(gstr1.TotalCgstAmount, totalPurCgst);
        report.TotalItcUtilized.StateTax = Math.Min(gstr1.TotalSgstAmount, totalPurSgst);

        report.NetTaxPayableInCash.IntegratedTax = Math.Max(0, gstr1.TotalIgstAmount - totalPurIgst);
        report.NetTaxPayableInCash.CentralTax = Math.Max(0, gstr1.TotalCgstAmount - totalPurCgst);
        report.NetTaxPayableInCash.StateTax = Math.Max(0, gstr1.TotalSgstAmount - totalPurSgst);

        return report;
    }

    public async Task<IReadOnlyList<GstPurchaseRegisterItem>> GetGstPurchaseRegisterAsync(
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default)
    {
        var list = new List<GstPurchaseRegisterItem>();

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // 1. Fetch Inward Purchases from Bill table (NEW SILVER PURCHASE / NSP, EXPENSES, etc.)
        try
        {
            var salesVouTypeMap = await GetSalesVouTypesMapAsync(connection, cancellationToken);
            const string billPurSql = @"
                SELECT [VouNo], [SupBillNo], [SupDate], [BillDate], [VouType], [CustomerName], [AccountName],
                       [VatNo], [City], [Item], [ItemName], [GrWt], [NtWt], [Tax], [ABeforeTax], [TaxAmt],
                       [NetAmt], [Roundedoff], [Status]
                FROM [Bill]
                WHERE [BillDate] >= ? AND [BillDate] <= ?
                ORDER BY [BillDate] ASC, [VouNo] ASC";

            await using var billPurCmd = new OleDbCommand(billPurSql, connection);
            billPurCmd.Parameters.Add("@FromDate", OleDbType.Date).Value = fromDate.Date;
            billPurCmd.Parameters.Add("@ToDate", OleDbType.Date).Value = toDate.Date;

            await using var rBill = await billPurCmd.ExecuteReaderAsync(cancellationToken);
            while (await rBill.ReadAsync(cancellationToken))
            {
                var vt = GetString(rBill, "VouType");
                var vn = GetString(rBill, "VouNo");
                var status = GetString(rBill, "Status");

                if (string.Equals(status, "Deleted", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Ignore all vouchers where SalesType table Hide = -1 (ticked)
                if (salesVouTypeMap.TryGetValue(vt, out var svtInfo) && svtInfo.Hide)
                {
                    continue;
                }

                if (!IsPurchaseOrExpenseVoucher(vt, vn, salesVouTypeMap))
                {
                    continue;
                }

                var gstin = CleanGstin(GetString(rBill, "VatNo", "GSTIN"));
                var city = GetString(rBill, "City");
                var stateCode = gstin.Length == 15 ? gstin.Substring(0, 2) : GetStateCodeFromCity(city);
                var netAmt = GetDecimal(rBill, "NetAmt");
                var taxAmt = GetDecimal(rBill, "TaxAmt");
                var aBeforeTax = GetDecimal(rBill, "ABeforeTax");
                var taxable = aBeforeTax > 0 ? aBeforeTax : (netAmt > taxAmt ? netAmt - taxAmt : netAmt);
                var rate = GetDecimal(rBill, "Tax");
                if (rate <= 0) rate = 3.00m;

                decimal igst = 0m;
                decimal cgst = 0m;
                decimal sgst = 0m;

                if (stateCode != "21")
                {
                    igst = taxAmt;
                }
                else
                {
                    cgst = Math.Round(taxAmt / 2m, 2);
                    sgst = taxAmt - cgst;
                }

                var supBillNo = GetString(rBill, "SupBillNo");
                if (string.IsNullOrWhiteSpace(supBillNo)) supBillNo = vn;

                var supDate = GetDateTime(rBill, "SupDate");
                if (supDate == DateTime.MinValue || supDate == default)
                {
                    supDate = GetDateTime(rBill, "BillDate");
                }

                var partyName = GetString(rBill, "CustomerName", "AccountName");
                var head = GetString(rBill, "Item", "ItemName");
                if (string.IsNullOrWhiteSpace(head)) head = "PURCHASE";

                list.Add(new GstPurchaseRegisterItem
                {
                    VouNo = vn,
                    SuppBillNo = supBillNo,
                    BillDate = supDate,
                    SupplierName = partyName,
                    SupplierGstin = gstin,
                    PlaceOfSupply = $"{stateCode}-{GetStateName(stateCode)}",
                    VoucherType = vt,
                    Head = head,
                    GrossWt = GetDecimal(rBill, "GrWt"),
                    NetWt = GetDecimal(rBill, "NtWt"),
                    TaxableAmount = taxable,
                    GstRate = rate,
                    IgstAmount = igst,
                    CgstAmount = cgst,
                    SgstAmount = sgst,
                    RoundOff = GetDecimal(rBill, "Roundedoff"),
                    NetAmount = netAmt,
                    Status = status
                });
            }
        }
        catch { }

        // 2. Also check Purchases table (for modular/new DB structure)
        try
        {
            const string purSql = @"
                SELECT [VouNo], [SuppBillNo], [BillDate], [AccountName], [VatNo], [City],
                       [GrWt], [NtWt], [TaxableAmount], [IGSTAmt], [CGSTAmt], [SGSTAmt], [TaxAmt], [AddLess], [NetAmt], [Status]
                FROM [Purchases]
                WHERE [BillDate] >= ? AND [BillDate] <= ?
                ORDER BY [BillDate] ASC, [VouNo] ASC";

            await using var purCmd = new OleDbCommand(purSql, connection);
            purCmd.Parameters.Add("@FromDate", OleDbType.Date).Value = fromDate.Date;
            purCmd.Parameters.Add("@ToDate", OleDbType.Date).Value = toDate.Date;

            await using var rPur = await purCmd.ExecuteReaderAsync(cancellationToken);
            while (await rPur.ReadAsync(cancellationToken))
            {
                var gstin = CleanGstin(GetString(rPur, "VatNo", "GSTIN"));
                var stateCode = gstin.Length == 15 ? gstin.Substring(0, 2) : "21";
                var vn = GetString(rPur, "VouNo");

                // Avoid duplicating if already added from Bill table
                if (list.Any(x => string.Equals(x.VouNo, vn, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                list.Add(new GstPurchaseRegisterItem
                {
                    VouNo = vn,
                    SuppBillNo = GetString(rPur, "SuppBillNo"),
                    BillDate = GetDateTime(rPur, "BillDate"),
                    SupplierName = GetString(rPur, "AccountName"),
                    SupplierGstin = gstin,
                    PlaceOfSupply = $"{stateCode}-{GetStateName(stateCode)}",
                    VoucherType = "Stock Purchase",
                    Head = "PURCHASE",
                    GrossWt = GetDecimal(rPur, "GrWt"),
                    NetWt = GetDecimal(rPur, "NtWt"),
                    TaxableAmount = GetDecimal(rPur, "TaxableAmount"),
                    GstRate = 3.00m,
                    IgstAmount = GetDecimal(rPur, "IGSTAmt"),
                    CgstAmount = GetDecimal(rPur, "CGSTAmt"),
                    SgstAmount = GetDecimal(rPur, "SGSTAmt"),
                    RoundOff = GetDecimal(rPur, "AddLess"),
                    NetAmount = GetDecimal(rPur, "NetAmt"),
                    Status = GetString(rPur, "Status")
                });
            }
        }
        catch { }

        // 3. PurchaseE (Purchase Expenses)
        try
        {
            const string pexSql = @"
                SELECT [VouNo], [Nos], [BillDate], [PartyName], [AccountName], [Head], [GSTIN],
                       [TaxableAmount], [IGST], [CGST], [SGST], [NetAmt], [Status]
                FROM [PurchaseE]
                WHERE [BillDate] >= ? AND [BillDate] <= ?
                ORDER BY [BillDate] ASC, [VouNo] ASC";

            await using var pexCmd = new OleDbCommand(pexSql, connection);
            pexCmd.Parameters.Add("@FromDate", OleDbType.Date).Value = fromDate.Date;
            pexCmd.Parameters.Add("@ToDate", OleDbType.Date).Value = toDate.Date;

            await using var rPex = await pexCmd.ExecuteReaderAsync(cancellationToken);
            while (await rPex.ReadAsync(cancellationToken))
            {
                var gstin = CleanGstin(GetString(rPex, "GSTIN"));
                var stateCode = gstin.Length == 15 ? gstin.Substring(0, 2) : "21";
                var vn = GetString(rPex, "VouNo");

                if (list.Any(x => string.Equals(x.VouNo, vn, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                list.Add(new GstPurchaseRegisterItem
                {
                    VouNo = vn,
                    SuppBillNo = vn,
                    BillDate = GetDateTime(rPex, "BillDate", "EntryDate"),
                    SupplierName = GetString(rPex, "PartyName", "AccountName"),
                    SupplierGstin = gstin,
                    PlaceOfSupply = $"{stateCode}-{GetStateName(stateCode)}",
                    VoucherType = "Purchase Expense",
                    Head = GetString(rPex, "Head"),
                    GrossWt = 0,
                    NetWt = 0,
                    TaxableAmount = GetDecimal(rPex, "TaxableAmount"),
                    GstRate = 5.00m,
                    IgstAmount = GetDecimal(rPex, "IGST"),
                    CgstAmount = GetDecimal(rPex, "CGST"),
                    SgstAmount = GetDecimal(rPex, "SGST"),
                    RoundOff = 0,
                    NetAmount = GetDecimal(rPex, "NetAmt"),
                    Status = GetString(rPex, "Status")
                });
            }
        }
        catch { }

        return list.OrderBy(x => x.BillDate).ThenBy(x => x.VouNo).ToList();
    }

    // ── Helper methods ────────────────────────────────────────────────────────

    private static async Task<Dictionary<string, (string ShortName, string HeadCategory, bool Hide)>> GetSalesVouTypesMapAsync(
        OleDbConnection connection,
        CancellationToken cancellationToken)
    {
        var map = new Dictionary<string, (string ShortName, string HeadCategory, bool Hide)>(StringComparer.OrdinalIgnoreCase);
        string[] tableCandidates = { "SALESVOUTYPE", "SalesType", "SalesVouType" };
        foreach (var tbl in tableCandidates)
        {
            try
            {
                var sql = $"SELECT [VouType], [ShortName], [HeadCategory], [Hide] FROM [{tbl}]";
                await using var cmd = new OleDbCommand(sql, connection);
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var vt = GetString(reader, "VouType");
                    var sn = GetString(reader, "ShortName");
                    var hc = GetString(reader, "HeadCategory");
                    var hide = GetBool(reader, "Hide");
                    if (!string.IsNullOrWhiteSpace(vt))
                    {
                        map[vt] = (sn, hc, hide);
                    }
                }
                if (map.Count > 0)
                {
                    break;
                }
            }
            catch
            {
                try
                {
                    var fallbackSql = $"SELECT [VouType], [ShortName], [HeadCategory] FROM [{tbl}]";
                    await using var cmdFallback = new OleDbCommand(fallbackSql, connection);
                    await using var readerFallback = await cmdFallback.ExecuteReaderAsync(cancellationToken);
                    while (await readerFallback.ReadAsync(cancellationToken))
                    {
                        var vt = GetString(readerFallback, "VouType");
                        var sn = GetString(readerFallback, "ShortName");
                        var hc = GetString(readerFallback, "HeadCategory");
                        if (!string.IsNullOrWhiteSpace(vt) && !map.ContainsKey(vt))
                        {
                            map[vt] = (sn, hc, false);
                        }
                    }
                    if (map.Count > 0)
                    {
                        break;
                    }
                }
                catch { }
            }
        }
        return map;
    }

    private static bool IsPurchaseOrExpenseVoucher(
        string? vouType,
        string? vouNo,
        IReadOnlyDictionary<string, (string ShortName, string HeadCategory, bool Hide)> salesVouTypes)
    {
        var vt = (vouType ?? string.Empty).Trim();
        var vn = (vouNo ?? string.Empty).Trim();

        if (salesVouTypes.TryGetValue(vt, out var svt))
        {
            if (svt.Hide)
            {
                return false;
            }

            var head = svt.HeadCategory?.Trim() ?? string.Empty;
            var sn = svt.ShortName?.Trim().ToUpperInvariant() ?? string.Empty;

            if (head.Equals("Purchases", StringComparison.OrdinalIgnoreCase) ||
                head.Equals("Purchase Return", StringComparison.OrdinalIgnoreCase) ||
                head.Equals("Estimate", StringComparison.OrdinalIgnoreCase) ||
                head.Equals("Approval", StringComparison.OrdinalIgnoreCase) ||
                head.Equals("Issue", StringComparison.OrdinalIgnoreCase) ||
                head.Equals("Receive", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (sn is "NSP" or "OSP" or "SSP" or "DMP" or "PRD" or "EX" or "PRS" or "PCZ" or "ISS" or "RES")
            {
                return true;
            }
        }

        // Voucher Type keyword checks
        if (vt.Contains("Purchase", StringComparison.OrdinalIgnoreCase) ||
            vt.Contains("Expense", StringComparison.OrdinalIgnoreCase) ||
            vt.StartsWith("NSP", StringComparison.OrdinalIgnoreCase) ||
            vt.StartsWith("OSP", StringComparison.OrdinalIgnoreCase) ||
            vt.StartsWith("SSP", StringComparison.OrdinalIgnoreCase) ||
            vt.StartsWith("DMP", StringComparison.OrdinalIgnoreCase) ||
            vt.Contains("Karigar", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Voucher Number prefix checks (e.g. NSP/0001/26-27, EX/0001/26-27, OSP/...)
        if (vn.StartsWith("NSP/", StringComparison.OrdinalIgnoreCase) ||
            vn.StartsWith("NSP-", StringComparison.OrdinalIgnoreCase) ||
            vn.StartsWith("EX/", StringComparison.OrdinalIgnoreCase) ||
            vn.StartsWith("EX-", StringComparison.OrdinalIgnoreCase) ||
            vn.StartsWith("OSP/", StringComparison.OrdinalIgnoreCase) ||
            vn.StartsWith("SSP/", StringComparison.OrdinalIgnoreCase) ||
            vn.StartsWith("DMP/", StringComparison.OrdinalIgnoreCase) ||
            vn.StartsWith("PRD/", StringComparison.OrdinalIgnoreCase) ||
            vn.StartsWith("PRS/", StringComparison.OrdinalIgnoreCase) ||
            vn.StartsWith("PCZ/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static bool IsSalesOrReturnVoucher(
        string? vouType,
        string? vouNo,
        IReadOnlyDictionary<string, (string ShortName, string HeadCategory, bool Hide)> salesVouTypes)
    {
        var vt = (vouType ?? string.Empty).Trim();
        if (salesVouTypes.TryGetValue(vt, out var svt) && svt.Hide)
        {
            return false;
        }

        if (IsPurchaseOrExpenseVoucher(vouType, vouNo, salesVouTypes))
        {
            return false;
        }

        if (salesVouTypes.TryGetValue(vt, out var svt2))
        {
            var head = svt2.HeadCategory?.Trim() ?? string.Empty;
            if (head.Equals("Sales", StringComparison.OrdinalIgnoreCase) ||
                head.Equals("Sales Return", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // Fallback pattern matching
        if (vt.Contains("Sale", StringComparison.OrdinalIgnoreCase) ||
            vt.Contains("Invoice", StringComparison.OrdinalIgnoreCase) ||
            vt.Contains("Retail", StringComparison.OrdinalIgnoreCase) ||
            vt.Contains("Tax Inv", StringComparison.OrdinalIgnoreCase) ||
            vt.Contains("Challan", StringComparison.OrdinalIgnoreCase) ||
            vt.Contains("Bill", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static bool IsSalesReturnVoucher(
        string? vouType,
        IReadOnlyDictionary<string, (string ShortName, string HeadCategory, bool Hide)> salesVouTypes)
    {
        var vt = (vouType ?? string.Empty).Trim();
        if (salesVouTypes.TryGetValue(vt, out var svt))
        {
            if (svt.Hide) return false;
            if (svt.HeadCategory.Equals("Sales Return", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return vt.Contains("Return", StringComparison.OrdinalIgnoreCase) ||
               vt.Contains("Credit Note", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class RawSalesRow
    {
        public string VouNo { get; set; } = string.Empty;
        public DateTime BillDate { get; set; }
        public string VouType { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string Gstin { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public decimal NetAmt { get; set; }
        public decimal GrossAmt { get; set; }
        public decimal TaxAmt { get; set; }
        public bool BillCanceled { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public decimal Qty { get; set; }
        public decimal NtWt { get; set; }
        public decimal GrWt { get; set; }
        public decimal Rate { get; set; }
        public decimal Amount { get; set; }
        public decimal VatAmt { get; set; }
        public decimal VatPer { get; set; }
        public decimal GstRate { get; set; }
        public decimal IgstAmt { get; set; }
        public decimal CgstAmt { get; set; }
        public decimal SgstAmt { get; set; }
    }

    private static (string Hsn, string Description) ResolveHsn(string? itemName, IReadOnlyDictionary<string, string>? itemHsnMap = null)
    {
        if (string.IsNullOrWhiteSpace(itemName)) return ("7114", "Silverware & Articles");
        var name = itemName.Trim().ToUpperInvariant();

        if (itemHsnMap != null && itemHsnMap.TryGetValue(name, out var masterHsn) && !string.IsNullOrWhiteSpace(masterHsn))
        {
            var hsnCode = masterHsn.Trim();
            var desc = GetHsnDescription(hsnCode, itemName.Trim());
            return (hsnCode, desc);
        }

        if (name.Contains("SILVER") || name.Contains("PAYAL") || name.Contains("CHAIN S") || name.Contains("TOE") || name.Contains("PUJA") || name.Contains("DABBI")) return ("7114", "Silverware & Articles");
        if (name.Contains("DIAMOND") || name.Contains("SOLITAIRE")) return ("7102", "Worked Diamonds");
        if (name.Contains("MAKING") || name.Contains("LABOUR") || name.Contains("JOB")) return ("9988", "Jobwork & Manufacturing Services");
        if (name.Contains("IMITATION") || name.Contains("BRONZE")) return ("7117", "Imitation Jewellery");
        return ("7114", "Silverware & Articles");
    }

    private static string GetHsnDescription(string hsn, string itemName)
    {
        return hsn switch
        {
            "7114" => "Silverware & Articles",
            "7113" => "Gold & Precious Jewellery",
            "7102" => "Worked Diamonds",
            "7106" => "Silver Unwrought or Semi-manufactured",
            "7108" => "Gold Unwrought or Semi-manufactured",
            "7117" => "Imitation Jewellery",
            "9988" => "Jobwork & Manufacturing Services",
            _ => $"{itemName} (HSN {hsn})"
        };
    }

    private static string CleanGstin(string? gstin)
    {
        if (string.IsNullOrWhiteSpace(gstin)) return string.Empty;
        return Regex.Replace(gstin.Trim().ToUpperInvariant(), @"[^A-Z0-9]", "");
    }

    private static string GetGstin(DbDataReader r)
    {
        var val = GetString(r, "VatNo");
        if (!string.IsNullOrWhiteSpace(val)) return val;
        return GetString(r, "Gstin", "GSTIN");
    }

    private static string GetStateCodeFromCity(string? city)
    {
        if (string.IsNullOrWhiteSpace(city)) return "21";
        var c = city.Trim().ToUpperInvariant();
        if (c.Contains("KOLKATA") || c.Contains("HOWRAH") || c.Contains("BENGAL")) return "19";
        if (c.Contains("MUMBAI") || c.Contains("MAHARASHTRA") || c.Contains("PUNE")) return "27";
        if (c.Contains("DELHI")) return "07";
        if (c.Contains("HYDERABAD") || c.Contains("TELANGANA")) return "36";
        if (c.Contains("BANGALORE") || c.Contains("KARNATAKA")) return "29";
        if (c.Contains("CHENNAI") || c.Contains("TAMIL")) return "33";
        if (c.Contains("RANCHI") || c.Contains("JHARKHAND")) return "20";
        if (c.Contains("RAIPUR") || c.Contains("CHHATTISGARH")) return "22";
        if (c.Contains("JAIPUR") || c.Contains("RAJASTHAN")) return "08";
        if (c.Contains("AHMEDABAD") || c.Contains("SURAT") || c.Contains("GUJARAT")) return "24";
        if (c.Contains("AGRA") || c.Contains("KANPUR") || c.Contains("UP")) return "09";
        if (c.Contains("TENALI") || c.Contains("VIJAYAWADA") || c.Contains("ANDHRA")) return "37";
        return "21"; // Odisha default
    }

    private static string GetStateName(string stateCode) => stateCode switch
    {
        "21" => "Odisha",
        "19" => "West Bengal",
        "27" => "Maharashtra",
        "07" => "Delhi",
        "36" => "Telangana",
        "29" => "Karnataka",
        "33" => "Tamil Nadu",
        "20" => "Jharkhand",
        "22" => "Chhattisgarh",
        "09" => "Uttar Pradesh",
        "08" => "Rajasthan",
        "24" => "Gujarat",
        "23" => "Madhya Pradesh",
        "10" => "Bihar",
        "37" => "Andhra Pradesh",
        "32" => "Kerala",
        _ => "Other"
    };

    private static string GetString(DbDataReader r, params string[] cols)
    {
        foreach (var col in cols)
        {
            try
            {
                var idx = FindOrdinal(r, col);
                if (idx >= 0 && !r.IsDBNull(idx))
                {
                    return Convert.ToString(r.GetValue(idx))?.Trim() ?? string.Empty;
                }
            }
            catch { }
        }
        return string.Empty;
    }

    private static decimal GetDecimal(DbDataReader r, params string[] cols)
    {
        foreach (var col in cols)
        {
            try
            {
                var idx = FindOrdinal(r, col);
                if (idx >= 0 && !r.IsDBNull(idx))
                {
                    return Convert.ToDecimal(r.GetValue(idx));
                }
            }
            catch { }
        }
        return 0m;
    }

    private static DateTime GetDateTime(DbDataReader r, params string[] cols)
    {
        foreach (var col in cols)
        {
            try
            {
                var idx = FindOrdinal(r, col);
                if (idx >= 0 && !r.IsDBNull(idx))
                {
                    return Convert.ToDateTime(r.GetValue(idx));
                }
            }
            catch { }
        }
        return DateTime.Today;
    }

    private static bool GetBool(DbDataReader r, params string[] cols)
    {
        foreach (var col in cols)
        {
            try
            {
                var idx = FindOrdinal(r, col);
                if (idx >= 0 && !r.IsDBNull(idx))
                {
                    var val = r.GetValue(idx);
                    if (val is bool b) return b;
                    if (val is int i) return i != 0;
                    if (val is short s) return s != 0;
                    if (val is long l) return l != 0;
                    if (val is sbyte sb) return sb != 0;
                    if (val is string str)
                    {
                        if (bool.TryParse(str, out var parsedBool)) return parsedBool;
                        if (int.TryParse(str, out var parsedInt)) return parsedInt != 0;
                    }
                    return Convert.ToBoolean(val);
                }
            }
            catch { }
        }
        return false;
    }

    private static int FindOrdinal(DbDataReader r, string columnName)
    {
        for (var i = 0; i < r.FieldCount; i++)
        {
            if (string.Equals(r.GetName(i), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }
        return -1;
    }

    public async Task<(string Gstin, string CompanyName, string StateCode)> GetCompanyDetailsAsync(CancellationToken cancellationToken = default)
    {
        string gstin = string.Empty;
        string company = "TATTVA JEWELS";
        string stateCode = "21";

        try
        {
            await using var connection = new OleDbConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = "SELECT TOP 1 [Company], [TIN], [City], [POS] FROM [DateRange]";
            await using var command = new OleDbCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            if (await reader.ReadAsync(cancellationToken))
            {
                var comp = reader["Company"]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(comp)) company = comp;

                var tin = reader["TIN"]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(tin))
                {
                    var match = Regex.Match(tin, @"[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1}", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        gstin = match.Value.ToUpperInvariant();
                        stateCode = gstin.Substring(0, 2);
                    }
                    else
                    {
                        var clean = CleanGstin(tin);
                        if (clean.Length == 15)
                        {
                            gstin = clean;
                            stateCode = gstin.Substring(0, 2);
                        }
                    }
                }
            }
        }
        catch
        {
            // Ignore if DateRange table doesn't exist
        }

        return (gstin, company, stateCode);
    }
}
