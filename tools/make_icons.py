"""Generates the drone ability icons in the style of the vanilla drone skill icons
(black glitch background, glowing corner brackets, single-colour glowing glyph).

    python tools/make_icons.py

Writes 256x256 PNGs into src/DroneImprovements/Assets/.
"""
import math
import os
import random

from PIL import Image, ImageChops, ImageDraw, ImageFilter

S = 1024  # working resolution, downscaled at the end for anti-aliasing
OUT = 256
HERE = os.path.dirname(os.path.abspath(__file__))
ASSETS = os.path.join(HERE, "..", "src", "DroneImprovements", "Assets")


def background(seed, color=(255, 255, 255)):
    rng = random.Random(seed)
    img = Image.new("RGB", (S, S), (6, 6, 8))
    d = ImageDraw.Draw(img)
    # Dark pixel/glitch blocks like the vanilla drone icons.
    for _ in range(260):
        w = rng.choice([24, 32, 48, 64, 96, 128])
        h = rng.choice([16, 24, 32, 48])
        x = rng.randrange(0, S, 16)
        y = rng.randrange(0, S, 16)
        v = rng.randint(10, 30)
        t = rng.random() * 0.12
        d.rectangle([x, y, x + w, y + h], fill=tuple(int(v + c * t) for c in color))
    for _ in range(40):
        y = rng.randrange(0, S, 8)
        v = rng.randint(22, 40)
        d.rectangle([0, y, S, y + rng.choice([4, 8])], fill=(v, v, v))
    return img


