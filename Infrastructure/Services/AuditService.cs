using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Services;

public static class AuditService
{
    private const string DeletedMarker = "#Deleted#";

    public static async Task LogAccountModificationAsync(
        IAuditRepository auditRepo,
        Account? original,
        Account current,
        string username,
        CancellationToken ct = default)
    {
        if (original is null) return;

        var entries = new List<AuditLogEntry>();
        var now = DateTime.Now;
        var vouNo = !string.IsNullOrWhiteSpace(current.AccountName) ? current.AccountName : current.PartyCode;
        const string entryType = "Ledger Creation";

        void Compare(string fieldName, string? oldVal, string? newVal)
        {
            var o = oldVal?.Trim() ?? string.Empty;
            var n = newVal?.Trim() ?? string.Empty;
            if (!string.Equals(o, n, StringComparison.Ordinal))
            {
                entries.Add(new AuditLogEntry
                {
                    User = username,
                    EntryType = entryType,
                    VouNo = vouNo,
                    FieldName = fieldName,
                    OldValue = o,
                    NewValue = n,
                    Date = now.Date,
                    Time = now,
                    Checked = false
                });
            }
        }

        void CompareDecimal(string fieldName, decimal? oldVal, decimal? newVal, int decimals = 2)
        {
            var o = oldVal.HasValue ? oldVal.Value.ToString($"F{decimals}") : string.Empty;
            var n = newVal.HasValue ? newVal.Value.ToString($"F{decimals}") : string.Empty;
            if (!string.Equals(o, n, StringComparison.Ordinal))
            {
                entries.Add(new AuditLogEntry
                {
                    User = username,
                    EntryType = entryType,
                    VouNo = vouNo,
                    FieldName = fieldName,
                    OldValue = o,
                    NewValue = n,
                    Date = now.Date,
                    Time = now,
                    Checked = false
                });
            }
        }

        Compare("Account Name", original.AccountName, current.AccountName);
        Compare("HeadCategory", original.HeadCategory, current.HeadCategory);
        Compare("SubCategory", original.SubCategory, current.SubCategory);
        Compare("Address", original.Address, current.Address);
        Compare("Address2", original.Address2, current.Address2);
        Compare("City", original.City, current.City);
        Compare("PinCode", original.PinCode, current.PinCode);
        Compare("Phone No", original.PhoneNo, current.PhoneNo);
        Compare("Mobile", original.Mobile, current.Mobile);
        Compare("pan No", original.PanNo, current.PanNo);
        Compare("Vat Type", original.VatType, current.VatType);
        Compare("Vat No", original.VatNo, current.VatNo);
        Compare("Email", original.Email, current.Email);
        Compare("Bank Ac", original.BankAc, current.BankAc);
        CompareDecimal("Opening Amount", original.OpAmt1, current.OpAmt1);
        CompareDecimal("Opening Gold Weight", original.OpGoldWeight, current.OpGoldWeight, 3);
        CompareDecimal("Credit Limit", original.CreditLimit, current.CreditLimit);
        CompareDecimal("Credit Days", original.CreditDays, current.CreditDays, 0);

        if (entries.Count > 0)
        {
            await auditRepo.LogAuditBatchAsync(entries, ct);
        }
    }

    public static async Task LogAccountDeletionAsync(
        IAuditRepository auditRepo,
        Account account,
        string username,
        CancellationToken ct = default)
    {
        var entries = new List<AuditLogEntry>();
        var now = DateTime.Now;
        var vouNo = !string.IsNullOrWhiteSpace(account.AccountName) ? account.AccountName : account.PartyCode;
        const string entryType = "Ledger Creation";

        void AddDel(string fieldName, string? val)
        {
            if (!string.IsNullOrWhiteSpace(val))
            {
                entries.Add(new AuditLogEntry
                {
                    User = username,
                    EntryType = entryType,
                    VouNo = vouNo,
                    FieldName = fieldName,
                    OldValue = val.Trim(),
                    NewValue = DeletedMarker,
                    Date = now.Date,
                    Time = now,
                    Checked = false
                });
            }
        }

        AddDel("Account Name", account.AccountName);
        AddDel("HeadCategory", account.HeadCategory);
        AddDel("City", account.City);
        AddDel("Phone No", account.PhoneNo);
        AddDel("pan No", account.PanNo);
        AddDel("Opening Amount", account.OpAmt1.ToString("F2"));

        if (entries.Count > 0)
        {
            await auditRepo.LogAuditBatchAsync(entries, ct);
        }
    }

    public static async Task LogCustomerModificationAsync(
        IAuditRepository auditRepo,
        Customer? original,
        Customer current,
        string username,
        CancellationToken ct = default)
    {
        if (original is null) return;

        var entries = new List<AuditLogEntry>();
        var now = DateTime.Now;
        var vouNo = !string.IsNullOrWhiteSpace(current.CompanyName) ? current.CompanyName : current.Code;
        const string entryType = "Customer Creation";

        void Compare(string fieldName, string? oldVal, string? newVal)
        {
            var o = oldVal?.Trim() ?? string.Empty;
            var n = newVal?.Trim() ?? string.Empty;
            if (!string.Equals(o, n, StringComparison.Ordinal))
            {
                entries.Add(new AuditLogEntry
                {
                    User = username,
                    EntryType = entryType,
                    VouNo = vouNo,
                    FieldName = fieldName,
                    OldValue = o,
                    NewValue = n,
                    Date = now.Date,
                    Time = now,
                    Checked = false
                });
            }
        }

        Compare("Customer Name", original.CompanyName, current.CompanyName);
        Compare("Address", original.Address, current.Address);
        Compare("Address2", original.Address2, current.Address2);
        Compare("City", original.City, current.City);
        Compare("Pin Code", original.Pincode, current.Pincode);
        Compare("Phone", original.Phone, current.Phone);
        Compare("Mobile", original.Mobile, current.Mobile);
        Compare("Email", original.Email, current.Email);
        Compare("Group", original.Group, current.Group);

        if (entries.Count > 0)
        {
            await auditRepo.LogAuditBatchAsync(entries, ct);
        }
    }

    public static async Task LogCustomerDeletionAsync(
        IAuditRepository auditRepo,
        Customer customer,
        string username,
        CancellationToken ct = default)
    {
        var entries = new List<AuditLogEntry>();
        var now = DateTime.Now;
        var vouNo = !string.IsNullOrWhiteSpace(customer.CompanyName) ? customer.CompanyName : customer.Code;
        const string entryType = "Customer Creation";

        void AddDel(string fieldName, string? val)
        {
            if (!string.IsNullOrWhiteSpace(val))
            {
                entries.Add(new AuditLogEntry
                {
                    User = username,
                    EntryType = entryType,
                    VouNo = vouNo,
                    FieldName = fieldName,
                    OldValue = val.Trim(),
                    NewValue = DeletedMarker,
                    Date = now.Date,
                    Time = now,
                    Checked = false
                });
            }
        }

        AddDel("Customer Name", customer.CompanyName);
        AddDel("Phone", customer.Phone);
        AddDel("Mobile", customer.Mobile);
        AddDel("City", customer.City);

        if (entries.Count > 0)
        {
            await auditRepo.LogAuditBatchAsync(entries, ct);
        }
    }

