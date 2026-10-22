using System;
using System.Collections.Generic;
using System.Linq;
using NineToFight.Characters;
using UnityEditor;
using UnityEngine;

namespace NineToFight.EditorTools
{
    /// <summary>
    /// Office workers built from Quaternius' modular characters (CC0, Art/Characters/Quaternius).
    /// Each worker carries a man and a woman skeleton with interchangeable heads and outfits;
    /// CharacterLook picks one per person. Each skeleton gets a humanoid avatar and an Animator, and plays
    /// the fight clips (Mixamo, plus the models' own idle/walk/run converted to humanoid). The old
    /// procedural pivots (Visual/Model/Chest/ArmPivot/Elbow/HandSocket...) are kept as the fallback rig.
    /// </summary>
    public static partial class StarterContentBuilder
    {
        const string CharacterArtDir = Root + "/Art/Characters/Quaternius";
        const string CharacterMeshDir = Root + "/Art/Characters/Generated";
        /// <summary>The source figures stand ~1.83 m; the office is built around ~2.2 m people.</summary>
        const float FigureScale = 1.2f;
        /// <summary>Arms hang this many degrees out from the body at rest.</summary>
        const float ArmSplay = 4f;

        enum Tint { Keep, Skin, Hair, Garment, Accent }

        class FigureSpec
        {
            public string name, skeleton;
            public string[] heads;
            public (string file, bool formal)[] outfits;
        }

        static readonly FigureSpec[] FigureSpecs =
        {
            new FigureSpec
            {
                name = "Man", skeleton = "Man_Suit",
                heads = new[] { "Man_Suit", "Man_Casual", "Man_Hoodie" },
                outfits = new[] { ("Man_Suit", true), ("Man_Casual", false), ("Man_Hoodie", false) },
            },
            new FigureSpec
            {
                name = "Woman", skeleton = "Woman_Suit",
                heads = new[] { "Woman_Suit", "Woman_Dress" },
                outfits = new[] { ("Woman_Suit", true), ("Woman_Blouse", false), ("Woman_Dress", false) },
            },
        };

        /// <summary>Which parts take the class colour (main garment) and tie colour (accent).</summary>
        static Tint TintOf(string file, string part, string material)
        {
            if (material.StartsWith("Skin")) return Tint.Skin;
            if (part == "Head")
                return material is "Hair" or "Hair_Blond" or "Hair_Brown" or "Eyebrows" or "Red" ? Tint.Hair : Tint.Keep;
            switch ($"{file}/{part}/{material}")
            {
                case "Man_Suit/Body/Suit":
                case "Man_Casual/Body/LightBrown":
                case "Man_Hoodie/Body/Purple":
                case "Woman_Suit/Body/Black":
                case "Woman_Blouse/Body/White":
                case "Woman_Dress/Body/LimeGreen":
                case "Woman_Dress/Legs/LimeGreen":
                    return Tint.Garment;
                case "Man_Suit/Body/Tie":
                case "Man_Casual/Feet/Red_Dark":
                case "Man_Hoodie/Feet/Purple":
                case "Woman_Dress/Body/Gold":
                case "Woman_Dress/Feet/Red":
                case "Woman_Blouse/Feet/Grey":
                    return Tint.Accent;
            }
            return Tint.Keep;
        }

        static float SmoothnessOf(string material) =>
            material.StartsWith("Skin") ? 0.32f : material.StartsWith("Hair") ? 0.3f : material == "Eye" ? 0.7f :
            material is "Tie" or "Gold" ? 0.5f : 0.12f;

        class PartPrimitive
        {
            public string part, material;
            public Mesh mesh;
            public Material shared;
            public string[] bones;
        }

        class BuiltFigure
        {
            public Transform root;
            public Dictionary<string, Transform> bones;
            public readonly List<GameObject> heads = new List<GameObject>();
            public readonly List<CharacterLook.Outfit> outfits = new List<CharacterLook.Outfit>();
            public Vector3 grip;
            public Quaternion gripRotation, punchRotation;
            public Vector3 headForward;
            public Animator animator;
            public Transform[] fingers;
            public Quaternion[] fist;
        }

