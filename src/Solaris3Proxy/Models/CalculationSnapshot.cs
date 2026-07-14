namespace Shiron.Solaris3Proxy.Models;

/// <summary>
/// An immutable snapshot of a single calculation cycle produced by the background worker.
/// </summary>
/// <param name="Sequence">Monotonically increasing index of the calculation cycle.</param>
/// <param name="ComputedAt">UTC timestamp at which the value was produced.</param>
/// <param name="Value">The value computed during this cycle.</param>
/// <param name="MovingAverage">Running average of all values produced so far.</param>
public sealed record CalculationSnapshot(
    long Sequence,
    DateTime ComputedAt,
    double Value,
    double MovingAverage);
