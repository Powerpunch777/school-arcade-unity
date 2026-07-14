from pathlib import Path
from PIL import Image, ImageDraw
import hashlib

ROOT = Path(__file__).resolve().parent
EFFECTS = ROOT.parent / "Effects"
ROOT.mkdir(parents=True, exist_ok=True)
EFFECTS.mkdir(parents=True, exist_ok=True)

MASTER_HEX = (
    "#000000", "#141416", "#2D2F33", "#575D65", "#8E98A5", "#D2D9E4", "#FFFFFF",
    "#E8D5B7", "#2B4E8C", "#497AD3", "#82AAFF", "#3D2314", "#6A4023", "#A87449",
    "#ECD09B", "#FCDB5D", "#FFE999", "#FFD4C0", "#E59A7A", "#123C24", "#216C3E",
    "#43AF6B", "#0B5757", "#00A2A2", "#4BF3E6", "#4A1525", "#8F1F2E", "#D9383A",
    "#FF7D7F", "#251C33", "#452F66", "#7F5EC4",
)
MASTER = {tuple(bytes.fromhex(value[1:])) for value in MASTER_HEX}

# Semantic names deliberately point only to the exact 32-color master above.
# Several earlier draft colors were near-matches and are intentionally removed.
HEX = {
    "ink": "#000000", "deep": "#141416", "green_deep": "#123C24",
    "green_dark": "#216C3E", "navy": "#2B4E8C", "shadow": "#2D2F33",
    "green_mid": "#216C3E", "green": "#43AF6B", "brown": "#3D2314",
    "purple_deep": "#4A1525", "slate": "#575D65", "slate_light": "#8E98A5",
    "leather": "#6A4023", "blue": "#497AD3", "sky": "#82AAFF", "green_light": "#43AF6B",
    "tan": "#A87449", "olive": "#43AF6B", "gray_light": "#8E98A5",
    "red_dark": "#8F1F2E", "brown_light": "#A87449", "orange": "#A87449",
    "gold_dark": "#ECD09B", "gold": "#FCDB5D", "gold_light": "#FFE999",
    "skin": "#FFD4C0", "red": "#D9383A", "white": "#FFFFFF",
    "sand": "#A87449", "sand_light": "#ECD09B", "ecru": "#E8D5B7",
}
P = {k: tuple(bytes.fromhex(v[1:])) for k, v in HEX.items()}


def rgba(color):
    return (*P[color], 255)


def transparent(size):
    return Image.new("RGBA", size, (0, 0, 0, 0))


def snap_to_master(img):
    """Replace any non-master opaque pixel and normalize alpha to 0/255."""
    out = img.copy().convert("RGBA")
    px = out.load()
    for y in range(out.height):
        for x in range(out.width):
            r, g, b, a = px[x, y]
            if a == 0:
                px[x, y] = (0, 0, 0, 0)
                continue
            rgb = (r, g, b)
            if rgb not in MASTER:
                rgb = min(MASTER, key=lambda candidate: sum((candidate[i] - rgb[i]) ** 2 for i in range(3)))
            px[x, y] = (*rgb, 255)
    return out


def save_asset(img, path):
    snap_to_master(img).save(path)


def rect(d, box, color):
    d.rectangle(box, fill=rgba(color))


def poly(d, points, color):
    d.polygon(points, fill=rgba(color))


def line(d, points, color, width=1):
    d.line(points, fill=rgba(color), width=width, joint="curve")


def cell(img, x, y, size=16):
    return ImageDraw.Draw(img), x * size, y * size


def draw_heart(d, ox, oy, state):
    rect(d, (ox + 4, oy + 1, ox + 5, oy + 2), "red_dark")
    rect(d, (ox + 10, oy + 1, ox + 11, oy + 2), "red_dark")
    rect(d, (ox + 3, oy + 2, ox + 12, oy + 5), "red_dark")
    rect(d, (ox + 4, oy + 6, ox + 11, oy + 8), "red_dark")
    rect(d, (ox + 5, oy + 9, ox + 10, oy + 10), "red_dark")
    rect(d, (ox + 4, oy + 3, ox + 7, oy + 5), "slate" if state == "empty" else "red")
    rect(d, (ox + 8, oy + 3, ox + 11, oy + 5), "slate" if state == "empty" else "red")
    rect(d, (ox + 5, oy + 6, ox + 10, oy + 8), "slate" if state == "empty" else "red")
    rect(d, (ox + 6, oy + 9, ox + 9, oy + 9), "slate" if state == "empty" else "red")
    if state == "half":
        rect(d, (ox + 8, oy + 3, ox + 11, oy + 5), "slate")
        rect(d, (ox + 8, oy + 6, ox + 10, oy + 8), "slate")
        rect(d, (ox + 8, oy + 9, ox + 9, oy + 9), "slate")