def bracket_mask():
    """Double-line chamfered corner brackets around the edge."""
    m = Image.new("L", (S, S), 0)
    d = ImageDraw.Draw(m)
    for inset, width in ((34, 64), (140, 40)):
        arm = 330 - inset // 3
        ch = 90
        for sx, sy in ((1, 1), (-1, 1), (1, -1), (-1, -1)):
            ox = inset if sx == 1 else S - inset
            oy = inset if sy == 1 else S - inset
            pts = [
                (ox + sx * arm, oy),
                (ox + sx * ch, oy),
                (ox, oy + sy * ch),
                (ox, oy + sy * arm),
            ]
            d.line(pts, fill=255, width=width, joint="curve")
    # Small notches on the edges, like the circuit tabs on the originals.
    for cx, cy, w, h in ((S // 2, 30, 160, 26), (S // 2, S - 30, 160, 26), (30, S // 2, 26, 160), (S - 30, S // 2, 26, 160)):
        d.rectangle([cx - w // 2, cy - h // 2, cx + w // 2, cy + h // 2], fill=255)
    return m


def compose(glyph, color, seed, scale=1.12):
    if scale != 1:
        big = glyph.resize((int(S * scale), int(S * scale)), Image.BICUBIC)
        off = (big.width - S) // 2
        glyph = big.crop((off, off, off + S, off + S))
    base = background(seed, color).convert("RGBA")
    col = Image.new("RGBA", (S, S), color + (255,))

    # Frame: slightly darker tint of the glyph colour.
    frame = bracket_mask()
    dark = tuple(int(c * 0.72) for c in color)
    frame_layer = Image.new("RGBA", (S, S), dark + (255,))
    base.paste(frame_layer, (0, 0), frame)

    # Glow: blurred glyph, added on top.
    glow = glyph.filter(ImageFilter.GaussianBlur(38))
    glow_rgb = Image.merge("RGB", [ImageChops.multiply(glow, Image.new("L", (S, S), c)) for c in color])
    base = Image.merge("RGBA", (*ImageChops.add(base.convert("RGB"), glow_rgb).split(), Image.new("L", (S, S), 255)))

    # Solid glyph with a bright core.
    base.paste(col, (0, 0), glyph)
    core = glyph.filter(ImageFilter.MinFilter(21)).filter(ImageFilter.GaussianBlur(10))
    hi = tuple(min(255, int(c + (255 - c) * 0.6)) for c in color)
    base.paste(Image.new("RGBA", (S, S), hi + (255,)), (0, 0), ImageChops.multiply(core, Image.new("L", (S, S), 150)))
    return base.resize((OUT, OUT), Image.LANCZOS)


def teleport_glyph():
    g = Image.new("L", (S, S), 0)
    d = ImageDraw.Draw(g)
    # Player silhouette (head + shoulders) on the right.
    px, py = 690, 470
    d.ellipse([px - 78, py - 250, px + 78, py - 94], fill=255)
    d.rounded_rectangle([px - 150, py - 70, px + 150, py + 240], radius=120, fill=255)
    d.rectangle([px - 150, py + 150, px + 150, py + 260], fill=255)
    # Cut a clean gap around the arrow tip so the shapes read separately.
    d.polygon([(420, 520), (560, 440), (560, 600)], fill=0)
    # Dash trail from the lower left, broken into segments like the gunner icon.
    trail = [(170, 700), (250, 650), (330, 600), (410, 555)]
    widths = [40, 58, 76, 94]
    for (x, y), w in zip(trail, widths):
        ang = math.atan2(-50, 80)
        dx, dy = math.cos(ang) * 60, math.sin(ang) * 60
        d.line([(x - dx, y - dy), (x + dx, y + dy)], fill=255, width=w)
    # Arrow head pointing at the player.
    d.polygon([(585, 520), (400, 400), (450, 520), (400, 640)], fill=255)
    # Sparkle burst at the destination.
    cx, cy = 560, 330
    for a in range(0, 360, 45):
        r = 90 if a % 90 == 0 else 55
        x = cx + math.cos(math.radians(a)) * r
        y = cy + math.sin(math.radians(a)) * r
        d.polygon([(cx, cy), (x + math.sin(math.radians(a)) * 10, y - math.cos(math.radians(a)) * 10), (x, y), (x - math.sin(math.radians(a)) * 10, y + math.cos(math.radians(a)) * 10)], fill=255)
    return g


def disconnect_glyph():
    g = Image.new("L", (S, S), 0)
    d = ImageDraw.Draw(g)
    angle = -35  # whole glyph tilted like the vanilla icons

    layer = Image.new("L", (S, S), 0)
    ld = ImageDraw.Draw(layer)
    cy = S // 2
    # Plug (left): cable, body, two prongs.
    ld.rectangle([90, cy - 34, 300, cy + 34], fill=255)
    ld.rounded_rectangle([280, cy - 120, 440, cy + 120], radius=36, fill=255)
    ld.rectangle([440, cy - 80, 540, cy - 44], fill=255)
    ld.rectangle([440, cy + 44, 540, cy + 80], fill=255)
    # Socket (right): body with two holes, cable.
    ld.rounded_rectangle([620, cy - 120, 790, cy + 120], radius=36, fill=255)
    ld.rectangle([620, cy - 84, 690, cy - 40], fill=0)
    ld.rectangle([620, cy + 40, 690, cy + 84], fill=0)
    ld.rectangle([770, cy - 34, 960, cy + 34], fill=255)
    layer = layer.rotate(angle, resample=Image.BICUBIC, center=(S // 2, S // 2))
    g.paste(255, (0, 0), layer)

    # Sparks in the gap.
    for (x0, y0), pts in (
        ((520, 400), [(0, 0), (40, -60), (15, -60), (60, -130)]),
        ((545, 640), [(0, 0), (-35, 60), (-10, 60), (-55, 130)]),
    ):
        d.line([(x0 + x, y0 + y) for x, y in pts], fill=255, width=30, joint="curve")
    return g


def drone_glyph():
    g = Image.new("L", (S, S), 0)
    d = ImageDraw.Draw(g)
    cx, cy = S // 2, 560
    # Rotor arms and blades.
    for side in (-1, 1):
        ax = cx + side * 250
        d.line([(cx + side * 110, cy - 40), (ax, cy - 150)], fill=255, width=46)
        d.rectangle([ax - 18, cy - 210, ax + 18, cy - 140], fill=255)
        d.rounded_rectangle([ax - 150, cy - 250, ax + 150, cy - 210], radius=20, fill=255)
    # Body with a lens.
    d.rounded_rectangle([cx - 170, cy - 90, cx + 170, cy + 130], radius=60, fill=255)
    d.ellipse([cx - 62, cy - 20, cx + 62, cy + 104], fill=0)
    d.ellipse([cx - 34, cy + 8, cx + 34, cy + 76], fill=255)
    # Landing skids.
    for side in (-1, 1):
        d.line([(cx + side * 110, cy + 120), (cx + side * 150, cy + 210)], fill=255, width=34)
    d.rounded_rectangle([cx - 230, cy + 200, cx + 230, cy + 236], radius=16, fill=255)
    # Signal arcs above.
    for r, w in ((90, 30), (160, 30)):
        d.arc([cx - r, 180 - r, cx + r, 180 + r], start=210, end=330, fill=255, width=w)
    d.ellipse([cx - 22, 158, cx + 22, 202], fill=255)
    return g


def main():
    os.makedirs(ASSETS, exist_ok=True)
    teleport = compose(teleport_glyph(), (53, 214, 255), seed=3)
    disconnect = compose(disconnect_glyph(), (255, 170, 40), seed=7)
    teleport.save(os.path.join(ASSETS, "texDroneTeleportIcon.png"))
    disconnect.save(os.path.join(ASSETS, "texDroneDisconnectIcon.png"))

    # Thunderstore package icon: a drone with a signal link.
    package_icon = compose(drone_glyph(), (80, 200, 255), seed=11, scale=1.0)
    package_icon.save(os.path.join(HERE, "..", "thunderstore", "DroneImprovements", "icon.png"))
    print("wrote icons to", os.path.normpath(ASSETS))


if __name__ == "__main__":
    main()
