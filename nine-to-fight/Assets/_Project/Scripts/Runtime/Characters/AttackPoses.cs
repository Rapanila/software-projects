using NineToFight.Combat;
using UnityEngine;

namespace NineToFight.Characters
{
    public enum AttackPhase { None, Windup, Strike, FollowThrough, Recovery }

    /// <summary>
    /// One key pose of an attack, relative to rest.
    /// arm: weapon arm rotation; it hangs straight down at rest (-90 on X points it forward, -180 straight up).
    /// armSpin: extra loops of the arm around the shoulder in degrees (for windmills), interpolated linearly.
    /// torso: +X leans forward, +Y twists the weapon-side shoulder back.
    /// spin: whole-body turn in degrees (for spin attacks), interpolated linearly so it can exceed 360.
    /// body: whole-body offset in metres: +Y hop, -Y crouch, +Z step forward. The legs follow it.
    /// elbow / backElbow: bend in degrees (0 straight); NaN picks a default for the key's role
    /// (chambered in the wind-up, snapping straight on the strike).
    /// kick: legs for kicks, knees and leaps: x/y are the rear thigh (negative = forward/up) and knee,
    /// z/w the lead thigh and knee (0 leaves a leg to the footwork).
    /// </summary>
    public struct AttackPose
    {
        public Vector3 arm;
        public Vector3 armOffset;
        public Vector3 wrist;
        public Vector3 torso;
        public Vector3 backArm;
        public Vector3 body;
        public float spin;
        public float armSpin;
        public float elbow;
        public float backElbow;
        public Vector4 kick;

        public AttackPose(Vector3 arm, Vector3 wrist = default, Vector3 torso = default, Vector3 armOffset = default,
            Vector3 backArm = default, Vector3 body = default, float spin = 0f, float armSpin = 0f,
            float elbow = float.NaN, float backElbow = float.NaN, Vector4 kick = default)
        {
            this.kick = kick;
            this.arm = arm;
            this.wrist = wrist;
            this.torso = torso;
            this.armOffset = armOffset;
            this.backArm = backArm;
            this.body = body;
            this.spin = spin;
            this.armSpin = armSpin;
            this.elbow = elbow;
            this.backElbow = backElbow;
        }
    }

    /// <summary>
    /// Key poses for one attack style. The wind-up passes through every "windup" key during startup;
    /// the strike snaps through every "strike" key at the start of the active window (last one = impact).
    /// </summary>
    public struct AttackChoreography
    {
        public AttackPose[] windup;
        public AttackPose[] strike;
        public AttackPose follow;
        public bool twoHanded;
        /// <summary>Seconds for the swing from wind-up into the strike; bigger arcs need longer. 0 = default.</summary>
        public float swingTime;

        public AttackPose Windup => windup[windup.Length - 1];
        public AttackPose Strike => strike[strike.Length - 1];
    }

    public struct SampledPose
    {
        public Quaternion arm, wrist, torso, backArm;
        public Vector3 armOffset, body;
        public float spin, armSpin, elbow, backElbow;
        public Vector4 kick;
        /// <summary>How much the attack overrides the stance: eases in through the wind-up, out over the recovery.</summary>
        public float weight;
        public bool twoHanded;
        public AttackPhase phase;
    }

    /// <summary>Hand-authored attack choreography, sampled over a move's startup/active/recovery timing.</summary>
    public static class AttackPoses
    {
        /// <summary>Direction the arm points (shoulder to hand) for a given arm rotation.</summary>
        public static Vector3 ArmDirection(Quaternion armRotation) => armRotation * Vector3.down;

        static AttackPose P(Vector3 arm, Vector3 wrist = default, Vector3 torso = default, Vector3 armOffset = default,
            Vector3 backArm = default, Vector3 body = default, float spin = 0f, float armSpin = 0f,
            float elbow = float.NaN, float backElbow = float.NaN, Vector4 kick = default) =>
            new AttackPose(arm, wrist, torso, armOffset, backArm, body, spin, armSpin, elbow, backElbow, kick);

        static Vector4 K(float rearThigh, float rearKnee, float leadThigh = 0f, float leadKnee = 0f) =>
            new Vector4(rearThigh, rearKnee, leadThigh, leadKnee);

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        /// <summary>The style's choreography with every elbow filled in. Shared: don't modify the keys.</summary>
        public static AttackChoreography For(AttackStyle style)
        {
            if (Cache.TryGetValue(style, out var cached)) return cached;
            var c = Author(style);
            int last = c.strike.Length - 1;
            if (float.IsNaN(c.strike[last].elbow)) c.strike[last].elbow = 5f; // the arm snaps straight on impact
            Resolve(c.windup, 75f, 100f);
            Resolve(c.strike, 35f, 80f);
            c.follow = Resolved(c.follow, 12f, 85f);
            Cache[style] = c;
            return c;
        }

