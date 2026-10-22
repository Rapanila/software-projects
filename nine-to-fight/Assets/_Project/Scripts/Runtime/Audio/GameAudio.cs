using System.Collections.Generic;
using UnityEngine;

namespace NineToFight.Audio
{
    public enum Sfx
    {
        HitLight, HitHeavy, PlayerHurt, Whoosh, Dodge, KnockOut, Pickup, Promote,
        ElevatorDing, UiMove, UiConfirm, ComboUp, BossIntro, GameOver,
        Footstep, DoorSlide, Heartbeat, UiBack, ElevatorHum,
    }

    public enum MusicTrack { None, Lounge, Fight, Boss }

    /// <summary>Plays the synthesized effects and music. Created on demand; no scene setup needed.</summary>
    public class GameAudio : MonoBehaviour
    {
        const int Voices = 16;
        const string MuteKey = "NineToFight.Muted";

        static GameAudio instance;
        static readonly Dictionary<Sfx, AudioClip> Clips = new Dictionary<Sfx, AudioClip>();
        static readonly Dictionary<MusicTrack, AudioClip> Music = new Dictionary<MusicTrack, AudioClip>();

        readonly AudioSource[] voices = new AudioSource[Voices];
        AudioSource music, ambience;
        int nextVoice;
        MusicTrack currentTrack;
        float musicVolume = 0.32f;

        public static bool Muted { get; private set; }

        static GameAudio Instance
        {
            get
            {
                if (instance == null && Application.isPlaying)
                {
                    var go = new GameObject("Game Audio");
                    // Survives restarts so the clips are synthesized once per session.
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<GameAudio>();
                }
                return instance;
            }
        }

        void Awake()
        {
            instance = this;
            Muted = PlayerPrefs.GetInt(MuteKey, 0) == 1;
            for (int i = 0; i < Voices; i++)
            {
                voices[i] = gameObject.AddComponent<AudioSource>();
                voices[i].playOnAwake = false;
                voices[i].spatialBlend = 0f;
            }
            music = gameObject.AddComponent<AudioSource>();
            music.loop = true;
            music.playOnAwake = false;
            music.volume = musicVolume;
            ambience = gameObject.AddComponent<AudioSource>();
            ambience.loop = true;
            ambience.playOnAwake = false;
            Game.GameSettings.Changed += ApplyMute;
            ApplyMute();
        }

        void OnDestroy()
        {
            Game.GameSettings.Changed -= ApplyMute;
            if (instance == this) instance = null;
        }

        static AudioClip Clip(Sfx sfx)
        {
            if (!Clips.TryGetValue(sfx, out var clip) || clip == null)
                Clips[sfx] = clip = Synth.Build(sfx);
            return clip;
        }

        /// <summary>Fire-and-forget sound effect with slight pitch variation so repeats don't grate.</summary>
        public static void Play(Sfx sfx, float volume = 1f, float pitchJitter = 0.08f)
        {
            var self = Instance;
            if (self == null) return;
            var source = self.voices[self.nextVoice];
            self.nextVoice = (self.nextVoice + 1) % Voices;
            source.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            source.PlayOneShot(Clip(sfx), volume * 0.8f * Game.GameSettings.EffectsVolume);
        }

        public static void PlayMusic(MusicTrack track)
        {
            var self = Instance;
            if (self == null || self.currentTrack == track) return;
            self.currentTrack = track;
            if (track == MusicTrack.None)
            {
                self.music.Stop();
                return;
            }
            if (!Music.TryGetValue(track, out var clip) || clip == null)
            {
                var def = track == MusicTrack.Boss ? Synth.Boss : track == MusicTrack.Fight ? Synth.Fight : Synth.Lounge;
                Music[track] = clip = Synth.BuildMusic(def);
            }
            self.music.clip = clip;
            self.music.Play();
        }

        /// <summary>Synthesizes every clip up front so the first fight doesn't hitch.</summary>
        public static void Prewarm()
        {
            if (Instance == null) return;
            foreach (Sfx sfx in System.Enum.GetValues(typeof(Sfx))) Clip(sfx);
            foreach (MusicTrack track in new[] { MusicTrack.Fight, MusicTrack.Boss })
                if (!Music.TryGetValue(track, out var existing) || existing == null)
                    Music[track] = Synth.BuildMusic(track == MusicTrack.Boss ? Synth.Boss : Synth.Fight);
        }

        /// <summary>Muffles the music (pause menu) without stopping it.</summary>
        public static void SetMusicDucked(bool ducked)
        {
            var self = Instance;
            if (self == null) return;
            self.ducked = ducked;
            self.ApplyMute();
        }

        public static void ToggleMute()
        {
            Muted = !Muted;
            PlayerPrefs.SetInt(MuteKey, Muted ? 1 : 0);
            if (Instance != null) Instance.ApplyMute();
        }

        bool ducked;

        /// <summary>Applies mute, the master/music/effects settings and pause ducking.</summary>
        void ApplyMute()
        {
            AudioListener.volume = Muted ? 0f : Game.GameSettings.MasterVolume;
            if (music != null) music.volume = musicVolume * Game.GameSettings.MusicVolume * (ducked ? 0.35f : 1f);
            if (ambience != null) ambience.volume = 0.22f * Game.GameSettings.EffectsVolume * (ducked ? 0.35f : 1f);
        }

        /// <summary>Office room tone under the fighting: HVAC hum, keyboards, the odd phone.</summary>
        public static void PlayAmbience(bool on)
        {
            var self = Instance;
            if (self == null) return;
            if (!on)
            {
                self.ambience.Stop();
                return;
            }
            if (self.ambience.isPlaying) return;
            if (ambienceClip == null) ambienceClip = Synth.BuildAmbience();
            self.ambience.clip = ambienceClip;
            self.ambience.Play();
        }

        static AudioClip ambienceClip;
    }
}
