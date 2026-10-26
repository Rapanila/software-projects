using System.Collections;
using System.Collections.Generic;
using NineToFight.Audio;
using NineToFight.CameraRig;
using NineToFight.Core;
using NineToFight.Enemies;
using NineToFight.Fx;
using NineToFight.Player;
using NineToFight.UI;
using NineToFight.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NineToFight.Game
{
    public enum GameState { Title, ClassSelect, Playing, FloorCleared, GameOver, Ending, Paused, Options }

    /// <summary>
    /// Runs the climb up the office tower: title menu, department pick, then nine floors of fighting,
    /// each ending with an elevator ride. Floor 1 is the hand-built scene; higher floors are re-staged
    /// in the same room by the TowerDirector.
    /// </summary>
    public class GameFlow : MonoBehaviour
    {
        public static GameFlow Instance { get; private set; }
        public static bool IsPlaying => Instance != null &&
            (Instance.State == GameState.Playing || Instance.State == GameState.FloorCleared);
        /// <summary>The pause menu (or the options opened from it) is up: gameplay input is ignored.</summary>
        public static bool IsPaused => Instance != null && (Instance.State == GameState.Paused ||
            (Instance.State == GameState.Options && Instance.stateBeforeOptions == GameState.Paused));

        const string BestFloorKey = "NineToFight.BestFloor";
        const string ChampionKey = "NineToFight.BecameCeo";
        const float HealBetweenFloors = 0.4f;

        // Survives scene reloads so "try again" keeps your department highlighted.
        static int lastClassIndex;

        [Header("Floor")]
        [SerializeField] int floorNumber = 1;
        [SerializeField] string floorName = "The Cubicle Farm";
        [SerializeField] string nextFloorTeaser = "Floor 2: Accounting. The spreadsheets fight back.";

        [Header("References")]
        [SerializeField] PlayerController player;
        [SerializeField] GameHud hud;
        [SerializeField] ElevatorDoor elevator;
        [SerializeField] PlayerClass[] playerClasses;
        Vector3 showcaseSpot;

        int selectedClass;
        bool hitStopping, riding;
        float timeEffectUntil;
        Coroutine timeRoutine;
        GameState stateBeforePause, stateBeforeOptions;
        TowerDirector director;
        Vector3 playerStart;

        // Run variety: perks offered at the elevator, events rolled per floor.
        List<Perk> perkOffer;
        int perkChoice;
        readonly List<FloorEvent> usedEvents = new List<FloorEvent>();
        readonly System.Random runRng = new System.Random();
        float drillEndsAt;

        public FloorEvent CurrentEvent { get; private set; }
        /// <summary>The fight fence: shut while coworkers are engaged (see <see cref="ArenaLock"/>).</summary>
        public ArenaLock Arena { get; } = new ArenaLock();
        /// <summary>Fire Drill: seconds left before the elevator locks (0 when not running).</summary>
        public float DrillTimeLeft => CurrentEvent == FloorEvent.FireDrill && drillEndsAt > 0f ? Mathf.Max(0f, drillEndsAt - Time.time) : 0f;
        public IReadOnlyList<Perk> PerkOffer => perkOffer;
        /// <summary>Tests: guarantees this perk is in the next elevator offer (cleared once used), like TowerDirector.ForceEvent.</summary>
        public static PerkId? ForcePerk;

        public GameState State { get; private set; }
        public float ShiftStartTime { get; private set; }
        public float RunStartTime { get; private set; }
        public int Floor => floorNumber;
        public string FloorName => floorNumber == 1 ? floorName : TowerPlan.FloorName(floorNumber);
        public PlayerController Player => player;
        public PlayerClass[] PlayerClasses => playerClasses;
        public ComboTracker Combo { get; } = new ComboTracker();
        public int Knockouts { get; private set; }
        /// <summary>True while the elevator doors are animating between floors.</summary>
        public bool IsRiding => riding;
        /// <summary>The boss currently fighting the player, for the HUD's boss bar.</summary>
        public EnemyBrain EngagedBoss { get; private set; }
        public static int BestFloor => PlayerPrefs.GetInt(BestFloorKey, 0);
        public static bool HasBecomeCeo => PlayerPrefs.GetInt(ChampionKey, 0) == 1;

        void Awake()
        {
            Instance = this;
            Time.timeScale = 1f;
            GameSettings.Apply();
            director = gameObject.AddComponent<TowerDirector>();
            director.Capture();
            gameObject.AddComponent<GameFeedback>();
            gameObject.AddComponent<ScreenFx>();
            gameObject.AddComponent<Tutorial>();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Time.timeScale = 1f;
        }

        void OnEnable()
        {
            EnemyBrain.AnyDefeated += OnEnemyDefeated;
            Health.AnyDamaged += OnAnyDamaged;
        }

        void OnDisable()
        {
            EnemyBrain.AnyDefeated -= OnEnemyDefeated;
            Health.AnyDamaged -= OnAnyDamaged;
        }

        void Start()
        {
            playerStart = player.transform.position;
            director.PromoteSceneBosses();
            var caster = player.GetComponent<AbilityCaster>();
            HitEffects.Ensure(caster != null ? caster.EffectMaterial : null);

            player.Died += OnPlayerDied;
            player.Progression.Promoted += (level, rank) =>
                hud.ShowBanner("Promoted!", $"You are now a {rank}. Max HP up.");
            player.Progression.MoveLearned += move =>
                hud.ShowBanner("New move: " + move.displayName, move.description);

            hud.Bind(player, this);
            hud.TitleMenu.Chosen += OnTitleChoice;
            hud.PauseMenu.Chosen += OnPauseChoice;
            hud.OptionsMenu.Chosen += OnOptionsChoice;
            foreach (var menu in new[] { hud.TitleMenu, hud.PauseMenu, hud.OptionsMenu })
                menu.SelectionChanged += _ => GameAudio.Play(Sfx.UiMove, 0.45f, 0f);

            player.SetControlEnabled(false);
            selectedClass = playerClasses != null && playerClasses.Length > 0
                ? Mathf.Clamp(lastClassIndex, 0, playerClasses.Length - 1)
                : 0;
            SetState(GameState.Title);
            GameAudio.Prewarm();
        }

        void SetState(GameState state)
        {
            State = state;
            hud.ShowScreen(state);
            switch (state)
            {
                case GameState.Title:
                case GameState.ClassSelect:
                case GameState.Ending:
                    GameAudio.PlayMusic(MusicTrack.Lounge);
                    GameAudio.PlayAmbience(false);
                    break;
                case GameState.Playing:
                    GameAudio.PlayMusic(MusicTrack.Fight);
                    GameAudio.PlayAmbience(true);
                    break;
            }
            if (state != GameState.Options && state != GameState.Paused)
                SideScrollCamera.SetShot(state == GameState.Title ? CameraShot.Attract : state == GameState.ClassSelect ? CameraShot.Showcase : CameraShot.Follow);
            player.Posing = state == GameState.ClassSelect;
        }

        void Update()
        {
            var input = player.Input;
            if (input.MutePressed)
            {
                GameAudio.ToggleMute();
                Toast(GameAudio.Muted ? "Sound off. Press M to turn it back on." : "Sound on");
            }

            switch (State)
            {
                case GameState.Title:
                    hud.TitleMenu.Move(input.NavigateY);
                    if (input.ConfirmPressed) hud.TitleMenu.Choose();
                    break;

                case GameState.ClassSelect:
                    int nav = input.NavigateX;
                    if (nav != 0) SelectClass((selectedClass + nav + playerClasses.Length) % playerClasses.Length);
                    if (input.ConfirmPressed) ConfirmClass();
                    else if (input.CancelPressed)
                    {
                        GameAudio.Play(Sfx.UiBack, 0.6f, 0f);
                        SetState(GameState.Title);
                    }
                    break;

                case GameState.Playing:
                case GameState.FloorCleared:
                    if (input.PausePressed && !riding) Pause();
                    else
                    {
                        TrackBoss();
                        UpdateArena();
                        TrackFireDrill();
                    }
                    break;

                case GameState.Paused:
                    hud.PauseMenu.Move(input.NavigateY);
                    if (input.RestartPressed) ConfirmRestart();
                    else if (input.SubmitPressed) hud.PauseMenu.Choose();
                    else if (input.PausePressed || input.CancelPressed) Resume();
                    break;

                case GameState.Options:
                    hud.OptionsMenu.Move(input.NavigateY);
                    int change = input.NavigateX;
                    if (change != 0) StepSetting(change);
                    if (input.SubmitPressed) hud.OptionsMenu.Choose();
                    else if (input.CancelPressed || input.PausePressed) CloseOptions();
                    break;

                case GameState.GameOver:
                    // Only once the termination letter is up, so a button mashed through the death doesn't skip it.
                    if (gameOverShown && (input.RestartPressed || input.ConfirmPressed)) Restart();
                    break;

                case GameState.Ending:
                    if (riding) break;
                    if (perkOffer != null && perkOffer.Count > 0)
                    {
                        int pick = input.NavigateX;
                        if (pick != 0) SelectPerk((perkChoice + pick + perkOffer.Count) % perkOffer.Count);
                    }
                    if (input.RestartPressed) ConfirmRestart();
                    else if (input.ConfirmPressed) ConfirmEnding();
                    break;
            }
        }

        // ------------------------------------------------------------------ menus

        void OnTitleChoice(int index)
        {
            if (State != GameState.Title) return;
            switch (index)
            {
                case 0: OpenClassSelect(); break;
                case 1: OpenOptions(); break;
                default: Quit(); break;
            }
        }

        void OnPauseChoice(int index)
        {
            if (State != GameState.Paused) return;
            switch (index)
            {
                case 0: Resume(); break;
                case 1: OpenOptions(); break;
                default: Restart(); break; // back to the lobby (title)
            }
        }

        void OnOptionsChoice(int index)
        {
            if (State != GameState.Options) return;
            if (index >= GameSettings.All.Count) CloseOptions();
            else CycleSetting(index);
        }

        void StepSetting(int direction)
        {
            int index = hud.OptionsMenu.Selected;
            if (index >= GameSettings.All.Count) return;
            GameSettings.Step(GameSettings.All[index], direction);
            hud.RefreshOptions();
            GameAudio.Play(Sfx.UiMove, 0.6f, 0f);
        }

        /// <summary>A click on a setting raises it and wraps round from the top, so mouse players can lower it too.</summary>
        void CycleSetting(int index)
        {
            var id = GameSettings.All[index];
            int before = GameSettings.Get(id);
            GameSettings.Step(id, 1);
            if (GameSettings.Get(id) == before) GameSettings.Step(id, -before); // at the top: wrap to the bottom
            hud.RefreshOptions();
            GameAudio.Play(Sfx.UiMove, 0.6f, 0f);
        }

        public void OpenOptions()
        {
            stateBeforeOptions = State;
            hud.OptionsMenu.Reset();
            GameAudio.Play(Sfx.UiConfirm, 0.6f, 0f);
            State = GameState.Options;
            hud.ShowScreen(GameState.Options);
        }

        void CloseOptions()
        {
            GameAudio.Play(Sfx.UiBack, 0.6f, 0f);
            State = stateBeforeOptions;
            hud.ShowScreen(State);
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------------ shift start

        /// <summary>Skips the menus straight into the shift (tests and quick-start).</summary>
        public void StartShiftWithClass(int classIndex)
        {
            if (playerClasses != null && playerClasses.Length > 0)
            {
                selectedClass = Mathf.Clamp(classIndex, 0, playerClasses.Length - 1);
                player.ApplyClass(playerClasses[selectedClass]);
            }
            BeginShift();
        }

        public void OpenClassSelect()
        {
            GameAudio.Play(Sfx.UiConfirm, 0.7f, 0f);
            if (playerClasses == null || playerClasses.Length == 0)
            {
                BeginShift();
                return;
            }
            hud.BuildClassCards(playerClasses);
            showcaseSpot = player.transform.position;
            SetState(GameState.ClassSelect);
            ShowcaseClass(selectedClass);
        }

        /// <summary>Highlights a department (keyboard, gamepad or mouse) and shows it off on the player.</summary>
        public void SelectClass(int index)
        {
            if (State != GameState.ClassSelect || index == selectedClass) return;
            selectedClass = Mathf.Clamp(index, 0, playerClasses.Length - 1);
            GameAudio.Play(Sfx.UiMove, 0.6f, 0f);
            ShowcaseClass(selectedClass);
        }

        void ShowcaseClass(int index)
        {
            player.ApplyClass(playerClasses[index]);
            hud.HighlightClass(index);
            // Back on the mark (a showcase swing must never walk them out of shot), three-quarter view toward
            // the camera, then a swing with the department's signature item, played on the spot.
            player.Attack.Interrupt();
            player.Motor.Stop();
            var controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            player.transform.position = showcaseSpot;
            if (controller != null) controller.enabled = true;
            player.Motor.FaceTowards(player.transform.position + new Vector3(0.6f, 0f, -1f));
            var demo = player.Progression.MoveFor(Combat.MoveSlot.Heavy)?.Clone();
            if (demo == null) return;
            demo.lunge = 0f;
            demo.moveMultiplier = 0f;
            demo.cooldown = 0f;
            player.Attack.TryAttack(demo);
        }

        public void ConfirmClass()
        {
            if (State == GameState.ClassSelect) BeginShift();
        }

        void BeginShift()
        {
            GameAudio.Play(Sfx.UiConfirm, 0.7f, 0f);
            lastClassIndex = selectedClass;
            ShiftStartTime = RunStartTime = Time.time;
            player.Attack.Interrupt();
            player.Motor.FaceTowards(player.transform.position + Vector3.right);
            player.SetControlEnabled(true);
            SetState(GameState.Playing);
            RecordBestFloor();
            string department = player.Class != null ? $"{player.Class.displayName} intern" : "New intern";
            hud.ShowBanner("9:00 AM, Monday", $"{department} on {FloorName}. Fight your way to the elevator.");
        }

        // ------------------------------------------------------------------ floors

        /// <summary>Re-stages the office as the next floor (instant; the elevator ride wraps this).</summary>
        public void NextFloor()
        {
            if (floorNumber >= TowerPlan.FloorCount) return;
            floorNumber++;
            EngagedBoss = null;
            Combo.Break();
            Combo.Window = ComboTracker.BaseWindow + player.Perks.ComboWindowBonus;
            perkOffer = null;
            hud.ShowPerkOffer(null);
            player.ResetFloorPerks();

            if (elevator != null) elevator.Close();
            TeleportPlayer(playerStart);
            player.Health.Heal(player.Health.Max * HealBetweenFloors);
            if (player.Ability != null) player.Ability.ResetCooldown();
            var plan = director.Stage(floorNumber, usedEvents);
            CurrentEvent = plan.floorEvent;
            drillEndsAt = 0f;

            ShiftStartTime = Time.time;
            player.SetControlEnabled(true);
            hud.RefreshFloor();
            SetState(GameState.Playing);
            RecordBestFloor();
            hud.SetEvent(CurrentEvent);
            if (CurrentEvent != FloorEvent.None)
            {
                hud.ShowBanner($"Floor {floorNumber}: {TowerPlan.EventName(CurrentEvent)}!", TowerPlan.EventDescription(CurrentEvent));
                GameAudio.Play(Sfx.BossIntro, 0.5f, 0f);
                if (CurrentEvent == FloorEvent.FireDrill)
                {
                    drillEndsAt = Time.time + plan.timer;
                    if (elevator != null) elevator.Open();
                }
                if (CurrentEvent == FloorEvent.AllHands) Toast($"Wave 1 of {plan.waveCount}");
            }
            else
            {
                hud.ShowBanner($"Floor {floorNumber}: {plan.name}",
                    plan.IsFinal ? "The CEO is waiting. Coffee won't save you now." : plan.tagline);
            }
        }

        /// <summary>Takes one of the offered perks (index into PerkOffer). Safe to call with no offer.</summary>
        public void ChoosePerk(int index)
        {
            if (perkOffer == null || perkOffer.Count == 0) return;
            var perk = perkOffer[Mathf.Clamp(index, 0, perkOffer.Count - 1)];
            player.Perks.Add(perk.id);
            perkOffer = null;
            GameAudio.Play(Sfx.Promote, 0.6f, 0f);
        }

        public void SelectPerk(int index)
        {
            if (perkOffer == null || perkOffer.Count == 0 || index == perkChoice) return;
            perkChoice = Mathf.Clamp(index, 0, perkOffer.Count - 1);
            hud.HighlightPerk(perkChoice);
            GameAudio.Play(Sfx.UiMove, 0.6f, 0f);
        }

        /// <summary>Leaves the elevator screen: take the benefit and ride up (or start over after the last floor).</summary>
        public void ConfirmEnding()
        {
            if (State != GameState.Ending || riding) return;
            if (floorNumber >= TowerPlan.FloorCount)
            {
                Restart();
                return;
            }
            ChoosePerk(perkChoice);
            StartCoroutine(RideUp());
        }

        IEnumerator RideUp()
        {
            riding = true;
            int from = floorNumber;
            yield return hud.ElevatorRide(from, from + 1, () =>
            {
                NextFloor();
                player.SetControlEnabled(false); // hold still until the doors open
            });
            player.SetControlEnabled(true);
            riding = false;
        }

        void TrackFireDrill()
        {
            if (drillEndsAt <= 0f || Time.time < drillEndsAt) return;
            drillEndsAt = 0f;
            if (State == GameState.FloorCleared) return;
            // Too slow: the elevator locks until everyone is dealt with.
            if (elevator != null) elevator.Close();
            hud.ShowBanner("Drill over", "The elevator is locked again. Clear the floor.");
            GameAudio.Play(Sfx.GameOver, 0.5f, 0f);
        }

        void TeleportPlayer(Vector3 position)
        {
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.transform.position = position;
            if (cc != null) cc.enabled = true;
            player.Motor.Stop();
        }

        void RecordBestFloor()
        {
            if (floorNumber > BestFloor) PlayerPrefs.SetInt(BestFloorKey, floorNumber);
        }

        // ------------------------------------------------------------------ bosses

        void TrackBoss()
        {
            if (EngagedBoss != null && (EngagedBoss.Health.IsDead || !EngagedBoss.isActiveAndEnabled)) EngagedBoss = null;
            if (EngagedBoss != null) return;
            foreach (var enemy in EnemyBrain.Active)
            {
                if (enemy.Boss == BossRank.None || !enemy.IsAggro) continue;
                EngagedBoss = enemy;
                BossIntro(enemy);
                break;
            }
        }

        /// <summary>Letterbox, slow motion and a camera pull onto the boss, with a name card.</summary>
        void BossIntro(EnemyBrain boss)
        {
            GameAudio.Play(Sfx.BossIntro, 1f, 0f);
            if (boss.Boss == BossRank.FinalBoss) GameAudio.PlayMusic(MusicTrack.Boss);
            hud.ShowBossIntro(boss.DisplayName, BossSubtitle(boss), 2.2f);
            SideScrollCamera.FocusOn(boss.transform, 1.7f);
            SideScrollCamera.Shake(0.3f, 0.4f);
            SlowMotion(1.6f, 0.3f);
            Toast($"{boss.DisplayName}: {BossIntroLine(boss)}");
        }

        static readonly Dictionary<string, string> BossTitles = new Dictionary<string, string>
        {
            ["Middle Manager"] = "Let's circle back to your beatdown",
            ["HR Director"] = "Head of Human Remains",
            ["Senior Counsel"] = "Bills in six-minute beatings",
            ["VP of Synergy"] = "Vice President of Synergy",
            ["THE CEO"] = "Chief Executive Obliterator",
        };

        /// <summary>The line under a boss's name on the intro card and boss bar.</summary>
        public static string BossSubtitle(EnemyBrain boss)
        {
            if (boss == null) return "";
            if (BossTitles.TryGetValue(boss.DisplayName, out var title)) return title;
            return boss.WorkerClass != null ? boss.WorkerClass.tagline : "";
        }

        static string BossIntroLine(EnemyBrain boss) =>
            boss.Boss == BossRank.FinalBoss
                ? "\"So. You're the intern everyone's been talking about.\""
                : "\"I'm going to need you to come in on Saturday.\"";

        // ------------------------------------------------------------------ combat events

        void OnAnyDamaged(Health target, DamageInfo info)
        {
            if (!IsPlaying) return;
            if (target == player.Health)
            {
                Combo.Break();
                return;
            }
            if (info.source == player.gameObject && Combo.RegisterHit(Time.time))
            {
                GameAudio.Play(Sfx.ComboUp, 0.7f, 0f);
                hud.PunchCombo();
            }
        }

        void OnEnemyDefeated(EnemyBrain enemy)
        {
            Knockouts++;
            float bonus = Combo.XpMultiplier(Time.time);
            int baseXp = enemy.WorkerClass != null ? enemy.WorkerClass.xpReward : 10;
            int xp = Mathf.RoundToInt(baseXp * bonus * player.Perks.XpMultiplier);
            player.Progression.AddXp(xp);
            string combo = bonus > 1.01f ? " (combo x" + bonus.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + ")" : "";
            Toast($"{enemy.DisplayName} clocked out. +{xp} XP{combo}");

            if (enemy.Boss != BossRank.None)
            {
                if (enemy == EngagedBoss) EngagedBoss = null;
                SlowMotion(enemy.Boss == BossRank.FinalBoss ? 1.6f : 0.8f, 0.2f);
                if (enemy.Boss == BossRank.FinalBoss) GameAudio.PlayMusic(MusicTrack.Fight);
            }

            // Hostile Takeover perk: knockouts burst and hurt anyone close by.
            float burst = player.Perks.BurstDamage;
            if (burst > 0f) StartCoroutine(Burst(enemy.transform.position, burst));

            // All-Hands: the next wave files in when this one is down.
            if (State == GameState.Playing && EnemyBrain.Active.Count == 0 && director.HasPendingWave)
            {
                director.SpawnNextWave(player.transform.position);
                int total = director.Current != null ? director.Current.waveCount : 1;
                hud.ShowBanner($"Wave {director.Wave + 1} of {total}", "More attendees are filing in.");
                GameAudio.Play(Sfx.BossIntro, 0.5f, 0f);
                return;
            }

            if (State == GameState.Playing && EnemyBrain.Active.Count == 0)
            {
                drillEndsAt = 0f;
                SetState(GameState.FloorCleared);
                if (elevator != null) elevator.Open();
                hud.ShowBanner("Floor cleared",
                    floorNumber >= TowerPlan.FloorCount
                        ? "The board has been dissolved. Take the elevator to the corner office."
                        : "The elevator is open. Head right and go up.");
            }
        }

        IEnumerator Burst(Vector3 at, float damage)
        {
            yield return new WaitForSeconds(0.12f);
            HitEffects.Burst(at + Vector3.up, Vector3.up, new Color(1f, 0.42f, 0.29f), 24, 9f, 0.1f);
            HitEffects.Flash(at + Vector3.up, new Color(1f, 0.55f, 0.3f), 2.6f);
            GameAudio.Play(Sfx.HitHeavy, 0.7f);
            foreach (var other in EnemyBrain.Active.ToArray())
            {
                Vector3 d = other.transform.position - at;
                d.y = 0f;
                if (d.magnitude > 2.6f) continue;
                other.Health.TakeDamage(new DamageInfo
                {
                    amount = damage,
                    knockback = d.normalized * 8f + Vector3.up * 4f,
                    source = player.gameObject,
                    hitStop = 0.03f,
                    heavy = true,
                });
            }
        }

        void OnPlayerDied() => StartCoroutine(GameOverAfterDelay());

        IEnumerator GameOverAfterDelay()
        {
            State = GameState.GameOver;
            EngagedBoss = null;
            GameAudio.PlayMusic(MusicTrack.None);
            GameAudio.PlayAmbience(false);
            GameAudio.Play(Sfx.GameOver, 1f, 0f);
            string who = player.Class != null ? player.Class.displayName : "employee";
            hud.SetGameOverText(
                $"Dear {who},\n\n" +
                $"After careful review, your position on floor {floorNumber} ({FloorName}) has been eliminated, effective immediately.\n\n" +
                $"Before leaving you clocked out {Plural(Knockouts, "coworker")}, reached a best combo of {Combo.Best} " +
                $"and rose to the rank of {player.Progression.Rank}.\n\n" +
                "Please return your badge and stapler to reception on your way out.");
            yield return new WaitForSecondsRealtime(1.2f);
            hud.ShowScreen(GameState.GameOver);
            gameOverShown = true;
        }

        public void OnElevatorReached()
        {
            // Normally the floor must be cleared; during a fire drill the open elevator is the point.
            bool drillExit = State == GameState.Playing && DrillTimeLeft > 0f;
            if (State != GameState.FloorCleared && !drillExit) return;
            // Running for it skips everyone still on the floor (and their XP); HR only hands out benefits for a cleared floor.
            bool skipped = drillExit && EnemyBrain.Active.Count > 0;
            if (drillExit)
            {
                drillEndsAt = 0f;
                foreach (var e in EnemyBrain.Active.ToArray()) e.gameObject.SetActive(false); // they're still at the muster point
            }
            player.SetControlEnabled(false);
            bool final = floorNumber >= TowerPlan.FloorCount;
            string stats =
                $"Rank reached: {player.Progression.Rank} (level {player.Progression.Level}).  " +
                $"Floor clocked at {GameHud.ClockString(Time.time - ShiftStartTime)}.\n" +
                $"{Plural(Knockouts, "knockout")}, best combo {Combo.Best}.";
            if (final)
            {
                PlayerPrefs.SetInt(ChampionKey, 1);
                hud.SetEndingText("You are the CEO",
                    $"{stats}\nWhole climb: {GameHud.DurationString(Time.time - RunStartTime)}.\n\n" +
                    "Monday-morning intern to corner office. The view is incredible.\nYour first act as CEO: everyone gets Fridays off.",
                    "Enter|Start a new career");
                hud.ShowPerkOffer(null);
            }
            else
            {
                perkOffer = skipped ? new List<Perk>() : PerkLibrary.Roll(runRng, 3, player.Perks);
                if (!skipped && ForcePerk.HasValue && perkOffer.Count > 0 && !perkOffer.Exists(p => p.id == ForcePerk.Value))
                    perkOffer[0] = PerkLibrary.Get(ForcePerk.Value);
                ForcePerk = null;
                perkChoice = 0;
                string benefits = skipped
                    ? "You skipped the floor, so no benefits package this time."
                    : "HR has a benefits package for you. Pick one.";
                hud.SetEndingText(drillExit ? "Made it out" : "Going up",
                    $"{stats}\n{(floorNumber == 1 ? nextFloorTeaser : TowerPlan.NextFloorTeaser(floorNumber))}\n\n{benefits}",
                    perkOffer.Count > 0
                        ? $"A D|Choose a benefit;Enter|Take it and ride up;R|Start over"
                        : $"Enter|Ride up to floor {floorNumber + 1};R|Start over");
                hud.ShowPerkOffer(perkOffer, player.Perks);
                hud.HighlightPerk(0);
            }
            SetState(GameState.Ending);
            GameAudio.Play(final ? Sfx.Promote : Sfx.ElevatorDing, 0.9f, 0f);
        }

        static string Plural(int n, string noun) => n == 1 ? $"1 {noun}" : $"{n} {noun}s";

        // ------------------------------------------------------------------ arena

        /// <summary>Fences the player and the camera in while coworkers are engaged; opens up when they're down.</summary>
        void UpdateArena()
        {
            var cam = SideScrollCamera.Main;
            Arena.BeginFrame();
            foreach (var enemy in EnemyBrain.Active)
                if (enemy.IsAggro && !enemy.Health.IsDead) Arena.AddEngaged(enemy.transform.position.x);
            // Fire drills are a race for the elevator; nothing may wall the player in.
            bool allowed = State == GameState.Playing && CurrentEvent != FloorEvent.FireDrill && !riding;
            float levelMin = cam != null ? cam.LevelMinX - 1f : float.NegativeInfinity;
            float levelMax = cam != null ? cam.LevelMaxX + 1f : float.PositiveInfinity;
            Arena.EndFrame(player.transform.position.x, allowed, levelMin, levelMax);
            if (Arena.Locked)
            {
                player.Motor.SetArena(Arena.Min, Arena.Max);
                // Keep the arena's edges in frame: the camera centre stays a little inside the walls.
                if (cam != null) cam.SetBounds(Arena.Min + 4f, Arena.Max - 4f);
            }
            else
            {
                player.Motor.ClearArena();
                if (cam != null) cam.ResetBounds();
            }
        }

        // ------------------------------------------------------------------ pause

        /// <summary>Opens the pause menu (same as pressing Esc during play).</summary>
        public void PauseGame()
        {
            if (IsPlaying && !riding) Pause();
        }

        void Pause()
        {
            stateBeforePause = State;
            State = GameState.Paused;
            Time.timeScale = 0f;
            hud.PauseMenu.Reset();
            hud.ShowScreen(GameState.Paused);
            GameAudio.SetMusicDucked(true);
            GameAudio.Play(Sfx.UiMove, 0.7f, 0f);
        }

        void Resume()
        {
            State = stateBeforePause;
            if (timeRoutine != null) StopCoroutine(timeRoutine);
            Time.timeScale = 1f;
            hitStopping = false;
            hud.ShowScreen(State);
            GameAudio.SetMusicDucked(false);
            GameAudio.Play(Sfx.UiConfirm, 0.6f, 0f);
        }

        public void Toast(string message)
        {
            if (hud != null) hud.Toast(message);
        }

        /// <summary>Seconds a first R press stays armed; a second press inside it abandons the run.</summary>
        public const float RestartConfirmWindow = 2f;
        float restartArmedUntil = -1f;
        bool gameOverShown;

        /// <summary>R mid-run asks first: the second press within <see cref="RestartConfirmWindow"/> s starts over.</summary>
        void ConfirmRestart()
        {
            if (Time.unscaledTime <= restartArmedUntil)
            {
                Restart();
                return;
            }
            restartArmedUntil = Time.unscaledTime + RestartConfirmWindow;
            Toast("Press R again to quit this career and start over.");
            GameAudio.Play(Sfx.UiMove, 0.6f, 0f);
        }

        void Restart()
        {
            Time.timeScale = 1f;
            // GameAudio outlives the scene: don't carry the pause menu's muffled music into the new run.
            GameAudio.SetMusicDucked(false);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        // ------------------------------------------------------------------ time effects

        /// <summary>Freeze-frame on impact; makes hits feel heavy. Overlapping requests keep the longest.</summary>
        public static void HitStop(float duration)
        {
            if (Instance == null || duration <= 0f) return;
            if (Instance.hitStopping)
            {
                Instance.timeEffectUntil = Mathf.Max(Instance.timeEffectUntil, Time.unscaledTime + duration);
                return;
            }
            Instance.timeRoutine = Instance.StartCoroutine(Instance.TimeScaleRoutine(duration, 0.05f));
        }

        /// <summary>Dramatic slow-down, e.g. when a boss goes down.</summary>
        public static void SlowMotion(float duration, float scale)
        {
            if (Instance == null) return;
            if (Instance.timeRoutine != null) Instance.StopCoroutine(Instance.timeRoutine);
            Instance.hitStopping = false;
            Instance.timeRoutine = Instance.StartCoroutine(Instance.TimeScaleRoutine(duration, scale));
        }

        IEnumerator TimeScaleRoutine(float duration, float scale)
        {
            hitStopping = true;
            timeEffectUntil = Time.unscaledTime + duration;
            if (State != GameState.Paused && State != GameState.Options) Time.timeScale = scale;
            while (Time.unscaledTime < timeEffectUntil) yield return null;
            if (State != GameState.Paused && State != GameState.Options) Time.timeScale = 1f;
            hitStopping = false;
        }
    }
}
