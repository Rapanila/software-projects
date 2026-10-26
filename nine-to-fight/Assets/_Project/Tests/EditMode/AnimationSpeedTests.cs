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
    /// Guards the clip timing: every strike is scrubbed to its move's frame data, and none may play so
    /// fast it reads as a teleport or so slow it reads as slow motion. Uses the real frame data of every move.
    /// </summary>
    public class AnimationSpeedTests
    {
        [Test]
        public void LocomotionClipsContainFootTargetsForBlendingWithTheGuard()
        {
            var set = AssetDatabase.LoadAssetAtPath<FightAnimationSet>("Assets/_Project/Data/FightAnimations.asset");
            foreach (var clip in new[] { set.relaxedIdle, set.walk, set.run })
            {
                var names = AnimationUtility.GetCurveBindings(clip).Select(b => b.propertyName).ToArray();
                foreach (var foot in new[] { "LeftFoot", "RightFoot" })
                foreach (var component in new[] { "T.x", "T.y", "T.z", "Q.x", "Q.y", "Q.z", "Q.w" })
                    CollectionAssert.Contains(names, foot + component, clip.name + ": needs foot targets so guard blends do not fold the legs");
            }
        }

        /// <summary>Slowest a wind-up or recovery may play once trimmed to the clip (a clip too short for its move).</summary>
        const float SlowestRate = 0.3f;

        static IEnumerable<T> Load<T>() where T : Object =>
            AssetDatabase.FindAssets("t:" + typeof(T).Name)
                .Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g)));

        static FightAnimationSet Set => AssetDatabase.LoadAssetAtPath<FightAnimationSet>("Assets/_Project/Data/FightAnimations.asset");

        static List<FightingMove> AllMoves() =>
            Load<PlayerClass>().SelectMany(c => c.BuildMoveSet())
                .Concat(Load<WorkerClass>().Select(w => w.attack))
                .Concat(MoveLibrary.All)
                .GroupBy(m => m.id).Select(g => g.First())
                .ToList();

        [Test]
        public void EveryStrikeOfEveryMoveHasAClip()
        {
            var set = Set;
            var missing = AllMoves().SelectMany(m => Enumerable.Range(0, Mathf.Max(1, m.hits)).Select(h => (m, style: m.StyleForHit(h))))
                .Where(x => set.For(x.style) == null)
                .Select(x => $"{x.m.displayName}: {x.style}").Distinct().ToList();
            Assert.IsEmpty(missing, "strikes without a clip:\n" + string.Join("\n", missing));
        }

        [Test]
        public void StrikeWindowsAreOrderedInsideTheirClips()
        {
            var bad = new List<string>();
            foreach (var s in Set.strikes.Where(s => s != null && s.clip != null))
                if (!(s.start >= 0f && s.start < s.impact && s.impact < s.end && s.end <= s.clip.length + 1e-3f))
                    bad.Add($"{s.style} ({s.clip.name}): start {s.start:0.00} impact {s.impact:0.00} end {s.end:0.00} length {s.clip.length:0.00}");
            Assert.IsEmpty(bad, string.Join("\n", bad));
        }

        [Test]
        public void NoStrikePlaysUnbelievablyFastOrSlow()
        {
            var set = Set;
            var offenders = new List<string>();
            foreach (var move in AllMoves())
            for (int hit = 0; hit < Mathf.Max(1, move.hits); hit++)
            {
                var strike = set.For(move.StyleForHit(hit));
                if (strike == null) continue; // reported by EveryStrikeOfEveryMoveHasAClip
                var w = FightAnimator.StrikeWindow(strike, move);
                float windup = w.WindupRate(strike), recovery = w.RecoveryRate(strike);
                if (windup > FightAnimator.MaxWindupSpeedUp + 1e-3f || windup < SlowestRate ||
                    recovery > FightAnimator.MaxRecoverySpeedUp + 1e-3f || recovery < SlowestRate)
                    offenders.Add($"{move.displayName} hit {hit + 1} ({strike.style}): wind-up x{windup:0.00}, recovery x{recovery:0.00}");
            }
            Assert.IsEmpty(offenders, "playback rates out of range:\n" + string.Join("\n", offenders));
        }

        [Test]
        public void TheClipImpactLandsInsideTheMovesHitWindow()
        {
            var set = Set;
            foreach (var move in AllMoves())
            {
                var strike = set.For(move.style);
                if (strike == null) continue;
                var w = FightAnimator.StrikeWindow(strike, move);
                // The clip reaches its impact frame exactly preImpact seconds into the move...
                Assert.That(w.preImpact, Is.InRange(move.startup, move.startup + move.active + 1e-4f), move.displayName);
                // ...and the window shown actually contains that frame.
                Assert.That(strike.impact, Is.InRange(w.clipStart, w.clipEnd), move.displayName);
            }
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
    }
}
