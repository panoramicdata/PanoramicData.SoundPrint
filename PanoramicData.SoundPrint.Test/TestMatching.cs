namespace PanoramicData.SoundPrint.Test;

internal static class TestMatching
{
	/// <summary>Runs a match with the test context's cancellation token, as xUnit analyzers require.</summary>
	public static ValueTask<IReadOnlyList<Match>> RunAsync(this IMatcher matcher, IReadOnlyList<Landmark> query, MatchOptions? options = null) =>
		matcher.MatchAsync(query, options, TestContext.Current.CancellationToken);
}
