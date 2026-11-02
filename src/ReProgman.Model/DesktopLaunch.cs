namespace ReProgman.Model;

/// <summary>A process to start: the program, its arguments and where to run it.</summary>
public sealed record LaunchCommand(string FileName, IReadOnlyList<string> Arguments, string? WorkingDirectory);

/// <summary>
/// Decides how a Linux program item is started. Items found by the scan point at
/// .desktop files, which are not executable themselves, so their Exec line is
/// run instead.
/// </summary>
public static class DesktopLaunch
{
    private const string DesktopExtension = ".desktop";

    // Terminal emulators in the order they are tried, with the option that makes
    // the rest of the command line the program to run. xdg-terminal-exec is the
    // emerging standard and honours the user's choice, so it goes first.
    private static readonly (string Program, string[] Prefix)[] TerminalCandidates =
    [
        ("xdg-terminal-exec", []),
        ("x-terminal-emulator", ["-e"]),
        ("gnome-terminal", ["--"]),
        ("konsole", ["-e"]),
        ("xfce4-terminal", ["-x"]),
        ("mate-terminal", ["-x"]),
        ("tilix", ["-e"]),
        ("kitty", []),
        ("alacritty", ["-e"]),
        ("foot", []),
        ("wezterm", ["start", "--"]),
        ("xterm", ["-e"]),
    ];

    public static bool IsDesktopFile(string path) => path.EndsWith(DesktopExtension, StringComparison.Ordinal);

    /// <param name="terminal">
    /// The command that runs a program in a terminal window, or null when none is
    /// installed. A Terminal=true entry then starts without a window.
    /// </param>
    public static LaunchCommand? ForEntry(DesktopEntry entry, string desktopFilePath, IReadOnlyList<string>? terminal)
    {
        if (entry.Exec is not { } exec)
        {
            return null;
        }

        var arguments = DesktopExec.Expand(exec, entry.Icon, entry.Name, desktopFilePath);
        if (arguments.Count == 0)
        {
            return null;
        }

        if (entry.Terminal && terminal is { Count: > 0 })
        {
            arguments = [.. terminal, .. arguments];
        }

        var workingDirectory = string.IsNullOrEmpty(entry.WorkingDirectory) ? null : entry.WorkingDirectory;
        return new LaunchCommand(arguments[0], arguments.Skip(1).ToArray(), workingDirectory);
    }

    /// <param name="terminalVariable">The value of $TERMINAL, which wins when set.</param>
    /// <param name="isInstalled">Whether a program name can be found on PATH.</param>
    public static IReadOnlyList<string>? TerminalPrefix(string? terminalVariable, Func<string, bool> isInstalled)
    {
        if (!string.IsNullOrWhiteSpace(terminalVariable))
        {
            // There is no standard for $TERMINAL's arguments; -e is what nearly all
            // emulators understand.
            return [terminalVariable.Trim(), "-e"];
        }

        foreach (var (program, prefix) in TerminalCandidates)
        {
            if (isInstalled(program))
            {
                return [program, .. prefix];
            }
        }

        return null;
    }
}
