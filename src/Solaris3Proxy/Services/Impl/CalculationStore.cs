using Shiron.Solaris3Proxy.Models;

namespace Shiron.Solaris3Proxy.Services.Impl;

/// <summary>
/// Default <see cref="ICalculationStore"/> backed by a single volatile reference.
/// The <see cref="CalculationSnapshot"/> is immutable, so lock-free reads always
/// observe a fully-constructed value.
/// </summary>
public sealed class CalculationStore : ICalculationStore {
    private volatile CalculationSnapshot? _latest;

    /// <inheritdoc/>
    public CalculationSnapshot? Latest => _latest;

    /// <inheritdoc/>
    public void Update(CalculationSnapshot snapshot) => _latest = snapshot;
}
