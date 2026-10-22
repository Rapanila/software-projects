using System.Linq;
using NineToFight.Audio;
using NineToFight.Game;
using NUnit.Framework;

namespace NineToFight.Tests
{
    public class TowerPlanTests
    {
        static readonly string[] BaseWorkers = { "Intern", "ITSupport", "Accountant", "HRRep", "MiddleManager" };

        [Test]
        public void FloorOneUsesTheHandBuiltScene()
        {
            var plan = TowerPlan.Build(1);
            Assert.AreEqual("The Cubicle Farm", plan.name);
            Assert.IsEmpty(plan.spawns);
        }

        [Test]
        public void EveryHigherFloorHasCoworkersBuiltFromRealWorkerClasses()
        {
            for (int floor = 2; floor <= TowerPlan.FloorCount; floor++)
            {
                var plan = TowerPlan.Build(floor);
                Assert.IsNotEmpty(plan.spawns, $"floor {floor}");
                Assert.IsNotEmpty(plan.coffee, $"floor {floor} has coffee");
                foreach (var s in plan.spawns)
                {
                    CollectionAssert.Contains(BaseWorkers, s.who.baseId, $"floor {floor}: {s.who.title}");
                    Assert.That(s.position.z, Is.InRange(-2.5f, 2.5f), "spawns inside the depth lane");
                    Assert.That(s.position.x, Is.InRange(TowerPlan.FirstEncounterX, TowerPlan.LastEncounterX + 0.01f));
                }
            }
        }

        [Test]
        public void TowerGetsHarderAndEndsWithTheCeo()
        {
            var plans = Enumerable.Range(1, TowerPlan.FloorCount).Select(f => TowerPlan.Build(f)).ToList();
            for (int i = 1; i < plans.Count; i++)
            {
                Assert.Greater(plans[i].healthScale, plans[i - 1].healthScale);
                Assert.Greater(plans[i].damageScale, plans[i - 1].damageScale);
            }
            Assert.Greater(plans[8].spawns.Count, plans[1].spawns.Count, "more coworkers further up");

            var final = plans.Last();
            Assert.IsTrue(final.IsFinal);
            var ceo = final.spawns.Single(s => s.who.boss == BossRank.FinalBoss);
            Assert.AreEqual("THE CEO", ceo.who.title);
            Assert.AreEqual(1, plans.Count(p => p.spawns.Any(s => s.who.boss == BossRank.FinalBoss)), "only one final boss");
            Assert.IsTrue(plans[2].HasBoss && plans[5].HasBoss && plans[7].HasBoss, "mini-bosses on floors 3, 6 and 8");
        }

        [Test]
        public void SameSeedGivesSameLayout()
        {
            var a = TowerPlan.Build(5, 42);
            var b = TowerPlan.Build(5, 42);
            Assert.AreEqual(a.spawns.Count, b.spawns.Count);
            for (int i = 0; i < a.spawns.Count; i++)
                Assert.AreEqual(a.spawns[i].position, b.spawns[i].position);
        }

        [Test]
        public void TeaserPointsAtTheNextFloor()
        {
            StringAssert.StartsWith("Floor 3: Human Resources", TowerPlan.NextFloorTeaser(2));
            Assert.AreEqual("The corner office is yours.", TowerPlan.NextFloorTeaser(TowerPlan.FloorCount));
        }
    }

    public class ComboTrackerTests
    {
        [Test]
        public void HitsInsideTheWindowChain()
        {
            var combo = new ComboTracker();
            for (int i = 0; i < 5; i++) combo.RegisterHit(i * 0.5f);
            Assert.AreEqual(5, combo.Count);
            Assert.AreEqual("PRODUCTIVE", ComboTracker.Rating(combo.Count));
        }

        [Test]
        public void ChainResetsAfterTheWindowButBestIsKept()
        {
            var combo = new ComboTracker();
            combo.RegisterHit(0f);
            combo.RegisterHit(1f);
            combo.RegisterHit(1f + ComboTracker.BaseWindow + 0.1f);
            Assert.AreEqual(1, combo.Count);
            Assert.AreEqual(2, combo.Best);
        }

        [Test]
        public void ReachingANewRatingIsReported()
        {
            var combo = new ComboTracker();
            bool[] tierUps = Enumerable.Range(0, 10).Select(i => combo.RegisterHit(i * 0.1f)).ToArray();
            Assert.IsTrue(tierUps[4], "5th hit: PRODUCTIVE");
            Assert.IsTrue(tierUps[9], "10th hit: SYNERGY!");
            Assert.AreEqual(2, tierUps.Count(t => t));
        }

        [Test]
        public void XpBonusGrowsWithTheComboAndCapsAtDouble()
        {
            var combo = new ComboTracker();
            Assert.AreEqual(1f, combo.XpMultiplier(0f));
            for (int i = 0; i < 80; i++) combo.RegisterHit(i * 0.1f);
            Assert.AreEqual(2f, combo.XpMultiplier(8f), 0.0001f);
            combo.Break();
            Assert.AreEqual(1f, combo.XpMultiplier(8f));
        }
    }

    public class SynthTests
    {
        [Test]
        public void EveryEffectRendersAudibleSamples()
        {
            foreach (Sfx sfx in System.Enum.GetValues(typeof(Sfx)))
            {
                var clip = Synth.Build(sfx);
                Assert.Greater(clip.samples, 1000, sfx.ToString());
                var data = new float[clip.samples];
                clip.GetData(data, 0);
                Assert.Greater(data.Max(System.Math.Abs), 0.5f, $"{sfx} is audible");
                Assert.LessOrEqual(data.Max(System.Math.Abs), 1f, $"{sfx} doesn't clip");
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void MusicLoopsAreWholeBars()
        {
            var clip = Synth.BuildMusic(Synth.Lounge);
            float bar = 60f / Synth.Lounge.bpm * 4f;
            Assert.AreEqual(bar * Synth.Lounge.chords.Length, clip.length, 0.01f);
            UnityEngine.Object.DestroyImmediate(clip);
        }
    }
}
