using System.Collections.Generic;
using NineToFight.Combat;
using NineToFight.Progression;
using NUnit.Framework;

namespace NineToFight.Tests
{
    public class CareerLadderTests
    {
        [Test]
        public void StartsAsInternAndEndsAsCeo()
        {
            Assert.AreEqual("Intern", CareerLadder.RankTitle(1));
            Assert.AreEqual("CEO", CareerLadder.RankTitle(CareerLadder.MaxLevel));
        }

        [Test]
        public void RankTitleClampsOutOfRangeLevels()
        {
            Assert.AreEqual("Intern", CareerLadder.RankTitle(0));
            Assert.AreEqual("CEO", CareerLadder.RankTitle(99));
        }

        [Test]
        public void XpRequirementGrowsEachLevel()
        {
            for (int level = 1; level < CareerLadder.MaxLevel - 1; level++)
                Assert.Less(CareerLadder.XpToNextLevel(level), CareerLadder.XpToNextLevel(level + 1));
        }
    }

    public class ProgressionStateTests
    {
        [Test]
        public void NewHireIsLevelOneWithNoXp()
        {
            var state = new ProgressionState();
            Assert.AreEqual(1, state.Level);
            Assert.AreEqual(0, state.Xp);
        }

        [Test]
        public void EnoughXpLevelsUpAndCarriesRemainder()
        {
            var state = new ProgressionState();
            int needed = CareerLadder.XpToNextLevel(1);

            int gained = state.AddXp(needed + 5);

            Assert.AreEqual(1, gained);
            Assert.AreEqual(2, state.Level);
            Assert.AreEqual(5, state.Xp);
        }

        [Test]
        public void LargeXpGainCanSkipSeveralRanks()
        {
            var state = new ProgressionState();
            int total = CareerLadder.XpToNextLevel(1) + CareerLadder.XpToNextLevel(2) + CareerLadder.XpToNextLevel(3);

            Assert.AreEqual(3, state.AddXp(total));
            Assert.AreEqual(4, state.Level);
        }

        [Test]
        public void XpStopsAtCeo()
        {
            var state = new ProgressionState();
            state.AddXp(1000000);
            Assert.AreEqual(CareerLadder.MaxLevel, state.Level);
            Assert.AreEqual(0, state.Xp);
            Assert.IsTrue(state.IsMaxLevel);
        }

        [Test]
        public void LevelUpUnlocksMovesForThatLevel()
        {
            var state = new ProgressionState();
            var unlocked = new List<FightingMove>();
            state.MoveUnlocked += unlocked.Add;

            Assert.IsFalse(state.IsUnlocked(MoveLibrary.CoffeeSplash));
            state.AddXp(CareerLadder.XpToNextLevel(1));

            Assert.IsTrue(state.IsUnlocked(MoveLibrary.CoffeeSplash));
            CollectionAssert.Contains(unlocked, MoveLibrary.CoffeeSplash);
        }

        [Test]
        public void TrainingCanUnlockAMoveEarly()
        {
            var state = new ProgressionState();
            Assert.IsTrue(state.UnlockMove(MoveLibrary.KeyboardSmash.id));
            Assert.IsTrue(state.IsUnlocked(MoveLibrary.KeyboardSmash));
            Assert.IsFalse(state.UnlockMove(MoveLibrary.KeyboardSmash.id), "unlocking twice reports no change");
        }

        [Test]
        public void BestMovePrefersHighestUnlockedTier()
        {
            var basic = new FightingMove { id = "basic", slot = MoveSlot.Special, unlockLevel = 2 };
            var advanced = new FightingMove { id = "advanced", slot = MoveSlot.Special, unlockLevel = 3 };
            var state = new ProgressionState(new[] { basic, advanced });
            Assert.IsNull(state.BestMove(MoveSlot.Special));
            Assert.AreSame(basic, state.NextLocked(MoveSlot.Special));

            state.AddXp(CareerLadder.XpToNextLevel(1));
            Assert.AreSame(basic, state.BestMove(MoveSlot.Special));

            state.UnlockMove("advanced");
            Assert.AreSame(advanced, state.BestMove(MoveSlot.Special));
            Assert.IsNull(state.NextLocked(MoveSlot.Special));
        }

        [Test]
        public void ClassMoveSetReplacesDefaults()
        {
            var jab = new FightingMove { id = "cable_whip", slot = MoveSlot.Light, unlockLevel = 1 };
            var state = new ProgressionState(new[] { jab, MoveLibrary.KeyboardSmash });

            Assert.AreSame(jab, state.BestMove(MoveSlot.Light));
            Assert.IsNull(state.BestMove(MoveSlot.Heavy));
            Assert.IsFalse(state.UnlockMove(MoveLibrary.CoffeeSplash.id), "moves outside the class set can't be learned");
            Assert.IsTrue(state.UnlockMove(MoveLibrary.KeyboardSmash.id));
            Assert.AreSame(MoveLibrary.KeyboardSmash, state.BestMove(MoveSlot.Super));
        }

        [Test]
        public void HotbarSlotsFollowMoveSetOrder()
        {
            var a = new FightingMove { id = "a", slot = MoveSlot.Light, unlockLevel = 1 };
            var b = new FightingMove { id = "b", slot = MoveSlot.Special, unlockLevel = 2 };
            var state = new ProgressionState(new[] { a, b });

            Assert.AreEqual(10, ProgressionState.HotbarSize);
            Assert.AreSame(a, state.HotbarMove(0));
            Assert.AreSame(b, state.HotbarMove(1));
            Assert.IsNull(state.HotbarMove(2), "unfilled slots are empty");
            Assert.IsNull(state.HotbarMove(-1));
            Assert.IsNull(state.HotbarMove(ProgressionState.HotbarSize));
        }

        [Test]
        public void HotbarKeyLabelsRunOneToZero()
        {
            Assert.AreEqual("1", ProgressionState.HotbarKey(0));
            Assert.AreEqual("9", ProgressionState.HotbarKey(8));
            Assert.AreEqual("0", ProgressionState.HotbarKey(9));
        }

        [Test]
        public void StarterMovesAreAvailableImmediately()
        {
            var state = new ProgressionState();
            Assert.AreSame(MoveLibrary.StaplerJab, state.BestMove(MoveSlot.Light));
            Assert.AreSame(MoveLibrary.BriefcaseSwing, state.BestMove(MoveSlot.Heavy));
        }
    }
}
