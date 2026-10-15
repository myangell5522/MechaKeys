"""
Mecha Keys icon + animation generator (pixel art, drawn procedurally).

Outputs (into <mod dir>):
  icon.png                     80x80   tModLoader mod list icon (static, frame 0)
  icon_small.png               30x30   mod browser small icon
  icon_workshop.png            512x512 static workshop preview (used by in-game publisher)
  Assets/Textures/LogoSheet.png   80 x (80*F)  base frames (keycap etc.), vertical strip
  Assets/Textures/LogoAccent.png  80 x (80*F)  white accent frames, tinted at runtime
and into <workshop dir>:
  preview.gif                  512x512 animated workshop preview (< 1 MB)
"""
import math
import os
import sys

from PIL import Image

N = 40  # logical canvas (scaled x2 for the 80px icon)

# ----------------------------------------------------------------- palette
def hx(s, a=255):
    s = s.lstrip("#")
    return (int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16), a)


OUT = hx("0b0a17")
BG0 = hx("15132b")
BG1 = hx("1d1a3c")
BG2 = hx("272351")
FR_HI = hx("4b45a3")
FR_MID = hx("332e72")
PLATE_TOP = hx("454a60")
PLATE = hx("2e3142")
PLATE_DK = hx("1c1e29")
NB_TOP = hx("5a5e78")
NB_SIDE = hx("40435a")
NB_DK = hx("2c2e3f")
CAP_HI = hx("fffaf0")
CAP_TOP = hx("efe5cf")
CAP_SIDE = hx("dccaa4")
CAP_SIDE_DK = hx("a58f69")
CAP_OUT = hx("2a2433")
CAP_FRONT = hx("c7b48e")
CAP_DISH = hx("e2d6bc")
CAP_EDGE = hx("fff3dc")
LEGEND = hx("3b3350")
HOUSE = hx("3a3d50")
HOUSE_HI = hx("575b75")

# accent colours (one per press in the animated previews): switch stem colours
ACCENTS = [
    ("Blue", hx("3fa9ff")),
    ("Red", hx("ff4d5e")),
    ("Jade", hx("36d39a")),
    ("Brown", hx("d08a3c")),
    ("Yellow", hx("ffd23f")),
    ("Panda", hx("ff7ac8")),
    ("Silent", hx("a77bff")),
]

# press timeline: (cap depth px, ring radius step or None, spark?)
TIMELINE = [
    (0, None, False),
    (1, None, False),
    (3, None, False),
    (4, 0, True),
    (4, 1, True),
    (3, 2, False),
    (2, 3, False),
    (0, 4, False),
    (-1, 5, False),
    (0, None, False),
]
F = len(TIMELINE)


class Canvas:
    def __init__(self):
        self.base = Image.new("RGBA", (N, N), (0, 0, 0, 0))
        self.acc = Image.new("RGBA", (N, N), (0, 0, 0, 0))  # white-ish, tinted later
        self.bp = self.base.load()
        self.ap = self.acc.load()

    def px(self, x, y, c):
        if 0 <= x < N and 0 <= y < N:
            self.bp[x, y] = c
            # base pixels hide accent pixels underneath
            self.ap[x, y] = (0, 0, 0, 0)

    def ax(self, x, y, v, a=255):
        """Accent pixel: v = brightness (0..255) that multiplies the tint."""
        if 0 <= x < N and 0 <= y < N:
            self.ap[x, y] = (v, v, v, a)

    def rect(self, x0, y0, x1, y1, c):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                self.px(x, y, c)

    def hline(self, x0, x1, y, c):
        for x in range(x0, x1 + 1):
            self.px(x, y, c)


def lerp(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(4))


def draw_background(c: Canvas):
    # rounded panel with stepped corners
    corner = {0: 3, 1: 2, 2: 1}
    for y in range(N):
        for x in range(N):
            dy = min(y, N - 1 - y)
            dx = min(x, N - 1 - x)
            if dy in corner and dx < corner[dy]:
                continue
            t = y / (N - 1)
            col = lerp(BG2, BG0, t)
            # soft vignette spot behind the key
            d = math.hypot(x - 19.5, y - 15) / 22
            col = lerp(hx("2c2766"), col, min(1, d))
            c.px(x, y, col)
    # frame: outline + highlight ring
    for y in range(N):
        for x in range(N):
            if c.bp[x, y][3] == 0:
                continue
            edge = False
            for ox, oy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + ox, y + oy
                if not (0 <= nx < N and 0 <= ny < N) or c.bp[nx, ny][3] == 0:
                    edge = True
            if edge:
                c.px(x, y, OUT)
    for y in range(N):
        for x in range(N):
            if c.bp[x, y] == OUT or c.bp[x, y][3] == 0:
                continue
            near = any(
                0 <= x + ox < N and 0 <= y + oy < N and c.bp[x + ox, y + oy] == OUT
                for ox, oy in ((1, 0), (-1, 0), (0, 1), (0, -1))
            )
            if near:
                c.px(x, y, FR_HI if y < N * 0.5 else FR_MID)


