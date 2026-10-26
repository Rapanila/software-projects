using System;
using System.Collections;
using System.Collections.Generic;
using NineToFight.CameraRig;
using NineToFight.Characters;
using NineToFight.Combat;
using NineToFight.Core;
using NineToFight.Game;
using NineToFight.Player;
using UnityEngine;

namespace NineToFight.Enemies
{
    /// <summary>
    /// Coworker AI. Every role notices the player, lines up in their depth lane and telegraphs before it hits;
    /// on top of that, Throwers keep their distance and lob supplies, Guards block light hits from the front,
    /// Supports heal and rally their colleagues, and Chargers bull-rush and are left winded if they miss.
    /// </summary>
    [RequireComponent(typeof(CharacterMotor2_5D), typeof(AttackExecutor), typeof(Health))]
    public class EnemyBrain : MonoBehaviour
    {
        public static readonly List<EnemyBrain> Active = new List<EnemyBrain>();
        public static event Action<EnemyBrain> AnyDefeated;
        /// <summary>Raised with a short callout for the floating text ("BLOCKED", "GUARD BREAK!", "WINDED"...).</summary>
        public static event Action<EnemyBrain, string, Color> Callout;

        static readonly Color TelegraphColor = new Color(1f, 0.6f, 0.15f);
        static readonly Color BossTelegraphColor = new Color(1f, 0.15f, 0.1f);

        /// <summary>
        /// Attack slots: how many coworkers may press the attack at once; the rest circle and wait their turn.
        /// Every attack (swing, throw, charge, boss slam) needs a slot. Bosses always get one, but it counts.
        /// </summary>
        public const int MaxAttackers = 2;
        static readonly List<EnemyBrain> SlotHolders = new List<EnemyBrain>();
        /// <summary>Coworkers currently holding an attack slot.</summary>
        public static IReadOnlyList<EnemyBrain> AttackSlots { get { PruneSlots(); return SlotHolders; } }
        /// <summary>A slot held this long without swinging goes back to the pool.</summary>
        const float SlotPatience = 3f;
        const float StandoffDistance = 3.2f;

        [SerializeField] WorkerClass workerClass;
        [SerializeField] float hitStun = 0.4f;
        [Tooltip("How closely the enemy must match the player's depth before attacking.")]
        [SerializeField] float laneTolerance = 0.45f;
        [SerializeField] float separationRadius = 1.1f;
        [SerializeField] float corpseLifetime = 2.5f;

        bool aggro;
        float stunnedUntil;
        float nextAttackTime;
        FightingMove attackMove;
        FightingMove bossSlam;
        float nextSlamTime;
        float circleSign = 1f;

        // Role state
        FightingMove throwMove, chargeMove;
        float nextSpecialTime;
        float guardBrokenUntil, nextTurnTime;
        float buffedUntil;
        bool chargeHit;
        Coroutine roleRoutine;
        float slotTakenAt, nextSlotTime;

        public WorkerClass WorkerClass => workerClass;
        public CharacterMotor2_5D Motor { get; private set; }
        public AttackExecutor Attack { get; private set; }
        public Health Health { get; private set; }
        public CharacterVisual Visual { get; private set; }
        public HeldItemDisplay HeldItem { get; private set; }
        public bool IsStunned => Time.time < stunnedUntil;
        public bool IsAggro => aggro;
        public Game.BossRank Boss { get; private set; }
        public string DisplayName => workerClass != null ? workerClass.displayName : "Coworker";
        public EnemyRole Role { get; private set; } = EnemyRole.Brawler;
        public bool IsElite { get; set; }
        public bool IsBuffed => Time.time < buffedUntil;
        public bool IsGuarding => Role == EnemyRole.Guard && Time.time >= guardBrokenUntil && !Attack.IsAttacking && !IsStunned;
        public bool HasAttackSlot => SlotHolders.Contains(this);
        /// <summary>Committed to an attack that a hit can't stop: the boss slam.</summary>
        public bool IsArmored => Attack.IsAttacking && bossSlam != null && Attack.Current == bossSlam;

        static readonly Color BlockColor = new Color(0.7f, 0.85f, 1f);
        static readonly Color RallyColor = new Color(0.95f, 0.55f, 0.75f);

