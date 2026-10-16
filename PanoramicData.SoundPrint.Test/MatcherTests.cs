using PanoramicData.SoundPrint.TestAudio;

namespace PanoramicData.SoundPrint.Test;

/// <summary>Builds one database of synthetic songs that all matcher tests share.</summary>
public sealed class MatcherFixture : IDisposable
{
	public const int Rate = 8000;
	public const int TrackCount = 12;
	public const double Seconds = 40;

	public FingerprintOptions Options { get; } = new();
	public InMemoryFingerprintStore Store { get; }
	public Matcher Matcher { get; }
	public float[][] Songs { get; }

	public MatcherFixture()
	{
		Store = new InMemoryFingerprintStore(Options);
		Songs = [.. Enumerable.Range(0, TrackCount).Select(i => SyntheticSong.Generate(100 + i, Seconds, Rate))];
		for (var i = 0; i < Songs.Length; i++)
		{
			Store.AddTrack($"song-{i}", Fingerprinter.Fingerprint(Songs[i], Rate, Options));
		}

		Matcher = new Matcher(Store, Options);
	}

	public IReadOnlyList<Landmark> Query(float[] audio, int rate = Rate) => Fingerprinter.Fingerprint(audio, rate, Options);

	public void Dispose() => Store.Dispose();
}

public class MatcherTests(MatcherFixture fixture) : IClassFixture<MatcherFixture>
{
	private static readonly double[] ExpectedLoopStarts = [0.0, 28.0];

	private static float[] Clip(float[] song, double startSeconds, double lengthSeconds) =>
		song.AsSpan((int)(startSeconds * MatcherFixture.Rate), (int)(lengthSeconds * MatcherFixture.Rate)).ToArray();

	[Theory]
	[InlineData(3, 5.0, 10.0)]
	[InlineData(7, 0.0, 8.0)]
	[InlineData(11, 25.5, 12.0)]
	public async Task CleanClip_MatchesItsTrackAtTheRightPosition(int song, double start, double length)
	{
		var matches = await fixture.Matcher.RunAsync(fixture.Query(Clip(fixture.Songs[song], start, length)));

		var best = matches[0];
		best.TrackId.Should().Be($"song-{song}");
		best.TrackStart.TotalSeconds.Should().BeApproximately(start, 0.5);
		best.Confidence.Should().BeGreaterThan(0.9);
		best.QueryStart.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
		best.QueryEnd.Should().BeLessThanOrEqualTo(TimeSpan.FromSeconds(length + 3));
		best.Votes.Should().BeGreaterThan(20);
	}

	[Fact]
	public async Task OffsetInTrack_IsTrackTimeMinusQueryTime()
	{
		var matches = await fixture.Matcher.RunAsync(fixture.Query(Clip(fixture.Songs[2], 10, 10)));
		matches[0].OffsetInTrack.TotalSeconds.Should().BeApproximately(10, 0.1);
	}

	[Fact]
	public async Task ClipWithNoise_StillMatches()
	{
		var noisy = Degradations.AddWhiteNoise(Clip(fixture.Songs[4], 12, 10), 10, 1);
		var matches = await fixture.Matcher.RunAsync(fixture.Query(noisy));
		matches[0].TrackId.Should().Be("song-4");
	}

	[Fact]
	public async Task ClipAtAnotherSampleRate_StillMatches()
	{
		var clip44 = SyntheticSong.Generate(100 + 5, MatcherFixture.Seconds, 44100);
		var clip = clip44.AsSpan(10 * 44100, 10 * 44100).ToArray();
		var matches = await fixture.Matcher.RunAsync(fixture.Query(clip, 44100));
		matches.Should().NotBeEmpty();
		matches[0].TrackId.Should().Be("song-5");
	}

	[Fact]
	public async Task StraddlingClip_ReportsBothSongsInOrder()
	{
		// Last 6 s of song 1 followed by the first 6 s of song 8, with a short crossfade.
		var a = fixture.Songs[1];
		var b = fixture.Songs[8];
		var rate = MatcherFixture.Rate;
		var spliced = Degradations.Splice(a.AsSpan(a.Length - (6 * rate)), b, 6 * rate, rate / 4, 6 * rate);

		var matches = await fixture.Matcher.RunAsync(fixture.Query(spliced));

		// Synthetic songs repeat their phrases, so a track can legitimately match at more than one offset; pick the occurrence at the track end / start.
		var first = matches.Single(m => m.TrackId == "song-1" && Math.Abs(m.TrackEnd.TotalSeconds - MatcherFixture.Seconds) < 3);
		var second = matches.Single(m => m.TrackId == "song-8" && m.TrackStart.TotalSeconds < 3);
		first.QueryStart.Should().BeLessThan(second.QueryStart);
		first.QueryEnd.Should().BeLessThan(TimeSpan.FromSeconds(8));
		second.QueryStart.Should().BeGreaterThan(TimeSpan.FromSeconds(4));
	}

