using System;
using UnityEngine;

namespace NineToFight.Audio
{
    /// <summary>
    /// Builds every sound in the game from maths at startup: no audio files to import.
    /// Effects are short enveloped noise/tone blends; music is a looping lounge "muzak" track.
    /// </summary>
    public static class Synth
    {
        public const int SampleRate = 44100;
        const float TwoPi = Mathf.PI * 2f;

        // ------------------------------------------------------------------ primitives

        static float[] Buffer(float seconds) => new float[Mathf.CeilToInt(seconds * SampleRate)];

        public static AudioClip ToClip(string name, float[] samples, bool normalize = true, float peak = 0.9f, bool fadeOut = true)
        {
            // One-shots end in silence however their last segment was shaped (loops keep their seam).
            if (fadeOut)
                for (int i = 0; i < ReleaseSamples && i < samples.Length; i++)
                    samples[samples.Length - 1 - i] *= i / (float)ReleaseSamples;
            if (normalize)
            {
                float max = 0.0001f;
                foreach (float s in samples) max = Mathf.Max(max, Mathf.Abs(s));
                float gain = peak / max;
                for (int i = 0; i < samples.Length; i++) samples[i] *= gain;
            }
            var clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>Every tone and noise burst fades out over its last few milliseconds, so a cut-off never clicks.</summary>
        public const float ReleaseSeconds = 0.008f;
        static readonly int ReleaseSamples = Mathf.CeilToInt(ReleaseSeconds * SampleRate);

        /// <summary>1 inside a segment, ramping to 0 over its last <see cref="ReleaseSeconds"/>.</summary>
        static float Release(int index, int count) => Mathf.Clamp01((count - index) / (float)ReleaseSamples);

        /// <summary>Attack/decay envelope: quick rise then exponential fall.</summary>
        static float Env(float t, float attack, float decay) =>
            t < attack ? t / Mathf.Max(attack, 0.0001f) : Mathf.Exp(-(t - attack) / Mathf.Max(decay, 0.0001f));

        /// <summary>Adds a tone whose pitch glides from f0 to f1.</summary>
        static void Tone(float[] buf, float start, float length, float f0, float f1, float amp,
            float attack, float decay, Func<float, float> wave)
        {
            int from = Mathf.FloorToInt(start * SampleRate);
            int count = Mathf.Min(buf.Length - from, Mathf.CeilToInt(length * SampleRate));
            float phase = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float f = Mathf.Lerp(f0, f1, t / length);
                phase += f / SampleRate;
                buf[from + i] += wave(phase) * amp * Env(t, attack, decay) * Release(i, count);
            }
        }

        /// <summary>Adds one-pole low-passed noise; cutoff glides between two values.</summary>
        static void Noise(float[] buf, System.Random rng, float start, float length, float amp,
            float attack, float decay, float cutoff0, float cutoff1)
        {
            int from = Mathf.FloorToInt(start * SampleRate);
            int count = Mathf.Min(buf.Length - from, Mathf.CeilToInt(length * SampleRate));
            float lp = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float cutoff = Mathf.Lerp(cutoff0, cutoff1, t / length);
                float a = 1f - Mathf.Exp(-TwoPi * cutoff / SampleRate);
                lp += a * ((float)rng.NextDouble() * 2f - 1f - lp);
                buf[from + i] += lp * amp * Env(t, attack, decay) * Release(i, count);
            }
        }

        public static float Sine(float p) => Mathf.Sin(p * TwoPi);
        public static float Square(float p) => (p - Mathf.Floor(p)) < 0.5f ? 1f : -1f;
        public static float Triangle(float p) { float x = p - Mathf.Floor(p); return 4f * Mathf.Abs(x - 0.5f) - 1f; }
        static float Saw(float p) => 2f * (p - Mathf.Floor(p)) - 1f;

        /// <summary>Soft electric-piano voice: sine with a fast-decaying bell overtone.</summary>
        static float EPiano(float p) => Mathf.Sin(p * TwoPi) + 0.25f * Mathf.Sin(p * TwoPi * 3f);

        public static float Midi(float note) => 440f * Mathf.Pow(2f, (note - 69f) / 12f);

        // ------------------------------------------------------------------ effects

