using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Shiron.Solaris3Proxy.Models;
using Shiron.Solaris3Proxy.Options;
using SkiaSharp;
using Tesseract;

namespace Shiron.Solaris3Proxy.Services.Impl;

/// <summary>
/// Extracts the coordinate and user ID by cropping both configured relative regions with Skia,
/// binarizing them, compositing them into a single image, and running one Tesseract OCR pass
/// restricted to the characters both fields use.
/// </summary>
public sealed partial class CoordinateExtractor(
    IOptions<CoordinateExtractionOptions> options,
    ILogger<CoordinateExtractor> logger) : ICoordinateExtractor, IDisposable {
    // Union of the characters used by the coordinate (digits, comma, minus) and the
    // "User ID: <n>" label (letters, colon, space). Keeping one whitelist lets both regions
    // share a single OCR pass.
    private const string CharacterWhitelist = "-,:0123456789 UserID";

    // Vertical gap (px) between the two stacked regions so OCR treats them as separate lines.
    private const int RegionGap = 30;

    private readonly CoordinateExtractionOptions _options = options.Value;

    // Tesseract engines are not thread-safe; a single cached engine guarded by this lock avoids
    // reloading the trained data on every call (the worker runs OCR continuously).
    private readonly Lock _ocrGate = new();
    private TesseractEngine? _engine;

    /// <summary>Matches a coordinate triplet <c>X,Y,Z</c> where each value may be negative.</summary>
    [GeneratedRegex(@"-?\d+,-?\d+,-?\d+", RegexOptions.CultureInvariant)]
    private static partial Regex CoordinateRegex();

    /// <summary>Matches the <c>User ID: &lt;digits&gt;</c> label and captures the numeric id.</summary>
    [GeneratedRegex(@"User\s*ID\s*:?\s*(\d+)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex UserIdRegex();

    /// <inheritdoc/>
    public CoordinateExtractionResult Extract(byte[] imageBytes) {
        try {
            var processed = Preprocess(imageBytes);

            string rawText;
            float confidence;
            lock (_ocrGate) {
                var engine = _engine ??= CreateEngine();
                using var pix = Pix.LoadFromMemory(processed);
                using var page = engine.Process(pix, PageSegMode.SingleBlock);
                rawText = page.GetText().Trim();
                confidence = page.GetMeanConfidence();
            }

            var coordinate = ParseCoordinate(rawText);
            var userId = ParseUserId(rawText);
            if (coordinate is null || userId is null) {
                var error = (coordinate, userId) switch {
                    (null, null) => "No coordinate or user ID found in image.",
                    (null, _) => "No coordinate found in image.",
                    _ => "No user ID found in image.",
                };
                logger.LogDebug("Incomplete extraction from OCR output '{RawText}'.", rawText);
                return new CoordinateExtractionResult(false, coordinate, userId, rawText, confidence, error);
            }

            return new CoordinateExtractionResult(true, coordinate, userId, rawText, confidence, null);
        } catch (Exception ex) {
            logger.LogError(ex, "Coordinate extraction failed.");
            return new CoordinateExtractionResult(false, null, null, string.Empty, 0f, ex.Message);
        }
    }

    private static Coordinate? ParseCoordinate(string text) {
        var match = CoordinateRegex().Match(text);
        if (!match.Success) return null;
        var parts = match.Value.Split(',');
        return new Coordinate(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]));
    }

    private static long? ParseUserId(string text) {
        var match = UserIdRegex().Match(text);
        return match.Success && long.TryParse(match.Groups[1].Value, out var id) ? id : null;
    }

    private TesseractEngine CreateEngine() {
        var engine = new TesseractEngine(_options.TessDataPath, _options.Language, EngineMode.Default);
        engine.SetVariable("tessedit_char_whitelist", CharacterWhitelist);
        return engine;
    }

    /// <inheritdoc/>
    public void Dispose() {
        lock (_ocrGate) {
            _engine?.Dispose();
            _engine = null;
        }
    }

    /// <summary>
    /// Renders both regions (coordinate and user ID) as binarized images and stacks them into a
    /// single PNG so one OCR pass covers both. Returns PNG-encoded bytes.
    /// </summary>
    private byte[] Preprocess(byte[] imageBytes) {
        using var original = SKBitmap.Decode(imageBytes)
            ?? throw new InvalidOperationException("Unsupported or corrupt image.");

        using var coordinateRegion = RenderRegion(original,
            _options.RelativeLeft, _options.RelativeTop, _options.RelativeRight, _options.RelativeBottom);
        using var userIdRegion = RenderRegion(original,
            _options.UserIdRelativeLeft, _options.UserIdRelativeTop, _options.UserIdRelativeRight, _options.UserIdRelativeBottom);

        var width = Math.Max(coordinateRegion.Width, userIdRegion.Width);
        var height = coordinateRegion.Height + RegionGap + userIdRegion.Height;

        using var composite = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(composite)) {
            canvas.Clear(SKColors.White);
            canvas.DrawBitmap(coordinateRegion,
                new SKRect(0, 0, coordinateRegion.Width, coordinateRegion.Height), new SKSamplingOptions(), null);
            var top = coordinateRegion.Height + RegionGap;
            canvas.DrawBitmap(userIdRegion,
                new SKRect(0, top, userIdRegion.Width, top + userIdRegion.Height), new SKSamplingOptions(), null);
        }

        using var image = SKImage.FromBitmap(composite);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>Crops the given relative region, upscales it, and binarizes it (dark text on white).</summary>
    private SKBitmap RenderRegion(SKBitmap original, double relLeft, double relTop, double relRight, double relBottom) {
        var rect = ResolveCropRect(original.Width, original.Height, relLeft, relTop, relRight, relBottom);

        using var cropped = new SKBitmap(rect.Width, rect.Height);
        if (!original.ExtractSubset(cropped, rect))
            throw new InvalidOperationException("Failed to crop the extraction region.");

        var scale = Math.Max(1, _options.UpscaleFactor);
        var width = rect.Width * scale;
        var height = rect.Height * scale;

        using var scaled = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(scaled)) {
            canvas.DrawBitmap(cropped, new SKRect(0, 0, width, height),
                new SKSamplingOptions(SKCubicResampler.Mitchell), null);
        }

        var binary = new SKBitmap(new SKImageInfo(width, height, SKColorType.Gray8, SKAlphaType.Opaque));
        var threshold = _options.LuminanceThreshold;
        for (var y = 0; y < height; y++) {
            for (var x = 0; x < width; x++) {
                var pixel = scaled.GetPixel(x, y);
                // ITU-R BT.601 luma; text is brighter than the background.
                var luma = (byte) ((pixel.Red * 299 + pixel.Green * 587 + pixel.Blue * 114) / 1000);
                var value = (byte) (luma > threshold ? 0 : 255);
                binary.SetPixel(x, y, new SKColor(value, value, value));
            }
        }

        return binary;
    }

    /// <summary>Resolves a pixel crop rectangle from relative bounds, clamped to the image.</summary>
    private static SKRectI ResolveCropRect(int imageWidth, int imageHeight,
        double relLeft, double relTop, double relRight, double relBottom) {
        var left = (int) Math.Round(relLeft * imageWidth);
        var top = (int) Math.Round(relTop * imageHeight);
        var right = (int) Math.Round(relRight * imageWidth);
        var bottom = (int) Math.Round(relBottom * imageHeight);

        left = Math.Clamp(left, 0, imageWidth - 1);
        top = Math.Clamp(top, 0, imageHeight - 1);
        right = Math.Clamp(right, left + 1, imageWidth);
        bottom = Math.Clamp(bottom, top + 1, imageHeight);

        return new SKRectI(left, top, right, bottom);
    }
}