def draw_plate(c: Canvas):
    # keyboard plate / case strip
    c.rect(4, 28, 35, 28, PLATE_TOP)
    c.rect(4, 29, 35, 32, PLATE)
    c.rect(4, 33, 35, 33, PLATE_DK)
    c.px(4, 28, PLATE)
    c.px(35, 28, PLATE)
    # little screws / texture
    for x in (7, 32):
        c.px(x, 31, PLATE_DK)


def draw_neighbour(c: Canvas, x0, x1):
    # a smaller, darker keycap sitting on the plate (left/right of the hero key)
    c.rect(x0, 20, x1, 27, NB_SIDE)
    c.rect(x0 + 1, 20, x1 - 1, 24, NB_TOP)
    c.hline(x0, x1, 27, NB_DK)
    c.hline(x0 + 1, x1 - 1, 19, CAP_OUT)
    for y in range(20, 28):
        c.px(x0 - 1, y, CAP_OUT)
        c.px(x1 + 1, y, CAP_OUT)


def draw_switch(c: Canvas, depth):
    # housing
    c.rect(13, 25, 26, 27, HOUSE)
    c.hline(13, 26, 25, HOUSE_HI)
    c.px(12, 26, CAP_OUT)
    c.px(27, 26, CAP_OUT)
    c.px(12, 27, CAP_OUT)
    c.px(27, 27, CAP_OUT)
    # stem (accent) from the cap bottom down to the housing
    cap_bottom = 22 + depth
    for y in range(cap_bottom + 1, 25):
        c.ax(19, y, 255)
        c.ax(20, y, 200)
        if y == 24:
            c.ax(18, y, 170)
            c.ax(21, y, 170)
    # LED glow on the housing top
    for x in range(15, 25):
        c.ax(x, 25, 150 if x in (15, 24) else 220, 200)


def draw_cap(c: Canvas, depth):
    d = depth
    top, bot = 7 + d, 22 + d          # outline rows
    face0, face1 = top + 1, top + 9   # dished top face rows
    # skirt: trapezoid that widens toward the bottom
    for y in range(top, bot + 1):
        k = (y - top) / (bot - top)
        xl = int(round(13 - 3 * k))
        xr = int(round(26 + 3 * k))
        for x in range(xl, xr + 1):
            if x <= xl + 1:
                col = CAP_SIDE
            elif x >= xr - 1:
                col = CAP_SIDE_DK
            else:
                col = CAP_FRONT
            c.px(x, y, col)
        c.px(xl - 1, y, CAP_OUT)
        c.px(xr + 1, y, CAP_OUT)
    c.hline(13, 26, top - 1, CAP_OUT)
    c.hline(10, 29, bot + 1, CAP_OUT)
    c.hline(11, 28, bot, CAP_SIDE_DK)
    # top face (lighter, slightly narrower than the skirt at that height)
    for y in range(face0, face1 + 1):
        for x in range(14, 26):
            c.px(x, y, CAP_TOP)
    c.hline(14, 25, face0, CAP_HI)
    for y in range(face0, face1):
        c.px(14, y, CAP_HI)
    c.hline(15, 24, face1, CAP_DISH)          # dish shadow
    c.hline(14, 25, face1 + 1, CAP_EDGE)      # crisp edge between face and front skirt
    # legend: a small speaker + sound arcs
    lx, ly = 16, top + 3
    for (x, y) in [(lx, ly + 1), (lx, ly + 2), (lx + 1, ly + 1), (lx + 1, ly + 2),
                   (lx + 2, ly), (lx + 2, ly + 1), (lx + 2, ly + 2), (lx + 2, ly + 3),
                   (lx + 3, ly - 1), (lx + 3, ly), (lx + 3, ly + 1), (lx + 3, ly + 2), (lx + 3, ly + 3), (lx + 3, ly + 4)]:
        c.px(x, y, LEGEND)
    for (x, y) in [(lx + 5, ly + 1), (lx + 5, ly + 2)]:
        c.px(x, y, LEGEND)
    for (x, y) in [(lx + 6, ly - 1), (lx + 7, ly), (lx + 7, ly + 1), (lx + 7, ly + 2), (lx + 7, ly + 3), (lx + 6, ly + 4)]:
        c.px(x, y, LEGEND)


def draw_rings(c: Canvas, depth, step):
    if step is None:
        return
    cx, cy = 19.5, 15 + depth
    fade = [255, 235, 200, 150, 100, 55][step]
    for k, extra in enumerate((0, 4)):
        r = 12 + step * 2.2 + extra
        if r > 21:
            continue
        alpha = int(fade * (1 - 0.45 * k))
        for i in range(720):
            a = i / 720 * 2 * math.pi
            x = int(round(cx + math.cos(a) * r))
            y = int(round(cy + math.sin(a) * r * 0.82))
            if abs(x - cx) < 10.5 or y > 27 or y < 2 or not (1 <= x < N - 1):
                continue
            if c.bp[x, y][3] and c.bp[x, y] in (OUT, FR_HI, FR_MID):
                continue
            c.ax(x, y, 255, alpha)
            if k == 0 and step <= 1:
                c.ax(x, y, 255, 255)


