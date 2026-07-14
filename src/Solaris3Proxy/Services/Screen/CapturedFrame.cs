namespace Shiron.Solaris3Proxy.Services.Screen;

/// <summary>
/// A single captured screen frame as encoded image bytes (PNG).
/// </summary>
/// <param name="Data">The encoded image bytes.</param>
/// <param name="CapturedAt">UTC time the frame was produced.</param>
public sealed record CapturedFrame(byte[] Data, DateTime CapturedAt);
