using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Data.Access.Repositories;

public interface IBarcodeCreationRepository
{
    Task<IReadOnlyList<BarcodeSubformItem>> GetOpeningBarcodesAsync(int count = 100, CancellationToken cancellationToken = default);

    Task<BarcodeSubformItem?> GetBarcodeByCodeAsync(string barcode, CancellationToken cancellationToken = default);

    Task<string> GetNextBarcodeNumberAsync(CancellationToken cancellationToken = default);

    Task<int> GetNextBarcodeAutoNoAsync(CancellationToken cancellationToken = default);

    Task SaveSingleOpeningBarcodeAsync(BarcodeSubformItem item, bool isUpdate = false, CancellationToken cancellationToken = default);

    Task DeleteBarcodeAsync(string barcode, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BarcodeCreationHeader>> GetRecentVouchersAsync(int count = 50, CancellationToken cancellationToken = default);

    Task<BarcodeCreationHeader?> GetVoucherByVouNoAsync(string vouNo, CancellationToken cancellationToken = default);

    Task<int> GetNextVoucherNosAsync(CancellationToken cancellationToken = default);

    Task SaveBulkVoucherAsync(BarcodeCreationHeader voucher, bool updateExisting = false, CancellationToken cancellationToken = default);

    Task DeleteVoucherAsync(string vouNo, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetStickerFormatsAsync(CancellationToken cancellationToken = default);

    Task UpdateBarcodePhotoAsync(string barcode, string photoPath, CancellationToken cancellationToken = default);

    Task UpdateBarcodeAdvancedAsync(string barcode, decimal? mrp, string? size, bool reprintSticker, CancellationToken cancellationToken = default);

    Task UpdateBarcodeAdvancedAsync(string barcode, decimal? mrp, string? size, bool reprintSticker, string? stickerFormat, CancellationToken cancellationToken = default);

    Task UpdateBarcodeAdvancedAsync(string barcode, decimal? mrp, string? size, bool reprintSticker, string? stickerFormat, string? bType, decimal? making, CancellationToken cancellationToken = default);
}
