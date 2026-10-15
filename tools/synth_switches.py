"""
Procedural keyboard-switch sound synthesizer for Mecha Keys.

Every sound here is generated from scratch (filtered noise, modal resonators,
damped sinusoids) so the new packs are original and carry no third-party audio.

Usage:  python synth_switches.py <out_dir> [--variants 32] [--demo demo.ogg]
"""
import argparse
import os
import subprocess
import sys
import tempfile

import numpy as np
from scipy.signal import lfilter, butter, sosfilt

SR = 44100
LEN = 0.30  # seconds per clip


# ----------------------------------------------------------------- primitives
def _bp(f, q):
    """RBJ constant-0dB-peak band-pass biquad."""
    f = min(f, SR * 0.45)
    w0 = 2 * np.pi * f / SR
    alpha = np.sin(w0) / (2 * q)
    b = np.array([alpha, 0.0, -alpha])
    a = np.array([1 + alpha, -2 * np.cos(w0), 1 - alpha])
    return b / a[0], a / a[0]


def _t(n):
    return np.arange(n) / SR


def _place(buf, sig, t0):
    i = int(round(t0 * SR))
    if i >= len(buf):
        return
    n = min(len(sig), len(buf) - i)
    buf[i:i + n] += sig[:n]


def excitation(rng, ms, smooth=0.0):
    """Short noise burst; `smooth` (ms) softens the attack (rubber, fingers)."""
    n = int(SR * max(ms, 0.05) * 6 / 1000) + 8
    t = _t(n)
    env = np.exp(-t / (ms / 1000.0))
    if smooth > 0:
        env *= 1 - np.exp(-t / (smooth / 1000.0))
    return rng.standard_normal(n) * env


def knock(rng, modes, exc_ms=0.5, smooth=0.0, tick=0.0, tail=0.12, jitter=0.03):
    """Impact through a bank of resonant band-passes: (freq, Q, gain)."""
    n = int(SR * tail)
    e = np.zeros(n)
    x = excitation(rng, exc_ms, smooth)
    e[:len(x)] = x[:n]
    out = np.zeros(n)
    for f, q, g in modes:
        f *= 1 + rng.uniform(-jitter, jitter)
        g *= 1 + rng.uniform(-0.25, 0.25)
        b, a = _bp(f, q)
        out += g * lfilter(b, a, e) * np.sqrt(q)  # sqrt(q) keeps narrow modes audible
    if tick > 0:
        sos = butter(2, 5000, "hp", fs=SR, output="sos")
        out += tick * sosfilt(sos, e) * 0.5
    return out / (np.abs(out).max() + 1e-9)


def ring(rng, freqs, tau_ms, level, detune=0.004, tail=0.2):
    """Metallic ring: damped sines with a hint of beating."""
    n = int(SR * tail)
    t = _t(n)
    out = np.zeros(n)
    for k, f in enumerate(freqs):
        f *= 1 + rng.uniform(-0.02, 0.02)
        amp = level / (1 + 0.7 * k)
        tau = tau_ms / 1000.0 / (1 + 0.35 * k)
        ph = rng.uniform(0, 2 * np.pi)
        s = np.sin(2 * np.pi * f * t + ph) + 0.5 * np.sin(2 * np.pi * f * (1 + detune) * t + ph)
        out += amp * s * np.exp(-t / tau)
    att = np.minimum(1, t / 0.0004)
    return out * att


def thud(rng, f, tau_ms, level, tail=0.15):
    """Low body / desk resonance with a little downward pitch glide."""
    n = int(SR * tail)
    t = _t(n)
    f = f * (1 + rng.uniform(-0.06, 0.06))
    glide = f * (1 + 0.25 * np.exp(-t / 0.004))
    phase = 2 * np.pi * np.cumsum(glide) / SR
    att = np.minimum(1, t / 0.0012)
    return 0.5 * level * np.sin(phase) * np.exp(-t / (tau_ms / 1000.0)) * att


def scrape(rng, dur_ms, lo, hi, level):
    """Friction / tactile bump / dome collapse: band-limited noise, hann envelope."""
    n = max(16, int(SR * dur_ms / 1000))
    x = rng.standard_normal(n)
    sos = butter(2, [lo, min(hi, SR * 0.45)], "bp", fs=SR, output="sos")
    x = sosfilt(sos, x)
    # grainy amplitude: friction is not smooth
    grain = 0.6 + 0.4 * np.abs(sosfilt(butter(1, 300, "lp", fs=SR, output="sos"), rng.standard_normal(n))) * 4
    return level * x * np.hanning(n) * np.clip(grain, 0, 2)


