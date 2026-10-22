using UnityEngine;

namespace NineToFight.Characters
{
    /// <summary>
    /// The fighting stance as a living pose rather than a held one. The ready guard is bladed (pelvis
    /// turned, lead side forward), the weapon hand cocked by the shoulder with the item tipped at the
    /// opponent and the lead fist out front; it bounces through the knees, rocks its weight, weaves, and
    /// every few seconds fidgets (a feint, a shuffle, a shoulder roll, a beckon, a weapon twirl).
    /// The block is a high, hunched cover with both forearms in front of the face.
    /// Each fighter gets their own tempo and timing so a crowd never moves in step.
    /// </summary>
    public class FightStance
    {
        /// <summary>Stance pose in the rig's terms (arms hang straight down at identity).</summary>
        public struct Pose
        {
            public Quaternion weaponArm, leadArm, wrist;
            public float weaponElbow, leadElbow;
            /// <summary>Chest over pelvis in degrees: x leans forward, y turns the weapon shoulder back, z rolls.</summary>
            public Vector3 torso;
            /// <summary>Whole-body turn in degrees (the blade of the stance).</summary>
            public float pelvisYaw;
            /// <summary>Added to the head after it counters the body: x tucks the chin, y turns, z tilts.</summary>
            public Vector3 head;
            /// <summary>How far the hips sink, in metres (the knees bend to match).</summary>
            public float drop;
            /// <summary>Weight shift in metres: x side to side, z rocking forward and back (feet stay planted).</summary>
            public Vector3 sway;
            /// <summary>A shuffle step forward, in metres (the lead foot steps).</summary>
            public float step;
        }

        public enum Fidget { None, Feint, Shuffle, ShoulderRoll, Beckon, Twirl }

        readonly System.Random rng;
        readonly float tempo, phase, noise;
        float fidgetStart, fidgetLength, nextFidget;

        public Fidget Current { get; private set; }

        public FightStance(int seed)
        {
            rng = new System.Random(seed);
            tempo = 1.5f + (float)rng.NextDouble() * 0.35f;
            phase = (float)rng.NextDouble() * 10f;
            noise = (float)rng.NextDouble() * 100f;
            nextFidget = 2f + (float)rng.NextDouble() * 4f;
        }

        /// <summary>
        /// The stance at time t. <paramref name="still"/> scales the bounce and weave (0 while walking or
        /// airborne), <paramref name="block"/> blends into the high cover, and fidgets only play while allowed.
        /// </summary>
        public Pose Sample(float t, float still, float block, bool allowFidgets)
        {
            var ready = Ready(t, still);
            UpdateFidget(ref ready, t, allowFidgets && block < 0.05f);
            return block <= 0f ? ready : Lerp(ready, Block(t, still), block);
        }

        /// <summary>Starts a fidget now (for previews and tests).</summary>
        public void Play(Fidget fidget, float t)
        {
            Current = fidget;
            fidgetStart = t;
            fidgetLength = Length(fidget);
        }

        Pose Ready(float t, float still)
        {
            float beat = (t + phase) * tempo * 2f * Mathf.PI;
            float hop = 0.5f - 0.5f * Mathf.Cos(beat);       // 0 up on the toes, 1 sunk into the knees
            float rock = Mathf.Sin(beat * 0.5f);              // weight forward / back, every other bounce
            float hands = Mathf.Sin(beat - 0.7f);             // the guard bobs a little behind the legs
            float weave = Mathf.PerlinNoise(t * 0.3f, noise) * 2f - 1f;
            float turn = Mathf.PerlinNoise(noise, t * 0.25f) * 2f - 1f;
            float circle = (t + phase) * 2.4f;                // the weapon tip draws small circles

            return new Pose
            {
                pelvisYaw = 20f + 5f * turn * still,
                torso = new Vector3(7f + 2.5f * rock * still, 10f + 3f * turn * still, 3.5f * weave * still),
                head = new Vector3(7f - 1.5f * rock * still, 0f, -2.5f * weave * still),
                drop = 0.03f + 0.024f * hop * still,
                sway = new Vector3(0.03f * weave * still, 0f, 0.025f * rock * still),
                // Rear fist held at shoulder height out in front, the item pointed up at the opponent.
                weaponArm = Quaternion.Euler(-40f + 3f * hands * still, -16f, 12f),
                weaponElbow = 85f + 4f * hands * still,
                wrist = Quaternion.Euler(8f + 7f * Mathf.Sin(circle) * still, 9f * Mathf.Cos(circle) * still, 0f),
                leadArm = Quaternion.Euler(-62f - 6f * rock * still + 2f * hands * still, -22f, -6f),
                leadElbow = 86f - 8f * rock * still,
            };
        }

        Pose Block(float t, float still)
        {
            // Hunched behind both forearms, item held up like a shield, peeking over the top.
            float beat = (t + phase) * tempo * 2f * Mathf.PI;
            float settle = Mathf.Sin(beat * 0.5f) * still;
            return new Pose
            {
                pelvisYaw = 10f,
                torso = new Vector3(15f + 1.5f * settle, 5f, 0f),
                head = new Vector3(16f, 0f, 0f),
                drop = 0.08f + 0.006f * (0.5f - 0.5f * Mathf.Cos(beat)) * still,
                sway = new Vector3(0f, 0f, -0.02f),
                weaponArm = Quaternion.Euler(-66f + settle, -30f, 10f),
                weaponElbow = 140f,
                // The item stands upright in front of the face like a shield.
                wrist = Quaternion.Euler(26f, 0f, 0f),
                leadArm = Quaternion.Euler(-70f + settle, 28f, -8f),
                leadElbow = 142f,
            };
        }

