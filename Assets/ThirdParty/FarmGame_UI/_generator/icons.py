"""All game icons, each drawn on a 64x64 canvas in the faceted low-poly style."""
import math
from lowpoly import P, facet, flat, ngon, blob, stick, rect_pts, shade, ground_shadow

ICONS = {}  # key -> (category, russian name, draw fn)


def icon(key, cat, name):
    def deco(fn):
        ICONS[key] = (cat, name, fn)
        return fn
    return deco


def star_pts(cx, cy, ro, ri, n=5, rot=0):
    pts = []
    for i in range(n * 2):
        r = ro if i % 2 == 0 else ri
        a = rot - math.pi / 2 + math.pi * i / n
        pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return pts


def check_mark(color, cx=32, cy=34, s=1.0):
    pts = [(cx - 16 * s, cy - 2 * s), (cx - 9 * s, cy - 9 * s), (cx - 4 * s, cy - 4 * s),
           (cx + 11 * s, cy - 19 * s), (cx + 18 * s, cy - 12 * s), (cx - 4 * s, cy + 10 * s)]
    return facet(pts, color, center=(cx - 4 * s, cy + 1 * s))


def cross(color, cx=32, cy=32, s=1.0):
    return stick(cx - 15 * s, cy - 15 * s, cx + 15 * s, cy + 15 * s, 10 * s, color) + \
        stick(cx + 15 * s, cy - 15 * s, cx - 15 * s, cy + 15 * s, 10 * s, color)


# ───────────────────────── МЕНЮ / СИСТЕМА ─────────────────────────

@icon("play", "menu", "Старт / Играть")
def _():
    return facet([(18, 10), (52, 32), (18, 54)], P["grass"], center=(28, 32))


@icon("continue", "menu", "Продолжить")
def _():
    return facet([(10, 12), (34, 32), (10, 52)], P["grass"], center=(18, 32)) + \
        facet([(30, 12), (54, 32), (30, 52)], P["leaf"], center=(38, 32))


@icon("pause", "menu", "Пауза")
def _():
    return facet(rect_pts(14, 12, 13, 40), P["sky_dk"]) + facet(rect_pts(37, 12, 13, 40), P["sky_dk"])


@icon("settings", "menu", "Настройки")
def _():
    pts = []
    for i in range(32):
        a = -math.pi / 2 + 2 * math.pi * i / 32
        r = 26 if (i % 4) in (0, 1) else 19
        pts.append((32 + r * math.cos(a), 32 + r * math.sin(a)))
    return facet(pts, P["stone"], center=(32, 32)) + facet(ngon(32, 32, 10, 8), P["stone_dk"]) + \
        facet(ngon(32, 32, 5, 6), P["ink"])


@icon("save", "menu", "Сохранить")
def _():
    body = [(10, 10), (46, 10), (54, 18), (54, 54), (10, 54)]
    return facet(body, P["sky_dk"]) + facet(rect_pts(18, 10, 24, 14), P["cream"]) + \
        facet(rect_pts(34, 12, 5, 9), P["ink"]) + facet(rect_pts(16, 32, 32, 22), P["white"]) + \
        flat(rect_pts(20, 38, 24, 2.5), P["stone"]) + flat(rect_pts(20, 44, 24, 2.5), P["stone"])


@icon("load", "menu", "Загрузить")
def _():
    # folder with an arrow coming out
    back = [(8, 16), (24, 16), (29, 21), (56, 21), (56, 52), (8, 52)]
    front = [(8, 28), (56, 28), (56, 52), (8, 52)]
    arrow = [(32, 31), (43, 41), (36, 41), (36, 49), (28, 49), (28, 41), (21, 41)]
    return facet(back, P["wood"]) + facet(front, P["wood_lt"]) + facet(arrow, P["cream"], center=(32, 41))


@icon("new_game", "menu", "Новая игра (+)")
def _():
    return facet(ngon(32, 32, 26, 10), P["grass"]) + \
        facet(rect_pts(28, 16, 8, 32), P["white"]) + facet(rect_pts(16, 28, 32, 8), P["white"])


@icon("exit", "menu", "Выход")
def _():
    door = [(12, 8), (36, 8), (36, 56), (12, 56)]
    arrow = [(36, 26), (46, 26), (46, 18), (58, 32), (46, 46), (46, 38), (36, 38)]
    return facet(door, P["wood"]) + flat([(12, 8), (30, 14), (30, 56), (12, 56)], P["wood_dk"]) + \
        flat(ngon(26, 34, 2, 6), P["gold"]) + facet(arrow, P["red"], center=(46, 32))


@icon("back", "menu", "Назад")
def _():
    return facet([(8, 32), (30, 10), (30, 23), (56, 23), (56, 41), (30, 41), (30, 54)], P["wood_lt"], center=(28, 32))


@icon("home", "menu", "Главное меню")
def _():
    return facet([(32, 8), (58, 30), (6, 30)], P["red"], center=(32, 22)) + \
        facet(rect_pts(14, 30, 36, 24), P["cream"]) + facet(rect_pts(27, 38, 10, 16), P["wood"])


@icon("close", "menu", "Закрыть")
def _():
    return facet(ngon(32, 32, 26, 10), P["red"]) + cross(P["white"], s=0.65)


@icon("confirm", "menu", "Подтвердить")
def _():
    return facet(ngon(32, 32, 26, 10), P["grass"]) + check_mark(P["white"], cx=31, cy=35, s=0.85)


@icon("delete", "menu", "Удалить")
def _():
    can = [(16, 20), (48, 20), (44, 56), (20, 56)]
    return facet(can, P["stone"]) + facet(rect_pts(12, 13, 40, 7), P["stone_dk"]) + \
        facet(rect_pts(26, 8, 12, 5), P["stone_dk"]) + \
        flat(rect_pts(24, 26, 3, 24), shade(P["stone_dk"], -0.1)) + flat(rect_pts(30.5, 26, 3, 24), shade(P["stone_dk"], -0.1)) + \
        flat(rect_pts(37, 26, 3, 24), shade(P["stone_dk"], -0.1))


