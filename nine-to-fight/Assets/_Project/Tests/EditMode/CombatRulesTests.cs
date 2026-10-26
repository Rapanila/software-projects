using System.Linq;
using NineToFight.Combat;
using NineToFight.Game;
using NineToFight.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace NineToFight.Tests
{
    /// <summary>Cancel rules, knockback direction, dodge timing and the arena lock: the rules the fights run on.</summary>
    public class CombatRulesTests
    {
        static FightingMove Move(MoveSlot slot) => new FightingMove { id = slot.ToString(), slot = slot };

        [Test]
        public void HitConfirmedMovesCancelOnlyIntoStrongerOnes()
        {
            Assert.IsTrue(AttackExecutor.CancelAllowed(Move(MoveSlot.Light), Move(MoveSlot.Heavy), 0));
            Assert.IsTrue(AttackExecutor.CancelAllowed(Move(MoveSlot.Light), Move(MoveSlot.Special), 0));
            Assert.IsTrue(AttackExecutor.CancelAllowed(Move(MoveSlot.Heavy), Move(MoveSlot.Super), 0));
            Assert.IsTrue(AttackExecutor.CancelAllowed(Move(MoveSlot.Special), Move(MoveSlot.Super), 0));
            Assert.IsFalse(AttackExecutor.CancelAllowed(Move(MoveSlot.Heavy), Move(MoveSlot.Light), 0), "no stepping back down");
            Assert.IsFalse(AttackExecutor.CancelAllowed(Move(MoveSlot.Heavy), Move(MoveSlot.Heavy), 0));
            Assert.IsFalse(AttackExecutor.CancelAllowed(Move(MoveSlot.Super), Move(MoveSlot.Super), 0));
        }

        [Test]
        public void LightStringsStopAfterThreeHits()
        {
            var light = Move(MoveSlot.Light);
            for (int chain = 0; chain < AttackExecutor.MaxLightChain; chain++)
                Assert.IsTrue(AttackExecutor.CancelAllowed(light, light, chain), $"light #{chain + 2}");
            Assert.IsFalse(AttackExecutor.CancelAllowed(light, light, AttackExecutor.MaxLightChain), "the string has to end");
        }

        [Test]
        public void KnockbackPushesAwayFromTheAttacker()
        {
            var facingRight = Vector3.right;
            // In front: pushed on along the facing.
            Assert.Greater(AttackExecutor.KnockbackDirection(Vector3.zero, new Vector3(1f, 0f, 0f), facingRight).x, 0.9f);
            // Behind (an all-round sweep): pushed back the other way, never through the attacker.
            Assert.Less(AttackExecutor.KnockbackDirection(Vector3.zero, new Vector3(-1f, 0f, 0f), facingRight).x, -0.9f);
            // Off to one side of the lane: pushed outward, flat on the floor.
            var side = AttackExecutor.KnockbackDirection(Vector3.zero, new Vector3(0f, 1f, 1f), facingRight);
            Assert.Greater(side.z, 0.9f);
            Assert.AreEqual(0f, side.y, 1e-5f);
            // Right on top of the attacker: the way the blow was thrown.
            Assert.AreEqual(facingRight, AttackExecutor.KnockbackDirection(Vector3.zero, new Vector3(0.05f, 0f, 0f), facingRight));
        }

        [Test]
        public void DodgesCanNeverChainIntoPermanentInvulnerability()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player.prefab");
            var so = new SerializedObject(prefab.GetComponent<PlayerController>());
            float invulnerable = so.FindProperty("dodgeInvulnerability").floatValue;
            var classes = AssetDatabase.FindAssets("t:PlayerClass")
                .Select(g => AssetDatabase.LoadAssetAtPath<PlayerClass>(AssetDatabase.GUIDToAssetPath(g))).ToList();
            Assert.IsNotEmpty(classes);
            foreach (var c in classes)
            {
                Assert.Greater(c.dodgeCooldown, invulnerable, c.name + ": the authored cooldown outlasts the i-frames");
                // Even with Flex Time stacked several times the code keeps a vulnerable gap.
                for (int stacks = 0; stacks <= 5; stacks++)
                {
                    float cooldown = PlayerController.DodgeCooldownFor(c.dodgeCooldown, Mathf.Pow(0.7f, stacks), invulnerable);
                    Assert.GreaterOrEqual(cooldown, invulnerable + PlayerController.DodgeVulnerableGap - 1e-5f, $"{c.name} x{stacks} Flex Time");
                }
            }
        }

        [Test]
        public void TheArenaLocksAroundTheFightAndOpensWhenItsOver()
        {
            var arena = new ArenaLock();
            arena.BeginFrame();
            arena.EndFrame(10f, true, -1f, 53f);
            Assert.IsFalse(arena.Locked, "nobody engaged, nothing to lock");

            arena.BeginFrame();
            arena.AddEngaged(13f);
            arena.EndFrame(10f, true, -1f, 53f);
            Assert.IsTrue(arena.Locked);
            Assert.AreEqual(10f - ArenaLock.HalfWidth, arena.Min, 1e-4f);
            Assert.AreEqual(10f + ArenaLock.HalfWidth, arena.Max, 1e-4f);

            // A thrower engaging from far off widens the arena to take it in, and it doesn't follow the player.
            arena.BeginFrame();
            arena.AddEngaged(13f);
            arena.AddEngaged(20f);
            arena.EndFrame(12f, true, -1f, 53f);
            Assert.AreEqual(10f - ArenaLock.HalfWidth, arena.Min, 1e-4f);
            Assert.AreEqual(20f + ArenaLock.Margin, arena.Max, 1e-4f);

            // Never past the level's own walls.
            arena.BeginFrame();
            arena.AddEngaged(60f);
            arena.EndFrame(12f, true, -1f, 53f);
            Assert.AreEqual(53f, arena.Max, 1e-4f);

            // A fire drill (not allowed) or the last coworker down opens it.
            arena.BeginFrame();
            arena.AddEngaged(13f);
            arena.EndFrame(12f, false, -1f, 53f);
            Assert.IsFalse(arena.Locked);
            arena.BeginFrame();
            arena.EndFrame(12f, true, -1f, 53f);
            Assert.IsFalse(arena.Locked);
        }
    }
}
