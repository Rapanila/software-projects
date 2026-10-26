using UnityEngine;

namespace NineToFight.Progression
{
    /// <summary>Rank titles and XP curve: one rung per level, from Intern to CEO.</summary>
    public static class CareerLadder
    {
        static readonly string[] Ranks =
        {
            "Intern",
            "Junior Associate",
            "Associate",
            "Senior Associate",
            "Team Lead",
            "Manager",
            "Senior Manager",
            "Director",
            "Vice President",
            "CEO",
        };

        public static int MaxLevel => Ranks.Length;

        public static string RankTitle(int level) => Ranks[Mathf.Clamp(level, 1, MaxLevel) - 1];

        /// <summary>
        /// XP from one rank to the next: 100, then 165 more each rank (6,840 to CEO). Paced to the tower's knockout
        /// XP (which grows 15% a floor): about rank 3 after floor 1, rank 9 (the Super move) on floor 7 and CEO late
        /// on floor 8, so XP and Career Coaching keep mattering all the way up.
        /// </summary>
        public static int XpToNextLevel(int level) => 100 + (Mathf.Max(level, 1) - 1) * 165;
    }
}
