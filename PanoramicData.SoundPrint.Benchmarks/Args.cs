namespace PanoramicData.SoundPrint.Benchmarks;

/// <summary>Minimal <c>--key value</c> and <c>--flag</c> command-line parsing.</summary>
internal sealed class Args
{
	private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

	public Args(IEnumerable<string> args)
	{
		var list = args.ToList();
		for (var i = 0; i < list.Count; i++)
		{
			if (!list[i].StartsWith("--", StringComparison.Ordinal))
			{
				continue;
			}

			var key = list[i][2..];
			_values[key] = i + 1 < list.Count && !list[i + 1].StartsWith("--", StringComparison.Ordinal) ? list[++i] : "true";
		}
	}

	public int Int(string key, int fallback) => _values.TryGetValue(key, out var v) ? int.Parse(v, System.Globalization.CultureInfo.InvariantCulture) : fallback;

	public double Double(string key, double fallback) => _values.TryGetValue(key, out var v) ? double.Parse(v, System.Globalization.CultureInfo.InvariantCulture) : fallback;

	public string? String(string key) => _values.GetValueOrDefault(key);

	public bool Flag(string key) => _values.ContainsKey(key);
}
