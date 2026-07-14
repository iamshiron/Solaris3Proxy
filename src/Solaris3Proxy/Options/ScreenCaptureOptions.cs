namespace Shiron.Solaris3Proxy.Options;

/// <summary>
/// Configuration for continuous screen capture and periodic coordinate extraction.
/// </summary>
public sealed class ScreenCaptureOptions {
    /// <summary>Configuration section name to bind from.</summary>
    public const string SectionName = "ScreenCapture";

    /// <summary>Interval between coordinate-extraction samples, in milliseconds.</summary>
    public int IntervalMilliseconds { get; set; } = 500;

    /// <summary>Frames per second the capture pipeline produces (kept low; only the latest frame is used).</summary>
    public int FrameRate { get; set; } = 5;

    /// <summary>
    /// Cursor mode passed to the ScreenCast portal: 1 = hidden, 2 = embedded, 4 = metadata.
    /// Hidden keeps the coordinate overlay free of cursor artifacts.
    /// </summary>
    public uint CursorMode { get; set; } = 1;

    /// <summary>
    /// Directory where the capture pipeline writes frames and the reader picks up the latest one.
    /// Defaults to a per-run subdirectory under the system temp path when unset.
    /// </summary>
    public string? FrameDirectory { get; set; }

    /// <summary>
    /// File storing the portal restore token so subsequent runs re-use the granted stream
    /// without prompting. Defaults to a file next to the frame directory when unset.
    /// </summary>
    public string? RestoreTokenPath { get; set; }

    /// <summary>Seconds to wait for the user to approve the portal consent dialog.</summary>
    public int ConsentTimeoutSeconds { get; set; } = 120;

    /// <summary>How many times to attempt starting capture before disabling it (e.g. if the portal is restarting).</summary>
    public int StartMaxAttempts { get; set; } = 3;

    /// <summary>Delay between capture start attempts, in seconds.</summary>
    public int StartRetryDelaySeconds { get; set; } = 5;
}
