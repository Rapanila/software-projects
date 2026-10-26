using System.Collections.Generic;
using System.Linq;
using NineToFight.Characters;
using NineToFight.Combat;
using NineToFight.EditorTools;
using NineToFight.Enemies;
using NineToFight.Game;
using NineToFight.Progression;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace NineToFight.Tests
{
    /// <summary>Pacing and content rules: the XP curve against the tower, boss scaling, thrown moves, loops and outfits.</summary>
    public class BalanceAndContentTests
    {
        static int TotalXpToCeo() => Enumerable.Range(1, CareerLadder.MaxLevel - 1).Sum(CareerLadder.XpToNextLevel);

        /// <summary>Knockout XP a floor is worth (no combo bonus, no perks), from the real plans and worker assets.</summary>
        static float FloorXp(int floor)
        {
            var workers = AssetDatabase.FindAssets("t:WorkerClass")
                .Select(g => AssetDatabase.LoadAssetAtPath<WorkerClass>(AssetDatabase.GUIDToAssetPath(g)))
                .ToDictionary(w => w.name);
            if (floor == 1) return workers.Values.Where(w => w.tier < 3).Sum(w => w.xpReward) + workers["MiddleManager"].xpReward * 3f;
            var plan = TowerPlan.Build(floor);
            float total = 0f;
            foreach (var s in plan.spawns)
            {
                float xp = workers[s.who.baseId].xpReward * plan.xpScale;
                if (s.who.boss == BossRank.MiniBoss) xp *= 3f;
                if (s.who.boss == BossRank.FinalBoss) xp *= 5f;
                if (s.elite) xp *= 2f;
                total += xp;
            }
            return total;
        }

        [Test]
        public void TheCareerLadderLastsTheWholeTower()
        {
            int ceo = TotalXpToCeo();
            Assert.AreEqual(100, CareerLadder.XpToNextLevel(1));
            Assert.AreEqual(6840, ceo);
            float cumulative = 0f;
            var byFloor = new List<float>();
            for (int floor = 1; floor <= 9; floor++) byFloor.Add(cumulative += FloorXp(floor));
            // Before: 1,440 XP to CEO, done during floor 3. Now the top rank waits until the last floors.
            Assert.Less(byFloor[5], ceo, "not CEO by the end of floor 6, even without combos");
            Assert.GreaterOrEqual(byFloor[8], ceo, "but reachable by the end of the tower");
        }

        [Test]
        public void MiniBossesAreScaledWhereverTheyComeFrom()
        {
            var source = AssetDatabase.LoadAssetAtPath<WorkerClass>("Assets/_Project/Data/WorkerClasses/MiddleManager.asset");
            var w = Object.Instantiate(source);
            try
            {
                TowerDirector.ApplyBossRank(w, BossRank.MiniBoss);
                Assert.AreEqual(source.maxHealth * 2.2f, w.maxHealth, 1e-3f);
                Assert.AreEqual(source.scale * 1.12f, w.scale, 1e-4f);
                Assert.GreaterOrEqual(w.poise, 0.7f);
                Assert.AreEqual(source.xpReward * 3, w.xpReward);
                Assert.AreEqual(source.maxHealth, AssetDatabase.LoadAssetAtPath<WorkerClass>("Assets/_Project/Data/WorkerClasses/MiddleManager.asset").maxHealth,
                    "the shared asset itself is never scaled");
            }
            finally { Object.DestroyImmediate(w); }
        }

        [Test]
        public void ThrownMovesAreActuallyThrown()
        {
            var wellness = ClassMoveBook.For("HRRep").First(m => m.id == "wellness_check");
            Assert.IsTrue(wellness.IsThrown);
            Assert.IsFalse(wellness.pierce, "one mug, one target");
            Assert.IsTrue(MoveLibrary.ReplyAll.IsThrown);
            Assert.IsTrue(MoveLibrary.ReplyAll.pierce, "Reply All reaches everyone in the lane");
            Assert.IsFalse(MoveLibrary.CoffeeSplash.IsThrown, "a splash is close range");
        }

        [Test]
        public void EnemyItemOverridesUseRealProps()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<PropCatalog>("Assets/_Project/Data/PropCatalog.asset");
            var props = new HashSet<string>();
            for (int floor = 2; floor <= TowerPlan.FloorCount; floor++)
                foreach (var s in TowerPlan.Build(floor).spawns.Where(s => !string.IsNullOrEmpty(s.who.prop)))
                {
                    Assert.IsNotNull(catalog.Get(s.who.prop), s.who.title + " fights with " + s.who.prop);
                    props.Add(s.who.prop);
                }
            // Items that no worker class carries now turn up on the floors that suit them.
            foreach (var item in new[] { "golf_club", "desk_phone", "smartphone", "stapler", "briefcase" })
                Assert.Contains(item, props.ToList(), item + " appears on some floor");
        }

        [Test]
        public void ClosingALoopMakesTheLastKeyMatchTheFirst()
        {
            var curve = new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(0.5f, 0.6f), new Keyframe(1f, 0.1f),
                new Keyframe(1.5f, 0.35f), new Keyframe(2f, 0.5f));
            StarterContentBuilder.CloseLoop(curve, 4);
            Assert.AreEqual(curve.keys[0].value, curve.keys[curve.length - 1].value, 1e-5f);
            Assert.AreEqual(0.2f, curve.keys[0].value, 1e-5f, "the start isn't touched");
        }

        [Test]
        public void LocomotionCyclesLoopWithoutAPop()
        {
            var set = AssetDatabase.LoadAssetAtPath<FightAnimationSet>("Assets/_Project/Data/FightAnimations.asset");
            var bad = new List<string>();
            foreach (var clip in new[] { set.relaxedIdle, set.walk, set.run })
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (binding.propertyName.StartsWith("RootT.x") || binding.propertyName.StartsWith("RootT.z") || binding.propertyName.StartsWith("RootQ")) continue;
                var keys = AnimationUtility.GetEditorCurve(clip, binding).keys;
                if (keys.Length > 2 && Mathf.Abs(keys[0].value - keys[keys.Length - 1].value) > 1e-3f)
                    bad.Add($"{clip.name} {binding.propertyName}: {keys[0].value:0.0000} -> {keys[keys.Length - 1].value:0.0000}");
            }
            Assert.IsEmpty(bad, string.Join("\n", bad.Take(20)));
        }

        [Test]
        public void TheSuitBlouseTakesTheShirtColourAndTheBlazerTheAccent()
        {
            foreach (var name in new[] { "Player", "Coworker" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Prefabs/{name}.prefab");
                var so = new SerializedObject(prefab.GetComponentInChildren<CharacterVisual>(true));
                string[] Meshes(string field)
                {
                    var p = so.FindProperty(field);
                    return Enumerable.Range(0, p.arraySize)
                        .Select(i => p.GetArrayElementAtIndex(i).objectReferenceValue as SkinnedMeshRenderer)
                        .Where(r => r != null && r.sharedMesh != null).Select(r => r.sharedMesh.name).ToArray();
                }
                var shirts = Meshes("shirtRenderers");
                var accents = Meshes("accentRenderers");
                CollectionAssert.Contains(shirts, "Woman_Suit_Body_White", name + ": the blouse wears the department colour");
                CollectionAssert.DoesNotContain(shirts, "Woman_Suit", name + ": the blazer isn't painted the shirt colour");
                CollectionAssert.Contains(accents, "Woman_Suit", name + ": the blazer takes the accent colour");
            }
        }
    }
}
