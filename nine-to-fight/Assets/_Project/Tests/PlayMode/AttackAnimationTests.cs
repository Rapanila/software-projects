using System.Collections;
using System.IO;
using System.Linq;
using NineToFight.Characters;
using NineToFight.Combat;
using NineToFight.Enemies;
using NineToFight.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace NineToFight.Tests
{
    /// <summary>
    /// Plays every class's moves in the real scene on the animation clips, checks the body really moves (the
    /// weapon hand travels and swings through), that the item stays in the fist, and screenshots each strike.
    /// </summary>
    public class AttackAnimationTests
    {
        static readonly string ScreenshotDir = Path.GetFullPath("Builds/Screenshots/Attacks");

        [UnityTest]
        public IEnumerator EveryClassMoveAnimatesItsOwnWay()
        {
            SceneManager.LoadScene("Floor01_CubicleFarm");
            yield return null;
            yield return null;

            var flow = GameFlow.Instance;
            Assert.AreEqual(4, flow.PlayerClasses.Count(c => c != null), "every department is wired into the scene");
            flow.StartShiftWithClass(0);
            var player = flow.Player;
            player.enabled = false;
            foreach (var enemy in EnemyBrain.Active.ToArray())
                enemy.gameObject.SetActive(false);

            Assert.IsTrue(player.Visual.UsesClips, "the body is animated by clips");
            var follower = player.Visual.GetComponent<RigFollower>();
            var socket = player.transform.Find("Visual/Model/Chest/ArmPivot/Elbow/HandSocket");
            Assert.IsNotNull(socket, "hand socket");
            Directory.CreateDirectory(ScreenshotDir);

            foreach (var playerClass in flow.PlayerClasses)
            {
                player.ApplyClass(playerClass);
                player.Progression.AddXp(player.Progression.State.XpToNext);
                player.Health.Heal(9999f);
                yield return null;
                var hand = follower.Hand;
                var body = player.transform;

                for (int slot = 0; slot < 3; slot++)
                {
                    var move = player.Progression.State.HotbarMove(slot);
                    yield return new WaitForSeconds(0.3f);
                    Vector3 restHand = body.InverseTransformPoint(hand.position);

                    Assert.IsTrue(player.Attack.TryAttack(move), $"{move.displayName} starts");
                    // Track how far the weapon hand travels from the wind-up to the impact.
                    float travelled = 0f;
                    Vector3 last = body.InverseTransformPoint(hand.position);
                    float impact = AttackPoses.ImpactTime(move.startup, move.active);
                    for (float t = 0f; t < impact; t += Time.deltaTime)
                    {
                        yield return null;
                        Vector3 now = body.InverseTransformPoint(hand.position);
                        travelled += (now - last).magnitude;
                        last = now;
                    }
                    Assert.Greater(travelled + (last - restHand).magnitude, 0.25f, $"{move.displayName}: the body swings through");
                    Assert.AreEqual(move.prop, player.HeldItem.CurrentProp, $"{move.displayName} holds its item");
                    Assert.Less(Vector3.Distance(socket.position, hand.position), 0.2f, $"{move.displayName}: item stays in the fist");
                    Assert.Greater(player.Motor.FacingDirection.x, 0.9f, $"{move.displayName}: still facing right");

                    ScreenCapture.CaptureScreenshot(Path.Combine(ScreenshotDir, $"{playerClass.name}_{slot + 1}_{move.style}.png"));
                    yield return new WaitForSeconds(move.TotalDuration - impact + 0.1f);
                    Assert.IsFalse(player.Attack.IsAttacking);
                }
            }
        }
    }
}
