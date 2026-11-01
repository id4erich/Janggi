# Fairy-Stockfish

- `fairy-stockfish-largeboard_x86-64-modern.exe` — PC(에디터)·자동 대국 보정용 장기 AI 엔진.
- `janggi-9991472750de.nnue` — 장기 신경망 평가(NNUE). 앱에는 `Assets/Plugins/Android/libs/arm64-v8a/libjanggi_nnue.so`로 같은 파일이 들어감.

- 엔진 소스: https://github.com/fairy-stockfish/Fairy-Stockfish 커밋 `9f778da667f6e07dae1e85d3e2ea204fc6dee94d` (빌드 방법은 저장소 README)
- 신경망 출처: https://github.com/fairy-stockfish/Fairy-Stockfish-NNUE
- 라이선스: 둘 다 GNU GPL v3 — 전문은 같은 폴더의 `Copying.txt`

앱(APK·AAB)에는 안드로이드용 엔진과 신경망이 들어가므로, 배포할 때 GPL v3 고지와 소스 위치를 함께 알려야 한다.
