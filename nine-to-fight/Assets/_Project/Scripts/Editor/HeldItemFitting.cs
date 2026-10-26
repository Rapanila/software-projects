using System.IO;
using System.Linq;
using NineToFight.Characters;
using NineToFight.Combat;
using UnityEditor;
using UnityEngine;

namespace NineToFight.EditorTools
{
    /// <summary>Uses the avatar's original T-pose to inspect each item against the actual skinned hand.</summary>
    public static class HeldItemFitting
    {
        const string Output = "Builds/Screenshots/ItemFitting";
        public const string ProfilePath = "Assets/_Project/Data/HeldItemGrips.json";
        const int Layer = 28;

        [System.Serializable]
        public class Profile
        {
            public string id;
            public ItemHoldPose manGrip;
            public ItemHoldPose womanGrip;
        }

        [System.Serializable]
        class Profiles { public Profile[] items; }

        [System.Serializable]
        class Request
        {
            public string id = "calculator";
            public bool aimThumb;
            public float thumbZ, thumbY;
        }

        public static void AssignProfiles(PropCatalog catalog)
        {
            var profiles = JsonUtility.FromJson<Profiles>(File.ReadAllText(ProfilePath));
            var entries = catalog.Entries.ToArray();
            for (int i = 0; i < entries.Length; i++)
            {
                var profile = profiles.items.Single(p => p.id == entries[i].id);
                entries[i].manGrip = profile.manGrip;
                entries[i].womanGrip = profile.womanGrip;
            }
            catalog.SetEntries(entries);
            EditorUtility.SetDirty(catalog);
        }

