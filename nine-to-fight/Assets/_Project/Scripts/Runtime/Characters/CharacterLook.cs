using System;
using UnityEngine;

namespace NineToFight.Characters
{
    /// <summary>
    /// Per-person variety on the shared office-worker rig: body (each with its own skeleton), head
    /// and hairstyle, outfit (suits for management), skin tone and hair colour. Picked
    /// deterministically from a seed so the same coworker always looks the same.
    /// </summary>
    [RequireComponent(typeof(CharacterVisual))]
    public class CharacterLook : MonoBehaviour
    {
        static readonly Color[] SkinTones =
        {
            new Color32(0xF1, 0xC2, 0x9C, 0xFF), new Color32(0xE0, 0xAC, 0x69, 0xFF), new Color32(0xC6, 0x86, 0x42, 0xFF),
            new Color32(0x8D, 0x5A, 0x34, 0xFF), new Color32(0x5C, 0x3A, 0x22, 0xFF), new Color32(0xF6, 0xD5, 0xB8, 0xFF),
        };

        static readonly Color[] HairColors =
        {
            new Color32(0x1E, 0x1B, 0x1F, 0xFF), new Color32(0x3B, 0x2A, 0x20, 0xFF), new Color32(0x6B, 0x45, 0x2B, 0xFF),
            new Color32(0xA8, 0x55, 0x2E, 0xFF), new Color32(0xD9, 0xB3, 0x6C, 0xFF), new Color32(0x9C, 0x9C, 0xA3, 0xFF),
        };

        [Serializable]
        public class Outfit
        {
            public string name;
            [Tooltip("Suits, worn by management.")]
            public bool formal;
            public GameObject root;
        }

        [Serializable]
        public class Figure
        {
            public string name;
            [Tooltip("Mutually exclusive heads (face and hairstyle).")]
            public GameObject[] heads;
            public Outfit[] outfits;
        }

        [Tooltip("One per RigFollower rig, in the same order.")]
        [SerializeField] Figure[] figures;
        [SerializeField] Renderer[] skinRenderers;
        [Tooltip("Hair and brows, dyed together.")]
        [SerializeField] Renderer[] hairRenderers;

        CharacterVisual visual;
        RigFollower follower;

        /// <summary>Which body, head and outfit were picked last.</summary>
        public int FigureIndex { get; private set; }
        public int HeadIndex { get; private set; }
        public string OutfitName { get; private set; }

#if UNITY_EDITOR
        /// <summary>Content builder hook.</summary>
        public void Configure(Figure[] figures, Renderer[] skinRenderers, Renderer[] hairRenderers)
        {
            this.figures = figures;
            this.skinRenderers = skinRenderers;
            this.hairRenderers = hairRenderers;
        }
#endif

        /// <summary>Stable seed from a name, so a class or worker title always gets the same face.</summary>
        public static int Seed(string text)
        {
            unchecked
            {
                int h = 23;
                foreach (char c in text ?? "") h = h * 31 + c;
                return h;
            }
        }

        /// <summary>Dresses this person. <paramref name="outfit"/> forces a specific outfit (and so body) by name.</summary>
        public void Apply(int seed, bool suit, string outfit = null)
        {
            if (visual == null) visual = GetComponent<CharacterVisual>();
            if (follower == null) follower = GetComponent<RigFollower>();
            var rng = new System.Random(seed);

            visual.Recolor(skinRenderers, SkinTones[rng.Next(SkinTones.Length)]);
            visual.Recolor(hairRenderers, HairColors[rng.Next(HairColors.Length)]);
            if (figures == null || figures.Length == 0) return;

            FigureIndex = rng.Next(figures.Length);
            int forced = -1;
            if (!string.IsNullOrEmpty(outfit))
                for (int f = 0; f < figures.Length && forced < 0; f++)
                    for (int o = 0; o < figures[f].outfits.Length && forced < 0; o++)
                        if (figures[f].outfits[o].name == outfit)
                        {
                            FigureIndex = f;
                            forced = o;
                        }
            if (follower != null) follower.Select(FigureIndex);
            var figure = figures[FigureIndex];

            HeadIndex = figure.heads.Length > 0 ? rng.Next(figure.heads.Length) : -1;
            for (int i = 0; i < figure.heads.Length; i++) figure.heads[i].SetActive(i == HeadIndex);

            // Suits for management, office casual for everyone else.
            int matching = 0;
            foreach (var o in figure.outfits) if (o.formal == suit) matching++;
            int pick = matching > 0 ? rng.Next(matching) : rng.Next(figure.outfits.Length);
            for (int i = 0, n = 0; i < figure.outfits.Length; i++)
            {
                var candidate = figure.outfits[i];
                bool on = forced >= 0 ? i == forced : (matching == 0 || candidate.formal == suit) && n++ == pick;
                candidate.root.SetActive(on);
                if (on) OutfitName = candidate.name;
            }
            visual.RefreshColors();
        }

        /// <summary>Every outfit name, across all bodies.</summary>
        public System.Collections.Generic.IEnumerable<string> OutfitNames
        {
            get
            {
                if (figures == null) yield break;
                foreach (var f in figures)
                    foreach (var o in f.outfits)
                        yield return o.name;
            }
        }
    }
}
