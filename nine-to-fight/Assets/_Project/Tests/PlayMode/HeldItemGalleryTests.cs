using System.Collections;
using System.IO;
using System.Linq;
using NineToFight.Characters;
using NineToFight.Combat;
using NineToFight.Enemies;
using NineToFight.Player;
using NineToFight.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace NineToFight.Tests
{
    /// <summary>Individual gameplay close-ups and body views, in addition to the T-pose fitting captures.</summary>
    public class HeldItemGalleryTests
    {
        static readonly string Dir = Path.GetFullPath("Builds/Screenshots/HeldItems");
        const int CaptureLayer = 28;

        [UnityTest]
        public IEnumerator EveryItemKeepsItsAuthoredGraspThroughIdleGuardAndStrike()
        {
            Directory.CreateDirectory(Dir);
            SceneManager.LoadScene("Floor01_CubicleFarm");
            yield return null;
            yield return null;
            Object.FindAnyObjectByType<GameHud>().gameObject.SetActive(false);
            var crowd = EnemyBrain.Active.ToArray();
            var actor = crowd[0];
            foreach (var other in crowd.Skip(1)) other.gameObject.SetActive(false);
            actor.enabled = false;
            actor.GetComponent<CharacterMotor2_5D>().enabled = false;
            actor.GetComponent<CharacterController>().enabled = false;
            actor.transform.SetPositionAndRotation(new Vector3(20f, 0f, -2f), Quaternion.Euler(0f, 200f, 0f));
            actor.Visual.transform.localRotation = Quaternion.identity;
            var follower = actor.Visual.GetComponent<RigFollower>();
            var look = actor.Visual.GetComponent<CharacterLook>();
            var moves = Resources.FindObjectsOfTypeAll<PlayerClass>().SelectMany(c => c.BuildMoveSet())
                .Concat(Resources.FindObjectsOfTypeAll<WorkerClass>().Select(c => c.attack)).Concat(MoveLibrary.All).ToArray();
            var cameraObject = new GameObject("Gameplay grip camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.cullingMask = 1 << CaptureLayer;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.14f, 0.18f);
            camera.orthographic = true;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 15f;
            var target = RenderTexture.GetTemporary(640, 640, 24);
            camera.targetTexture = target;
            try
            {
                for (int body = 0; body < follower.RigCount; body++)
                {
                    look.Apply(17, false, body == 0 ? "Man_Casual" : "Woman_Blouse");
                    Assert.AreEqual(body, follower.Active);
                    foreach (var entry in actor.HeldItem.Catalog.Entries.OrderBy(e => e.id == "coffee_cup" ? 0 : e.id == "briefcase" ? 1 : 2))
                    {
                        actor.HeldItem.SetIdleProp(entry.id);
                        var move = moves.FirstOrDefault(m => m.prop == entry.id);
                        Assert.IsNotNull(move, entry.id + " has a game move to exercise its grip");
                        for (int state = 0; state < 3; state++)
                        {
                            actor.Visual.InCombat = state != 0;
                            if (state == 2)
                            {
                                actor.Visual.PlayAttack(move);
                                yield return new WaitForSeconds(FightingMove.ImpactTime(move.startup, move.active));
                            }
                            // The game keeps its guard for 2.2 seconds after a strike.
                            // Wait for that grace period before calling a capture relaxed idle.
                            else yield return new WaitForSeconds(state == 0 ? 3.2f : 0.8f);
                            yield return new WaitForEndOfFrame();
                            Assert.IsTrue(actor.Visual.UsesClips);
                            foreach (var t in actor.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = CaptureLayer;
                            var socket = actor.transform.Find("Visual/Model/Chest/ArmPivot/Elbow/HandSocket");
                            var item = socket.Cast<Transform>().First(t => t.gameObject.activeSelf && t.name.StartsWith(entry.prefab.name));
                            var pose = actor.HeldItem.CurrentGrip;
                            string label = entry.id + "_" + body + "_" + new[] { "idle", "guard", "strike" }[state];
                            Assert.Less(Vector3.Distance(pose.position, item.localPosition), 0.00001f, label + ": fitted position");
                            Assert.Less(Quaternion.Angle(Quaternion.Euler(pose.eulerAngles), item.localRotation), 0.1f, label + ": fitted orientation");
                            if (entry.id == "coffee_cup" && state != 2)
                                Assert.Greater(Vector3.Dot(item.up, Vector3.up), 0.98f, label + ": carry the cup upright");
                            Assert.Less(Vector3.Distance(socket.position, follower.Hand.TransformPoint(follower.ActiveRig.grip)), 0.0001f, label + ": attached to hand");
                            var frame = follower.Hand.rotation * follower.ActiveRig.gripRotation;
                            var center = socket.position + frame * new Vector3(-0.02f, 0f, 0.015f);
                            camera.orthographicSize = 0.20f;
                            camera.transform.SetPositionAndRotation(center + frame * new Vector3(-0.8f, 0.2f, 0.4f),
                                Quaternion.LookRotation(frame * new Vector3(0.8f, -0.2f, -0.4f), frame * Vector3.up));
                            Capture(camera, target, label + "_hand");
                            center = actor.transform.position + Vector3.up * 1.2f;
                            camera.orthographicSize = 1.5f;
                            camera.transform.SetPositionAndRotation(center + new Vector3(-0.5f, 0.5f, -5f),
                                Quaternion.LookRotation(new Vector3(0.5f, -0.5f, 5f)));
                            Capture(camera, target, label + "_body");
                            if (state == 2)
                            {
                                actor.Visual.CancelAttack();
                                yield return new WaitForSeconds(0.3f);
                            }
                        }
                    }
                }
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.ReleaseTemporary(target);
                Object.Destroy(cameraObject);
            }
        }

        static void Capture(Camera camera, RenderTexture target, string name)
        {
            var previous = RenderTexture.active;
            var image = new Texture2D(640, 640, TextureFormat.RGB24, false);
            try
            {
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 640, 640), 0, 0);
                image.Apply();
                File.WriteAllBytes(Path.Combine(Dir, name + ".png"), image.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; Object.Destroy(image); }
        }
    }
}