def lowpass(x, f):
    return sosfilt(butter(2, f, "lp", fs=SR, output="sos"), x)


def highpass(x, f):
    return sosfilt(butter(2, f, "hp", fs=SR, output="sos"), x)


def desk(rng, x, amount=0.12):
    """Very small early reflections, like a keyboard sitting on a desk."""
    y = x.copy()
    for d_ms, g in ((3.1, 0.45), (5.7, 0.3), (9.2, 0.18), (14.5, 0.1)):
        d = int(SR * (d_ms * (1 + rng.uniform(-0.1, 0.1))) / 1000)
        y[d:] += amount * g * lowpass(x, 3500)[:-d]
    return y


def V(rng, a, b):
    return rng.uniform(a, b)


# ---------------------------------------------------------------- switch models
def gateron_yellow(rng):
    b = np.zeros(int(SR * LEN))
    t0 = 0.004
    _place(b, scrape(rng, 6, 2500, 7000, 0.015), t0 - 0.002)  # very smooth stem
    _place(b, knock(rng, [(1150, 6, 0.45), (2300, 8, 1.0), (3400, 9, 0.8), (5200, 7, 0.4), (8000, 4, 0.2)],
                    exc_ms=0.5, tick=0.15), t0)
    _place(b, thud(rng, 260, 12, 0.22), t0)
    tr = t0 + V(rng, 0.095, 0.135)
    _place(b, V(rng, 0.38, 0.5) * knock(rng, [(1300, 6, 0.4), (2600, 8, 1.0), (3900, 8, 0.7), (6000, 6, 0.3)],
                                       exc_ms=0.4, tick=0.1), tr)
    _place(b, thud(rng, 300, 8, 0.08), tr)
    return b, 0.72


def oil_king(rng):  # deep, "thocky" lubed linear with long-pole feel
    b = np.zeros(int(SR * LEN))
    t0 = 0.004
    _place(b, knock(rng, [(520, 4, 0.7), (1050, 5, 1.0), (1700, 6, 0.75), (2600, 6, 0.4), (4200, 5, 0.12)],
                    exc_ms=0.9, smooth=0.25), t0)
    _place(b, thud(rng, 175, 22, 0.3), t0)
    tr = t0 + V(rng, 0.105, 0.145)
    _place(b, V(rng, 0.3, 0.42) * knock(rng, [(800, 5, 0.6), (1500, 6, 1.0), (2400, 6, 0.6), (3600, 5, 0.25)],
                                       exc_ms=0.7, smooth=0.2), tr)
    _place(b, thud(rng, 210, 12, 0.12), tr)
    return lowpass(b, 6500), 0.75


def holy_panda(rng):  # tactile, big bump, thocky
    b = np.zeros(int(SR * LEN))
    _place(b, scrape(rng, V(rng, 7, 10), 1200, 4200, 0.09), 0.0)  # leaf over the bump
    t0 = V(rng, 0.010, 0.014)
    _place(b, knock(rng, [(700, 5, 0.8), (1400, 6, 1.0), (2100, 7, 0.65), (3200, 6, 0.35), (5000, 5, 0.12)],
                    exc_ms=0.7, smooth=0.15), t0)
    _place(b, thud(rng, 190, 18, 0.25), t0)
    tr = t0 + V(rng, 0.10, 0.14)
    _place(b, scrape(rng, 6, 1500, 4500, 0.05), tr - 0.006)
    _place(b, V(rng, 0.42, 0.55) * knock(rng, [(1800, 6, 0.8), (2900, 7, 1.0), (4300, 6, 0.5)], exc_ms=0.5), tr)
    return b, 0.72


def boba_u4(rng):  # silent-ish tactile: rounded bump, damped bottom-out
    b = np.zeros(int(SR * LEN))
    _place(b, scrape(rng, 9, 600, 2500, 0.06), 0.0)
    t0 = 0.011
    k = knock(rng, [(450, 3, 0.9), (900, 4, 1.0), (1600, 4, 0.5)], exc_ms=1.6, smooth=0.8)
    _place(b, k, t0)
    _place(b, thud(rng, 160, 16, 0.22), t0)
    tr = t0 + V(rng, 0.10, 0.14)
    _place(b, 0.3 * knock(rng, [(700, 3, 0.8), (1300, 4, 1.0)], exc_ms=1.4, smooth=0.6), tr)
    return lowpass(b, 3000), 0.42


