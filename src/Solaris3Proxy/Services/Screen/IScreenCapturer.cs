using System.Diagnostics.CodeAnalysis;

namespace Shiron.Solaris3Proxy.Services.Screen;

/// <summary>
/// Captures the user's screen at the OS/compositor level (never by hooking applications) and
/// exposes the most recent frame. Platform-specific implementations exist per display server;
/// see <see cref="IScreenCapturerFactory"/>.
/// </summary>
public interface IScreenCapturer : IAsyncDisposable {
    /// <summary>
    /// Starts the capture session (acquiring any user consent required by the platform) and
    /// begins producing frames. Must be called once before <see cref="TryGetLatestFrame"/>.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel start-up.</param>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Returns the most recent fully-captured frame, if one is available yet.
    /// </summary>
    /// <param name="frame">The latest frame when the method returns <c>true</c>.</param>
    /// <returns><c>true</c> if a frame was available; otherwise <c>false</c>.</returns>
    bool TryGetLatestFrame([NotNullWhen(true)] out CapturedFrame? frame);
}
