# 성원장기

[English](README.md) | **한국어**

[![Rule check](https://github.com/id4erich/Janggi/actions/workflows/rule-check.yml/badge.svg)](https://github.com/id4erich/Janggi/actions/workflows/rule-check.yml)

Unity로 만든 한국 장기 앱입니다. Android(arm64)용으로 빌드하며, AI 상대로 Fairy-Stockfish 엔진을 씁니다.

## 기능

- 한국 장기 규칙
  - 궁성 대각선 이동(궁·사·차·포·졸)
  - 장군을 방치하는 수 금지, 외통 판정
  - 한수 쉼, 무르기, 기권
- 대국 방식: AI와(나는 초), AI와(나는 한 · AI 선공, 판을 돌려 내 진영이 아래), 둘이서(한 기기)
- 효과음과 음성 대사를 따로 켜고 끔(메뉴 → 설정)
- 마상 차림 4종 선택(초·한 각각)
- AI 1~9단: Fairy-Stockfish를 쓰고, 엔진을 실행할 수 없으면 내장 알파베타 AI로 대체
- 3D 화면: 모따기한 8각 회양목 기물, 음각 붓글씨, 나뭇결 장기판, 화면 폭을 채우는 바로 위 시점("시점" 버튼으로 비스듬한 입체 시점과 전환), 부드러운 그림자
- 위아래 대국자 패널(차례 강조, AI 단, 남은 시간, 전적)과 하단 도구 막대(무르기·한수 쉼·힌트·시점·메뉴). 기권·나가기는 메뉴 안에서 한 번 더 확인
- 두던 대국은 수마다 저장되어 앱을 다시 켜면 이어서 둠(무르기 기록 포함)
- 장군을 받으면 궁 아래 붉은 고리로 표시, "힌트" 버튼은 엔진 추천 수를 화살표로 보여 줌
- 기보 보기(메뉴 → 기보): 번호·진영·기물·좌표·잡은 말을 목록으로
- 규칙 선택: Fairy-Stockfish의 장기 변형과 1:1로 맞춰 AI도 같은 규칙으로 둠
  - 일반(`janggicasual`): 빅장 없음, 연속 한수 쉼은 무승부
  - 빅장 무승부(`janggitraditional`): 두 궁이 마주 보면 상대는 빅장을 풀거나 한수 쉼으로 받아들이고, 받아들이면 무승부
  - 대회(`janggi`): 빅장·연속 한수 쉼으로 끝나면 점수 판정(차13·포7·마5·상3·사3·졸2, 한 1.5점 덤)
- 한 수 제한 시간 선택(제한 없음/30초/60초), 시간을 넘기면 시간패
- 잡은 말은 판 옆 탁자에 진열(초가 잡은 말은 아래쪽, 한이 잡은 말은 위쪽)
- 음성 대사("장이야!" 등)는 기기에 내장된 TextToSpeech로 재생(오프라인)

## 폴더 구조

| 경로 | 내용 |
|---|---|
| `Assets/Scripts/Core` | 규칙(`RuleValidator`), 게임 진행(`GameManager`), 기본 타입 |
| `Assets/Scripts/AI` | Fairy-Stockfish 연동(`FairyStockfishEngine`), 내장 AI |
| `Assets/Scripts/View` | 장기판·기물 표시, 기물 메시 생성 |
| `Assets/Scripts/UI` | 모바일 UI, 카메라 |
| `Assets/Scripts/Editor` | APK 빌드 메뉴, EditMode 테스트 |
| `Assets/Plugins/Android/libs/arm64-v8a` | Android용 Fairy-Stockfish 실행 파일 |
| `Assets/Plugins/Android/AndroidManifest.xml` | 커스텀 메인 매니페스트(기본 구성 + Android 11 이상에서 TTS 엔진 조회 선언) |
| `Assets/Resources/PieceArt`, `BoardArt` | 미리 구운 기물·장기판 텍스처와 재질 |
| `Tools/FairyStockfish` | PC용 엔진, AI 단 보정 스크립트 |
| `Tools/RuleCheck` | 규칙 엔진을 Fairy-Stockfish의 수 생성과 대조하는 도구 |
| `Tools/PieceArt` | 기물·장기판 텍스처를 굽는 스크립트 |
| `Tools/Fonts` | 텍스처를 구울 때 쓰는 붓글씨 글꼴(앱에는 포함되지 않음) |

## 개발 환경

- Unity 6000.3.16f1 (URP, Android 빌드 모듈)
- 스크립트 실행용 Python 3 + Pillow, NumPy

## 빌드와 실행

- **에디터**: `Assets/Scenes/SampleScene.unity`를 열고 플레이합니다. PC에서는 `Tools/FairyStockfish`의 Windows용 엔진을 씁니다.
- **APK**: 메뉴에서 **Build → Build Android APK**를 실행하면 `Builds/Android/성원장기.apk`가 만들어집니다.
- **테스트**: **Window → General → Test Runner**의 EditMode 탭에서 실행합니다. 규칙, 게임 진행, AI, 엔진 연동을 검사합니다.

## Fairy-Stockfish와 규칙 대조

`Tools/RuleCheck`는 무작위 대국을 두면서 국면마다 앱의 `RuleValidator`가 계산한 둘 수 있는 수 전체를 Fairy-Stockfish의 `go perft 1`(janggicasual)과 비교합니다. 한수 쉼은 앱에서 별도 버튼이라 비교에서 뺍니다.

```bash
cd Tools/RuleCheck && dotnet run -c Release -- 2026 200
```

인자는 무작위 시드, 판 수, 그리고 선택으로 변형(`janggicasual`, `janggitraditional`, `janggi`)입니다. 작성 시점에 260판(약 3만 8천 국면)에서 모두 일치했습니다.

## AI 단 보정

단별 설정표는 `GameManager.AiLevels`에 있습니다. 인접한 단끼리 PC 엔진으로 자동 대국을 시켜 승률과 Elo 차이를 잽니다.

```bash
python Tools/FairyStockfish/calibrate_levels.py --games 24 --time-scale 0.4
```

`--time-scale 0.4`는 PC를 휴대폰(갤럭시 S10) 속도에 가깝게 맞추는 값입니다. 단 대신 엔진 실력 단계를 직접 비교하려면 `--ladder="-10:1000,-5:1000,0:1000"` 형식을 씁니다.

## 기물·장기판 텍스처 다시 굽기

```bash
python Tools/PieceArt/bake_piece_textures.py
```

```bash
python Tools/PieceArt/bake_board_texture.py
```

결과물은 `Assets/Resources/PieceArt`, `Assets/Resources/BoardArt`에 저장됩니다. 노멀맵(`*_n.png`)은 Unity에서 Texture Type을 Normal map으로 설정해야 합니다.

## Android(arm64)용 엔진 다시 빌드하기

[Fairy-Stockfish](https://github.com/fairy-stockfish/Fairy-Stockfish) `fairy_sf_14` 소스의 `src` 폴더에서, Unity에 들어 있는 NDK(r27c) clang으로 빌드합니다.

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

빌드한 뒤 `llvm-strip`으로 디버그 정보를 지우고 `Assets/Plugins/Android/libs/arm64-v8a/`에 넣습니다. Android는 앱 데이터 폴더의 파일을 실행하지 못하게 막기 때문에, 엔진을 `.so` 이름으로 넣어 설치할 때 네이티브 라이브러리 폴더에 풀리게 합니다. 앱은 실행 중에 이 폴더에서 엔진을 찾아 `Runtime.exec`로 실행합니다.

## 개인정보

앱은 개인정보를 수집하지 않고 인터넷으로 아무것도 보내지 않습니다. 자세한 내용은 [`PRIVACY.md`](PRIVACY.md)를 보세요.

## 라이선스

성원장기 앱 코드: GNU GPL v3 (`LICENSE`).

이 앱에 들어 있거나 앱을 만드는 데 쓰는 외부 구성 요소의 라이선스입니다.

| 구성 요소 | 라이선스 | 위치 |
|---|---|---|
| Fairy-Stockfish | GNU GPL v3 | `Tools/FairyStockfish/Copying.txt`, 소스: https://github.com/fairy-stockfish/Fairy-Stockfish |
| Yuji Boku, Yuji Syuku 글꼴 | SIL Open Font License 1.1 | `Tools/Fonts/OFL-*.txt` |

Fairy-Stockfish는 앱과 별도 프로세스로 실행되며 UCI로 통신합니다. APK에 엔진이 들어 있으므로, 앱을 배포할 때 GPL v3 고지와 엔진 소스 위치를 함께 알려야 합니다. 앱 안에서는 차림 설정 창 아래쪽에 표시됩니다.

