"""1920x1080 screen mockups built from the icon set and shared UI components."""
import math
import os
from icons import ICONS
from lowpoly import P, facet, flat, ngon, blob, stick, shade, ground_shadow

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
W, H = 1920, 1080
FONT = "Nunito, Arial Rounded MT Bold, sans-serif"

# UI tokens (repeat the world palette, per GDD §2.2)
UI = {
    "panel": "#FFF4DC", "panel_edge": "#C98B55", "panel_edge_dk": "#8A5A32", "line": "#EAD3A8",
    "btn": "#6CC24A", "btn_dk": "#3E8A27", "btn2": "#F2A23A", "btn2_dk": "#B86E1E",
    "btn3": "#FFF4DC", "btn3_dk": "#D9B98A", "danger": "#E2533F", "danger_dk": "#A8382A",
    "text": "#3B2A1A", "muted": "#8A7560", "overlay": "#1E2A14",
}


def ico(key, x, y, size=64):
    s = size / 64
    return f'<g id="icon_{key}" transform="translate({x},{y}) scale({s:.4f})">{ICONS[key][2]()}</g>'


def text(x, y, s, size=28, weight=800, color=UI["text"], anchor="start", extra=""):
    return (f'<text x="{x}" y="{y}" font-family="{FONT}" font-weight="{weight}" font-size="{size}" '
            f'fill="{color}" text-anchor="{anchor}"{extra}>{s}</text>')


def panel(x, y, w, h, r=28, name="panel"):
    return (f'<g id="{name}"><rect x="{x}" y="{y + 10}" width="{w}" height="{h}" rx="{r}" fill="#000" opacity="0.18"/>'
            f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{r}" fill="{UI["panel_edge_dk"]}"/>'
            f'<rect x="{x}" y="{y}" width="{w}" height="{h - 8}" rx="{r}" fill="{UI["panel_edge"]}"/>'
            f'<rect x="{x + 10}" y="{y + 10}" width="{w - 20}" height="{h - 28}" rx="{r - 10}" fill="{UI["panel"]}"/></g>')


def banner(cx, y, label, w=520):
    """Wooden ribbon title on top of a panel."""
    x = cx - w / 2
    return (f'<g id="title_{label}">' + facet([(x - 40, y + 10), (x + 10, y + 10), (x + 10, y + 70), (x - 40, y + 70), (x - 20, y + 40)], UI["panel_edge_dk"]) +
            facet([(x + w + 40, y + 10), (x + w - 10, y + 10), (x + w - 10, y + 70), (x + w + 40, y + 70), (x + w + 20, y + 40)], UI["panel_edge_dk"]) +
            f'<rect x="{x}" y="{y}" width="{w}" height="72" rx="16" fill="{shade(P["wood"], -0.2)}"/>'
            f'<rect x="{x}" y="{y}" width="{w}" height="64" rx="16" fill="{P["wood"]}"/>' +
            text(cx, y + 46, label, 38, 900, P["cream"], "middle") + '</g>')


def button(x, y, w, h, label, kind="primary", icon_key=None, size=32, name=None, state="normal"):
    face, lip = {"primary": (UI["btn"], UI["btn_dk"]), "secondary": (UI["btn2"], UI["btn2_dk"]),
                 "ghost": (UI["btn3"], UI["btn3_dk"]), "danger": (UI["danger"], UI["danger_dk"])}[kind]
    if state == "hover":
        face = shade(face, 0.12)
    if state == "disabled":
        face, lip = "#D8D2C6", "#AFA898"
    dy = 6 if state == "pressed" else 0
    fg = UI["text"] if kind == "ghost" or state == "disabled" else "#FFFFFF"
    inner = ""
    tx = x + w / 2
    if icon_key:
        isz = h * 0.6
        tw = len(label) * size * 0.56
        ix = x + w / 2 - (isz + 14 + tw) / 2
        inner += ico(icon_key, ix, y + dy + (h - 8 - isz) / 2, isz)
        tx = ix + isz + 14 + tw / 2
    out = (f'<g id="{name or "btn_" + label}"><rect x="{x}" y="{y + 8}" width="{w}" height="{h}" rx="{h * 0.32}" fill="#000" opacity="0.15"/>'
           f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{h * 0.32}" fill="{lip}"/>'
           f'<rect x="{x}" y="{y + dy}" width="{w}" height="{h - 8}" rx="{h * 0.32}" fill="{face}"/>'
           f'<rect x="{x + 10}" y="{y + dy + 6}" width="{w - 20}" height="{(h - 8) * 0.32}" rx="{h * 0.14}" fill="#FFFFFF" opacity="0.18"/>')
    if label:
        out += text(tx, y + dy + (h - 8) / 2 + size * 0.36, label, size, 900, fg, "middle")
    return out + inner + '</g>'


