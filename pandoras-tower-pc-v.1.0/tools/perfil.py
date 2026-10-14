"""Pone nombre a las muestras del perfilador de wiiboot (WIIKIT_PROFILE=1),
también por tramos, para ver qué cambia cuando el juego va lento.

    python tools/perfil.py build/recomp-build/wiiboot.exe build/run.err build/recomp/symbols.tsv

El perfilador escribe cada 10 segundos el recuento acumulado de muestras
("profile-raw: DIRECCIÓN CUENTA [DLL]") y el renderizador, cada segundo, una
línea "perf:" con los fps. Este script resta cada recuento del anterior para
obtener lo que pasó en cada tramo de 10 s, y lo pone junto a los fps de ese
tramo. Usa `nm`, o `llvm-nm` si no lo hay (en MSYS2 CLANG64 puede faltar).
"""
import bisect
import collections
import re
import shutil
import subprocess
import sys


def load_host_symbols(exe):
    nm = shutil.which("nm") or shutil.which("llvm-nm")
    if not nm:
        sys.exit("no hay nm ni llvm-nm: instala mingw-w64-clang-x86_64-llvm con pacman")
    out = subprocess.run([nm, "-C", "--defined-only", exe], capture_output=True, text=True).stdout
    syms = []
    for line in out.splitlines():
        p = line.split(" ", 2)
        if len(p) == 3 and p[1] in "tT":
            try:
                syms.append((int(p[0], 16), p[2]))
            except ValueError:
                pass
    syms.sort()
    return [a for a, _ in syms], syms


def load_guest_names(tsv):
    guest = {}
    for line in open(tsv, encoding="utf-8"):
        p = line.rstrip("\n").split("\t")
        if len(p) >= 7 and p[0] != "addr":
            guest.setdefault(int(p[0], 16), p[6])
    return guest


def parse_log(path):
    """[(fps de ese tramo, {(dirección, dll): cuenta acumulada})], en orden."""
    blocks, fps, cur = [], [], None
    for line in open(path, encoding="utf-8", errors="replace"):
        line = line.rstrip("\r\n")
        if line.startswith("perf:"):
            m = re.match(r"perf: ([0-9.]+) fps", line)
            if m:
                fps.append(float(m.group(1)))
        elif line.startswith("profile:"):
            cur = {}
            blocks.append((fps, cur))
            fps = []
        elif line.startswith("profile-raw:") and cur is not None:
            f = line.split()
            key = (int(f[1], 16), f[3] if len(f) > 3 else "")
            cur[key] = cur.get(key, 0) + int(f[2])
    return blocks


def main():
    if len(sys.argv) < 4:
        sys.exit(__doc__)
    exe, log, tsv = sys.argv[1:4]
    addrs, syms = load_host_symbols(exe)
    guest = load_guest_names(tsv)
    blocks = parse_log(log)
    if not blocks:
        sys.exit("no hay líneas profile-raw en el registro (¿se lanzó con ./perfil.sh?)")

    def name(a, dll):
        i = bisect.bisect_right(addrs, a) - 1
        n = syms[i][1] if i >= 0 else "?"
        if n.startswith("f_") and "(" in n:
            g = int(n[2:10], 16)
            n = f"{guest.get(g, n)}  [guest {g:08X}]"
        return n + (f"  -> in {dll}" if dll else "")

    def top(counts, k):
        per = collections.Counter()
        for (a, dll), n in counts.items():
            if n > 0:
                per[name(a, dll)] += n
        total = sum(per.values())
        return total, ([(100.0 * n / total, nm) for nm, n in per.most_common(k)] if total else [])

    total, rows = top(blocks[-1][1], 40)
    print(f"== Todo el recorrido ({total} muestras)")
    for pct, nm in rows:
        print(f"{pct:5.1f}%  {nm}")

    print("\n== Por tramos de 10 s (fps medio, mínimo y máximo del tramo)")
    prev = {}
    for k, (fps, raw) in enumerate(blocks, 1):
        delta = {key: n - prev.get(key, 0) for key, n in raw.items()}
        prev = raw
        t, rows = top(delta, 8)
        f = (f"{sum(fps) / len(fps):5.1f} fps (mín {min(fps):.1f}, máx {max(fps):.1f})" if fps
             else "  sin datos de fps")
        print(f"\n-- tramo {k}: {f}, {t} muestras")
        for pct, nm in rows:
            print(f"   {pct:5.1f}%  {nm}")


if __name__ == "__main__":
    main()
