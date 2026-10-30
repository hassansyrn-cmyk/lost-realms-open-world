#!/usr/bin/env python3
"""Builds colour-graded terrain albedos from the stylised (pre-tint) Idyllic ground textures.

Each source is gradient-mapped through a natural palette using its own normalised luminance, so
the painted detail (blades, pebbles, cracks) survives while the hue becomes a believable ground.
Two-tone sources (forest floor, dirt with stones) are split by chroma/hue into two palettes.
Rock and cliff sit on the dark side of real stone so faces keep their shape against snow and sand
under the bright midday grade (the painter keeps snow and sand from veiling rock thinly). Sand and
snow stay below the tonemapper's shoulder: brighter, the dunes read white from afar and snowfields
lose all texture.

Cliff faces use the Terrain Toolkit's photographic layered rock (high-passed so its large light and
dark blotches do not repeat across a mountainside), with a normal map derived from its relief. Rock
keeps Idyllic's painted cracks, roughened with granular grain from the toolkit's basic rock, so it
reads as weathered stone rather than polished, veined marble.
"""
import os
import sys
import numpy as np
from PIL import Image, ImageFilter

SRC = '/mnt/user-data/uploads/Lost-Realms-Open-World/Assets'
OUT = sys.argv[1] if len(sys.argv) > 1 else '/mnt/user-data/outputs/lr/Assets/GameContent/Terrain/Textures'
SIZE = 2048
IDY = SRC + '/Idyllic Fantasy Nature/Textures'
TOOLKIT = SRC + '/com/heparo/terrain/toolkit/textures'


def load(path):
    im = Image.open(path).convert('RGB')
    if im.size != (SIZE, SIZE):
        im = im.resize((SIZE, SIZE), Image.LANCZOS)
    return np.asarray(im).astype(np.float32) / 255.0


def lum(a):
    return a[..., 0] * 0.299 + a[..., 1] * 0.587 + a[..., 2] * 0.114


def ramp(t, stops):
    """Piecewise-linear colour ramp; stops = [(pos, (r,g,b) 0-255), ...]."""
    t = np.clip(t, 0.0, 1.0)
    out = np.zeros(t.shape + (3,), np.float32)
    pos = [p for p, _ in stops]
    cols = [np.array(c, np.float32) / 255.0 for _, c in stops]
    for i in range(len(stops) - 1):
        a, b = pos[i], pos[i + 1]
        m = (t >= a) & (t <= b) if i == len(stops) - 2 else (t >= a) & (t < b)
        k = ((t - a) / max(b - a, 1e-5))[..., None]
        seg = cols[i] * (1 - k) + cols[i + 1] * k
        out = np.where(m[..., None], seg, out)
    return out


def norm_t(L, k, bias=0.0):
    m, s = float(L.mean()), float(L.std())
    return 0.5 + bias + (L - m) / max(s, 1e-3) * k


def save(name, a):
    os.makedirs(OUT, exist_ok=True)
    img = Image.fromarray(np.clip(a * 255.0 + 0.5, 0, 255).astype(np.uint8), 'RGB')
    p = os.path.join(OUT, name)
    img.save(p, optimize=True)
    st = (a.reshape(-1, 3).mean(0) * 255).round().astype(int)
    print(f'{name}: mean {tuple(st)} -> {os.path.getsize(p) / 1e6:.1f} MB')


def three(dark, mid, light):
    return [(0.0, dark), (0.5, mid), (1.0, light)]


def grade_simple(name, src, stops, k, bias=0.0):
    a = load(src)
    save(name, ramp(norm_t(lum(a), k, bias), stops))


def highpass(L, radius):
    """Removes brightness variation larger than ~radius px, keeping the mean (tile-friendly)."""
    img = Image.fromarray(np.clip(L * 255.0, 0, 255).astype(np.uint8))
    low = np.asarray(img.filter(ImageFilter.GaussianBlur(radius))).astype(np.float32) / 255.0
    return L - low + float(L.mean())


