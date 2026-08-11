namespace Yolla.Harvester;

/// <summary>
/// Basit komut satırı argümanı okuyucu: <c>--anahtar değer</c> ve <c>--bayrak</c> biçimlerini destekler.
/// </summary>
public sealed class CommandLineArgs
{
    private readonly Dictionary<string, string?> _values = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _positional = [];

    public CommandLineArgs(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];

            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                _positional.Add(arg);
                continue;
            }

            var key = arg[2..];
            var hasValue = i + 1 < args.Count && !args[i + 1].StartsWith("--", StringComparison.Ordinal);

            _values[key] = hasValue ? args[++i] : null;
        }
    }

    public IReadOnlyList<string> Positional => _positional;

    public string? GetValue(string key, string? defaultValue = null) =>
        _values.TryGetValue(key, out var value) && value is not null ? value : defaultValue;

    public bool HasFlag(string key) => _values.ContainsKey(key);
}
