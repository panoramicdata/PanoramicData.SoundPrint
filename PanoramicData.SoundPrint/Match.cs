namespace PanoramicData.SoundPrint;

/// <summary>
/// A query's match against one stretch of one track. A query window that straddles two songs produces two matches, one per song,
/// each with the span of the query it explains.
/// </summary>
/// <param name="TrackId">The caller's id for the matched track.</param>
/// <param name="OffsetInTrack">Track time minus query time: the audio at query time <c>q</c> is at track time <c>q + OffsetInTrack</c>. Negative when the query began before the track did, as when a window straddles a song start.</param>
/// <param name="QueryStart">The first query time that the matching landmarks support, on the query's own clock (frame times of the landmarks passed in).</param>
/// <param name="QueryEnd">The last query time that the matching landmarks support, including the span of the last landmark pair.</param>
/// <param name="Confidence">Calibrated probability-like score from 0 to 1 that the match is correct.</param>
/// <param name="Votes">Landmarks that agreed on this track and offset (within the offset tolerance).</param>
/// <param name="Score">Significance of <paramref name="Votes"/> against the track's background level, as the negative base-10 logarithm of a Poisson tail probability.</param>
public sealed record Match(
	string TrackId,
	TimeSpan OffsetInTrack,
	TimeSpan QueryStart,
	TimeSpan QueryEnd,
	double Confidence,
	int Votes,
	double Score)
{
	/// <summary>Where in the track the matched span begins.</summary>
	public TimeSpan TrackStart => QueryStart + OffsetInTrack;

	/// <summary>Where in the track the matched span ends.</summary>
	public TimeSpan TrackEnd => QueryEnd + OffsetInTrack;
}