    public static async Task LogStaffModificationAsync(
        IAuditRepository auditRepo,
        StaffMember? original,
        StaffMember current,
        string username,
        CancellationToken ct = default)
    {
        if (original is null) return;

        var entries = new List<AuditLogEntry>();
        var now = DateTime.Now;
        var vouNo = current.Staff;
        const string entryType = "Staff Creation";

        void Compare(string fieldName, string? oldVal, string? newVal)
        {
            var o = oldVal?.Trim() ?? string.Empty;
            var n = newVal?.Trim() ?? string.Empty;
            if (!string.Equals(o, n, StringComparison.Ordinal))
            {
                entries.Add(new AuditLogEntry
                {
                    User = username,
                    EntryType = entryType,
                    VouNo = vouNo,
                    FieldName = fieldName,
                    OldValue = o,
                    NewValue = n,
                    Date = now.Date,
                    Time = now,
                    Checked = false
                });
            }
        }

        Compare("Staff", original.Staff, current.Staff);
        Compare("Status", original.Status, current.Status);

        if (entries.Count > 0)
        {
            await auditRepo.LogAuditBatchAsync(entries, ct);
        }
    }

    public static async Task LogStaffDeletionAsync(
        IAuditRepository auditRepo,
        string staffName,
        string username,
        CancellationToken ct = default)
    {
        var now = DateTime.Now;
        var entry = new AuditLogEntry
        {
            User = username,
            EntryType = "Staff Creation",
            VouNo = staffName,
            FieldName = "Staff",
            OldValue = staffName,
            NewValue = DeletedMarker,
            Date = now.Date,
            Time = now,
            Checked = false
        };
        await auditRepo.LogAuditAsync(entry, ct);
    }

    public static async Task LogSchemeModificationAsync(
        IAuditRepository auditRepo,
        SchemeCreationItem? original,
        SchemeCreationItem current,
        string username,
        CancellationToken ct = default)
    {
        if (original is null) return;

        var entries = new List<AuditLogEntry>();
        var now = DateTime.Now;
        var vouNo = current.Scode;
        const string entryType = "Scheme Creation";

        void Compare(string fieldName, string? oldVal, string? newVal)
        {
            var o = oldVal?.Trim() ?? string.Empty;
            var n = newVal?.Trim() ?? string.Empty;
            if (!string.Equals(o, n, StringComparison.Ordinal))
            {
                entries.Add(new AuditLogEntry
                {
                    User = username,
                    EntryType = entryType,
                    VouNo = vouNo,
                    FieldName = fieldName,
                    OldValue = o,
                    NewValue = n,
                    Date = now.Date,
                    Time = now,
                    Checked = false
                });
            }
        }

        Compare("Customer Name", original.CustomerName, current.CustomerName);
        Compare("Staff", original.Staff, current.Staff);
        Compare("Status", original.Status, current.Status);
        Compare("Installment Amount", original.InstallmentAmt.ToString("F2"), current.InstallmentAmt.ToString("F2"));
        Compare("Month", original.Month.ToString("F0"), current.Month.ToString("F0"));
        Compare("Total Amount", original.TotalAmt.ToString("F2"), current.TotalAmt.ToString("F2"));
        Compare("Scheme Discount", original.SchemeDiscount.ToString("F2"), current.SchemeDiscount.ToString("F2"));
        Compare("Total Value", original.TotalValue.ToString("F2"), current.TotalValue.ToString("F2"));

        if (entries.Count > 0)
        {
            await auditRepo.LogAuditBatchAsync(entries, ct);
        }
    }

    public static async Task LogSchemeDeletionAsync(
        IAuditRepository auditRepo,
        SchemeCreationItem scheme,
        string username,
        CancellationToken ct = default)
    {
        var entries = new List<AuditLogEntry>();
        var now = DateTime.Now;
        var vouNo = scheme.Scode;
        const string entryType = "Scheme Creation";

        void AddDel(string fieldName, string? val)
        {
            if (!string.IsNullOrWhiteSpace(val))
            {
                entries.Add(new AuditLogEntry
                {
                    User = username,
                    EntryType = entryType,
                    VouNo = vouNo,
                    FieldName = fieldName,
                    OldValue = val.Trim(),
                    NewValue = DeletedMarker,
                    Date = now.Date,
                    Time = now,
                    Checked = false
                });
            }
        }

        AddDel("Customer Name", scheme.CustomerName);
        AddDel("Installment Amount", scheme.InstallmentAmt.ToString("F2"));
        AddDel("Total Value", scheme.TotalValue.ToString("F2"));
        AddDel("Scheme", scheme.Scode);

        if (entries.Count > 0)
        {
            await auditRepo.LogAuditBatchAsync(entries, ct);
        }
    }

