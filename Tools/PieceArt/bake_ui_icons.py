"""하단 도구 막대·메뉴 아이콘(흰색 선, 투명 배경)을 그려 Assets/Resources/UIIcons에 저장한다.

    python Tools/PieceArt/bake_ui_icons.py

Unity에서 색을 입혀 쓰므로 흰색으로 그린다. 4배 크기로 그린 뒤 줄여 가장자리를 매끄럽게 한다.
"""
import math
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets/Resources/UIIcons"
SIZE, SS = 128, 4
S = SIZE * SS
W = 10 * SS          # 선 굵기
WHITE = (255, 255, 255, 255)


def canvas():
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    return img, ImageDraw.Draw(img)


def p(x, y):
    return x * SS, y * SS


def dot(d, x, y, r):
    d.ellipse([*p(x - r, y - r), *p(x + r, y + r)], fill=WHITE)


def polyline(d, pts):
    d.line([p(x, y) for x, y in pts], fill=WHITE, width=W, joint="curve")
    for x, y in (pts[0], pts[-1]):
        dot(d, x, y, 5)


def arc_points(cx, cy, r, a0, a1, n=48):
    return [(cx + r * math.cos(math.radians(a0 + (a1 - a0) * i / n)),
             cy + r * math.sin(math.radians(a0 + (a1 - a0) * i / n))) for i in range(n + 1)]


def undo():  # 무르기: 왼쪽으로 돌아가는 화살표
    img, d = canvas()
    polyline(d, [(30, 52), (78, 52)] + arc_points(78, 76, 24, -90, 90)[1:] + [(46, 100)])
    polyline(d, [(48, 32), (28, 52), (48, 72)])
    return img


def pass_turn():  # 한수 쉼: 모래시계
    img, d = canvas()
    polyline(d, [(34, 22), (94, 22)])
    polyline(d, [(34, 106), (94, 106)])
    polyline(d, [(40, 24), (40, 34), (64, 62), (88, 34), (88, 24)])
    polyline(d, [(40, 104), (40, 94), (64, 66), (88, 94), (88, 104)])
    d.polygon([p(50, 100), p(78, 100), p(64, 84)], fill=WHITE)
    return img


def view():  # 시점: 회전 화살표 두 개
    img, d = canvas()
    polyline(d, arc_points(64, 64, 38, 200, 340))
    polyline(d, arc_points(64, 64, 38, 20, 160))
    d.polygon([p(98, 64), p(112, 48), p(88, 46)], fill=WHITE)
    d.polygon([p(30, 64), p(16, 80), p(40, 82)], fill=WHITE)
    dot(d, 64, 64, 9)
    return img


def hint():  # 힌트: 전구
    img, d = canvas()
    d.ellipse([*p(32, 14), *p(96, 78)], outline=WHITE, width=W)
    polyline(d, [(50, 74), (52, 90), (76, 90), (78, 74)])
    polyline(d, [(52, 102), (76, 102)])
    polyline(d, [(58, 114), (70, 114)])
    return img


def menu():  # 메뉴: 세 줄
    img, d = canvas()
    for y in (36, 64, 92):
        polyline(d, [(26, y), (102, y)])
    return img


def settings():  # 설정: 조절 막대 세 개
    img, d = canvas()
    for y, knob in ((34, 44), (64, 84), (94, 58)):
        polyline(d, [(22, y), (106, y)])
        d.ellipse([*p(knob - 11, y - 11), *p(knob + 11, y + 11)], fill=WHITE)
    return img


def new_game():  # 새 대국: 원 안의 더하기
    img, d = canvas()
    d.ellipse([*p(18, 18), *p(110, 110)], outline=WHITE, width=W)
    polyline(d, [(64, 42), (64, 86)])
    polyline(d, [(42, 64), (86, 64)])
    return img


def flag():  # 기권: 깃발
    img, d = canvas()
    polyline(d, [(36, 18), (36, 110)])
    d.polygon([p(40, 22), p(98, 22), p(84, 44), p(98, 66), p(40, 66)], fill=WHITE)
    return img


def exit_icon():  # 나가기: 문과 화살표
    img, d = canvas()
    polyline(d, [(70, 26), (28, 26), (28, 102), (70, 102)])
    polyline(d, [(54, 64), (106, 64)])
    polyline(d, [(88, 46), (106, 64), (88, 82)])
    return img


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    for name, fn in [("undo", undo), ("pass", pass_turn), ("view", view), ("menu", menu), ("hint", hint), ("settings", settings),
                     ("new_game", new_game), ("flag", flag), ("exit", exit_icon)]:
        fn().resize((SIZE, SIZE), Image.LANCZOS).save(OUT / f"{name}.png")
    print("saved to", OUT)


if __name__ == "__main__":
    main()