        /// <summary>A bind-pose (T-pose) skeleton built from a source file, feet hanging off the shins.</summary>
        class Skeleton
        {
            public Transform figure;
            public Glb glb;
            public Dictionary<int, Transform> made;
            public Dictionary<string, Transform> bones;
        }

        static readonly Dictionary<string, List<PartPrimitive>> CharacterParts = new Dictionary<string, List<PartPrimitive>>();

        static CharacterVisual BuildOfficeWorker(Transform root)
        {
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root, false);
            var model = new GameObject("Model").transform;
            model.SetParent(visual, false);

            // The procedural rig: invisible pivots, placed on the first figure's joints below.
            Transform Pivot(string name, Transform parent)
            {
                var t = new GameObject(name).transform;
                t.SetParent(parent, false);
                return t;
            }
            var chest = Pivot("Chest", model);
            var head = Pivot("Head", chest);
            var frontArm = Pivot("ArmPivot", chest);
            var frontElbow = Pivot("Elbow", frontArm);
            var socket = Pivot("HandSocket", frontElbow);
            var backArm = Pivot("ArmBack", chest);
            var backElbow = Pivot("Elbow", backArm);
            var legLeft = Pivot("LegL", model);
            var kneeLeft = Pivot("KneeL", legLeft);
            var ankleLeft = Pivot("AnkleL", kneeLeft);
            var legRight = Pivot("LegR", model);
            var kneeRight = Pivot("KneeR", legRight);
            var ankleRight = Pivot("AnkleR", kneeRight);

            var skin = new List<Renderer>();
            var hair = new List<Renderer>();
            var garments = new List<Renderer>();
            var accents = new List<Renderer>();
            var figures = FigureSpecs.Select(spec => BuildFigure(model, spec, skin, hair, garments, accents)).ToList();

            // Parents before children, so placing one doesn't drag the next.
            var b = figures[0].bones;
            chest.position = b["Abdomen"].position;
            head.position = b["Head"].position;
            frontArm.position = b["UpperArm.R"].position;
            frontElbow.position = b["LowerArm.R"].position;
            socket.position = b["Wrist.R"].TransformPoint(figures[0].grip);
            backArm.position = b["UpperArm.L"].position;
            backElbow.position = b["LowerArm.L"].position;
            legLeft.position = b["UpperLeg.L"].position;
            kneeLeft.position = b["LowerLeg.L"].position;
            ankleLeft.position = b["Foot.L"].position;
            legRight.position = b["UpperLeg.R"].position;
            kneeRight.position = b["LowerLeg.R"].position;
            ankleRight.position = b["Foot.R"].position;
            socket.localScale = Vector3.one;

            // Pivot -> bone, parents first. The waist pivot curls the whole spine and the head turns the neck too.
            var drive = new (Transform driver, string bone, float weight)[]
            {
                (chest, "Abdomen", 0.3f), (chest, "Torso", 0.65f), (chest, "Chest", 1f),
                (head, "Neck", 0.45f), (head, "Head", 1f),
                (frontArm, "UpperArm.R", 1f), (frontElbow, "LowerArm.R", 1f), (socket, "Wrist.R", 1f),
                (backArm, "UpperArm.L", 1f), (backElbow, "LowerArm.L", 1f),
                (legLeft, "UpperLeg.L", 1f), (kneeLeft, "LowerLeg.L", 1f), (ankleLeft, "Foot.L", 1f),
                (legRight, "UpperLeg.R", 1f), (kneeRight, "LowerLeg.R", 1f), (ankleRight, "Foot.R", 1f),
            };
            var rigs = figures.Select(f => new RigFollower.Rig
            {
                root = f.root.gameObject,
                bones = drive.Select(d => f.bones[d.bone]).ToArray(),
                drivers = drive.Select(d => d.driver).ToArray(),
                offsets = drive.Select(d => Quaternion.Inverse(d.driver.rotation) * f.bones[d.bone].rotation).ToArray(),
                weights = drive.Select(d => d.weight).ToArray(),
                driverRest = drive.Select(d => d.driver.localPosition).ToArray(),
                boneRest = drive.Select(d => f.bones[d.bone].localPosition).ToArray(),
                hand = f.bones["Wrist.R"],
                grip = f.grip,
                gripRotation = f.gripRotation,
                punchRotation = f.punchRotation,
                animator = f.animator,
                fingers = f.fingers,
                fist = f.fist,
                chest = f.bones["Chest"],
                headForward = f.headForward,
                neck = f.bones["Neck"],
            }).ToArray();

