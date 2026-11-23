using System.Globalization;
using System.Text;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.Infrastructure.Printing;
using ConceptJewelleryPro.Infrastructure.Settings;
using ConceptJewelleryPro.WPF.ViewModels;

namespace ConceptJewelleryPro.WPF.Services;

public static class FastReportDocumentService
{
    private static readonly CultureInfo IndianCulture = new("en-IN");

    public static FastReportPrintService.PreparedFastReportDocument PrepareVoucherReport(VoucherEntryViewModel vm)
    {
        var company = FastReportPrintService.GetCompanyHeaderInfo();
        bool isPayment = string.Equals(vm.VouType, VoucherType.Payments, StringComparison.OrdinalIgnoreCase);
        var templateName = isPayment ? "PaymentPrint" : "ReceiptPrint";

        var refParts = new List<string>();
        if (!vm.IsJournal)
        {
            if (!string.IsNullOrWhiteSpace(vm.ChequeNo)) refParts.Add($"Cheque/Ref: {vm.ChequeNo.Trim()}");
            if (vm.ChqDate.HasValue) refParts.Add($"Date: {vm.ChqDate.Value:dd/MM/yyyy}");
            if (!string.IsNullOrWhiteSpace(vm.BankName)) refParts.Add($"Bank: {vm.BankName.Trim()}");
            if (!string.IsNullOrWhiteSpace(vm.CardType)) refParts.Add($"Card: {vm.CardType.Trim()}");
            if (vm.CardComm > 0) refParts.Add($"Comm: {vm.CardComm}% (₹ {vm.CommAmt:N2}) Net: ₹ {vm.NetCredited:N2}");
        }

        var model = new FastReportPrintService.VoucherFrxPrintModel
        {
            CompanyName = company.Name,
            CompanyAddress = company.Address,
            CompanyPhone = company.Phone,
            CompanyGstin = company.Gstin,
            VoucherTitle = $"{vm.Title.ToUpperInvariant()} VOUCHER",
            VouNo = vm.VouNo?.Trim() ?? string.Empty,
            VouDate = vm.VouDate.ToString("dd/MM/yyyy"),
            PaymentMode = vm.IsJournal ? "Journal" : (vm.PaymentMode ?? "Cash"),
            DrAccountLabel = vm.DrAccountLabel,
            DrAccount = vm.DrAccount?.Trim() ?? string.Empty,
            CrAccountLabel = vm.CrAccountLabel,
            CrAccount = vm.CrAccount?.Trim() ?? string.Empty,
            PartyLabel = isPayment ? "Paid To" : "Received From",
            PartyName = (isPayment ? vm.DrAccount : vm.CrAccount)?.Trim() ?? string.Empty,
            CashBankLabel = isPayment ? "Paid From" : "Received In",
            CashBankAccount = (isPayment ? vm.CrAccount : vm.DrAccount)?.Trim() ?? string.Empty,
            Amount = vm.Amount,
            AmountFormatted = FastReportPrintService.FormatInr(vm.Amount),
            AmountInWords = FastReportPrintService.ConvertAmountToWords(vm.Amount),
            ChequeNo = vm.ChequeNo?.Trim() ?? string.Empty,
            ChqDate = vm.ChqDate?.ToString("dd/MM/yyyy") ?? string.Empty,
            BankName = vm.BankName?.Trim() ?? string.Empty,
            CardType = vm.CardType?.Trim() ?? string.Empty,
            CardCommText = vm.CardComm > 0 ? $"{vm.CardComm}% (₹ {vm.CommAmt:N2})" : string.Empty,
            ReferenceSummary = refParts.Count > 0 ? string.Join("  |  ", refParts) : "Cash / Direct",
            Narration = vm.Narration?.Trim() ?? string.Empty,
            PreparedBy = vm.CurrentUsername
        };

        return FastReportPrintService.PrepareReport(
            templateName,
            $"{vm.Title} Voucher Print Preview - {vm.VouNo}",
            $"{(isPayment ? "Payment" : "Receipt")}-{vm.VouNo}",
            report => report.RegisterData(new[] { model }, "Voucher"));
    }

