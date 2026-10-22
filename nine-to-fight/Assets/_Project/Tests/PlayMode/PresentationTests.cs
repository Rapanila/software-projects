using System.Collections;
using System.IO;
using System.Linq;
using NineToFight.Core;
using NineToFight.Enemies;
using NineToFight.Game;
using NineToFight.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace NineToFight.Tests
{
    /// <summary>
    /// Walks the whole presentation layer: title menu, options, department select, HUD, pause,
    /// boss intro, benefits, the elevator ride and the termination letter. Captures each screen.
    /// </summary>
    public class PresentationTests
    {
        static readonly string Dir = Path.GetFullPath("Builds/Screenshots/Presentation");

        static IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(Dir, name + ".png"));
            yield return null;
        }

        static void Teleport(Component c, Vector3 position)
        {
            var cc = c.GetComponent<CharacterController>();
            cc.enabled = false;
            c.transform.position = position;
            cc.enabled = true;
        }

        [UnityTest]
        public IEnumerator EveryScreenWorks()
        {
            Directory.CreateDirectory(Dir);
            SceneManager.LoadScene("Floor01_CubicleFarm");
            yield return null;
            yield return null;
            var flow = GameFlow.Instance;
            var hud = Object.FindAnyObjectByType<GameHud>();
            Assert.AreEqual(GameState.Title, flow.State);
            Assert.AreEqual(3, hud.TitleMenu.Count, "Clock in, Options, Quit");
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Capture("01_title");

            // Options open over the title and close back to it.
            hud.TitleMenu.Select(1);
            hud.TitleMenu.Choose();
            Assert.AreEqual(GameState.Options, flow.State);
            Assert.AreEqual(GameSettings.All.Count + 1, hud.OptionsMenu.Count, "a row per setting plus Back");
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Capture("02_options");
            hud.OptionsMenu.Select(hud.OptionsMenu.Count - 1);
            hud.OptionsMenu.Choose();
            Assert.AreEqual(GameState.Title, flow.State, "Back returns to the title");

            hud.TitleMenu.Select(0);
            hud.TitleMenu.Choose();
            Assert.AreEqual(GameState.ClassSelect, flow.State);
            yield return new WaitForSecondsRealtime(1.3f);
            yield return Capture("03_department_select");
            flow.SelectClass(2);
            Assert.AreEqual("Sales Rep", flow.Player.Class.displayName, "selecting a tab applies the department");
            yield return new WaitForSecondsRealtime(1f);
            yield return Capture("04_department_select_sales");

            flow.ConfirmClass();
            Assert.AreEqual(GameState.Playing, flow.State);
            var player = flow.Player;
            yield return new WaitForSeconds(1.5f);
            yield return Capture("05_hud");

            flow.PauseGame();
            Assert.AreEqual(GameState.Paused, flow.State);
            Assert.AreEqual(0f, Time.timeScale);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Capture("06_pause");
            hud.PauseMenu.Select(0);
            hud.PauseMenu.Choose();
            Assert.AreEqual(GameState.Playing, flow.State, "Back to work resumes");
            Assert.AreEqual(1f, Time.timeScale);

            // Walk up to the floor's manager: the boss intro plays.
            var boss = EnemyBrain.Active.First(e => e.Boss != BossRank.None);
            foreach (var e in EnemyBrain.Active.ToArray()) if (e != boss) e.gameObject.SetActive(false);
            player.Health.MakeInvulnerable(30f);
            Teleport(player, boss.transform.position + Vector3.left * 6f);
            for (float t = 0f; t < 2f && flow.EngagedBoss == null; t += Time.unscaledDeltaTime) yield return null;
            Assert.AreSame(boss, flow.EngagedBoss, "boss engaged");
            yield return new WaitForSecondsRealtime(0.7f);
            yield return Capture("07_boss_intro");
            Assert.AreEqual("Let's circle back to your beatdown", GameFlow.BossSubtitle(boss));

            boss.Health.TakeDamage(new DamageInfo { amount = 9999f });
            yield return new WaitForSecondsRealtime(2f);
            Assert.AreEqual(GameState.FloorCleared, flow.State);

            Teleport(player, new Vector3(51.5f, 0f, 0f));
            player.enabled = false;
            player.Motor.CanMove = true;
            for (float t = 0f; t < 1.5f && flow.State != GameState.Ending; t += Time.deltaTime)
            {
                player.Motor.SetMoveInput(Vector2.right);
                yield return null;
            }
            player.Motor.SetMoveInput(Vector2.zero);
            player.enabled = true;
            Assert.AreEqual(GameState.Ending, flow.State);
            Assert.AreEqual(3, flow.PerkOffer.Count);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Capture("08_benefits");

            TowerDirector.ForceEvent = FloorEvent.None;
            flow.SelectPerk(1);
            flow.ConfirmEnding();
            Assert.IsTrue(flow.IsRiding);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Capture("09_elevator");
            for (float t = 0f; t < 5f && flow.IsRiding; t += Time.unscaledDeltaTime) yield return null;
            Assert.IsFalse(flow.IsRiding, "the ride ends");
            Assert.AreEqual(2, flow.Floor);
            Assert.AreEqual(GameState.Playing, flow.State);
            Assert.AreEqual(1, player.Perks.Count, "the chosen benefit was taken");
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Capture("10_floor_two");

            // Knock the player out (twice if the Golden Parachute benefit catches the first one).
            var invulnerable = typeof(Health).GetField("invulnerableUntil", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            for (int attempt = 0; attempt < 3 && !player.Health.IsDead; attempt++)
            {
                invulnerable.SetValue(player.Health, 0f);
                player.Health.TakeDamage(new DamageInfo { amount = 99999f });
                yield return null;
            }
            Assert.IsTrue(player.Health.IsDead);
            yield return new WaitForSecondsRealtime(1.8f);
            Assert.AreEqual(GameState.GameOver, flow.State);
            yield return Capture("11_termination_letter");
        }
    }
}
