"""Create the original My Little Farm demo audio. Requires NumPy and FFmpeg."""

from pathlib import Path
import argparse
import io
import subprocess
import wave

import numpy as np


RATE = 44100
TAU = 2 * np.pi


def timeline(seconds):
    return np.arange(round(seconds * RATE)) / RATE


def frequency(note):
    return 440 * 2 ** ((note - 69) / 12)


def pluck(note, length, warmth):
    t = timeline(length)
    phase = TAU * frequency(note) * t
    tone = (np.sin(phase) + .37 * np.sin(2 * phase) * np.exp(-t * 5)
            + warmth * np.sin(3 * phase + .16 * np.sin(phase)) * np.exp(-t * 8))
    return tone * (1 - np.exp(-t * 240)) * np.exp(-t * (4 + warmth))


def layer(target, signal, at, level, pan=0):
    index = (round(at * RATE) + np.arange(len(signal))) % len(target)
    np.add.at(target[:, 0], index, signal * level * np.sqrt((1 - pan) * .5))
    np.add.at(target[:, 1], index, signal * level * np.sqrt((1 + pan) * .5))


def music(tempo, progression, melody, warmth):
    beat = 60 / tempo
    output = np.zeros((round(beat * 32 * RATE), 2))
    for bar, chord in enumerate(progression):
        downbeat = bar * 4
        pad_time = timeline(beat * 4.8)
        envelope = np.minimum(pad_time / (beat * 1.05), 1)
        envelope *= np.clip((beat * 4.8 - pad_time) / (beat * 1.55), 0, 1)
        for part, note in enumerate(chord):
            phase = TAU * frequency(note + 12) * pad_time
            pad = (.55 * np.sin(phase) + .24 * np.sin(phase * 1.002)
                   + .13 * np.sin(phase * 2 + .2)) * envelope
            layer(output, pad, downbeat * beat, .034, (part - 1) * .42)

        bass = pluck(chord[0], beat * 2.2, .1)
        layer(output, bass, downbeat * beat, .21)
        layer(output, pluck(chord[1], beat * 1.7, .1), (downbeat + 2.5) * beat, .13, .15)
        for subdivision, voice in [(0.5, 1), (1.25, 2), (2, 1), (3, 2), (3.5, 1)]:
            signal = pluck(chord[voice] + 12, beat * 1.8, warmth)
            layer(output, signal, (downbeat + subdivision) * beat, .12, (-1) ** voice * .36)
        for offset, note in melody[bar]:
            signal = pluck(note, beat * 2.2, warmth)
            layer(output, signal, (downbeat + offset) * beat, .25, -.13)
            layer(output, signal, (downbeat + offset + .57) * beat, .045, .52)
    return output


def ambience(night):
    length = 24
    t = timeline(length)
    rng = np.random.default_rng(817 if night else 932)
    spectrum = np.fft.rfft(rng.normal(size=len(t)))
    f = np.fft.rfftfreq(len(t), 1 / RATE)
    wind = np.fft.irfft(spectrum / (1 + (f / 270) ** 2) * f / (95 + f), n=len(t))
    wind *= (.11 if night else .18) * (.7 + .3 * np.sin(TAU * t / length))
    output = np.column_stack((wind, np.roll(wind, round(.012 * RATE))))
    if night:
        for phrase in np.arange(.31, length, .73):
            for chirp in (.0, .074, .151):
                s = timeline(.046)
                note = np.sin(TAU * (3600 * s + 260 * s * s)) * np.sin(np.pi * s / .046) ** 3
                layer(output, note, phrase + chirp, rng.uniform(.021, .04), rng.uniform(-.65, .65))
    else:
        for phrase in (1.1, 4.6, 10.4, 13.1, 18.8, 22.7):
            pan = rng.uniform(-.75, .75)
            for syllable in (0, .19, .38):
                s = timeline(.135)
                start = rng.uniform(1950, 3050)
                pitch = start * s + 410 * s**2 + 11 * np.sin(s * 12)
                note = (np.sin(TAU * pitch) + .22 * np.sin(TAU * 1.52 * pitch))
                layer(output, note * np.sin(np.pi * s / .135) ** 2, phrase + syllable, .115, pan)
    return output


def foley(duration, start, end, texture, decay, seed, rasp=0, body=.6):
    t = timeline(duration)
    rng = np.random.default_rng(seed)
    noise = rng.normal(size=len(t))
    filtered = np.convolve(noise, np.hanning(25) / 12, mode="same") * 3
    filtered = filtered * .8 + noise * .2
    phase = TAU * (start * t + (end - start) * t**2 / (2 * duration))
    tone = np.sin(phase + .11 * np.sin(phase * .55)) + rasp * np.sin(phase * 2.36)
    envelope = np.clip(t / .009, 0, 1) * np.exp(-t * decay)
    envelope *= np.clip((duration - t) / .025, 0, 1)
    return (tone * body + texture * filtered) * envelope


def motif(notes, beat=.075, warmth=.44):
    output = np.zeros(round((len(notes) * beat + .36) * RATE))
    for index, note in enumerate(notes):
        offset = round(beat * index * RATE)
        signal = pluck(note, .33, warmth) * (.72 ** index)
        signal *= np.clip((len(signal) - np.arange(len(signal))) / (RATE * .045), 0, 1)
        output[offset:offset + len(signal)] += signal
    return output


