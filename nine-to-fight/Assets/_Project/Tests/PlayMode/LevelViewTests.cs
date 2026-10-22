using System.Collections;
using System.IO;
using System.Linq;
using NineToFight.Enemies;
using NineToFight.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace NineToFight.Tests
{
    /// <summary>
    /// Walks the player to both ends of the floor with the gameplay camera and captures what it sees (the view
    /// must stay inside the office), plus each wall poster, and the department screen after switching.
    /// </summary>
    public class LevelViewTests
    {
        static readonly string Dir = Path.GetFullPath("Builds/Screenshots/Level");

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
        public IEnumerator EndsPostersAndShowcase()
        {
            Directory.CreateDirectory(Dir);
            TowerDirector.ForceEvent = FloorEvent.None;
            SceneManager.LoadScene("Floor01_CubicleFarm");
            yield return null;
            yield return null;
            var flow = GameFlow.Instance;

            // The department screen: switch twice; the character must stay on its mark.
            flow.OpenClassSelect();
            yield return new WaitForSecondsRealtime(1.2f);
            var mark = flow.Player.transform.position;
            flow.SelectClass(1);
            yield return new WaitForSecondsRealtime(1.4f);
            flow.SelectClass(3);
            yield return new WaitForSecondsRealtime(1.4f);
            yield return Capture("department_after_switching");
            Assert.Less(Vector3.Distance(flow.Player.transform.position, mark), 0.05f, "showcase swings don't walk the character off its mark");

            flow.ConfirmClass();
            var player = flow.Player;
            foreach (var e in EnemyBrain.Active.ToArray()) e.gameObject.SetActive(false);
            player.Health.MakeInvulnerable(60f);

            foreach (var (name, x) in new[] { ("left_end", -3.5f), ("right_end", 55.5f) })
            {
                Teleport(player, new Vector3(x, 0f, 0f));
                yield return new WaitForSeconds(1.5f);
                yield return Capture(name);
            }

            var cam = Camera.main;
            cam.GetComponent<CameraRig.SideScrollCamera>().enabled = false;
            int i = 0;
            foreach (var poster in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t.name == "Poster"))
            {
                cam.transform.position = poster.position + new Vector3(0f, 0f, -2.2f);
                cam.transform.rotation = Quaternion.LookRotation(Vector3.forward);
                yield return Capture($"poster_{i++}");
            }
            Assert.Greater(i, 0, "posters found");
        }
    }
}
