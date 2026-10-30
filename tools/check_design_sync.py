"""Сверка DESIGN.json с tokens.json главного источника — дизайн-системы claude.ai «Chrono Business - Apple Design».

Только стандартная библиотека Python. Запуск из корня репозитория:
    python -I tools/check_design_sync.py <путь к скачанному tokens.json> [путь к DESIGN.json]
Источник правды — артефакт https://claude.ai/artifact/KU2VGZWGkhx5R3RTLhRDtK (файл project/tokens.json); DESIGN.json — его копия
плюс поля, которых в формате артефакта нет (tabular, dynamicType, maxSize, font.android, viewport, appearances).
Сверка идёт по смыслу, а не по тексту:
  - имена: kebab ↔ camel (secondary-label ↔ secondaryLabel, gray-2 ↔ gray2, space-1-5 ↔ space1_5, size-wheel ↔ size.wheel);
  - темы: light / dark / light-hc / dark-hc ↔ light / dark / lightHighContrast / darkHighContrast;
  - ссылки {имя} (и {имя}@0.16) раскрываются до цвета, так что tint = {blue} и tint = {orange} сравниваются как #0088FF и #FF8D28;
  - px и em отбрасываются, rgba(255,255,255,1) = #FFFFFF, одно значение на все темы разворачивается в четыре.
Код возврата 1 — есть расхождение значений или имя только с одной стороны (кроме производных цветов, см. DERIVED_ONLY_IN_JSON).
"""
import json
import os
import re
import sys

sys.stdout.reconfigure(encoding="utf-8")

# Темы DESIGN.json → темы артефакта.
THEMES = {"light": "light", "dark": "dark", "lightHighContrast": "light-hc", "darkHighContrast": "dark-hc"}
# Цвета, которые в артефакте получаются в CSS через color-mix, а в DESIGN.json записаны отдельными токенами: расхождением не считаются.
DERIVED_ONLY_IN_JSON = {"tintfill", "destructivefill"}
# Допуск при сравнении числа с плавающей точкой (трекинг, прозрачность).
EPS = 0.0005

problems = 0


def canon(name):
    """Общий вид имени: без дефисов и подчёркиваний, строчными (kebab и camel сходятся)."""
    return re.sub(r"[-_]", "", name).lower()


def num(text):
    """'216px' → 216.0, '0.012em' → 0.012, '0.2s' → 0.2; число остаётся числом."""
    if isinstance(text, (int, float)):
        return float(text)
    return float(re.sub(r"(px|em|s)$", "", text.strip()))


def report(title, lines, count=None):
    """Печатает раздел и прибавляет к счётчику расхождений (count — если одна строка описывает несколько)."""
    global problems
    problems += len(lines) if count is None else count
    print(f"{title}: {'расхождений нет' if not lines else (len(lines) if count is None else count)}")
    for line in lines:
        print("  " + line)


# ---------- цвета ----------


def parse_color(text):
    """Цвет → (r, g, b, a) или None, если формат не разобран."""
    text = text.strip().lower().replace(" ", "")
    m = re.fullmatch(r"#([0-9a-f]{3}|[0-9a-f]{6}|[0-9a-f]{8})", text)
    if m:
        h = m[1]
        if len(h) == 3:
            h = "".join(c * 2 for c in h)
        return int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), (int(h[6:8], 16) / 255 if len(h) == 8 else 1.0)
    m = re.fullmatch(r"rgba?\((\d+),(\d+),(\d+)(?:,([0-9.]+))?\)", text)
    if m:
        return int(m[1]), int(m[2]), int(m[3]), float(m[4] if m[4] is not None else 1)
    return None


def resolve(name, theme, tokens, depth=0):
    """Раскрывает ссылки {имя}[@альфа] до цвета (r, g, b, a); не разобранное значение возвращает строкой."""
    if depth > 8:
        return f"цикл ссылок: {name}"
    value = tokens[name][theme]
    m = re.fullmatch(r"\{([\w-]+)\}(?:@([0-9.]+))?", value.strip())
    if m:
        if canon(m[1]) not in tokens:
            return f"нет токена {m[1]}"
        inner = resolve(canon(m[1]), theme, tokens, depth + 1)
        if isinstance(inner, str):
            return inner
        r, g, b, a = inner
        return r, g, b, a * float(m[2] or 1)
    return parse_color(value) or value


