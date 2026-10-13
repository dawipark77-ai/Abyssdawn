# Abyssdawn 타이틀 — Claude Design(ui_kits/title/index.html)의 정적 레이어를 그림으로 굽는다.
import os, math
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

PROJ = r'E:\Dawi\My Programs\Game Maker\Unity\Projects\Abyssdawn 01'
OUT = os.path.join(PROJ, 'Assets', 'Images', 'Title')
os.makedirs(OUT, exist_ok=True)

def hexc(h, a=1.0):
    h = h.lstrip('#')
    return np.array([int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16)], dtype=np.float32) / 255.0, a

# ───────────────────────── 1) 배경 ─────────────────────────
# 디자인 #root = 430×932. 9:16 화면에서도 옆이 비지 않게 524×932 상자로 구운 뒤(가운데 430이 디자인 영역),
# 화면에 '꽉 채우기(Envelope)'로 깐다. 배율 2배.
S = 2
BW, BH = 524, 932
W, H = BW * S, BH * S
OX = (BW - 430) / 2  # 디자인 x 0 이 상자에서 어디인지

src = Image.open(os.path.join(PROJ, 'Assets', 'Images', 'Background', 'Background 01.png')).convert('RGB')
# background: center 30% / cover
sc = max(W / src.width, H / src.height)
nw, nh = int(round(src.width * sc)), int(round(src.height * sc))
img = src.resize((nw, nh), Image.LANCZOS)
left = int(round((nw - W) * 0.5)); top = int(round((nh - H) * 0.30))
img = img.crop((left, top, left + W, top + H))
c = np.asarray(img).astype(np.float32) / 255.0

# filter: brightness(.4) contrast(1.2) saturate(.75)  (sRGB, 단계마다 0~1로 자름)
c = np.clip(c * 0.4, 0, 1)
c = np.clip((c - 0.5) * 1.2 + 0.5, 0, 1)
s = 0.75
M = np.array([[0.213 + 0.787 * s, 0.715 - 0.715 * s, 0.072 - 0.072 * s],
              [0.213 - 0.213 * s, 0.715 + 0.285 * s, 0.072 - 0.072 * s],
              [0.213 - 0.213 * s, 0.715 - 0.715 * s, 0.072 + 0.928 * s]], dtype=np.float32)
c = np.clip(c @ M.T, 0, 1)

yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
X = xx / S; Y = yy / S  # 상자 좌표 (디자인 px)

def over(base, col, alpha):
    a = alpha[..., None]
    return base * (1 - a) + col[None, None, :] * a

def radial_farthest(cx, cy, bx0, by0, bw, bh):
    # CSS ellipse farthest-corner: rx = √2·max(좌,우), ry = √2·max(위,아래)
    rx = math.sqrt(2) * max(cx - bx0, bx0 + bw - cx)
    ry = math.sqrt(2) * max(cy - by0, by0 + bh - cy)
    return np.sqrt(((X - cx) / rx) ** 2 + ((Y - cy) / ry) ** 2)

# ::after — 아래층: 위→아래 어둡게 (10,8,7) .75 / .2@38% / .3@60% / .98@100%
dark = hexc('#0a0807')[0]
t = Y / BH
a_lin = np.interp(t, [0, 0.38, 0.60, 1.0], [0.75, 0.2, 0.3, 0.98]).astype(np.float32)
c = over(c, dark, a_lin)
# ::after — 위층: 금빛 둥근 빛 ellipse at 50% 38%, .28 → 0 @50%
gold = hexc('#d4a761')[0]
r = radial_farthest(BW / 2, BH * 0.38, 0, 0, BW, BH)
a_rad = np.clip(0.28 * (1 - r / 0.5), 0, 0.28).astype(np.float32)
c = over(c, gold, a_rad)

