using ReProgman.Model;

namespace ReProgman.Model.Tests;

public sealed class IconThemeLookupTests : IDisposable
{
    private readonly string _root;
    private readonly string _icons;
    private readonly string _userIcons;
    private readonly string _pixmaps;

    public IconThemeLookupTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "ReProgmanIconTest_" + Guid.NewGuid().ToString("N"));
        _icons = Path.Combine(_root, "share", "icons");
        _userIcons = Path.Combine(_root, "home", ".icons");
        _pixmaps = Path.Combine(_root, "share", "pixmaps");
        Directory.CreateDirectory(_icons);
        Directory.CreateDirectory(_userIcons);
        Directory.CreateDirectory(_pixmaps);

        Theme(_icons, "hicolor", null, ("32x32/apps", "Size=32\nType=Threshold"), ("48x48/apps", "Size=48\nType=Threshold"));
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static void Theme(string baseDir, string name, string? inherits, params (string Dir, string Keys)[] directories)
    {
        var dir = Path.Combine(baseDir, name);
        Directory.CreateDirectory(dir);
        var text = "[Icon Theme]\nName=" + name + "\n"
            + (inherits is null ? string.Empty : "Inherits=" + inherits + "\n")
            + "Directories=" + string.Join(',', directories.Select(d => d.Dir)) + "\n\n"
            + string.Concat(directories.Select(d => $"[{d.Dir}]\n{d.Keys}\n\n"));
        File.WriteAllText(Path.Combine(dir, "index.theme"), text);
    }

    private static string Icon(string baseDir, string theme, string subdir, string file)
    {
        var path = Path.Combine(baseDir, theme, subdir, file);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, []);
        return path;
    }

    private IconThemeLookup Lookup(params string[] extensions) =>
        new([_userIcons, _icons], [_pixmaps], extensions.Length > 0 ? extensions : [".png", ".svg"]);

    [Fact]
    public void Find_ReturnsTheExactSizeFromTheTheme()
    {
        Icon(_icons, "hicolor", "48x48/apps", "calc.png");
        var expected = Icon(_icons, "hicolor", "32x32/apps", "calc.png");

        Assert.Equal(expected, Lookup().Find("calc", "hicolor", 32));
    }

    [Fact]
    public void Find_PrefersTheSelectedThemeOverHicolor()
    {
        Theme(_icons, "Fancy", null, ("32x32/apps", "Size=32\nType=Fixed"));
        Icon(_icons, "hicolor", "32x32/apps", "calc.png");
        var expected = Icon(_icons, "Fancy", "32x32/apps", "calc.svg");

        Assert.Equal(expected, Lookup().Find("calc", "Fancy", 32));
    }

    [Fact]
    public void Find_FollowsInheritsBeforeHicolor()
    {
        Theme(_icons, "Child", "Parent", ("32x32/apps", "Size=32\nType=Fixed"));
        Theme(_icons, "Parent", null, ("32x32/apps", "Size=32\nType=Fixed"));
        Icon(_icons, "hicolor", "32x32/apps", "calc.png");
        var expected = Icon(_icons, "Parent", "32x32/apps", "calc.png");

        Assert.Equal(expected, Lookup().Find("calc", "Child", 32));
    }

    [Fact]
    public void Find_FallsBackToHicolor_WhenTheThemeLacksTheIcon()
    {
        Theme(_icons, "Fancy", null, ("32x32/apps", "Size=32\nType=Fixed"));
        var expected = Icon(_icons, "hicolor", "48x48/apps", "calc.png");

        Assert.Equal(expected, Lookup().Find("calc", "Fancy", 32));
    }

    [Fact]
    public void Find_FallsBackToHicolor_WhenTheThemeDoesNotExist()
    {
        var expected = Icon(_icons, "hicolor", "32x32/apps", "calc.png");

        Assert.Equal(expected, Lookup().Find("calc", "Missing", 32));
    }

    [Fact]
    public void Find_SurvivesAnInheritanceLoop()
    {
        Theme(_icons, "A", "B", ("32x32/apps", "Size=32"));
        Theme(_icons, "B", "A", ("32x32/apps", "Size=32"));

        Assert.Null(Lookup().Find("calc", "A", 32));
    }

    [Fact]
    public void Find_TakesTheClosestSize_WhenNoneMatches()
    {
        Icon(_icons, "hicolor", "48x48/apps", "calc.png");

        Assert.Equal(Path.Combine(_icons, "hicolor", "48x48/apps", "calc.png"), Lookup().Find("calc", "hicolor", 32));
    }

    [Fact]
    public void Find_ScalableDirectoryMatchesWithinItsRange()
    {
        Theme(_icons, "Vector", null,
            ("64x64/apps", "Size=64\nType=Fixed"),
            ("scalable/apps", "Size=48\nType=Scalable\nMinSize=16\nMaxSize=256"));
        Icon(_icons, "Vector", "64x64/apps", "calc.svg");
        var expected = Icon(_icons, "Vector", "scalable/apps", "calc.svg");

        Assert.Equal(expected, Lookup().Find("calc", "Vector", 32));
    }

    [Fact]
    public void Find_ThresholdDirectoryMatchesNearbySizes()
    {
        Theme(_icons, "T", null, ("30x30/apps", "Size=30\nType=Threshold\nThreshold=2"), ("32x32/apps", "Size=32\nType=Fixed"));
        Icon(_icons, "T", "32x32/apps", "other.png");
        var expected = Icon(_icons, "T", "30x30/apps", "calc.png");

        Assert.Equal(expected, Lookup().Find("calc", "T", 32));
    }

    [Fact]
    public void Find_SkipsHighDpiDirectories()
    {
        Theme(_icons, "Hi", null, ("32x32@2/apps", "Size=32\nScale=2\nType=Fixed"), ("48x48/apps", "Size=48\nType=Fixed"));
        Icon(_icons, "Hi", "32x32@2/apps", "calc.png");
        var expected = Icon(_icons, "Hi", "48x48/apps", "calc.png");

        Assert.Equal(expected, Lookup().Find("calc", "Hi", 32));
    }

    [Fact]
    public void Find_SearchesEveryBaseDirectoryOfATheme()
    {
        Theme(_icons, "Fancy", null, ("32x32/apps", "Size=32\nType=Fixed"));
        var expected = Icon(_userIcons, "Fancy", "32x32/apps", "calc.png");

        Assert.Equal(expected, Lookup().Find("calc", "Fancy", 32));
    }

    [Fact]
    public void Find_PrefersEarlierExtensions()
    {
        Icon(_icons, "hicolor", "32x32/apps", "calc.svg");
        var expected = Icon(_icons, "hicolor", "32x32/apps", "calc.png");

        Assert.Equal(expected, Lookup(".png", ".svg").Find("calc", "hicolor", 32));
    }

    [Fact]
    public void Find_IgnoresExtensionsItCannotUse()
    {
        Icon(_icons, "hicolor", "32x32/apps", "calc.xpm");

        Assert.Null(Lookup(".png").Find("calc", "hicolor", 32));
    }

    [Fact]
    public void Find_FallsBackToPixmaps()
    {
        var expected = Icon(_root, "share", "pixmaps", "calc.png");

        Assert.Equal(expected, Lookup().Find("calc", "hicolor", 32));
    }

    [Fact]
    public void Find_AcceptsAnAbsolutePath()
    {
        var path = Icon(_root, "opt", "app", "logo.png");

        Assert.Equal(path, Lookup().Find(path, "hicolor", 32));
        Assert.Null(Lookup().Find(Path.Combine(_root, "nope.png"), "hicolor", 32));
    }

    [Fact]
    public void Find_ToleratesANameWithAnExtension()
    {
        var expected = Icon(_icons, "hicolor", "32x32/apps", "calc.png");

        Assert.Equal(expected, Lookup().Find("calc.png", "hicolor", 32));
    }
}

public sealed class IconThemeSettingsTests
{
    [Fact]
    public void FromKdeGlobals_ReadsTheIconsGroup()
    {
        Assert.Equal("breeze-dark", IconThemeSettings.FromKdeGlobals("[General]\nx=y\n[Icons]\nTheme=breeze-dark\n"));
        Assert.Null(IconThemeSettings.FromKdeGlobals("[General]\n"));
    }

    [Fact]
    public void FromGtkSettings_ReadsTheIconThemeName()
    {
        Assert.Equal("Papirus", IconThemeSettings.FromGtkSettings("[Settings]\ngtk-icon-theme-name = Papirus\n"));
        Assert.Null(IconThemeSettings.FromGtkSettings("[Settings]\n"));
    }

    [Theory]
    [InlineData("'Papirus'\n", "Papirus")]
    [InlineData("\"Adwaita\"", "Adwaita")]
    [InlineData("''", null)]
    [InlineData("", null)]
    public void FromGsettingsOutput_StripsTheQuotes(string output, string? expected)
    {
        Assert.Equal(expected, IconThemeSettings.FromGsettingsOutput(output));
    }
}
