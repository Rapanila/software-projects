using System.Collections.Generic;
using NineToFight.Game;
using NineToFight.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NineToFight.UI
{
    /// <summary>
    /// Builds the whole UI in code in the Synergex house style (see UiKit): the employee-badge HUD,
    /// keycap hotbar, notifications and banners, the title/options/pause menus, department select,
    /// the elevator ride between floors, boss intros, and the termination letter.
    /// Split across partial files: this one holds the API, the update loop and the builder helpers.
    /// </summary>
    public partial class GameHud : MonoBehaviour
    {
        PlayerController player;
        GameFlow flow;
        RectTransform root;

        // Screens fade and settle in when they appear.
        class ScreenFade { public GameObject go; public CanvasGroup group; public float shownAt; public bool wasActive; }
        readonly List<ScreenFade> screens = new List<ScreenFade>();

        GameObject hudRoot, titleScreen, classScreen, gameOverScreen, endingScreen, pauseScreen, optionsScreen;

        /// <summary>Title menu: Clock in / Options / Quit.</summary>
        public MenuList TitleMenu { get; private set; }
        /// <summary>Pause menu: Back to work / Options / Quit to lobby.</summary>
        public MenuList PauseMenu { get; private set; }
        /// <summary>Options rows (one per setting, plus Back).</summary>
        public MenuList OptionsMenu { get; private set; }

        void Awake()
        {
            EnsureEventSystem();
            Build();
        }

        static void EnsureEventSystem()
        {
            if (EventSystem.current != null || FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            go.transform.SetParent(null);
        }

        public void Bind(PlayerController target, GameFlow gameFlow)
        {
            player = target;
            flow = gameFlow;
            player.Progression.Changed += RefreshMoves;
            player.Progression.MoveLearned += _ => RefreshMoves();
            player.Health.Damaged += _ => hurtFlash = 1f;
            RefreshFloor();
            RefreshMoves();
            SetupPortrait();
        }

        public void ShowScreen(GameState state)
        {
            bool overlay = state == GameState.Paused || state == GameState.Options;
            pauseScreen.SetActive(state == GameState.Paused);
            optionsScreen.SetActive(state == GameState.Options);
            if (!overlay) // pause and options sit over whatever was showing
            {
                titleScreen.SetActive(state == GameState.Title);
                classScreen.SetActive(state == GameState.ClassSelect);
                gameOverScreen.SetActive(state == GameState.GameOver);
                endingScreen.SetActive(state == GameState.Ending);
                hudRoot.SetActive(state == GameState.Playing || state == GameState.FloorCleared || state == GameState.GameOver);
            }
            if (state == GameState.Options) RefreshOptions();
            float now = Time.unscaledTime;
            foreach (var f in screens)
            {
                bool active = f.go.activeSelf;
                if (active && !f.wasActive)
                {
                    f.shownAt = now;
                    f.group.alpha = 0f;
                }
                f.wasActive = active;
            }
        }

        public static string DurationString(float seconds)
        {
            int s = Mathf.FloorToInt(seconds);
            return $"{s / 60}m {s % 60:00}s";
        }

        public static string ClockString(float elapsedSeconds)
        {
            // One real second is one office minute, starting at 9:00 AM.
            int minutes = 9 * 60 + Mathf.FloorToInt(elapsedSeconds);
            int hour24 = (minutes / 60) % 24;
            int hour12 = hour24 % 12 == 0 ? 12 : hour24 % 12;
            return $"{hour12}:{minutes % 60:00} {(hour24 < 12 ? "AM" : "PM")}";
        }

        void Update()
        {
            float now = Time.unscaledTime;
            if (player != null && hudRoot.activeSelf) UpdateHud(now);
            UpdateMessages(now);
            UpdateMenus();
            UpdateScreenEffects(now);
            AnimateScreens(now);
        }

        void AnimateScreens(float now)
        {
            foreach (var f in screens)
            {
                if (!f.go.activeSelf) continue;
                float t = Mathf.Clamp01((now - f.shownAt) / 0.3f);
                float e = 1f - (1f - t) * (1f - t);
                f.group.alpha = e;
                f.go.transform.localScale = Vector3.one * Mathf.Lerp(1.03f, 1f, e);
            }
        }

        static float Glide(float from, float to, float rate) =>
            Mathf.Lerp(from, to, 1f - Mathf.Exp(-rate * Mathf.Min(Time.unscaledDeltaTime, 0.05f)));

        /// <summary>Overshoots slightly past 1 and settles: things land instead of appearing.</summary>
        static float EaseOutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float c = 1.70158f;
            float u = t - 1f;
            return 1f + (c + 1f) * u * u * u + c * u * u;
        }

        static float EaseInOut(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        // ---------------------------------------------------------------- construction

        void Build()
        {
            var canvasGo = new GameObject("HUD Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            root = (RectTransform)canvasGo.transform;

            BuildScreenEffects(root);
            BuildHud(root);
            BuildTitle(root);
            BuildClassSelect(root);
            BuildGameOver(root);
            BuildEnding(root);
            BuildCinematics(root);
            BuildMessages(root);
            BuildPause(root);
            BuildOptions(root);
            BuildElevator(root);
        }

        // ---------------------------------------------------------------- helpers

        static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static Image Panel(string name, Transform parent, Color color)
        {
            var image = Rect(name, parent).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Rounded panel. Radius is in canvas units.</summary>
        static Image Card(string name, Transform parent, Color color, float radius = 14f)
        {
            var image = Panel(name, parent, color);
            image.sprite = UiKit.Rounded;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 33f / Mathf.Max(radius, 1f);
            return image;
        }

        /// <summary>Soft shadow behind a card: same rect, pushed down a little and spread out.</summary>
        static Image DropShadow(RectTransform card, float strength = 0.45f, float drop = 8f)
        {
            var shadow = Panel(card.name + " Shadow", card.parent, new Color(0f, 0f, 0f, strength));
            shadow.sprite = UiKit.Shadow;
            shadow.type = Image.Type.Sliced;
            var r = shadow.rectTransform;
            r.anchorMin = card.anchorMin;
            r.anchorMax = card.anchorMax;
            r.pivot = card.pivot;
            r.offsetMin = card.offsetMin + new Vector2(-30f, -30f - drop);
            r.offsetMax = card.offsetMax + new Vector2(30f, 30f - drop);
            r.SetSiblingIndex(card.GetSiblingIndex());
            return shadow;
        }

        RectTransform FullScreen(string name, RectTransform parent, Color color)
        {
            var rect = Panel(name, parent, color).rectTransform;
            Stretch(rect, 0f);
            screens.Add(new ScreenFade { go = rect.gameObject, group = rect.gameObject.AddComponent<CanvasGroup>() });
            return rect;
        }

        enum Face { Display, Heading, Body, Strong, Italic }

        static Font FontFor(Face face)
        {
            switch (face)
            {
                case Face.Display: return UiKit.Display;
                case Face.Heading: return UiKit.Heading;
                case Face.Strong: return UiKit.BodyStrong;
                case Face.Italic: return UiKit.BodyItalic;
                default: return UiKit.Body;
            }
        }

        static Text Label(string name, Transform parent, string text, int size, TextAnchor anchor, Color color, Face face = Face.Body)
        {
            var label = Rect(name, parent).gameObject.AddComponent<Text>();
            label.font = FontFor(face);
            label.text = text;
            label.fontSize = size;
            label.alignment = anchor;
            label.color = color;
            label.supportRichText = true;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            // Condensed display faces read better a touch tighter; body copy a touch looser.
            label.lineSpacing = face == Face.Display || face == Face.Heading ? 0.9f : 1.1f;
            return label;
        }

        static void Shadowed(Graphic g, float distance = 3f, float alpha = 0.55f)
        {
            var shadow = g.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, alpha);
            shadow.effectDistance = new Vector2(distance, -distance);
        }

        static void Stretch(RectTransform rect, float inset) =>
            Place(rect, Vector2.zero, Vector2.one, new Vector2(inset, inset), new Vector2(-inset, -inset));

        static void Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        /// <summary>Fixed-size rect around an anchor point.</summary>
        static void PlaceAt(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        /// <summary>A small keyboard keycap with a legend, used in control prompts.</summary>
        static RectTransform KeyGlyph(Transform parent, string key, float height = 38f)
        {
            var cap = Panel("Key " + key, parent, UiKit.Paper);
            cap.sprite = UiKit.Keycap;
            cap.type = Image.Type.Sliced;
            cap.pixelsPerUnitMultiplier = 2.2f;
            float width = Mathf.Max(height, 18f + key.Length * height * 0.36f);
            var le = cap.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = height;
            var legend = Label("Legend", cap.rectTransform, key, Mathf.RoundToInt(height * 0.55f), TextAnchor.MiddleCenter, UiKit.Ink, Face.Heading);
            Place(legend.rectTransform, Vector2.zero, Vector2.one, new Vector2(2f, height * 0.12f), new Vector2(-2f, 0f));
            return cap.rectTransform;
        }

        /// <summary>
        /// A row of control prompts: "A D|Choose;Enter|Clock in". Keys are drawn as keycaps, actions in plain text.
        /// Keys separated by spaces inside one prompt become separate caps.
        /// </summary>
        static RectTransform PromptBar(Transform parent, string spec, Color textColor, TextAnchor align = TextAnchor.MiddleCenter, float keyHeight = 38f)
        {
            var bar = Rect("Prompts", parent);
            var layout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = align;
            layout.spacing = 10f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            foreach (var part in spec.Split(';'))
            {
                var bits = part.Split('|');
                if (bits.Length != 2) continue;
                foreach (var key in bits[0].Split(' '))
                    if (key.Length > 0) KeyGlyph(bar, key, keyHeight);
                var text = Label("Action", bar, bits[1], Mathf.RoundToInt(keyHeight * 0.58f), TextAnchor.MiddleLeft, textColor, Face.Strong);
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                var le = text.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = text.preferredWidth + 26f;
                le.preferredHeight = keyHeight;
            }
            return bar;
        }

        static void SetFill(RectTransform fill, float amount) =>
            fill.anchorMax = new Vector2(Mathf.Clamp01(amount), fill.anchorMax.y);
    }

    /// <summary>
    /// A vertical list of choices drawn with the highlighter-swipe selection. Driven by the keyboard or
    /// gamepad from GameFlow (Move/Choose) or by the mouse (hover selects, click chooses).
    /// </summary>
    public class MenuList
    {
        internal class Row
        {
            public RectTransform rect;
            public Text label, value;
            public Image swipe, bar, barFill;
            public float sweep;
        }

        internal readonly List<Row> rows = new List<Row>();
        public int Selected { get; private set; }
        public int Count => rows.Count;

        public event System.Action<int> Chosen;
        public event System.Action<int> SelectionChanged;

        public void Move(int up)
        {
            if (up == 0 || rows.Count == 0) return;
            Select((Selected - up + rows.Count) % rows.Count);
        }

        public void Select(int index)
        {
            index = Mathf.Clamp(index, 0, Mathf.Max(0, rows.Count - 1));
            if (index == Selected) return;
            Selected = index;
            rows[index].sweep = 0f; // re-draw the stroke from the left
            SelectionChanged?.Invoke(index);
        }

        public void Choose() => Chosen?.Invoke(Selected);

        public void Reset(int index = 0)
        {
            Selected = Mathf.Clamp(index, 0, Mathf.Max(0, rows.Count - 1));
            foreach (var r in rows) r.sweep = 0f;
        }
    }
}
