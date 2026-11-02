using ReProgman.Model;

namespace ReProgman.Model.Tests;

public sealed class XdgDirectoriesTests
{
    private static XdgDirectories From(params (string Key, string Value)[] env)
    {
        var map = env.ToDictionary(e => e.Key, e => e.Value);
        return new XdgDirectories(key => map.GetValueOrDefault(key));
    }

    [Fact]
    public void Defaults_FollowTheBaseDirectorySpec()
    {
        var xdg = From(("HOME", "/home/u"));

        Assert.Equal("/home/u/.config", xdg.ConfigHome);
        Assert.Equal("/home/u/.local/share", xdg.DataHome);
        Assert.Equal(["/usr/local/share", "/usr/share"], xdg.DataDirs);
    }

    [Fact]
    public void Variables_OverrideTheDefaults()
    {
        var xdg = From(
            ("HOME", "/home/u"),
            ("XDG_CONFIG_HOME", "/cfg"),
            ("XDG_DATA_HOME", "/data"),
            ("XDG_DATA_DIRS", "/a:/b/:"));

        Assert.Equal("/cfg", xdg.ConfigHome);
        Assert.Equal("/data", xdg.DataHome);
        Assert.Equal(["/a", "/b"], xdg.DataDirs);
    }

    [Fact]
    public void RelativePaths_AreIgnored_AsTheSpecRequires()
    {
        var xdg = From(("HOME", "/home/u"), ("XDG_CONFIG_HOME", "relative"), ("XDG_DATA_DIRS", "rel:/ok"));

        Assert.Equal("/home/u/.config", xdg.ConfigHome);
        Assert.Equal(["/ok"], xdg.DataDirs);
    }

    [Fact]
    public void ApplicationDirectories_PutTheUserFirst()
    {
        var xdg = From(("HOME", "/home/u"), ("XDG_DATA_DIRS", "/usr/share"));

        Assert.Equal(["/home/u/.local/share/applications", "/usr/share/applications"], xdg.ApplicationDirectories);
    }

    [Fact]
    public void IconDirectories_FollowTheIconThemeSpec()
    {
        var xdg = From(("HOME", "/home/u"), ("XDG_DATA_DIRS", "/usr/share"));

        Assert.Equal(["/home/u/.icons", "/home/u/.local/share/icons", "/usr/share/icons"], xdg.IconBaseDirectories);
    }

    [Fact]
    public void CurrentDesktops_SplitsAtColons()
    {
        Assert.Equal(["ubuntu", "GNOME"], From(("XDG_CURRENT_DESKTOP", "ubuntu:GNOME")).CurrentDesktops);
        Assert.Empty(From().CurrentDesktops);
    }
}

public sealed class DesktopMenuGroupsTests
{
    [Theory]
    [InlineData("GTK;Utility;Calculator;", DesktopMenuGroup.Accessories)]
    [InlineData("Game;ArcadeGame;", DesktopMenuGroup.Games)]
    [InlineData("System;TerminalEmulator;", DesktopMenuGroup.Main)]
    [InlineData("Settings;DesktopSettings;", DesktopMenuGroup.ControlPanel)]
    [InlineData("Office;WordProcessor;", DesktopMenuGroup.Office)]
    [InlineData("Graphics;2DGraphics;", DesktopMenuGroup.Graphics)]
    [InlineData("Network;WebBrowser;", DesktopMenuGroup.Internet)]
    [InlineData("AudioVideo;Player;", DesktopMenuGroup.Multimedia)]
    [InlineData("Audio;", DesktopMenuGroup.Multimedia)]
    [InlineData("Video;", DesktopMenuGroup.Multimedia)]
    [InlineData("Development;IDE;", DesktopMenuGroup.Development)]
    [InlineData("Education;", DesktopMenuGroup.Education)]
    [InlineData("Science;Math;", DesktopMenuGroup.Science)]
    [InlineData("X-Custom;", DesktopMenuGroup.Main)]
    [InlineData("", DesktopMenuGroup.Main)]
    public void GroupFor_MapsMainCategories(string categories, DesktopMenuGroup expected)
    {
        Assert.Equal(expected, DesktopMenuGroups.GroupFor(categories.Split(';', StringSplitOptions.RemoveEmptyEntries)));
    }

    [Fact]
    public void GroupFor_FirstMainCategoryWins()
    {
        Assert.Equal(DesktopMenuGroup.Development, DesktopMenuGroups.GroupFor(["Development", "Utility"]));
        Assert.Equal(DesktopMenuGroup.Accessories, DesktopMenuGroups.GroupFor(["Utility", "Development"]));
    }
}

public sealed class DesktopEntryScannerTests : IDisposable
{
    private readonly string _root;
    private readonly string _user;
    private readonly string _system;

