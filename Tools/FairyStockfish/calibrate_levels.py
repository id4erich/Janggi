"""AI 1~9단 간격 보정용 자동 대국.

GameManager.cs의 단별 설정표(AiLevels)를 읽어, 인접한 단끼리 Fairy-Stockfish로 대국시키고
윗단의 점수율과 Elo 차이를 출력한다.

    python Tools/FairyStockfish/calibrate_levels.py --games 24 --time-scale 0.4

--time-scale: PC는 휴대폰보다 약 2.6배 빠르므로(갤럭시 S10 기준) 0.4를 곱하면 휴대폰 실력에 가깝다.
"""
import argparse
import math
import random
import re
import subprocess
import sys
from concurrent.futures import ThreadPoolExecutor, as_completed
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
ENGINE = ROOT / "Tools/FairyStockfish/fairy-stockfish-largeboard_x86-64-modern.exe"
NNUE = ROOT / "Tools/FairyStockfish/janggi-9991472750de.nnue"  # 앱과 같은 신경망 평가
GAME_MANAGER = ROOT / "Assets/Scripts/Core/GameManager.cs"

# 앱의 차림 4종 (x=1,2,6,7 순서의 마/상) → FEN 1줄
PLACEMENTS = ["rnba1anbr", "rbna1abnr", "rnba1abnr", "rbna1anbr"]
MAX_PLIES = 200          # 이 수를 넘으면 마지막 평가값으로 판정
ADJUDICATE_CP = 500      # 판정 시 이 이상 우세하면 승리로 봄


def read_levels():
    src = GAME_MANAGER.read_text(encoding="utf-8")
    table = src[src.index("AiLevels ="):src.index("};", src.index("AiLevels ="))]
    levels = [(int(s), int(t)) for s, t, _ in re.findall(r"\((-?\d+),\s*(\d+),\s*(\d+)\)", table)]
    assert len(levels) == 9, levels
    return levels


def start_fen(cho, han):
    return f"{han}/4k4/1c5c1/p1p1p1p1p/9/9/P1P1P1P1P/1C5C1/4K4/{cho.upper()} w - - 0 1"


class Engine:
    def __init__(self, skill):
        self.p = subprocess.Popen([str(ENGINE)], stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                                  text=True, bufsize=1, creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
        self.send("uci"); self.wait("uciok")
        self.send("setoption name UCI_Variant value janggicasual")
        self.send(f"setoption name Skill Level value {skill}")
        self.send(f"setoption name EvalFile value {NNUE.as_posix()}")
        self.send("isready"); self.wait("readyok")

    def send(self, cmd):
        self.p.stdin.write(cmd + "\n")

    def wait(self, prefix):
        for line in self.p.stdout:
            if line.startswith(prefix):
                return line
        raise RuntimeError("engine exited")

    def best_move(self, fen, moves, movetime):
        self.send(f"position fen {fen}" + (" moves " + " ".join(moves) if moves else ""))
        self.send(f"go movetime {movetime}")
        score = 0
        for line in self.p.stdout:
            m = re.search(r" score (cp|mate) (-?\d+)", line)
            if m:
                score = int(m.group(2)) if m.group(1) == "cp" else (100000 if int(m.group(2)) > 0 else -100000)
            if line.startswith("bestmove"):
                return line.split()[1], score
        raise RuntimeError("engine exited")

    def close(self):
        try:
            self.send("quit"); self.p.wait(2)
        except Exception:
            self.p.kill()


def play_game(levels, white_lv, black_lv, time_scale, rng):
    """반환: (초(백) 기준 결과 1/0.5/0, 수 수, 외통으로 끝났는지)"""
    fen = start_fen(rng.choice(PLACEMENTS), rng.choice(PLACEMENTS))
    engines = {}
    try:
        for side, lv in (("w", white_lv), ("b", black_lv)):
            engines[side] = (Engine(levels[lv - 1][0]), max(50, int(levels[lv - 1][1] * time_scale)))
        moves, side, score = [], "w", 0
        while len(moves) < MAX_PLIES:
            eng, mt = engines[side]
            move, score = eng.best_move(fen, moves, mt)
            if move == "(none)":  # 둘 수가 없음 = 외통
                return (0.0 if side == "w" else 1.0), len(moves), True
            moves.append(move)
            side = "b" if side == "w" else "w"
        # 마지막으로 둔 쪽의 평가값으로 판정
        mover_white = side == "b"
        if abs(score) < ADJUDICATE_CP:
            return 0.5, len(moves), False
        return (1.0 if (score > 0) == mover_white else 0.0), len(moves), False
    finally:
        for eng, _ in engines.values():
            eng.close()


def elo(score):
    if score <= 0: return -999
    if score >= 1: return 999
    return -400 * math.log10(1 / score - 1)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--games", type=int, default=24, help="인접 단 한 쌍당 대국 수 (짝수)")
    ap.add_argument("--time-scale", type=float, default=0.4)
    ap.add_argument("--workers", type=int, default=8)
    ap.add_argument("--pairs", default="1-8", help="낮은 단 범위, 예: 1-8 또는 3-4")
    ap.add_argument("--seed", type=int, default=2026)
    ap.add_argument("--ladder", help="설정표 대신 시험할 '실력단계:ms' 목록, 예: -10:1000,-5:1000,0:1000")
    args = ap.parse_args()

    if args.ladder:
        levels = [tuple(int(v) for v in item.split(":")) for item in args.ladder.split(",")]
        lo, hi = 1, len(levels) - 1
    else:
        levels = read_levels()
        lo, hi = map(int, args.pairs.split("-"))
    label = (lambda lv: f"skill {levels[lv - 1][0]}/{levels[lv - 1][1]}ms") if args.ladder else (lambda lv: f"{lv}단")
    rng = random.Random(args.seed)
    jobs = []
    for low in range(lo, hi + 1):
        for g in range(args.games):
            high_is_white = g % 2 == 0  # 선후 교대
            jobs.append((low, high_is_white, random.Random(rng.random())))

    results = {low: [] for low in range(lo, hi + 1)}
    stats = {low: [] for low in range(lo, hi + 1)}
    with ThreadPoolExecutor(args.workers) as ex:
        futs = {}
        for low, high_white, grng in jobs:
            w, b = (low + 1, low) if high_white else (low, low + 1)
            futs[ex.submit(play_game, levels, w, b, args.time_scale, grng)] = (low, high_white)
        for f in as_completed(futs):
            low, high_white = futs[f]
            r, plies, mated = f.result()
            results[low].append(r if high_white else 1 - r)
            stats[low].append((plies, mated))
            done = sum(len(v) for v in results.values())
            print(f"\r진행 {done}/{len(jobs)}", end="", file=sys.stderr, flush=True)
    print(file=sys.stderr)

    print("대국 | 윗단 승-무-패 | 윗단 점수율 | Elo 차이 | 평균 수 | 외통 비율")
    for low, rs in results.items():
        w, d = rs.count(1.0), rs.count(0.5)
        s = sum(rs) / len(rs)
        plies = sum(p for p, _ in stats[low]) / len(rs)
        mates = sum(m for _, m in stats[low]) / len(rs)
        print(f"{label(low)} vs {label(low + 1)} | {w}-{d}-{len(rs) - w - d} | {s:.0%} | {elo(s):+.0f} | {plies:.0f} | {mates:.0%}")


if __name__ == "__main__":
    main()