        /// <summary>Freezes this coworker in place (e.g. IT Support's Blue Screen).</summary>
        public void Stun(float duration, Color tint)
        {
            if (Health.IsDead) return;
            aggro = true;
            Attack.Interrupt();
            Motor.SetMoveInput(Vector2.zero);
            stunnedUntil = Mathf.Max(stunnedUntil, Time.time + duration);
            if (Visual != null) Visual.Flash(tint, duration);
        }

        void Awake()
        {
            Motor = GetComponent<CharacterMotor2_5D>();
            Attack = GetComponent<AttackExecutor>();
            Health = GetComponent<Health>();
            Visual = GetComponentInChildren<CharacterVisual>();
            HeldItem = GetComponentInChildren<HeldItemDisplay>();

            ApplyClass();
            Health.Damaged += OnDamaged;
            Health.Died += OnDied;
            Health.ModifyIncoming = GuardIncoming;
            Attack.HitLanded += (_, move) => { if (move == chargeMove) chargeHit = true; };
            // One swing per turn: give the slot back so the next coworker can step in.
            Attack.AttackEnded += () => ReleaseSlot(UnityEngine.Random.Range(0.6f, 1.2f));
            Attack.AttackStarted += move =>
            {
                bool slam = move == bossSlam;
                if (Visual != null) Visual.Flash(slam ? BossTelegraphColor : TelegraphColor, move.startup * 0.7f);
                if (slam) GameFlow.Instance?.Toast($"{DisplayName.ToUpperInvariant()}: \"{SlamQuote()}\"");
            };
            circleSign = UnityEngine.Random.value < 0.5f ? -1f : 1f;
        }

        void OnEnable() => Active.Add(this);
        void OnDisable()
        {
            Active.Remove(this);
            ReleaseSlot(0f);
        }

        public void Configure(WorkerClass value)
        {
            workerClass = value;
            ApplyClass();
        }

        /// <summary>Spawned-at-runtime setup: class plus boss behaviour.</summary>
        public void Configure(WorkerClass value, Game.BossRank boss)
        {
            Boss = boss;
            Configure(value);
            if (boss == Game.BossRank.None) return;
            // Bosses get a telegraphed leaping slam on top of their normal swing.
            bossSlam = attackMove.Clone();
            bossSlam.id = attackMove.id + "_slam";
            bossSlam.displayName = boss == Game.BossRank.FinalBoss ? "Hostile Takeover" : "Restructuring";
            bossSlam.damage *= 1.5f;
            bossSlam.startup = 0.75f;
            bossSlam.lunge = 11f;
            bossSlam.radius *= 1.5f;
            bossSlam.knockback *= 1.4f;
            bossSlam.knockUp = 6f;
            bossSlam.hitStop = 0.1f;
            bossSlam.style = AttackStyle.Stamp;
            nextSlamTime = Time.time + 3f;
        }

        void ApplyClass()
        {
            if (workerClass == null) return;
            Health.SetMax(workerClass.maxHealth, true);
            Motor.moveSpeed = workerClass.moveSpeed;
            Motor.depthSpeed = workerClass.moveSpeed * 0.75f;
            transform.localScale = Vector3.one * workerClass.scale;
            attackMove = workerClass.attack.Clone();
            attackMove.id = "enemy_" + workerClass.name;
            attackMove.cooldown = 0f;
            if (Visual != null) Visual.SetColors(workerClass.shirtColor, workerClass.tieColor);
            // Same job title, different person: vary looks per coworker; management wears suits.
            var look = Visual != null ? Visual.GetComponent<CharacterLook>() : null;
            if (look != null) look.Apply(LookSeed(), workerClass.tier >= 3 || Boss != Game.BossRank.None);
            if (HeldItem != null) HeldItem.SetIdleProp(attackMove.prop);
            BuildRoleMoves();
        }

        /// <summary>
        /// Same job title, same spot, same floor: same person, every run. Varies with where they stand and
        /// the floor so a room of Interns isn't a room of twins.
        /// </summary>
        int LookSeed()
        {
            var p = transform.position;
            int floor = GameFlow.Instance != null ? GameFlow.Instance.Floor : 1;
            unchecked
            {
                return CharacterLook.Seed(workerClass.displayName) ^ (Mathf.RoundToInt(p.x * 4f) * 73856093) ^
                       (Mathf.RoundToInt(p.z * 4f) * 19349663) ^ (floor * 83492791);
            }
        }

