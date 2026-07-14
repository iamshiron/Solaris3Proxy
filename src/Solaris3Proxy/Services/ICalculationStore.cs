using Shiron.Solaris3Proxy.Models;

namespace Shiron.Solaris3Proxy.Services;

/// <summary>
/// Thread-safe store holding the most recent <see cref="CalculationSnapshot"/>.
/// Reads are lock-free; writes atomically swap the stored reference.
/// </summary>
public interface ICalculationStore {
    /// <summary>The latest snapshot, or <c>null</c> if no calculation has run yet.</summary>
    CalculationSnapshot? Latest { get; }

    /// <summary>Atomically replaces the stored snapshot with <paramref name="snapshot"/>.</summary>
    void Update(CalculationSnapshot snapshot);
}
