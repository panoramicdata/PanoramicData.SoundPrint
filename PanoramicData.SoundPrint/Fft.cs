namespace PanoramicData.SoundPrint;

/// <summary>In-place radix-2 complex FFT with tables built once, so transforms allocate nothing.</summary>
internal sealed class Fft
{
	private readonly int _size;
	private readonly double[] _cos;
	private readonly double[] _sin;
	private readonly int[] _bitReverse;

	public Fft(int size)
	{
		_size = size;
		_cos = new double[size / 2];
		_sin = new double[size / 2];
		for (var i = 0; i < size / 2; i++)
		{
			var angle = -2 * Math.PI * i / size;
			_cos[i] = DeterministicMath.Cos(angle);
			_sin[i] = DeterministicMath.Sin(angle);
		}

		_bitReverse = new int[size];
		var bits = System.Numerics.BitOperations.Log2((uint)size);
		for (var i = 0; i < size; i++)
		{
			var reversed = 0;
			for (var b = 0; b < bits; b++)
			{
				reversed |= ((i >> b) & 1) << (bits - 1 - b);
			}

			_bitReverse[i] = reversed;
		}
	}

	public void Transform(double[] re, double[] im)
	{
		for (var i = 0; i < _size; i++)
		{
			var j = _bitReverse[i];
			if (i < j)
			{
				(re[i], re[j]) = (re[j], re[i]);
				(im[i], im[j]) = (im[j], im[i]);
			}
		}

		for (var length = 2; length <= _size; length <<= 1)
		{
			var half = length >> 1;
			var step = _size / length;
			for (var start = 0; start < _size; start += length)
			{
				for (var k = 0; k < half; k++)
				{
					var wr = _cos[k * step];
					var wi = _sin[k * step];
					var a = start + k;
					var b = a + half;
					var tr = (re[b] * wr) - (im[b] * wi);
					var ti = (re[b] * wi) + (im[b] * wr);
					re[b] = re[a] - tr;
					im[b] = im[a] - ti;
					re[a] += tr;
					im[a] += ti;
				}
			}
		}
	}
}
