using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Data.Access.Repositories;

public enum ReceiptNavigationDirection
{
    First,
    Previous,
    Next,
    Last
}

public interface ICardAdjustmentReceiptRepository
{
    Task<string> GetNextReceiptNoAsync(string? counter = null, CancellationToken cancellationToken = default);
    Task<CardAdjustmentReceipt?> GetByReceiptNoAsync(string receiptNo, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CardAdjustmentReceipt>> GetAllAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CardAdjustmentReceiptItem>> GetPendingCardTransactionsAsync(string cardBank, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetCardBanksAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(CardAdjustmentReceipt receipt, bool updateExisting = false, CancellationToken cancellationToken = default);
    Task DeleteAsync(string receiptNo, CancellationToken cancellationToken = default);
    Task<string?> NavigateReceiptNoAsync(string? currentReceiptNo, ReceiptNavigationDirection direction, CancellationToken cancellationToken = default);
}