def icon_button(x, y, key, size=84, kind="ghost", badge=None):
    out = button(x, y, size, size, "", kind, name=f"iconbtn_{key}")
    out += ico(key, x + size * 0.18, y + size * 0.12, size * 0.64)
    if badge:
        out += f'<circle cx="{x + size - 6}" cy="{y + 6}" r="15" fill="{UI["danger"]}" stroke="#fff" stroke-width="3"/>' + text(x + size - 6, y + 13, badge, 18, 900, "#fff", "middle")
    return out


def slider(x, y, w, value, label, icon_key):
    fill = w * value
    return (f'<g id="slider_{label}">' + ico(icon_key, x, y - 14, 52) + text(x + 70, y + 22, label, 28, 800) +
            f'<rect x="{x + 380}" y="{y + 4}" width="{w}" height="22" rx="11" fill="{UI["line"]}"/>'
            f'<rect x="{x + 380}" y="{y + 4}" width="{fill}" height="22" rx="11" fill="{UI["btn"]}"/>' +
            facet(ngon(x + 380 + fill, y + 15, 22, 8), P["gold"]) +
            text(x + 400 + w + 30, y + 26, f"{round(value * 100)}%", 26, 900, UI["muted"]) + '</g>')


def toggle(x, y, on, label):
    bg = UI["btn"] if on else "#CFC6B4"
    kx = x + 548 if on else x + 508
    return (f'<g id="toggle_{label}">' + text(x, y + 30, label, 28, 800) +
            f'<rect x="{x + 490}" y="{y}" width="96" height="44" rx="22" fill="{bg}"/>'
            f'<circle cx="{kx + 10}" cy="{y + 22}" r="17" fill="#fff"/>' + '</g>')


def dropdown(x, y, label, value):
    return (f'<g id="dropdown_{label}">' + text(x, y + 32, label, 28, 800) +
            f'<rect x="{x + 380}" y="{y}" width="380" height="50" rx="14" fill="#fff" stroke="{UI["line"]}" stroke-width="3"/>' +
            text(x + 400, y + 34, value, 24, 800) + ico("arrow_left", x + 720, y + 12, 26).replace('scale(0.4062)', 'scale(0.4062) rotate(-90 32 32)') + '</g>')


def tabs(x, y, items, active, w=230):
    out = '<g id="tabs">'
    for i, (k, lab) in enumerate(items):
        on = i == active
        out += (f'<rect x="{x + i * (w + 12)}" y="{y}" width="{w}" height="64" rx="18" fill="{"#FFFBEF" if on else UI["btn3_dk"]}"'
                f'{(" stroke=" + chr(34) + P["gold"] + chr(34) + " stroke-width=" + chr(34) + "5" + chr(34)) if on else ""}/>' +
                ico(k, x + i * (w + 12) + 16, y + 10, 42) + text(x + i * (w + 12) + 70, y + 42, lab, 26, 900, UI["text"] if on else UI["muted"]))
    return out + '</g>'


def slot(x, y, size, key=None, count=None, selected=False, hotkey=None, locked=False, name="slot"):
    edge = P["gold"] if selected else UI["line"]
    out = (f'<g id="{name}"><rect x="{x}" y="{y + 5}" width="{size}" height="{size}" rx="{size * 0.2}" fill="#000" opacity="0.12"/>'
           f'<rect x="{x}" y="{y}" width="{size}" height="{size}" rx="{size * 0.2}" fill="{"#FFFBEF" if selected else "#FFF8E8"}" stroke="{edge}" stroke-width="{6 if selected else 4}"/>')
    if locked:
        out += ico("lock", x + size * 0.25, y + size * 0.22, size * 0.5)
        out = out.replace('fill="#FFF8E8"', 'fill="#E8DECB"', 1)
    if key:
        out += ico(key, x + size * 0.14, y + size * 0.1, size * 0.72)
    if count:
        out += text(x + size - 10, y + size - 10, count, size * 0.24, 900, "#fff", "end", ' stroke="#3B2A1A" stroke-width="4" paint-order="stroke"')
    if hotkey:
        out += text(x + 12, y + 26, hotkey, 20, 900, UI["muted"])
    return out + '</g>'


# ───────── backgrounds ─────────

def tree(x, y, s=1.0, seed=1):
    return ground_shadow(x, y + 2, 26 * s) + stick(x, y, x, y - 40 * s, 9 * s, P["wood"]) + \
        blob(x, y - 62 * s, 34 * s, n=11, seed=seed, color=P["leaf"] if seed % 3 else shade(P["leaf"], -0.1))