    public static async Task LogInvoiceModificationAsync(
        IAuditRepository auditRepo,
        Bill? originalBill,
        IList<BillSubform>? originalItems,
        IList<PaymentSubform>? originalPayments,
        Bill currentBill,
        IList<BillSubform> currentItems,
        IList<PaymentSubform> currentPayments,
        string username,
        CancellationToken ct = default)
    {
        if (originalBill is null) return;

        var entries = new List<AuditLogEntry>();
        var now = DateTime.Now;
        var vouNo = currentBill.VouNo;
        const string entryType = "Invoice Entry";

        void Compare(string fieldName, string? oldVal, string? newVal)
        {
            var o = oldVal?.Trim() ?? string.Empty;
            var n = newVal?.Trim() ?? string.Empty;
            if (!string.Equals(o, n, StringComparison.Ordinal))
            {
                entries.Add(new AuditLogEntry
                {
                    User = username,
                    EntryType = entryType,
                    VouNo = vouNo,
                    FieldName = fieldName,
                    OldValue = o,
                    NewValue = n,
                    Date = now.Date,
                    Time = now,
                    Checked = false
                });
            }
        }

        void CompareDecimal(string fieldName, decimal oldVal, decimal newVal, int decimals = 2)
        {
            var o = oldVal.ToString($"F{decimals}");
            var n = newVal.ToString($"F{decimals}");
            if (!string.Equals(o, n, StringComparison.Ordinal))
            {
                entries.Add(new AuditLogEntry
                {
                    User = username,
                    EntryType = entryType,
                    VouNo = vouNo,
                    FieldName = fieldName,
                    OldValue = o,
                    NewValue = n,
                    Date = now.Date,
                    Time = now,
                    Checked = false
                });
            }
        }

        // Header level diffs
        Compare("Customer Name", originalBill.CustomerName, currentBill.CustomerName);
        Compare("Phone", originalBill.Phone, currentBill.Phone);
        Compare("Address", originalBill.Address, currentBill.Address);
        CompareDecimal("Gross Amount", originalBill.GrossAmt, currentBill.GrossAmt);
        CompareDecimal("Tax Amount", originalBill.TaxAmt, currentBill.TaxAmt);
        CompareDecimal("Discount Allowed", originalBill.DiscountAllowed, currentBill.DiscountAllowed);
        CompareDecimal("Amount", originalBill.NetAmt, currentBill.NetAmt);
        Compare("Remarks", originalBill.Remarks, currentBill.Remarks);

        // Subform items diffs
        var origItemMap = (originalItems ?? Array.Empty<BillSubform>())
            .Where(x => !string.IsNullOrWhiteSpace(x.Barcode) || !string.IsNullOrWhiteSpace(x.ItemName))
            .ToList();
        var currItemMap = (currentItems ?? Array.Empty<BillSubform>())
            .Where(x => !string.IsNullOrWhiteSpace(x.Barcode) || !string.IsNullOrWhiteSpace(x.ItemName))
            .ToList();

        // Check for deleted items
        foreach (var oldItem in origItemMap)
        {
            var exists = currItemMap.Any(c => 
                (!string.IsNullOrWhiteSpace(oldItem.Barcode) && string.Equals(c.Barcode, oldItem.Barcode, StringComparison.OrdinalIgnoreCase)) ||
                (string.IsNullOrWhiteSpace(oldItem.Barcode) && string.Equals(c.ItemName, oldItem.ItemName, StringComparison.OrdinalIgnoreCase)));
            
            if (!exists)
            {
                entries.Add(new AuditLogEntry
                {
                    User = username,
                    EntryType = entryType,
                    VouNo = vouNo,
                    FieldName = "Item Name",
                    OldValue = oldItem.ItemName ?? string.Empty,
                    NewValue = DeletedMarker,
                    Date = now.Date,
                    Time = now,
                    Checked = false
                });
                if (!string.IsNullOrWhiteSpace(oldItem.Barcode))
                {
                    entries.Add(new AuditLogEntry
                    {
                        User = username,
                        EntryType = entryType,
                        VouNo = vouNo,
                        FieldName = "Barcode",
                        OldValue = oldItem.Barcode,
                        NewValue = DeletedMarker,
                        Date = now.Date,
                        Time = now,
                        Checked = false
                    });
                }
            }
        }

        // Check for modified or new items
        foreach (var newItem in currItemMap)
        {
            var match = origItemMap.FirstOrDefault(o => 
                (!string.IsNullOrWhiteSpace(newItem.Barcode) && string.Equals(o.Barcode, newItem.Barcode, StringComparison.OrdinalIgnoreCase)) ||
                (string.IsNullOrWhiteSpace(newItem.Barcode) && string.Equals(o.ItemName, newItem.ItemName, StringComparison.OrdinalIgnoreCase)));

            if (match != null)
            {
                Compare("Sales Man", match.SalesMan, newItem.SalesMan);
                CompareDecimal("Gross Wt", match.GrWt, newItem.GrWt, 3);
                CompareDecimal("Net wt", match.NtWt, newItem.NtWt, 3);
                CompareDecimal("Rate", match.Rate, newItem.Rate, 2);
                CompareDecimal("Making", match.Making, newItem.Making, 2);
                CompareDecimal("Metal Value", match.MetalValue, newItem.MetalValue, 2);
                CompareDecimal("Stone Value", match.StoneValue, newItem.StoneValue, 2);
            }
            else
            {
                // New item added during edit
                Compare("Item Name", string.Empty, newItem.ItemName);
                if (!string.IsNullOrWhiteSpace(newItem.Barcode))
                {
                    Compare("Barcode", string.Empty, newItem.Barcode);
                }
                CompareDecimal("Amount", 0, newItem.Amount, 2);
            }
        }

        // Payments diffs
        var origPayments = (originalPayments ?? Array.Empty<PaymentSubform>()).Where(p => !string.IsNullOrWhiteSpace(p.AccountName)).ToList();
        var currPayments = (currentPayments ?? Array.Empty<PaymentSubform>()).Where(p => !string.IsNullOrWhiteSpace(p.AccountName)).ToList();

        foreach (var currP in currPayments)
        {
            var matchP = origPayments.FirstOrDefault(o => string.Equals(o.AccountName, currP.AccountName, StringComparison.OrdinalIgnoreCase));
            if (matchP != null)
            {
                CompareDecimal("Payment Amount (" + currP.AccountName + ")", matchP.Amount, currP.Amount, 2);
            }
            else
            {
                Compare("Account Name", string.Empty, currP.AccountName);
                CompareDecimal("Amount", 0, currP.Amount, 2);
            }
        }

        if (entries.Count > 0)
        {
            await auditRepo.LogAuditBatchAsync(entries, ct);
        }
    }

    public static async Task LogInvoiceDeletionAsync(
        IAuditRepository auditRepo,
        Bill bill,
        IList<BillSubform>? items,
        IList<PaymentSubform>? payments,
        string username,
        CancellationToken ct = default)
    {
        var entries = new List<AuditLogEntry>();
        var now = DateTime.Now;
        var vouNo = bill.VouNo;
        const string entryType = "Invoice Entry";

        void AddDel(string fieldName, string? val)
        {
            if (!string.IsNullOrWhiteSpace(val))
            {
                entries.Add(new AuditLogEntry
                {
                    User = username,
                    EntryType = entryType,
                    VouNo = vouNo,
                    FieldName = fieldName,
                    OldValue = val.Trim(),
                    NewValue = DeletedMarker,
                    Date = now.Date,
                    Time = now,
                    Checked = false
                });
            }
        }

        AddDel("Customer Name", bill.CustomerName);
        AddDel("Amount", bill.NetAmt.ToString("F2"));
        AddDel("Gross Amount", bill.GrossAmt.ToString("F2"));

        if (items != null)
        {
            foreach (var item in items)
            {
                if (!string.IsNullOrWhiteSpace(item.Barcode))
                {
                    AddDel("Barcode", item.Barcode);
                }
                AddDel("Item Name", item.ItemName);
                AddDel("Gross Wt", item.GrWt.ToString("F3"));
                AddDel("Net wt", item.NtWt.ToString("F3"));
                AddDel("Amount", item.Amount.ToString("F2"));
            }
        }

        if (payments != null)
        {
            foreach (var pay in payments)
            {
                AddDel("Account Name", pay.AccountName);
                AddDel("Amount", pay.Amount.ToString("F2"));
            }
        }

        if (entries.Count > 0)
        {
            await auditRepo.LogAuditBatchAsync(entries, ct);
        }
    }

