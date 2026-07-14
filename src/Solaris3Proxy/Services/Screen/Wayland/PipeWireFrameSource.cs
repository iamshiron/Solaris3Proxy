using System.Diagnostics;

namespace Shiron.Solaris3Proxy.Services.Screen.Wayland;

/// <summary>
/// Consumes a PipeWire screen-cast node with a GStreamer <c>pipewiresrc</c> pipeline that writes
/// PNG frames into a directory, and exposes the newest fully-written frame. Decoupling capture
/// (continuous, GStreamer-driven) from sampling (the worker's interval) via a frame directory keeps
/// this independently testable — dropping a PNG into the directory feeds the pipeline just like a
/// real capture would.
/// </summary>
public sealed class PipeWireFrameSource(ILogger<PipeWireFrameSource> logger) : IAsyncDisposable {
    // PNG streams always end with the IEND chunk; used to detect fully-written files.
    private static readonly byte[] PngTrailer = [0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82];

    private string _frameDirectory = string.Empty;
    private Process? _process;

    /// <summary>The directory frames are read from (and written to by the pipeline).</summary>
    public string FrameDirectory => _frameDirectory;

    /// <summary>Starts the GStreamer pipeline consuming <paramref name="nodeId"/> at <paramref name="frameRate"/> fps.</summary>
    public void Start(uint nodeId, int frameRate, string frameDirectory) {
        _frameDirectory = frameDirectory;
        Directory.CreateDirectory(_frameDirectory);
        foreach (var stale in Directory.EnumerateFiles(_frameDirectory, "f*.png")) TryDelete(stale);

        var fps = Math.Max(1, frameRate);
        var arguments =
            $"-q pipewiresrc path={nodeId} ! videorate ! video/x-raw,framerate={fps}/1 " +
            $"! videoconvert ! pngenc ! multifilesink location={Path.Combine(_frameDirectory, "f%05d.png")} " +
            "max-files=6 post-messages=false";

        var startInfo = new ProcessStartInfo("gst-launch-1.0", arguments) {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };

        _process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start gst-launch-1.0.");
        _process.EnableRaisingEvents = true;
        _process.ErrorDataReceived += (_, e) => { if (e.Data is { Length: > 0 }) logger.LogDebug("gst: {Line}", e.Data); };
        _process.Exited += (_, _) => {
            if (_process is { ExitCode: not 0 } p)
                logger.LogWarning("Capture pipeline (gst-launch-1.0) exited unexpectedly with code {Code}.", p.ExitCode);
        };
        _process.BeginErrorReadLine();

        logger.LogInformation("Capture pipeline started: gst-launch-1.0 {Args}", arguments);
    }

    /// <summary>Returns the newest fully-written PNG frame, or <c>null</c> when none is available yet.</summary>
    public CapturedFrame? TryReadLatest() {
        if (string.IsNullOrEmpty(_frameDirectory)) return null;

        // Highest index first; skip any file still being written (missing PNG trailer).
        var files = Directory.EnumerateFiles(_frameDirectory, "f*.png")
            .OrderByDescending(static path => path, StringComparer.Ordinal);

        foreach (var file in files) {
            byte[] bytes;
            try {
                bytes = File.ReadAllBytes(file);
            } catch (IOException) {
                continue; // being written/rotated — try the next candidate
            }

            if (IsComplete(bytes))
                return new CapturedFrame(bytes, File.GetLastWriteTimeUtc(file));
        }

        return null;
    }

    private static bool IsComplete(byte[] png) =>
        png.Length > PngTrailer.Length && png.AsSpan(png.Length - PngTrailer.Length).SequenceEqual(PngTrailer);

    private static void TryDelete(string path) {
        try { File.Delete(path); } catch (IOException) { /* ignore */ }
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() {
        if (_process is { } process) {
            try {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
            } catch (Exception ex) {
                logger.LogDebug(ex, "Failed to stop capture pipeline cleanly.");
            }
            process.Dispose();
        }

        return ValueTask.CompletedTask;
    }
}