            // Default look until CharacterLook picks one: the first figure, head and outfit.
            for (int i = 0; i < figures.Count; i++)
            {
                figures[i].root.gameObject.SetActive(i == 0);
                for (int h = 0; h < figures[i].heads.Count; h++) figures[i].heads[h].SetActive(h == 0);
                for (int o = 0; o < figures[i].outfits.Count; o++) figures[i].outfits[o].root.SetActive(o == 0);
            }

            var held = visual.gameObject.AddComponent<HeldItemDisplay>();
            Wire(held, "catalog", propCatalog);
            Wire(held, "handSocket", socket);

            var cv = visual.gameObject.AddComponent<CharacterVisual>();
            Wire(cv, "model", model);
            Wire(cv, "chest", chest);
            Wire(cv, "attackArm", frontArm);
            Wire(cv, "backArm", backArm);
            Wire(cv, "elbowFront", frontElbow);
            Wire(cv, "elbowBack", backElbow);
            Wire(cv, "wrist", socket);
            Wire(cv, "legLeft", legLeft);
            Wire(cv, "legRight", legRight);
            Wire(cv, "kneeLeft", kneeLeft);
            Wire(cv, "kneeRight", kneeRight);
            Wire(cv, "ankleLeft", ankleLeft);
            Wire(cv, "ankleRight", ankleRight);
            Wire(cv, "head", head);
            SetArray(cv, "shirtRenderers", garments.ToArray());
            SetArray(cv, "accentRenderers", accents.ToArray());
            Wire(cv, "animationSet", fightAnimations);

            var follower = visual.gameObject.AddComponent<RigFollower>();
            follower.Configure(rigs, socket);

            var look = visual.gameObject.AddComponent<CharacterLook>();
            look.Configure(figures.Select((f, i) => new CharacterLook.Figure
            {
                name = FigureSpecs[i].name,
                heads = f.heads.ToArray(),
                outfits = f.outfits.ToArray(),
            }).ToArray(), skin.ToArray(), hair.ToArray());

            Wire(root.GetComponent<CharacterMotor2_5D>(), "visual", visual);
            return cv;
        }

