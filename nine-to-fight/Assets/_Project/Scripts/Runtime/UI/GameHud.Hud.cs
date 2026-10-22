using System.Collections.Generic;
using NineToFight.Combat;
using NineToFight.Game;
using NineToFight.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace NineToFight.UI
{
    /// <summary>In-game HUD: badge, floor plate, keycap hotbar, combo, boss bar, messages, screen effects.</summary>
    public partial class GameHud
    {
        // Badge
        RectTransform hpFill, hpChip, xpFill, perkPills;
        Text rankText, roleText, levelText, hpText, xpText;
        RawImage portrait;
        RenderTexture portraitTexture;
        Camera portraitCamera;
        RectTransform badge;
        float hpChipAmount = 1f, badgeShake;
        int shownPerkCount = -1;

        // Floor plate and event note
        Text floorNumberText, floorNameText, clockText, eventText;
        GameObject eventNote;
        FloorEvent shownEvent;

        // Hotbar
        class HotbarSlot
        {
            public RectTransform root;
            public Image cap, cooldown, dot;
            public RawImage icon;
            public Text legend, name, cooldownText, lockText;
            public FightingMove move;
            public bool unlocked;
            public float press;
        }
        readonly HotbarSlot[] hotbar = new HotbarSlot[ProgressionState.HotbarSize];
        HotbarSlot abilitySlot;

        // Combo and boss
        GameObject comboRoot, bossRoot;
        RectTransform comboTimer, bossFill, bossChip, comboRatingStroke;
        Text comboCount, comboRating, bossName, bossTitle;
        float comboPunch, bossChipAmount = 1f;

        // Messages
        Text toastText, bannerTitle, bannerBody;
        CanvasGroup toastGroup, bannerGroup;
        RectTransform bannerRect, toastRect, bannerStroke;
        float toastUntil, bannerUntil, bannerShownAt = -10f, toastShownAt = -10f;

        // Tutorial hint
        RectTransform hintRoot, hintPrompts;
        Text hintText;
        CanvasGroup hintGroup;
        bool hintVisible;

        CanvasGroup hudGroup;

        // Screen effects
        Image damageVignette;
        float hurtFlash;

        static Color SlotAccent(MoveSlot slot)
        {
            switch (slot)
            {
                case MoveSlot.Heavy: return UiKit.Red;
                case MoveSlot.Special: return UiKit.Teal;
                case MoveSlot.Super: return UiKit.Highlighter;
                default: return UiKit.Steel;
            }
        }

        // ================================================================ public API

        public void Toast(string message)
        {
            toastText.text = message;
            toastUntil = Time.unscaledTime + 2.4f;
            toastShownAt = Time.unscaledTime;
        }

        public void ShowBanner(string title, string body)
        {
            bannerTitle.text = title;
            bannerBody.text = body;
            // Re-unfold only if it wasn't already showing; back-to-back banners just swap text.
            if (Time.unscaledTime > bannerUntil - 0.2f) bannerShownAt = Time.unscaledTime;
            bannerUntil = Time.unscaledTime + 3f;
        }

        public void RefreshFloor()
        {
            if (flow == null) return;
            floorNumberText.text = $"Floor {flow.Floor}";
            floorNameText.text = flow.FloorName;
        }

        /// <summary>Kept for compatibility; floors now change behind the elevator doors.</summary>
        public void FadeIn() { }

        /// <summary>Bumps the combo counter when it reaches a new rating.</summary>
        public void PunchCombo() => comboPunch = 1f;

        /// <summary>Shows the floor's event as a sticky note (and the drill countdown).</summary>
        public void SetEvent(FloorEvent floorEvent)
        {
            shownEvent = floorEvent;
            eventNote.SetActive(floorEvent != FloorEvent.None);
            eventText.text = TowerPlan.EventName(floorEvent);
        }

        /// <summary>Tutorial prompt above the hotbar, e.g. "A D|Move;W S|Change lane".</summary>
        public void ShowHint(string spec)
        {
            foreach (Transform child in hintPrompts) Destroy(child.gameObject);
            var bar = PromptBar(hintPrompts, spec, UiKit.Paper, TextAnchor.MiddleCenter, 42f);
            Stretch(bar, 0f);
            hintVisible = true;
        }

        public void HideHint() => hintVisible = false;

        // ================================================================ per frame

        void UpdateHud(float now)
        {
            var health = player.Health;
            SetFill(hpFill, health.Normalized);
            // The pale "chip" lags behind so you can see how much a hit took.
            hpChipAmount = health.Normalized >= hpChipAmount
                ? health.Normalized
                : Mathf.MoveTowards(hpChipAmount, health.Normalized, Time.unscaledDeltaTime * 0.6f);
            SetFill(hpChip, hpChipAmount);
            hpText.text = $"{Mathf.CeilToInt(health.Current)} / {Mathf.CeilToInt(health.Max)}";

            var state = player.Progression.State;
            if (state == null) return; // not configured yet (or dropped by a script reload in Play mode)
            SetFill(xpFill, state.IsMaxLevel ? 1f : (float)state.Xp / state.XpToNext);
            xpText.text = state.IsMaxLevel ? "Maxed out" : $"{state.Xp} / {state.XpToNext}";
            rankText.text = state.Rank;
            levelText.text = state.Level.ToString();
            roleText.text = player.Class != null ? $"{player.Class.displayName}, {player.Class.department}" : "New hire";
            clockText.text = flow != null && GameFlow.IsPlaying ? ClockString(Time.time - flow.ShiftStartTime) : "9:00 AM";

            // The badge swings on its lanyard when you get hit.
            badgeShake = Mathf.MoveTowards(badgeShake, 0f, Time.unscaledDeltaTime * 2.5f);
            badge.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(now * 22f) * 4f * badgeShake);

            UpdateHotbar();
            UpdateCombo();
            UpdateBossBar();
            UpdateEventAndPerks();
        }

        void UpdateMessages(float now)
        {
            toastGroup.alpha = Mathf.Clamp01((toastUntil - now) / 0.3f);
            float p = EaseOutBack((now - toastShownAt) / 0.3f);
            toastRect.anchoredPosition = new Vector2(0f, Mathf.LerpUnclamped(40f, 0f, p));

            bannerGroup.alpha = Mathf.Clamp01((bannerUntil - now) / 0.4f);
            float b = (now - bannerShownAt) / 0.32f;
            bannerRect.localScale = new Vector3(1f, Mathf.LerpUnclamped(0.05f, 1f, EaseOutBack(b)), 1f);
            // The highlighter stroke sweeps across behind the title, then the title settles.
            float sweep = EaseInOut((now - bannerShownAt - 0.12f) / 0.35f);
            float titleWidth = Mathf.Min(bannerTitle.preferredWidth + 70f, 1600f);
            bannerStroke.sizeDelta = new Vector2(titleWidth * sweep, 96f);
            bannerStroke.anchoredPosition = new Vector2(-titleWidth / 2f, 36f);

            hintGroup.alpha = Glide(hintGroup.alpha, hintVisible ? 1f : 0f, 8f);
            hintRoot.anchoredPosition = new Vector2(0f, Mathf.Lerp(150f, 196f, hintGroup.alpha));
        }

        void UpdateEventAndPerks()
        {
            if (flow == null) return;
            if (shownEvent == FloorEvent.FireDrill)
            {
                float left = flow.DrillTimeLeft;
                eventText.text = left > 0f ? $"Fire drill  {Mathf.FloorToInt(left / 60f)}:{Mathf.FloorToInt(left % 60f):00}" : "Fire drill: locked";
                eventText.color = left > 0f && left < 10f && Mathf.Repeat(Time.unscaledTime, 0.5f) < 0.25f ? UiKit.Red : UiKit.Ink;
            }

            var perks = player.Perks;
            if (perks.Count == shownPerkCount) return;
            shownPerkCount = perks.Count;
            foreach (Transform child in perkPills) Destroy(child.gameObject);
            foreach (var kv in perks.Owned)
            {
                var perk = PerkLibrary.Get(kv.Key);
                var pill = Card("Perk " + perk.name, perkPills, UiKit.WithAlpha(UiKit.NavyDeep, 0.88f), 12f);
                var layout = pill.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.padding = new RectOffset(10, 12, 4, 4);
                layout.spacing = 8f;
                layout.childAlignment = TextAnchor.MiddleLeft;
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = layout.childForceExpandHeight = false;
                var dot = Card("Dot", pill.rectTransform, perk.color, 8f);
                var le = dot.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = le.preferredHeight = 12f;
                var text = Label("Name", pill.rectTransform, kv.Value > 1 ? $"{perk.name} x{kv.Value}" : perk.name, 17, TextAnchor.MiddleLeft, UiKit.Paper, Face.Strong);
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                var tle = text.gameObject.AddComponent<LayoutElement>();
                tle.preferredWidth = text.preferredWidth;
            }
        }

        void UpdateCombo()
        {
            if (flow == null) return;
            var combo = flow.Combo;
            float now = Time.time;
            bool show = combo.IsActive(now) && combo.Count >= 2;
            comboRoot.SetActive(show);
            if (!show) return;
            comboCount.text = combo.Count.ToString();
            string rating = ComboTracker.Rating(combo.Count);
            comboRating.text = rating.Length > 0 ? rating : "hits";
            comboRating.color = rating.Length > 0 ? UiKit.Ink : UiKit.Paper;
            comboRatingStroke.gameObject.SetActive(rating.Length > 0);
            comboRatingStroke.sizeDelta = new Vector2(comboRating.preferredWidth + 40f, 52f);
            comboTimer.anchorMax = new Vector2(Mathf.Clamp01(combo.WindowLeft(now)), 1f);
            comboPunch = Mathf.MoveTowards(comboPunch, 0f, Time.unscaledDeltaTime * 4f);
            float hitPop = Mathf.Clamp01(1f - (now - combo.LastHitTime) * 8f) * 0.12f;
            comboRoot.transform.localScale = Vector3.one * (1f + comboPunch * 0.35f + hitPop);
        }

        void UpdateBossBar()
        {
            var boss = flow != null ? flow.EngagedBoss : null;
            bossRoot.SetActive(boss != null);
            if (boss == null)
            {
                bossChipAmount = 1f;
                return;
            }
            bossName.text = boss.DisplayName;
            bossTitle.text = GameFlow.BossSubtitle(boss);
            float n = boss.Health.Normalized;
            SetFill(bossFill, n);
            bossChipAmount = Mathf.MoveTowards(Mathf.Max(bossChipAmount, n), n, Time.unscaledDeltaTime * 0.35f);
            SetFill(bossChip, bossChipAmount);
        }

        void RefreshMoves()
        {
            if (player == null) return;
            var state = player.Progression.State;
            var catalog = player.HeldItem != null ? player.HeldItem.Catalog : null;
            for (int i = 0; i < hotbar.Length; i++)
            {
                var move = state.HotbarMove(i);
                var slot = hotbar[i];
                slot.move = move;
                slot.unlocked = state.IsUnlocked(move);
                slot.root.gameObject.SetActive(move != null);
                if (move == null) continue;

                slot.icon.texture = PropIcons.Get(catalog, move.prop);
                slot.icon.enabled = slot.icon.texture != null;
                slot.dot.color = SlotAccent(move.slot);
                slot.name.text = move.displayName;
                if (slot.unlocked)
                {
                    slot.cap.color = UiKit.Paper;
                    slot.icon.color = Color.white;
                    slot.lockText.text = "";
                    slot.name.color = UiKit.Paper;
                    slot.legend.color = UiKit.Ink;
                }
                else
                {
                    slot.cap.color = UiKit.Steel;
                    slot.icon.color = new Color(0.25f, 0.28f, 0.35f, 0.55f);
                    slot.lockText.text = $"Lv {move.unlockLevel}";
                    slot.name.color = UiKit.Muted;
                    slot.legend.color = UiKit.InkSoft;
                }
            }
        }

        void UpdateHotbar()
        {
            var attack = player.Attack;
            foreach (var slot in hotbar)
            {
                if (slot.move == null) continue;
                float remaining = slot.unlocked && slot.move.cooldown > 0f ? attack.CooldownRemaining(slot.move) : 0f;
                slot.cooldown.fillAmount = slot.move.cooldown > 0f ? Mathf.Clamp01(remaining / slot.move.cooldown) : 0f;
                slot.cooldownText.text = remaining > 0.05f ? remaining.ToString(remaining < 1f ? "0.0" : "0") : "";
                AnimateKey(slot, attack.Current == slot.move);
            }

            var caster = player.Ability;
            var ability = caster != null ? caster.Ability : null;
            abilitySlot.root.gameObject.SetActive(ability != null);
            if (ability == null) return;
            abilitySlot.name.text = ability.displayName;
            abilitySlot.dot.color = ability.color;
            abilitySlot.cap.color = caster.IsReady || caster.IsActive ? UiKit.Paper : UiKit.Steel;
            abilitySlot.cooldown.fillAmount = Mathf.Clamp01(caster.CooldownFraction);
            abilitySlot.cooldownText.text = !caster.IsReady ? Mathf.CeilToInt(caster.CooldownRemaining).ToString() : "";
            AnimateKey(abilitySlot, caster.IsActive);
        }

        static void AnimateKey(HotbarSlot slot, bool down)
        {
            // Pressed keys sink into the keyboard and catch the highlighter.
            slot.press = Glide(slot.press, down ? 1f : 0f, down ? 40f : 14f);
            slot.root.anchoredPosition = new Vector2(slot.root.anchoredPosition.x, -6f * slot.press);
            if (down) slot.cap.color = Color.Lerp(UiKit.Paper, UiKit.Highlighter, slot.press);
        }

        // ================================================================ portrait

        /// <summary>The badge photo is live: a small camera looking at the player's face.</summary>
        void SetupPortrait()
        {
            const int layer = 30;
            if (player == null || player.Visual == null || portraitCamera != null) return;
            foreach (var t in player.Visual.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;

            portraitTexture = new RenderTexture(220, 256, 16, RenderTextureFormat.ARGB32) { name = "Badge Portrait" };
            var camGo = new GameObject("Badge Portrait Camera");
            camGo.transform.SetParent(player.Visual.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 2.02f, 1.45f);
            camGo.transform.localRotation = Quaternion.LookRotation(new Vector3(0f, 1.96f, 0f) - camGo.transform.localPosition);
            portraitCamera = camGo.AddComponent<Camera>();
            portraitCamera.cullingMask = 1 << layer;
            portraitCamera.clearFlags = CameraClearFlags.SolidColor;
            portraitCamera.backgroundColor = UiKit.Hex("A9BAD3");
            portraitCamera.fieldOfView = 24f;
            portraitCamera.nearClipPlane = 0.05f;
            portraitCamera.farClipPlane = 4f;
            portraitCamera.targetTexture = portraitTexture;
            portraitCamera.depth = -10f;
            portrait.texture = portraitTexture;
            portrait.color = Color.white;
            player.Health.Damaged += _ => badgeShake = 1f;
        }

        void OnDestroy()
        {
            if (portraitTexture != null) portraitTexture.Release();
        }

        // ================================================================ screen effects

        void BuildScreenEffects(RectTransform parent)
        {
            damageVignette = Panel("Damage Vignette", parent, new Color(UiKit.Red.r, UiKit.Red.g * 0.4f, UiKit.Red.b * 0.4f, 0f));
            damageVignette.sprite = UiKit.Vignette;
            Stretch(damageVignette.rectTransform, 0f);
        }

        void UpdateScreenEffects(float now)
        {
            hurtFlash = Mathf.MoveTowards(hurtFlash, 0f, Time.unscaledDeltaTime * 2.2f);
            float low = 0f;
            if (player != null && hudRoot.activeSelf && !player.Health.IsDead)
            {
                float n = player.Health.Normalized;
                // Low health: the edges pulse with a heartbeat.
                if (n < 0.3f) low = (0.3f - n) / 0.3f * (0.55f + 0.45f * Mathf.Pow(Mathf.Abs(Mathf.Sin(now * 3.2f)), 6f));
            }
            var c = damageVignette.color;
            c.a = Mathf.Clamp01(hurtFlash * 0.7f + low * 0.6f);
            damageVignette.color = c;
        }

        // ================================================================ construction

        void BuildHud(RectTransform parent)
        {
            hudRoot = Rect("Gameplay HUD", parent).gameObject;
            var hud = (RectTransform)hudRoot.transform;
            Stretch(hud, 0f);
            hudGroup = hudRoot.AddComponent<CanvasGroup>();

            BuildBadge(hud);
            BuildFloorPlate(hud);
            BuildHotbar(hud);
            BuildCombo(hud);
            BuildBossBar(hud);
            BuildHint(hud);
        }

        void BuildBadge(RectTransform hud)
        {
            // Lanyard: a strap from the top of the screen to the badge clip.
            var strap = Panel("Lanyard", hud, UiKit.Highlighter);
            PlaceAt(strap.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(150f, 0f), new Vector2(22f, 40f));
            var clip = Card("Clip", hud, UiKit.Steel, 4f);
            PlaceAt(clip.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(150f, -34f), new Vector2(40f, 16f));

            badge = Rect("Badge Pivot", hud);
            // Pivot at the clip so it swings from the lanyard; left edge lands 32 units in from the screen edge.
            PlaceAt(badge, new Vector2(0f, 1f), new Vector2(118f / 540f, 1f), new Vector2(150f, -44f), new Vector2(540f, 210f));
            var card = Card("Badge", badge, UiKit.Paper, 16f).rectTransform;
            Stretch(card, 0f);
            DropShadow(card, 0.5f, 10f);

            var header = Card("Header", card, UiKit.Navy, 16f).rectTransform;
            Place(header, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -42f), Vector2.zero);
            var headerFlat = Panel("Header Base", header, UiKit.Navy).rectTransform;
            Place(headerFlat, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 16f));
            var brand = Label("Brand", header, "SYNERGEX", 26, TextAnchor.MiddleLeft, UiKit.Brass, Face.Display);
            Place(brand.rectTransform, Vector2.zero, Vector2.one, new Vector2(18f, 0f), new Vector2(-18f, 0f));
            var kind = Label("Kind", header, "Employee ID", 15, TextAnchor.MiddleRight, UiKit.Muted, Face.Strong);
            Place(kind.rectTransform, Vector2.zero, Vector2.one, new Vector2(18f, 0f), new Vector2(-18f, 0f));

            // Photo, with the level on a round sticker in its corner.
            var frame = Card("Photo Frame", card, UiKit.PaperShade, 10f).rectTransform;
            Place(frame, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(14f, 14f), new Vector2(146f, -54f));
            var mask = Card("Photo Mask", frame, Color.white, 8f);
            Stretch(mask.rectTransform, 4f);
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            portrait = Rect("Photo", mask.rectTransform).gameObject.AddComponent<RawImage>();
            portrait.raycastTarget = false;
            portrait.color = UiKit.Hex("A9BAD3");
            Stretch(portrait.rectTransform, 0f);
            var sticker = Card("Level Sticker", card, UiKit.Highlighter, 22f);
            PlaceAt(sticker.rectTransform, new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), new Vector2(138f, 22f), new Vector2(44f, 44f));
            sticker.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -8f);
            levelText = Label("Level", sticker.rectTransform, "1", 28, TextAnchor.MiddleCenter, UiKit.Ink, Face.Display);
            Stretch(levelText.rectTransform, 0f);

            rankText = Label("Rank", card, "Intern", 38, TextAnchor.UpperLeft, UiKit.Ink, Face.Display);
            Place(rankText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(166f, -92f), new Vector2(-16f, -48f));
            roleText = Label("Role", card, "", 17, TextAnchor.UpperLeft, UiKit.InkSoft, Face.Strong);
            Place(roleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(168f, -116f), new Vector2(-16f, -92f));

            hpFill = Meter(card, "Health", UiKit.Red, -120f, out hpText, out hpChip);
            xpFill = Meter(card, "Performance", UiKit.Teal, -160f, out xpText, out _);

            perkPills = Rect("Perks", hud);
            PlaceAt(perkPills, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, -268f), new Vector2(1200f, 30f));
            var perkLayout = perkPills.gameObject.AddComponent<HorizontalLayoutGroup>();
            perkLayout.spacing = 8f;
            perkLayout.childAlignment = TextAnchor.MiddleLeft;
            perkLayout.childControlWidth = perkLayout.childControlHeight = true;
            perkLayout.childForceExpandWidth = perkLayout.childForceExpandHeight = false;
        }

        RectTransform Meter(RectTransform card, string label, Color color, float top, out Text value, out RectTransform chip)
        {
            var name = Label(label, card, label, 15, TextAnchor.LowerLeft, UiKit.InkSoft, Face.Strong);
            Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(168f, top - 20f), new Vector2(-18f, top));
            value = Label(label + " Value", card, "", 15, TextAnchor.LowerRight, UiKit.Ink, Face.Strong);
            Place(value.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(168f, top - 20f), new Vector2(-18f, top));
            var back = Card(label + " Back", card, UiKit.PaperShade, 7f).rectTransform;
            Place(back, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(168f, top - 36f), new Vector2(-18f, top - 22f));
            chip = Card(label + " Chip", back, new Color(1f, 0.86f, 0.8f), 7f).rectTransform;
            Place(chip, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var fill = Card(label + " Fill", back, color, 7f).rectTransform;
            Place(fill, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return fill;
        }

        void BuildFloorPlate(RectTransform hud)
        {
            var plate = Card("Floor Plate", hud, UiKit.Navy, 14f).rectTransform;
            PlaceAt(plate, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-32f, -32f), new Vector2(420f, 112f));
            DropShadow(plate, 0.45f, 8f);
            var trim = Card("Brass Trim", plate, UiKit.Brass, 14f);
            Stretch(trim.rectTransform, 0f);
            var inner = Card("Inner", plate, UiKit.Navy, 12f);
            Stretch(inner.rectTransform, 3f);
            floorNumberText = Label("Floor", inner.rectTransform, "Floor 1", 46, TextAnchor.UpperLeft, UiKit.Paper, Face.Display);
            Place(floorNumberText.rectTransform, Vector2.zero, Vector2.one, new Vector2(20f, 0f), new Vector2(-10f, -8f));
            floorNameText = Label("Name", inner.rectTransform, "", 17, TextAnchor.LowerLeft, UiKit.Muted, Face.Strong);
            Place(floorNameText.rectTransform, Vector2.zero, Vector2.one, new Vector2(21f, 14f), new Vector2(-10f, 0f));
            clockText = Label("Clock", inner.rectTransform, "9:00 AM", 40, TextAnchor.MiddleRight, UiKit.Highlighter, Face.Heading);
            Place(clockText.rectTransform, Vector2.zero, Vector2.one, new Vector2(10f, 0f), new Vector2(-20f, 0f));

            // Floor events go on a sticky note under the plate.
            var note = Panel("Event Note", hud, UiKit.Highlighter);
            PlaceAt(note.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-48f, -156f), new Vector2(290f, 62f));
            note.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 2.5f);
            Shadowed(note, 4f, 0.35f);
            eventNote = note.gameObject;
            eventText = Label("Text", note.rectTransform, "", 28, TextAnchor.MiddleCenter, UiKit.Ink, Face.Heading);
            Stretch(eventText.rectTransform, 6f);
            eventNote.SetActive(false);
        }

        /// <summary>Ten attack keys along the bottom (1-9, 0) drawn as keycaps, plus the Q ability key apart.</summary>
        void BuildHotbar(RectTransform hud)
        {
            const float size = 92f, gap = 12f;
            float total = hotbar.Length * size + (hotbar.Length - 1) * gap;
            var bar = Rect("Hotbar", hud);
            PlaceAt(bar, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-70f, 58f), new Vector2(total, size + 8f));

            for (int i = 0; i < hotbar.Length; i++)
                hotbar[i] = CreateKey(bar, ProgressionState.HotbarKey(i), size / 2f + i * (size + gap), size);
            abilitySlot = CreateKey(bar, "Q", total + 40f + 60f, 120f);
            abilitySlot.icon.enabled = false;
            PlaceAt(abilitySlot.dot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(6f, 4f), new Vector2(46f, 46f));
        }

        HotbarSlot CreateKey(RectTransform bar, string legend, float x, float width)
        {
            var root = Rect("Key " + legend, bar);
            PlaceAt(root, new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(x, 0f), new Vector2(width, 100f));
            var cap = Panel("Cap", root, UiKit.Paper);
            cap.sprite = UiKit.Keycap;
            cap.type = Image.Type.Sliced;
            cap.pixelsPerUnitMultiplier = 1f;
            Stretch(cap.rectTransform, 0f);
            Shadowed(cap, 4f, 0.45f);

            var icon = Rect("Icon", cap.rectTransform).gameObject.AddComponent<RawImage>();
            icon.raycastTarget = false;
            PlaceAt(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(6f, 2f), new Vector2(64f, 64f));
            var keyLegend = Label("Legend", cap.rectTransform, legend, 24, TextAnchor.UpperLeft, UiKit.Ink, Face.Heading);
            Place(keyLegend.rectTransform, Vector2.zero, Vector2.one, new Vector2(12f, 0f), new Vector2(-8f, -8f));
            var dot = Card("Type", cap.rectTransform, UiKit.Steel, 6f);
            PlaceAt(dot.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -14f), new Vector2(12f, 12f));
            var lockText = Label("Lock", cap.rectTransform, "", 20, TextAnchor.MiddleCenter, UiKit.Ink, Face.Heading);
            Place(lockText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 12f), Vector2.zero);

            var cooldown = Card("Cooldown", cap.rectTransform, new Color(0.07f, 0.1f, 0.18f, 0.72f), 14f);
            cooldown.type = Image.Type.Filled;
            cooldown.fillMethod = Image.FillMethod.Vertical;
            cooldown.fillOrigin = (int)Image.OriginVertical.Top;
            cooldown.fillAmount = 0f;
            Place(cooldown.rectTransform, Vector2.zero, Vector2.one, new Vector2(5f, 14f), new Vector2(-5f, -4f));
            var cooldownText = Label("Seconds", cap.rectTransform, "", 30, TextAnchor.MiddleCenter, UiKit.Paper, Face.Display);
            Place(cooldownText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 10f), Vector2.zero);

            var name = Label("Name", root, "", 14, TextAnchor.UpperCenter, UiKit.Paper, Face.Strong);
            Place(name.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(-8f, -40f), new Vector2(8f, -4f));
            name.lineSpacing = 0.95f;
            Shadowed(name, 2f, 0.8f);

            return new HotbarSlot { root = root, cap = cap, cooldown = cooldown, dot = dot, icon = icon, legend = keyLegend, name = name, cooldownText = cooldownText, lockText = lockText };
        }

        void BuildCombo(RectTransform hud)
        {
            var rootRect = Rect("Combo", hud);
            PlaceAt(rootRect, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-48f, 40f), new Vector2(360f, 260f));
            comboRoot = rootRect.gameObject;

            comboCount = Label("Count", rootRect, "0", 170, TextAnchor.LowerRight, UiKit.Paper, Face.Display);
            Place(comboCount.rectTransform, new Vector2(0f, 0.3f), Vector2.one, Vector2.zero, Vector2.zero);
            Shadowed(comboCount, 5f, 0.6f);

            var stroke = Panel("Rating Stroke", rootRect, UiKit.Highlighter);
            stroke.sprite = UiKit.HighlighterStroke;
            comboRatingStroke = stroke.rectTransform;
            PlaceAt(comboRatingStroke, new Vector2(1f, 0.2f), new Vector2(1f, 0.5f), new Vector2(10f, 0f), new Vector2(200f, 52f));
            comboRating = Label("Rating", rootRect, "", 36, TextAnchor.MiddleRight, UiKit.Ink, Face.Display);
            Place(comboRating.rectTransform, new Vector2(0f, 0.08f), new Vector2(1f, 0.32f), Vector2.zero, new Vector2(-10f, 0f));

            var back = Card("Timer Back", rootRect, new Color(0f, 0f, 0f, 0.45f), 4f).rectTransform;
            Place(back, new Vector2(0.35f, 0f), new Vector2(1f, 0f), new Vector2(0f, 4f), new Vector2(0f, 12f));
            comboTimer = Card("Timer", back, UiKit.Highlighter, 4f).rectTransform;
            Place(comboTimer, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            comboRoot.SetActive(false);
        }

        void BuildBossBar(RectTransform hud)
        {
            var rootRect = Rect("Boss", hud);
            PlaceAt(rootRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(900f, 110f));
            bossRoot = rootRect.gameObject;
            bossName = Label("Name", rootRect, "", 46, TextAnchor.UpperLeft, UiKit.Paper, Face.Display);
            Place(bossName.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -54f), Vector2.zero);
            Shadowed(bossName, 3f, 0.7f);
            bossTitle = Label("Title", rootRect, "", 19, TextAnchor.UpperRight, UiKit.Highlighter, Face.Italic);
            Place(bossTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -48f), new Vector2(0f, -18f));
            Shadowed(bossTitle, 2f, 0.7f);
            var back = Card("Back", rootRect, new Color(0.05f, 0.06f, 0.1f, 0.8f), 8f).rectTransform;
            Place(back, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -86f), new Vector2(0f, -60f));
            bossChip = Card("Chip", back, new Color(1f, 0.86f, 0.8f), 8f).rectTransform;
            Place(bossChip, Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f));
            bossFill = Card("Fill", back, UiKit.Red, 8f).rectTransform;
            Place(bossFill, Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f));
            bossRoot.SetActive(false);
        }

        void BuildHint(RectTransform hud)
        {
            hintRoot = Rect("Hint", hud);
            PlaceAt(hintRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 196f), new Vector2(900f, 64f));
            hintGroup = hintRoot.gameObject.AddComponent<CanvasGroup>();
            hintGroup.alpha = 0f;
            var pill = Card("Pill", hintRoot, UiKit.WithAlpha(UiKit.NavyDeep, 0.92f), 20f);
            Stretch(pill.rectTransform, 0f);
            hintPrompts = Rect("Prompts", pill.rectTransform);
            Stretch(hintPrompts, 10f);
        }

        void BuildMessages(RectTransform parent)
        {
            // Notifications: a pill that drops in under the top HUD.
            var toastAnchor = Rect("Toast Anchor", parent);
            PlaceAt(toastAnchor, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(980f, 58f));
            toastRect = Rect("Toast", toastAnchor);
            PlaceAt(toastRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(980f, 58f));
            toastGroup = toastRect.gameObject.AddComponent<CanvasGroup>();
            toastGroup.alpha = 0f;
            var toastCard = Card("Card", toastRect, UiKit.WithAlpha(UiKit.NavyDeep, 0.94f), 22f);
            Stretch(toastCard.rectTransform, 0f);
            var toastMark = Card("Mark", toastCard.rectTransform, UiKit.Highlighter, 6f);
            Place(toastMark.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(14f, 14f), new Vector2(26f, -14f));
            toastText = Label("Text", toastCard.rectTransform, "", 23, TextAnchor.MiddleCenter, UiKit.Paper, Face.Strong);
            Stretch(toastText.rectTransform, 0f);
            Place(toastText.rectTransform, Vector2.zero, Vector2.one, new Vector2(36f, 0f), new Vector2(-20f, 0f));

            // Banners: a band across the middle; the title gets a highlighter pass.
            var banner = Panel("Banner", parent, new Color(0.07f, 0.1f, 0.18f, 0.88f)).rectTransform;
            Place(banner, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 40f), new Vector2(0f, 250f));
            bannerRect = banner;
            bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.alpha = 0f;
            var topRule = Panel("Rule", banner, UiKit.Brass).rectTransform;
            Place(topRule, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -3f), Vector2.zero);
            var bottomRule = Panel("Rule", banner, UiKit.Brass).rectTransform;
            Place(bottomRule, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 3f));
            var stroke = Panel("Stroke", banner, UiKit.Highlighter);
            stroke.sprite = UiKit.HighlighterStroke;
            bannerStroke = stroke.rectTransform;
            bannerStroke.anchorMin = bannerStroke.anchorMax = new Vector2(0.5f, 0.5f);
            bannerStroke.pivot = new Vector2(0f, 0.5f);
            bannerTitle = Label("Title", banner, "", 84, TextAnchor.MiddleCenter, UiKit.Ink, Face.Display);
            Place(bannerTitle.rectTransform, new Vector2(0f, 0.38f), Vector2.one, Vector2.zero, Vector2.zero);
            bannerTitle.horizontalOverflow = HorizontalWrapMode.Overflow;
            bannerBody = Label("Body", banner, "", 27, TextAnchor.MiddleCenter, UiKit.Paper, Face.Body);
            Place(bannerBody.rectTransform, Vector2.zero, new Vector2(1f, 0.4f), new Vector2(200f, 8f), new Vector2(-200f, 0f));
        }
    }
}