def draw_soul(d, ox, oy, scale=1):
    pts = [(ox + 8 * scale, oy + 1 * scale), (ox + 12 * scale, oy + 6 * scale),
           (ox + 8 * scale, oy + 14 * scale), (ox + 3 * scale, oy + 8 * scale)]
    poly(d, pts, "navy")
    poly(d, [(ox + 8 * scale, oy + 3 * scale), (ox + 10 * scale, oy + 6 * scale),
             (ox + 8 * scale, oy + 10 * scale), (ox + 6 * scale, oy + 7 * scale)], "sky")
    rect(d, (ox + 7 * scale, oy + 4 * scale, ox + 8 * scale, oy + 6 * scale), "white")


def draw_slot(d, ox, oy, focus=False):
    border = "gold" if focus else "slate"
    rect(d, (ox + 1, oy + 1, ox + 14, oy + 14), "deep")
    rect(d, (ox + 2, oy + 2, ox + 13, oy + 2), border)
    rect(d, (ox + 2, oy + 13, ox + 13, oy + 13), border)
    rect(d, (ox + 2, oy + 2, ox + 2, oy + 13), border)
    rect(d, (ox + 13, oy + 2, ox + 13, oy + 13), border)


def draw_badge(d, ox, oy, n=2):
    rect(d, (ox + 4, oy + 4, ox + 11, oy + 11), "gold_dark")
    rect(d, (ox + 5, oy + 3, ox + 10, oy + 12), "gold")
    rect(d, (ox + 6, oy + 5, ox + 9, oy + 10), "deep")
    rect(d, (ox + 7, oy + 6, ox + 8, oy + 9), "white")


def draw_pause(d, ox, oy):
    rect(d, (ox + 2, oy + 2, ox + 13, oy + 13), "deep")
    rect(d, (ox + 4, oy + 4, ox + 5, oy + 11), "gold")
    rect(d, (ox + 10, oy + 4, ox + 11, oy + 11), "gold")
    rect(d, (ox + 2, oy + 2, ox + 13, oy + 2), "slate")
    rect(d, (ox + 2, oy + 13, ox + 13, oy + 13), "slate")


def draw_ring(d, ox, oy, frame=0):
    points = [(ox + 7, oy + 1), (ox + 10, oy + 2), (ox + 13, oy + 5), (ox + 14, oy + 8),
              (ox + 13, oy + 11), (ox + 10, oy + 14), (ox + 7, oy + 15), (ox + 4, oy + 14),
              (ox + 1, oy + 11), (ox, oy + 8), (ox + 1, oy + 5), (ox + 4, oy + 2)]
    for i in range(len(points)):
        if (i + frame) % 3:
            line(d, [points[i], points[(i + 1) % len(points)]], "gold", 1)
    rect(d, (ox + 6, oy + 6, ox + 9, oy + 9), "gold_dark")


def draw_hp(d, ox, oy, width=14, ratio=0.7, boss=False):
    rect(d, (ox, oy, ox + width, oy + (4 if boss else 3)), "deep")
    rect(d, (ox, oy, ox + width, oy), "gold_dark" if boss else "slate")
    rect(d, (ox, oy + (4 if boss else 3), ox + width, oy + (4 if boss else 3)), "ink")
    fill = max(1, int(round(width * ratio)))
    rect(d, (ox + 1, oy + 1, ox + fill, oy + (3 if boss else 2)), "red" if boss else "green_light")
    rect(d, (ox + 1, oy + 1, ox + fill, oy + 1), "gold_light" if boss else "olive")


def draw_lock(d, ox, oy):
    rect(d, (ox + 4, oy + 6, ox + 11, oy + 13), "slate")
    line(d, [(ox + 5, oy + 6), (ox + 5, oy + 3), (ox + 6, oy + 2), (ox + 9, oy + 2), (ox + 10, oy + 3), (ox + 10, oy + 6)], "gray_light")
    rect(d, (ox + 7, oy + 8, ox + 8, oy + 10), "deep")


