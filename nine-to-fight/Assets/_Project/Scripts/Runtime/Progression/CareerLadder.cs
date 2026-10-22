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

        public static int XpToNextLevel(int level) => 40 + (Mathf.Max(level, 1) - 1) * 30;
    }
}
