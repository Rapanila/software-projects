using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NineToFight.Characters;
using NineToFight.CameraRig;
using NineToFight.Combat;
using NineToFight.Core;
using NineToFight.Enemies;
using NineToFight.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace NineToFight.Tests
{
    public class DeathAnimationTests
    {
        [UnityTest]
        public IEnumerator KillingHitsStartTheFallPromptlyEvenDuringAnAttack()
        {
            TowerDirector.ForceEvent = FloorEvent.None;
            SceneManager.LoadScene("Floor01_CubicleFarm");
            yield return null;
            yield return null;
            var flow = GameFlow.Instance;
            flow.StartShiftWithClass(2);
            flow.Player.enabled = false;
            var crowd = EnemyBrain.Active.ToArray();
            var template = crowd[0];
            foreach (var other in crowd) other.gameObject.SetActive(false);
            Directory.CreateDirectory("Builds/Screenshots/Death");
            var camera = Camera.main;
            camera.GetComponent<SideScrollCamera>().enabled = false;
            camera.cullingMask = 1 << 28;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.14f, 0.18f);
            camera.orthographic = true;
            camera.orthographicSize = 1.25f;
            camera.transform.SetPositionAndRotation(new Vector3(24f, 0.9f, -2f), Quaternion.LookRotation(Vector3.left));
            var problems = new List<string>();
            var report = new System.Text.StringBuilder();

            for (int body = 0; body < 2; body++)
            foreach (bool attacking in new[] { false, true })
            {
                var actor = Object.Instantiate(template, new Vector3(20f, 0f, -2f), Quaternion.identity);
                actor.gameObject.SetActive(true);
                actor.enabled = false;
                actor.Motor.enabled = false;
                actor.GetComponent<CharacterController>().enabled = false;
                yield return null;
                actor.Visual.GetComponent<CharacterLook>().Apply(17, false, body == 0 ? "Man_Casual" : "Woman_Blouse");
                actor.Visual.InCombat = true;
                foreach (var part in actor.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = 28;
                Time.timeScale = 1f;
                yield return new WaitForSeconds(0.7f);
                if (attacking)
                {
                    Assert.IsTrue(actor.Attack.TryAttack(new FightingMove { id = "death-timing-test", startup = 0.7f, active = 0.1f, recovery = 0.5f, style = AttackStyle.Slam }));
                    yield return new WaitForSeconds(0.2f);
                    Assert.IsTrue(actor.Attack.IsAttacking, "kill the actor during the live wind-up");
                }
                var animator = actor.Visual.GetComponent<RigFollower>().ActiveRig.animator;
                var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                var head = animator.GetBoneTransform(HumanBodyBones.Head);
                float diedAt = Time.time;
                actor.Health.TakeDamage(new DamageInfo { amount = 99999f, heavy = true, hitStop = 0.06f });
                Assert.IsTrue(actor.Health.IsDead);
                Assert.IsFalse(actor.Attack.IsAttacking, "the killing hit interrupts the attack immediately");
                float fallAt = float.PositiveInfinity;
                float headAfterFall = float.PositiveInfinity;
                int nextSample = 0;
                for (float t = 0f; t < 1.8f; t = Time.time - diedAt)
                {
                    yield return new WaitForEndOfFrame();
                    float lean = Vector3.Angle(Vector3.up, head.position - hips.position);
                    if (lean > 25f) fallAt = Mathf.Min(fallAt, Time.time - diedAt);
                    if (t >= 1.6f) headAfterFall = Mathf.Min(headAfterFall, head.position.y - actor.transform.position.y);
                    if (t >= nextSample * 0.2f)
                    {
                        report.AppendLine($"body {body}, attack {attacking}, t={t:0.00}: hips {hips.position.y:0.000}, head {head.position.y:0.000}, lean {lean:0.0}");
                        Capture(camera, $"body_{body}_attack_{attacking}_{nextSample * 200:0000}ms");
                        nextSample++;
                    }
                }
                report.AppendLine($"body {body}, attack {attacking}: visible fall at {fallAt:0.000}s");
                if (fallAt > 0.3f) problems.Add($"body {body}, attack {attacking}: fall only becomes visible at {fallAt:0.00}s");
                if (headAfterFall > 0.75f) problems.Add($"body {body}, attack {attacking}: fall has not completed after 1.6 seconds (head {headAfterFall:0.00}m)");
                Object.Destroy(actor.gameObject);
                yield return null;
            }
            Debug.Log("DEATH TIMING\n" + report);
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        static void Capture(Camera camera, string name)
        {
            var previous = RenderTexture.active;
            var previousTarget = camera.targetTexture;
            var target = RenderTexture.GetTemporary(640, 480, 24);
            var image = new Texture2D(640, 480, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 640, 480), 0, 0);
                image.Apply();
                File.WriteAllBytes($"Builds/Screenshots/Death/{name}.png", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Object.Destroy(image);
            }
        }
    }
}
