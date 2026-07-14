using Microsoft.Extensions.Options;
using Shiron.Solaris3Proxy.Models;
using Shiron.Solaris3Proxy.Options;
using Shiron.Solaris3Proxy.Services.Screen;

namespace Shiron.Solaris3Proxy.Services;

/// <summary>
/// Background service that continuously captures the user's screen and, on a fixed interval,
/// runs the coordinate-extraction pipeline over the latest frame, publishing the result to the
/// <see cref="ICoordinateStore"/> for HTTP endpoints to read.
/// </summary>
public sealed class CoordinateCaptureWorker(
    IScreenCapturer capturer,
    ICoordinateExtractor extractor,
    ICoordinateStore store,
    IOptions<ScreenCaptureOptions> options,
    ILogger<CoordinateCaptureWorker> logger) : BackgroundService {
    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        if (!await TryStartCaptureAsync(stoppingToken)) return;

        var interval = TimeSpan.FromMilliseconds(Math.Max(1, options.Value.IntervalMilliseconds));
        logger.LogInformation("Coordinate capture worker sampling every {Interval}.", interval);

        using var timer = new PeriodicTimer(interval);
        try {
            do {
                if (!capturer.TryGetLatestFrame(out var frame)) continue;

                var result = extractor.Extract(frame.Data);
                store.Update(new CoordinateSnapshot(
                    CapturedAt: frame.CapturedAt,
                    ExtractedAt: DateTime.UtcNow,
                    Success: result.Success,
                    Coordinate: result.Coordinate,
                    Confidence: result.Confidence,
                    RawText: result.RawText));
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        } catch (OperationCanceledException) {
            // Graceful shutdown.
        }
    }

    /// <summary>
    /// Attempts to start capture, retrying a bounded number of times so a restarting/crashed
    /// portal doesn't permanently disable extraction. Returns <c>false</c> if capture is unavailable.
    /// </summary>
    private async Task<bool> TryStartCaptureAsync(CancellationToken stoppingToken) {
        var maxAttempts = Math.Max(1, options.Value.StartMaxAttempts);
        var retryDelay = TimeSpan.FromSeconds(Math.Max(1, options.Value.StartRetryDelaySeconds));

        for (var attempt = 1; ; attempt++) {
            try {
                await capturer.StartAsync(stoppingToken);
                return true;
            } catch (OperationCanceledException) {
                return false;
            } catch (PlatformNotSupportedException ex) {
                logger.LogError(ex, "Screen capture is not supported on this platform; coordinate extraction is disabled.");
                return false;
            } catch (Exception ex) when (attempt < maxAttempts) {
                logger.LogWarning(ex, "Screen capture start failed (attempt {Attempt}/{Max}); retrying in {Delay}.",
                    attempt, maxAttempts, retryDelay);
                try { await Task.Delay(retryDelay, stoppingToken); } catch (OperationCanceledException) { return false; }
            } catch (Exception ex) {
                logger.LogError(ex, "Screen capture could not be started after {Max} attempts; coordinate extraction is disabled.", maxAttempts);
                return false;
            }
        }
    }
}
