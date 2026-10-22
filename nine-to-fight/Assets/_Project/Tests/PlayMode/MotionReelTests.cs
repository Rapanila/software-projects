using System.Collections;
using System.IO;
using System.Linq;
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
    /// <summary>
    /// Records consecutive frames of the core motions (walk, stance, attacks, hit, jump) so the
    /// animation can be reviewed as a flip-book. Frames are named with their game time.
    /// </summary>
    public class MotionReelTests
    {
        static readonly string Dir = Path.GetFullPath("Builds/Screenshots/Motion");

        [UnityTest]
        public IEnumerator RecordMotionReel()
        {
            if (Directory.Exists(Dir)) Directory.Delete(Dir, true);
            Directory.CreateDirectory(Dir);

            SceneManager.LoadScene("Floor01_CubicleFarm");
            yield return null;
            yield return null;
            var flow = GameFlow.Instance;
            flow.StartShiftWithClass(2); // Sales Rep: suit, tie, big swings
            var player = flow.Player;
            player.enabled = false;
            var motor = player.Motor;
            motor.CanMove = true;
            foreach (var e in EnemyBrain.Active.ToArray()) e.gameObject.SetActive(false);
            float start = Time.time;
            int frame = 0;

            IEnumerator Record(string clip, float seconds, System.Action<float> drive)
            {
                float t0 = Time.time;
                while (Time.time - t0 < seconds)
                {
                    drive?.Invoke(Time.time - t0);
                    yield return new WaitForEndOfFrame();
                    var tex = ScreenCapture.CaptureScreenshotAsTexture();
                    File.WriteAllBytes(Path.Combine(Dir, $"{clip}_{frame++:0000}_{(Time.time - start) * 1000f:000000}.png"), tex.EncodeToPNG());
                    Object.Destroy(tex);
                }
            }

            yield return Record("1walk", 1.6f, t => motor.SetMoveInput(t < 1.2f ? Vector2.right : Vector2.zero));

            // A coworker shows up: guard goes up.
            player.Visual.InCombat = true;
            yield return Record("2stance", 1.2f, null);

            player.Attack.TryAttack(player.Progression.MoveFor(MoveSlot.Light));
            yield return Record("3light", 0.8f, null);
            player.Attack.TryAttack(player.Progression.MoveFor(MoveSlot.Heavy));
            yield return Record("4heavy", 1.1f, null);

            player.Health.TakeDamage(new DamageInfo { amount = 5f, knockback = Vector3.left * 8f + Vector3.up * 2f, hitStop = 0.03f });
            yield return Record("5hit", 0.9f, null);

            motor.RequestJump();
            yield return Record("6jump", 1.1f, null);

            Assert.Greater(frame, 40, "recorded enough frames to review");
        }
    }
}
