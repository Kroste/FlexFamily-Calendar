#!/usr/bin/env python3
"""
Generiert das FlexFamily-Calendar-App-Icon reproduzierbar aus Code.

Motiv: dunkelblauer Rundeck-Grund (Marken-Akzent) + weißes Kalenderblatt mit
farbigen Punkten für die Familienmitglieder — bei 16×16 als Kalender erkennbar
(weiße Fläche mit farbigem Header), bei 256×256 mit Personen-Punkten lesbar.

Ausgabe:
- src/Assets/flexfamily-calendar.png (256×256, transparent, master)
- src/Assets/flexfamily-calendar.ico (multi-res 16/24/32/48/64/128/256)

Vorbedingung: pip install pillow (bzw. `python3-pillow` per Distro).
"""

import os
from pathlib import Path
from PIL import Image, ImageDraw

SIZE = 256
OUT_DIR = Path(__file__).resolve().parent.parent / "src" / "Assets"
APP_NAME = "flexfamily-calendar"

ACCENT = (26, 35, 126, 255)          # #1A237E — Marken-Indigo (App-Header)
ACCENT_LIGHT = (63, 76, 179, 255)    # etwas heller für den Kalender-Header-Streifen
PAPER = (255, 255, 255, 255)
RING = (255, 255, 255, 255)

# Personenfarben aus der App-Palette (UserColorPalette).
PERSON_COLORS = [
    (46, 134, 193, 255),   # Blau
    (230, 126, 34, 255),   # Orange
    (39, 174, 96, 255),    # Grün
    (142, 68, 173, 255),   # Violett
    (192, 57, 43, 255),    # Rot
]


def rounded_rect(draw, box, radius, fill):
    """Rundeck-Rechteck; PIL kann das nativ ab 9.x über draw.rounded_rectangle."""
    draw.rounded_rectangle(box, radius=radius, fill=fill)


def draw_motif(img):
    d = ImageDraw.Draw(img)

    # Rundeck-Grund (App-Akzent).
    rounded_rect(d, (0, 0, SIZE, SIZE), radius=44, fill=ACCENT)

    # Kalenderblatt (weißes Rundeck mit dünnem Abstand zum Rand).
    pad_x = 44
    pad_top = 68
    pad_bot = 40
    paper_box = (pad_x, pad_top, SIZE - pad_x, SIZE - pad_bot)
    rounded_rect(d, paper_box, radius=14, fill=PAPER)

    # Header-Streifen im Kalender (Indigo hell).
    header_h = 34
    header_box = (paper_box[0], paper_box[1],
                  paper_box[2], paper_box[1] + header_h)
    rounded_rect(d, header_box, radius=14, fill=ACCENT_LIGHT)
    # Untere Ecken des Header-Streifens wieder eckig machen (Overlay auf dem Blatt).
    d.rectangle((header_box[0], header_box[1] + header_h - 14,
                 header_box[2], header_box[1] + header_h), fill=ACCENT_LIGHT)

    # Zwei kleine "Aufhänger" oben (typisches Kalender-Detail).
    ring_r = 7
    ring_y = paper_box[1] - 4
    for cx in (paper_box[0] + 40, paper_box[2] - 40):
        d.ellipse((cx - ring_r, ring_y - ring_r, cx + ring_r, ring_y + ring_r),
                  fill=RING)

    # Familien-Punkte im Kalenderblatt (2 Reihen × 3 Spalten, letzte Spalte
    # unten leer, weil wir nur 5 Farben zeigen — passt zur echten Familie).
    inner_top = paper_box[1] + header_h + 22
    inner_bot = paper_box[3] - 22
    inner_left = paper_box[0] + 24
    inner_right = paper_box[2] - 24
    dot_r = 18
    cols = 3
    rows = 2
    step_x = (inner_right - inner_left) / (cols - 1)
    step_y = (inner_bot - inner_top) / (rows - 1)
    i = 0
    for r in range(rows):
        for c in range(cols):
            if i >= len(PERSON_COLORS):
                break
            cx = int(inner_left + c * step_x)
            cy = int(inner_top + r * step_y)
            d.ellipse((cx - dot_r, cy - dot_r, cx + dot_r, cy + dot_r),
                      fill=PERSON_COLORS[i])
            i += 1


def main():
    OUT_DIR.mkdir(parents=True, exist_ok=True)

    master = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    draw_motif(master)

    png_path = OUT_DIR / f"{APP_NAME}.png"
    master.save(png_path, format="PNG")
    print(f"geschrieben: {png_path}")

    # Multi-Res-ICO: Windows verwendet je nach Kontext die passende Größe.
    ico_path = OUT_DIR / f"{APP_NAME}.ico"
    sizes = [(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)]
    master.save(ico_path, format="ICO", sizes=sizes)
    print(f"geschrieben: {ico_path} (Größen: {', '.join(f'{w}x{h}' for w, h in sizes)})")


if __name__ == "__main__":
    main()
