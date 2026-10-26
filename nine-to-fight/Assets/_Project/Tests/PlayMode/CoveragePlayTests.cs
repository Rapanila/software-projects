using System.Collections;
using System.Linq;
using NineToFight.CameraRig;
using NineToFight.Characters;
using NineToFight.Combat;
using NineToFight.Core;
using NineToFight.Enemies;
using NineToFight.Fx;
using NineToFight.Game;
using NineToFight.Player;
using NineToFight.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace NineToFight.Tests
{
    /// <summary>Play-mode coverage: icons, effects pools, the follow camera, the CEO fight and Support/Charger roles.</summary>
    public class CoveragePlayTests
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

        static void KillAll()
        {
            foreach (var e in EnemyBrain.Active.ToArray()) e.Health.TakeDamage(new DamageInfo { amount = 9999f });
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

        // ------------------------------------------------------------------ prop icons

        [UnityTest]
        public IEnumerator EveryCatalogPropRendersAnIcon()
        {
            yield return Setup();
            var catalog = Object.FindObjectsByType<HeldItemDisplay>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Select(h => h.Catalog).First(c => c != null);
            Assert.Greater(catalog.Entries.Count, 5, "catalog has items");

            Assert.IsNull(PropIcons.Get(null, "stapler"), "no catalog");
            Assert.IsNull(PropIcons.Get(catalog, ""), "empty id");
            Assert.IsNull(PropIcons.Get(catalog, null), "null id");
            Assert.IsNull(PropIcons.Get(catalog, "definitely_not_an_item"), "unknown id");

            foreach (var entry in catalog.Entries)
            {
                if (entry.prefab == null) continue;
                var icon = PropIcons.Get(catalog, entry.id);
                Assert.IsNotNull(icon, $"icon for {entry.id}");
                Assert.AreEqual(128, icon.width, entry.id);
                Assert.AreEqual(128, icon.height, entry.id);
                Assert.AreSame(icon, PropIcons.Get(catalog, entry.id), $"{entry.id} is cached");
                var px = icon.GetPixels32();
                Assert.Greater(px.Count(p => p.a > 200), 30, $"{entry.id} has visible pixels");
                Assert.Greater(px.Count(p => p.a < 10), 1000, $"{entry.id} has a transparent backdrop");
            }
        }

        // ------------------------------------------------------------------ hit effects and screen fx

        static int ActiveChildren(Transform root, string name) =>
            root.Cast<Transform>().Count(t => t.name == name && t.gameObject.activeSelf);

        static int Children(Transform root, string name) => root.Cast<Transform>().Count(t => t.name == name);

        [UnityTest]
        public IEnumerator HitEffectsActivatePooledObjects()
        {
            yield return Setup();
            var root = GameObject.Find("Hit Effects");
            Assert.IsNotNull(root, "pool exists after the scene starts");
            var t = root.transform;
            Assert.AreEqual(160, Children(t, "Scrap"));
            Assert.AreEqual(24, Children(t, "Flash"));
            Assert.AreEqual(48, Children(t, "Dust"));
            Assert.AreEqual(24, Children(t, "Popup"));

            int scrap = ActiveChildren(t, "Scrap"), flash = ActiveChildren(t, "Flash");
            int dust = ActiveChildren(t, "Dust"), popup = ActiveChildren(t, "Popup");
            var at = player.transform.position + Vector3.up;

            HitEffects.Burst(at, Vector3.right, Color.red, 10);
            HitEffects.Flash(at, Color.white);
            HitEffects.Dust(at, 0.5f, 3);
            HitEffects.Text(at, "42", Color.yellow);

            Assert.AreEqual(scrap + 10, ActiveChildren(t, "Scrap"));
            Assert.AreEqual(flash + 1, ActiveChildren(t, "Flash"));
            Assert.AreEqual(dust + 3, ActiveChildren(t, "Dust"));
            Assert.AreEqual(popup + 1, ActiveChildren(t, "Popup"));
            var shown = t.Cast<Transform>().Where(c => c.name == "Popup" && c.gameObject.activeSelf)
                .Select(c => c.GetComponent<TextMesh>().text);
            CollectionAssert.Contains(shown.ToArray(), "42");

            // They expire on their own (longest life is the popup's 0.8 s).
            for (float wait = 0f; wait < 3f && (ActiveChildren(t, "Scrap") + ActiveChildren(t, "Flash") +
                                                 ActiveChildren(t, "Dust") + ActiveChildren(t, "Popup")) > popup + scrap + flash + dust; wait += Time.deltaTime)
                yield return null;
            Assert.LessOrEqual(ActiveChildren(t, "Scrap"), scrap);
            Assert.LessOrEqual(ActiveChildren(t, "Flash"), flash);
            Assert.LessOrEqual(ActiveChildren(t, "Dust"), dust);
            Assert.LessOrEqual(ActiveChildren(t, "Popup"), popup);
        }

        [UnityTest]
        public IEnumerator HitEffectPoolsRecycleInsteadOfGrowing()
        {
            yield return Setup();
            var t = GameObject.Find("Hit Effects").transform;
            int children = t.childCount;
            var at = player.transform.position + Vector3.up;

            for (int i = 0; i < 200; i++)
            {
                HitEffects.Burst(at, Vector3.left, Color.cyan, 5);
                HitEffects.Flash(at, Color.white, 1f);
                HitEffects.Dust(at, 0.4f, 2, Vector3.right);
                HitEffects.Text(at, i.ToString(), Color.white, 1.2f, 0.5f);
            }
            Assert.AreEqual(children, t.childCount, "no new objects were created");
            Assert.LessOrEqual(ActiveChildren(t, "Scrap"), 160);
            Assert.LessOrEqual(ActiveChildren(t, "Flash"), 24);
            Assert.LessOrEqual(ActiveChildren(t, "Dust"), 48);
            Assert.LessOrEqual(ActiveChildren(t, "Popup"), 24);
            Assert.AreEqual(160, ActiveChildren(t, "Scrap"), "the whole scrap pool is in use");

            for (float wait = 0f; wait < 3f && t.Cast<Transform>().Any(c => c.gameObject.activeSelf); wait += Time.deltaTime)
                yield return null;
            Assert.IsFalse(t.Cast<Transform>().Any(c => c.gameObject.activeSelf), "everything returned to the pool");
            Assert.AreEqual(children, t.childCount);
        }

        [UnityTest]
        public IEnumerator HitEffectsAndScreenFxAcceptEdgeCases()
        {
            yield return Setup();
            var at = player.transform.position;
            Assert.DoesNotThrow(() =>
            {
                HitEffects.Burst(at, Vector3.zero, Color.white, 0);
                HitEffects.Dust(at, 0.5f, 0);
                HitEffects.Text(at, "", Color.white);
                ScreenFx.Kick(0f);
                ScreenFx.Kick(0.7f);
                ScreenFx.Kick(50f);
                ScreenFx.Kick(-3f);
            });
            Assert.IsNotNull(flow.GetComponent<ScreenFx>(), "the game flow carries the screen fx");
            Assert.IsNotNull(flow.transform.Find("Screen FX Volume"), "post-processing volume created");
            yield return null;
            yield return null; // Update/LateUpdate of both run without errors
        }

        // ------------------------------------------------------------------ camera

        static float CamX => SideScrollCamera.Main.transform.position.x;

        IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime) yield return null;
        }

        [UnityTest]
        public IEnumerator CameraFollowsThePlayerAlongX()
        {
            yield return Setup();
            var cam = SideScrollCamera.Main;
            Assert.IsNotNull(cam);
            cam.SetTarget(player.transform);
            SideScrollCamera.SetShot(CameraShot.Follow);
            Teleport(player, new Vector3(10f, 0f, 0f));
            yield return Wait(1.2f);
            float before = CamX;
            Assert.AreEqual(10f, before, 3f, "settled near the player");

            Teleport(player, new Vector3(20f, 0f, 0f));
            yield return Wait(0.1f);
            Assert.Less(CamX, 20f - 1f, "still catching up shortly after the move");
            yield return Wait(1.2f);
            Assert.Greater(CamX, before + 8f, "moved toward the player");
            Assert.AreEqual(20f, CamX, 3f, "within the look-ahead of the player");
        }

        [UnityTest]
        public IEnumerator CameraNeverCentresBeyondTheBoundsPlusThree()
        {
            yield return Setup();
            var cam = SideScrollCamera.Main;
            cam.SetTarget(player.transform);
            SideScrollCamera.SetShot(CameraShot.Follow);

            Teleport(player, new Vector3(cam.MaxX + 40f, 0f, 0f));
            yield return Wait(1.5f);
            Assert.LessOrEqual(CamX, cam.MaxX + 3f + 0.05f, "clamped on the right");
            Assert.Greater(CamX, cam.MaxX, "but pushed out to the limit");

            Teleport(player, new Vector3(cam.MinX - 40f, 0f, 0f));
            yield return Wait(1.5f);
            Assert.GreaterOrEqual(CamX, cam.MinX - 3f - 0.05f, "clamped on the left");
            Assert.Less(CamX, cam.MinX, "but pushed out to the limit");
        }

        [UnityTest]
        public IEnumerator CameraShakeOffsetsThenSettles()
        {
            yield return Setup();
            if (GameSettings.ScreenShake <= 0f) Assert.Ignore("Screen shake is switched off in the settings");
            var cam = SideScrollCamera.Main;
            cam.SetTarget(player.transform);
            SideScrollCamera.SetShot(CameraShot.Follow);
            Teleport(player, new Vector3(10f, 0f, 0f));
            yield return Wait(1.5f);
            Vector3 rest = cam.transform.position;

            SideScrollCamera.Shake(2f, 0.4f);
            float maxOffset = 0f;
            for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime)
            {
                yield return null;
                maxOffset = Mathf.Max(maxOffset, (cam.transform.position - rest).magnitude);
            }
            Assert.Greater(maxOffset, 0.05f, "the camera visibly shook");
            Assert.LessOrEqual(maxOffset, 2f * GameSettings.ScreenShake + 0.5f, "within the requested amplitude");

            yield return Wait(1.2f);
            Assert.Less((cam.transform.position - rest).magnitude, 0.1f, "settled back where it was");
        }

        [UnityTest]
        public IEnumerator SetShotChangesTheCameraShot()
        {
            yield return Setup();
            var cam = SideScrollCamera.Main;
            SideScrollCamera.SetShot(CameraShot.Showcase);
            Assert.AreEqual(CameraShot.Showcase, cam.Shot);
            SideScrollCamera.SetShot(CameraShot.Attract);
            Assert.AreEqual(CameraShot.Attract, cam.Shot);
            SideScrollCamera.SetShot(CameraShot.Follow);
            Assert.AreEqual(CameraShot.Follow, cam.Shot);
        }

        [UnityTest]
        public IEnumerator CameraBoundsCanBeNarrowedAndRestored()
        {
            yield return Setup();
            var cam = SideScrollCamera.Main;
            float min = cam.LevelMinX, max = cam.LevelMaxX;
            cam.SetBounds(10f, 20f);
            Assert.AreEqual(10f, cam.MinX);
            Assert.AreEqual(20f, cam.MaxX);
            cam.SetBounds(15f, 5f);
            Assert.AreEqual(15f, cam.MaxX, "max never drops below min");
            cam.ResetBounds();
            Assert.AreEqual(min, cam.MinX);
            Assert.AreEqual(max, cam.MaxX);
        }

        // ------------------------------------------------------------------ the CEO

        [UnityTest]
        public IEnumerator BeatingTheCeoEndsTheGameAndRemembersIt()
        {
            const string key = "NineToFight.BecameCeo";
            bool had = PlayerPrefs.HasKey(key);
            int previous = PlayerPrefs.GetInt(key, 0);
            try
            {
                PlayerPrefs.DeleteKey(key);
                yield return Setup();
                KillAll();
                yield return null;

                for (int floor = 2; floor <= TowerPlan.FloorCount; floor++)
                {
                    TowerDirector.ForceEvent = FloorEvent.None;
                    flow.NextFloor();
                    Assert.AreEqual(floor, flow.Floor);
                    if (floor < TowerPlan.FloorCount)
                    {
                        Assert.IsFalse(EnemyBrain.Active.Any(e => e.Boss == BossRank.FinalBoss), $"no CEO on floor {floor}");
                        KillAll();
                        yield return null;
                    }
                }

                Assert.AreEqual(TowerPlan.FloorCount, flow.Floor);
                var ceo = EnemyBrain.Active.FirstOrDefault(e => e.Boss == BossRank.FinalBoss && !e.Health.IsDead);
                Assert.IsNotNull(ceo, "the CEO waits on floor 9");
                Assert.AreEqual(1, EnemyBrain.Active.Count(e => e.Boss == BossRank.FinalBoss));
                Assert.AreEqual(EnemyRole.Brawler, ceo.Role, "bosses use their own pattern");
                Assert.AreEqual(1.6f, ceo.transform.localScale.x, 0.01f, "the CEO is big");
                Assert.IsFalse(GameFlow.HasBecomeCeo, "not yet");

                KillAll();
                for (float t = 0f; t < 4f && flow.State != GameState.FloorCleared; t += Time.unscaledDeltaTime) yield return null;
                Assert.AreEqual(GameState.FloorCleared, flow.State);

                yield return WalkIntoElevator();
                Assert.AreEqual(GameState.Ending, flow.State);
                Assert.AreEqual(1, PlayerPrefs.GetInt(key, 0));
                Assert.IsTrue(GameFlow.HasBecomeCeo);
                Assert.IsEmpty(flow.PerkOffer ?? new System.Collections.Generic.List<Perk>(), "no benefits package for the CEO");
            }
            finally
            {
                if (had) PlayerPrefs.SetInt(key, previous);
                else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
        }

        // ------------------------------------------------------------------ Support and Charger

        /// <summary>A runtime copy of an enemy's class with the given role and tweaks, applied to that enemy.</summary>
        static void Reclass(EnemyBrain e, EnemyRole role, float moveSpeed, BossRank boss = BossRank.None)
        {
            var w = Object.Instantiate(e.WorkerClass);
            w.name = "Test " + role;
            w.role = role;
            w.attack = w.attack.Clone();
            w.aggroRange = 40f;
            w.maxHealth = 100f;
            w.moveSpeed = moveSpeed;
            if (boss == BossRank.None) e.Configure(w); else e.Configure(w, boss);
        }

        [UnityTest]
        public IEnumerator ChargerThatMissesIsLeftWinded()
        {
            yield return Setup();
            var all = EnemyBrain.Active.ToArray();
            var charger = all.First(e => e.Boss == BossRank.None);
            foreach (var e in all) if (e != charger) e.gameObject.SetActive(false);
            Reclass(charger, EnemyRole.Charger, 0.3f); // creeps, so it is still 3-9 m away when it decides to charge
            Assert.AreEqual(EnemyRole.Charger, charger.Role);
            Teleport(charger, new Vector3(30f, 0f, 0f));
            Teleport(player, new Vector3(23f, 0f, 0f));
            charger.Motor.FaceTowards(player.transform.position);
            float hp = player.Health.Current;
            Assert.IsFalse(charger.IsStunned);

            bool charging = false;
            for (float t = 0f; t < 5f && !charging; t += Time.deltaTime)
            {
                yield return null;
                charging = charger.Attack.IsAttacking;
            }
            Assert.IsTrue(charging, "the charger winds up from range");
            Assert.IsFalse(charger.IsStunned, "not winded until the rush ends");

            // The player is gone by the time it rushes: a guaranteed miss.
            Teleport(player, new Vector3(60f, 0f, 0f));
            bool winded = false;
            for (float t = 0f; t < 3f && !winded; t += Time.deltaTime)
            {
                yield return null;
                winded = charger.IsStunned;
            }
            Assert.IsTrue(winded, "a missed charge leaves it stunned");
            Assert.AreEqual(hp, player.Health.Current, "the player was never hit");
        }

        [UnityTest]
        public IEnumerator SupportRalliesWoundedBrawlersButNotBossesOrOtherSupports()
        {
            yield return Setup();
            var all = EnemyBrain.Active.Where(e => e.Boss == BossRank.None).ToArray();
            Assert.GreaterOrEqual(all.Length, 4, "floor 1 has enough ordinary coworkers to stage the scene");
            foreach (var b in EnemyBrain.Active.Where(x => x.Boss != BossRank.None).ToArray()) b.gameObject.SetActive(false);
            var support = all[0];
            var brawler = all[1];
            var boss = all[2];
            var otherSupport = all[3];
            foreach (var e in all.Skip(4)) e.gameObject.SetActive(false);

            Reclass(support, EnemyRole.Support, 1f);
            Reclass(brawler, EnemyRole.Brawler, 0.1f);
            Reclass(boss, EnemyRole.Brawler, 0.1f, BossRank.MiniBoss);
            Reclass(otherSupport, EnemyRole.Support, 0.1f);
            Assert.AreEqual(EnemyRole.Support, support.Role);
            Assert.AreEqual(EnemyRole.Brawler, brawler.Role);
            Assert.AreEqual(EnemyRole.Support, otherSupport.Role);
            Assert.AreEqual(BossRank.MiniBoss, boss.Boss);

            Teleport(support, new Vector3(30f, 0f, 0f));
            Teleport(brawler, new Vector3(31.5f, 0f, 0.5f));
            Teleport(boss, new Vector3(32f, 0f, -0.5f));
            Teleport(otherSupport, new Vector3(31f, 0f, 1f));
            Teleport(player, new Vector3(24f, 0f, 0f));

            foreach (var e in new[] { brawler, boss, otherSupport })
            {
                e.Health.SetMax(100f, true);
                e.Health.TakeDamage(new DamageInfo { amount = 40f });
                Assert.AreEqual(60f, e.Health.Current, 0.01f, e.name + " wounded");
                e.Stun(30f, Color.white); // keep the audience in place, still in the rally radius
            }

            bool healed = false;
            for (float t = 0f; t < 6f && !healed; t += Time.deltaTime)
            {
                yield return null;
                healed = brawler.Health.Current > 60.01f;
            }
            Assert.IsTrue(healed, "the support rallied within a few seconds");
            Assert.AreEqual(85f, brawler.Health.Current, 0.5f, "a rally heals 25% of max health");
            Assert.IsTrue(brawler.IsBuffed, "and buffs damage");
            Assert.AreEqual(60f, boss.Health.Current, 0.01f, "bosses are never rallied");
            Assert.IsFalse(boss.IsBuffed);
            Assert.AreEqual(60f, otherSupport.Health.Current, 0.01f, "supports don't heal each other");
            Assert.IsFalse(otherSupport.IsBuffed);
        }
    }
}