    // ==========================================
    // BARCODE CREATION (SINGLE & BULK)
    // ==========================================
    public static async Task LogBarcodeModificationAsync(
        IAuditRepository? auditRepo,
        BarcodeSubformItem? original,
        BarcodeSubformItem current,
        string username,
        CancellationToken ct = default)
    {
        if (auditRepo is null || original is null) return;

        var entries = new List<AuditLogEntry>();
        var now = DateTime.Now;
        var vouNo = current.Barcode;
        const string entryType = "Barcode Creation";

        void Compare(string fieldName, string? oldVal, string? newVal)
        {
            var o = oldVal?.Trim() ?? string.Empty;
            var n = newVal?.Trim() ?? string.Empty;
            if (!string.Equals(o, n, StringComparison.Ordinal))
            {
                entries.Add(new AuditLogEntry
                {
                    User = username,
                    EntryType = entryType,
                    VouNo = vouNo,
                    FieldName = fieldName,
                    OldValue = o,
                    NewValue = n,
                    Date = now.Date,
                    Time = now,
                    Checked = false
                });
            }
        }

        void CompareDecimal(string fieldName, decimal oldVal, decimal newVal, int decimals = 2)
        {
            var o = oldVal.ToString($"F{decimals}");
            var n = newVal.ToString($"F{decimals}");
            if (!string.Equals(o, n, StringComparison.Ordinal))
            {
                entries.Add(new AuditLogEntry
                {
                    User = username,
                    EntryType = entryType,
                    VouNo = vouNo,
                    FieldName = fieldName,
                    OldValue = o,
                    NewValue = n,
                    Date = now.Date,
                    Time = now,
                    Checked = false
                });
            }
        }

        Compare("Item Name", original.ItemName, current.ItemName);
        Compare("Purity", original.Purity, current.Purity);
        Compare("Description", original.Description, current.Description);
        Compare("HUID", original.HUID, current.HUID);
        Compare("Size", original.Size, current.Size);
        CompareDecimal("Quantity", original.Qty, current.Qty, 2);
        CompareDecimal("Gross Weight", original.GrossWt, current.GrossWt, 3);
        CompareDecimal("Less Weight", original.StoneWt, current.StoneWt, 3);
        CompareDecimal("Net Weight", original.NetWt, current.NetWt, 3);
        CompareDecimal("Making", original.Making, current.Making, 2);
        Compare("Making Type", original.MakingType, current.MakingType);
        CompareDecimal("Stone Value", original.StoneValue, current.StoneValue, 2);
        CompareDecimal("Cost Touch", original.CostTounch, current.CostTounch, 2);
        CompareDecimal("Cost Per Gram", original.CostPg, current.CostPg, 2);
        CompareDecimal("Cost MC", original.CostMaking, current.CostMaking, 2);
        CompareDecimal("Cost Price", original.CostPrice, current.CostPrice, 2);
        CompareDecimal("Cost Rate", original.CostRate, current.CostRate, 2);
        CompareDecimal("MRP", original.MRP, current.MRP, 2);
        Compare("Supplier", original.SupplierName, current.SupplierName);
        Compare("Invoice No", original.SuppInvNo, current.SuppInvNo);
        Compare("Box No", original.BoxNo, current.BoxNo);
        Compare("Godown", original.Godown, current.Godown);
        Compare("Sticker Format", original.Sti, current.Sti);

        if (entries.Count > 0)
        {
            await auditRepo.LogAuditBatchAsync(entries, ct);
        }
    }

    public static async Task LogBarcodeDeletionAsync(
        IAuditRepository? auditRepo,
        BarcodeSubformItem barcode,
        string username,
        CancellationToken ct = default)
    {
        if (auditRepo is null) return;

        var entries = new List<AuditLogEntry>();
        var now = DateTime.Now;
        var vouNo = barcode.Barcode;
        const string entryType = "Barcode Creation";

        void AddDel(string fieldName, string? val)
        {
            if (!string.IsNullOrWhiteSpace(val))
            {
                entries.Add(new AuditLogEntry
                {
                    User = username,
                    EntryType = entryType,
                    VouNo = vouNo,
                    FieldName = fieldName,
                    OldValue = val.Trim(),
                    NewValue = DeletedMarker,
                    Date = now.Date,
                    Time = now,
                    Checked = false
                });
            }
        }

        AddDel("Barcode", barcode.Barcode);
        AddDel("Item Name", barcode.ItemName);
        AddDel("Purity", barcode.Purity);
        AddDel("Gross Weight", barcode.GrossWt.ToString("F3"));
        AddDel("Net Weight", barcode.NetWt.ToString("F3"));
        AddDel("MRP", barcode.MRP.ToString("F2"));
        AddDel("Supplier", barcode.SupplierName);
        AddDel("HUID", barcode.HUID);

        if (entries.Count > 0)
        {
            await auditRepo.LogAuditBatchAsync(entries, ct);
        }
    }

