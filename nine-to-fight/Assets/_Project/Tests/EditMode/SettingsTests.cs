using System.Collections.Generic;
using NineToFight.Game;
using NUnit.Framework;

namespace NineToFight.Tests
{
    public class SettingsTests
    {
        readonly Dictionary<GameSettings.Id, int> saved = new Dictionary<GameSettings.Id, int>();

        [SetUp]
        public void Remember()
        {
            saved.Clear();
            foreach (var id in GameSettings.All) saved[id] = GameSettings.Get(id);
        }

        [TearDown]
        public void Restore()
        {
            // Step each setting back to where the player had it.
            foreach (var kv in saved)
                for (int guard = 0; guard < 12 && GameSettings.Get(kv.Key) != kv.Value; guard++)
                    GameSettings.Step(kv.Key, GameSettings.Get(kv.Key) < kv.Value ? 1 : -1);
        }

        [Test]
        public void VolumesStepInTensAndClamp()
        {
            for (int i = 0; i < 15; i++) GameSettings.Step(GameSettings.Id.MusicVolume, 1);
            Assert.AreEqual(1f, GameSettings.MusicVolume);
            Assert.AreEqual("100%", GameSettings.ValueText(GameSettings.Id.MusicVolume));
            for (int i = 0; i < 15; i++) GameSettings.Step(GameSettings.Id.MusicVolume, -1);
            Assert.AreEqual(0f, GameSettings.MusicVolume);
            Assert.AreEqual("Off", GameSettings.ValueText(GameSettings.Id.MusicVolume));
        }

        [Test]
        public void TogglesFlipEitherWay()
        {
            bool before = GameSettings.DamageNumbers;
            GameSettings.Step(GameSettings.Id.DamageNumbers, -1);
            Assert.AreNotEqual(before, GameSettings.DamageNumbers);
            GameSettings.Step(GameSettings.Id.DamageNumbers, 1);
            Assert.AreEqual(before, GameSettings.DamageNumbers);
        }

        [Test]
        public void ScreenShakeHasThreeLevels()
        {
            for (int i = 0; i < 3; i++) GameSettings.Step(GameSettings.Id.ScreenShake, -1);
            Assert.AreEqual(0f, GameSettings.ScreenShake);
            GameSettings.Step(GameSettings.Id.ScreenShake, 1);
            Assert.AreEqual("Reduced", GameSettings.ValueText(GameSettings.Id.ScreenShake));
            GameSettings.Step(GameSettings.Id.ScreenShake, 1);
            Assert.AreEqual(1f, GameSettings.ScreenShake);
        }

        [Test]
        public void EverySettingHasALabelAndValue()
        {
            foreach (var id in GameSettings.All)
            {
                Assert.IsNotEmpty(GameSettings.Label(id));
                Assert.IsNotEmpty(GameSettings.ValueText(id));
            }
        }
    }
}
