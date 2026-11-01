using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Janggi
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public Team CurrentTurn { get; private set; } = Team.Cho;
        public PieceData[,] Board { get; private set; } = new PieceData[9, 10];
        public bool IsGameOver { get; private set; }

        [Header("AI Settings")]
        public bool isPlayerVsAI = true;
        public Team aiTeam = Team.Han;
        [Tooltip("AI 수준 1~9단. 차림 설정 창에서 고른 값이 PlayerPrefs에 저장되어 우선함")]
        [Range(MinAiLevel, MaxAiLevel)] public int aiLevel = 5;
        public const int MinAiLevel = 1, MaxAiLevel = 9;
        public const string KeyAiLevel = "Janggi_AI_Level";

        // 단별 (Fairy-Stockfish Skill Level, 한 수 생각 시간 ms, 엔진이 없을 때 내장 AI 수읽기 깊이)
        // 장기 신경망(NNUE)을 쓰는 엔진 기준. Tools/FairyStockfish/calibrate_levels.py 자동 대국(10판씩) 측정:
        // 실력 단계는 -6부터 2단계마다 약 +250~+500 Elo로 가팔라짐 (그 아래는 완만).
        // 1단(-20, 0.05초)→2단(-12)→3단(-6)은 각각 약 +120·+150 Elo(12판씩 측정)로 입문자도 단계를 밟을 수 있게 하고,
        // 4~6단은 2단계씩, 7~9단은 시간을 늘리며 최대 실력(20)까지 올림.
        // 9단은 엔진 최대 실력. 내장 AI 깊이는 보정하지 않은 추정값
        private static readonly (int Skill, int TimeMs, int Depth)[] AiLevels =
        {
            (-20, 50, 1), (-12, 1000, 2), (-6, 1000, 2), (-4, 1000, 3), (-2, 1000, 3),
            (0, 1500, 4), (3, 1500, 5), (8, 2000, 6), (20, 3000, 6)
        };

        public static (int Skill, int TimeMs, int Depth) GetAiLevelSettings(int level)
            => AiLevels[Mathf.Clamp(level, MinAiLevel, MaxAiLevel) - 1];

        [Header("Fairy-Stockfish (GPL v3, PC·Android arm64)")]
        public bool useFairyStockfish = false;
        [Tooltip("PC용, 프로젝트 루트 기준 경로 (Android는 앱에 포함된 엔진을 자동 사용)")]
        public string fairyStockfishPath = "Tools/FairyStockfish/fairy-stockfish-largeboard_x86-64-modern.exe";

        private FairyStockfishEngine fsEngine;
        private bool fsFailed;

        public event Action<BoardPos, BoardPos> OnPieceMoved;

        public PlacementType choPlacement = PlacementType.MaSangMaSang;
        public PlacementType hanPlacement = PlacementType.MaSangMaSang;

        public enum GameMode { VsAiAsCho = 0, VsAiAsHan = 1, TwoPlayers = 2 }

        // 규칙: Fairy-Stockfish 장기 변형과 1:1로 맞춰 AI도 같은 규칙으로 둠
        //  Casual      janggicasual      빅장 없음, 연속 한수 쉼 = 무승부
        //  Traditional janggitraditional 빅장을 받아들이면(한수 쉼) 무승부
        //  Tournament  janggi            빅장·연속 한수 쉼으로 끝나면 점수 판정 (한 1.5점 덤)
        public enum RuleSet { Casual = 0, Traditional = 1, Tournament = 2 }
        public const string KeyRuleSet = "Janggi_RuleSet";
        public RuleSet ruleSet = RuleSet.Casual;
        public bool BikjangRule => ruleSet != RuleSet.Casual;
        public string EngineVariant => ruleSet == RuleSet.Tournament ? "janggi" : ruleSet == RuleSet.Traditional ? "janggitraditional" : "janggicasual";

        // 점수 판정용 기물 점수 (궁 제외)
        public int MaterialPoints(Team team)
        {
            int points = 0;
            foreach (var p in Board)
            {
                if (p == null || p.Team != team) continue;
                switch (p.Type)
                {
                    case PieceType.Car: points += 13; break;
                    case PieceType.Cannon: points += 7; break;
                    case PieceType.Horse: points += 5; break;
                    case PieceType.Elephant:
                    case PieceType.Guard: points += 3; break;
                    case PieceType.Pawn: points += 2; break;
                }
            }
            return points;
        }
        public const string KeyGameMode = "Janggi_GameMode";

        public GameMode Mode => !isPlayerVsAI ? GameMode.TwoPlayers : aiTeam == Team.Han ? GameMode.VsAiAsCho : GameMode.VsAiAsHan;

        public void ApplyMode(GameMode mode)
        {
            isPlayerVsAI = mode != GameMode.TwoPlayers;
            aiTeam = mode == GameMode.VsAiAsHan ? Team.Cho : Team.Han;
        }

        // 내가 한(漢)으로 두면 판을 180도 돌려 내 진영이 화면 아래에 오게 함
        public bool ViewFlipped => isPlayerVsAI && aiTeam == Team.Cho;

        [Tooltip("한 수 제한 시간(초). 0이면 제한 없음, 시간을 넘기면 시간패")]
        public int moveTimeLimitSec = 0;
        public const string KeyMoveTimeLimit = "Janggi_MoveTimeLimit";
        public const string KeySavedGame = "Janggi_SavedGame";

        // 저장 중인 수를 다시 두는 동안 화면 이벤트·AI·저장을 끔
        private bool replaying;
        private bool persistEnabled; // 실제 게임 인스턴스(Awake 실행)만 저장. 에디터 테스트가 저장값을 덮어쓰지 않게
        private Coroutine hintRoutine;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                persistEnabled = true;
                choPlacement = (PlacementType)PlayerPrefs.GetInt("Janggi_Cho_Placement", (int)PlacementType.MaSangMaSang);
                hanPlacement = (PlacementType)PlayerPrefs.GetInt("Janggi_Han_Placement", (int)PlacementType.MaSangMaSang);
                aiLevel = PlayerPrefs.GetInt(KeyAiLevel, aiLevel);
                moveTimeLimitSec = PlayerPrefs.GetInt(KeyMoveTimeLimit, moveTimeLimitSec);
                ApplyMode((GameMode)PlayerPrefs.GetInt(KeyGameMode, (int)Mode));
                ruleSet = (RuleSet)PlayerPrefs.GetInt(KeyRuleSet, (int)ruleSet);
                InitializeBoard(choPlacement, hanPlacement);

                // 앱이 꺼졌다 켜지면 두던 대국을 이어 둠
                string saved = PlayerPrefs.GetString(KeySavedGame, "");
                bool loaded = false;
                if (saved.Length > 0)
                {
                    try { loaded = LoadFromJson(saved); }
                    catch (Exception e) { Debug.LogWarning($"저장된 대국을 불러오지 못함: {e.Message}"); }
                }
                if (saved.Length > 0 && !loaded)
                {
                    PlayerPrefs.DeleteKey(KeySavedGame);
                    choPlacement = (PlacementType)PlayerPrefs.GetInt("Janggi_Cho_Placement", (int)PlacementType.MaSangMaSang);
                    hanPlacement = (PlacementType)PlayerPrefs.GetInt("Janggi_Han_Placement", (int)PlacementType.MaSangMaSang);
                    ApplyMode((GameMode)PlayerPrefs.GetInt(KeyGameMode, (int)GameMode.VsAiAsCho));
                    InitializeBoard(choPlacement, hanPlacement);
                }
            }
            else Destroy(gameObject);
        }

        private void Start()
        {
            // AI가 생각하던 중에 앱이 꺼졌다면 이어서 AI 차례를 진행
            if (!IsGameOver && !IsHumanTurn) aiRoutine = StartCoroutine(ProcessAITurn());
        }

        public void StartGame(PlacementType cho, PlacementType han)
        {
            StopAiRoutines();
            choPlacement = cho;
            hanPlacement = han;
            InitializeBoard(choPlacement, hanPlacement);
            PersistGame();
            if (!IsHumanTurn) aiRoutine = StartCoroutine(ProcessAITurn()); // AI가 초(선공)일 때
        }

        private void StopAiRoutines()
        {
            if (aiRoutine != null) { StopCoroutine(aiRoutine); aiRoutine = null; }
            if (hintRoutine != null) { StopCoroutine(hintRoutine); hintRoutine = null; }
            aiThinkStart = -1f; // 멈춘 코루틴은 finally가 돌지 않으므로 여기서 진행 막대를 거둠
        }

        // ── 대국 저장·이어 하기 ──────────────────────────────
        // 시작 차림과 둔 수 목록만 저장하고, 불러올 때 같은 수를 다시 둬서 무르기 기록까지 복원

        [Serializable]
        private class SavedGame
        {
            public int cho, han, mode, rules;
            public List<int> moves = new List<int>(); // 한수 쉼 = -1, 그 외 from + to * 100 (칸 번호 = x + y * 9)
        }

        public string SaveToJson()
        {
            var game = new SavedGame { cho = (int)choPlacement, han = (int)hanPlacement, mode = (int)Mode, rules = (int)ruleSet };
            MoveRecord[] records = historyStack.ToArray();
            Array.Reverse(records); // Stack은 마지막 수부터 나오므로 처음 수부터로 뒤집음
            foreach (var r in records)
                game.moves.Add(r.MovedPiece == null ? -1 : (r.From.x + r.From.y * 9) + (r.To.x + r.To.y * 9) * 100);
            return JsonUtility.ToJson(game);
        }

        public bool LoadFromJson(string json)
        {
            SavedGame game;
            try { game = JsonUtility.FromJson<SavedGame>(json); }
            catch (Exception) { return false; }
            if (game == null) return false;

            choPlacement = (PlacementType)game.cho;
            hanPlacement = (PlacementType)game.han;
            ApplyMode((GameMode)game.mode);
            ruleSet = (RuleSet)game.rules;
            InitializeBoard(choPlacement, hanPlacement);
            replaying = true;
            try
            {
                foreach (int m in game.moves)
                {
                    if (IsGameOver) return false;
                    if (m < 0)
                    {
                        int before = historyStack.Count;
                        PassTurn();
                        if (historyStack.Count == before) return false;
                        continue;
                    }
                    int from = m % 100, to = m / 100;
                    if (from >= 90 || to >= 90) return false; // 판 밖 좌표 = 손상된 저장값
                    if (!MakeMove(new BoardPos(from % 9, from / 9), new BoardPos(to % 9, to / 9))) return false;
                }
            }
            finally { replaying = false; }
            return !IsGameOver;
        }

        private void PersistGame()
        {
            if (replaying || !persistEnabled || IsReviewing) return;
            if (IsGameOver) PlayerPrefs.DeleteKey(KeySavedGame); // 끝난 대국은 이어 두지 않음
            else PlayerPrefs.SetString(KeySavedGame, SaveToJson());
            PlayerPrefs.Save();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) PlayerPrefs.Save();
        }

        // ── 복기 ──────────────────────────────────────────
        // 둔 수를 처음부터 원하는 수까지 다시 둬서 그 국면을 보여 줌. 복기 중에는 착수·AI·저장을 막고, 끝내면 원래 국면으로 돌아옴

        public bool IsReviewing { get; private set; }
        public int ReviewPly { get; private set; }
        public int ReviewTotal { get; private set; }
        private string reviewSaved;

        public bool EnterReview()
        {
            if (IsReviewing || historyStack.Count == 0) return false;
            StopAiRoutines();
            reviewSaved = SaveToJson();
            ReviewTotal = historyStack.Count;
            IsReviewing = true;
            ReviewTo(ReviewTotal);
            return true;
        }

        public void ReviewTo(int ply)
        {
            if (!IsReviewing) return;
            ReviewPly = Mathf.Clamp(ply, 0, ReviewTotal);
            var game = JsonUtility.FromJson<SavedGame>(reviewSaved);
            game.moves.RemoveRange(ReviewPly, game.moves.Count - ReviewPly);
            LoadFromJson(JsonUtility.ToJson(game)); // 끝난 판의 마지막 수는 false를 돌려주지만 판은 그 국면이 됨
            RefreshAfterReplay();
        }

        public void ExitReview()
        {
            if (!IsReviewing) return;
            LoadFromJson(reviewSaved);
            IsReviewing = false;
            RefreshAfterReplay();
            if (!IsGameOver && !IsHumanTurn) aiRoutine = StartCoroutine(ProcessAITurn());
        }

        private void RefreshAfterReplay()
        {
            if (MobileJanggiUIManager.Instance != null) MobileJanggiUIManager.Instance.UpdateTurnDisplay(CurrentTurn);
            OnBoardReset?.Invoke();
        }

        // 공유용 기보 글: 날짜·대국 방식·규칙·차림 머리말 + 둔 수
        public string GetShareText()
        {
            string[] modes = { "AI와 (나는 초)", "AI와 (나는 한)", "둘이서" };
            string[] rules = { "일반", "빅장 무승부", "대회(점수 판정)" };
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"성원장기 기보 · {DateTime.Now:yyyy-MM-dd HH:mm}");
            sb.AppendLine($"{modes[(int)Mode]}{(isPlayerVsAI ? $" · AI {aiLevel}단" : "")} · 규칙 {rules[(int)ruleSet]}");
            string[] placements = { "마상마상", "상마상마", "외상마", "양귀마" }; // PlacementType 순서
            sb.AppendLine($"차림: 초 {placements[(int)choPlacement]} / 한 {placements[(int)hanPlacement]} · 좌표는 초 쪽 왼쪽 아래가 1,1");
            foreach (string line in GetMoveRecordLines()) sb.AppendLine(line);
            if (IsGameOver) sb.AppendLine("(대국 종료)");
            return sb.ToString();
        }

        // ── 기보 ──────────────────────────────────────────
        // 좌표는 초(楚) 쪽 왼쪽 아래를 1,1로 한 (가로,세로). 예: "3. 초 차 1,1→1,7 ×병"

        public List<string> GetMoveRecordLines()
        {
            MoveRecord[] records = historyStack.ToArray();
            Array.Reverse(records);
            var lines = new List<string>(records.Length);
            for (int i = 0; i < records.Length; i++) lines.Add(Notate(records[i], i + 1));
            return lines;
        }

        public static string Notate(MoveRecord r, int number)
        {
            string side = r.Mover == Team.Cho ? "초" : "한";
            if (r.MovedPiece == null) return $"{number}. {side} 한수 쉼";
            string capture = r.CapturedPiece != null ? $" ×{PieceName(r.CapturedPiece)}" : "";
            return $"{number}. {side} {PieceName(r.MovedPiece)} {r.From.x + 1},{r.From.y + 1}→{r.To.x + 1},{r.To.y + 1}{capture}";
        }

        private static string PieceName(PieceData p)
        {
            switch (p.Type)
            {
                case PieceType.King: return "궁";
                case PieceType.Guard: return "사";
                case PieceType.Car: return "차";
                case PieceType.Cannon: return "포";
                case PieceType.Horse: return "마";
                case PieceType.Elephant: return "상";
                default: return p.Team == Team.Cho ? "졸" : "병";
            }
        }

        // ── 힌트 ──────────────────────────────────────────
        // 사람 차례에 엔진 최고 실력으로 짧게 읽어 추천 수를 돌려줌 (한수 쉼 추천이면 From == To)
        public bool RequestHint(Action<Move> onResult)
        {
            // 복기 중에는 어느 쪽 차례든 그 국면의 추천 수를 볼 수 있음
            bool allowed = !IsGameOver && (IsReviewing || IsHumanTurn);
            if (!allowed || aiRoutine != null || hintRoutine != null) return false;
            hintRoutine = StartCoroutine(HintRoutine(onResult));
            return true;
        }

        private IEnumerator HintRoutine(Action<Move> onResult)
        {
            int moveCount = historyStack.Count;
            PieceData[,] snapshot = CloneBoard(Board);
            Team team = CurrentTurn;
            string variant = EngineVariant;
            FairyStockfishEngine engine = GetFairyStockfish();
            var task = System.Threading.Tasks.Task.Run(() => engine != null
                ? engine.GetBestMove(snapshot, team, variant, 20, 800)
                : JanggiAIHighLevel.GetBestMove(snapshot, team, 4, 800));
            while (!task.IsCompleted) yield return null;
            hintRoutine = null;
            // 계산하는 사이 수가 바뀌었으면 옛 국면의 추천은 버림
            if (task.IsFaulted || historyStack.Count != moveCount || CurrentTurn != team) yield break;
            onResult(task.Result);
        }

        // ── 제한 시간 ──────────────────────────────────────
        public void TimeOut()
        {
            if (IsGameOver || moveTimeLimitSec <= 0 || !IsHumanTurn) return;
            EndGame(CurrentTurn == Team.Cho ? Team.Han : Team.Cho, "시간 초과");
        }

        public void InitializeBoard(PlacementType choPlacement, PlacementType hanPlacement)
        {
            Board = new PieceData[9, 10];

            // 1. 초(Cho) 기물 배치 (y=0~3)
            SetupTeamPieces(Team.Cho, 0, 1, 2, 3, choPlacement);
            // 2. 한(Han) 기물 배치 (y=9~6)
            SetupTeamPieces(Team.Han, 9, 8, 7, 6, hanPlacement);

            CurrentTurn = Team.Cho;
            IsGameOver = false;
            historyStack.Clear(); // 새 대국에서 무르기가 이전 대국으로 넘어가지 않게
        }

        private void SetupTeamPieces(Team team, int rowKingLine, int rowPalaceFront, int rowPawnLine, int rowCannonLine, PlacementType placement)
        {
            int cannonRow = (team == Team.Cho) ? 2 : 7;
            int pawnRow = (team == Team.Cho) ? 3 : 6;

            // 궁 및 사
            Board[4, rowKingLine + ((team == Team.Cho) ? 1 : -1)] = new PieceData(PieceType.King, team, new BoardPos(4, rowKingLine + ((team == Team.Cho) ? 1 : -1)));
            Board[3, rowKingLine] = new PieceData(PieceType.Guard, team, new BoardPos(3, rowKingLine));
            Board[5, rowKingLine] = new PieceData(PieceType.Guard, team, new BoardPos(5, rowKingLine));

            // 차 (0, 8)
            Board[0, rowKingLine] = new PieceData(PieceType.Car, team, new BoardPos(0, rowKingLine));
            Board[8, rowKingLine] = new PieceData(PieceType.Car, team, new BoardPos(8, rowKingLine));

            // 포 (1, 7)
            Board[1, cannonRow] = new PieceData(PieceType.Cannon, team, new BoardPos(1, cannonRow));
            Board[7, cannonRow] = new PieceData(PieceType.Cannon, team, new BoardPos(7, cannonRow));

            // 졸/병 (0, 2, 4, 6, 8)
            for (int x = 0; x < 9; x += 2)
            {
                Board[x, pawnRow] = new PieceData(PieceType.Pawn, team, new BoardPos(x, pawnRow));
            }

            // 상 & 마 배치 (PlacementType에 따름)
            SetupHorseElephant(team, rowKingLine, placement);
        }

        private void SetupHorseElephant(Team team, int y, PlacementType placement)
        {
            PieceType p1, p2, p3, p4;

            switch (placement)
            {
                case PlacementType.SangMaSangMa: // 상마상마
                    p1 = PieceType.Elephant; p2 = PieceType.Horse; p3 = PieceType.Elephant; p4 = PieceType.Horse;
                    break;
                case PlacementType.MaSangSangMa: // 외상마
                    p1 = PieceType.Horse; p2 = PieceType.Elephant; p3 = PieceType.Elephant; p4 = PieceType.Horse;
                    break;
                case PlacementType.SangMaMaSang: // 양귀마
                    p1 = PieceType.Elephant; p2 = PieceType.Horse; p3 = PieceType.Horse; p4 = PieceType.Elephant;
                    break;
                case PlacementType.MaSangMaSang: // 마상마상 (기본)
                default:
                    p1 = PieceType.Horse; p2 = PieceType.Elephant; p3 = PieceType.Horse; p4 = PieceType.Elephant;
                    break;
            }

            Board[1, y] = new PieceData(p1, team, new BoardPos(1, y));
            Board[2, y] = new PieceData(p2, team, new BoardPos(2, y));
            Board[6, y] = new PieceData(p3, team, new BoardPos(6, y));
            Board[7, y] = new PieceData(p4, team, new BoardPos(7, y));
        }

        public struct MoveRecord
        {
            public BoardPos From;
            public BoardPos To;
            public PieceData MovedPiece;
            public PieceData CapturedPiece;
            public Team Mover; // 한수 쉼은 MovedPiece == null
        }

        private Coroutine aiRoutine;

        public bool IsHumanTurn => !(isPlayerVsAI && CurrentTurn == aiTeam);

        private Stack<MoveRecord> historyStack = new Stack<MoveRecord>();
        public event Action OnBoardReset;

        public bool MakeMove(BoardPos from, BoardPos to)
        {
            if (IsGameOver) return false;

            PieceData p = Board[from.x, from.y];
            if (p == null || p.Team != CurrentTurn) return false;

            var validMoves = RuleValidator.GetLegalMoves(p, Board, BikjangRule);
            if (!validMoves.Contains(to)) return false;

            PieceData target = Board[to.x, to.y];
            bool isKingCaptured = false;
            if (target != null)
            {
                target.IsCaptured = true;
                if (target.Type == PieceType.King)
                {
                    isKingCaptured = true;
                }
            }

            // 히스토리 기록
            historyStack.Push(new MoveRecord { From = from, To = to, MovedPiece = p, CapturedPiece = target, Mover = p.Team });

            Board[to.x, to.y] = p;
            Board[from.x, from.y] = null;
            p.Position = to;

            if (!replaying) OnPieceMoved?.Invoke(from, to);

            // 1. 궁(King) 잡힘 시 즉시 승부 결정 종료!
            if (isKingCaptured)
            {
                EndGame(p.Team, "궁 잡음");
                return true;
            }

            // 2. 장이야! / 멍이야! 상태 판정 및 텍스트 팝업 알림
            Team opponentTeam = (CurrentTurn == Team.Cho) ? Team.Han : Team.Cho;
            bool isCheck = RuleValidator.IsInCheck(Board, opponentTeam);

            if (RuleValidator.IsCheckmate(Board, opponentTeam, BikjangRule))
            {
                EndGame(p.Team, "외통");
                return true;
            }

            if (isCheck && !replaying)
            {
                if (MobileJanggiUIManager.Instance != null)
                {
                    MobileJanggiUIManager.Instance.ShowJangiyoBanner((p.Team == Team.Cho) ? "⚔️ 초(楚): 장이야!" : "⚔️ 한(漢): 장이야!");
                }
            }

            // 턴 전환
            EndTurn();
            PersistGame();
            return true;
        }

        public void PassTurn()
        {
            if (IsGameOver) return;
            bool bikjang = BikjangRule && RuleValidator.KingsFacing(Board);
            if (RuleValidator.IsInCheck(Board, CurrentTurn) && !bikjang) // 빅장 중에는 장군이어도 한수 쉼 가능
            {
                Debug.Log("장군 상태에서는 한수 쉼을 할 수 없습니다.");
                return;
            }

            if (!replaying) Debug.Log($"{CurrentTurn} 패스(한수 쉼)");
            bool previousWasPass = historyStack.Count > 0 && historyStack.Peek().MovedPiece == null;
            historyStack.Push(new MoveRecord { Mover = CurrentTurn });

            // 빅장을 한수 쉼으로 받아들이거나, 두 사람이 연달아 한수 쉼을 두면 대국 종료
            if (bikjang || previousWasPass)
            {
                EndByRule(bikjang ? "빅장" : "연속 한수 쉼");
                return;
            }
            EndTurn();
            PersistGame();
        }

        // 빅장·연속 한수 쉼으로 끝난 대국: 대회 규칙은 점수 판정, 나머지는 무승부
        private void EndByRule(string reason)
        {
            if (ruleSet == RuleSet.Tournament)
            {
                int cho = MaterialPoints(Team.Cho), han = MaterialPoints(Team.Han);
                // 한(후공)에 1.5점 덤: 점수가 같거나 초가 1점 앞서도 한 승
                Team winner = cho - han - 1 > 0 ? Team.Cho : Team.Han;
                EndGame(winner, $"{reason} · 점수 초 {cho} : 한 {han + 1.5f:0.#}");
            }
            else EndDraw(reason);
        }

        private void EndDraw(string reason)
        {
            IsGameOver = true;
            StopAiRoutines();
            PersistGame();
            if (replaying) return;
            if (MobileJanggiUIManager.Instance != null)
                MobileJanggiUIManager.Instance.ShowGameOverDialog($"{reason}으로 끝남", false, isDraw: true); // reason: 빅장·연속 한수 쉼 (받침으로 끝남)
        }

        // 되돌릴 사람의 수가 있을 때만 (AI가 먼저 둔 첫 수만 있으면 되돌려도 AI가 다시 둘 뿐)
        public bool CanUndo
        {
            get
            {
                if (IsGameOver) return false;
                foreach (var r in historyStack)
                    if (!isPlayerVsAI || r.Mover != aiTeam) return true;
                return false;
            }
        }

        public bool UndoMove()
        {
            if (!CanUndo) return false;

            // AI가 아직 두기 전이면 대기 중인 AI 차례(와 힌트 계산)를 취소
            StopAiRoutines();

            // Player 대 AI 대전 시 사람 차례가 될 때까지 되돌림 (한수 쉼 포함)
            do
            {
                MoveRecord rec = historyStack.Pop();
                CurrentTurn = rec.Mover;
                if (rec.MovedPiece == null) continue;

                Board[rec.From.x, rec.From.y] = rec.MovedPiece;
                rec.MovedPiece.Position = rec.From;

                Board[rec.To.x, rec.To.y] = rec.CapturedPiece;
                if (rec.CapturedPiece != null)
                {
                    rec.CapturedPiece.IsCaptured = false;
                    rec.CapturedPiece.Position = rec.To;
                }
            } while (!IsHumanTurn && historyStack.Count > 0);

            if (MobileJanggiUIManager.Instance != null)
            {
                MobileJanggiUIManager.Instance.UpdateTurnDisplay(CurrentTurn);
            }

            OnBoardReset?.Invoke();

            // AI가 선공(초)인 대국에서 첫 수까지 되돌린 경우 AI가 다시 둠
            if (!IsHumanTurn) aiRoutine = StartCoroutine(ProcessAITurn());
            PersistGame();
            return true;
        }

        public void Resign()
        {
            if (IsGameOver) return;
            // AI 대전에서는 언제 누르든 사람이 기권
            Team loser = isPlayerVsAI ? (aiTeam == Team.Cho ? Team.Han : Team.Cho) : CurrentTurn;
            Debug.Log($"{loser} 기권 선언! 대국 종료.");
            EndGame(loser == Team.Cho ? Team.Han : Team.Cho, "기권");
        }

        private void EndGame(Team winner, string reason)
        {
            IsGameOver = true;
            StopAiRoutines();
            PersistGame();
            if (replaying) return;

            string who = (winner == Team.Cho) ? "초(楚)" : "한(漢)";
            if (isPlayerVsAI) who += (winner == aiTeam) ? "·AI" : "·나";
            // 사람끼리 대국은 기존처럼 초 기준으로 전적 기록
            bool isPlayerWin = isPlayerVsAI ? winner != aiTeam : winner == Team.Cho;

            if (MobileJanggiUIManager.Instance != null)
            {
                MobileJanggiUIManager.Instance.ShowGameOverDialog($"{who} 승리 · {reason}", isPlayerWin);
            }
        }

        private void EndTurn()
        {
            CurrentTurn = (CurrentTurn == Team.Cho) ? Team.Han : Team.Cho;
            if (replaying) return;

            if (MobileJanggiUIManager.Instance != null)
            {
                MobileJanggiUIManager.Instance.UpdateTurnDisplay(CurrentTurn);
            }

            if (isPlayerVsAI && CurrentTurn == aiTeam)
            {
                aiRoutine = StartCoroutine(ProcessAITurn());
            }
        }

        // 실행에 실패하면 이번 실행 동안은 내장 AI로 대체. 신경망만 실패하면 신경망 없이 엔진을 씀
        private FairyStockfishEngine GetFairyStockfish()
        {
            if (!useFairyStockfish || fsFailed) return null;
            if (fsEngine != null) return fsEngine;
            string path = null, nnue = null;
            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                path = FairyStockfishEngine.DefaultPath(); // Plugins/Android/libs/arm64-v8a
                nnue = FairyStockfishEngine.PrepareNnue();
#else
                path = System.IO.Path.Combine(Application.dataPath, "..", fairyStockfishPath);
                nnue = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path), FairyStockfishEngine.NnueFileName));
                if (!System.IO.File.Exists(nnue)) nnue = null;