    public static async Task LogBarcodeBulkVoucherModificationAsync(
        IAuditRepository? auditRepo,
        BarcodeCreationHeader? original,
        BarcodeCreationHeader current,
        string username,
        CancellationToken ct = default)
    {
        if (auditRepo is null || original is null) return;

        var entries = new List<AuditLogEntry>();
        var now = DateTime.Now;
        var vouNo = current.VouNo;
        const string entryType = "Barcode Creation";

        void Compare(string fieldName, string? oldVal, string? newVal)
        {
            var o = oldVal?.Trim() ?? string.Empty;
            var n = newVal?.Trim() ?? string.Empty;
            if (!string.Equals(o, n, StringComparison.Ordinal))
            {
                entries.Add(new AuditLogEntry
                {
                    User = username,
                    EntryType = entryType,
                    VouNo = vouNo,
                    FieldName = fieldName,
                    OldValue = o,
                    NewValue = n,
                    Date = now.Date,
                    Time = now,
                    Checked = false
                });
            }
        }

        Compare("Supplier", original.ArtisianName, current.ArtisianName);
        Compare("Supplier Bill No", original.SuppBillNo, current.SuppBillNo);

        var origItems = original.Items ?? new List<BarcodeSubformItem>();
        var currItems = current.Items ?? new List<BarcodeSubformItem>();

        foreach (var oldItem in origItems)
        {
            if (!currItems.Any(c => string.Equals(c.Barcode, oldItem.Barcode, StringComparison.OrdinalIgnoreCase)))
            {
                entries.Add(new AuditLogEntry
                {
                    User = username,
                    EntryType = entryType,
                    VouNo = vouNo,
                    FieldName = $"Item ({oldItem.Barcode})",
                    OldValue = $"{oldItem.ItemName} ({oldItem.NetWt:F3}g)",
                    NewValue = DeletedMarker,
                    Date = now.Date,
                    Time = now,
                    Checked = false
                });
            }
        }

        foreach (var newItem in currItems)
        {
            var match = origItems.FirstOrDefault(o => string.Equals(o.Barcode, newItem.Barcode, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                if (!string.Equals(match.ItemName?.Trim(), newItem.ItemName?.Trim(), StringComparison.Ordinal))
                {
                    entries.Add(new AuditLogEntry
                    {
                        User = username,
                        EntryType = entryType,
                        VouNo = vouNo,
                        FieldName = $"Item Name ({newItem.Barcode})",
                        OldValue = match.ItemName?.Trim() ?? string.Empty,
                        NewValue = newItem.ItemName?.Trim() ?? string.Empty,
                        Date = now.Date,
                        Time = now,
                        Checked = false
                    });
                }
                if (match.NetWt != newItem.NetWt)
                {
                    entries.Add(new AuditLogEntry
                    {
                        User = username,
                        EntryType = entryType,
                        VouNo = vouNo,
                        FieldName = $"Net Weight ({newItem.Barcode})",
                        OldValue = match.NetWt.ToString("F3"),
                        NewValue = newItem.NetWt.ToString("F3"),
                        Date = now.Date,
                        Time = now,
                        Checked = false
                    });
                }
                if (match.MRP != newItem.MRP)
                {
                    entries.Add(new AuditLogEntry
                    {
                        User = username,
                        EntryType = entryType,
                        VouNo = vouNo,
                        FieldName = $"MRP ({newItem.Barcode})",
                        OldValue = match.MRP.ToString("F2"),
                        NewValue = newItem.MRP.ToString("F2"),
                        Date = now.Date,
                        Time = now,
                        Checked = false
                    });
                }
            }
            else
            {
                entries.Add(new AuditLogEntry
                {
                    User = username,
                    EntryType = entryType,
                    VouNo = vouNo,
                    FieldName = $"New Item ({newItem.Barcode})",
                    OldValue = string.Empty,
                    NewValue = $"{newItem.ItemName} ({newItem.NetWt:F3}g)",
                    Date = now.Date,
                    Time = now,
                    Checked = false
                });
            }
        }

        if (entries.Count > 0)
        {
            await auditRepo.LogAuditBatchAsync(entries, ct);
        }
    }

    public static async Task LogBarcodeBulkVoucherDeletionAsync(
        IAuditRepository? auditRepo,
        BarcodeCreationHeader voucher,
        string username,
        CancellationToken ct = default)
    {
        if (auditRepo is null) return;

        var entries = new List<AuditLogEntry>();
        var now = DateTime.Now;
        var vouNo = voucher.VouNo;
        const string entryType = "Barcode Creation";

        void AddDel(string fieldName, string? val)
        {
            if (!string.IsNullOrWhiteSpace(val))
            {
                entries.Add(new AuditLogEntry
                {
                    User = username,
                    EntryType = entryType,
                    VouNo = vouNo,
                    FieldName = fieldName,
                    OldValue = val.Trim(),
                    NewValue = DeletedMarker,
                    Date = now.Date,
                    Time = now,
                    Checked = false
                });
            }
        }

        AddDel("Voucher No", voucher.VouNo);
        AddDel("Supplier", voucher.ArtisianName);
        AddDel("Supplier Bill No", voucher.SuppBillNo);

        if (voucher.Items != null)
        {
            foreach (var item in voucher.Items)
            {
                AddDel($"Barcode ({item.Barcode})", $"{item.ItemName} - GrWt: {item.GrossWt:F3}g, NtWt: {item.NetWt:F3}g, MRP: {item.MRP:F2}");
            }
        }

        if (entries.Count > 0)
        {
            await auditRepo.LogAuditBatchAsync(entries, ct);
        }
    }

    // ==========================================
    // PURCHASE ENTRY
    // ==========================================
    public static async Task LogPurchaseModificationAsync(
        IAuditRepository? auditRepo,
        Purchase? originalPurchase,
        IList<PurchaseSubform>? originalItems,
        IList<PaymentSubform>? originalPayments,
        Purchase currentPurchase,
        IList<PurchaseSubform> currentItems,
        IList<PaymentSubform> currentPayments,
        string username,
        CancellationToken ct = default)
    {
        if (auditRepo is null || originalPurchase is null) return;

        var entries = new List<AuditLogEntry>();
        var now = DateTime.Now;
        var vouNo = currentPurchase.VouNo;
        const string entryType = "Purchase Entry";

        void Compare(string fieldName, string? oldVal, string? newVal)
        {
            var o = oldVal?.Trim() ?? string.Empty;
            var n = newVal?.Trim() ?? string.Empty;
            if (!string.Equals(o, n, StringComparison.Ordinal))
            {
                entries.Add(new AuditLogEntry
                {
                    User = username,
                    EntryType = entryType,
                    VouNo = vouNo,
                    FieldName = fieldName,
                    OldValue = o,
                    NewValue = n,
                    Date = now.Date,
                    Time = now,
                    Checked = false
                });
            }
        }

        void CompareDecimal(string fieldName, decimal oldVal, decimal newVal, int decimals = 2)
        {
            var o = oldVal.ToString($"F{decimals}");
            var n = newVal.ToString($"F{decimals}");
            if (!string.Equals(o, n, StringComparison.Ordinal))
            {
                entries.Add(new AuditLogEntry
                {
                    User = username,
                    EntryType = entryType,
                    VouNo = vouNo,
                    FieldName = fieldName,
                    OldValue = o,
                    NewValue = n,
                    Date = now.Date,
                    Time = now,
                    Checked = false
                });
            }
        }

        Compare("Supplier Name", originalPurchase.AccountName, currentPurchase.AccountName);
        Compare("Supplier Bill No", originalPurchase.SuppBillNo, currentPurchase.SuppBillNo);
        Compare("Phone", originalPurchase.Phone, currentPurchase.Phone);
        Compare("City", originalPurchase.City, currentPurchase.City);
        CompareDecimal("Gross Amount", originalPurchase.GrossAmt, currentPurchase.GrossAmt);
        CompareDecimal("Tax Amount", originalPurchase.TaxAmt, currentPurchase.TaxAmt);
        CompareDecimal("Discount Allowed", originalPurchase.DiscountAllowed, currentPurchase.DiscountAllowed);
        CompareDecimal("Net Amount", originalPurchase.NetAmt, currentPurchase.NetAmt);
        CompareDecimal("Total Gross Weight", originalPurchase.GrWt, currentPurchase.GrWt, 3);
        CompareDecimal("Total Net Weight", originalPurchase.NtWt, currentPurchase.NtWt, 3);
        Compare("Remarks", originalPurchase.Remarks, currentPurchase.Remarks);
        Compare("Narration", originalPurchase.Narration, currentPurchase.Narration);

        var origItems = (originalItems ?? Array.Empty<PurchaseSubform>()).Where(x => !string.IsNullOrWhiteSpace(x.ItemName)).ToList();
        var currItems = (currentItems ?? Array.Empty<PurchaseSubform>()).Where(x => !string.IsNullOrWhiteSpace(x.ItemName)).ToList();

        foreach (var oldItem in origItems)
        {
            var exists = currItems.Any(c =>
                (!string.IsNullOrWhiteSpace(oldItem.Barcode) && string.Equals(c.Barcode, oldItem.Barcode, StringComparison.OrdinalIgnoreCase)) ||
                (string.IsNullOrWhiteSpace(oldItem.Barcode) && c.SlNo == oldItem.SlNo));

            if (!exists)
            {
                entries.Add(new AuditLogEntry
                {
                    User = username,
                    EntryType = entryType,
                    VouNo = vouNo,
                    FieldName = "Item Name",
                    OldValue = oldItem.ItemName ?? string.Empty,
                    NewValue = DeletedMarker,
                    Date = now.Date,
                    Time = now,
                    Checked = false
                });
            }
        }

        foreach (var newItem in currItems)
        {
            var match = origItems.FirstOrDefault(o =>
                (!string.IsNullOrWhiteSpace(newItem.Barcode) && string.Equals(o.Barcode, newItem.Barcode, StringComparison.OrdinalIgnoreCase)) ||
                (string.IsNullOrWhiteSpace(newItem.Barcode) && o.SlNo == newItem.SlNo));

            if (match != null)
            {
                Compare($"Item Name (Row {newItem.SlNo})", match.ItemName, newItem.ItemName);
                CompareDecimal($"Gross Wt (Row {newItem.SlNo})", match.GrWt, newItem.GrWt, 3);
                CompareDecimal($"Net Wt (Row {newItem.SlNo})", match.NtWt, newItem.NtWt, 3);
                CompareDecimal($"Rate (Row {newItem.SlNo})", match.Rate, newItem.Rate, 2);
                CompareDecimal($"Amount (Row {newItem.SlNo})", match.Amount, newItem.Amount, 2);
            }
            else
            {
                Compare($"New Item (Row {newItem.SlNo})", string.Empty, newItem.ItemName);
                CompareDecimal($"Amount (Row {newItem.SlNo})", 0, newItem.Amount, 2);
            }
        }

        var origPayments = (originalPayments ?? Array.Empty<PaymentSubform>()).Where(p => !string.IsNullOrWhiteSpace(p.AccountName)).ToList();
        var currPayments = (currentPayments ?? Array.Empty<PaymentSubform>()).Where(p => !string.IsNullOrWhiteSpace(p.AccountName)).ToList();

        foreach (var currP in currPayments)
        {
            var matchP = origPayments.FirstOrDefault(o => string.Equals(o.AccountName, currP.AccountName, StringComparison.OrdinalIgnoreCase));
            if (matchP != null)
            {
                CompareDecimal("Payment Amount (" + currP.AccountName + ")", matchP.Amount, currP.Amount, 2);
            }
            else
            {
                Compare("Payment Account", string.Empty, currP.AccountName);
                CompareDecimal("Payment Amount", 0, currP.Amount, 2);
            }
        }

        if (entries.Count > 0)
        {
            await auditRepo.LogAuditBatchAsync(entries, ct);
        }
    }

    public static async Task LogPurchaseDeletionAsync(
        IAuditRepository? auditRepo,
        Purchase purchase,
        IList<PurchaseSubform>? items,
        IList<PaymentSubform>? payments,
        string username,
        CancellationToken ct = default)
    {
        if (auditRepo is null) return;

        var entries = new List<AuditLogEntry>();
        var now = DateTime.Now;
        var vouNo = purchase.VouNo;
        const string entryType = "Purchase Entry";

        void AddDel(string fieldName, string? val)
        {
            if (!string.IsNullOrWhiteSpace(val))
            {
                entries.Add(new AuditLogEntry
                {
                    User = username,
                    EntryType = entryType,
                    VouNo = vouNo,
                    FieldName = fieldName,
                    OldValue = val.Trim(),
                    NewValue = DeletedMarker,
                    Date = now.Date,
                    Time = now,
                    Checked = false
                });
            }
        }

        AddDel("Supplier Name", purchase.AccountName);
        AddDel("Supplier Bill No", purchase.SuppBillNo);
        AddDel("Net Amount", purchase.NetAmt.ToString("F2"));
        AddDel("Gross Amount", purchase.GrossAmt.ToString("F2"));
        AddDel("Total Net Weight", purchase.NtWt.ToString("F3"));

        if (items != null)
        {
            foreach (var item in items)
            {
                AddDel($"Item ({item.ItemName})", $"GrWt: {item.GrWt:F3}g, NtWt: {item.NtWt:F3}g, Amount: {item.Amount:F2}");
            }
        }

        if (payments != null)
        {
            foreach (var pay in payments)
            {
                AddDel("Payment Account", $"{pay.AccountName}: {pay.Amount:F2}");
            }
        }

        if (entries.Count > 0)
        {
            await auditRepo.LogAuditBatchAsync(entries, ct);
        }
    }

    // ==========================================
    // PURCHASE EXPENSE
    // ==========================================
    public static async Task LogPurchaseExpenseModificationAsync(
        IAuditRepository? auditRepo,
        PurchaseExpense? original,
        PurchaseExpense current,
        string username,
        CancellationToken ct = default)
    {
        if (auditRepo is null || original is null) return;

        var entries = new List<AuditLogEntry>();
        var now = DateTime.Now;
        var vouNo = current.VouNo;
        const string entryType = "Purchase Expense";

        void Compare(string fieldName, string? oldVal, string? newVal)
        {
            var o = oldVal?.Trim() ?? string.Empty;
            var n = newVal?.Trim() ?? string.Empty;
            if (!string.Equals(o, n, StringComparison.Ordinal))
            {
                entries.Add(new AuditLogEntry
                {
                    User = username,
                    EntryType = entryType,
                    VouNo = vouNo,
                    FieldName = fieldName,
                    OldValue = o,
                    NewValue = n,
                    Date = now.Date,
                    Time = now,
                    Checked = false
                });
            }
        }

        void CompareDecimal(string fieldName, decimal oldVal, decimal newVal, int decimals = 2)
        {
            var o = oldVal.ToString($"F{decimals}");
            var n = newVal.ToString($"F{decimals}");
            if (!string.Equals(o, n, StringComparison.Ordinal))
            {
                entries.Add(new AuditLogEntry
                {
                    User = username,
                    EntryType = entryType,
                    VouNo = vouNo,
                    FieldName = fieldName,
                    OldValue = o,
                    NewValue = n,
                    Date = now.Date,
                    Time = now,
                    Checked = false
                });
            }
        }

        Compare("Supplier / Account Name", original.AccountName, current.AccountName);
        Compare("Purchase Head", original.Head, current.Head);
        Compare("GST Bill From", original.PartyName, current.PartyName);
        Compare("GSTIN", original.Gstin, current.Gstin);
        Compare("Supplier Bill No", original.SuppBillNo, current.SuppBillNo);
        CompareDecimal("Gross Total", original.GrossTotal, current.GrossTotal);
        CompareDecimal("Taxable Amount", original.TaxableAmount, current.TaxableAmount);
        CompareDecimal("Tax Amount", original.TaxAmount, current.TaxAmount);
        CompareDecimal("IGST", original.Igst, current.Igst);
        CompareDecimal("CGST", original.Cgst, current.Cgst);
        CompareDecimal("SGST", original.Sgst, current.Sgst);
        CompareDecimal("Net Total", original.NetAmount, current.NetAmount);
        Compare("Note", original.Narration, current.Narration);

        var origLines = original.Lines ?? new List<PurchaseExpenseLine>();
        var currLines = current.Lines ?? new List<PurchaseExpenseLine>();

        foreach (var oldLine in origLines)
        {
            if (!currLines.Any(c => string.Equals(c.SlNo, oldLine.SlNo, StringComparison.OrdinalIgnoreCase) && string.Equals(c.ItemName, oldLine.ItemName, StringComparison.OrdinalIgnoreCase)))
            {
                entries.Add(new AuditLogEntry
                {
                    User = username,
                    EntryType = entryType,
                    VouNo = vouNo,
                    FieldName = $"Line Item ({oldLine.ItemName})",
                    OldValue = $"{oldLine.ItemName} (₹{oldLine.GrossAmount:F2})",
                    NewValue = DeletedMarker,
                    Date = now.Date,
                    Time = now,
                    Checked = false
                });
            }
        }

        foreach (var newLine in currLines)
        {
            var match = origLines.FirstOrDefault(o => string.Equals(o.SlNo, newLine.SlNo, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                Compare($"Line Item Name (Row {newLine.SlNo})", match.ItemName, newLine.ItemName);
                CompareDecimal($"Line Qty (Row {newLine.SlNo})", match.Qty, newLine.Qty, 2);
                CompareDecimal($"Line Rate (Row {newLine.SlNo})", match.Rate, newLine.Rate, 2);
                CompareDecimal($"Line Rate Amount (Row {newLine.SlNo})", match.RateAmount, newLine.RateAmount, 2);
                CompareDecimal($"Line Tax Rate (Row {newLine.SlNo})", match.TaxRate, newLine.TaxRate, 2);
                CompareDecimal($"Line Gross Amount (Row {newLine.SlNo})", match.GrossAmount, newLine.GrossAmount, 2);
            }
            else
            {
                Compare($"New Line Item (Row {newLine.SlNo})", string.Empty, newLine.ItemName);
                CompareDecimal($"New Line Gross Amount (Row {newLine.SlNo})", 0, newLine.GrossAmount, 2);
            }
        }

        if (entries.Count > 0)
        {
            await auditRepo.LogAuditBatchAsync(entries, ct);
        }
    }

    public static async Task LogPurchaseExpenseDeletionAsync(
        IAuditRepository? auditRepo,
        PurchaseExpense expense,
        string username,
        CancellationToken ct = default)
    {
        if (auditRepo is null) return;

        var entries = new List<AuditLogEntry>();
        var now = DateTime.Now;
        var vouNo = expense.VouNo;
        const string entryType = "Purchase Expense";

        void AddDel(string fieldName, string? val)
        {
            if (!string.IsNullOrWhiteSpace(val))
            {
                entries.Add(new AuditLogEntry
                {
                    User = username,
                    EntryType = entryType,
                    VouNo = vouNo,
                    FieldName = fieldName,
                    OldValue = val.Trim(),
                    NewValue = DeletedMarker,
                    Date = now.Date,
                    Time = now,
                    Checked = false
                });
            }
        }

        AddDel("Supplier / Account Name", expense.AccountName);
        AddDel("Purchase Head", expense.Head);
        AddDel("Supplier Bill No", expense.SuppBillNo);
        AddDel("Net Total", expense.NetAmount.ToString("F2"));
        AddDel("Gross Total", expense.GrossTotal.ToString("F2"));

        if (expense.Lines != null)
        {
            foreach (var line in expense.Lines)
            {
                AddDel($"Line Item ({line.ItemName})", $"Qty: {line.Qty:F2}, Rate: {line.Rate:F2}, Gross: {line.GrossAmount:F2}");
            }
        }

        if (entries.Count > 0)
        {
            await auditRepo.LogAuditBatchAsync(entries, ct);
        }
    }

    // ==========================================
    // VOUCHERS (PAYMENTS, RECEIPTS, JOURNALS)
    // ==========================================
    public static async Task LogVoucherModificationAsync(
        IAuditRepository? auditRepo,
        VoucherEntry? original,
        VoucherEntry current,
        string entryType,
        string username,
        CancellationToken ct = default)
    {
        if (auditRepo is null || original is null) return;

        var entries = new List<AuditLogEntry>();
        var now = DateTime.Now;
        var vouNo = current.VouNo;

        void Compare(string fieldName, string? oldVal, string? newVal)
        {
            var o = oldVal?.Trim() ?? string.Empty;
            var n = newVal?.Trim() ?? string.Empty;
            if (!string.Equals(o, n, StringComparison.Ordinal))
            {
                entries.Add(new AuditLogEntry
                {
                    User = username,
                    EntryType = entryType,
                    VouNo = vouNo,
                    FieldName = fieldName,
                    OldValue = o,
                    NewValue = n,
                    Date = now.Date,
                    Time = now,
                    Checked = false
                });
            }
        }

        void CompareDecimal(string fieldName, decimal oldVal, decimal newVal, int decimals = 2)
        {
            var o = oldVal.ToString($"F{decimals}");
            var n = newVal.ToString($"F{decimals}");
            if (!string.Equals(o, n, StringComparison.Ordinal))
            {
                entries.Add(new AuditLogEntry
                {
                    User = username,
                    EntryType = entryType,
                    VouNo = vouNo,
                    FieldName = fieldName,
                    OldValue = o,
                    NewValue = n,
                    Date = now.Date,
                    Time = now,
                    Checked = false
                });
            }
        }

        Compare("Debit Account", original.DrAccount, current.DrAccount);
        Compare("Credit Account", original.CrAccount, current.CrAccount);
        CompareDecimal("Amount", original.DrAmt, current.DrAmt, 2);
        Compare("Payment Mode", original.PaymentMode, current.PaymentMode);
        Compare("Cheque No", original.ChequeNo, current.ChequeNo);
        Compare("Bank Name", original.BankName, current.BankName);
        Compare("Card Type", original.CardType, current.CardType);
        CompareDecimal("Card Comm", original.CardComm, current.CardComm, 2);
        CompareDecimal("Comm Amt", original.CommAmt, current.CommAmt, 2);
        CompareDecimal("Net Credited", original.NetCredited, current.NetCredited, 2);
        Compare("Narration", original.Narration, current.Narration);

        if (entries.Count > 0)
        {
            await auditRepo.LogAuditBatchAsync(entries, ct);
        }
    }

    public static async Task LogVoucherDeletionAsync(
        IAuditRepository? auditRepo,
        VoucherEntry voucher,
        string entryType,
        string username,
        CancellationToken ct = default)
    {
        if (auditRepo is null) return;

        var entries = new List<AuditLogEntry>();
        var now = DateTime.Now;
        var vouNo = voucher.VouNo;

        void AddDel(string fieldName, string? val)
        {
            if (!string.IsNullOrWhiteSpace(val))
            {
                entries.Add(new AuditLogEntry
                {
                    User = username,
                    EntryType = entryType,
                    VouNo = vouNo,
                    FieldName = fieldName,
                    OldValue = val.Trim(),
                    NewValue = DeletedMarker,
                    Date = now.Date,
                    Time = now,
                    Checked = false
                });
            }
        }

        AddDel("Debit Account", voucher.DrAccount);
        AddDel("Credit Account", voucher.CrAccount);
        AddDel("Amount", voucher.DrAmt.ToString("F2"));
        AddDel("Payment Mode", voucher.PaymentMode);
        AddDel("Narration", voucher.Narration);

        if (entries.Count > 0)
        {
            await auditRepo.LogAuditBatchAsync(entries, ct);
        }
    }

    // ==========================================
    // SCHEME RECEIPTS
    // ==========================================
    public static async Task LogSchemeReceiptModificationAsync(
        IAuditRepository? auditRepo,
        VoucherEntry? original,
        VoucherEntry current,
        string username,
        CancellationToken ct = default)
    {
        if (auditRepo is null || original is null) return;

        var entries = new List<AuditLogEntry>();
        var now = DateTime.Now;
        var vouNo = current.VouNo;
        const string entryType = "Scheme Receipt";

        void Compare(string fieldName, string? oldVal, string? newVal)
        {
            var o = oldVal?.Trim() ?? string.Empty;
            var n = newVal?.Trim() ?? string.Empty;
            if (!string.Equals(o, n, StringComparison.Ordinal))
            {
                entries.Add(new AuditLogEntry
                {
                    User = username,
                    EntryType = entryType,
                    VouNo = vouNo,
                    FieldName = fieldName,
                    OldValue = o,
                    NewValue = n,
                    Date = now.Date,
                    Time = now,
                    Checked = false
                });
            }
        }

        void CompareDecimal(string fieldName, decimal oldVal, decimal newVal, int decimals = 2)
        {
            var o = oldVal.ToString($"F{decimals}");
            var n = newVal.ToString($"F{decimals}");
            if (!string.Equals(o, n, StringComparison.Ordinal))
            {
                entries.Add(new AuditLogEntry
                {
                    User = username,
                    EntryType = entryType,
                    VouNo = vouNo,
                    FieldName = fieldName,
                    OldValue = o,
                    NewValue = n,
                    Date = now.Date,
                    Time = now,
                    Checked = false
                });
            }
        }

        Compare("Scheme Code (Scode)", original.Scode, current.Scode);
        Compare("Customer (CrAccount)", original.CrAccount, current.CrAccount);
        Compare("Received By (DrAccount)", original.DrAccount, current.DrAccount);
        CompareDecimal("Installment No", original.InstallmentNo, current.InstallmentNo, 0);
        CompareDecimal("Amount", original.DrAmt, current.DrAmt, 2);
        Compare("Payment Mode", original.PaymentMode, current.PaymentMode);
        Compare("Cheque No", original.ChequeNo, current.ChequeNo);
        Compare("Bank Name", original.BankName, current.BankName);
        Compare("Narration", original.Narration, current.Narration);

        if (entries.Count > 0)
        {
            await auditRepo.LogAuditBatchAsync(entries, ct);
        }
    }

    public static async Task LogSchemeReceiptDeletionAsync(
        IAuditRepository? auditRepo,
        VoucherEntry receipt,
        string username,
        CancellationToken ct = default)
    {
        if (auditRepo is null) return;

        var entries = new List<AuditLogEntry>();
        var now = DateTime.Now;
        var vouNo = receipt.VouNo;
        const string entryType = "Scheme Receipt";

        void AddDel(string fieldName, string? val)
        {
            if (!string.IsNullOrWhiteSpace(val))
            {
                entries.Add(new AuditLogEntry
                {
                    User = username,
                    EntryType = entryType,
                    VouNo = vouNo,
                    FieldName = fieldName,
                    OldValue = val.Trim(),
                    NewValue = DeletedMarker,
                    Date = now.Date,
                    Time = now,
                    Checked = false
                });
            }
        }

        AddDel("Scheme Code (Scode)", receipt.Scode);
        AddDel("Customer", receipt.CrAccount);
        AddDel("Received By", receipt.DrAccount);
        AddDel("Installment No", receipt.InstallmentNo.ToString());
        AddDel("Amount", receipt.DrAmt.ToString("F2"));
        AddDel("Payment Mode", receipt.PaymentMode);
        AddDel("Narration", receipt.Narration);

        if (entries.Count > 0)
        {
            await auditRepo.LogAuditBatchAsync(entries, ct);
        }
    }

    // ==========================================
    // RATE MASTER / PURITY RATE UPDATES
    // ==========================================

    public static async Task LogRateUpdatesAsync(
        IAuditRepository? auditRepo,
        IEnumerable<RateUpdateItem> rateUpdates,
        string username,
        CancellationToken ct = default)
    {
        if (auditRepo is null) return;

        var entries = new List<AuditLogEntry>();
        var now = DateTime.Now;
        const string entryType = "Rate Master";

        foreach (var item in rateUpdates)
        {
            var o = item.OldRate.ToString("F2");
            var n = item.NewRate.ToString("F2");

            if (!string.Equals(o, n, StringComparison.Ordinal))
            {
                entries.Add(new AuditLogEntry
                {
                    User = username,
                    EntryType = entryType,
                    VouNo = item.Purity,
                    FieldName = "Rate/Gr",
                    OldValue = o,
                    NewValue = n,
                    Date = now.Date,
                    Time = now,
                    Checked = false
                });
            }
        }

        if (entries.Count > 0)
        {
            await auditRepo.LogAuditBatchAsync(entries, ct);
        }
    }
}
