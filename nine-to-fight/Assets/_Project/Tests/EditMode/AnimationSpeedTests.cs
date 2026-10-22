using System.Collections.Generic;
using System.Linq;
using NineToFight.Characters;
using NineToFight.Combat;
using NineToFight.Enemies;
using NineToFight.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace NineToFight.Tests
{
    /// <summary>
    /// Guards against attacks that move so far in one frame they read as a teleport instead of a swing.
    /// Uses the real frame data of every move in the game.
    /// </summary>
    public class AnimationSpeedTests
    {
        const float Frame = 1f / 30f;
        const float MaxArmDegreesPerFrame = 70f;
        const float MaxSpinDegreesPerFrame = 60f;

        static IEnumerable<T> Load<T>() where T : Object =>
            AssetDatabase.FindAssets("t:" + typeof(T).Name)
                .Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g)));

        static List<FightingMove> AllMoves() =>
            Load<PlayerClass>().SelectMany(c => c.BuildMoveSet())
                .Concat(Load<WorkerClass>().Select(w => w.attack))
                .Concat(MoveLibrary.All)
                .GroupBy(m => m.id).Select(g => g.First())
                .ToList();

        static Quaternion Arm(SampledPose p) => Quaternion.Euler(p.armSpin, 0f, 0f) * p.arm;

        [Test]
        public void NoAttackMovesTooFarInOneFrame()
        {
            var offenders = new List<string>();
            foreach (var move in AllMoves())
            {
                float total = move.startup + move.active + move.recovery;
                // Every strike of a combo, not just the first.
                for (int hit = 0; hit < Mathf.Max(1, move.hits); hit++)
                {
                    var style = move.StyleForHit(hit);
                    float worstArm = 0f, worstSpin = 0f;
                    for (float t = 0f; t + Frame <= total; t += 1f / 240f)
                    {
                        var a = AttackPoses.Sample(style, t, move.startup, move.active, move.recovery);
                        var b = AttackPoses.Sample(style, t + Frame, move.startup, move.active, move.recovery);
                        worstArm = Mathf.Max(worstArm, Quaternion.Angle(Arm(a), Arm(b)));
                        worstSpin = Mathf.Max(worstSpin, Mathf.Abs(b.spin - a.spin));
                    }
                    if (worstArm > MaxArmDegreesPerFrame || worstSpin > MaxSpinDegreesPerFrame)
                        offenders.Add($"{move.displayName} hit {hit + 1} ({style}): arm {worstArm:0}°/frame, spin {worstSpin:0}°/frame");
                }
            }
            Assert.IsEmpty(offenders, "too fast:\n" + string.Join("\n", offenders));
        }

        [Test]
        public void FlurriesAreCombosNotOneSwingRepeated()
        {
            var offenders = new List<string>();
            foreach (var move in AllMoves().Concat(MoveLibrary.Shared).Where(m => m.hits > 1))
            {
                for (int hit = 1; hit < move.hits; hit++)
                    if (move.StyleForHit(hit) == move.StyleForHit(hit - 1))
                        offenders.Add($"{move.displayName}: hit {hit} and {hit + 1} are both {move.StyleForHit(hit)}");
                if (Enumerable.Range(0, move.hits).Select(move.StyleForHit).Distinct().Count() < 2)
                    offenders.Add($"{move.displayName}: every hit is {move.style}");
            }
            Assert.IsEmpty(offenders, string.Join("\n", offenders));
        }

        [Test]
        public void ImpactLandsInsideTheHitWindow()
        {
            foreach (var move in AllMoves())
            {
                float impact = AttackPoses.ImpactTime(move.startup, move.active);
                Assert.That(impact, Is.InRange(move.startup, move.startup + move.active), move.displayName);
            }
        }
    }
}
