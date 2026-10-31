"""장기판 윗면(나뭇결 + 먹선) 텍스처를 만들어 Assets/Resources/BoardArt에 저장한다.

    python Tools/PieceArt/bake_board_texture.py

board.png(색) + board_n.png(노멀맵: 잔잔한 나뭇결과 살짝 파인 먹선) + board_side.png(판 옆면)
배치는 기존 BoardTextureGenerator와 같다: 정사각 텍스처, 여백 = 한 변의 80/1024, 가로 8칸·세로 9칸.
(판 Plane의 비균일 스케일 0.952 x 1.071이 칸을 정사각형으로 맞춘다)
"""
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets/Resources/BoardArt"
SIZE = 2048
SS = 2                      # 먹선 안티에일리어싱용 슈퍼샘플링 배율
MARGIN = SIZE * 80 / 1024
INK = (38, 26, 16)
rng = np.random.default_rng(1592)


def smooth_noise(w, h, cells_x, cells_y):
    raw = (rng.random((cells_y, cells_x)) * 255).astype(np.uint8)
    return np.asarray(Image.fromarray(raw).resize((w, h), Image.BICUBIC), dtype=np.float32) / 255


def board_wood(w, h, base, dark, rings, contrast):
    """세로로 길게 뻗은 결 + 완만한 휘어짐 + 가는 섬유결"""
    y, x = np.mgrid[0:h, 0:w] / max(w, h)
    warp = smooth_noise(w, h, 3, 6) * 0.9 + smooth_noise(w, h, 9, 20) * 0.1
    grain = np.sin((x + warp * 0.08) * rings * np.pi * 2) * 0.5 + 0.5
    fibers = smooth_noise(w, h, w // 3, 6)
    t = (grain ** 3) * contrast + fibers * contrast * 0.45
    base, dark = np.array(base, np.float32), np.array(dark, np.float32)
    return base + (dark - base) * t[..., None], t


def ink_mask():
    s = SIZE * SS
    img = Image.new("L", (s, s), 0)
    d = ImageDraw.Draw(img)
    m, sx, sy = MARGIN * SS, (s - 2 * MARGIN * SS) / 8, (s - 2 * MARGIN * SS) / 9
    line = int(5 * SS)

    def p(ix, iy):
        return m + ix * sx, m + iy * sy

    for i in range(10):
        d.line([p(0, i), p(8, i)], fill=255, width=line)
    for i in range(9):
        d.line([p(i, 0), p(i, 9)], fill=255, width=line)
    for y0 in (0, 7):  # 궁성 X
        d.line([p(3, y0), p(5, y0 + 2)], fill=255, width=line)
        d.line([p(5, y0), p(3, y0 + 2)], fill=255, width=line)
    # 이중 테두리: 굵은 바깥선 + 가는 안쪽선
    for off, wdt in ((28 * SS, int(11 * SS)), (14 * SS, int(3 * SS))):
        d.rectangle([m - off, m - off, s - m + off, s - m + off], outline=255, width=wdt)
    img = img.resize((SIZE, SIZE), Image.LANCZOS)
    return np.asarray(img, dtype=np.float32) / 255


def normal_from_height(h, strength):
    gy, gx = np.gradient(h)
    n = np.dstack([-gx * strength, gy * strength, np.ones_like(h)])
    n /= np.linalg.norm(n, axis=2, keepdims=True)
    return ((n * 0.5 + 0.5) * 255).astype(np.uint8)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    wood, t = board_wood(SIZE, SIZE, (226, 186, 130), (176, 128, 74), rings=6.0, contrast=0.32)
    ink = ink_mask()
    # 먹이 나무에 스며든 듯 농담을 살짝 흔듦
    ink_density = ink * (0.82 + smooth_noise(SIZE, SIZE, 40, 40) * 0.18)
    color = wood + (np.array(INK, np.float32) - wood) * ink_density[..., None]
    Image.fromarray(np.clip(color, 0, 255).astype(np.uint8)).save(OUT / "board.png")

    soft_ink = np.asarray(Image.fromarray((ink * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.5)),
                          dtype=np.float32) / 255
    height = t * 0.6 - soft_ink * 0.8
    Image.fromarray(normal_from_height(height, 3.0)).save(OUT / "board_n.png")

    side, _ = board_wood(512, 128, (150, 104, 60), (102, 66, 34), rings=10.0, contrast=0.4)
    Image.fromarray(np.clip(side, 0, 255).astype(np.uint8)).save(OUT / "board_side.png")
    print("saved to", OUT)


if __name__ == "__main__":
    main()
