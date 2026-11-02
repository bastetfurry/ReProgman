using Avalonia.Media.Imaging;

namespace ReProgman.Services;

/// <summary>
/// Platform entry point for turning a program item path into its 32x32 icon.
/// Windows reads the shell icon of the shortcut, macOS decodes the .icns of the
/// application bundle, Linux looks the .desktop entry's icon up in the icon theme.
/// A null result leaves the hand-drawn group icon in place.
/// </summary>
public static class IconLoader
{
    public static Bitmap? GetLargeIcon(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return IconExtractor.GetLargeIcon(path);
        }

        if (OperatingSystem.IsMacOS())
        {
            return MacIconLoader.GetLargeIcon(path);
        }

        if (OperatingSystem.IsLinux())
        {
            return LinuxIconLoader.GetLargeIcon(path);
        }

        return null;
    }
}
