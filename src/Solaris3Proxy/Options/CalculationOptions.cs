namespace Shiron.Solaris3Proxy.Options;

/// <summary>
/// Configuration for the background calculation worker.
/// </summary>
public sealed class CalculationOptions {
    /// <summary>Configuration section name to bind from.</summary>
    public const string SectionName = "Calculation";

    /// <summary>Interval between calculation cycles, in milliseconds.</summary>
    public int IntervalMilliseconds { get; set; } = 1000;
}