    public static FastReportPrintService.PreparedFastReportDocument PrepareSchemeReceiptReport(SchemeReceiptViewModel vm)
    {
        var company = FastReportPrintService.GetCompanyHeaderInfo();

        var refParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(vm.ChequeNo)) refParts.Add($"Cheque/Ref: {vm.ChequeNo.Trim()}");
        if (vm.ChqDate.HasValue) refParts.Add($"Date: {vm.ChqDate.Value:dd/MM/yyyy}");
        if (!string.IsNullOrWhiteSpace(vm.BankName)) refParts.Add($"Bank: {vm.BankName.Trim()}");
        if (!string.IsNullOrWhiteSpace(vm.CardType)) refParts.Add($"Card: {vm.CardType.Trim()}");

        string schemeTotalValue = string.Empty;
        string schemeDuration = string.Empty;
        string schemeMaturity = string.Empty;
        string schemeSummary = string.Empty;
        if (vm.SelectedSchemeItem is not null)
        {
            schemeTotalValue = FastReportPrintService.FormatInr(vm.SelectedSchemeItem.TotalValue);
            schemeDuration = $"{vm.SelectedSchemeItem.Month:0} Months";
            schemeMaturity = vm.SelectedSchemeItem.EDate.ToString("dd/MM/yyyy");
            schemeSummary = $"Total Value: {schemeTotalValue}  |  Duration: {schemeDuration}  |  Maturity: {schemeMaturity}";
        }

        var model = new FastReportPrintService.SchemeReceiptFrxPrintModel
        {
            CompanyName = company.Name,
            CompanyAddress = company.Address,
            CompanyPhone = company.Phone,
            CompanyGstin = company.Gstin,
            VouNo = vm.VouNo?.Trim() ?? string.Empty,
            VouDate = vm.VouDate.ToString("dd/MM/yyyy"),
            Scode = vm.Scode?.Trim() ?? string.Empty,
            PaymentMode = vm.PaymentMode ?? "Cash",
            CustomerName = vm.CrAccount?.Trim() ?? string.Empty,
            ReceivedByAccount = vm.DrAccount?.Trim() ?? string.Empty,
            InstallmentNo = vm.InstallmentNo,
            PaidInstallmentsCount = vm.PaidInstallmentsCount,
            InstallmentSummary = $"Installment #{vm.InstallmentNo} (Paid {vm.PaidInstallmentsCount} Installments)",
            Amount = vm.Amount,
            AmountFormatted = FastReportPrintService.FormatInr(vm.Amount),
            AmountInWords = FastReportPrintService.ConvertAmountToWords(vm.Amount),
            ChequeNo = vm.ChequeNo?.Trim() ?? string.Empty,
            ChqDate = vm.ChqDate?.ToString("dd/MM/yyyy") ?? string.Empty,
            BankName = vm.BankName?.Trim() ?? string.Empty,
            CardType = vm.CardType?.Trim() ?? string.Empty,
            ReferenceSummary = refParts.Count > 0 ? string.Join("  |  ", refParts) : "Cash / Direct",
            SchemeTotalValue = schemeTotalValue,
            SchemeDurationMonths = schemeDuration,
            SchemeMaturityDate = schemeMaturity,
            SchemeDetailsSummary = schemeSummary,
            Narration = vm.Narration?.Trim() ?? string.Empty,
            PreparedBy = vm.CurrentUsername
        };

