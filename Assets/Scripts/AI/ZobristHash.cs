using System;

namespace Janggi
{
    public static class ZobristHash
    {
        // 9x10 보드, 2개 팀, 7개 기물 타입에 대한 64비트 난수 테이블
        private static readonly ulong[,,,] PieceKeys = new ulong[9, 10, 2, 7];
        private static readonly ulong SideKey;

        static ZobristHash()
        {
            Random rng = new Random(2026);

            for (int x = 0; x < 9; x++)
            {
                for (int y = 0; y < 10; y++)
                {
                    for (int team = 0; team < 2; team++)
                    {
                        for (int piece = 0; piece < 7; piece++)
                        {
                            PieceKeys[x, y, team, piece] = NextULong(rng);
                        }
                    }
                }
            }

            SideKey = NextULong(rng);
        }

        private static ulong NextULong(Random rng)
        {
            byte[] buf = new byte[8];
            rng.NextBytes(buf);
            return BitConverter.ToUInt64(buf, 0);
        }

        public static ulong CalculateHash(PieceData[,] board, Team currentTurn)
        {
            ulong hash = 0;

            for (int x = 0; x < 9; x++)
            {
                for (int y = 0; y < 10; y++)
                {
                    PieceData p = board[x, y];
                    if (p != null && !p.IsCaptured)
                    {
                        int teamIdx = (int)p.Team;
                        int pieceIdx = (int)p.Type;
                        hash ^= PieceKeys[x, y, teamIdx, pieceIdx];
                    }
                }
            }

            if (currentTurn == Team.Han)
            {
                hash ^= SideKey;
            }

            return hash;
        }

        public static ulong TogglePiece(ulong hash, int x, int y, Team team, PieceType type)
        {
            return hash ^ PieceKeys[x, y, (int)team, (int)type];
        }

        public static ulong ToggleTurn(ulong hash)
        {
            return hash ^ SideKey;
        }
    }
}
