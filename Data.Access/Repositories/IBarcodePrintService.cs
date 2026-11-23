using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Data.Access.Repositories;

public interface IBarcodePrintService
{
    Task PrintBarcodeStickerAsync(BarcodeSubformItem item, string? customStickerFormat = null, CancellationToken cancellationToken = default);

    Task PrintBarcodeStickerAsync(BarcodeSubformItem item, string? customStickerFormat, string? targetPrinter, CancellationToken cancellationToken = default);

    Task PrintBarcodeStickerAsync(BarcodeSubformItem item, string? customStickerFormat, string? targetPrinter, string? printEngine, CancellationToken cancellationToken = default);
}
