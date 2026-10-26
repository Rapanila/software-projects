using System.Collections;
using NineToFight.Combat;
using UnityEngine;

namespace NineToFight.Characters
{
    /// <summary>
    /// Presents one office worker. Every frame it works out the locomotion state (walking, running, airborne,
    /// guard up, blocking) and hands it with the current action to the <see cref="FightAnimator"/>, which plays
    /// the Mixamo clips on the body <see cref="RigFollower"/> has active. On top of the clips it adds a sprung
    /// jolt of the spine on hits and heavy impacts, a squash of the whole figure, the swing trail through each
    /// strike, footstep events, per-class colours and hit flashes.
    /// Without an animation set (or before the Animator is up) the body simply holds its rest pose.
    /// </summary>
    public class CharacterVisual : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Tooltip("Whole-body pivot at the feet: squashed on hits and landings.")]
        [SerializeField] Transform model;
        [Tooltip("Hand socket holding the weapon; the swing trail rides on it.")]
        [SerializeField] Transform wrist;
        [Tooltip("Shirt/sleeve renderers recoloured per class.")]
        [SerializeField] Renderer[] shirtRenderers;
        [Tooltip("Tie renderers recoloured per class.")]
        [SerializeField] Renderer[] accentRenderers;
        [Tooltip("Animation clips to fight with.")]
        [SerializeField] FightAnimationSet animationSet;

        [Header("Locomotion")]
        [Tooltip("Metres per step. Raise to slow the footstep cadence, lower to speed it up.")]
        [SerializeField] float strideLength = DefaultStrideLength;

        /// <summary>Metres travelled per step; longer strides mean a slower step cadence.</summary>
        public const float DefaultStrideLength = 1.9f;

        /// <summary>Footsteps per second at a given ground speed.</summary>
        public static float StepsPerSecond(float speed, float strideLength) => speed / Mathf.Max(strideLength, 0.05f);

        /// <summary>A hit flinch that isn't given a length lasts this long before fading back to the stance.</summary>
        public const float DefaultFlinch = 0.4f;

        RigFollower follower;
        FightAnimator clips;
        int clipRig = -1;
        bool impactPlayed;
        // The Animator wipes renderer colour overrides when it starts up; put them back once it has.
        int recolorFrames;
        // Hits jolt the upper spine on top of the animation.
        SpringRotation joltSpring;

        /// <summary>True when animation clips are posing the body.</summary>
        public bool UsesClips => EnsureClips();

        /// <summary>Set by the controller/AI: raises the guard into a fighting stance.</summary>
        public bool InCombat { get; set; }

        /// <summary>Set while actually blocking: both forearms up.</summary>
        public bool Blocking { get; set; }

        /// <summary>Item-specific carrying leaves strikes, reactions and blocks to their animation.</summary>
        public bool CanCarryItem => !Blocking && !knockedOut && attack == null &&
            Time.time - hitStart > 0.6f && (clips == null || !clips.Acting);

        /// <summary>A foot hit the floor (or the whole body landed). Strength ~1 for a step, ~2+ for a landing.</summary>
        public static event System.Action<CharacterVisual, Vector3, float> Stepped;

        TrailRenderer swingTrail;

        Renderer[] renderers;
        Color[] baseColors;
        MaterialPropertyBlock block;
        CharacterMotor2_5D motor;
        bool initialized;
        Vector3 modelScale;

        float walkPhase, walkBlend, airBlend, stance, blockBlend, run, lastLift;
        float lastVerticalSpeed;
        bool wasGrounded = true;

        FightingMove attack;
        AttackStyle attackStyle;
        float attackStart, lastAttackTime = -10f;

        float hitStart = -10f;
        bool knockedOut;

        Color flashColor;
        float flashStart, flashDuration;
        bool flashing;
        Coroutine squashRoutine;

        void Awake() => EnsureInit();

        void EnsureInit()
        {
            if (initialized) return;
            initialized = true;
            if (model == null) model = transform;
            motor = GetComponentInParent<CharacterMotor2_5D>();
            follower = GetComponent<RigFollower>();
            block = new MaterialPropertyBlock();
            renderers = GetComponentsInChildren<Renderer>(true);
            baseColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                var mat = renderers[i].sharedMaterial;
                baseColors[i] = mat != null && mat.HasProperty(BaseColorId) ? mat.GetColor(BaseColorId) : Color.white;
            }
            modelScale = model.localScale;
            CreateSwingTrail();
        }

        /// <summary>Makes sure the clip player is running on the body currently shown.</summary>
        bool EnsureClips()
        {
            EnsureInit();
            if (animationSet == null || follower == null) return false;
            var rig = follower.ActiveRig;
            if (rig == null || rig.animator == null || !rig.animator.isActiveAndEnabled) return false;
            if (clips != null && clips.Valid && clipRig == follower.Active && clips.Animator == rig.animator) return true;
            clips?.Dispose();
            float figureGround = animationSet.figureGround != null && follower.Active < animationSet.figureGround.Length
                ? animationSet.figureGround[follower.Active] : 0f;
            clips = new FightAnimator(rig.animator, animationSet, figureGround, rig.headForward);
            clipRig = follower.Active;
            follower.ClipDriven = true;
            recolorFrames = 2;
            joltSpring.Reset(Quaternion.identity);
            // A body swapped in mid-knockout stays down.
            if (knockedOut) clips.Knockout(Time.time);
            return true;
        }

        void OnDisable()
        {
            clips?.Dispose();
            clips = null;
            // A body that's switched off and on again (pooled, or revived) comes back standing.
            knockedOut = false;
        }

        // ------------------------------------------------------------------ colours

        public void SetColors(Color shirt, Color accent)
        {
            EnsureInit();
            Recolor(shirtRenderers, shirt);
            Recolor(accentRenderers, accent);
            TintTrail(accent);
        }

        /// <summary>A ribbon off the end of the held item, drawn only through the strike of a swing.</summary>
        void CreateSwingTrail()
        {
            if (wrist == null) return;
            var mat = Resources.Load<Material>("FX/Trail");
            if (mat == null) return;
            var go = new GameObject("Swing Trail");
            go.transform.SetParent(wrist, false);
            go.transform.localPosition = DefaultTrailTip;
            swingTrail = go.AddComponent<TrailRenderer>();
            swingTrail.sharedMaterial = mat;
            swingTrail.time = 0.16f;
            swingTrail.minVertexDistance = 0.02f;
            swingTrail.widthMultiplier = 0.34f;
            swingTrail.widthCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
            swingTrail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            swingTrail.receiveShadows = false;
            swingTrail.emitting = false;
            TintTrail(Color.white);
        }

        /// <summary>Where the trail rides without an item: just past the fist.</summary>
        public static readonly Vector3 DefaultTrailTip = new Vector3(0f, -0.12f, 0f);

        /// <summary>Puts the swing trail at the far end of the held item (hand-socket space); null for a bare hand.</summary>
        public void SetTrailTip(Vector3? tip)
        {
            EnsureInit();
            if (swingTrail == null) return;
            swingTrail.transform.localPosition = tip ?? DefaultTrailTip;
            swingTrail.Clear();
        }

        /// <summary>The trail's anchor in hand-socket space (tests and tools).</summary>
        public Vector3 TrailTip => swingTrail != null ? swingTrail.transform.localPosition : DefaultTrailTip;

        void TintTrail(Color accent)
        {
            if (swingTrail == null) return;
            var head = Color.Lerp(accent, Color.white, 0.55f);
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(head, 0f), new GradientColorKey(accent, 1f) },
                new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0f, 1f) });
            swingTrail.colorGradient = gradient;
        }

        /// <summary>
        /// Puts every part's resting colour back, now and again once the Animator has settled (switching body
        /// parts on and off makes the Animator wipe renderer colour overrides).
        /// </summary>
        public void RefreshColors()
        {
            EnsureInit();
            recolorFrames = 2;
            if (flashing) return;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) Apply(renderers[i], baseColors[i]);
        }

        /// <summary>Sets the resting colour of specific parts (skin, hair...), surviving flashes.</summary>
        public void Recolor(Renderer[] targets, Color color)
        {
            EnsureInit();
            if (targets == null) return;
            foreach (var r in targets)
            {
                int index = System.Array.IndexOf(renderers, r);
                if (index < 0) continue;
                baseColors[index] = color;
                Apply(r, color);
            }
        }

        void Apply(Renderer r, Color color)
        {
            // Rebuilt if a script reload during Play mode dropped it (it can't be saved across the reload).
            if (block == null) block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            r.SetPropertyBlock(block);
        }

        /// <summary>Tints the whole character toward a colour, holding, then fading back out.</summary>
        public void Flash(Color color, float duration)
        {
            EnsureInit();
            if (!isActiveAndEnabled) return;
            flashColor = color;
            flashStart = Time.time;
            flashDuration = Mathf.Max(duration, 0.02f);
            flashing = true;
        }

        void UpdateFlash()
        {
            if (!flashing) return;
            float t = (Time.time - flashStart) / flashDuration;
            if (t >= 1f)
            {
                flashing = false;
                for (int i = 0; i < renderers.Length; i++) Apply(renderers[i], baseColors[i]);
                return;
            }
            // Full strength for the first half, then ease back to the real colours.
            float amount = t < 0.5f ? 0.85f : Mathf.Lerp(0.85f, 0f, (t - 0.5f) / 0.5f);
            for (int i = 0; i < renderers.Length; i++) Apply(renderers[i], Color.Lerp(baseColors[i], flashColor, amount));
        }

        // ------------------------------------------------------------------ triggers

        /// <summary>Starts the move's strike, timed to its startup/active/recovery.</summary>
        public void PlayAttack(FightingMove move) => PlayAttack(move, move.style, false);

        /// <summary>
        /// Starts one strike of a move. A strike thrown while the last one is still on screen (the next hit
        /// of a combo) cross-fades on from it. <paramref name="moreToCome"/> holds the follow-through for the
        /// next hit instead of settling back to the guard.
        /// </summary>
        public void PlayAttack(FightingMove move, AttackStyle style, bool moreToCome)
        {
            EnsureInit();
            attack = move;
            attackStyle = style;
            attackStart = Time.time;
            lastAttackTime = Time.time;
            impactPlayed = false;
            if (EnsureClips()) clips.Attack(move, style, moreToCome, Time.time);
        }

        public void CancelAttack()
        {
            attack = null;
            if (swingTrail != null) swingTrail.emitting = false;
            clips?.Cancel();
        }

        public void PlayHitReaction() => PlayHitReaction(Vector3.zero, DefaultFlinch);

        public void PlayHitReaction(Vector3 knockback) => PlayHitReaction(knockback, DefaultFlinch);

        /// <summary>
        /// Flinch away from the blow for <paramref name="duration"/> seconds (the stun), then fade back to the
        /// stance, so the body never keeps reeling after control has come back.
        /// </summary>
        public void PlayHitReaction(Vector3 knockback, float duration)
        {
            EnsureInit();
            CancelAttack();
            hitStart = Time.time;
            lastAttackTime = Time.time;
            Squash(0.16f);
            float strength = Mathf.Clamp(knockback.magnitude / 8f, 0.6f, 1.6f);
            joltSpring.Kick(JoltAxis(knockback) * 7f * strength);
            if (EnsureClips()) clips.React(Time.time, Mathf.Max(duration, 0.15f));
        }

        /// <summary>A hit stopped by the guard: the spine is driven back and the body skids.</summary>
        public void PlayBlockReaction(Vector3 knockback)
        {
            EnsureInit();
            joltSpring.Kick(JoltAxis(knockback) * 10f);
            Squash(0.07f);
        }

        /// <summary>The axis (in the body's space) a blow from this direction rocks the spine about.</summary>
        Vector3 JoltAxis(Vector3 knockback)
        {
            Vector3 dir = knockback;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = -model.forward; // unknown: rock backwards
            return Vector3.Cross(Vector3.up, model.InverseTransformDirection(dir.normalized)).normalized;
        }

        public void PlayKnockedOut()
        {
            EnsureInit();
            CancelAttack();
            knockedOut = true;
            if (EnsureClips()) clips.Knockout(Time.time);
        }

        void Squash(float amount)
        {
            if (!isActiveAndEnabled) return;
            if (squashRoutine != null) StopCoroutine(squashRoutine);
            squashRoutine = StartCoroutine(SquashRoutine(amount));
        }

        IEnumerator SquashRoutine(float amount)
        {
            const float duration = 0.26f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                // Squash, then a small springy overshoot back past rest.
                float k = t / duration;
                float s = amount * Mathf.Cos(k * Mathf.PI * 1.5f) * (1f - k);
                model.localScale = Vector3.Scale(modelScale, new Vector3(1f + s, 1f - s, 1f + s));
                yield return null;
            }
            model.localScale = modelScale;
            squashRoutine = null;
        }

        // ------------------------------------------------------------------ per frame

        void LateUpdate()
        {
            if (!initialized) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            UpdateFlash();
            if (dt <= 0f) return;

            if (knockedOut)
            {
                if (EnsureClips()) DriveClips(dt, 0f);
                return;
            }

            // ---- locomotion state
            Vector3 velocity = motor != null ? motor.Velocity : Vector3.zero;
            float speed = new Vector2(velocity.x, velocity.z).magnitude;
            bool grounded = motor == null || motor.IsGrounded;
            walkBlend = Mathf.MoveTowards(walkBlend, grounded ? Mathf.Clamp01(speed / 4f) : 0f, dt * 6f);
            airBlend = Mathf.MoveTowards(airBlend, grounded ? 0f : 1f, dt * 7f);
            run = Mathf.MoveTowards(run, Mathf.Clamp01((speed - 4.5f) / 2.5f), dt * 4f);
            if (grounded && !wasGrounded)
            {
                // Landing: the figure squashes and the floor hears it.
                float impact = Mathf.Clamp01(-lastVerticalSpeed / 12f);
                Squash(0.08f + 0.08f * impact);
                Stepped?.Invoke(this, model.position, 1.5f + 2f * impact);
            }
            wasGrounded = grounded;
            lastVerticalSpeed = velocity.y;

            // Footsteps: a foot plants each time the stride passes its widest point.
            walkPhase += StepsPerSecond(speed, strideLength) * Mathf.PI * dt;
            float lift = Mathf.Cos(walkPhase);
            if (grounded && walkBlend > 0.4f && Mathf.Sign(lift) != Mathf.Sign(lastLift))
                Stepped?.Invoke(this, FootPosition(lift > 0f), walkBlend * (1f + run));
            lastLift = lift;

            bool fightReady = InCombat || Blocking || Time.time - lastAttackTime < 2.2f;
            // The guard snaps up and drops slowly; a block goes up faster still.
            stance = Mathf.MoveTowards(stance, fightReady ? 1f : 0f, dt * (fightReady ? 5f : 2.5f));
            blockBlend = Mathf.MoveTowards(blockBlend, Blocking ? 1f : 0f, dt * (Blocking ? 7f : 4f));
            if (EnsureClips()) DriveClips(dt, speed);
        }

        /// <summary>Where the stepping foot is: the animated foot bone of the body on show.</summary>
        Vector3 FootPosition(bool right)
        {
            var animator = follower != null ? follower.ActiveRig?.animator : null;
            var foot = animator != null && animator.isHuman
                ? animator.GetBoneTransform(right ? HumanBodyBones.RightFoot : HumanBodyBones.LeftFoot)
                : null;
            return foot != null ? foot.position : model.position;
        }

        /// <summary>Clip-driven frame: keeps the attack bookkeeping, plays the clips, then jolts the spine.</summary>
        void DriveClips(float dt, float speed)
        {
            bool striking = false;
            if (attack != null)
            {
                float t = Time.time - attackStart;
                float total = attack.startup + attack.active + attack.recovery;
                float impact = FightingMove.ImpactTime(attack.startup, attack.active);
                if (t >= total)
                {
                    attack = null;
                }
                else
                {
                    lastAttackTime = Time.time;
                    striking = t >= impact - 0.1f && t <= attack.startup + attack.active + 0.06f;
                    if (!impactPlayed && t >= impact)
                    {
                        // Impact: the body takes the shock; heavy swings squash the whole figure.
                        impactPlayed = true;
                        joltSpring.Kick(new Vector3(IsHeavyStyle(attackStyle) ? 3.5f : 2f, 0f, 0f));
                        if (IsHeavyStyle(attackStyle)) Squash(0.08f);
                    }
                }
            }
            if (swingTrail != null) swingTrail.emitting = striking;

            clips.Evaluate(new FightAnimator.Locomotion
            {
                walk = walkBlend, air = airBlend, stance = stance, block = blockBlend, speed = speed,
            }, Time.time, dt);
            if (recolorFrames > 0)
            {
                recolorFrames--;
                if (!flashing)
                    for (int i = 0; i < renderers.Length; i++)
                        if (renderers[i] != null) Apply(renderers[i], baseColors[i]);
            }

            // Jolts are sprung in the body's space and added on top of the animated spine.
            var rig = follower.ActiveRig;
            var jolt = joltSpring.Step(Quaternion.identity, 5f, 0.35f, dt);
            var world = model.rotation * jolt * Quaternion.Inverse(model.rotation);
            if (rig.chest != null) rig.chest.rotation = world * rig.chest.rotation;
            if (rig.neck != null) rig.neck.rotation = Quaternion.Slerp(Quaternion.identity, world, 0.5f) * rig.neck.rotation;
        }

        static bool IsHeavyStyle(AttackStyle style) =>
            style == AttackStyle.Slam || style == AttackStyle.Chop || style == AttackStyle.Blast || style == AttackStyle.Golf ||
            style == AttackStyle.Stamp || style == AttackStyle.Shove || style == AttackStyle.Spin ||
            style == AttackStyle.Pound || style == AttackStyle.Cleave || style == AttackStyle.Uppercut;
    }
}
