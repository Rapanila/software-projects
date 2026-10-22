using System.Collections;
using System.IO;
using System.Linq;
using NineToFight.CameraRig;
using NineToFight.Characters;
using NineToFight.Combat;
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
    /// Films every attack style in close-up from the side at a fixed 30 fps (game time advances one
    /// frame per capture), so the choreography can be reviewed frame by frame.
    /// </summary>
    public class AttackReelTests
    {
        static readonly string Dir = Path.GetFullPath("Builds/Screenshots/AttackReel");

        [UnityTest]
        public IEnumerator FilmEveryAttackStyle()
        {
            Directory.CreateDirectory(Dir);
            foreach (var old in Directory.GetFiles(Dir, "*.png")) File.Delete(old);
            TowerDirector.ForceEvent = FloorEvent.None;
            SceneManager.LoadScene("Floor01_CubicleFarm");
            yield return null;
            yield return null;

            var flow = GameFlow.Instance;
            flow.StartShiftWithClass(2);
            var player = flow.Player;
            player.enabled = false;
            foreach (var e in EnemyBrain.Active.ToArray()) e.gameObject.SetActive(false);
            Object.FindAnyObjectByType<GameHud>().gameObject.SetActive(false);
            var cam = Camera.main;
            cam.GetComponent<SideScrollCamera>().enabled = false;
            var at = player.transform.position;
            cam.transform.position = at + new Vector3(0.35f, 1.35f, -4.6f);
            cam.transform.rotation = Quaternion.LookRotation(at + new Vector3(0.35f, 1.15f, 0f) - cam.transform.position);
            player.Visual.InCombat = true;
            yield return new WaitForSeconds(1f);

            int frames = 0;
            Time.captureDeltaTime = 1f / 30f;
            try
            {
                foreach (AttackStyle style in System.Enum.GetValues(typeof(AttackStyle)))
                {
                    var move = new FightingMove { style = style, startup = 0.3f, active = 0.12f, recovery = 0.4f };
                    player.Visual.PlayAttack(move);
                    for (int f = 0; f < 30; f++)
                    {
                        yield return new WaitForEndOfFrame();
                        var tex = ScreenCapture.CaptureScreenshotAsTexture();
                        File.WriteAllBytes(Path.Combine(Dir, $"{style}_{f:00}.png"), tex.EncodeToPNG());
                        Object.Destroy(tex);
                        frames++;
                    }
                }
            }
            finally
            {
                Time.captureDeltaTime = 0f;
            }
            Assert.Greater(frames, 100);
        }

        /// <summary>Films every flurry as a combo: each hit its own strike, flowing on from the last.</summary>
        [UnityTest]
        public IEnumerator FilmTheCombos()
        {
            string dir = Path.GetFullPath("Builds/Screenshots/ComboReel");
            Directory.CreateDirectory(dir);
            foreach (var old in Directory.GetFiles(dir, "*.png")) File.Delete(old);
            TowerDirector.ForceEvent = FloorEvent.None;
            SceneManager.LoadScene("Floor01_CubicleFarm");
            yield return null;
            yield return null;

            var flow = GameFlow.Instance;
            flow.StartShiftWithClass(2);
            var player = flow.Player;
            player.enabled = false;
            foreach (var e in EnemyBrain.Active.ToArray()) e.gameObject.SetActive(false);
            Object.FindAnyObjectByType<GameHud>().gameObject.SetActive(false);
            var cam = Camera.main;
            cam.GetComponent<SideScrollCamera>().enabled = false;
            var at = player.transform.position;
            cam.transform.position = at + new Vector3(0.35f, 1.35f, -4.6f);
            cam.transform.rotation = Quaternion.LookRotation(at + new Vector3(0.35f, 1.15f, 0f) - cam.transform.position);
            player.Visual.InCombat = true;
            yield return new WaitForSeconds(1f);

            var flurries = ClassMoveBook.ClassIds.SelectMany(ClassMoveBook.For).Concat(MoveLibrary.Shared).Where(m => m.hits > 1).ToList();
            Assert.IsNotEmpty(flurries);
            int saved = 0;
            Time.captureDeltaTime = 1f / 30f;
            try
            {
                foreach (var move in flurries)
                {
                    int frame = 0;
                    for (int hit = 0; hit < move.hits; hit++)
                    {
                        player.Visual.PlayAttack(move, move.StyleForHit(hit), hit < move.hits - 1);
                        int frames = Mathf.CeilToInt((move.startup + move.active + move.recovery) * 30f);
                        for (int f = 0; f < frames; f++, frame++)
                        {
                            yield return new WaitForEndOfFrame();
                            if (frame % 2 != 0) continue;
                            var tex = ScreenCapture.CaptureScreenshotAsTexture();
                            File.WriteAllBytes(Path.Combine(dir, $"{move.id}_{frame / 2:00}.png"), tex.EncodeToPNG());
                            Object.Destroy(tex);
                            saved++;
                        }
                    }
                    for (int f = 0; f < 12; f++) yield return null; // settle between combos
                }
            }
            finally
            {
                Time.captureDeltaTime = 0f;
            }
            Assert.Greater(saved, 50);
        }

        /// <summary>
        /// Films the fighting stance: the guard idling, every fidget, the block going up and taking a hit,
        /// from the side and from a three-quarter view. Every other frame at 30 fps.
        /// </summary>
        [UnityTest]
        public IEnumerator FilmTheStance()
        {
            string dir = Path.GetFullPath("Builds/Screenshots/StanceReel");
            Directory.CreateDirectory(dir);
            foreach (var old in Directory.GetFiles(dir, "*.png")) File.Delete(old);
            TowerDirector.ForceEvent = FloorEvent.None;
            SceneManager.LoadScene("Floor01_CubicleFarm");
            yield return null;
            yield return null;

            var flow = GameFlow.Instance;
            flow.StartShiftWithClass(2);
            var player = flow.Player;
            player.enabled = false;
            foreach (var e in EnemyBrain.Active.ToArray()) e.gameObject.SetActive(false);
            Object.FindAnyObjectByType<GameHud>().gameObject.SetActive(false);
            var cam = Camera.main;
            cam.GetComponent<SideScrollCamera>().enabled = false;
            var visual = player.Visual;
            visual.InCombat = true;
            // Let the shift settle (spawn point, floor event) before framing the shot.
            yield return new WaitForSeconds(1f);
            var at = player.transform.position;

            int saved = 0;
            Time.captureDeltaTime = 1f / 30f;
            try
            {
                IEnumerator Film(string clip, int frames, System.Action start = null)
                {
                    start?.Invoke();
                    for (int f = 0; f < frames; f++)
                    {
                        yield return new WaitForEndOfFrame();
                        if (f % 2 != 0) continue;
                        var tex = ScreenCapture.CaptureScreenshotAsTexture();
                        File.WriteAllBytes(Path.Combine(dir, $"{clip}_{f / 2:00}.png"), tex.EncodeToPNG());
                        Object.Destroy(tex);
                        saved++;
                    }
                }

                foreach (var (view, offset, look) in new[]
                {
                    ("side", new Vector3(0.35f, 1.35f, -4.6f), new Vector3(0.35f, 1.15f, 0f)),
                    ("quarter", new Vector3(2.3f, 1.7f, -3.2f), new Vector3(0.1f, 1.25f, 0f)),
                })
                {
                    cam.transform.position = at + offset;
                    cam.transform.rotation = Quaternion.LookRotation(at + look - cam.transform.position);
                    visual.Blocking = false;
                    yield return new WaitForSeconds(0.6f);
                    yield return Film($"{view}_idle", 72);
                    foreach (FightStance.Fidget fidget in System.Enum.GetValues(typeof(FightStance.Fidget)))
                        if (fidget != FightStance.Fidget.None)
                            yield return Film($"{view}_fidget_{fidget}", 36, () => visual.PlayFidget(fidget));
                    yield return Film($"{view}_block_up", 24, () => visual.Blocking = true);
                    yield return Film($"{view}_block_hit", 24, () => visual.PlayBlockReaction(Vector3.left * 4f));
                    yield return Film($"{view}_block_down", 24, () => visual.Blocking = false);
                }
            }
            finally
            {
                Time.captureDeltaTime = 0f;
            }
            Assert.Greater(saved, 100);
        }
    }
}
