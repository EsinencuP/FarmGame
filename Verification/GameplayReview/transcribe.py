"""Local-only gameplay commentary transcription with timestamped output."""
import json
import wave
import sys
import numpy as np
from pathlib import Path
from faster_whisper import WhisperModel, BatchedInferencePipeline

sys.stdout.reconfigure(encoding="utf-8")

root = Path(__file__).parent
model = WhisperModel("small", device="cpu", compute_type="int8", cpu_threads=6,
                     download_root="D:/AI/models/whisper", num_workers=1)
with wave.open(str(root / "audio.wav"), "rb") as source:
    assert source.getframerate() == 16000 and source.getnchannels() == 1 and source.getsampwidth() == 2
    samples = np.frombuffer(source.readframes(source.getnframes()), dtype=np.int16).astype(np.float32) / 32768.0
segments, info = BatchedInferencePipeline(model).transcribe(samples, language="ru",
                                  vad_filter=True, beam_size=3, batch_size=8)
result = []
def timestamp(value):
    return f"{int(value // 60):02d}:{int(value % 60):02d}"

with (root / "transcript.txt").open("w", encoding="utf-8") as output:
    for segment in segments:
        record = {"start":segment.start,"end":segment.end,"text":segment.text.strip()}
        result.append(record)
        line = f"[{timestamp(segment.start)}–{timestamp(segment.end)}] {segment.text.strip()}"
        output.write(line + "\n")
        output.flush()
        print(line, flush=True)
(root / "transcript.json").write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
print(f"DONE: {len(result)} segments; language={info.language}", flush=True)