def scenery(darken=0.0, ground_y=640):
    out = ['<defs><linearGradient id="sky" x1="0" y1="0" x2="0" y2="1">'
           '<stop offset="0" stop-color="#4FA8E8"/><stop offset="0.7" stop-color="#BFE4F7"/><stop offset="1" stop-color="#E8F5FB"/></linearGradient></defs>'
           f'<rect width="{W}" height="{H}" fill="url(#sky)"/>']
    for cx, cy, s in [(260, 160, 1.6), (900, 110, 1.2), (1500, 190, 1.8), (1780, 90, 1.0)]:
        out.append(blob(cx, cy, 34 * s, n=9, seed=2, color="#FFFFFF", sx=1.8, sy=0.7))
    # far hills
    hills = [(0, ground_y), (0, ground_y - 120), (260, ground_y - 200), (560, ground_y - 110), (900, ground_y - 230), (1240, ground_y - 120), (1560, ground_y - 210), (1920, ground_y - 90), (1920, ground_y)]
    for i in range(1, len(hills) - 2):
        out.append(facet([hills[i], hills[i + 1], (hills[i + 1][0], ground_y), (hills[i][0], ground_y)], "#7FC45A", seed=i))
    out.append(facet([(0, ground_y), (W, ground_y), (W, H), (0, H)], P["grass"], center=(W / 2, ground_y - 200), strength=0.12))
    for i, x in enumerate(range(-40, W + 80, 120)):
        out.append(tree(x + (i % 2) * 40, ground_y + 20 + (i % 3) * 10, 0.9, seed=i + 1))
    # pond, path, field
    out.append(facet(ngon(1460, 900, 240, 12, sy=0.32), P["water_dk"]) + facet(ngon(1460, 895, 222, 12, sy=0.3), P["water"]))
    out.append(facet([(820, H), (900, ground_y + 60), (960, ground_y + 60), (1000, H)], P["soil"], strength=0.1))
    for r in range(3):
        for c in range(5):
            x, y = 330 + c * 90 - r * 40, 820 + r * 60
            out.append(facet([(x, y), (x + 80, y), (x + 60, y + 50), (x - 20, y + 50)], P["soil"], seed=r + c))
            out.append(ico(["crop_carrot", "crop_corn", "crop_wheat", "crop_tomato", "crop_pumpkin"][(r + c) % 5], x + 6, y - 22, 52))
    out.append(ico("b_house", 1100, 560, 230) + ico("b_lantern", 1050, 700, 110) + ico("b_lantern", 1330, 700, 110))
    for i, x in enumerate([60, 260, 1700, 1860]):
        out.append(tree(x, 1060, 1.9, seed=i + 4))
    if darken:
        out.append(f'<rect width="{W}" height="{H}" fill="{UI["overlay"]}" opacity="{darken}"/>')
    return "".join(out)


def frame(name, body):
    return (f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}">'
            f'<g id="{name}">{body}</g></svg>')


# ───────── screens ─────────

def main_menu():
    b = scenery(0.15)
    # logo sign
    b += '<g id="logo">' + stick(860, 330, 860, 230, 16, P["wood_dk"]) + stick(1060, 330, 1060, 230, 16, P["wood_dk"]) + \
        facet([(600, 70), (1320, 70), (1340, 250), (580, 250)], P["wood"]) + facet([(620, 86), (1300, 86), (1316, 234), (604, 234)], P["wood_lt"]) + \
        text(960, 160, "MY LITTLE", 64, 900, P["cream"], "middle", ' stroke="#6E4423" stroke-width="10" paint-order="stroke"') + \
        text(960, 222, "FARM", 84, 900, P["yellow"], "middle", ' stroke="#6E4423" stroke-width="12" paint-order="stroke"') + \
        ico("crop_carrot", 520, 40, 120) + ico("crop_corn", 1290, 40, 120) + '</g>'
    bx = 960 - 230
    b += button(bx, 380, 460, 120, "СТАРТ", "primary", "play", 52, "btn_start")
    for i, (lab, ic, kind) in enumerate([("Продолжить", "continue", "ghost"), ("Загрузить", "load", "ghost"), ("Настройки", "settings", "ghost"), ("Выход", "exit", "ghost")]):
        b += button(bx + 30, 530 + i * 100, 400, 84, lab, kind, ic, 32)
    b += text(40, 1050, "v0.1 · MVP", 22, 800, "#fff") + text(1880, 1050, "Esinencu P. · команда My Little Farm", 22, 800, "#fff", "end")
    b += icon_button(1800, 40, "volume", 80) + icon_button(1700, 40, "language", 80)
    return frame("01_Главное_меню", b)


