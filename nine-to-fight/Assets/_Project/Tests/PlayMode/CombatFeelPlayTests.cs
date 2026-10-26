using System.Collections;
using System.Linq;
using NineToFight.CameraRig;
using NineToFight.Combat;
using NineToFight.Core;
using NineToFight.Enemies;
using NineToFight.Game;
using NineToFight.Player;
using NineToFight.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace NineToFight.Tests
{
    /// <summary>
    /// The fight's feel in play: the pause menu swallows input, presses are buffered, a landed hit can be
    /// cancelled into a heavy, stuns aren't shortened, the arena locks, attack slots cap the crowd, and
    /// banners queue instead of overwriting each other.
    /// </summary>
    public class CombatFeelPlayTests
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
        }

        static void Teleport(Component c, Vector3 position)
        {
            var cc = c.GetComponent<CharacterController>();
            cc.enabled = false;
            c.transform.position = position;
            cc.enabled = true;
        }

        /// <summary>One coworker in front of the player, its AI switched off.</summary>
        EnemyBrain LoneDummy()
        {
            var dummy = EnemyBrain.Active.First(e => e.Role == EnemyRole.Brawler);
            foreach (var e in EnemyBrain.Active.ToArray())
                if (e != dummy) e.gameObject.SetActive(false);
            dummy.enabled = false;
            dummy.Health.SetMax(5000f, true);
            Teleport(player, dummy.transform.position + Vector3.left * 1.0f);
            player.Motor.FaceTowards(dummy.transform.position);
            return dummy;
        }

        [UnityTest]
        public IEnumerator ThePauseMenuStopsThePlayerActing()
        {
            yield return Setup();
            yield return null;
            Assert.IsTrue(player.Motor.CanMove, "free to move before the pause");
            flow.PauseGame();
            yield return null;
            Assert.IsTrue(GameFlow.IsPaused);
            Assert.IsFalse(player.Motor.CanMove, "the pause menu holds the player still");
            flow.OpenOptions();
            yield return null;
            Assert.IsTrue(GameFlow.IsPaused, "options opened from the pause menu still count as paused");
            Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator AnEarlyPressFiresWhenTheCurrentMoveEnds()
        {
            yield return Setup();
            foreach (var e in EnemyBrain.Active.ToArray()) e.gameObject.SetActive(false); // swings at nothing
            var light = player.Progression.MoveFor(MoveSlot.Light);
            var heavy = player.Progression.MoveFor(MoveSlot.Heavy);
            Assert.IsTrue(player.RequestAttack(light));
            yield return new WaitForSeconds(light.TotalDuration - 0.12f);
            Assert.AreSame(light, player.Attack.Current, "still finishing the light attack");
            Assert.IsFalse(player.RequestAttack(heavy), "busy, so the heavy is buffered");
            Assert.AreSame(heavy, player.BufferedAttack);
            yield return new WaitForSeconds(0.2f);
            Assert.AreSame(heavy, player.Attack.Current, "the buffered heavy came out as soon as the light ended");

            // A press older than the window is dropped.
            yield return new WaitUntil(() => !player.Attack.IsAttacking);
            Assert.IsTrue(player.RequestAttack(light));
            Assert.IsFalse(player.RequestAttack(heavy));
            yield return new WaitForSeconds(PlayerController.AttackBufferWindow + 0.05f);
            Assert.IsNull(player.BufferedAttack, "too early: the press expired");
        }

        [UnityTest]
        public IEnumerator ALandedHitCancelsIntoAHeavy()
        {
            yield return Setup();
            player.enabled = false;
            var dummy = LoneDummy();
            yield return null;
            var light = player.Progression.MoveFor(MoveSlot.Light);
            var heavy = player.Progression.MoveFor(MoveSlot.Heavy);
            float before = dummy.Health.Current;
            Assert.IsTrue(player.Attack.TryAttack(light));
            Assert.IsFalse(player.Attack.TryAttack(heavy), "no cancel before anything has landed");
            yield return new WaitUntil(() => player.Attack.InCancelWindow || !player.Attack.IsAttacking);
            Assert.Less(dummy.Health.Current, before, "the light attack connected");
            Assert.IsTrue(player.Attack.InCancelWindow, "a connected light opens its recovery to cancels");
            Assert.IsTrue(player.Attack.TryAttack(heavy), "light cancels into heavy");
            Assert.AreSame(heavy, player.Attack.Current);
            Assert.IsFalse(player.Attack.TryAttack(light), "but not back down into a light");
        }

        [UnityTest]
        public IEnumerator HitsDoNotShortenAStun()
        {
            yield return Setup();
            player.enabled = false;
            var dummy = LoneDummy();
            dummy.enabled = true;
            yield return null;
            dummy.Stun(2.2f, Color.cyan);
            dummy.Health.TakeDamage(new DamageInfo { amount = 1f, source = player.gameObject, knockback = Vector3.right });
            yield return new WaitForSeconds(1f);
            Assert.IsTrue(dummy.IsStunned, "a 2.2 s freeze outlasts the 0.4 s flinch of a hit landed on it");
        }

        [UnityTest]
        public IEnumerator TheArenaLocksUntilTheFightIsOver()
        {
            yield return Setup();
            var cam = SideScrollCamera.Main;
            yield return null;
            Assert.IsFalse(flow.Arena.Locked, "nobody engaged at the start");
            var first = EnemyBrain.Active.OrderBy(e => e.transform.position.x).First();
            Teleport(player, first.transform.position + Vector3.left * 2f);
            first.Health.TakeDamage(new DamageInfo { amount = 1f, source = player.gameObject });
            yield return null;
            yield return null;
            Assert.IsTrue(flow.Arena.Locked, "an engaged coworker locks the arena");
            Assert.Less(cam.MaxX, cam.LevelMaxX, "the camera is held inside the arena");

            // Walk right as far as possible: the arena wall stops the player.
            for (float t = 0f; t < 3f; t += Time.deltaTime)
            {
                player.Motor.SetMoveInput(Vector2.right);
                yield return null;
            }
            Assert.LessOrEqual(player.transform.position.x, flow.Arena.Max + 0.05f);

            foreach (var e in EnemyBrain.Active.ToList()) e.Health.TakeDamage(new DamageInfo { amount = 9999f });
            yield return null;
            yield return null;
            Assert.IsFalse(flow.Arena.Locked, "everyone down: the arena opens");
            Assert.AreEqual(cam.LevelMaxX, cam.MaxX, 1e-4f);
        }

        [UnityTest]
        public IEnumerator NoMoreThanTwoCoworkersHoldAttackSlots()
        {
            yield return Setup();
            player.Health.SetMax(100000f, true);
            var crowd = EnemyBrain.Active.Where(e => e.Boss == BossRank.None).Take(5).ToList();
            foreach (var e in EnemyBrain.Active.ToArray())
                if (!crowd.Contains(e)) e.gameObject.SetActive(false);
            for (int i = 0; i < crowd.Count; i++)
            {
                Teleport(crowd[i], player.transform.position + new Vector3(i % 2 == 0 ? 1.5f + i * 0.4f : -1.5f - i * 0.4f, 0f, 0f));
                crowd[i].Health.TakeDamage(new DamageInfo { amount = 0.01f, source = player.gameObject }); // aggro
            }
            int most = 0;
            for (float t = 0f; t < 4f; t += Time.deltaTime)
            {
                most = Mathf.Max(most, EnemyBrain.AttackSlots.Count);
                Assert.LessOrEqual(crowd.Count(e => e.Attack.IsAttacking), EnemyBrain.MaxAttackers, "attacking without a slot");
                yield return null;
            }
            Assert.LessOrEqual(most, EnemyBrain.MaxAttackers);
            Assert.Greater(most, 0, "somebody did get to attack");
        }

        [UnityTest]
        public IEnumerator BannersQueueInsteadOfOverwriting()
        {
            yield return Setup();
            var hud = Object.FindAnyObjectByType<GameHud>();
            // Let the shift-start banner finish first.
            yield return new WaitUntil(() => hud.BannerTitle == "" && hud.QueuedBanners == 0);
            hud.ShowBanner("Promoted!", "You are now a Senior Associate.");
            hud.ShowBanner("New move: Test", "Second banner.");
            yield return null;
            Assert.AreEqual("Promoted!", hud.BannerTitle);
            Assert.AreEqual(1, hud.QueuedBanners);
            yield return new WaitForSecondsRealtime(3.2f);
            Assert.AreEqual("New move: Test", hud.BannerTitle, "the second banner gets its turn");
        }
    }
}
