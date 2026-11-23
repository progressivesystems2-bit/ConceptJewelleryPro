using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Authentication;

public sealed class DemoUserPermissionRepository : IUserPermissionRepository
{
    private readonly List<UserPermission> _users;

    public DemoUserPermissionRepository()
    {
        _users = new List<UserPermission>
        {
            new()
            {
                User = "admin",
                Password = "123",
                Toolbar = "DP1",
                SaleAdd = true, SaleMod = true, SaleDel = true,
                LedAdd = true, LedMod = true, LedDel = true,
                ItemAdd = true, ItemMod = true, ItemDel = true,
                BarAdd = true, BarMod = true, BarDel = true,
                BcAdd = true, BcMod = true, BcDel = true,
                RetAdd = true, RetMod = true, RetDel = true,
                PurAdd = true, PurMod = true, PurDel = true,
                RecAdd = true, RecMod = true, RecDel = true,
                CusAdd = true, CusMod = true, CusDel = true,
                PayAdd = true, PayMod = true, PayDel = true,
                JouAdd = true, JouMod = true, JouDel = true,
                MIssAdd = true, MIssMod = true, MIssDel = true,
                MRecAdd = true, MRecMod = true, MRecDel = true,
                AIssAdd = true, AIssMod = true, AIssDel = true,
                ARecAdd = true, ARecMod = true, ARecDel = true,
                HIssAdd = true, HIssMod = true, HIssDel = true,
                HRecAdd = true, HRecMod = true, HRecDel = true,
                UserAdd = true, UserMod = true, UserDel = true,
                RepAdd = true, RepMod = true, RepDel = true,
                ScAdd = true, ScMod = true, ScDel = true,
                StaffAdd = true, StaffMod = true, StaffDel = true,
                OrdAdd = true, OrdMod = true, OrdDel = true,
                BhavAdd = true, BhavMod = true, BhavDel = true,
                NoteTransfer = true, NoteTransferMod = true, NoteTransferDel = true,
                SalesRegister = true, ItemWiseSales = true, VatRegister = true,
                PurchaseRegister = true, BarcodeStock = true, RateChart = true, LabelPrinting = true,
                LedgerIndividual = true, LedgerGroup = true, MetalWiseLedger = true, KarigarLedger = true,
                TrialBalance = true, OpeningTrial = true, Trading = true, ProfitLoss = true, BalanceSheet = true,
                CashBook = true, CashBookOpening = true, AuditView = true, AuditDel = true, Attendance = true
            },
            new()
            {
                User = "operator",
                Password = "123",
                Toolbar = "DP1",
                SaleAdd = true, SaleMod = false, SaleDel = false,
                RecAdd = true, RecMod = false, RecDel = false,
                CusAdd = true, CusMod = false, CusDel = false,
                SalesRegister = true, RateChart = true, CashBook = true
            }
        };
    }

    public Task<IReadOnlyList<UserPermission>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<UserPermission> list = _users.OrderBy(u => u.User).ToList();
        return Task.FromResult(list);
    }

    public Task<UserPermission?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        var user = _users.FirstOrDefault(u => string.Equals(u.User, username, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(user);
    }

    public Task AddAsync(UserPermission userPermission, CancellationToken cancellationToken = default)
    {
        _users.Add(userPermission);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(string originalUsername, UserPermission userPermission, CancellationToken cancellationToken = default)
    {
        var idx = _users.FindIndex(u => string.Equals(u.User, originalUsername, StringComparison.OrdinalIgnoreCase));
        if (idx >= 0)
        {
            _users[idx] = userPermission;
        }
        else
        {
            _users.Add(userPermission);
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(string username, CancellationToken cancellationToken = default)
    {
        _users.RemoveAll(u => string.Equals(u.User, username, StringComparison.OrdinalIgnoreCase));
        return Task.CompletedTask;
    }
}
