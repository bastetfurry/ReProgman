using ReProgman.Model;

namespace ReProgman.Model.Tests;

public sealed class DesktopExecTests
{
    private static IReadOnlyList<string> Expand(string exec, string? icon = null, string name = "App", string file = "/apps/app.desktop") =>
        DesktopExec.Expand(exec, icon, name, file);

    [Fact]
    public void Expand_SplitsAtSpaces()
    {
        Assert.Equal(["gimp", "-n"], Expand("gimp -n"));
    }

    [Fact]
    public void Expand_CollapsesRepeatedSpaces()
    {
        Assert.Equal(["a", "b"], Expand("a   b "));
    }

    [Fact]
    public void Expand_KeepsQuotedArgumentsTogether()
    {
        Assert.Equal(["/opt/My App/run", "--x"], Expand("\"/opt/My App/run\" --x"));
    }

    [Fact]
    public void Expand_UnescapesInsideQuotes()
    {
        Assert.Equal(["say \"hi\" $HOME `x` \\"], Expand(@"""say \""hi\"" \$HOME \`x\` \\"""));
    }

    [Theory]
    [InlineData("%f")]
    [InlineData("%F")]
    [InlineData("%u")]
    [InlineData("%U")]
    [InlineData("%d")]
    [InlineData("%D")]
    [InlineData("%n")]
    [InlineData("%N")]
    [InlineData("%v")]
    [InlineData("%m")]
    public void Expand_DropsFileFieldCodes_BecauseNothingIsOpened(string code)
    {
        Assert.Equal(["app", "--new"], Expand($"app {code} --new"));
    }

    [Fact]
    public void Expand_RemovesAFieldCodeEmbeddedInAnArgument()
    {
        Assert.Equal(["app", "--file="], Expand("app --file=%f"));
    }

    [Fact]
    public void Expand_ReplacesIconCodeWithIconOption()
    {
        Assert.Equal(["app", "--icon", "app-icon"], Expand("app %i", icon: "app-icon"));
    }

    [Fact]
    public void Expand_DropsIconCodeWithoutIcon()
    {
        Assert.Equal(["app"], Expand("app %i"));
    }

    [Fact]
    public void Expand_ReplacesNameAndLocationCodes()
    {
        Assert.Equal(["app", "My App", "/x/app.desktop"], Expand("app %c %k", name: "My App", file: "/x/app.desktop"));
    }

    [Fact]
    public void Expand_TurnsDoublePercentIntoOne()
    {
        Assert.Equal(["printf", "100%"], Expand("printf 100%%"));
    }

    [Fact]
    public void Expand_EmptyCommandGivesNoArguments()
    {
        Assert.Empty(Expand("   "));
    }

    [Fact]
    public void Expand_UnterminatedQuoteTakesTheRest()
    {
        Assert.Equal(["a", "b c"], Expand("a \"b c"));
    }
}
