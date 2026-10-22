using UnityEngine;

namespace NineToFight.Game
{
    /// <summary>Counts consecutive player hits; the chain breaks if you stop hitting or get hit.</summary>
    public class ComboTracker
    {
        public const float BaseWindow = 2.2f;
        /// <summary>Seconds between hits before the chain breaks (perks lengthen it).</summary>
        public float Window { get; set; } = BaseWindow;
        const int MaxBonusAt = 50;

        static readonly (int hits, string title)[] Ratings =
        {
            (50, "CEO MATERIAL"),
            (35, "10x EMPLOYEE"),
            (20, "DISRUPTIVE!!"),
            (10, "SYNERGY!"),
            (5, "PRODUCTIVE"),
        };

        float lastHitTime = float.NegativeInfinity;

        public int Count { get; private set; }
        public int Best { get; private set; }
        public float LastHitTime => lastHitTime;

        public bool IsActive(float now) => Count > 0 && now - lastHitTime <= Window;

        /// <summary>Registers a hit. Returns true when the hit reaches a new rating tier.</summary>
        public bool RegisterHit(float now)
        {
            if (!IsActive(now)) Count = 0;
            string before = Rating(Count);
            Count++;
            lastHitTime = now;
            if (Count > Best) Best = Count;
            return Rating(Count) != before;
        }

        public void Break() => Count = 0;

        /// <summary>Remaining fraction of the combo window, for the HUD timer bar.</summary>
        public float WindowLeft(float now) => IsActive(now) ? 1f - (now - lastHitTime) / Window : 0f;

        /// <summary>XP multiplier for a knockout landed during this combo: up to x2.</summary>
        public float XpMultiplier(float now) => IsActive(now) ? 1f + Mathf.Min(Count, MaxBonusAt) / (float)MaxBonusAt : 1f;

        public static string Rating(int count)
        {
            foreach (var (hits, title) in Ratings)
                if (count >= hits) return title;
            return "";
        }
    }
}
