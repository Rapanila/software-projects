using NineToFight.Player;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NineToFight.Fx
{
    /// <summary>
    /// Camera-level finish: turns on post-processing and anti-aliasing, adds a warm/cool colour grade,
    /// kicks chromatic aberration on big hits, and drains colour when the player is close to going down.
    /// </summary>
    public class ScreenFx : MonoBehaviour
    {
        static ScreenFx instance;

        Volume volume;
        VolumeProfile profile;
        ColorAdjustments grade;
        ChromaticAberration chroma;
        float kick;

        /// <summary>A short lens jolt for heavy impacts (0..1).</summary>
        public static void Kick(float amount)
        {
            if (instance != null) instance.kick = Mathf.Max(instance.kick, Mathf.Clamp01(amount));
        }

        void Awake()
        {
            instance = this;
            var cam = Camera.main;
            if (cam != null)
            {
                var data = cam.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                data.antialiasingQuality = AntialiasingQuality.High;
            }

            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Screen FX (runtime)";
            grade = profile.Add<ColorAdjustments>(true);
            grade.contrast.Override(12f);
            grade.saturation.Override(8f);
            grade.postExposure.Override(0.1f);
            var split = profile.Add<SplitToning>(true);
            split.shadows.Override(new Color(0.42f, 0.52f, 0.66f));
            split.highlights.Override(new Color(1f, 0.86f, 0.7f));
            split.balance.Override(-15f);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.26f);
            vignette.smoothness.Override(0.45f);
            chroma = profile.Add<ChromaticAberration>(true);
            chroma.intensity.Override(0f);

            var go = new GameObject("Screen FX Volume");
            go.transform.SetParent(transform, false);
            volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
            if (profile != null) Destroy(profile);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            kick = Mathf.MoveTowards(kick, 0f, dt * 3f);
            chroma.intensity.value = kick * 0.9f;

            // Low health: the world loses its colour as you fade.
            float low = 0f;
            var player = PlayerController.Instance;
            if (player != null && !player.Health.IsDead) low = Mathf.Clamp01((0.35f - player.Health.Normalized) / 0.35f);
            grade.saturation.value = Mathf.Lerp(8f, -55f, low);
        }
    }
}
