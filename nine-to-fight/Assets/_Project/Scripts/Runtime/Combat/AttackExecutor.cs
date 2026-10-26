using System;
using System.Collections;
using System.Collections.Generic;
using NineToFight.Characters;
using NineToFight.Core;
using UnityEngine;

namespace NineToFight.Combat
{
    /// <summary>
    /// Plays a FightingMove: startup, active hit window, recovery. Once a move has landed a hit, its recovery
    /// can be cancelled into a stronger move (or, up to <see cref="MaxLightChain"/> times, another light attack),
    /// which is what lets lights chain into heavies and specials.
    /// </summary>
    [RequireComponent(typeof(CharacterMotor2_5D), typeof(Health))]
    public class AttackExecutor : MonoBehaviour
    {
        static readonly Collider[] HitBuffer = new Collider[16];

        [SerializeField] CharacterVisual visual;

        CharacterMotor2_5D motor;
        Health self;
        Coroutine routine;
        readonly HashSet<Health> alreadyHit = new HashSet<Health>();
        readonly Dictionary<string, float> readyAt = new Dictionary<string, float>();
        bool landedThisMove, recovering;
        int lightChain;

        /// <summary>How many light attacks may follow a light attack by cancelling (a three-hit string).</summary>
        public const int MaxLightChain = 2;

        public bool IsAttacking => routine != null;
        /// <summary>The current move has connected and is in its final recovery: it can be cancelled into the next.</summary>
        public bool InCancelWindow => routine != null && recovering && landedThisMove;
        public float DamageMultiplier { get; set; } = 1f;
        /// <summary>Temporary damage boost from abilities; stacks with DamageMultiplier.</summary>
        public float BuffDamageMultiplier { get; set; } = 1f;
        /// <summary>Permanent run bonuses (perks).</summary>
        public float PerkDamageMultiplier { get; set; } = 1f;
        public float CritChance { get; set; }
        public float Lifesteal { get; set; }
        public FightingMove Current { get; private set; }

        public event Action<FightingMove> AttackStarted;
        /// <summary>Raised for every swing, including each repeat of a multi-hit flurry.</summary>
        public event Action<FightingMove> Swing;
        public event Action AttackEnded;
        public event Action<Health, FightingMove> HitLanded;

        void Awake()
        {
            motor = GetComponent<CharacterMotor2_5D>();
            self = GetComponent<Health>();
            if (visual == null) visual = GetComponentInChildren<CharacterVisual>();
        }

        public bool IsReady(FightingMove move) =>
            move != null && (!readyAt.TryGetValue(move.id, out float t) || Time.time >= t);

        public float CooldownRemaining(FightingMove move) =>
            move != null && readyAt.TryGetValue(move.id, out float t) ? Mathf.Max(0f, t - Time.time) : 0f;

        /// <summary>
        /// Cancel rules after a hit lands: into a stronger slot (light, then heavy, then special, then super), or a
        /// light into another light while the string is shorter than <see cref="MaxLightChain"/>.
        /// </summary>
        public static bool CancelAllowed(FightingMove from, FightingMove to, int lightChain)
        {
            if (from == null || to == null) return false;
            if (from.slot == MoveSlot.Light && to.slot == MoveSlot.Light) return lightChain < MaxLightChain;
            return (int)to.slot > (int)from.slot;
        }

        public bool TryAttack(FightingMove move)
        {
            if (move == null || self.IsDead || !IsReady(move)) return false;
            if (IsAttacking)
            {
                // Mid-move: only a hit-confirmed recovery can be cancelled, and only into what the rules allow.
                if (!InCancelWindow || !CancelAllowed(Current, move, lightChain)) return false;
                lightChain = Current.slot == MoveSlot.Light && move.slot == MoveSlot.Light ? lightChain + 1 : 0;
                StopCoroutine(routine);
                Finish(); // the strike on screen cross-fades into the next one
            }
            else
            {
                lightChain = 0;
            }
            if (move.cooldown > 0f) readyAt[move.id] = Time.time + move.cooldown;
            routine = StartCoroutine(Run(move));
            return true;
        }

        public void Interrupt()
        {
            if (routine != null) StopCoroutine(routine);
            Finish();
            if (visual != null) visual.CancelAttack();
        }

        void Finish()
        {
            bool wasAttacking = Current != null;
            routine = null;
            Current = null;
            recovering = false;
            motor.SpeedMultiplier = 1f;
            motor.LockFacing(false);
            if (wasAttacking) AttackEnded?.Invoke();
        }

