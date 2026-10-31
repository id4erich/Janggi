using System;
using System.Collections.Generic;

namespace Janggi
{
    public struct Move
    {
        public BoardPos From;
        public BoardPos To;

        public Move(BoardPos from, BoardPos to)
        {
            From = from;
            To = to;
        }
    }

    public static class BoardEvaluator
    {
        public static float Evaluate(PieceData[,] board, Team currentTeam)
        {
            float choScore = 0f;
            float hanScore = 1.5f; // 한(Red) 후공 덤 점수 1.5점

            for (int x = 0; x < 9; x++)
            {
                for (int y = 0; y < 10; y++)
                {
                    PieceData piece = board[x, y];
                    if (piece == null || piece.IsCaptured) continue;

                    float value = GetPieceValue(piece.Type);
                    if (piece.Team == Team.Cho)
                        choScore += value;
                    else
                        hanScore += value;
                }
            }

            return currentTeam == Team.Cho ? (choScore - hanScore) : (hanScore - choScore);
        }

        public static float GetPieceValue(PieceType type)
        {
            switch (type)
            {
                case PieceType.Car: return 13.0f;
                case PieceType.Cannon: return 7.0f;
                case PieceType.Horse: return 5.0f;
                case PieceType.Elephant: return 3.0f;
                case PieceType.Guard: return 3.0f;
                case PieceType.Pawn: return 2.0f;
                case PieceType.King: return 1000.0f;
                default: return 0f;
            }
        }
    }

    public class JanggiAI
    {
        public static Move GetBestMove(PieceData[,] board, Team aiTeam, int depth)
        {
            List<Move> allPossibleMoves = GetAllPossibleMoves(board, aiTeam);
            Move bestMove = default;
            float maxEval = float.MinValue;

            foreach (var move in allPossibleMoves)
            {
                PieceData src = board[move.From.x, move.From.y];
                PieceData dst = board[move.To.x, move.To.y];

                // 가상 이동 적용
                board[move.To.x, move.To.y] = src;
                board[move.From.x, move.From.y] = null;
                src.Position = move.To;

                Team opponent = (aiTeam == Team.Cho) ? Team.Han : Team.Cho;
                float eval = Minimax(board, depth - 1, false, aiTeam, opponent, float.MinValue, float.MaxValue);

                // 복원
                board[move.From.x, move.From.y] = src;
                board[move.To.x, move.To.y] = dst;
                src.Position = move.From;

                if (eval > maxEval)
                {
                    maxEval = eval;
                    bestMove = move;
                }
            }

            return bestMove;
        }

        private static float Minimax(PieceData[,] board, int depth, bool isMaximizing, Team aiTeam, Team currentTurn, float alpha, float beta)
        {
            if (depth == 0)
            {
                return BoardEvaluator.Evaluate(board, aiTeam);
            }

            List<Move> moves = GetAllPossibleMoves(board, currentTurn);
            if (moves.Count == 0) return BoardEvaluator.Evaluate(board, aiTeam);

            if (isMaximizing)
            {
                float maxEval = float.MinValue;
                foreach (var move in moves)
                {
                    PieceData src = board[move.From.x, move.From.y];
                    PieceData dst = board[move.To.x, move.To.y];

                    board[move.To.x, move.To.y] = src;
                    board[move.From.x, move.From.y] = null;
                    src.Position = move.To;

                    Team nextTurn = (currentTurn == Team.Cho) ? Team.Han : Team.Cho;
                    float eval = Minimax(board, depth - 1, false, aiTeam, nextTurn, alpha, beta);

                    board[move.From.x, move.From.y] = src;
                    board[move.To.x, move.To.y] = dst;
                    src.Position = move.From;

                    maxEval = Math.Max(maxEval, eval);
                    alpha = Math.Max(alpha, eval);
                    if (beta <= alpha) break;
                }
                return maxEval;
            }
            else
            {
                float minEval = float.MaxValue;
                foreach (var move in moves)
                {
                    PieceData src = board[move.From.x, move.From.y];
                    PieceData dst = board[move.To.x, move.To.y];

                    board[move.To.x, move.To.y] = src;
                    board[move.From.x, move.From.y] = null;
                    src.Position = move.To;

                    Team nextTurn = (currentTurn == Team.Cho) ? Team.Han : Team.Cho;
                    float eval = Minimax(board, depth - 1, true, aiTeam, nextTurn, alpha, beta);

                    board[move.From.x, move.From.y] = src;
                    board[move.To.x, move.To.y] = dst;
                    src.Position = move.From;

                    minEval = Math.Min(minEval, eval);
                    beta = Math.Min(beta, eval);
                    if (beta <= alpha) break;
                }
                return minEval;
            }
        }

        private static List<Move> GetAllPossibleMoves(PieceData[,] board, Team team)
        {
            List<Move> list = new List<Move>();
            for (int x = 0; x < 9; x++)
            {
                for (int y = 0; y < 10; y++)
                {
                    PieceData p = board[x, y];
                    if (p != null && p.Team == team)
                    {
                        var targets = RuleValidator.GetValidMoves(p, board);
                        foreach (var target in targets)
                        {
                            list.Add(new Move(p.Position, target));
                        }
                    }
                }
            }
            return list;
        }
    }
}
