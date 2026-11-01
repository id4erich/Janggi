# Seongwon Janggi (성원장기)

**English** | [한국어](README.ko.md)

[![Rule check](https://github.com/id4erich/Janggi/actions/workflows/rule-check.yml/badge.svg)](https://github.com/id4erich/Janggi/actions/workflows/rule-check.yml)

A Korean chess (janggi) app built with Unity. It targets Android (arm64) and uses the Fairy-Stockfish engine as the AI opponent.

## Features

- Korean janggi rules
  - Diagonal moves along the palace lines (General, Guards, Chariots, Cannons, Soldiers)
  - Moves that leave your own General in check are rejected; checkmate ends the game
  - Pass, undo, resign
- Game modes: vs AI as Cho, vs AI as Han (the AI moves first and the board is turned around), or two players on one device
- Sound effects, voice lines and vibration can be turned on or off separately (Menu → Settings); the win/loss record can be reset there too
- Four Horse/Elephant setups, chosen separately for each side (Cho and Han)
- AI levels 1–9 (dan). Uses Fairy-Stockfish with the janggi neural network (NNUE); level 9 is the engine at full strength. Falls back to classical evaluation if the network cannot be loaded, and to a built-in alpha-beta AI if the engine cannot be started. A gold bar under the AI panel shows its thinking time, and its piece is lifted with a gold ring before it moves
- 3D presentation: chamfered octagonal boxwood pieces with engraved brush-script characters, a wood-grain board, a top-down view that fills the screen width (the "view" button switches to an angled 3D view), and soft shadows
- Player panels for both sides (turn highlight, AI level, remaining time, record) and a bottom toolbar (undo, pass, hint, view, menu); resign and quit are in the menu and ask for confirmation
- The game in progress is saved after every move and resumed when the app restarts (undo history included)
- When a General is in check it is marked with a red ring; the "hint" button shows the engine's suggested move with an arrow
- Move record (Menu → Move record): numbered list of moves with side, piece, coordinates and captures. It can be shared as text, or opened in review mode to step through the game and ask the engine for its move in any position
- Rule sets matching Fairy-Stockfish's janggi variants, so the AI plays by the same rules:
  - Casual (`janggicasual`): no bikjang; two passes in a row end the game in a draw
  - Bikjang draw (`janggitraditional`): when the Generals face each other on an open file, the opponent must break it or accept with a pass, which ends the game in a draw
  - Tournament (`janggi`): bikjang or two passes end the game and the winner is decided by material points (Han gets a 1.5-point bonus)
- Optional move time limit (none / 30 s / 60 s); running out of time loses the game
- Captured pieces are shown as small icons in the capturing player's panel
- Moving pieces: tap-then-tap or drag-and-drop; a wooden clack and vibration when a piece lands; illegal targets shake the piece
- Two-player games are not counted in the win/loss record
- Voice lines (such as "Janggun!", the call for check) are spoken with the device's built-in TextToSpeech (works offline)

The in-game text is in Korean.

## Project layout

| Path | Contents |
|---|---|
| `Assets/Scripts/Core` | Rules (`RuleValidator`), game flow (`GameManager`), core types |
| `Assets/Scripts/AI` | Fairy-Stockfish integration (`FairyStockfishEngine`), built-in AI |
| `Assets/Scripts/View` | Board and piece rendering, piece mesh generation |
| `Assets/Scripts/UI` | Mobile UI, camera |
| `Assets/Scripts/Editor` | APK build menu, EditMode tests |
| `Assets/Plugins/Android/libs/arm64-v8a` | Fairy-Stockfish executable for Android |
| `Assets/Plugins/Android/AndroidManifest.xml` | Custom main manifest (Unity defaults plus the TTS engine query required on Android 11+) |
| `Assets/Resources/PieceArt`, `BoardArt` | Pre-baked piece and board textures and materials |
| `Tools/FairyStockfish` | Windows engine for the editor, AI level calibration script |
| `Tools/RuleCheck` | Checks the rule engine against Fairy-Stockfish move generation |
| `Tools/PieceArt` | Scripts that bake the piece and board textures |
| `Tools/Fonts` | Brush-script fonts used only for baking textures (not shipped in the app) |

## Requirements

- Unity 6000.3.16f1 (URP, Android Build Support)
- Python 3 with Pillow and NumPy, for the tool scripts

## Build and run

- **Editor**: open `Assets/Scenes/SampleScene.unity` and press Play. In the editor the Windows engine in `Tools/FairyStockfish` is used.
- **APK**: run **Build → Build Android APK** from the menu. The output is `Builds/Android/성원장기.apk`.
- **Tests**: open **Window → General → Test Runner** and run the EditMode tests. They cover the rules, game flow, the AI and the engine integration.

## Rule check against Fairy-Stockfish

`Tools/RuleCheck` plays random games and, at every position, compares the legal moves from the app's `RuleValidator` with Fairy-Stockfish's `go perft 1` (janggicasual). Pass moves are excluded because the app has a separate pass button.

```bash
cd Tools/RuleCheck && dotnet run -c Release -- 2026 200
```

Arguments are the random seed, the number of games and optionally the variant (`janggicasual`, `janggitraditional`, `janggi`). At the time of writing, 260 games (about 38,000 positions) matched exactly.

## AI level calibration

The per-level settings live in `GameManager.AiLevels`. The calibration script plays automatic games between adjacent levels with the PC engine and reports the score and Elo difference.

```bash
python Tools/FairyStockfish/calibrate_levels.py --games 24 --time-scale 0.4
```

`--time-scale 0.4` scales the PC's thinking time to roughly match a phone (Galaxy S10). To compare raw engine skill levels instead of the app's levels, use the form `--ladder="-10:1000,-5:1000,0:1000"`.

## Re-baking piece and board textures

```bash
python Tools/PieceArt/bake_piece_textures.py
```

```bash
python Tools/PieceArt/bake_board_texture.py
```

The results are written to `Assets/Resources/PieceArt` and `Assets/Resources/BoardArt`. In Unity, set the Texture Type of the normal maps (`*_n.png`) to Normal map.

## Rebuilding the Android (arm64) engine

Build from the `src` folder of [Fairy-Stockfish](https://github.com/fairy-stockfish/Fairy-Stockfish) at commit [`9f778da`](https://github.com/fairy-stockfish/Fairy-Stockfish/tree/9f778da667f6e07dae1e85d3e2ea204fc6dee94d) (the version the janggi network requires), using the clang from the NDK (r27c) bundled with Unity.

```bash
"$NDK/toolchains/llvm/prebuilt/windows-x86_64/bin/clang++.exe" --target=aarch64-linux-android25 \
  -std=c++17 -stdlib=libc++ -fPIE -fno-exceptions -O3 -DNDEBUG -DIS_64BIT -DUSE_POPCNT -DUSE_NEON \
  -DLARGEBOARDS -DPRECOMPUTED_MAGICS -DNNUE_EMBEDDING_OFF -w \
  benchmark.cpp bitbase.cpp bitboard.cpp endgame.cpp evaluate.cpp main.cpp material.cpp misc.cpp \
  movegen.cpp movepick.cpp pawns.cpp position.cpp psqt.cpp search.cpp thread.cpp timeman.cpp tt.cpp \
  uci.cpp ucioption.cpp tune.cpp syzygy/tbprobe.cpp nnue/evaluate_nnue.cpp nnue/features/half_ka_v2.cpp \
  partner.cpp parser.cpp piece.cpp variant.cpp xboard.cpp nnue/features/half_ka_v2_variants.cpp \
  -static-libstdc++ -pie -lm -latomic -o libfairystockfish.so
```

Strip it with `llvm-strip` and place it in `Assets/Plugins/Android/libs/arm64-v8a/`. Android does not allow executing files from the app's data directory, so the engine is shipped under a `.so` name; the installer then extracts it to the native library directory, where it may be executed. At runtime the app looks up that directory and starts the engine with `Runtime.exec`.

The PC engine (`Tools/FairyStockfish/fairy-stockfish-largeboard_x86-64-modern.exe`) is built from the same source with the same NDK clang targeting `x86_64-pc-windows-msvc` (MSVC itself has no 128-bit integers). Run it from a Visual Studio 2022 `vcvars64` prompt, with a copy of the NDK's `ld.lld.exe` named `lld-link.exe` (plus the NDK `bin` DLLs) on `PATH`:

```bat
clang++ --target=x86_64-pc-windows-msvc -fuse-ld=lld -std=c++17 -D_ALLOW_COMPILER_AND_STL_VERSION_MISMATCH -O3 -DNDEBUG ^
  -DIS_64BIT -DUSE_POPCNT -DUSE_SSE41 -DUSE_SSSE3 -DUSE_SSE2 -msse4.1 -mssse3 -mpopcnt ^
  -DLARGEBOARDS -DPRECOMPUTED_MAGICS -DNNUE_EMBEDDING_OFF -w -fms-extensions -Xclang -fcxx-exceptions ^
  (same source files) -o fairy-stockfish-largeboard_x86-64-modern.exe
```

### Neural network evaluation (NNUE)

The engine plays with the janggi network `janggi-9991472750de.nnue` from [Fairy-Stockfish-NNUE](https://github.com/fairy-stockfish/Fairy-Stockfish-NNUE) (GPL v3). It also serves `janggicasual` and `janggitraditional`, but only under a file name that starts with `janggi`. The PC copy is `Tools/FairyStockfish/janggi-9991472750de.nnue`. On Android it ships as `Assets/Plugins/Android/libs/arm64-v8a/libjanggi_nnue.so` and is copied once to the app's data folder under its original name. If the network cannot be loaded, the app falls back to the engine's classical evaluation.

## Privacy

The app collects no personal data and sends nothing over the network; see [`PRIVACY.md`](PRIVACY.md).

## License

Seongwon Janggi is licensed under the GNU GPL v3 (see [`LICENSE`](LICENSE)).

Third-party components included in the app or used to build it:

| Component | License | Location |
|---|---|---|
| Fairy-Stockfish | GNU GPL v3 | `Tools/FairyStockfish/Copying.txt`; source: https://github.com/fairy-stockfish/Fairy-Stockfish |
| Fairy-Stockfish janggi NNUE (`janggi-9991472750de.nnue`) | GNU GPL v3 | source: https://github.com/fairy-stockfish/Fairy-Stockfish-NNUE |
| Yuji Boku, Yuji Syuku fonts | SIL Open Font License 1.1 | `Tools/Fonts/OFL-*.txt` |

Fairy-Stockfish runs as a separate process and talks to the app over UCI. Because the APK contains the engine, any distribution of the app must include the GPL v3 notice and point to the engine's source. In the app, these notices are shown at the bottom of the setup dialog.