def settings_base(active):
    b = scenery(0.55) + panel(360, 120, 1200, 860, name="settings_panel") + banner(960, 70, "НАСТРОЙКИ")
    b += tabs(420, 180, [("volume", "Звук"), ("graphics", "Графика"), ("controls", "Управление"), ("language", "Язык")], active, 255)
    b += f'<rect x="400" y="244" width="1120" height="4" fill="{UI["line"]}"/>'
    b += button(1040, 860, 230, 84, "Сбросить", "ghost", None, 28) + button(1290, 860, 230, 84, "Применить", "primary", "confirm", 28)
    b += button(420, 860, 200, 84, "Назад", "ghost", "back", 28)
    b += icon_button(1490, 100, "close", 76, "danger")
    return b


def settings_sound():
    b = settings_base(0)
    for i, (lab, ic, v) in enumerate([("Общая громкость", "volume", 0.8), ("Музыка", "music", 0.6), ("Эффекты", "sfx", 0.75), ("Окружение", "weather_rain", 0.5)]):
        b += slider(440, 300 + i * 100, 560, v, lab, ic)
    b += toggle(440, 720, True, "Звук в свёрнутом окне") + toggle(440, 790, False, "Без звука")
    return frame("02_Настройки_Звук", b)


def settings_graphics():
    b = settings_base(1)
    rows = [("Разрешение", "1920 × 1080"), ("Режим окна", "Полный экран"), ("Качество", "Высокое"), ("Частота кадров", "60 FPS")]
    for i, (l, v) in enumerate(rows):
        b += dropdown(440, 290 + i * 84, l, v)
    b += slider(440, 650, 380, 0.7, "Яркость", "brightness")
    b += toggle(440, 730, True, "Тени") + toggle(440, 790, True, "Вертикальная синхронизация")
    return frame("03_Настройки_Графика", b)