        void BuildRoleMoves()
        {
            // Bosses keep their own pattern (swing + slam); everyone else fights to their role.
            Role = Boss != Game.BossRank.None ? EnemyRole.Brawler : workerClass.ResolvedRole;
            nextSpecialTime = Time.time + UnityEngine.Random.Range(1.5f, 3f);

            throwMove = attackMove.Clone();
            throwMove.id = attackMove.id + "_throw";
            throwMove.style = AttackStyle.Toss;
            throwMove.damage = 0f;
            throwMove.radius = 0.01f;
            throwMove.startup = 0.5f;
            throwMove.active = 0.08f;
            throwMove.recovery = 0.35f;
            throwMove.lunge = 0f;

            chargeMove = attackMove.Clone();
            chargeMove.id = attackMove.id + "_charge";
            chargeMove.style = AttackStyle.Lunge;
            chargeMove.damage = attackMove.damage * 1.3f;
            chargeMove.reach = 0.7f;
            chargeMove.radius = 0.85f;
            chargeMove.knockback = 13f;
            chargeMove.knockUp = 3f;
            chargeMove.startup = 0.6f;
            chargeMove.active = 0.5f;
            chargeMove.recovery = 0.25f;
            chargeMove.lunge = 0f;
            chargeMove.hitStop = 0.09f;
        }

        void Update()
        {
            if (Health.IsDead || workerClass == null) return;

            var player = PlayerController.Instance;
            bool canAct = GameFlow.IsPlaying && player != null && !player.Health.IsDead;
            if (Visual != null)
            {
                Visual.InCombat = aggro && canAct && !IsStunned;
                Visual.Blocking = Visual.InCombat && IsGuarding;
            }
            if (!canAct || Time.time < stunnedUntil || Attack.IsAttacking)
            {
                if (!canAct || Time.time < stunnedUntil) ReleaseSlot(0.4f);
                Motor.SetMoveInput(Vector2.zero);
                return;
            }
            // A slot that isn't being used goes back to the pool.
            if (HasAttackSlot && Time.time - slotTakenAt > SlotPatience) ReleaseSlot(1f);

            Vector3 toPlayer = player.transform.position - transform.position;
            if (!aggro && Mathf.Abs(toPlayer.x) <= workerClass.aggroRange)
            {
                aggro = true;
                nextAttackTime = Time.time + UnityEngine.Random.Range(0.4f, 0.9f);
            }
            if (!aggro)
            {
                Motor.SetMoveInput(Vector2.zero);
                return;
            }

            // Guards turn slowly: dodge past one and you get a window at its back.
            if (Role != EnemyRole.Guard || Time.time >= nextTurnTime)
            {
                Motor.FaceTowards(player.transform.position);
                if (Role == EnemyRole.Guard) nextTurnTime = Time.time + 0.7f;
            }
            if (Role == EnemyRole.Guard) Motor.SpeedMultiplier = IsGuarding ? 0.7f : 1f;

            if (RoleBehaviour(player, toPlayer)) return;

            float side = toPlayer.x >= 0f ? 1f : -1f;
            float desired = attackMove.reach * workerClass.scale + 0.35f;
            float dx = toPlayer.x - side * desired;
            float dz = toPlayer.z;
            bool inRange = Mathf.Abs(toPlayer.x) <= desired + 0.35f && Mathf.Abs(dz) <= laneTolerance;

            // Bosses leap in from mid-range with a heavily telegraphed slam.
            if (bossSlam != null && Time.time >= nextSlamTime && Mathf.Abs(toPlayer.x) < 7f && Mathf.Abs(dz) < 1.2f && TryTakeSlot())
            {
                Motor.SetMoveInput(Vector2.zero);
                if (Attack.TryAttack(bossSlam))
                {
                    float rage = Health.Normalized < 0.5f ? 0.6f : 1f;
                    nextSlamTime = Time.time + UnityEngine.Random.Range(5f, 7.5f) * rage;
                    return;
                }
            }

            // Only a couple of coworkers press the attack; the rest hang back and circle.
            if (!TryTakeSlot())
            {
                float holdX = toPlayer.x - side * Mathf.Max(StandoffDistance, desired + 1.8f);
                float wobble = Mathf.Sin(Time.time * 0.9f + GetInstanceID()) * 1.6f * circleSign;
                float holdZ = Mathf.Clamp(player.transform.position.z + wobble, -2.3f, 2.3f) - transform.position.z;
                Vector2 hold = new Vector2(Mathf.Abs(holdX) > 0.4f ? Mathf.Clamp(holdX, -1f, 1f) * 0.6f : 0f, Mathf.Clamp(holdZ, -1f, 1f) * 0.5f);
                Motor.SetMoveInput(hold + Separation());
                return;
            }

            if (inRange)
            {
                Motor.SetMoveInput(Vector2.zero);
                if (Time.time >= nextAttackTime && Attack.TryAttack(attackMove))
                    nextAttackTime = Time.time + workerClass.attackCooldown * UnityEngine.Random.Range(0.8f, 1.25f);
                return;
            }

            Vector2 input = new Vector2(Mathf.Clamp(dx, -1f, 1f), Mathf.Clamp(dz * 1.5f, -1f, 1f)) + Separation();
            Motor.SetMoveInput(input);
        }

