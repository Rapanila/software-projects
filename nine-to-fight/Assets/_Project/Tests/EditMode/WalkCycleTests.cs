using NineToFight.Characters;
using NUnit.Framework;

namespace NineToFight.Tests
{
    public class WalkCycleTests
    {
        [Test]
        public void RunningCadenceIsAboutThreeStepsPerSecond()
        {
            float steps = CharacterVisual.StepsPerSecond(6f, CharacterVisual.DefaultStrideLength);
            Assert.That(steps, Is.InRange(2.5f, 3.6f));
        }

        [Test]
        public void SlowerMovementTakesFewerSteps()
        {
            float stride = CharacterVisual.DefaultStrideLength;
            Assert.Less(CharacterVisual.StepsPerSecond(2.6f, stride), CharacterVisual.StepsPerSecond(6f, stride));
            Assert.AreEqual(0f, CharacterVisual.StepsPerSecond(0f, stride));
        }
    }
}