        static BuiltFigure BuildFigure(Transform model, FigureSpec spec, List<Renderer> skin, List<Renderer> hair,
            List<Renderer> garments, List<Renderer> accents)
        {
            var skeleton = BuildSkeleton(model, spec.name, spec.skeleton);
            var figure = skeleton.figure;
            var bones = skeleton.bones;
            var built = new BuiltFigure { root = figure, bones = bones };

            // Humanoid avatar from the T-pose, so the fight clips retarget onto this body.
            var avatar = BuildAvatar(figure);
            EnsureFolder(CharacterMeshDir);
            string avatarPath = $"{CharacterMeshDir}/{spec.name}_Avatar.asset";
            AssetDatabase.DeleteAsset(avatarPath);
            AssetDatabase.CreateAsset(avatar, avatarPath);
            built.animator = figure.gameObject.AddComponent<Animator>();
            built.animator.avatar = avatar;
            built.animator.applyRootMotion = false;
            built.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            PoseAtRest(figure, bones);

            // Closed fists (held on top of every clip) and the grip: the centre of the curled fingers, with
            // the item running across the palm, its business end out past the thumb.
            var fingerBones = new List<Transform>();
            foreach (var side in new[] { "L", "R" })
                foreach (var finger in new[] { "Thumb1", "Thumb2", "Thumb3", "Index2", "Index3", "Index4", "Middle2", "Middle3",
                             "Middle4", "Ring2", "Ring3", "Ring4", "Pinky2", "Pinky3", "Pinky4" })
                    fingerBones.Add(bones[$"{finger}.{side}"]);
            built.fingers = fingerBones.ToArray();
            built.fist = fingerBones.Select(f => f.localRotation).ToArray();
            var hand = bones["Wrist.R"];
            var curled = new[] { "Index", "Middle", "Ring", "Pinky" }.SelectMany(f => new[] { 2, 3, 4 }.Select(i => bones[$"{f}{i}.R"].position)).ToList();
            var center = curled.Aggregate(Vector3.zero, (a, b) => a + b) / curled.Count;
            var towardPinky = (bones["Pinky2.R"].position - bones["Index2.R"].position).normalized;
            var knuckles = Vector3.ProjectOnPlane(bones["Middle2.R"].position - hand.position, towardPinky).normalized;
            built.grip = hand.InverseTransformPoint(center);
            built.gripRotation = Quaternion.Inverse(hand.rotation) * Quaternion.LookRotation(knuckles, towardPinky);
            built.headForward = Quaternion.Inverse(bones["Head"].rotation) * figure.forward;
            // Punch grip: the item's business end straight out the front of the fist.
            built.punchRotation = Quaternion.Inverse(hand.rotation) * Quaternion.LookRotation(-towardPinky, -knuckles);

            foreach (var file in spec.heads)
            {
                var group = new GameObject("Head_" + file);
                group.transform.SetParent(figure, false);
                AddParts(group.transform, spec.name, file, part => part == "Head", built, skin, hair, garments, accents);
                built.heads.Add(group);
            }
            foreach (var (file, formal) in spec.outfits)
            {
                var group = new GameObject("Outfit_" + file);
                group.transform.SetParent(figure, false);
                AddParts(group.transform, spec.name, file, part => part != "Head", built, skin, hair, garments, accents);
                built.outfits.Add(new CharacterLook.Outfit { name = file, formal = formal, root = group });
            }
            return built;
        }

        /// <summary>Every joint of the file's first skin and its ancestors, snapped into the bind (T) pose.</summary>
        static Skeleton BuildSkeleton(Transform parent, string name, string file)
        {
            var figure = new GameObject("Figure_" + name).transform;
            figure.SetParent(parent, false);
            figure.localScale = Vector3.one * FigureScale;
            var glb = new Glb($"{CharacterArtDir}/{file}.glb");
            int skinned = Enumerable.Range(0, glb.NodeCount).First(i => glb.Node(i).ContainsKey("skin"));
            var skinJson = Glb.Obj(glb.List("skins")[Glb.Int(glb.Node(skinned), "skin")]);
            var joints = Glb.List(skinJson, "joints").Select(j => Convert.ToInt32(j)).ToArray();
            var needed = new HashSet<int>();
            foreach (int j in joints)
                for (int n = j; n >= 0; n = glb.Parent(n)) needed.Add(n);

            var made = new Dictionary<int, Transform>();
            void Make(int index, Transform parent)
            {
                var t = new GameObject(glb.NodeName(index)).transform;
                t.SetParent(parent, false);
                var local = Glb.ToUnity(glb.Local(index));
                t.localPosition = Glb.Position(local);
                t.localRotation = Glb.Rotation(local);
                t.localScale = Glb.Scale(local);
                made[index] = t;
                foreach (var child in Glb.List(glb.Node(index), "children"))
                {
                    int c = Convert.ToInt32(child);
                    if (needed.Contains(c)) Make(c, t);
                }
            }
            for (int i = 0; i < glb.NodeCount; i++)
                if (glb.Parent(i) < 0 && needed.Contains(i)) Make(i, figure);

            int Depth(int n) { int d = 0; for (int p = glb.Parent(n); p >= 0; p = glb.Parent(p)) d++; return d; }
            var inverseBinds = glb.Read(Glb.Int(skinJson, "inverseBindMatrices"), out _);
            var meshSpace = glb.Global(skinned);
            foreach (int k in Enumerable.Range(0, joints.Length).OrderBy(k => Depth(joints[k])))
            {
                var bind = Glb.ToUnity(meshSpace * Glb.Mat4(inverseBinds, k).inverse);
                var t = made[joints[k]];
                t.position = figure.TransformPoint(Glb.Position(bind));
                t.rotation = figure.rotation * Glb.Rotation(bind);
            }
            var bones = joints.ToDictionary(j => glb.NodeName(j), j => made[j]);

            // The source rig plants its feet by IK from the root; here they hang off the shins.
            foreach (var side in new[] { "L", "R" }) bones["Foot." + side].SetParent(bones["LowerLeg." + side], true);
            return new Skeleton { figure = figure, glb = glb, made = made, bones = bones };
        }

