namespace ReProgman.Model;

/// <summary>
/// Resolves an icon name to a file following the freedesktop.org Icon Theme
/// Specification: the selected theme, its Inherits chain, hicolor, and finally
/// the plain pixmap directories.
/// </summary>
/// <remarks>
/// Directory listings and index.theme files are cached per instance, because a
/// scan resolves hundreds of icons against the same few themes and probing every
/// candidate path with a stat call would cost thousands of syscalls per icon.
/// </remarks>
public sealed class IconThemeLookup(
    IReadOnlyList<string> baseDirectories,
    IReadOnlyList<string> fallbackDirectories,
    IReadOnlyList<string> extensions)
{
    private const string FallbackTheme = "hicolor";

    private readonly Dictionary<string, ThemeInfo?> _themes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>?> _listings = new(StringComparer.Ordinal);

    private sealed record ThemeInfo(IReadOnlyList<string> Parents, IReadOnlyList<SubDirectory> Directories);

    private enum SizeType { Fixed, Scalable, Threshold }

    private sealed record SubDirectory(string Path, int Size, SizeType Type, int MinSize, int MaxSize, int Threshold)
    {
        public bool Matches(int size) => Type switch
        {
            SizeType.Fixed => size == Size,
            SizeType.Scalable => size >= MinSize && size <= MaxSize,
            _ => size >= Size - Threshold && size <= Size + Threshold,
        };

        public int Distance(int size) => Type switch
        {
            SizeType.Fixed => Math.Abs(Size - size),
            SizeType.Scalable => size < MinSize ? MinSize - size : size > MaxSize ? size - MaxSize : 0,
            _ => size < Size - Threshold ? Size - Threshold - size : size > Size + Threshold ? size - Size - Threshold : 0,
        };
    }

    public string? Find(string iconName, string themeName, int size)
    {
        if (string.IsNullOrWhiteSpace(iconName))
        {
            return null;
        }

        if (Path.IsPathRooted(iconName))
        {
            return File.Exists(iconName) ? iconName : null;
        }

        // Some .desktop files name the file instead of the icon; the spec says to
        // drop the extension then.
        var name = extensions.Any(e => iconName.EndsWith(e, StringComparison.OrdinalIgnoreCase))
            ? Path.GetFileNameWithoutExtension(iconName)
            : iconName;

        foreach (var theme in ThemeChain(themeName))
        {
            if (LookupInTheme(theme, name, size) is { } found)
            {
                return found;
            }
        }

        foreach (var directory in fallbackDirectories)
        {
            if (FindFile(directory, name) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private IEnumerable<string> ThemeChain(string themeName)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>();
        pending.Push(themeName);
        while (pending.Count > 0)
        {
            var theme = pending.Pop();
            if (!visited.Add(theme))
            {
                continue;
            }

            yield return theme;
            if (LoadTheme(theme) is { } info)
            {
                // Pushed in reverse so that the first parent is searched first.
                for (var i = info.Parents.Count - 1; i >= 0; i--)
                {
                    pending.Push(info.Parents[i]);
                }
            }
        }

        if (visited.Add(FallbackTheme))
        {
            yield return FallbackTheme;
        }
    }

    private string? LookupInTheme(string theme, string name, int size)
    {
        if (LoadTheme(theme) is not { } info)
        {
            return null;
        }

        foreach (var subdirectory in info.Directories.Where(d => d.Matches(size)))
        {
            if (FindInThemeDirectory(theme, subdirectory, name) is { } exact)
            {
                return exact;
            }
        }

        string? closest = null;
        var bestDistance = int.MaxValue;
        foreach (var subdirectory in info.Directories)
        {
            var distance = subdirectory.Distance(size);
            if (distance < bestDistance && FindInThemeDirectory(theme, subdirectory, name) is { } candidate)
            {
                closest = candidate;
                bestDistance = distance;
            }
        }

        return closest;
    }

    private string? FindInThemeDirectory(string theme, SubDirectory subdirectory, string name)
    {
        foreach (var baseDirectory in baseDirectories)
        {
            if (FindFile(Path.Combine(baseDirectory, theme, subdirectory.Path), name) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private string? FindFile(string directory, string name)
    {
        if (List(directory) is not { } files)
        {
            return null;
        }

        foreach (var extension in extensions)
        {
            if (files.Contains(name + extension))
            {
                return Path.Combine(directory, name + extension);
            }
        }

        return null;
    }

    private HashSet<string>? List(string directory)
    {
        if (_listings.TryGetValue(directory, out var cached))
        {
            return cached;
        }

        HashSet<string>? files = null;
        try
        {
            if (Directory.Exists(directory))
            {
                files = Directory.EnumerateFileSystemEntries(directory)
                    .Select(Path.GetFileName)
                    .OfType<string>()
                    .ToHashSet(StringComparer.Ordinal);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Treated like a missing directory.
        }

        _listings[directory] = files;
        return files;
    }

    private ThemeInfo? LoadTheme(string theme)
    {
        if (_themes.TryGetValue(theme, out var cached))
        {
            return cached;
        }

        ThemeInfo? info = null;
        // The first index.theme found describes the theme; the other base
        // directories only contribute files to the same subdirectories.
        foreach (var baseDirectory in baseDirectories)
        {
            var index = Path.Combine(baseDirectory, theme, "index.theme");
            try
            {
                if (File.Exists(index))
                {
                    info = ParseIndex(KeyFile.Parse(File.ReadAllText(index)));
                    break;
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Try the next base directory.
            }
        }

        _themes[theme] = info;
        return info;
    }

    private static ThemeInfo ParseIndex(KeyFile index)
    {
        const string Group = "Icon Theme";
        static IReadOnlyList<string> List(string? value) =>
            (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var directories = new List<SubDirectory>();
        foreach (var path in List(index.Get(Group, "Directories")).Concat(List(index.Get(Group, "ScaledDirectories"))).Distinct())
        {
            int? Int(string key) => int.TryParse(index.Get(path, key), out var value) ? value : null;

            // High-DPI variants hold bigger bitmaps under the same nominal size;
            // Program Manager draws at 1x, so they would only be scaled back down.
            if (Int("Size") is not { } size || (Int("Scale") ?? 1) != 1)
            {
                continue;
            }

            var type = index.Get(path, "Type") switch
            {
                "Fixed" => SizeType.Fixed,
                "Scalable" => SizeType.Scalable,
                _ => SizeType.Threshold,
            };
            directories.Add(new SubDirectory(path, size, type, Int("MinSize") ?? size, Int("MaxSize") ?? size, Int("Threshold") ?? 2));
        }

        return new ThemeInfo(List(index.Get(Group, "Inherits")), directories);
    }
}

/// <summary>Reads the selected icon theme name out of the desktops' own settings.</summary>
public static class IconThemeSettings
{
    /// <summary>KDE Plasma: ~/.config/kdeglobals, [Icons] Theme=.</summary>
    public static string? FromKdeGlobals(string text) => NonEmpty(KeyFile.Parse(text).Get("Icons", "Theme"));

    /// <summary>GTK: ~/.config/gtk-3.0/settings.ini, [Settings] gtk-icon-theme-name=.</summary>
    public static string? FromGtkSettings(string text) => NonEmpty(KeyFile.Parse(text).Get("Settings", "gtk-icon-theme-name"));

    /// <summary>The output of <c>gsettings get … icon-theme</c>, a quoted GVariant string.</summary>
    public static string? FromGsettingsOutput(string output) => NonEmpty(output.Trim().Trim('\'', '"'));

    private static string? NonEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
