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
    /// <summary>The things that keep floors from feeling the same: enemy roles, perks and floor events.</summary>
    public class VarietyPlayTests
    {
        static readonly string ScreenshotDir = Path.GetFullPath("Builds/Screenshots/Variety");

        GameFlow flow;
        PlayerController player;

        IEnumerator Setup()
        {
            SceneManager.LoadScene("Floor01_CubicleFarm");
            yield return null;
            yield return null;
            flow = GameFlow.Instance;
            flow.StartShiftWithClass(0);
            player = flow.Player;
            player.enabled = false;
            Directory.CreateDirectory(ScreenshotDir);
        }

        static void Teleport(Component c, Vector3 position)
        {
            var cc = c.GetComponent<CharacterController>();
            cc.enabled = false;
            c.transform.position = position;
            cc.enabled = true;
        }

        static IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(ScreenshotDir, name + ".png"));
            yield return null;
        }

        /// <summary>Walks into the elevator trigger (teleporting alone doesn't fire trigger events).</summary>
        IEnumerator WalkIntoElevator()
        {
            Teleport(player, new Vector3(51.5f, 0f, 0f));
            player.Motor.CanMove = true;
            for (float t = 0f; t < 1.2f && flow.State != GameState.Ending; t += Time.deltaTime)
            {
                player.Motor.SetMoveInput(Vector2.right);
                yield return null;
            }
            player.Motor.SetMoveInput(Vector2.zero);
        }

        void KeepOnly(EnemyBrain keep)
        {
            foreach (var e in EnemyBrain.Active.ToArray())
                if (e != keep) e.gameObject.SetActive(false);
        }

        [UnityTest]
        public IEnumerator AccountantsBlockLightHitsButNotHeaviesOrBackstabs()
        {
            yield return Setup();
            var guard = EnemyBrain.Active.First(e => e.Role == EnemyRole.Guard);
            KeepOnly(guard);
            guard.enabled = false; // freeze its AI; the guard itself still works
            Teleport(player, guard.transform.position + Vector3.left * 1.2f);
            yield return null;
            guard.Motor.FaceTowards(player.transform.position);
            Assert.IsTrue(guard.IsGuarding);

            float hp = guard.Health.Current;
            guard.Health.TakeDamage(new DamageInfo { amount = 10f, source = player.gameObject });
            Assert.AreEqual(hp - 1.5f, guard.Health.Current, 0.01f, "light hit from the front is blocked");

            hp = guard.Health.Current;
            guard.Motor.FaceTowards(guard.transform.position + Vector3.right * 3f);
            guard.Health.TakeDamage(new DamageInfo { amount = 10f, source = player.gameObject });
            Assert.AreEqual(hp - 10f, guard.Health.Current, 0.01f, "hit from behind lands in full");

            yield return new WaitForSeconds(0.6f);
            guard.Motor.FaceTowards(player.transform.position);
            hp = guard.Health.Current;
            guard.Health.TakeDamage(new DamageInfo { amount = 10f, source = player.gameObject, heavy = true });
            Assert.Greater(hp - guard.Health.Current, 10f, "heavy breaks the guard");
            Assert.IsFalse(guard.IsGuarding, "guard stays broken for a moment");
        }

        [UnityTest]
        public IEnumerator ITSupportThrowsFromRange()
        {
            yield return Setup();
            var thrower = EnemyBrain.Active.First(e => e.Role == EnemyRole.Thrower);
            KeepOnly(thrower);
            Teleport(player, new Vector3(thrower.transform.position.x - 6f, 0f, thrower.transform.position.z));
            bool thrown = false, captured = false;
            for (float t = 0f; t < 6f && !thrown; t += Time.deltaTime)
            {
                if (Projectile.Live.Count > 0)
                {
                    thrown = true;
                    if (!captured) { captured = true; yield return Capture("ITSupport_Throw"); }
                }
                yield return null;
            }
            Assert.IsTrue(thrown, "IT Support throws something");
            Assert.Greater(Mathf.Abs(thrower.transform.position.x - player.transform.position.x), 3f, "keeps its distance");
        }

        [UnityTest]
        public IEnumerator AttacksSwatProjectilesBack()
        {
            yield return Setup();
            KeepOnly(null);
            var p = Projectile.Launch(null, player.transform.position + new Vector3(1.4f, 1f, 0f), Vector3.left, 2f, 10f, Team.Enemy, null);
            player.Motor.FaceTowards(player.transform.position + Vector3.right);
            yield return null;
            Assert.IsTrue(player.Attack.TryAttack(player.Progression.MoveFor(MoveSlot.Heavy)));
            float hp = player.Health.Current;
            yield return new WaitForSeconds(0.5f);
            Assert.IsNotNull(p, "the swing caught the projectile before it hit");
            Assert.AreEqual(Team.Player, p.Team, "reflected projectile now belongs to the player");
            Assert.AreEqual(hp, player.Health.Current, "player wasn't hit");
        }

        [UnityTest]
        public IEnumerator ElevatorOffersPerksThatChangeTheRun()
        {
            yield return Setup();
            foreach (var e in EnemyBrain.Active.ToList()) e.Health.TakeDamage(new DamageInfo { amount = 9999f });
            yield return null;
            yield return WalkIntoElevator();
            Assert.AreEqual(GameState.Ending, flow.State);
            Assert.AreEqual(3, flow.PerkOffer.Count, "three benefits to choose from");
            yield return Capture("Perk_Choice");

            var stock = flow.PerkOffer.FirstOrDefault(pk => pk.id == PerkId.StockOptions) ?? flow.PerkOffer[0];
            int index = flow.PerkOffer.ToList().IndexOf(stock);
            flow.ChoosePerk(index);
            Assert.AreEqual(1, player.Perks.Stacks(stock.id));
            TowerDirector.ForceEvent = FloorEvent.None;
            flow.NextFloor();
            Assert.AreEqual(2, flow.Floor);
            if (stock.id == PerkId.StockOptions) Assert.AreEqual(1.15f, player.Attack.PerkDamageMultiplier, 0.001f);
        }

        [UnityTest]
        public IEnumerator AllHandsMeetingArrivesInWaves()
        {
            yield return Setup();
            foreach (var e in EnemyBrain.Active.ToList()) e.Health.TakeDamage(new DamageInfo { amount = 9999f });
            yield return null;
            TowerDirector.ForceEvent = FloorEvent.AllHands;
            flow.NextFloor();
            yield return null;
            Assert.AreEqual(FloorEvent.AllHands, flow.CurrentEvent);

            for (int wave = 1; wave <= 3; wave++)
            {
                Assert.IsNotEmpty(EnemyBrain.Active, $"wave {wave} arrived");
                Assert.AreEqual(GameState.Playing, flow.State);
                if (wave == 2) yield return Capture("AllHands_Wave2");
                foreach (var e in EnemyBrain.Active.ToList()) e.Health.TakeDamage(new DamageInfo { amount = 9999f });
                yield return null;
            }
            Assert.AreEqual(GameState.FloorCleared, flow.State, "cleared after the last wave");
        }

        [UnityTest]
        public IEnumerator FireDrillLetsYouRunForTheElevator()
        {
            yield return Setup();
            foreach (var e in EnemyBrain.Active.ToList()) e.Health.TakeDamage(new DamageInfo { amount = 9999f });
            yield return null;
            TowerDirector.ForceEvent = FloorEvent.FireDrill;
            flow.NextFloor();
            yield return new WaitForSeconds(0.5f);
            Assert.Greater(flow.DrillTimeLeft, 30f);
            Assert.IsNotEmpty(EnemyBrain.Active, "coworkers still on the floor");
            yield return Capture("FireDrill");
            yield return WalkIntoElevator();
            Assert.AreEqual(GameState.Ending, flow.State, "escaped without clearing the floor");
        }

        [UnityTest]
        public IEnumerator PowerOutageDimsTheLights()
        {
            yield return Setup();
            foreach (var e in EnemyBrain.Active.ToList()) e.Health.TakeDamage(new DamageInfo { amount = 9999f });
            yield return null;
            var key = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).First(l => l.name == "Key Light");
            float bright = key.intensity;
            TowerDirector.ForceEvent = FloorEvent.PowerOutage;
            flow.NextFloor();
            yield return new WaitForSeconds(1f);
            Assert.Less(key.intensity, bright * 0.5f);
            Assert.IsNotNull(player.GetComponentInChildren<Light>(), "player has a flashlight");
            yield return Capture("PowerOutage");
        }
    }
}
