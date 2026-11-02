using ReProgman.Model;

namespace ReProgman.Model.Tests;

public sealed class KeyFileTests
{
    [Fact]
    public void Parse_ReadsGroupsAndKeys_IgnoringCommentsAndBlankLines()
    {
        var file = KeyFile.Parse("""
            # A comment
            [Desktop Entry]
            Name=Calculator

            [Desktop Action New]
            Name=New Window
            """);

        Assert.Equal("Calculator", file.Get("Desktop Entry", "Name"));
        Assert.Equal("New Window", file.Get("Desktop Action New", "Name"));
    }

    [Fact]
    public void Parse_KeysAreCaseSensitive_AsTheSpecRequires()
    {
        var file = KeyFile.Parse("[Desktop Entry]\nName=a\nname=b");

        Assert.Equal("a", file.Get("Desktop Entry", "Name"));
        Assert.Equal("b", file.Get("Desktop Entry", "name"));
    }

    [Fact]
    public void Parse_TrimsSpaceAroundTheEqualsSign()
    {
        var file = KeyFile.Parse("[Desktop Entry]\nName = Text Editor ");

        Assert.Equal("Text Editor", file.Get("Desktop Entry", "Name"));
    }

    [Fact]
    public void Parse_FirstOccurrenceOfAKeyWins()
    {
        var file = KeyFile.Parse("[Desktop Entry]\nName=first\nName=second");

        Assert.Equal("first", file.Get("Desktop Entry", "Name"));
    }

    [Fact]
    public void Get_ReturnsNullForMissingGroupOrKey()
    {
        var file = KeyFile.Parse("[Desktop Entry]\nName=x");

        Assert.Null(file.Get("Desktop Entry", "Exec"));
        Assert.Null(file.Get("Other", "Name"));
    }

    [Fact]
    public void Parse_HandlesWindowsLineEndings()
    {
        var file = KeyFile.Parse("[Desktop Entry]\r\nName=x\r\n");

        Assert.Equal("x", file.Get("Desktop Entry", "Name"));
    }

    [Fact]
    public void GroupNames_ListsGroupsInDocumentOrder()
    {
        var file = KeyFile.Parse("[B]\n[A]\nk=v");

        Assert.Equal(["B", "A"], file.GroupNames);
    }
}

public sealed class DesktopEntryTests
{
    private static DesktopEntry Parse(string body, string locale = "C") =>
        DesktopEntry.Parse("[Desktop Entry]\n" + body, locale)!;

    [Fact]
    public void Parse_ReturnsNullWithoutADesktopEntryGroup()
    {
        Assert.Null(DesktopEntry.Parse("[Something Else]\nName=x", "C"));
    }

    [Fact]
    public void Parse_ReadsTheCommonKeys()
    {
        var entry = Parse("""
            Type=Application
            Name=Terminal
            Exec=xterm -ls
            Icon=utilities-terminal
            Path=/tmp
            Terminal=false
            Categories=System;TerminalEmulator;
            """);

        Assert.Equal("Application", entry.Type);
        Assert.Equal("Terminal", entry.Name);
        Assert.Equal("xterm -ls", entry.Exec);
        Assert.Equal("utilities-terminal", entry.Icon);
        Assert.Equal("/tmp", entry.WorkingDirectory);
        Assert.False(entry.Terminal);
        Assert.Equal(["System", "TerminalEmulator"], entry.Categories);
    }

    [Theory]
    [InlineData("de_DE.UTF-8", "Rechner (de_DE)")]
    [InlineData("de_AT.UTF-8", "Rechner (de)")]
    [InlineData("fr_FR", "Calculator")]
    [InlineData("C", "Calculator")]
    [InlineData("sr_RS.UTF-8@latin", "Kalkulator (sr@latin)")]
    public void Parse_PicksTheBestLocalizedName(string locale, string expected)
    {
        var entry = Parse("""
            Name=Calculator
            Name[de]=Rechner (de)
            Name[de_DE]=Rechner (de_DE)
            Name[sr@latin]=Kalkulator (sr@latin)
            """, locale);

        Assert.Equal(expected, entry.Name);
    }

    [Fact]
    public void Parse_UnescapesStringValues()
    {
        var entry = Parse(@"Name=Tab\there\sand\\back");

        Assert.Equal("Tab\there and\\back", entry.Name);
    }

    [Fact]
    public void Parse_ListsKeepEscapedSemicolons()
    {
        var entry = Parse(@"Categories=A\;B;C");

        Assert.Equal(["A;B", "C"], entry.Categories);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("", false)]
    public void Parse_ReadsBooleans(string value, bool expected)
    {
        var entry = Parse("Terminal=" + value);

        Assert.Equal(expected, entry.Terminal);
    }

    [Fact]
    public void IsShownIn_RequiresAnApplication()
    {
        Assert.True(Parse("Type=Application\nName=x").IsShownIn([]));
        Assert.False(Parse("Type=Link\nName=x").IsShownIn([]));
        Assert.False(Parse("Name=x").IsShownIn([]));
    }

    [Fact]
    public void IsShownIn_HonoursNoDisplayAndHidden()
    {
        Assert.False(Parse("Type=Application\nNoDisplay=true").IsShownIn([]));
        Assert.False(Parse("Type=Application\nHidden=true").IsShownIn([]));
    }

    [Fact]
    public void IsShownIn_HonoursOnlyShowIn()
    {
        var entry = Parse("Type=Application\nOnlyShowIn=GNOME;Unity;");

        Assert.True(entry.IsShownIn(["ubuntu", "GNOME"]));
        Assert.False(entry.IsShownIn(["X-Cinnamon"]));
        Assert.False(entry.IsShownIn([]));
    }

    [Fact]
    public void IsShownIn_HonoursNotShowIn()
    {
        var entry = Parse("Type=Application\nNotShowIn=KDE;");

        Assert.False(entry.IsShownIn(["KDE"]));
        Assert.True(entry.IsShownIn(["X-Cinnamon"]));
        Assert.True(entry.IsShownIn([]));
    }
}
