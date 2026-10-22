using System.Collections.Generic;
using NineToFight.Combat;
using UnityEngine;

namespace NineToFight.UI
{
    /// <summary>
    /// Renders each office item's 3D model to a small icon once, so the hotbar shows the actual
    /// stapler, golf club or laptop. Rendered against a key colour and keyed out (works whether or
    /// not the render pipeline keeps alpha).
    /// </summary>
    public static class PropIcons
    {
        const int Layer = 29;
        const int Size = 128;
        static readonly Color Key = new Color(1f, 0f, 1f, 1f);
        static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        public static Texture2D Get(PropCatalog catalog, string id)
        {
            if (catalog == null || string.IsNullOrEmpty(id)) return null;
            if (Cache.TryGetValue(id, out var cached) && cached != null) return cached;
            var prefab = catalog.Get(id);
            if (prefab == null) return null;
            var icon = Render(prefab);
            Cache[id] = icon;
            return icon;
        }

        static Texture2D Render(GameObject prefab)
        {
            var studio = new GameObject("Icon Studio");
            studio.transform.position = new Vector3(0f, -500f, 0f);
            var item = Object.Instantiate(prefab, studio.transform);
            item.transform.localPosition = Vector3.zero;
            // Items hang along -Y from the grip; tip them up to the right and turn them a little toward the camera.
            item.transform.localRotation = Quaternion.Euler(12f, -28f, 0f) * Quaternion.Euler(0f, 0f, 135f);
            foreach (var t in item.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
            foreach (var c in item.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);

            var renderers = item.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(studio.transform.position, Vector3.one * 0.3f);
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);

            var camGo = new GameObject("Icon Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.enabled = false;
            cam.cullingMask = 1 << Layer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Key;
            cam.orthographic = true;
            cam.orthographicSize = Mathf.Max(bounds.extents.x, bounds.extents.y) * 1.12f + 0.01f;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 20f;
            cam.allowMSAA = false;
            cam.transform.position = bounds.center + new Vector3(0f, 0f, -5f);
            cam.transform.rotation = Quaternion.identity;

            // A studio key light from the front-left; the scene's sun comes from behind.
            var lightGo = new GameObject("Icon Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.cullingMask = 1 << Layer;
            light.intensity = 1.4f;
            light.color = new Color(1f, 0.97f, 0.92f);
            light.transform.rotation = Quaternion.Euler(35f, 25f, 0f);

            var rt = RenderTexture.GetTemporary(Size, Size, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            Texture2D icon = null;
            try
            {
                cam.Render();
                var previous = RenderTexture.active;
                RenderTexture.active = rt;
                icon = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = "Icon_" + prefab.name, hideFlags = HideFlags.DontSave };
                icon.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                RenderTexture.active = previous;
                KeyOut(icon);
            }
            finally
            {
                cam.targetTexture = null;
                RenderTexture.ReleaseTemporary(rt);
                Object.Destroy(camGo);
                Object.Destroy(lightGo);
                Object.DestroyImmediate(studio);
            }
            return icon;
        }

        /// <summary>Turns the magenta backdrop transparent and removes the magenta fringe from edges.</summary>
        static void KeyOut(Texture2D tex)
        {
            var px = tex.GetPixels();
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                // How much of this pixel is backdrop: magenta has high R and B with no G.
                float keyness = Mathf.Clamp01(Mathf.Min(c.r, c.b) - c.g * 1.5f) * 1.6f;
                float a = Mathf.Clamp01(1f - keyness);
                if (a <= 0.02f)
                {
                    px[i] = new Color(0f, 0f, 0f, 0f);
                    continue;
                }
                // Un-mix the backdrop from partially covered edge pixels.
                float k = 1f - a;
                px[i] = new Color(Mathf.Clamp01((c.r - k) / a), Mathf.Clamp01(c.g / a), Mathf.Clamp01((c.b - k) / a), a);
            }
            tex.SetPixels(px);
            tex.Apply();
        }
    }
}
