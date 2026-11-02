using System.Diagnostics;
using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Media.Imaging;
using ReProgman.Model;

namespace ReProgman.Services;

/// <summary>
/// Reads the 32x32 icon of a .desktop entry from the user's icon theme.
/// </summary>
/// <remarks>
/// Many current themes (Papirus, Breeze, Adwaita) ship SVG only. Why not an SVG
/// library: the managed ones build on SVG.NET, which is MS-PL licensed and not
/// NativeAOT friendly. rsvg-convert comes with librsvg, which every GTK desktop
/// already has; without it only PNG icons are used.
/// </remarks>
[SupportedOSPlatform("linux")]
public static class LinuxIconLoader
{
    private const int IconSize = 32;
    private const string SvgRenderer = "rsvg-convert";

    // One lookup for the whole session, so its directory caches are shared by
    // every icon. Icons are loaded from a single background thread, the lock only
    // guards against a refresh starting a second one.
    private static readonly Lazy<IconThemeLookup> Lookup = new(() => new IconThemeLookup(
        LinuxDesktop.Xdg.IconBaseDirectories,
        ["/usr/share/pixmaps"],
        CanRenderSvg ? [".png", ".svg"] : [".png"]));

    private static readonly Lazy<string> ThemeName = new(DetectThemeName);
    private static readonly object Gate = new();

    private static bool CanRenderSvg { get; } = LinuxDesktop.IsOnPath(SvgRenderer);

    public static Bitmap? GetLargeIcon(string path)
    {
        if (!DesktopLaunch.IsDesktopFile(path) || LinuxDesktop.ReadEntry(path)?.Icon is not { Length: > 0 } iconName)
        {
            return null;
        }

        string? file;
        lock (Gate)
        {
            file = Lookup.Value.Find(iconName, ThemeName.Value, IconSize);
        }

        if (file is null)
        {
            return null;
        }

        try
        {
            return file.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ? RenderSvg(file) : LoadPng(file);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            // A broken icon file costs that one icon; the generic one stays.
            return null;
        }
    }

    private static Bitmap LoadPng(string file)
    {
        using var stream = File.OpenRead(file);
        var bitmap = new Bitmap(stream);
        if (bitmap.PixelSize.Width == IconSize && bitmap.PixelSize.Height == IconSize)
        {
            return bitmap;
        }

        // The closest size the theme had; the icon grid expects exactly 32x32.
        using (bitmap)
        {
            return bitmap.CreateScaledBitmap(new PixelSize(IconSize, IconSize), BitmapInterpolationMode.HighQuality);
        }
    }

    private static Bitmap? RenderSvg(string file)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = SvgRenderer,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in new[] { "--width", IconSize.ToString(), "--height", IconSize.ToString(), "--keep-aspect-ratio", "--format", "png", file })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return null;
        }

        using var png = new MemoryStream();
        process.StandardOutput.BaseStream.CopyTo(png);
        process.WaitForExit();
        if (process.ExitCode != 0 || png.Length == 0)
        {
            return null;
        }

        png.Position = 0;
        return new Bitmap(png);
    }

    /// <summary>
    /// Asks the desktop for its icon theme: KDE keeps it in kdeglobals, the GTK
    /// family in GSettings (with a schema per desktop), and settings.ini covers
    /// the rest.
    /// </summary>
    private static string DetectThemeName()
    {
        var desktops = LinuxDesktop.Xdg.CurrentDesktops;
        var config = LinuxDesktop.Xdg.ConfigHome;

        if (desktops.Contains("KDE") && ReadFile(Path.Combine(config, "kdeglobals")) is { } kdeGlobals
            && IconThemeSettings.FromKdeGlobals(kdeGlobals) is { } kdeTheme)
        {
            return kdeTheme;
        }

        var schema = desktops.Contains("X-Cinnamon") ? "org.cinnamon.desktop.interface"
            : desktops.Contains("MATE") ? "org.mate.interface"
            : "org.gnome.desktop.interface";
        if (Gsettings(schema, "icon-theme") is { } gsettingsTheme)
        {
            return gsettingsTheme;
        }

        if (ReadFile(Path.Combine(config, "gtk-3.0", "settings.ini")) is { } gtkSettings
            && IconThemeSettings.FromGtkSettings(gtkSettings) is { } gtkTheme)
        {
            return gtkTheme;
        }

        return "hicolor";
    }

    private static string? Gsettings(string schema, string key)
    {
        try
        {
            var startInfo = new ProcessStartInfo("gsettings")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            startInfo.ArgumentList.Add("get");
            startInfo.ArgumentList.Add(schema);
            startInfo.ArgumentList.Add(key);
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 ? IconThemeSettings.FromGsettingsOutput(output) : null;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // gsettings is not installed; the next source is tried.
            return null;
        }
    }

    private static string? ReadFile(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
