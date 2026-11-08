using System.Runtime.InteropServices;

namespace PanoramicData.SoundPrint;

public sealed partial class Fingerprinter
{
	private static readonly Comparison<Peak> ByBandThenPower = static (a, b) =>
	{
		var c = a.Band.CompareTo(b.Band);
		if (c != 0) { return c; }
		c = b.Power.CompareTo(a.Power);
		if (c != 0) { return c; }
		c = a.Frame.CompareTo(b.Frame);
		return c != 0 ? c : a.Bin.CompareTo(b.Bin);
	};

	private static readonly Comparison<Peak> ByFrameThenBin = static (a, b) =>
	{
		var c = a.Frame.CompareTo(b.Frame);
		return c != 0 ? c : a.Bin.CompareTo(b.Bin);
	};

	private void ProcessCandidates(bool final)
	{
		var lookahead = final ? 0 : _options.PeakNeighbourhoodFrames;
		while (_nextCandidateFrame <= FramesProcessed - 1 - lookahead)
		{
			EvaluateFrame(_nextCandidateFrame);
			_nextCandidateFrame++;
		}
	}

	private void EvaluateFrame(long frame)
	{
		var block = frame / _options.DensityBlockFrames;
		while (_block < block)
		{
			FinalizeBlock();
		}

		var row = _power[(int)(frame % _power.Length)];
		var lastBin = _options.BinCount - 2;
		for (var bin = _options.MinPeakBin; bin <= lastBin; bin++)
		{
			var value = row[bin];
			if (value > _options.MinPeakPower && IsLocalMaximum(frame, bin, value))
			{
				_candidates.Add(new Peak(checked((int)frame), bin, _bandOfBin[bin], value));
			}
		}
	}

	private bool IsLocalMaximum(long frame, int bin, double value)
	{
		var frameRange = _options.PeakNeighbourhoodFrames;
		for (var dt = -frameRange; dt <= frameRange; dt++)
		{
			var other = frame + dt;
			if (other >= 0 && other < FramesProcessed && RowHasDominator(_power[(int)(other % _power.Length)], dt, bin, value))
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>Whether any bin of a neighbouring frame within the bin range beats the candidate. Ties go to the earlier position, so a plateau yields exactly one peak.</summary>
	private bool RowHasDominator(double[] row, int frameOffset, int bin, double value)
	{
		var from = Math.Max(0, bin - _options.PeakNeighbourhoodBins);
		var to = Math.Min(_options.BinCount - 1, bin + _options.PeakNeighbourhoodBins);
		for (var other = from; other <= to; other++)
		{
			if (frameOffset == 0 && other == bin)
			{
				continue;
			}

			var order = row[other].CompareTo(value);
			if (order > 0 || (order == 0 && NeighbourIsEarlier(frameOffset, other, bin)))
			{
				return true;
			}
		}

		return false;
	}

	private void NotifyObserver(ReadOnlySpan<Peak> kept)
	{
		if (PeakObserver is null)
		{
			return;
		}

		var observed = new SpectralPeak[kept.Length];
		for (var i = 0; i < kept.Length; i++)
		{
			observed[i] = new SpectralPeak(kept[i].Frame, kept[i].Bin, kept[i].Power);
		}

		PeakObserver.OnPeaks(observed);
	}

	/// <summary>Ties between equal peaks go to the earlier position in (frame, bin) order, so a plateau yields exactly one peak.</summary>
	internal static bool NeighbourIsEarlier(int frameOffset, int neighbourBin, int bin) =>
		frameOffset < 0 || (frameOffset == 0 && neighbourBin < bin);

	private void FinalizeBlock()
	{
		var span = CollectionsMarshal.AsSpan(_candidates);
		span.Sort(ByBandThenPower);
		var kept = 0;
		var inBand = 0;
		var band = -1;
		for (var i = 0; i < span.Length; i++)
		{
			if (span[i].Band != band)
			{
				band = span[i].Band;
				inBand = 0;
			}

			if (inBand < _options.PeaksPerBandPerBlock)
			{
				span[kept++] = span[i];
				inBand++;
			}
		}

		var keptSpan = span[..kept];
		keptSpan.Sort(ByFrameThenBin);
		NotifyObserver(keptSpan);
		_peaks.AddRange(keptSpan);
		_candidates.Clear();
		var horizon = ((_block + 1) * _options.DensityBlockFrames) - 1;
		_block++;
		EmitAnchors(horizon, final: false);
	}

	private readonly record struct Peak(int Frame, int Bin, int Band, double Power);
}