def box_jade(rng):  # click bar: crisp, loud, both ways
    b = np.zeros(int(SR * LEN))
    t0 = 0.003
    _place(b, knock(rng, [(3800, 14, 1.0), (5600, 16, 0.8), (7900, 12, 0.5), (2400, 8, 0.3)], exc_ms=0.15, tick=0.6), t0)
    _place(b, ring(rng, [4300, 6150, 8900], 5.5, 0.22), t0)
    tb = t0 + V(rng, 0.007, 0.011)
    _place(b, 0.6 * knock(rng, [(1300, 6, 0.5), (2500, 8, 1.0), (3700, 8, 0.6)], exc_ms=0.5), tb)
    _place(b, thud(rng, 280, 9, 0.15), tb)
    tr = t0 + V(rng, 0.10, 0.13)
    _place(b, 0.75 * knock(rng, [(4100, 14, 1.0), (6000, 14, 0.7), (8400, 10, 0.4)], exc_ms=0.15, tick=0.5), tr)
    _place(b, ring(rng, [4600, 6600], 4.0, 0.15), tr)
    _place(b, 0.35 * knock(rng, [(2700, 8, 1.0), (4000, 7, 0.5)], exc_ms=0.4), tr + 0.004)
    return b, 0.8


def box_navy(rng):  # heavier click bar, deeper and louder
    b = np.zeros(int(SR * LEN))
    t0 = 0.003
    _place(b, knock(rng, [(3000, 14, 1.0), (4500, 16, 0.85), (6600, 12, 0.5), (1900, 8, 0.35)], exc_ms=0.18, tick=0.5), t0)
    _place(b, ring(rng, [3350, 4900, 7200], 8.5, 0.25), t0)
    tb = t0 + V(rng, 0.009, 0.013)
    _place(b, 0.7 * knock(rng, [(900, 5, 0.6), (1900, 7, 1.0), (3000, 7, 0.6)], exc_ms=0.6), tb)
    _place(b, thud(rng, 220, 14, 0.25), tb)
    tr = t0 + V(rng, 0.11, 0.14)
    _place(b, 0.8 * knock(rng, [(3300, 14, 1.0), (5000, 14, 0.7), (7000, 10, 0.4)], exc_ms=0.18, tick=0.4), tr)
    _place(b, ring(rng, [3600, 5300], 6.0, 0.18), tr)
    return b, 0.82


def alps_blue(rng):  # vintage clicky Alps: sharp, high, a little scratchy
    b = np.zeros(int(SR * LEN))
    _place(b, scrape(rng, 4, 3000, 9000, 0.05), 0.0)
    t0 = 0.004
    _place(b, knock(rng, [(5400, 18, 1.0), (7600, 16, 0.7), (3600, 10, 0.4)], exc_ms=0.12, tick=0.7), t0)
    _place(b, ring(rng, [5900, 8300], 3.5, 0.12), t0)
    tb = t0 + V(rng, 0.005, 0.008)
    _place(b, 0.75 * knock(rng, [(1800, 9, 0.7), (2700, 10, 1.0), (4100, 9, 0.6), (6200, 7, 0.3)], exc_ms=0.35, tick=0.3), tb)
    _place(b, thud(rng, 320, 7, 0.1), tb)
    tr = t0 + V(rng, 0.085, 0.12)
    _place(b, 0.5 * knock(rng, [(5000, 16, 1.0), (7200, 14, 0.6), (3000, 9, 0.5)], exc_ms=0.12, tick=0.4), tr)
    _place(b, 0.3 * knock(rng, [(2200, 8, 1.0), (3400, 8, 0.6)], exc_ms=0.35), tr + 0.003)
    return b, 0.74


def topre(rng):  # rubber dome over conical spring: muted "thock", strong upstroke
    b = np.zeros(int(SR * LEN))
    _place(b, scrape(rng, 8, 400, 1800, 0.07), 0.0)  # dome collapse
    t0 = V(rng, 0.008, 0.011)
    _place(b, knock(rng, [(480, 4, 1.0), (950, 5, 0.85), (1600, 5, 0.45), (2600, 4, 0.15)], exc_ms=1.2, smooth=0.6), t0)
    _place(b, thud(rng, 210, 20, 0.3), t0)
    tr = t0 + V(rng, 0.11, 0.15)
    _place(b, V(rng, 0.62, 0.78) * knock(rng, [(620, 4, 0.9), (1250, 5, 1.0), (2100, 5, 0.55), (3300, 4, 0.18)],
                                        exc_ms=0.9, smooth=0.4), tr)
    _place(b, thud(rng, 240, 14, 0.15), tr)
    return lowpass(b, 4200), 0.7


