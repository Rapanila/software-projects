using System.Collections;
using System.IO;
using System.Linq;
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
    /// <summary>Casts each class's Q ability in the real scene and checks it actually does its thing.</summary>
    public class ClassAbilityPlayTests
    {
        static readonly string ScreenshotDir = Path.GetFullPath("Builds/Screenshots/Abilities");

        PlayerController player;
        GameFlow flow;

        IEnumerator Setup(string className)
        {
            SceneManager.LoadScene("Floor01_CubicleFarm");
            yield return null;
            yield return null;
            flow = GameFlow.Instance;
            int index = System.Array.FindIndex(flow.PlayerClasses, c => c.name == className);
            Assert.GreaterOrEqual(index, 0, className);
            flow.StartShiftWithClass(index);
            player = flow.Player;
            player.enabled = false;
            Directory.CreateDirectory(ScreenshotDir);
            yield return new WaitForSeconds(0.2f);
        }

        static void Teleport(PlayerController p, Vector3 position)
        {
            var cc = p.GetComponent<CharacterController>();
            cc.enabled = false;
            p.transform.position = position;
            cc.enabled = true;
        }

        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(ScreenshotDir, name + ".png"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator AccountantTaxRefundHeals()
        {
            yield return Setup("Accountant");
            player.Health.TakeDamage(new DamageInfo { amount = 50f });
            float hurt = player.Health.Current;

            Assert.IsTrue(player.Ability.TryCast());
            yield return Capture("Accountant_TaxRefund");
            Assert.AreEqual(Mathf.Min(hurt + 35f, player.Health.Max), player.Health.Current, 0.01f);
            Assert.IsFalse(player.Ability.TryCast(), "on cooldown");
            Assert.Greater(player.Ability.CooldownFraction, 0.9f);
        }

        [UnityTest]
        public IEnumerator ITBlueScreenFreezesNearbyEnemies()
        {
            yield return Setup("ITSupport");
            var near = EnemyBrain.Active.OrderBy(e => e.transform.position.x).First();
            var far = EnemyBrain.Active.OrderByDescending(e => e.transform.position.x).First();
            Teleport(player, near.transform.position + Vector3.left * 2f);
            yield return null;

            Assert.IsTrue(player.Ability.TryCast());
            yield return new WaitForSeconds(0.1f);
            yield return Capture("ITSupport_BlueScreen");
            Assert.IsTrue(near.IsStunned, "nearby coworker frozen");
            Assert.IsFalse(far.IsStunned, "far coworker unaffected");
            Vector3 frozenAt = near.transform.position;
            yield return new WaitForSeconds(1f);
            Assert.Less(Vector3.Distance(frozenAt, near.transform.position), 0.3f, "frozen coworker doesn't move");
            yield return new WaitForSeconds(1.3f);
            Assert.IsFalse(near.IsStunned, "wears off");
        }

        [UnityTest]
        public IEnumerator SalesHustleBoostsSpeedAndDamage()
        {
            yield return Setup("SalesRep");
            Assert.IsTrue(player.Ability.TryCast());
            yield return Capture("SalesRep_Hustle");
            Assert.AreEqual(1.5f, player.Motor.BuffSpeedMultiplier, 0.001f);
            Assert.AreEqual(1.3f, player.Attack.BuffDamageMultiplier, 0.001f);
            Assert.IsTrue(player.Ability.IsActive);
            yield return new WaitForSeconds(4.2f);
            Assert.AreEqual(1f, player.Motor.BuffSpeedMultiplier, 0.001f, "speed returns to normal");
            Assert.AreEqual(1f, player.Attack.BuffDamageMultiplier, 0.001f, "damage returns to normal");
        }

        [UnityTest]
        public IEnumerator HRFormalComplaintBlocksDamage()
        {
            yield return Setup("HRRep");
            Assert.IsTrue(player.Ability.TryCast());
            yield return new WaitForSeconds(0.25f);
            yield return Capture("HRRep_FormalComplaint");
            float before = player.Health.Current;
            Assert.IsFalse(player.Health.TakeDamage(new DamageInfo { amount = 30f }), "shielded");
            Assert.AreEqual(before, player.Health.Current);
            yield return new WaitForSeconds(3f);
            Assert.IsTrue(player.Health.TakeDamage(new DamageInfo { amount = 30f }), "shield expires");
        }
    }
}