        public static AudioClip Build(Sfx sfx)
        {
            var rng = new System.Random((int)sfx * 7919 + 13);
            float[] b;
            switch (sfx)
            {
                case Sfx.HitLight:
                    b = Buffer(0.16f);
                    Noise(b, rng, 0f, 0.16f, 1f, 0.001f, 0.03f, 6000f, 1500f);
                    Tone(b, 0f, 0.14f, 220f, 70f, 0.9f, 0.002f, 0.05f, Sine);
                    break;
                case Sfx.HitHeavy:
                    b = Buffer(0.32f);
                    Noise(b, rng, 0f, 0.3f, 1f, 0.001f, 0.06f, 4000f, 400f);
                    Tone(b, 0f, 0.3f, 140f, 38f, 1.2f, 0.002f, 0.11f, Sine);
                    Tone(b, 0f, 0.08f, 900f, 300f, 0.25f, 0.001f, 0.02f, Square);
                    break;
                case Sfx.PlayerHurt:
                    b = Buffer(0.28f);
                    Tone(b, 0f, 0.26f, 420f, 150f, 0.6f, 0.002f, 0.1f, Square);
                    Noise(b, rng, 0f, 0.12f, 0.6f, 0.001f, 0.04f, 3000f, 800f);
                    break;
                case Sfx.Whoosh:
                    b = Buffer(0.2f);
                    Noise(b, rng, 0f, 0.2f, 1f, 0.07f, 0.06f, 700f, 3200f);
                    break;
                case Sfx.Dodge:
                    b = Buffer(0.22f);
                    Noise(b, rng, 0f, 0.22f, 1f, 0.02f, 0.08f, 3000f, 900f);
                    Tone(b, 0f, 0.15f, 500f, 900f, 0.15f, 0.01f, 0.05f, Sine);
                    break;
                case Sfx.KnockOut:
                    b = Buffer(0.7f);
                    Tone(b, 0f, 0.45f, 520f, 90f, 0.6f, 0.005f, 0.2f, Triangle);
                    Noise(b, rng, 0.38f, 0.3f, 1f, 0.002f, 0.07f, 1800f, 200f);
                    Tone(b, 0.38f, 0.3f, 90f, 40f, 1f, 0.002f, 0.1f, Sine);
                    break;
                case Sfx.Pickup:
                    b = Buffer(0.4f);
                    Tone(b, 0f, 0.2f, Midi(79), Midi(79), 0.5f, 0.003f, 0.08f, EPiano);
                    Tone(b, 0.08f, 0.32f, Midi(86), Midi(86), 0.5f, 0.003f, 0.12f, EPiano);
                    break;
                case Sfx.Promote:
                    b = Buffer(1.3f);
                    int[] arp = { 60, 64, 67, 72, 76, 79, 84 };
                    for (int i = 0; i < arp.Length; i++)
                        Tone(b, i * 0.07f, 0.8f, Midi(arp[i]), Midi(arp[i]), 0.35f, 0.003f, 0.25f, Square);
                    Tone(b, 0.5f, 0.8f, Midi(84), Midi(84), 0.4f, 0.01f, 0.35f, EPiano);
                    break;
                case Sfx.ElevatorDing:
                    b = Buffer(1.6f);
                    Tone(b, 0f, 1.6f, Midi(88), Midi(88), 0.5f, 0.002f, 0.5f, EPiano);
                    Tone(b, 0.35f, 1.25f, Midi(84), Midi(84), 0.5f, 0.002f, 0.6f, EPiano);
                    break;
                case Sfx.UiMove:
                    b = Buffer(0.06f);
                    Tone(b, 0f, 0.06f, 1400f, 1400f, 0.4f, 0.001f, 0.015f, Square);
                    break;
                case Sfx.UiConfirm:
                    b = Buffer(0.3f);
                    Tone(b, 0f, 0.12f, Midi(72), Midi(72), 0.4f, 0.002f, 0.05f, Square);
                    Tone(b, 0.09f, 0.2f, Midi(79), Midi(79), 0.4f, 0.002f, 0.08f, Square);
                    break;
                case Sfx.ComboUp:
                    b = Buffer(0.25f);
                    Tone(b, 0f, 0.25f, 600f, 1500f, 0.4f, 0.005f, 0.09f, Triangle);
                    break;
                case Sfx.BossIntro:
                    b = Buffer(1.8f);
                    Tone(b, 0f, 1.8f, 55f, 52f, 1f, 0.02f, 0.7f, Saw);
                    Tone(b, 0f, 1.8f, 82.4f, 78f, 0.7f, 0.02f, 0.7f, Saw);
                    Noise(b, rng, 0f, 1.2f, 0.6f, 0.002f, 0.25f, 900f, 100f);
                    break;
                case Sfx.GameOver:
                    b = Buffer(1.8f);
                    int[] sad = { 67, 66, 65, 64 };
                    for (int i = 0; i < sad.Length; i++)
                        Tone(b, i * 0.35f, i == 3 ? 1.1f : 0.4f, Midi(sad[i]), Midi(sad[i]) * (i == 3 ? 0.97f : 1f), 0.45f, 0.01f, i == 3 ? 0.45f : 0.15f, Square);
                    break;
                case Sfx.Footstep: // soft carpet scuff
                    b = Buffer(0.12f);
                    Noise(b, rng, 0f, 0.12f, 0.7f, 0.004f, 0.03f, 1400f, 500f);
                    Tone(b, 0f, 0.08f, 95f, 60f, 0.5f, 0.002f, 0.025f, Sine);
                    break;
                case Sfx.DoorSlide: // elevator doors: a rumbling slide that thuds shut
                    b = Buffer(0.7f);
                    Noise(b, rng, 0f, 0.55f, 0.6f, 0.08f, 0.25f, 500f, 1200f);
                    Tone(b, 0f, 0.55f, 70f, 85f, 0.35f, 0.05f, 0.3f, Triangle);
                    Noise(b, rng, 0.5f, 0.2f, 1f, 0.002f, 0.05f, 900f, 200f);
                    Tone(b, 0.5f, 0.2f, 80f, 45f, 0.8f, 0.002f, 0.07f, Sine);
                    break;
                case Sfx.Heartbeat: // lub-dub
                    b = Buffer(0.55f);
                    Tone(b, 0f, 0.14f, 60f, 42f, 1f, 0.004f, 0.05f, Sine);
                    Tone(b, 0.2f, 0.14f, 52f, 38f, 0.8f, 0.004f, 0.05f, Sine);
                    break;
                case Sfx.UiBack:
                    b = Buffer(0.18f);
                    Tone(b, 0f, 0.09f, Midi(76), Midi(76), 0.35f, 0.002f, 0.04f, Square);
                    Tone(b, 0.07f, 0.11f, Midi(69), Midi(69), 0.35f, 0.002f, 0.05f, Square);
                    break;
                case Sfx.ElevatorHum: // the ride between floors
                    b = Buffer(1.1f);
                    Tone(b, 0f, 1.1f, 58f, 66f, 0.6f, 0.15f, 0.8f, Triangle);
                    Noise(b, rng, 0f, 1.1f, 0.25f, 0.2f, 0.7f, 300f, 450f);
                    break;
                default:
                    b = Buffer(0.05f);
                    break;
            }
            return ToClip(sfx.ToString(), b);
        }