@icon("info", "menu", "Информация")
def _():
    return facet(ngon(32, 32, 26, 10), P["sky_dk"]) + facet(ngon(32, 19, 4, 6), P["white"]) + \
        facet(rect_pts(28, 27, 8, 20), P["white"])


@icon("help", "menu", "Подсказка / ?")
def _():
    q = [(22, 22), (26, 14), (34, 12), (42, 16), (44, 24), (38, 31), (35, 34), (35, 40), (29, 40), (29, 31), (36, 25), (36, 21), (32, 19), (29, 22)]
    return facet(ngon(32, 32, 26, 10), P["orange"]) + facet(q, P["white"], center=(34, 22)) + facet(ngon(32, 47, 4, 6), P["white"])


@icon("lock", "menu", "Закрыто")
def _():
    arc = []
    for i in range(9):
        a = math.pi + math.pi * i / 8
        arc.append((32 + 12 * math.cos(a), 24 + 12 * math.sin(a)))
    sh = "".join(stick(*arc[i], *arc[i + 1], 6, P["stone_dk"]) for i in range(8))
    return sh + stick(20, 24, 20, 30, 6, P["stone_dk"]) + stick(44, 24, 44, 30, 6, P["stone_dk"]) + \
        facet(rect_pts(12, 28, 40, 28), P["gold"]) + facet([(30, 36), (34, 36), (35, 40), (33.5, 41), (34, 48), (30, 48), (30.5, 41), (29, 40)], P["ink"])


@icon("unlock", "menu", "Открыть участок")
def _():
    arc = []
    for i in range(9):
        a = math.pi + math.pi * i / 8
        arc.append((44 + 12 * math.cos(a), 18 + 12 * math.sin(a)))
    sh = "".join(stick(*arc[i], *arc[i + 1], 6, P["stone_dk"]) for i in range(8))
    return sh + stick(56, 18, 56, 24, 6, P["stone_dk"]) + stick(32, 18, 32, 30, 6, P["stone_dk"]) + \
        facet(rect_pts(12, 28, 40, 28), P["leaf"]) + facet([(30, 36), (34, 36), (35, 40), (33.5, 41), (34, 48), (30, 48), (30.5, 41), (29, 40)], P["ink"])


@icon("arrow_left", "menu", "Стрелка влево")
def _():
    return facet([(14, 32), (40, 8), (40, 56)], P["wood_lt"], center=(30, 32))


@icon("arrow_right", "menu", "Стрелка вправо")
def _():
    return facet([(50, 32), (24, 8), (24, 56)], P["wood_lt"], center=(34, 32))


# ───────────────────────── НАСТРОЙКИ ─────────────────────────

def speaker(color):
    return facet([(8, 24), (20, 24), (34, 10), (34, 54), (20, 40), (8, 40)], color, center=(22, 32))


def arc_lines(cx, cy, r, w, color, a0=-50, a1=50, n=6):
    pts = [(cx + r * math.cos(math.radians(a0 + (a1 - a0) * i / n)), cy + r * math.sin(math.radians(a0 + (a1 - a0) * i / n))) for i in range(n + 1)]
    return "".join(stick(*pts[i], *pts[i + 1], w, color) for i in range(n))


@icon("volume", "settings", "Общая громкость")
def _():
    return speaker(P["sky_dk"]) + arc_lines(32, 32, 12, 4.5, P["sky_dk"]) + arc_lines(32, 32, 22, 4.5, P["sky_dk"])


@icon("volume_mute", "settings", "Звук выключен")
def _():
    return speaker(P["stone"]) + cross(P["red"], cx=47, cy=32, s=0.42)


@icon("music", "settings", "Музыка")
def _():
    return stick(26, 46, 26, 12, 5, P["purple"]) + stick(50, 40, 50, 8, 5, P["purple"]) + \
        facet([(24, 9), (52, 4), (52, 14), (24, 19)], P["purple"]) + \
        facet(ngon(20, 47, 8, 7, sx=1.2), P["purple"]) + facet(ngon(44, 41, 8, 7, sx=1.2), P["purple"])


@icon("sfx", "settings", "Звуковые эффекты")
def _():
    burst = star_pts(26, 34, 20, 10, n=8, rot=0.2)
    return facet(burst, P["orange"], center=(26, 34)) + facet(star_pts(26, 34, 9, 5, n=8, rot=0.2), P["yellow"], center=(26, 34)) + \
        facet(star_pts(50, 14, 8, 3.5, n=4), P["yellow"], center=(50, 14))


@icon("graphics", "settings", "Графика / Качество")
def _():
    # faceted gem = "low-poly quality"
    top = [(14, 22), (22, 12), (42, 12), (50, 22)]
    return facet(top, P["sky"]) + facet([(14, 22), (50, 22), (32, 54)], P["sky_dk"], center=(32, 30)) + \
        flat([(22, 12), (32, 22), (42, 12)], shade(P["sky"], 0.4)) + \
        facet(star_pts(52, 46, 7, 2.5, n=4), P["yellow"], center=(52, 46))


@icon("display", "settings", "Экран / Разрешение")
def _():
    return facet(rect_pts(6, 10, 52, 34), P["ink"]) + facet(rect_pts(10, 14, 44, 26), P["sky"]) + \
        facet([(10, 40), (24, 26), (34, 34), (42, 28), (54, 40)], P["grass"], center=(32, 36)) + \
        facet(rect_pts(28, 44, 8, 6), P["stone_dk"]) + facet(rect_pts(18, 50, 28, 5), P["stone_dk"])


@icon("brightness", "settings", "Яркость")
def _():
    rays = facet(star_pts(32, 32, 28, 17, n=10), P["gold_dk"], center=(32, 32))
    return rays + facet(ngon(32, 32, 15, 10), P["gold"])


@icon("language", "settings", "Язык")
def _():
    s = facet(ngon(32, 32, 26, 12), P["sky_dk"]) + \
        facet([(16, 16), (30, 12), (34, 22), (26, 30), (20, 28)], P["grass"]) + \
        facet([(36, 32), (48, 30), (54, 38), (44, 52), (36, 44)], P["grass"]) + \
        facet([(12, 36), (20, 34), (24, 44), (18, 50)], P["grass"])
    return s


