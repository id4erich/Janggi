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
        }

        [Test]
        public void NewGame_ClearsUndoHistory()
        {
            Assert.IsTrue(gm.MakeMove(new BoardPos(0, 3), new BoardPos(0, 4)));
            gm.InitializeBoard(PlacementType.MaSangMaSang, PlacementType.MaSangMaSang);
            Assert.IsFalse(gm.UndoMove());
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
