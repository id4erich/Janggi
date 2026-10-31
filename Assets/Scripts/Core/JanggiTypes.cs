using System;
using System.Collections.Generic;
using UnityEngine;

namespace Janggi
{
    public enum PieceType
    {
        King,     // 궁(楚/漢)
        Guard,    // 사(士)
        Car,      // 차(車)
        Cannon,   // 포(包)
        Horse,    // 마(馬)
        Elephant, // 상(象)
        Pawn      // 병/졸(卒/兵)
    }

    public static class PieceHelper
    {
        public static string GetHanja(PieceType type, Team team)
        {
            switch (type)
            {
                case PieceType.King: return team == Team.Cho ? "楚" : "漢";
                case PieceType.Guard: return "士";
                case PieceType.Car: return "車";
                case PieceType.Cannon: return "包";
                case PieceType.Horse: return "馬";
                case PieceType.Elephant: return "象";
                case PieceType.Pawn: return team == Team.Cho ? "卒" : "兵";
                default: return "";
            }
        }
    }

    public enum Team
    {
        Cho, // 초 (Green/Blue, 선공)
        Han  // 한 (Red, 후공)
    }

    public enum PlacementType
    {
        MaSangMaSang, // 마상마상 (기본)
        SangMaSangMa, // 상마상마
        MaSangSangMa, // 마상상마
        SangMaMaSang  // 상마마상
    }

    [Serializable]
    public struct BoardPos
    {
        public int x;
        public int y;

        public BoardPos(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        public static bool operator ==(BoardPos a, BoardPos b) => a.x == b.x && a.y == b.y;
        public static bool operator !=(BoardPos a, BoardPos b) => !(a == b);
        public override bool Equals(object obj) => obj is BoardPos other && this == other;
        public override int GetHashCode() => HashCode.Combine(x, y);
        public override string ToString() => $"({x}, {y})";
    }

    public class PieceData
    {
        public PieceType Type;
        public Team Team;
        public BoardPos Position;
        public bool IsCaptured;

        public PieceData(PieceType type, Team team, BoardPos position)
        {
            Type = type;
            Team = team;
            Position = position;
            IsCaptured = false;
        }

        public PieceData Clone()
        {
            return new PieceData(Type, Team, Position) { IsCaptured = IsCaptured };
        }
    }
}
