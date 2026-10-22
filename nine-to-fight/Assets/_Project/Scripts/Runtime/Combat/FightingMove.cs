using System;
using UnityEngine;

namespace NineToFight.Combat
{
    /// <summary>Light/Heavy come with your class, Special is your class signature, Super is shared and learned late.</summary>
    public enum MoveSlot { Light, Heavy, Special, Super }

    /// <summary>How the body moves for an attack; each weapon picks the motion that suits it.</summary>
    public enum AttackStyle
    {
        [Tooltip("Shoulder twist back, straight thrust forward.")] Jab,
        [Tooltip("One-handed overhead, brought down hard.")] Chop,
        [Tooltip("Two-handed overhead smash.")] Slam,
        [Tooltip("Horizontal swing across the body.")] Sweep,
        [Tooltip("High wind-up, wrist cracks forward.")] Whip,
        [Tooltip("Underhand fling.")] Toss,
        [Tooltip("Two-handed backswing, low strike, high follow-through.")] Golf,
        [Tooltip("Lean back, then thrust forward.")] Blast,
        [Tooltip("High and outside, cut down across the body.")] DiagonalSlash,
        [Tooltip("Hop up, crouch and slam straight down.")] Stamp,
        [Tooltip("Arm loops in full circles, then flings forward.")] Windmill,
        [Tooltip("Two-handed push with a big step in.")] Shove,
        [Tooltip("Deep fencing lunge with a long step.")] Lunge,
        [Tooltip("From across the body, slap outward.")] Backhand,
        [Tooltip("Coil, then a full 360 spin into the hit.")] Spin,
        [Tooltip("Crouch low, then drive up and through.")] Uppercut,
        [Tooltip("Bent-arm hook from the outside in.")] Hook,
        [Tooltip("Overhand pitch, like throwing a fastball.")] Hurl,
        [Tooltip("Two-handed horizontal cleave across the whole body.")] Cleave,
        [Tooltip("Short two-handed pound straight down.")] Pound,
        [Tooltip("Snapping straight punch with the lead (off) hand; the item stays on guard.")] LeadJab,
        [Tooltip("Chamber the rear knee, then a straight push kick.")] Kick,
        [Tooltip("Grab the collar and drive the rear knee up.")] Knee,
        [Tooltip("Arm folded, the elbow whips across at head height.")] Elbow,
        [Tooltip("Crouch, launch, and spear the rear leg out in mid-air.")] FlyingKick,
        [Tooltip("Leap, lie back in the air and plant both feet in them.")] Dropkick,
        [Tooltip("Hop off the rear leg, kick it back and fly in behind a straight punch.")] SupermanPunch,
    }

    /// <summary>Frame data and hit properties for one attack.</summary>
    [Serializable]
    public class FightingMove
    {
        public string id;
        public string displayName;
        [TextArea] public string description;
        public MoveSlot slot;
        [Tooltip("Id of the office item held in hand for this move (see PropCatalog). Empty = bare-handed.")]
        public string prop;
        [Tooltip("Body motion used to animate this move.")]
        public AttackStyle style;
        [Tooltip("For flurries: the strikes after the first (hit 2, 3...), so a combo isn't one swing repeated.")]
        public AttackStyle[] combo;
        [Tooltip("Career level at which this move is learned automatically.")]
        public int unlockLevel = 1;

        [Header("Hit")]
        public float damage = 10f;
        [Tooltip("Distance in front of the attacker where the hit sphere is centred.")]
        public float reach = 0.9f;
        public float radius = 0.6f;
        public float height = 1.2f;
        public float knockback = 4f;
        public float knockUp;
        public float hitStop = 0.04f;
        [Min(1), Tooltip("Times the whole swing repeats back-to-back (flurries). Each repeat can hit again.")]
        public int hits = 1;
        [Range(0f, 1f), Tooltip("Share of the damage dealt that heals the attacker.")]
        public float healOnHit;

        [Header("Timing (seconds)")]
        public float startup = 0.1f;
        public float active = 0.1f;
        public float recovery = 0.2f;
        public float cooldown;

        [Header("Movement")]
        [Tooltip("Forward impulse applied when the move becomes active.")]
        public float lunge;
        [Tooltip("Speed multiplier while the move plays out.")]
        public float moveMultiplier = 0.15f;

        public float TotalDuration => (startup + active + recovery) * Mathf.Max(1, hits);

        /// <summary>The strike thrown on a given hit of the move (0 = first).</summary>
        public AttackStyle StyleForHit(int hit) =>
            hit <= 0 || combo == null || combo.Length == 0 ? style : combo[(hit - 1) % combo.Length];

        public FightingMove Clone() => (FightingMove)MemberwiseClone();
    }
}