def draw_sparks(c: Canvas, depth):
    y0 = 6 + depth
    for (x, y) in [(10, y0), (9, y0 - 1), (8, y0 - 2),
                   (29, y0), (30, y0 - 1), (31, y0 - 2),
                   (19, y0 - 2), (20, y0 - 2), (19, y0 - 3), (20, y0 - 3)]:
        c.ax(x, y, 255)


def frame(i):
    depth, step, spark = TIMELINE[i]
    c = Canvas()
    draw_background(c)
    draw_plate(c)
    draw_neighbour(c, 3, 8)
    draw_neighbour(c, 31, 36)
    draw_rings(c, depth, step)
    draw_switch(c, depth)
    draw_cap(c, depth)
    if spark:
        draw_sparks(c, depth)
    return c.base, c.acc


def tint(acc, color):
    out = Image.new("RGBA", acc.size, (0, 0, 0, 0))
    ap, op = acc.load(), out.load()
    for y in range(acc.size[1]):
        for x in range(acc.size[0]):
            v, _, _, a = ap[x, y]
            if a:
                op[x, y] = (color[0] * v // 255, color[1] * v // 255, color[2] * v // 255, a)
    return out


def compose(base, acc, color):
    img = base.copy()
    img.alpha_composite(tint(acc, color))
    return img


def up(img, s):
    return img.resize((img.width * s, img.height * s), Image.NEAREST)


def main(mod_dir, workshop_dir):
    frames = [frame(i) for i in range(F)]
    blue = ACCENTS[0][1]

    # static icons
    icon80 = up(compose(*frames[4], blue), 2)  # the "click!" frame reads best as a still
    icon80.save(os.path.join(mod_dir, "icon.png"))
    icon80.resize((30, 30), Image.LANCZOS).save(os.path.join(mod_dir, "icon_small.png"))
    ws = Image.new("RGBA", (512, 512), (0, 0, 0, 0))
    ws.alpha_composite(up(compose(*frames[4], blue), 12), (16, 16))
    ws.save(os.path.join(mod_dir, "icon_workshop.png"))

    # in-game sheets
    tex = os.path.join(mod_dir, "Assets", "Textures")
    os.makedirs(tex, exist_ok=True)
    sb = Image.new("RGBA", (80, 80 * F))
    sa = Image.new("RGBA", (80, 80 * F))
    for i, (b, a) in enumerate(frames):
        sb.paste(up(b, 2), (0, 80 * i))
        sa.paste(up(a, 2), (0, 80 * i))
    sb.save(os.path.join(tex, "LogoSheet.png"))
    sa.save(os.path.join(tex, "LogoAccent.png"))

    # animated workshop preview: one press per switch colour, with idle beats between
    os.makedirs(workshop_dir, exist_ok=True)
    seq, durs = [], []
    for name, col in ACCENTS:
        for i in range(F):
            seq.append(compose(*frames[i], col))
            durs.append(60 if i in range(1, F - 1) else 70)
        seq.append(compose(*frames[0], col))
        durs.append(260)
    # merge identical consecutive frames to save bytes
    m_seq, m_dur = [], []
    for im, d in zip(seq, durs):
        if m_seq and im.tobytes() == m_seq[-1].tobytes():
            m_dur[-1] += d
        else:
            m_seq.append(im)
            m_dur.append(d)
    big = []
    for im in m_seq:
        canvas = Image.new("RGBA", (512, 512), (0, 0, 0, 0))
        canvas.alpha_composite(up(im, 12), (16, 16))
        flat = Image.new("RGB", (512, 512), (14, 12, 28))
        flat.paste(canvas, mask=canvas.split()[3])
        big.append(flat)
    # one shared palette keeps every frame consistent and the file small
    pal_src = Image.new("RGB", (512, 512 * len(big)))
    for i, im in enumerate(big):
        pal_src.paste(im, (0, 512 * i))
    pal = pal_src.quantize(colors=255, method=Image.MAXCOVERAGE, dither=Image.NONE)
    q = [im.quantize(palette=pal, dither=Image.NONE) for im in big]
    gif = os.path.join(workshop_dir, "preview.gif")
    q[0].save(gif, save_all=True, append_images=q[1:], duration=m_dur, loop=0, optimize=True, disposal=1)
    print("frames", len(q), "gif bytes", os.path.getsize(gif))

    # small animated preview for docs/README
    small = [im.resize((128, 128), Image.NEAREST) for im in big]
    small[0].save(os.path.join(workshop_dir, "preview_small.gif"), save_all=True, append_images=small[1:],
                  duration=m_dur, loop=0, optimize=True)


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
