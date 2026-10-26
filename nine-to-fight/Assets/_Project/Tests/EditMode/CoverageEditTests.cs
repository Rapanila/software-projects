using System.Collections.Generic;
using System.Linq;
using NineToFight.Audio;
using NineToFight.Combat;
using NineToFight.Game;
using NineToFight.Items;
using NineToFight.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace NineToFight.Tests
{
    /// <summary>Pure-logic coverage: perks, UI kit, synthesized audio, floors 2-9 and pickups.</summary>
    public class CoverageEditTests
    {
        // ------------------------------------------------------------------ perks

        static PerkSet Stacked(PerkId id, int times)
        {
            var set = new PerkSet();
            for (int i = 0; i < times; i++) Assert.IsTrue(set.Add(id), $"{id} stack {i + 1}");
            return set;
        }

        [Test]
        public void PerkSetStartsNeutral()
        {
            var set = new PerkSet();
            Assert.AreEqual(0, set.Count);
            Assert.AreEqual(0f, set.MaxHealthBonus);
            Assert.AreEqual(1f, set.DamageMultiplier);
            Assert.AreEqual(1f, set.DodgeCooldownMultiplier);
            Assert.AreEqual(1f, set.CoffeeMultiplier);
            Assert.AreEqual(0f, set.Lifesteal);
            Assert.AreEqual(0f, set.ComboWindowBonus);
            Assert.AreEqual(1f, set.XpMultiplier);
            Assert.AreEqual(1f, set.AbilityCooldownMultiplier);
            Assert.AreEqual(0f, set.CritChance);
            Assert.IsFalse(set.HasParachute);
            Assert.AreEqual(0f, set.BurstDamage);
            Assert.AreEqual(1f, set.SpeedMultiplier);
        }

        [Test]
        public void DentalPlanAdds25MaxHpPerStack()
        {
            Assert.AreEqual(25f, Stacked(PerkId.DentalPlan, 1).MaxHealthBonus, 0.0001f);
            Assert.AreEqual(75f, Stacked(PerkId.DentalPlan, 3).MaxHealthBonus, 0.0001f);
        }

        [Test]
        public void StockOptionsAdd15PercentDamagePerStack()
        {
            Assert.AreEqual(1.15f, Stacked(PerkId.StockOptions, 1).DamageMultiplier, 0.0001f);
            Assert.AreEqual(1.45f, Stacked(PerkId.StockOptions, 3).DamageMultiplier, 0.0001f);
        }

        [Test]
        public void FlexTimeCutsDodgeCooldownBySeventyPercentCompounding()
        {
            Assert.AreEqual(0.7f, Stacked(PerkId.FlexTime, 1).DodgeCooldownMultiplier, 0.0001f);
            Assert.AreEqual(0.49f, Stacked(PerkId.FlexTime, 2).DodgeCooldownMultiplier, 0.0001f);
        }

        [Test]
        public void FreeCoffeeAddsOneToTheCoffeeMultiplierPerStack()
        {
            Assert.AreEqual(2f, Stacked(PerkId.FreeCoffee, 1).CoffeeMultiplier, 0.0001f);
            Assert.AreEqual(3f, Stacked(PerkId.FreeCoffee, 2).CoffeeMultiplier, 0.0001f);
        }

        [Test]
        public void ExpenseAccountLifestealIsSixPercentPerStack()
        {
            Assert.AreEqual(0.06f, Stacked(PerkId.ExpenseAccount, 1).Lifesteal, 0.0001f);
            Assert.AreEqual(0.18f, Stacked(PerkId.ExpenseAccount, 3).Lifesteal, 0.0001f);
        }

        [Test]
        public void TeamBuildingAddsOneSecondOfComboWindowPerStack()
        {
            Assert.AreEqual(1f, Stacked(PerkId.TeamBuilding, 1).ComboWindowBonus, 0.0001f);
            Assert.AreEqual(2f, Stacked(PerkId.TeamBuilding, 2).ComboWindowBonus, 0.0001f);
        }

        [Test]
        public void CareerCoachingAddsThirtyPercentXpPerStack()
        {
            Assert.AreEqual(1.3f, Stacked(PerkId.CareerCoaching, 1).XpMultiplier, 0.0001f);
            Assert.AreEqual(1.9f, Stacked(PerkId.CareerCoaching, 3).XpMultiplier, 0.0001f);
        }

        [Test]
        public void PaidTimeOffCutsAbilityCooldownBySeventyPercentCompounding()
        {
            Assert.AreEqual(0.7f, Stacked(PerkId.PaidTimeOff, 1).AbilityCooldownMultiplier, 0.0001f);
            Assert.AreEqual(0.49f, Stacked(PerkId.PaidTimeOff, 2).AbilityCooldownMultiplier, 0.0001f);
        }

        [Test]
        public void CornerDeskCritChanceIsFifteenPercentPerStack()
        {
            Assert.AreEqual(0.15f, Stacked(PerkId.CornerDesk, 1).CritChance, 0.0001f);
            Assert.AreEqual(0.45f, Stacked(PerkId.CornerDesk, 3).CritChance, 0.0001f);
        }

        [Test]
        public void GoldenParachuteIsSingleStack()
        {
            var set = new PerkSet();
            Assert.IsFalse(set.HasParachute);
            Assert.IsTrue(set.Add(PerkId.GoldenParachute));
            Assert.IsTrue(set.HasParachute);
            Assert.IsFalse(set.Add(PerkId.GoldenParachute), "only one allowed");
            Assert.AreEqual(1, set.Stacks(PerkId.GoldenParachute));
        }

        [Test]
        public void HostileTakeoverBurstIs18PerStack()
        {
            Assert.AreEqual(18f, Stacked(PerkId.HostileTakeover, 1).BurstDamage, 0.0001f);
            Assert.AreEqual(36f, Stacked(PerkId.HostileTakeover, 2).BurstDamage, 0.0001f);
        }

        [Test]
        public void OvertimeAddsTwelvePercentSpeedPerStack()
        {
            Assert.AreEqual(1.12f, Stacked(PerkId.Overtime, 1).SpeedMultiplier, 0.0001f);
            Assert.AreEqual(1.24f, Stacked(PerkId.Overtime, 2).SpeedMultiplier, 0.0001f);
        }

        [Test]
        public void EveryPerkStopsAtItsMaxStacksAndIsDefined()
        {
            foreach (PerkId id in System.Enum.GetValues(typeof(PerkId)))
            {
                var perk = PerkLibrary.Get(id);
                Assert.AreEqual(id, perk.id);
                Assert.IsNotEmpty(perk.name, id.ToString());
                Assert.IsNotEmpty(perk.description, id.ToString());
                Assert.GreaterOrEqual(perk.maxStacks, 1);
                var set = Stacked(id, perk.maxStacks);
                Assert.IsFalse(set.Add(id), $"{id} can't pass {perk.maxStacks} stacks");
                Assert.AreEqual(perk.maxStacks, set.Stacks(id));
                Assert.AreEqual(perk.maxStacks, set.Count);
            }
            Assert.AreEqual(System.Enum.GetValues(typeof(PerkId)).Length, PerkLibrary.All.Count);
        }

        [Test]
        public void AddingAPerkRaisesTheAddedEvent()
        {
            var set = new PerkSet();
            Perk seen = null;
            set.Added += p => seen = p;
            set.Add(PerkId.Overtime);
            Assert.IsNotNull(seen);
            Assert.AreEqual(PerkId.Overtime, seen.id);
        }

        [Test]
        public void RollReturnsDistinctPerksAndIsSeedDeterministic()
        {
            var a = PerkLibrary.Roll(new System.Random(1234), 3, new PerkSet());
            var b = PerkLibrary.Roll(new System.Random(1234), 3, new PerkSet());
            Assert.AreEqual(3, a.Count);
            Assert.AreEqual(3, a.Select(p => p.id).Distinct().Count());
            CollectionAssert.AreEqual(a.Select(p => p.id).ToArray(), b.Select(p => p.id).ToArray());

            var all = PerkLibrary.Roll(new System.Random(5), 99, null);
            Assert.AreEqual(PerkLibrary.All.Count, all.Count, "asking for more than exist returns every perk once");
            Assert.AreEqual(all.Count, all.Select(p => p.id).Distinct().Count());
        }

        [Test]
        public void RollSkipsPerksAlreadyAtMaxStacks()
        {
            var owned = new PerkSet();
            owned.Add(PerkId.GoldenParachute);
            var all = PerkLibrary.Roll(new System.Random(9), 99, owned);
            Assert.AreEqual(PerkLibrary.All.Count - 1, all.Count);
            Assert.IsFalse(all.Any(p => p.id == PerkId.GoldenParachute));

            owned.Add(PerkId.FlexTime);
            owned.Add(PerkId.FlexTime);
            owned.Add(PerkId.StockOptions);
            var next = PerkLibrary.Roll(new System.Random(9), 99, owned);
            Assert.IsFalse(next.Any(p => p.id == PerkId.FlexTime), "maxed at two stacks");
            Assert.IsTrue(next.Any(p => p.id == PerkId.StockOptions), "one of three stacks, still offered");
        }

        // ------------------------------------------------------------------ ui kit

        static void AssertColor(string hex, Color actual)
        {
            int v = System.Convert.ToInt32(hex, 16);
            Assert.AreEqual(((v >> 16) & 0xFF) / 255f, actual.r, 0.005f, hex + " r");
            Assert.AreEqual(((v >> 8) & 0xFF) / 255f, actual.g, 0.005f, hex + " g");
            Assert.AreEqual((v & 0xFF) / 255f, actual.b, 0.005f, hex + " b");
            Assert.AreEqual(1f, actual.a, 0.005f);
        }

        [Test]
        public void UiKitPaletteMatchesTheHouseColours()
        {
            AssertColor("1B2A4A", UiKit.Navy);
            AssertColor("111B31", UiKit.NavyDeep);
            AssertColor("F3EEE2", UiKit.Paper);
            AssertColor("D9D1BF", UiKit.PaperShade);
            AssertColor("1C1F26", UiKit.Ink);
            AssertColor("5A6070", UiKit.InkSoft);
            AssertColor("FFD23F", UiKit.Highlighter);
            AssertColor("E5484D", UiKit.Red);
            AssertColor("2BB3A3", UiKit.Teal);
            AssertColor("9AA6BF", UiKit.Muted);
            AssertColor("C9A44C", UiKit.Brass);
            AssertColor("8E98A8", UiKit.Steel);
        }

        [Test]
        public void UiKitHexFallsBackToMagentaAndWithAlphaKeepsRgb()
        {
            Assert.AreEqual(Color.magenta, UiKit.Hex("not a colour"));
            var c = UiKit.WithAlpha(UiKit.Red, 0.25f);
            Assert.AreEqual(UiKit.Red.r, c.r);
            Assert.AreEqual(UiKit.Red.g, c.g);
            Assert.AreEqual(UiKit.Red.b, c.b);
            Assert.AreEqual(0.25f, c.a, 0.0001f);
        }

        [Test]
        public void UiKitFontsAlwaysResolve()
        {
            Assert.IsNotNull(UiKit.Display);
            Assert.IsNotNull(UiKit.Heading);
            Assert.IsNotNull(UiKit.Body);
            Assert.IsNotNull(UiKit.BodyStrong);
            Assert.IsNotNull(UiKit.BodyItalic);
        }

        static void AssertSprite(Sprite s, int w, int h, string name)
        {
            Assert.IsNotNull(s, name);
            Assert.AreEqual(w, s.rect.width, 0.01f, name + " width");
            Assert.AreEqual(h, s.rect.height, 0.01f, name + " height");
            Assert.AreEqual(w, s.texture.width, name + " texture width");
            Assert.AreEqual(h, s.texture.height, name + " texture height");
        }

        [Test]
        public void UiKitProceduralSpritesHaveTheDocumentedSizes()
        {
            AssertSprite(UiKit.Rounded, 68, 68, "Rounded");
            AssertSprite(UiKit.RoundedOutline, 68, 68, "RoundedOutline");
            AssertSprite(UiKit.Shadow, 96, 96, "Shadow");
            AssertSprite(UiKit.Keycap, 96, 104, "Keycap");
            AssertSprite(UiKit.HighlighterStroke, 256, 64, "HighlighterStroke");
            AssertSprite(UiKit.Vignette, 256, 256, "Vignette");
            AssertSprite(UiKit.FadeRight, 128, 2, "FadeRight");
            AssertSprite(UiKit.FadeUp, 2, 128, "FadeUp");
            AssertSprite(UiKit.BrushedSteel, 128, 256, "BrushedSteel");
        }

        [Test]
        public void UiKitNineSlicedSpritesHaveBorders()
        {
            Assert.AreEqual(new Vector4(33, 33, 33, 33), UiKit.Rounded.border);
            Assert.AreEqual(new Vector4(36, 36, 36, 36), UiKit.Shadow.border);
            Assert.AreEqual(Vector4.zero, UiKit.Vignette.border);
        }

        [Test]
        public void UiKitSpritesAreCached()
        {
            Assert.AreSame(UiKit.Rounded, UiKit.Rounded);
            Assert.AreSame(UiKit.Keycap, UiKit.Keycap);
            Assert.AreSame(UiKit.BrushedSteel, UiKit.BrushedSteel);
        }

        [Test]
        public void UiKitRoundedCornersAreClearAndCentreIsSolid()
        {
            var tex = UiKit.Rounded.texture;
            Assert.AreEqual(0f, tex.GetPixel(0, 0).a, 0.01f, "corner is transparent");
            Assert.AreEqual(1f, tex.GetPixel(34, 34).a, 0.01f, "centre is opaque");
            Assert.AreEqual(1f, UiKit.Rounded.texture.GetPixel(34, 34).r, 0.01f, "white, tinted by Image colour");
        }

        [Test]
        public void UiKitGradientsFadeFromOpaqueToClear()
        {
            var right = UiKit.FadeRight.texture;
            Assert.AreEqual(1f, right.GetPixel(0, 0).a, 0.01f);
            Assert.AreEqual(0f, right.GetPixel(127, 0).a, 0.01f);
            var up = UiKit.FadeUp.texture;
            Assert.AreEqual(1f, up.GetPixel(0, 0).a, 0.01f);
            Assert.AreEqual(0f, up.GetPixel(0, 127).a, 0.01f);
        }

        // ------------------------------------------------------------------ synth

        static float[] Samples(AudioClip clip)
        {
            var data = new float[clip.samples];
            clip.GetData(data, 0);
            return data;
        }

        [Test]
        public void EveryEffectClipIsNonEmptyMonoAndFadesToSilence()
        {
            foreach (Sfx sfx in System.Enum.GetValues(typeof(Sfx)))
            {
                var clip = Synth.Build(sfx);
                try
                {
                    Assert.IsNotNull(clip, sfx.ToString());
                    Assert.Greater(clip.samples, 0, sfx.ToString());
                    Assert.AreEqual(1, clip.channels, sfx.ToString());
                    Assert.AreEqual(Synth.SampleRate, clip.frequency, sfx.ToString());
                    var data = Samples(clip);
                    Assert.Less(Mathf.Abs(data[data.Length - 1]), 0.02f, $"{sfx} ends in silence");
                    Assert.Greater(data.Max(x => Mathf.Abs(x)), 0.5f, $"{sfx} is audible");
                    Assert.AreEqual(0.9f, data.Max(x => Mathf.Abs(x)), 0.02f, $"{sfx} is normalised to the default 0.9 peak");
                }
                finally { Object.DestroyImmediate(clip); }
            }
        }

        [Test]
        public void EffectNamesMatchTheEnum()
        {
            foreach (Sfx sfx in System.Enum.GetValues(typeof(Sfx)))
            {
                var clip = Synth.Build(sfx);
                Assert.AreEqual(sfx.ToString(), clip.name);
                Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void EffectsAreDeterministic()
        {
            var a = Synth.Build(Sfx.HitHeavy);
            var b = Synth.Build(Sfx.HitHeavy);
            CollectionAssert.AreEqual(Samples(a), Samples(b));
            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);
        }

        [Test]
        public void EffectLengthsFollowTheirBuffers()
        {
            var ding = Synth.Build(Sfx.ElevatorDing);
            Assert.AreEqual(1.6f, ding.length, 0.01f);
            var uiMove = Synth.Build(Sfx.UiMove);
            Assert.AreEqual(0.06f, uiMove.length, 0.01f);
            Object.DestroyImmediate(ding);
            Object.DestroyImmediate(uiMove);
        }

        [Test]
        public void AllMusicTracksBuildWholeBarLoops()
        {
            foreach (var track in new[] { Synth.Lounge, Synth.Fight, Synth.Boss })
            {
                var clip = Synth.BuildMusic(track);
                try
                {
                    float bar = 60f / track.bpm * 4f;
                    Assert.AreEqual(bar * track.chords.Length, clip.length, 0.01f, track.name);
                    Assert.AreEqual(track.name, clip.name);
                    var data = Samples(clip);
                    Assert.AreEqual(0.8f, data.Max(x => Mathf.Abs(x)), 0.02f, track.name + " peak");
                    Assert.IsTrue(data.All(s => !float.IsNaN(s)), track.name + " has no NaN");
                }
                finally { Object.DestroyImmediate(clip); }
            }
        }

        [Test]
        public void AmbienceBuildsASixteenSecondLoopThatFadesAtTheSeam()
        {
            var clip = Synth.BuildAmbience();
            Assert.AreEqual(16f, clip.length, 0.01f);
            var data = Samples(clip);
            Assert.Less(Mathf.Abs(data[0]), 0.02f, "starts quiet");
            Assert.Less(Mathf.Abs(data[data.Length - 1]), 0.02f, "ends quiet");
            Assert.AreEqual(0.6f, data.Max(x => Mathf.Abs(x)), 0.02f);
            Object.DestroyImmediate(clip);
        }

        [Test]
        public void SynthWaveformsAndMidiMapAreCorrect()
        {
            Assert.AreEqual(440f, Synth.Midi(69f), 0.001f);
            Assert.AreEqual(880f, Synth.Midi(81f), 0.01f);
            Assert.AreEqual(0f, Synth.Sine(0f), 0.0001f);
            Assert.AreEqual(1f, Synth.Sine(0.25f), 0.0001f);
            Assert.AreEqual(1f, Synth.Square(0.1f));
            Assert.AreEqual(-1f, Synth.Square(0.6f));
            Assert.AreEqual(1f, Synth.Triangle(0f), 0.0001f);
            Assert.AreEqual(-1f, Synth.Triangle(0.5f), 0.0001f);
        }

        // ------------------------------------------------------------------ floors 2-9

        static readonly int[] BossFloors = { 3, 6, 8, 9 };
        static readonly int[] SeminarFloors = { 3, 5, 7 };

        static PropCatalog LoadCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<PropCatalog>("Assets/_Project/Data/PropCatalog.asset");
            Assert.IsNotNull(catalog, "PropCatalog asset");
            return catalog;
        }

        [Test]
        public void EveryGeneratedFloorHasTheExpectedNumberOfCoworkers()
        {
            for (int floor = 2; floor <= TowerPlan.FloorCount; floor++)
            {
                bool boss = BossFloors.Contains(floor);
                int expected = 0;
                for (int e = 0; e < 5; e++)
                {
                    int count = 1 + (e + floor) / 3;
                    if (e == 4 && boss) count = Mathf.Max(1, count - 1);
                    expected += count;
                }
                if (boss) expected += 1; // the boss stands at the end
                var plan = TowerPlan.Build(floor, 7);
                Assert.Greater(plan.spawns.Count, 0, $"floor {floor}");
                Assert.AreEqual(expected, plan.spawns.Count, $"floor {floor} spawn count");
            }
        }

        [Test]
        public void BossesAppearExactlyOnBossFloorsAndTheCeoOnFloorNine()
        {
            for (int floor = 2; floor <= TowerPlan.FloorCount; floor++)
            {
                var plan = TowerPlan.Build(floor, 3);
                int bosses = plan.spawns.Count(s => s.who.boss != BossRank.None);
                Assert.AreEqual(BossFloors.Contains(floor) ? 1 : 0, bosses, $"floor {floor} bosses");
                Assert.AreEqual(BossFloors.Contains(floor), plan.HasBoss, $"floor {floor} HasBoss");
                int final = plan.spawns.Count(s => s.who.boss == BossRank.FinalBoss);
                Assert.AreEqual(floor == TowerPlan.FloorCount ? 1 : 0, final, $"floor {floor} final boss");
                if (bosses == 1)
                {
                    var boss = plan.spawns.Single(s => s.who.boss != BossRank.None);
                    Assert.AreEqual(TowerPlan.LastEncounterX, boss.position.x, 0.001f);
                    Assert.AreEqual(0f, boss.position.z, 0.001f);
                    Assert.IsFalse(boss.elite);
                }
            }
        }

        [Test]
        public void FloorScalesFollowTheirFormulas()
        {
            for (int floor = 1; floor <= TowerPlan.FloorCount; floor++)
            {
                var plan = TowerPlan.Build(floor);
                Assert.AreEqual(floor, plan.number);
                Assert.AreEqual(1f + 0.2f * (floor - 1), plan.healthScale, 0.0001f, $"health {floor}");
                Assert.AreEqual(1f + 0.1f * (floor - 1), plan.damageScale, 0.0001f, $"damage {floor}");
                Assert.AreEqual(1f + 0.15f * (floor - 1), plan.xpScale, 0.0001f, $"xp {floor}");
            }
            Assert.AreEqual(2.6f, TowerPlan.Build(9).healthScale, 0.0001f);
            Assert.IsTrue(TowerPlan.Build(9).IsFinal);
            Assert.IsFalse(TowerPlan.Build(8).IsFinal);
        }

        [Test]
        public void BuildClampsFloorNumbers()
        {
            Assert.AreEqual(1, TowerPlan.Build(-4).number);
            Assert.AreEqual(TowerPlan.FloorCount, TowerPlan.Build(40).number);
        }

        [Test]
        public void SeminarsAppearOnFloorsThreeFiveAndSeven()
        {
            for (int floor = 1; floor <= TowerPlan.FloorCount; floor++)
            {
                var plan = TowerPlan.Build(floor);
                string move = TowerPlan.SeminarMove(floor);
                if (SeminarFloors.Contains(floor))
                {
                    Assert.IsNotNull(move, $"floor {floor} teaches a move");
                    Assert.AreEqual(1, plan.seminars.Count, $"floor {floor} seminars");
                    Assert.AreEqual(move, plan.seminars[0].moveId);
                    Assert.AreEqual(1.8f, plan.seminars[0].position.z, 0.001f);
                }
                else
                {
                    Assert.IsNull(move, $"floor {floor}");
                    Assert.IsEmpty(plan.seminars, $"floor {floor} seminars");
                }
            }
            Assert.AreEqual(MoveLibrary.KeyboardSmash.id, TowerPlan.SeminarMove(3));
            Assert.AreEqual(MoveLibrary.ReplyAll.id, TowerPlan.SeminarMove(5));
            Assert.AreEqual(MoveLibrary.KeyboardSmash.id, TowerPlan.SeminarMove(7));
        }

        [Test]
        public void SuppliesAreLaidOutPerFloor()
        {
            for (int floor = 2; floor <= TowerPlan.FloorCount; floor++)
            {
                var plan = TowerPlan.Build(floor);
                // One coffee per even gap (all four gaps from floor 6), plus one after the last encounter.
                Assert.AreEqual(floor >= 6 ? 5 : 3, plan.coffee.Count, $"floor {floor} coffee");
                Assert.AreEqual(1, plan.reviews.Count, $"floor {floor} reviews");
                Assert.AreEqual((TowerPlan.FirstEncounterX + TowerPlan.LastEncounterX) / 2f, plan.reviews[0].x, 0.001f);
                Assert.AreEqual(TowerPlan.LastEncounterX + 4f, plan.coffee.Last().x, 0.001f);
            }
        }

        [Test]
        public void ArchetypeItemOverridesUseRealCatalogProps()
        {
            var catalog = LoadCatalog();
            var seen = new HashSet<string>();
            for (int floor = 2; floor <= TowerPlan.FloorCount; floor++)
                for (int seed = 0; seed < 40; seed++)
                    foreach (var s in TowerPlan.Build(floor, seed).spawns)
                    {
                        if (string.IsNullOrEmpty(s.who.prop)) continue;
                        seen.Add(s.who.prop);
                        Assert.IsNotNull(catalog.Get(s.who.prop), $"floor {floor}: '{s.who.prop}' ({s.who.title}) is in the PropCatalog");
                    }
            foreach (var expected in new[] { "stapler", "golf_club", "desk_phone", "smartphone", "briefcase", "megaphone" })
                Assert.IsTrue(seen.Contains(expected), $"{expected} is carried by some coworker");
        }

        [Test]
        public void FloorNamesAndTaglinesExistForEveryFloor()
        {
            for (int floor = 1; floor <= TowerPlan.FloorCount; floor++)
            {
                Assert.IsNotEmpty(TowerPlan.FloorName(floor));
                Assert.IsNotEmpty(TowerPlan.Tagline(floor));
                Assert.AreEqual(TowerPlan.FloorName(floor), TowerPlan.Build(floor).name);
            }
            Assert.AreEqual("The Boardroom", TowerPlan.FloorName(TowerPlan.FloorCount));
        }

        [Test]
        public void ElitesOnlyAppearFromFloorThree()
        {
            for (int seed = 0; seed < 40; seed++)
                Assert.IsFalse(TowerPlan.Build(2, seed).spawns.Any(s => s.elite), "no elites on floor 2");
        }

        // ------------------------------------------------------------------ pickups

        [Test]
        public void CoffeeIsWastedOnlyAtFullHealth()
        {
            Assert.IsTrue(Pickup.WouldWaste(PickupKind.Coffee, 100f, 100f));
            Assert.IsTrue(Pickup.WouldWaste(PickupKind.Coffee, 99.995f, 100f), "within the 0.01 tolerance");
            Assert.IsFalse(Pickup.WouldWaste(PickupKind.Coffee, 99f, 100f));
            Assert.IsFalse(Pickup.WouldWaste(PickupKind.Coffee, 1f, 100f));
        }

        [Test]
        public void OtherPickupsAreNeverWasted()
        {
            Assert.IsFalse(Pickup.WouldWaste(PickupKind.PerformanceReview, 100f, 100f));
            Assert.IsFalse(Pickup.WouldWaste(PickupKind.TrainingSeminar, 100f, 100f));
            Assert.IsFalse(Pickup.WouldWaste(PickupKind.PerformanceReview, 10f, 100f));
        }
    }
}
