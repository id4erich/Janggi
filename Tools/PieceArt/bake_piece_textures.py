"""장기말 윗면(음각 붓글씨)·옆면(나뭇결) 텍스처를 만들어 Assets/Resources/PieceArt에 저장한다.

    python Tools/PieceArt/bake_piece_textures.py

윗면: {Team}_{Type}.png(색) + {Team}_{Type}_n.png(노멀맵, 글자가 파인 음각)
옆면: wood_side.png
글꼴(OFL): 漢 = Yuji Boku, 楚 = Yuji Syuku (Tools/Fonts)
"""
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets/Resources/PieceArt"
FONTS = {"Han": ROOT / "Tools/Fonts/YujiBoku-Regular.ttf", "Cho": ROOT / "Tools/Fonts/YujiSyuku-Regular.ttf"}
INK = {"Han": (168, 10, 16), "Cho": (12, 46, 150)}  # 조명 아래에서도 바래 보이지 않게 진하게
GLYPHS = {
    "King": {"Cho": "楚", "Han": "漢"}, "Guard": "士", "Car": "車", "Cannon": "包",
    "Horse": "馬", "Elephant": "象", "Pawn": {"Cho": "卒", "Han": "兵"},
}
SIZE = 512
GLYPH_RATIO = 0.74      # 윗면 지름 대비 글자 크기
ENGRAVE_DEPTH = 6.0     # 노멀맵 세기 (클수록 깊게 파인 느낌)
rng = np.random.default_rng(2026)


def wood(size, base, dark, rings=9.0, contrast=0.22):
    """회양목 느낌의 잔잔한 곧은 나뭇결 (값이 매번 같도록 고정 시드)"""
    y, x = np.mgrid[0:size, 0:size] / size
    def smooth_noise(cells):
        raw = (rng.random((cells, cells)) * 255).astype(np.uint8)
        return np.asarray(Image.fromarray(raw).resize((size, size), Image.BICUBIC), dtype=np.float32) / 255
    warp = smooth_noise(4) * 0.8 + smooth_noise(12) * 0.15
    grain = np.sin((x + warp * 0.12) * rings * np.pi * 2) * 0.5 + 0.5
    streak = np.asarray(Image.fromarray((rng.random((size // 2, 8)) * 255).astype(np.uint8)).resize(
        (size, size), Image.BICUBIC), dtype=np.float32).T / 255
    t = (grain ** 4) * contrast + streak * contrast * 0.35
    base, dark = np.array(base, np.float32), np.array(dark, np.float32)
    return base + (dark - base) * t[..., None]


def glyph_mask(char, team):
    img = Image.new("L", (SIZE, SIZE), 0)
    font = ImageFont.truetype(str(FONTS[team]), int(SIZE * GLYPH_RATIO))
    d = ImageDraw.Draw(img)
    l, t, r, b = d.textbbox((0, 0), char, font=font)
    d.text(((SIZE - (r - l)) / 2 - l, (SIZE - (b - t)) / 2 - t), char, font=font, fill=255)
    return np.asarray(img, dtype=np.float32) / 255


def normal_from_height(h):
    gy, gx = np.gradient(h)
    n = np.dstack([-gx * ENGRAVE_DEPTH, gy * ENGRAVE_DEPTH, np.ones_like(h)])
    n /= np.linalg.norm(n, axis=2, keepdims=True)
    return ((n * 0.5 + 0.5) * 255).astype(np.uint8)


def bake_face(team, type_name, char):
    mask = glyph_mask(char, team)
    soft = np.asarray(Image.fromarray((mask * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(2.5)),
                      dtype=np.float32) / 255
    face = wood(SIZE, (236, 222, 190), (205, 184, 142))
    # 홈 가장자리는 그늘져 어둡고, 바닥은 먹(인주)으로 채운 느낌
    edge = np.clip(soft - mask, 0, 1)
    ink = np.array(INK[team], np.float32)
    color = face * (1 - edge[..., None] * 0.35)
    color = color + (ink - color) * mask[..., None] * 0.95
    Image.fromarray(np.clip(color, 0, 255).astype(np.uint8)).save(OUT / f"{team}_{type_name}.png")
    Image.fromarray(normal_from_height(1 - soft)).save(OUT / f"{team}_{type_name}_n.png")


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    for type_name, g in GLYPHS.items():
        for team in ("Cho", "Han"):
            bake_face(team, type_name, g[team] if isinstance(g, dict) else g)
    side = wood(256, (214, 194, 152), (160, 128, 86), rings=5.0, contrast=0.4)
    Image.fromarray(np.clip(side, 0, 255).astype(np.uint8)).save(OUT / "wood_side.png")
    print("saved to", OUT)


if __name__ == "__main__":
    main()
