# 성원장기 · Play 스토어 등록 자료

- 패키지: `com.seongwon.janggi`
- 버전: 0.3.0 (버전 코드 3)
- 카테고리: 게임 › 보드
- 콘텐츠 등급: 전체이용가 (폭력·광고·결제·채팅 없음)
- 개인정보처리방침: 저장소의 [`PRIVACY.md`](../../PRIVACY.md) (스토어에는 이 문서의 공개 URL을 입력)
- 라이선스: GNU GPL v3 · 소스 https://github.com/id4erich/Janggi

## 한국어

**앱 이름 (30자)**
성원장기

**간단한 설명 (80자)**
최강 엔진 Fairy-Stockfish와 두는 3D 장기. 1~9단 AI, 복기·기보 공유, 광고 없음.

**자세한 설명**
나무 장기판과 회양목 말을 3D로 살린 장기 앱입니다. 광고도, 회원 가입도, 인터넷 연결도 필요 없습니다.

■ 강한 AI, 1단부터 9단까지
· 세계적인 장기 엔진 Fairy-Stockfish와 장기 신경망(NNUE)이 휴대폰 안에서 직접 수를 읽습니다.
· 입문자용 1단부터 엔진 최대 실력인 9단까지 고를 수 있습니다.

■ 실제 장기 같은 손맛
· 말을 "탁" 내려놓는 나무 소리와 진동, 잡힌 말이 밀려나는 연출
· 누르고 누르기, 손가락으로 끌어다 놓기 모두 지원
· 위에서 내려다보기와 비스듬히 보기 시점 전환

■ 규칙과 대국 방식
· 규칙: 일반(빅장 없음), 빅장 = 무승부, 대회(점수 판정)
· 차림: 마상마상·상마상마·외상마·양귀마
· AI와 초로 두기, 한으로 두기, 둘이서 두기
· 한 수 제한 시간(30초·60초)

■ 공부에 도움이 되는 기능
· 힌트: 엔진이 추천하는 수를 판 위에 표시
· 기보 보기와 공유(카카오톡·메모 등)
· 복기: 둔 수를 앞뒤로 넘겨 보며 국면마다 엔진 추천 수 확인
· 무르기, 대국 저장 후 이어 두기, 장군 표시, 전적

성원장기는 GNU GPL v3로 공개된 오픈 소스입니다.

## English

**App name**
Seongwon Janggi

**Short description (80 chars)**
3D Korean chess vs the Fairy-Stockfish engine. 9 AI levels, review, no ads.

**Full description**
Janggi (Korean chess) with a 3D wooden board and boxwood pieces. No ads, no sign-up, no internet connection needed.

■ A strong AI, from beginner to master
· The Fairy-Stockfish engine with a janggi neural network (NNUE) runs entirely on your device.
· Nine levels, from a gentle level 1 to the engine's full strength at level 9.

■ The feel of real pieces
· Wooden "clack" sounds and haptics, captured pieces knocked aside
· Tap-to-move or drag-and-drop
· Top-down and tilted camera views

■ Rules and modes
· Rules: casual (no bikjang), bikjang = draw, tournament (point count)
· All four opening setups
· Play as Cho or Han against the AI, or two players on one device
· Optional 30 s / 60 s move timer

■ Study tools
· Hints from the engine shown on the board
· Move record with sharing
· Review mode: step through the game and see the engine's move for any position
· Undo, auto-save and resume, check marker, win/loss record

Seongwon Janggi is open source under the GNU GPL v3.

## 스크린샷

`screenshots/`의 태블릿 화면(1600×2560)은 "10인치 태블릿" 칸에 올립니다. 휴대폰 칸에는 최소 2장이 필요하므로, 휴대폰(예: 갤럭시 S10)에서 같은 장면을 찍어 추가합니다.

| 파일 | 장면 |
|---|---|
| `tablet-1-game.png` | AI 9단과 대국 중 (생각 진행 막대) |
| `tablet-2-setup.png` | 대국 설정: 방식·규칙·차림·AI 수준·제한 시간 |
| `tablet-3-review.png` | 복기: 국면별 엔진 추천 수 |
| `tablet-4-record.png` | 기보와 공유·복기 버튼 |

## 출시 빌드 순서

1. Unity를 켜고 Player Settings → Publishing Settings에 키스토어·키 비밀번호를 입력
2. Build Profiles에서 **Build App Bundle (Google Play)** 를 켜고 Build → `성원장기.aab`
3. Play Console 내부 테스트 트랙에 올려 휴대폰에서 받아 확인한 뒤 프로덕션으로 승격
