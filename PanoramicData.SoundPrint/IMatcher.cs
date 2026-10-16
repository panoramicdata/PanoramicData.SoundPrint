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
	ValueTask<IReadOnlyList<Match>> MatchAsync(IReadOnlyList<Landmark> query, MatchOptions? options = null, CancellationToken cancellationToken = default);
}