def draw_ability(d, ox, oy, kind):
    # All icons are 16x16 and intentionally text-free.
    if kind == "attack":
        line(d, [(ox + 3, oy + 12), (ox + 12, oy + 3)], "leather", 2)
        line(d, [(ox + 5, oy + 13), (ox + 13, oy + 5)], "brown_light", 1)
        line(d, [(ox + 6, oy + 10), (ox + 10, oy + 14)], "blue", 2)
        poly(d, [(ox + 11, oy + 2), (ox + 14, oy + 1), (ox + 13, oy + 4)], "gold")
        rect(d, (ox + 2, oy + 4, ox + 4, oy + 5), "gold_dark")
    elif kind == "speed":
        rect(d, (ox + 3, oy + 3, ox + 12, oy + 12), "slate")
        rect(d, (ox + 4, oy + 4, ox + 11, oy + 11), "deep")
        for p in [(ox + 7, oy + 3), (ox + 11, oy + 7), (ox + 7, oy + 11), (ox + 3, oy + 7)]: rect(d, (p[0], p[1], p[0] + 1, p[1] + 1), "gold_dark")
        line(d, [(ox + 7, oy + 7), (ox + 10, oy + 5)], "gold", 1)
        rect(d, (ox + 6, oy + 6, ox + 8, oy + 8), "gold")
    elif kind == "pierce":
        line(d, [(ox + 2, oy + 8), (ox + 13, oy + 8)], "brown_light", 2)
        poly(d, [(ox + 14, oy + 8), (ox + 10, oy + 5), (ox + 10, oy + 11)], "gold")
        rect(d, (ox + 4, oy + 4, ox + 5, oy + 12), "red_dark")
        rect(d, (ox + 8, oy + 4, ox + 9, oy + 12), "red_dark")
    elif kind == "reflect":
        line(d, [(ox + 3, oy + 12), (ox + 3, oy + 5), (ox + 11, oy + 5), (ox + 11, oy + 3)], "gold", 2)
        poly(d, [(ox + 11, oy + 1), (ox + 14, oy + 4), (ox + 10, oy + 5)], "gold_light")
        rect(d, (ox + 1, oy + 11, ox + 4, oy + 13), "blue")
    elif kind == "heal":
        draw_heart(d, ox, oy + 1, "full")
        rect(d, (ox + 7, oy + 4, ox + 8, oy + 10), "white")
        rect(d, (ox + 4, oy + 7, ox + 11, oy + 8), "white")
    elif kind == "move":
        poly(d, [(ox + 3, oy + 8), (ox + 6, oy + 4), (ox + 12, oy + 5), (ox + 13, oy + 9),
                 (ox + 10, oy + 11), (ox + 6, oy + 11)], "leather")
        rect(d, (ox + 5, oy + 7, ox + 11, oy + 8), "brown_light")
        rect(d, (ox + 3, oy + 10, ox + 11, oy + 12), "blue")
        rect(d, (ox + 9, oy + 5, ox + 12, oy + 6), "gold_dark")


def draw_shop(d, ox, oy, kind):
    if kind == "recovery":
        rect(d, (ox + 4, oy + 3, ox + 11, oy + 12), "red_dark")
        rect(d, (ox + 5, oy + 5, ox + 10, oy + 11), "red")
        rect(d, (ox + 6, oy + 1, ox + 9, oy + 4), "gold_dark")
        rect(d, (ox + 7, oy + 6, ox + 8, oy + 10), "white")
        rect(d, (ox + 5, oy + 7, ox + 10, oy + 8), "white")
    elif kind == "amulet":
        poly(d, [(ox + 8, oy + 2), (ox + 12, oy + 4), (ox + 12, oy + 10), (ox + 8, oy + 13),
                 (ox + 4, oy + 10), (ox + 4, oy + 4)], "gold_dark")
        poly(d, [(ox + 8, oy + 4), (ox + 10, oy + 5), (ox + 10, oy + 9), (ox + 8, oy + 11),
                 (ox + 6, oy + 9), (ox + 6, oy + 5)], "navy")
        rect(d, (ox + 7, oy + 6, ox + 8, oy + 8), "sky")
    else:
        rect(d, (ox + 3, oy + 5, ox + 12, oy + 11), "leather")
        rect(d, (ox + 4, oy + 4, ox + 11, oy + 5), "brown_light")
        line(d, [(ox + 3, oy + 4), (ox + 12, oy + 12)], "gold", 2)
        rect(d, (ox + 6, oy + 6, ox + 8, oy + 7), "gold_light")


