using System.Collections.Generic;
using System.IO;
using System.Linq;
using NineToFight.Characters;
using NineToFight.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace NineToFight.EditorTools
{
    /// <summary>
    /// Art pass: procedural textures and the "golden hour, 40th floor"
    /// level: a window wall over a dusk skyline, zoned into reception, cubicles, a glass meeting room,
    /// a break room and the brass elevator bank.
    /// </summary>
    public static partial class StarterContentBuilder
    {
        const string TexturesDir = Root + "/Textures";

        // Palette
        const string TowerNavy = "1F2A44";
        const string MemoCream = "EFE6D2";
        const string CarpetSlate = "4B5A73";
        const string SunsetAmber = "F2A65A";
        const string Brass = "C9A44C";
        const string Fluorescent = "F4F8FF";

        // Zones along the floor (x)
        const float LobbyEnd = 8f, CubiclesEnd = 28f, MeetingEnd = 38f, BreakEnd = 50f;
        const float WallZ = 5f;

        // ================================================================== textures

        static Texture2D SaveTexture(string name, int width, int height, System.Func<int, int, System.Random, Color> pixel,
            bool sRGB = true, TextureWrapMode wrap = TextureWrapMode.Repeat)
        {
            EnsureFolder(TexturesDir);
            var rng = new System.Random(name.GetHashCode());
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    pixels[y * width + x] = pixel(x, y, rng);
            tex.SetPixels(pixels);
            string path = $"{TexturesDir}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = wrap;
            importer.sRGBTexture = sRGB;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.anisoLevel = 4;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static float Noise(System.Random rng, float amount) => ((float)rng.NextDouble() * 2f - 1f) * amount;

        static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();

        static Texture2D Tex(string name)
        {
            if (Textures.TryGetValue(name, out var cached) && cached != null) return cached;
            Texture2D t;
            switch (name)
            {
                case "carpet_tiles": // 2x2 carpet tiles with quarter-turn shading and seams
                    t = SaveTexture(name, 128, 128, (x, y, r) =>
                    {
                        bool seam = x % 64 == 0 || y % 64 == 0;
                        float turn = ((x / 64) + (y / 64)) % 2 == 0 ? 1f : 0.93f;
                        float fibre = 0.9f + Noise(r, 0.07f) + (((x + y * 3) % 5 == 0) ? 0.04f : 0f);
                        float v = seam ? 0.72f : turn * fibre;
                        return new Color(v, v, v, 1f);
                    });
                    break;
                case "terrazzo":
                    t = SaveTexture(name, 128, 128, (x, y, r) =>
                    {
                        double roll = r.NextDouble();
                        if (roll < 0.008) return Hex("C9A44C");
                        if (roll < 0.03) return Hex("A49E94");
                        if (roll < 0.045) return Hex("6E7584");
                        float v = 0.9f + Noise(r, 0.02f);
                        return new Color(v, v * 0.98f, v * 0.94f, 1f);
                    });
                    break;
                case "vinyl_tiles": // break-room checkerboard
                    t = SaveTexture(name, 64, 64, (x, y, r) =>
                    {
                        bool a = ((x / 32) + (y / 32)) % 2 == 0;
                        var c = a ? Hex("E9E2D0") : Hex("8FA89A");
                        float n = 1f + Noise(r, 0.03f);
                        return x % 32 == 0 || y % 32 == 0 ? c * 0.85f : c * n;
                    });
                    break;
                case "ceiling_tiles":
                    t = SaveTexture(name, 64, 64, (x, y, r) =>
                    {
                        bool grid = x < 2 || y < 2;
                        float v = grid ? 0.7f : 0.96f + Noise(r, 0.03f) - (r.NextDouble() < 0.04 ? 0.08f : 0f);
                        return new Color(v, v, v, 1f);
                    });
                    break;
                case "wood_slats": // vertical slats with grain and dark gaps
                    t = SaveTexture(name, 64, 64, (x, y, r) =>
                    {
                        int slat = x % 16;
                        if (slat < 2) return new Color(0.25f, 0.18f, 0.13f, 1f);
                        float grain = 0.85f + 0.1f * Mathf.Sin((y * 0.35f) + (x / 16) * 1.7f + Mathf.Sin(y * 0.08f) * 2f) + Noise(r, 0.03f);
                        return new Color(0.62f * grain, 0.42f * grain, 0.27f * grain, 1f);
                    });
                    break;
                case "desk_grain":
                    t = SaveTexture(name, 64, 64, (x, y, r) =>
                    {
                        float grain = 0.9f + 0.07f * Mathf.Sin(x * 0.5f + Mathf.Sin(y * 0.15f) * 3f) + Noise(r, 0.02f);
                        return new Color(grain, grain, grain, 1f);
                    });
                    break;
                case "facade": // night facade: window grid, some offices still lit
                case "facade_emit":
                    bool emit = name == "facade_emit";
                    t = SaveTexture(name, 64, 128, (x, y, r) =>
                    {
                        int cx = x % 8, cy = y % 8;
                        bool window = cx >= 1 && cx <= 6 && cy >= 2 && cy <= 6;
                        // Lit state per window cell (stable per cell).
                        int cell = (x / 8) * 131 + (y / 8) * 977;
                        float lit = Mathf.Abs(Mathf.Sin(cell * 12.9898f) * 43758.55f) % 1f;
                        Color light = lit < 0.28f ? Hex("FFD08A") : lit < 0.36f ? Hex("BFD8FF") : Color.black;
                        if (emit) return window ? light : Color.black;
                        if (!window) return new Color(0.16f, 0.18f, 0.24f, 1f);
                        return light == Color.black ? new Color(0.22f, 0.27f, 0.36f, 1f) : light;
                    }, sRGB: true);
                    break;
                case "sky_gradient": // dusk: violet-blue overhead, amber at the horizon
                    t = SaveTexture(name, 8, 256, (x, y, r) =>
                    {
                        float k = y / 255f;
                        Color c = k < 0.42f
                            ? Color.Lerp(Hex("E9925A"), Hex("F6C27E"), k / 0.42f)
                            : k < 0.55f ? Color.Lerp(Hex("F6C27E"), Hex("C9786A"), (k - 0.42f) / 0.13f)
                            : Color.Lerp(Hex("C9786A"), Hex("2A3357"), (k - 0.55f) / 0.45f);
                        return c;
                    }, wrap: TextureWrapMode.Clamp);
                    break;
                default:
                    t = null;
                    break;
            }
            Textures[name] = t;
            return t;
        }

        /// <summary>Lit material with a tiling texture.</summary>
        static Material MatTex(string name, string texture, Vector2 tiling, Color color, float smoothness = 0.2f)
        {
            var mat = Mat(name, color, smoothness);
            var tex = Tex(texture);
            mat.SetTexture("_BaseMap", tex);
            mat.SetTextureScale("_BaseMap", tiling);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>See-through URP Lit material for glass.</summary>
        static Material MatGlass(string name, Color color, float smoothness = 0.95f)
        {
            var mat = Mat(name, color, smoothness);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static GameObject NoShadow(GameObject go)
        {
            var r = go.GetComponent<Renderer>();
            r.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        // ================================================================== fx materials

        const string FxDir = Root + "/Resources/FX";

        /// <summary>Soft particle-style materials the runtime effects load from Resources/FX.</summary>
        [MenuItem("Nine to Fight/Build FX Assets", priority = 1)]
        public static void BuildFxAssets()
        {
            EnsureFolder(Root + "/Resources");
            EnsureFolder(FxDir);
            var dot = SaveTexture("fx_soft_dot", 64, 64, (x, y, r) =>
            {
                float dx = (x + 0.5f) / 32f - 1f, dy = (y + 0.5f) / 32f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                return new Color(1f, 1f, 1f, a * a * (3f - 2f * a));
            }, wrap: TextureWrapMode.Clamp);
            FxMaterial("Trail", null, additive: true);
            FxMaterial("Glow", dot, additive: true);
            FxMaterial("Dust", dot, additive: false);
            AssetDatabase.SaveAssets();
        }

        static Material FxMaterial(string name, Texture texture, bool additive)
        {
            string path = $"{FxDir}/{name}.mat";
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.SetFloat("_Surface", 1f);                 // transparent
            mat.SetFloat("_Blend", additive ? 2f : 0f);   // additive or alpha
            mat.SetColor("_BaseColor", Color.white);
            mat.SetTexture("_BaseMap", texture);
            UnityEditor.BaseShaderGUI.SetupMaterialBlendMode(mat);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ================================================================== lighting & atmosphere

        static void BuildLighting()
        {
            // Low sun through the window wall behind the lane: long mullion shadows fall toward the camera.
            var sun = new GameObject("Key Light").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = Hex("FFC98F");
            sun.intensity = 1.55f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            sun.transform.rotation = Quaternion.Euler(30f, 157f, 0f);

            // Cool fill from the camera side, no shadows: keeps faces readable against the backlight.
            var fill = new GameObject("Fill Light").AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = Hex("A9BEE8");
            fill.intensity = 0.55f;
            fill.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.Euler(32f, 18f, 0f);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Hex("8EA3C8");
            RenderSettings.ambientEquatorColor = Hex("B4917C");
            RenderSettings.ambientGroundColor = Hex("3A3340");

            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/SampleSceneProfile.asset");
            if (profile != null)
            {
                var volume = new GameObject("Global Volume").AddComponent<Volume>();
                volume.isGlobal = true;
                volume.sharedProfile = profile;
            }

            // Fluorescent fixtures: a cool counterpoint to the warm sun.
            for (float x = 2f; x < LevelEndX; x += 12f)
            {
                var lamp = new GameObject("Ceiling Light").AddComponent<Light>();
                lamp.type = LightType.Point;
                lamp.color = Hex(Fluorescent);
                lamp.intensity = 4.5f;
                lamp.range = 10f;
                lamp.transform.position = new Vector3(x, 6.2f, 0.5f);
            }
        }

        static void ConfigureAtmosphere()
        {
            // Haze on the skyline only; the office interior sits inside the fog start.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Hex("D49A78");
            RenderSettings.fogStartDistance = 26f;
            RenderSettings.fogEndDistance = 150f;
        }

        static void MarkStatic(Transform root, Transform except)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == except || t.IsChildOf(except)) continue;
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
            }
        }

        // ================================================================== level

        static void BuildShell(Transform parent)
        {
            var shell = Group("Shell", parent);
            float width = LevelEndX - LevelStartX + 4f;
            float centerX = (LevelStartX + LevelEndX) / 2f;

            // Floor: carpet tiles everywhere (tinted per floor), terrazzo in reception, vinyl in the break room.
            Part(PrimitiveType.Cube, "Floor", shell, new Vector3(centerX, -0.5f, 1f), new Vector3(width, 1f, 13f),
                MatTex("Carpet", "carpet_tiles", new Vector2(width / 2f, 6.5f), Hex(CarpetSlate), 0.05f), keepCollider: true);
            float lobbyWidth = LobbyEnd - (LevelStartX - 2f);
            Part(PrimitiveType.Cube, "Lobby Floor", shell, new Vector3(LevelStartX - 2f + lobbyWidth / 2f, 0.004f, 1f), new Vector3(lobbyWidth, 0.01f, 13f),
                MatTex("Floor_Terrazzo", "terrazzo", new Vector2(lobbyWidth / 1.5f, 13f / 1.5f), Hex("CFC6B6"), 0.5f));
            Part(PrimitiveType.Cube, "Break Room Floor", shell, new Vector3((MeetingEnd + BreakEnd) / 2f, 0.004f, 1f), new Vector3(BreakEnd - MeetingEnd, 0.01f, 13f),
                MatTex("Floor_Vinyl", "vinyl_tiles", new Vector2((BreakEnd - MeetingEnd) / 1f, 13f / 1f), Color.white, 0.45f));
            float bankWidth = LevelEndX + 2f - BreakEnd;
            Part(PrimitiveType.Cube, "Elevator Floor", shell, new Vector3(BreakEnd + bankWidth / 2f, 0.004f, 1f), new Vector3(bankWidth, 0.01f, 13f),
                MatTex("Floor_Terrazzo_Bank", "terrazzo", new Vector2(bankWidth / 1.5f, 13f / 1.5f), Hex("CFC6B6"), 0.5f));

            // Window wall: low navy sill, floor-to-ceiling glass, mullions that throw the sun's shadow stripes.
            var navy = Mat("Trim", Hex(TowerNavy), 0.35f);
            var glass = MatGlass("Window_Glass", new Color(0.72f, 0.82f, 0.95f, 0.12f));
            Part(PrimitiveType.Cube, "Sill", shell, new Vector3(centerX, 0.18f, WallZ), new Vector3(width, 0.36f, 0.35f), navy);
            NoShadow(Part(PrimitiveType.Cube, "Glass", shell, new Vector3(centerX, 3.4f, WallZ + 0.05f), new Vector3(width, 6.1f, 0.02f), glass));
            Part(PrimitiveType.Cube, "Transom", shell, new Vector3(centerX, 6.45f, WallZ), new Vector3(width, 0.12f, 0.2f), navy);
            Part(PrimitiveType.Cube, "Soffit", shell, new Vector3(centerX, 6.85f, WallZ - 0.1f), new Vector3(width, 0.7f, 0.4f), Mat("Wall", Hex(MemoCream), 0.1f));
            for (float x = LevelStartX - 2f; x <= LevelEndX + 2f; x += 2.5f)
                Part(PrimitiveType.Cube, "Mullion", shell, new Vector3(x, 3.4f, WallZ), new Vector3(0.1f, 6.1f, 0.18f), navy);

            var ceiling = MatTex("Ceiling", "ceiling_tiles", new Vector2(width / 1.2f, 13f / 1.2f), Hex("ECEAE4"), 0.05f);
            Part(PrimitiveType.Cube, "Ceiling", shell, new Vector3(centerX, 7.15f, 1f), new Vector3(width, 0.3f, 13f), ceiling);
            var panel = Mat("Ceiling_Panel", Hex("FFFFFF"), emission: Hex("DDE6F5"));
            for (float x = LevelStartX + 2f; x < LevelEndX; x += 4f)
                NoShadow(Part(PrimitiveType.Cube, "Light Panel", shell, new Vector3(x, 6.98f, 0.8f), new Vector3(1.8f, 0.04f, 0.9f), panel));

            // End walls run from the windows all the way out past the camera, so the view never slips round
            // the end of the floor; floor and ceiling run out under and over the camera to meet them.
            var wall = Mat("Wall", Hex(MemoCream), 0.1f);
            const float front = -16f;
            float endDepth = WallZ + 0.4f - front, endZ = (WallZ + 0.4f + front) / 2f;
            Part(PrimitiveType.Cube, "Left Wall", shell, new Vector3(LevelStartX - 1.2f, 3.6f, endZ), new Vector3(0.4f, 7.6f, endDepth), wall);
            Part(PrimitiveType.Cube, "Right Wall", shell, new Vector3(LevelEndX + 1.2f, 3.6f, endZ), new Vector3(0.4f, 7.6f, endDepth), wall);
            foreach (float x in new[] { LevelStartX - 1f, LevelEndX + 1f })
                Part(PrimitiveType.Cube, "Wall Skirting", shell, new Vector3(x, 0.12f, endZ), new Vector3(0.06f, 0.24f, endDepth), navy);
            Part(PrimitiveType.Cube, "Floor Apron", shell, new Vector3(centerX, -0.5f, (front - 5.5f) / 2f), new Vector3(width, 0.98f, -5.5f - front),
                MatTex("Carpet", "carpet_tiles", new Vector2(width / 2f, 6.5f), Hex(CarpetSlate), 0.05f));
            Part(PrimitiveType.Cube, "Ceiling Apron", shell, new Vector3(centerX, 7.15f, (front - 5.5f) / 2f), new Vector3(width, 0.3f, -5.5f - front), ceiling);

            // Structural columns mark the zone boundaries.
            var concrete = Mat("Column", Hex("E4DCCB"), 0.15f);
            foreach (float x in new[] { LobbyEnd, CubiclesEnd, MeetingEnd, BreakEnd })
            {
                Part(PrimitiveType.Cube, "Column", shell, new Vector3(x, 3.5f, WallZ - 0.55f), new Vector3(0.8f, 7f, 0.8f), concrete);
                Part(PrimitiveType.Cube, "Column Base", shell, new Vector3(x, 0.15f, WallZ - 0.55f), new Vector3(0.86f, 0.3f, 0.86f), navy);
                Part(PrimitiveType.Cube, "Column Band", shell, new Vector3(x, 2.6f, WallZ - 0.55f), new Vector3(0.84f, 0.06f, 0.84f), Mat("Brass", Hex(Brass), 0.8f));
            }

            BuildSkyline(shell);

            // Invisible ends of the floor.
            Blocker(shell, "Start Blocker", new Vector3(LevelStartX - 0.6f, 3f, 0f));
            Blocker(shell, "End Blocker", new Vector3(LevelEndX + 0.6f, 3f, 0f));
        }

        /// <summary>The city at dusk, seen from the 40th floor: towers rise from far below, lit offices, a low sun.</summary>
        static void BuildSkyline(Transform parent)
        {
            var city = Group("Skyline", parent);
            var sky = Mat("Sky", Color.white, unlit: true);
            sky.SetTexture("_BaseMap", Tex("sky_gradient"));
            EditorUtility.SetDirty(sky);
            NoShadow(Part(PrimitiveType.Quad, "Sky", city, new Vector3(26f, 20f, 140f), new Vector3(420f, 170f, 1f), sky));

            var sunDisc = Mat("Sun", Hex("FFD9A0"), unlit: true);
            NoShadow(Part(PrimitiveType.Sphere, "Sun", city, new Vector3(-6f, 6f, 130f), new Vector3(16f, 16f, 16f), sunDisc));

            string[] tints = { "7D8CB0", "A88E8A", "6E9AA0" };
            var rng = new System.Random(40);
            for (int i = 0; i < 70; i++)
            {
                float depth = Mathf.Lerp(22f, 105f, (float)rng.NextDouble());
                float x = Mathf.Lerp(-60f, 115f, (float)rng.NextDouble());
                float w = Mathf.Lerp(6f, 16f, (float)rng.NextDouble());
                float top = Mathf.Lerp(-22f, depth * 0.35f, (float)rng.NextDouble());
                const float bottom = -90f;
                float h = top - bottom;
                int tint = rng.Next(tints.Length);
                // Bucket tiling by size so windows stay roughly the same scale on every tower.
                int tx = Mathf.Max(1, Mathf.RoundToInt(w / 4f)), ty = Mathf.Max(1, Mathf.RoundToInt(h / 16f));
                var mat = Mat($"Facade_{tint}_{tx}x{ty}", Hex(tints[tint]), 0.6f, emission: Hex("FFB56B") * 0.55f);
                mat.SetTexture("_BaseMap", Tex("facade"));
                mat.SetTextureScale("_BaseMap", new Vector2(tx, ty));
                mat.SetTexture("_EmissionMap", Tex("facade_emit"));
                EditorUtility.SetDirty(mat);
                NoShadow(Part(PrimitiveType.Cube, "Tower", city, new Vector3(x, bottom + h / 2f, depth), new Vector3(w, h, w), mat));
                if (rng.NextDouble() < 0.25)
                    NoShadow(Part(PrimitiveType.Cube, "Antenna", city, new Vector3(x, top + 3f, depth), new Vector3(0.3f, 6f, 0.3f), Mat("Trim", Hex(TowerNavy), 0.35f)));
            }
        }

        static void BuildCubicles(Transform parent)
        {
            var group = Group("Cubicles", parent);
            var fabric = Mat("Cubicle_Fabric", Hex("6F7F99"), 0.02f);
            var aluminium = Mat("Aluminium", Hex("C4CAD3"), 0.7f);
            var desk = MatTex("Desk", "desk_grain", new Vector2(2f, 1f), Hex("C9A57A"), 0.35f);
            var monitor = Mat("Monitor", Hex("1C1F26"), 0.6f);
            var screenA = Mat("Monitor_Screen", Hex("6FA8DC"), 0.8f, emission: Hex("3C78B4"));
            var screenB = Mat("Monitor_Screen_Sheet", Hex("8FD19E"), 0.8f, emission: Hex("2F7A44"));
            var chair = Mat("Chair", Hex("2B2D33"), 0.3f);
            var paper = Mat("Paper", Hex("F5F1E6"));
            var mug = Mat("Mug", Hex("F2F2F2"));
            string[] notes = { "FFE66D", "FF9FB2", "9BE7FF" };

            int i = 0;
            for (float x = LobbyEnd + 2.6f; x < CubiclesEnd - 2f; x += 6.4f, i++)
            {
                var c = Group("Cubicle " + (i + 1), group);
                c.position = new Vector3(x, 0f, 3.85f);
                // Fabric panels with aluminium caps.
                Part(PrimitiveType.Cube, "Back Panel", c, new Vector3(0f, 0.8f, 0.75f), new Vector3(3f, 1.6f, 0.08f), fabric);
                Part(PrimitiveType.Cube, "Back Cap", c, new Vector3(0f, 1.62f, 0.75f), new Vector3(3.02f, 0.05f, 0.1f), aluminium);
                foreach (float sx in new[] { -1.5f, 1.5f })
                {
                    Part(PrimitiveType.Cube, "Side Panel", c, new Vector3(sx, 0.8f, 0.15f), new Vector3(0.08f, 1.6f, 1.3f), fabric);
                    Part(PrimitiveType.Cube, "Side Cap", c, new Vector3(sx, 1.62f, 0.15f), new Vector3(0.1f, 0.05f, 1.32f), aluminium);
                }
                // Desk with drawer pedestal.
                Part(PrimitiveType.Cube, "Desk", c, new Vector3(0f, 0.75f, 0.35f), new Vector3(2.8f, 0.06f, 0.75f), desk);
                Part(PrimitiveType.Cube, "Pedestal", c, new Vector3(-1.05f, 0.37f, 0.35f), new Vector3(0.5f, 0.72f, 0.65f), aluminium);
                Part(PrimitiveType.Cube, "Leg", c, new Vector3(1.3f, 0.37f, 0.35f), new Vector3(0.05f, 0.72f, 0.6f), aluminium);
                // Monitor, keyboard, mouse.
                Part(PrimitiveType.Cube, "Monitor", c, new Vector3(0.2f, 1.13f, 0.5f), new Vector3(0.74f, 0.46f, 0.04f), monitor);
                Part(PrimitiveType.Cube, "Screen", c, new Vector3(0.2f, 1.13f, 0.475f), new Vector3(0.67f, 0.39f, 0.01f), i % 2 == 0 ? screenA : screenB);
                Part(PrimitiveType.Cube, "Stand", c, new Vector3(0.2f, 0.86f, 0.53f), new Vector3(0.06f, 0.18f, 0.06f), monitor);
                Part(PrimitiveType.Cube, "Stand Foot", c, new Vector3(0.2f, 0.785f, 0.5f), new Vector3(0.26f, 0.015f, 0.18f), monitor);
                Part(PrimitiveType.Cube, "Keyboard", c, new Vector3(0.2f, 0.79f, 0.2f), new Vector3(0.5f, 0.025f, 0.15f), monitor);
                Part(PrimitiveType.Capsule, "Mouse", c, new Vector3(0.62f, 0.79f, 0.2f), new Vector3(0.06f, 0.02f, 0.09f), monitor);
                // Clutter: paper stack, mug, sticky notes, a desk lamp or a plant.
                for (int p = 0; p < 3; p++)
                {
                    var sheet = Part(PrimitiveType.Cube, "Paper", c, new Vector3(-0.75f, 0.785f + p * 0.012f, 0.3f), new Vector3(0.3f, 0.01f, 0.4f), paper);
                    sheet.transform.localRotation = Quaternion.Euler(0f, p * 7f - 6f, 0f);
                }
                Part(PrimitiveType.Cylinder, "Mug", c, new Vector3(-0.35f, 0.83f, 0.45f), new Vector3(0.1f, 0.06f, 0.1f), mug);
                for (int n = 0; n < 3; n++)
                {
                    var note = Part(PrimitiveType.Cube, "Sticky Note", c, new Vector3(-0.12f + n * 0.09f, 1.38f - (n % 2) * 0.06f, 0.47f), new Vector3(0.07f, 0.07f, 0.005f),
                        Mat("Sticky_" + notes[n], Hex(notes[n]), 0.1f));
                    note.transform.localRotation = Quaternion.Euler(0f, 0f, n * 9f - 8f);
                }
                if (i % 2 == 0)
                {
                    var arm = Part(PrimitiveType.Cylinder, "Lamp Arm", c, new Vector3(1.05f, 1.0f, 0.55f), new Vector3(0.025f, 0.22f, 0.025f), monitor);
                    arm.transform.localRotation = Quaternion.Euler(0f, 0f, 20f);
                    Part(PrimitiveType.Cylinder, "Lamp Base", c, new Vector3(1.12f, 0.79f, 0.55f), new Vector3(0.16f, 0.015f, 0.16f), monitor);
                    var shade = Part(PrimitiveType.Cylinder, "Lamp Shade", c, new Vector3(0.95f, 1.2f, 0.5f), new Vector3(0.16f, 0.06f, 0.16f), Mat("Lamp_Shade", Hex("E9C46A"), 0.5f, emission: Hex("5A4410")));
                    shade.transform.localRotation = Quaternion.Euler(20f, 0f, -25f);
                }
                else
                {
                    Part(PrimitiveType.Cylinder, "Plant Pot", c, new Vector3(1.05f, 0.85f, 0.5f), new Vector3(0.16f, 0.08f, 0.16f), Mat("Plant_Pot", Hex("B5653B")));
                    Part(PrimitiveType.Sphere, "Plant", c, new Vector3(1.05f, 1.02f, 0.5f), new Vector3(0.26f, 0.24f, 0.26f), Mat("Plant_Leaves", Hex("4C8C4A"), 0.2f));
                }
                // Office chair: seat, rounded back, gas lift, five-star base.
                var seat = new GameObject("Chair").transform;
                seat.SetParent(c, false);
                seat.localPosition = new Vector3(0.15f, 0f, -0.35f);
                seat.localRotation = Quaternion.Euler(0f, 15f - i * 12f, 0f);
                Part(PrimitiveType.Cube, "Seat", seat, new Vector3(0f, 0.5f, 0f), new Vector3(0.52f, 0.08f, 0.5f), chair);
                Part(PrimitiveType.Capsule, "Back", seat, new Vector3(0f, 0.9f, -0.26f), new Vector3(0.48f, 0.3f, 0.08f), chair);
                Part(PrimitiveType.Cylinder, "Lift", seat, new Vector3(0f, 0.27f, 0f), new Vector3(0.06f, 0.2f, 0.06f), aluminium);
                for (int s = 0; s < 5; s++)
                {
                    var spoke = Part(PrimitiveType.Cube, "Spoke", seat, new Vector3(0f, 0.06f, 0f), new Vector3(0.05f, 0.04f, 0.6f), chair);
                    spoke.transform.localRotation = Quaternion.Euler(0f, s * 72f, 0f);
                    spoke.transform.localPosition = spoke.transform.localRotation * new Vector3(0f, 0.06f, 0.15f);
                }
            }
        }

        static void BuildMeetingRoom(Transform parent)
        {
            var room = Group("Conference Room B", parent);
            float cx = (CubiclesEnd + MeetingEnd) / 2f;
            var frame = Mat("Trim", Hex(TowerNavy), 0.35f);
            var glass = MatGlass("Partition_Glass", new Color(0.8f, 0.88f, 0.95f, 0.16f));
            var frosted = MatGlass("Partition_Frosted", new Color(1f, 1f, 1f, 0.55f), 0.3f);
            var wood = MatTex("Boardroom_Wood", "desk_grain", new Vector2(3f, 1f), Hex("6B4429"), 0.55f);
            var chair = Mat("Chair_Leather", Hex("3A2C2A"), 0.45f);

            // Glass front with a frosted privacy band, framed in navy.
            float front = 3.05f, width = MeetingEnd - CubiclesEnd - 1.2f;
            NoShadow(Part(PrimitiveType.Cube, "Glass Front", room, new Vector3(cx, 1.6f, front), new Vector3(width, 3.2f, 0.02f), glass));
            NoShadow(Part(PrimitiveType.Cube, "Frosted Band", room, new Vector3(cx, 1.15f, front - 0.015f), new Vector3(width, 0.35f, 0.01f), frosted));
            Part(PrimitiveType.Cube, "Head Rail", room, new Vector3(cx, 3.22f, front), new Vector3(width + 0.1f, 0.08f, 0.1f), frame);
            Part(PrimitiveType.Cube, "Floor Rail", room, new Vector3(cx, 0.03f, front), new Vector3(width + 0.1f, 0.06f, 0.1f), frame);
            for (float x = CubiclesEnd + 0.6f; x <= MeetingEnd - 0.6f + 0.01f; x += width / 3f)
                Part(PrimitiveType.Cube, "Post", room, new Vector3(x, 1.6f, front), new Vector3(0.07f, 3.2f, 0.1f), frame);
            WorldText(room, "CONFERENCE ROOM B", new Vector3(cx, 2.9f, front - 0.03f), 0.045f, Hex(TowerNavy));
            WorldText(room, "IN USE: Q3 BLAME ALLOCATION", new Vector3(cx, 1.15f, front - 0.03f), 0.03f, Hex("5B6474"));

            // Long table, chairs, a TV with a chart heading the wrong way.
            Part(PrimitiveType.Cube, "Table", room, new Vector3(cx, 0.76f, 4.05f), new Vector3(5.2f, 0.07f, 1.1f), wood);
            Part(PrimitiveType.Cube, "Table Base", room, new Vector3(cx, 0.38f, 4.05f), new Vector3(4.2f, 0.7f, 0.4f), frame);
            for (int s = 0; s < 5; s++)
            {
                float x = cx - 2f + s;
                Part(PrimitiveType.Cube, "Chair Seat", room, new Vector3(x, 0.5f, 3.35f), new Vector3(0.5f, 0.08f, 0.5f), chair);
                Part(PrimitiveType.Capsule, "Chair Back", room, new Vector3(x, 0.92f, 3.12f), new Vector3(0.46f, 0.34f, 0.08f), chair);
            }
            Part(PrimitiveType.Cube, "TV", room, new Vector3(MeetingEnd - 1.2f, 1.7f, 4.5f), new Vector3(1.5f, 0.88f, 0.06f), Mat("Monitor", Hex("1C1F26"), 0.6f));
            Part(PrimitiveType.Cube, "TV Screen", room, new Vector3(MeetingEnd - 1.2f, 1.7f, 4.465f), new Vector3(1.4f, 0.78f, 0.01f), Mat("TV_Screen", Hex("F4F6FA"), 0.5f, emission: Hex("8A93A6")));
            var chartRed = Mat("Chart_Red", Hex("E04848"), 0.3f, emission: Hex("701818"));
            float[] values = { 0.55f, 0.42f, 0.47f, 0.3f, 0.12f };
            for (int b = 0; b < values.Length; b++)
                Part(PrimitiveType.Cube, "Bar", room, new Vector3(MeetingEnd - 1.7f + b * 0.24f, 1.36f + values[b] / 2f, 4.455f), new Vector3(0.14f, values[b], 0.01f), chartRed);

            // Warm pendant over the table.
            Part(PrimitiveType.Cylinder, "Pendant", room, new Vector3(cx, 5.2f, 4.05f), new Vector3(1.6f, 0.05f, 0.5f), Mat("Pendant", Hex("FFE2B0"), 0.5f, emission: Hex("FFC77A")));
            Part(PrimitiveType.Cylinder, "Pendant Cord", room, new Vector3(cx, 6.1f, 4.05f), new Vector3(0.02f, 0.9f, 0.02f), frame);
            var light = new GameObject("Pendant Light").AddComponent<Light>();
            light.transform.SetParent(room, false);
            light.type = LightType.Point;
            light.color = Hex("FFC77A");
            light.intensity = 3f;
            light.range = 6f;
            light.transform.position = new Vector3(cx, 4.6f, 3.6f);
        }

        static void BuildBreakRoom(Transform parent)
        {
            var room = Group("Break Room", parent);
            var counter = Mat("Counter", Hex("E9E4DA"), 0.6f);
            var cabinet = Mat("Cabinet", Hex("8FA89A"), 0.3f);
            var steel = Mat("Elevator_Steel", Hex("A7B0BE"), 0.75f);
            var navy = Mat("Trim", Hex(TowerNavy), 0.35f);

            float x0 = MeetingEnd + 1.2f;
            // Kitchenette run: cabinets, counter, sink, coffee machine, upper cabinets.
            Part(PrimitiveType.Cube, "Base Cabinets", room, new Vector3(x0 + 2.6f, 0.45f, 4.35f), new Vector3(5.2f, 0.9f, 0.7f), cabinet);
            Part(PrimitiveType.Cube, "Counter", room, new Vector3(x0 + 2.6f, 0.93f, 4.32f), new Vector3(5.3f, 0.06f, 0.76f), counter);
            for (float x = x0 + 0.65f; x < x0 + 5.2f; x += 1.3f)
                Part(PrimitiveType.Cube, "Handle", room, new Vector3(x, 0.78f, 3.99f), new Vector3(0.18f, 0.025f, 0.02f), steel);
            Part(PrimitiveType.Cube, "Sink", room, new Vector3(x0 + 1.4f, 0.95f, 4.3f), new Vector3(0.6f, 0.02f, 0.45f), steel);
            var tap = Part(PrimitiveType.Cylinder, "Tap", room, new Vector3(x0 + 1.4f, 1.1f, 4.5f), new Vector3(0.03f, 0.15f, 0.03f), steel);
            Part(PrimitiveType.Cube, "Upper Cabinets", room, new Vector3(x0 + 2.6f, 2.4f, 4.6f), new Vector3(5.2f, 0.8f, 0.4f), cabinet);

            var machine = Group("Coffee Machine", room);
            machine.position = new Vector3(x0 + 3.4f, 0.96f, 4.35f);
            Part(PrimitiveType.Cube, "Body", machine, new Vector3(0f, 0.28f, 0f), new Vector3(0.42f, 0.56f, 0.4f), Mat("Coffee_Machine", Hex("2B2D33"), 0.7f));
            Part(PrimitiveType.Cube, "Display", machine, new Vector3(0f, 0.45f, -0.205f), new Vector3(0.2f, 0.08f, 0.01f), Mat("Coffee_Display", Hex("FF6B4A"), 0.5f, emission: Hex("FF4A2A")));
            Part(PrimitiveType.Cylinder, "Carafe", machine, new Vector3(0f, 0.1f, -0.1f), new Vector3(0.16f, 0.08f, 0.16f), Mat("Coffee", Hex("4E2A1E"), 0.8f));
            WorldText(room, "OUT OF DECAF", new Vector3(x0 + 3.4f, 1.75f, 4.38f), 0.025f, Hex("C0392B"));

            // Fridge with a passive-aggressive note, and a glowing vending machine.
            Part(PrimitiveType.Cube, "Fridge", room, new Vector3(x0 + 6f, 1f, 4.4f), new Vector3(0.85f, 2f, 0.7f), Mat("Fridge", Hex("E8ECF0"), 0.6f));
            Part(PrimitiveType.Cube, "Fridge Handle", room, new Vector3(x0 + 5.7f, 1.3f, 4.03f), new Vector3(0.03f, 0.5f, 0.03f), steel);
            Part(PrimitiveType.Cube, "Note", room, new Vector3(x0 + 6.05f, 1.45f, 4.045f), new Vector3(0.26f, 0.2f, 0.005f), Mat("Paper", Hex("F5F1E6")));
            WorldText(room, "LABEL YOUR\nFOOD. -MGMT", new Vector3(x0 + 6.05f, 1.45f, 4.04f), 0.012f, Hex("C0392B"));

            var vend = Group("Vending Machine", room);
            vend.position = new Vector3(x0 + 7.4f, 0f, 4.35f);
            Part(PrimitiveType.Cube, "Body", vend, new Vector3(0f, 1.05f, 0f), new Vector3(1f, 2.1f, 0.8f), Mat("Vending", Hex("B23A48"), 0.5f));
            Part(PrimitiveType.Cube, "Window", vend, new Vector3(-0.1f, 1.25f, -0.405f), new Vector3(0.66f, 1.4f, 0.01f), Mat("Vending_Glow", Hex("FFF3D6"), 0.8f, emission: Hex("C9B98A")));
            string[] snacks = { "F2A65A", "4CC9F0", "8FD19E", "FF8FAB" };
            for (int row = 0; row < 4; row++)
                for (int col = 0; col < 4; col++)
                    Part(PrimitiveType.Cube, "Snack", vend, new Vector3(-0.35f + col * 0.17f, 0.7f + row * 0.32f, -0.41f), new Vector3(0.1f, 0.14f, 0.01f), Mat("Snack_" + snacks[(row + col) % 4], Hex(snacks[(row + col) % 4]), 0.4f));
            Part(PrimitiveType.Cube, "Keypad", vend, new Vector3(0.36f, 1.3f, -0.405f), new Vector3(0.14f, 0.3f, 0.01f), navy);

            // Bistro table and stools, plus the water cooler.
            float tx = x0 + 3f;
            Part(PrimitiveType.Cylinder, "Bistro Top", room, new Vector3(tx, 1.05f, 3.35f), new Vector3(0.8f, 0.025f, 0.8f), counter);
            Part(PrimitiveType.Cylinder, "Bistro Post", room, new Vector3(tx, 0.52f, 3.35f), new Vector3(0.06f, 0.52f, 0.06f), steel);
            foreach (float sx in new[] { -0.7f, 0.7f })
            {
                Part(PrimitiveType.Cylinder, "Stool Seat", room, new Vector3(tx + sx, 0.75f, 3.4f), new Vector3(0.36f, 0.04f, 0.36f), navy);
                Part(PrimitiveType.Cylinder, "Stool Post", room, new Vector3(tx + sx, 0.37f, 3.4f), new Vector3(0.04f, 0.37f, 0.04f), steel);
            }
            var cooler = Group("Water Cooler", room);
            cooler.position = new Vector3(x0 + 8.8f, 0f, 4.3f);
            Part(PrimitiveType.Cube, "Base", cooler, new Vector3(0f, 0.55f, 0f), new Vector3(0.45f, 1.1f, 0.45f), Mat("Cooler", Hex("ECEFF3")));
            Part(PrimitiveType.Cylinder, "Bottle", cooler, new Vector3(0f, 1.45f, 0f), new Vector3(0.36f, 0.32f, 0.36f), MatGlass("Cooler_Water", new Color(0.5f, 0.78f, 0.9f, 0.6f)));
        }

        static void BuildForeground(Transform parent)
        {
            // Dark silhouettes between the camera and the lane: frame the bottom edge, never hide fighters.
            var group = Group("Foreground", parent);
            var pot = Mat("Plant_Pot_Dark", Hex("4A2E24"), 0.3f);
            var leaves = Mat("Plant_Leaves_Dark", Hex("2D4A33"), 0.2f);
            var cabinet = Mat("Filing_Cabinet", Hex("5D6573"), 0.5f);
            var handle = Mat("Trim", Hex(TowerNavy), 0.35f);
            var copier = Mat("Copier", Hex("CFCBC2"), 0.4f);

            int i = 0;
            for (float x = 10f; x < LevelEndX - 2f; x += 8.5f, i++)
            {
                float z = -LaneDepth - 2.7f;
                switch (i % 3)
                {
                    case 0:
                        var p = Group("Potted Plant", group);
                        p.position = new Vector3(x, 0f, z);
                        Part(PrimitiveType.Cylinder, "Pot", p, new Vector3(0f, 0.28f, 0f), new Vector3(0.55f, 0.28f, 0.55f), pot);
                        Part(PrimitiveType.Sphere, "Leaves", p, new Vector3(0f, 0.85f, 0f), new Vector3(0.8f, 0.7f, 0.8f), leaves);
                        Part(PrimitiveType.Sphere, "Leaves", p, new Vector3(0.22f, 1.1f, 0.05f), new Vector3(0.5f, 0.55f, 0.5f), leaves);
                        Part(PrimitiveType.Sphere, "Leaves", p, new Vector3(-0.2f, 1.18f, -0.05f), new Vector3(0.45f, 0.5f, 0.45f), leaves);
                        break;
                    case 1:
                        var c = Group("Filing Cabinet", group);
                        c.position = new Vector3(x, 0f, z);
                        Part(PrimitiveType.Cube, "Body", c, new Vector3(0f, 0.55f, 0f), new Vector3(0.6f, 1.1f, 0.6f), cabinet);
                        for (int d = 0; d < 3; d++)
                            Part(PrimitiveType.Cube, "Handle", c, new Vector3(0f, 0.25f + d * 0.33f, -0.31f), new Vector3(0.2f, 0.03f, 0.02f), handle);
                        break;
                    default:
                        var cp = Group("Copier", group);
                        cp.position = new Vector3(x, 0f, z);
                        Part(PrimitiveType.Cube, "Body", cp, new Vector3(0f, 0.5f, 0f), new Vector3(0.9f, 1f, 0.6f), copier);
                        Part(PrimitiveType.Cube, "Lid", cp, new Vector3(0f, 1.03f, 0.05f), new Vector3(0.85f, 0.06f, 0.5f), Mat("Monitor", Hex("1C1F26"), 0.6f));
                        Part(PrimitiveType.Cube, "Panel", cp, new Vector3(0.3f, 0.95f, -0.31f), new Vector3(0.2f, 0.08f, 0.01f), Mat("Copier_Panel", Hex("8FD19E"), 0.5f, emission: Hex("2F7A44")));
                        Part(PrimitiveType.Cube, "Paper Jam", cp, new Vector3(-0.47f, 0.7f, 0f), new Vector3(0.05f, 0.02f, 0.3f), Mat("Paper", Hex("F5F1E6")));
                        break;
                }
            }
        }

        static void BuildDecor(Transform parent)
        {
            var group = Group("Decor", parent);
            var brass = Mat("Brass", Hex(Brass), 0.8f);
            var navy = Mat("Trim", Hex(TowerNavy), 0.35f);

            // Reception: a wood-slat feature wall covering the glass, brass logo, and a front desk.
            float lobbyWidth = LobbyEnd - LevelStartX + 1f;
            float lx = LevelStartX - 1f + lobbyWidth / 2f;
            Part(PrimitiveType.Cube, "Feature Wall", group, new Vector3(lx, 3.3f, WallZ - 0.25f), new Vector3(lobbyWidth, 6.6f, 0.12f),
                MatTex("Wood_Slats", "wood_slats", new Vector2(lobbyWidth / 1.2f, 1f), Color.white, 0.3f));
            WorldText(group, "SYNERGEX", new Vector3(lx, 4.3f, WallZ - 0.33f), 0.16f, Hex(Brass));
            WorldText(group, "Leveraging people since 1987", new Vector3(lx, 3.65f, WallZ - 0.33f), 0.035f, Hex(MemoCream));
            WorldText(group, "FLOOR 1  ·  THE CUBICLE FARM", new Vector3(lx, 3.15f, WallZ - 0.33f), 0.035f, Hex(MemoCream));

            var desk = Group("Reception Desk", group);
            desk.position = new Vector3(lx + 1.5f, 0f, 3.75f);
            Part(PrimitiveType.Cube, "Front", desk, new Vector3(0f, 0.55f, 0f), new Vector3(3.2f, 1.1f, 0.6f), navy);
            Part(PrimitiveType.Cube, "Top", desk, new Vector3(0f, 1.13f, 0.05f), new Vector3(3.3f, 0.05f, 0.75f), Mat("Counter", Hex("E9E4DA"), 0.6f));
            Part(PrimitiveType.Cube, "Brass Band", desk, new Vector3(0f, 0.85f, -0.305f), new Vector3(3.2f, 0.05f, 0.01f), brass);
            Part(PrimitiveType.Cylinder, "Bell", desk, new Vector3(0.9f, 1.19f, -0.1f), new Vector3(0.1f, 0.04f, 0.1f), brass);
            Part(PrimitiveType.Cube, "Visitor Book", desk, new Vector3(-0.6f, 1.165f, -0.1f), new Vector3(0.35f, 0.02f, 0.25f), Mat("Seminar_Book", Hex("7B4FB0")));

            // Posters on the columns, in the office's motivational dialect.
            Poster(group, "SYNERGY", new Vector3(LobbyEnd, 3.8f, WallZ - 0.97f), Hex("2E86AB"));
            Poster(group, "HANG IN\nTHERE", new Vector3(CubiclesEnd, 3.8f, WallZ - 0.97f), Hex("E07A5F"));
            Poster(group, "TEAMWORK\nMAKES THE\nDREAM WORK", new Vector3(BreakEnd, 3.8f, WallZ - 0.97f), Hex("81B29A"));

            BuildMeetingRoom(group);
            BuildBreakRoom(group);

            // Elevator bank: dark stone surround, a second car that is (of course) out of order.
            var stone = Mat("Elevator_Stone", Hex("2E3446"), 0.55f);
            float bankWidth = LevelEndX + 1f - BreakEnd - 0.4f;
            Part(PrimitiveType.Cube, "Elevator Wall", group, new Vector3(BreakEnd + 0.4f + bankWidth / 2f, 3.3f, WallZ - 0.2f), new Vector3(bankWidth, 6.6f, 0.12f), stone);
            WorldText(group, "CORNER OFFICE: 40 FLOORS UP", new Vector3(54.5f, 5.6f, WallZ - 0.28f), 0.06f, Hex(Brass));
            var broken = Group("Elevator (Out of Order)", group);
            broken.position = new Vector3(51.6f, 0f, 0f);
            Part(PrimitiveType.Cube, "Frame", broken, new Vector3(0f, 1.9f, 4.72f), new Vector3(2.2f, 3.6f, 0.1f), brass);
            Part(PrimitiveType.Cube, "Doors", broken, new Vector3(0f, 1.7f, 4.64f), new Vector3(1.9f, 3.2f, 0.06f), Mat("Elevator_Steel", Hex("A7B0BE"), 0.75f));
            Part(PrimitiveType.Cube, "Sign", broken, new Vector3(0f, 1.9f, 4.6f), new Vector3(0.7f, 0.45f, 0.01f), Mat("Paper", Hex("F5F1E6")));
            WorldText(broken, "OUT OF\nORDER", new Vector3(51.6f, 1.9f, 4.59f), 0.03f, Hex("C0392B"));
        }

        /// <summary>A framed poster on a column face (0.8 m wide), its text sized to fit inside the paper.</summary>
        static void Poster(Transform parent, string text, Vector3 position, Color color)
        {
            Part(PrimitiveType.Cube, "Poster Frame", parent, position + new Vector3(0f, 0f, 0.01f), new Vector3(0.76f, 1.08f, 0.02f), Mat("Trim", Hex(TowerNavy), 0.35f));
            Part(PrimitiveType.Cube, "Poster", parent, position, new Vector3(0.68f, 1f, 0.02f), Mat("Poster_" + text.Split('\n')[0], color));
            // Barlow Condensed at this setting is about 3.3 x size metres per character.
            int longest = text.Split('\n').Max(line => line.Length);
            float size = Mathf.Min(0.034f, 0.56f / (3.3f * Mathf.Max(longest, 1)));
            WorldText(parent, text, position + new Vector3(0f, 0f, -0.03f), size, Color.white);
        }

        static ElevatorDoor BuildElevator(Transform parent, Vector3 position)
        {
            var steel = Mat("Elevator_Steel", Hex("A7B0BE"), 0.75f);
            var frame = Mat("Elevator_Frame", Hex(Brass), 0.8f);

            var root = Group("Elevator", parent);
            root.position = position;
            var zone = root.gameObject.AddComponent<BoxCollider>();
            zone.isTrigger = true;
            zone.center = new Vector3(0f, 1.5f, 0f);
            zone.size = new Vector3(2f, 3f, LaneDepth * 2f + 1f);

            Part(PrimitiveType.Cube, "Frame", root, new Vector3(0f, 1.9f, 4.72f), new Vector3(2.6f, 3.8f, 0.12f), frame);
            Part(PrimitiveType.Cube, "Car", root, new Vector3(0f, 1.6f, 4.9f), new Vector3(1.8f, 3.2f, 0.3f), Mat("Elevator_Car", Hex("FFE2B0"), 0.4f, emission: Hex("806040")));
            var left = Part(PrimitiveType.Cube, "Door L", root, new Vector3(-0.45f, 1.6f, 4.6f), new Vector3(0.9f, 3.2f, 0.08f), steel);
            var right = Part(PrimitiveType.Cube, "Door R", root, new Vector3(0.45f, 1.6f, 4.6f), new Vector3(0.9f, 3.2f, 0.08f), steel);
            var indicator = Part(PrimitiveType.Cube, "Indicator", root, new Vector3(0f, 3.55f, 4.6f), new Vector3(0.5f, 0.2f, 0.05f),
                Mat("Indicator_Locked", Hex("C0392B"), emission: Hex("FF2D20")));
            Part(PrimitiveType.Cylinder, "Call Button", root, new Vector3(1.55f, 1.2f, 4.62f), new Vector3(0.12f, 0.01f, 0.12f), frame)
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            WorldText(root, "UP", new Vector3(position.x, 3.95f, 4.55f), 0.07f, Color.white);

            var door = root.gameObject.AddComponent<ElevatorDoor>();
            Wire(door, "leftDoor", left.transform);
            Wire(door, "rightDoor", right.transform);
            Wire(door, "indicator", indicator.GetComponent<Renderer>());
            Wire(door, "lockedMaterial", Mat("Indicator_Locked", Hex("C0392B"), emission: Hex("FF2D20")));
            Wire(door, "openMaterial", Mat("Indicator_Open", Hex("27AE60"), emission: Hex("2DFF7A")));
            return door;
        }
    }
}
