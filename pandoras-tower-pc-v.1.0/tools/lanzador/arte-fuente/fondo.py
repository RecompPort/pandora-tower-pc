# Fondo original del lanzador: una torre gótica encadenada sobre un abismo, al anochecer.
# Todo se genera con código (ruido, polígonos, curvas): ningún material del juego.
import numpy as np, math, random
from PIL import Image, ImageDraw, ImageFilter

W, H = 1920, 1080
rng = np.random.default_rng(13)
random.seed(13)

def fbm(w, h, octaves=6, base=4, seed=0):
    r = np.random.default_rng(seed)
    out = np.zeros((h, w), np.float32); amp = 1.0; tot = 0
    for o in range(octaves):
        n = base * 2 ** o
        g = r.random((n + 1, int(n * w / h) + 2)).astype(np.float32)
        im = Image.fromarray((g * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC)
        out += amp * (np.asarray(im, np.float32) / 255); tot += amp; amp *= 0.5
    return out / tot

yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
u, v = xx / W, yy / H

# ---- cielo: de un violeta pardo arriba a un ámbar apagado en el horizonte
top = np.array([14, 10, 16], np.float32); mid = np.array([58, 34, 34], np.float32); hor = np.array([176, 118, 62], np.float32)
t = np.clip(v / 0.72, 0, 1)[..., None]
sky = np.where(t < 0.55, top + (mid - top) * (t / 0.55), mid + (hor - mid) * ((t - 0.55) / 0.45))
# luna grande, alta a la derecha, con halo
mx, my, mr = 0.70 * W, 0.24 * H, 0.085 * H
d = np.sqrt((xx - mx) ** 2 + (yy - my) ** 2)
halo = np.exp(-((d - mr).clip(0)) / (0.22 * H))[..., None]
sky = sky + halo * np.array([120, 92, 60], np.float32) * 0.55
moon = np.clip((mr - d) / 2.0, 0, 1)[..., None]
mtex = fbm(W, H, 5, 6, seed=3)[..., None]
moon_col = np.array([236, 222, 190], np.float32) * (0.86 + 0.18 * mtex)
sky = sky * (1 - moon) + moon_col * moon
# nubes: ruido fractal en bandas, iluminadas hacia la luna
c1 = fbm(W, H, 6, 3, seed=7); c2 = fbm(W, H, 5, 5, seed=11)
cl = np.clip((c1 * 0.7 + c2 * 0.3 - 0.44) * 3.6, 0, 1)
band = np.exp(-((v - 0.30) / 0.20) ** 2) + 0.6 * np.exp(-((v - 0.58) / 0.10) ** 2)
band = band * (0.55 + 0.45 * np.clip(np.abs(yy - (my + 0.35 * mr)) / (0.6 * mr), 0, 1))
cl = (cl * band)[..., None]
lit = np.exp(-d / (0.45 * H))[..., None]
cloud_col = np.array([46, 30, 30], np.float32) * (1 - lit) + np.array([170, 128, 86], np.float32) * lit
shift = np.roll(np.roll(cl, 6, axis=0), -8, axis=1)
rimc = np.clip(cl - shift, 0, 1) * lit * 2.5
sky = sky * (1 - 0.9 * cl) + cloud_col * 0.9 * cl + rimc * np.array([230, 190, 140], np.float32)
img = sky

def comp(img, layer_rgba):
    a = layer_rgba[..., 3:4] / 255.0
    return img * (1 - a) + layer_rgba[..., :3] * a

# ---- montañas lejanas y bruma
def ridge(y0, amp, seed, col, blur=0):
    r = np.random.default_rng(seed)
    pts = np.cumsum(r.normal(0, 1, W // 8 + 2)); pts = (pts - pts.min()) / (np.ptp(pts) + 1e-6)
    ys = y0 - amp * np.interp(np.arange(W), np.arange(len(pts)) * 8, pts)
    m = (yy >= ys[None, :]).astype(np.float32)
    if blur: m = np.asarray(Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(blur)), np.float32) / 255
    lay = np.zeros((H, W, 4), np.float32); lay[..., :3] = col; lay[..., 3] = m * 255
    return lay
img = comp(img, ridge(0.70 * H, 0.10 * H, 21, np.array([70, 44, 40]), 2))
mist = fbm(W, H, 5, 4, seed=5)
mistm = np.clip((mist - 0.35) * 1.6, 0, 1) * np.exp(-((v - 0.70) / 0.08) ** 2)
img = img * (1 - 0.55 * mistm[..., None]) + np.array([150, 104, 66], np.float32) * 0.55 * mistm[..., None]
img = comp(img, ridge(0.80 * H, 0.08 * H, 33, np.array([40, 26, 26]), 1))

# ---- la torre (a 3x, con antialias)
S = 3
tw, th = W * S, H * S
tower = Image.new('L', (tw, th), 0); td = ImageDraw.Draw(tower)
win = Image.new('L', (tw, th), 0); wd = ImageDraw.Draw(win)
far = Image.new('L', (tw, th), 0); fd = ImageDraw.Draw(far)
cx = 0.64 * W * S; base = 0.80 * H * S
def tier(cx, y_bot, y_top, w_bot, w_top, spikes=0):
    td.polygon([(cx - w_bot, y_bot), (cx - w_top, y_top), (cx + w_top, y_top), (cx + w_bot, y_bot)], fill=255)
    # almenas / pináculos en el borde superior
    for i in range(spikes):
        x = cx - w_top + (2 * w_top) * (i + 0.5) / spikes
        td.polygon([(x - w_top * 0.10, y_top), (x, y_top - w_top * 0.55), (x + w_top * 0.10, y_top)], fill=255)
def gothic_window(x, y, w, h, glow):
    pts = [(x - w, y + h), (x - w, y)]
    for a in np.linspace(math.pi, 0, 12):
        pts.append((x + w * math.cos(a) * 1.0, y - w * 1.4 * math.sin(a) * 0.9))
    pts += [(x + w, y), (x + w, y + h)]
    wd.polygon(pts, fill=int(255 * glow))
# cuerpo principal, en tramos que se estrechan
tiers = [(base, 0.62, 95, 82, 6), (0.62, 0.47, 80, 66, 5), (0.47, 0.35, 64, 52, 4), (0.35, 0.25, 50, 38, 4), (0.25, 0.18, 36, 26, 3)]
yb = base
for (_, ytop, wb, wt, sp) in tiers:
    y_top = ytop * H * S
    tier(cx, yb, y_top, wb * S, wt * S, sp)
    # una cornisa
    td.rectangle([cx - wt * S * 1.25, y_top, cx + wt * S * 1.25, y_top + 7 * S], fill=255)
    yb = y_top
# aguja central
td.polygon([(cx - 22 * S, yb), (cx, 0.055 * H * S), (cx + 22 * S, yb)], fill=255)
td.line([(cx, 0.055 * H * S), (cx, 0.03 * H * S)], fill=255, width=3 * S)
# la base: una masa de roca colgante, irregular, bajo la torre
rb = []
for i, a in enumerate(np.linspace(0, 1, 26)):
    x = cx + (a - 0.5) * 2 * 150 * S
    depth = (1 - abs(a - 0.5) * 2) ** 0.8
    rb.append((x + random.uniform(-6, 6) * S, base + (10 + 160 * depth ** 1.2 + random.uniform(-34, 30) * depth) * S))
td.polygon([(cx - 150 * S, base)] + rb + [(cx + 150 * S, base)], fill=255)
# contrafuertes a los lados
for sgn in (-1, 1):
    for (yt, yb2, off, wdt) in [(0.50, base, 120, 26), (0.40, 0.66, 98, 20), (0.30, 0.50, 78, 14)]:
        x0 = cx + sgn * off * S
        td.polygon([(x0 - wdt * S, yb2 * H * S if yb2 < 1 else yb2), (x0 - wdt * 0.6 * S, yt * H * S), (x0, yt * H * S - 50 * S),
                    (x0 + wdt * 0.6 * S, yt * H * S), (x0 + wdt * S, yb2 * H * S if yb2 < 1 else yb2)], fill=255)
        td.polygon([(x0, yt * H * S - 50 * S), (x0 - 6 * S, yt * H * S - 20 * S), (x0 + 6 * S, yt * H * S - 20 * S)], fill=255)
# torres menores, a lo lejos
for (x, y_top, wdt, hb) in [(0.42, 0.50, 18, 0.74), (0.86, 0.46, 22, 0.74), (0.94, 0.56, 13, 0.76), (0.34, 0.58, 12, 0.76)]:
    X = x * W * S
    fd.polygon([(X - wdt * S, hb * H * S), (X - wdt * 0.8 * S, y_top * H * S), (X + wdt * 0.8 * S, y_top * H * S), (X + wdt * S, hb * H * S)], fill=255)
    fd.polygon([(X - wdt * 0.9 * S, y_top * H * S), (X, (y_top - 0.07) * H * S), (X + wdt * 0.9 * S, y_top * H * S)], fill=255)
    for k in (-1, 1):
        fd.polygon([(X + k * wdt * 1.0 * S, (y_top + 0.05) * H * S), (X + k * wdt * 1.3 * S, (y_top + 0.12) * H * S), (X + k * wdt * 0.7 * S, (y_top + 0.12) * H * S)], fill=255)
# ventanas ojivales (algunas encendidas)
random.seed(4)
for (yt, wb) in [(0.70, 70), (0.56, 58), (0.43, 46), (0.31, 34), (0.22, 24)]:
    n = 3 if wb > 40 else 2
    for i in range(n):
        x = cx + (i - (n - 1) / 2) * wb * 0.7 * S
        gothic_window(x, yt * H * S, 7 * S, 22 * S, 1.0 if random.random() < 0.45 else 0.35)
tower = tower.resize((W, H), Image.LANCZOS); win = win.resize((W, H), Image.LANCZOS)
fm = np.asarray(far.resize((W, H), Image.LANCZOS), np.float32) / 255
haze = np.array([66, 44, 40], np.float32) * (0.9 + 0.2 * fbm(W, H, 4, 8, seed=23)[..., None])
img = img * (1 - fm[..., None]) + haze * fm[..., None]
fogf = np.clip((fbm(W, H, 5, 4, seed=31) - 0.3) * 1.5, 0, 1) * np.exp(-((v - 0.72) / 0.07) ** 2)
img = img * (1 - 0.6 * fogf[..., None]) + np.array([140, 96, 62], np.float32) * 0.6 * fogf[..., None]
tm = np.asarray(tower, np.float32) / 255; wm = np.asarray(win, np.float32) / 255
# color de la torre: piedra oscura, con luz de borde hacia la luna (a la derecha)
stone = fbm(W, H, 6, 12, seed=17)[..., None]
tcol = np.array([24, 17, 18], np.float32) * (0.8 + 0.4 * stone)
edge = tm - np.asarray(Image.fromarray((tm * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(3)).transform(
    (W, H), Image.AFFINE, (1, 0, -4, 0, 1, 3)), np.float32) / 255
rim = np.clip(edge, 0, 1)[..., None]
courses = (np.mod(yy, 14) < 1.5).astype(np.float32)[..., None]          # hiladas de piedra
pil = (np.abs(np.mod(xx - 0.64 * W, 46) - 23) < 1.2).astype(np.float32)[..., None]  # pilastras
tcol = tcol * (1 - 0.35 * courses) + pil * np.array([34, 26, 24], np.float32) * 0.6
side = np.clip((xx - 0.64 * W) / 140, 0, 1)[..., None]                   # el lado de la luna, más claro
tcol = tcol + side * np.array([22, 15, 12], np.float32)
tcol = tcol + rim * np.array([210, 160, 104], np.float32) * 1.3
img = img * (1 - tm[..., None]) + tcol * tm[..., None]
# ventanas: ámbar con un brillo alrededor
wlit = (wm > 0.6).astype(np.float32) * wm
wglow = np.asarray(Image.fromarray((wlit * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(10)), np.float32) / 255
img = img + wglow[..., None] * np.array([255, 150, 60], np.float32) * 0.9
img = img * (1 - wm[..., None] * tm[..., None]) + (wm * tm)[..., None] * (np.array([255, 190, 100], np.float32) * (wm > 0.6)[..., None] + np.array([40, 26, 20], np.float32) * (wm <= 0.6)[..., None])

# ---- cadenas: eslabones a lo largo de catenarias, desde los riscos a la torre
chain = Image.new('RGBA', (W * S, H * S), (0, 0, 0, 0)); cd = ImageDraw.Draw(chain)
def catenary(p0, p1, sag, n):
    pts = []
    for i in range(n + 1):
        tt = i / n
        x = p0[0] + (p1[0] - p0[0]) * tt; y = p0[1] + (p1[1] - p0[1]) * tt + sag * 4 * tt * (1 - tt)
        pts.append((x, y))
    return pts
def draw_chain(p0, p1, sag, link, thick):
    p0 = (p0[0] * S, p0[1] * S); p1 = (p1[0] * S, p1[1] * S)
    L = math.hypot(p1[0] - p0[0], p1[1] - p0[1]); n = max(8, int(L / (link * S * 0.82)))
    pts = catenary(p0, p1, sag * S, n)
    for i in range(n):
        (x0, y0), (x1, y1) = pts[i], pts[i + 1]
        ang = math.atan2(y1 - y0, x1 - x0); mxp, myp = (x0 + x1) / 2, (y0 + y1) / 2
        lw, lh = link * S * 0.62, link * S * 0.34
        if i % 2 == 0:   # eslabón de frente: un óvalo hueco
            poly = [(mxp + lw * math.cos(a) * math.cos(ang) - lh * math.sin(a) * math.sin(ang),
                     myp + lw * math.cos(a) * math.sin(ang) + lh * math.sin(a) * math.cos(ang)) for a in np.linspace(0, 2 * math.pi, 28)]
            cd.line(poly + [poly[0]], fill=(30, 22, 20, 255), width=int(thick * S))
            cd.line(poly[2:12], fill=(196, 150, 100, 230), width=max(1, int(thick * S * 0.35)))
        else:            # eslabón de canto: una barra
            cd.line([(mxp - lw * math.cos(ang), myp - lw * math.sin(ang)), (mxp + lw * math.cos(ang), myp + lw * math.sin(ang))],
                    fill=(30, 22, 20, 255), width=int(thick * S * 1.25))
            cd.line([(mxp - lw * 0.8 * math.cos(ang), myp - lw * 0.8 * math.sin(ang) - thick * S * 0.3),
                     (mxp + lw * 0.8 * math.cos(ang), myp + lw * 0.8 * math.sin(ang) - thick * S * 0.3)],
                    fill=(196, 150, 100, 200), width=max(1, int(thick * S * 0.35)))
tx = 0.64 * W
draw_chain((-40, 0.30 * H), (tx - 60, 0.42 * H), 0.10 * H, 34, 5.0)
draw_chain((-40, 0.62 * H), (tx - 80, 0.60 * H), 0.12 * H, 40, 6.0)
draw_chain((W + 40, 0.12 * H), (tx + 40, 0.30 * H), 0.08 * H, 30, 4.5)
draw_chain((W + 40, 0.56 * H), (tx + 90, 0.52 * H), 0.10 * H, 38, 5.5)
draw_chain((tx - 70, 0.86 * H), (0.50 * W, 1.06 * H), 0.03 * H, 30, 5.0)
draw_chain((tx + 70, 0.86 * H), (0.80 * W, 1.06 * H), 0.03 * H, 30, 5.0)
chain = chain.resize((W, H), Image.LANCZOS)
img = comp(img, np.asarray(chain, np.float32))

# ---- riscos en primer plano, abajo
def cliff(side, seed):
    r = np.random.default_rng(seed)
    lay = Image.new('L', (W, H), 0); ld = ImageDraw.Draw(lay)
    pts = []
    xs = np.linspace(0, 0.30, 24) if side < 0 else np.linspace(0.74, 1.0, 20)
    for x in xs:
        edge_y = (0.80 + 0.22 * (x / 0.30) ** 1.6) if side < 0 else (0.80 + 0.22 * ((1 - x) / 0.26) ** 1.6)
        pts.append((x * W, (edge_y + r.normal(0, 0.012)) * H))
    if side < 0: poly = [(0, H)] + [(0, pts[0][1])] + pts + [(pts[-1][0], H)]
    else: poly = [(pts[0][0], H)] + pts + [(W, pts[-1][1]), (W, H)]
    ld.polygon(poly, fill=255)
    return np.asarray(lay.filter(ImageFilter.GaussianBlur(1)), np.float32) / 255
for sd, seed in ((-1, 41), (1, 43)):
    m = cliff(sd, seed)[..., None]
    rock = np.array([16, 11, 12], np.float32) * (0.8 + 0.5 * fbm(W, H, 5, 10, seed=seed)[..., None])
    img = img * (1 - m) + rock * m
# bruma del abismo, delante
fog = fbm(W, H, 6, 3, seed=29)
fogm = np.clip((fog - 0.40) * 1.8, 0, 1) * np.clip((v - 0.80) / 0.20, 0, 1)
img = img * (1 - 0.6 * fogm[..., None]) + np.array([120, 84, 58], np.float32) * 0.6 * fogm[..., None]

# ---- motas de polvo / brasas
dust = Image.new('RGBA', (W, H), (0, 0, 0, 0)); dd = ImageDraw.Draw(dust)
for _ in range(260):
    x, y = random.uniform(0, W), random.uniform(0.1 * H, H); r = random.choice([1, 1, 1, 1.5, 2, 2.5])
    a = random.randint(40, 160); col = (255, random.randint(170, 210), random.randint(100, 140), a)
    dd.ellipse([x - r, y - r, x + r, y + r], fill=col)
dust = dust.filter(ImageFilter.GaussianBlur(0.8))
img = comp(img, np.asarray(dust, np.float32))

# ---- viñeta, y el lado izquierdo más oscuro (allí van las opciones)
vig = 1 - 0.55 * np.clip(((u - 0.55) ** 2 * 1.4 + (v - 0.48) ** 2 * 2.2), 0, 1)
left = 0.38 + 0.62 * np.clip((u - 0.06) / 0.42, 0, 1) ** 1.3
img = img * (vig * left)[..., None]
# tono: un poco de sepia y grano
lum = img.mean(axis=2, keepdims=True)
img = img * 0.82 + lum * np.array([1.08, 0.94, 0.78], np.float32) * 0.18
img = img + rng.normal(0, 2.2, img.shape).astype(np.float32)
out = Image.fromarray(np.clip(img, 0, 255).astype(np.uint8))
import os
dest = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), 'fondo.png')
out.save(dest, optimize=True)        # tools/lanzador/fondo.png
print(dest)