@icon("controls", "settings", "Управление")
def _():
    keys = ""
    for (x, y, l) in [(24, 8, "W"), (8, 26, "A"), (24, 26, "S"), (40, 26, "D")]:
        keys += facet(rect_pts(x, y, 16, 16), P["cream_dk"]) + facet(rect_pts(x + 2, y + 2, 12, 11), P["cream"]) + \
            f'<text x="{x + 8}" y="{y + 11.5}" font-family="Nunito, Arial Rounded MT Bold, sans-serif" font-weight="900" font-size="9" text-anchor="middle" fill="{P["ink"]}">{l}</text>'
    return keys + facet(rect_pts(8, 46, 48, 12), P["cream_dk"]) + facet(rect_pts(10, 48, 44, 7), P["cream"])


@icon("mouse", "settings", "Мышь / Камера")
def _():
    body = ngon(32, 34, 22, 12, sx=0.72, sy=1.0)
    return facet(body, P["cream_dk"]) + \
        stick(32, 12, 32, 30, 2, P["stone_dk"]) + facet(rect_pts(30, 18, 4, 8), P["orange"])


@icon("camera", "settings", "Камера")
def _():
    return facet(rect_pts(6, 18, 52, 34), P["stone_dk"]) + facet(rect_pts(20, 12, 14, 6), P["stone_dk"]) + \
        facet(ngon(32, 35, 13, 10), P["ink"]) + facet(ngon(32, 35, 8, 8), P["sky_dk"]) + flat(ngon(29, 32, 2.5, 6), P["white"]) + \
        facet(rect_pts(46, 22, 7, 4), P["orange"])


@icon("fullscreen", "settings", "Полный экран")
def _():
    c = P["sky_dk"]
    return facet([(8, 8), (26, 8), (20, 14), (14, 14), (14, 20), (8, 26)], c) + \
        facet([(56, 8), (56, 26), (50, 20), (50, 14), (44, 14), (38, 8)], c) + \
        facet([(8, 56), (8, 38), (14, 44), (14, 50), (20, 50), (26, 56)], c) + \
        facet([(56, 56), (38, 56), (44, 50), (50, 50), (50, 44), (56, 38)], c)


@icon("checkbox_on", "settings", "Галочка (вкл.)")
def _():
    return facet(rect_pts(8, 8, 48, 48), P["grass"]) + check_mark(P["white"], cx=31, cy=36, s=0.85)


@icon("checkbox_off", "settings", "Галочка (выкл.)")
def _():
    return facet(rect_pts(8, 8, 48, 48), P["cream_dk"]) + facet(rect_pts(14, 14, 36, 36), P["cream"])


# ───────────────────────── HUD ─────────────────────────

@icon("coin", "hud", "Монеты")
def _():
    return facet(ngon(32, 33, 26, 12), P["gold_dk"]) + facet(ngon(32, 31, 24, 12), P["gold"]) + \
        facet(ngon(32, 31, 15, 8, rot=math.pi / 8), P["gold_dk"]) + \
        facet([(32, 20), (40, 31), (32, 42), (24, 31)], P["yellow"])


@icon("coins_stack", "hud", "Прибыль / Кошелёк")
def _():
    s = ""
    for i, y in enumerate([50, 43, 36]):
        s += facet(ngon(24, y, 16, 10, sy=0.42), P["gold_dk"]) + facet(ngon(24, y - 3, 16, 10, sy=0.42), P["gold"])
    return s + facet(ngon(45, 32, 15, 10), P["gold_dk"]) + facet(ngon(45, 30, 14, 10), P["gold"]) + \
        facet([(45, 22), (50, 30), (45, 38), (40, 30)], P["yellow"])


@icon("clock", "hud", "Время")
def _():
    return facet(ngon(32, 32, 27, 12), P["wood"]) + facet(ngon(32, 32, 22, 12), P["cream"]) + \
        stick(32, 32, 32, 16, 4, P["ink"]) + stick(32, 32, 43, 38, 4, P["ink"]) + facet(ngon(32, 32, 3.5, 6), P["red"])


@icon("speed", "hud", "Скорость времени")
def _():
    return facet([(6, 14), (30, 32), (6, 50)], P["orange"], center=(14, 32)) + facet([(30, 14), (54, 32), (30, 50)], P["orange"], center=(38, 32)) + \
        facet(rect_pts(54, 14, 5, 36), P["orange"])


@icon("day", "hud", "День")
def _():
    return facet(star_pts(32, 32, 28, 18, n=8, rot=math.pi / 8), P["gold_dk"], center=(32, 32)) + facet(ngon(32, 32, 16, 10), P["gold"])


@icon("night", "hud", "Ночь")
def _():
    out, inn = [], []
    N = 10
    for k in range(N + 1):
        t = k / N
        th = math.radians(60 + 240 * t)
        sq = 1 - 0.62 * (1 - abs(t - 0.5) * 2)
        out.append((34 + 24 * math.cos(th), 32 + 24 * math.sin(th)))
        inn.append((34 + 24 * math.cos(th) * sq, 32 + 24 * math.sin(th)))
    s = "".join(facet([out[k], out[k + 1], inn[k + 1], inn[k]], P["yellow"], seed=k) for k in range(N))
    return s + facet(star_pts(48, 16, 7, 2.8, n=4), P["gold"], center=(48, 16)) + facet(star_pts(52, 42, 4.5, 1.8, n=4), P["gold"], center=(52, 42))


def cloud(cx, cy, s, color):
    return blob(cx - 10 * s, cy + 2 * s, 9 * s, n=8, seed=2, color=color) + blob(cx + 9 * s, cy + 3 * s, 9 * s, n=8, seed=5, color=color) + \
        blob(cx, cy - 4 * s, 12 * s, n=9, seed=3, color=color) + facet(rect_pts(cx - 18 * s, cy + 2 * s, 36 * s, 9 * s), color)


