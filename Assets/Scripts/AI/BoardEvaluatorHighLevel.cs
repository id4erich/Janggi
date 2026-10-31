using System;
using UnityEngine;

namespace Janggi
{
    public static class BoardEvaluatorHighLevel
    {
        // 1. 기물 기본 가치
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

        // 2. 위치별 가산점 테이블 (Positional Weight Matrix)
        private static readonly float[,] PawnPosWeight = {
            { 0.0f, 0.0f, 0.0f, 0.2f, 0.3f, 0.2f, 0.0f, 0.0f, 0.0f },
            { 0.0f, 0.0f, 0.1f, 0.3f, 0.4f, 0.3f, 0.1f, 0.0f, 0.0f },
            { 0.1f, 0.2f, 0.3f, 0.5f, 0.6f, 0.5f, 0.3f, 0.2f, 0.1f },
            { 0.2f, 0.3f, 0.5f, 0.7f, 0.8f, 0.7f, 0.5f, 0.3f, 0.2f },
            { 0.3f, 0.4f, 0.6f, 0.8f, 1.0f, 0.8f, 0.6f, 0.4f, 0.3f },
            { 0.4f, 0.5f, 0.7f, 0.9f, 1.2f, 0.9f, 0.7f, 0.5f, 0.4f },
            { 0.5f, 0.6f, 0.8f, 1.0f, 1.3f, 1.0f, 0.8f, 0.6f, 0.5f },
            { 0.6f, 0.7f, 0.9f, 1.1f, 1.4f, 1.1f, 0.9f, 0.7f, 0.6f },
            { 0.7f, 0.8f, 1.0f, 1.2f, 1.5f, 1.2f, 1.0f, 0.8f, 0.7f },
            { 0.8f, 0.9f, 1.1f, 1.3f, 1.6f, 1.3f, 1.1f, 0.9f, 0.8f }
        };

        public static float Evaluate(PieceData[,] board, Team currentTeam)
        {
            float choScore = 0f;
            float hanScore = 1.5f; // 한(Red) 후공 덤 1.5점

            for (int x = 0; x < 9; x++)
            {
                for (int y = 0; y < 10; y++)
                {
                    PieceData piece = board[x, y];
                    if (piece == null || piece.IsCaptured) continue;

                    float baseVal = GetPieceValue(piece.Type);
                    float posBonus = 0f;

                    // 1) 위치 가산점 적용
                    if (piece.Type == PieceType.Pawn)
                    {
                        int row = (piece.Team == Team.Cho) ? y : (9 - y);
                        posBonus = PawnPosWeight[row, x];
                    }
                    else if (piece.Type == PieceType.Car)
                    {
                        // 중앙 열 점유 우대
                        if (x >= 3 && x <= 5) posBonus += 0.4f;
                    }
                    else if (piece.Type == PieceType.King || piece.Type == PieceType.Guard)
                    {
                        // 궁성 수비 완성도
                        if (RuleValidator.IsInPalace(piece.Position, piece.Team)) posBonus += 0.5f;
                    }

                    // 2) 기물 움직임 가능 수(Mobility) 보너스
                    var validMoves = RuleValidator.GetValidMoves(piece, board);
                    float mobilityBonus = validMoves.Count * 0.05f;

                    float totalPieceScore = baseVal + posBonus + mobilityBonus;

                    if (piece.Team == Team.Cho)
                        choScore += totalPieceScore;
                    else
                        hanScore += totalPieceScore;
                }
            }

            return currentTeam == Team.Cho ? (choScore - hanScore) : (hanScore - choScore);
        }
    }
}
