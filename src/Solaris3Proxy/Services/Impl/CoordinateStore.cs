using Shiron.Solaris3Proxy.Models;

namespace Shiron.Solaris3Proxy.Services.Impl;

/// <summary>
/// Default <see cref="ICoordinateStore"/> backed by volatile references. The snapshot is immutable
/// and the image array is never mutated after storing, so lock-free reads always observe a
/// fully-constructed value.
/// </summary>
public sealed class CoordinateStore : ICoordinateStore {
    private volatile CoordinateSnapshot? _latest;
    private volatile byte[]? _latestImage;

    /// <inheritdoc/>
    public CoordinateSnapshot? Latest => _latest;

    /// <inheritdoc/>
    public byte[]? LatestImage => _latestImage;

    /// <inheritdoc/>
    public void Update(CoordinateSnapshot snapshot, byte[] image) {
        _latest = snapshot;
        _latestImage = image;
    }
}