@icon("weather_sunny", "hud", "Погода: солнечно")
def _():
    return facet(star_pts(32, 32, 26, 16, n=8, rot=math.pi / 8), P["gold_dk"], center=(32, 32)) + facet(ngon(32, 32, 14, 10), P["gold"])


@icon("weather_cloudy", "hud", "Погода: облачно")
def _():
    return facet(star_pts(42, 20, 16, 10, n=8), P["gold_dk"], center=(42, 20)) + facet(ngon(42, 20, 9, 8), P["gold"]) + cloud(28, 38, 1.05, P["white"])


@icon("weather_rain", "hud", "Погода: дождь")
def _():
    drops = "".join(facet([(x, y), (x + 3, y + 7), (x, y + 10), (x - 3, y + 7)], P["sky_dk"]) for x, y in [(20, 44), (32, 48), (44, 44)])
    return cloud(32, 26, 1.1, P["stone"]) + drops


@icon("calendar", "hud", "День фермы")
def _():
    return facet(rect_pts(8, 12, 48, 44), P["cream"]) + facet(rect_pts(8, 12, 48, 12), P["red"]) + \
        stick(20, 6, 20, 16, 5, P["ink"]) + stick(44, 6, 44, 16, 5, P["ink"]) + \
        "".join(flat(rect_pts(14 + 10 * (i % 4), 30 + 9 * (i // 4), 6, 5), P["cream_dk"] if i != 5 else P["grass"]) for i in range(8))


@icon("level", "hud", "Уровень фермы")
def _():
    return facet(star_pts(32, 33, 27, 12), P["gold"], center=(32, 33))


@icon("achievements", "hud", "Достижения")
def _():
    cup = [(14, 10), (50, 10), (48, 24), (40, 34), (24, 34), (16, 24)]
    return stick(16, 14, 8, 22, 4, P["gold_dk"]) + stick(48, 14, 56, 22, 4, P["gold_dk"]) + facet(cup, P["gold"]) + \
        facet(rect_pts(28, 34, 8, 8), P["gold_dk"]) + facet(rect_pts(18, 42, 28, 6), P["wood"]) + facet(rect_pts(14, 48, 36, 8), P["wood_dk"]) + \
        facet(star_pts(32, 21, 7, 3), P["yellow"], center=(32, 21))


@icon("journal", "hud", "Журнал фермы")
def _():
    return facet(rect_pts(12, 8, 40, 48), P["grass_dk"]) + facet(rect_pts(16, 8, 36, 44), P["grass"]) + \
        facet(rect_pts(16, 52, 36, 4), P["cream"]) + facet(rect_pts(24, 18, 22, 12), P["cream"]) + \
        facet([(42, 8), (48, 8), (48, 20), (45, 17), (42, 20)], P["red"])


@icon("catalog", "hud", "Каталог / Магазин")
def _():
    roof = [(6, 22), (12, 8), (52, 8), (58, 22)]
    stripes = "".join(facet([(6 + i * 13, 22), (12 + i * 10, 8), (22 + i * 10, 8), (19 + i * 13, 22)], P["red"] if i % 2 == 0 else P["white"]) for i in range(4))
    return facet(rect_pts(10, 22, 44, 34), P["wood"]) + facet(rect_pts(14, 32, 36, 6), P["wood_lt"]) + \
        blob(22, 28, 5, n=6, seed=4, color=P["red"]) + blob(32, 28, 5, n=6, seed=2, color=P["orange"]) + blob(42, 28, 5, n=6, seed=6, color=P["leaf"]) + \
        facet(roof, P["red"]) + stripes


@icon("build", "hud", "Режим строительства")
def _():
    return facet([(4, 46), (40, 34), (44, 44), (8, 56)], P["wood_lt"]) + hammer(26, 52, 44, 20)


@icon("inventory", "hud", "Инвентарь")
def _():
    bag = [(12, 22), (52, 22), (54, 54), (10, 54)]
    return stick(24, 22, 26, 10, 4, P["wood_dk"]) + stick(40, 22, 38, 10, 4, P["wood_dk"]) + stick(26, 10, 38, 10, 4, P["wood_dk"]) + \
        facet(bag, P["wood"]) + facet([(12, 22), (52, 22), (48, 36), (16, 36)], P["wood_lt"]) + facet(rect_pts(28, 32, 8, 7), P["gold"])


@icon("map", "hud", "Карта")
def _():
    a = [(6, 14), (22, 8), (22, 50), (6, 56)]
    b = [(22, 8), (42, 14), (42, 56), (22, 50)]
    c = [(42, 14), (58, 8), (58, 50), (42, 56)]
    return facet(a, P["cream"]) + facet(b, P["cream_dk"]) + facet(c, P["cream"]) + \
        blob(16, 28, 6, n=6, seed=3, color=P["grass"]) + facet(ngon(48, 36, 6, 6), P["water"]) + \
        cross(P["red"], cx=32, cy=26, s=0.25)


@icon("notification", "hud", "Уведомление")
def _():
    bell = [(32, 8), (42, 14), (46, 30), (52, 44), (12, 44), (18, 30), (22, 14)]
    return facet(bell, P["gold"], center=(32, 30)) + facet(ngon(32, 50, 6, 8), P["gold_dk"]) + facet(ngon(48, 14, 8, 8), P["red"])


@icon("quest", "hud", "Заказ рынка")
def _():
    return facet(rect_pts(12, 8, 40, 50), P["cream"]) + facet(rect_pts(24, 4, 16, 8), P["wood"]) + \
        "".join(flat(rect_pts(26, 20 + 10 * i, 20, 3), P["stone"]) for i in range(3)) + \
        "".join(facet(rect_pts(16, 18 + 10 * i, 6, 6), P["grass"] if i < 2 else P["cream_dk"]) for i in range(3))


@icon("sell", "hud", "Продать")
def _():
    tag = [(10, 32), (30, 10), (54, 10), (54, 34), (32, 56)]
    return facet(tag, P["orange"], center=(36, 30)) + facet(ngon(44, 20, 4, 6), P["cream"]) + \
        f'<text x="31" y="41" font-family="Nunito, Arial Rounded MT Bold, sans-serif" font-weight="900" font-size="15" text-anchor="middle" fill="{P["white"]}" transform="rotate(-45 31 36)">$</text>'


@icon("buy", "hud", "Купить")
def _():
    basket = [(8, 26), (56, 26), (50, 54), (14, 54)]
    return blob(24, 22, 8, n=7, seed=3, color=P["red"]) + blob(40, 20, 8, n=7, seed=5, color=P["orange"]) + \
        facet(basket, P["wood"]) + "".join(flat(rect_pts(14, 32 + 7 * i, 36, 2.5), P["wood_dk"]) for i in range(3))


@icon("interact", "hud", "Взаимодействие (рука)")
def _():
    hand = [(16, 34), (20, 30), (24, 34), (24, 12), (29, 10), (31, 14), (31, 26), (32, 10), (37, 9), (39, 13), (39, 26), (40, 14), (45, 13), (47, 17), (47, 28),
            (48, 22), (52, 21), (54, 25), (52, 44), (44, 56), (28, 56), (20, 46)]
    return facet(hand, P["cream_dk"], center=(36, 38))


@icon("upgrade", "hud", "Улучшение")
def _():
    return facet(ngon(32, 32, 27, 10), P["grass"]) + facet([(32, 12), (48, 30), (38, 30), (38, 50), (26, 50), (26, 30), (16, 30)], P["white"], center=(32, 32))


@icon("warning", "hud", "Нельзя / Ошибка")
def _():
    return facet([(32, 6), (60, 56), (4, 56)], P["orange"], center=(32, 38)) + facet(rect_pts(29, 22, 6, 18), P["ink"]) + facet(ngon(32, 47, 3.5, 6), P["ink"])


@icon("rotate", "hud", "Повернуть (стройка)")
def _():
    pts = [(32 + 20 * math.cos(math.radians(a)), 32 + 20 * math.sin(math.radians(a))) for a in range(-200, 80, 28)]
    s = "".join(stick(*pts[i], *pts[i + 1], 7, P["sky_dk"]) for i in range(len(pts) - 1))
    ex, ey = pts[-1]
    return s + facet([(ex - 12, ey - 6), (ex + 6, ey - 12), (ex + 2, ey + 8)], P["sky_dk"])


# ───────────────────────── ИНСТРУМЕНТЫ ─────────────────────────

@icon("tool_hoe", "tools", "Мотыга")
def _():
    return stick(14, 58, 40, 12, 5, P["wood"]) + facet([(36, 10), (44, 12), (58, 30), (52, 34), (40, 20)], P["stone"], center=(46, 20))


@icon("tool_watering_can", "tools", "Лейка")
def _():
    return stick(40, 34, 58, 18, 5, P["sky_dk"]) + facet(ngon(58, 16, 5, 6), P["sky_dk"]) + \
        facet([(12, 26), (42, 26), (44, 54), (10, 54)], P["sky"]) + \
        "".join(stick(*a, *b, 4, P["sky_dk"]) for a, b in [((16, 26), (18, 14)), ((18, 14), (34, 14)), ((34, 14), (38, 26))]) + \
        facet([(52, 26), (56, 28), (54, 32)], P["water"]) + facet([(48, 34), (52, 36), (49, 40)], P["water"])


@icon("tool_shovel", "tools", "Лопата")
def _():
    return stick(46, 10, 30, 38, 5, P["wood"]) + stick(40, 8, 52, 14, 5, P["wood_dk"]) + \
        facet([(28, 34), (38, 40), (32, 54), (20, 58), (16, 48)], P["stone"], center=(26, 46))


@icon("tool_axe", "tools", "Топор")
def _():
    return stick(18, 56, 40, 12, 5, P["wood"]) + facet([(34, 10), (50, 6), (56, 20), (50, 34), (40, 26)], P["stone"], center=(46, 18))


@icon("tool_sickle", "tools", "Серп (сбор урожая)")
def _():
    blade = []
    for i in range(9):
        a = math.radians(200 + 150 * i / 8)
        blade.append((34 + 20 * math.cos(a), 30 + 20 * math.sin(a)))
    for i in range(9):
        a = math.radians(350 - 150 * i / 8)
        blade.append((36 + 14 * math.cos(a), 32 + 13 * math.sin(a)))
    return facet(blade, P["stone"], center=(34, 18)) + stick(16, 38, 10, 56, 6, P["wood"])


def hammer(x1, y1, x2, y2):
    L = math.hypot(x2 - x1, y2 - y1)
    dx, dy = (x2 - x1) / L, (y2 - y1) / L
    px, py = -dy, dx
    head = [(x2 + px * 14 + dx * 6, y2 + py * 14 + dy * 6), (x2 - px * 10 + dx * 6, y2 - py * 10 + dy * 6),
            (x2 - px * 12 + dx * 2, y2 - py * 12 + dy * 2), (x2 - px * 10 - dx * 6, y2 - py * 10 - dy * 6),
            (x2 + px * 14 - dx * 6, y2 + py * 14 - dy * 6)]
    return stick(x1, y1, x2, y2, 5.5, P["wood"]) + facet(head, P["stone_dk"])


@icon("tool_hammer", "tools", "Молоток")
def _():
    return hammer(18, 56, 40, 20)


@icon("tool_pickaxe", "tools", "Кирка (камни)")
def _():
    head = [(10, 22), (22, 10), (36, 6), (50, 10), (58, 20), (46, 16), (34, 14), (20, 16)]
    return stick(24, 56, 38, 12, 5, P["wood"]) + facet(head, P["stone"], center=(34, 11))


@icon("tool_hand", "tools", "Рука (без инструмента)")
def _():
    return ICONS["interact"][2]()


@icon("tool_upgrade_area", "tools", "Улучшение: 3 клетки")
def _():
    return "".join(facet(rect_pts(6 + 18 * i, 30, 16, 16), P["soil"]) for i in range(3)) + \
        facet([(32, 6), (44, 20), (36, 20), (36, 28), (28, 28), (28, 20), (20, 20)], P["grass"], center=(32, 18))


# ───────────────────────── КУЛЬТУРЫ / УРОЖАЙ ─────────────────────────

def leaves(x, y, color=P["grass"], s=1.0):
    return facet([(x, y), (x - 9 * s, y - 14 * s), (x - 2 * s, y - 12 * s)], color) + \
        facet([(x, y), (x - 1 * s, y - 18 * s), (x + 4 * s, y - 14 * s)], shade(color, 0.1)) + \
        facet([(x, y), (x + 8 * s, y - 14 * s), (x + 9 * s, y - 6 * s)], color)


@icon("crop_carrot", "crops", "Морковь")
def _():
    return leaves(40, 20) + facet([(30, 18), (46, 24), (14, 58)], P["orange"], center=(34, 26))


@icon("crop_corn", "crops", "Кукуруза")
def _():
    cob = ngon(32, 30, 22, 10, sx=0.48)
    kern = "".join(flat(rect_pts(26 + 4 * (i % 3), 14 + 5 * (i // 3), 3, 3.5), shade(P["gold"], 0.2 if (i + i // 3) % 2 else -0.05)) for i in range(18))
    return facet(cob, P["gold"]) + kern + facet([(32, 58), (14, 30), (20, 24), (30, 44)], P["grass"]) + facet([(32, 58), (50, 30), (44, 24), (34, 44)], P["grass_dk"])


@icon("crop_wheat", "crops", "Пшеница")
def _():
    s = ""
    for dx, ang in [(-10, -0.3), (0, 0), (10, 0.3)]:
        bx, by = 32 + dx * 0.4, 58
        tx, ty = 32 + dx + math.sin(ang) * 10, 18
        s += stick(bx, by, tx, ty + 10, 2.5, P["gold_dk"])
        for k in range(4):
            yy = ty + k * 5
            s += facet(ngon(tx - 3, yy, 3.5, 5, sy=1.5), P["gold"]) + facet(ngon(tx + 3, yy + 2, 3.5, 5, sy=1.5), P["gold"])
        s += facet(ngon(tx, ty - 4, 3, 5, sy=1.6), P["gold"])
    return s


@icon("crop_tomato", "crops", "Томат")
def _():
    return blob(32, 36, 22, n=11, seed=4, color=P["red"], wobble=0.08) + \
        facet(star_pts(32, 17, 10, 4, n=5), P["grass_dk"], center=(32, 17)) + stick(32, 16, 34, 6, 3, P["grass_dk"])


@icon("crop_cucumber", "crops", "Огурец")
def _():
    body = ngon(32, 32, 26, 12, sx=0.42, rot=0)
    body = [((x - 32) * math.cos(0.7) - (y - 32) * math.sin(0.7) + 32, (x - 32) * math.sin(0.7) + (y - 32) * math.cos(0.7) + 32) for x, y in body]
    return facet(body, P["grass"]) + "".join(flat(ngon(x, y, 1.4, 4), P["leaf"]) for x, y in [(26, 30), (34, 26), (38, 36), (30, 40), (42, 30)]) + \
        stick(46, 14, 52, 8, 3, P["grass_dk"])


@icon("crop_potato", "crops", "Картофель")
def _():
    return blob(32, 34, 22, n=11, seed=7, sx=1.15, sy=0.85, color="#C99A5B", wobble=0.15) + \
        "".join(flat(ngon(x, y, 1.8, 5), "#8E6838") for x, y in [(22, 30), (36, 26), (42, 38), (28, 42)])


@icon("crop_pumpkin", "crops", "Тыква")
def _():
    return blob(20, 38, 15, n=8, seed=2, sy=0.9, color=P["orange"], wobble=0.06) + blob(44, 38, 15, n=8, seed=4, sy=0.9, color=P["orange"], wobble=0.06) + \
        blob(32, 38, 16, n=9, seed=6, sy=0.95, color=shade(P["orange"], 0.08), wobble=0.05) + stick(32, 24, 35, 12, 5, P["grass_dk"]) + \
        facet([(36, 16), (48, 10), (44, 20)], P["grass"])


@icon("crop_apple", "crops", "Яблоко")
def _():
    return blob(32, 38, 21, n=11, seed=3, color=P["red"], wobble=0.08) + stick(32, 20, 34, 8, 3, P["wood_dk"]) + facet([(35, 14), (48, 8), (44, 18)], P["grass"])


@icon("sapling", "crops", "Саженец / Яблоня")
def _():
    return stick(32, 56, 32, 34, 5, P["wood"]) + blob(32, 26, 20, n=10, seed=2, color=P["leaf"]) + \
        facet(ngon(24, 30, 4, 6), P["red"]) + facet(ngon(40, 22, 4, 6), P["red"]) + facet(ngon(36, 34, 4, 6), P["red"]) + \
        facet([(22, 56), (42, 56), (40, 60), (24, 60)], P["soil"])


def seed_bag(color, emblem):
    bag = [(14, 18), (50, 18), (54, 56), (10, 56)]
    return facet(bag, P["cream_dk"]) + facet([(18, 8), (46, 8), (50, 18), (14, 18)], P["cream"]) + stick(16, 18, 48, 18, 3, P["wood_dk"]) + \
        facet(ngon(32, 38, 12, 8), color) + emblem


@icon("seeds_carrot", "crops", "Семена моркови")
def _():
    return seed_bag(P["orange"], facet([(32, 30), (37, 34), (30, 46)], P["white"]))


@icon("seeds_corn", "crops", "Семена кукурузы")
def _():
    return seed_bag(P["gold"], facet(ngon(32, 38, 6, 6, sx=0.6, sy=1.2), P["white"]))


@icon("seeds_wheat", "crops", "Семена пшеницы")
def _():
    return seed_bag(P["gold_dk"], stick(32, 46, 32, 30, 2, P["white"]) + facet(ngon(29, 35, 2.5, 5), P["white"]) + facet(ngon(35, 33, 2.5, 5), P["white"]))


@icon("seeds_tomato", "crops", "Семена томата")
def _():
    return seed_bag(P["red"], facet(ngon(32, 39, 6, 7), P["white"]))


@icon("seeds_rare", "crops", "Редкие семена")
def _():
    return seed_bag(P["purple"], facet(star_pts(32, 38, 8, 3.5), P["yellow"], center=(32, 38)))


@icon("soil", "crops", "Грядка / Вспахать")
def _():
    top = [(32, 18), (58, 32), (32, 46), (6, 32)]
    return facet([(6, 32), (32, 46), (32, 54), (6, 40)], P["wood_dk"]) + facet([(32, 46), (58, 32), (58, 40), (32, 54)], P["soil"]) + \
        facet(top, shade(P["soil"], 0.12)) + "".join(stick(14 + i * 8, 28 + i * 4 - 6, 28 + i * 8, 36 + i * 4 - 6, 2, P["wood_dk"]) for i in range(3))


@icon("water_drop", "crops", "Полито / Влага")
def _():
    return facet([(32, 6), (46, 30), (48, 40), (42, 52), (32, 56), (22, 52), (16, 40), (18, 30)], P["sky"], center=(28, 36)) + \
        facet([(24, 38), (28, 36), (28, 46)], P["white"])


@icon("growth", "crops", "Рост / Таймер")
def _():
    return facet([(14, 52), (50, 52), (46, 60), (18, 60)], P["soil"]) + stick(32, 52, 32, 28, 4, P["grass_dk"]) + \
        facet([(32, 36), (14, 26), (20, 18), (30, 28)], P["leaf"]) + facet([(32, 30), (50, 18), (46, 10), (34, 22)], P["grass"]) + \
        facet(ngon(50, 44, 8, 8), P["cream"]) + stick(50, 44, 50, 39, 2, P["ink"]) + stick(50, 44, 54, 44, 2, P["ink"])


# ───────────────────────── ЖИВОТНЫЕ / ПРОДУКТЫ ─────────────────────────

@icon("egg", "animals", "Яйцо")
def _():
    return facet(ngon(32, 34, 22, 11, sx=0.78, wobble=0.04), "#EBD3AA", center=(28, 28))


@icon("eggs_basket", "animals", "Корзина яиц")
def _():
    return facet(ngon(22, 30, 10, 8, sx=0.8), "#EBD3AA") + facet(ngon(36, 28, 10, 8, sx=0.8), "#F6E6CC") + facet(ngon(46, 32, 9, 8, sx=0.8), "#D9B98A") + \
        facet([(6, 34), (58, 34), (52, 56), (12, 56)], P["wood"]) + "".join(flat(rect_pts(12, 40 + 6 * i, 40, 2), P["wood_dk"]) for i in range(2))


@icon("milk", "animals", "Молоко")
def _():
    return facet([(22, 8), (42, 8), (42, 16), (50, 26), (50, 56), (14, 56), (14, 26), (22, 16)], P["white"], center=(30, 30)) + \
        facet(rect_pts(20, 4, 24, 6), P["sky_dk"]) + facet(rect_pts(14, 32, 36, 14), P["sky"]) + \
        facet(ngon(32, 39, 5, 6), P["white"])


@icon("chicken", "animals", "Курица")
def _():
    body = [(14, 30), (24, 22), (40, 24), (50, 34), (46, 48), (30, 52), (16, 46)]
    return stick(26, 52, 24, 60, 3, P["orange"]) + stick(36, 52, 38, 60, 3, P["orange"]) + \
        facet(body, P["white"], center=(30, 36)) + facet([(6, 22), (16, 28), (14, 38), (6, 34)], P["cream_dk"]) + \
        facet(ngon(44, 22, 10, 8), P["white"]) + facet([(52, 20), (60, 23), (52, 26)], P["orange"]) + \
        facet([(40, 12), (44, 8), (48, 12), (46, 14)], P["red"]) + facet([(50, 27), (53, 31), (49, 31)], P["red"]) + flat(ngon(46, 20, 1.6, 5), P["ink"]) + \
        facet([(24, 34), (38, 32), (34, 42)], P["cream_dk"])


@icon("cow", "animals", "Корова")
def _():
    head = [(18, 18), (46, 18), (50, 34), (44, 50), (20, 50), (14, 34)]
    return facet([(6, 20), (18, 18), (16, 28)], P["white"]) + facet([(58, 20), (46, 18), (48, 28)], P["white"]) + \
        facet([(18, 18), (22, 8), (26, 18)], P["cream_dk"]) + facet([(38, 18), (42, 8), (46, 18)], P["cream_dk"]) + \
        facet(head, P["white"], center=(32, 30)) + facet([(18, 18), (30, 18), (24, 30), (16, 28)], P["ink"]) + \
        facet([(20, 40), (44, 40), (42, 52), (22, 52)], P["pink"]) + flat(ngon(27, 46, 2, 5), "#B05A70") + flat(ngon(37, 46, 2, 5), "#B05A70") + \
        flat(ngon(24, 32, 2.2, 5), P["ink"]) + flat(ngon(40, 32, 2.2, 5), P["ink"])


@icon("feed", "animals", "Корм")
def _():
    return facet([(10, 36), (54, 36), (48, 54), (16, 54)], P["wood"]) + \
        "".join(facet(ngon(16 + 6 * i, 34 - (i % 2) * 3, 5, 5), P["gold"] if i % 2 else P["gold_dk"]) for i in range(6))


@icon("hay", "animals", "Сено")
def _():
    front = [(8, 26), (40, 26), (40, 54), (8, 54)]
    return facet([(8, 26), (22, 14), (56, 14), (40, 26)], shade(P["gold"], 0.15)) + facet([(40, 26), (56, 14), (56, 42), (40, 54)], P["gold_dk"]) + \
        facet(front, P["gold"]) + stick(8, 36, 40, 36, 2.5, P["wood"]) + stick(40, 36, 56, 24, 2.5, P["wood"])


# ───────────────────────── ПОСТРОЙКИ / ДЕКОР ─────────────────────────

def house_base(wall, roof):
    return facet([(6, 30), (32, 8), (58, 30)], roof, center=(32, 22)) + facet(rect_pts(12, 30, 40, 26), wall)


@icon("b_house", "build", "Дом")
def _():
    return house_base(P["cream"], P["red"]) + facet(rect_pts(28, 40, 9, 16), P["wood"]) + facet(rect_pts(16, 36, 8, 8), P["sky"]) + facet(rect_pts(42, 36, 8, 8), P["sky"]) + \
        facet(rect_pts(42, 10, 6, 12), P["stone_dk"])


@icon("b_coop", "build", "Курятник")
def _():
    return house_base(P["wood_lt"], P["wood_dk"]) + facet([(26, 56), (26, 44), (32, 38), (38, 44), (38, 56)], P["ink"]) + stick(40, 58, 56, 46, 4, P["wood"]) + \
        facet(ngon(32, 30, 4, 7, sx=0.8), P["cream"])


@icon("b_storage", "build", "Склад")
def _():
    return facet([(4, 30), (14, 12), (50, 12), (60, 30)], P["grass_dk"]) + facet(rect_pts(8, 30, 48, 26), P["wood"]) + \
        facet(rect_pts(20, 34, 24, 22), P["wood_dk"]) + stick(20, 34, 44, 56, 3, P["wood_lt"]) + stick(44, 34, 20, 56, 3, P["wood_lt"])


@icon("b_market", "build", "Рынок")
def _():
    return ICONS["catalog"][2]()


@icon("b_chest", "build", "Сундук")
def _():
    return facet([(8, 28), (14, 14), (50, 14), (56, 28)], P["wood_lt"]) + facet(rect_pts(8, 28, 48, 26), P["wood"]) + \
        facet(rect_pts(8, 26, 48, 4), P["gold_dk"]) + facet(rect_pts(14, 14, 4, 40), P["gold_dk"]) + facet(rect_pts(46, 14, 4, 40), P["gold_dk"]) + \
        facet(rect_pts(28, 24, 8, 10), P["gold"])


@icon("b_fence", "build", "Забор")
def _():
    posts = "".join(facet([(x, 18), (x + 4, 12), (x + 8, 18), (x + 8, 56), (x, 56)], P["white"] if True else P["wood"]) for x in (6, 28, 50))
    return stick(4, 28, 60, 28, 5, P["cream_dk"]) + stick(4, 44, 60, 44, 5, P["cream_dk"]) + posts


@icon("b_path", "build", "Дорожка")
def _():
    s = ""
    for i in range(3):
        for j in range(3):
            x, y = 32 + (i - j) * 9, 18 + (i + j) * 5
            s += facet([(x, y - 5), (x + 9, y), (x, y + 5), (x - 9, y)], P["stone"] if (i + j) % 2 else shade(P["stone"], 0.12))
    return s


@icon("b_lantern", "build", "Фонарь")
def _():
    return stick(32, 58, 32, 26, 4, P["wood_dk"]) + facet([(22, 14), (32, 6), (42, 14)], P["wood_dk"]) + facet(rect_pts(22, 14, 20, 16), P["orange"]) + \
        facet(rect_pts(26, 17, 12, 10), P["yellow"]) + facet(rect_pts(26, 56, 12, 4), P["wood_dk"])


@icon("b_flowerbed", "build", "Клумба")
def _():
    s = facet([(8, 40), (56, 40), (52, 56), (12, 56)], P["wood"])
    for x, y, c in [(18, 30, P["pink"]), (32, 26, P["yellow"]), (46, 30, P["purple"]), (25, 36, P["red"]), (40, 36, P["white"])]:
        s += stick(x, y, x, 42, 2, P["grass_dk"]) + facet(star_pts(x, y, 6, 3, n=5), c, center=(x, y)) + flat(ngon(x, y, 1.6, 5), P["gold"])
    return s


@icon("b_well", "build", "Колодец")
def _():
    return stick(16, 40, 16, 14, 4, P["wood"]) + stick(48, 40, 48, 14, 4, P["wood"]) + facet([(8, 18), (32, 4), (56, 18)], P["red"], center=(32, 13)) + \
        facet(ngon(32, 42, 20, 10, sy=0.35), P["stone_dk"]) + facet(rect_pts(12, 42, 40, 14), P["stone"]) + facet(ngon(32, 42, 15, 10, sy=0.3), P["water_dk"])


@icon("b_tree", "build", "Дерево (декор)")
def _():
    return stick(32, 58, 32, 36, 6, P["wood"]) + blob(32, 26, 22, n=11, seed=4, color=P["leaf"])


@icon("b_rock", "build", "Камень (убрать)")
def _():
    return facet([(8, 50), (14, 32), (28, 20), (44, 22), (56, 38), (54, 52), (30, 56)], P["stone"], center=(30, 34))


@icon("b_bench", "build", "Лавочка")
def _():
    return stick(10, 34, 54, 34, 6, P["wood_lt"]) + stick(10, 22, 54, 22, 5, P["wood"]) + stick(14, 36, 14, 52, 4, P["wood_dk"]) + \
        stick(50, 36, 50, 52, 4, P["wood_dk"]) + stick(14, 22, 14, 34, 3, P["wood_dk"]) + stick(50, 22, 50, 34, 3, P["wood_dk"])


@icon("b_sign", "build", "Табличка фермы")
def _():
    return stick(32, 58, 32, 30, 5, P["wood_dk"]) + facet(rect_pts(8, 12, 48, 22), P["wood_lt"]) + \
        stick(14, 20, 50, 20, 2, P["wood"]) + stick(14, 27, 40, 27, 2, P["wood"])


@icon("b_sector", "build", "Новый участок")
def _():
    s = ""
    for i in range(2):
        for j in range(2):
            x, y = 32 + (i - j) * 13, 20 + (i + j) * 8
            s += facet([(x, y - 8), (x + 13, y), (x, y + 8), (x - 13, y)], P["grass"] if (i, j) != (1, 1) else P["cream_dk"])
    return s + facet(ngon(48, 46, 10, 10), P["gold"]) + facet(rect_pts(46.5, 40, 3, 12), P["white"]) + facet(rect_pts(42, 44.5, 12, 3), P["white"])


@icon("b_demolish", "build", "Снести")
def _():
    return ICONS["b_house"][2]() + facet(ngon(46, 46, 14, 10), P["red"]) + cross(P["white"], cx=46, cy=46, s=0.33)
