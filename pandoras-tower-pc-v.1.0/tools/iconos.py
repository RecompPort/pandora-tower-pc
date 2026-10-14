"""Junta en un zip las texturas pequeñas volcadas, que es donde están los
iconos de los botones, para identificarlas y hacer los paquetes de iconos de
Xbox, PlayStation y teclado.

    python tools/iconos.py           las nuevas de build/texdump (./run.sh --volcar-texturas)
                                     desde el último build/iconos.zip
    python tools/iconos.py --todas   todas las de build/texdump
    python tools/iconos.py --raton   las de build/texdump-raton (./run.sh --volcar-iconos,
                                     jugando con ratón y teclado) que no estén ya en
                                     build/texdump -> build/iconos-raton.zip
"""
import os
import re
import sys
import zipfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
BUILD = os.path.join(ROOT, "build")
LIMIT = 24 << 20                                          # que quepa al subirlo
NAME = re.compile(r"(\d+)x(\d+)_f(\d+)_[0-9a-f]{16}\.png$")


def main():
    raton = "--raton" in sys.argv[1:]
    dump = os.path.join(BUILD, "texdump-raton" if raton else "texdump")
    out = os.path.join(BUILD, "iconos-raton.zip" if raton else "iconos.zip")
    if not os.path.isdir(dump):
        sys.exit("no hay " + os.path.relpath(dump, ROOT) + ": juega antes un rato con "
                 + ("./run.sh --volcar-iconos" if raton else "./run.sh --volcar-texturas"))
    since = 0.0
    if not raton and "--todas" not in sys.argv[1:] and os.path.exists(out):
        since = os.path.getmtime(out)                     # lo volcado después del último zip
    known = set()
    if raton:                                             # lo que ya salió jugando con mando
        classic = os.path.join(BUILD, "texdump")
        if os.path.isdir(classic):
            known = set(os.listdir(classic))
    found = []
    for n in os.listdir(dump):
        if n in known or os.path.getmtime(os.path.join(dump, n)) <= since:
            continue
        m = NAME.match(n)
        if m and max(int(m.group(1)), int(m.group(2))) <= 512:
            found.append((int(m.group(1)) * int(m.group(2)), n))
    found.sort()                                          # las más pequeñas primero
    if not found:
        # nada nuevo: el zip anterior se queda como está (no se cambia por uno vacío)
        if raton:
            print("no hay texturas nuevas en build/texdump-raton; ¿jugaste con ./run.sh --volcar-iconos?")
        else:
            print("no hay texturas nuevas desde el último build/iconos.zip; ¿jugaste con ./run.sh --volcar-texturas?")
            print("para juntar todas las volcadas: python tools/iconos.py --todas")
        return
    size = 0
    kept = 0
    with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
        for _, n in found:
            p = os.path.join(dump, n)
            s = os.path.getsize(p)
            if size + s > LIMIT:
                break
            z.write(p, n)
            size += s
            kept += 1
    what = "nuevas " if since or known else ""
    print(f"{kept} de {len(found)} texturas pequeñas {what}-> {out} ({size / 1e6:.1f} MB)")


if __name__ == "__main__":
    main()
