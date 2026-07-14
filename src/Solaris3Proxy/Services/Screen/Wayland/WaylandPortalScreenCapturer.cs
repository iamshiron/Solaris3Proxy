using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Options;
using Shiron.Solaris3Proxy.Options;

namespace Shiron.Solaris3Proxy.Services.Screen.Wayland;

/// <summary>
/// Linux/Wayland <see cref="IScreenCapturer"/> that captures via the xdg-desktop-portal ScreenCast
/// interface (D-Bus) and a PipeWire/GStreamer frame pipeline. This never hooks into applications —
/// the compositor produces the frames after the user grants consent.
/// </summary>
public sealed class WaylandPortalScreenCapturer(
    IOptions<ScreenCaptureOptions> options,
    ILoggerFactory loggerFactory,
    ILogger<WaylandPortalScreenCapturer> logger) : IScreenCapturer {
    private readonly ScreenCaptureOptions _options = options.Value;
    private ScreenCastPortal? _portal;
    private PipeWireFrameSource? _frameSource;

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken) {
        // Idempotent: tear down any prior (e.g. failed) attempt so retries don't leak resources.
        if (_frameSource is not null) { await _frameSource.DisposeAsync(); _frameSource = null; }
        if (_portal is not null) { await _portal.DisposeAsync(); _portal = null; }

        var frameDirectory = _options.FrameDirectory
            ?? Path.Combine(Path.GetTempPath(), "solaris3proxy-frames");
        var restoreTokenPath = _options.RestoreTokenPath
            ?? Path.Combine(frameDirectory, "restore-token");

        Directory.CreateDirectory(frameDirectory);
        var restoreToken = ReadRestoreToken(restoreTokenPath);

        _portal = new ScreenCastPortal(loggerFactory.CreateLogger<ScreenCastPortal>());
        var stream = await _portal.StartAsync(_options, restoreToken, cancellationToken);
        if (stream.RestoreToken is { Length: > 0 } token) WriteRestoreToken(restoreTokenPath, token);

        _frameSource = new PipeWireFrameSource(loggerFactory.CreateLogger<PipeWireFrameSource>());
        _frameSource.Start(stream.NodeId, _options.FrameRate, frameDirectory);
    }

    /// <inheritdoc/>
    public bool TryGetLatestFrame([NotNullWhen(true)] out CapturedFrame? frame) {
        frame = _frameSource?.TryReadLatest();
        return frame is not null;
    }

    private string? ReadRestoreToken(string path) {
        try {
            return File.Exists(path) ? File.ReadAllText(path).Trim() is { Length: > 0 } t ? t : null : null;
        } catch (IOException ex) {
            logger.LogDebug(ex, "Could not read restore token from {Path}.", path);
            return null;
        }
    }

    private void WriteRestoreToken(string path, string token) {
        try {
            File.WriteAllText(path, token);
        } catch (IOException ex) {
            logger.LogDebug(ex, "Could not persist restore token to {Path}.", path);
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() {
        if (_frameSource is not null) await _frameSource.DisposeAsync();
        if (_portal is not null) await _portal.DisposeAsync();
    }
}
