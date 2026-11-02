using System.Diagnostics;
using System.Runtime.Versioning;
using ReProgman.Model;

namespace ReProgman.Services;

public static class ShellLauncher
{
    private const string BundleExtension = ".app";
    private const string MacOpenTool = "/usr/bin/open";
    private const string LinuxOpenTool = "xdg-open";

    /// <summary>Launches a shortcut or application the way a double click would.</summary>
    public static void Launch(string path, bool minimized = false)
    {
        if (OperatingSystem.IsLinux() && DesktopLaunch.IsDesktopFile(path))
        {
            StartDesktopEntry(path);
            return;
        }

        if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
        {
            StartOnUnix(path, arguments: null);
            return;
        }

        Process.Start(new ProcessStartInfo(path)
        {
            UseShellExecute = true,
            WindowStyle = minimized ? ProcessWindowStyle.Minimized : ProcessWindowStyle.Normal,
        })?.Dispose();
    }

    /// <summary>
    /// Launches a Run-dialog command line. The whole text is tried as a single path first
    /// so that unquoted paths with spaces keep working; otherwise it is split into
    /// program + arguments at the first space.
    /// </summary>
    public static void LaunchCommandLine(string commandLine, bool minimized)
    {
        var text = commandLine.Trim();
        if (text.Length == 0)
        {
            return;
        }

        var (fileName, arguments) = SplitCommandLine(text);
        if (OperatingSystem.IsLinux() && DesktopLaunch.IsDesktopFile(fileName))
        {
            // Without file field codes there is nowhere to put the extra arguments.
            StartDesktopEntry(fileName);
            return;
        }

        if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
        {
            // Run Minimized has no macOS or Linux equivalent, so the checkbox is ignored here.
            StartOnUnix(fileName, arguments);
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = true,
            WindowStyle = minimized ? ProcessWindowStyle.Minimized : ProcessWindowStyle.Normal,
        })?.Dispose();
    }

    private static (string FileName, string Arguments) SplitCommandLine(string text)
    {
        if (text.StartsWith('"'))
        {
            var closing = text.IndexOf('"', 1);
            return closing > 0
                ? (text[1..closing], text[(closing + 1)..].Trim())
                : (text.Trim('"'), string.Empty);
        }

        if (File.Exists(text) || Directory.Exists(text) || !text.Contains(' '))
        {
            return (text, string.Empty);
        }

        var separator = text.IndexOf(' ');
        return (text[..separator], text[(separator + 1)..].Trim());
    }

    /// <summary>
    /// Runs the Exec line of a .desktop file. The file itself is not executable,
    /// and handing it to xdg-open would open it in a text editor.
    /// </summary>
    [SupportedOSPlatform("linux")]
    private static void StartDesktopEntry(string path)
    {
        var entry = LinuxDesktop.ReadEntry(path) ?? throw new FileNotFoundException(null, path);
        var command = DesktopLaunch.ForEntry(entry, path, LinuxDesktop.Terminal)
            ?? throw new FileNotFoundException(null, path);

        var startInfo = new ProcessStartInfo { FileName = command.FileName, UseShellExecute = false };
        foreach (var argument in command.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (command.WorkingDirectory is { } workingDirectory && Directory.Exists(workingDirectory))
        {
            startInfo.WorkingDirectory = workingDirectory;
        }

        Process.Start(startInfo)?.Dispose();
    }

    /// <summary>
    /// Application bundles, documents and folders go through the desktop's opener
    /// (<c>open</c> on macOS, <c>xdg-open</c> on Linux), which is what a double
    /// click in Finder or the file manager does. Plain executables are started
    /// directly, because the opener would only show them in an editor.
    /// </summary>
    [SupportedOSPlatform("macos")]
    [SupportedOSPlatform("linux")]
    private static void StartOnUnix(string path, string? arguments)
    {
        var startInfo = new ProcessStartInfo { UseShellExecute = false };
        if (IsBundle(path) || IsDocument(path))
        {
            startInfo.FileName = OperatingSystem.IsMacOS() ? MacOpenTool : LinuxOpenTool;
            startInfo.ArgumentList.Add(path);
            // xdg-open takes exactly one argument, so extra arguments only reach
            // macOS applications.
            if (OperatingSystem.IsMacOS() && arguments is { Length: > 0 })
            {
                startInfo.ArgumentList.Add("--args");
                startInfo.ArgumentList.Add(arguments);
            }
        }
        else
        {
            startInfo.FileName = path;
            startInfo.Arguments = arguments ?? string.Empty;
        }

        Process.Start(startInfo)?.Dispose();
    }

    private static bool IsBundle(string path) =>
        OperatingSystem.IsMacOS()
            ? path.TrimEnd('/').EndsWith(BundleExtension, StringComparison.OrdinalIgnoreCase) && Directory.Exists(path)
            // A folder opens in the file manager, like a bundle opens in Finder.
            : Directory.Exists(path);

    [SupportedOSPlatform("macos")]
    [SupportedOSPlatform("linux")]
    private static bool IsDocument(string path)
    {
        if (!File.Exists(path))
        {
            // Bare command names such as "ls" are resolved through PATH instead.
            return false;
        }

        const UnixFileMode executable = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
        return (File.GetUnixFileMode(path) & executable) == 0;
    }
}
