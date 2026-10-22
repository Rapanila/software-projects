using System.Collections.Generic;
using System.Linq;
using NineToFight.Characters;
using NineToFight.Combat;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace NineToFight.EditorTools
{
    /// <summary>
    /// The fight animation set: Mixamo clips for the stance, block, reactions and every attack style, plus
    /// the Quaternius models' own idle, walk and run converted to humanoid clips. Each attack clip is
    /// sampled on the real skeleton to find where its wind-up starts, where the strike lands (the striking
    /// limb's fastest moment) and where it has settled, so the clips can be timed to each move.
    /// </summary>
    public static partial class StarterContentBuilder
    {
        const string GeneratedClipsDir = Root + "/Art/Animations/Generated";
        static FightAnimationSet fightAnimations;

        static readonly (AttackStyle style, string clip)[] StrikeClips =
        {
            (AttackStyle.Jab, "cross"), (AttackStyle.LeadJab, "jab"), (AttackStyle.Hook, "hook"), (AttackStyle.Uppercut, "uppercut"),
            (AttackStyle.Elbow, "elbow"), (AttackStyle.Knee, "knee"), (AttackStyle.Kick, "front_kick"),
            (AttackStyle.FlyingKick, "flying_kick"), (AttackStyle.Dropkick, "dropkick"), (AttackStyle.SupermanPunch, "cross"),
            (AttackStyle.Chop, "overhead_chop"), (AttackStyle.Sweep, "horizontal_swing"), (AttackStyle.Backhand, "horizontal_swing"),
            (AttackStyle.DiagonalSlash, "diagonal_slash"), (AttackStyle.Spin, "spin_attack"), (AttackStyle.Stamp, "jump_slam"),
            (AttackStyle.Slam, "two_hand_slam"), (AttackStyle.Cleave, "two_hand_cleave"), (AttackStyle.Golf, "golf_swing"),
            (AttackStyle.Hurl, "overhand_throw"), (AttackStyle.Toss, "overhand_throw"), (AttackStyle.Shove, "shove"),
            (AttackStyle.Lunge, "lunge_stab"), (AttackStyle.Blast, "megaphone_blast"), (AttackStyle.Pound, "ground_pound"),
            (AttackStyle.Windmill, "windmill"), (AttackStyle.Whip, "diagonal_slash"),
        };

        static AnimationClip MixamoClip(string name) =>
            AssetDatabase.LoadAllAssetsAtPath($"{MixamoImport.Folder}/{name}.fbx").OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__"));

        [MenuItem("Nine to Fight/Build Fight Animations", priority = 3)]
        public static void BuildFightAnimationsMenu() => BuildFightAnimations();

        /// <summary>Share of each leap kept: Mixamo's jump kicks clear a metre and more, too much for an office.</summary>
        static float LiftKept(AttackStyle style)
        {
            switch (style)
            {
                case AttackStyle.Kick: return 0.2f;
                case AttackStyle.FlyingKick:
                case AttackStyle.Dropkick: return 0.5f;
                case AttackStyle.Stamp: return 0.4f;
                default: return 0.6f;
            }
        }

        static FightAnimationSet BuildFightAnimations()
        {
            EnsureFolder(GeneratedClipsDir);
            var probe = new GameObject("Fight Animation Probe");
            try
            {
                var skeleton = BuildSkeleton(probe.transform, "Probe", "Man_Suit");
                var avatar = BuildAvatar(skeleton.figure);
                // The T-pose's lowest foot is where the feet belong.
                var root = skeleton.figure;
                float floor = Mathf.Min(root.InverseTransformPoint(skeleton.bones["Foot.L"].position).y,
                    root.InverseTransformPoint(skeleton.bones["Foot.R"].position).y);

                var relaxed = ConvertClip(skeleton, avatar, "Idle");
                var walk = ConvertClip(skeleton, avatar, "Walk");
                var run = ConvertClip(skeleton, avatar, "Run");

                var animator = skeleton.figure.gameObject.AddComponent<Animator>();
                animator.avatar = avatar;
                animator.applyRootMotion = false;

                var report = new System.Text.StringBuilder("[Nine to Fight] Fight clips (start / impact / end s, floor fix):\n");
                var grounding = new List<FightAnimationSet.Grounded>();
                var hits = new[] { MixamoClip("hit_head"), MixamoClip("hit_body") }.Where(c => c != null).ToArray();
                // Loops sit on their lowest planted foot; one-shots on where they start.
                var others = new List<(AnimationClip clip, bool loop)>
                {
                    (relaxed, true), (walk, true), (run, true), (MixamoClip("fight_idle"), true), (MixamoClip("block_idle"), true),
                    (MixamoClip("knockout"), false),
                };
                others.AddRange(hits.Select(h => (h, false)));
                foreach (var (clip, loop) in others)
                {
                    if (clip == null) continue;
                    var feet = SampleClip(animator, skeleton.bones, clip, new Transform[0], out _);
                    float ground = floor - (loop ? feet.Min() : feet[0]);
                    grounding.Add(new FightAnimationSet.Grounded { clip = clip, ground = ground });
                    report.AppendLine($"  {clip.name,-18} floor fix {ground:+0.00;-0.00}");
                }

                var strikes = new List<FightAnimationSet.Strike>();
                foreach (var (style, name) in StrikeClips)
                {
                    var clip = MixamoClip(name);
                    if (clip == null)
                    {
                        Debug.LogWarning($"[Nine to Fight] Missing Mixamo clip {name}.fbx for {style}");
                        continue;
                    }
                    // The striking limbs, plus the hips last, to see where the hit lands relative to the body.
                    var strikers = FightAnimationSet.StrikingBones(style).Select(animator.GetBoneTransform).ToArray();
                    var tracked = strikers.Append(animator.GetBoneTransform(HumanBodyBones.Hips)).ToArray();
                    var feet = SampleClip(animator, skeleton.bones, clip, tracked, out var positions);
                    var (start, impact, end, limb) = FindStrike(clip, positions, strikers.Length);
                    int frame = Mathf.Clamp(Mathf.RoundToInt(impact / SampleStep), 0, positions.GetLength(1) - 1);
                    if (FightAnimationSet.LandsExtended(style))
                        limb = Enumerable.Range(0, strikers.Length).OrderByDescending(l =>
                        {
                            var d = positions[l, frame] - positions[strikers.Length, frame];
                            d.y = 0f;
                            return d.sqrMagnitude;
                        }).First();
                    var lands = positions[limb, frame] - positions[strikers.Length, frame];
                    lands.y = 0f;
                    float landingYaw = lands.sqrMagnitude > 1e-4f ? Vector3.SignedAngle(Vector3.forward, lands, Vector3.up) : 0f;
                    var lift = new AnimationCurve();
                    for (int f = 0; f < feet.Length; f += 2) lift.AddKey(f * SampleStep, Mathf.Max(0f, feet[f] - feet[0]));
                    strikes.Add(new FightAnimationSet.Strike
                    {
                        style = style, clip = clip, start = start, impact = impact, end = end,
                        ground = floor - feet[0], lift = lift, liftKept = LiftKept(style), landingYaw = landingYaw,
                    });
                    report.AppendLine($"  {style,-14} {name,-18} {start:0.00} / {impact:0.00} / {end:0.00} of {clip.length:0.00}, " +
                                      $"floor fix {floor - feet[0]:+0.00;-0.00}, leaps {feet.Max() - feet[0]:0.00}, lands {landingYaw:+0;-0}°");
                }

                var set = Asset<FightAnimationSet>(Root + "/Data", "FightAnimations");
                set.footHeight = floor;
                set.relaxedIdle = relaxed;
                set.fightIdle = MixamoClip("fight_idle");
                set.walk = walk;
                set.run = run;
                set.block = MixamoClip("block_idle");
                set.knockout = MixamoClip("knockout");
                set.hits = hits;
                set.strikes = strikes.ToArray();
                set.grounding = grounding.ToArray();
                set.figureGround = MeasureFigureGround(new[] { relaxed, walk, MixamoClip("fight_idle"), MixamoClip("block_idle"), MixamoClip("cross") },
                    grounding, strikes, report);
                set.upperBody = UpperBodyMask();
                Debug.Log(report.ToString());
                EditorUtility.SetDirty(set);
                AssetDatabase.SaveAssets();
                return set;
            }
            finally
            {
                Object.DestroyImmediate(probe);
            }
        }

        /// <summary>
        /// How much more (or less) each body needs lowering than the man the clips were measured on: the bodies
        /// differ in leg length and where the ankle sits. Averaged over a few representative clips.
        /// </summary>
        static float[] MeasureFigureGround(AnimationClip[] clips, List<FightAnimationSet.Grounded> grounding,
            List<FightAnimationSet.Strike> strikes, System.Text.StringBuilder report)
        {
            var result = new float[FigureSpecs.Length];
            for (int i = 1; i < FigureSpecs.Length; i++)
            {
                var probe = new GameObject("Figure Probe");
                try
                {
                    var skeleton = BuildSkeleton(probe.transform, "Probe", FigureSpecs[i].skeleton);
                    var avatar = BuildAvatar(skeleton.figure);
                    var root = skeleton.figure;
                    float floor = Mathf.Min(root.InverseTransformPoint(skeleton.bones["Foot.L"].position).y,
                        root.InverseTransformPoint(skeleton.bones["Foot.R"].position).y);
                    var animator = skeleton.figure.gameObject.AddComponent<Animator>();
                    animator.avatar = avatar;
                    animator.applyRootMotion = false;
                    var deltas = new List<float>();
                    foreach (var clip in clips.Where(c => c != null))
                    {
                        var feet = SampleClip(animator, skeleton.bones, clip, new Transform[0], out _);
                        bool loop = clip.isLooping;
                        float own = floor - (loop ? feet.Min() : feet[0]);
                        float measured = grounding.FirstOrDefault(g => g.clip == clip)?.ground
                                         ?? strikes.FirstOrDefault(s => s.clip == clip)?.ground ?? 0f;
                        deltas.Add(own - measured);
                        report.AppendLine($"  {FigureSpecs[i].name} {clip.name,-14} needs {own:+0.00;-0.00} vs {measured:+0.00;-0.00}");
                    }
                    result[i] = deltas.Count > 0 ? deltas.Average() : 0f;
                    report.AppendLine($"  {FigureSpecs[i].name}: extra floor fix {result[i]:+0.00;-0.00}");
                }
                finally
                {
                    Object.DestroyImmediate(probe);
                }
            }
            return result;
        }

        static AvatarMask UpperBodyMask()
        {
            string path = Root + "/Data/UpperBody.mask";
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
            if (mask == null)
            {
                mask = new AvatarMask();
                AssetDatabase.CreateAsset(mask, path);
            }
            for (var part = AvatarMaskBodyPart.Root; part < AvatarMaskBodyPart.LastBodyPart; part++)
                mask.SetHumanoidBodyPartActive(part, part == AvatarMaskBodyPart.Head || part == AvatarMaskBodyPart.LeftArm ||
                    part == AvatarMaskBodyPart.RightArm || part == AvatarMaskBodyPart.LeftFingers || part == AvatarMaskBodyPart.RightFingers);
            EditorUtility.SetDirty(mask);
            return mask;
        }

        const float SampleStep = 1f / 60f;


        /// <summary>
        /// Plays the clip on the skeleton at 60 fps, recording the given limbs (body space) and the height of the
        /// lowest foot each frame. Leaves the skeleton as it was.
        /// </summary>
        static float[] SampleClip(Animator animator, Dictionary<string, Transform> bones, AnimationClip clip, Transform[] limbs,
            out Vector3[,] positions)
        {
            int frames = Mathf.CeilToInt(clip.length / SampleStep) + 1;
            positions = new Vector3[limbs.Length, frames];
            var lowestFoot = new float[frames];
            var root = animator.transform;
            var saved = root.GetComponentsInChildren<Transform>().Select(t => (t, t.localPosition, t.localRotation)).ToList();

            var graph = PlayableGraph.Create("Measure " + clip.name);
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = AnimationPlayableOutput.Create(graph, "Body", animator);
            var playable = AnimationClipPlayable.Create(graph, clip);
            output.SetSourcePlayable(playable);
            graph.Play();
            for (int f = 0; f < frames; f++)
            {
                playable.SetTime(Mathf.Min(f * SampleStep, clip.length));
                graph.Evaluate(0f);
                for (int l = 0; l < limbs.Length; l++) positions[l, f] = root.InverseTransformPoint(limbs[l].position);
                lowestFoot[f] = Mathf.Min(root.InverseTransformPoint(bones["Foot.L"].position).y,
                    root.InverseTransformPoint(bones["Foot.R"].position).y);
            }
            graph.Destroy();
            foreach (var (t, p, r) in saved)
            {
                t.localPosition = p;
                t.localRotation = r;
            }
            return lowestFoot;
        }

        /// <summary>
        /// The hit lands where the fastest striking limb peaks; the wind-up starts where that limb last sat still
        /// before it, and the move has settled shortly after it slows down again. Only the first
        /// <paramref name="limbs"/> tracked positions are strikers. Returns which one landed it.
        /// </summary>
        static (float start, float impact, float end, int limb) FindStrike(AnimationClip clip, Vector3[,] positions, int limbs)
        {
            int frames = positions.GetLength(1);
            float Speed(int l, int f)
            {
                int a = Mathf.Max(0, f - 1), b = Mathf.Min(frames - 1, f + 1);
                return b > a ? (positions[l, b] - positions[l, a]).magnitude / ((b - a) * SampleStep) : 0f;
            }

            int best = -1, limb = 0;
            float peak = 0f;
            for (int l = 0; l < limbs; l++)
                for (int f = Mathf.FloorToInt(frames * 0.08f); f < Mathf.CeilToInt(frames * 0.92f); f++)
                {
                    float v = Speed(l, f);
                    if (v > peak)
                    {
                        peak = v;
                        best = f;
                        limb = l;
                    }
                }
            if (best < 0 || peak < 0.05f) return (0f, clip.length * 0.5f, clip.length, 0);

            int s = best;
            while (s > 0 && Speed(limb, s) > peak * 0.18f) s--;
            int e = best;
            while (e < frames - 1 && Speed(limb, e) > peak * 0.15f) e++;
            return (Mathf.Max(0f, s * SampleStep - 0.1f), best * SampleStep, Mathf.Min(clip.length, e * SampleStep + 0.25f), limb);
        }

        /// <summary>
        /// One of the source model's own animations as a humanoid clip: each frame is posed on the skeleton
        /// and read back as muscle values. Fingers are left out (the hands are held in fists anyway).
        /// </summary>
        static AnimationClip ConvertClip(Skeleton skeleton, Avatar avatar, string name)
        {
            var glb = skeleton.glb;
            var animation = glb.List("animations").Select(Glb.Obj).FirstOrDefault(a => Glb.Str(a, "name") == "CharacterArmature|" + name);
            if (animation == null)
            {
                Debug.LogWarning($"[Nine to Fight] No '{name}' animation in the character file.");
                return null;
            }

            var samplers = Glb.List(animation, "samplers").Select(Glb.Obj).ToList();
            var channels = new List<(int node, bool rotation, float[] times, float[] values)>();
            float length = 0f;
            foreach (var channel in Glb.List(animation, "channels").Select(Glb.Obj))
            {
                var target = Glb.Obj(channel["target"]);
                string path = Glb.Str(target, "path");
                if (path != "translation" && path != "rotation") continue;
                var sampler = samplers[Glb.Int(channel, "sampler")];
                var times = glb.Read(Glb.Int(sampler, "input"), out _);
                var values = glb.Read(Glb.Int(sampler, "output"), out _);
                channels.Add((Glb.Int(target, "node"), path == "rotation", times, values));
                length = Mathf.Max(length, times[times.Length - 1]);
            }

            var all = skeleton.figure.GetComponentsInChildren<Transform>().Select(t => (t, t.localPosition, t.localRotation)).ToList();
            var handler = new HumanPoseHandler(avatar, skeleton.figure);
            var pose = new HumanPose();
            var muscles = Enumerable.Range(0, HumanTrait.MuscleCount).Where(i => !IsFingerMuscle(HumanTrait.MuscleName[i])).ToArray();
            var rootCurves = new[] { "RootT.x", "RootT.y", "RootT.z", "RootQ.x", "RootQ.y", "RootQ.z", "RootQ.w" }
                .ToDictionary(n => n, n => new AnimationCurve());
            var muscleCurves = muscles.ToDictionary(i => i, i => new AnimationCurve());

            // Feet hang off the shins here, but the source animates them from the root (IK): pose them in world space.
            var feet = new Dictionary<int, (Vector3? p, Quaternion? r)>();
            int frames = Mathf.Max(2, Mathf.RoundToInt(length * 30f) + 1);
            for (int f = 0; f < frames; f++)
            {
                float t = Mathf.Min(f / 30f, length);
                feet.Clear();
                foreach (var (node, rotation, times, values) in channels)
                {
                    if (!skeleton.made.TryGetValue(node, out var bone)) continue;
                    var v = SampleChannel(times, values, rotation ? 4 : 3, t, rotation);
                    bool foot = bone.name == "Foot.L" || bone.name == "Foot.R";
                    if (rotation)
                    {
                        var q = new Quaternion(v[0], -v[1], -v[2], v[3]);
                        if (foot) feet[node] = (feet.TryGetValue(node, out var e) ? e.p : null, q);
                        else bone.localRotation = q;
                    }
                    else
                    {
                        var p = new Vector3(-v[0], v[1], v[2]);
                        if (foot) feet[node] = (p, feet.TryGetValue(node, out var e) ? e.r : null);
                        else bone.localPosition = p;
                    }
                }
                foreach (var kv in feet)
                {
                    var bone = skeleton.made[kv.Key];
                    var sourceParent = skeleton.made[glb.Parent(kv.Key)];
                    var rest = Glb.ToUnity(glb.Local(kv.Key));
                    bone.position = sourceParent.TransformPoint(kv.Value.p ?? Glb.Position(rest));
                    bone.rotation = sourceParent.rotation * (kv.Value.r ?? Glb.Rotation(rest));
                }

                handler.GetHumanPose(ref pose);
                rootCurves["RootT.x"].AddKey(t, pose.bodyPosition.x);
                rootCurves["RootT.y"].AddKey(t, pose.bodyPosition.y);
                rootCurves["RootT.z"].AddKey(t, pose.bodyPosition.z);
                rootCurves["RootQ.x"].AddKey(t, pose.bodyRotation.x);
                rootCurves["RootQ.y"].AddKey(t, pose.bodyRotation.y);
                rootCurves["RootQ.z"].AddKey(t, pose.bodyRotation.z);
                rootCurves["RootQ.w"].AddKey(t, pose.bodyRotation.w);
                foreach (int i in muscles) muscleCurves[i].AddKey(t, pose.muscles[i]);
            }
            handler.Dispose();
            foreach (var (t, p, r) in all)
            {
                t.localPosition = p;
                t.localRotation = r;
            }

            var clip = new AnimationClip { name = name, frameRate = 30f };
            foreach (var kv in rootCurves)
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), kv.Key), kv.Value);
            foreach (var kv in muscleCurves)
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), HumanTrait.MuscleName[kv.Key]), kv.Value);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            settings.loopBlendOrientation = true;
            settings.loopBlendPositionY = true;
            settings.keepOriginalOrientation = true;
            // Centre of mass: "from the feet" needs an offset Unity only works out when importing a model.
            settings.keepOriginalPositionY = false;
            settings.heightFromFeet = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            string assetPath = $"{GeneratedClipsDir}/{name}.anim";
            AssetDatabase.DeleteAsset(assetPath);
            AssetDatabase.CreateAsset(clip, assetPath);
            return clip;
        }

        static bool IsFingerMuscle(string muscle) =>
            muscle.Contains("Thumb") || muscle.Contains("Index") || muscle.Contains("Middle") || muscle.Contains("Ring") || muscle.Contains("Little");

        static float[] SampleChannel(float[] times, float[] values, int components, float t, bool rotation)
        {
            var result = new float[components];
            int last = times.Length - 1;
            int i = 0;
            while (i < last && times[i + 1] < t) i++;
            int j = Mathf.Min(i + 1, last);
            float k = j == i || times[j] <= times[i] ? 0f : Mathf.Clamp01((t - times[i]) / (times[j] - times[i]));
            float sign = 1f;
            if (rotation)
            {
                float dot = 0f;
                for (int c = 0; c < 4; c++) dot += values[i * 4 + c] * values[j * 4 + c];
                if (dot < 0f) sign = -1f;
            }
            float norm = 0f;
            for (int c = 0; c < components; c++)
            {
                result[c] = Mathf.Lerp(values[i * components + c], values[j * components + c] * sign, k);
                norm += result[c] * result[c];
            }
            if (rotation && norm > 1e-8f)
            {
                norm = Mathf.Sqrt(norm);
                for (int c = 0; c < 4; c++) result[c] /= norm;
            }
            return result;
        }
    }
}