	[Fact]
	public async Task UnknownSong_DoesNotProduceAConfidentMatch()
	{
		var unknown = SyntheticSong.Generate(9999, 10, MatcherFixture.Rate);
		var matches = await fixture.Matcher.RunAsync(fixture.Query(unknown));
		matches.Should().NotContain(m => m.Confidence > 0.5);
	}

	[Fact]
	public async Task Silence_MatchesNothing() =>
		(await fixture.Matcher.RunAsync(fixture.Query(new float[MatcherFixture.Rate * 5]))).Should().BeEmpty();

	[Fact]
	public async Task LargeQuery_WithMoreThanTheInitialBufferOfHits_StillMatches()
	{
		// A long recording of one song against a store holding it produces far more than 4096 hits.
		var long90 = SyntheticSong.Generate(321, 90, MatcherFixture.Rate);
		using var store = new InMemoryFingerprintStore(fixture.Options);
		var landmarks = fixture.Query(long90);
		store.AddTrack("long", landmarks);
		var matcher = new Matcher(store);

		landmarks.Count.Should().BeGreaterThan(4096);
		var matches = await matcher.RunAsync(landmarks);

		matches[0].TrackId.Should().Be("long");
		matches[0].Votes.Should().BeGreaterThan(4096);
	}

	[Fact]
	public async Task EmptyQuery_MatchesNothing() =>
		(await fixture.Matcher.RunAsync([])).Should().BeEmpty();

	[Fact]
	public async Task RepeatedSection_ReportsEachOccurrence()
	{
		// A track that plays the same 8 s phrase twice, with filler between, in its own store.
		var phrase = Clip(fixture.Songs[6], 5, 8);
		var filler = Clip(fixture.Songs[9], 0, 20);
		var track = phrase.Concat(filler).Concat(phrase).ToArray();
		using var store = new InMemoryFingerprintStore(fixture.Options);
		store.AddTrack("loop", fixture.Query(track));
		var matcher = new Matcher(store, fixture.Options);

		var matches = await matcher.RunAsync(fixture.Query(phrase));

		matches.Should().HaveCount(2);
		matches.Select(m => m.TrackStart.TotalSeconds).Order().Should().BeEquivalentTo(
			ExpectedLoopStarts,
			options => options.Using<double>(ctx => ctx.Subject.Should().BeApproximately(ctx.Expectation, 0.5)).WhenTypeIs<double>());
	}

	[Fact]
	public async Task RemovedTrack_NoLongerMatches()
	{
		using var store = new InMemoryFingerprintStore(fixture.Options);
		store.AddTrack("x", fixture.Query(fixture.Songs[0]));
		var matcher = new Matcher(store, fixture.Options);
		var query = fixture.Query(Clip(fixture.Songs[0], 5, 10));
		(await matcher.RunAsync(query)).Should().NotBeEmpty();
		store.RemoveTrack("x");
		(await matcher.RunAsync(query)).Should().BeEmpty();
	}

	[Fact]
	public async Task MinConfidence_FiltersWeakMatches()
	{
		var query = fixture.Query(Clip(fixture.Songs[3], 5, 10));
		var matches = await fixture.Matcher.RunAsync(query, new MatchOptions { MinConfidence = 0.999999 });
		matches.Should().OnlyContain(m => m.Confidence >= 0.999999);
	}

	[Fact]
	public async Task MaxMatches_CapsTheResult()
	{
		var query = fixture.Query(Clip(fixture.Songs[3], 5, 10));
		var matches = await fixture.Matcher.RunAsync(query, new MatchOptions { MinVotes = 1, MaxMatches = 1 });
		matches.Should().HaveCount(1);
	}

	[Fact]
	public async Task Results_AreOrderedByConfidence()
	{
		var query = fixture.Query(Clip(fixture.Songs[3], 5, 10));
		var matches = await fixture.Matcher.RunAsync(query, new MatchOptions { MinVotes = 1, MaxMatches = 5 });
		matches.Select(m => m.Confidence).Should().BeInDescendingOrder();
	}

	[Fact]
	public async Task ConcurrentMatches_AllGiveTheRightAnswer()
	{
		var tasks = Enumerable.Range(0, 24).Select(i => Task.Run(async () =>
		{
			var song = i % MatcherFixture.TrackCount;
			var matches = await fixture.Matcher.RunAsync(fixture.Query(Clip(fixture.Songs[song], 6, 8)));
			return matches[0].TrackId == $"song-{song}";
		}));
		(await Task.WhenAll(tasks)).Should().OnlyContain(ok => ok);
	}

	[Fact]
	public async Task OptionsOverride_IsValidated()
	{
		var query = fixture.Query(Clip(fixture.Songs[3], 5, 10));
		var act = async () => await fixture.Matcher.RunAsync(query, new MatchOptions { MinVotes = 0 });
		await act.Should().ThrowAsync<ArgumentException>();
	}

