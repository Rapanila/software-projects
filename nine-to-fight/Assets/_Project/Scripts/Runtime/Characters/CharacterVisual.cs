using System.Collections;
using NineToFight.Combat;
using UnityEngine;

namespace NineToFight.Characters
{
    /// <summary>
    /// Procedural animation for the office workers. Each frame it builds a target pose from layers
    /// (walk cycle, combat stance, idle, the current attack's choreography, flinches) and then drives
    /// every joint toward it with damped springs, so motion lags, overshoots and settles like it has
    /// weight. The body bends at the waist and elbows, feet stay planted, turns ripple up the spine,
    /// and ties and hair swing. Also handles the face, colour tinting and flashes.
    /// </summary>
    public class CharacterVisual : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Tooltip("Whole-body pivot at the feet: bob, crouch, steps and spins.")]
        [SerializeField] Transform model;
        [Tooltip("Optional waist pivot: the upper body leans and twists here instead of the whole body tipping.")]
        [SerializeField] Transform chest;
        [Tooltip("Shoulder pivot of the weapon arm.")]
        [SerializeField] Transform attackArm;
        [Tooltip("Shoulder pivot of the off-hand arm.")]
        [SerializeField] Transform backArm;
        [Tooltip("Optional elbow pivots (weapon arm, off arm).")]
        [SerializeField] Transform elbowFront;
        [SerializeField] Transform elbowBack;
        [Tooltip("Hand socket holding the weapon; rotated for wrist flicks.")]
        [SerializeField] Transform wrist;
        [SerializeField] Transform legLeft;
        [SerializeField] Transform legRight;
        [Tooltip("Optional knee pivots (children of the legs) for a bent-knee stride.")]
        [SerializeField] Transform kneeLeft;
        [SerializeField] Transform kneeRight;
        [Tooltip("Optional ankle pivots, keeping feet flat on the floor.")]
        [SerializeField] Transform ankleLeft;
        [SerializeField] Transform ankleRight;
        [Tooltip("Optional neck pivot; nods, bobs and recoils independently of the body.")]
        [SerializeField] Transform head;
        [Tooltip("Optional swinging parts: tie and hair lag behind the body.")]
        [SerializeField] Transform tie;
        [SerializeField] Transform hair;
        [Tooltip("Optional eyes, squashed shut to blink.")]
        [SerializeField] Transform[] eyes;
        [Tooltip("Optional brows, scowl during attacks. Left first, then right.")]
        [SerializeField] Transform[] brows;
        [Tooltip("Shirt/sleeve renderers recoloured per class.")]
        [SerializeField] Renderer[] shirtRenderers;
        [Tooltip("Tie renderers recoloured per class.")]
        [SerializeField] Renderer[] accentRenderers;
        [Tooltip("Animation clips to fight with. Without them the body is posed procedurally.")]
        [SerializeField] FightAnimationSet animationSet;

        RigFollower follower;
        FightAnimator clips;
        int clipRig = -1;
        bool impactPlayed;
        // The Animator wipes renderer colour overrides when it starts up; put them back once it has.
        int recolorFrames;
        // Hits jolt the upper spine on top of the animation.
        SpringRotation joltSpring;

        /// <summary>True when animation clips (not the procedural rig) are posing the body.</summary>
        public bool UsesClips => EnsureClips();

        /// <summary>Metres travelled per step; longer strides mean a slower leg cycle.</summary>
        public const float DefaultStrideLength = 1.9f;

        /// <summary>Footsteps per second at a given ground speed.</summary>
        public static float StepsPerSecond(float speed, float strideLength) => speed / UnityEngine.Mathf.Max(strideLength, 0.05f);

        /// <summary>Seconds to blend from walking into an attack pose; short so the wind-up still reads.</summary>
        public const float AttackBlendIn = 0.05f;
        /// <summary>Seconds to blend back out to walking after an attack ends or is cancelled.</summary>
        public const float AttackBlendOut = 0.16f;

        /// <summary>The hips and spine run this far ahead of the arm through an attack...</summary>
        const float ChainLead = 0.045f;
        /// <summary>...and the wrist this far behind, so a swing travels up the body and cracks at the end.</summary>
        const float ChainLag = 0.03f;
        /// <summary>Share of the upper body's twist the pelvis takes (turning the feet with it).</summary>
        const float PelvisShare = 0.35f;

        [Header("Locomotion")]
        [Tooltip("Metres per step. Raise to slow the walk cycle, lower to speed it up.")]
        [SerializeField] float strideLength = DefaultStrideLength;
        [SerializeField] float legSwing = 32f;
        [SerializeField] float kneeBend = 55f;
        [SerializeField] float armSwing = 30f;
        [SerializeField] float bobHeight = 0.05f;
        [SerializeField] float hipTwist = 8f;
        [SerializeField] float runLean = 8f;

        /// <summary>Set by the controller/AI: raises the guard into a fighting stance.</summary>
        public bool InCombat { get; set; }

        /// <summary>Set while actually blocking: a high, hunched cover behind both forearms.</summary>
        public bool Blocking { get; set; }

        /// <summary>The living fighting stance (bounce, weave, fidgets) this character uses.</summary>
        public FightStance Stance { get { EnsureInit(); return fightStance; } }

        FightStance fightStance;
        float blockBlend;

        /// <summary>A foot hit the floor (or the whole body landed). Strength ~1 for a step, ~2+ for a landing.</summary>
        public static event System.Action<CharacterVisual, Vector3, float> Stepped;

        TrailRenderer swingTrail;
        float lastLift;

        Renderer[] renderers;
        Color[] baseColors;
        MaterialPropertyBlock block;
        CharacterMotor2_5D motor;
        bool initialized;

        Vector3 modelPosition, modelScale, armPosition, backArmPosition;
        Quaternion modelRotation, chestRotation, armRotation, backArmRotation, wristRotation, legLeftRotation, legRightRotation;
        Quaternion kneeLeftRotation, kneeRightRotation, ankleLeftRotation, ankleRightRotation, headRotation, tieRotation, hairRotation;
        Quaternion elbowFrontRotation, elbowBackRotation;
        Vector3[] eyeScales;
        Quaternion[] browRotations;

        // Springs: the reason nothing snaps.
        SpringRotation chestSpring, headSpring, armSpring, backArmSpring, wristSpring, tieSpring, hairSpring, modelSpring;
        Spring1 elbowFrontSpring, elbowBackSpring, dipSpring;

        float walkPhase, walkBlend, airBlend, idleTime, stance, run;
        float lastYaw, lastVerticalSpeed;
        bool wasGrounded = true;
        float nextBlink, blinkStart = -1f;
        float lookYaw, nextLookTime;

        FightingMove attack;
        AttackStyle attackStyle;
        bool holdFollow;
        float attackStart, lastAttackTime = -10f;

        // Where the body was last frame, so the next strike of a combo flows on from it instead of the guard.
        struct Carried
        {
            public Quaternion arm, offArm, wrist, torso;
            public float elbow, backElbow, twist;
            public Vector3 armOffset, body;
            public Vector4 kick;
        }
        Carried applied, carry;
        bool carrying;
        AttackPhase lastPhase;
        float attackWeight;
        // Last attack poses, held while blending back out so cancelled swings don't snap.
        SampledPose heldPose, heldLead, heldLag;
        bool hasHeldPose;
        // Hip to ankle, for keeping feet on the floor as the body crouches and steps.
        float legLength = 1f, lastStepTravel, stepLift;

        float hitStart = -10f;
        bool knockedOut;
        float knockOutStart;

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

            modelPosition = model.localPosition;
            modelRotation = model.localRotation;
            modelScale = model.localScale;
            chestRotation = Rest(chest);
            if (attackArm != null) { armPosition = attackArm.localPosition; armRotation = attackArm.localRotation; }
            if (backArm != null) { backArmPosition = backArm.localPosition; backArmRotation = backArm.localRotation; }
            wristRotation = Rest(wrist);
            elbowFrontRotation = Rest(elbowFront);
            elbowBackRotation = Rest(elbowBack);
            legLeftRotation = Rest(legLeft);
            legRightRotation = Rest(legRight);
            kneeLeftRotation = Rest(kneeLeft);
            kneeRightRotation = Rest(kneeRight);
            ankleLeftRotation = Rest(ankleLeft);
            ankleRightRotation = Rest(ankleRight);
            headRotation = Rest(head);
            if (legLeft != null && ankleLeft != null)
                legLength = Mathf.Max(0.3f, Vector3.Distance(model.InverseTransformPoint(legLeft.position), model.InverseTransformPoint(ankleLeft.position)));
            tieRotation = Rest(tie);
            hairRotation = Rest(hair);
            eyeScales = new Vector3[eyes != null ? eyes.Length : 0];
            for (int i = 0; i < eyeScales.Length; i++) eyeScales[i] = eyes[i] != null ? eyes[i].localScale : Vector3.one;
            browRotations = new Quaternion[brows != null ? brows.Length : 0];
            for (int i = 0; i < browRotations.Length; i++) browRotations[i] = brows[i] != null ? brows[i].localRotation : Quaternion.identity;

            CreateSwingTrail();

            // Desynchronise crowds so coworkers don't breathe, blink and bounce in lockstep.
            idleTime = Random.Range(0f, 10f);
            fightStance = new FightStance(Random.Range(int.MinValue, int.MaxValue));
            nextBlink = Time.time + Random.Range(1f, 4f);
            nextLookTime = Time.time + Random.Range(2f, 5f);
            lastYaw = transform.localEulerAngles.y;
        }

        static Quaternion Rest(Transform t) => t != null ? t.localRotation : Quaternion.identity;

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
            return true;
        }

        void OnDisable()
        {
            clips?.Dispose();
            clips = null;
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
            go.transform.localPosition = new Vector3(0f, -0.42f, 0f);
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

        /// <summary>Starts the move's choreography, timed to its startup/active/recovery.</summary>
        public void PlayAttack(FightingMove move) => PlayAttack(move, move.style, false);

        /// <summary>
        /// Starts one strike of a move. A strike thrown while the last one is still on screen (the next hit
        /// of a combo) flows on from wherever that one left the body. <paramref name="moreToCome"/> holds the
        /// follow-through for the next hit instead of settling back to the guard.
        /// </summary>
        public void PlayAttack(FightingMove move, AttackStyle style, bool moreToCome)
        {
            EnsureInit();
            carrying = (attack != null || hasHeldPose) && attackWeight > 0.3f;
            carry = applied;
            attack = move;
            attackStyle = style;
            holdFollow = moreToCome;
            attackStart = Time.time;
            lastAttackTime = Time.time;
            lastPhase = AttackPhase.None;
            impactPlayed = false;
            if (EnsureClips()) clips.Attack(move, style, moreToCome, Time.time);
        }

        public void CancelAttack()
        {
            attack = null;
            clips?.Cancel();
        }

        public void PlayHitReaction() => PlayHitReaction(Vector3.zero);

        /// <summary>Flinch away from the blow: the spine, head and arms are kicked and wobble back.</summary>
        public void PlayHitReaction(Vector3 knockback)
        {
            EnsureInit();
            CancelAttack();
            hitStart = Time.time;
            lastAttackTime = Time.time;
            Squash(0.16f);

            Vector3 dir = knockback;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = -model.forward; // unknown: rock backwards
            Vector3 local = model.InverseTransformDirection(dir.normalized);
            Vector3 axis = Vector3.Cross(Vector3.up, local).normalized;
            float strength = Mathf.Clamp(knockback.magnitude / 8f, 0.6f, 1.6f);
            chestSpring.Kick(axis * 9f * strength);
            headSpring.Kick(axis * 13f * strength);
            joltSpring.Kick(axis * 7f * strength);
            if (EnsureClips()) clips.React(Time.time);
            armSpring.Kick(new Vector3(-6f, 0f, 4f) * strength);
            backArmSpring.Kick(new Vector3(-6f, 0f, -4f) * strength);
            dipSpring.velocity -= 0.8f * strength;
        }

        /// <summary>A hit stopped by the guard: the forearms are driven back into the face and the body skids.</summary>
        public void PlayBlockReaction(Vector3 knockback)
        {
            EnsureInit();
            Vector3 dir = knockback;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = -model.forward;
            Vector3 axis = Vector3.Cross(Vector3.up, model.InverseTransformDirection(dir.normalized)).normalized;
            chestSpring.Kick(axis * 9f);
            headSpring.Kick(axis * 13f);
            joltSpring.Kick(axis * 10f);
            armSpring.Kick(new Vector3(14f, 0f, 0f));
            backArmSpring.Kick(new Vector3(14f, 0f, 0f));
            dipSpring.velocity -= 0.7f;
            Squash(0.07f);
        }

        /// <summary>Plays a stance fidget now (previews and tests).</summary>
        public void PlayFidget(FightStance.Fidget fidget)
        {
            EnsureInit();
            fightStance.Play(fidget, idleTime);
        }

        public void PlayKnockedOut()
        {
            EnsureInit();
            CancelAttack();
            if (swingTrail != null) swingTrail.emitting = false;
            knockedOut = true;
            knockOutStart = Time.time;
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

        // ------------------------------------------------------------------ per-frame pose

        void LateUpdate()
        {
            if (!initialized) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            UpdateFlash();
            UpdateFace(dt);
            if (dt <= 0f) return;

            if (knockedOut)
            {
                if (EnsureClips()) DriveClips(dt, 0f);
                else PoseKnockedOut(dt);
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
                // Landing: knees absorb it, the spine nods forward, then everything settles.
                float impact = Mathf.Clamp01(-lastVerticalSpeed / 12f);
                Squash(0.08f + 0.08f * impact);
                dipSpring.velocity -= 0.6f + 1.8f * impact;
                chestSpring.Kick(new Vector3(3f + 4f * impact, 0f, 0f));
                headSpring.Kick(new Vector3(4f + 4f * impact, 0f, 0f));
            }
            if (grounded && !wasGrounded) Stepped?.Invoke(this, model.position, 1.5f + 2f * Mathf.Clamp01(-lastVerticalSpeed / 12f));
            wasGrounded = grounded;
            lastVerticalSpeed = velocity.y;

            walkPhase += StepsPerSecond(speed, strideLength) * Mathf.PI * dt;
            idleTime += dt;
            float swing = Mathf.Sin(walkPhase), lift = Mathf.Cos(walkPhase);
            // A foot plants each time the stride passes its widest point.
            if (grounded && walkBlend > 0.4f && Mathf.Sign(lift) != Mathf.Sign(lastLift))
            {
                var foot = lift > 0f ? ankleRight : ankleLeft;
                Stepped?.Invoke(this, foot != null ? foot.position : model.position, walkBlend * (1f + run));
            }
            lastLift = lift;
            bool fightReady = InCombat || Blocking || Time.time - lastAttackTime < 2.2f;
            // The guard snaps up and drops slowly; a block goes up faster still.
            stance = Mathf.MoveTowards(stance, fightReady ? 1f : 0f, dt * (fightReady ? 5f : 2.5f));
            blockBlend = Mathf.MoveTowards(blockBlend, Blocking ? 1f : 0f, dt * (Blocking ? 7f : 4f));
            if (EnsureClips())
            {
                DriveClips(dt, speed);
                return;
            }
            float calm = (1f - walkBlend) * (1f - stance);
            float breathe = Mathf.Sin(idleTime * 2.1f) * (1f - walkBlend);
            // Planted in a fight: the stance bounces, rocks, weaves and fidgets only while standing.
            float still = stance * (1f - walkBlend) * (1f - airBlend);
            var guard = fightStance.Sample(idleTime, still, blockBlend,
                allowFidgets: still > 0.9f && attack == null && attackWeight <= 0f && Time.time - hitStart > 0.8f);
            float guardWeight = Mathf.Max(stance, blockBlend) * (1f - airBlend);
            // Walking squares the body back up toward the direction of travel.
            float bladed = guardWeight * (1f - 0.6f * walkBlend);

            // Turns ripple up the body: the spine and head lag a fast turn, then catch up.
            float yaw = transform.localEulerAngles.y;
            float yawDelta = Mathf.DeltaAngle(lastYaw, yaw);
            lastYaw = yaw;
            if (Mathf.Abs(yawDelta) > 0.5f)
            {
                float turnRate = Mathf.Clamp(-yawDelta * Mathf.Deg2Rad / dt, -14f, 14f);
                chestSpring.Kick(new Vector3(0f, turnRate * 0.35f, 0f));
                headSpring.Kick(new Vector3(0f, turnRate * 0.3f, 0f));
                tieSpring.Kick(new Vector3(0f, 0f, turnRate * 0.4f));
            }

            // ---- legs: contralateral stride, knees lift in the swing phase, feet plant flat
            float legAmp = legSwing * Mathf.Lerp(0.9f, 1.25f, run) * walkBlend;
            float leftLeg = swing * legAmp;
            float rightLeg = -swing * legAmp;
            float bend = kneeBend * (1f + 0.35f * run) * walkBlend;
            float leftLift = Mathf.Max(0f, -lift), rightLift = Mathf.Max(0f, lift);
            float leftKnee = leftLift * bend + 5f * walkBlend;
            float rightKnee = rightLift * bend + 5f * walkBlend;

            // ---- whole body: bob (lowest when the legs are spread), hip sway, idle weight shift
            float weightShift = Mathf.Sin(idleTime * 0.7f) * calm;
            float dip = dipSpring.Step(0f, 4.5f, 0.45f, dt);
            float guardDrop = guard.drop * guardWeight;
            Vector3 bodyBob = new Vector3(weightShift * 0.025f + guard.sway.x,
                -Mathf.Abs(swing) * bobHeight * walkBlend * (1f + run) - guardDrop + breathe * 0.006f + dip,
                guard.sway.z + guard.step);
            Quaternion hips = Quaternion.Euler(0f, swing * hipTwist * 0.7f * walkBlend + guard.pelvisYaw * bladed,
                -swing * 2.5f * walkBlend + weightShift * 1.5f);

            // ---- upper body targets
            float lean = walkBlend * runLean * (1f + 0.6f * run) + guard.torso.x * guardWeight + breathe * 1.2f;
            float chestYaw = -swing * hipTwist * 1.3f * walkBlend + guard.torso.y * bladed;
            float chestRoll = swing * 1.5f * walkBlend + guard.torso.z * guardWeight;
            Quaternion torsoWalk = Quaternion.Euler(lean, chestYaw, chestRoll);

            float armAmp = armSwing * (1f + 0.5f * run) * walkBlend * (1f - 0.6f * stance);
            // Right (weapon) arm swings with the left leg and vice versa.
            Quaternion frontRelaxed = Quaternion.Euler(swing * armAmp - airBlend * 30f + breathe * 2f, 0f, 4f + airBlend * 28f + breathe);
            Quaternion backRelaxed = Quaternion.Euler(-swing * armAmp - airBlend * 30f + breathe * 2f, 0f, -4f - airBlend * 28f - breathe);
            // The guard (or block), pumping a little with the stride when walking.
            Quaternion frontGuard = Quaternion.Euler(swing * armAmp * 0.4f, 0f, 0f) * guard.weaponArm;
            Quaternion backGuard = Quaternion.Euler(-swing * armAmp * 0.4f, 0f, 0f) * guard.leadArm;
            Quaternion armWalk = Quaternion.Slerp(frontRelaxed, frontGuard, guardWeight);
            Quaternion backArmWalk = Quaternion.Slerp(backRelaxed, backGuard, guardWeight);
            Quaternion wristGuard = Quaternion.Slerp(Quaternion.identity, guard.wrist, guardWeight);
            float elbowWalkFront = 12f + Mathf.Max(0f, -swing) * 30f * walkBlend + 35f * run + 30f * airBlend;
            float elbowWalkBack = 12f + Mathf.Max(0f, swing) * 30f * walkBlend + 35f * run + 30f * airBlend;
            elbowWalkFront = Mathf.Lerp(elbowWalkFront, guard.weaponElbow, guardWeight);
            elbowWalkBack = Mathf.Lerp(elbowWalkBack, guard.leadElbow, guardWeight);

            // ---- attack choreography, cross-faded over the locomotion pose. The body works as a chain:
            // hips and spine lead the move, the arm follows, the wrist trails and cracks last.
            SampledPose pose = default, lead = default, lag = default;
            bool attacking = false;
            if (attack != null)
            {
                float t = Time.time - attackStart;
                pose = AttackPoses.Sample(attackStyle, t, attack.startup, attack.active, attack.recovery, holdFollow);
                if (pose.phase == AttackPhase.None)
                {
                    attack = null;
                }
                else
                {
                    float end = attack.startup + attack.active + attack.recovery - 0.0001f;
                    lead = AttackPoses.Sample(attackStyle, Mathf.Min(t + ChainLead, end), attack.startup, attack.active, attack.recovery, holdFollow);
                    lag = AttackPoses.Sample(attackStyle, Mathf.Max(t - ChainLag, 0f), attack.startup, attack.active, attack.recovery, holdFollow);
                    attacking = true;
                    heldPose = pose;
                    heldLead = lead;
                    heldLag = lag;
                    hasHeldPose = true;
                    lastAttackTime = Time.time;
                    if (lastPhase == AttackPhase.Strike && pose.phase == AttackPhase.FollowThrough)
                    {
                        // Impact: the body takes the shock; heavy swings squash the whole figure.
                        if (IsHeavyStyle(attackStyle))
                        {
                            Squash(0.1f);
                            chestSpring.Kick(new Vector3(4f, 0f, 0f));
                        }
                        else
                        {
                            chestSpring.Kick(new Vector3(2.5f, 0f, 0f));
                        }
                        headSpring.Kick(new Vector3(3f, 0f, 0f));
                    }
                    lastPhase = pose.phase;
                }
            }
            attackWeight = Mathf.MoveTowards(attackWeight, attacking ? 1f : 0f, dt / (attacking ? AttackBlendIn : AttackBlendOut));
            if (swingTrail != null) swingTrail.emitting = attacking && (pose.phase == AttackPhase.Strike || pose.phase == AttackPhase.FollowThrough);
            if (!attacking && hasHeldPose)
            {
                pose = heldPose;
                lead = heldLead;
                lag = heldLag;
                if (attackWeight <= 0f) hasHeldPose = false;
            }

            // Each strike eases in over a base pose: the guard, or mid-combo wherever the last strike left the
            // body (until the new strike has fully taken over, wrist last).
            if (carrying && !(attacking && lag.weight < 0.999f)) carrying = false;
            Quaternion baseArm = carrying ? carry.arm : armWalk, baseOffArm = carrying ? carry.offArm : backArmWalk;
            Quaternion baseWrist = carrying ? carry.wrist : wristGuard, baseTorso = carrying ? carry.torso : torsoWalk;
            float baseElbow = carrying ? carry.elbow : elbowWalkFront, baseBackElbow = carrying ? carry.backElbow : elbowWalkBack;
            Vector3 baseArmOffset = carrying ? carry.armOffset : Vector3.zero, baseBody = carrying ? carry.body : Vector3.zero;
            float baseTwist = carrying ? carry.twist : 0f;
            Vector4 baseKick = carrying ? carry.kick : Vector4.zero;

            Quaternion arm = baseArm, offArm = baseOffArm, wristPose = baseWrist, torso = baseTorso;
            Vector3 armOffset = baseArmOffset, backArmOffset = Vector3.zero, bodyOffset = baseBody;
            float spin = 0f, twist = baseTwist, elbowFrontAngle = baseElbow, elbowBackAngle = baseBackElbow;
            Vector4 kick = baseKick;
            float w = 0f, wBody = 0f;
            bool afterImpact = false;
            if (attackWeight > 0f && (attacking || hasHeldPose))
            {
                // The attack eases in through its wind-up and hands back over its recovery.
                float blend = EaseInOut(attackWeight);
                w = blend * pose.weight;
                wBody = blend * lead.weight;
                afterImpact = pose.phase == AttackPhase.FollowThrough || pose.phase == AttackPhase.Recovery;

                Quaternion poseArm = Quaternion.Euler(pose.armSpin, 0f, 0f) * pose.arm;
                Quaternion poseOffArm = pose.twoHanded ? poseArm : pose.backArm;
                arm = Quaternion.Slerp(baseArm, poseArm, w);
                offArm = Quaternion.Slerp(baseOffArm, poseOffArm, w);
                wristPose = Quaternion.Slerp(baseWrist, lag.wrist, blend * lag.weight);
                armOffset = Vector3.Lerp(baseArmOffset, pose.armOffset, w);
                if (pose.twoHanded) backArmOffset = new Vector3(-pose.armOffset.x, pose.armOffset.y, pose.armOffset.z) * w;
                // Elbows chamber in the wind-up and snap straight through the strike (the whip).
                elbowFrontAngle = Mathf.Lerp(baseElbow, pose.elbow, w);
                elbowBackAngle = Mathf.Lerp(baseBackElbow, pose.twoHanded ? pose.elbow : pose.backElbow, w);
                kick = Vector4.Lerp(baseKick, pose.kick, w);

                // The spine carries the move (plus a little of the walk so it doesn't freeze), the pelvis turns
                // first and takes its share of the twist, and the legs follow the body's travel.
                torso = Quaternion.Slerp(baseTorso, lead.torso, wBody * 0.92f);
                twist = Mathf.Lerp(baseTwist, Signed(lead.torso.eulerAngles.y) * PelvisShare, wBody);
                bodyOffset = Vector3.Lerp(baseBody, lead.body, wBody);
                // A whole turn looks the same as none, so blending in and out never rewinds a spin.
                spin = Mathf.DeltaAngle(0f, lead.spin) * wBody;
            }
            applied = new Carried
            {
                arm = arm, offArm = offArm, wrist = wristPose, torso = torso,
                elbow = elbowFrontAngle, backElbow = elbowBackAngle, twist = twist,
                armOffset = armOffset, body = bodyOffset, kick = kick,
            };
            torso = Quaternion.Euler(0f, -twist, 0f) * torso;

            // ---- legs: stride, plus footwork. Knees bend exactly enough to keep the feet on the floor as
            // the body sinks; the trailing foot stays planted as the body travels and the lead foot steps.
            float drop = Mathf.Max(0f, guardDrop - dip - Mathf.Min(bodyOffset.y, 0f)) * (1f - airBlend);
            // Standing still in a fight: a staggered stance, lead foot forward, trail foot back.
            float stagger = still;
            float sink = Mathf.Acos(1f - Mathf.Min(drop, legLength * 0.5f) / legLength) * Mathf.Rad2Deg;
            // Rocking keeps both feet planted; stepping (attacks, shuffles) moves the lead foot.
            float rock = Mathf.Asin(Mathf.Clamp(guard.sway.z / legLength, -0.5f, 0.5f)) * Mathf.Rad2Deg;
            float travel = bodyOffset.z + guard.step;
            float plant = Mathf.Asin(Mathf.Clamp(travel / legLength, -0.8f, 0.8f)) * Mathf.Rad2Deg;
            float stepSpeed = (travel - lastStepTravel) / dt;
            lastStepTravel = travel;
            stepLift = Mathf.MoveTowards(stepLift, Mathf.Clamp01(Mathf.Abs(stepSpeed) / 2.5f), dt * 8f);
            float reach = Mathf.Clamp01(travel / 0.3f);
            // Kicks and knees take over the rear leg (leaps the lead leg too); otherwise the lead leg plants
            // and takes the weight.
            float kicking = Mathf.Clamp01(Mathf.Abs(kick.x) / 30f);
            float leaping = Mathf.Clamp01(Mathf.Abs(kick.z) / 20f);
            float leadThigh = Mathf.Lerp(leftLeg - sink - 13f * stagger + rock + (travel > 0f ? -plant * 1.3f : plant),
                leftLeg - sink - 6f + plant, kicking);
            float leadKnee = leftKnee + 2f * sink + 35f * stepLift * (1f - kicking) + 8f * reach;
            leadThigh = Mathf.Lerp(leadThigh, kick.z, leaping);
            leadKnee = Mathf.Lerp(leadKnee, kick.w, leaping);
            float rearThigh = Mathf.Lerp(rightLeg - sink + 11f * stagger + rock + plant, kick.x, kicking);
            float rearKnee = Mathf.Lerp(rightKnee + 2f * sink + 6f * reach, kick.y, kicking);
            SetLegs(leadThigh, rearThigh, leadKnee, rearKnee,
                Mathf.Max(Mathf.Max(leftLift * walkBlend, stepLift * (1f - kicking)), leaping), Mathf.Max(rightLift * walkBlend, kicking));

            // Hit flinch: arms come up to cover, on top of the spring kick.
            float hitT = (Time.time - hitStart) / 0.35f;
            if (hitT < 1f)
            {
                float flinch = Mathf.Sin(Mathf.Clamp01(hitT) * Mathf.PI) * (1f - hitT);
                arm = Quaternion.Slerp(arm, Quaternion.Euler(-75f, 0f, 25f), flinch);
                offArm = Quaternion.Slerp(offArm, Quaternion.Euler(-75f, 0f, -25f), flinch);
                elbowFrontAngle = Mathf.Lerp(elbowFrontAngle, 110f, flinch);
                elbowBackAngle = Mathf.Lerp(elbowBackAngle, 110f, flinch);
            }

            // ---- drive the joints. Through the strike itself the limbs hit their marks exactly (stiff,
            // critically damped), so the swing snaps instead of lagging behind; after the impact the springs
            // loosen and the arm overshoots and settles.
            bool striking = attacking && pose.phase == AttackPhase.Strike && w > 0.5f;
            float armFreq = striking ? 40f : Mathf.Lerp(8f, afterImpact ? 14f : 24f, w);
            float armDamp = striking ? 1f : Mathf.Lerp(0.55f, afterImpact ? 0.45f : 0.85f, w);
            Quaternion hipsAndSpin = hips * Quaternion.Euler(0f, spin + twist, 0f);
            // Spins must follow exactly (they can pass 180 degrees in a few frames); the rest is sprung.
            if (Mathf.Abs(spin) > 1f) modelSpring.Reset(hipsAndSpin);
            model.localRotation = modelRotation * modelSpring.Step(hipsAndSpin, Mathf.Lerp(6f, 16f, wBody), 0.6f, dt);
            model.localPosition = modelPosition + bodyBob + bodyOffset;

            if (chest != null)
                chest.localRotation = chestSpring.Step(chestRotation * torso, striking ? 26f : Mathf.Lerp(6f, 18f, wBody),
                    striking ? 0.9f : Mathf.Lerp(0.45f, 0.8f, wBody), dt);
            else
                model.localRotation = model.localRotation * torso; // older rigs: bend the whole body

            if (attackArm != null)
            {
                attackArm.localRotation = armSpring.Step(armRotation * arm, armFreq, armDamp, dt);
                attackArm.localPosition = armPosition + armOffset;
            }
            if (backArm != null)
            {
                // The off arm is looser so it trails the body (unless both hands are on the item).
                float offFreq = striking || pose.twoHanded ? armFreq : Mathf.Lerp(6f, 14f, w);
                float offDamp = striking ? 1f : Mathf.Lerp(0.45f, 0.75f, w);
                backArm.localRotation = backArmSpring.Step(backArmRotation * offArm, offFreq, offDamp, dt);
                backArm.localPosition = backArmPosition + backArmOffset;
            }
            if (elbowFront != null)
                elbowFront.localRotation = elbowFrontRotation * Quaternion.Euler(-elbowFrontSpring.Step(elbowFrontAngle,
                    striking ? 40f : Mathf.Lerp(9f, 22f, w), striking ? 1f : Mathf.Lerp(0.55f, 0.85f, w), dt), 0f, 0f);
            if (elbowBack != null)
                elbowBack.localRotation = elbowBackRotation * Quaternion.Euler(-elbowBackSpring.Step(elbowBackAngle,
                    striking ? 40f : Mathf.Lerp(8f, 18f, w), striking ? 1f : 0.5f, dt), 0f, 0f);
            if (wrist != null) wrist.localRotation = wristSpring.Step(wristRotation * wristPose, striking ? 40f : Mathf.Lerp(10f, 24f, w), striking ? 1f : 0.7f, dt);

            if (head != null)
            {
                // Head stays level-ish: counter the lean and twist, nod with each step, glance around when idle.
                UpdateLook(calm);
                // Counter most of the spine's lean and twist (pelvis included) so the eyes stay on the target.
                float torsoPitch = Signed(torso.eulerAngles.x);
                float torsoYaw = Signed(torso.eulerAngles.y) + twist + guard.pelvisYaw * bladed;
                // In a fight the eyes lock on: almost all of the bladed turn is countered, chin tucked.
                float counter = Mathf.Lerp(0.6f, 0.9f, guardWeight);
                float nod = Mathf.Abs(swing) * 3f * walkBlend - torsoPitch * 0.6f + guard.head.x * guardWeight + breathe * 1.5f;
                var target = headRotation * Quaternion.Euler(nod, -torsoYaw * counter + lookYaw, -chestRoll * 0.5f + guard.head.z * guardWeight);
                head.localRotation = headSpring.Step(target, 5.5f, 0.4f, dt);
            }

            Dangle(tie, tieRotation, ref tieSpring, 3.2f, 0.22f, gravity: 0.5f, maxAngle: 30f, keepOffBody: true, dt);
            Dangle(hair, hairRotation, ref hairSpring, 5f, 0.3f, gravity: 0f, maxAngle: 10f, keepOffBody: false, dt);
        }

        /// <summary>Clip-driven frame: keeps the attack bookkeeping, plays the clips, then jolts the spine.</summary>
        void DriveClips(float dt, float speed)
        {
            bool striking = false;
            if (attack != null)
            {
                float t = Time.time - attackStart;
                float total = attack.startup + attack.active + attack.recovery;
                float impact = AttackPoses.ImpactTime(attack.startup, attack.active);
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
            follower.SwingGrip = clips.SwingGrip;
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

        /// <summary>Idle characters glance around now and then.</summary>
        void UpdateLook(float calm)
        {
            if (calm < 0.5f)
            {
                lookYaw = Mathf.MoveTowards(lookYaw, 0f, Time.deltaTime * 120f);
                return;
            }
            if (Time.time >= nextLookTime)
            {
                nextLookTime = Time.time + Random.Range(2.5f, 6f);
                lookYaw = Random.value < 0.35f ? 0f : Random.Range(-38f, 38f);
            }
        }

        /// <summary>
        /// A part that hangs off the body (tie, hair) and lags behind its motion, pulled partly by gravity,
        /// limited so it never swings through the body.
        /// </summary>
        static void Dangle(Transform part, Quaternion rest, ref SpringRotation spring, float frequency, float damping,
            float gravity, float maxAngle, bool keepOffBody, float dt)
        {
            if (part == null || part.parent == null) return;
            Quaternion attached = part.parent.rotation * rest;
            Quaternion target = attached;
            if (gravity > 0f)
            {
                Quaternion hang = Quaternion.FromToRotation(attached * Vector3.down, Vector3.down) * attached;
                target = Quaternion.Slerp(attached, hang, gravity);
            }
            Quaternion world = spring.Step(target, frequency, damping, dt);

            // Clamp relative to the attached pose.
            Vector3 e = (Quaternion.Inverse(attached) * world).eulerAngles;
            e = new Vector3(Signed(e.x), Signed(e.y), Signed(e.z));
            e.x = Mathf.Clamp(e.x, -maxAngle, keepOffBody ? 2f : maxAngle); // +X would swing into the chest
            e.y = Mathf.Clamp(e.y, -maxAngle * 0.4f, maxAngle * 0.4f);
            e.z = Mathf.Clamp(e.z, -maxAngle, maxAngle);
            part.localRotation = rest * Quaternion.Euler(e);
            spring.value = part.rotation;
        }

        static float Signed(float angle) => angle > 180f ? angle - 360f : angle;

        void PoseKnockedOut(float dt)
        {
            // Topple backwards with a bounce off the carpet; limbs go loose and flop on soft springs.
            float t = Time.time - knockOutStart;
            float fall = Mathf.Clamp01(t / 0.42f);
            float angle = -88f * fall * fall;
            if (t > 0.42f) angle += 10f * Mathf.Sin(Mathf.Clamp01((t - 0.42f) / 0.25f) * Mathf.PI) * Mathf.Exp(-(t - 0.42f) * 4f);
            model.localRotation = modelRotation * Quaternion.Euler(angle, 0f, 0f);
            model.localPosition = modelPosition;
            if (chest != null) chest.localRotation = chestSpring.Step(chestRotation * Quaternion.Euler(-12f, 0f, 0f), 3f, 0.3f, dt);
            if (attackArm != null) attackArm.localRotation = armSpring.Step(armRotation * Quaternion.Euler(-155f, 0f, 30f), 3.5f, 0.3f, dt);
            if (backArm != null) backArm.localRotation = backArmSpring.Step(backArmRotation * Quaternion.Euler(-150f, 0f, -35f), 3f, 0.3f, dt);
            if (elbowFront != null) elbowFront.localRotation = elbowFrontRotation * Quaternion.Euler(-elbowFrontSpring.Step(25f, 3f, 0.3f, dt), 0f, 0f);
            if (elbowBack != null) elbowBack.localRotation = elbowBackRotation * Quaternion.Euler(-elbowBackSpring.Step(35f, 3f, 0.3f, dt), 0f, 0f);
            airBlend = 0f;
            SetLegs(-10f * fall, 6f * fall, 25f * fall, 10f * fall, 0f, 0f);
            if (head != null) head.localRotation = headSpring.Step(headRotation * Quaternion.Euler(25f, 20f, 0f), 3f, 0.3f, dt);
            Dangle(tie, tieRotation, ref tieSpring, 3f, 0.25f, 0.8f, 40f, false, dt);
        }

        void SetLegs(float leftLeg, float rightLeg, float leftKnee, float rightKnee, float leftLift, float rightLift)
        {
            // In the air: one knee tucks high, the other trails.
            leftLeg += -40f * airBlend;
            rightLeg += -15f * airBlend;
            leftKnee += 55f * airBlend;
            rightKnee += 25f * airBlend;
            if (legLeft != null) legLeft.localRotation = legLeftRotation * Quaternion.Euler(leftLeg, 0f, 0f);
            if (legRight != null) legRight.localRotation = legRightRotation * Quaternion.Euler(rightLeg, 0f, 0f);
            if (kneeLeft != null) kneeLeft.localRotation = kneeLeftRotation * Quaternion.Euler(leftKnee, 0f, 0f);
            if (kneeRight != null) kneeRight.localRotation = kneeRightRotation * Quaternion.Euler(rightKnee, 0f, 0f);
            // Planted feet stay flat on the floor; lifted feet trail with the shin, toes pointing down.
            float leftFoot = -(leftLeg + leftKnee) * Mathf.Lerp(1f, 0.35f, Mathf.Max(leftLift, airBlend));
            float rightFoot = -(rightLeg + rightKnee) * Mathf.Lerp(1f, 0.35f, Mathf.Max(rightLift, airBlend));
            if (ankleLeft != null) ankleLeft.localRotation = ankleLeftRotation * Quaternion.Euler(leftFoot, 0f, 0f);
            if (ankleRight != null) ankleRight.localRotation = ankleRightRotation * Quaternion.Euler(rightFoot, 0f, 0f);
        }

        void UpdateFace(float dt)
        {
            // Blink every few seconds; KO'd faces stay shut.
            if (eyes != null && eyes.Length > 0)
            {
                if (blinkStart < 0f && Time.time >= nextBlink)
                {
                    blinkStart = Time.time;
                    nextBlink = Time.time + Random.Range(2.2f, 5.5f);
                }
                float open = 1f;
                if (knockedOut) open = 0.1f;
                else if (blinkStart >= 0f)
                {
                    float b = (Time.time - blinkStart) / 0.14f;
                    if (b >= 1f) blinkStart = -1f;
                    else open = Mathf.Lerp(1f, 0.1f, Mathf.Sin(b * Mathf.PI));
                }
                for (int i = 0; i < eyes.Length; i++)
                    if (eyes[i] != null) eyes[i].localScale = new Vector3(eyeScales[i].x, eyeScales[i].y * open, eyeScales[i].z);
            }

            // Brows knit into a scowl while fighting, and stay a little furrowed in a fighting stance.
            if (brows != null && brows.Length > 0)
            {
                float scowl = attack != null ? 1f : stance * 0.45f;
                for (int i = 0; i < brows.Length; i++)
                {
                    if (brows[i] == null) continue;
                    float side = i % 2 == 0 ? 1f : -1f;
                    var target = browRotations[i] * Quaternion.Euler(0f, 0f, -18f * side * scowl);
                    brows[i].localRotation = Quaternion.Slerp(brows[i].localRotation, target, 1f - Mathf.Exp(-dt * 18f));
                }
            }
        }

        static float EaseInOut(float k) { k = Mathf.Clamp01(k); return k * k * (3f - 2f * k); }
    }
}
