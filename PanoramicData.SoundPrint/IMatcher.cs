namespace PanoramicData.SoundPrint;

/// <summary>Finds which stored tracks a query's landmarks match, and where. Implementations are thread-safe.</summary>
public interface IMatcher
{
	/// <summary>
	/// Matches a query. Returns every track and offset with enough agreeing landmarks, strongest first, so a query window covering the
	/// end of one song and the start of the next reports both.
	/// </summary>
	/// <param name="query">Landmarks of the query audio, from <see cref="IFingerprinter"/>. Their frame numbers define the query clock.</param>
	/// <param name="options">Per-call overrides; the matcher's defaults when null.</param>
	/// <param name="cancellationToken">Cancels the operation.</param>
	/// <returns>The matches, most confident first; empty when nothing matched.</returns>
	ValueTask<IReadOnlyList<Match>> MatchAsync(IReadOnlyList<Landmark> query, MatchOptions? options, CancellationToken cancellationToken);
}

/// <summary>Convenience overloads for <see cref="IMatcher"/>.</summary>
public static class MatcherExtensions
{
	/// <summary>Matches a query with the matcher's default options and no cancellation.</summary>
	/// <param name="matcher">The matcher.</param>
	/// <param name="query">Landmarks of the query audio.</param>
	/// <returns>The matches, most confident first; empty when nothing matched.</returns>
	public static ValueTask<IReadOnlyList<Match>> MatchAsync(this IMatcher matcher, IReadOnlyList<Landmark> query)
	{
		ArgumentNullException.ThrowIfNull(matcher);
		return matcher.MatchAsync(query, null, CancellationToken.None);
	}
}
