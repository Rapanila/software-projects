using System;
using System.Linq;
using NineToFight.Combat;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace NineToFight.Characters
{
    /// <summary>
    /// Plays the fight animation clips on one body. Three layers:
    /// a base locomotion blend (relaxed idle, fight stance, block, walk, run), the guard held up over the
    /// walk (arms only), and actions on top (attacks, hit reactions, the knockout), cross-faded so combos
    /// flow from one strike into the next. Attack clips are scrubbed to the move's frame data: the clip's
    /// own impact frame lands exactly when the move's hit window does. Evaluated manually each frame.
    /// </summary>
    public sealed class FightAnimator : IDisposable
    {
        public struct Locomotion
        {
            public float walk, air, stance, block, speed;
        }

        enum Kind { None, Attack, Reaction, Knockout }

        const int RelaxedInput = 0, FightInput = 1, BlockInput = 2, WalkInput = 3, RunInput = 4, AirInput = 5;
        /// <summary>Fastest and slowest a strike's wind-up and recovery may be played (1 = as authored).</summary>
        public const float MaxWindupSpeedUp = 2.4f, MinWindupSpeed = 0.75f, MaxRecoverySpeedUp = 2.2f, MinRecoverySpeed = 0.8f;

        readonly FightAnimationSet set;
        readonly float[] slotGround = new float[2];
        readonly float[] slotYaw = new float[2];
        readonly Quaternion restRotation;
        readonly Vector3 headForward;
        Transform neck, head;
        float alignRamp, aimWeight;

        /// <summary>Turn each strike so the hit lands straight ahead (measured where the limb is at impact).</summary>
        public static bool AlignStrikes = true;
        /// <summary>Turn into the strike gradually over the wind-up instead of from its first frame.</summary>
        public static bool RampAlignment = true;
        /// <summary>How strongly the face stays on the opponent on top of the clips (0 = clips only).</summary>
        public static float HeadAim = 0.75f;
        const float MaxAlign = 70f, MaxHeadTurn = 80f;
        readonly float restY, bodyScale;
        FightAnimationSet.Strike strike;
        PlayableGraph graph;
        AnimationLayerMixerPlayable layers;
        AnimationMixerPlayable baseMix, guardMix, actionMix;
        AnimationClipPlayable walk, run, air;
        readonly AnimationClipPlayable[] slots = new AnimationClipPlayable[2];
        int current = -1;
        float crossfade = 1f, actionWeight;

        Kind kind;
        AnimationClip actionClip;
        float actionStart, fadeIn = 0.08f, fadeOut = 0.15f;
        // Attack timing: the move's frame data and the clip's measured window.
        float preImpact, postImpact, clipStart, clipImpact, clipEnd;
        // Reactions: how long the flinch shows before it fades back to the stance.
        float reactionEnd;
        bool hold;
        int nextHit;

        public Animator Animator { get; }
        float extraGround;
        // Feet on the floor for the stance and locomotion: measured after each evaluation.
        Transform leftFoot, rightFoot;
        readonly float sole;
        float groundedY;
        bool groundedYet, airborne;
        public bool Valid => graph.IsValid();
        /// <summary>True while an action (attack, reaction, knockout) is showing.</summary>
        public bool Acting => kind != Kind.None || actionWeight > 0.01f;

        public FightAnimator(Animator animator, FightAnimationSet set, float figureGround = 0f, Vector3? headForward = null)
        {
            this.headForward = headForward ?? Vector3.forward;
            restRotation = animator.transform.localRotation;
            extraGround = figureGround;
            Animator = animator;
            this.set = set;
            restY = animator.transform.localPosition.y;
            bodyScale = animator.transform.localScale.y;
            // Where a planted foot belongs: the skeleton's foot height in its rest pose, on the floor.
            sole = restY + set.footHeight * bodyScale;
            graph = PlayableGraph.Create(animator.name + " Fight");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = AnimationPlayableOutput.Create(graph, "Body", animator);

            layers = AnimationLayerMixerPlayable.Create(graph, 3);
            output.SetSourcePlayable(layers);

            baseMix = AnimationMixerPlayable.Create(graph, 6);
            Connect(Loop(set.relaxedIdle), baseMix, RelaxedInput);
            Connect(Loop(set.fightIdle), baseMix, FightInput);
            Connect(Loop(set.block), baseMix, BlockInput);
            walk = Loop(set.walk);
            run = Loop(set.run);
            Connect(walk, baseMix, WalkInput);
            Connect(run, baseMix, RunInput);
            // In the air: a frozen mid-stride frame of the run.
            air = Clip(set.run);
            air.SetSpeed(0);
            air.SetTime(set.run != null ? set.run.length * 0.3f : 0f);
            Connect(air, baseMix, AirInput);
            graph.Connect(baseMix, 0, layers, 0);
            layers.SetInputWeight(0, 1f);

            guardMix = AnimationMixerPlayable.Create(graph, 2);
            Connect(Loop(set.fightIdle), guardMix, 0);
            Connect(Loop(set.block), guardMix, 1);
            graph.Connect(guardMix, 0, layers, 1);
            if (set.upperBody != null) layers.SetLayerMaskFromAvatarMask(1, set.upperBody);

            actionMix = AnimationMixerPlayable.Create(graph, 2);
            graph.Connect(actionMix, 0, layers, 2);

            graph.Play();
        }

        // Imported stance loops need their authored goals to preserve foot placement when retargeted.
        // Native locomotion includes matching goals for blending; its own bone pose needs no IK pass.
        AnimationClipPlayable Clip(AnimationClip clip, bool footIK = false)
        {
            var p = AnimationClipPlayable.Create(graph, clip);
            p.SetApplyFootIK(footIK);
            return p;
        }

        AnimationClipPlayable Loop(AnimationClip clip, bool footIK = false)
        {
            var p = Clip(clip, footIK || clip == set.fightIdle || clip == set.block);
            // Start loops at a random point so a crowd doesn't move in step.
            if (clip != null) p.SetTime(UnityEngine.Random.value * clip.length);
            return p;
        }

        void Connect(Playable p, Playable mixer, int input) => graph.Connect(p, 0, mixer, input);

        float LowestFoot()
        {
            // The Animator only hands out its bones once it has initialised.
            if (leftFoot == null) leftFoot = Animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            if (rightFoot == null) rightFoot = Animator.GetBoneTransform(HumanBodyBones.RightFoot);
            var parent = Animator.transform.parent;
            if (leftFoot == null || rightFoot == null || parent == null) return sole;
            return Mathf.Min(parent.InverseTransformPoint(leftFoot.position).y, parent.InverseTransformPoint(rightFoot.position).y);
        }

        public void Dispose()
        {
            if (graph.IsValid()) graph.Destroy();
            if (Animator != null)
            {
                var position = Animator.transform.localPosition;
                position.y = restY;
                Animator.transform.localPosition = position;
                Animator.transform.localRotation = restRotation;
            }
        }

        // ------------------------------------------------------------------ actions

        /// <summary>One strike of a move, timed so the clip's hit lands in the move's hit window.</summary>
        public bool Attack(FightingMove move, AttackStyle style, bool moreToCome, float now)
        {
            var found = set.For(style);
            if (found == null) return false;
            strike = found;
            var window = StrikeWindow(strike, move);
            preImpact = window.preImpact;
            postImpact = window.postImpact;
            clipStart = window.clipStart;
            clipImpact = strike.impact;
            clipEnd = window.clipEnd;
            hold = moreToCome;
            Begin(strike.clip, Kind.Attack, now, Mathf.Clamp(move.startup * 0.5f, 0.04f, 0.1f), Mathf.Clamp(move.recovery * 0.6f, 0.08f, 0.22f));
            slotGround[current] = strike.ground;
            slotYaw[current] = AlignStrikes ? Mathf.Clamp(-strike.landingYaw, -MaxAlign, MaxAlign) : 0f;
            return true;
        }

        /// <summary>How one strike is scrubbed to a move's frame data.</summary>
        public struct Window
        {
            /// <summary>Move seconds before and after the impact.</summary>
            public float preImpact, postImpact;
            /// <summary>Clip seconds shown over the move: from clipStart through the impact to clipEnd.</summary>
            public float clipStart, clipEnd;
            /// <summary>Playback rate of the wind-up (1 = as authored).</summary>
            public float WindupRate(FightAnimationSet.Strike strike) => (strike.impact - clipStart) / preImpact;
            /// <summary>Playback rate of the recovery (1 = as authored).</summary>
            public float RecoveryRate(FightAnimationSet.Strike strike) => (clipEnd - strike.impact) / postImpact;
        }

        /// <summary>
        /// Lines a strike's clip up with a move: the clip's impact frame lands at the move's impact time, and
        /// the wind-up and recovery are trimmed (or extended) so neither plays unbelievably fast or slow.
        /// </summary>
        public static Window StrikeWindow(FightAnimationSet.Strike strike, FightingMove move)
        {
            var w = new Window();
            w.preImpact = Mathf.Max(FightingMove.ImpactTime(move.startup, move.active), 0.01f);
            w.postImpact = Mathf.Max(move.startup + move.active + move.recovery - w.preImpact, 0.01f);
            float impact = strike.impact;
            w.clipStart = Mathf.Clamp(strike.start, impact - w.preImpact * MaxWindupSpeedUp, impact - w.preImpact * MinWindupSpeed);
            w.clipStart = Mathf.Max(0f, w.clipStart);
            w.clipEnd = Mathf.Clamp(strike.end, impact + w.postImpact * MinRecoverySpeed, impact + w.postImpact * MaxRecoverySpeedUp);
            w.clipEnd = Mathf.Min(strike.clip.length, w.clipEnd);
            return w;
        }

        /// <summary>
        /// Flinch from a hit (alternating head and body hits). The flinch shows for <paramref name="duration"/>
        /// seconds (the stun) and then fades back, rather than playing the whole clip after control returns.
        /// </summary>
        public void React(float now, float duration = float.MaxValue)
        {
            if (set.hits == null || set.hits.Length == 0) return;
            var clip = set.hits[nextHit++ % set.hits.Length];
            Begin(clip, Kind.Reaction, now, 0.04f, 0.2f);
            reactionEnd = Mathf.Min(duration + fadeOut, clip.length);
            slotGround[current] = set.GroundFor(clip);
            slotYaw[current] = 0f;
        }

        public void Knockout(float now)
        {
            if (set.knockout == null) return;
            Begin(set.knockout, Kind.Knockout, now, 0.08f, 0.2f);
            slotGround[current] = set.GroundFor(set.knockout);
            slotYaw[current] = 0f;
        }

        /// <summary>Drops the current action (interrupted); it fades back to the stance.</summary>
        public void Cancel()
        {
            if (kind != Kind.Knockout) kind = Kind.None;
        }

        void Begin(AnimationClip clip, Kind newKind, float now, float fadeInTime, float fadeOutTime)
        {
            int next = current < 0 ? 0 : 1 - current;
            if (slots[next].IsValid())
            {
                graph.Disconnect(actionMix, next);
                slots[next].Destroy();
            }
            slots[next] = Clip(clip);
            slots[next].SetSpeed(0); // time is set by hand every frame
            Connect(slots[next], actionMix, next);
            // Cross-fade from whatever action was showing; from the stance, the layer weight does the fade.
            crossfade = actionWeight > 0.01f ? 0f : 1f;
            current = next;
            kind = newKind;
            actionClip = clip;
            actionStart = now;
            fadeIn = fadeInTime;
            fadeOut = fadeOutTime;
        }

        float AttackClipTime(float t)
        {
            if (t < preImpact) return Mathf.Lerp(clipStart, clipImpact, t / preImpact);
            return Mathf.Lerp(clipImpact, clipEnd, (t - preImpact) / postImpact);
        }

        // ------------------------------------------------------------------ per frame

        public void Evaluate(Locomotion loco, float now, float dt)
        {
            if (!graph.IsValid()) return;

            // Base: stand (relaxed / fight / block), walk or run, or airborne.
            float onFloor = 1f - loco.air, stand = 1f - loco.walk;
            airborne = loco.air > 0.05f;
            float running = Mathf.Clamp01((loco.speed - 2.8f) / 1.6f);
            baseMix.SetInputWeight(RelaxedInput, onFloor * stand * (1f - loco.stance) * (1f - loco.block));
            baseMix.SetInputWeight(FightInput, onFloor * stand * loco.stance * (1f - loco.block));
            baseMix.SetInputWeight(BlockInput, onFloor * stand * loco.block);
            baseMix.SetInputWeight(WalkInput, onFloor * loco.walk * (1f - running));
            baseMix.SetInputWeight(RunInput, onFloor * loco.walk * running);
            baseMix.SetInputWeight(AirInput, loco.air);
            walk.SetSpeed(Mathf.Clamp(loco.speed / Mathf.Max(set.walkSpeed, 0.1f), 0.6f, 1.6f));
            run.SetSpeed(Mathf.Clamp(loco.speed / Mathf.Max(set.runSpeed, 0.1f), 0.7f, 1.5f));

            // Moving in a fight keeps the guard (or the block) up over the walk.
            layers.SetInputWeight(1, onFloor * loco.walk * Mathf.Max(loco.stance, loco.block) * (1f - 0.6f * running));
            guardMix.SetInputWeight(0, 1f - loco.block);
            guardMix.SetInputWeight(1, loco.block);

            // Actions.
            float target = 0f;
            if (kind != Kind.None && actionClip != null && slots[current].IsValid())
            {
                float t = now - actionStart;
                switch (kind)
                {
                    case Kind.Attack:
                    {
                        float total = preImpact + postImpact;
                        float clipTime = AttackClipTime(Mathf.Min(t, total));
                        slots[current].SetTime(clipTime);
                        // Turn into the strike over the wind-up, square to the target by the impact.
                        alignRamp = RampAlignment ? Smooth(t / preImpact) : 1f;
                        // Big leaps are scaled down: lower the body by the share of the jump not kept.
                        slotGround[current] = strike.ground - strike.lift.Evaluate(clipTime) * (1f - strike.liftKept);
                        if (t >= total && !hold) kind = Kind.None;
                        else target = hold || t < total - fadeOut ? 1f : 0f;
                        break;
                    }
                    case Kind.Reaction:
                        slots[current].SetTime(Mathf.Min(t, actionClip.length));
                        if (t >= reactionEnd) kind = Kind.None;
                        else target = t < reactionEnd - fadeOut ? 1f : 0f;
                        break;
                    case Kind.Knockout:
                        slots[current].SetTime(Mathf.Min(t, actionClip.length));
                        target = 1f;
                        break;
                }
            }
            float rate = target > actionWeight ? 1f / Mathf.Max(fadeIn, 0.01f) : 1f / Mathf.Max(fadeOut, 0.01f);
            actionWeight = Mathf.MoveTowards(actionWeight, target, dt * rate);
            crossfade = Mathf.MoveTowards(crossfade, 1f, dt / Mathf.Max(fadeIn, 0.01f));
            if (current >= 0)
            {
                actionMix.SetInputWeight(current, crossfade);
                actionMix.SetInputWeight(1 - current, slots[1 - current].IsValid() ? 1f - crossfade : 0f);
            }
            layers.SetInputWeight(2, actionWeight);

            graph.Evaluate(dt);

            // Feet on the floor. The stance and locomotion are grounded by measuring the planted foot after each
            // evaluation; actions use their measured correction, so leaps still leave the floor.
            var position = Animator.transform.localPosition;
            float needed = position.y + (sole - LowestFoot());
            // Measured after this frame's pose, so it's exact: the planted foot sits on the floor every frame.
            // Only in the air (jumps) is it eased, so landing doesn't pop.
            groundedY = groundedYet && airborne ? Mathf.MoveTowards(groundedY, needed, dt * 3f) : needed;
            groundedYet = true;
            float acting = 0f;
            if (current >= 0)
            {
                acting = actionMix.GetInputWeight(current) * slotGround[current];
                if (slots[1 - current].IsValid()) acting += actionMix.GetInputWeight(1 - current) * slotGround[1 - current];
            }
            float actionY = restY + (acting + extraGround) * bodyScale;
            position.y = Mathf.Lerp(groundedY, actionY, actionWeight);
            Animator.transform.localPosition = position;

            // Strikes land straight ahead: the whole body turns by each strike's measured landing angle.
            float yaw = 0f;
            if (current >= 0)
            {
                yaw = actionMix.GetInputWeight(current) * slotYaw[current] * alignRamp;
                if (slots[1 - current].IsValid()) yaw += actionMix.GetInputWeight(1 - current) * slotYaw[1 - current];
            }
            Animator.transform.localRotation = restRotation * Quaternion.Euler(0f, yaw * actionWeight, 0f);

            // Eyes on the opponent: in a fight the face is turned back toward the target on top of the clip.
            float aim = HeadAim * loco.stance * (kind == Kind.Knockout ? 0f : kind == Kind.Reaction ? 0.35f : 1f);
            aimWeight = Mathf.MoveTowards(aimWeight, aim, dt * 4f);
            if (aimWeight > 0.001f) AimHead(aimWeight);
        }

        /// <summary>Turns the neck and head (about the vertical) so the face points along the character's facing.</summary>
        void AimHead(float weight)
        {
            if (head == null) head = Animator.GetBoneTransform(HumanBodyBones.Head);
            if (neck == null) neck = Animator.GetBoneTransform(HumanBodyBones.Neck);
            var parent = Animator.transform.parent;
            if (head == null || parent == null) return;
            var target = parent.forward;
            target.y = 0f;
            var face = head.rotation * headForward;
            var flat = new Vector3(face.x, 0f, face.z);
            // Looking straight up or down (a dropkick, the floor), there's no sensible way to turn.
            float level = Mathf.Clamp01((flat.magnitude - 0.3f) / 0.4f);
            if (level <= 0f || target.sqrMagnitude < 1e-4f) return;
            float error = Mathf.Clamp(Vector3.SignedAngle(flat, target, Vector3.up), -MaxHeadTurn, MaxHeadTurn);
            float turn = error * weight * level;
            if (neck != null)
            {
                neck.rotation = Quaternion.AngleAxis(turn * 0.4f, Vector3.up) * neck.rotation;
                head.rotation = Quaternion.AngleAxis(turn * 0.6f, Vector3.up) * head.rotation;
            }
            else
            {
                head.rotation = Quaternion.AngleAxis(turn, Vector3.up) * head.rotation;
            }
        }

        static float Smooth(float k)
        {
            k = Mathf.Clamp01(k);
            return k * k * (3f - 2f * k);
        }
    }
}