        static readonly (string human, string bone)[] HumanBones =
        {
            ("Hips", "Body"), ("Spine", "Abdomen"), ("Chest", "Torso"), ("UpperChest", "Chest"), ("Neck", "Neck"), ("Head", "Head"),
            ("LeftShoulder", "Shoulder.L"), ("LeftUpperArm", "UpperArm.L"), ("LeftLowerArm", "LowerArm.L"), ("LeftHand", "Wrist.L"),
            ("RightShoulder", "Shoulder.R"), ("RightUpperArm", "UpperArm.R"), ("RightLowerArm", "LowerArm.R"), ("RightHand", "Wrist.R"),
            ("LeftUpperLeg", "UpperLeg.L"), ("LeftLowerLeg", "LowerLeg.L"), ("LeftFoot", "Foot.L"),
            ("RightUpperLeg", "UpperLeg.R"), ("RightLowerLeg", "LowerLeg.R"), ("RightFoot", "Foot.R"),
            ("Left Thumb Proximal", "Thumb1.L"), ("Left Thumb Intermediate", "Thumb2.L"), ("Left Thumb Distal", "Thumb3.L"),
            ("Left Index Proximal", "Index2.L"), ("Left Index Intermediate", "Index3.L"), ("Left Index Distal", "Index4.L"),
            ("Left Middle Proximal", "Middle2.L"), ("Left Middle Intermediate", "Middle3.L"), ("Left Middle Distal", "Middle4.L"),
            ("Left Ring Proximal", "Ring2.L"), ("Left Ring Intermediate", "Ring3.L"), ("Left Ring Distal", "Ring4.L"),
            ("Left Little Proximal", "Pinky2.L"), ("Left Little Intermediate", "Pinky3.L"), ("Left Little Distal", "Pinky4.L"),
            ("Right Thumb Proximal", "Thumb1.R"), ("Right Thumb Intermediate", "Thumb2.R"), ("Right Thumb Distal", "Thumb3.R"),
            ("Right Index Proximal", "Index2.R"), ("Right Index Intermediate", "Index3.R"), ("Right Index Distal", "Index4.R"),
            ("Right Middle Proximal", "Middle2.R"), ("Right Middle Intermediate", "Middle3.R"), ("Right Middle Distal", "Middle4.R"),
            ("Right Ring Proximal", "Ring2.R"), ("Right Ring Intermediate", "Ring3.R"), ("Right Ring Distal", "Ring4.R"),
            ("Right Little Proximal", "Pinky2.R"), ("Right Little Intermediate", "Pinky3.R"), ("Right Little Distal", "Pinky4.R"),
        };

