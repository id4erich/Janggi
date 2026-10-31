using NUnit.Framework;

namespace Janggi.Tests
{
    public class RuleValidatorTests
    {
        private PieceData[,] board;

        [SetUp]
        public void SetUp() => board = new PieceData[9, 10];

        private PieceData Place(PieceType type, Team team, int x, int y)
        {
            var p = new PieceData(type, team, new BoardPos(x, y));
            board[x, y] = p;
            return p;
        }

        [Test]
        public void Car_MovesAlongPalaceDiagonal()
        {
            var car = Place(PieceType.Car, Team.Cho, 3, 7);
            var moves = RuleValidator.GetValidMoves(car, board);
            Assert.Contains(new BoardPos(4, 8), moves);
            Assert.Contains(new BoardPos(5, 9), moves);
        }

        [Test]
        public void Car_PalaceDiagonalBlockedByOwnPiece()
        {
            var car = Place(PieceType.Car, Team.Cho, 3, 7);
            Place(PieceType.Pawn, Team.Cho, 4, 8);
            var moves = RuleValidator.GetValidMoves(car, board);
            CollectionAssert.DoesNotContain(moves, new BoardPos(4, 8));
            CollectionAssert.DoesNotContain(moves, new BoardPos(5, 9));
        }

        [Test]
        public void Cannon_JumpsOverPalaceCenter()
        {
            var cannon = Place(PieceType.Cannon, Team.Cho, 3, 7);
            Place(PieceType.Guard, Team.Han, 4, 8);
            Assert.Contains(new BoardPos(5, 9), RuleValidator.GetValidMoves(cannon, board));
        }

        [Test]
        public void Cannon_CannotJumpCannonInPalaceCenter()
        {
            var cannon = Place(PieceType.Cannon, Team.Cho, 3, 7);
            Place(PieceType.Cannon, Team.Han, 4, 8);
            CollectionAssert.DoesNotContain(RuleValidator.GetValidMoves(cannon, board), new BoardPos(5, 9));
        }

        [Test]
        public void Pawn_MovesDiagonallyForwardInEnemyPalace()
        {
            var pawn = Place(PieceType.Pawn, Team.Cho, 3, 7);
            Assert.Contains(new BoardPos(4, 8), RuleValidator.GetValidMoves(pawn, board));

            board[3, 7] = null;
            board[4, 8] = pawn;
            pawn.Position = new BoardPos(4, 8);
            var moves = RuleValidator.GetValidMoves(pawn, board);
            Assert.Contains(new BoardPos(3, 9), moves);
            Assert.Contains(new BoardPos(5, 9), moves);
            CollectionAssert.DoesNotContain(moves, new BoardPos(3, 7)); // 후진 대각선 불가
        }

        [Test]
        public void PinnedPiece_CannotExposeKing()
        {
            Place(PieceType.King, Team.Han, 4, 9);
            var guard = Place(PieceType.Guard, Team.Han, 4, 8);
            Place(PieceType.Car, Team.Cho, 4, 0);
            var legal = RuleValidator.GetLegalMoves(guard, board);
            Assert.AreEqual(1, legal.Count);
            Assert.Contains(new BoardPos(4, 7), legal);
        }

        [Test]
        public void Checkmate_WhenKingHasNoEscape()
        {
            Place(PieceType.King, Team.Han, 4, 9);
            Place(PieceType.Car, Team.Cho, 4, 0);
            Assert.IsFalse(RuleValidator.IsCheckmate(board, Team.Han));

            Place(PieceType.Car, Team.Cho, 3, 0);
            Place(PieceType.Car, Team.Cho, 5, 0);
            Assert.IsTrue(RuleValidator.IsCheckmate(board, Team.Han));
        }
    }
}
