namespace Shiron.Solaris3Proxy.Options;

/// <summary>
/// Configuration for OCR-based coordinate extraction.
/// The crop region is expressed in <em>relative</em> coordinates (fractions of width/height)
/// so it works across resolutions (1080p, 1440p, and beyond).
/// </summary>
/// <remarks>
/// Defaults are derived from the reference 1440p (2560x1440) screenshot, where the
/// coordinate text occupies the pixel box x:[38,257], y:[1401,1437] in the bottom-left.
/// </remarks>
public sealed class CoordinateExtractionOptions {
    /// <summary>Configuration section name to bind from.</summary>
    public const string SectionName = "CoordinateExtraction";

    /// <summary>Left edge of the crop region as a fraction of image width (38 / 2560).</summary>
    public double RelativeLeft { get; set; } = 0.0148437500;

    /// <summary>Top edge of the crop region as a fraction of image height (1401 / 1440).</summary>
    public double RelativeTop { get; set; } = 0.9729166667;

    /// <summary>Right edge of the crop region as a fraction of image width (257 / 2560).</summary>
    public double RelativeRight { get; set; } = 0.1003906250;

    /// <summary>Bottom edge of the crop region as a fraction of image height (1437 / 1440).</summary>
    public double RelativeBottom { get; set; } = 0.9979166667;

    /// <summary>
    /// Luminance cutoff [0, 255]. Pixels brighter than this become text (black); the text is a
    /// slightly-transparent light color over a darker background, so a mid-low cutoff isolates it.
    /// </summary>
    public byte LuminanceThreshold { get; set; } = 62;

    /// <summary>Integer factor by which the cropped region is upscaled before OCR to aid recognition.</summary>
    public int UpscaleFactor { get; set; } = 4;

    /// <summary>Path to the directory containing the Tesseract <c>*.traineddata</c> files.</summary>
    public string TessDataPath { get; set; } = "/usr/share/tessdata";

    /// <summary>Tesseract language to load.</summary>
    public string Language { get; set; } = "eng";

    /// <summary>Maximum accepted upload size in bytes.</summary>
    public long MaxUploadBytes { get; set; } = 20 * 1024 * 1024;
}