def draw_map_type(d, ox, oy, kind):
    if kind == "battle":
        line(d, [(ox + 3, oy + 12), (ox + 12, oy + 3)], "red", 2)
        line(d, [(ox + 3, oy + 4), (ox + 12, oy + 13)], "gold", 2)
        rect(d, (ox + 2, oy + 11, ox + 4, oy + 13), "red_dark")
        rect(d, (ox + 11, oy + 2, ox + 13, oy + 4), "gold_dark")
    elif kind == "upgrade":
        poly(d, [(ox + 8, oy + 1), (ox + 10, oy + 6), (ox + 15, oy + 7), (ox + 11, oy + 10),
                 (ox + 12, oy + 15), (ox + 8, oy + 12), (ox + 4, oy + 15), (ox + 5, oy + 10),
                 (ox + 1, oy + 7), (ox + 6, oy + 6)], "gold")
        rect(d, (ox + 7, oy + 5, ox + 8, oy + 10), "white")
        rect(d, (ox + 5, oy + 7, ox + 10, oy + 8), "white")
    elif kind == "shop":
        rect(d, (ox + 3, oy + 5, ox + 13, oy + 11), "gold_dark")
        rect(d, (ox + 5, oy + 3, ox + 11, oy + 5), "gold")
        rect(d, (ox + 5, oy + 7, ox + 6, oy + 9), "navy")
        rect(d, (ox + 10, oy + 7, ox + 11, oy + 9), "navy")
        rect(d, (ox + 5, oy + 11, ox + 6, oy + 13), "deep")
        rect(d, (ox + 10, oy + 11, ox + 11, oy + 13), "deep")
    elif kind == "reward":
        rect(d, (ox + 2, oy + 6, ox + 13, oy + 12), "sand")
        rect(d, (ox + 2, oy + 5, ox + 13, oy + 7), "gold")
        rect(d, (ox + 7, oy + 6, ox + 8, oy + 11), "gold_light")
        poly(d, [(ox + 4, oy + 5), (ox + 8, oy + 2), (ox + 12, oy + 5)], "sand_light")
    elif kind == "heal":
        rect(d, (ox + 3, oy + 3, ox + 12, oy + 12), "green_deep")
        rect(d, (ox + 7, oy + 4, ox + 8, oy + 11), "white")
        rect(d, (ox + 4, oy + 7, ox + 11, oy + 8), "white")
        rect(d, (ox + 2, oy + 12, ox + 13, oy + 13), "green_light")
    elif kind == "boss":
        rect(d, (ox + 4, oy + 5, ox + 11, oy + 12), "red_dark")
        poly(d, [(ox + 3, oy + 5), (ox + 4, oy + 1), (ox + 7, oy + 4), (ox + 8, oy + 1),
                 (ox + 10, oy + 4), (ox + 13, oy + 1), (ox + 13, oy + 7)], "red")
        rect(d, (ox + 6, oy + 8, ox + 7, oy + 9), "gold")
        rect(d, (ox + 9, oy + 8, ox + 10, oy + 9), "gold")
    elif kind == "complete":
        rect(d, (ox + 4, oy + 2, ox + 5, oy + 13), "gold_dark")
        poly(d, [(ox + 5, oy + 3), (ox + 13, oy + 4), (ox + 9, oy + 7), (ox + 13, oy + 9), (ox + 5, oy + 10)], "gold")
        line(d, [(ox + 7, oy + 7), (ox + 8, oy + 9), (ox + 11, oy + 5)], "green_deep", 1)


def draw_map_state(d, ox, oy, state):
    if state == "current":
        poly(d, [(ox + 8, oy + 1), (ox + 14, oy + 8), (ox + 8, oy + 15), (ox + 1, oy + 8)], "gold")
        rect(d, (ox + 7, oy + 6, ox + 9, oy + 9), "white")
    elif state == "visited":
        rect(d, (ox + 3, oy + 3, ox + 12, oy + 12), "slate_light")
        rect(d, (ox + 5, oy + 5, ox + 10, oy + 10), "green_deep")
    else:
        line(d, [(ox + 3, oy + 3), (ox + 12, oy + 3), (ox + 12, oy + 12), (ox + 3, oy + 12), (ox + 3, oy + 3)], "gray_light")
        rect(d, (ox + 6, oy + 6, ox + 9, oy + 9), "deep")


def build_ui_atlas():
    img = transparent((256, 128)); d = ImageDraw.Draw(img)
    for i, state in enumerate(("full", "half", "empty")): draw_heart(d, i * 16, 0, state)
    draw_soul(d, 48, 0); draw_slot(d, 64, 0); draw_badge(d, 80, 0); draw_pause(d, 96, 0)
    draw_ring(d, 112, 0); draw_hp(d, 128, 2); draw_hp(d, 144, 1, 14, 0.6, True)
    rect(d, (160, 2, 175, 13), "slate"); rect(d, (162, 4, 173, 11), "deep")
    rect(d, (176, 4, 187, 13), "deep"); draw_lock(d, 176, 0)
    poly(d, [(194, 8), (202, 3), (202, 13)], "gold")
    for x in (208, 224, 240):
        rect(d, (x + 2, 3, x + 13, 12), "deep")
        line(d, [(x + 3, 8), (x + 12, 8)], "gold_dark")
    return img


