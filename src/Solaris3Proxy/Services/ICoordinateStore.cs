using Shiron.Solaris3Proxy.Models;

namespace Shiron.Solaris3Proxy.Services;

/// <summary>
/// Thread-safe store holding the most recent <see cref="CoordinateSnapshot"/> produced by the
/// capture worker. Reads are lock-free; writes atomically swap the stored reference.
/// </summary>
public interface ICoordinateStore {
    /// <summary>The latest snapshot, or <c>null</c> if nothing has been extracted yet.</summary>
    CoordinateSnapshot? Latest { get; }

    /// <summary>Atomically replaces the stored snapshot with <paramref name="snapshot"/>.</summary>
    void Update(CoordinateSnapshot snapshot);
}
