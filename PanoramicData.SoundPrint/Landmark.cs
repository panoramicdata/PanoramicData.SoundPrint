namespace PanoramicData.SoundPrint;

/// <summary>
/// One landmark: the hash of a pair of spectral peaks and the frame at which the first (anchor) peak occurs.
/// </summary>
/// <param name="Hash">The 32-bit landmark hash. Layout: bits 22..14 anchor frequency bin, bits 13..6 frequency delta plus 128, bits 5..0 frame delta.</param>
/// <param name="AnchorFrame">Frame index of the anchor peak, counted from the start of the stream. One frame lasts <see cref="FingerprintOptions.FrameDuration"/>.</param>
public readonly record struct Landmark(uint Hash, int AnchorFrame)
{
	/// <summary>The number of frames between the anchor peak and its partner peak, as encoded in the hash (1 to 63).</summary>
	public int DeltaFrames => (int)(Hash & 0x3F);
}