	[Fact]
	public void Constructor_RejectsAStoreWithDifferentParameters()
	{
		using var store = new InMemoryFingerprintStore(new FingerprintOptions { FanOut = 2 });
		var act = () => new Matcher(store, new FingerprintOptions());
		act.Should().Throw<ArgumentException>();
	}

	[Fact]
	public async Task ShortOverloads_UseTheDefaultOptions()
	{
		var query = fixture.Query(Clip(fixture.Songs[3], 5, 10));
		(await fixture.Matcher.MatchAsync(query))[0].TrackId.Should().Be("song-3");
		IMatcher viaInterface = fixture.Matcher;
		(await viaInterface.MatchAsync(query))[0].TrackId.Should().Be("song-3");
	}

	[Fact]
	public void Constructor_RejectsNullOptions()
	{
		using var store = new InMemoryFingerprintStore();
		var nullFingerprintOptions = () => new Matcher(store, null!, new MatchOptions());
		nullFingerprintOptions.Should().Throw<ArgumentNullException>();
		var nullMatchOptions = () => new Matcher(store, new FingerprintOptions(), null!);
		nullMatchOptions.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public void Constructor_RejectsNullStore()
	{
		var act = () => new Matcher(null!);
		act.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public async Task MatchAsync_RejectsNullQuery()
	{
		var act = async () => await fixture.Matcher.RunAsync(null!);
		await act.Should().ThrowAsync<ArgumentNullException>();
	}

	[Fact]
	public async Task VotesBelowMinimum_ProduceNoMatch()
	{
		var query = fixture.Query(Clip(fixture.Songs[3], 5, 0.5));
		var matches = await fixture.Matcher.RunAsync(query, new MatchOptions { MinVotes = 100000 });
		matches.Should().BeEmpty();
	}

	[Fact]
	public async Task NegativeOffset_IsReportedWhenTheQueryStartsBeforeTheTrack()
	{
		// Query = 9 s of other music, then song 2 from 5 s: the query clock runs ahead of the track clock by 4 s.
		var query = Clip(fixture.Songs[5], 0, 9).Concat(Clip(fixture.Songs[2], 5, 6)).ToArray();
		var matches = await fixture.Matcher.RunAsync(fixture.Query(query));
		var match = matches.First(m => m.TrackId == "song-2");
		match.OffsetInTrack.TotalSeconds.Should().BeApproximately(-4, 0.2);
		match.TrackStart.TotalSeconds.Should().BeApproximately(5, 1);
	}

	[Fact]
	public void PoissonScore_MatchesKnownValues()
	{
		// P(X >= 1) for lambda = 1 is 1 - e^-1.
		OffsetVoting.PoissonScore(1, 1.0).Should().BeApproximately(-Math.Log10(1 - Math.Exp(-1)), 1e-9);
		// P(X >= 0) = 1.
		OffsetVoting.PoissonScore(0, 3.0).Should().Be(0);
		// P(X >= 3) for lambda = 2: 1 - e^-2 (1 + 2 + 2).
		OffsetVoting.PoissonScore(3, 2.0).Should().BeApproximately(-Math.Log10(1 - (Math.Exp(-2) * 5)), 1e-9);
	}

	[Fact]
	public void PoissonScore_RisesWithVotesAndFallsWithBackground()
	{
		OffsetVoting.PoissonScore(20, 0.5).Should().BeGreaterThan(OffsetVoting.PoissonScore(10, 0.5));
		OffsetVoting.PoissonScore(10, 5.0).Should().BeLessThan(OffsetVoting.PoissonScore(10, 0.5));
	}
}

public class MatchOptionsTests
{
	public static TheoryData<string, MatchOptions> Invalid => new()
	{
		{ "MinVotes", new MatchOptions { MinVotes = 0 } },
		{ "OffsetToleranceFrames", new MatchOptions { OffsetToleranceFrames = -1 } },
		{ "SuppressionFrames", new MatchOptions { SuppressionFrames = 0 } },
		{ "MaxMatches", new MatchOptions { MaxMatches = 0 } },
		{ "MinConfidence", new MatchOptions { MinConfidence = -0.1 } },
		{ "MinConfidence", new MatchOptions { MinConfidence = 1.5 } },
		{ "MinBackground", new MatchOptions { MinBackground = 0 } },
		{ "ConfidenceMidpoint", new MatchOptions { ConfidenceMidpoint = double.NaN } },
		{ "ConfidenceSlope", new MatchOptions { ConfidenceSlope = 0 } }
	};

	[Fact]
	public void Defaults_AreValid() => new MatchOptions().Validate();

	[Theory]
	[MemberData(nameof(Invalid))]
	public void Validate_RejectsInvalidValue(string parameter, MatchOptions options)
	{
		var act = options.Validate;
		act.Should().Throw<ArgumentException>().Which.ParamName.Should().Be(parameter);
	}
}
