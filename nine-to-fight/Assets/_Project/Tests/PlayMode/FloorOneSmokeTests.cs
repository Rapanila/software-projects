using System.Collections;
using System.IO;
using System.Linq;
using NineToFight.Characters;
using NineToFight.Combat;
using NineToFight.Core;
using NineToFight.Enemies;
using NineToFight.Game;
using NineToFight.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace NineToFight.Tests
{
    /// <summary>Drives the real Floor 1 scene end to end: menus, movement, combat, promotion, exit.</summary>
    public class FloorOneSmokeTests
    {
        const string SceneName = "Floor01_CubicleFarm";
        static readonly string ScreenshotDir = Path.GetFullPath("Builds/Screenshots");

        [UnityTest]
        public IEnumerator FullShiftLoop()
        {
            SceneManager.LoadScene(SceneName);
            yield return null;
            yield return null;

            var flow = GameFlow.Instance;
            Assert.IsNotNull(flow, "GameFlow missing from scene");
            Assert.AreEqual(GameState.Title, flow.State);
            yield return new WaitForSeconds(0.3f);
            yield return Capture("01_title");

            flow.OpenClassSelect();
            yield return null;
            Assert.AreEqual(GameState.ClassSelect, flow.State);
            yield return Capture("02_class_select");

            flow.StartShiftWithClass(1);
            var player = flow.Player;
            Assert.AreEqual(GameState.Playing, flow.State);
            Assert.AreEqual("IT Support", player.Class.displayName);
            Assert.AreEqual(80f, player.Health.Max, "class health applied");
            Assert.AreEqual("cable_whip", player.Progression.MoveFor(MoveSlot.Light).id, "class move set applied");
            Assert.AreEqual("ethernet_cable", player.HeldItem.CurrentProp, "carries the light attack's item");
            Assert.IsTrue(EnemyBrain.Active.All(e => !string.IsNullOrEmpty(e.HeldItem.CurrentProp)), "coworkers hold items");

            // Drive the motor directly (input devices aren't available in tests).
            player.enabled = false;
            var motor = player.Motor;
            motor.CanMove = true;

            float startX = player.transform.position.x;
            yield return Hold(motor, Vector2.right, 0.6f);
            Assert.Greater(player.transform.position.x, startX + 2f, "runs right");
            Assert.AreEqual(1, motor.Facing);

            motor.RequestJump();
            yield return new WaitForSeconds(0.2f);
            Assert.Greater(player.transform.position.y, 0.5f, "jumps");
            yield return new WaitForSeconds(0.8f);
            Assert.IsTrue(motor.IsGrounded, "lands");

            yield return Hold(motor, Vector2.up, 1f);
            Assert.LessOrEqual(player.transform.position.z, 2.5f + 0.01f, "depth lane clamps");
            Assert.Greater(player.transform.position.z, 2f, "moves along depth");
            AssertYaw(motor, 0f, "W turns to face the back wall");
            yield return Capture("06_facing_wall");

            yield return Hold(motor, Vector2.down, 0.55f);
            AssertYaw(motor, 180f, "S turns to face the camera");
            yield return Capture("07_facing_camera");

            yield return Hold(motor, new Vector2(-1f, -1f), 0.15f);
            AssertYaw(motor, -135f, "diagonals face in between");

            var intern = EnemyBrain.Active
                .Where(e => e.WorkerClass.displayName == "Intern")
                .OrderBy(e => e.transform.position.x)
                .First();
            Teleport(player, intern.transform.position + Vector3.left * 1.1f);
            yield return Hold(motor, Vector2.right * 0.3f, 0.1f);
            AssertYaw(motor, 90f, "faces the intern before swinging");
            yield return new WaitForSeconds(0.5f);
            yield return Capture("03_gameplay");

            int levelBefore = player.Progression.Level;
            int xpBefore = player.Progression.State.Xp;
            var heavy = player.Progression.MoveFor(MoveSlot.Heavy);
            bool swungMouse = false, captured = false;
            for (float t = 0f; t < 8f && !intern.Health.IsDead; t += Time.deltaTime)
            {
                if (!player.Attack.IsAttacking) player.TryHotbar(1); // key 2 = heavy
                if (player.Attack.IsAttacking && player.HeldItem.CurrentProp == "mouse") swungMouse = true;
                if (!captured && swungMouse)
                {
                    captured = true;
                    yield return new WaitForSeconds(heavy.startup);
                    yield return Capture("05_item_swing");
                }
                yield return null;
            }
            Assert.IsTrue(swungMouse, "heavy attack swaps to its own item");
            Assert.IsTrue(intern.Health.IsDead, "player can beat an intern");
            Assert.IsTrue(player.Progression.Level > levelBefore || player.Progression.State.Xp > xpBefore, "XP awarded");

            player.Progression.AddXp(player.Progression.State.XpToNext);
            Assert.GreaterOrEqual(player.Progression.Level, 2);
            Assert.AreEqual("hard_reboot", player.Progression.MoveFor(MoveSlot.Special)?.id, "signature unlocked on promotion");

            foreach (var enemy in EnemyBrain.Active.ToList())
                enemy.Health.TakeDamage(new DamageInfo { amount = 9999f });
            yield return null;
            Assert.AreEqual(GameState.FloorCleared, flow.State);

            Teleport(player, new Vector3(51.5f, 0f, 0f));
            yield return Hold(motor, Vector2.right, 1.2f);
            Assert.AreEqual(GameState.Ending, flow.State, "elevator ends the floor");
            yield return new WaitForSeconds(0.2f);
            yield return Capture("04_floor_cleared");

            // Ride the elevator up: the same room is re-staged as Floor 2.
            player.enabled = true;
            TowerDirector.ForceEvent = FloorEvent.None; // events are covered by VarietyPlayTests
            flow.NextFloor();
            yield return null;
            Assert.AreEqual(2, flow.Floor);
            Assert.AreEqual(GameState.Playing, flow.State);
            Assert.Less(player.transform.position.x, 2f, "player starts the new floor at the left");
            Assert.IsNotEmpty(EnemyBrain.Active, "floor 2 has coworkers");
            Assert.IsTrue(EnemyBrain.Active.Any(e => e.DisplayName == "Junior Accountant" || e.DisplayName == "Audit Intern" || e.DisplayName == "Auditor"),
                "floor 2 is staffed by Accounting");
            Assert.IsTrue(EnemyBrain.Active.All(e => e.Health.Current > 0f && e.WorkerClass != null));
            yield return new WaitForSeconds(1f);
            Assert.AreEqual(1, EnemyBrain.Active[0].GetComponentsInChildren<Transform>().Count(t => t.name == "Overhead"), "exactly one overhead bar");
            yield return Capture("08_floor_two");

            foreach (var enemy in EnemyBrain.Active.ToList())
                enemy.Health.TakeDamage(new DamageInfo { amount = 9999f });
            yield return null;
            Assert.AreEqual(GameState.FloorCleared, flow.State, "floor 2 can be cleared");
        }

        static IEnumerator Hold(CharacterMotor2_5D motor, Vector2 input, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                motor.SetMoveInput(input);
                yield return null;
            }
            motor.SetMoveInput(Vector2.zero);
        }

        static void AssertYaw(CharacterMotor2_5D motor, float expected, string message)
        {
            float yaw = motor.Visual.localEulerAngles.y;
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(yaw, expected)), 5f, $"{message} (yaw {yaw:0})");
        }

        static void Teleport(PlayerController player, Vector3 position)
        {
            var cc = player.GetComponent<CharacterController>();
            cc.enabled = false;
            player.transform.position = position;
            cc.enabled = true;
        }

        static IEnumerator Capture(string name)
        {
            Directory.CreateDirectory(ScreenshotDir);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(ScreenshotDir, name + ".png"));
            yield return null;
            yield return null;
        }
    }
}