def tiled(path, reps):
    """Loads a small seamless texture and repeats it so it keeps its own grain size."""
    im = Image.open(path).convert('RGB')
    a = np.asarray(im.resize((SIZE // reps, SIZE // reps), Image.LANCZOS)).astype(np.float32) / 255.0
    return np.tile(a, (reps, reps, 1))


def save_normal(name, height, strength, size=1024):
    """Tangent-space normal map (OpenGL, +Y up) from a 0-1 height field, at the source's resolution."""
    img = Image.fromarray(np.clip(height * 255.0, 0, 255).astype(np.uint8)).resize((size, size), Image.LANCZOS)
    h = np.asarray(img.filter(ImageFilter.GaussianBlur(0.8))).astype(np.float32) / 255.0
    strength *= size / SIZE * 2.0
    gx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * 0.5
    gy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * 0.5   # rows run down the image, v runs up
    n = np.stack([-gx * strength, gy * strength, np.ones_like(h)], -1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    os.makedirs(OUT, exist_ok=True)
    p = os.path.join(OUT, name)
    Image.fromarray(np.clip((n * 0.5 + 0.5) * 255.0 + 0.5, 0, 255).astype(np.uint8), 'RGB').save(p, optimize=True)
    print(f'{name}: normal map -> {os.path.getsize(p) / 1e6:.1f} MB')


def grade_photo_cliff(name, normal_name, src, stops, k):
    L = highpass(lum(load(src)), 48)
    save(name, ramp(norm_t(L, k), stops))
    save_normal(normal_name, np.clip(0.5 + (L - L.mean()) * 1.6, 0, 1), 14.0)


def grade_rock_grain(name, src, grain_src, stops, k, grain):
    L = lum(load(src))
    g = highpass(lum(tiled(grain_src, 4)), 24)
    g = (g - g.mean()) / max(float(g.std()), 1e-3)
    save(name, ramp(norm_t(L, k) + g * grain, stops))


def periodic_noise(octaves, seed=7):
    """Seamless soft noise for one tile: random-phase waves with whole cycles per tile."""
    rng = np.random.default_rng(seed)
    y, x = np.mgrid[0:SIZE, 0:SIZE].astype(np.float32) / SIZE * 2.0 * np.pi
    out = np.zeros((SIZE, SIZE), np.float32)
    for lo, hi, amp, count in octaves:
        for _ in range(count):
            fx, fy = rng.integers(-hi, hi + 1, 2)
            if max(abs(fx), abs(fy)) < lo:
                continue
            out += amp * np.cos(fx * x + fy * y + rng.uniform(0, 2 * np.pi))
    return out / max(float(np.abs(out).max()), 1e-3)


def grade_snow(name, src, stops, k, drift):
    """Snow's own grain is centimetre-scale and vanishes past a few metres, leaving a flat white
    plane; soft wind-packed undulation (2-6 m and ~1 m) keeps snowfields readable."""
    L = lum(load(src))
    n = periodic_noise([(2, 6, 1.0, 14), (8, 14, 0.45, 18)])
    save(name, ramp(norm_t(L, k) + n * drift, stops))


def grade_forest(name, src):
    a = load(src)
    L = lum(a)
    t = norm_t(L, 0.16)
    green = np.clip((a[..., 1] - a[..., 0] + 0.05) / 0.14, 0.0, 1.0)
    green = green * green * (3 - 2 * green)
    soil = ramp(t, three((46, 38, 26), (84, 68, 46), (120, 100, 68)))
    moss = ramp(t, three((42, 58, 26), (72, 94, 40), (106, 128, 58)))
    save(name, soil * (1 - green[..., None]) + moss * green[..., None])


def grade_dirt_stone(name, src):
    a = load(src)
    L = lum(a)
    chroma = a.max(-1) - a.min(-1)
    stone = np.clip((0.26 - chroma) / 0.14, 0.0, 1.0)
    stone = stone * stone * (3 - 2 * stone)
    t = norm_t(L, 0.14)
    soil = ramp(t, three((74, 54, 36), (108, 82, 56), (136, 108, 76)))
    st = ramp(np.clip(0.5 + (L - L.mean()) * 3.0, 0, 1), three((78, 76, 72), (112, 109, 104), (138, 134, 128)))
    # Thin dark rim around each stone reads as the stone sitting in the soil.
    rim = np.clip(stone * (1 - stone) * 4.0, 0, 1)[..., None] * 0.22
    out = soil * (1 - stone[..., None]) + st * stone[..., None]
    save(name, out * (1 - rim))


if __name__ == '__main__':
    grade_simple('Realm_Grass_Albedo.png', IDY + '/Ground/Grass/Grass_Albedo.png', three((48, 74, 28), (80, 110, 42), (118, 144, 60)), 0.16)
    grade_forest('Realm_Forest_Albedo.png', IDY + '/Ground/Forest/Forest_Albedo.png')
    grade_dirt_stone('Realm_Dirt_Albedo.png', IDY + '/Ground/Dirt & Stone/Dirt_Stone_Albedo.png')
    grade_simple('Realm_Cobble_Albedo.png', IDY + '/Ground/Cobblestone/Cobblestone_Albedo.png', three((56, 53, 48), (106, 101, 93), (148, 142, 131)), 0.19)
    grade_rock_grain('Realm_Rock_Albedo.png', IDY + '/Ground/Rock/Rock_Albedo.png', TOOLKIT + '/Rock (Basic).jpg', three((38, 37, 35), (76, 74, 69), (110, 106, 99)), 0.14, 0.08)
    grade_photo_cliff('Realm_Cliff_Albedo.png', 'Realm_Cliff_Normal.png', TOOLKIT + '/Cliff (Layered Rock).jpg', three((30, 27, 24), (66, 61, 55), (104, 97, 87)), 0.21)
    grade_simple('Realm_Sand_Albedo.png', IDY + '/Ground/Sand/Sand_Albedo.png', three((150, 114, 72), (188, 152, 104), (218, 188, 140)), 0.14)
    grade_snow('Realm_Snow_Albedo.png', TOOLKIT + '/Snow.jpg', three((158, 168, 188), (210, 217, 231), (240, 243, 249)), 0.2, 0.16)
