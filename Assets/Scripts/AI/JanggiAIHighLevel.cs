using System;
using System.Collections.Generic;
using System.Diagnostics;

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

    public enum TTNodeType { Exact, LowerBound, UpperBound }

    public struct TTEntry
    {
        public ulong Hash;
        public int Depth;
        public float Score;
        public TTNodeType Type;
        public Move BestMove;
    }

    public class JanggiAIHighLevel
    {
        // 탐색 1회분 상태: 트랜스포지션 테이블을 탐색마다 새로 만들어 메모리 누적과 스레드 간 공유를 막음
        private class SearchContext
        {
            public readonly Dictionary<ulong, TTEntry> TT = new Dictionary<ulong, TTEntry>(100000);
            public readonly Stopwatch Clock = Stopwatch.StartNew();
            public int TimeLimitMs;
            public bool CanAbort;
            public bool Aborted;

            public bool TimeUp()
            {
                if (CanAbort && Clock.ElapsedMilliseconds > TimeLimitMs) Aborted = true;
                return Aborted;
            }
        }

        // 메인 스레드 밖에서 호출해도 되도록 board 외의 공유 상태를 쓰지 않음 (board는 복사본을 넘길 것)
        public static Move GetBestMove(PieceData[,] board, Team aiTeam, int maxDepth, int timeLimitMs)
        {
            var ctx = new SearchContext { TimeLimitMs = timeLimitMs };
            Move bestMove = default;

            // Iterative Deepening: 시간 안에 끝난 가장 깊은 수읽기 결과를 사용 (1수 읽기는 항상 완료)
            for (int currentDepth = 1; currentDepth <= maxDepth; currentDepth++)
            {
                ctx.CanAbort = currentDepth > 1;
                Move moveThisDepth = GetBestMoveAtDepth(board, aiTeam, currentDepth, bestMove, ctx);
                if (ctx.Aborted) break;
                if (moveThisDepth.From != moveThisDepth.To) bestMove = moveThisDepth;
            }

            return bestMove;
        }

        private static Move GetBestMoveAtDepth(PieceData[,] board, Team aiTeam, int depth, Move previousBest, SearchContext ctx)
        {
            List<Move> moves = GetAllPossibleMoves(board, aiTeam, legalOnly: true);
            OrderMoves(moves, board, aiTeam); // Move Ordering (좋은 수 먼저 탐색하여 가지치기 극대화)
            PromoteMove(moves, previousBest); // 이전 깊이의 최선수를 먼저 탐색

            Move bestMove = default;
            float bestScore = float.MinValue;
            float alpha = float.MinValue;
            float beta = float.MaxValue;

            foreach (var move in moves)
            {
                PieceData src = board[move.From.x, move.From.y];
                PieceData dst = board[move.To.x, move.To.y];

                // Make Move
                board[move.To.x, move.To.y] = src;
                board[move.From.x, move.From.y] = null;
                src.Position = move.To;

                Team opponent = (aiTeam == Team.Cho) ? Team.Han : Team.Cho;
                float eval = -AlphaBeta(board, depth - 1, -beta, -alpha, opponent, aiTeam, ctx);

                // Undo Move
                board[move.From.x, move.From.y] = src;
                board[move.To.x, move.To.y] = dst;
                src.Position = move.From;
                if (ctx.Aborted) break;

                if (eval > bestScore)
                {
                    bestScore = eval;
                    bestMove = move;
                }
                alpha = Math.Max(alpha, eval);
            }

            return bestMove;
        }

        private static float AlphaBeta(PieceData[,] board, int depth, float alpha, float beta, Team currentTurn, Team aiTeam, SearchContext ctx)
        {
            if (ctx.TimeUp()) return 0f; // 중단된 깊이의 결과는 GetBestMove에서 버려짐

            ulong hash = ZobristHash.CalculateHash(board, currentTurn);

            // 1. Transposition Table Lookup
            bool hasEntry = ctx.TT.TryGetValue(hash, out TTEntry entry);
            if (hasEntry && entry.Depth >= depth)
            {
                if (entry.Type == TTNodeType.Exact) return entry.Score;
                if (entry.Type == TTNodeType.LowerBound && entry.Score >= beta) return entry.Score;
                if (entry.Type == TTNodeType.UpperBound && entry.Score <= alpha) return entry.Score;
            }

            if (depth == 0)
            {
                return BoardEvaluatorHighLevel.Evaluate(board, currentTurn);
            }

            List<Move> moves = GetAllPossibleMoves(board, currentTurn);
            if (moves.Count == 0) return BoardEvaluatorHighLevel.Evaluate(board, currentTurn);

            OrderMoves(moves, board, currentTurn);
            if (hasEntry) PromoteMove(moves, entry.BestMove); // 이전 탐색에서 찾은 최선수 우선

            float originalAlpha = alpha;
            float bestValue = float.MinValue;
            Move bestMove = default;

            foreach (var move in moves)
            {
                PieceData src = board[move.From.x, move.From.y];
                PieceData dst = board[move.To.x, move.To.y];

                board[move.To.x, move.To.y] = src;
                board[move.From.x, move.From.y] = null;
                src.Position = move.To;

                Team nextTurn = (currentTurn == Team.Cho) ? Team.Han : Team.Cho;
                float value = -AlphaBeta(board, depth - 1, -beta, -alpha, nextTurn, aiTeam, ctx);

                board[move.From.x, move.From.y] = src;
                board[move.To.x, move.To.y] = dst;
                src.Position = move.From;
                if (ctx.Aborted) return 0f;

                if (value > bestValue)
                {
                    bestValue = value;
                    bestMove = move;
                }

                alpha = Math.Max(alpha, value);
                if (alpha >= beta) break; // Beta Cutoff
            }

            // Transposition Table Store
            TTEntry newEntry;
            newEntry.Hash = hash;
            newEntry.Depth = depth;
            newEntry.Score = bestValue;
            newEntry.BestMove = bestMove;

            if (bestValue <= originalAlpha) newEntry.Type = TTNodeType.UpperBound;
            else if (bestValue >= beta) newEntry.Type = TTNodeType.LowerBound;
            else newEntry.Type = TTNodeType.Exact;

            ctx.TT[hash] = newEntry;

            return bestValue;
        }

        private static void PromoteMove(List<Move> moves, Move first)
        {
            int idx = moves.FindIndex(m => m.From == first.From && m.To == first.To);
            if (idx <= 0) return;
            moves.RemoveAt(idx);
            moves.Insert(0, first);
        }

        private static void OrderMoves(List<Move> moves, PieceData[,] board, Team turn)
        {
            // MVV-LVA (Most Valuable Victim - Least Valuable Aggressor) 기반 무브 오더링
            moves.Sort((a, b) =>
            {
                PieceData targetA = board[a.To.x, a.To.y];
                PieceData targetB = board[b.To.x, b.To.y];

                float scoreA = (targetA != null) ? BoardEvaluatorHighLevel.GetPieceValue(targetA.Type) * 10f : 0f;
                float scoreB = (targetB != null) ? BoardEvaluatorHighLevel.GetPieceValue(targetB.Type) * 10f : 0f;

                return scoreB.CompareTo(scoreA);
            });
        }

        // 루트에서만 legalOnly: 내부 탐색은 궁 가치(1000)로 자충수를 걸러내 비용을 아낌
        private static List<Move> GetAllPossibleMoves(PieceData[,] board, Team team, bool legalOnly = false)
        {
            List<Move> list = new List<Move>();
            for (int x = 0; x < 9; x++)
            {
                for (int y = 0; y < 10; y++)
                {
                    PieceData p = board[x, y];
                    if (p != null && p.Team == team)
                    {
                        var targets = legalOnly ? RuleValidator.GetLegalMoves(p, board) : RuleValidator.GetValidMoves(p, board);
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
