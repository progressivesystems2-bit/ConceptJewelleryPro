using ConceptJewelleryPro.API.Models;

namespace ConceptJewelleryPro.API.Services;

public interface IInvoiceParsingService
{
    Task<InvoiceResult> ParseInvoiceAsync(string imagePath, CancellationToken cancellationToken = default);
}
