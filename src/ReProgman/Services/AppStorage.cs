using ReProgman.Model;

namespace ReProgman.Services;

/// <summary>
/// The folder that holds ReProgman.ini and Groups.ini. Both stores share it so the
/// settings and the catalog never drift apart.
/// </summary>
public static class AppStorage
{
    public static string Directory { get; } = Resolve();

    private static string Resolve()
    {
        var portable = StorageLocation.GetPortableDirectory(AppContext.BaseDirectory);
        // Windows keeps the original portable-only behavior: an unwritable install
        // folder silently loses settings. macOS needs a fallback because App
        // Translocation runs the bundle from a read-only image, and Linux because
        // packages install into root-owned directories such as /opt or /usr/lib.
        if (OperatingSystem.IsWindows())
        {
            return portable;
        }

        var fallback = OperatingSystem.IsMacOS()
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library",
                "Application Support",
                "ReProgman")
            : Path.Combine(XdgDirectories.FromEnvironment().ConfigHome, "ReProgman");

        var resolved = StorageLocation.Resolve(portable, fallback, StorageLocation.IsWritable);
        if (!string.Equals(resolved, portable, StringComparison.Ordinal))
        {
            try
            {
                System.IO.Directory.CreateDirectory(resolved);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Nothing left to fall back to; the stores then fail their writes
                // quietly, exactly like a read-only install folder on Windows.
            }
        }

        return resolved;
    }
}
