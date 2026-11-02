using System.Text;

namespace ReProgman.Model;

/// <summary>
/// The parts of a freedesktop.org .desktop file that Program Manager needs:
/// what to call the item, how to start it, which icon to show and which group
/// it belongs to.
/// </summary>
public sealed class DesktopEntry
{
    private const string MainGroup = "Desktop Entry";

    public string? Type { get; private init; }
    public string Name { get; private init; } = string.Empty;
    public string? Exec { get; private init; }
    public string? Icon { get; private init; }
    public string? WorkingDirectory { get; private init; }
    public bool Terminal { get; private init; }
    public bool NoDisplay { get; private init; }
    public bool Hidden { get; private init; }
    public IReadOnlyList<string> Categories { get; private init; } = [];
    public IReadOnlyList<string> OnlyShowIn { get; private init; } = [];
    public IReadOnlyList<string> NotShowIn { get; private init; } = [];

    /// <param name="text">The file contents.</param>
    /// <param name="locale">A POSIX locale such as <c>de_DE.UTF-8</c>, used for localized names.</param>
    public static DesktopEntry? Parse(string text, string locale)
    {
        var file = KeyFile.Parse(text);
        if (!file.HasGroup(MainGroup))
        {
            return null;
        }

        string? Raw(string key) => file.Get(MainGroup, key);
        string? String(string key) => Raw(key) is { } value ? Unescape(value) : null;
        bool Boolean(string key) => string.Equals(Raw(key), "true", StringComparison.Ordinal);
        IReadOnlyList<string> List(string key) => Raw(key) is { } value ? SplitList(value) : [];

        return new DesktopEntry
        {
            Type = String("Type"),
            Name = LocalizedString(file, "Name", locale) ?? string.Empty,
            Exec = String("Exec"),
            Icon = LocalizedString(file, "Icon", locale),
            WorkingDirectory = String("Path"),
            Terminal = Boolean("Terminal"),
            NoDisplay = Boolean("NoDisplay"),
            Hidden = Boolean("Hidden"),
            Categories = List("Categories"),
            OnlyShowIn = List("OnlyShowIn"),
            NotShowIn = List("NotShowIn"),
        };
    }

    /// <summary>
    /// Whether a menu would list this entry on a desktop identified by
    /// <c>XDG_CURRENT_DESKTOP</c> (already split at ':').
    /// </summary>
    public bool IsShownIn(IReadOnlyCollection<string> currentDesktops)
    {
        if (!string.Equals(Type, "Application", StringComparison.Ordinal) || NoDisplay || Hidden)
        {
            return false;
        }

        if (OnlyShowIn.Count > 0 && !OnlyShowIn.Any(currentDesktops.Contains))
        {
            return false;
        }

        return !NotShowIn.Any(currentDesktops.Contains);
    }

    /// <summary>
    /// Looks up <c>Key[locale]</c> in the order the spec gives:
    /// lang_COUNTRY@MODIFIER, lang_COUNTRY, lang@MODIFIER, lang, then the plain key.
    /// </summary>
    private static string? LocalizedString(KeyFile file, string key, string locale)
    {
        foreach (var candidate in LocaleCandidates(locale))
        {
            if (file.Get(MainGroup, $"{key}[{candidate}]") is { } localized)
            {
                return Unescape(localized);
            }
        }

        return file.Get(MainGroup, key) is { } value ? Unescape(value) : null;
    }

    private static IEnumerable<string> LocaleCandidates(string locale)
    {
        // POSIX form: lang_COUNTRY.ENCODING@MODIFIER. The encoding never takes part
        // in the match.
        var modifierAt = locale.IndexOf('@');
        var modifier = modifierAt >= 0 ? locale[(modifierAt + 1)..] : null;
        var rest = modifierAt >= 0 ? locale[..modifierAt] : locale;
        var dot = rest.IndexOf('.');
        if (dot >= 0)
        {
            rest = rest[..dot];
        }

        var underscore = rest.IndexOf('_');
        var language = underscore >= 0 ? rest[..underscore] : rest;
        var country = underscore >= 0 ? rest[(underscore + 1)..] : null;

        if (language.Length == 0 || language is "C" or "POSIX")
        {
            yield break;
        }

        if (country is not null && modifier is not null)
        {
            yield return $"{language}_{country}@{modifier}";
        }

        if (country is not null)
        {
            yield return $"{language}_{country}";
        }

        if (modifier is not null)
        {
            yield return $"{language}@{modifier}";
        }

        yield return language;
    }

    private static string Unescape(string value)
    {
        if (!value.Contains('\\'))
        {
            return value;
        }

        var builder = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] != '\\' || i + 1 == value.Length)
            {
                builder.Append(value[i]);
                continue;
            }

            i++;
            builder.Append(value[i] switch
            {
                's' => ' ',
                'n' => '\n',
                't' => '\t',
                'r' => '\r',
                '\\' => '\\',
                // Unknown escapes are kept verbatim; Exec relies on that for its
                // own quoting layer.
                var other => "\\" + other,
            });
        }

        return builder.ToString();
    }

    private static IReadOnlyList<string> SplitList(string value)
    {
        var items = new List<string>();
        var current = new StringBuilder();
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '\\' && i + 1 < value.Length && value[i + 1] == ';')
            {
                current.Append(';');
                i++;
            }
            else if (value[i] == ';')
            {
                items.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(value[i]);
            }
        }

        items.Add(current.ToString());
        return items.Select(Unescape).Where(i => i.Length > 0).ToArray();
    }
}