def buckling_spring(rng):  # IBM Model M style
    b = np.zeros(int(SR * LEN))
    t0 = 0.003
    # the spring buckles: loud broadband snap
    _place(b, knock(rng, [(2400, 15, 1.0), (3700, 20, 0.85), (5100, 15, 0.55), (7400, 10, 0.25)], exc_ms=0.2, tick=0.6), t0)
    _place(b, ring(rng, [1850, 3320, 4710], 32, 0.11, detune=0.012, tail=0.25), t0)  # spring "ping"
    # hammer hits the membrane
    th = t0 + V(rng, 0.003, 0.006)
    _place(b, 0.85 * knock(rng, [(900, 6, 0.8), (1700, 8, 1.0), (2600, 8, 0.7)], exc_ms=0.5), th)
    # steel backplate + barrel resonance
    _place(b, 0.6 * knock(rng, [(1300, 45, 1.0), (2100, 50, 0.7), (2950, 40, 0.4)], exc_ms=0.3, tail=0.25), th)
    _place(b, thud(rng, 150, 24, 0.3), th)
    # release: spring re-straightens with another (softer) snap and a longer twang
    tr = t0 + V(rng, 0.11, 0.15)
    _place(b, 0.55 * knock(rng, [(2700, 15, 1.0), (4000, 18, 0.7), (5600, 14, 0.4)], exc_ms=0.2, tick=0.4), tr)
    _place(b, ring(rng, [1900, 3400], 40, 0.13, detune=0.015, tail=0.2), tr)
    _place(b, 0.3 * knock(rng, [(1300, 45, 1.0), (2100, 50, 0.6)], exc_ms=0.3, tail=0.15), tr + 0.002)
    return b, 0.85


def choc_red(rng):  # low-profile linear: short, light, high pitched
    b = np.zeros(int(SR * LEN))
    t0 = 0.003
    _place(b, knock(rng, [(2600, 7, 1.0), (4200, 8, 0.75), (6500, 6, 0.35), (1500, 5, 0.25)], exc_ms=0.35, tick=0.2, tail=0.06), t0)
    _place(b, thud(rng, 340, 6, 0.1), t0)
    tr = t0 + V(rng, 0.06, 0.085)
    _place(b, V(rng, 0.35, 0.45) * knock(rng, [(3000, 7, 1.0), (4800, 8, 0.6)], exc_ms=0.3, tail=0.05), tr)
    return b, 0.6


def silent_linear(rng):  # dampened stem: soft, low and quiet
    b = np.zeros(int(SR * LEN))
    t0 = 0.004
    _place(b, knock(rng, [(350, 3, 1.0), (800, 4, 0.8), (1500, 3, 0.3)], exc_ms=2.2, smooth=1.0), t0)
    _place(b, thud(rng, 170, 12, 0.2), t0)
    tr = t0 + V(rng, 0.10, 0.14)
    _place(b, 0.28 * knock(rng, [(500, 3, 1.0), (1100, 4, 0.6)], exc_ms=1.8, smooth=0.8), tr)
    return lowpass(b, 2200), 0.32


def membrane(rng):  # office rubber dome keyboard: mushy, plasticky, rattly
    b = np.zeros(int(SR * LEN))
    _place(b, scrape(rng, V(rng, 12, 18), 500, 3000, 0.08), 0.0)
    t0 = V(rng, 0.014, 0.02)
    _place(b, knock(rng, [(1200, 4, 0.9), (2500, 5, 1.0), (3900, 4, 0.5), (6000, 3, 0.15)], exc_ms=1.4, smooth=0.5), t0)
    _place(b, thud(rng, 240, 9, 0.18), t0)
    tr = t0 + V(rng, 0.09, 0.13)
    _place(b, 0.42 * knock(rng, [(1600, 5, 1.0), (3000, 6, 0.6)], exc_ms=0.6), tr)
    _place(b, 0.2 * knock(rng, [(2000, 6, 1.0), (3600, 6, 0.6)], exc_ms=0.4), tr + V(rng, 0.004, 0.007))  # wobble rattle
    return b, 0.55


