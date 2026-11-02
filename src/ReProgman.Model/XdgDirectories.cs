namespace ReProgman.Model;

/// <summary>
/// The XDG Base Directory locations ReProgman uses on Linux. The environment is
/// passed in as a lookup so the rules can be tested without touching the real one.
/// </summary>
public sealed class XdgDirectories(Func<string, string?> getEnvironmentVariable)
{
    private string Home => getEnvironmentVariable("HOME") ?? "/";

    public static XdgDirectories FromEnvironment() => new(Environment.GetEnvironmentVariable);

    public string ConfigHome => AbsoluteOrNull("XDG_CONFIG_HOME") ?? Path.Combine(Home, ".config");

    public string DataHome => AbsoluteOrNull("XDG_DATA_HOME") ?? Path.Combine(Home, ".local", "share");

    public IReadOnlyList<string> DataDirs
    {
        get
        {
            var dirs = SplitAbsolute(getEnvironmentVariable("XDG_DATA_DIRS"));
            return dirs.Count > 0 ? dirs : ["/usr/local/share", "/usr/share"];
        }
    }

    /// <summary>Where .desktop files live, most important first.</summary>
    public IReadOnlyList<string> ApplicationDirectories =>
        [Path.Combine(DataHome, "applications"), .. DataDirs.Select(d => Path.Combine(d, "applications"))];

    /// <summary>
    /// The icon theme base directories in lookup order. /usr/share/pixmaps is not
    /// part of the list; the spec treats it as a separate, last fallback.
    /// </summary>
    public IReadOnlyList<string> IconBaseDirectories =>
    [
        Path.Combine(Home, ".icons"),
        Path.Combine(DataHome, "icons"),
        .. DataDirs.Select(d => Path.Combine(d, "icons")),
    ];

    /// <summary>The desktop names from XDG_CURRENT_DESKTOP, used by OnlyShowIn/NotShowIn.</summary>
    public IReadOnlyList<string> CurrentDesktops =>
        (getEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? string.Empty)
            .Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private string? AbsoluteOrNull(string name) =>
        getEnvironmentVariable(name) is { } value && Path.IsPathRooted(value) ? value : null;

    private static IReadOnlyList<string> SplitAbsolute(string? value) =>
        (value ?? string.Empty)
            .Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            // The spec says relative entries must be ignored.
            .Where(Path.IsPathRooted)
            .Select(d => d.Length > 1 ? d.TrimEnd('/') : d)
            .ToArray();
}
