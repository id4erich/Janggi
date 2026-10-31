using System.Collections.Generic;

namespace Janggi
{
    public static class RuleValidator
    {
        public static bool IsValidBounds(BoardPos pos)
        {
            return pos.x >= 0 && pos.x < 9 && pos.y >= 0 && pos.y < 10;
        }

        public static bool IsInPalace(BoardPos pos, Team team)
        {
            if (pos.x < 3 || pos.x > 5) return false;
            if (team == Team.Cho) return pos.y >= 0 && pos.y <= 2;
            else return pos.y >= 7 && pos.y <= 9;
        }

        public static List<BoardPos> GetValidMoves(PieceData piece, PieceData[,] board)
        {
            List<BoardPos> validMoves = new List<BoardPos>();
            if (piece == null || piece.IsCaptured) return validMoves;

            switch (piece.Type)
            {
                case PieceType.King:
                case PieceType.Guard:
                    GetKingOrGuardMoves(piece, board, validMoves);
                    break;
                case PieceType.Car:
                    GetCarMoves(piece, board, validMoves);
                    break;
                case PieceType.Cannon:
                    GetCannonMoves(piece, board, validMoves);
                    break;
                case PieceType.Horse:
                    GetHorseMoves(piece, board, validMoves);
                    break;
                case PieceType.Elephant:
                    GetElephantMoves(piece, board, validMoves);
                    break;
                case PieceType.Pawn:
                    GetPawnMoves(piece, board, validMoves);
                    break;
            }

            return validMoves;
        }

        private static void GetKingOrGuardMoves(PieceData piece, PieceData[,] board, List<BoardPos> validMoves)
        {
            int minX = 3, maxX = 5;
            int minY = (piece.Team == Team.Cho) ? 0 : 7;
            int maxY = (piece.Team == Team.Cho) ? 2 : 9;

            int[] dx = { 0, 0, -1, 1, -1, 1, -1, 1 };
            int[] dy = { -1, 1, 0, 0, -1, -1, 1, 1 };

            BoardPos center = new BoardPos(4, minY + 1);

            for (int i = 0; i < 8; i++)
            {
                // 대각선 이동은 궁성 중앙이나 모서리에서만 가능
                if (i >= 4)
                {
                    if (piece.Position != center && !IsPalaceCorner(piece.Position, minY))
                        continue;

                    // 모서리에서 대각선 이동 시 중앙을 거치는지 체크
                    BoardPos target = new BoardPos(piece.Position.x + dx[i], piece.Position.y + dy[i]);
                    if (piece.Position != center && target != center)
                        continue;
                }

                BoardPos next = new BoardPos(piece.Position.x + dx[i], piece.Position.y + dy[i]);
                if (next.x >= minX && next.x <= maxX && next.y >= minY && next.y <= maxY)
                {
                    if (board[next.x, next.y] == null || board[next.x, next.y].Team != piece.Team)
                    {
                        validMoves.Add(next);
                    }
                }
            }
        }

        private static bool IsPalaceCorner(BoardPos pos, int minY)
        {
            return (pos.x == 3 && (pos.y == minY || pos.y == minY + 2)) ||
                   (pos.x == 5 && (pos.y == minY || pos.y == minY + 2));
        }

        private static void GetCarMoves(PieceData piece, PieceData[,] board, List<BoardPos> validMoves)
        {
            int[] dx = { 0, 0, -1, 1 };
            int[] dy = { 1, -1, 0, 0 };

            for (int i = 0; i < 4; i++)
            {
                int nx = piece.Position.x + dx[i];
                int ny = piece.Position.y + dy[i];

                while (IsValidBounds(new BoardPos(nx, ny)))
                {
                    if (board[nx, ny] == null)
                    {
                        validMoves.Add(new BoardPos(nx, ny));
                    }
                    else
                    {
                        if (board[nx, ny].Team != piece.Team)
                        {
                            validMoves.Add(new BoardPos(nx, ny));
                        }
                        break;
                    }
                    nx += dx[i];
                    ny += dy[i];
                }
            }

            // 궁성 대각선 이동
            foreach (var ray in GetPalaceDiagonalRays(piece.Position))
            {
                foreach (var next in ray)
                {
                    PieceData target = board[next.x, next.y];
                    if (target == null)
                    {
                        validMoves.Add(next);
                        continue;
                    }
                    if (target.Team != piece.Team) validMoves.Add(next);
                    break;
                }
            }
        }

