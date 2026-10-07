"""Синтез встроенных мелодий Chrono: 5 файлов WAV (PCM 16 бит, моно, 22 050 Гц, 20 с).

Только стандартная библиотека Python. Лицензионно чисто: звуки создаются математически.
Запуск из корня репозитория:  python tools/gen_sounds.py
Результат: src/Chrono/Resources/Raw/{bell,chime,digital,rising,soft}.wav
"""
import math
import os
import struct
import sys
import wave

RATE = 22050
DURATION = 20.0  # iOS проигрывает звук уведомления не дольше 30 с
OUT_DIR = sys.argv[1] if len(sys.argv) > 1 else os.path.join("src", "Chrono", "Resources", "Raw")


def strike(t, freq, partials, decay):
    """Удар с затуханием: сумма обертонов (множитель частоты, амплитуда)."""
    if t < 0:
        return 0.0
    env = math.exp(-decay * t) * min(1.0, t * 400)  # 2.5 мс атака без щелчка
    return env * sum(a * math.sin(2 * math.pi * freq * m * t) for m, a in partials)


BELL = [(1.0, 1.0), (2.0, 0.6), (2.76, 0.4), (5.4, 0.25), (8.93, 0.1)]
SINE = [(1.0, 1.0), (2.0, 0.15)]
MARIMBA = [(1.0, 1.0), (4.0, 0.2)]


def bell(t):
    return strike(t % 2.0, 660, BELL, 2.2)


def chime(t):
    notes = [1046.5, 1318.5, 1568.0, 2093.0]  # до-ми-соль-до
    cycle = t % 3.0
    return sum(strike(cycle - i * 0.25, f, SINE, 3.0) for i, f in enumerate(notes))


def digital(t):
    cycle = t % 1.6
    beep = 0 <= cycle % 0.25 < 0.12 and cycle < 1.0  # четыре коротких сигнала и пауза
    if not beep:
        return 0.0
    # Мягкий «квадрат»: нечётные гармоники с убыванием.
    return sum(math.sin(2 * math.pi * 2000 * k * t) / k for k in (1, 3, 5)) * 0.8


def rising(t):
    gain = 0.15 + 0.85 * min(1.0, t / DURATION)  # громкость растёт до конца файла
    cycle = t % 1.0
    return gain * (strike(cycle, 880, SINE, 4.0) + strike(cycle - 0.5, 1175, SINE, 4.0))


def soft(t):
    notes = [440.0, 523.25, 659.25, 783.99, 659.25, 523.25]  # пентатоника вверх-вниз
    cycle = t % 3.6
    return sum(strike(cycle - i * 0.6, f, MARIMBA, 2.5) for i, f in enumerate(notes))


def render(name, fn):
    frames = [fn(i / RATE) for i in range(int(RATE * DURATION))]
    peak = max(abs(x) for x in frames) or 1.0
    scale = 0.8 * 32767 / peak  # нормализация до -2 dBFS
    path = os.path.join(OUT_DIR, f"{name}.wav")
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(x * scale)) for x in frames))
    print(f"{path}: {os.path.getsize(path)} байт")


if __name__ == "__main__":
    os.makedirs(OUT_DIR, exist_ok=True)
    for sound_name, sound_fn in [("bell", bell), ("chime", chime), ("digital", digital), ("rising", rising), ("soft", soft)]:
        render(sound_name, sound_fn)