        /// <summary>Humanoid avatar for a skeleton standing in its T-pose. The source's "Body" bone is the pelvis root.</summary>
        static Avatar BuildAvatar(Transform figure)
        {
            var description = new HumanDescription
            {
                human = HumanBones.Select(h => new HumanBone { humanName = h.human, boneName = h.bone, limit = new HumanLimit { useDefaultValues = true } }).ToArray(),
                skeleton = figure.GetComponentsInChildren<Transform>(true).Select(t => new SkeletonBone
                {
                    name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale,
                }).ToArray(),
                upperArmTwist = 0.5f, lowerArmTwist = 0.5f, upperLegTwist = 0.5f, lowerLegTwist = 0.5f,
                armStretch = 0.05f, legStretch = 0.05f, feetSpacing = 0f, hasTranslationDoF = false,
            };
            var avatar = AvatarBuilder.BuildHumanAvatar(figure.gameObject, description);
            avatar.name = figure.name + " Avatar";
            if (!avatar.isValid || !avatar.isHuman) Debug.LogError($"[Nine to Fight] {figure.name}: humanoid avatar is not valid.");
            return avatar;
        }

        /// <summary>
        /// From the T-pose to the procedural rig's rest pose: arms hanging just off the body, legs
        /// straight, hands balled into fists.
        /// </summary>
        static void PoseAtRest(Transform figure, Dictionary<string, Transform> bones)
        {
            void Aim(string bone, string toward, Vector3 direction)
            {
                var b = bones[bone];
                b.rotation = Quaternion.FromToRotation(bones[toward].position - b.position, direction) * b.rotation;
            }

            var down = -figure.up;
            foreach (var (side, sign) in new[] { ("R", 1f), ("L", -1f) })
            {
                float splay = ArmSplay * Mathf.Deg2Rad;
                var hang = figure.TransformDirection(new Vector3(Mathf.Sin(splay) * sign, -Mathf.Cos(splay), 0f));
                Aim("UpperArm." + side, "LowerArm." + side, hang);
                Aim("LowerArm." + side, "Wrist." + side, hang);
                Aim("Wrist." + side, "Middle1." + side, hang);
                Aim("UpperLeg." + side, "LowerLeg." + side, down);
                Aim("LowerLeg." + side, "Foot." + side, down);

                // Fists: the fingers fold about the knuckle line toward the palm, the thumb wraps across them.
                var palm = figure.TransformDirection(new Vector3(-sign, 0f, 0f));
                // Which way folds toward the palm is decided once, on the straight hand: once a finger has
                // curled, it already points into the palm and can't tell the directions apart any more.
                var knuckles = (bones[$"Index2.{side}"].position - bones[$"Pinky2.{side}"].position).normalized;
                var fingerDir = bones[$"Middle2.{side}"].position - bones[$"Middle1.{side}"].position;
                knuckles *= TowardPalm(fingerDir, knuckles, palm);
                foreach (var finger in new[] { "Index", "Middle", "Ring", "Pinky" })
                {
                    Fold(bones[$"{finger}2.{side}"], knuckles, 90f);
                    Fold(bones[$"{finger}3.{side}"], knuckles, 95f);
                    Fold(bones[$"{finger}4.{side}"], knuckles, 65f);
                }
                var thumb = bones[$"Thumb1.{side}"];
                var thumbDir = bones[$"Thumb2.{side}"].position - thumb.position;
                var thumbHinge = Vector3.Cross(thumbDir, palm).normalized;
                thumbHinge *= TowardPalm(thumbDir, thumbHinge, palm);
                var tip = bones[$"Thumb3.{side}"].position - thumb.position;
                var across = bones[$"Middle3.{side}"].position - thumb.position;
                thumb.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(tip, across), 0.55f) * thumb.rotation;
                Fold(bones[$"Thumb2.{side}"], thumbHinge, 30f);
                Fold(bones[$"Thumb3.{side}"], thumbHinge, 35f);
            }
        }

        /// <summary>+1 if turning <paramref name="along"/> about the hinge (positive angle) swings it toward the palm, else -1.</summary>
        static float TowardPalm(Vector3 along, Vector3 hinge, Vector3 palm) =>
            Vector3.Dot(Quaternion.AngleAxis(30f, hinge) * along, palm) >= Vector3.Dot(along, palm) ? 1f : -1f;

        /// <summary>Bends a joint about a hinge (already oriented to fold toward the palm).</summary>
        static void Fold(Transform bone, Vector3 hinge, float angle) =>
            bone.rotation = Quaternion.AngleAxis(angle, hinge) * bone.rotation;

        static void AddParts(Transform group, string figureName, string file, Func<string, bool> include, BuiltFigure figure,
            List<Renderer> skin, List<Renderer> hair, List<Renderer> garments, List<Renderer> accents)
        {
            var hips = figure.bones["Hips"];
            // Generous bounds (arms overhead, lying flat on a knockout) in the hips' space.
            var world = new Bounds(figure.root.TransformPoint(new Vector3(0f, 0.95f, 0.05f)), Vector3.one * 2.6f * FigureScale);
            var local = new Bounds(hips.InverseTransformPoint(world.center), Vector3.zero);
            for (int c = 0; c < 8; c++)
            {
                var corner = world.center + Vector3.Scale(world.extents, new Vector3(c & 1, (c >> 1) & 1, (c >> 2) & 1) * 2f - Vector3.one);
                local.Encapsulate(hips.InverseTransformPoint(corner));
            }

            foreach (var p in LoadParts(figureName, file))
            {
                if (!include(p.part)) continue;
                var go = new GameObject($"{p.part}_{p.material}");
                go.transform.SetParent(group, false);
                var smr = go.AddComponent<SkinnedMeshRenderer>();
                smr.sharedMesh = p.mesh;
                smr.sharedMaterial = p.shared;
                smr.bones = p.bones.Select(n => figure.bones[n]).ToArray();
                smr.rootBone = hips;
                smr.localBounds = local;
                switch (TintOf(file, p.part, p.material))
                {
                    case Tint.Skin: skin.Add(smr); break;
                    case Tint.Hair: hair.Add(smr); break;
                    case Tint.Garment: garments.Add(smr); break;
                    case Tint.Accent: accents.Add(smr); break;
                }
            }
        }

        /// <summary>Every skinned primitive in a source file as a mesh asset (built once per content build).</summary>
        static List<PartPrimitive> LoadParts(string figureName, string file)
        {
            if (CharacterParts.TryGetValue(file, out var cached)) return cached;
            var glb = new Glb($"{CharacterArtDir}/{file}.glb");
            var parts = new List<PartPrimitive>();
            for (int n = 0; n < glb.NodeCount; n++)
            {
                var node = glb.Node(n);
                if (!node.ContainsKey("mesh") || !node.ContainsKey("skin")) continue;
                var meshJson = Glb.Obj(glb.List("meshes")[Glb.Int(node, "mesh")]);
                string meshName = Glb.Str(meshJson, "name") ?? glb.NodeName(n);
                string part = meshName.Substring(meshName.LastIndexOf('_') + 1);

                var skinJson = Glb.Obj(glb.List("skins")[Glb.Int(node, "skin")]);
                var joints = Glb.List(skinJson, "joints").Select(j => Convert.ToInt32(j)).ToArray();
                var inverseBinds = glb.Read(Glb.Int(skinJson, "inverseBindMatrices"), out _);
                var bindposes = new Matrix4x4[joints.Length];
                for (int k = 0; k < joints.Length; k++) bindposes[k] = Glb.ToUnity(Glb.Mat4(inverseBinds, k));
                var boneNames = joints.Select(glb.NodeName).ToArray();

                foreach (var primitive in Glb.List(meshJson, "primitives").Select(Glb.Obj))
                {
                    var (material, color) = glb.Material(Glb.Int(primitive, "material"));
                    parts.Add(new PartPrimitive
                    {
                        part = part,
                        material = material,
                        mesh = glb.SkinnedPrimitive(primitive, bindposes, $"{file}_{part}_{material}"),
                        shared = Mat($"Char_{figureName}_{material}", color, SmoothnessOf(material)),
                        bones = boneNames,
                    });
                }
            }

            EnsureFolder(CharacterMeshDir);
            string path = $"{CharacterMeshDir}/{file}.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(parts[0].mesh, path);
            foreach (var p in parts.Skip(1)) AssetDatabase.AddObjectToAsset(p.mesh, path);

            CharacterParts[file] = parts;
            return parts;
        }
    }
}
