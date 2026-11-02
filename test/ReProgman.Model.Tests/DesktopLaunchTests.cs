using ReProgman.Model;

namespace ReProgman.Model.Tests;

public sealed class DesktopLaunchTests
{
    private static DesktopEntry Entry(string body) => DesktopEntry.Parse("[Desktop Entry]\nType=Application\n" + body, "C")!;

    [Fact]
    public void ForEntry_StartsTheExpandedExecLine()
    {
        var command = DesktopLaunch.ForEntry(Entry("Name=Editor\nExec=gedit --new-window %U"), "/a/gedit.desktop", terminal: null);

        Assert.NotNull(command);
        Assert.Equal("gedit", command.FileName);
        Assert.Equal(["--new-window"], command.Arguments);
        Assert.Null(command.WorkingDirectory);
    }

    [Fact]
    public void ForEntry_UsesThePathKeyAsWorkingDirectory()
    {
        var command = DesktopLaunch.ForEntry(Entry("Exec=run\nPath=/opt/game"), "/a/x.desktop", terminal: null);

        Assert.Equal("/opt/game", command!.WorkingDirectory);
    }

    [Fact]
    public void ForEntry_WrapsTerminalEntriesInTheTerminal()
    {
        var command = DesktopLaunch.ForEntry(Entry("Exec=htop -d 10\nTerminal=true"), "/a/htop.desktop", ["gnome-terminal", "--"]);

        Assert.Equal("gnome-terminal", command!.FileName);
        Assert.Equal(["--", "htop", "-d", "10"], command.Arguments);
    }

    [Fact]
    public void ForEntry_StartsTerminalEntriesDirectly_WhenNoTerminalIsKnown()
    {
        var command = DesktopLaunch.ForEntry(Entry("Exec=htop\nTerminal=true"), "/a/htop.desktop", terminal: null);

        Assert.Equal("htop", command!.FileName);
    }

    [Fact]
    public void ForEntry_ReturnsNullWithoutExec()
    {
        Assert.Null(DesktopLaunch.ForEntry(Entry("Name=x"), "/a/x.desktop", terminal: null));
        Assert.Null(DesktopLaunch.ForEntry(Entry("Exec=%f"), "/a/x.desktop", terminal: null));
    }

    [Fact]
    public void TerminalPrefix_PrefersTheTerminalVariable()
    {
        Assert.Equal(["kitty", "-e"], DesktopLaunch.TerminalPrefix("kitty", _ => true));
    }

    [Fact]
    public void TerminalPrefix_TakesTheFirstInstalledCandidate()
    {
        var installed = new HashSet<string> { "konsole", "xterm" };

        Assert.Equal(["konsole", "-e"], DesktopLaunch.TerminalPrefix(null, installed.Contains));
    }

    [Fact]
    public void TerminalPrefix_PrefersXdgTerminalExec()
    {
        var installed = new HashSet<string> { "xdg-terminal-exec", "xterm" };

        Assert.Equal(["xdg-terminal-exec"], DesktopLaunch.TerminalPrefix(" ", installed.Contains));
    }

    [Fact]
    public void TerminalPrefix_IsNullWhenNothingIsInstalled()
    {
        Assert.Null(DesktopLaunch.TerminalPrefix(null, _ => false));
    }

    [Theory]
    [InlineData("/usr/share/applications/gedit.desktop", true)]
    [InlineData("/x/APP.DESKTOP", false)]
    [InlineData("/usr/bin/gedit", false)]
    public void IsDesktopFile_ChecksTheExtension(string path, bool expected)
    {
        Assert.Equal(expected, DesktopLaunch.IsDesktopFile(path));
    }
}
