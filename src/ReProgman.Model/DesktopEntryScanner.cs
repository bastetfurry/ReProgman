namespace ReProgman.Model;

/// <summary>
/// The program groups a Linux desktop is split into. The names lean on the
/// Windows 3.1 defaults (Main, Accessories, Games, Control Panel) where a
/// category matches one.
/// </summary>
public enum DesktopMenuGroup
{
    Main,
    Accessories,
    Games,
    ControlPanel,
    Office,
    Graphics,
    Internet,
    Multimedia,
    Development,
    Education,
    Science,
}

/// <summary>Maps freedesktop.org menu categories onto <see cref="DesktopMenuGroup"/>.</summary>
public static class DesktopMenuGroups
{
    // The registered main categories. System lands in Main because that is where
    // Program Manager kept File Manager and the DOS prompt. Settings gets a group
    // of its own: desktops ship dozens of settings panels, which would bury Main.
    private static readonly Dictionary<string, DesktopMenuGroup> MainCategories = new(StringComparer.Ordinal)
    {
        ["AudioVideo"] = DesktopMenuGroup.Multimedia,
        ["Audio"] = DesktopMenuGroup.Multimedia,
        ["Video"] = DesktopMenuGroup.Multimedia,
        ["Development"] = DesktopMenuGroup.Development,
        ["Education"] = DesktopMenuGroup.Education,
        ["Game"] = DesktopMenuGroup.Games,
        ["Graphics"] = DesktopMenuGroup.Graphics,
        ["Network"] = DesktopMenuGroup.Internet,
        ["Office"] = DesktopMenuGroup.Office,
        ["Science"] = DesktopMenuGroup.Science,
        ["Settings"] = DesktopMenuGroup.ControlPanel,
        ["System"] = DesktopMenuGroup.Main,
        ["Utility"] = DesktopMenuGroup.Accessories,
    };

    /// <summary>The group of the first main category in the list; Main when there is none.</summary>
    public static DesktopMenuGroup GroupFor(IEnumerable<string> categories)
    {
        foreach (var category in categories)
        {
            if (MainCategories.TryGetValue(category, out var group))
            {
                return group;
            }
        }

        return DesktopMenuGroup.Main;
    }
}

/// <summary>
/// Builds Program Manager groups from the .desktop files below the XDG
/// application directories. Linux has no folder per program group, so the
/// grouping comes from each entry's Categories key instead of the directory tree.
/// </summary>
public sealed class DesktopEntryScanner(string locale, IReadOnlyCollection<string> currentDesktops)
{
    private const string DesktopExtension = ".desktop";

    /// <param name="applicationDirectories">Most important first; earlier ones shadow later ones.</param>
    /// <param name="groupName">The display name of each group.</param>
    public IReadOnlyList<ProgramGroup> Scan(IEnumerable<string> applicationDirectories, Func<DesktopMenuGroup, string> groupName)
    {
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        var groups = new Dictionary<DesktopMenuGroup, Dictionary<string, ProgramItem>>();

        foreach (var directory in applicationDirectories)
        {
            if (!Directory.Exists(directory))
            {
                continue;
            }

            foreach (var file in EnumerateDesktopFiles(directory))
            {
                // The desktop file id decides shadowing, and it is claimed even by an
                // entry that is not shown: Hidden=true in ~/.local is how a user
                // removes a system-wide entry.
                if (!seenIds.Add(DesktopFileId(directory, file)))
                {
                    continue;
                }

                if (TryRead(file) is not { } entry || !entry.IsShownIn(currentDesktops))
                {
                    continue;
                }

                var name = entry.Name.Length > 0 ? entry.Name : Path.GetFileNameWithoutExtension(file);
                var group = DesktopMenuGroups.GroupFor(entry.Categories);
                if (!groups.TryGetValue(group, out var items))
                {
                    items = new Dictionary<string, ProgramItem>(StringComparer.OrdinalIgnoreCase);
                    groups[group] = items;
                }

                items.TryAdd(name, new ProgramItem(name, file));
            }
        }

        return groups
            .Select(g => (Key: g.Key, Name: groupName(g.Key), Items: g.Value.Values))
            .OrderBy(g => g.Key != DesktopMenuGroup.Main)
            .ThenBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => new ProgramGroup(
                g.Name,
                g.Items.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToArray()))
            .ToArray();
    }

    private static IEnumerable<string> EnumerateDesktopFiles(string directory)
    {
        try
        {
            // Sorted so that the result does not depend on the file system's order
            // when two files claim the same name.
            return Directory
                .EnumerateFiles(directory, "*" + DesktopExtension, new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true,
                    MatchCasing = MatchCasing.CaseSensitive,
                })
                .Order(StringComparer.Ordinal)
                .ToArray();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static string DesktopFileId(string root, string file) =>
        Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '-');

    private DesktopEntry? TryRead(string file)
    {
        try
        {
            return DesktopEntry.Parse(File.ReadAllText(file), locale);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // A dangling symlink or an unreadable file costs one item, not the scan.
            return null;
        }
    }
}
