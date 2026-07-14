using Shiron.Solaris3Proxy.Models;

namespace Shiron.Solaris3Proxy.Services.Impl;

/// <summary>
/// Default <see cref="ICoordinateStore"/> backed by a single volatile reference.
/// The <see cref="CoordinateSnapshot"/> is immutable, so lock-free reads always observe a
/// fully-constructed value.
/// </summary>
public sealed class CoordinateStore : ICoordinateStore {
    private volatile CoordinateSnapshot? _latest;

    /// <inheritdoc/>
    public CoordinateSnapshot? Latest => _latest;

    /// <inheritdoc/>
    public void Update(CoordinateSnapshot snapshot) => _latest = snapshot;
}