def show(color, raw):
    """Цвет для вывода: #RRGGBB (или с @альфой) и, если значение было ссылкой, сама ссылка."""
    if isinstance(color, str):
        text = color
    else:
        r, g, b, a = color
        text = "#%02X%02X%02X" % (r, g, b) + ("" if abs(a - 1) < EPS else f"@{a:.2f}")
    return text if raw.strip().startswith("#") or raw.strip().startswith("rgb") else f"{raw} = {text}"


def same_color(a, b):
    if isinstance(a, str) or isinstance(b, str):
        return a == b
    return all(abs(x - y) <= 1 for x, y in zip(a[:3], b[:3])) and abs(a[3] - b[3]) <= EPS


def color_tokens(art, design):
    """{canon: (имя, {тема артефакта: значение})} для обеих сторон."""
    themes = [t["id"] for t in art["color"]["themes"]]
    left = {}
    for t in art["color"]["tokens"]:
        v = t["value"]
        left[canon(t["name"])] = (t["name"], v if isinstance(v, dict) else {th: v for th in themes})
    right = {}
    for k, v in design["color"].items():
        right[canon(k)] = (k, {THEMES[a]: x for a, x in v.items()} if isinstance(v, dict) else {th: v for th in THEMES.values()})
    return left, right


def check_colors(art, design):
    left, right = color_tokens(art, design)
    lv = {k: v[1] for k, v in left.items()}
    rv = {k: v[1] for k, v in right.items()}
    lines = []
    for key in left:
        if key not in right:
            continue
        groups = {}
        for theme in THEMES.values():
            a, b = resolve(key, theme, lv), resolve(key, theme, rv)
            if not same_color(a, b):
                text = f"артефакт {show(a, lv[key][theme])}; DESIGN.json {show(b, rv[key][theme])}"
                groups.setdefault(text, []).append(theme)
        for text, themes in groups.items():
            where = "все темы" if len(themes) == 4 else ", ".join(themes)
            lines.append(f"{left[key][0]} / {right[key][0]} [{where}]: {text}")
    report("Цвета (значения)", lines)
    names("Цвета", left, right, DERIVED_ONLY_IN_JSON)


def names(title, left, right, allowed=()):
    """Имена, которые есть только с одной стороны."""
    only_art = [left[k][0] for k in left if k not in right]
    only_json = [right[k][0] for k in right if k not in left and k not in allowed]
    derived = [right[k][0] for k in right if k in allowed and k not in left]
    # Совпадающие наборы имён молчат: строка появляется только при расхождении.
    if only_art:
        report(f"{title}, имена только в артефакте", [", ".join(only_art)], len(only_art))
    if only_json:
        report(f"{title}, имена только в DESIGN.json", [", ".join(only_json)], len(only_json))
    if derived:
        print(f"  (производные, не считаются: {', '.join(derived)})")


# ---------- числа ----------


def check_numbers(title, art_tokens, design_group, strip_prefix=""):
    """Отступы, радиусы, размеры: '216px' против 216."""
    left = {canon(t["name"][len(strip_prefix):] if t["name"].startswith(strip_prefix) else t["name"]): (t["name"], num(t["value"])) for t in art_tokens}
    right = {canon(k): (k, float(v)) for k, v in design_group.items()}
    lines = [f"{left[k][0]} / {right[k][0]}: артефакт {left[k][1]:g}, DESIGN.json {right[k][1]:g}"
             for k in left if k in right and abs(left[k][1] - right[k][1]) > EPS]
    report(f"{title} (значения)", lines)
    names(title, left, right)


# ---------- типографика ----------


def check_type(art, design):
    left = {}
    for group in art["type"]["groups"]:
        for s in group["styles"]:
            # У caption-1 нулевой трекинг в артефакте не записан — letterSpacing отсутствует.
            left[canon(s["name"])] = (s["name"], (num(s["fontSize"]), num(s["lineHeight"]), float(s["fontWeight"]), num(s.get("letterSpacing", "0em"))))
    right = {canon(k): (k, (float(s["size"]), float(s["lineHeight"]), float(s["weight"]), float(s["tracking"]))) for k, s in design["type"].items()}
    fields = ("кегль", "интерлиньяж", "вес", "трекинг")
    lines = []
    for k in left:
        if k in right:
            diff = [f"{f}: артефакт {a:g}, DESIGN.json {b:g}" for f, a, b in zip(fields, left[k][1], right[k][1]) if abs(a - b) > EPS]
            if diff:
                lines.append(f"{left[k][0]} / {right[k][0]}: " + "; ".join(diff))
    report("Типографика (значения)", lines)
    names("Типографика", left, right)


