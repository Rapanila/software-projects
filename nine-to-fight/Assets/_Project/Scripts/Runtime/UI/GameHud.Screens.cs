using System.Collections;
using System.Collections.Generic;
using NineToFight.Audio;
using NineToFight.Game;
using NineToFight.Player;
using NineToFight.Progression;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NineToFight.UI
{
    /// <summary>Full-screen moments: title, department select, options, pause, termination letter, elevator.</summary>
    public partial class GameHud
    {
        class MenuView
        {
            public MenuList list;
            public GameObject screen;
            public Color normal, selected;
        }
        readonly List<MenuView> menuViews = new List<MenuView>();

        Text bestText, gameOverBody, endingTitle, endingBody, endingPromptText;
        RectTransform endingPrompts;

        // Department select
        RectTransform classSheet, classTabs;
        PlayerClass[] classes;
        readonly List<MenuList.Row> classTabRows = new List<MenuList.Row>();
        int highlightedClass = -1;

        // Benefits (perks) offered at the elevator
        RectTransform perkRow;
        class PerkCard { public RectTransform rect; public Image tick, stroke, shadow; public float sweep; }
        readonly List<PerkCard> perkCards = new List<PerkCard>();
        int highlightedPerk;

        // Elevator ride
        GameObject elevatorRoot;
        RectTransform doorLeft, doorRight;
        Text elevatorNumber;
        CanvasGroup elevatorGroup;

        // Boss intro
        RectTransform letterTop, letterBottom, bossCard;
        Text bossCardName, bossCardTitle;
        CanvasGroup bossCardGroup;
        float letterbox, cinematicUntil;

        static readonly Color Amber = UiKit.Hex("FFB347");

        // ================================================================ public API

        public void SetEndingText(string title, string body, string prompt)
        {
            endingTitle.text = title;
            endingBody.text = body;
            foreach (Transform child in endingPrompts) Destroy(child.gameObject);
            Stretch(PromptBar(endingPrompts, prompt, UiKit.Paper), 0f);
        }

        public void SetGameOverText(string text) => gameOverBody.text = text;

        /// <summary>Benefit forms on the elevator screen (null clears them).</summary>
        public void ShowPerkOffer(IReadOnlyList<Perk> offer, PerkSet owned = null)
        {
            foreach (Transform child in perkRow) Destroy(child.gameObject);
            perkCards.Clear();
            bool any = offer != null && offer.Count > 0;
            Place(endingTitle.rectTransform, new Vector2(0f, any ? 0.74f : 0.6f), new Vector2(1f, any ? 0.9f : 0.78f), Vector2.zero, Vector2.zero);
            Place(endingBody.rectTransform, new Vector2(0.15f, any ? 0.5f : 0.3f), new Vector2(0.85f, any ? 0.74f : 0.6f), Vector2.zero, Vector2.zero);
            if (!any) return;

            const float width = 460f, height = 250f, gap = 40f;
            float total = offer.Count * width + (offer.Count - 1) * gap;
            for (int i = 0; i < offer.Count; i++)
            {
                var perk = offer[i];
                var holder = Rect("Benefit " + perk.name, perkRow);
                PlaceAt(holder, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-total / 2f + width / 2f + i * (width + gap), 0f), new Vector2(width, height));
                var card = Card("Form", holder, UiKit.Paper, 12f).rectTransform;
                Stretch(card, 0f);
                var shadow = DropShadow(card, 0.5f, 10f);

                var band = Card("Band", card, perk.color, 12f).rectTransform;
                Place(band, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -14f), Vector2.zero);
                var bandBase = Panel("Band Base", band, perk.color).rectTransform;
                Place(bandBase, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 7f));

                // A checkbox that gets ticked on the chosen form.
                var box = Card("Checkbox", card, UiKit.Ink, 5f).rectTransform;
                PlaceAt(box, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, -40f), new Vector2(34f, 34f));
                var boxInner = Card("Inner", box, UiKit.Paper, 4f);
                Stretch(boxInner.rectTransform, 3f);
                var tick = Rect("Tick", box).gameObject.AddComponent<Image>();
                tick.color = new Color(0, 0, 0, 0);
                tick.raycastTarget = false;
                Stretch(tick.rectTransform, 0f);
                var tickShort = Panel("Short", tick.rectTransform, UiKit.Ink).rectTransform;
                PlaceAt(tickShort, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-6f, -3f), new Vector2(6f, 16f));
                tickShort.localRotation = Quaternion.Euler(0f, 0f, 40f);
                var tickLong = Panel("Long", tick.rectTransform, UiKit.Ink).rectTransform;
                PlaceAt(tickLong, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(4f, 2f), new Vector2(6f, 30f));
                tickLong.localRotation = Quaternion.Euler(0f, 0f, -38f);

                var stroke = Panel("Stroke", card, UiKit.Highlighter);
                stroke.sprite = UiKit.HighlighterStroke;
                stroke.type = Image.Type.Filled;
                stroke.fillMethod = Image.FillMethod.Horizontal;
                Place(stroke.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(72f, -76f), new Vector2(-30f, -22f));
                var name = Label("Name", card, perk.name, 42, TextAnchor.MiddleLeft, UiKit.Ink, Face.Display);
                Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(76f, -76f), new Vector2(-24f, -22f));
                var desc = Label("Description", card, perk.description, 21, TextAnchor.UpperLeft, UiKit.InkSoft, Face.Body);
                Place(desc.rectTransform, Vector2.zero, Vector2.one, new Vector2(28f, 50f), new Vector2(-28f, -92f));
                int have = owned != null ? owned.Stacks(perk.id) : 0;
                var tag = Label("Owned", card, have > 0 ? $"You have {have}. Take another?" : "New benefit", 17, TextAnchor.LowerLeft,
                    have > 0 ? UiKit.Brass : UiKit.Teal, Face.Italic);
                Place(tag.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(28f, 18f), new Vector2(-28f, 48f));

                int index = i;
                AddPointer(holder.gameObject, () => { flow?.SelectPerk(index); }, () => { flow?.SelectPerk(index); flow?.ConfirmEnding(); });
                perkCards.Add(new PerkCard { rect = holder, tick = tick, stroke = stroke, shadow = shadow });
            }
            HighlightPerk(0);
        }

        public void HighlightPerk(int index)
        {
            highlightedPerk = index;
            for (int i = 0; i < perkCards.Count; i++)
                if (i == index) perkCards[i].sweep = 0f;
        }

        // ---------------------------------------------------------------- department select

        public void BuildClassCards(PlayerClass[] all)
        {
            classes = all;
            foreach (Transform child in classTabs) Destroy(child.gameObject);
            classTabRows.Clear();
            highlightedClass = -1;
            for (int i = 0; i < all.Length; i++)
            {
                var row = new MenuList.Row { rect = Rect("Tab " + all[i].displayName, classTabs) };
                var le = row.rect.gameObject.AddComponent<LayoutElement>();
                var swipe = Panel("Swipe", row.rect, UiKit.Highlighter);
                swipe.sprite = UiKit.HighlighterStroke;
                swipe.type = Image.Type.Filled;
                swipe.fillMethod = Image.FillMethod.Horizontal;
                Stretch(swipe.rectTransform, 0f);
                row.swipe = swipe;
                row.label = Label("Label", row.rect, all[i].displayName, 40, TextAnchor.MiddleCenter, UiKit.Paper, Face.Display);
                Stretch(row.label.rectTransform, 0f);
                le.preferredWidth = row.label.preferredWidth + 56f;
                le.preferredHeight = 64f;
                int index = i;
                AddPointer(row.rect.gameObject, () => flow?.SelectClass(index), () => { flow?.SelectClass(index); flow?.ConfirmClass(); });
                classTabRows.Add(row);
            }
        }

        public void HighlightClass(int index)
        {
            if (classes == null || index < 0 || index >= classes.Length) return;
            if (index != highlightedClass && index < classTabRows.Count) classTabRows[index].sweep = 0f;
            highlightedClass = index;
            BuildClassSheet(classes[index]);
        }

        void BuildClassSheet(PlayerClass c)
        {
            foreach (Transform child in classSheet) Destroy(child.gameObject);
            var card = Card("Sheet", classSheet, UiKit.Paper, 16f).rectTransform;
            Stretch(card, 0f);
            DropShadow(card, 0.55f, 12f);

            // Department colours as the sheet's header band: shirt with a tie stripe.
            var band = Card("Band", card, c.shirtColor, 16f).rectTransform;
            Place(band, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -20f), Vector2.zero);
            var bandBase = Panel("Band Base", band, c.shirtColor).rectTransform;
            Place(bandBase, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 10f));
            var tieStripe = Panel("Tie", band, c.tieColor).rectTransform;
            PlaceAt(tieStripe, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(46f, 0f), new Vector2(18f, 20f));

            var dept = Label("Department", card, $"{c.department} department", 20, TextAnchor.UpperLeft, UiKit.InkSoft, Face.Strong);
            Place(dept.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(40f, -64f), new Vector2(-40f, -36f));
            var name = Label("Name", card, c.displayName, 76, TextAnchor.UpperLeft, UiKit.Ink, Face.Display);
            Place(name.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(38f, -142f), new Vector2(-40f, -58f));
            var tagline = Label("Tagline", card, c.tagline, 21, TextAnchor.UpperLeft, UiKit.InkSoft, Face.Italic);
            Place(tagline.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(40f, -196f), new Vector2(-40f, -144f));

            float y = -206f;
            StatRow(card, "Health", c.maxHealth / 130f, ref y);
            StatRow(card, "Speed", c.moveSpeed / 7.5f, ref y);
            StatRow(card, "Power", c.damageMultiplier / 1.25f, ref y);
            StatRow(card, "Agility", 0.25f / Mathf.Max(0.1f, c.dodgeCooldown), ref y);

            y -= 10f;
            var movesHeading = Label("Moves", card, "Moves you'll learn on the way up", 22, TextAnchor.UpperLeft, UiKit.Ink, Face.Heading);
            Place(movesHeading.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(40f, y - 30f), new Vector2(-40f, y));
            y -= 38f;

            var moves = c.BuildMoveSet();
            for (int m = 0; m < moves.Count && m < ProgressionState.HotbarSize; m++)
            {
                int col = m / 5, row = m % 5;
                var line = Rect("Move", card);
                PlaceAt(line, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f + col * 340f, y - row * 36f), new Vector2(330f, 32f));
                var layout = line.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = 10f;
                layout.childAlignment = TextAnchor.MiddleLeft;
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = layout.childForceExpandHeight = false;
                KeyGlyph(line, ProgressionState.HotbarKey(m), 30f);
                var move = moves[m];
                string level = move.unlockLevel > 1 ? $"  <color=#8A8F9C>Lv {move.unlockLevel}</color>" : "";
                var text = Label("Name", line, move.displayName + level, 18, TextAnchor.MiddleLeft, UiKit.Ink, Face.Strong);
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.gameObject.AddComponent<LayoutElement>().preferredWidth = 280f;
            }
            y -= 5 * 36f + 8f;

            if (!string.IsNullOrEmpty(c.ability.displayName))
            {
                var abilityLine = Rect("Ability", card);
                PlaceAt(abilityLine, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, y), new Vector2(680f, 34f));
                var layout = abilityLine.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = 10f;
                layout.childAlignment = TextAnchor.MiddleLeft;
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = layout.childForceExpandHeight = false;
                KeyGlyph(abilityLine, "Q", 32f);
                var text = Label("Name", abilityLine,
                    $"<color=#{ColorUtility.ToHtmlStringRGB(c.ability.color * 0.8f)}>{c.ability.displayName}</color>  {c.ability.description}",
                    18, TextAnchor.MiddleLeft, UiKit.Ink, Face.Strong);
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.gameObject.AddComponent<LayoutElement>().preferredWidth = 620f;
            }

            var perk = Label("Perk", card, c.perk, 19, TextAnchor.LowerLeft, UiKit.InkSoft, Face.Italic);
            Place(perk.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(40f, 24f), new Vector2(-40f, 74f));
        }

        void StatRow(RectTransform parent, string label, float amount, ref float y)
        {
            var text = Label(label, parent, label, 19, TextAnchor.MiddleLeft, UiKit.InkSoft, Face.Strong);
            Place(text.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, y - 26f), new Vector2(170f, y));
            // Ten pips, like a rating on an appraisal form.
            int filled = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(amount) * 10f), 1, 10);
            for (int i = 0; i < 10; i++)
            {
                var pip = Card("Pip", parent, i < filled ? UiKit.Navy : UiKit.PaperShade, 4f).rectTransform;
                PlaceAt(pip, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(180f + i * 34f, y - 13f), new Vector2(28f, 14f));
            }
            y -= 32f;
        }

        // ---------------------------------------------------------------- options

        public void RefreshOptions()
        {
            var settings = GameSettings.All;
            for (int i = 0; i < OptionsMenu.rows.Count; i++)
            {
                var row = OptionsMenu.rows[i];
                if (i >= settings.Count)
                {
                    if (row.value != null) row.value.text = "";
                    if (row.bar != null) row.bar.gameObject.SetActive(false);
                    continue;
                }
                var id = settings[i];
                row.value.text = GameSettings.ValueText(id);
                float fraction = GameSettings.Fraction(id);
                row.bar.gameObject.SetActive(fraction >= 0f);
                if (fraction >= 0f) row.barFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
            }
        }

        // ---------------------------------------------------------------- elevator

        /// <summary>
        /// The ride between floors: doors slide shut, the floor counter ticks over, ding, doors open.
        /// <paramref name="atClosed"/> runs while the doors are shut (that's when the floor is swapped).
        /// </summary>
        public IEnumerator ElevatorRide(int from, int to, System.Action atClosed)
        {
            elevatorRoot.SetActive(true);
            elevatorNumber.text = from.ToString();
            GameAudio.Play(Sfx.DoorSlide, 0.9f, 0f);
            for (float t = 0f; t < 0.55f; t += Time.unscaledDeltaTime)
            {
                SetDoors(1f - EaseInOut(t / 0.55f));
                elevatorGroup.alpha = Mathf.Clamp01(t / 0.2f);
                yield return null;
            }
            SetDoors(0f);
            elevatorGroup.alpha = 1f;
            atClosed?.Invoke();

            GameAudio.Play(Sfx.ElevatorHum, 0.8f, 0f);
            yield return new WaitForSecondsRealtime(0.35f);
            for (int f = from + 1; f <= to; f++)
            {
                elevatorNumber.text = f.ToString();
                for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime)
                {
                    elevatorNumber.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.25f, 1f, EaseOutBack(t / 0.3f));
                    yield return null;
                }
            }
            GameAudio.Play(Sfx.ElevatorDing, 0.8f, 0f);
            yield return new WaitForSecondsRealtime(0.4f);

            GameAudio.Play(Sfx.DoorSlide, 0.8f, 0f);
            for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime)
            {
                SetDoors(EaseInOut(t / 0.6f));
                elevatorGroup.alpha = 1f - Mathf.Clamp01((t - 0.4f) / 0.2f);
                yield return null;
            }
            elevatorRoot.SetActive(false);
        }

        void SetDoors(float open)
        {
            float w = root.rect.width / 2f + 6f;
            doorLeft.anchoredPosition = new Vector2(-w * open, 0f);
            doorRight.anchoredPosition = new Vector2(w * open, 0f);
        }

        // ---------------------------------------------------------------- boss intro

        /// <summary>Letterbox bars and a name card while the camera pulls onto a boss.</summary>
        public void ShowBossIntro(string name, string subtitle, float seconds)
        {
            bossCardName.text = name;
            bossCardTitle.text = subtitle;
            cinematicUntil = Time.unscaledTime + seconds;
        }

        void UpdateCinematics(float now)
        {
            bool on = now < cinematicUntil;
            letterbox = Glide(letterbox, on ? 1f : 0f, on ? 6f : 4f);
            letterTop.sizeDelta = new Vector2(0f, 130f * letterbox);
            letterBottom.sizeDelta = new Vector2(0f, 130f * letterbox);
            bossCardGroup.alpha = Glide(bossCardGroup.alpha, on ? 1f : 0f, on ? 5f : 8f);
            hudGroup.alpha = 1f - letterbox * 0.92f;
            bossCard.anchoredPosition = new Vector2(Mathf.Lerp(-120f, 90f, bossCardGroup.alpha), 170f);
        }

        // ---------------------------------------------------------------- menus (per frame)

        void UpdateMenus()
        {
            float dt = Time.unscaledDeltaTime;
            foreach (var view in menuViews)
            {
                if (!view.screen.activeInHierarchy) continue;
                for (int i = 0; i < view.list.rows.Count; i++)
                {
                    var row = view.list.rows[i];
                    bool on = i == view.list.Selected;
                    row.sweep = on ? Mathf.MoveTowards(row.sweep, 1f, dt / 0.18f) : 0f;
                    row.swipe.fillAmount = EaseInOut(row.sweep);
                    row.label.color = on ? view.selected : view.normal;
                    if (row.value != null) row.value.color = on ? view.selected : view.normal;
                }
            }

            for (int i = 0; i < classTabRows.Count; i++)
            {
                var row = classTabRows[i];
                bool on = i == highlightedClass;
                row.sweep = on ? Mathf.MoveTowards(row.sweep, 1f, dt / 0.2f) : 0f;
                row.swipe.fillAmount = EaseInOut(row.sweep);
                row.label.color = on ? UiKit.Ink : UiKit.Paper;
            }

            for (int i = 0; i < perkCards.Count; i++)
            {
                var card = perkCards[i];
                bool on = i == highlightedPerk;
                card.sweep = on ? Mathf.MoveTowards(card.sweep, 1f, dt / 0.22f) : 0f;
                card.stroke.fillAmount = EaseInOut(card.sweep);
                card.tick.gameObject.SetActive(on && card.sweep > 0.6f);
                float scale = Glide(card.rect.localScale.x, on ? 1.05f : 0.96f, 14f);
                card.rect.localScale = Vector3.one * scale;
                card.shadow.color = new Color(0f, 0f, 0f, on ? 0.65f : 0.35f);
            }

            UpdateCinematics(Time.unscaledTime);
        }

        // ---------------------------------------------------------------- construction

        MenuList BuildMenu(RectTransform parent, string[] items, int fontSize, float rowHeight, Color normal, Color selected,
            GameObject screen, TextAnchor align = TextAnchor.MiddleLeft, bool withValues = false)
        {
            var list = new MenuList();
            for (int i = 0; i < items.Length; i++)
            {
                var row = new MenuList.Row { rect = Rect("Item " + items[i], parent) };
                Place(row.rect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -(i + 1) * rowHeight), new Vector2(0f, -i * rowHeight));
                var hit = row.rect.gameObject.AddComponent<Image>();
                hit.color = new Color(0f, 0f, 0f, 0f);
                var swipe = Panel("Swipe", row.rect, UiKit.Highlighter);
                swipe.sprite = UiKit.HighlighterStroke;
                swipe.type = Image.Type.Filled;
                swipe.fillMethod = Image.FillMethod.Horizontal;
                Place(swipe.rectTransform, Vector2.zero, Vector2.one, new Vector2(-18f, 4f), new Vector2(withValues ? 0f : -40f, -4f));
                row.swipe = swipe;
                row.label = Label("Label", row.rect, items[i], fontSize, align, normal, withValues ? Face.Strong : Face.Display);
                Place(row.label.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f, 0f), new Vector2(-4f, 0f));
                if (withValues)
                {
                    row.value = Label("Value", row.rect, "", fontSize + 2, TextAnchor.MiddleRight, normal, Face.Heading);
                    Place(row.value.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f, 0f), new Vector2(-12f, 0f));
                    var bar = Card("Bar", row.rect, UiKit.PaperShade, 5f);
                    PlaceAt(bar.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-120f, 0f), new Vector2(200f, 12f));
                    var fill = Card("Fill", bar.rectTransform, UiKit.Navy, 5f);
                    Place(fill.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                    row.bar = bar;
                    row.barFill = fill;
                }
                int index = i;
                AddPointer(row.rect.gameObject, () => list.Select(index), () => { list.Select(index); list.Choose(); });
                list.rows.Add(row);
            }
            menuViews.Add(new MenuView { list = list, screen = screen, normal = normal, selected = selected });
            return list;
        }

        static void AddPointer(GameObject go, System.Action hover, System.Action click)
        {
            if (go.GetComponent<Graphic>() == null)
            {
                var hit = go.AddComponent<Image>();
                hit.color = new Color(0f, 0f, 0f, 0f);
            }
            var pointer = go.AddComponent<MenuPointer>();
            pointer.hover = hover;
            pointer.click = click;
        }

        void BuildTitle(RectTransform parent)
        {
            // The office itself is the backdrop: the camera drifts through it behind the menu.
            var screen = FullScreen("Title Screen", parent, new Color(0f, 0f, 0f, 0f));
            titleScreen = screen.gameObject;
            var solid = Panel("Solid", screen, UiKit.WithAlpha(UiKit.NavyDeep, 0.55f));
            Place(solid.rectTransform, Vector2.zero, new Vector2(0.3f, 1f), Vector2.zero, Vector2.zero);
            var shade = Panel("Shade", screen, UiKit.WithAlpha(UiKit.NavyDeep, 0.9f));
            shade.sprite = UiKit.FadeRight;
            Place(shade.rectTransform, Vector2.zero, new Vector2(0.72f, 1f), Vector2.zero, Vector2.zero);
            var floorShade = Panel("Floor Shade", screen, UiKit.WithAlpha(UiKit.NavyDeep, 0.7f));
            floorShade.sprite = UiKit.FadeUp;
            Place(floorShade.rectTransform, Vector2.zero, new Vector2(1f, 0.25f), Vector2.zero, Vector2.zero);

            var logoTop = Label("Logo Top", screen, "NINE TO", 150, TextAnchor.LowerLeft, UiKit.Paper, Face.Display);
            Place(logoTop.rectTransform, new Vector2(0f, 1f), new Vector2(0.7f, 1f), new Vector2(116f, -270f), new Vector2(0f, -110f));
            var logoBottom = Label("Logo Bottom", screen, "FIGHT", 230, TextAnchor.UpperLeft, UiKit.Highlighter, Face.Display);
            Place(logoBottom.rectTransform, new Vector2(0f, 1f), new Vector2(0.7f, 1f), new Vector2(112f, -500f), new Vector2(0f, -250f));
            Shadowed(logoBottom, 6f, 0.5f);
            var tagline = Label("Tagline", screen, "A corporate brawler. Nine floors between you and the corner office.", 24, TextAnchor.UpperLeft,
                UiKit.WithAlpha(UiKit.Paper, 0.85f), Face.Body);
            Place(tagline.rectTransform, new Vector2(0f, 1f), new Vector2(0.6f, 1f), new Vector2(122f, -560f), new Vector2(0f, -500f));

            var menu = Rect("Menu", screen);
            PlaceAt(menu, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(110f, -600f), new Vector2(420f, 3 * 70f));
            TitleMenu = BuildMenu(menu, new[] { "Clock in", "Options", "Quit" }, 50, 70f, UiKit.Paper, UiKit.Ink, titleScreen);

            bestText = Label("Best", screen, BestRecord(), 19, TextAnchor.UpperLeft, UiKit.Muted, Face.Strong);
            Place(bestText.rectTransform, new Vector2(0f, 1f), new Vector2(0.6f, 1f), new Vector2(122f, -860f), new Vector2(0f, -820f));

            var prompts = PromptBar(screen, "W S|Choose;Enter|Select", UiKit.Paper, TextAnchor.MiddleLeft, 34f);
            Place(prompts, new Vector2(0f, 0f), new Vector2(0.6f, 0f), new Vector2(118f, 40f), new Vector2(0f, 80f));
            var version = Label("Version", screen, "Version " + Application.version, 16, TextAnchor.LowerRight, UiKit.Muted, Face.Body);
            Place(version.rectTransform, new Vector2(0.6f, 0f), new Vector2(1f, 0f), new Vector2(0f, 30f), new Vector2(-36f, 60f));
            // Woman_Suit is CC-BY 3.0 (the other characters are CC0): the licence requires this credit.
            var credits = Label("Credits", screen, CreditsLine, 15, TextAnchor.LowerRight, UiKit.Muted, Face.Body);
            Place(credits.rectTransform, new Vector2(0.4f, 0f), new Vector2(1f, 0f), new Vector2(0f, 60f), new Vector2(-36f, 86f));
        }

        /// <summary>Third-party credits shown on the title screen.</summary>
        public const string CreditsLine = "Characters by Quaternius (quaternius.com), CC0 and CC-BY 3.0.  Fonts: Barlow Condensed, IBM Plex Sans (OFL).";

        void BuildClassSelect(RectTransform parent)
        {
            // The player stands on the left in the live scene; the sheet sits on a dark band to the right.
            var screen = FullScreen("Class Select", parent, new Color(0f, 0f, 0f, 0f));
            classScreen = screen.gameObject;
            var shade = Panel("Shade", screen, UiKit.WithAlpha(UiKit.NavyDeep, 0.92f));
            shade.sprite = UiKit.FadeRight;
            Place(shade.rectTransform, new Vector2(0.38f, 0f), Vector2.one, Vector2.zero, Vector2.zero);
            shade.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            var floorShade = Panel("Floor Shade", screen, UiKit.WithAlpha(UiKit.NavyDeep, 0.85f));
            floorShade.sprite = UiKit.FadeUp;
            Place(floorShade.rectTransform, Vector2.zero, new Vector2(1f, 0.3f), Vector2.zero, Vector2.zero);

            var topShade = Panel("Top Shade", screen, UiKit.WithAlpha(UiKit.NavyDeep, 0.9f));
            topShade.sprite = UiKit.FadeUp;
            Place(topShade.rectTransform, new Vector2(0f, 0.62f), Vector2.one, Vector2.zero, Vector2.zero);
            topShade.rectTransform.localScale = new Vector3(1f, -1f, 1f);
            var heading = Label("Heading", screen, "Choose your department", 64, TextAnchor.UpperLeft, UiKit.Paper, Face.Display);
            Place(heading.rectTransform, new Vector2(0f, 1f), new Vector2(0.55f, 1f), new Vector2(110f, -140f), new Vector2(0f, -56f));
            Shadowed(heading, 4f, 0.6f);
            var sub = Label("Sub", screen, "Your department decides how you fight. Your rank decides how far you've climbed.", 21, TextAnchor.UpperLeft,
                UiKit.WithAlpha(UiKit.Paper, 0.85f), Face.Body);
            Place(sub.rectTransform, new Vector2(0f, 1f), new Vector2(0.45f, 1f), new Vector2(112f, -196f), new Vector2(0f, -142f));
            Shadowed(sub, 2f, 0.6f);

            classSheet = Rect("Sheet", screen);
            PlaceAt(classSheet, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-90f, 34f), new Vector2(780f, 780f));

            classTabs = Rect("Tabs", screen);
            Place(classTabs, new Vector2(0f, 0f), new Vector2(0.55f, 0f), new Vector2(100f, 104f), new Vector2(0f, 168f));
            var tabsLayout = classTabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            tabsLayout.spacing = 6f;
            tabsLayout.childAlignment = TextAnchor.MiddleLeft;
            tabsLayout.childControlWidth = tabsLayout.childControlHeight = true;
            tabsLayout.childForceExpandWidth = tabsLayout.childForceExpandHeight = false;

            var prompts = PromptBar(screen, "A D|Choose department;Enter|Clock in;Esc|Back", UiKit.Paper, TextAnchor.MiddleLeft, 34f);
            Place(prompts, new Vector2(0f, 0f), new Vector2(0.6f, 0f), new Vector2(110f, 40f), new Vector2(0f, 80f));
        }

        void BuildOptions(RectTransform parent)
        {
            var screen = FullScreen("Options", parent, UiKit.WithAlpha(UiKit.NavyDeep, 0.78f));
            optionsScreen = screen.gameObject;
            var card = Card("Card", screen, UiKit.Paper, 18f).rectTransform;
            PlaceAt(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(860f, 700f));
            DropShadow(card, 0.6f, 12f);
            var title = Label("Title", card, "Options", 72, TextAnchor.UpperLeft, UiKit.Ink, Face.Display);
            Place(title.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(56f, -120f), new Vector2(-56f, -30f));

            var list = Rect("Rows", card);
            Place(list, new Vector2(0f, 1f), Vector2.one, new Vector2(56f, -660f), new Vector2(-56f, -130f));
            var names = new List<string>();
            foreach (var id in GameSettings.All) names.Add(GameSettings.Label(id));
            names.Add("Back");
            OptionsMenu = BuildMenu(list, names.ToArray(), 24, 56f, UiKit.Ink, UiKit.Ink, optionsScreen, TextAnchor.MiddleLeft, withValues: true);

            var prompts = PromptBar(screen, "W S|Choose;A D|Change;Esc|Back", UiKit.Paper, TextAnchor.MiddleCenter, 34f);
            Place(prompts, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 40f), new Vector2(0f, 80f));
            optionsScreen.SetActive(false);
        }

        void BuildPause(RectTransform parent)
        {
            var screen = FullScreen("Paused", parent, UiKit.WithAlpha(UiKit.NavyDeep, 0.72f));
            pauseScreen = screen.gameObject;

            var card = Card("Card", screen, UiKit.Paper, 18f).rectTransform;
            PlaceAt(card, new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-30f, 20f), new Vector2(620f, 560f));
            DropShadow(card, 0.6f, 12f);
            var title = Label("Title", card, "On break", 84, TextAnchor.UpperLeft, UiKit.Ink, Face.Display);
            Place(title.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(56f, -140f), new Vector2(-40f, -36f));
            var note = Label("Note", card, "Out of office. Your coworkers will wait.", 21, TextAnchor.UpperLeft, UiKit.InkSoft, Face.Italic);
            Place(note.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(58f, -184f), new Vector2(-40f, -144f));
            var menu = Rect("Menu", card);
            Place(menu, new Vector2(0f, 1f), Vector2.one, new Vector2(56f, -440f), new Vector2(-56f, -220f));
            PauseMenu = BuildMenu(menu, new[] { "Back to work", "Options", "Quit to lobby" }, 46, 70f, UiKit.Ink, UiKit.Ink, pauseScreen);
            var tip = Label("Tip", card, "Only two coworkers fight you at once. Keep a combo going for bonus XP.", 18, TextAnchor.LowerLeft, UiKit.InkSoft, Face.Body);
            Place(tip.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(58f, 34f), new Vector2(-40f, 96f));

            // Controls reference alongside.
            var controls = Card("Controls", screen, UiKit.WithAlpha(UiKit.Navy, 0.96f), 18f).rectTransform;
            PlaceAt(controls, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(30f, 20f), new Vector2(520f, 560f));
            DropShadow(controls, 0.6f, 12f);
            var heading = Label("Heading", controls, "Controls", 44, TextAnchor.UpperLeft, UiKit.Paper, Face.Display);
            Place(heading.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(40f, -96f), new Vector2(-40f, -30f));
            string[] rows =
            {
                "A D|Move", "W S|Change lane", "Space|Jump", "Shift|Dodge", "1 2 3|Attacks (to 0)", "J K I U|Light, heavy, special, super",
                "Q|Department ability", "Esc|Pause", "M|Mute", "LT+X Y B A RB RT|Gamepad hotbar 1-6",
            };
            for (int i = 0; i < rows.Length; i++)
            {
                var bar = PromptBar(controls, rows[i], UiKit.Paper, TextAnchor.MiddleLeft, 32f);
                Place(bar, new Vector2(0f, 1f), Vector2.one, new Vector2(40f, -146f - i * 44f), new Vector2(-30f, -112f - i * 44f));
            }
            pauseScreen.SetActive(false);
        }

        void BuildGameOver(RectTransform parent)
        {
            var screen = FullScreen("Game Over", parent, new Color(0.12f, 0.03f, 0.04f, 0.8f));
            gameOverScreen = screen.gameObject;

            // A termination letter, dropped slightly askew on the desk.
            var letter = Card("Letter", screen, UiKit.Paper, 6f).rectTransform;
            PlaceAt(letter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(880f, 640f));
            letter.localRotation = Quaternion.Euler(0f, 0f, -1.5f);
            DropShadow(letter, 0.7f, 14f);
            var brand = Label("Brand", letter, "SYNERGEX", 34, TextAnchor.UpperLeft, UiKit.Brass, Face.Display);
            Place(brand.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(60f, -88f), new Vector2(-60f, -40f));
            var dept = Label("Dept", letter, "Human Resources", 18, TextAnchor.UpperRight, UiKit.InkSoft, Face.Strong);
            Place(dept.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(60f, -80f), new Vector2(-60f, -52f));
            var rule = Panel("Rule", letter, UiKit.Ink).rectTransform;
            Place(rule, new Vector2(0f, 1f), Vector2.one, new Vector2(60f, -96f), new Vector2(-60f, -94f));
            var heading = Label("Heading", letter, "Notice of termination", 58, TextAnchor.UpperLeft, UiKit.Ink, Face.Display);
            Place(heading.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(60f, -180f), new Vector2(-60f, -110f));
            gameOverBody = Label("Body", letter, "", 22, TextAnchor.UpperLeft, UiKit.Ink, Face.Body);
            Place(gameOverBody.rectTransform, new Vector2(0f, 0f), Vector2.one, new Vector2(60f, 120f), new Vector2(-60f, -196f));
            var signature = Label("Signature", letter, "Pat Whitfield\nHR Business Partner", 20, TextAnchor.LowerLeft, UiKit.InkSoft, Face.Italic);
            Place(signature.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(60f, 46f), new Vector2(-60f, 120f));

            var stamp = Card("Stamp", letter, UiKit.WithAlpha(UiKit.Red, 0.85f), 10f);
            stamp.sprite = UiKit.RoundedOutline;
            PlaceAt(stamp.rectTransform, new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-210f, 110f), new Vector2(360f, 104f));
            stamp.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 13f);
            var stampText = Label("Text", stamp.rectTransform, "TERMINATED", 66, TextAnchor.MiddleCenter, UiKit.WithAlpha(UiKit.Red, 0.85f), Face.Display);
            Stretch(stampText.rectTransform, 0f);

            var prompts = PromptBar(screen, "Enter|Start over", UiKit.Paper, TextAnchor.MiddleCenter, 36f);
            Place(prompts, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 40f), new Vector2(0f, 84f));
        }

        void BuildEnding(RectTransform parent)
        {
            var screen = FullScreen("Floor Cleared", parent, UiKit.WithAlpha(UiKit.NavyDeep, 0.92f));
            endingScreen = screen.gameObject;
            endingTitle = Label("Title", screen, "Going up", 120, TextAnchor.MiddleCenter, UiKit.Paper, Face.Display);
            Place(endingTitle.rectTransform, new Vector2(0f, 0.6f), new Vector2(1f, 0.78f), Vector2.zero, Vector2.zero);
            Shadowed(endingTitle, 5f, 0.5f);
            endingBody = Label("Body", screen, "", 25, TextAnchor.MiddleCenter, UiKit.WithAlpha(UiKit.Paper, 0.9f), Face.Body);
            Place(endingBody.rectTransform, new Vector2(0.15f, 0.3f), new Vector2(0.85f, 0.6f), Vector2.zero, Vector2.zero);
            perkRow = Rect("Benefits", screen);
            Place(perkRow, new Vector2(0f, 0.17f), new Vector2(1f, 0.47f), Vector2.zero, Vector2.zero);
            endingPrompts = Rect("Prompts", screen);
            Place(endingPrompts, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 40f), new Vector2(0f, 84f));
        }

        void BuildElevator(RectTransform parent)
        {
            elevatorRoot = Rect("Elevator Ride", parent).gameObject;
            var rootRect = (RectTransform)elevatorRoot.transform;
            Stretch(rootRect, 0f);
            elevatorGroup = elevatorRoot.AddComponent<CanvasGroup>();

            RectTransform Door(string name, float anchorX)
            {
                var door = Panel(name, rootRect, UiKit.Hex("C4CBD6"));
                door.sprite = UiKit.BrushedSteel;
                var r = door.rectTransform;
                Place(r, new Vector2(anchorX, 0f), new Vector2(anchorX + 0.5f, 1f), new Vector2(-3f, 0f), new Vector2(3f, 0f));
                // A dark rubber seal where the doors meet.
                var seal = Panel("Seal", r, UiKit.Hex("2A2E36")).rectTransform;
                if (anchorX < 0.5f) Place(seal, new Vector2(1f, 0f), Vector2.one, new Vector2(-6f, 0f), Vector2.zero);
                else Place(seal, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(6f, 0f));
                return r;
            }
            doorLeft = Door("Door Left", 0f);
            doorRight = Door("Door Right", 0.5f);

            var header = Panel("Header", rootRect, UiKit.Brass).rectTransform;
            Place(header, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -150f), Vector2.zero);
            var headerInner = Panel("Inner", header, UiKit.Hex("8A6F2E")).rectTransform;
            Place(headerInner, Vector2.zero, Vector2.one, new Vector2(0f, 8f), new Vector2(0f, 0f));
            var display = Card("Display", header, UiKit.Hex("1A1410"), 12f).rectTransform;
            PlaceAt(display, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(240f, 106f));
            elevatorNumber = Label("Floor", display, "1", 92, TextAnchor.MiddleCenter, Amber, Face.Display);
            Stretch(elevatorNumber.rectTransform, 0f);
            var up = Label("Up", display, "UP", 20, TextAnchor.UpperLeft, UiKit.WithAlpha(Amber, 0.8f), Face.Heading);
            Place(up.rectTransform, Vector2.zero, Vector2.one, new Vector2(14f, 0f), new Vector2(0f, -8f));
            var sill = Panel("Sill", rootRect, UiKit.Hex("8A6F2E")).rectTransform;
            Place(sill, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 26f));
            elevatorRoot.SetActive(false);
        }

        void BuildCinematics(RectTransform parent)
        {
            letterTop = Panel("Letterbox Top", parent, Color.black).rectTransform;
            letterTop.anchorMin = new Vector2(0f, 1f);
            letterTop.anchorMax = Vector2.one;
            letterTop.pivot = new Vector2(0.5f, 1f);
            letterTop.sizeDelta = Vector2.zero;
            letterBottom = Panel("Letterbox Bottom", parent, Color.black).rectTransform;
            letterBottom.anchorMin = Vector2.zero;
            letterBottom.anchorMax = new Vector2(1f, 0f);
            letterBottom.pivot = new Vector2(0.5f, 0f);
            letterBottom.sizeDelta = Vector2.zero;

            bossCard = Rect("Boss Card", parent);
            PlaceAt(bossCard, Vector2.zero, Vector2.zero, new Vector2(90f, 170f), new Vector2(1100f, 170f));
            bossCardGroup = bossCard.gameObject.AddComponent<CanvasGroup>();
            bossCardGroup.alpha = 0f;
            var stroke = Panel("Stroke", bossCard, UiKit.Highlighter);
            stroke.sprite = UiKit.HighlighterStroke;
            Place(stroke.rectTransform, new Vector2(0f, 0f), new Vector2(0.7f, 0.35f), new Vector2(-14f, -6f), new Vector2(0f, 6f));
            bossCardName = Label("Name", bossCard, "", 110, TextAnchor.LowerLeft, UiKit.Paper, Face.Display);
            Place(bossCardName.rectTransform, new Vector2(0f, 0.3f), Vector2.one, Vector2.zero, Vector2.zero);
            Shadowed(bossCardName, 6f, 0.7f);
            bossCardTitle = Label("Title", bossCard, "", 30, TextAnchor.MiddleLeft, UiKit.Ink, Face.Heading);
            Place(bossCardTitle.rectTransform, Vector2.zero, new Vector2(1f, 0.32f), new Vector2(6f, 0f), Vector2.zero);
        }

        static string BestRecord()
        {
            if (GameFlow.HasBecomeCeo) return "Career record: CEO. You've done it before. Do it faster.";
            int best = GameFlow.BestFloor;
            return best > 0
                ? $"Career record: reached floor {best} of {TowerPlan.FloorCount}"
                : $"{TowerPlan.FloorCount} floors. One elevator. Zero HR complaints filed (yet).";
        }
    }

    /// <summary>Mouse support for menu rows and cards: hover selects, click chooses.</summary>
    public class MenuPointer : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        public System.Action hover, click;
        public void OnPointerEnter(PointerEventData eventData) => hover?.Invoke();
        public void OnPointerClick(PointerEventData eventData) => click?.Invoke();
    }
}
