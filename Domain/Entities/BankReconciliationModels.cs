namespace ConceptJewelleryPro.Domain.Entities;

public enum BankReconciliationStatus
{
    Reconciled,
    PartialMatch,
    BankOnly,
    ErpOnly
}

public sealed class BankStatementTransaction
{
    public int RowNo { get; set; }
    public DateTime Date { get; set; } = DateTime.Today;
    public DateTime? ValueDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string ReferenceNo { get; set; } = string.Empty;
    public decimal WithdrawalDebit { get; set; }
    public decimal DepositCredit { get; set; }
    public decimal Debit => WithdrawalDebit;
    public decimal Credit => DepositCredit;
    public decimal Balance { get; set; }
    public bool IsReconciled { get; set; }
    public string? MatchedVouNo { get; set; }
    public string? MatchedNotes { get; set; }
}

public sealed class BankReconciliationItem
{
    public BankReconciliationStatus Status { get; set; } = BankReconciliationStatus.BankOnly;
    public DateTime TransactionDate { get; set; } = DateTime.Today;
    public string Description { get; set; } = string.Empty;
    public string ReferenceNo { get; set; } = string.Empty;

    // Bank Statement amounts (from Bank's perspective: Deposit = Credit, Withdrawal = Debit)
    public decimal StatementDebit { get; set; }
    public decimal StatementCredit { get; set; }

    // ERP Ledger amounts (from Company's perspective: Deposit = Debit, Payment = Credit)
    public decimal ErpDebit { get; set; }
    public decimal ErpCredit { get; set; }

    public string ErpVouNo { get; set; } = string.Empty;
    public string ErpVouType { get; set; } = string.Empty;
    public string ErpParticulars { get; set; } = string.Empty;
    public string MatchedNotes { get; set; } = string.Empty;
    public double MatchScore { get; set; }

    public bool IsReconciled => Status == BankReconciliationStatus.Reconciled;
    public bool IsPartialMatch => Status == BankReconciliationStatus.PartialMatch;
    public bool IsBankOnly => Status == BankReconciliationStatus.BankOnly;
    public bool IsErpOnly => Status == BankReconciliationStatus.ErpOnly;

    public string StatusBadgeText => Status switch
    {
        BankReconciliationStatus.Reconciled => "✓ Reconciled",
        BankReconciliationStatus.PartialMatch => "⚡ Partial Match",
        BankReconciliationStatus.BankOnly => "⚠️ Bank Only",
        BankReconciliationStatus.ErpOnly => "ℹ️ ERP Only",
        _ => "Unknown"
    };

    public string StatusBadgeColor => Status switch
    {
        BankReconciliationStatus.Reconciled => "#22C55E",
        BankReconciliationStatus.PartialMatch => "#8B5CF6",
        BankReconciliationStatus.BankOnly => "#F59E0B",
        BankReconciliationStatus.ErpOnly => "#3B82F6",
        _ => "#6B7280"
    };
}

public sealed class BankReconciliationSummary
{
    public decimal StatementOpeningBalance { get; set; }
    public decimal StatementClosingBalance { get; set; }
    public decimal BookOpeningBalance { get; set; }
    public decimal BookClosingBalance { get; set; }

    public decimal TotalUnclearedDeposits { get; set; }
    public decimal TotalUnpresentedWithdrawals { get; set; }

    public decimal ReconciledBalance => StatementClosingBalance + TotalUnclearedDeposits - TotalUnpresentedWithdrawals;
    public decimal NetDifference => Math.Round(ReconciledBalance - BookClosingBalance, 2);
    public bool IsBalanced => Math.Abs(NetDifference) < 0.01m;

    public int TotalStatementCount { get; set; }
    public int ReconciledCount { get; set; }
    public int PartialMatchCount { get; set; }
    public int BankOnlyCount { get; set; }
    public int ErpOnlyCount { get; set; }
}

public sealed class BankReconciliationNavArgs
{
    public string? BankAccountName { get; set; }
    public string? StatementFilePath { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? Remarks { get; set; }
}
