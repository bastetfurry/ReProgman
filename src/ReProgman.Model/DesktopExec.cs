using System.Text;

namespace ReProgman.Model;

/// <summary>
/// Turns the Exec key of a .desktop file into an argument vector, following the
/// quoting and field code rules of the Desktop Entry Specification.
/// </summary>
public static class DesktopExec
{
    /// <summary>
    /// Expands an (already string-unescaped) Exec value for a plain launch, i.e.
    /// without files or URLs to open.
    /// </summary>
    public static IReadOnlyList<string> Expand(string exec, string? icon, string name, string desktopFilePath)
    {
        var arguments = new List<string>();
        foreach (var (token, quoted) in Tokenize(exec))
        {
            if (quoted)
            {
                // Field codes are not allowed inside quotes, so the text is literal.
                arguments.Add(token);
                continue;
            }

            switch (token)
            {
                case "%i":
                    if (!string.IsNullOrEmpty(icon))
                    {
                        arguments.Add("--icon");
                        arguments.Add(icon);
                    }

                    continue;
                case "%c":
                    arguments.Add(name);
                    continue;
                case "%k":
                    arguments.Add(desktopFilePath);
                    continue;
            }

            var expanded = ExpandInline(token, name, desktopFilePath);
            // An argument that consisted of nothing but a file code disappears,
            // instead of being passed as an empty string.
            if (expanded.Length > 0 || token.Length == 0)
            {
                arguments.Add(expanded);
            }
        }

        return arguments;
    }

    private static string ExpandInline(string token, string name, string desktopFilePath)
    {
        if (!token.Contains('%'))
        {
            return token;
        }

        var builder = new StringBuilder(token.Length);
        for (var i = 0; i < token.Length; i++)
        {
            if (token[i] != '%' || i + 1 == token.Length)
            {
                builder.Append(token[i]);
                continue;
            }

            i++;
            switch (token[i])
            {
                case '%':
                    builder.Append('%');
                    break;
                case 'c':
                    builder.Append(name);
                    break;
                case 'k':
                    builder.Append(desktopFilePath);
                    break;
                default:
                    // %f %u %d %n %v %m and friends: nothing is being opened, and the
                    // deprecated ones are to be dropped anyway.
                    break;
            }
        }

        return builder.ToString();
    }

    private static IEnumerable<(string Token, bool Quoted)> Tokenize(string exec)
    {
        var current = new StringBuilder();
        var inToken = false;
        var quoted = false;
        var inQuotes = false;

        for (var i = 0; i < exec.Length; i++)
        {
            var c = exec[i];
            if (inQuotes)
            {
                if (c == '\\' && i + 1 < exec.Length && exec[i + 1] is '"' or '`' or '$' or '\\')
                {
                    current.Append(exec[++i]);
                }
                else if (c == '"')
                {
                    inQuotes = false;
                }
                else
                {
                    current.Append(c);
                }

                continue;
            }

            if (c == ' ' || c == '\t')
            {
                if (inToken)
                {
                    yield return (current.ToString(), quoted);
                    current.Clear();
                    inToken = false;
                    quoted = false;
                }

                continue;
            }

            inToken = true;
            if (c == '"')
            {
                inQuotes = true;
                quoted = true;
            }
            else
            {
                current.Append(c);
            }
        }

        if (inToken)
        {
            yield return (current.ToString(), quoted);
        }
    }
}
