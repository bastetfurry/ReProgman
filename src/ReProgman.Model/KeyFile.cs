namespace ReProgman.Model;

/// <summary>
/// Reader for the freedesktop.org key file format shared by .desktop files and
/// icon theme index.theme files.
/// </summary>
/// <remarks>
/// Why not <see cref="IniDocument"/>: key files are case-sensitive, comment with
/// '#' instead of ';', and are only ever read, never written back.
/// </remarks>
public sealed class KeyFile
{
    private readonly List<string> _groupOrder = [];
    private readonly Dictionary<string, Dictionary<string, string>> _groups = new(StringComparer.Ordinal);

    public IReadOnlyList<string> GroupNames => _groupOrder;

    public static KeyFile Parse(string text)
    {
        var file = new KeyFile();
        Dictionary<string, string>? current = null;
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                var name = line[1..^1];
                if (!file._groups.TryGetValue(name, out current))
                {
                    current = new Dictionary<string, string>(StringComparer.Ordinal);
                    file._groups[name] = current;
                    file._groupOrder.Add(name);
                }

                continue;
            }

            var separator = line.IndexOf('=');
            if (separator > 0 && current is not null)
            {
                // The spec forbids duplicate keys; when a broken file has them anyway,
                // keeping the first matches what GLib does.
                current.TryAdd(line[..separator].TrimEnd(), line[(separator + 1)..].TrimStart());
            }
        }

        return file;
    }

    public string? Get(string group, string key) =>
        _groups.TryGetValue(group, out var values) && values.TryGetValue(key, out var value) ? value : null;

    public bool HasGroup(string group) => _groups.ContainsKey(group);
}
