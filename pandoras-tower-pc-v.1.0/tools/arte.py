"""Saca de tu copia del juego el icono y el banner originales del menú de la
Wii (files/opening.bnr) para el lanzador. No hay nada de esto en el paquete:
se lee de tu disco extraído y se guarda en build/arte/.

    python tools/arte.py CARPETA_EXTRAIDA   (o la ISO del juego)

opening.bnr: una cabecera IMET y un archivo U8 con meta/icon.bin y
meta/banner.bin. Cada uno lleva una cabecera IMD5 y, comprimido en LZ77
(Nintendo LZ10/LZ11), otro archivo U8 con las texturas en arc/timg/*.tpl.

Deja en build/arte/:
    icon_*.png, banner_*.png   todas las texturas, para verlas
    icono.ico                  la textura más grande del icono, en cuadrado
    fondo.png                  la textura opaca más grande del banner
    logo.png                   la textura con transparencia más grande del banner
    arte.txt                   lo que se encontró y qué se eligió
"""
import os
import struct
import sys
import zlib

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from wiikit import gxtex, tpl, u8  # noqa: E402

U8_MAGIC = b"\x55\xAA\x38\x2D"


def lz_decompress(buf):
    """Nintendo LZ10 / LZ11, con o sin la etiqueta "LZ77" delante."""
    if buf[:4] == b"LZ77":
        buf = buf[4:]
    kind = buf[0]
    size = buf[1] | buf[2] << 8 | buf[3] << 16
    pos = 4
    if size == 0:                                          # tamaño en 32 bits aparte
        size = struct.unpack_from("<I", buf, 4)[0]
        pos = 8
    out = bytearray()
    while len(out) < size:
        flags = buf[pos]
        pos += 1
        for bit in range(8):
            if len(out) >= size:
                break
            if not flags & (0x80 >> bit):
                out.append(buf[pos])
                pos += 1
                continue
            b1 = buf[pos]
            if kind == 0x10:
                n = (b1 >> 4) + 3
                disp = ((b1 & 0xF) << 8 | buf[pos + 1]) + 1
                pos += 2
            else:                                          # LZ11
                hi = b1 >> 4
                if hi == 0:
                    n = (((b1 & 0xF) << 4) | (buf[pos + 1] >> 4)) + 0x11
                    disp = (((buf[pos + 1] & 0xF) << 8) | buf[pos + 2]) + 1
                    pos += 3
                elif hi == 1:
                    n = (((b1 & 0xF) << 12) | (buf[pos + 1] << 4) | (buf[pos + 2] >> 4)) + 0x111
                    disp = (((buf[pos + 2] & 0xF) << 8) | buf[pos + 3]) + 1
                    pos += 4
                else:
                    n = hi + 1
                    disp = (((b1 & 0xF) << 8) | buf[pos + 1]) + 1
                    pos += 2
            for _ in range(n):
                out.append(out[-disp])
    return bytes(out)


def unwrap(buf):
    """icon.bin / banner.bin: quita IMD5 y LZ77 hasta llegar al archivo U8."""
    if buf[:4] == b"IMD5":
        buf = buf[0x20:]
    if buf[:4] == b"LZ77" or buf[:1] in (b"\x10", b"\x11"):
        buf = lz_decompress(buf)
    if buf[:4] != U8_MAGIC:
        raise ValueError("no es un archivo U8 tras quitar IMD5/LZ77")
    return buf


def textures(arc):
    """[(nombre, ancho, alto, rgba)] de cada imagen de cada .tpl del archivo U8."""
    out = []
    for path, off, size in u8.parse(arc):
        if not path.lower().endswith(".tpl"):
            continue
        data = arc[off:off + size]
        for i, w, h, fmt, pal, doff in tpl.images(data):
            if fmt not in gxtex.BLOCK:
                continue                                  # paletas: no se usan aquí
            name = os.path.splitext(os.path.basename(path))[0] + (f"_{i}" if i else "")
            out.append((name, w, h, gxtex.decode(fmt, w, h, data[doff:])))
    return out


def transparency(w, h, rgba):
    """Fracción de píxeles no opacos."""
    return sum(1 for i in range(3, w * h * 4, 4) if rgba[i] < 250) / float(w * h)


def resize(w, h, rgba, nw, nh):
    """Cambio de tamaño por promedio de área, con el color ponderado por la opacidad."""
    out = bytearray(nw * nh * 4)
    for y in range(nh):
        y0, y1 = y * h / nh, (y + 1) * h / nh
        for x in range(nw):
            x0, x1 = x * w / nw, (x + 1) * w / nw
            r = g = b = a = wt = 0.0
            for sy in range(int(y0), min(h, int(y1) + (y1 > int(y1)))):
                fy = min(y1, sy + 1) - max(y0, sy)
                if fy <= 0:
                    continue
                for sx in range(int(x0), min(w, int(x1) + (x1 > int(x1)))):
                    fx = min(x1, sx + 1) - max(x0, sx)
                    if fx <= 0:
                        continue
                    f = fx * fy
                    i = (sy * w + sx) * 4
                    al = rgba[i + 3] / 255.0
                    r += rgba[i] * al * f
                    g += rgba[i + 1] * al * f
                    b += rgba[i + 2] * al * f
                    a += al * f
                    wt += f
            o = (y * nw + x) * 4
            if a > 0:
                out[o] = min(255, int(r / a + 0.5))
                out[o + 1] = min(255, int(g / a + 0.5))
                out[o + 2] = min(255, int(b / a + 0.5))
            out[o + 3] = min(255, int(a / wt * 255 + 0.5)) if wt else 0
    return out


