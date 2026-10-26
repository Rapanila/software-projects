using NineToFight.Characters;
using NUnit.Framework;
using UnityEngine;

namespace NineToFight.Tests
{
    /// <summary>The spring behind the hit jolt layered on top of the clips.</summary>
    public class SpringTests
    {
        [Test]
        public void UnderdampedSpringOvershootsALittle()
        {
            var s = new SpringRotation();
            s.Step(Quaternion.identity, 6f, 0.45f, 1f / 60f);
            var target = Quaternion.Euler(30f, 0f, 0f);
            float peak = 0f;
            for (int i = 0; i < 120; i++)
            {
                s.Step(target, 6f, 0.45f, 1f / 60f);
                peak = Mathf.Max(peak, Quaternion.Angle(Quaternion.identity, s.value));
            }
            Assert.That(peak / 30f, Is.InRange(1.05f, 1.35f), "a little overshoot reads as weight; too much reads as jelly");
            Assert.Less(Quaternion.Angle(s.value, target), 0.5f, "and it settles on the target");
        }

        [Test]
        public void StiffSpringsStayStableOnLongFrames()
        {
            var s = new SpringRotation();
            s.Step(Quaternion.identity, 26f, 0.85f, 0.05f);
            var target = Quaternion.Euler(0f, 0f, 40f);
            for (int i = 0; i < 40; i++) s.Step(target, 26f, 0.85f, 0.05f);
            Assert.IsFalse(float.IsNaN(s.value.x));
            Assert.Less(Quaternion.Angle(s.value, target), 0.5f);
        }

        [Test]
        public void RotationSpringFollowsLargeTurnsTheShortWay()
        {
            var s = new SpringRotation();
            s.Step(Quaternion.identity, 8f, 0.6f, 1f / 60f);
            var target = Quaternion.Euler(0f, 170f, 0f);
            for (int i = 0; i < 180; i++) s.Step(target, 8f, 0.6f, 1f / 60f);
            Assert.Less(Quaternion.Angle(s.value, target), 0.5f);
        }

        [Test]
        public void KickedRotationWobblesBack()
        {
            var s = new SpringRotation();
            s.Step(Quaternion.identity, 6f, 0.45f, 1f / 60f);
            s.Kick(new Vector3(10f, 0f, 0f));
            float maxAngle = 0f;
            for (int i = 0; i < 120; i++) maxAngle = Mathf.Max(maxAngle, Quaternion.Angle(s.Step(Quaternion.identity, 6f, 0.45f, 1f / 60f), Quaternion.identity));
            Assert.Greater(maxAngle, 5f, "the kick moves it");
            Assert.Less(Quaternion.Angle(s.value, Quaternion.identity), 0.5f, "and it settles");
        }
    }
}