        private static readonly BoardPos[] PalaceCenters = { new BoardPos(4, 1), new BoardPos(4, 8) };
        private static readonly int[] DiagDx = { -1, 1, -1, 1 };
        private static readonly int[] DiagDy = { -1, -1, 1, 1 };

        // 궁성 대각선 경로: 모서리에서는 [중앙, 반대 모서리], 중앙에서는 [각 모서리]
        private static List<List<BoardPos>> GetPalaceDiagonalRays(BoardPos pos)
        {
            var rays = new List<List<BoardPos>>();
            foreach (var c in PalaceCenters)
            {
                for (int i = 0; i < 4; i++)
                {
                    BoardPos corner = new BoardPos(c.x + DiagDx[i], c.y + DiagDy[i]);
                    if (pos == c) rays.Add(new List<BoardPos> { corner });
                    else if (pos == new BoardPos(c.x - DiagDx[i], c.y - DiagDy[i])) rays.Add(new List<BoardPos> { c, corner });
                }
            }
            return rays;
        }

        private static void GetCannonMoves(PieceData piece, PieceData[,] board, List<BoardPos> validMoves)
        {
            int[] dx = { 0, 0, -1, 1 };
            int[] dy = { 1, -1, 0, 0 };

            for (int i = 0; i < 4; i++)
            {
                int nx = piece.Position.x + dx[i];
                int ny = piece.Position.y + dy[i];
                bool jumped = false;

                while (IsValidBounds(new BoardPos(nx, ny)))
                {
                    PieceData target = board[nx, ny];
                    if (!jumped)
                    {
                        if (target != null)
                        {
                            if (target.Type == PieceType.Cannon) break; // 포는 다리가 될 수 없음
                            jumped = true;
                        }
                    }
                    else
                    {
                        if (target == null)
                        {
                            validMoves.Add(new BoardPos(nx, ny));
                        }
                        else
                        {
                            if (target.Team != piece.Team && target.Type != PieceType.Cannon)
                            {
                                validMoves.Add(new BoardPos(nx, ny));
                            }
                            break;
                        }
                    }
                    nx += dx[i];
                    ny += dy[i];
                }
            }

            // 궁성 대각선: 모서리에서 중앙의 기물(포 제외)을 넘어 반대 모서리로
            foreach (var ray in GetPalaceDiagonalRays(piece.Position))
            {
                if (ray.Count < 2) continue;
                PieceData screen = board[ray[0].x, ray[0].y];
                if (screen == null || screen.Type == PieceType.Cannon) continue;

                PieceData target = board[ray[1].x, ray[1].y];
                if (target == null || (target.Team != piece.Team && target.Type != PieceType.Cannon))
                {
                    validMoves.Add(ray[1]);
                }
            }
        }

        private static void GetHorseMoves(PieceData piece, PieceData[,] board, List<BoardPos> validMoves)
        {
            // 마의 8가지 이동 경로 및 목 체크 (직선 1칸 목이 비어있어야 함)
            int[,] offsets = {
                {0, 1, -1, 2}, {0, 1, 1, 2},    // 위로 1칸 -> 대각선
                {0, -1, -1, -2}, {0, -1, 1, -2}, // 아래로 1칸 -> 대각선
                {-1, 0, -2, 1}, {-1, 0, -2, -1}, // 좌로 1칸 -> 대각선
                {1, 0, 2, 1}, {1, 0, 2, -1}      // 우로 1칸 -> 대각선
            };

            for (int i = 0; i < 8; i++)
            {
                BoardPos neck = new BoardPos(piece.Position.x + offsets[i, 0], piece.Position.y + offsets[i, 1]);
                BoardPos target = new BoardPos(piece.Position.x + offsets[i, 2], piece.Position.y + offsets[i, 3]);

                if (IsValidBounds(target) && IsValidBounds(neck))
                {
                    if (board[neck.x, neck.y] == null) // 목이 비어있어야 이동 가능
                    {
                        if (board[target.x, target.y] == null || board[target.x, target.y].Team != piece.Team)
                        {
                            validMoves.Add(target);
                        }
                    }
                }
            }
        }