# .shaft — 180×380, top 40, 가운데. radial at top, .22 → 0 @70%, screen, blur 4px
sx0, sy0, sw, sh = OX + 125, 40, 180, 380
inside = (X >= sx0) & (X <= sx0 + sw) & (Y >= sy0) & (Y <= sy0 + sh)
rs = radial_farthest(sx0 + sw / 2, sy0, sx0, sy0, sw, sh)
a_sh = np.where(inside, np.clip(0.22 * (1 - rs / 0.7), 0, 0.22), 0).astype(np.float32)
a_sh = np.asarray(Image.fromarray((a_sh * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(4 * S))).astype(np.float32) / 255.0
screen = 1 - (1 - c) * (1 - gold[None, None, :])
c = c + (screen - c) * a_sh[..., None]

# .grain — 3px 격자에 1px 흰 점(.3), overlay, opacity .14
gx = (X % 3) - 1.5; gy = (Y % 3) - 1.5
dot = np.clip(1 - np.sqrt(gx * gx + gy * gy), 0, 1) * 0.3
g = np.ones_like(c) * 0.5  # 점 없는 곳은 투명 → overlay 영향 없게 dot 알파로 섞음
ov = np.where(c < 0.5, 2 * c * 1.0, 1 - 2 * (1 - c) * (1 - 1.0))  # 흰색과 overlay
c = c + (ov - c) * (dot * 0.14)[..., None]

# .vignette — ellipse at center, transparent 30% → rgba(0,0,0,.9) 100% (디자인 #root 기준)
rv = radial_farthest(OX + 215, 466, OX, 0, 430, 932)
a_v = np.clip((rv - 0.3) / 0.7, 0, 1) * 0.9
a_v = np.where(rv > 1, 0.9, a_v).astype(np.float32)
c = over(c, np.zeros(3, np.float32), a_v)

Image.fromarray((np.clip(c, 0, 1) * 255 + 0.5).astype(np.uint8)).save(os.path.join(OUT, 'Title_BG.png'))
print('bg', W, H)

# ───────────────────────── 2) 왕관 장식 (SVG 140×52, 빛 번짐 8px) ─────────────────────────
K = 4; PAD = 14
CW, CH = (140 + PAD * 2) * K, (52 + PAD * 2) * K
def P(x, y): return ((x + PAD) * K, (y + PAD) * K)
def qbez(p0, p1, p2, n=80):
    return [((1 - t) ** 2 * p0[0] + 2 * (1 - t) * t * p1[0] + t * t * p2[0],
             (1 - t) ** 2 * p0[1] + 2 * (1 - t) * t * p1[1] + t * t * p2[1]) for t in [i / n for i in range(n + 1)]]

def crown_layer():
    im = Image.new('RGBA', (CW, CH), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    gold = (212, 167, 97); gold2 = (179, 138, 76); cream = (243, 231, 200)
    lw = int(round(1.2 * K))
    def poly(pts, alpha):
        layer = Image.new('RGBA', (CW, CH), (0, 0, 0, 0)); dd = ImageDraw.Draw(layer)
        pp = [P(*p) for p in pts]
        dd.line(pp, fill=gold + (255,), width=lw, joint='curve')
        for q in (pp[0], pp[-1]):
            dd.ellipse((q[0] - lw / 2, q[1] - lw / 2, q[0] + lw / 2, q[1] + lw / 2), fill=gold + (255,))
        a = layer.split()[3].point(lambda v: int(v * alpha)); layer.putalpha(a)
        return layer
    im = Image.alpha_composite(im, poly(qbez((10, 40), (70, 4), (130, 40)), 0.7))
    im = Image.alpha_composite(im, poly(qbez((30, 34), (70, 14), (110, 34)), 0.5))
    for seg in [((70, 4), (70, -4)), ((30, 34), (26, 26)), ((110, 34), (114, 26)), ((50, 18), (48, 10)), ((90, 18), (92, 10))]:
        im = Image.alpha_composite(im, poly(list(seg), 1.0))
    d = ImageDraw.Draw(im)
    def diamond(cx, cy, half, col):
        d.polygon([P(cx, cy - half), P(cx + half, cy), P(cx, cy + half), P(cx - half, cy)], fill=col)
    diamond(70, 4, 4 * math.sqrt(2), gold + (255,))
    inner = Image.new('RGBA', (CW, CH), (0, 0, 0, 0))
    ImageDraw.Draw(inner).polygon([P(70, 4 - 2 * math.sqrt(2)), P(70 + 2 * math.sqrt(2), 4), P(70, 4 + 2 * math.sqrt(2)), P(70 - 2 * math.sqrt(2), 4)], fill=cream + (178,))
    im = Image.alpha_composite(im, inner); d = ImageDraw.Draw(im)
    for (x, y, rr, col) in [(30, 34, 2.2, gold), (110, 34, 2.2, gold), (50, 18, 1.6, gold2), (90, 18, 1.6, gold2)]:
        q = P(x, y); R = rr * K
        d.ellipse((q[0] - R, q[1] - R, q[0] + R, q[1] + R), fill=col + (255,))
    return im

base = crown_layer()
# drop-shadow(0 0 8px rgba(212,167,97,.5)) → 금빛 번짐을 밑에 깔고, 전체 opacity .92
glow_a = base.split()[3].filter(ImageFilter.GaussianBlur(8 * K / 2)).point(lambda v: int(v * 0.5))
glow = Image.new('RGBA', base.size, (212, 167, 97, 0)); glow.putalpha(glow_a)
crown = Image.alpha_composite(glow, base)
crown.putalpha(crown.split()[3].point(lambda v: int(v * 0.92)))
crown.save(os.path.join(OUT, 'Title_Crown.png'))
print('crown', crown.size, 'unity size', 140 + PAD * 2, 52 + PAD * 2)

# ───────────────────────── 3) 마름모 (금빛 번짐 10px) ─────────────────────────
K = 8; PADD = 12; SZ = 10
DS = (SZ + PADD * 2) * K
im = Image.new('RGBA', (DS, DS), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
cxy = DS / 2; half = SZ / 2 * math.sqrt(2) * K
sq = [(cxy, cxy - half), (cxy + half, cxy), (cxy, cxy + half), (cxy - half, cxy)]
d.polygon(sq, fill=(212, 167, 97, 255))
glow_a = im.split()[3].filter(ImageFilter.GaussianBlur(10 * K / 2)).point(lambda v: min(255, int(v * 0.8 * 1.6)))
gl = Image.new('RGBA', im.size, (212, 167, 97, 0)); gl.putalpha(glow_a)
# inset 0 1px 0 rgba(255,255,255,.3): 윗쪽 두 모서리 안쪽 밝은 테
hl = Image.new('RGBA', im.size, (0, 0, 0, 0)); hd = ImageDraw.Draw(hl)
hd.line([sq[3], sq[0], sq[1]], fill=(255, 255, 255, 77), width=K)
dia = Image.alpha_composite(Image.alpha_composite(gl, im), hl)
dia.save(os.path.join(OUT, 'Title_Diamond.png'))
print('diamond unity size', SZ + PADD * 2)

# ───────────────────────── 4) 가는 선 (가운데가 밝은 / 한쪽으로 사라지는) ─────────────────────────
lw_, lh_ = 512, 4
x = np.linspace(0, 1, lw_, dtype=np.float32)
def save_line(alpha, name):
    a = np.tile(alpha[None, :], (lh_, 1))
    arr = np.zeros((lh_, lw_, 4), np.uint8); arr[..., :3] = 255; arr[..., 3] = (a * 255).astype(np.uint8)
    Image.fromarray(arr, 'RGBA').save(os.path.join(OUT, name))
save_line(1 - np.abs(x * 2 - 1), 'Title_LineCenter.png')  # transparent → 색 → transparent
save_line(x, 'Title_LineFade.png')                          # transparent → 색

# ───────────────────────── 5) 불씨 (2px 점 + 6px 번짐) ─────────────────────────
K = 8; E = 16
im = Image.new('RGBA', (E * K, E * K), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
cxy = E * K / 2
d.ellipse((cxy - K, cxy - K, cxy + K, cxy + K), fill=(212, 167, 97, 255))
glow_a = im.split()[3].filter(ImageFilter.GaussianBlur(6 * K / 2)).point(lambda v: min(255, int(v * 2.2)))
gl = Image.new('RGBA', im.size, (212, 167, 97, 0)); gl.putalpha(glow_a)
Image.alpha_composite(gl, im).save(os.path.join(OUT, 'Title_Ember.png'))
print('ember unity size', E)

# ───────────────────────── 6) 언어 버튼 바탕 94×32 ─────────────────────────
K = 4; PW, PH = 94, 32; PP = 8
im = Image.new('RGBA', ((PW + PP * 2) * K, (PH + PP * 2) * K), (0, 0, 0, 0))
# box-shadow 0 2px 8px rgba(0,0,0,.6)
sh = Image.new('L', im.size, 0); ImageDraw.Draw(sh).rectangle((PP * K, (PP + 2) * K, (PP + PW) * K, (PP + PH + 2) * K), fill=153)
sh = sh.filter(ImageFilter.GaussianBlur(8 * K / 2))
shadow = Image.new('RGBA', im.size, (0, 0, 0, 0)); shadow.putalpha(sh)
im = Image.alpha_composite(im, shadow)
body = np.zeros((PH * K, PW * K, 4), np.float32)
tt = np.linspace(0, 1, PH * K, dtype=np.float32)[:, None]
top_c = np.array([29, 24, 20], np.float32); bot_c = np.array([10, 8, 7], np.float32)
body[..., :3] = top_c * (1 - tt[..., None]) + bot_c * tt[..., None]
body[..., 3] = 0.85 * 255
bimg = Image.fromarray(body.astype(np.uint8), 'RGBA')
layer = Image.new('RGBA', im.size, (0, 0, 0, 0)); layer.paste(bimg, (PP * K, PP * K))
im = Image.alpha_composite(im, layer)
d = ImageDraw.Draw(im)
x0, y0, x1, y1 = PP * K, PP * K, (PP + PW) * K - 1, (PP + PH) * K - 1
d.line([(x0 + K, y0 + K // 2), (x1 - K, y0 + K // 2)], fill=(212, 167, 97, 38), width=K)  # inset 윗 하이라이트
d.rectangle((x0, y0, x1, y1), outline=(90, 67, 34, 255), width=K)                       # 1px #5a4322
d.rectangle((x0 + 2 * K, y0 + 2 * K, x1 - 2 * K, y1 - 2 * K), outline=(58, 47, 30, 255), width=K)  # 안쪽 #3a2f1e
im.save(os.path.join(OUT, 'Title_LangPill.png'))
print('pill unity size', PW + PP * 2, PH + PP * 2)
