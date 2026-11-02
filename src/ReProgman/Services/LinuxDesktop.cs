using System.Runtime.Versioning;
using ReProgman.Model;

namespace ReProgman.Services;

/// <summary>
/// Facts about the running Linux session that the scanner, the launcher and the
/// icon loader share. Everything is read lazily and once; the session does not
/// change its locale or desktop while ReProgman runs.
/// </summary>
[SupportedOSPlatform("linux")]
public static class LinuxDesktop
{
    private static readonly Lazy<IReadOnlyList<string>?> TerminalLazy = new(() =>
        DesktopLaunch.TerminalPrefix(Environment.GetEnvironmentVariable("TERMINAL"), IsOnPath));

    public static XdgDirectories Xdg { get; } = XdgDirectories.FromEnvironment();

    /// <summary>
    /// The POSIX locale for message translations, resolved the way gettext does.
    /// CultureInfo would lose the @modifier that .desktop files can match on.
    /// </summary>
    public static string Locale { get; } =
        FirstNonEmpty("LC_ALL", "LC_MESSAGES", "LANG") ?? "C";

    /// <summary>The command that runs a program inside a terminal window, if one is installed.</summary>
    public static IReadOnlyList<string>? Terminal => TerminalLazy.Value;

    public static bool IsOnPath(string program)
    {
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(':', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory, program);
            if (File.Exists(candidate) && IsExecutable(candidate))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsExecutable(string path)
    {
        const UnixFileMode executable = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
        return (File.GetUnixFileMode(path) & executable) != 0;
    }

    public static DesktopEntry? ReadEntry(string desktopFilePath) =>
        File.Exists(desktopFilePath) ? DesktopEntry.Parse(File.ReadAllText(desktopFilePath), Locale) : null;

    private static string? FirstNonEmpty(params string[] names) =>
        names.Select(Environment.GetEnvironmentVariable).FirstOrDefault(v => !string.IsNullOrEmpty(v));
}
