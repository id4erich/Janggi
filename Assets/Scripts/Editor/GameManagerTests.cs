using NUnit.Framework;
using UnityEngine;

namespace Janggi.Tests
{
    public class GameManagerTests
    {
        private GameObject go;
        private GameManager gm;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("GM");
            gm = go.AddComponent<GameManager>(); // 에디트 모드라 Awake는 실행되지 않음
            gm.isPlayerVsAI = false;
            gm.InitializeBoard(PlacementType.MaSangMaSang, PlacementType.MaSangMaSang);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(go);

        [Test]
        public void Undo_RestoresPassAndMoveInOrder()
        {
            Assert.IsTrue(gm.MakeMove(new BoardPos(0, 3), new BoardPos(0, 4)));
            gm.PassTurn();
            Assert.AreEqual(Team.Cho, gm.CurrentTurn);

            Assert.IsTrue(gm.UndoMove()); // 한수 쉼 취소
            Assert.AreEqual(Team.Han, gm.CurrentTurn);
            Assert.IsNotNull(gm.Board[0, 4]);

            Assert.IsTrue(gm.UndoMove()); // 졸 이동 취소
            Assert.AreEqual(Team.Cho, gm.CurrentTurn);
            Assert.IsNotNull(gm.Board[0, 3]);
            Assert.IsNull(gm.Board[0, 4]);

            Assert.IsFalse(gm.UndoMove());
        }

        [Test]
        public void AiLevels_GetStrongerWithLevelAndClamp()
        {
            for (int lv = GameManager.MinAiLevel + 1; lv <= GameManager.MaxAiLevel; lv++)
            {
                var prev = GameManager.GetAiLevelSettings(lv - 1);
                var cur = GameManager.GetAiLevelSettings(lv);
                Assert.Greater(cur.Skill, prev.Skill);
                Assert.GreaterOrEqual(cur.TimeMs, prev.TimeMs);
                Assert.GreaterOrEqual(cur.Depth, prev.Depth);
            }
            Assert.AreEqual(GameManager.GetAiLevelSettings(1), GameManager.GetAiLevelSettings(0));
            Assert.AreEqual(GameManager.GetAiLevelSettings(9), GameManager.GetAiLevelSettings(99));
        }

        [Test]
        public void Resign_EndsGameAndBlocksMoves()
        {
            gm.Resign();
            Assert.IsTrue(gm.IsGameOver);
            Assert.IsFalse(gm.MakeMove(new BoardPos(0, 3), new BoardPos(0, 4)));
            Assert.IsFalse(gm.UndoMove());
        }

        [Test]
        public void SaveLoad_RestoresBoardTurnAndUndoHistory()
        {
            Assert.IsTrue(gm.MakeMove(new BoardPos(0, 3), new BoardPos(1, 3)));  // 초 졸 옆으로
            gm.PassTurn();                                                     // 한 한수 쉼
            Assert.IsTrue(gm.MakeMove(new BoardPos(0, 0), new BoardPos(0, 6)));  // 초 차가 한 병을 잡음
            string json = gm.SaveToJson();

            var go2 = new GameObject("GM2");
            var gm2 = go2.AddComponent<GameManager>();
            gm2.isPlayerVsAI = false;
            try
            {
                Assert.IsTrue(gm2.LoadFromJson(json));
                Assert.AreEqual(gm.CurrentTurn, gm2.CurrentTurn);
                for (int x = 0; x < 9; x++)
                    for (int y = 0; y < 10; y++)
                    {
                        PieceData a = gm.Board[x, y], b = gm2.Board[x, y];
                        Assert.AreEqual(a == null, b == null, $"({x},{y})");
                        if (a != null) { Assert.AreEqual(a.Type, b.Type); Assert.AreEqual(a.Team, b.Team); }
                    }

                // 무르기 기록도 복원됨: 잡힌 병이 되살아남
                Assert.IsTrue(gm2.UndoMove());
                Assert.AreEqual(PieceType.Pawn, gm2.Board[0, 6].Type);
                Assert.AreEqual(Team.Han, gm2.Board[0, 6].Team);
            }
            finally { Object.DestroyImmediate(go2); }
        }

        [Test]
        public void LoadFromJson_RejectsBrokenData()
        {
            Assert.IsFalse(gm.LoadFromJson("not json"));
            Assert.IsFalse(gm.LoadFromJson("{\"cho\":0,\"han\":0,\"moves\":[4040]}")); // 둘 수 없는 수
            Assert.IsFalse(gm.LoadFromJson("{\"cho\":0,\"han\":0,\"moves\":[9999]}")); // 판 밖 좌표 (예외 없이 실패)
        }

