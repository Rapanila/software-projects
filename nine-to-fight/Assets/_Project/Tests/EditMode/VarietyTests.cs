using System.Collections.Generic;
using System.Linq;
using NineToFight.Game;
using NUnit.Framework;

namespace NineToFight.Tests
{
    public class PerkTests
    {
        [Test]
        public void OfferIsThreeDistinctPerks()
        {
            var offer = PerkLibrary.Roll(new System.Random(1), 3, new PerkSet());
            Assert.AreEqual(3, offer.Count);
            Assert.AreEqual(3, offer.Select(p => p.id).Distinct().Count());
        }

        [Test]
        public void MaxedPerksAreNoLongerOffered()
        {
            var owned = new PerkSet();
            Assert.IsTrue(owned.Add(PerkId.GoldenParachute));
            Assert.IsFalse(owned.Add(PerkId.GoldenParachute), "one parachute per climb");
            for (int seed = 0; seed < 50; seed++)
                Assert.IsFalse(PerkLibrary.Roll(new System.Random(seed), 3, owned).Any(p => p.id == PerkId.GoldenParachute));
        }

        [Test]
        public void PerksStack()
        {
            var owned = new PerkSet();
            Assert.AreEqual(1f, owned.DamageMultiplier);
            owned.Add(PerkId.StockOptions);
            owned.Add(PerkId.StockOptions);
            Assert.AreEqual(1.3f, owned.DamageMultiplier, 0.0001f);
            owned.Add(PerkId.FlexTime);
            Assert.AreEqual(0.7f, owned.DodgeCooldownMultiplier, 0.0001f);
            owned.Add(PerkId.DentalPlan);
            Assert.AreEqual(25f, owned.MaxHealthBonus);
            Assert.AreEqual(4, owned.Count);
        }
    }

    public class FloorEventTests
    {
        [Test]
        public void BossFloorsAndTheLobbyHaveNoEvent()
        {
            var rng = new System.Random(3);
            foreach (int floor in new[] { 1, 3, 6, 8, 9 })
                Assert.AreEqual(FloorEvent.None, TowerPlan.RollEvent(floor, rng, null), $"floor {floor}");
        }

        [Test]
        public void EventsNeverRepeatInOneClimb()
        {
            for (int seed = 0; seed < 30; seed++)
            {
                var rng = new System.Random(seed);
                var used = new List<FloorEvent>();
                foreach (int floor in new[] { 2, 4, 5, 7 })
                {
                    var e = TowerPlan.RollEvent(floor, rng, used);
                    Assert.AreNotEqual(FloorEvent.None, e);
                    CollectionAssert.DoesNotContain(used, e, $"seed {seed} floor {floor}");
                    used.Add(e);
                }
            }
        }

        [Test]
        public void AllHandsSplitsTheFloorIntoThreeWaves()
        {
            var plan = TowerPlan.Build(4, 7);
            TowerPlan.ApplyEvent(plan, FloorEvent.AllHands, new System.Random(1));
            Assert.AreEqual(3, plan.waveCount);
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, plan.spawns.Select(s => s.wave).Distinct());
        }

        [Test]
        public void CasualFridayBringsMoreButSofterCoworkers()
        {
            var normal = TowerPlan.Build(5, 7);
            var casual = TowerPlan.Build(5, 7);
            TowerPlan.ApplyEvent(casual, FloorEvent.CasualFriday, new System.Random(1));
            Assert.Greater(casual.spawns.Count, normal.spawns.Count);
            Assert.Less(casual.healthScale, normal.healthScale);
            Assert.IsTrue(casual.spawns.All(s => s.position.x <= TowerPlan.LastEncounterX + 0.01f));
        }

        [Test]
        public void FireDrillHasATimerAndReviewWeekDoublesXp()
        {
            var drill = TowerPlan.Build(2, 1);
            TowerPlan.ApplyEvent(drill, FloorEvent.FireDrill, new System.Random(1));
            Assert.Greater(drill.timer, 0f);
            var review = TowerPlan.Build(2, 1);
            float xp = review.xpScale;
            TowerPlan.ApplyEvent(review, FloorEvent.ReviewWeek, new System.Random(1));
            Assert.AreEqual(xp * 2f, review.xpScale, 0.0001f);
        }

        [Test]
        public void EliteCoworkersOnlyAppearHigherUp()
        {
            Assert.IsFalse(Enumerable.Range(0, 20).Any(seed => TowerPlan.Build(2, seed).spawns.Any(s => s.elite)));
            Assert.IsTrue(Enumerable.Range(0, 20).Any(seed => TowerPlan.Build(7, seed).spawns.Any(s => s.elite)));
        }

        [Test]
        public void EveryJobHasAFightingStyle()
        {
            var roles = new Dictionary<string, NineToFight.Enemies.EnemyRole>();
            foreach (var id in new[] { "Intern", "ITSupport", "Accountant", "HRRep", "MiddleManager" })
            {
                var w = UnityEngine.ScriptableObject.CreateInstance<NineToFight.Enemies.WorkerClass>();
                w.name = id + " (Some Title)";
                roles[id] = w.ResolvedRole;
                UnityEngine.Object.DestroyImmediate(w);
            }
            Assert.AreEqual(5, roles.Values.Distinct().Count(), "five jobs, five ways to fight");
        }
    }
}