        // ------------------------------------------------------------------ roles

        /// <summary>Runs the role's own behaviour; returns true if it took control this frame.</summary>
        bool RoleBehaviour(PlayerController player, Vector3 toPlayer)
        {
            float distance = Mathf.Abs(toPlayer.x);
            float dz = toPlayer.z;
            bool lined = Mathf.Abs(dz) < 0.5f;
            switch (Role)
            {
                case EnemyRole.Thrower:
                {
                    // Keep 5-7 m away in the player's lane; throw when lined up.
                    if (Time.time >= nextSpecialTime && lined && distance > 2.5f && distance < 11f && TryTakeSlot())
                    {
                        Motor.SetMoveInput(Vector2.zero);
                        if (Attack.TryAttack(throwMove))
                        {
                            roleRoutine = StartCoroutine(ThrowRoutine());
                            nextSpecialTime = Time.time + UnityEngine.Random.Range(2.2f, 3.2f) / (IsBuffed ? 1.5f : 1f);
                        }
                        return true;
                    }
                    if (distance < 2f) return false; // cornered: fight normally
                    float side = toPlayer.x >= 0f ? 1f : -1f;
                    float want = toPlayer.x - side * 6f;
                    var input = new Vector2(Mathf.Abs(want) > 0.6f ? Mathf.Clamp(want, -1f, 1f) : 0f, Mathf.Clamp(dz * 1.5f, -1f, 1f));
                    Motor.SetMoveInput(input + Separation());
                    return true;
                }

                case EnemyRole.Support:
                {
                    // Rally the team when there's someone to rally; otherwise hang back.
                    if (Time.time >= nextSpecialTime && HasAlliesNearby(7f))
                    {
                        Motor.SetMoveInput(Vector2.zero);
                        roleRoutine = StartCoroutine(RallyRoutine());
                        nextSpecialTime = Time.time + UnityEngine.Random.Range(6.5f, 8.5f);
                        return true;
                    }
                    if (!HasAlliesNearby(9f) || distance < 1.6f) return false;
                    float side = toPlayer.x >= 0f ? 1f : -1f;
                    float want = toPlayer.x - side * 5f;
                    var input = new Vector2(Mathf.Abs(want) > 0.6f ? Mathf.Clamp(want, -1f, 1f) : 0f, Mathf.Clamp(-dz * 0.6f, -1f, 1f) * 0.5f);
                    Motor.SetMoveInput(input + Separation());
                    return true;
                }

                case EnemyRole.Charger:
                {
                    if (Time.time >= nextSpecialTime && Mathf.Abs(dz) < 0.7f && distance > 3f && distance < 9f && TryTakeSlot())
                    {
                        Motor.SetMoveInput(Vector2.zero);
                        if (Attack.TryAttack(chargeMove))
                        {
                            roleRoutine = StartCoroutine(ChargeRoutine());
                            nextSpecialTime = Time.time + UnityEngine.Random.Range(4.5f, 6.5f);
                        }
                        return true;
                    }
                    return false;
                }
            }
            return false;
        }

        bool HasAlliesNearby(float radius)
        {
            foreach (var other in Active)
                if (other != this && !other.Health.IsDead && other.Role != EnemyRole.Support &&
                    (other.transform.position - transform.position).sqrMagnitude < radius * radius)
                    return true;
            return false;
        }