        static readonly System.Collections.Generic.Dictionary<AttackStyle, AttackChoreography> Cache =
            new System.Collections.Generic.Dictionary<AttackStyle, AttackChoreography>();

        static void Resolve(AttackPose[] keys, float elbow, float backElbow)
        {
            for (int i = 0; i < keys.Length; i++) keys[i] = Resolved(keys[i], elbow, backElbow);
        }

        static AttackPose Resolved(AttackPose key, float elbow, float backElbow)
        {
            if (float.IsNaN(key.elbow)) key.elbow = elbow;
            if (float.IsNaN(key.backElbow)) key.backElbow = backElbow;
            return key;
        }

        /// <summary>Hand-authored keys per style; unset elbows are filled in by For.</summary>
        static AttackChoreography Author(AttackStyle style)
        {
            switch (style)
            {
                case AttackStyle.Jab: // calculator: quick straight punch
                    return new AttackChoreography
                    {
                        swingTime = 0.06f,
                        windup = new[] { P(V(-80, 15, 0), torso: V(-4, 30, 0), armOffset: V(0, 0, -0.06f), backArm: V(-35, 0, 0), body: V(0, -0.03f, -0.04f), elbow: 110f, backElbow: 105f) },
                        strike = new[] { P(V(-92, -6, 0), torso: V(10, -30, 0), armOffset: V(0, 0, 0.07f), backArm: V(30, 0, 0), body: V(0, -0.04f, 0.12f), elbow: 2f, backElbow: 110f) },
                        follow = P(V(-88, -4, 0), torso: V(6, -20, 0), armOffset: V(0, 0, 0.05f), backArm: V(20, 0, 0), body: V(0, -0.03f, 0.08f), elbow: 8f, backElbow: 100f),
                    };

                case AttackStyle.Chop: // rolled-up report: one-handed overhead smack
                    return new AttackChoreography
                    {
                        swingTime = 0.11f,
                        windup = new[] { P(V(-210, 0, 0), wrist: V(35, 0, 0), torso: V(-10, 12, 0), backArm: V(-30, 0, 0), body: V(0, 0.02f, -0.06f), elbow: 115f) },
                        strike = new[] { P(V(-70, 0, 0), wrist: V(-25, 0, 0), torso: V(16, -6, 0), backArm: V(35, 0, 0), body: V(0, -0.08f, 0.12f), elbow: 8f) },
                        follow = P(V(-40, 0, 0), wrist: V(-15, 0, 0), torso: V(12, -4, 0), backArm: V(25, 0, 0), body: V(0, -0.07f, 0.1f), elbow: 15f),
                    };

                case AttackStyle.Slam: // binder: two hands straight over the head, crouch into it
                    return new AttackChoreography
                    {
                        swingTime = 0.12f,
                        twoHanded = true,
                        windup = new[] { P(V(-200, 0, 0), wrist: V(25, 0, 0), torso: V(-16, 0, 0), armOffset: V(-0.07f, 0.05f, 0), body: V(0, 0.05f, -0.05f), elbow: 70f) },
                        strike = new[] { P(V(-55, 0, 0), wrist: V(-20, 0, 0), torso: V(24, 0, 0), armOffset: V(-0.07f, -0.03f, 0.05f), body: V(0, -0.12f, 0.16f), elbow: 10f) },
                        follow = P(V(-35, 0, 0), wrist: V(-10, 0, 0), torso: V(20, 0, 0), armOffset: V(-0.07f, -0.04f, 0.03f), body: V(0, -0.1f, 0.12f), elbow: 15f),
                    };

                case AttackStyle.Sweep: // smartphone: horizontal backhand slap across the body
                    return new AttackChoreography
                    {
                        swingTime = 0.1f,
                        windup = new[] { P(V(-85, 90, 0), wrist: V(0, 30, 0), torso: V(-3, 38, 0), backArm: V(-20, 0, 0), body: V(0, -0.03f, -0.04f), elbow: 70f) },
                        strike = new[] { P(V(-90, -15, 0), wrist: V(0, -20, 0), torso: V(6, -18, 0), backArm: V(30, 0, 0), body: V(0, -0.05f, 0.1f), elbow: 15f) },
                        follow = P(V(-75, -80, 0), wrist: V(0, -30, 0), torso: V(4, -40, 0), backArm: V(35, 0, 0), body: V(0, -0.04f, 0.08f), elbow: 25f),
                    };

                case AttackStyle.Whip: // ethernet cable: high wind-up, wrist cracks it forward
                    return new AttackChoreography
                    {
                        swingTime = 0.12f,
                        windup = new[]
                        {
                            P(V(-110, 0, 15), wrist: V(40, 0, 0), torso: V(-4, 10, 0), backArm: V(-20, 0, 0), body: V(0, 0, -0.04f), elbow: 60f),
                            P(V(-195, 0, 25), wrist: V(80, 0, 0), torso: V(-8, 18, 0), backArm: V(-25, 0, 0), body: V(0, 0.02f, -0.08f), elbow: 105f),
                        },
                        strike = new[]
                        {
                            P(V(-128, 0, 8), wrist: V(55, 0, 0), torso: V(3, 3, 0), body: V(0, -0.02f, 0.04f), elbow: 45f),
                            P(V(-60, 0, -10), wrist: V(-55, 0, 0), torso: V(14, -12, 0), backArm: V(30, 0, 0), body: V(0, -0.05f, 0.12f), elbow: 0f),
                        },
                        follow = P(V(-30, 0, -10), wrist: V(-25, 0, 0), torso: V(10, -10, 0), backArm: V(20, 0, 0), body: V(0, -0.04f, 0.1f), elbow: 10f),
                    };

                case AttackStyle.Toss: // coffee: underhand fling
                    return new AttackChoreography
                    {
                        swingTime = 0.15f,
                        windup = new[] { P(V(50, 0, 0), wrist: V(30, 0, 0), torso: V(8, 18, 0), backArm: V(-30, 0, 0), body: V(0, -0.08f, -0.05f), elbow: 25f) },
                        strike = new[] { P(V(-120, 0, 0), wrist: V(-40, 0, 0), torso: V(-6, -12, 0), backArm: V(25, 0, 0), body: V(0, 0.02f, 0.1f), elbow: 10f) },
                        follow = P(V(-145, 0, 0), wrist: V(-30, 0, 0), torso: V(-8, -14, 0), backArm: V(20, 0, 0), body: V(0, 0.02f, 0.08f), elbow: 15f),
                    };

                case AttackStyle.Golf: // golf club: backswing, low strike, high follow-through
                    return new AttackChoreography
                    {
                        swingTime = 0.17f,
                        twoHanded = true,
                        windup = new[] { P(V(-205, 55, 0), wrist: V(40, 0, 0), torso: V(4, 45, 0), armOffset: V(-0.08f, 0, 0), body: V(0, -0.06f, -0.04f), elbow: 30f) },
                        strike = new[]
                        {
                            P(V(-110, 20, 0), wrist: V(20, 0, 0), torso: V(10, 15, 0), armOffset: V(-0.08f, 0, 0), body: V(0, -0.08f, 0), elbow: 20f),
                            P(V(-35, -15, 0), wrist: V(-30, 0, 0), torso: V(12, -20, 0), armOffset: V(-0.08f, -0.03f, 0.03f), body: V(0, -0.08f, 0.04f), elbow: 5f),
                        },
                        follow = P(V(-165, -65, 0), wrist: V(-20, 0, 0), torso: V(-6, -50, 0), armOffset: V(-0.08f, 0, 0), body: V(0, -0.02f, 0.06f), elbow: 35f),
                    };

                case AttackStyle.Blast: // megaphone: lean back, then thrust
                    return new AttackChoreography
                    {
                        swingTime = 0.08f,
                        windup = new[] { P(V(-100, 0, 0), torso: V(-18, 0, 0), armOffset: V(0, 0.04f, -0.06f), backArm: V(-20, 0, 30), body: V(0, 0, -0.1f), elbow: 75f, backElbow: 60f) },
                        strike = new[] { P(V(-88, 0, 0), torso: V(18, 0, 0), armOffset: V(0, 0, 0.07f), backArm: V(15, 0, 35), body: V(0, -0.05f, 0.2f), elbow: 3f, backElbow: 40f) },
                        follow = P(V(-85, 0, 0), torso: V(12, 0, 0), armOffset: V(0, 0, 0.05f), backArm: V(10, 0, 30), body: V(0, -0.04f, 0.16f), elbow: 6f, backElbow: 45f),
                    };

                case AttackStyle.DiagonalSlash: // ledger: from high outside, cut down across the body
                    return new AttackChoreography
                    {
                        swingTime = 0.11f,
                        windup = new[] { P(V(-160, 65, 0), wrist: V(20, 30, 0), torso: V(-6, 32, 0), backArm: V(-25, 0, -20), body: V(0, 0.02f, -0.06f), elbow: 105f) },
                        strike = new[] { P(V(-65, -45, 0), wrist: V(-15, -25, 0), torso: V(14, -26, 0), backArm: V(30, 0, 0), body: V(0, -0.07f, 0.12f), elbow: 10f) },
                        follow = P(V(-35, -70, 0), wrist: V(-10, -30, 0), torso: V(10, -32, 0), backArm: V(25, 0, 0), body: V(0, -0.06f, 0.1f), elbow: 18f),
                    };

                case AttackStyle.Stamp: // audit stamp: hop up, crouch and slam it straight down
                    return new AttackChoreography
                    {
                        swingTime = 0.13f,
                        windup = new[]
                        {
                            P(V(-120, 0, 0), torso: V(-6, 0, 0), backArm: V(-40, 0, 20), body: V(0, -0.1f, 0), elbow: 60f, backElbow: 50f),
                            P(V(-185, 0, 0), wrist: V(30, 0, 0), torso: V(-12, 0, 0), backArm: V(-60, 0, 30), body: V(0, 0.32f, 0.05f), elbow: 95f, backElbow: 40f),
                        },
                        strike = new[] { P(V(-25, 0, 0), wrist: V(-10, 0, 0), torso: V(32, 0, 0), armOffset: V(0, -0.04f, 0.06f), backArm: V(20, 0, 25), body: V(0, -0.2f, 0.12f), elbow: 5f, backElbow: 60f) },
                        follow = P(V(-20, 0, 0), torso: V(28, 0, 0), armOffset: V(0, -0.04f, 0.06f), backArm: V(15, 0, 20), body: V(0, -0.18f, 0.12f), elbow: 8f, backElbow: 60f),
                    };

                case AttackStyle.Windmill: // mouse on a cord: a full loop, then fling
                    return new AttackChoreography
                    {
                        swingTime = 0.12f,
                        windup = new[]
                        {
                            P(V(-90, 0, 15), wrist: V(40, 0, 0), torso: V(-4, 10, 0), backArm: V(-20, 0, 0), body: V(0, 0, -0.03f), armSpin: -180f, elbow: 10f),
                            P(V(-130, 0, 15), wrist: V(60, 0, 0), torso: V(-8, 14, 0), backArm: V(-25, 0, 0), body: V(0, 0.01f, -0.05f), armSpin: -360f, elbow: 10f),
                        },
                        strike = new[] { P(V(-65, 0, 5), wrist: V(-50, 0, 0), torso: V(14, -14, 0), backArm: V(30, 0, 0), body: V(0, -0.04f, 0.1f), armSpin: -360f, elbow: 5f) },
                        follow = P(V(-45, 0, 0), wrist: V(-30, 0, 0), torso: V(10, -10, 0), backArm: V(20, 0, 0), body: V(0, -0.03f, 0.08f), armSpin: -360f, elbow: 10f),
                    };

                case AttackStyle.Shove: // laptop: tuck it in two hands, then push with a big step
                    return new AttackChoreography
                    {
                        swingTime = 0.09f,
                        twoHanded = true,
                        windup = new[] { P(V(-60, 0, 0), wrist: V(30, 0, 0), torso: V(-10, 0, 0), armOffset: V(-0.07f, -0.03f, -0.05f), body: V(0, -0.06f, -0.14f), elbow: 115f) },
                        strike = new[] { P(V(-95, 0, 0), wrist: V(-10, 0, 0), torso: V(18, 0, 0), armOffset: V(-0.07f, 0, 0.07f), body: V(0, -0.06f, 0.36f), elbow: 15f) },
                        follow = P(V(-92, 0, 0), torso: V(14, 0, 0), armOffset: V(-0.07f, 0, 0.05f), body: V(0, -0.05f, 0.32f), elbow: 18f),
                    };

                case AttackStyle.Lunge: // desk phone: deep fencing lunge
                    return new AttackChoreography
                    {
                        swingTime = 0.1f,
                        windup = new[] { P(V(-55, 25, 0), wrist: V(10, 0, 0), torso: V(-6, 28, 0), backArm: V(-90, 0, -30), body: V(0, -0.16f, -0.12f), elbow: 85f, backElbow: 30f) },
                        strike = new[] { P(V(-98, -5, 0), wrist: V(-10, 0, 0), torso: V(26, -15, 0), armOffset: V(0, 0.03f, 0.07f), backArm: V(40, 0, -20), body: V(0, -0.24f, 0.42f), elbow: 0f, backElbow: 15f) },
                        follow = P(V(-95, -5, 0), torso: V(22, -12, 0), armOffset: V(0, 0.03f, 0.05f), backArm: V(35, 0, -20), body: V(0, -0.2f, 0.36f), elbow: 4f, backElbow: 20f),
                    };

                case AttackStyle.Backhand: // clipboard: from across the body, slap outward
                    return new AttackChoreography
                    {
                        swingTime = 0.1f,
                        windup = new[] { P(V(-80, -75, 0), wrist: V(0, -30, 0), torso: V(-2, -35, 0), backArm: V(-10, 0, 0), body: V(0, -0.03f, -0.03f), elbow: 110f) },
                        strike = new[] { P(V(-92, 22, 0), wrist: V(0, 35, 0), torso: V(8, 12, 0), backArm: V(25, 0, 0), body: V(0, -0.04f, 0.08f), elbow: 15f) },
                        follow = P(V(-80, 85, 0), wrist: V(0, 30, 0), torso: V(4, 32, 0), backArm: V(30, 0, 0), body: V(0, -0.03f, 0.06f), elbow: 25f),
                    };

                case AttackStyle.Spin: // keyboard: coil, then a full turn with arms out, landing the hit facing front
                    return new AttackChoreography
                    {
                        swingTime = 0.12f,
                        twoHanded = true,
                        windup = new[]
                        {
                            P(V(-80, 40, 0), torso: V(-6, 20, 0), armOffset: V(-0.07f, 0, 0), body: V(0, -0.08f, 0), spin: -60f, elbow: 40f),
                            P(V(-90, 10, 0), torso: V(4, 0, 0), armOffset: V(-0.07f, 0, 0.05f), body: V(0, -0.04f, 0), spin: 120f, elbow: 25f),
                            P(V(-90, 10, 0), torso: V(6, 0, 0), armOffset: V(-0.07f, 0, 0.05f), body: V(0, -0.04f, 0), spin: 270f, elbow: 25f),
                        },
                        strike = new[] { P(V(-88, -15, 0), wrist: V(-20, 0, 0), torso: V(14, -10, 0), armOffset: V(-0.07f, 0, 0.07f), body: V(0, -0.06f, 0.08f), spin: 360f, elbow: 15f) },
                        follow = P(V(-80, -30, 0), torso: V(10, -14, 0), armOffset: V(-0.07f, 0, 0.05f), body: V(0, -0.05f, 0.05f), spin: 360f, elbow: 20f),
                    };

                case AttackStyle.Uppercut: // crouch low, then drive the item up through the chin
                    return new AttackChoreography
                    {
                        swingTime = 0.17f,
                        windup = new[] { P(V(25, 10, 0), wrist: V(20, 0, 0), torso: V(14, 25, 0), backArm: V(-30, 0, 0), body: V(0, -0.16f, -0.03f), elbow: 100f) },
                        strike = new[] { P(V(-150, -5, 0), wrist: V(-30, 0, 0), torso: V(-10, -18, 0), backArm: V(30, 0, 0), body: V(0, 0.1f, 0.12f), elbow: 55f) },
                        follow = P(V(-168, -5, 0), wrist: V(-20, 0, 0), torso: V(-12, -20, 0), backArm: V(25, 0, 0), body: V(0, 0.06f, 0.1f), elbow: 45f),
                    };

                case AttackStyle.Hook: // bent-arm hook from the outside, fast and tight
                    return new AttackChoreography
                    {
                        swingTime = 0.1f,
                        windup = new[] { P(V(-90, 60, 0), wrist: V(0, 50, 0), torso: V(-2, 32, 0), backArm: V(-30, 0, 0), body: V(0, -0.04f, -0.03f), elbow: 95f, backElbow: 105f) },
                        strike = new[] { P(V(-92, -30, 0), wrist: V(0, -45, 0), torso: V(8, -28, 0), backArm: V(30, 0, 0), body: V(0, -0.06f, 0.1f), elbow: 88f, backElbow: 105f) },
                        follow = P(V(-88, -50, 0), wrist: V(0, -40, 0), torso: V(6, -34, 0), backArm: V(25, 0, 0), body: V(0, -0.05f, 0.08f), elbow: 92f, backElbow: 100f),
                    };

                case AttackStyle.Hurl: // overhand pitch: rear back, step in, release forward
                    return new AttackChoreography
                    {
                        swingTime = 0.14f,
                        windup = new[]
                        {
                            P(V(-110, 20, -20), wrist: V(20, 0, 0), torso: V(-6, 25, 0), backArm: V(-80, 0, -20), body: V(0, 0, -0.05f), elbow: 70f, backElbow: 20f),
                            P(V(-195, 25, -25), wrist: V(45, 0, 0), torso: V(-14, 40, 0), backArm: V(-95, 0, -25), body: V(0, 0.02f, -0.1f), elbow: 115f, backElbow: 15f),
                        },
                        strike = new[] { P(V(-65, -10, 0), wrist: V(-40, 0, 0), torso: V(20, -28, 0), backArm: V(35, 0, 0), body: V(0, -0.04f, 0.2f), elbow: 10f, backElbow: 90f) },
                        follow = P(V(-25, -20, 0), wrist: V(-30, 0, 0), torso: V(26, -34, 0), backArm: V(30, 0, 0), body: V(0, -0.06f, 0.18f)),
                    };

                case AttackStyle.Cleave: // two-handed horizontal cleave across the whole body
                    return new AttackChoreography
                    {
                        swingTime = 0.17f,
                        twoHanded = true,
                        windup = new[] { P(V(-95, 95, 0), wrist: V(0, 30, 0), torso: V(-4, 48, 0), armOffset: V(-0.07f, 0, 0), body: V(0, -0.06f, -0.05f), elbow: 45f) },
                        strike = new[] { P(V(-90, -40, 0), wrist: V(0, -25, 0), torso: V(8, -38, 0), armOffset: V(-0.07f, 0, 0.05f), body: V(0, -0.08f, 0.12f)) },
                        follow = P(V(-82, -90, 0), wrist: V(0, -30, 0), torso: V(4, -52, 0), armOffset: V(-0.07f, 0, 0.03f), body: V(0, -0.06f, 0.1f)),
                    };

                case AttackStyle.Pound: // short two-handed pound straight down, again and again
                    return new AttackChoreography
                    {
                        swingTime = 0.08f,
                        twoHanded = true,
                        windup = new[] { P(V(-140, 0, 0), wrist: V(20, 0, 0), torso: V(-8, 0, 0), armOffset: V(-0.07f, 0.04f, 0), body: V(0, 0.04f, 0), elbow: 85f) },
                        strike = new[] { P(V(-60, 0, 0), wrist: V(-25, 0, 0), torso: V(22, 0, 0), armOffset: V(-0.07f, -0.04f, 0.05f), body: V(0, -0.1f, 0.06f), elbow: 20f) },
                        follow = P(V(-50, 0, 0), wrist: V(-15, 0, 0), torso: V(18, 0, 0), armOffset: V(-0.07f, -0.04f, 0.04f), body: V(0, -0.08f, 0.05f), elbow: 25f),
                    };

                case AttackStyle.LeadJab: // snapping straight punch with the lead hand; the item stays on guard
                    return new AttackChoreography
                    {
                        swingTime = 0.05f,
                        windup = new[] { P(V(-42, -16, 12), wrist: V(8, 0, 0), torso: V(-2, 2, 0), backArm: V(-62, -18, 0), body: V(0, -0.03f, -0.03f), elbow: 88f, backElbow: 112f) },
                        strike = new[] { P(V(-46, -16, 12), wrist: V(8, 0, 0), torso: V(6, 22, 0), backArm: V(-92, -14, 0), body: V(0, -0.04f, 0.1f), elbow: 92f, backElbow: 0f) },
                        follow = P(V(-44, -16, 12), wrist: V(8, 0, 0), torso: V(4, 16, 0), backArm: V(-88, -14, 0), body: V(0, -0.03f, 0.08f), elbow: 90f, backElbow: 8f),
                    };

                case AttackStyle.Kick: // chamber the rear knee high, then a straight push kick; the weapon arm swings back to balance
                    return new AttackChoreography
                    {
                        swingTime = 0.09f,
                        windup = new[] { P(V(-50, -16, 14), wrist: V(8, 0, 0), torso: V(-8, -10, 0), backArm: V(-62, -20, -6), body: V(0, 0.02f, -0.03f), elbow: 100f, backElbow: 95f, kick: K(-75, 115)) },
                        strike = new[] { P(V(20, -10, 22), wrist: V(10, 0, 0), torso: V(-16, -22, 0), backArm: V(-66, -24, -8), body: V(0, 0.04f, 0.12f), elbow: 35f, backElbow: 95f, kick: K(-92, 6)) },
                        follow = P(V(10, -10, 18), wrist: V(8, 0, 0), torso: V(-12, -16, 0), backArm: V(-62, -22, -6), body: V(0, 0.03f, 0.1f), elbow: 45f, backElbow: 92f, kick: K(-72, 45)),
                    };

                case AttackStyle.Knee: // grab the collar with both hands, haul them down onto the rising rear knee
                    return new AttackChoreography
                    {
                        swingTime = 0.1f,
                        windup = new[] { P(V(-85, -22, 0), wrist: V(10, 0, 0), torso: V(-4, -6, 0), backArm: V(-85, 22, 0), body: V(0, -0.02f, 0.04f), elbow: 30f, backElbow: 30f, kick: K(-15, 35)) },
                        strike = new[] { P(V(-50, -16, 0), torso: V(16, -14, 0), backArm: V(-50, 16, 0), body: V(0, 0.07f, 0.12f), elbow: 95f, backElbow: 95f, kick: K(-105, 125)) },
                        follow = P(V(-55, -16, 0), torso: V(12, -10, 0), backArm: V(-55, 16, 0), body: V(0, 0.05f, 0.1f), elbow: 90f, backElbow: 90f, kick: K(-85, 118)),
                    };

                case AttackStyle.Elbow: // arm folded tight, the point of the elbow whips across at head height
                    return new AttackChoreography
                    {
                        swingTime = 0.08f,
                        windup = new[] { P(V(-70, 55, 25), wrist: V(20, 0, 0), torso: V(-2, 34, 0), backArm: V(-60, -20, 0), body: V(0, -0.03f, -0.04f), elbow: 145f, backElbow: 110f) },
                        strike = new[] { P(V(-98, -35, 10), wrist: V(10, 0, 0), torso: V(8, -30, 0), backArm: V(20, 0, 0), body: V(0, -0.05f, 0.12f), elbow: 150f, backElbow: 85f) },
                        follow = P(V(-92, -60, 5), wrist: V(10, 0, 0), torso: V(6, -40, 0), backArm: V(25, 0, 0), body: V(0, -0.04f, 0.1f), elbow: 148f, backElbow: 85f),
                    };

                case AttackStyle.FlyingKick: // crouch, launch, and spear the rear leg out in mid-air, lead knee tucked
                    return new AttackChoreography
                    {
                        swingTime = 0.12f,
                        windup = new[] { P(V(30, -10, 18), wrist: V(10, 0, 0), torso: V(12, -6, 0), backArm: V(30, 0, -18), body: V(0, -0.12f, -0.04f), elbow: 40f, backElbow: 40f, kick: K(-15, 50, -10, 45)) },
                        strike = new[] { P(V(35, -10, 40), wrist: V(10, 0, 0), torso: V(-22, -18, 0), backArm: V(-70, -20, -30), body: V(0, 0.42f, 0.32f), elbow: 25f, backElbow: 60f, kick: K(-88, 4, -40, 110)) },
                        follow = P(V(25, -10, 35), wrist: V(8, 0, 0), torso: V(-14, -12, 0), backArm: V(-60, -20, -25), body: V(0, 0.2f, 0.38f), elbow: 30f, backElbow: 60f, kick: K(-60, 40, -25, 70)),
                    };

                case AttackStyle.Dropkick: // leap, lie back in the air with arms flung wide, and plant both feet
                    return new AttackChoreography
                    {
                        swingTime = 0.12f,
                        windup = new[] { P(V(25, -10, 20), torso: V(14, 0, 0), backArm: V(25, 0, -20), body: V(0, -0.14f, -0.03f), elbow: 35f, backElbow: 35f, kick: K(-10, 45, -10, 45)) },
                        strike = new[] { P(V(10, -10, 70), torso: V(-48, -10, 0), backArm: V(10, 0, -70), body: V(0, 0.5f, 0.3f), elbow: 15f, backElbow: 15f, kick: K(-95, 5, -88, 10)) },
                        follow = P(V(0, -10, 60), torso: V(-40, -8, 0), backArm: V(0, 0, -60), body: V(0, 0.3f, 0.4f), elbow: 20f, backElbow: 20f, kick: K(-80, 25, -75, 30)),
                    };

                case AttackStyle.SupermanPunch: // hop off the rear leg, kick it back and fly in behind a straight punch
                    return new AttackChoreography
                    {
                        swingTime = 0.1f,
                        windup = new[] { P(V(-30, -12, 14), wrist: V(10, 0, 0), torso: V(-4, 18, 0), backArm: V(-60, -20, -6), body: V(0, -0.08f, -0.04f), elbow: 115f, backElbow: 100f, kick: K(-35, 70)) },
                        strike = new[] { P(V(-96, -6, 0), wrist: V(-5, 0, 0), torso: V(18, -24, 0), backArm: V(35, 0, -15), body: V(0, 0.16f, 0.42f), elbow: 0f, backElbow: 70f, kick: K(45, 70, -30, 50)) },
                        follow = P(V(-92, -6, 0), torso: V(14, -20, 0), backArm: V(30, 0, -12), body: V(0, 0.08f, 0.4f), elbow: 6f, backElbow: 75f, kick: K(30, 50, -20, 35)),
                    };

                default:
                    return Author(AttackStyle.Jab);
            }
        }

