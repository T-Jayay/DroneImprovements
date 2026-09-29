"""Draw the DroneImprovements icons with Pillow.

    python tools/make_icons.py

Writes three 256x256 PNGs:

- src/DroneImprovements/Assets/texDroneTeleportIcon.png and
  texDroneDisconnectIcon.png: the skill icons, embedded in the DLL;
- thunderstore/DroneImprovements/icon.png: the package icon, also shown
  in the Risk of Options mod list.

They copy the style of the vanilla drone skill icons: a dark glitch
background, glowing corner brackets and a glowing one-colour glyph. Each
glyph is drawn as a white-on-black mask at WORK_SIZE and coloured by
compose(). The output only depends on the Pillow version: Pillow 12.3.0
reproduces the committed PNGs byte for byte.
"""
from __future__ import annotations

import math
import random
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFilter

WORK_SIZE = 1024  # drawn large, then downscaled for antialiasing
ICON_SIZE = 256
REPO = Path(__file__).resolve().parent.parent
ASSETS = REPO / "src" / "DroneImprovements" / "Assets"
PACKAGE_ICON = REPO / "thunderstore" / "DroneImprovements" / "icon.png"

Color = tuple[int, int, int]


def background(seed: int, color: Color = (255, 255, 255)) -> Image.Image:
    """Return the near-black background with faint glitch blocks."""
    rng = random.Random(seed)
    image = Image.new("RGB", (WORK_SIZE, WORK_SIZE), (6, 6, 8))
    draw = ImageDraw.Draw(image)
    # Dark pixel/glitch blocks like the vanilla drone icons, slightly
    # tinted with the glyph colour.
    for _ in range(260):
        w = rng.choice([24, 32, 48, 64, 96, 128])
        h = rng.choice([16, 24, 32, 48])
        x = rng.randrange(0, WORK_SIZE, 16)
        y = rng.randrange(0, WORK_SIZE, 16)
        value = rng.randint(10, 30)
        tint = rng.random() * 0.12
        fill = tuple(int(value + c * tint) for c in color)
        draw.rectangle([x, y, x + w, y + h], fill=fill)
    # Grey scanlines across the whole width.
    for _ in range(40):
        y = rng.randrange(0, WORK_SIZE, 8)
        value = rng.randint(22, 40)
        draw.rectangle([0, y, WORK_SIZE, y + rng.choice([4, 8])],
                       fill=(value, value, value))
    return image