# ---------- тени, материалы, движение ----------


def flat(text):
    """Строка тени или кривой без px и пробелов: '0 8px 24px rgba(0,0,0,0.10)' → '0824rgba(0,0,0,0.10)'."""
    return re.sub(r"px|\s+", "", text.lower())


def check_shadows(art, design):
    themes = [t["id"] for t in art["color"]["themes"]]
    left = {canon(t["name"]): (t["name"], t["value"] if isinstance(t["value"], dict) else {th: t["value"] for th in themes}) for t in art["shadow"]["tokens"]}
    right = {canon(k): (k, {THEMES[a]: x for a, x in v.items()} if isinstance(v, dict) else {th: v for th in THEMES.values()}) for k, v in design["shadow"].items()}
    lines = [f"{left[k][0]} / {right[k][0]} [{th}]: артефакт «{left[k][1][th]}», DESIGN.json «{right[k][1][th]}»"
             for k in left if k in right for th in THEMES.values() if flat(left[k][1][th]) != flat(right[k][1][th])]
    report("Тени (значения)", lines)
    names("Тени", left, right)


def check_material(art, design):
    # В артефакте материал — строка 'blur(24px) saturate(180%)', в DESIGN.json — радиус размытия и отдельный glassSaturate.
    mat = design["material"]
    left = {canon(t["name"]): (t["name"], t["value"]) for t in art["material"]["tokens"]}
    right = {canon(k): (k, v) for k, v in mat.items() if k not in ("note", "glassSaturate")}
    lines = []
    for k in left:
        if k not in right:
            continue
        blur = re.search(r"blur\(([\d.]+)px\)", left[k][1])
        if not blur or abs(float(blur[1]) - float(right[k][1])) > EPS:
            lines.append(f"{left[k][0]} / {right[k][0]}: артефакт «{left[k][1]}», DESIGN.json blur {right[k][1]}")
        saturate = re.search(r"saturate\(([\d.]+)%\)", left[k][1])
        # Насыщенность в DESIGN.json есть только у основного стекла (glassSaturate).
        if k == "glassblur" and (not saturate or abs(float(saturate[1]) / 100 - float(mat["glassSaturate"])) > EPS):
            lines.append(f"{left[k][0]}: артефакт «{left[k][1]}», DESIGN.json saturate {mat['glassSaturate']}")
    report("Материалы (значения)", lines)
    names("Материалы", left, right)


def check_motion(art, design):
    # duration-quick '0.2s' ↔ quick 200 (мс); easing-spring ↔ spring.
    left = {}
    for t in art["timing"]["tokens"]:
        key = canon(re.sub(r"^(duration|easing)-", "", t["name"]))
        left[key] = (t["name"], t["value"])
    right = {canon(k): (k, v) for k, v in design["motion"].items()}
    lines = []
    for k in left:
        if k not in right:
            continue
        a, b = left[k][1], right[k][1]
        same = abs(num(a) * 1000 - float(b)) <= EPS if isinstance(b, (int, float)) else flat(a) == flat(b)
        if not same:
            lines.append(f"{left[k][0]} / {right[k][0]}: артефакт «{a}», DESIGN.json «{b}»")
    report("Движение (значения)", lines)
    names("Движение", left, right)


def main():
    if len(sys.argv) < 2:
        sys.exit("Использование: python -I tools/check_design_sync.py <tokens.json артефакта> [DESIGN.json]")
    default_design = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "DESIGN.json")
    art = json.load(open(sys.argv[1], encoding="utf-8"))
    design = json.load(open(sys.argv[2] if len(sys.argv) > 2 else default_design, encoding="utf-8"))
    print(f"Артефакт «{art['name']}», версия tokens.json {art['version']}  ↔  DESIGN.json {design['id']} {design['version']}\n")
    check_colors(art, design)
    check_type(art, design)
    check_numbers("Отступы", art["spacing"]["tokens"], design["spacing"])
    check_numbers("Радиусы", art["radius"]["tokens"], design["radius"], "radius-")
    check_numbers("Размеры", art["size"]["tokens"], design["size"], "size-")
    check_shadows(art, design)
    check_material(art, design)
    check_motion(art, design)
    print(f"\nИтого расхождений: {problems}")
    sys.exit(1 if problems else 0)


main()
