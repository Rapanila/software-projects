using System;
using NineToFight.CameraRig;
using NineToFight.Characters;
using NineToFight.Combat;
using NineToFight.Core;
using NineToFight.Game;
using NineToFight.Progression;
using UnityEngine;

namespace NineToFight.Player
{
    /// <summary>The tired office worker: routes input into movement, attacks and dodges.</summary>
    [RequireComponent(typeof(CharacterMotor2_5D), typeof(AttackExecutor), typeof(Health))]
    [RequireComponent(typeof(PlayerInputReader), typeof(PlayerProgression))]
    public class PlayerController : MonoBehaviour
    {
        public static PlayerController Instance { get; private set; }

        [Header("Dodge")]
        [SerializeField] float dodgeSpeed = 13f;
        [SerializeField] float dodgeDuration = 0.2f;
        [SerializeField] float dodgeCooldown = 0.55f;
        [SerializeField] float dodgeInvulnerability = 0.3f;

        /// <summary>Gap after the i-frames run out before the next dodge, so dodging can never chain into permanent invulnerability.</summary>
        public const float DodgeVulnerableGap = 0.15f;

        /// <summary>Seconds between dodges: the department's cooldown, shortened by Flex Time, never below the i-frames plus a gap.</summary>
        public float DodgeCooldown => DodgeCooldownFor(dodgeCooldown, Perks.DodgeCooldownMultiplier, dodgeInvulnerability);

        public static float DodgeCooldownFor(float classCooldown, float perkMultiplier, float invulnerability) =>
            Mathf.Max(classCooldown * perkMultiplier, invulnerability + DodgeVulnerableGap);
        public float DodgeInvulnerability => dodgeInvulnerability;

        [Header("Hit Reaction")]
        [SerializeField] float hitStun = 0.3f;

        bool controlEnabled;
        float stunnedUntil;
        float nextDodgeTime;

        /// <summary>
        /// Seconds an attack press is remembered while the body is busy: it fires the moment the current move
        /// ends or opens its cancel window, so a press slightly early isn't lost.
        /// </summary>
        public const float AttackBufferWindow = 0.2f;
        FightingMove bufferedMove;
        float bufferedAt;

        public CharacterMotor2_5D Motor { get; private set; }
        public AttackExecutor Attack { get; private set; }
        public Health Health { get; private set; }
        public PlayerInputReader Input { get; private set; }
        public PlayerProgression Progression { get; private set; }
        public CharacterVisual Visual { get; private set; }
        public PlayerClass Class { get; private set; }
        public HeldItemDisplay HeldItem { get; private set; }
        public AbilityCaster Ability { get; private set; }
        /// <summary>Company benefits picked at the elevator this climb.</summary>
        public PerkSet Perks { get; } = new PerkSet();
        /// <summary>Department select: hold a fighting stance for the camera.</summary>
        public bool Posing { get; set; }
        bool parachuteUsed;

        public event Action Died;
        public event Action Dodged;

