using PanoramicData.SoundPrint.SyntheticAudio;

namespace PanoramicData.SoundPrint.Demo.Services;

/// <summary>A track in the demo catalogue. Audio is kept only for tracks the browser generated, so they can be played back.</summary>
/// <param name="Id">The track id used in the fingerprint store.</param>
/// <param name="Title">Display title.</param>
/// <param name="Artist">Display artist.</param>
/// <param name="Source">Where the audio came from.</param>
/// <param name="Seconds">Duration.</param>
/// <param name="Landmarks">How many landmarks the track produced.</param>
/// <param name="Samples">Mono samples at <see cref="DemoCatalogue.SampleRate"/>, or null when not kept.</param>
public sealed record CatalogueTrack(string Id, string Title, string Artist, string Source, double Seconds, int Landmarks, float[]? Samples);

/// <summary>
/// The demo's fingerprint database, held in the browser: an in-memory store plus display information. It starts with synthetic songs
/// generated on the spot (no recordings are shipped) and visitors can add their own files, which are fingerprinted locally and never uploaded.
/// </summary>
public sealed class DemoCatalogue
{
	/// <summary>Sample rate of the audio the catalogue keeps for playback and explanation.</summary>
	public const int SampleRate = 11025;

	private readonly List<CatalogueTrack> _tracks = [];
	private int _nextId = 1;

	/// <summary>Creates the catalogue with an empty store.</summary>
	public DemoCatalogue()
	{
		Store = new InMemoryFingerprintStore(Options);
		Matcher = new Matcher(Store, Options);
	}

	/// <summary>Raised when the catalogue changes.</summary>
	public event Action? Changed;

	/// <summary>The extraction options in use.</summary>
	public FingerprintOptions Options { get; } = new();

	/// <summary>The in-memory store.</summary>
	public InMemoryFingerprintStore Store { get; }

	/// <summary>The matcher over the store.</summary>
	public Matcher Matcher { get; }

	/// <summary>The tracks currently in the catalogue.</summary>
	public IReadOnlyList<CatalogueTrack> Tracks => _tracks;

	/// <summary>Whether the starting catalogue has been generated.</summary>
	public bool Seeded { get; private set; }

	/// <summary>Total landmarks across tracks.</summary>
	public long LandmarkCount => _tracks.Sum(t => (long)t.Landmarks);

	/// <summary>Generates the starting catalogue of synthetic songs, yielding between songs so the page stays responsive.</summary>
	public async Task SeedAsync(IProgress<(int Done, int Total)>? progress = null)
	{
		if (Seeded)
		{
			return;
		}

		const int count = 8;
		for (var i = 0; i < count; i++)
		{
			await Task.Yield();
			var seed = 4000 + i;
			var samples = SyntheticSong.Generate(seed, 40, SampleRate);
			await AddCoreAsync($"Synthetic song {i + 1}", "SoundPrint test generator", "Generated in your browser", samples, SampleRate, keepSamples: true);
			progress?.Report((i + 1, count));
		}

		Seeded = true;
		Changed?.Invoke();
	}

	/// <summary>Fingerprints audio and adds it to the catalogue.</summary>
	public async Task<CatalogueTrack> AddAsync(string title, string artist, float[] samples, int rate)
	{
		var track = await AddCoreAsync(title, artist, "Added by you (stays in this browser)", samples, rate, keepSamples: false);
		Changed?.Invoke();
		return track;
	}

	/// <summary>Removes a track.</summary>
	public void Remove(string id)
	{
		if (Store.RemoveTrack(id))
		{
			_tracks.RemoveAll(t => t.Id == id);
			Changed?.Invoke();
		}
	}

	/// <summary>Display information for a track id.</summary>
	public CatalogueTrack? Find(string id) => _tracks.Find(t => t.Id == id);

	private async Task<CatalogueTrack> AddCoreAsync(string title, string artist, string source, float[] samples, int rate, bool keepSamples)
	{
		await Task.Yield();
		var id = $"track-{_nextId++}";
		var landmarks = Fingerprinter.Fingerprint(samples, rate, Options);
		Store.AddTrack(id, landmarks);
		var track = new CatalogueTrack(id, title, artist, source, (double)samples.Length / rate, landmarks.Count, keepSamples ? samples : null);
		_tracks.Add(track);
		return track;
	}

}
