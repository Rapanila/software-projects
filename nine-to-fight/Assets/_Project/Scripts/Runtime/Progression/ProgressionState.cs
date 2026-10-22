using System;
using System.Collections.Generic;
using NineToFight.Combat;

namespace NineToFight.Progression
{
    /// <summary>Plain-data career progress: level, XP and learned moves from a class move set.</summary>
    public class ProgressionState
    {
        readonly HashSet<string> trainedMoves = new HashSet<string>();
        readonly IReadOnlyList<FightingMove> moves;

        public ProgressionState(IReadOnlyList<FightingMove> moveSet = null)
        {
            moves = moveSet ?? MoveLibrary.All;
        }

        public int Level { get; private set; } = 1;
        public int Xp { get; private set; }
        public int XpToNext => CareerLadder.XpToNextLevel(Level);
        public bool IsMaxLevel => Level >= CareerLadder.MaxLevel;
        public string Rank => CareerLadder.RankTitle(Level);
        public IReadOnlyList<FightingMove> Moves => moves;

        public event Action<int> LeveledUp;
        public event Action<FightingMove> MoveUnlocked;

        /// <returns>Number of levels gained.</returns>
        public int AddXp(int amount)
        {
            if (amount <= 0 || IsMaxLevel) return 0;

            int gained = 0;
            Xp += amount;
            while (!IsMaxLevel && Xp >= XpToNext)
            {
                Xp -= XpToNext;
                Level++;
                gained++;
                LeveledUp?.Invoke(Level);
                foreach (var move in moves)
                    if (move.unlockLevel == Level && !trainedMoves.Contains(move.id))
                        MoveUnlocked?.Invoke(move);
            }
            if (IsMaxLevel) Xp = 0;
            return gained;
        }

        /// <returns>False if the move isn't in this move set or is already available.</returns>
        public bool UnlockMove(string moveId)
        {
            var move = MoveLibrary.Find(moves, moveId);
            if (move == null || IsUnlocked(move)) return false;
            trainedMoves.Add(moveId);
            MoveUnlocked?.Invoke(move);
            return true;
        }

        public bool IsUnlocked(FightingMove move) =>
            move != null && (Level >= move.unlockLevel || trainedMoves.Contains(move.id));

        /// <summary>The highest-tier unlocked move for a slot, or null.</summary>
        public FightingMove BestMove(MoveSlot slot)
        {
            FightingMove best = null;
            foreach (var move in moves)
                if (move.slot == slot && IsUnlocked(move) && (best == null || move.unlockLevel > best.unlockLevel))
                    best = move;
            return best;
        }

        /// <summary>Number keys 1..9 and 0 map to ten hotbar slots, filled in move-set order.</summary>
        public const int HotbarSize = 10;

        public static string HotbarKey(int index) => index == 9 ? "0" : (index + 1).ToString();

        /// <summary>The move in a hotbar slot (locked or not), or null if the slot is empty.</summary>
        public FightingMove HotbarMove(int index) =>
            index >= 0 && index < HotbarSize && index < moves.Count ? moves[index] : null;

        /// <summary>The next not-yet-learned move for a slot, for "unlocks at Lv X" hints.</summary>
        public FightingMove NextLocked(MoveSlot slot)
        {
            FightingMove next = null;
            foreach (var move in moves)
                if (move.slot == slot && !IsUnlocked(move) && (next == null || move.unlockLevel < next.unlockLevel))
                    next = move;
            return next;
        }
    }
}
