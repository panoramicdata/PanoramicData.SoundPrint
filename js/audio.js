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

// ---------------------------------------------------------------------------------------------------------------------------------
// "How it works" figure: spectrogram heat map with labelled axes, peaks, landmark pairs and a selected anchor's target zone.
// ---------------------------------------------------------------------------------------------------------------------------------

const explainState = {};

function decodeImage(value) {
	if (typeof value !== 'string') { return Uint8Array.from(value); }
	// Base64, possibly URL-safe and unpadded.
	let s = value.replace(/-/g, '+').replace(/_/g, '/');
	while (s.length % 4) { s += '='; }
	return Uint8Array.from(atob(s), c => c.charCodeAt(0));
}

function heat(v) {
	// A simple dark-to-bright colour ramp for a 0..255 value.
	const t = v / 255;
	const r = Math.min(255, Math.round(255 * Math.pow(t, 0.9) * 1.25));
	const g = Math.min(255, Math.round(255 * Math.pow(t, 2.2) * 1.1));
	const b = Math.min(255, Math.round(70 + 140 * Math.sin(Math.PI * Math.min(1, t * 1.2)) * (1 - t * 0.6)));
	return [r, g, Math.max(0, t < 0.05 ? 20 : b)];
}

export function drawExplain(canvasId, o) {
	const canvas = document.getElementById(canvasId);
	if (!canvas) { return; }
	const ctx = canvas.getContext('2d');
	const W = canvas.width, H = canvas.height;
	const L = 70, R = 12, T = 12, B = 44;           // margins: room for the axis labels
	const pw = W - L - R, ph = H - T - B;
	const previous = explainState[canvasId];
	const image = o.image ? decodeImage(o.image) : (previous ? previous.image : null);
	const x = (frame) => L + (frame / o.frames) * pw;
	const y = (bin) => T + ph - (bin / (o.bins - 1)) * ph;
	explainState[canvasId] = { o, x, y, L, T, pw, ph, image };

	ctx.clearRect(0, 0, W, H);
	ctx.fillStyle = '#0a0e18';
	ctx.fillRect(0, 0, W, H);

	if (o.showSpectrogram && image && image.length) {
		const off = document.createElement('canvas');
		off.width = o.frames; off.height = o.bins;
		const octx = off.getContext('2d');
		const img = octx.createImageData(o.frames, o.bins);
		for (let f = 0; f < o.frames; f++) {
			for (let k = 0; k < o.bins; k++) {
				const v = image[f * o.bins + k];
				const [r, g, b] = heat(v);
				const p = ((o.bins - 1 - k) * o.frames + f) * 4;
				img.data[p] = r; img.data[p + 1] = g; img.data[p + 2] = b; img.data[p + 3] = 255;
			}
		}
		octx.putImageData(img, 0, 0);
		ctx.imageSmoothingEnabled = true;
		ctx.drawImage(off, L, T, pw, ph);
	}

	// Axes, ticks and gridlines.
	ctx.strokeStyle = '#2a3552'; ctx.fillStyle = '#8f9bb8'; ctx.lineWidth = 1;
	ctx.font = '12px "Segoe UI", sans-serif';
	ctx.strokeRect(L, T, pw, ph);
	const seconds = o.frames * o.frameSeconds;
	for (let s = 0; s <= seconds + 1e-9; s += 1) {
		const px = x(s / o.frameSeconds);
		ctx.beginPath(); ctx.moveTo(px, T + ph); ctx.lineTo(px, T + ph + 5); ctx.stroke();
		ctx.textAlign = 'center';
		ctx.fillText((o.startSeconds + s).toFixed(0), px, T + ph + 18);
	}
	for (let hz = 0; hz <= o.maxHz + 1e-9; hz += 1000) {
		const py = y(hz / o.hzPerBin);
		ctx.beginPath(); ctx.moveTo(L - 5, py); ctx.lineTo(L, py); ctx.stroke();
		ctx.textAlign = 'right';
		ctx.fillText(hz === 0 ? '0' : (hz / 1000) + ' kHz', L - 8, py + 4);
		if (hz > 0) { ctx.strokeStyle = 'rgba(255,255,255,0.07)'; ctx.beginPath(); ctx.moveTo(L, py); ctx.lineTo(L + pw, py); ctx.stroke(); ctx.strokeStyle = '#2a3552'; }
	}
	ctx.textAlign = 'center';
	ctx.fillStyle = '#c9d3ea';
	ctx.fillText('Time in the track (seconds)', L + pw / 2, H - 6);
	ctx.save();
	ctx.translate(14, T + ph / 2); ctx.rotate(-Math.PI / 2);
	ctx.fillText('Frequency (Hz)', 0, 0);
	ctx.restore();

	// Landmark pairs.
	if (o.showPairs) {
		ctx.strokeStyle = 'rgba(124,240,178,0.55)'; ctx.lineWidth = 1;
		for (let i = 0; i + 3 < o.pairs.length; i += 4) {
			ctx.beginPath(); ctx.moveTo(x(o.pairs[i]), y(o.pairs[i + 1])); ctx.lineTo(x(o.pairs[i + 2]), y(o.pairs[i + 3])); ctx.stroke();
		}
	}

	// Selected anchor: target zone rectangle and its pairs in a strong colour.
	if (o.zone && o.zone.length === 4) {
		const [f0, b0, f1, b1] = o.zone;
		ctx.fillStyle = 'rgba(255,214,102,0.10)'; ctx.strokeStyle = 'rgba(255,214,102,0.9)'; ctx.lineWidth = 1.5;
		const zx = x(f0), zy = y(Math.min(b1, o.bins - 1)), zw = x(f1) - x(f0), zh = y(Math.max(b0, 0)) - y(Math.min(b1, o.bins - 1));
		ctx.fillRect(zx, zy, zw, zh); ctx.strokeRect(zx, zy, zw, zh);
	}
	if (o.selectedPairs) {
		ctx.strokeStyle = '#ffd666'; ctx.lineWidth = 2;
		for (let i = 0; i + 3 < o.selectedPairs.length; i += 4) {
			ctx.beginPath(); ctx.moveTo(x(o.selectedPairs[i]), y(o.selectedPairs[i + 1])); ctx.lineTo(x(o.selectedPairs[i + 2]), y(o.selectedPairs[i + 3])); ctx.stroke();
			ctx.fillStyle = '#ffd666'; ctx.beginPath(); ctx.arc(x(o.selectedPairs[i + 2]), y(o.selectedPairs[i + 3]), 4, 0, 7); ctx.fill();
		}
	}

	// Peaks.
	if (o.showPeaks) {
		for (let i = 0; i + 1 < o.peaks.length; i += 2) {
			ctx.fillStyle = '#4cc2ff'; ctx.strokeStyle = '#06202e'; ctx.lineWidth = 1;
			ctx.beginPath(); ctx.arc(x(o.peaks[i]), y(o.peaks[i + 1]), 3, 0, 7); ctx.fill(); ctx.stroke();
		}
	}
	if (o.anchor && o.anchor.length === 2) {
		ctx.strokeStyle = '#ffd666'; ctx.lineWidth = 2; ctx.fillStyle = '#ffd666';
		ctx.beginPath(); ctx.arc(x(o.anchor[0]), y(o.anchor[1]), 6, 0, 7); ctx.stroke();
		ctx.beginPath(); ctx.arc(x(o.anchor[0]), y(o.anchor[1]), 2.5, 0, 7); ctx.fill();
	}
}

// Lets a click on the figure choose the nearest peak; .NET is told the index into the peaks array last drawn.
export function enablePeakPicking(canvasId, dotnetRef) {
	const canvas = document.getElementById(canvasId);
	if (!canvas) { return; }
	if (canvas._pick) { canvas.removeEventListener('click', canvas._pick); }
	canvas._pick = (e) => {
		const s = explainState[canvasId];
		if (!s) { return; }
		const rect = canvas.getBoundingClientRect();
		const px = (e.clientX - rect.left) * (canvas.width / rect.width);
		const py = (e.clientY - rect.top) * (canvas.height / rect.height);
		let best = -1, bestD = 18 * 18;
		for (let i = 0; i + 1 < s.o.peaks.length; i += 2) {
			const dx = s.x(s.o.peaks[i]) - px, dy = s.y(s.o.peaks[i + 1]) - py;
			const d = dx * dx + dy * dy;
			if (d < bestD) { bestD = d; best = i / 2; }
		}
		dotnetRef.invokeMethodAsync('OnPeakPicked', best);
	};
	canvas.addEventListener('click', canvas._pick);
}
