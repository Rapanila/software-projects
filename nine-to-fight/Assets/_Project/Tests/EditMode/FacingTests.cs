using NineToFight.Characters;
using NUnit.Framework;
using UnityEngine;

namespace NineToFight.Tests
{
    public class FacingTests
    {
        [Test]
        public void MovingIntoTheScreenFacesTheBackWall() =>
            Assert.AreEqual(0f, CharacterMotor2_5D.YawFromDirection(Vector3.forward), 0.01f);

        [Test]
        public void MovingTowardTheCameraFacesTheScreen() =>
            Assert.AreEqual(180f, Mathf.Abs(CharacterMotor2_5D.YawFromDirection(Vector3.back)), 0.01f);

        [Test]
        public void MovingSidewaysFacesLeftOrRight()
        {
            Assert.AreEqual(90f, CharacterMotor2_5D.YawFromDirection(Vector3.right), 0.01f);
            Assert.AreEqual(-90f, CharacterMotor2_5D.YawFromDirection(Vector3.left), 0.01f);
        }

        [Test]
        public void DiagonalsFaceInBetween() =>
            Assert.AreEqual(45f, CharacterMotor2_5D.YawFromDirection(new Vector3(1f, 0f, 1f)), 0.01f);

        [Test]
        public void InputToDirectionIgnoresTinyInput()
        {
            Assert.IsFalse(CharacterMotor2_5D.TryInputDirection(new Vector2(0.05f, 0.05f), out _));
            Assert.IsTrue(CharacterMotor2_5D.TryInputDirection(new Vector2(0f, 1f), out var dir));
            Assert.AreEqual(Vector3.forward, dir);
        }
    }
}
