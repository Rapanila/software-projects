using System.Collections;
using System.Linq;
using NineToFight.Characters;
using NineToFight.Enemies;
using NineToFight.Game;
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
