using System.Collections.Generic;

namespace NineToFight.Combat
{
    /// <summary>
    /// Default move set (used when no player class is chosen) and moves shared by every class.
    /// Class-specific moves live on PlayerClass assets.
    /// </summary>
    public static class MoveLibrary
    {
        public static readonly FightingMove StaplerJab = new FightingMove
        {
            id = "stapler_jab", displayName = "Stapler Jab", slot = MoveSlot.Light, prop = "stapler", style = AttackStyle.Jab, unlockLevel = 1,
            description = "A quick, professional jab. Fastens opinions.",
            damage = 8f, reach = 0.9f, radius = 0.6f, knockback = 3f,
            startup = 0.09f, active = 0.08f, recovery = 0.16f, lunge = 2f, hitStop = 0.03f,
        };

        public static readonly FightingMove BriefcaseSwing = new FightingMove
        {
            id = "briefcase_swing", displayName = "Briefcase Swing", slot = MoveSlot.Heavy, prop = "briefcase", style = AttackStyle.Sweep, unlockLevel = 1,
            description = "Slow, heavy and full of quarterly reports.",
            damage = 18f, reach = 1.1f, radius = 0.8f, knockback = 9f, knockUp = 3f,
            startup = 0.24f, active = 0.12f, recovery = 0.32f, lunge = 4f, hitStop = 0.07f,
        };

        public static readonly FightingMove CoffeeSplash = new FightingMove
        {
            id = "coffee_splash", displayName = "Coffee Splash", slot = MoveSlot.Special, prop = "coffee_cup", style = AttackStyle.Toss, unlockLevel = 2,
            description = "Scalding hot. Wide arc. Ruins a shirt.",
            damage = 22f, reach = 1.5f, radius = 1.2f, knockback = 10f, knockUp = 4f,
            startup = 0.28f, active = 0.16f, recovery = 0.36f, cooldown = 2.5f, hitStop = 0.08f,
        };

        public static readonly FightingMove KeyboardSmash = new FightingMove
        {
            id = "keyboard_smash", displayName = "Keyboard Smash", slot = MoveSlot.Super, prop = "keyboard", style = AttackStyle.Spin, unlockLevel = 4,
            description = "asdfghjkl. Bypasses all firewalls.",
            damage = 38f, reach = 1.2f, radius = 1.0f, knockback = 14f, knockUp = 6f,
            startup = 0.36f, active = 0.18f, recovery = 0.45f, cooldown = 4f, lunge = 5f, hitStop = 0.12f,
        };

        public static readonly FightingMove ReplyAll = new FightingMove
        {
            id = "reply_all", displayName = "Reply All", slot = MoveSlot.Super, prop = "rolled_report", style = AttackStyle.Toss, unlockLevel = 8,
            description = "Three flung memos (underhand, sidearm, overhand) that hit everyone in range. Nobody asked for this.",
            damage = 12f, reach = 1.2f, radius = 1.5f, knockback = 7f, knockUp = 3f,
            startup = 0.2f, active = 0.08f, recovery = 0.22f, cooldown = 5f, lunge = 1.5f, hitStop = 0.06f, hits = 3,
            combo = new[] { AttackStyle.Sweep, AttackStyle.Hurl },
        };

        public static readonly IReadOnlyList<FightingMove> All = new[]
        {
            StaplerJab, BriefcaseSwing, CoffeeSplash, KeyboardSmash,
        };

        /// <summary>Moves every class can learn regardless of department.</summary>
        public static readonly IReadOnlyList<FightingMove> Shared = new[] { KeyboardSmash, ReplyAll };

        public static FightingMove Find(IReadOnlyList<FightingMove> moves, string id)
        {
            foreach (var move in moves)
                if (move.id == id) return move;
            return null;
        }
    }
}
