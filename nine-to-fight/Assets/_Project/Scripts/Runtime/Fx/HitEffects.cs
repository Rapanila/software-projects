using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NineToFight.Fx
{
    /// <summary>
    /// Pooled impact effects: paper-scrap and spark bursts, glowing impact flashes, dust puffs
    /// (footsteps, landings, dodges), floating damage numbers and knockout callouts.
    /// Built from primitives plus two soft FX materials in Resources/FX.
    /// </summary>
    public class HitEffects : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        const int DebrisPool = 160;
        const int TextPool = 24;
        const float Gravity = -18f;

        static HitEffects instance;

        class Debris
        {
            public Transform transform;
            public Renderer renderer;
            public Vector3 velocity, spin, baseScale;
            public float life, maxLife, size;
        }

        class Popup
        {
            public Transform transform;
            public TextMesh text;
            public Color color;
            public float life, maxLife, rise, scale;
        }

        /// <summary>A camera-facing soft sprite: impact flash (additive) or dust puff (alpha).</summary>
        class Puff
        {
            public Transform transform;
            public Renderer renderer;
            public Color color;
            public Vector3 velocity;
            public float life, maxLife, startSize, endSize;
        }

        readonly List<Debris> debris = new List<Debris>();
        readonly List<Popup> popups = new List<Popup>();
        readonly List<Puff> flashes = new List<Puff>();
        readonly List<Puff> dust = new List<Puff>();
        MaterialPropertyBlock block;
        Material material;
        Font font;
        int nextDebris, nextPopup, nextFlash, nextDust;

        /// <summary>Creates the effect pool; the unlit material tints each scrap.</summary>
        public static void Ensure(Material unlitMaterial)
        {
            if (instance != null) return;
            var go = new GameObject("Hit Effects");
            instance = go.AddComponent<HitEffects>();
            instance.Init(unlitMaterial);
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        void Init(Material unlitMaterial)
        {
            material = unlitMaterial;
            block = new MaterialPropertyBlock();
            font = UI.UiKit.Display;
            BuildPuffs(flashes, Resources.Load<Material>("FX/Glow"), 24, "Flash");
            BuildPuffs(dust, Resources.Load<Material>("FX/Dust"), 48, "Dust");

            if (material != null)
            {
                for (int i = 0; i < DebrisPool; i++)
                {
                    var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cube.name = "Scrap";
                    Destroy(cube.GetComponent<Collider>());
                    cube.transform.SetParent(transform, false);
                    var r = cube.GetComponent<Renderer>();
                    r.sharedMaterial = material;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    r.receiveShadows = false;
                    cube.SetActive(false);
                    debris.Add(new Debris { transform = cube.transform, renderer = r });
                }
            }

            for (int i = 0; i < TextPool; i++)
            {
                var go = new GameObject("Popup");
                go.transform.SetParent(transform, false);
                var text = go.AddComponent<TextMesh>();
                text.font = font;
                text.fontSize = 64;
                text.characterSize = 0.05f;
                text.anchor = TextAnchor.MiddleCenter;
                text.alignment = TextAlignment.Center;
                go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
                go.SetActive(false);
                popups.Add(new Popup { transform = go.transform, text = text });
            }
        }

        void BuildPuffs(List<Puff> pool, Material mat, int count, string name)
        {
            if (mat == null) return;
            for (int i = 0; i < count; i++)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = name;
                Destroy(quad.GetComponent<Collider>());
                quad.transform.SetParent(transform, false);
                var r = quad.GetComponent<Renderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                quad.SetActive(false);
                pool.Add(new Puff { transform = quad.transform, renderer = r });
            }
        }

        static Puff Next(List<Puff> pool, ref int next)
        {
            var p = pool[next];
            next = (next + 1) % pool.Count;
            return p;
        }

        /// <summary>A bright bloom of light at the point of impact.</summary>
        public static void Flash(Vector3 position, Color color, float size = 1.2f)
        {
            if (instance == null || instance.flashes.Count == 0) return;
            var f = Next(instance.flashes, ref instance.nextFlash);
            f.color = color;
            f.maxLife = f.life = 0.12f;
            f.startSize = size * 0.35f;
            f.endSize = size;
            f.velocity = Vector3.zero;
            f.transform.position = position + new Vector3(0f, 0f, -0.3f);
            f.transform.gameObject.SetActive(true);
        }

        /// <summary>A few soft dust puffs kicked up from the floor.</summary>
        public static void Dust(Vector3 position, float size = 0.5f, int count = 3, Vector3 push = default)
        {
            if (instance == null || instance.dust.Count == 0) return;
            for (int i = 0; i < count; i++)
            {
                var d = Next(instance.dust, ref instance.nextDust);
                d.color = new Color(0.82f, 0.78f, 0.7f, 0.5f);
                d.maxLife = d.life = Random.Range(0.35f, 0.6f);
                d.startSize = size * Random.Range(0.4f, 0.7f);
                d.endSize = size * Random.Range(1.1f, 1.6f);
                d.velocity = push * Random.Range(0.5f, 1f) + new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(0.3f, 0.8f), Random.Range(-0.3f, 0.3f));
                d.transform.position = new Vector3(position.x, 0.12f, position.z) + new Vector3(Random.Range(-0.15f, 0.15f), 0f, Random.Range(-0.1f, 0.1f));
                d.transform.gameObject.SetActive(true);
            }
        }

        void UpdatePuffs(List<Puff> pool, float dt, Camera cam, bool fadeIn)
        {
            foreach (var p in pool)
            {
                if (!p.transform.gameObject.activeSelf) continue;
                p.life -= dt;
                if (p.life <= 0f)
                {
                    p.transform.gameObject.SetActive(false);
                    continue;
                }
                float age = 1f - p.life / p.maxLife;
                p.velocity *= 1f - 3f * dt;
                p.transform.position += p.velocity * dt;
                float size = Mathf.Lerp(p.startSize, p.endSize, 1f - (1f - age) * (1f - age));
                p.transform.localScale = Vector3.one * size;
                if (cam != null) p.transform.rotation = cam.transform.rotation;
                var c = p.color;
                c.a *= fadeIn ? Mathf.Clamp01(age * 6f) * (1f - age) * 1.3f : 1f - age;
                block.SetColor(BaseColorId, c);
                p.renderer.SetPropertyBlock(block);
            }
        }

        /// <summary>Sprays scraps in a cone away from the hit, biased in <paramref name="direction"/>.</summary>
        public static void Burst(Vector3 position, Vector3 direction, Color color, int count, float speed = 6f, float size = 0.09f)
        {
            if (instance == null || instance.debris.Count == 0) return;
            for (int i = 0; i < count; i++)
            {
                var d = instance.debris[instance.nextDebris];
                instance.nextDebris = (instance.nextDebris + 1) % instance.debris.Count;

                Vector3 v = Random.insideUnitSphere + direction.normalized * 0.9f + Vector3.up * 0.8f;
                d.velocity = v.normalized * speed * Random.Range(0.5f, 1.2f);
                d.spin = Random.insideUnitSphere * 900f;
                d.maxLife = d.life = Random.Range(0.35f, 0.7f);
                d.size = size * Random.Range(0.6f, 1.4f);
                d.transform.position = position;
                d.transform.rotation = Random.rotation;
                // Flat scraps read as paper; thicker ones as sparks.
                d.baseScale = new Vector3(d.size, d.size * Random.Range(0.15f, 1f), d.size);
                d.transform.localScale = d.baseScale;
                Color c = Color.Lerp(color, Color.white, Random.Range(0f, 0.45f));
                instance.block.SetColor(BaseColorId, c);
                d.renderer.SetPropertyBlock(instance.block);
                d.transform.gameObject.SetActive(true);
            }
        }

        /// <summary>Text that pops, floats up and fades: damage numbers and callouts.</summary>
        public static void Text(Vector3 position, string message, Color color, float scale = 1f, float life = 0.8f)
        {
            if (instance == null) return;
            var p = instance.popups[instance.nextPopup];
            instance.nextPopup = (instance.nextPopup + 1) % instance.popups.Count;
            p.text.text = message;
            p.color = color;
            p.text.color = color;
            p.maxLife = p.life = life;
            p.rise = 1.4f;
            p.scale = scale;
            p.transform.position = position + new Vector3(Random.Range(-0.25f, 0.25f), 0f, -0.6f);
            p.transform.gameObject.SetActive(true);
        }

        void LateUpdate()
        {
            // Unscaled so effects keep moving (slowly) through hit-stop freezes.
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f) * Mathf.Max(Time.timeScale, 0.25f);
            if (Time.timeScale == 0f) dt = 0f;

            foreach (var d in debris)
            {
                if (!d.transform.gameObject.activeSelf) continue;
                d.life -= dt;
                if (d.life <= 0f)
                {
                    d.transform.gameObject.SetActive(false);
                    continue;
                }
                d.velocity.y += Gravity * dt;
                d.velocity *= 1f - 2.5f * dt;
                Vector3 pos = d.transform.position + d.velocity * dt;
                if (pos.y < 0.02f)
                {
                    pos.y = 0.02f;
                    d.velocity = new Vector3(d.velocity.x * 0.5f, -d.velocity.y * 0.3f, d.velocity.z * 0.5f);
                }
                d.transform.position = pos;
                d.transform.Rotate(d.spin * dt, Space.Self);
                float shrink = Mathf.Clamp01(d.life / (d.maxLife * 0.4f));
                d.transform.localScale = d.baseScale * shrink;
            }

            var cam = Camera.main;
            UpdatePuffs(flashes, dt, cam, false);
            UpdatePuffs(dust, dt, cam, true);
            foreach (var p in popups)
            {
                if (!p.transform.gameObject.activeSelf) continue;
                p.life -= dt;
                if (p.life <= 0f)
                {
                    p.transform.gameObject.SetActive(false);
                    continue;
                }
                float age = 1f - p.life / p.maxLife;
                p.transform.position += Vector3.up * p.rise * (1f - age) * dt * 1.6f;
                float pop = age < 0.12f ? Mathf.Lerp(1.8f, 1f, age / 0.12f) : 1f;
                p.transform.localScale = Vector3.one * p.scale * pop;
                if (cam != null) p.transform.rotation = cam.transform.rotation;
                var c = p.color;
                c.a = age > 0.6f ? Mathf.Lerp(1f, 0f, (age - 0.6f) / 0.4f) : 1f;
                p.text.color = c;
            }
        }
    }
}