        /// <summary>A long, quiet office room-tone loop: HVAC, scattered typing, a distant phone.</summary>
        public static AudioClip BuildAmbience()
        {
            var rng = new System.Random(99);
            const float length = 16f;
            var b = Buffer(length);
            // HVAC: steady low-passed noise plus a faint mains hum.
            Noise(b, rng, 0f, length, 0.35f, 0.5f, 1000f, 380f, 420f);
            Tone(b, 0f, length, 60f, 60f, 0.05f, 0.5f, 1000f, Sine);
            // Keyboard bursts: little clusters of clicks.
            for (float t = 0.6f; t < length - 1f; t += 1.2f + (float)rng.NextDouble() * 2.4f)
            {
                int keys = 4 + rng.Next(10);
                for (int k = 0; k < keys; k++)
                    Noise(b, rng, t + k * (0.07f + (float)rng.NextDouble() * 0.06f), 0.03f, 0.18f, 0.001f, 0.006f, 6000f, 3000f);
            }
            // A distant desk phone, twice in the loop.
            foreach (float t in new[] { 4.3f, 11.7f })
                for (int ring = 0; ring < 2; ring++)
                {
                    Tone(b, t + ring * 0.45f, 0.35f, 1300f, 1300f, 0.04f, 0.01f, 0.3f, Square);
                    Tone(b, t + ring * 0.45f, 0.35f, 1650f, 1650f, 0.03f, 0.01f, 0.3f, Square);
                }
            // Fade the ends together so the loop seam is invisible.
            int fade = SampleRate / 2;
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                b[i] *= k;
                b[b.Length - 1 - i] *= k;
            }
            return ToClip("Office_Ambience", b, normalize: true, peak: 0.6f, fadeOut: false);
        }

        // ------------------------------------------------------------------ music

        /// <summary>One looping music track: chord changes, walking bass, brushed hats.</summary>
        public struct Track
        {
            public string name;
            public float bpm;
            /// <summary>Each chord as MIDI notes; first note is the bass root. One chord per bar.</summary>
            public int[][] chords;
            public bool driving;
        }

        public static readonly Track Lounge = new Track
        {
            name = "Muzak_Lounge",
            bpm = 104f,
            // Fmaj7 - Dm7 - Gm7 - C7, twice with a turnaround
            chords = new[]
            {
                new[] { 41, 64, 69, 72, 76 }, new[] { 38, 65, 69, 72, 74 },
                new[] { 43, 65, 70, 74, 77 }, new[] { 36, 64, 67, 70, 74 },
                new[] { 41, 64, 69, 72, 76 }, new[] { 38, 65, 69, 72, 74 },
                new[] { 43, 65, 70, 74, 77 }, new[] { 36, 64, 70, 72, 76 },
            },
        };

        public static readonly Track Fight = new Track
        {
            name = "Muzak_Overtime",
            bpm = 128f,
            driving = true,
            // Am - F - G - Em, with a tense E7 at the end
            chords = new[]
            {
                new[] { 45, 64, 69, 72 }, new[] { 41, 65, 69, 72 },
                new[] { 43, 67, 71, 74 }, new[] { 40, 64, 67, 71 },
                new[] { 45, 64, 69, 72 }, new[] { 41, 65, 69, 72 },
                new[] { 43, 67, 71, 74 }, new[] { 40, 64, 68, 71 },
            },
        };

        public static readonly Track Boss = new Track
        {
            name = "Muzak_Boardroom",
            bpm = 146f,
            driving = true,
            // Dm - Bb - Gm - A7: the CEO's theme
            chords = new[]
            {
                new[] { 38, 62, 65, 69 }, new[] { 34, 62, 65, 70 },
                new[] { 43, 62, 67, 70 }, new[] { 45, 61, 64, 67 },
                new[] { 38, 62, 65, 69 }, new[] { 34, 62, 65, 70 },
                new[] { 43, 62, 67, 70 }, new[] { 45, 61, 64, 69 },
            },
        };

        public static AudioClip BuildMusic(Track track)
        {
            var rng = new System.Random(track.name.GetHashCode());
            float beat = 60f / track.bpm;
            float bar = beat * 4f;
            var b = Buffer(bar * track.chords.Length);

            for (int c = 0; c < track.chords.Length; c++)
            {
                var chord = track.chords[c];
                float barStart = c * bar;
                int root = chord[0];
                var next = track.chords[(c + 1) % track.chords.Length];

                // Bass: walking quarters (lounge) or pumping eighths (fight).
                if (track.driving)
                {
                    for (int e = 0; e < 8; e++)
                    {
                        int note = root + (e == 7 ? 7 : e % 4 == 3 ? 12 : 0);
                        Tone(b, barStart + e * beat * 0.5f, beat * 0.48f, Midi(note), Midi(note), 0.32f, 0.004f, 0.12f, Triangle);
                    }
                }
                else
                {
                    int[] walk = { 0, 4, 7, next[0] - root - 1 };
                    for (int q = 0; q < 4; q++)
                    {
                        int note = root + walk[q];
                        Tone(b, barStart + q * beat, beat * 0.95f, Midi(note), Midi(note), 0.38f, 0.006f, 0.3f, Triangle);
                    }
                }

                // Chords: soft keys, comped on the off-beats.
                float[] hits = track.driving ? new[] { 0f, 1.5f, 2.5f, 3.5f } : new[] { 0f, 1.5f, 2.5f };
                foreach (float h in hits)
                    for (int n = 1; n < chord.Length; n++)
                        Tone(b, barStart + h * beat, beat * 1.4f, Midi(chord[n]), Midi(chord[n]), 0.09f, 0.008f, track.driving ? 0.18f : 0.4f, EPiano);

                // Melody: a lazy pentatonic doodle over each bar, seeded so it's the same every loop.
                int steps = track.driving ? 8 : 4;
                for (int s = 0; s < steps; s++)
                {
                    if (rng.NextDouble() < (track.driving ? 0.35 : 0.3)) continue;
                    int note = chord[1 + rng.Next(chord.Length - 1)] + 12;
                    float len = beat * (track.driving ? 0.5f : 1f);
                    Tone(b, barStart + s * len, len * 0.9f, Midi(note), Midi(note), 0.1f, 0.01f, len * 0.6f, track.driving ? Square : Sine);
                }

                // Drums: kick + brushed hats.
                for (int q = 0; q < 4; q++)
                {
                    float t = barStart + q * beat;
                    if (track.driving || q % 2 == 0)
                        Tone(b, t, 0.16f, 120f, 45f, track.driving ? 0.5f : 0.3f, 0.002f, 0.06f, Sine);
                    if (track.driving && q % 2 == 1)
                        Noise(b, rng, t, 0.15f, 0.22f, 0.001f, 0.05f, 5000f, 2000f);
                    Noise(b, rng, t + beat * 0.5f, 0.06f, 0.08f, 0.001f, 0.015f, 9000f, 9000f);
                    if (!track.driving) Noise(b, rng, t + beat * 0.66f, 0.05f, 0.05f, 0.001f, 0.01f, 9000f, 9000f);
                }
            }

            // Soft-clip so overlapping voices stay warm instead of harsh.
            for (int i = 0; i < b.Length; i++) b[i] = (float)Math.Tanh(b[i] * 1.2f);
            return ToClip(track.name, b, normalize: true, peak: 0.8f, fadeOut: false);
        }
    }
}
