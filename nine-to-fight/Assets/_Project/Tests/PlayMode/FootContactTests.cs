using System.Collections;
using System.IO;
using System.Linq;
using NineToFight.Characters;
using NineToFight.CameraRig;
using NineToFight.Enemies;
using NineToFight.Game;
using NineToFight.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace NineToFight.Tests
{
    /// <summary>Walks and runs the player, relaxed and in a fight, and checks the feet never sink into the floor.</summary>
    public class FootContactTests
    {
        [UnityTest]
        public IEnumerator IdleStancesKeepTheBodyBalancedOverTheFeet()
        {
            TowerDirector.ForceEvent = FloorEvent.None;
            SceneManager.LoadScene("Floor01_CubicleFarm");
            yield return null;
            yield return null;
            var flow = GameFlow.Instance;
            flow.StartShiftWithClass(2);
            var player = flow.Player;
            player.enabled = false;
            player.Motor.CanMove = false;
            Object.FindAnyObjectByType<GameHud>().gameObject.SetActive(false);
            foreach (var enemy in EnemyBrain.Active.ToArray()) enemy.gameObject.SetActive(false);
            var follower = player.Visual.GetComponent<RigFollower>();
            var look = player.Visual.GetComponent<CharacterLook>();
            var camera = Camera.main;
            camera.GetComponent<SideScrollCamera>().enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 1.5f;
            Directory.CreateDirectory("Builds/Screenshots/Stance");
            var problems = new System.Collections.Generic.List<string>();
            var report = new System.Text.StringBuilder();
            for (int body = 0; body < follower.RigCount; body++)
            {
                look.Apply(17, false, body == 0 ? "Man_Casual" : "Woman_Blouse");
                var root = player.Visual.transform;
                foreach (var (label, combat, block) in new[] { ("relaxed", false, false), ("guard", true, false), ("block", true, true), ("guard_after_block", true, false), ("relaxed_after_guard", false, false) })
                {
                    player.Visual.InCombat = combat;
                    player.Visual.Blocking = block;
                    float worstBehind = 0f, lowest = float.PositiveInfinity, minHips = float.PositiveInfinity;
                    for (float time = 0f; time < 1.3f; time += Time.deltaTime)
                    {
                        yield return new WaitForEndOfFrame();
                        var animator = follower.ActiveRig.animator;
                        var hips = root.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.Hips).position);
                        var left = root.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.LeftFoot).position);
                        var right = root.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.RightFoot).position);
                        lowest = Mathf.Min(lowest, Mathf.Min(left.y, right.y));
                        minHips = Mathf.Min(minHips, hips.y);
                        if (time > 0.5f && !block) worstBehind = Mathf.Max(worstBehind, Mathf.Min(left.z, right.z) - hips.z);
                    }
                    string context = $"body {body} {label}: hips behind rear ankle {worstBehind:0.000} m, hips height {minHips:0.00}, ankle height {lowest:0.00}";
                    report.AppendLine(context);
                    if (worstBehind > 0.08f) problems.Add(context + ": both feet ahead of the body");
                    if (minHips < 0.8f || lowest < -0.06f) problems.Add(context + ": legs collapse or feet sink during the blend");
                    var center = root.position + Vector3.up * 1.1f;
                    camera.transform.SetPositionAndRotation(center + root.right * 4f, Quaternion.LookRotation(-root.right));
                    ScreenCapture.CaptureScreenshot($"Builds/Screenshots/Stance/idle_{body}_{label}_side.png");
                    yield return null;
                }
            }
            Debug.Log("IDLE STANCE\n" + report);
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [UnityTest]
        public IEnumerator FeetStayOnTheFloorWhileMoving()
        {
            TowerDirector.ForceEvent = FloorEvent.None;
            SceneManager.LoadScene("Floor01_CubicleFarm");
            yield return null;
            yield return null;
            var flow = GameFlow.Instance;
            flow.StartShiftWithClass(2);
            var player = flow.Player;
            player.enabled = false;
            foreach (var e in EnemyBrain.Active.ToArray()) e.gameObject.SetActive(false);
            var motor = player.Motor;
            motor.CanMove = true;
            var report = new System.Text.StringBuilder();
            bool sunk = false;

            foreach (var (label, combat, speed) in new[] { ("walk", false, 0.5f), ("run", false, 1f), ("fight walk", true, 0.6f) })
            {
                player.Visual.InCombat = combat;
                float lowest = 9f, highestPlanted = -9f, minHips = 9f;
                for (float t = 0f; t < 2.2f; t += Time.deltaTime)
                {
                    motor.SetMoveInput(new Vector2(t < 1.1f ? speed : -speed, 0f));
                    yield return null;
                    if (t < 0.5f) continue; // let the blend settle
                    var anim = player.Visual.GetComponent<RigFollower>().ActiveRig.animator;
                    float floor = player.transform.position.y;
                    float l = anim.GetBoneTransform(HumanBodyBones.LeftFoot).position.y - floor;
                    float r = anim.GetBoneTransform(HumanBodyBones.RightFoot).position.y - floor;
                    float low = Mathf.Min(l, r);
                    lowest = Mathf.Min(lowest, low);
                    highestPlanted = Mathf.Max(highestPlanted, low);
                    float hips = anim.GetBoneTransform(HumanBodyBones.Hips).position.y - floor;
                    minHips = Mathf.Min(minHips, hips);
                }
                report.AppendLine($"{label}: planted foot {lowest:0.00}..{highestPlanted:0.00} m, hips >= {minHips:0.00} m");
                if (lowest < -0.06f || minHips < 0.8f) sunk = true;
            }
            motor.SetMoveInput(Vector2.zero);
            Debug.Log("FEET " + report);
            Assert.IsFalse(sunk, "sinking into the floor:\n" + report);
        }
    }
}