        IEnumerator Run(FightingMove move)
        {
            Current = move;
            landedThisMove = false;
            recovering = false;
            alreadyHit.Clear();
            motor.SpeedMultiplier = move.moveMultiplier;
            motor.LockFacing(true);
            AttackStarted?.Invoke(move);

            // Flurries are combos: each hit throws its own strike, flowing on from the one before.
            int hits = Mathf.Max(1, move.hits);
            for (int h = 0; h < hits; h++)
            {
                alreadyHit.Clear();
                // A thrown item is back in hand for each throw of a flurry.
                if (move.IsThrown && h > 0 && visual != null) visual.GetComponent<HeldItemDisplay>()?.Show(move.prop);
                Swing?.Invoke(move);
                if (visual != null) visual.PlayAttack(move, move.StyleForHit(h), h < hits - 1);

                if (move.startup > 0f) yield return new WaitForSeconds(move.startup);

                if (move.lunge > 0f) motor.AddImpulse(motor.FacingDirection * (h == 0 ? move.lunge : move.lunge * 0.5f));
                if (move.IsThrown)
                {
                    Throw(move);
                    if (move.active > 0f) yield return new WaitForSeconds(move.active);
                }
                else
                {
                    for (float t = 0f; t < move.active; t += Time.deltaTime)
                    {
                        Sweep(move);
                        yield return null;
                    }
                    Sweep(move);
                }

                recovering = h == hits - 1;
                if (move.recovery > 0f) yield return new WaitForSeconds(move.recovery);
            }
            Finish();
        }

        /// <summary>The held item leaves the hand and flies down the lane; its hits count like a swing's.</summary>
        void Throw(FightingMove move)
        {
            var held = visual != null ? visual.GetComponent<HeldItemDisplay>() : null;
            var prefab = held != null && held.Catalog != null ? held.Catalog.Get(move.prop) : null;
            Vector3 dir = motor.FacingDirection;
            dir.y = 0f;
            dir.z = 0f; // down the lane, like every thrown item
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.right * motor.Facing;
            dir.Normalize();
            bool crit = CritChance > 0f && UnityEngine.Random.value < CritChance;
            float amount = move.damage * DamageMultiplier * BuffDamageMultiplier * PerkDamageMultiplier * (crit ? 2f : 1f);
            var at = transform.position + Vector3.up * move.height * transform.lossyScale.y + dir * 0.6f;
            Projectile.Launch(prefab, at, dir, move.throwSpeed, amount, self.Team, gameObject,
                move.knockback, move.hitStop * (crit ? 1.6f : 1f), move.pierce, (target, info) =>
                {
                    if (self == null || self.IsDead) return;
                    float heal = move.healOnHit + Lifesteal;
                    if (heal > 0f) self.Heal(info.amount * heal);
                    landedThisMove = true;
                    HitLanded?.Invoke(target, move);
                });
            if (held != null) held.Show(null); // it's in the air now; the idle item comes back when the move ends
        }

        void Sweep(FightingMove move)
        {
            Vector3 center = transform.position + motor.FacingDirection * move.reach * transform.lossyScale.x
                           + Vector3.up * move.height * transform.lossyScale.y;
            // Swatting an incoming throw sends it back.
            for (int p = Projectile.Live.Count - 1; p >= 0; p--)
            {
                var projectile = Projectile.Live[p];
                if (projectile.Team != self.Team && (projectile.transform.position - center).sqrMagnitude <= (move.radius + 0.5f) * (move.radius + 0.5f))
                    projectile.Reflect(self.Team, gameObject);
            }

            int count = Physics.OverlapSphereNonAlloc(center, move.radius, HitBuffer, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var target = HitBuffer[i].GetComponentInParent<Health>();
                if (target == null || target == self || target.Team == self.Team || target.IsDead) continue;
                if (!alreadyHit.Add(target)) continue;

                bool crit = CritChance > 0f && UnityEngine.Random.value < CritChance;
                var info = new DamageInfo
                {
                    amount = move.damage * DamageMultiplier * BuffDamageMultiplier * PerkDamageMultiplier * (crit ? 2f : 1f),
                    knockback = KnockbackDirection(target.transform.position) * move.knockback + Vector3.up * move.knockUp,
                    source = gameObject,
                    hitStop = move.hitStop * (crit ? 1.6f : 1f),
                    heavy = move.slot != MoveSlot.Light,
                    crit = crit,
                };
                if (!target.TakeDamage(info)) continue;
                float heal = move.healOnHit + Lifesteal;
                if (heal > 0f) self.Heal(info.amount * heal);
                landedThisMove = true;
                HitLanded?.Invoke(target, move);
            }
        }

        /// <summary>
        /// Hits push away from the attacker: straight ahead for a jab, outward for the all-round sweeps,
        /// so nobody standing behind gets shoved through the attacker.
        /// </summary>
        public Vector3 KnockbackDirection(Vector3 targetPosition) =>
            KnockbackDirection(transform.position, targetPosition, motor.FacingDirection);

        public static Vector3 KnockbackDirection(Vector3 attacker, Vector3 target, Vector3 facing)
        {
            var away = target - attacker;
            away.y = 0f;
            // Someone standing right on top of the attacker still goes the way the blow was thrown.
            return away.sqrMagnitude < 0.15f * 0.15f ? facing : away.normalized;
        }

        void OnDisable()
        {
            if (routine != null) StopCoroutine(routine);
            routine = null;
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (Current == null || motor == null) return;
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.5f);
            Gizmos.DrawWireSphere(transform.position + motor.FacingDirection * Current.reach + Vector3.up * Current.height, Current.radius);
        }
#endif
    }
}