    public DesktopEntryScannerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "ReProgmanDesktopTest_" + Guid.NewGuid().ToString("N"));
        _user = Path.Combine(_root, "user");
        _system = Path.Combine(_root, "system");
        Directory.CreateDirectory(_user);
        Directory.CreateDirectory(_system);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static string Write(string directory, string fileName, string body)
    {
        var path = Path.Combine(directory, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "[Desktop Entry]\nType=Application\n" + body);
        return path;
    }

    private IReadOnlyList<ProgramGroup> Scan(string[]? desktops = null) =>
        new DesktopEntryScanner("C", desktops ?? [])
            .Scan([_user, _system, Path.Combine(_root, "missing")], g => g.ToString());

    [Fact]
    public void Scan_GroupsEntriesByCategory()
    {
        var calc = Write(_system, "calc.desktop", "Name=Calculator\nExec=calc\nCategories=Utility;");
        var mines = Write(_system, "mines.desktop", "Name=Mines\nExec=mines\nCategories=Game;");

        var groups = Scan();

        Assert.Equal(["Accessories", "Games"], groups.Select(g => g.Name));
        Assert.Equal(new ProgramItem("Calculator", calc), Assert.Single(groups[0].Items));
        Assert.Equal(new ProgramItem("Mines", mines), Assert.Single(groups[1].Items));
    }

    [Fact]
    public void Scan_PutsMainFirst_ThenSortsByName()
    {
        Write(_system, "a.desktop", "Name=A\nCategories=Office;");
        Write(_system, "b.desktop", "Name=B\nCategories=System;");
        Write(_system, "c.desktop", "Name=C\nCategories=Development;");

        Assert.Equal(["Main", "Development", "Office"], Scan().Select(g => g.Name));
    }

    [Fact]
    public void Scan_SortsItemsByName()
    {
        Write(_system, "z.desktop", "Name=Zeta");
        Write(_system, "a.desktop", "Name=alpha");

        Assert.Equal(["alpha", "Zeta"], Assert.Single(Scan()).Items.Select(i => i.Name));
    }

    [Fact]
    public void Scan_SkipsEntriesThatAreNotShown()
    {
        Write(_system, "hidden.desktop", "Name=Hidden\nNoDisplay=true");
        Write(_system, "gnome.desktop", "Name=Gnome Only\nOnlyShowIn=GNOME;");
        Write(_system, "notes.txt", "Name=Not a desktop file");

        Assert.Empty(Scan(["X-Cinnamon"]));
        Assert.Equal("Gnome Only", Assert.Single(Assert.Single(Scan(["GNOME"])).Items).Name);
    }

    [Fact]
    public void Scan_UserEntryShadowsTheSystemEntryWithTheSameId()
    {
        var user = Write(_user, "editor.desktop", "Name=My Editor");
        Write(_system, "editor.desktop", "Name=Editor");

        var item = Assert.Single(Assert.Single(Scan()).Items);
        Assert.Equal(new ProgramItem("My Editor", user), item);
    }

    [Fact]
    public void Scan_AHiddenUserEntryRemovesTheSystemEntry()
    {
        Write(_user, "editor.desktop", "Name=Editor\nHidden=true");
        Write(_system, "editor.desktop", "Name=Editor");

        Assert.Empty(Scan());
    }

    [Fact]
    public void Scan_SubdirectoriesFormThePrefixOfTheDesktopId()
    {
        // kde4/konsole.desktop has the id kde4-konsole.desktop, so it shadows a
        // top-level file of that name in a later directory.
        var nested = Write(_user, Path.Combine("kde4", "konsole.desktop"), "Name=Nested");
        Write(_system, "kde4-konsole.desktop", "Name=Flat");

        Assert.Equal(new ProgramItem("Nested", nested), Assert.Single(Assert.Single(Scan()).Items));
    }

    [Fact]
    public void Scan_FallsBackToTheFileNameWithoutAName()
    {
        Write(_system, "nameless.desktop", "Exec=x");

        Assert.Equal("nameless", Assert.Single(Assert.Single(Scan()).Items).Name);
    }

    [Fact]
    public void Scan_KeepsTheFirstOfTwoEntriesWithTheSameNameInAGroup()
    {
        var first = Write(_user, "a.desktop", "Name=Same");
        Write(_system, "b.desktop", "Name=Same");

        Assert.Equal(first, Assert.Single(Assert.Single(Scan()).Items).Path);
    }

    [Fact]
    public void Scan_UsesTheLocaleForNames()
    {
        Write(_system, "calc.desktop", "Name=Calculator\nName[de]=Taschenrechner");

        var groups = new DesktopEntryScanner("de_DE.UTF-8", []).Scan([_system], g => g.ToString());

        Assert.Equal("Taschenrechner", Assert.Single(Assert.Single(groups).Items).Name);
    }
}
