using UnityEngine;

namespace NineToFight.Combat
{
    /// <summary>Simple time-based cooldown; pass in the current time so it stays testable.</summary>
    public class Cooldown
    {
        float readyAt = float.NegativeInfinity;

        public float Duration { get; private set; }

        public bool IsReady(float now) => now >= readyAt;

        public float Remaining(float now) => Mathf.Max(0f, readyAt - now);

        /// <summary>1 right after triggering, falling to 0 when ready.</summary>
        public float Fraction(float now) => Duration > 0f ? Remaining(now) / Duration : 0f;

        public void Trigger(float now, float duration)
        {
            Duration = duration;
            readyAt = now + duration;
        }

        public void Reset() => readyAt = float.NegativeInfinity;
    }
}
