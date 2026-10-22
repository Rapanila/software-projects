using System;
using NineToFight.Combat;
using UnityEngine;

namespace NineToFight.Characters
{
    /// <summary>
    /// The animation clips the office workers fight with: locomotion, stance, block, reactions, and one
    /// clip per attack style with where its wind-up starts, where the hit lands and where it has settled.
    /// Built by the content builder, which measures those times from each clip, plus how far each clip
    /// has to be lowered to keep the feet on the floor (the source skeletons put the ankle at different heights).
    /// </summary>
    public class FightAnimationSet : ScriptableObject
    {
        [Serializable]
        public class Strike
        {
            public AttackStyle style;
            public AnimationClip clip;
            [Tooltip("Clip seconds where the move's wind-up begins.")]
            public float start;
            [Tooltip("Clip seconds where the strike connects.")]
            public float impact;
            [Tooltip("Clip seconds where the follow-through has settled.")]
            public float end;
            [Tooltip("Height correction that puts the feet on the floor (body units).")]
            public float ground;
            [Tooltip("How far the feet leave the floor over the clip (body units, by clip seconds).")]
            public AnimationCurve lift = new AnimationCurve();
            [Tooltip("Share of the clip's jump kept (big Mixamo leaps are scaled down).")]
            public float liftKept = 1f;
            [Tooltip("Where the hit lands at impact, in degrees off straight ahead (+ = to the right), measured from the hips.")]
            public float landingYaw;
        }

        [Serializable]
        public class Grounded
        {
            public AnimationClip clip;
            public float ground;
        }

        public AnimationClip relaxedIdle;
        public AnimationClip fightIdle;
        public AnimationClip walk;
        public AnimationClip run;
        public AnimationClip block;
        public AnimationClip knockout;
        public AnimationClip[] hits;
        public Strike[] strikes;
        [Tooltip("Height corrections for the non-attack clips.")]
        public Grounded[] grounding;
        [Tooltip("Height of the skeleton's foot bones above the floor in the rest pose (body units).")]
        public float footHeight;
        [Tooltip("Extra height correction per body (RigFollower order): the bodies put the ankle at different heights.")]
        public float[] figureGround;
        [Tooltip("Arms and head: lets the guard stay up over the walk.")]
        public AvatarMask upperBody;
        [Tooltip("Ground speed (m/s) the walk and run cycles look right at.")]
        public float walkSpeed = 1.9f;
        public float runSpeed = 4.6f;

        /// <summary>The body parts that land each kind of strike (the item hand unless it's a kick, knee or elbow).</summary>
        public static HumanBodyBones[] StrikingBones(AttackStyle style)
        {
            switch (style)
            {
                case AttackStyle.LeadJab: return new[] { HumanBodyBones.LeftHand };
                case AttackStyle.Kick:
                case AttackStyle.FlyingKick:
                case AttackStyle.Dropkick: return new[] { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot };
                case AttackStyle.Knee: return new[] { HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg };
                case AttackStyle.Elbow: return new[] { HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm };
                default: return new[] { HumanBodyBones.RightHand };
            }
        }

        /// <summary>
        /// Kicks land with the leg extended, so the kicking foot is the one farthest out, not the fastest (the
        /// other leg often snaps its tuck faster). Everything else lands with the fastest striking limb.
        /// </summary>
        public static bool LandsExtended(AttackStyle style) =>
            style == AttackStyle.Kick || style == AttackStyle.FlyingKick || style == AttackStyle.Dropkick;

        public Strike For(AttackStyle style)
        {
            if (strikes == null) return null;
            foreach (var s in strikes)
                if (s != null && s.style == style && s.clip != null) return s;
            return null;
        }

        public float GroundFor(AnimationClip clip)
        {
            if (clip == null || grounding == null) return 0f;
            foreach (var g in grounding)
                if (g != null && g.clip == clip) return g.ground;
            return 0f;
        }
    }
}
