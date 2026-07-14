using Shiron.Solaris3Proxy.Models;

namespace Shiron.Solaris3Proxy.Services;

/// <summary>
/// Extracts a coordinate triplet from an image using OCR.
/// </summary>
public interface ICoordinateExtractor {
    /// <summary>
    /// Preprocesses <paramref name="imageBytes"/>, runs OCR over the configured relative region,
    /// and parses the first <c>X,Y,Z</c> coordinate found.
    /// </summary>
    /// <param name="imageBytes">The raw encoded image (PNG, JPEG, …).</param>
    /// <returns>The extraction result; never <c>null</c>.</returns>
    CoordinateExtractionResult Extract(byte[] imageBytes);
}
