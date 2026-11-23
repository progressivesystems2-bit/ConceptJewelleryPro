using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Application.Abstractions;

public sealed class BankStatementParseResult
{
    public string BankName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal ClosingBalance { get; set; }
    public List<BankStatementTransaction> Transactions { get; set; } = [];
}

public interface IBankStatementParserService
{
    Task<BankStatementParseResult> ParseStatementAsync(string filePath, CancellationToken cancellationToken = default);
}