        /// <summary>When the strike pose lands: the middle of the move's hit window.</summary>
        public static float ImpactTime(float startup, float active) => startup + active * 0.5f;

        /// <summary>Share of the recovery spent finishing the follow-through before handing back to the stance.</summary>
        const float FollowThroughShareOfRecovery = 0.45f;
        /// <summary>Share of the wind-up spent loading into the coil; the rest keeps tightening it.</summary>
        const float CoilShareOfWindup = 0.65f;
        /// <summary>How much further the body and off arm keep coiling (away from the strike) before the release.</summary>
        const float CoilTightening = 0.12f;

        /// <summary>
        /// Pose at time t into a move. The attack eases in from whatever stance the body is in, loads
        /// into its wind-up keys, then keeps coiling a little tighter until the release (anticipation
        /// with tension instead of a dead hold). The swing toward the strike keys takes the style's own
        /// swing time and lands at the impact time; the follow-through carries on into the first part of
        /// recovery, then the weight eases out so the stance takes back over (no drop to a limp rest).
        /// Phases are labelled by the move's frame data, so Strike/FollowThrough line up with the hit window.
        /// With <paramref name="hold"/> (more hits of a combo to come) the follow-through is held instead.
        /// </summary>
        public static SampledPose Sample(AttackStyle style, float t, float startup, float active, float recovery, bool hold = false)
        {
            var c = For(style);
            float total = startup + active + recovery;
            if (t < 0f || t >= total) return Blend(c.windup[0], c.windup[0], 0f, 0f, c.twoHanded, AttackPhase.None);

            float impact = ImpactTime(startup, active);
            float swing = Mathf.Min(c.swingTime > 0f ? c.swingTime : 0.08f, impact * 0.75f);
            float windupEnd = impact - swing;
            float followEnd = startup + active + recovery * FollowThroughShareOfRecovery;
            // Single-pose wind-ups load quickly and keep tightening; multi-pose ones (loops, spins) need
            // the whole wind-up to travel through their keys.
            bool coils = c.windup.Length == 1;
            var release = coils ? Coiled(c) : c.Windup;

            AttackPhase phase =
                t < windupEnd ? AttackPhase.Windup :
                t < impact ? AttackPhase.Strike :
                t < startup + active ? AttackPhase.FollowThrough :
                AttackPhase.Recovery;

            if (t < windupEnd)
            {
                float coilEnd = coils ? windupEnd * CoilShareOfWindup : windupEnd;
                if (t >= coilEnd)
                    return Blend(c.Windup, release, EaseInOut((t - coilEnd) / Mathf.Max(windupEnd - coilEnd, 0.0001f)), 1f, c.twoHanded, phase);
                float scaled = t / Mathf.Max(coilEnd, 0.0001f) * c.windup.Length;
                int index = Mathf.Min((int)scaled, c.windup.Length - 1);
                float k = scaled - index;
                // The first key fades in over the stance; later keys flow from one to the next.
                return index == 0
                    ? Blend(c.windup[0], c.windup[0], 0f, EaseOutQuad(k), c.twoHanded, phase)
                    : Blend(c.windup[index - 1], c.windup[index], k, 1f, c.twoHanded, phase);
            }
            if (t < impact)
                return Path(release, c.strike, SwingEase((t - windupEnd) / swing), c.twoHanded, phase);
            if (t < followEnd)
                return Blend(c.Strike, c.follow, EaseOutQuad((t - impact) / Mathf.Max(followEnd - impact, 0.0001f)), 1f, c.twoHanded, phase);

            // Mid-combo the follow-through is held, so the next strike flows straight on from it.
            if (hold) return Blend(c.follow, c.follow, 0f, 1f, c.twoHanded, phase);
            float settle = EaseInOut((t - followEnd) / Mathf.Max(total - followEnd, 0.0001f));
            return Blend(c.follow, c.follow, 0f, 1f - settle, c.twoHanded, phase);
        }

