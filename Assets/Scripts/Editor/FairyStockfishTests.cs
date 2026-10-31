using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Janggi.Tests
{
    public class FairyStockfishTests
    {
        private const string StartFen = "rnba1abnr/4k4/1c5c1/p1p1p1p1p/9/9/P1P1P1P1P/1C5C1/4K4/RNBA1ABNR w - - 0 1";

        private static PieceData[,] StartBoard()
        {
            // 엔진 기본 시작 국면과 같은 차림: 초·한 모두 x=1 마, x=2 상, x=6 상, x=7 마
            var go = new GameObject("GM");
            var gm = go.AddComponent<GameManager>();
            gm.InitializeBoard(PlacementType.MaSangSangMa, PlacementType.MaSangSangMa);
            var board = gm.Board;
            Object.DestroyImmediate(go);
            return board;
        }

        [Test]
        public void ToFen_MatchesEngineStartPosition()
        {
            Assert.AreEqual(StartFen, FairyStockfishEngine.ToFen(StartBoard(), Team.Cho));
        }

        [Test]
        public void ParseMove_HandlesTwoDigitRanksAndPass()
        {
            Move m = FairyStockfishEngine.ParseMove("e10e9");
            Assert.AreEqual(new BoardPos(4, 9), m.From);
            Assert.AreEqual(new BoardPos(4, 8), m.To);

            Move pass = FairyStockfishEngine.ParseMove("e2e2");
            Assert.AreEqual(pass.From, pass.To);
        }

        [Test]
        public void Engine_ReturnsLegalMove()
        {
            string path = Path.Combine(Application.dataPath, "..", "Tools/FairyStockfish/fairy-stockfish-largeboard_x86-64-modern.exe");
            if (!File.Exists(path)) Assert.Ignore("엔진 실행 파일 없음");

            PieceData[,] board = StartBoard();
            using (var engine = new FairyStockfishEngine(path))
            {
                Move m = engine.GetBestMove(board, Team.Cho, 20, 200);
                PieceData piece = board[m.From.x, m.From.y];
                Assert.IsNotNull(piece);
                Assert.AreEqual(Team.Cho, piece.Team);
                Assert.Contains(m.To, RuleValidator.GetLegalMoves(piece, board));
            }
        }
    }
}