        void Awake()
        {
            Instance = this;
            Motor = GetComponent<CharacterMotor2_5D>();
            Attack = GetComponent<AttackExecutor>();
            Health = GetComponent<Health>();
            Input = GetComponent<PlayerInputReader>();
            Progression = GetComponent<PlayerProgression>();
            Visual = GetComponentInChildren<CharacterVisual>();
            HeldItem = GetComponentInChildren<HeldItemDisplay>();
            Ability = GetComponent<AbilityCaster>();

            Health.Damaged += OnDamaged;
            Health.Died += OnDied;
            Health.PreventDeath = TryParachute;
            Perks.Added += OnPerkAdded;
            Attack.HitLanded += (_, move) => SideScrollCamera.Shake(0.06f + move.damage * 0.004f, 0.12f);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Applies a department's stats, look and moves. Call before the shift starts.</summary>
        public void ApplyClass(PlayerClass playerClass)
        {
            if (playerClass == null) return;
            Class = playerClass;
            Motor.moveSpeed = playerClass.moveSpeed;
            Motor.depthSpeed = playerClass.depthSpeed;
            Motor.jumpHeight = playerClass.jumpHeight;
            Attack.DamageMultiplier = playerClass.damageMultiplier;
            dodgeCooldown = playerClass.dodgeCooldown;
            Progression.Configure(playerClass.BuildMoveSet(), playerClass.maxHealth, playerClass.healthPerLevel);
            if (Visual != null)
            {
                Visual.SetColors(playerClass.shirtColor, playerClass.tieColor);
                var look = Visual.GetComponent<CharacterLook>();
                if (look != null) look.Apply(CharacterLook.Seed(playerClass.name), playerClass.name == "SalesRep", playerClass.outfit);
            }
            if (HeldItem != null) HeldItem.SetIdleProp(playerClass.lightAttack.prop);
            if (Ability != null) Ability.ResetCooldown();
            ApplyPerks();
        }

        void OnPerkAdded(Perk perk)
        {
            if (perk.id == PerkId.DentalPlan) Progression.ExtraMaxHealth = Perks.MaxHealthBonus;
            ApplyPerks();
        }

        /// <summary>Pushes perk totals into the systems they modify.</summary>
        void ApplyPerks()
        {
            Attack.PerkDamageMultiplier = Perks.DamageMultiplier;
            Attack.CritChance = Perks.CritChance;
            Attack.Lifesteal = Perks.Lifesteal;
            Motor.PerkSpeedMultiplier = Perks.SpeedMultiplier;
            if (Ability != null) Ability.CooldownMultiplier = Perks.AbilityCooldownMultiplier;
        }

        /// <summary>New floor: once-per-floor perks recharge.</summary>
        public void ResetFloorPerks() => parachuteUsed = false;

        bool TryParachute()
        {
            if (!Perks.HasParachute || parachuteUsed) return false;
            parachuteUsed = true;
            Health.Heal(Health.Max * 0.35f);
            Health.MakeInvulnerable(2f);
            if (Visual != null) Visual.Flash(new Color(1f, 0.85f, 0.35f), 0.6f);
            GameFlow.Instance?.Toast("GOLDEN PARACHUTE!  Back on your feet.");
            return true;
        }

        void TryAbility()
        {
            if (Ability == null || Ability.Ability == null) return;
            if (!Ability.TryCast() && !Ability.IsReady)
                GameFlow.Instance?.Toast($"{Ability.Ability.displayName} ready in {Mathf.CeilToInt(Ability.CooldownRemaining)}s");
        }

        void Start()
        {
            // Without a chosen class, carry the default light attack's item.
            if (Class == null && HeldItem != null) HeldItem.SetIdleProp(Progression.MoveFor(MoveSlot.Light)?.prop);
        }

        public void SetControlEnabled(bool enabled)
        {
            controlEnabled = enabled;
            if (!enabled) Motor.SetMoveInput(Vector2.zero);
        }

        void Update()
        {
            if (Health.IsDead) return;
            if (Visual != null)
            {
                Visual.InCombat = Posing || (controlEnabled && EnemyNearby(7f));
                // The Shield ability is a held block.
                Visual.Blocking = Ability != null && Ability.IsActive && Ability.Ability != null && Ability.Ability.kind == AbilityKind.Shield;
            }

            bool stunned = Time.time < stunnedUntil;
            // The pause menu reads the same keys (and mouse clicks); none of them should swing or cast.
            bool paused = GameFlow.IsPaused;
            Motor.CanMove = controlEnabled && !stunned && !paused;
            if (!controlEnabled || stunned || paused)
            {
                Motor.SetMoveInput(Vector2.zero);
                return;
            }

            Motor.SetMoveInput(Input.Move);
            if (Input.JumpPressed && !Attack.IsAttacking) Motor.RequestJump();
            if (Input.DodgePressed && Time.time >= nextDodgeTime && Motor.IsGrounded) Dodge();
            if (Input.AbilityPressed) TryAbility();
            if (Motor.IsDashing) return;

            int hotbarSlot = Input.HotbarPressed;
            if (hotbarSlot >= 0) TryHotbar(hotbarSlot);
            else if (Input.LightPressed) RequestAttack(Progression.MoveFor(MoveSlot.Light));
            else if (Input.HeavyPressed) RequestAttack(Progression.MoveFor(MoveSlot.Heavy));
            else if (Input.SpecialPressed) TryLearnedMove(MoveSlot.Special);
            else if (Input.SuperPressed) TryLearnedMove(MoveSlot.Super);
            else TickAttackBuffer();
        }

        /// <summary>
        /// Starts a move now if the body is free (or the current move can be cancelled into it); otherwise
        /// remembers it for <see cref="AttackBufferWindow"/> seconds. The newest press replaces an older one.
        /// Returns true if it started straight away.
        /// </summary>
        public bool RequestAttack(FightingMove move)
        {
            if (move == null) return false;
            if (Attack.TryAttack(move))
            {
                bufferedMove = null;
                return true;
            }
            bufferedMove = move;
            bufferedAt = Time.time;
            return false;
        }

        /// <summary>The buffered press waiting to fire (null if none, or if it has expired).</summary>
        public FightingMove BufferedAttack => bufferedMove != null && Time.time - bufferedAt <= AttackBufferWindow ? bufferedMove : null;

        /// <summary>Fires a buffered press as soon as it can; drops it once it's older than the window.</summary>
        public void TickAttackBuffer()
        {
            if (bufferedMove == null) return;
            if (Time.time - bufferedAt > AttackBufferWindow) bufferedMove = null;
            else if (Attack.TryAttack(bufferedMove)) bufferedMove = null;
        }

        bool EnemyNearby(float range)
        {
            foreach (var enemy in Enemies.EnemyBrain.Active)
                if (enemy.IsAggro && (enemy.transform.position - transform.position).sqrMagnitude < range * range) return true;
            return false;
        }

        /// <summary>Number keys 1-9 and 0 fire the move in that hotbar slot.</summary>
        public void TryHotbar(int index)
        {
            var state = Progression.State;
            var move = state.HotbarMove(index);
            string key = ProgressionState.HotbarKey(index);
            if (move == null)
            {
                GameFlow.Instance?.Toast($"Slot {key} is empty. Collect more moves to fill it.");
                return;
            }
            if (!state.IsUnlocked(move))
            {
                GameFlow.Instance?.Toast($"{move.displayName} unlocks at {CareerLadder.RankTitle(move.unlockLevel)} (Lv {move.unlockLevel})");
                return;
            }
            RequestAttack(move);
        }

        void TryLearnedMove(MoveSlot slot)
        {
            var move = Progression.MoveFor(slot);
            if (move == null)
            {
                var next = Progression.State.NextLocked(slot);
                GameFlow.Instance?.Toast(next != null
                    ? $"{next.displayName} unlocks at {CareerLadder.RankTitle(next.unlockLevel)} (Lv {next.unlockLevel})"
                    : "You haven't learned that yet.");
                return;
            }
            RequestAttack(move);
        }

        void Dodge()
        {
            // Dodge the way you're pushing, or the way you're facing if standing still.
            Vector3 direction = CharacterMotor2_5D.TryInputDirection(Input.Move, out var inputDirection)
                ? inputDirection
                : Motor.FacingDirection;
            Attack.Interrupt();
            bufferedMove = null;
            Motor.Dash(direction, dodgeSpeed, dodgeDuration);
            Health.MakeInvulnerable(dodgeInvulnerability);
            if (Visual != null) Visual.Flash(new Color(0.75f, 0.9f, 1f), dodgeDuration);
            nextDodgeTime = Time.time + DodgeCooldown;
            Dodged?.Invoke();
        }

        void OnDamaged(DamageInfo info)
        {
            Attack.Interrupt();
            bufferedMove = null;
            Motor.AddImpulse(info.knockback);
            stunnedUntil = Time.time + hitStun;
            if (Visual != null)
            {
                Visual.Flash(new Color(1f, 0.45f, 0.4f), 0.1f);
                Visual.PlayHitReaction(info.knockback, hitStun);
            }
            GameFlow.HitStop(info.hitStop);
            SideScrollCamera.Shake(0.25f, 0.18f);
        }

        void OnDied()
        {
            SetControlEnabled(false);
            Motor.Stop();
            Motor.CanMove = false;
            if (Visual != null) Visual.PlayKnockedOut();
            Died?.Invoke();
        }
    }
}
