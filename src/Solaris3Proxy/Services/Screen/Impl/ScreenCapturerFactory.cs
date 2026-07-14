using Microsoft.Extensions.DependencyInjection;
using Shiron.Solaris3Proxy.Services.Screen.Wayland;

namespace Shiron.Solaris3Proxy.Services.Screen.Impl;

/// <summary>
/// Selects the screen capturer for the current platform. Only Linux/Wayland (via the
/// xdg-desktop-portal ScreenCast interface) is implemented today; X11 and Windows are intended
/// to slot in here as additional branches without touching callers.
/// </summary>
public sealed class ScreenCapturerFactory(IServiceProvider services) : IScreenCapturerFactory {
    /// <inheritdoc/>
    public IScreenCapturer Create() {
        if (OperatingSystem.IsLinux() && IsWayland())
            return ActivatorUtilities.CreateInstance<WaylandPortalScreenCapturer>(services);

        // Planned: X11 (XShmGetImage) and Windows (Desktop Duplication) capturers.
        throw new PlatformNotSupportedException(
            "Screen capture currently supports only Linux/Wayland via xdg-desktop-portal. " +
            "X11 and Windows support are planned.");
    }

    private static bool IsWayland() =>
        string.Equals(Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"), "wayland", StringComparison.OrdinalIgnoreCase)
        || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));
}