        // ------------------------------------------------------------------ fidgets

        static float Length(Fidget fidget)
        {
            switch (fidget)
            {
                case Fidget.Feint: return 0.42f;
                case Fidget.Shuffle: return 0.6f;
                case Fidget.ShoulderRoll: return 1f;
                case Fidget.Beckon: return 1.1f;
                case Fidget.Twirl: return 0.75f;
                default: return 0f;
            }
        }

        Fidget Pick()
        {
            double roll = rng.NextDouble();
            return roll < 0.3 ? Fidget.Feint : roll < 0.55 ? Fidget.Shuffle : roll < 0.75 ? Fidget.Twirl :
                roll < 0.9 ? Fidget.ShoulderRoll : Fidget.Beckon;
        }

        void UpdateFidget(ref Pose p, float t, bool allowed)
        {
            if (!allowed)
            {
                Current = Fidget.None;
                nextFidget = Mathf.Max(nextFidget, t + 1.2f);
                return;
            }
            if (Current == Fidget.None)
            {
                if (t < nextFidget) return;
                Play(Pick(), t);
            }

            float u = (t - fidgetStart) / fidgetLength;
            if (u >= 1f || u < 0f)
            {
                Current = Fidget.None;
                nextFidget = t + 2.5f + (float)rng.NextDouble() * 3.5f;
                return;
            }
            float arc = Mathf.Sin(u * Mathf.PI);
            switch (Current)
            {
                case Fidget.Feint:
                {
                    // A quick half-jab with the lead hand and a shoulder dip, snapped back.
                    float e = u < 0.3f ? EaseOut(u / 0.3f) : 1f - EaseInOut((u - 0.3f) / 0.7f);
                    p.leadArm = Quaternion.Slerp(p.leadArm, Quaternion.Euler(-86f, -26f, -4f), e);
                    p.leadElbow = Mathf.Lerp(p.leadElbow, 14f, e);
                    p.torso.y -= 9f * e;
                    p.pelvisYaw -= 5f * e;
                    p.sway.z += 0.035f * e;
                    p.head.x += 3f * e;
                    break;
                }
                case Fidget.Shuffle:
                    // In and back out again: the lead foot steps, the trail foot drags after it.
                    p.step += 0.24f * arc;
                    p.torso.x += 3f * arc;
                    break;
                case Fidget.ShoulderRoll:
                {
                    // Loosen up: roll the shoulders, tilt the neck, chin up.
                    float roll = Mathf.Sin(u * 2f * Mathf.PI) * arc;
                    p.torso.z += 10f * roll;
                    p.head.z -= 16f * roll;
                    p.head.x -= 5f * arc;
                    p.weaponElbow -= 12f * arc;
                    p.leadElbow -= 10f * arc;
                    break;
                }
                case Fidget.Beckon:
                    // "Come on then": the lead hand curls in twice, chin up.
                    p.leadArm = Quaternion.Slerp(p.leadArm, Quaternion.Euler(-72f, -18f, -12f), arc);
                    p.leadElbow = Mathf.Lerp(p.leadElbow, 55f + 55f * Mathf.Abs(Mathf.Sin(u * 3f * Mathf.PI)), Mathf.Sqrt(arc));
                    p.head.x -= 7f * arc;
                    p.torso.x -= 3f * arc;
                    break;
                case Fidget.Twirl:
                    // Flip the item end over end and catch it.
                    p.wrist = Quaternion.Euler(-360f * EaseInOut(u), 0f, 0f) * p.wrist;
                    p.weaponElbow -= 18f * arc;
                    break;
            }
        }

        // ------------------------------------------------------------------ helpers

        static Pose Lerp(Pose a, Pose b, float k)
        {
            return new Pose
            {
                weaponArm = Quaternion.Slerp(a.weaponArm, b.weaponArm, k),
                leadArm = Quaternion.Slerp(a.leadArm, b.leadArm, k),
                wrist = Quaternion.Slerp(a.wrist, b.wrist, k),
                weaponElbow = Mathf.Lerp(a.weaponElbow, b.weaponElbow, k),
                leadElbow = Mathf.Lerp(a.leadElbow, b.leadElbow, k),
                torso = Vector3.Lerp(a.torso, b.torso, k),
                pelvisYaw = Mathf.Lerp(a.pelvisYaw, b.pelvisYaw, k),
                head = Vector3.Lerp(a.head, b.head, k),
                drop = Mathf.Lerp(a.drop, b.drop, k),
                sway = Vector3.Lerp(a.sway, b.sway, k),
                step = Mathf.Lerp(a.step, b.step, k),
            };
        }

        static float EaseOut(float k) { k = Mathf.Clamp01(k); return 1f - (1f - k) * (1f - k); }
        static float EaseInOut(float k) { k = Mathf.Clamp01(k); return k * k * (3f - 2f * k); }
    }
}
