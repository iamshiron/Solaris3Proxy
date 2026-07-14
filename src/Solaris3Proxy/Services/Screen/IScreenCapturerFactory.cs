namespace Shiron.Solaris3Proxy.Services.Screen;

/// <summary>
/// Creates the <see cref="IScreenCapturer"/> appropriate for the current platform / display server.
/// </summary>
public interface IScreenCapturerFactory {
    /// <summary>Creates a capturer for the current environment.</summary>
    /// <exception cref="PlatformNotSupportedException">Thrown when no capturer supports the environment.</exception>
    IScreenCapturer Create();
}
