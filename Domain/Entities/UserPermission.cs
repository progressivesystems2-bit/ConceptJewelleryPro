namespace ConceptJewelleryPro.Domain.Entities;

public class UserPermission
{
    public string User { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Toolbar { get; set; } = "DP1";

    // --- Entries / Transaction Permissions (Add, Mod, Del) ---
    // Sales / Invoice
    public bool SaleAdd { get; set; }
    public bool SaleMod { get; set; }
    public bool SaleDel { get; set; }

    // Ledgers / Accounts
    public bool LedAdd { get; set; }
    public bool LedMod { get; set; }
    public bool LedDel { get; set; }

    // Item Master
    public bool ItemAdd { get; set; }
    public bool ItemMod { get; set; }
    public bool ItemDel { get; set; }

    // Barcode
    public bool BarAdd { get; set; }
    public bool BarMod { get; set; }
    public bool BarDel { get; set; }

    // Bank / Cash (Bc)
    public bool BcAdd { get; set; }
    public bool BcMod { get; set; }
    public bool BcDel { get; set; }

    // Sales Return / Return
    public bool RetAdd { get; set; }
    public bool RetMod { get; set; }
    public bool RetDel { get; set; }

    // Purchase
    public bool PurAdd { get; set; }
    public bool PurMod { get; set; }
    public bool PurDel { get; set; }

    // Receipts
    public bool RecAdd { get; set; }
    public bool RecMod { get; set; }
    public bool RecDel { get; set; }

    // Customers
    public bool CusAdd { get; set; }
    public bool CusMod { get; set; }
    public bool CusDel { get; set; }

    // Payments
    public bool PayAdd { get; set; }
    public bool PayMod { get; set; }
    public bool PayDel { get; set; }

    // Journal
    public bool JouAdd { get; set; }
    public bool JouMod { get; set; }
    public bool JouDel { get; set; }

    // Material / Karighar Issue (MIss)
    public bool MIssAdd { get; set; }
    public bool MIssMod { get; set; }
    public bool MIssDel { get; set; }

    // Material / Karighar Receive (MRec)
    public bool MRecAdd { get; set; }
    public bool MRecMod { get; set; }
    public bool MRecDel { get; set; }

    // Approval Issue (AIss)
    public bool AIssAdd { get; set; }
    public bool AIssMod { get; set; }
    public bool AIssDel { get; set; }

    // Approval Receive (ARec)
    public bool ARecAdd { get; set; }
    public bool ARecMod { get; set; }
    public bool ARecDel { get; set; }

    // Hallmark Issue (HIss)
    public bool HIssAdd { get; set; }
    public bool HIssMod { get; set; }
    public bool HIssDel { get; set; }

    // Hallmark Receive (HRec)
    public bool HRecAdd { get; set; }
    public bool HRecMod { get; set; }
    public bool HRecDel { get; set; }

    // User Management
    public bool UserAdd { get; set; }
    public bool UserMod { get; set; }
    public bool UserDel { get; set; }

    // Repairs
    public bool RepAdd { get; set; }
    public bool RepMod { get; set; }
    public bool RepDel { get; set; }

    // Schemes (Sc)
    public bool ScAdd { get; set; }
    public bool ScMod { get; set; }
    public bool ScDel { get; set; }

    // Staff
    public bool StaffAdd { get; set; }
    public bool StaffMod { get; set; }
    public bool StaffDel { get; set; }

    // Orders
    public bool OrdAdd { get; set; }
    public bool OrdMod { get; set; }
    public bool OrdDel { get; set; }

    // Bhav / Rate Cut
    public bool BhavAdd { get; set; }
    public bool BhavMod { get; set; }
    public bool BhavDel { get; set; }

    // Note Transfer
    public bool NoteTransfer { get; set; }
    public bool NoteTransferMod { get; set; }
    public bool NoteTransferDel { get; set; }

    // --- Reports & Registers Permissions ---
    public bool SalesRegister { get; set; }
    public bool ItemWiseSales { get; set; }
    public bool VatRegister { get; set; }
    public bool HidePartyDetails { get; set; }

    public bool PurchaseRegister { get; set; }
    public bool BarcodeStock { get; set; }
    public bool RateChart { get; set; }
    public bool LabelPrinting { get; set; }

    public bool LedgerIndividual { get; set; }
    public bool LedgerGroup { get; set; }
    public bool MetalWiseLedger { get; set; }
    public bool KarigarLedger { get; set; }
    public bool TrialBalance { get; set; }
    public bool OpeningTrial { get; set; }
    public bool Trading { get; set; }
    public bool ProfitLoss { get; set; }
    public bool BalanceSheet { get; set; }
    public bool CashBook { get; set; }
    public bool CashBookOpening { get; set; }

    public bool AuditView { get; set; }
    public bool AuditDel { get; set; }
    public bool Attendance { get; set; }

    public int TotalGrantedEntries =>
        (SaleAdd ? 1 : 0) + (SaleMod ? 1 : 0) + (SaleDel ? 1 : 0) +
        (LedAdd ? 1 : 0) + (LedMod ? 1 : 0) + (LedDel ? 1 : 0) +
        (ItemAdd ? 1 : 0) + (ItemMod ? 1 : 0) + (ItemDel ? 1 : 0) +
        (BarAdd ? 1 : 0) + (BarMod ? 1 : 0) + (BarDel ? 1 : 0) +
        (BcAdd ? 1 : 0) + (BcMod ? 1 : 0) + (BcDel ? 1 : 0) +
        (RetAdd ? 1 : 0) + (RetMod ? 1 : 0) + (RetDel ? 1 : 0) +
        (PurAdd ? 1 : 0) + (PurMod ? 1 : 0) + (PurDel ? 1 : 0) +
        (RecAdd ? 1 : 0) + (RecMod ? 1 : 0) + (RecDel ? 1 : 0) +
        (CusAdd ? 1 : 0) + (CusMod ? 1 : 0) + (CusDel ? 1 : 0) +
        (PayAdd ? 1 : 0) + (PayMod ? 1 : 0) + (PayDel ? 1 : 0) +
        (JouAdd ? 1 : 0) + (JouMod ? 1 : 0) + (JouDel ? 1 : 0) +
        (MIssAdd ? 1 : 0) + (MIssMod ? 1 : 0) + (MIssDel ? 1 : 0) +
        (MRecAdd ? 1 : 0) + (MRecMod ? 1 : 0) + (MRecDel ? 1 : 0) +
        (AIssAdd ? 1 : 0) + (AIssMod ? 1 : 0) + (AIssDel ? 1 : 0) +
        (ARecAdd ? 1 : 0) + (ARecMod ? 1 : 0) + (ARecDel ? 1 : 0) +
        (HIssAdd ? 1 : 0) + (HIssMod ? 1 : 0) + (HIssDel ? 1 : 0) +
        (HRecAdd ? 1 : 0) + (HRecMod ? 1 : 0) + (HRecDel ? 1 : 0) +
        (UserAdd ? 1 : 0) + (UserMod ? 1 : 0) + (UserDel ? 1 : 0) +
        (RepAdd ? 1 : 0) + (RepMod ? 1 : 0) + (RepDel ? 1 : 0) +
        (ScAdd ? 1 : 0) + (ScMod ? 1 : 0) + (ScDel ? 1 : 0) +
        (StaffAdd ? 1 : 0) + (StaffMod ? 1 : 0) + (StaffDel ? 1 : 0) +
        (OrdAdd ? 1 : 0) + (OrdMod ? 1 : 0) + (OrdDel ? 1 : 0) +
        (BhavAdd ? 1 : 0) + (BhavMod ? 1 : 0) + (BhavDel ? 1 : 0) +
        (NoteTransfer ? 1 : 0) + (NoteTransferMod ? 1 : 0) + (NoteTransferDel ? 1 : 0);

    public int TotalGrantedReports =>
        (SalesRegister ? 1 : 0) + (ItemWiseSales ? 1 : 0) + (VatRegister ? 1 : 0) + (HidePartyDetails ? 1 : 0) +
        (PurchaseRegister ? 1 : 0) + (BarcodeStock ? 1 : 0) + (RateChart ? 1 : 0) + (LabelPrinting ? 1 : 0) +
        (LedgerIndividual ? 1 : 0) + (LedgerGroup ? 1 : 0) + (MetalWiseLedger ? 1 : 0) + (KarigarLedger ? 1 : 0) +
        (TrialBalance ? 1 : 0) + (OpeningTrial ? 1 : 0) + (Trading ? 1 : 0) + (ProfitLoss ? 1 : 0) +
        (BalanceSheet ? 1 : 0) + (CashBook ? 1 : 0) + (CashBookOpening ? 1 : 0) +
        (AuditView ? 1 : 0) + (AuditDel ? 1 : 0) + (Attendance ? 1 : 0);

    public int TotalPermissions => TotalGrantedEntries + TotalGrantedReports;
}
