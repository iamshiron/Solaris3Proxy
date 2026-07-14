using Shiron.Solaris3Proxy.Models;

namespace Shiron.Solaris3Proxy.Services;

/// <summary>
/// Thread-safe store holding the most recent successful <see cref="CoordinateSnapshot"/> and the
/// frame image it came from. Reads are lock-free; writes atomically swap the stored references.
/// Only the latest is kept — there is no history.
/// </summary>
public interface ICoordinateStore {
    /// <summary>The latest snapshot, or <c>null</c> if nothing has been extracted yet.</summary>
    CoordinateSnapshot? Latest { get; }

    /// <summary>The PNG-encoded frame the latest snapshot was extracted from, or <c>null</c>.</summary>
    byte[]? LatestImage { get; }

    /// <summary>Atomically replaces the stored snapshot and its source image.</summary>
    void Update(CoordinateSnapshot snapshot, byte[] image);
}