        [Test]
        public void CanUndo_FalseWhenOnlyAiHasMoved()
        {
            gm.ApplyMode(GameManager.GameMode.VsAiAsHan); // AI가 초(선공)
            Assert.IsFalse(gm.CanUndo);
            Assert.IsTrue(gm.MakeMove(new BoardPos(0, 3), new BoardPos(1, 3))); // AI(초)의 첫 수
            Assert.IsFalse(gm.CanUndo);
            Assert.IsFalse(gm.UndoMove());
        }

        [Test]
        public void NewGame_ClearsUndoHistory()
        {
            Assert.IsTrue(gm.MakeMove(new BoardPos(0, 3), new BoardPos(0, 4)));
            gm.InitializeBoard(PlacementType.MaSangMaSang, PlacementType.MaSangMaSang);
            Assert.IsFalse(gm.UndoMove());
        }

        [Test]
        public void MoveRecordLines_ShowSidePieceCoordinatesAndCaptures()
        {
            Assert.IsTrue(gm.MakeMove(new BoardPos(0, 3), new BoardPos(1, 3)));
            gm.PassTurn();
            Assert.IsTrue(gm.MakeMove(new BoardPos(0, 0), new BoardPos(0, 6)));

            var lines = gm.GetMoveRecordLines();
            Assert.AreEqual(3, lines.Count);
            Assert.AreEqual("1. 초 졸 1,4→2,4", lines[0]);
            Assert.AreEqual("2. 한 한수 쉼", lines[1]);
            Assert.AreEqual("3. 초 차 1,1→1,7 ×병", lines[2]);
        }

        // 두 궁 사이(4번 줄)의 졸·병을 치워 빅장을 만듦
        private void OpenCenterFile()
        {
            gm.Board[4, 3] = null;
            gm.Board[4, 6] = null;
        }

        [Test]
        public void DoublePass_EndsGame()
        {
            gm.PassTurn();
            Assert.IsFalse(gm.IsGameOver);
            gm.PassTurn();
            Assert.IsTrue(gm.IsGameOver);
        }

        [Test]
        public void Bikjang_IgnoredInCasual_EndsGameWhenAcceptedOtherwise()
        {
            OpenCenterFile();
            Assert.IsTrue(RuleValidator.KingsFacing(gm.Board));

            gm.ruleSet = GameManager.RuleSet.Casual;
            gm.PassTurn(); // 일반 규칙: 빅장이 아님, 한 번 쉰 것뿐
            Assert.IsFalse(gm.IsGameOver);

            gm.InitializeBoard(PlacementType.MaSangMaSang, PlacementType.MaSangMaSang);
            OpenCenterFile();
            gm.ruleSet = GameManager.RuleSet.Traditional;
            // 빅장 중에는 빅장을 유지하는 수는 못 둠 (초 차가 옆으로 비켜도 빅장 유지)
            Assert.IsFalse(gm.MakeMove(new BoardPos(0, 0), new BoardPos(0, 1)));
            gm.PassTurn(); // 빅장을 받아들임
            Assert.IsTrue(gm.IsGameOver);
        }

        [Test]
        public void MaterialPoints_CountsStandardValues()
        {
            Assert.AreEqual(72, gm.MaterialPoints(Team.Cho)); // 차26 포14 마10 상6 사6 졸10
            Assert.AreEqual(72, gm.MaterialPoints(Team.Han));
        }

        [Test]
        public void Review_StepsThroughMovesAndRestores()
        {
            Assert.IsTrue(gm.MakeMove(new BoardPos(0, 3), new BoardPos(1, 3)));
            gm.PassTurn();
            Assert.IsTrue(gm.MakeMove(new BoardPos(0, 0), new BoardPos(0, 6))); // 병을 잡음

            Assert.IsTrue(gm.EnterReview());
            Assert.AreEqual(3, gm.ReviewTotal);
            gm.ReviewTo(1); // 첫 수만 둔 국면
            Assert.IsNotNull(gm.Board[1, 3]);
            Assert.AreEqual(PieceType.Pawn, gm.Board[0, 6].Type);
            Assert.AreEqual(Team.Han, gm.CurrentTurn);
            gm.ReviewTo(0);
            Assert.IsNotNull(gm.Board[0, 3]);

            gm.ExitReview();
            Assert.IsFalse(gm.IsReviewing);
            Assert.AreEqual(PieceType.Car, gm.Board[0, 6].Type);
            Assert.AreEqual(Team.Han, gm.CurrentTurn);
            Assert.IsTrue(gm.UndoMove()); // 무르기 기록도 원래대로
            Assert.AreEqual(PieceType.Pawn, gm.Board[0, 6].Type);
        }

        [Test]
        public void TimeOut_LosesOnlyWhenLimitIsSet()
        {
            gm.moveTimeLimitSec = 0;
            gm.TimeOut();
            Assert.IsFalse(gm.IsGameOver);

            gm.moveTimeLimitSec = 30;
            gm.TimeOut();
            Assert.IsTrue(gm.IsGameOver);
        }
    }
}
