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

        [Test]
        public void Bikjang_MustBeBroken_AndIsNotCheckmate()
        {
            Place(PieceType.King, Team.Cho, 4, 1);
            Place(PieceType.King, Team.Han, 4, 8);
            var car = Place(PieceType.Car, Team.Cho, 0, 4);
            Assert.IsTrue(RuleValidator.KingsFacing(board));

            var withRule = RuleValidator.GetLegalMoves(car, board, bikjangRule: true);
            Assert.Contains(new BoardPos(4, 4), withRule);                 // 사이를 막아 빅장을 풂
            CollectionAssert.DoesNotContain(withRule, new BoardPos(0, 5)); // 빅장 유지 → 불가

            var withoutRule = RuleValidator.GetLegalMoves(car, board);
            Assert.Contains(new BoardPos(0, 5), withoutRule);

            // 빅장 중에는 한수 쉼이 가능하므로 외통이 아님
            Assert.IsFalse(RuleValidator.IsCheckmate(board, Team.Cho, bikjangRule: true));
        }

        [Test]
        public void Horse_BlockedByPieceOnItsNeck()
        {
            var horse = Place(PieceType.Horse, Team.Cho, 4, 4);
            Place(PieceType.Pawn, Team.Han, 4, 5); // 위쪽 멱
            var moves = RuleValidator.GetValidMoves(horse, board);
            CollectionAssert.DoesNotContain(moves, new BoardPos(3, 6));
            CollectionAssert.DoesNotContain(moves, new BoardPos(5, 6));
            Assert.Contains(new BoardPos(6, 5), moves);
            Assert.Contains(new BoardPos(3, 2), moves);
        }

        [Test]
        public void Elephant_BlockedBySecondNeck()
        {
            var elephant = Place(PieceType.Elephant, Team.Cho, 4, 4);
            Place(PieceType.Pawn, Team.Cho, 5, 6); // (6,7)로 가는 길의 두 번째 멱
            var moves = RuleValidator.GetValidMoves(elephant, board);
            CollectionAssert.DoesNotContain(moves, new BoardPos(6, 7));
            Assert.Contains(new BoardPos(2, 7), moves);
        }

        [Test]
        public void King_StaysInPalace_AndMovesDiagonallyFromCorner()
        {
            var king = Place(PieceType.King, Team.Cho, 3, 0);
            var moves = RuleValidator.GetValidMoves(king, board);
            Assert.Contains(new BoardPos(4, 1), moves); // 모서리 → 중앙 대각선
            Assert.Contains(new BoardPos(3, 1), moves);
            Assert.Contains(new BoardPos(4, 0), moves);
            CollectionAssert.DoesNotContain(moves, new BoardPos(2, 0)); // 궁성 밖
            Assert.AreEqual(3, moves.Count);
        }

        [Test]
        public void Guard_NoDiagonalFromPalaceEdgeMiddle()
        {
            var guard = Place(PieceType.Guard, Team.Cho, 4, 0); // 변의 가운데: 궁성 대각선 위가 아님
            var moves = RuleValidator.GetValidMoves(guard, board);
            CollectionAssert.DoesNotContain(moves, new BoardPos(3, 1));
            CollectionAssert.DoesNotContain(moves, new BoardPos(5, 1));
            Assert.Contains(new BoardPos(4, 1), moves);
            Assert.Contains(new BoardPos(3, 0), moves);
            Assert.Contains(new BoardPos(5, 0), moves);
        }

        [Test]
        public void Pawn_NeverMovesBackward()
        {
            var cho = Place(PieceType.Pawn, Team.Cho, 4, 4);
            var choMoves = RuleValidator.GetValidMoves(cho, board);
            CollectionAssert.AreEquivalent(new[] { new BoardPos(4, 5), new BoardPos(3, 4), new BoardPos(5, 4) }, choMoves);

            var han = Place(PieceType.Pawn, Team.Han, 0, 5);
            var hanMoves = RuleValidator.GetValidMoves(han, board);
            Assert.Contains(new BoardPos(0, 4), hanMoves);
            CollectionAssert.DoesNotContain(hanMoves, new BoardPos(0, 6));
        }

        [Test]
        public void Cannon_CannotCaptureCannon_NorJumpOverCannon()
        {
            var cannon = Place(PieceType.Cannon, Team.Cho, 0, 0);
            Place(PieceType.Pawn, Team.Cho, 0, 2);   // 다리
            Place(PieceType.Cannon, Team.Han, 0, 5); // 포는 포를 못 잡음
            var moves = RuleValidator.GetValidMoves(cannon, board);
            CollectionAssert.DoesNotContain(moves, new BoardPos(0, 1)); // 다리 없이는 못 감
            Assert.Contains(new BoardPos(0, 3), moves);
            Assert.Contains(new BoardPos(0, 4), moves);
            CollectionAssert.DoesNotContain(moves, new BoardPos(0, 5));

            board[0, 2] = null;
            Place(PieceType.Cannon, Team.Han, 0, 2); // 포는 포를 다리로 못 씀
            moves = RuleValidator.GetValidMoves(cannon, board);
            CollectionAssert.DoesNotContain(moves, new BoardPos(0, 3));
        }

        [Test]
        public void Car_StopsAtOwnPiece_AndCapturesFirstEnemy()
        {
            var car = Place(PieceType.Car, Team.Cho, 0, 0);
            Place(PieceType.Pawn, Team.Cho, 0, 3);
            Place(PieceType.Horse, Team.Han, 3, 0);
            Place(PieceType.Horse, Team.Han, 5, 0);
            var moves = RuleValidator.GetValidMoves(car, board);
            CollectionAssert.AreEquivalent(new[]
            {
                new BoardPos(0, 1), new BoardPos(0, 2),                  // 우리 졸 앞에서 멈춤
                new BoardPos(1, 0), new BoardPos(2, 0), new BoardPos(3, 0) // 첫 상대 말까지 (잡기 포함)
            }, moves);
        }
    }
}
