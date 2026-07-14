using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Shiron.Solaris3Proxy.Models;
using Shiron.Solaris3Proxy.Options;
using SkiaSharp;
using Tesseract;

namespace Shiron.Solaris3Proxy.Services.Impl;

/// <summary>
/// Extracts coordinates by cropping the configured relative region with Skia, binarizing it,
/// and running Tesseract OCR restricted to the coordinate character set.
/// </summary>
public sealed partial class CoordinateExtractor(
    IOptions<CoordinateExtractionOptions> options,
    ILogger<CoordinateExtractor> logger) : ICoordinateExtractor, IDisposable {
    private const string CharacterWhitelist = "-0123456789,";

    private readonly CoordinateExtractionOptions _options = options.Value;

    // Tesseract engines are not thread-safe; a single cached engine guarded by this lock avoids
    // reloading the trained data on every call (the worker runs OCR continuously).
    private readonly Lock _ocrGate = new();
    private TesseractEngine? _engine;

    /// <summary>Matches a coordinate triplet <c>X,Y,Z</c> where each value may be negative.</summary>
    [GeneratedRegex(@"-?\d+,-?\d+,-?\d+", RegexOptions.CultureInvariant)]
    private static partial Regex CoordinateRegex();

    /// <inheritdoc/>
    public CoordinateExtractionResult Extract(byte[] imageBytes) {
        try {
            var processed = Preprocess(imageBytes);

            string rawText;
            float confidence;
            lock (_ocrGate) {
                var engine = _engine ??= CreateEngine();
                using var pix = Pix.LoadFromMemory(processed);
                using var page = engine.Process(pix, PageSegMode.SingleLine);
                rawText = page.GetText().Trim();
                confidence = page.GetMeanConfidence();
            }

            var match = CoordinateRegex().Match(rawText);
            if (!match.Success) {
                logger.LogDebug("No coordinate found in OCR output '{RawText}'.", rawText);
                return new CoordinateExtractionResult(false, null, rawText, confidence, "No coordinate found in image.");
            }

            var parts = match.Value.Split(',');
            var coordinate = new Coordinate(
                int.Parse(parts[0]),
                int.Parse(parts[1]),
                int.Parse(parts[2]));

            return new CoordinateExtractionResult(true, coordinate, rawText, confidence, null);
        } catch (Exception ex) {
            logger.LogError(ex, "Coordinate extraction failed.");
            return new CoordinateExtractionResult(false, null, string.Empty, 0f, ex.Message);
        }
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
    /// Crops the configured relative region, upscales it, and binarizes it (dark text on white)
    /// so the semi-transparent overlay text is legible to Tesseract. Returns PNG-encoded bytes.
    /// </summary>
    private byte[] Preprocess(byte[] imageBytes) {
        using var original = SKBitmap.Decode(imageBytes)
            ?? throw new InvalidOperationException("Unsupported or corrupt image.");

        var rect = ResolveCropRect(original.Width, original.Height);

        using var cropped = new SKBitmap(rect.Width, rect.Height);
        if (!original.ExtractSubset(cropped, rect))
            throw new InvalidOperationException("Failed to crop the coordinate region.");

        var scale = Math.Max(1, _options.UpscaleFactor);
        var width = rect.Width * scale;
        var height = rect.Height * scale;

        using var scaled = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(scaled)) {
            canvas.DrawBitmap(cropped, new SKRect(0, 0, width, height),
                new SKSamplingOptions(SKCubicResampler.Mitchell), null);
        }

        using var binary = new SKBitmap(new SKImageInfo(width, height, SKColorType.Gray8, SKAlphaType.Opaque));
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

        using var image = SKImage.FromBitmap(binary);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>Resolves the pixel crop rectangle from the relative bounds, clamped to the image.</summary>
    private SKRectI ResolveCropRect(int imageWidth, int imageHeight) {
        var left = (int) Math.Round(_options.RelativeLeft * imageWidth);
        var top = (int) Math.Round(_options.RelativeTop * imageHeight);
        var right = (int) Math.Round(_options.RelativeRight * imageWidth);
        var bottom = (int) Math.Round(_options.RelativeBottom * imageHeight);

        left = Math.Clamp(left, 0, imageWidth - 1);
        top = Math.Clamp(top, 0, imageHeight - 1);
        right = Math.Clamp(right, left + 1, imageWidth);
        bottom = Math.Clamp(bottom, top + 1, imageHeight);

        return new SKRectI(left, top, right, bottom);
    }
}
