using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NineToFight.Characters;
using NineToFight.Combat;
using NineToFight.Enemies;
using NineToFight.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace NineToFight.Tests
{
    /// <summary>
    /// Plays every attack and checks the body moves like a body throwing it at someone in front: the hit lands
    /// ahead, the face is on the target when it lands and never over the shoulder, and the shoulders never
    /// twist too far from the hips. Compares the clip corrections side by side, then holds the shipped settings
    /// to the limits.
    /// </summary>
    public class BodyMechanicsTests
    {
        const float MaxLanding = 40f, MaxHeadAtImpact = 35f, MaxHead = 90f, MaxSpineTwist = 80f;

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v.normalized; }

        struct Result
        {
            public float landing, headAtImpact, headWorst, twist;
        }

        static bool HeadFree(AttackStyle s) => s == AttackStyle.Spin || s == AttackStyle.Dropkick;

        [UnityTest, Timeout(300000)]
        public IEnumerator EveryAttackKeepsTheBodyTogether()
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
            player.Visual.InCombat = true;
            Time.timeScale = 1f;
            yield return new WaitForSecondsRealtime(1f);

            var rig = player.Visual.GetComponent<RigFollower>().ActiveRig;
            var anim = rig.animator;
            Transform B(HumanBodyBones b) => anim.GetBoneTransform(b);
            var head = B(HumanBodyBones.Head);
            var styles = (AttackStyle[])System.Enum.GetValues(typeof(AttackStyle));

            // The bare clips as the baseline. (Compared on 2026-10-22: strike alignment alone fixed where hits land
            // but turned heads away; head aim alone fixed heads but not hits; the two together beat both.)
            var variants = new (string name, bool align, bool ramp, float aim)[]
            {
                ("clips only", false, false, 0f),
            };
            var (keepAlign, keepRamp, keepAim) = (FightAnimator.AlignStrikes, FightAnimator.RampAlignment, FightAnimator.HeadAim);
            var results = new Dictionary<string, Dictionary<AttackStyle, Result>>();
            try
            {
                // The shipped settings last, so the assertion below sees them.
                foreach (var v in variants.Append(("shipped", keepAlign, keepRamp, keepAim)))
                {
                    FightAnimator.AlignStrikes = v.Item2;
                    FightAnimator.RampAlignment = v.Item3;
                    FightAnimator.HeadAim = v.Item4;
                    var perStyle = new Dictionary<AttackStyle, Result>();
                    foreach (var style in styles)
                    {
                        // Game time must run (a stray Esc or P pauses the game and would freeze these loops).
                        Time.timeScale = 1f;
                        var move = new FightingMove { style = style, startup = 0.3f, active = 0.12f, recovery = 0.4f };
                        float impact = AttackPoses.ImpactTime(move.startup, move.active);
                        var facing = Flat(player.Motor.FacingDirection);
                        var bones = FightAnimationSet.StrikingBones(style).Select(B).ToArray();
                        player.Visual.PlayAttack(move);
                        var r = new Result();
                        bool measuredImpact = false;
                        var last = bones.Select(b => b.position).ToArray();
                        float giveUp = Time.realtimeSinceStartup + 5f;
                        for (float t = 0f; t < 0.82f && Time.realtimeSinceStartup < giveUp; t += Time.deltaTime)
                        {
                            yield return null;
                            var hips = Flat(B(HumanBodyBones.LeftUpperLeg).position - B(HumanBodyBones.RightUpperLeg).position);
                            var shoulders = Flat(B(HumanBodyBones.LeftUpperArm).position - B(HumanBodyBones.RightUpperArm).position);
                            r.twist = Mathf.Max(r.twist, Vector3.Angle(hips, shoulders));
                            float headOff = Vector3.Angle(Flat(head.rotation * rig.headForward), facing);
                            r.headWorst = Mathf.Max(r.headWorst, headOff);
                            if (!measuredImpact && t + Time.deltaTime >= impact)
                            {
                                measuredImpact = true;
                                r.headAtImpact = headOff;
                                // Where the hit lands: the striking limb moving fastest at impact (the one throwing it).
                                var centre = B(HumanBodyBones.Hips).position;
                                int striker = FightAnimationSet.LandsExtended(style)
                                    ? Enumerable.Range(0, bones.Length).OrderByDescending(i => Vector3.ProjectOnPlane(bones[i].position - centre, Vector3.up).sqrMagnitude).First()
                                    : Enumerable.Range(0, bones.Length).OrderByDescending(i => (bones[i].position - last[i]).sqrMagnitude).First();
                                r.landing = Vector3.Angle(Flat(bones[striker].position - centre), facing);
                            }
                            for (int i = 0; i < bones.Length; i++) last[i] = bones[i].position;
                        }
                        perStyle[style] = r;
                        yield return new WaitForSecondsRealtime(0.3f);
                    }
                    results[v.Item1] = perStyle;
                }
            }
            finally
            {
                FightAnimator.AlignStrikes = keepAlign;
                FightAnimator.RampAlignment = keepRamp;
                FightAnimator.HeadAim = keepAim;
            }

            var report = new System.Text.StringBuilder("variant          | landing avg/max | head@impact avg/max | head worst max | twist max\n");
            foreach (var kv in results)
            {
                var all = kv.Value;
                var faced = all.Where(p => !HeadFree(p.Key)).Select(p => p.Value).ToList();
                report.AppendLine($"{kv.Key,-16} | {all.Values.Average(r => r.landing),5:0} / {all.Values.Max(r => r.landing),3:0}   | " +
                                  $"{faced.Average(r => r.headAtImpact),5:0} / {faced.Max(r => r.headAtImpact),3:0}       | " +
                                  $"{faced.Max(r => r.headWorst),5:0}          | {all.Values.Max(r => r.twist),3:0}");
            }
            var shipped = results["shipped"];
            var offenders = new List<string>();
            foreach (var p in shipped)
            {
                var r = p.Value;
                var why = new List<string>();
                if (p.Key != AttackStyle.Spin && r.landing > MaxLanding) why.Add($"lands {r.landing:0}° off");
                if (!HeadFree(p.Key) && r.headAtImpact > MaxHeadAtImpact) why.Add($"looks {r.headAtImpact:0}° away at impact");
                if (!HeadFree(p.Key) && r.headWorst > MaxHead) why.Add($"head turns {r.headWorst:0}° away");
                if (r.twist > MaxSpineTwist) why.Add($"spine twists {r.twist:0}°");
                report.AppendLine($"  {p.Key,-14} lands {r.landing,3:0}°  head@impact {r.headAtImpact,3:0}°  head worst {r.headWorst,3:0}°  twist {r.twist,3:0}°" +
                                  (why.Count > 0 ? "   <-- " + string.Join(", ", why) : ""));
                if (why.Count > 0) offenders.Add(p.Key + ": " + string.Join(", ", why));
            }
            Debug.Log("BODY\n" + report);
            Assert.IsEmpty(offenders, "bodies breaking:\n" + report);
        }
    }
}