        [MenuItem("Nine to Fight/Item Fitting/Import Grips")]
        public static void ImportGrips()
        {
            AssignProfiles(AssetDatabase.LoadAssetAtPath<PropCatalog>(StarterContentBuilder.PropCatalogPath));
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Nine to Fight/Item Fitting/Capture Item")]
        public static void CaptureItem()
        {
            ImportGrips();
            var request = File.Exists("Temp/ItemFittingRequest.json")
                ? JsonUtility.FromJson<Request>(File.ReadAllText("Temp/ItemFittingRequest.json")) : new Request();
            CaptureItem(request.id, request.aimThumb, request.thumbZ, request.thumbY);
        }

        [MenuItem("Nine to Fight/Item Fitting/Capture All Items")]
        public static void CaptureAllItems()
        {
            ImportGrips();
            var catalog = AssetDatabase.LoadAssetAtPath<PropCatalog>(StarterContentBuilder.PropCatalogPath);
            foreach (var entry in catalog.Entries) CaptureItem(entry.id);
        }

        public static void CaptureItem(string id, bool aimThumb = false, float thumbZ = 0f, float thumbY = 0f)
        {
            Directory.CreateDirectory(Output);
            var catalog = AssetDatabase.LoadAssetAtPath<PropCatalog>(StarterContentBuilder.PropCatalogPath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Coworker.prefab");
            var studio = Object.Instantiate(prefab);
            studio.hideFlags = HideFlags.HideAndDontSave;
            studio.transform.position = new Vector3(0f, -500f, 0f);
            try
            {
                foreach (var behaviour in studio.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
                var follower = studio.GetComponentInChildren<RigFollower>(true);
                for (int body = 0; body < follower.RigCount; body++)
                {
                    follower.Select(body);
                    var rig = follower.ActiveRig;
                    RestoreTPose(rig);
                    var pose = catalog.GetGrip(id, body);
                    if (aimThumb) pose.thumbRotation = Vector3.zero;
                    RigFollower.ApplyFingerPose(rig, pose);
                    var frame = rig.hand.rotation * rig.gripRotation;
                    var center = rig.hand.TransformPoint(rig.grip);
                    if (aimThumb)
                    {
                        // An author-selected contact on the back of this particular item. Bake the
                        // opposition once; runtime uses the saved rotation, with no IK or bounds fitting.
                        var bones = rig.root.GetComponentsInChildren<Transform>(true).GroupBy(t => t.name)
                            .ToDictionary(g => g.Key, g => g.First());
                        var thumb = bones["Thumb1.R"];
                        var last = bones["Thumb3.R"];
                        var end = last.position + (last.position - bones["Thumb2.R"].position) * 0.9f;
                        var from = Quaternion.Inverse(frame) * (end - thumb.position);
                        var at = Quaternion.Inverse(frame) * (thumb.position - center);
                        float dy = thumbY - at.y, dz = thumbZ - at.z;
                        float dx2 = from.sqrMagnitude - dy * dy - dz * dz;
                        if (dx2 <= 0f) throw new System.InvalidOperationException(id + ": thumb contact is out of reach");
                        var target = new Vector3(-Mathf.Sqrt(dx2), dy, dz);
                        pose.thumbRotation = Quaternion.FromToRotation(from, target).eulerAngles;
                        RigFollower.ApplyFingerPose(rig, pose);
                    }
                    var item = Object.Instantiate(catalog.Get(id), studio.transform);
                    try
                    {
                        item.transform.SetPositionAndRotation(center + frame * pose.position, frame * Quaternion.Euler(pose.eulerAngles));
                        foreach (var t in studio.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
                        var prefix = id + "_" + rig.root.name;
                        File.WriteAllLines($"{Output}/{prefix}_bones.csv", rig.root.GetComponentsInChildren<Transform>(true)
                            .Where(t => t.name.EndsWith(".R")).Select(t =>
                            {
                                var p = Quaternion.Inverse(frame) * (t.position - center);
                                return System.FormattableString.Invariant($"{t.name},{p.x},{p.y},{p.z}");
                            }));
                        Capture(studio, center + frame * new Vector3(-0.02f, 0f, 0.015f), frame, prefix + "_hand", 0.17f);
                        var renderers = item.GetComponentsInChildren<Renderer>();
                        var bounds = renderers[0].bounds;
                        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                        bounds.Encapsulate(center);
                        Capture(studio, bounds.center, frame, prefix + "_overview", Mathf.Max(0.2f, bounds.extents.magnitude * 1.12f), 1);
                    }
                    finally { Object.DestroyImmediate(item); }
                }
            }
            finally { Object.DestroyImmediate(studio); }
            if (aimThumb)
            {
                var saved = new Profiles { items = catalog.Entries.Select(e => new Profile
                    { id = e.id, manGrip = e.manGrip, womanGrip = e.womanGrip }).ToArray() };
                File.WriteAllText(ProfilePath, JsonUtility.ToJson(saved, true) + "\n");
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssets();
            }
            Debug.Log($"[Item fitting] {id}: both T-pose hands captured in {Output}");
        }

        [MenuItem("Nine to Fight/Item Fitting/Capture Neutral Hand")]
        public static void CaptureNeutralHand()
        {
            Directory.CreateDirectory(Output);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Coworker.prefab");
            var studio = Object.Instantiate(prefab);
            studio.name = "Item fitting studio";
            studio.hideFlags = HideFlags.HideAndDontSave;
            studio.transform.position = new Vector3(0f, -500f, 0f);
            try
            {
                foreach (var behaviour in studio.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
                var follower = studio.GetComponentInChildren<RigFollower>(true);
                for (int body = 0; body < follower.RigCount; body++)
                {
                    follower.Select(body);
                    var rig = follower.ActiveRig;
                    RestoreTPose(rig);
                    var frame = rig.hand.rotation * rig.gripRotation;
                    var center = rig.hand.TransformPoint(rig.grip);
                    var bones = rig.root.GetComponentsInChildren<Transform>(true);
                    File.WriteAllLines($"{Output}/{rig.root.name}_bones.csv", bones.Where(t => t.name.EndsWith(".R"))
                        .Select(t =>
                        {
                            var p = Quaternion.Inverse(frame) * (t.position - center);
                            return System.FormattableString.Invariant($"{t.name},{p.x},{p.y},{p.z}");
                        }));
                    foreach (var t in studio.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
                    Capture(studio, center + frame * new Vector3(0f, 0f, 0.035f), frame,
                        $"{rig.root.name}_neutral", 0.16f);
                }
            }
            finally { Object.DestroyImmediate(studio); }
            Debug.Log($"[Item fitting] Neutral T-pose hands saved to {Output}");
        }

        public static void RestoreTPose(RigFollower.Rig rig)
        {
            var rest = rig.animator.avatar.humanDescription.skeleton.ToDictionary(s => s.name);
            foreach (var t in rig.root.GetComponentsInChildren<Transform>(true))
                if (rest.TryGetValue(t.name, out var bone))
                {
                    t.localPosition = bone.position;
                    t.localRotation = bone.rotation;
                    t.localScale = bone.scale;
                }
        }

        static void Capture(GameObject studio, Vector3 center, Quaternion frame, string name, float size, int viewCount = 3)
        {
            var cameraObject = new GameObject("Fitting camera") { hideFlags = HideFlags.HideAndDontSave };
            var lightObject = new GameObject("Fitting light") { hideFlags = HideFlags.HideAndDontSave };
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.cullingMask = 1 << Layer;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.14f, 0.18f);
            camera.orthographic = true;
            camera.orthographicSize = size;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 10f;
            camera.allowMSAA = false;
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.cullingMask = 1 << Layer;
            light.intensity = 1.8f;
            var target = RenderTexture.GetTemporary(640, 640, 24);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                var views = new[] { new Vector3(-0.8f, 0.2f, 0.4f), new Vector3(0.8f, 0.2f, 0.4f), new Vector3(-0.8f, 0.25f, -0.5f) };
                for (int view = 0; view < viewCount; view++)
                {
                    camera.transform.position = center + frame * views[view];
                    camera.transform.rotation = Quaternion.LookRotation(center - camera.transform.position, frame * Vector3.up);
                    light.transform.rotation = camera.transform.rotation * Quaternion.Euler(20f, -25f, 0f);
                    camera.Render();
                    RenderTexture.active = target;
                    var image = new Texture2D(640, 640, TextureFormat.RGB24, false);
                    try
                    {
                        image.ReadPixels(new Rect(0, 0, 640, 640), 0, 0);
                        image.Apply();
                        File.WriteAllBytes($"{Output}/{name}_{view}.png", image.EncodeToPNG());
                    }
                    finally { Object.DestroyImmediate(image); }
                }
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(lightObject);
            }
        }
    }
}
