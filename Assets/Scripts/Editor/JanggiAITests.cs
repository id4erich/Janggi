using System.Diagnostics;
using NUnit.Framework;

namespace Janggi.Tests
{
    public class JanggiAITests
    {
        private static void Place(PieceData[,] board, PieceType type, Team team, int x, int y)
            => board[x, y] = new PieceData(type, team, new BoardPos(x, y));

        [Test]
        public void CapturesUndefendedCar()
        {
            var board = new PieceData[9, 10];
            Place(board, PieceType.King, Team.Cho, 4, 1);
            Place(board, PieceType.King, Team.Han, 4, 8);
            Place(board, PieceType.Car, Team.Han, 0, 9);
            Place(board, PieceType.Car, Team.Cho, 0, 5);

            Move m = JanggiAIHighLevel.GetBestMove(board, Team.Han, 3, 2000);
            Assert.AreEqual(new BoardPos(0, 9), m.From);
            Assert.AreEqual(new BoardPos(0, 5), m.To);
        }

        [Test]
        public void RespectsTimeLimitOnFullBoard()
        {
            var go = new UnityEngine.GameObject("GM");
            var gm = go.AddComponent<GameManager>();
            gm.InitializeBoard(PlacementType.MaSangMaSang, PlacementType.MaSangMaSang);

            var clock = Stopwatch.StartNew();
            Move m = JanggiAIHighLevel.GetBestMove(gm.Board, Team.Cho, 20, 300);
            clock.Stop();
            UnityEngine.Object.DestroyImmediate(go);

            Assert.AreNotEqual(m.From, m.To);
            Assert.Less(clock.ElapsedMilliseconds, 1500); // 1수 읽기 + 중단 지연 여유
        }
    }
}
