using System.Collections;
using System.IO;
using System.Linq;
using NineToFight.CameraRig;
using NineToFight.Characters;
using NineToFight.Enemies;
using NineToFight.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace NineToFight.Tests
{
    /// <summary>
    /// Lines up one coworker per outfit in front of the camera, relaxed and then squared up for a
    /// fight, and checks every body is skinned and keeps its item in its fist. Captures each line-up.
    /// </summary>
    public class CharacterLineupTests
    {
        static readonly string Dir = Path.GetFullPath("Builds/Screenshots/Characters");

        static IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(Dir, name + ".png"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator EveryOutfitStandsUpAndHoldsItsItem()
        {
            Directory.CreateDirectory(Dir);
            SceneManager.LoadScene("Floor01_CubicleFarm");
            yield return null;
            yield return null;
            Object.FindAnyObjectByType<GameHud>().gameObject.SetActive(false);
            var cam = Camera.main;
            cam.GetComponent<SideScrollCamera>().enabled = false;

            var crowd = EnemyBrain.Active.ToArray();
            var outfits = crowd[0].Visual.GetComponent<CharacterLook>().OutfitNames.ToArray();
            Assert.GreaterOrEqual(outfits.Length, 6, "suits and office casual for both bodies");
            Assert.GreaterOrEqual(crowd.Length, outfits.Length);

            const float centerX = 20f, spacing = 1.05f;
            var line = crowd.Take(outfits.Length).ToArray();
            foreach (var e in crowd.Skip(outfits.Length)) e.gameObject.SetActive(false);
            for (int i = 0; i < line.Length; i++)
            {
                var e = line[i];
                e.enabled = false;
                e.GetComponent<CharacterMotor2_5D>().enabled = false;
                var cc = e.GetComponent<CharacterController>();
                cc.enabled = false;
                e.transform.position = new Vector3(centerX + (i - (line.Length - 1) * 0.5f) * spacing, 0f, -2f);
                e.transform.rotation = Quaternion.Euler(0f, 200f, 0f);
                e.Visual.transform.localRotation = Quaternion.identity; // undo the motor's sideways facing
                var look = e.Visual.GetComponent<CharacterLook>();
                look.Apply(7919 * (i + 1), false, outfits[i]);
                Assert.AreEqual(outfits[i], look.OutfitName);
            }

            cam.transform.position = new Vector3(centerX, 1.5f, -7.6f);
            cam.transform.rotation = Quaternion.LookRotation(new Vector3(centerX, 1.15f, -2f) - cam.transform.position);
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Capture("lineup_rest");

            foreach (var e in line) e.Visual.InCombat = true;
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Capture("lineup_stance");

            foreach (var e in line)
            {
                var skinned = e.Visual.GetComponentsInChildren<SkinnedMeshRenderer>();
                Assert.Greater(skinned.Length, 4, e.name + " is wearing a head and an outfit");
                Assert.IsTrue(skinned.All(r => r.isVisible || r.bounds.size.sqrMagnitude > 0f));
                var hand = e.Visual.GetComponent<RigFollower>().Hand;
                var socket = e.transform.Find("Visual/Model/Chest/ArmPivot/Elbow/HandSocket");
                Assert.Less(Vector3.Distance(hand.position, socket.position), 0.25f, e.name + " holds its item in its hand");
                Assert.Greater(hand.position.y, 0.9f, "guard up: fists at chest height");
            }

            // The big items in every outfit's hand: sleeves (hoodie, dress) must not swallow the grip.
            foreach (var item in new[] { "golf_club", "binder", "laptop", "keyboard", "briefcase" })
            {
                foreach (var e in line) e.HeldItem.SetIdleProp(item);
                yield return new WaitForSecondsRealtime(0.5f);
                foreach (var e in line)
                {
                    var hand = e.Visual.GetComponent<RigFollower>().Hand;
                    var socket = e.transform.Find("Visual/Model/Chest/ArmPivot/Elbow/HandSocket");
                    Assert.AreEqual(item, e.HeldItem.CurrentProp, e.name);
                    Assert.Less(Vector3.Distance(hand.position, socket.position), 0.25f, $"{e.Visual.GetComponent<CharacterLook>().OutfitName} holds the {item}");
                }
                yield return Capture("lineup_" + item);
            }

            // Close up on the first two: faces, fists and items.
            cam.transform.position = new Vector3(line[0].transform.position.x + 0.55f, 1.75f, -4.4f);
            cam.transform.rotation = Quaternion.LookRotation(new Vector3(line[0].transform.position.x + 0.55f, 1.45f, -2f) - cam.transform.position);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Capture("closeup");
        }
    }
}
