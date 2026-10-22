using System;
using System.Linq;
using NineToFight.Characters;
using NineToFight.Combat;
using NUnit.Framework;
using UnityEngine;

namespace NineToFight.Tests
{
    public class AttackPoseTests
    {
        const float Startup = 0.3f, Active = 0.15f, Recovery = 0.3f;

        static Vector3 Dir(Vector3 armEuler) => AttackPoses.ArmDirection(Quaternion.Euler(armEuler));

        static AttackStyle[] AllStyles => (AttackStyle[])Enum.GetValues(typeof(AttackStyle));

        [Test]
        public void EveryStyleHasItsOwnStrike()
        {
            var strikes = AllStyles.Select(s => AttackPoses.For(s).Strike).ToList();
            for (int i = 0; i < strikes.Count; i++)
            for (int j = i + 1; j < strikes.Count; j++)
                Assert.IsFalse(strikes[i].Equals(strikes[j]) && AttackPoses.For(AllStyles[i]).Windup.Equals(AttackPoses.For(AllStyles[j]).Windup),
                    $"{AllStyles[i]} and {AllStyles[j]} animate identically");
        }

        [Test]
        public void ChopRaisesOverheadThenComesDownForward()
        {
            var c = AttackPoses.For(AttackStyle.Chop);
            Assert.Greater(Dir(c.Windup.arm).y, 0.5f, "wind-up arm is raised");
            Assert.Greater(Dir(c.Strike.arm).z, 0.5f, "strike lands in front");
            Assert.Less(Dir(c.Strike.arm).y, 0f, "strike comes downward");
        }

        [Test]
        public void TossStartsLowBehindAndFlingsForwardUp()
        {
            var c = AttackPoses.For(AttackStyle.Toss);
            Assert.Less(Dir(c.Windup.arm).z, 0f, "wind-up is behind the body");
            Assert.Greater(Dir(c.Strike.arm).z, 0f);
            Assert.Greater(Dir(c.Strike.arm).y, 0f, "fling ends upward");
        }

        [Test]
        public void JabPullsBackThenThrustsStraightOut()
        {
            var c = AttackPoses.For(AttackStyle.Jab);
            Assert.Greater(c.Windup.torso.y, 20f, "punching shoulder twists back");
            Assert.Less(c.Strike.torso.y, -20f, "then drives forward");
            Assert.Greater(c.Strike.body.z, 0.05f, "with a step in");
            Assert.Greater(Dir(c.Strike.arm).z, 0.9f, "arm is horizontal and forward");
        }

        [Test]
        public void ShoulderStaysAttached()
        {
            // Reach comes from twisting, leaning and stepping; the shoulder joint itself barely moves.
            const float maxShoulderShift = 0.1f;
            foreach (var style in AllStyles)
            {
                var c = AttackPoses.For(style);
                var keys = c.windup.Concat(c.strike).Append(c.follow);
                foreach (var key in keys)
                    Assert.LessOrEqual(key.armOffset.magnitude, maxShoulderShift + 0.0001f,
                        $"{style}: shoulder moves {key.armOffset.magnitude:0.00}m");
            }
        }

        [Test]
        public void SweepCrossesTheBodySideToSide()
        {
            var c = AttackPoses.For(AttackStyle.Sweep);
            Assert.Greater(Dir(c.Windup.arm).x, 0.6f, "starts out to the side");
            Assert.Less(Dir(c.Strike.arm).x, 0f, "ends across the body");
        }

        [Test]
        public void TwoHandedStylesUseBothArms()
        {
            Assert.IsTrue(AttackPoses.For(AttackStyle.Slam).twoHanded);
            Assert.IsTrue(AttackPoses.For(AttackStyle.Golf).twoHanded);
            Assert.IsFalse(AttackPoses.For(AttackStyle.Jab).twoHanded);
        }

        [Test]
        public void SampleWalksThroughThePhases()
        {
            foreach (var style in AllStyles)
            {
                Assert.AreEqual(AttackPhase.Windup, AttackPoses.Sample(style, 0.1f, Startup, Active, Recovery).phase);
                Assert.AreEqual(AttackPhase.Strike, AttackPoses.Sample(style, AttackPoses.ImpactTime(Startup, Active) - 0.01f, Startup, Active, Recovery).phase);
                Assert.AreEqual(AttackPhase.FollowThrough, AttackPoses.Sample(style, Startup + Active - 0.01f, Startup, Active, Recovery).phase);
                Assert.AreEqual(AttackPhase.Recovery, AttackPoses.Sample(style, Startup + Active + 0.1f, Startup, Active, Recovery).phase);
                Assert.AreEqual(AttackPhase.None, AttackPoses.Sample(style, Startup + Active + Recovery + 0.01f, Startup, Active, Recovery).phase);
            }
        }

        [Test]
        public void ReachesTheStrikePoseInsideTheHitWindow()
        {
            var c = AttackPoses.For(AttackStyle.Chop);
            var sampled = AttackPoses.Sample(AttackStyle.Chop, AttackPoses.ImpactTime(Startup, Active), Startup, Active, Recovery);
            Assert.Less(Quaternion.Angle(sampled.arm, Quaternion.Euler(c.Strike.arm)), 2f);
        }

