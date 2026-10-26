using UnityEngine;

namespace NineToFight.Game
{
    /// <summary>
    /// The classic brawler arena lock: once coworkers engage, the fight is fenced in. The arena opens around
    /// the player, widens to take in every engaged coworker, and stays shut until none are left. GameFlow
    /// applies it to the camera bounds and to the player's motor.
    /// </summary>
    public sealed class ArenaLock
    {
        /// <summary>Half the arena's width around the player when the fight starts (metres).</summary>
        public const float HalfWidth = 7f;
        /// <summary>Room kept beyond the outermost engaged coworker.</summary>
        public const float Margin = 1.5f;

        public bool Locked { get; private set; }
        public float Min { get; private set; }
        public float Max { get; private set; }

        int engaged;
        float lowest, highest;

        /// <summary>Starts a frame's count of engaged coworkers.</summary>
        public void BeginFrame()
        {
            engaged = 0;
            lowest = float.MaxValue;
            highest = float.MinValue;
        }

        /// <summary>An engaged coworker at this x.</summary>
        public void AddEngaged(float x)
        {
            engaged++;
            lowest = Mathf.Min(lowest, x);
            highest = Mathf.Max(highest, x);
        }

        /// <summary>
        /// Locks, widens or releases the arena. <paramref name="allowed"/> is false where the floor must stay
        /// open (a fire drill, between floors); the arena never reaches past the level's own bounds.
        /// </summary>
        public void EndFrame(float playerX, bool allowed, float levelMin, float levelMax)
        {
            if (!allowed || engaged == 0)
            {
                Locked = false;
                return;
            }
            if (!Locked)
            {
                Locked = true;
                Min = playerX - HalfWidth;
                Max = playerX + HalfWidth;
            }
            Min = Mathf.Max(Mathf.Min(Min, lowest - Margin), levelMin);
            Max = Mathf.Min(Mathf.Max(Max, highest + Margin), levelMax);
        }
    }
}
