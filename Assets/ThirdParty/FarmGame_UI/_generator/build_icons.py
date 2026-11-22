import os
from icons import ICONS
from lowpoly import P

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CATS = {
    "menu": "Меню и система", "settings": "Настройки", "hud": "HUD и интерфейс",
    "tools": "Инструменты", "crops": "Культуры и семена", "animals": "Животные и продукты", "build": "Постройки и декор",
}
FONT = "Nunito, Arial Rounded MT Bold, sans-serif"


def icon_svg(key, size=64):
    body = ICONS[key][2]()
    return (f'<svg xmlns="http://www.w3.org/2000/svg" width="{size}" height="{size}" viewBox="0 0 64 64">'
            f'<g id="{key}">{body}</g></svg>')


def write_icons():
    for key, (cat, name, _) in ICONS.items():
        d = os.path.join(ROOT, "icons", f"{list(CATS).index(cat) + 1:02d}_{cat}")
        os.makedirs(d, exist_ok=True)
        with open(os.path.join(d, f"{key}.svg"), "w") as f:
            f.write(icon_svg(key))


def sheet():
    cols, cell, pad = 8, 150, 40
    rows_svg, y = [], 150
    for cat, title in CATS.items():
        keys = [k for k, v in ICONS.items() if v[0] == cat]
        rows_svg.append(f'<text x="{pad}" y="{y}" font-family="{FONT}" font-weight="900" font-size="30" fill="{P["ink"]}">{title} · {len(keys)}</text>')
        y += 20
        for i, k in enumerate(keys):
            cx, cy = pad + (i % cols) * cell, y + (i // cols) * (cell + 10)
            rows_svg.append(
                f'<g id="sheet_{k}"><rect x="{cx}" y="{cy}" width="{cell - 14}" height="{cell - 4}" rx="18" fill="{P["cream"]}" stroke="{P["cream_dk"]}" stroke-width="3"/>'
                f'<g transform="translate({cx + 28},{cy + 12}) scale(1.25)">{ICONS[k][2]()}</g>'
                f'<text x="{cx + (cell - 14) / 2}" y="{cy + 112}" font-family="{FONT}" font-weight="800" font-size="12" text-anchor="middle" fill="{P["ink"]}">{ICONS[k][1]}</text>'
                f'<text x="{cx + (cell - 14) / 2}" y="{cy + 130}" font-family="monospace" font-size="10" text-anchor="middle" fill="#8a7a66">{k}</text></g>')
        y += ((len(keys) + cols - 1) // cols) * (cell + 10) + 50
    w = pad * 2 + cols * cell - 14
    head = (f'<text x="{pad}" y="70" font-family="{FONT}" font-weight="900" font-size="44" fill="{P["grass_dk"]}">My Little Farm — набор иконок</text>'
            f'<text x="{pad}" y="105" font-family="{FONT}" font-weight="700" font-size="18" fill="#8a7a66">{len(ICONS)} иконок · low-poly · сетка 64×64</text>')
    return f'<svg xmlns="http://www.w3.org/2000/svg" width="{w}" height="{y}" viewBox="0 0 {w} {y}"><rect width="100%" height="100%" fill="#FBF7EE"/>{head}{"".join(rows_svg)}</svg>'


if __name__ == "__main__":
    write_icons()
    with open(os.path.join(ROOT, "00_all_icons_sheet.svg"), "w") as f:
        f.write(sheet())
    print(len(ICONS), "icons")
