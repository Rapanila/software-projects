using System.Collections;
using System.Linq;
using NineToFight.Combat;
using NineToFight.Core;
using NineToFight.Enemies;
using NineToFight.Game;
using NineToFight.Items;
using NineToFight.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace NineToFight.Tests
{
    /// <summary>Thrown moves, the floor-1 mini-boss, coffee and seminars, the swing trail and the fire-drill reward, in play.</summary>
    public class ThrowsAndPickupsPlayTests
    {
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
        }

        static void Teleport(Component c, Vector3 position)
        {
            var cc = c.GetComponent<CharacterController>();
            cc.enabled = false;
            c.transform.position = position;
            cc.enabled = true;
        }

        IEnumerator KillEveryone()
        {
            foreach (var e in EnemyBrain.Active.ToList()) e.Health.TakeDamage(new DamageInfo { amount = 99999f });
            yield return null;
        }

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

        [UnityTest]
        public IEnumerator ThrownMemosFlyThroughEveryoneDownTheLane()
        {
            yield return Setup();
            var line = EnemyBrain.Active.Where(e => e.Boss == BossRank.None).Take(2).ToList();
            foreach (var e in EnemyBrain.Active.ToArray())
                if (!line.Contains(e)) e.gameObject.SetActive(false);
            Teleport(player, new Vector3(10f, 0f, 0f));
            player.Motor.FaceTowards(new Vector3(20f, 0f, 0f));
            for (int i = 0; i < line.Count; i++)
            {
                line[i].enabled = false;
                line[i].Health.SetMax(5000f, true);
                Teleport(line[i], new Vector3(15f + i * 2f, 0f, 0f)); // well beyond any swing's reach
            }
            yield return null;
            var memo = MoveLibrary.ReplyAll.Clone();
            memo.cooldown = 0f;
            memo.hits = 1;
            Assert.IsTrue(player.Attack.TryAttack(memo));
            yield return new WaitForSeconds(memo.startup + 0.05f);
            Assert.IsTrue(Projectile.Live.Any(p => p.Team == Team.Player && p.Pierces), "the memo is in the air");
            yield return new WaitForSeconds(1.2f);
            foreach (var e in line) Assert.Less(e.Health.Current, 5000f, e.name + " was hit by the memo passing through");
        }

        [UnityTest]
        public IEnumerator TheFloorOneManagerIsAProperMiniBoss()
        {
            yield return Setup();
            var boss = EnemyBrain.Active.FirstOrDefault(e => e.Boss == BossRank.MiniBoss);
            Assert.IsNotNull(boss, "floor 1's manager fights as a mini-boss");
            // MiddleManager.asset: maxHealth 140, scale 1.22, poise 0.55. A mini-boss gets x2.2 health, x1.12 size, poise >= 0.7.
            Assert.AreEqual(140f * 2.2f, boss.Health.Max, 0.01f, "with the boss health multiplier");
            Assert.AreEqual(1.22f * 1.12f, boss.transform.localScale.x, 0.001f, "and bigger");
            Assert.GreaterOrEqual(boss.WorkerClass.poise, 0.7f);
        }

        [UnityTest]
        public IEnumerator CoffeeWaitsUntilYouNeedItAndSeminarsReachLaterFloors()
        {
            yield return Setup();
            var director = Object.FindAnyObjectByType<TowerDirector>();
            yield return KillEveryone();
            var spot = player.transform.position + Vector3.right * 2f;
            var coffee = director.SpawnPickup(PickupKind.Coffee, spot);
            Assert.IsNotNull(coffee);
            player.Motor.CanMove = true;
            for (float t = 0f; t < 1f; t += Time.deltaTime) { player.Motor.SetMoveInput(Vector2.right); yield return null; }
            player.Motor.SetMoveInput(Vector2.zero);
            Assert.IsTrue(coffee != null, "full health: the coffee stays on the desk");
            player.Health.TakeDamage(new DamageInfo { amount = 40f });
            float hurt = player.Health.Current;
            Teleport(player, coffee.transform.position + Vector3.left * 1.5f);
            yield return null;
            for (float t = 0f; t < 1f && coffee != null; t += Time.deltaTime) { player.Motor.SetMoveInput(Vector2.right); yield return null; }
            player.Motor.SetMoveInput(Vector2.zero);
            Assert.IsTrue(coffee == null, "hurt: the coffee is drunk");
            Assert.Greater(player.Health.Current, hurt);

            // Floor 3 gets a training seminar.
            for (int floor = 2; floor <= 3; floor++)
            {
                TowerDirector.ForceEvent = FloorEvent.None;
                flow.NextFloor();
                yield return null;
                if (floor < 3) yield return KillEveryone();
            }
            var seminar = Object.FindObjectsByType<Pickup>(FindObjectsSortMode.None).FirstOrDefault(p => p.Kind == PickupKind.TrainingSeminar && p.isActiveAndEnabled);
            Assert.IsNotNull(seminar, "a seminar on floor 3");
            Assert.AreEqual(TowerPlan.SeminarMove(3), seminar.MoveId);
        }

        [UnityTest]
        public IEnumerator TheSwingTrailRidesAtTheEndOfTheItem()
        {
            yield return Setup();
            player.HeldItem.SetIdleProp("golf_club");
            yield return null;
            float club = player.Visual.TrailTip.magnitude;
            player.HeldItem.SetIdleProp("stapler");
            yield return null;
            float stapler = player.Visual.TrailTip.magnitude;
            Assert.Greater(club, stapler + 0.2f, "a golf club's trail is far out at the head; a stapler's is at the fist");
        }

        [UnityTest]
        public IEnumerator RunningFromAFireDrillEarnsNoBenefits()
        {
            yield return Setup();
            yield return KillEveryone();
            TowerDirector.ForceEvent = FloorEvent.FireDrill;
            flow.NextFloor();
            yield return new WaitForSeconds(0.5f);
            Assert.IsNotEmpty(EnemyBrain.Active, "coworkers still on the floor");
            yield return WalkIntoElevator();
            Assert.AreEqual(GameState.Ending, flow.State);
            Assert.AreEqual(0, flow.PerkOffer.Count, "skipping the floor skips the benefits package");
        }
    }
}