def settings_controls():
    b = settings_base(2)
    binds = [("Движение", "W A S D"), ("Основное действие", "ЛКМ / E"), ("Отмена / закрыть", "Esc / ПКМ"), ("Быстрые слоты", "1 – 8"),
             ("Инвентарь", "I / Tab"), ("Режим строительства", "B"), ("Поворот камеры", "Q / E"), ("Приближение", "Колесо")]
    for i, (a, k) in enumerate(binds):
        x, y = 440 + (i // 4) * 540, 290 + (i % 4) * 130
        b += text(x, y + 10, a, 26, 800, UI["muted"])
        b += f'<rect x="{x}" y="{y + 26}" width="480" height="66" rx="16" fill="#fff" stroke="{UI["line"]}" stroke-width="3"/>' + text(x + 240, y + 70, k, 30, 900, UI["text"], "middle")
    return frame("04_Настройки_Управление", b)


def save_card(x, y, idx, data=None, selected=False):
    w, h = 1040, 170
    edge = P["gold"] if selected else UI["line"]
    out = (f'<g id="save_slot_{idx}"><rect x="{x}" y="{y + 6}" width="{w}" height="{h}" rx="24" fill="#000" opacity="0.1"/>'
           f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="24" fill="#FFFBEF" stroke="{edge}" stroke-width="{6 if selected else 4}"/>')
    if data is None:
        out += f'<rect x="{x + 20}" y="{y + 20}" width="{w - 40}" height="{h - 40}" rx="18" fill="none" stroke="{UI["btn3_dk"]}" stroke-width="4" stroke-dasharray="14 10"/>'
        out += ico("new_game", x + w / 2 - 190, y + 45, 80) + text(x + w / 2 - 90, y + 98, "Новая ферма", 38, 900, UI["muted"])
        return out + text(x + 40, y + 40, f"СЛОТ {idx}", 20, 900, UI["muted"]) + '</g>'
    name, day, coins, time, date, weather = data
    # thumbnail: tiny farm
    out += f'<clipPath id="thumb{idx}"><rect x="{x + 20}" y="{y + 20}" width="220" height="130" rx="16"/></clipPath>'
    out += (f'<g clip-path="url(#thumb{idx})"><rect x="{x + 20}" y="{y + 20}" width="220" height="130" fill="{P["sky"]}"/>' +
            facet([(x + 20, y + 90), (x + 240, y + 80), (x + 240, y + 150), (x + 20, y + 150)], P["grass"]) +
            ico("b_house", x + 90, y + 40, 80) + ico("b_tree", x + 20, y + 50, 70) + ico("b_tree", x + 180, y + 60, 60) + '</g>')
    out += text(x + 270, y + 36, f"СЛОТ {idx}", 20, 900, UI["muted"]) + text(x + 270, y + 78, name, 38, 900)
    out += ico("calendar", x + 270, y + 100, 40) + text(x + 318, y + 132, f"День {day}", 26, 800)
    out += ico("coin", x + 460, y + 100, 40) + text(x + 508, y + 132, coins, 26, 800)
    out += ico("clock", x + 640, y + 100, 40) + text(x + 688, y + 132, time, 26, 800)
    out += ico(weather, x + 370, y + 8, 34) + text(x + 410, y + 36, date, 20, 800, UI["muted"])
    out += icon_button(x + w - 210, y + 44, "delete", 80, "ghost") + icon_button(x + w - 110, y + 44, "play", 80, "primary")
    return out + '</g>'


def save_slots():
    b = scenery(0.55) + panel(400, 110, 1120, 900, name="saves_panel") + banner(960, 60, "СОХРАНЕНИЯ")
    b += save_card(440, 200, 1, ("Солнечная ферма", 14, "2 450", "6 ч 12 мин", "21.10.2026 · 18:40", "weather_sunny"), True)
    b += save_card(440, 400, 2, ("Ферма у озера", 3, "320", "48 мин", "18.10.2026 · 12:05", "weather_rain"))
    b += save_card(440, 600, 3)
    b += button(440, 880, 220, 84, "Назад", "ghost", "back", 28) + button(1180, 880, 300, 84, "Загрузить", "primary", "load", 30)
    return frame("05_Загрузка_сохранений", b)


def delete_confirm():
    b = save_slots().split('<g id="05_Загрузка_сохранений">')[1].rsplit('</g></svg>', 1)[0]
    b += f'<rect width="{W}" height="{H}" fill="{UI["overlay"]}" opacity="0.55"/>'
    b += panel(610, 330, 700, 420, name="dialog") + ico("warning", 910, 360, 100)
    b += text(960, 510, "Удалить «Солнечная ферма»?", 36, 900, UI["text"], "middle")
    b += text(960, 556, "Прогресс нельзя будет восстановить.", 26, 800, UI["muted"], "middle")
    b += button(660, 610, 280, 88, "Отмена", "ghost", None, 30) + button(980, 610, 280, 88, "Удалить", "danger", "delete", 30)
    return frame("06_Диалог_удаления", b)


def loading():
    b = scenery(0.25)
    b += '<g id="loading_logo">' + facet([(660, 300), (1260, 300), (1276, 460), (644, 460)], P["wood"]) + facet([(676, 314), (1244, 314), (1258, 446), (662, 446)], P["wood_lt"]) + \
        text(960, 372, "MY LITTLE", 50, 900, P["cream"], "middle", ' stroke="#6E4423" stroke-width="8" paint-order="stroke"') + \
        text(960, 430, "FARM", 66, 900, P["yellow"], "middle", ' stroke="#6E4423" stroke-width="10" paint-order="stroke"') + '</g>'
    b += panel(360, 820, 1200, 200, name="loading_panel")
    b += f'<rect x="410" y="870" width="1100" height="40" rx="20" fill="{UI["line"]}"/><rect x="410" y="870" width="748" height="40" rx="20" fill="{UI["btn"]}"/>'
    b += f'<rect x="420" y="876" width="728" height="10" rx="5" fill="#fff" opacity="0.25"/>'
    b += ico("crop_carrot", 1118, 848, 76) + text(1510, 856, "68%", 28, 900, "#fff", "end", ' stroke="#3B2A1A" stroke-width="6" paint-order="stroke"')
    b += ico("help", 410, 925, 48) + text(470, 960, "Совет: морковь растёт быстрее всех — идеальна для первых денег.", 26, 800)
    b += text(960, 790, "Загрузка фермы…", 36, 900, "#fff", "middle", ' stroke="#3B2A1A" stroke-width="6" paint-order="stroke"')
    return frame("07_Экран_загрузки", b)


def hud_layer():
    b = ""
    # money (GDD: always visible, big number + icon)
    b += '<g id="hud_money">' + f'<rect x="30" y="30" width="330" height="88" rx="28" fill="{UI["panel_edge"]}"/><rect x="30" y="30" width="330" height="80" rx="28" fill="{UI["panel"]}"/>' + \
        ico("coin", 40, 34, 72) + text(124, 88, "2 450", 46, 900) + text(340, 88, "+18", 26, 900, UI["btn_dk"], "end") + '</g>'
    # clock / weather / speed
    b += '<g id="hud_time">' + f'<rect x="700" y="30" width="520" height="88" rx="28" fill="{UI["panel_edge"]}"/><rect x="700" y="30" width="520" height="80" rx="28" fill="{UI["panel"]}"/>' + \
        ico("day", 716, 40, 60) + text(786, 70, "День 14", 26, 900) + text(786, 100, "09:30", 26, 800, UI["muted"]) + \
        ico("weather_sunny", 930, 40, 60) + text(996, 82, "+10% рост", 22, 900, UI["btn_dk"]) + \
        button(1110, 44, 96, 58, "×2", "secondary", None, 28, "btn_speed") + '</g>'
    # right menu
    for i, (k, badge) in enumerate([("journal", None), ("catalog", None), ("achievements", "2"), ("map", None), ("pause", None)]):
        b += icon_button(1350 + i * 108, 30, k, 88, "ghost", badge)
    # quest / notification card
    b += '<g id="hud_quest">' + f'<rect x="30" y="150" width="380" height="190" rx="24" fill="{UI["panel"]}" opacity="0.95"/>' + ico("quest", 46, 166, 50) + \
        text(106, 202, "Заказ рынка", 28, 900) + ico("crop_carrot", 50, 228, 44) + text(104, 262, "Морковь  6 / 10", 24, 800) + \
        f'<rect x="54" y="280" width="330" height="18" rx="9" fill="{UI["line"]}"/><rect x="54" y="280" width="198" height="18" rx="9" fill="{UI["btn"]}"/>' + \
        ico("coin", 52, 300, 34) + text(92, 327, "Награда: 120", 22, 800, UI["muted"]) + '</g>'
    # toast
    b += '<g id="toast">' + f'<rect x="1540" y="160" width="350" height="76" rx="22" fill="{UI["btn"]}"/>' + ico("crop_tomato", 1552, 168, 58) + \
        text(1622, 210, "+3 Томат собран", 26, 900, "#fff") + '</g>'
    # quick bar 1-8 (GDD: tools/seeds at bottom)
    items = [("tool_hoe", None), ("tool_watering_can", None), ("tool_sickle", None), ("tool_axe", None), ("seeds_carrot", "12"), ("seeds_corn", "5"), ("seeds_tomato", "3"), (None, None)]
    x0 = 960 - (8 * 112 - 12) / 2
    b += f'<g id="quickbar"><rect x="{x0 - 24}" y="918" width="{8 * 112 + 36}" height="140" rx="34" fill="{UI["panel_edge"]}" opacity="0.92"/>'
    for i, (k, c) in enumerate(items):
        b += slot(x0 + i * 112, 940, 100, k, c, selected=(i == 4), hotkey=str(i + 1), name=f"quickslot_{i + 1}")
    b += '</g>'
    b += '<g id="hud_side">' + icon_button(1790, 960, "inventory", 100, "secondary") + text(1840, 1076, "I", 22, 900, "#fff", "middle") + \
        icon_button(1670, 970, "build", 90, "ghost") + text(1715, 1076, "B", 22, 900, "#fff", "middle") + '</g>'
    # interaction prompt (InteractionPromptUI)
    b += '<g id="interaction_prompt">' + f'<rect x="1000" y="560" width="300" height="70" rx="22" fill="{UI["panel"]}"/>' + \
        f'<rect x="1016" y="572" width="46" height="46" rx="10" fill="{UI["text"]}"/>' + text(1039, 606, "E", 28, 900, "#fff", "middle") + \
        ico("tool_sickle", 1070, 570, 50) + text(1126, 606, "Собрать", 30, 900) + facet([(1110, 630), (1140, 630), (1125, 648)], UI["panel"]) + '</g>'
    return b


def hud():
    return frame("08_HUD_игровой_экран", scenery(0) + hud_layer())


def inventory():
    b = scenery(0) + hud_layer() + f'<rect width="{W}" height="{H}" fill="{UI["overlay"]}" opacity="0.5"/>'
    b += panel(220, 110, 1480, 800, name="inventory_panel") + banner(960, 60, "ИНВЕНТАРЬ")
    b += tabs(270, 170, [("inventory", "Всё"), ("seeds_carrot", "Семена"), ("crop_apple", "Урожай"), ("egg", "Продукты"), ("b_flowerbed", "Декор")], 0, 196)
    b += icon_button(1630, 90, "close", 76, "danger")
    # permanent tool slots (GDD 8.2)
    b += text(270, 290, "Инструменты", 26, 900, UI["muted"])
    for i, k in enumerate(["tool_hoe", "tool_watering_can", "tool_sickle", "tool_axe", "tool_hammer", "tool_pickaxe"]):
        b += slot(270 + i * 112, 306, 100, k, name=f"toolslot_{i}")
    b += text(270, 450, "Предметы", 26, 900, UI["muted"])
    stock = [("crop_carrot", "24"), ("crop_corn", "8"), ("crop_wheat", "30"), ("crop_tomato", "12"), ("crop_cucumber", "6"), ("crop_potato", "15"),
             ("crop_pumpkin", "2"), ("crop_apple", "9"), ("egg", "11"), ("milk", "3"), ("seeds_carrot", "12"), ("seeds_corn", "5"),
             ("seeds_tomato", "3"), ("seeds_rare", "1"), ("hay", "20"), ("feed", "14"), (None, None), (None, None)]
    for i, (k, c) in enumerate(stock):
        b += slot(270 + (i % 6) * 112, 466 + (i // 6) * 112, 100, k, c, selected=(i == 3), name=f"invslot_{i}")
    b += slot(270 + 0 * 112, 466 + 3 * 112, 100, locked=True, name="invslot_locked") + slot(270 + 1 * 112, 466 + 3 * 112, 100, locked=True, name="invslot_locked2")
    # detail card
    dx = 980
    b += f'<g id="item_details"><rect x="{dx}" y="290" width="660" height="560" rx="24" fill="#FFFBEF" stroke="{UI["line"]}" stroke-width="4"/>'
    b += f'<rect x="{dx + 30}" y="320" width="200" height="200" rx="28" fill="{P["cream_dk"]}"/>' + ico("crop_tomato", dx + 50, 335, 160)
    b += text(dx + 260, 370, "Томат", 48, 900) + text(dx + 260, 410, "Урожай · многоразовый", 24, 800, UI["muted"])
    b += ico("coin", dx + 256, 432, 44) + text(dx + 306, 466, "15 / шт.", 30, 900) + ico("growth", dx + 446, 432, 44) + text(dx + 496, 466, "4 мин", 30, 900)
    b += text(dx + 30, 570, "Сочный томат с грядки. Растение плодоносит", 24, 700) + text(dx + 30, 604, "повторно — собирайте урожай несколько раз.", 24, 700)
    b += text(dx + 30, 664, "Количество", 24, 900, UI["muted"])
    b += button(dx + 30, 680, 64, 64, "−", "ghost", None, 34) + f'<rect x="{dx + 104}" y="680" width="120" height="56" rx="14" fill="#fff" stroke="{UI["line"]}" stroke-width="3"/>' + \
        text(dx + 164, 718, "12", 30, 900, UI["text"], "middle") + button(dx + 234, 680, 64, 64, "+", "ghost", None, 34)
    b += button(dx + 330, 676, 300, 72, "Продать · 180", "secondary", "sell", 26)
    b += button(dx + 30, 764, 600, 72, "В быстрый слот", "ghost", "arrow_right", 26) + '</g>'
    return frame("09_Инвентарь", b)


def shop():
    b = scenery(0) + hud_layer() + f'<rect width="{W}" height="{H}" fill="{UI["overlay"]}" opacity="0.5"/>'
    b += panel(220, 110, 1480, 800, name="shop_panel") + banner(960, 60, "КАТАЛОГ ФЕРМЫ")
    b += tabs(270, 170, [("seeds_carrot", "Семена"), ("chicken", "Животные"), ("b_house", "Постройки"), ("b_flowerbed", "Декор"), ("upgrade", "Улучшения")], 0, 214)
    b += icon_button(1630, 90, "close", 76, "danger")
    goods = [("seeds_carrot", "Морковь", "5", "60 сек", False), ("seeds_corn", "Кукуруза", "12", "3 мин", False), ("seeds_wheat", "Пшеница", "8", "2 мин", False),
             ("seeds_tomato", "Томат", "18", "4 мин", False), ("crop_pumpkin", "Тыква", "30", "8 мин", False), ("sapling", "Яблоня", "250", "20 мин", True),
             ("seeds_rare", "Редкие семена", "120", "?", True), ("crop_cucumber", "Огурец", "15", "3 мин", False)]
    for i, (k, n, price, t, locked) in enumerate(goods):
        x, y = 270 + (i % 4) * 352, 270 + (i // 4) * 300
        b += f'<g id="shop_card_{k}"><rect x="{x}" y="{y}" width="330" height="280" rx="24" fill="{"#EFE6D2" if locked else "#FFFBEF"}" stroke="{UI["line"]}" stroke-width="4"/>'
        b += ico(k, x + 105, y + 16, 120) + text(x + 165, y + 168, n, 30, 900, UI["text"] if not locked else UI["muted"], "middle")
        b += ico("growth", x + 60, y + 182, 34) + text(x + 100, y + 208, t, 22, 800, UI["muted"])
        if locked:
            b += ico("lock", x + 270, y + 14, 44) + button(x + 20, y + 214, 290, 56, "Уровень 3", "ghost", "lock", 22, state="disabled")
        else:
            b += button(x + 20, y + 214, 290, 56, price, "primary", "coin", 26)
        b += '</g>'
    b += text(1650, 880, "Нажмите на карточку, чтобы купить", 22, 800, UI["muted"], "end")
    return frame("10_Магазин_Каталог", b)


def pause():
    b = scenery(0) + hud_layer() + f'<rect width="{W}" height="{H}" fill="{UI["overlay"]}" opacity="0.6"/>'
    b += panel(660, 170, 600, 760, name="pause_panel") + banner(960, 120, "ПАУЗА", 380)
    for i, (lab, ic, kind) in enumerate([("Продолжить", "play", "primary"), ("Сохранить", "save", "ghost"), ("Загрузить", "load", "ghost"),
                                          ("Настройки", "settings", "ghost"), ("Главное меню", "home", "ghost"), ("Выход из игры", "exit", "danger")]):
        b += button(720, 240 + i * 108, 480, 88, lab, kind, ic, 30)
    return frame("11_Пауза", b)


def ui_kit():
    b = f'<rect width="{W}" height="{H}" fill="#FBF7EE"/>' + text(60, 90, "UI Kit · My Little Farm", 54, 900, P["grass_dk"])
    b += text(60, 170, "Кнопки", 32, 900)
    for i, st in enumerate(["normal", "hover", "pressed", "disabled"]):
        b += button(60 + i * 300, 190, 270, 84, {"normal": "Обычная", "hover": "Наведение", "pressed": "Нажата", "disabled": "Недоступна"}[st], "primary", None, 28, f"btn_primary_{st}", st)
    for i, kind in enumerate(["primary", "secondary", "ghost", "danger"]):
        b += button(60 + i * 300, 300, 270, 84, {"primary": "Основная", "secondary": "Акцент", "ghost": "Вторичная", "danger": "Опасная"}[kind], kind, None, 28, f"btn_kind_{kind}")
    b += text(60, 460, "Ячейки", 32, 900)
    b += slot(60, 480, 110, "crop_carrot", "24") + slot(190, 480, 110, "crop_carrot", "24", selected=True) + slot(320, 480, 110) + slot(450, 480, 110, locked=True) + slot(580, 480, 110, "tool_hoe", hotkey="1")
    b += text(60, 680, "Элементы управления", 32, 900)
    b += slider(60, 720, 360, 0.65, "Музыка", "music") + toggle(60, 800, True, "Тени") + toggle(60, 870, False, "Без звука")
    b += dropdown(60, 950, "Качество", "Высокое")
    b += text(1300, 170, "Палитра", 32, 900)
    pal = [("Трава", P["grass"]), ("Листва", P["leaf"]), ("Кнопка", UI["btn"]), ("Дерево", P["wood"]), ("Панель", UI["panel"]), ("Край панели", UI["panel_edge"]),
           ("Золото", P["gold"]), ("Фонарь", P["orange"]), ("Вода", P["water"]), ("Небо", P["sky"]), ("Ошибка", UI["danger"]), ("Текст", UI["text"])]
    for i, (n, c) in enumerate(pal):
        x, y = 1300 + (i % 3) * 200, 190 + (i // 3) * 130
        b += f'<rect x="{x}" y="{y}" width="180" height="80" rx="18" fill="{c}" stroke="#0001" stroke-width="2"/>' + text(x, y + 104, f"{n} {c}", 18, 800, UI["muted"])
    b += text(1300, 760, "Шрифт: Nunito", 32, 900) + text(1300, 820, "Заголовок 900 / 48", 48, 900) + text(1300, 870, "Кнопка 900 / 30", 30, 900) + \
        text(1300, 910, "Текст 700 / 24 — описание предмета", 24, 700) + text(1300, 950, "Подпись 800 / 20", 20, 800, UI["muted"])
    b += panel(920, 480, 330, 280, name="panel_example") + banner(1085, 440, "Окно", 200) + text(1085, 640, "Панель с рамкой", 24, 800, UI["muted"], "middle")
    return frame("00_UI_Kit", b)


SCREENS = [ui_kit, main_menu, settings_sound, settings_graphics, settings_controls, save_slots, delete_confirm, loading, hud, inventory, shop, pause]
