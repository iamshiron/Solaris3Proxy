namespace Shiron.Solaris3Proxy.Models;

/// <summary>
/// A parsed coordinate triplet in the <c>X,Y,Z</c> format. Each component may be negative.
/// </summary>
/// <param name="X">The X component.</param>
/// <param name="Y">The Y component.</param>
/// <param name="Z">The Z component.</param>
public sealed record Coordinate(int X, int Y, int Z);

/// <summary>
/// Result of attempting to extract a coordinate from an uploaded image.
/// </summary>
/// <param name="Success">Whether a coordinate was successfully parsed.</param>
/// <param name="Coordinate">The extracted coordinate, or <c>null</c> when none was found.</param>
/// <param name="RawText">The raw OCR text the coordinate was parsed from (for diagnostics).</param>
/// <param name="Confidence">Mean OCR confidence in the range [0, 1].</param>
/// <param name="Error">A human-readable error when <paramref name="Success"/> is <c>false</c>.</param>
public sealed record CoordinateExtractionResult(
    bool Success,
    Coordinate? Coordinate,
    string RawText,
    float Confidence,
    string? Error);
