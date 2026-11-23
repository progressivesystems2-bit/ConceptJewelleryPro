using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ConceptJewelleryPro.Application.Services;

public interface IAiImageEnhancementService
{
    /// <summary>
    /// Enhances a product image with background neutralization, lighting, and shadow.
    /// </summary>
    Task<AiImageEnhanceResult> EnhanceImageAsync(
        string sourcePath,
        string targetPath,
        AiImageEnhanceOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes an in-memory image stream or file to byte array for real-time preview.
    /// </summary>
    Task<byte[]> GeneratePreviewBytesAsync(
        string sourcePath,
        AiImageEnhanceOptions options,
        int maxPreviewDimension = 600,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves current default/saved options.
    /// </summary>
    AiImageEnhanceOptions GetDefaultOptions();

    /// <summary>
    /// Persists options as the new default configuration.
    /// </summary>
    void SaveDefaultOptions(AiImageEnhanceOptions options);
}