def write_wave(destination, sound, peak):
    sound = np.asarray(sound)
    data = np.round(sound * (peak / np.max(np.abs(sound))) * 32767).astype("<i2")
    with wave.open(str(destination) if isinstance(destination, Path) else destination, "wb") as writer:
        writer.setnchannels(1 if sound.ndim == 1 else sound.shape[1])
        writer.setsampwidth(2)
        writer.setframerate(RATE)
        writer.writeframes(data.tobytes())


def main(output):
    output.mkdir(parents=True, exist_ok=True)
    chords = [(48, 55, 59), (45, 52, 55), (41, 48, 52), (43, 50, 53),
              (48, 55, 62), (40, 47, 55), (41, 48, 55), (43, 50, 55)]
    theme = [
        [(0, 72), (1.5, 76), (2.5, 79)],
        [(0, 81), (1.5, 79), (3, 76)],
        [(0, 77), (1.5, 76), (2.5, 72)],
        [(0, 74), (2, 79)],
        [(0, 79), (1.5, 84), (3, 83)],
        [(0, 79), (1.5, 76), (3, 74)],
        [(0, 72), (1, 76), (2.5, 77)],
        [(0, 74), (2.5, 71)],
    ]
    night_chords = [(bass - 5, middle - 5, upper - 5) for bass, middle, upper in chords]
    night_theme = [[(beat, note - 5) for beat, note in bar[::2]] for bar in theme]
    day_theme = [[(beat, note + (12 if index == 4 else 0)) for beat, note in bar]
                 for index, bar in enumerate(theme)]
    loops = {
        "MusicMenu": (music(89, chords, theme, .45), .61),
        "MusicFarmDay": (music(103, chords[4:] + chords[:4], day_theme[4:] + day_theme[:4], .68), .51),
        "MusicFarmNight": (music(71, night_chords, night_theme, .18), .48),
        "AmbienceDay": (ambience(False), .44),
        "AmbienceNight": (ambience(True), .32),
    }
    tones = {
        "UiHover": ([84], .06), "UiClick": ([75, 82], .035), "UiBack": ([74, 67], .055),
        "UiOpen": ([67, 74], .05), "UiClose": ([72, 64], .06), "UiError": ([58, 55], .12),
        "Sell": ([84, 91, 88], .064), "Purchase": ([76, 81], .08), "EggCollect": ([79, 84], .085),
        "RareSeedCollect": ([77, 84, 89], .09), "SectorUnlock": ([60, 67, 72, 76], .112),
        "ToolUpgrade": ([64, 69, 76], .095), "HouseUpgrade": ([60, 65, 72, 77], .11),
        "Achievement": ([67, 72, 76, 84], .106), "Save": ([67, 72], .09), "Load": ([72, 76, 79], .065),
        "MilkCollect": ([71, 75, 78], .065), "ProductionCollect": ([72, 79, 84], .085),
        "FishingCatch": ([74, 79, 83], .063),
    }
    textures = {
        "Till": (.25, 132, 44, .95, 10, 11), "Plant": (.17, 310, 91, .62, 17, 12),
        "Harvest": (.21, 165, 405, .87, 12, 21), "BuildPlace": (.31, 102, 68, .44, 8, 31, .65),
        "BuildMove": (.25, 180, 72, .79, 11, 32), "BuildRemove": (.36, 156, 51, .8, 8, 33),
        "BuildRotate": (.11, 440, 174, .39, 27, 34), "TreePlant": (.26, 136, 61, .74, 11, 35),
        "AppleHarvest": (.23, 246, 115, .61, 13, 36), "ChickenBuy": (.30, 660, 340, .21, 9, 41, .63),
        "ChickenFeed": (.32, 540, 293, .59, 9, 42, .42), "FootstepA": (.15, 81, 50, .76, 20, 51),
        "FootstepB": (.16, 95, 44, .83, 19, 52), "FootstepC": (.14, 69, 38, 1.1, 24, 53),
        "Jump": (.16, 118, 237, .64, 13, 54), "Land": (.21, 86, 40, .88, 16, 55),
        "CowBuy": (.75, 107, 64, .30, 4, 61, .89), "CowFeed": (.64, 91, 69, .37, 4.4, 62, .69),
        "ProductionStart": (.30, 186, 82, .36, 9, 63, .77), "FishingCast": (.23, 557, 264, 1.1, 7, 64, .13, .21),
    }
    for name, (notes, beat) in tones.items():
        write_wave(output / f"{name}.wav", motif(notes, beat), .23 if name.startswith("Ui") else .40)
    for name, settings in textures.items():
        write_wave(output / f"{name}.wav", foley(*settings), .27 if name.startswith("Footstep") else .5)
    for name, (sound, peak) in loops.items():
        source = io.BytesIO()
        write_wave(source, sound, peak)
        subprocess.run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-f", "wav", "-i", "pipe:0",
                        "-c:a", "libvorbis", "-q:a", "5", str(output / f"{name}.ogg")],
                       input=source.getvalue(), check=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("output", type=Path)
    main(parser.parse_args().output)