        [Test]
        public void EasesInFromTheStanceAndHandsBackToIt()
        {
            foreach (var style in AllStyles)
            {
                var start = AttackPoses.Sample(style, 0f, Startup, Active, Recovery);
                Assert.Less(start.weight, 0.01f, $"{style}: starts from the stance");
                Assert.AreEqual(1f, AttackPoses.Sample(style, AttackPoses.ImpactTime(Startup, Active), Startup, Active, Recovery).weight, 0.001f,
                    $"{style}: fully committed at impact");
                var end = AttackPoses.Sample(style, Startup + Active + Recovery - 0.0001f, Startup, Active, Recovery);
                Assert.Less(end.weight, 0.01f, $"{style}: back to the stance by the end");
            }
        }

        [Test]
        public void ElbowsChamberThenSnapStraight()
        {
            foreach (var style in AllStyles)
            {
                var c = AttackPoses.For(style);
                Assert.IsFalse(float.IsNaN(c.Windup.elbow) || float.IsNaN(c.Strike.elbow) || float.IsNaN(c.follow.backElbow), style.ToString());
            }
            Assert.Greater(AttackPoses.For(AttackStyle.Jab).Windup.elbow, 90f, "jab chambers the fist by the chin");
            Assert.Less(AttackPoses.For(AttackStyle.Jab).Strike.elbow, 10f, "and punches out straight");
            Assert.Greater(AttackPoses.For(AttackStyle.Hook).Strike.elbow, 70f, "a hook lands with the arm bent");
        }

        [Test]
        public void WindupKeepsCoilingUntilTheRelease()
        {
            // Between reaching the wind-up and letting go, the torso keeps twisting away from the strike.
            float impact = AttackPoses.ImpactTime(Startup, Active);
            var early = AttackPoses.Sample(AttackStyle.Jab, impact * 0.45f, Startup, Active, Recovery);
            var late = AttackPoses.Sample(AttackStyle.Jab, impact - 0.07f, Startup, Active, Recovery);
            Assert.AreEqual(AttackPhase.Windup, late.phase);
            Assert.Greater(Quaternion.Angle(late.torso, Quaternion.Euler(AttackPoses.For(AttackStyle.Jab).Strike.torso)),
                Quaternion.Angle(early.torso, Quaternion.Euler(AttackPoses.For(AttackStyle.Jab).Strike.torso)));
        }

        [Test]
        public void SpinTurnsAllTheWayAroundIntoTheHit()
        {
            var c = AttackPoses.For(AttackStyle.Spin);
            Assert.GreaterOrEqual(c.Strike.spin, 359f, "lands the hit after a full turn");
            Assert.Less(c.windup[0].spin, 0f, "coils the other way first");
        }

        [Test]
        public void SpinRecoveryDoesNotUnwind()
        {
            var mid = AttackPoses.Sample(AttackStyle.Spin, Startup + Active + Recovery * 0.5f, Startup, Active, Recovery);
            Assert.AreEqual(360f, mid.spin, 0.01f, "stays a full turn around instead of spinning back");
        }

        [Test]
        public void WindmillLoopsTheArmBeforeReleasing()
        {
            var c = AttackPoses.For(AttackStyle.Windmill);
            Assert.LessOrEqual(c.Windup.armSpin, -360f, "at least one full arm loop");
            Assert.Greater(c.windup.Length, 1);
        }

        [Test]
        public void StampHopsThenCrouches()
        {
            var c = AttackPoses.For(AttackStyle.Stamp);
            Assert.Greater(c.Windup.body.y, 0.2f, "hops up");
            Assert.Less(c.Strike.body.y, -0.1f, "crouches into the stamp");
            Assert.Less(Dir(c.Strike.arm).y, -0.5f, "stamps downward");
        }

        [Test]
        public void LungeAndShoveStepForward()
        {
            Assert.Greater(AttackPoses.For(AttackStyle.Lunge).Strike.body.z, 0.25f);
            Assert.Greater(AttackPoses.For(AttackStyle.Shove).Strike.body.z, 0.15f);
            Assert.IsTrue(AttackPoses.For(AttackStyle.Shove).twoHanded);
        }

        [Test]
        public void BackhandMirrorsTheSweep()
        {
            var c = AttackPoses.For(AttackStyle.Backhand);
            Assert.Less(Dir(c.Windup.arm).x, -0.5f, "starts across the body");
            Assert.Greater(Dir(c.Strike.arm).x, 0.3f, "slaps outward");
        }

        [Test]
        public void DiagonalSlashCutsAcrossNotStraightDown()
        {
            var c = AttackPoses.For(AttackStyle.DiagonalSlash);
            Assert.Greater(Dir(c.Windup.arm).x, 0.3f, "starts high and outside");
            Assert.Greater(Dir(c.Windup.arm).y, 0.3f);
            Assert.Less(Dir(c.Strike.arm).x, -0.2f, "finishes across the body");
        }

        [Test]
        public void ZeroLengthPhasesDoNotBreak()
        {
            var p = AttackPoses.Sample(AttackStyle.Jab, 0f, 0f, 0.1f, 0f);
            Assert.IsFalse(float.IsNaN(p.arm.x));
        }
    }
}
