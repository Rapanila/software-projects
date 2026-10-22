using System.Linq;
using NineToFight.Combat;
using NineToFight.Player;
using NUnit.Framework;
using UnityEditor;

namespace NineToFight.Tests
{
    public class CooldownTests
    {
        [Test]
        public void ReadyUntilTriggered()
        {
            var cd = new Cooldown();
            Assert.IsTrue(cd.IsReady(0f));
            Assert.AreEqual(0f, cd.Fraction(0f));
        }

        [Test]
        public void TracksRemainingTime()
        {
            var cd = new Cooldown();
            cd.Trigger(10f, 4f);
            Assert.IsFalse(cd.IsReady(11f));
            Assert.AreEqual(3f, cd.Remaining(11f), 0.001f);
            Assert.AreEqual(0.75f, cd.Fraction(11f), 0.001f);
            Assert.IsTrue(cd.IsReady(14f));
            Assert.AreEqual(0f, cd.Fraction(20f));
        }

        [Test]
        public void ResetMakesItReadyAgain()
        {
            var cd = new Cooldown();
            cd.Trigger(0f, 10f);
            cd.Reset();
            Assert.IsTrue(cd.IsReady(1f));
        }
    }

    public class ClassAbilityContentTests
    {
        static PlayerClass[] Classes() =>
            AssetDatabase.FindAssets("t:PlayerClass")
                .Select(g => AssetDatabase.LoadAssetAtPath<PlayerClass>(AssetDatabase.GUIDToAssetPath(g)))
                .ToArray();

        [Test]
        public void EveryClassHasAQAbility()
        {
            var classes = Classes();
            Assert.IsNotEmpty(classes);
            foreach (var c in classes)
            {
                Assert.IsFalse(string.IsNullOrEmpty(c.ability.displayName), $"{c.displayName} has no ability");
                Assert.Greater(c.ability.cooldown, 0f, $"{c.displayName} ability needs a cooldown");
            }
        }

        [Test]
        public void EveryMoveInAClassHasItsOwnAnimation()
        {
            // You only ever play one department, so within a class no two moves may look alike.
            foreach (var c in Classes())
            {
                var duplicates = c.BuildMoveSet().GroupBy(m => m.style).Where(g => g.Count() > 1)
                    .Select(g => $"{g.Key}: {string.Join(", ", g.Select(m => m.displayName))}").ToList();
                Assert.IsEmpty(duplicates, $"{c.displayName} has moves sharing an animation style");
            }
        }

        [Test]
        public void EveryClassFillsTheWholeHotbar()
        {
            foreach (var c in Classes())
            {
                var moves = c.BuildMoveSet();
                Assert.AreEqual(NineToFight.Progression.ProgressionState.HotbarSize, moves.Count, c.displayName);
                Assert.AreEqual(moves.Count, moves.Select(m => m.id).Distinct().Count(), $"{c.displayName}: duplicate move ids");
                Assert.AreSame(c.lightAttack, moves[0], "key 1 stays the light attack");
                Assert.AreSame(c.heavyAttack, moves[1], "key 2 stays the heavy attack");
                for (int i = 1; i < moves.Count; i++)
                    Assert.LessOrEqual(moves[i - 1].unlockLevel, moves[i].unlockLevel, $"{c.displayName}: hotbar follows unlock order");
                Assert.LessOrEqual(moves.Max(m => m.unlockLevel), NineToFight.Progression.CareerLadder.MaxLevel, "every move is reachable");
                Assert.IsTrue(moves.Any(m => m.hits > 1), $"{c.displayName} has a multi-hit flurry");
            }
        }

        [Test]
        public void EveryClassMoveBookEntryMatchesAClassAsset()
        {
            var ids = Classes().Select(c => c.name).ToList();
            foreach (var id in ClassMoveBook.ClassIds) CollectionAssert.Contains(ids, id);
        }

        [Test]
        public void EachClassAbilityDoesSomethingDifferent()
        {
            var kinds = Classes().Select(c => c.ability.kind).ToList();
            Assert.AreEqual(kinds.Count, kinds.Distinct().Count());
        }
    }
}
