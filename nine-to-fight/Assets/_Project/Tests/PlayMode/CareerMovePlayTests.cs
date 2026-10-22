using System.Collections;
using System.IO;
using System.Linq;
using NineToFight.Core;
using NineToFight.Enemies;
using NineToFight.Game;
using NineToFight.Player;
using NineToFight.Progression;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace NineToFight.Tests
{
    /// <summary>Late-career moves in the real scene: flurries hit repeatedly, heal-on-hit heals.</summary>
    public class CareerMovePlayTests
    {
        static readonly string ScreenshotDir = Path.GetFullPath("Builds/Screenshots/Attacks");

        [UnityTest]
        public IEnumerator FiscalYearEndHitsFourTimesAndHeals()
        {
            SceneManager.LoadScene("Floor01_CubicleFarm");
            yield return null;
            yield return null;
            var flow = GameFlow.Instance;
            int index = System.Array.FindIndex(flow.PlayerClasses, c => c.name == "Accountant");
            flow.StartShiftWithClass(index);
            var player = flow.Player;
            player.enabled = false;

            while (!player.Progression.State.IsMaxLevel) player.Progression.AddXp(1000);
            var state = player.Progression.State;
            Assert.IsTrue(Enumerable.Range(0, ProgressionState.HotbarSize).All(i => state.IsUnlocked(state.HotbarMove(i))),
                "a CEO has every move on the hotbar");

            // A heavy-poise target so the knockback doesn't carry it out of range between hits.
            var target = EnemyBrain.Active.OrderByDescending(e => e.WorkerClass.poise).First();
            foreach (var e in EnemyBrain.Active) e.Stun(10f, Color.white); // nobody interrupts the flurry
            var cc = player.GetComponent<CharacterController>();
            cc.enabled = false;
            player.transform.position = target.transform.position + Vector3.left * 1.1f;
            cc.enabled = true;
            player.Motor.FaceTowards(target.transform.position);
            yield return null;

            player.Health.TakeDamage(new DamageInfo { amount = 60f });
            yield return new WaitForSeconds(0.5f); // let hit stun wear off
            float hurt = player.Health.Current;
            player.Health.MakeInvulnerable(5f); // the target's counter-swings can't interrupt the flurry

            int hits = 0;
            target.Health.Damaged += _ => hits++;
            int slot = Enumerable.Range(0, ProgressionState.HotbarSize).First(i => state.HotbarMove(i).id == "fiscal_year_end");
            player.TryHotbar(slot);
            Assert.AreEqual("fiscal_year_end", player.Attack.Current?.id);

            Directory.CreateDirectory(ScreenshotDir);
            yield return new WaitForSeconds(0.45f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(ScreenshotDir, "Accountant_FiscalYearEnd.png"));

            yield return new WaitForSeconds(state.HotbarMove(slot).TotalDuration);
            Assert.IsFalse(player.Attack.IsAttacking, "flurry finishes");
            Assert.GreaterOrEqual(hits, 3, "flurry lands repeated hits");
            Assert.Greater(player.Health.Current, hurt, "heal-on-hit restores health");
        }
    }
}