def typewriter(rng):  # manual typewriter keystroke
    b = np.zeros(int(SR * LEN))
    _place(b, 0.35 * knock(rng, [(400, 4, 1.0), (900, 5, 0.6)], exc_ms=1.5, smooth=0.8), 0.0)  # key lever
    ts = V(rng, 0.024, 0.034)  # typebar strikes the platen
    _place(b, knock(rng, [(1800, 10, 0.8), (3000, 12, 1.0), (4500, 10, 0.75), (7000, 8, 0.4)], exc_ms=0.25, tick=0.6), ts)
    _place(b, thud(rng, 240, 16, 0.4), ts)
    _place(b, ring(rng, [2700, 4100], 38, 0.06, tail=0.2), ts)
    _place(b, 0.6 * knock(rng, [(1500, 40, 1.0), (2300, 35, 0.6)], exc_ms=0.3, tail=0.2), ts)  # frame
    te = ts + V(rng, 0.03, 0.045)  # escapement tick
    _place(b, 0.3 * knock(rng, [(5600, 16, 1.0), (8000, 12, 0.6)], exc_ms=0.12, tick=0.3), te)
    tr = ts + V(rng, 0.13, 0.17)  # typebar falls back to the basket
    _place(b, 0.35 * knock(rng, [(700, 5, 1.0), (1400, 6, 0.8), (2400, 6, 0.3)], exc_ms=0.8), tr)
    return b, 0.85


# ------------------------------------------------------------------ mice
def mouse_classic(rng):  # micro switch
    b = np.zeros(int(SR * LEN))
    t0 = 0.002
    _place(b, knock(rng, [(3500, 10, 1.0), (5200, 12, 0.8), (7600, 9, 0.45)], exc_ms=0.15, tick=0.5, tail=0.06), t0)
    _place(b, 0.5 * knock(rng, [(1600, 6, 1.0), (2600, 6, 0.4)], exc_ms=0.4, tail=0.08), t0 + 0.0006)  # shell
    tr = t0 + V(rng, 0.07, 0.1)
    _place(b, 0.72 * knock(rng, [(3900, 10, 1.0), (5800, 12, 0.7), (8200, 9, 0.35)], exc_ms=0.15, tick=0.4, tail=0.05), tr)
    _place(b, 0.3 * knock(rng, [(1800, 6, 1.0)], exc_ms=0.4, tail=0.06), tr + 0.0006)
    return b, 0.62


def mouse_optical(rng):  # crisp optical gaming switch
    b = np.zeros(int(SR * LEN))
    t0 = 0.002
    _place(b, knock(rng, [(4600, 12, 1.0), (6800, 12, 0.8), (9200, 8, 0.4)], exc_ms=0.1, tick=0.6, tail=0.04), t0)
    _place(b, 0.35 * knock(rng, [(2100, 7, 1.0)], exc_ms=0.3, tail=0.05), t0 + 0.0005)
    tr = t0 + V(rng, 0.06, 0.09)
    _place(b, 0.6 * knock(rng, [(5000, 12, 1.0), (7400, 12, 0.6)], exc_ms=0.1, tick=0.5, tail=0.04), tr)
    return b, 0.58


def mouse_silent(rng):
    b = np.zeros(int(SR * LEN))
    t0 = 0.002
    _place(b, knock(rng, [(700, 3, 1.0), (1500, 4, 0.7), (2600, 3, 0.25)], exc_ms=1.2, smooth=0.5, tail=0.06), t0)
    tr = t0 + V(rng, 0.07, 0.1)
    _place(b, 0.4 * knock(rng, [(900, 3, 1.0), (1800, 4, 0.5)], exc_ms=1.0, smooth=0.4, tail=0.05), tr)
    return lowpass(b, 2800), 0.3


PACKS = {
    "gateron_yellow": gateron_yellow,
    "oil_king": oil_king,
    "holy_panda": holy_panda,
    "boba_u4": boba_u4,
    "kailh_box_jade": box_jade,
    "kailh_box_navy": box_navy,
    "alps_skcm_blue": alps_blue,
    "topre": topre,
    "buckling_spring": buckling_spring,
    "kailh_choc_red": choc_red,
    "silent_linear": silent_linear,
    "membrane": membrane,
    "typewriter": typewriter,
    "mouse_classic": mouse_classic,
    "mouse_optical": mouse_optical,
    "mouse_silent": mouse_silent,
}


