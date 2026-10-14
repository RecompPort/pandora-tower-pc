"""Saca de la imagen del disco solo lo que necesita la recompilación:
sys/main.dol y sys/boot.bin (el juego, al jugar, lee la imagen directamente).

    python tools/dol.py JUEGO.iso CARPETA
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from wiikit import disc  # noqa: E402


def main():
    if len(sys.argv) != 3:
        sys.exit(__doc__)
    image, out = sys.argv[1:3]
    _, p = disc.open_partition(image, "DATA", False)
    b = p.boot()
    os.makedirs(os.path.join(out, "sys"), exist_ok=True)
    with open(os.path.join(out, "sys", "boot.bin"), "wb") as f:
        f.write(b["raw"])
    with open(os.path.join(out, "sys", "main.dol"), "wb") as f:
        f.write(p.read(b["dol"], p.dol_size(b["dol"])))
    print(f"{b['raw'][:6].decode('ascii', 'replace')}: sys/main.dol y sys/boot.bin -> {out}")


if __name__ == "__main__":
    main()
