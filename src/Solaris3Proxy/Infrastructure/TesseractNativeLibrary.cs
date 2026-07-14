namespace Shiron.Solaris3Proxy.Infrastructure;

/// <summary>
/// Bridges the versioned native library names the Tesseract wrapper expects
/// (e.g. <c>libleptonica-1.82.0.so</c>) to the sonames installed on the host
/// (e.g. <c>libleptonica.so.6</c>) by creating symlinks in an <c>x64/</c> folder
/// next to the executable — the location the wrapper's loader searches.
/// </summary>
/// <remarks>
/// This keeps the app self-contained: it relies only on a system-installed Tesseract
/// (libtesseract / libleptonica) and does not require modifying system library paths.
/// No-op on non-Linux platforms, where the wrapper ships its own native binaries.
/// </remarks>
public static class TesseractNativeLibrary {
    private static readonly string[] SearchDirectories =
    [
        "/usr/lib",
        "/usr/lib64",
        "/lib",
        "/usr/local/lib",
        "/usr/lib/x86_64-linux-gnu",
        "/lib/x86_64-linux-gnu",
    ];

    /// <summary>
    /// Ensures the native library symlinks required by the Tesseract wrapper exist.
    /// </summary>
    /// <param name="logger">Logger for diagnostics.</param>
    public static void EnsureAvailable(ILogger logger) {
        if (!OperatingSystem.IsLinux()) return;

        var targetDir = Path.Combine(AppContext.BaseDirectory, "x64");
        Directory.CreateDirectory(targetDir);

        Link(targetDir, "libleptonica-1.82.0.so",
            ["libleptonica.so.6", "libleptonica.so.5", "libleptonica.so"], logger);
        Link(targetDir, "libtesseract50.so",
            ["libtesseract.so.5", "libtesseract.so"], logger);
    }

    private static void Link(string targetDir, string linkName, string[] candidates, ILogger logger) {
        var linkPath = Path.Combine(targetDir, linkName);

        // A resolvable symlink already exists — nothing to do.
        if (File.Exists(linkPath)) return;

        // Remove a stale/broken link before recreating it.
        if (Path.Exists(linkPath)) File.Delete(linkPath);

        var target = candidates
            .SelectMany(name => SearchDirectories.Select(dir => Path.Combine(dir, name)))
            .FirstOrDefault(File.Exists);

        if (target is null) {
            logger.LogWarning(
                "No system native library found for {LinkName}; install a Tesseract package. OCR will fail until then.",
                linkName);
            return;
        }

        File.CreateSymbolicLink(linkPath, target);
        logger.LogInformation("Linked native library {LinkName} -> {Target}.", linkName, target);
    }
}
