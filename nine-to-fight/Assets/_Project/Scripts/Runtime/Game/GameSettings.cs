using System.Collections.Generic;
using UnityEngine;

namespace NineToFight.Game
{
    /// <summary>Player options: saved between sessions, applied as soon as they change.</summary>
    public static class GameSettings
    {
        public enum Id { MasterVolume, MusicVolume, EffectsVolume, ScreenShake, DamageNumbers, Hints, Fullscreen, VSync }

        public static readonly IReadOnlyList<Id> All = new[]
        {
            Id.MasterVolume, Id.MusicVolume, Id.EffectsVolume, Id.ScreenShake, Id.DamageNumbers, Id.Hints, Id.Fullscreen, Id.VSync,
        };

        const string Prefix = "NineToFight.Settings.";
        static readonly Dictionary<Id, int> Values = new Dictionary<Id, int>();
        static bool loaded;

        /// <summary>Raised after any change so systems can pick up the new values.</summary>
        public static event System.Action Changed;

        public static float MasterVolume => Get(Id.MasterVolume) / 10f;
        public static float MusicVolume => Get(Id.MusicVolume) / 10f;
        public static float EffectsVolume => Get(Id.EffectsVolume) / 10f;
        /// <summary>0 = off, 0.5 = reduced, 1 = full.</summary>
        public static float ScreenShake => Get(Id.ScreenShake) * 0.5f;
        public static bool DamageNumbers => Get(Id.DamageNumbers) == 1;
        public static bool Hints => Get(Id.Hints) == 1;
        public static bool Fullscreen => Get(Id.Fullscreen) == 1;
        public static bool VSync => Get(Id.VSync) == 1;

        static int Default(Id id)
        {
            switch (id)
            {
                case Id.MasterVolume: return 8;
                case Id.MusicVolume: return 7;
                case Id.EffectsVolume: return 9;
                case Id.ScreenShake: return 2;
                default: return 1;
            }
        }

        static int Max(Id id)
        {
            switch (id)
            {
                case Id.MasterVolume:
                case Id.MusicVolume:
                case Id.EffectsVolume: return 10;
                case Id.ScreenShake: return 2;
                default: return 1;
            }
        }

        public static string Label(Id id)
        {
            switch (id)
            {
                case Id.MasterVolume: return "Master volume";
                case Id.MusicVolume: return "Music";
                case Id.EffectsVolume: return "Sound effects";
                case Id.ScreenShake: return "Screen shake";
                case Id.DamageNumbers: return "Damage numbers";
                case Id.Hints: return "Tutorial hints";
                case Id.Fullscreen: return "Fullscreen";
                case Id.VSync: return "V-Sync";
                default: return id.ToString();
            }
        }

        public static string ValueText(Id id)
        {
            int v = Get(id);
            switch (id)
            {
                case Id.MasterVolume:
                case Id.MusicVolume:
                case Id.EffectsVolume: return v == 0 ? "Off" : $"{v * 10}%";
                case Id.ScreenShake: return v == 0 ? "Off" : v == 1 ? "Reduced" : "Full";
                default: return v == 1 ? "On" : "Off";
            }
        }

        /// <summary>0..1 fill for slider-style settings, or -1 for toggles.</summary>
        public static float Fraction(Id id) => Max(id) > 2 ? Get(id) / (float)Max(id) : -1f;

        public static int Get(Id id)
        {
            EnsureLoaded();
            return Values.TryGetValue(id, out int v) ? v : Default(id);
        }

        /// <summary>Moves a setting one notch (toggles flip). Saves and applies.</summary>
        public static void Step(Id id, int direction)
        {
            EnsureLoaded();
            int max = Max(id);
            int v = Get(id);
            v = max == 1 ? 1 - v : Mathf.Clamp(v + direction, 0, max);
            Values[id] = v;
            PlayerPrefs.SetInt(Prefix + id, v);
            PlayerPrefs.Save();
            Apply();
        }

        static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            foreach (var id in All) Values[id] = Mathf.Clamp(PlayerPrefs.GetInt(Prefix + id, Default(id)), 0, Max(id));
        }

        /// <summary>Pushes every setting into the engine and the game systems.</summary>
        public static void Apply()
        {
            EnsureLoaded();
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            if (!Application.isEditor && Screen.fullScreen != Fullscreen)
                Screen.fullScreenMode = Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            Changed?.Invoke();
        }
    }
}