#endif
            }
            catch (Exception e) { Debug.LogWarning($"신경망 준비 실패: {e.Message}"); }

            // 코어 절반(최대 4)과 64MB 해시: 갤럭시 S10(8코어) 기준 4스레드
            int threads = Mathf.Clamp(SystemInfo.processorCount / 2, 1, 4);
            foreach (string evalFile in nnue != null ? new[] { nnue, null } : new string[] { null })
            {
                try
                {
                    fsEngine = new FairyStockfishEngine(path, evalFile, threads, 64);
                    return fsEngine;
                }
                catch (Exception e) { Debug.LogWarning($"Fairy-Stockfish 실행 실패 (신경망 {(evalFile != null ? "사용" : "없음")}): {e.Message}"); }
            }
            fsFailed = true;
            Debug.LogWarning("Fairy-Stockfish를 쓸 수 없어 내장 AI 사용");
            return null;
        }

        private void OnDestroy()
        {
            fsEngine?.Dispose();
            fsEngine = null;
        }

        private static PieceData[,] CloneBoard(PieceData[,] board)
        {
            var copy = new PieceData[9, 10];
            for (int x = 0; x < 9; x++)
                for (int y = 0; y < 10; y++)
                    copy[x, y] = board[x, y]?.Clone();
            return copy;
        }

        // AI 생각 진행률(0~1, 단별 생각 시간 기준). 생각 중이 아니면 -1
        private float aiThinkStart = -1f, aiThinkSeconds = 1f;
        public float AiThinkProgress => aiThinkStart < 0f ? -1f : Mathf.Clamp01((Time.time - aiThinkStart) / aiThinkSeconds);

        private IEnumerator ProcessAITurn()
        {
            yield return new WaitForSeconds(0.3f); // 연출을 위한 짧은 딜레이

            // 장기도사급 고수 AI (Iterative Deepening + Zobrist Transposition Table + MVV-LVA Move Ordering)
            // 화면이 멈추지 않도록 판 복사본으로 백그라운드 스레드에서 계산
            PieceData[,] snapshot = CloneBoard(Board);
            Team team = aiTeam;
            var level = GetAiLevelSettings(aiLevel);
            string variant = EngineVariant;
            FairyStockfishEngine engine = GetFairyStockfish();
            Exception engineError = null;
            var task = System.Threading.Tasks.Task.Run(() =>
            {
                if (engine != null)
                {
                    try { return engine.GetBestMove(snapshot, team, variant, level.Skill, level.TimeMs); }
                    catch (Exception e) { engineError = e; } // 예: 메모리가 부족해 기기가 엔진을 종료
                }
                return JanggiAIHighLevel.GetBestMove(snapshot, team, level.Depth, level.TimeMs);
            });
            aiThinkStart = Time.time;
            aiThinkSeconds = Mathf.Max(0.1f, level.TimeMs / 1000f);
            while (!task.IsCompleted) yield return null; // 이 사이 무르기·기권 시 코루틴이 중단되어 결과를 버림 (진행 막대는 StopAiRoutines가 거둠)
            aiThinkStart = -1f;
            aiRoutine = null;
            if (engineError != null)
            {
                // 엔진이 대국 중에 죽으면 한수 쉼 대신 내장 AI로 두고, 이번 실행 동안은 내장 AI를 씀
                Debug.LogWarning($"Fairy-Stockfish 중단, 내장 AI로 전환: {engineError.Message}");
                fsEngine?.Dispose();
                fsEngine = null;
                fsFailed = true;
            }

            Move bestMove = default;
            if (task.IsFaulted) Debug.LogException(task.Exception);
            else bestMove = task.Result;

            if (bestMove.From == bestMove.To || !MakeMove(bestMove.From, bestMove.To))
            {
                PassTurn();
            }
        }
    }
}