        /// <summary>The wind-up pulled a little further from the strike: body, torso and off arm load up.</summary>
        static AttackPose Coiled(AttackChoreography c)
        {
            var from = c.Windup;
            var toward = c.strike[0];
            var coiled = from;
            coiled.torso = Away(from.torso, toward.torso);
            coiled.backArm = Away(from.backArm, toward.backArm);
            coiled.body = from.body + (from.body - toward.body) * CoilTightening;
            coiled.elbow = Mathf.Min(from.elbow + 6f, 140f);
            return coiled;
        }

        static Vector3 Away(Vector3 from, Vector3 toward) =>
            Quaternion.SlerpUnclamped(Quaternion.Euler(from), Quaternion.Euler(toward), -CoilTightening).eulerAngles;

        static SampledPose Path(AttackPose from, AttackPose[] keys, float k, bool twoHanded, AttackPhase phase)
        {
            float scaled = Mathf.Clamp01(k) * keys.Length;
            int index = Mathf.Min((int)scaled, keys.Length - 1);
            var start = index == 0 ? from : keys[index - 1];
            return Blend(start, keys[index], scaled - index, 1f, twoHanded, phase);
        }

        static SampledPose Blend(AttackPose a, AttackPose b, float k, float weight, bool twoHanded, AttackPhase phase)
        {
            k = Mathf.Clamp01(k);
            return new SampledPose
            {
                arm = Quaternion.Slerp(Quaternion.Euler(a.arm), Quaternion.Euler(b.arm), k),
                wrist = Quaternion.Slerp(Quaternion.Euler(a.wrist), Quaternion.Euler(b.wrist), k),
                torso = Quaternion.Slerp(Quaternion.Euler(a.torso), Quaternion.Euler(b.torso), k),
                backArm = Quaternion.Slerp(Quaternion.Euler(a.backArm), Quaternion.Euler(b.backArm), k),
                armOffset = Vector3.Lerp(a.armOffset, b.armOffset, k),
                body = Vector3.Lerp(a.body, b.body, k),
                spin = Mathf.Lerp(a.spin, b.spin, k),
                armSpin = Mathf.Lerp(a.armSpin, b.armSpin, k),
                elbow = Mathf.Lerp(a.elbow, b.elbow, k),
                backElbow = Mathf.Lerp(a.backElbow, b.backElbow, k),
                kick = Vector4.Lerp(a.kick, b.kick, k),
                weight = Mathf.Clamp01(weight),
                twoHanded = twoHanded,
                phase = phase,
            };
        }

        // Curves are kept gentle (peak speed at most 2x the average) so no frame jumps too far.
        static float EaseOutQuad(float k) { k = Mathf.Clamp01(k); return 1f - (1f - k) * (1f - k); }
        // Accelerates into the hit, ending at 1.6x the average speed.
        static float SwingEase(float k) { k = Mathf.Clamp01(k); return k * (0.4f + 0.6f * k); }
        static float EaseInOut(float k) { k = Mathf.Clamp01(k); return k * k * (3f - 2f * k); }
    }
}