        return FastReportPrintService.PrepareReport(
            "SchemeReceiptPrint",
            $"Scheme Receipt Print Preview - {vm.VouNo}",
            $"SchemeReceipt-{vm.VouNo}",
            report => report.RegisterData(new[] { model }, "SchemeReceipt"));
    }

    public static bool HasBillFrxTemplate(InvoiceViewModel invoice, string? customTemplatePath = null)
    {
        var templateName = !string.IsNullOrWhiteSpace(customTemplatePath)
            ? customTemplatePath
            : invoice.GetPrintFileForVouType();

        return FastReportPrintService.HasFrxTemplate(templateName)
            || FastReportPrintService.HasFrxTemplate("BillPrint");
    }

    public static FastReportPrintService.PreparedFastReportDocument PrepareBillReport(InvoiceViewModel invoice, string? customTemplatePath = null)
    {
        var templateName = (!string.IsNullOrWhiteSpace(customTemplatePath)
            ? customTemplatePath
            : invoice.GetPrintFileForVouType()) ?? "BillPrint";

        if (!FastReportPrintService.HasFrxTemplate(templateName))
        {
            templateName = "BillPrint";
        }

        var company = FastReportPrintService.GetCompanyHeaderInfo();
        var storeName = !string.IsNullOrWhiteSpace(invoice.StoreName) ? invoice.StoreName.Trim() : company.Name;
        var storeGstin = !string.IsNullOrWhiteSpace(invoice.StoreGstin) ? invoice.StoreGstin.Trim() : company.Gstin;

        bool hideWtInPc = false;
        try { hideWtInPc = new LocalPrinterSettingsStore().Load().HideWtInPcItem; } catch { }

        var itemModels = new List<FastReportPrintService.BillItemFrxPrintModel>();
        int sno = 1;
        foreach (var item in invoice.Items)
        {
            var taxableVal = item.Amount > 0 ? item.Amount : item.GrossAmt;
            var purity = !string.IsNullOrWhiteSpace(item.Purity) ? item.Purity : "22K";
            var subtext = !string.IsNullOrWhiteSpace(item.OldBarcode) ? item.OldBarcode : item.ENote;

            bool shouldHideWeight = hideWtInPc && item.PcRate;
            string grWtStr = (shouldHideWeight || item.GrWt <= 0 || (item.GrWt == item.NtWt && item.NtWt > 0))
                ? "-"
                : item.GrWt.ToString("F3", CultureInfo.InvariantCulture);
            string ntWtStr = (shouldHideWeight || item.NtWt <= 0)
                ? "-"
                : item.NtWt.ToString("F3", CultureInfo.InvariantCulture);

            var itemHsn = !string.IsNullOrWhiteSpace(item.HsnCode)
                ? item.HsnCode
                : invoice.GetHsnForItem(item.ItemName);
            if (string.IsNullOrWhiteSpace(itemHsn)) itemHsn = "7113";

            itemModels.Add(new FastReportPrintService.BillItemFrxPrintModel
            {
                SNo = sno.ToString(),
                Barcode = item.Barcode?.Trim() ?? string.Empty,
                ItemName = item.ItemName?.Trim().ToUpperInvariant() ?? string.Empty,
                Subtext = string.IsNullOrWhiteSpace(subtext) ? string.Empty : $"({subtext.Trim()})",
                HsnCode = itemHsn,
                Purity = purity,
                Qty = item.Qty.ToString(),
                GrWt = grWtStr,
                NtWt = ntWtStr,
                Rate = item.Rate > 0 ? item.Rate.ToString("N2", IndianCulture) : "-",
                MetalValue = item.MetalValue > 0 ? item.MetalValue.ToString("N2", IndianCulture) : "-",
                Making = item.Making > 0 ? item.Making.ToString("N2", IndianCulture) : "-",
                DisAmt = item.DisAmt.ToString("N2", IndianCulture),
                TaxableValue = taxableVal > 0 ? taxableVal.ToString("N2", IndianCulture) : "-"
            });
            sno++;
        }

        var totalTaxable = invoice.Items.Sum(i => i.Amount > 0 ? i.Amount : i.GrossAmt);
        var totalDiscount = invoice.Discount > 0 ? invoice.Discount : invoice.Items.Sum(i => i.DisAmt);
        var sumVat = invoice.Items.Sum(i => i.VatAmt);
        decimal cgstAmt, sgstAmt;
        if (sumVat > 0)
        {
            cgstAmt = Math.Round(sumVat / 2m, 2);
            sgstAmt = Math.Round(sumVat - cgstAmt, 2);
        }
        else
        {
            cgstAmt = invoice.CgstAmt;
            sgstAmt = invoice.SgstAmt;
        }
        var totalGst = cgstAmt + sgstAmt;

        var taxableBase = invoice.Items.Sum(i => i.AbeforeTax > 0 ? i.AbeforeTax : 0m);
        if (taxableBase <= 0)
        {
            taxableBase = (totalGst > 0 && invoice.NetAmt > totalGst)
                ? invoice.NetAmt - totalGst
                : totalTaxable;
        }

        var activePayments = invoice.Payments.Where(p => p.Amount > 0).ToList();
        if (activePayments.Count == 0) activePayments = invoice.Payments.ToList();

        var paySb = new StringBuilder();
        var payAccSb = new StringBuilder();
        var payAmtSb = new StringBuilder();
        foreach (var p in activePayments)
        {
            string details = p.DetailsSummary;
            if (string.IsNullOrWhiteSpace(details))
            {
                if (p.GrWt > 0 || p.Wt > 0)
                    details = $"Old Gold Ornaments Gold Weight : {p.GrWt:F2} @ {p.Rate:F2}";
                else if (!string.IsNullOrWhiteSpace(p.Narration))
                    details = p.Narration;
            }

            if (paySb.Length > 0) paySb.Append("  |  ");
            paySb.Append(p.AccountName?.Trim().ToUpperInvariant());
            if (!string.IsNullOrWhiteSpace(details))
                paySb.Append($" ({details.Trim()})");
            paySb.Append($": ₹ {p.Amount.ToString("N2", IndianCulture)}");

            if (payAccSb.Length > 0)
            {
                payAccSb.AppendLine();
                payAmtSb.AppendLine();
            }
            payAccSb.Append(p.AccountName?.Trim().ToUpperInvariant());
            payAmtSb.Append(p.Amount.ToString("N2", IndianCulture));
            if (!string.IsNullOrWhiteSpace(details))
            {
                payAccSb.AppendLine();
                payAccSb.Append(details.Trim());
                payAmtSb.AppendLine();
            }
        }

        var netAmount = invoice.NetAmt > 0 ? invoice.NetAmt : totalTaxable;
        var custAddrParts = new[] { invoice.Address, invoice.Address2 }
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim().ToUpperInvariant());

        var headerModel = new FastReportPrintService.BillHeaderFrxPrintModel
        {
            CompanyName = storeName,
            CompanyAddress = company.Address,
            CompanyPhone = company.Phone,
            CompanyGstin = storeGstin,
            StateCode = company.StateCode,
            VouNo = invoice.VouNo?.Trim() ?? string.Empty,
            BillDate = invoice.BillDate.ToString("dd/MM/yy"),
            BillDateLong = invoice.BillDate.ToString("dd/MM/yyyy"),
            VouType = invoice.VouType?.Trim() ?? "TAX INVOICE",
            CustomerName = invoice.CustomerName?.Trim().ToUpperInvariant() ?? string.Empty,
            CustomerAddress = string.Join(", ", custAddrParts),
            CustomerPhone = invoice.Phone?.Trim() ?? string.Empty,
            CustomerCity = invoice.City?.Trim().ToUpperInvariant() ?? string.Empty,
            TotalQty = invoice.TotalQty > 0 ? invoice.TotalQty.ToString() : "0",
            TotalGrWt = invoice.TotalGrWt > 0 ? invoice.TotalGrWt.ToString("F3", CultureInfo.InvariantCulture) : "0.000",
            TotalNtWt = invoice.TotalNtWt > 0 ? invoice.TotalNtWt.ToString("F3", CultureInfo.InvariantCulture) : "0.000",
            TotalMetalValue = invoice.TotalMetalValue > 0 ? invoice.TotalMetalValue.ToString("N2", IndianCulture) : "0.00",
            TotalMaking = invoice.TotalMaking > 0 ? invoice.TotalMaking.ToString("N2", IndianCulture) : "0.00",
            GrossAmt = invoice.GrossAmt > 0 ? invoice.GrossAmt.ToString("N2", IndianCulture) : "0.00",
            TotalDiscount = totalDiscount.ToString("N2", IndianCulture),
            TaxableBaseAmt = taxableBase.ToString("N2", IndianCulture),
            CgstRate = "1.5%",
            SgstRate = "1.5%",
            CgstAmt = cgstAmt.ToString("N2", IndianCulture),
            SgstAmt = sgstAmt.ToString("N2", IndianCulture),
            TotalGstAmt = totalGst.ToString("N2", IndianCulture),
            NetAmt = netAmount.ToString("N2", IndianCulture),
            TotalAmount = (invoice.NetAmt > 0 ? invoice.NetAmt : totalTaxable).ToString("N2", IndianCulture),
            AmountInWords = FastReportPrintService.ConvertAmountToWords(netAmount),
            PaymentSummary = paySb.Length > 0 ? paySb.ToString() : "Cash / Settlement",
            PaymentAccountsText = payAccSb.ToString(),
            PaymentAmountsText = payAmtSb.ToString(),
            HsnSummary = invoice.GetInvoiceHsnSummary()
        };

        return FastReportPrintService.PrepareReport(
            templateName,
            $"Tax Invoice Print Preview - #{invoice.VouNo}",
            $"TaxInvoice-{invoice.VouNo}",
            report =>
            {
                report.RegisterData(new[] { headerModel }, "BillHeader");
                report.RegisterData(itemModels, "Items");
            });
    }
}