def bracket_mask() -> Image.Image:
    """Return the mask of the double-line chamfered corner brackets."""
    mask = Image.new("L", (WORK_SIZE, WORK_SIZE), 0)
    draw = ImageDraw.Draw(mask)
    for inset, width in ((34, 64), (140, 40)):
        arm = 330 - inset // 3
        chamfer = 90
        for sign_x, sign_y in ((1, 1), (-1, 1), (1, -1), (-1, -1)):
            corner_x = inset if sign_x == 1 else WORK_SIZE - inset
            corner_y = inset if sign_y == 1 else WORK_SIZE - inset
            points = [
                (corner_x + sign_x * arm, corner_y),
                (corner_x + sign_x * chamfer, corner_y),
                (corner_x, corner_y + sign_y * chamfer),
                (corner_x, corner_y + sign_y * arm),
            ]
            draw.line(points, fill=255, width=width, joint="curve")
    # Small notches in the middle of each edge, like the circuit tabs on
    # the vanilla icons: (centre x, centre y, width, height).
    middle, edge = WORK_SIZE // 2, WORK_SIZE - 30
    for cx, cy, w, h in ((middle, 30, 160, 26), (middle, edge, 160, 26),
                         (30, middle, 26, 160), (edge, middle, 26, 160)):
        draw.rectangle([cx - w // 2, cy - h // 2, cx + w // 2, cy + h // 2],
                       fill=255)
    return mask


def compose(glyph: Image.Image, color: Color, seed: int,
            scale: float = 1.0) -> Image.Image:
    """Colour a glyph mask and put it on the background and brackets.

    scale enlarges the glyph around the centre before composing.
    """
    if scale != 1:
        big = glyph.resize((int(WORK_SIZE * scale), int(WORK_SIZE * scale)),
                           Image.Resampling.BICUBIC)
        offset = (big.width - WORK_SIZE) // 2
        glyph = big.crop((offset, offset, offset + WORK_SIZE,
                          offset + WORK_SIZE))
    size = (WORK_SIZE, WORK_SIZE)
    base = background(seed, color).convert("RGBA")
    solid = Image.new("RGBA", size, color + (255,))

    # Frame: a slightly darker tint of the glyph colour.
    dark = tuple(int(c * 0.72) for c in color)
    base.paste(Image.new("RGBA", size, dark + (255,)), (0, 0), bracket_mask())

    # Glow: the blurred glyph, added on top.
    glow = glyph.filter(ImageFilter.GaussianBlur(38))
    glow_rgb = Image.merge("RGB", [
        ImageChops.multiply(glow, Image.new("L", size, c)) for c in color])
    lit = ImageChops.add(base.convert("RGB"), glow_rgb)
    base = Image.merge("RGBA", (*lit.split(), Image.new("L", size, 255)))

    # The solid glyph with a brighter core.
    base.paste(solid, (0, 0), glyph)
    core = glyph.filter(ImageFilter.MinFilter(21)).filter(
        ImageFilter.GaussianBlur(10))
    highlight = tuple(min(255, int(c + (255 - c) * 0.6)) for c in color)
    base.paste(Image.new("RGBA", size, highlight + (255,)), (0, 0),
               ImageChops.multiply(core, Image.new("L", size, 150)))
    return base.resize((ICON_SIZE, ICON_SIZE), Image.Resampling.LANCZOS)


def teleport_glyph() -> Image.Image:
    """Teleport skill: a dash trail and arrow reaching a player."""
    glyph = Image.new("L", (WORK_SIZE, WORK_SIZE), 0)
    draw = ImageDraw.Draw(glyph)
    # Player silhouette (head and shoulders) on the right.
    px, py = 690, 470
    draw.ellipse([px - 78, py - 250, px + 78, py - 94], fill=255)
    draw.rounded_rectangle([px - 150, py - 70, px + 150, py + 240],
                           radius=120, fill=255)
    draw.rectangle([px - 150, py + 150, px + 150, py + 260], fill=255)
    # Cut a gap around the arrow tip so the shapes read separately.
    draw.polygon([(420, 520), (560, 440), (560, 600)], fill=0)
    # Dash trail from the lower left, broken into segments like the
    # gunner drone icon; each segment is thicker than the one before.
    trail = [(170, 700), (250, 650), (330, 600), (410, 555)]
    widths = [40, 58, 76, 94]
    angle = math.atan2(-50, 80)
    dx, dy = math.cos(angle) * 60, math.sin(angle) * 60
    for (x, y), width in zip(trail, widths):
        draw.line([(x - dx, y - dy), (x + dx, y + dy)], fill=255,
                  width=width)
    # Arrow head pointing at the player.
    draw.polygon([(585, 520), (400, 400), (450, 520), (400, 640)], fill=255)
    # Sparkle burst at the destination: eight thin rays, each a
    # triangle from the centre to a 20 px wide outer end. The
    # diagonal rays are shorter.
    cx, cy = 560, 330
    for degrees in range(0, 360, 45):
        radians = math.radians(degrees)
        length = 90 if degrees % 90 == 0 else 55
        x = cx + math.cos(radians) * length
        y = cy + math.sin(radians) * length
        side_x, side_y = math.sin(radians) * 10, math.cos(radians) * 10
        draw.polygon([(cx, cy), (x + side_x, y - side_y), (x, y),
                      (x - side_x, y + side_y)], fill=255)
    return glyph


def disconnect_glyph() -> Image.Image:
    """Disconnect skill: a plug pulled out of its socket, sparking."""
    glyph = Image.new("L", (WORK_SIZE, WORK_SIZE), 0)
    draw = ImageDraw.Draw(glyph)
    angle = -35  # the whole plug is tilted, like the vanilla icons

    layer = Image.new("L", (WORK_SIZE, WORK_SIZE), 0)
    layer_draw = ImageDraw.Draw(layer)
    cy = WORK_SIZE // 2
    # Plug (left): cable, body, two prongs.
    layer_draw.rectangle([90, cy - 34, 300, cy + 34], fill=255)
    layer_draw.rounded_rectangle([280, cy - 120, 440, cy + 120], radius=36,
                                 fill=255)
    layer_draw.rectangle([440, cy - 80, 540, cy - 44], fill=255)
    layer_draw.rectangle([440, cy + 44, 540, cy + 80], fill=255)
    # Socket (right): body with two holes, cable.
    layer_draw.rounded_rectangle([620, cy - 120, 790, cy + 120], radius=36,
                                 fill=255)
    layer_draw.rectangle([620, cy - 84, 690, cy - 40], fill=0)
    layer_draw.rectangle([620, cy + 40, 690, cy + 84], fill=0)
    layer_draw.rectangle([770, cy - 34, 960, cy + 34], fill=255)
    layer = layer.rotate(angle, resample=Image.Resampling.BICUBIC,
                         center=(WORK_SIZE // 2, WORK_SIZE // 2))
    glyph.paste(255, (0, 0), layer)

    # Sparks in the gap: zigzags from a start point.
    for (x0, y0), points in (
        ((520, 400), [(0, 0), (40, -60), (15, -60), (60, -130)]),
        ((545, 640), [(0, 0), (-35, 60), (-10, 60), (-55, 130)]),
    ):
        draw.line([(x0 + x, y0 + y) for x, y in points], fill=255, width=30,
                  joint="curve")
    return glyph


def drone_glyph() -> Image.Image:
    """Package icon: a drone with signal arcs above it."""
    glyph = Image.new("L", (WORK_SIZE, WORK_SIZE), 0)
    draw = ImageDraw.Draw(glyph)
    cx, cy = WORK_SIZE // 2, 560
    # Rotor arms and blades.
    for side in (-1, 1):
        ax = cx + side * 250
        draw.line([(cx + side * 110, cy - 40), (ax, cy - 150)], fill=255,
                  width=46)
        draw.rectangle([ax - 18, cy - 210, ax + 18, cy - 140], fill=255)
        draw.rounded_rectangle([ax - 150, cy - 250, ax + 150, cy - 210],
                               radius=20, fill=255)
    # Body with a lens.
    draw.rounded_rectangle([cx - 170, cy - 90, cx + 170, cy + 130],
                           radius=60, fill=255)
    draw.ellipse([cx - 62, cy - 20, cx + 62, cy + 104], fill=0)
    draw.ellipse([cx - 34, cy + 8, cx + 34, cy + 76], fill=255)
    # Landing skids.
    for side in (-1, 1):
        draw.line([(cx + side * 110, cy + 120), (cx + side * 150, cy + 210)],
                  fill=255, width=34)
    draw.rounded_rectangle([cx - 230, cy + 200, cx + 230, cy + 236],
                           radius=16, fill=255)
    # Signal arcs above.
    for radius, width in ((90, 30), (160, 30)):
        draw.arc([cx - radius, 180 - radius, cx + radius, 180 + radius],
                 start=210, end=330, fill=255, width=width)
    draw.ellipse([cx - 22, 158, cx + 22, 202], fill=255)
    return glyph


def main() -> None:
    """Draw every icon and save it."""
    icons = {
        ASSETS / "texDroneTeleportIcon.png":
            compose(teleport_glyph(), (53, 214, 255), seed=3, scale=1.12),
        ASSETS / "texDroneDisconnectIcon.png":
            compose(disconnect_glyph(), (255, 170, 40), seed=7, scale=1.12),
        PACKAGE_ICON: compose(drone_glyph(), (80, 200, 255), seed=11),
    }
    for path, icon in icons.items():
        path.parent.mkdir(parents=True, exist_ok=True)
        icon.save(path)
        print(f"wrote {path.relative_to(REPO).as_posix()}")


if __name__ == "__main__":
    main()