# per-pack body/room tail: (wet amount, decay ms, lowpass Hz)
ROOM = {
    "silent_linear": (0.45, 22, 2500),
    "boba_u4": (0.5, 25, 3000),
    "mouse_silent": (0.35, 15, 2800),
    "topre": (0.7, 30, 4500),
    "oil_king": (0.75, 32, 6000),
    "buckling_spring": (0.9, 45, 8000),
    "typewriter": (0.9, 55, 8000),
    "kailh_choc_red": (0.5, 18, 8000),
    "mouse_classic": (0.4, 14, 8000),
    "mouse_optical": (0.35, 12, 9000),
}


def body(rng, x, wet, tau_ms, lp):
    """Noisy decaying tail: case/plate/room resonance that real recordings always carry."""
    n = int(SR * tau_ms * 5 / 1000)
    t = _t(n)
    ir = rng.standard_normal(n) * np.exp(-t / (tau_ms / 1000.0))
    ir = lowpass(ir, lp)
    ir[: int(SR * 0.0008)] *= np.linspace(0, 1, int(SR * 0.0008))
    ir /= np.sqrt((ir ** 2).sum())
    from scipy.signal import fftconvolve
    w = fftconvolve(x, ir)[: len(x)]
    rx = np.sqrt((x ** 2).mean()) + 1e-12
    rw = np.sqrt((w ** 2).mean()) + 1e-12
    return x + w * (wet * rx / rw)


def render(fn, seed, name=None):
    rng = np.random.default_rng(seed)
    x, peak = fn(rng)
    if name and not name.startswith("mouse"):
        # fingertip landing on the keycap just before the switch moves
        _place(x, 0.07 * lowpass(knock(rng, [(900, 3, 1.0), (2200, 3, 0.6)], exc_ms=1.5, smooth=0.7, tail=0.03), 3000), 0.0)
    wet, tau, lp = ROOM.get(name, (0.65, 28, 7500))
    x = body(rng, x, wet, tau, lp)
    x = desk(rng, x)
    x = highpass(x, 60)
    # tidy the tail
    n = len(x)
    fade = int(SR * 0.03)
    x[-fade:] *= np.linspace(1, 0, fade)
    # trim trailing silence (-60 dB)
    thr = np.abs(x).max() * 1e-3
    last = np.where(np.abs(x) > thr)[0]
    if len(last):
        end = min(n, last[-1] + int(SR * 0.01))
        x = x[:end]
        f2 = min(len(x), int(SR * 0.008))
        x[-f2:] *= np.linspace(1, 0, f2)
    x = x / (np.abs(x).max() + 1e-9) * peak * rng.uniform(0.9, 1.0)
    return x.astype(np.float32)


def write_ogg(x, path):
    with tempfile.NamedTemporaryFile(suffix=".f32", delete=False) as tf:
        tf.write(x.tobytes())
        raw = tf.name
    try:
        subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "f32le", "-ar", str(SR), "-ac", "1", "-i", raw,
                        "-c:a", "libvorbis", "-q:a", "4", path], check=True)
    finally:
        os.unlink(raw)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("out")
    ap.add_argument("--variants", type=int, default=32)
    ap.add_argument("--demo")
    ap.add_argument("--only", nargs="*")
    a = ap.parse_args()

    demo = []
    for pi, (name, fn) in enumerate(PACKS.items()):
        if a.only and name not in a.only:
            continue
        d = os.path.join(a.out, name)
        os.makedirs(d, exist_ok=True)
        clips = []
        for v in range(a.variants):
            x = render(fn, seed=1000 * (pi + 1) + v, name=name)
            write_ogg(x, os.path.join(d, f"key_{v + 1:02d}.ogg"))
            clips.append(x)
        print(f"{name}: {a.variants} clips", file=sys.stderr)
        if a.demo:
            # a little typing burst for each pack
            rng = np.random.default_rng(pi)
            seq = np.zeros(int(SR * 2.2), np.float32)
            t = 0.05
            for k in range(14):
                c = clips[rng.integers(len(clips))]
                _place(seq, c, t)
                t += rng.uniform(0.08, 0.17)
            demo.append(seq)
    if a.demo and demo:
        write_ogg(np.concatenate(demo), a.demo)


if __name__ == "__main__":
    main()
