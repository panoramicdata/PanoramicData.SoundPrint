// Browser audio glue for the SoundPrint demo. Everything stays on this device: audio is captured or decoded here, handed to the
// WebAssembly fingerprinter as 16-bit PCM, and never uploaded.

let micContext = null;
let micStream = null;
let analyser = null;
let spectrumHandle = 0;

function toInt16Bytes(float32) {
	const out = new Int16Array(float32.length);
	for (let i = 0; i < float32.length; i++) {
		const s = Math.max(-1, Math.min(1, float32[i]));
		out[i] = s < 0 ? s * 0x8000 : s * 0x7fff;
	}
	return new Uint8Array(out.buffer);
}

const workletSource = `
class Tap extends AudioWorkletProcessor {
	constructor() { super(); this.buffer = new Float32Array(4096); this.filled = 0; }
	process(inputs) {
		const channel = inputs[0] && inputs[0][0];
		if (channel) {
			for (let i = 0; i < channel.length; i++) {
				this.buffer[this.filled++] = channel[i];
				if (this.filled === this.buffer.length) {
					this.port.postMessage(this.buffer.slice(0));
					this.filled = 0;
				}
			}
		}
		return true;
	}
}
registerProcessor('tap', Tap);
`;

export async function startMic(dotnetRef, spectrumCanvasId) {
	await stopMic();
	micStream = await navigator.mediaDevices.getUserMedia({
		audio: { echoCancellation: false, noiseSuppression: false, autoGainControl: false }
	});
	micContext = new AudioContext();
	const url = URL.createObjectURL(new Blob([workletSource], { type: 'text/javascript' }));
	await micContext.audioWorklet.addModule(url);
	URL.revokeObjectURL(url);

	const source = micContext.createMediaStreamSource(micStream);
	const tap = new AudioWorkletNode(micContext, 'tap');
	tap.port.onmessage = (event) => {
		dotnetRef.invokeMethodAsync('PushAudio', toInt16Bytes(event.data), micContext.sampleRate);
	};
	source.connect(tap);

	analyser = micContext.createAnalyser();
	analyser.fftSize = 2048;
	source.connect(analyser);
	startSpectrum(spectrumCanvasId);
	return micContext.sampleRate;
}

export async function stopMic() {
	if (spectrumHandle) { cancelAnimationFrame(spectrumHandle); spectrumHandle = 0; }
	if (micStream) { micStream.getTracks().forEach(t => t.stop()); micStream = null; }
	if (micContext) { await micContext.close(); micContext = null; }
	analyser = null;
}

function startSpectrum(canvasId) {
	const canvas = document.getElementById(canvasId);
	if (!canvas || !analyser) { return; }
	const ctx = canvas.getContext('2d');
	const data = new Uint8Array(analyser.frequencyBinCount);
	const draw = () => {
		if (!analyser) { return; }
		analyser.getByteFrequencyData(data);
		// Scroll left by one column, then draw the newest spectrum on the right edge.
		const w = canvas.width, h = canvas.height;
		const image = ctx.getImageData(1, 0, w - 1, h);
		ctx.putImageData(image, 0, 0);
		const usable = Math.floor(data.length * 0.5); // up to about half Nyquist, where music lives
		for (let y = 0; y < h; y++) {
			const v = data[Math.floor((1 - y / h) * usable)];
			ctx.fillStyle = `rgb(${v * 0.3}, ${v * 0.8}, ${v})`;
			ctx.fillRect(w - 1, y, 1, 1);
		}
		spectrumHandle = requestAnimationFrame(draw);
	};
	draw();
}

// Decodes a dropped or chosen file and feeds it to .NET in one-second chunks, as if it were a live stream.
export async function streamFile(inputId, dotnetRef) {
	const input = document.getElementById(inputId);
	const file = input && input.files && input.files[0];
	if (!file) { return null; }
	const context = new AudioContext();
	const decoded = await context.decodeAudioData(await file.arrayBuffer());
	await context.close();

	const rate = decoded.sampleRate;
	const length = decoded.length;
	const mono = new Float32Array(length);
	for (let c = 0; c < decoded.numberOfChannels; c++) {
		const channel = decoded.getChannelData(c);
		for (let i = 0; i < length; i++) { mono[i] += channel[i] / decoded.numberOfChannels; }
	}

	const chunk = rate;
	for (let start = 0; start < length; start += chunk) {
		await dotnetRef.invokeMethodAsync('PushAudio', toInt16Bytes(mono.subarray(start, Math.min(length, start + chunk))), rate);
	}
	return { name: file.name, seconds: length / rate, rate };
}

// Decodes a chosen file completely and returns it as a single block for adding to the catalogue.
export async function decodeFile(inputId) {
	const input = document.getElementById(inputId);
	const file = input && input.files && input.files[0];
	if (!file) { return null; }
	const context = new AudioContext();
	const decoded = await context.decodeAudioData(await file.arrayBuffer());
	await context.close();
	const length = decoded.length;
	const mono = new Float32Array(length);
	for (let c = 0; c < decoded.numberOfChannels; c++) {
		const channel = decoded.getChannelData(c);
		for (let i = 0; i < length; i++) { mono[i] += channel[i] / decoded.numberOfChannels; }
	}
	return { name: file.name, rate: decoded.sampleRate, pcm: toInt16Bytes(mono) };
}

// Draws the constellation: peaks as dots, with chosen landmark pairs as lines. Points are [frame, bin] flat arrays.
export function drawConstellation(canvasId, peaks, pairs, maxFrame, maxBin) {
	const canvas = document.getElementById(canvasId);
	if (!canvas) { return; }
	const ctx = canvas.getContext('2d');
	const w = canvas.width, h = canvas.height;
	ctx.clearRect(0, 0, w, h);
	const x = (frame) => (frame / Math.max(1, maxFrame)) * (w - 12) + 6;
	const y = (bin) => h - 6 - (bin / Math.max(1, maxBin)) * (h - 12);

	ctx.strokeStyle = 'rgba(124,240,178,0.55)';
	ctx.lineWidth = 1;
	for (let i = 0; i + 3 < pairs.length; i += 4) {
		ctx.beginPath();
		ctx.moveTo(x(pairs[i]), y(pairs[i + 1]));
		ctx.lineTo(x(pairs[i + 2]), y(pairs[i + 3]));
		ctx.stroke();
	}

	ctx.fillStyle = '#4cc2ff';
	for (let i = 0; i + 1 < peaks.length; i += 2) {
		ctx.beginPath();
		ctx.arc(x(peaks[i]), y(peaks[i + 1]), 2.2, 0, Math.PI * 2);
		ctx.fill();
	}
}

export function clearCanvas(canvasId) {
	const canvas = document.getElementById(canvasId);
	if (canvas) { canvas.getContext('2d').clearRect(0, 0, canvas.width, canvas.height); }
}

export function createAudioUrl(bytes) {
	return URL.createObjectURL(new Blob([bytes], { type: 'audio/wav' }));
}
