import os, subprocess, sys
import build_icons
from screens import SCREENS, ROOT

build_icons.write_icons()
open(os.path.join(ROOT, "00_all_icons_sheet.svg"), "w").write(build_icons.sheet())
sd = os.path.join(ROOT, "screens")
os.makedirs(sd, exist_ok=True)
paths = []
for fn in SCREENS:
    svg = fn()
    name = svg.split('<g id="', 1)[1].split('"', 1)[0]
    p = os.path.join(sd, name + ".svg")
    open(p, "w").write(svg)
    paths.append(p)
print("\n".join(paths))
