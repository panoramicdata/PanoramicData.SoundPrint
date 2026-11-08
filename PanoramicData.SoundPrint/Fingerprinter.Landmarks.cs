namespace PanoramicData.SoundPrint;

public sealed partial class Fingerprinter
{
	private void EmitAnchors(long horizon, bool final)
	{
		while (_peakHead < _peaks.Count && (final || _peaks[_peakHead].Frame + _options.MaxDeltaFrames <= horizon))
		{
			EmitPairs(_peakHead);
			_peakHead++;
		}

		if (_peakHead >= CompactThreshold)
		{
			_peaks.RemoveRange(0, _peakHead);
			_peakHead = 0;
		}
	}

	/// <summary>Pairs one anchor peak with the next peaks in its target zone, up to the fan-out.</summary>
	private void EmitPairs(int anchorIndex)
	{
		var anchor = _peaks[anchorIndex];
		var taken = 0;
		for (var j = anchorIndex + 1; j < _peaks.Count && taken < _options.FanOut; j++)
		{
			if (TakeNextPair(anchor, _peaks[j]))
			{
				taken++;
			}
			else if (ExhaustedPartner(_peaks[j].Frame, anchor.Frame))
			{
				break;
			}
		}
	}

	private bool TakeNextPair(Peak anchor, Peak partner)
	{
		var deltaFrames = partner.Frame - anchor.Frame;
		if (deltaFrames > _options.MaxDeltaFrames)
		{
			return false;
		}

		var deltaBins = partner.Bin - anchor.Bin;
		if (deltaFrames < 1 || deltaBins is < -128 or > 127)
		{
			return false;
		}

		AddLandmark(new Landmark(((uint)anchor.Bin << 14) | ((uint)(deltaBins + 128) << 6) | (uint)deltaFrames, anchor.Frame));
		return true;
	}

	private bool ExhaustedPartner(long partnerFrame, long anchorFrame)
	{
		return partnerFrame - anchorFrame > _options.MaxDeltaFrames;
	}

	private void AddLandmark(Landmark landmark)
	{
		if (_batchCount == _batch.Length)
		{
			DeliverBatch();
		}

		_batch[_batchCount++] = landmark;
	}

	private void DeliverBatch()
	{
		if (_batchCount > 0)
		{
			_sink!.Add(_batch.AsSpan(0, _batchCount));
			_batchCount = 0;
		}
	}
}