def square(w, h, rgba):
    """Centra la imagen en un cuadrado transparente."""
    s = max(w, h)
    out = bytearray(s * s * 4)
    ox, oy = (s - w) // 2, (s - h) // 2
    for y in range(h):
        d = ((oy + y) * s + ox) * 4
        out[d:d + w * 4] = rgba[y * w * 4:(y + 1) * w * 4]
    return s, out


def png_bytes(w, h, rgba):
    raw = bytearray()
    for y in range(h):
        raw.append(0)
        raw += rgba[y * w * 4:(y + 1) * w * 4]

    def chunk(t, d):
        return struct.pack(">I", len(d)) + t + d + struct.pack(">I", zlib.crc32(t + d) & 0xFFFFFFFF)
    return (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0)) +
            chunk(b"IDAT", zlib.compress(bytes(raw), 9)) + chunk(b"IEND", b""))


def write_ico(path, w, h, rgba):
    """Icono de Windows con imágenes PNG de 256, 64, 48, 32 y 16 píxeles."""
    s, sq = square(w, h, rgba)
    imgs = [png_bytes(n, n, resize(s, s, sq, n, n)) for n in (256, 64, 48, 32, 16)]
    head = struct.pack("<HHH", 0, 1, len(imgs))
    off = 6 + 16 * len(imgs)
    ents = b""
    for n, data in zip((256, 64, 48, 32, 16), imgs):
        ents += struct.pack("<BBBBHHII", n % 256, n % 256, 0, 0, 1, 32, len(data), off)
        off += len(data)
    open(path, "wb").write(head + ents + b"".join(imgs))


def main():
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    ex = sys.argv[1]
    bnr_path = os.path.join(ex, "files", "opening.bnr")
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    out = os.path.join(root, "build", "arte")
    os.makedirs(out, exist_ok=True)
    report = []
    if os.path.isfile(ex):                                 # una ISO: opening.bnr leído de ella
        from wiikit import disc
        _, p = disc.open_partition(ex, "DATA", False)
        bnr = next((p.read(o, n) for rel, o, n in p.files() if rel.lower() == "opening.bnr"), None)
        if bnr is None:
            sys.exit("la imagen no tiene opening.bnr")
    else:
        bnr = open(bnr_path, "rb").read()
    start = bnr.find(U8_MAGIC)
    if start < 0:
        sys.exit("opening.bnr sin archivo U8")
    files = {os.path.basename(p).lower(): bnr[start + o:start + o + n] for p, o, n in u8.parse(bnr[start:])}
    chosen = {}
    for part in ("icon", "banner"):
        blob = files.get(part + ".bin")
        if blob is None:
            report.append(f"{part}.bin: no está")
            continue
        texs = textures(unwrap(blob))
        report.append(f"{part}.bin: {len(texs)} texturas")
        for name, w, h, rgba in texs:
            t = transparency(w, h, rgba)
            fn = f"{part}_{name}.png"
            open(os.path.join(out, fn), "wb").write(png_bytes(w, h, rgba))
            report.append(f"  {fn:40s} {w:4d} x {h:<4d} transparente {t:4.0%}")
        if part == "icon" and texs:
            name, w, h, rgba = max(texs, key=lambda t: t[1] * t[2])
            write_ico(os.path.join(out, "icono.ico"), w, h, rgba)
            chosen["icono.ico"] = name
        if part == "banner" and texs:
            scored = [(t[1] * t[2], transparency(t[1], t[2], t[3]), t) for t in texs]
            opaque = [s for s in scored if s[1] < 0.05]
            alpha = [s for s in scored if s[1] >= 0.2]
            if opaque:
                a, t, (name, w, h, rgba) = max(opaque, key=lambda s: s[0])
                open(os.path.join(out, "fondo.png"), "wb").write(png_bytes(w, h, rgba))
                chosen["fondo.png"] = name
            if alpha:
                a, t, (name, w, h, rgba) = max(alpha, key=lambda s: s[0])
                open(os.path.join(out, "logo.png"), "wb").write(png_bytes(w, h, rgba))
                chosen["logo.png"] = name
    report.append("elegidas: " + ", ".join(f"{k} <- {v}" for k, v in chosen.items()))
    open(os.path.join(out, "arte.txt"), "w", encoding="utf-8").write("\n".join(report) + "\n")
    print("\n".join(report))


if __name__ == "__main__":
    main()
