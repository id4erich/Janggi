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
        public void Resign_EndsGameAndBlocksMoves()
        {
            gm.Resign();
            Assert.IsTrue(gm.IsGameOver);
            Assert.IsFalse(gm.MakeMove(new BoardPos(0, 3), new BoardPos(0, 4)));
            Assert.IsFalse(gm.UndoMove());
        }
    }
}
