using System.Collections.Generic;
using System.Linq;
using NineToFight.Characters;
using NineToFight.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace NineToFight.Tests
{
    /// <summary>
    /// Every office item uses its authored grasp on both bodies
    /// without running through the fingers or the palm: the item's surface is sampled and measured against
    /// the finger and palm bones, each taken as a capsule.
    /// </summary>
    public class HeldItemFitTests
    {
        [Test]
        public void EveryItemHasAnIndividuallyAuthoredGripForEachBody()
        {
            // Serialized grips are never null, so check they were actually fitted: a fresh ItemHoldPose sits at the
            // hand's origin with every finger in the default fist, which is what an unfitted item would show.
            var catalog = AssetDatabase.LoadAssetAtPath<PropCatalog>("Assets/_Project/Data/PropCatalog.asset");
            var unfitted = new ItemHoldPose();
            var problems = new List<string>();
            foreach (var entry in catalog.Entries)
            foreach (var (body, grip) in new[] { ("man", entry.manGrip), ("woman", entry.womanGrip) })
            {
                if (grip == null) { problems.Add($"{entry.id}: no {body} grip"); continue; }
                if (grip.position.sqrMagnitude < 1e-8f && grip.eulerAngles.sqrMagnitude < 1e-6f)
                    problems.Add($"{entry.id}: {body} grip is at the hand origin (never fitted)");
                bool defaultFingers = grip.thumb == unfitted.thumb && grip.index == unfitted.index && grip.middle == unfitted.middle &&
                                      grip.ring == unfitted.ring && grip.pinky == unfitted.pinky;
                if (defaultFingers) problems.Add($"{entry.id}: {body} fingers are the default fist (never posed)");
            }
            Assert.IsEmpty(problems, "Items need individual holding positions and finger poses, not one shared fist:\n" + string.Join("\n", problems));
        }

        /// <summary>Half-thickness of a finger and of the palm, in metres (the figures stand ~2.2 m).</summary>
        const float FingerRadius = 0.011f, PalmRadius = 0.014f;
        /// <summary>
        /// How deep an item may press into a finger or the palm: a grip squeezes the flesh, but the item never
        /// reaches the bone (an item through the hand reaches the full radius).
        /// </summary>
        const float Tolerance = 0.0085f;
        const float SampleSpacing = 0.004f;
        /// <summary>The hand lies within this distance of the grip (the wrist is ~16 cm away).</summary>
        const float Reach = 0.2f;

        struct Segment
        {
            public Vector3 a, b;
            public float radius;
            public string name;
            public string finger;
        }

        static float Distance(Vector3 p, Segment s)
        {
            var ab = s.b - s.a;
            float t = Mathf.Clamp01(Vector3.Dot(p - s.a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-9f));
            return Vector3.Distance(p, s.a + ab * t);
        }

        /// <summary>The fingers and palm of a right fist, in world space.</summary>
        static List<Segment> Fist(Transform figure)
        {
            var bones = figure.GetComponentsInChildren<Transform>(true).GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
            var segments = new List<Segment>();
            void Add(string a, string b, float r)
            {
                if (bones.TryGetValue(a, out var ta) && bones.TryGetValue(b, out var tb))
                    segments.Add(new Segment { a = ta.position, b = tb.position, radius = r, name = $"{a}-{b}" });
            }
            void Tip(string last, string before)
            {
                var a = bones[last].position;
                segments.Add(new Segment { a = a, b = a + (a - bones[before].position) * 0.9f, radius = FingerRadius, name = last + "-tip" });
            }
            foreach (var f in new[] { "Index", "Middle", "Ring", "Pinky" })
            {
                Add("Wrist.R", $"{f}1.R", PalmRadius);
                Add($"{f}1.R", $"{f}2.R", PalmRadius);
                Add($"{f}2.R", $"{f}3.R", FingerRadius);
                Add($"{f}3.R", $"{f}4.R", FingerRadius);
                Tip($"{f}4.R", $"{f}3.R");
            }
            Add("Thumb1.R", "Thumb2.R", FingerRadius);
            Add("Thumb2.R", "Thumb3.R", FingerRadius);
            Tip("Thumb3.R", "Thumb2.R");
            return segments;
        }

        /// <summary>Points over the item's surface, in the item's own space.</summary>
        static List<Vector3> Surface(GameObject prop, Matrix4x4 pose)
        {
            var points = new List<Vector3>();
            var toRoot = prop.transform.worldToLocalMatrix;
            foreach (var filter in prop.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                if (mesh == null) continue;
                var m = pose * toRoot * filter.transform.localToWorldMatrix;
                var v = mesh.vertices.Select(x => m.MultiplyPoint3x4(x)).ToArray();
                var tris = mesh.triangles;
                for (int i = 0; i < tris.Length; i += 3)
                {
                    Vector3 a = v[tris[i]], b = v[tris[i + 1]], c = v[tris[i + 2]];
                    var bounds = new Bounds(a, Vector3.zero);
                    bounds.Encapsulate(b);
                    bounds.Encapsulate(c);
                    if (bounds.SqrDistance(Vector3.zero) > Reach * Reach) continue;
                    float longest = Mathf.Max((b - a).magnitude, (c - b).magnitude, (a - c).magnitude);
                    int n = Mathf.Clamp(Mathf.CeilToInt(longest / SampleSpacing), 1, 400);
                    for (int s = 0; s <= n; s++)
                        for (int t = 0; t <= n - s; t++)
                        {
                            var p = a + (b - a) * (s / (float)n) + (c - a) * (t / (float)n);
                            if (p.sqrMagnitude <= Reach * Reach) points.Add(p);
                        }
                }
            }
            return points;
        }

        [Test]
        public void SocketKeepsItsGripWithClipsAndProceduralAnimation()
        {
            var update = typeof(RigFollower).GetMethod("LateUpdate",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            foreach (var prefabName in new[] { "Player", "Coworker" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Prefabs/{prefabName}.prefab");
                var character = Object.Instantiate(prefab);
                try
                {
                    var follower = character.GetComponentInChildren<RigFollower>(true);
                    var socket = character.transform.Find("Visual/Model/Chest/ArmPivot/Elbow/HandSocket");
                    foreach (bool clips in new[] { false, true })
                    {
                        follower.ClipDriven = clips;
                        for (int body = 0; body < follower.RigCount; body++)
                        {
                            follower.Select(body);
                            for (int pose = 0; pose < 3; pose++)
                            {
                                socket.rotation = Quaternion.Euler(pose * 37f, 20f - pose * 41f, pose * 19f);
                                update.Invoke(follower, null);
                                var hand = follower.Hand;
                                var rotation = socket.rotation;
                                for (int repeat = 0; repeat < 3; repeat++)
                                {
                                    update.Invoke(follower, null);
                                    string context = $"{prefabName} body {body}, clips={clips}, pose {pose}";
                                    Assert.Less(Quaternion.Angle(hand.rotation * follower.ActiveRig.gripRotation, socket.rotation),
                                        0.1f, context + ": socket matches the calibrated grip");
                                    Assert.Less(Vector3.Distance(hand.TransformPoint(follower.ActiveRig.grip), socket.position),
                                        0.0001f, context + ": socket stays seated in the hand");
                                    Assert.Less(Quaternion.Angle(rotation, socket.rotation),
                                        0.1f, context + ": repeated updates must not rotate the item");
                                }
                            }
                        }
                    }
                }
                finally { Object.DestroyImmediate(character); }
            }
        }

        [Test]
        public void DisplayUsesTheAuthoredItemTransformAndUpdatesWhenTheBodyChanges()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player.prefab");
            var originalCatalog = AssetDatabase.LoadAssetAtPath<PropCatalog>("Assets/_Project/Data/PropCatalog.asset");
            var catalog = ScriptableObject.CreateInstance<PropCatalog>();
            var character = Object.Instantiate(source);
            try
            {
                catalog.SetEntries(new[] { new PropCatalog.Entry
                {
                    id = "fitted", prefab = originalCatalog.Get("calculator"),
                    manGrip = new ItemHoldPose { position = new Vector3(0.03f, 0.06f, -0.02f), eulerAngles = new Vector3(0f, 90f, 0f) },
                    womanGrip = new ItemHoldPose { position = new Vector3(-0.04f, 0.01f, 0.07f), eulerAngles = new Vector3(0f, -45f, 0f) },
                } });
                var display = character.GetComponentInChildren<HeldItemDisplay>(true);
                var serialized = new SerializedObject(display);
                serialized.FindProperty("catalog").objectReferenceValue = catalog;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var follower = display.GetComponent<RigFollower>();
                follower.ClipDriven = true;
                follower.Select(0);
                display.Show("fitted");
                var socket = character.transform.Find("Visual/Model/Chest/ArmPivot/Elbow/HandSocket");
                // The item, not the swing trail that also rides on the socket.
                var item = socket.Cast<Transform>().First(t => t.name.StartsWith(originalCatalog.Get("calculator").name));
                Assert.Less(Vector3.Distance(new Vector3(0.03f, 0.06f, -0.02f), item.localPosition), 0.00001f);
                Assert.Less(Quaternion.Angle(Quaternion.Euler(0f, 90f, 0f), item.localRotation), 0.1f);
                follower.Select(1);
                typeof(HeldItemDisplay).GetMethod("LateUpdate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(display, null);
                Assert.Less(Vector3.Distance(new Vector3(-0.04f, 0.01f, 0.07f), item.localPosition), 0.00001f);
                Assert.Less(Quaternion.Angle(Quaternion.Euler(0f, -45f, 0f), item.localRotation), 0.1f);
            }
            finally { Object.DestroyImmediate(character); Object.DestroyImmediate(catalog); }
        }

        [Test]
        public void TheCupCarryPosePreservesArmLengthsAndDoesNotFeedBackIntoTheProceduralDriver()
        {
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            foreach (var name in new[] { "Player", "Coworker" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Prefabs/{name}.prefab");
                var character = Object.Instantiate(prefab);
                try
                {
                    var follower = character.GetComponentInChildren<RigFollower>(true);
                    typeof(RigFollower).GetMethod("Awake", flags).Invoke(follower, null);
                    var display = follower.GetComponent<HeldItemDisplay>();
                    display.Show("coffee_cup");
                    follower.ClipDriven = false;
                    typeof(RigFollower).GetField("carryWeight", flags).SetValue(follower, 1f);
                    var update = typeof(RigFollower).GetMethod("LateUpdate", flags);
                    var socket = character.transform.Find("Visual/Model/Chest/ArmPivot/Elbow/HandSocket");
                    for (int body = 0; body < follower.RigCount; body++)
                    {
                        var rig = follower.GetRig(body);
                        var lower = rig.hand.parent;
                        var upper = lower.parent;
                        float a = Vector3.Distance(upper.position, lower.position), b = Vector3.Distance(lower.position, rig.hand.position);
                        follower.Select(body);
                        var rotation = socket.rotation;
                        var position = socket.position;
                        for (int repeat = 0; repeat < 8; repeat++)
                        {
                            update.Invoke(follower, null);
                            Assert.Greater(Vector3.Dot(socket.up, Vector3.up), 0.98f, name + ": cup upright");
                            Assert.Less(Quaternion.Angle(rotation, socket.rotation), 0.1f, name + ": no rotation feedback");
                            Assert.Less(Vector3.Distance(position, socket.position), 0.0001f, name + ": no arm drift");
                            Assert.AreEqual(a, Vector3.Distance(upper.position, lower.position), 0.0001f, "upper arm keeps its length");
                            Assert.AreEqual(b, Vector3.Distance(lower.position, rig.hand.position), 0.0001f, "forearm keeps its length");
                            Assert.Less(Vector3.Distance(socket.position, rig.hand.TransformPoint(rig.grip)), 0.0001f, "grasp stays attached");
                        }
                    }
                }
                finally { Object.DestroyImmediate(character); }
            }
        }

        [Test]
        public void ACustomGraspOpensTheHoldingFingersWithoutOpeningTheGuardHand()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player.prefab");
            var character = Object.Instantiate(prefab);
            try
            {
                var follower = character.GetComponentInChildren<RigFollower>(true);
                for (int body = 0; body < follower.RigCount; body++)
                {
                    var rig = follower.GetRig(body);
                    RigFollower.ApplyFingerPose(rig, new ItemHoldPose { index = Vector3.zero });
                    Assert.Less(Quaternion.Angle(rig.fingerOpen[18], rig.fingers[18].localRotation), 0.1f, "holding index opens");
                    Assert.Less(Quaternion.Angle(rig.fist[3], rig.fingers[3].localRotation), 0.1f, "guard index stays closed");
                    RigFollower.ApplyFingerPose(rig, null);
                    Assert.Less(Quaternion.Angle(rig.fist[18], rig.fingers[18].localRotation), 0.1f, "empty hand closes again");
                }
            }
            finally { Object.DestroyImmediate(character); }
        }

        [TestCase("calculator")]
        [TestCase("ledger")]
        [TestCase("audit_stamp")]
        [TestCase("ethernet_cable")]
        [TestCase("mouse")]
        [TestCase("laptop")]
        [TestCase("smartphone")]
        [TestCase("golf_club")]
        [TestCase("desk_phone")]
        [TestCase("clipboard")]
        [TestCase("binder")]
        [TestCase("megaphone")]
        [TestCase("keyboard")]
        [TestCase("stapler")]
        [TestCase("briefcase")]
        [TestCase("coffee_cup")]
        [TestCase("rolled_report")]
        public void TheFittedItemTouchesSeveralFingersWithoutPassingThroughTheHand(string id)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<PropCatalog>("Assets/_Project/Data/PropCatalog.asset");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Coworker.prefab");
            var character = Object.Instantiate(prefab);
            try
            {
                var follower = character.GetComponentInChildren<RigFollower>(true);
                follower.ClipDriven = true;
                var problems = new List<string>();
                for (int body = 0; body < follower.RigCount; body++)
                {
                    follower.Select(body);
                    var rig = follower.ActiveRig;
                    var pose = catalog.GetGrip(id, body);
                    Assert.IsNotNull(pose, id + " needs an authored grasp");
                    RigFollower.ApplyFingerPose(rig, pose);
                    var at = rig.hand.TransformPoint(rig.grip);
                    var inverse = Quaternion.Inverse(rig.hand.rotation * rig.gripRotation);
                    var hand = Fist(rig.root.transform).Select(segment => new Segment
                    {
                        a = inverse * (segment.a - at), b = inverse * (segment.b - at),
                        radius = segment.radius, name = segment.name,
                        finger = new[] { "Thumb", "Index", "Middle", "Ring", "Pinky" }.FirstOrDefault(f =>
                            segment.name.StartsWith(f + "2.") || segment.name.StartsWith(f + "3.") || segment.name.StartsWith(f + "4.")),
                    }).ToArray();
                    var surface = Surface(catalog.Get(id), Matrix4x4.TRS(pose.position, Quaternion.Euler(pose.eulerAngles), Vector3.one));
                    Assert.IsNotEmpty(surface, id + ": item must be within reach");
                    var gaps = new Dictionary<string, float>();
                    foreach (var finger in new[] { "Thumb", "Index", "Middle", "Ring", "Pinky" }) gaps[finger] = float.PositiveInfinity;
                    float clearance = float.PositiveInfinity;
                    string closest = null;
                    Vector3 closestPoint = Vector3.zero;
                    foreach (var point in surface)
                    foreach (var segment in hand)
                    {
                        float gap = Distance(point, segment) - segment.radius;
                        if (gap < clearance) { clearance = gap; closest = segment.name; closestPoint = point; }
                        if (segment.finger != null) gaps[segment.finger] = Mathf.Min(gaps[segment.finger], gap);
                    }
                    string report = id + " " + rig.root.name + ": " + string.Join(", ", gaps.Select(g => $"{g.Key} {g.Value * 1000f:0.0} mm"))
                        + $"; deepest {closest} at {closestPoint.ToString("F3")}: {clearance * 1000f:0.0} mm";
                    Debug.Log(report);
                    if (clearance < -Tolerance) problems.Add(report + ": item through hand");
                    if (gaps["Thumb"] > 0.006f) problems.Add(report + ": thumb needs contact with the item");
                    if (gaps.Count(g => g.Value <= 0.006f) < 3) problems.Add(report + ": needs contact with several fingers");
                }
                Assert.IsEmpty(problems, string.Join("\n", problems));
            }
            finally { Object.DestroyImmediate(character); }
        }
    }
}