        IEnumerator ThrowRoutine()
        {
            yield return new WaitForSeconds(throwMove.startup);
            if (Attack.Current != throwMove || Health.IsDead) yield break;
            var prefab = HeldItem != null && HeldItem.Catalog != null ? HeldItem.Catalog.Get(attackMove.prop) : null;
            Vector3 dir = Motor.FacingDirection;
            dir.z = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.right;
            float damage = attackMove.damage * 0.9f * (IsBuffed ? 1.3f : 1f);
            Projectile.Launch(prefab, transform.position + Vector3.up * 0.8f + dir.normalized * 0.6f, dir, 9f, damage, Team.Enemy, gameObject);
        }

        IEnumerator ChargeRoutine()
        {
            chargeHit = false;
            Callout?.Invoke(this, "!", TelegraphColor);
            yield return new WaitForSeconds(chargeMove.startup);
            if (Attack.Current != chargeMove || Health.IsDead) yield break;
            Vector3 dir = Motor.FacingDirection;
            dir.z = 0f;
            Motor.Dash(dir, 15f, chargeMove.active);
            yield return new WaitForSeconds(chargeMove.active + chargeMove.recovery + 0.02f);
            if (!chargeHit && !Health.IsDead)
            {
                // Missed: winded and wide open.
                stunnedUntil = Mathf.Max(stunnedUntil, Time.time + 1.4f);
                if (Visual != null) Visual.Flash(new Color(0.6f, 0.7f, 0.9f), 1.4f);
                Callout?.Invoke(this, "WINDED", new Color(0.7f, 0.8f, 1f));
            }
        }

        IEnumerator RallyRoutine()
        {
            const float cast = 0.9f;
            float until = Time.time + cast;
            stunnedUntil = Mathf.Max(stunnedUntil, until); // stands still while it rallies
            if (Visual != null) Visual.Flash(RallyColor, cast);
            Callout?.Invoke(this, "RALLYING...", RallyColor);
            float hpAtStart = Health.Current;
            while (Time.time < until)
            {
                // Any hit during the cast cancels it.
                if (Health.IsDead || Health.Current < hpAtStart)
                {
                    Callout?.Invoke(this, "INTERRUPTED!", Color.white);
                    yield break;
                }
                yield return null;
            }
            int rallied = 0;
            foreach (var other in Active.ToArray())
            {
                if (other == this || other.Health.IsDead || (other.transform.position - transform.position).sqrMagnitude > 49f) continue;
                // Rallies lift the rank and file: no healing a boss back up, and no Supports healing each other forever.
                if (other.Boss != Game.BossRank.None || other.Role == EnemyRole.Support) continue;
                other.Health.Heal(other.Health.Max * 0.25f);
                other.buffedUntil = Time.time + 5f;
                other.Attack.BuffDamageMultiplier = 1.3f;
                if (other.Visual != null) other.Visual.Flash(RallyColor, 0.5f);
                Callout?.Invoke(other, "+RALLIED", RallyColor);
                rallied++;
            }
            if (rallied > 0) GameFlow.Instance?.Toast($"{DisplayName}: \"{RallyQuote()}\"");
        }

        static string RallyQuote()
        {
            string[] lines = { "Let's take a wellness break, team!", "Remember: there's no I in team!", "Mandatory fun starts now!" };
            return lines[UnityEngine.Random.Range(0, lines.Length)];
        }

        /// <summary>Guards soak up light hits from the front; heavies and hits from behind get through.</summary>
        DamageInfo GuardIncoming(DamageInfo info)
        {
            if (!IsGuarding || info.source == null) return info;
            Vector3 toSource = info.source.transform.position - transform.position;
            toSource.y = 0f;
            if (Vector3.Dot(Motor.FacingDirection, toSource.normalized) < 0.2f) return info; // from behind

            if (info.heavy)
            {
                guardBrokenUntil = Time.time + 3f;
                Callout?.Invoke(this, "GUARD BREAK!", new Color(1f, 0.8f, 0.25f));
                info.amount *= 1.2f;
                info.hitStop = Mathf.Max(info.hitStop, 0.1f);
                return info;
            }
            info.amount *= 0.15f;
            info.knockback *= 0.3f;
            info.hitStop = 0.02f;
            info.blocked = true;
            if (Visual != null)
            {
                Visual.Flash(BlockColor, 0.12f);
                Visual.PlayBlockReaction(-toSource.normalized * 4f);
            }
            Callout?.Invoke(this, "BLOCKED", BlockColor);
            return info;
        }