def build_icon_atlas():
    img = transparent((256, 96)); d = ImageDraw.Draw(img)
    abilities = ["attack", "speed", "pierce", "reflect", "heal", "move"]
    for i, kind in enumerate(abilities): draw_ability(d, i * 16, 0, kind)
    for i, kind in enumerate(("recovery", "amulet", "whetstone")): draw_shop(d, (6 + i) * 16, 0, kind)
    types = ["battle", "upgrade", "shop", "reward", "heal", "boss", "complete"]
    for i, kind in enumerate(types): draw_map_type(d, i * 16, 16, kind)
    for i, state in enumerate(("current", "visited", "discovered")): draw_map_state(d, i * 16, 32, state)
    # Directional door markers and an unexplored marker occupy the remaining cells.
    for i, (dx, dy) in enumerate(((0, -1), (1, 0), (0, 1), (-1, 0))):
        ox, oy = (3 + i) * 16, 32
        rect(d, (ox + 6, oy + 6, ox + 9, oy + 9), "slate")
        if dx == 1: poly(d, [(ox + 13, oy + 8), (ox + 8, oy + 4), (ox + 8, oy + 12)], "gold")
        elif dx == -1: poly(d, [(ox + 2, oy + 8), (ox + 7, oy + 4), (ox + 7, oy + 12)], "gold")
        elif dy == -1: poly(d, [(ox + 8, oy + 2), (ox + 4, oy + 7), (ox + 12, oy + 7)], "gold")
        else: poly(d, [(ox + 8, oy + 14), (ox + 4, oy + 9), (ox + 12, oy + 9)], "gold")
    draw_map_state(d, 112, 32, "discovered")
    for i, kind in enumerate(("battle", "upgrade", "shop", "reward", "heal", "boss", "complete")):
        draw_map_type(d, i * 16, 48, kind)
    return img


def build_ability_icons():
    img = transparent((96, 16)); d = ImageDraw.Draw(img)
    for i, kind in enumerate(("attack", "speed", "pierce", "reflect", "heal", "move")): draw_ability(d, i * 16, 0, kind)
    return img


def build_shop_icons():
    img = transparent((48, 16)); d = ImageDraw.Draw(img)
    for i, kind in enumerate(("recovery", "amulet", "whetstone")): draw_shop(d, i * 16, 0, kind)
    return img


def build_minimap_icons():
    img = transparent((160, 32)); d = ImageDraw.Draw(img)
    for i, state in enumerate(("current", "visited", "discovered")): draw_map_state(d, i * 16, 0, state)
    for i, kind in enumerate(("battle", "upgrade", "shop", "reward", "heal", "boss", "complete")): draw_map_type(d, (3 + i) * 16, 0, kind)
    for i, (dx, dy) in enumerate(((0, -1), (1, 0), (0, 1), (-1, 0))):
        ox, oy = (10 + i) * 16, 0
        rect(d, (ox + 6, oy + 6, ox + 9, oy + 9), "slate")
        if dx == 1: poly(d, [(ox + 13, oy + 8), (ox + 8, oy + 4), (ox + 8, oy + 12)], "gold")
        elif dx == -1: poly(d, [(ox + 2, oy + 8), (ox + 7, oy + 4), (ox + 7, oy + 12)], "gold")
        elif dy == -1: poly(d, [(ox + 8, oy + 2), (ox + 4, oy + 7), (ox + 12, oy + 7)], "gold")
        else: poly(d, [(ox + 8, oy + 14), (ox + 4, oy + 9), (ox + 12, oy + 9)], "gold")
    return img


def build_panels():
    img = transparent((192, 64)); d = ImageDraw.Draw(img)
    for i, state in enumerate(("basic", "focus", "disabled", "confirmed")):
        ox = i * 48
        fill, border = {"basic": ("deep", "slate"), "focus": ("green_deep", "gold"),
                        "disabled": ("shadow", "slate_light"), "confirmed": ("navy", "gold_light")} [state]
        rect(d, (ox + 1, 1, ox + 46, 62), fill)
        rect(d, (ox + 1, 1, ox + 46, 1), border); rect(d, (ox + 1, 62, ox + 46, 62), border)
        rect(d, (ox + 1, 1, ox + 1, 62), border); rect(d, (ox + 46, 1, ox + 46, 62), border)
        draw_slot(d, ox + 16, 7, state == "focus")
        if state == "disabled":
            line(d, [(ox + 8, 8), (ox + 39, 55)], "slate_light", 2)
            draw_lock(d, ox + 16, 28)
        elif state == "focus":
            poly(d, [(ox + 4, 30), (ox + 11, 26), (ox + 11, 34)], "gold")
            poly(d, [(ox + 44, 30), (ox + 37, 26), (ox + 37, 34)], "gold")
        elif state == "confirmed":
            rect(d, (ox + 35, 7, ox + 42, 14), "gold")
            line(d, [(ox + 36, 10), (ox + 38, 12), (ox + 41, 8)], "green_deep", 1)
        # The middle and bottom rows remain deliberately empty for Unity Korean text.
        rect(d, (ox + 8, 39, ox + 39, 39), "slate")
    return img