        private static void GetElephantMoves(PieceData piece, PieceData[,] board, List<BoardPos> validMoves)
        {
            // 상의 8가지 이동 경로 및 2개 목 체크 (직선 1칸, 대각선 1칸 목이 비어있어야 함)
            int[,] offsets = {
                {0, 1, -1, 2, -2, 3}, {0, 1, 1, 2, 2, 3},       // 위
                {0, -1, -1, -2, -2, -3}, {0, -1, 1, -2, 2, -3}, // 아래
                {-1, 0, -2, 1, -3, 2}, {-1, 0, -2, -1, -3, -2}, // 좌
                {1, 0, 2, 1, 3, 2}, {1, 0, 2, -1, 3, -2}        // 우
            };

            for (int i = 0; i < 8; i++)
            {
                BoardPos neck1 = new BoardPos(piece.Position.x + offsets[i, 0], piece.Position.y + offsets[i, 1]);
                BoardPos neck2 = new BoardPos(piece.Position.x + offsets[i, 2], piece.Position.y + offsets[i, 3]);
                BoardPos target = new BoardPos(piece.Position.x + offsets[i, 4], piece.Position.y + offsets[i, 5]);

                if (IsValidBounds(target) && IsValidBounds(neck1) && IsValidBounds(neck2))
                {
                    if (board[neck1.x, neck1.y] == null && board[neck2.x, neck2.y] == null)
                    {
                        if (board[target.x, target.y] == null || board[target.x, target.y].Team != piece.Team)
                        {
                            validMoves.Add(target);
                        }
                    }
                }
            }
        }

        private static void GetPawnMoves(PieceData piece, PieceData[,] board, List<BoardPos> validMoves)
        {
            int forwardY = (piece.Team == Team.Cho) ? 1 : -1;
            int[] dx = { 0, -1, 1 };
            int[] dy = { forwardY, 0, 0 };

            for (int i = 0; i < 3; i++)
            {
                BoardPos target = new BoardPos(piece.Position.x + dx[i], piece.Position.y + dy[i]);
                if (IsValidBounds(target))
                {
                    if (board[target.x, target.y] == null || board[target.x, target.y].Team != piece.Team)
                    {
                        validMoves.Add(target);
                    }
                }
            }

            // 상대 궁성 안에서는 대각선 전진 가능
            foreach (var ray in GetPalaceDiagonalRays(piece.Position))
            {
                BoardPos target = ray[0];
                if (target.y - piece.Position.y != forwardY) continue;
                if (board[target.x, target.y] == null || board[target.x, target.y].Team != piece.Team)
                {
                    validMoves.Add(target);
                }
            }
        }

        // 이동 후 자기 궁이 장군 상태로 남는 수(장군 방치·자충수)는 제외
        public static List<BoardPos> GetLegalMoves(PieceData piece, PieceData[,] board)
        {
            var legal = new List<BoardPos>();
            BoardPos from = piece.Position;
            foreach (var to in GetValidMoves(piece, board))
            {
                PieceData captured = board[to.x, to.y];
                board[to.x, to.y] = piece;
                board[from.x, from.y] = null;
                piece.Position = to;

                bool exposed = IsInCheck(board, piece.Team);

                board[from.x, from.y] = piece;
                board[to.x, to.y] = captured;
                piece.Position = from;

                if (!exposed) legal.Add(to);
            }
            return legal;
        }

        public static bool HasAnyLegalMove(PieceData[,] board, Team team)
        {
            for (int x = 0; x < 9; x++)
            {
                for (int y = 0; y < 10; y++)
                {
                    PieceData p = board[x, y];
                    if (p != null && p.Team == team && !p.IsCaptured && GetLegalMoves(p, board).Count > 0)
                        return true;
                }
            }
            return false;
        }

        // 장기는 한수 쉼이 가능하므로 장군 상태에서 피할 수가 없을 때만 외통
        public static bool IsCheckmate(PieceData[,] board, Team team)
        {
            return IsInCheck(board, team) && !HasAnyLegalMove(board, team);
        }

        public static BoardPos? FindKing(PieceData[,] board, Team team)
        {
            for (int x = 0; x < 9; x++)
            {
                for (int y = 0; y < 10; y++)
                {
                    PieceData p = board[x, y];
                    if (p != null && p.Type == PieceType.King && p.Team == team && !p.IsCaptured)
                    {
                        return p.Position;
                    }
                }
            }
            return null;
        }

        public static bool IsInCheck(PieceData[,] board, Team kingTeam)
        {
            BoardPos? kingPos = FindKing(board, kingTeam);
            if (!kingPos.HasValue) return false;

            Team enemyTeam = (kingTeam == Team.Cho) ? Team.Han : Team.Cho;

            for (int x = 0; x < 9; x++)
            {
                for (int y = 0; y < 10; y++)
                {
                    PieceData p = board[x, y];
                    if (p != null && p.Team == enemyTeam && !p.IsCaptured)
                    {
                        var moves = GetValidMoves(p, board);
                        if (moves.Contains(kingPos.Value))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }
    }
}
