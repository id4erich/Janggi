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
        public int aiDepth = 6;
        [Tooltip("AI 한 수당 생각 시간 상한(ms). 이 안에 끝난 가장 깊은 수읽기를 사용")]
        public int aiTimeLimitMs = 2000;

        [Header("Fairy-Stockfish (PC 시험용, GPL v3)")]
        public bool useFairyStockfish = false;
        [Tooltip("프로젝트 루트 기준 경로")]
        public string fairyStockfishPath = "Tools/FairyStockfish/fairy-stockfish-largeboard_x86-64-modern.exe";
        [Range(-20, 20)] public int fairyStockfishSkill = 0;

        private FairyStockfishEngine fsEngine;
        private bool fsFailed;

        public event Action<BoardPos, BoardPos> OnPieceMoved;

        public PlacementType choPlacement = PlacementType.MaSangMaSang;
        public PlacementType hanPlacement = PlacementType.MaSangMaSang;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                choPlacement = (PlacementType)PlayerPrefs.GetInt("Janggi_Cho_Placement", (int)PlacementType.MaSangMaSang);
                hanPlacement = (PlacementType)PlayerPrefs.GetInt("Janggi_Han_Placement", (int)PlacementType.MaSangMaSang);
                InitializeBoard(choPlacement, hanPlacement);
            }
            else Destroy(gameObject);
        }

        private void Start()
        {
        }

        public void StartGame(PlacementType cho, PlacementType han)
        {
            choPlacement = cho;
            hanPlacement = han;
            InitializeBoard(choPlacement, hanPlacement);
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

            var validMoves = RuleValidator.GetLegalMoves(p, Board);
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

            OnPieceMoved?.Invoke(from, to);

            // 1. 궁(King) 잡힘 시 즉시 승부 결정 종료!
            if (isKingCaptured)
            {
                EndGame(p.Team, "궁 잡음");
                return true;
            }

            // 2. 장이야! / 멍이야! 상태 판정 및 텍스트 팝업 알림
            Team opponentTeam = (CurrentTurn == Team.Cho) ? Team.Han : Team.Cho;
            bool isCheck = RuleValidator.IsInCheck(Board, opponentTeam);

            if (isCheck && !RuleValidator.HasAnyLegalMove(Board, opponentTeam))
            {
                EndGame(p.Team, "외통");
                return true;
            }

            if (isCheck)
            {
                if (MobileJanggiUIManager.Instance != null)
                {
                    MobileJanggiUIManager.Instance.ShowJangiyoBanner((p.Team == Team.Cho) ? "⚔️ 초(楚): 장이야!" : "⚔️ 한(漢): 장이야!");
                }
            }

            // 턴 전환
            EndTurn();
            return true;
        }

        public void PassTurn()
        {
            if (IsGameOver) return;
            if (RuleValidator.IsInCheck(Board, CurrentTurn))
            {
                Debug.Log("장군 상태에서는 한수 쉼을 할 수 없습니다.");
                return;
            }

            Debug.Log($"{CurrentTurn} 패스(한수 쉼)");
            historyStack.Push(new MoveRecord { Mover = CurrentTurn });
            EndTurn();
        }

        public bool UndoMove()
        {
            if (IsGameOver || historyStack.Count == 0) return false;

            // AI가 아직 두기 전이면 대기 중인 AI 차례를 취소
            if (aiRoutine != null)
            {
                StopCoroutine(aiRoutine);
                aiRoutine = null;
            }

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
            if (aiRoutine != null)
            {
                StopCoroutine(aiRoutine);
                aiRoutine = null;
            }

            string who = (winner == Team.Cho) ? "🔵 초" : "🔴 한";
            if (isPlayerVsAI) who += (winner == aiTeam) ? "(AI)" : "(Player)";
            // 사람끼리 대국은 기존처럼 초 기준으로 전적 기록
            bool isPlayerWin = isPlayerVsAI ? winner != aiTeam : winner == Team.Cho;

            if (MobileJanggiUIManager.Instance != null)
            {
                MobileJanggiUIManager.Instance.ShowGameOverDialog($"{who} 승리! ({reason})", isPlayerWin);
            }
        }

        private void EndTurn()
        {
            CurrentTurn = (CurrentTurn == Team.Cho) ? Team.Han : Team.Cho;

            if (MobileJanggiUIManager.Instance != null)
            {
                MobileJanggiUIManager.Instance.UpdateTurnDisplay(CurrentTurn);
            }

            if (isPlayerVsAI && CurrentTurn == aiTeam)
            {
                aiRoutine = StartCoroutine(ProcessAITurn());
            }
        }

        // 실행에 실패하면 이번 실행 동안은 내장 AI로 대체
        private FairyStockfishEngine GetFairyStockfish()
        {
            if (!useFairyStockfish || fsFailed) return null;
            if (fsEngine != null) return fsEngine;
            try
            {
                string path = System.IO.Path.Combine(Application.dataPath, "..", fairyStockfishPath);
                fsEngine = new FairyStockfishEngine(path, fairyStockfishSkill);
            }
            catch (Exception e)
            {
                fsFailed = true;
                Debug.LogWarning($"Fairy-Stockfish 실행 실패, 내장 AI 사용: {e.Message}");
            }
            return fsEngine;
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

        private IEnumerator ProcessAITurn()
        {
            yield return new WaitForSeconds(0.3f); // 연출을 위한 짧은 딜레이

            // 장기도사급 고수 AI (Iterative Deepening + Zobrist Transposition Table + MVV-LVA Move Ordering)
            // 화면이 멈추지 않도록 판 복사본으로 백그라운드 스레드에서 계산
            PieceData[,] snapshot = CloneBoard(Board);
            Team team = aiTeam;
            int depth = aiDepth, timeLimitMs = aiTimeLimitMs;
            FairyStockfishEngine engine = GetFairyStockfish();
            var task = System.Threading.Tasks.Task.Run(() => engine != null
                ? engine.GetBestMove(snapshot, team, timeLimitMs)
                : JanggiAIHighLevel.GetBestMove(snapshot, team, depth, timeLimitMs));
            while (!task.IsCompleted) yield return null; // 이 사이 무르기·기권 시 코루틴이 중단되어 결과를 버림
            aiRoutine = null;

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