def effect_cell(size=32):
    return transparent((size, size))


def effect_wall(img, frame):
    d = ImageDraw.Draw(img); cx, cy = 16, 16
    rect(d, (13, 2, 18, 29), "slate")
    x = 7 + frame * 2
    line(d, [(x, cy), (13, cy)], "brown_light", 2)
    poly(d, [(13, cy), (8, cy - 4), (8, cy + 4)], "gold")
    for dx, dy in ((-3, -3), (-2, 3), (3, -4), (4, 3)):
        if (frame + dx + dy) % 2 == 0: rect(d, (cx + dx, cy + dy, cx + dx + 1, cy + dy + 1), "gold_light")


def effect_hit(img, frame, enemy=False):
    d = ImageDraw.Draw(img); cx, cy = 16, 16
    color = "red" if enemy else "blue"
    if frame == 0: rect(d, (cx - 4, cy - 4, cx + 4, cy + 4), color)
    elif frame == 1:
        line(d, [(cx - 13, cy), (cx + 13, cy)], color, 2); line(d, [(cx, cy - 13), (cx, cy + 13)], "white", 1)
        line(d, [(cx - 9, cy - 9), (cx + 9, cy + 9)], color, 1); line(d, [(cx - 9, cy + 9), (cx + 9, cy - 9)], color, 1)
    else:
        for x, y in ((4, 4), (26, 5), (5, 26), (26, 26)): rect(d, (x, y, x + 2, y + 2), color)
        rect(d, (cx - 3, cy - 3, cx + 3, cy + 3), "white")


def effect_soul(img, frame, release=False):
    d = ImageDraw.Draw(img); cx, cy = 16, 16
    if not release:
        draw_soul(d, 10, 12 - frame * 2, 1)
        for x, y in ((5 + frame, 25), (23 - frame, 8), (7, 9 + frame)):
            rect(d, (x, y, x + 1, y + 1), "sky")
    else:
        r = 3 + frame * 2
        line(d, [(cx - r, cy), (cx - r + 2, cy - 3), (cx, cy - r), (cx + r - 2, cy - 3), (cx + r, cy),
                 (cx + r - 2, cy + 3), (cx, cy + r), (cx - r + 2, cy + 3), (cx - r, cy)], "sky", 1)
        if frame < 4: draw_soul(d, 10, 10, 1)
        for x, y in ((4 + frame, 5), (26 - frame, 7), (5, 24 - frame), (25, 25 - frame)):
            rect(d, (x, y, x + 2, y + 2), "gold_light")