        void LateUpdate()
        {
            if (buffedUntil > 0f && Time.time >= buffedUntil)
            {
                buffedUntil = 0f;
                Attack.BuffDamageMultiplier = 1f;
            }
        }

        // ------------------------------------------------------------------ attack slots

        static void PruneSlots() => SlotHolders.RemoveAll(e => e == null || !e.isActiveAndEnabled || e.Health == null || e.Health.IsDead);

        /// <summary>Claims (or keeps) an attack slot. Bosses always get one; everyone else waits for a free one.</summary>
        bool TryTakeSlot()
        {
            if (HasAttackSlot) return true;
            if (Time.time < nextSlotTime && Boss == Game.BossRank.None) return false;
            PruneSlots();
            if (Boss == Game.BossRank.None && SlotHolders.Count >= MaxAttackers) return false;
            SlotHolders.Add(this);
            slotTakenAt = Time.time;
            return true;
        }

        /// <summary>Hands the slot back; this coworker waits <paramref name="rest"/> seconds before asking again.</summary>
        void ReleaseSlot(float rest)
        {
            if (!SlotHolders.Remove(this)) return;
            nextSlotTime = Time.time + rest;
        }

        string SlamQuote()
        {
            string[] lines = Boss == Game.BossRank.FinalBoss
                ? new[] { "You're FIRED!", "I own this building!", "Let's discuss your exit package." }
                : new[] { "Let's take this offline!", "Your position has been eliminated!", "Per my last email!" };
            return lines[UnityEngine.Random.Range(0, lines.Length)];
        }

        /// <summary>Keeps a crowd of coworkers from stacking into one blob.</summary>
        Vector2 Separation()
        {
            Vector3 push = Vector3.zero;
            foreach (var other in Active)
            {
                if (other == this) continue;
                Vector3 d = transform.position - other.transform.position;
                d.y = 0f;
                float dist = d.magnitude;
                if (dist > 0.001f && dist < separationRadius) push += d / dist * (1f - dist / separationRadius);
            }
            return new Vector2(push.x, push.z) * 0.8f;
        }

        void OnDamaged(DamageInfo info)
        {
            aggro = true;
            if (info.blocked)
            {
                // A blocked hit only nudges a guard back.
                Motor.AddImpulse(info.knockback);
                GameFlow.HitStop(info.hitStop);
                return;
            }
            if (IsArmored)
            {
                // Hyper armour: the slam is committed. The hit lands (damage, flash, freeze) but doesn't stop it.
                if (Visual != null) Visual.Flash(Color.white, 0.08f);
                GameFlow.HitStop(info.hitStop);
                return;
            }
            float poise = workerClass != null ? workerClass.poise : 0f;
            if (poise < 0.99f)
            {
                if (roleRoutine != null && Attack.IsAttacking) StopCoroutine(roleRoutine);
                Attack.Interrupt();
                Motor.CancelDash(); // a charger knocked out of its bull-rush stops rushing
            }
            // Keep the longer freeze: a hit mustn't shorten a Blue Screen or a winded stagger.
            float stun = hitStun * (1f - poise);
            stunnedUntil = Mathf.Max(stunnedUntil, Time.time + stun);
            Motor.AddImpulse(info.knockback * (1f - poise * 0.6f));
            if (Visual != null)
            {
                Visual.Flash(Color.white, 0.08f);
                Visual.PlayHitReaction(info.knockback, stun);
            }
            GameFlow.HitStop(info.hitStop);
        }

        void OnDied()
        {
            Active.Remove(this);
            Attack.Interrupt();
            Motor.Stop();
            Motor.CanMove = false;
            GetComponent<CharacterController>().enabled = false;
            if (Visual != null) Visual.PlayKnockedOut();
            SideScrollCamera.Shake(0.2f, 0.15f);
            AnyDefeated?.Invoke(this);
            StartCoroutine(SettleAndRemove());
        }

        IEnumerator SettleAndRemove()
        {
            // Drop to the floor if knocked out mid-air.
            Vector3 start = transform.position;
            Vector3 end = new Vector3(start.x, 0f, start.z);
            for (float t = 0f; t < 0.25f; t += Time.deltaTime)
            {
                transform.position = Vector3.Lerp(start, end, t / 0.25f);
                yield return null;
            }
            transform.position = end;
            yield return new WaitForSeconds(corpseLifetime);
            Destroy(gameObject);
        }
    }
}