def effect_door(img, frame):
    d = ImageDraw.Draw(img)
    gap = frame * 3
    rect(d, (4, 3, 11 - gap // 2, 29), "brown")
    rect(d, (20 + gap // 2, 3, 27, 29), "brown")
    rect(d, (12 - gap // 2, 4, 19 + gap // 2, 27), "gold_dark")
    if frame >= 2: rect(d, (14 - gap // 2, 8, 17 + gap // 2, 23), "white")
    for y in range(6, 27, 5): rect(d, (2, y, 4, y + 1), "gold")


def effect_light(img, frame):
    d = ImageDraw.Draw(img); cx, cy = 16, 16
    r = 3 + frame * 2
    line(d, [(cx, cy - r), (cx + 2, cy - 2), (cx + r, cy), (cx + 2, cy + 2), (cx, cy + r),
             (cx - 2, cy + 2), (cx - r, cy), (cx - 2, cy - 2), (cx, cy - r)], "gold", 1)
    if frame in (2, 3):
        poly(d, [(cx, cy - 7), (cx + 4, cy - 2), (cx + 7, cy), (cx + 3, cy + 2), (cx, cy + 7),
                 (cx - 3, cy + 2), (cx - 7, cy), (cx - 3, cy - 2)], "gold_light")
    rect(d, (14, 14, 17, 17), "white")
    if frame > 3: rect(d, (15, 10, 16, 11), "blue"); rect(d, (15, 20, 16, 21), "blue")


def effect_warning(img, frame, spawn=False):
    d = ImageDraw.Draw(img); color = "gold" if spawn else "red"
    inner = "gold_light" if spawn else "skin"
    if spawn:
        r = 4 + (frame % 3) * 3
        line(d, [(16 - r, 16), (16, 16 - r), (16 + r, 16), (16, 16 + r), (16 - r, 16)], color, 1)
        for x, y in ((4, 16), (28, 16), (16, 4), (16, 28)):
            rect(d, (x - frame % 2, y - frame % 2, x + 1, y + 1), inner)
    else:
        y = 16 + (frame % 2) * 2
        line(d, [(2, y), (30, y)], color, 2)
        for x in range(4, 30, 6):
            poly(d, [(x, y - 4), (x + 3, y), (x, y + 4)], inner)
        if frame % 2 == 0: rect(d, (1, 5, 3, 26), "red_dark")


def build_effects():
    img = transparent((256, 256))
    for frame in range(4):
        c = effect_cell(); effect_wall(c, frame); img.paste(c, (frame * 32, 0), c)
    for frame in range(3):
        c = effect_cell(); effect_hit(c, frame, False); img.paste(c, (frame * 32, 32), c)
        c = effect_cell(); effect_hit(c, frame, True); img.paste(c, ((4 + frame) * 32, 32), c)
    for frame in range(4):
        c = effect_cell(); effect_soul(c, frame, False); img.paste(c, (frame * 32, 64), c)
    for frame in range(6):
        c = effect_cell(); effect_soul(c, frame, True)
        img.paste(c, ((4 + frame) * 32, 64) if frame < 4 else ((frame - 4) * 32, 96), c)
    for frame in range(4):
        c = effect_cell(); effect_door(c, frame); img.paste(c, ((2 + frame) * 32, 96), c)
    for frame in range(6):
        c = effect_cell(); effect_light(c, frame)
        img.paste(c, ((6 + frame) * 32, 96) if frame < 2 else ((frame - 2) * 32, 128), c)
    for frame in range(6):
        c = effect_cell(); effect_warning(c, frame, False)
        img.paste(c, ((4 + frame) * 32, 128) if frame < 4 else ((frame - 4) * 32, 160), c)
        c = effect_cell(); effect_warning(c, frame, True); img.paste(c, ((2 + frame) * 32, 160), c)
    return img


def paste_sprite(base, path, xy, frame=0, cell_size=32):
    src = Image.open(path).convert("RGBA")
    crop = src.crop((frame * cell_size, 0, (frame + 1) * cell_size, cell_size))
    base.alpha_composite(crop, xy)


def draw_hud(base, include_boss=False):
    d = ImageDraw.Draw(base)
    # Pixel-safe opaque backing strips keep the HUD readable without blended colors.
    rect(d, (8, 8, 172, 42), "green_deep")
    rect(d, (8, 8, 172, 8), "green_mid")
    for i, state in enumerate(("full", "full", "full", "half", "empty")): draw_heart(d, 16 + i * 22, 14, state)
    draw_soul(d, 17, 31)
    for i in range(3):
        draw_slot(d, 16 + i * 32, 320, i == 0)
        draw_ability(d, 24 + i * 32, 320, ("attack", "pierce", "move")[i])
        draw_badge(d, 30 + i * 32, 326, i + 1)
    draw_pause(d, 604, 320)
    rect(d, (466, 8, 628, 112), "green_deep")
    rect(d, (466, 8, 628, 8), "green_mid")
    # room graph: current diamond, visited squares, discovered outline; icon shapes remain distinct.
    for x, y, state in ((500, 48, "visited"), (532, 48, "current"), (564, 48, "discovered"),
                         (532, 80, "visited"), (500, 80, "discovered"), (564, 80, "discovered")):
        draw_map_state(d, x, y, state)
    for a, b in [((508, 56), (524, 56)), ((540, 56), (556, 56)), ((532, 64), (532, 72)), ((508, 88), (524, 88))]: line(d, [a, b], "slate", 2)
    draw_map_type(d, 594, 16, "battle")
    draw_ring(d, 294, 218, 1)
    draw_hp(d, 274, 198, 54, 0.65, False)
    if include_boss:
        rect(d, (178, 12, 454, 28), "deep")
        draw_hp(d, 188, 16, 254, 0.55, True)


def preview_base():
    src = Image.open(ROOT.parent / "Environment" / "Forest" / "forest_preview.png").convert("RGBA")
    # The source preview contains legacy render-only colors. Snap every source
    # pixel to the exact master before UI/character compositing.
    return snap_to_master(src)


def build_previews():
    leon = ROOT.parent / "Player" / "Leon" / "leon_walk.png"
    base = preview_base(); draw_hud(base, True); paste_sprite(base, leon, (285, 238), 0); save_asset(base, ROOT / "hud_preview.png")
    mini = preview_base(); d = ImageDraw.Draw(mini); rect(d, (450, 14, 628, 150), "green_deep")
    for x, y, state in ((490, 45, "visited"), (522, 45, "current"), (554, 45, "discovered"), (522, 77, "visited"), (490, 77, "discovered"), (554, 77, "discovered")):
        draw_map_state(d, x, y, state)
    for a, b in [((498, 53), (514, 53)), ((530, 53), (546, 53)), ((522, 61), (522, 69)), ((498, 85), (514, 85))]: line(d, [a, b], "slate", 2)
    for i, kind in enumerate(("battle", "upgrade", "shop", "reward", "heal", "boss", "complete")): draw_map_type(d, 470 + (i % 4) * 32, 108 + (i // 4) * 24, kind)
    draw_hud(mini, False); save_asset(mini, ROOT / "minimap_preview.png")
    ability = preview_base(); d = ImageDraw.Draw(ability); rect(d, (0, 0, 639, 359), "green_deep")
    rect(d, (124, 54, 516, 306), "deep"); rect(d, (124, 54, 516, 54), "gold_dark")
    for i, (kind, state) in enumerate(zip(("attack", "speed", "pierce"), ("basic", "focus", "disabled"))):
        ox = 146 + i * 118; fill = "green_deep" if state == "focus" else ("shadow" if state == "disabled" else "deep"); border = "gold" if state == "focus" else "slate"
        rect(d, (ox, 116, ox + 92, 264), fill); rect(d, (ox, 116, ox + 92, 116), border); rect(d, (ox, 116, ox, 264), border); rect(d, (ox + 92, 116, ox + 92, 264), border); rect(d, (ox, 264, ox + 92, 264), border)
        draw_ability(d, ox + 38, 134, kind); draw_slot(d, ox + 40, 174, state == "focus")
        rect(d, (ox + 16, 222, ox + 76, 222), "slate"); rect(d, (ox + 24, 238, ox + 68, 238), "slate")
        if state == "focus": poly(d, [(ox + 4, 187), (ox + 14, 181), (ox + 14, 193)], "gold")
        if state == "disabled": draw_lock(d, ox + 40, 178)
    paste_sprite(ability, leon, (304, 280), 0); save_asset(ability, ROOT / "ability_preview.png")
    shop = preview_base(); d = ImageDraw.Draw(shop); rect(d, (0, 0, 639, 359), "green_deep"); rect(d, (105, 50, 535, 310), "deep"); rect(d, (105, 50, 535, 50), "gold_dark")
    for i, (kind, state) in enumerate(zip(("recovery", "amulet", "whetstone"), ("basic", "disabled", "confirmed"))):
        ox = 136 + i * 124; fill = "shadow" if state == "disabled" else ("navy" if state == "confirmed" else "deep"); border = "gold_light" if state == "confirmed" else "slate"
        rect(d, (ox, 116, ox + 96, 272), fill); rect(d, (ox, 116, ox + 96, 116), border); rect(d, (ox, 116, ox, 272), border); rect(d, (ox + 96, 116, ox + 96, 272), border); rect(d, (ox, 272, ox + 96, 272), border)
        draw_shop(d, ox + 40, 134, kind); rect(d, (ox + 18, 216, ox + 78, 216), "slate"); rect(d, (ox + 28, 232, ox + 68, 232), "slate")
        if state == "disabled": draw_lock(d, ox + 40, 176)
        if state == "confirmed":
            rect(d, (ox + 72, 128, ox + 88, 144), "gold"); line(d, [(ox + 75, 136), (ox + 79, 140), (ox + 86, 132)], "green_deep", 2)
    paste_sprite(shop, leon, (304, 284), 0); save_asset(shop, ROOT / "shop_preview.png")


def save_all():
    save_asset(build_ui_atlas(), ROOT / "ui_atlas.png")
    save_asset(build_icon_atlas(), ROOT / "icon_atlas.png")
    save_asset(build_ability_icons(), ROOT / "ability_icons.png")
    save_asset(build_shop_icons(), ROOT / "shop_icons.png")
    save_asset(build_minimap_icons(), ROOT / "minimap_icons.png")
    save_asset(build_panels(), ROOT / "panel_states.png")
    save_asset(build_effects(), EFFECTS / "effects_sheet.png")
    build_previews()


if __name__ == "__main__":
    save_all()
    print("generated UI and effects assets")
