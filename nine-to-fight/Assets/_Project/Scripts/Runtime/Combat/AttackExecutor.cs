using System;
using System.Collections;
using System.Collections.Generic;
using NineToFight.Characters;
using NineToFight.Core;
using UnityEngine;

namespace NineToFight.Combat
{
    /// <summary>Plays a FightingMove: startup, active hit window, recovery.</summary>
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

        public bool IsAttacking => routine != null;
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

        public bool TryAttack(FightingMove move)
        {
            if (move == null || IsAttacking || self.IsDead || !IsReady(move)) return false;
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
            motor.SpeedMultiplier = 1f;
            motor.LockFacing(false);
            if (wasAttacking) AttackEnded?.Invoke();
        }

        IEnumerator Run(FightingMove move)
        {
            Current = move;
            alreadyHit.Clear();
            motor.SpeedMultiplier = move.moveMultiplier;
            motor.LockFacing(true);
            AttackStarted?.Invoke(move);

            // Flurries are combos: each hit throws its own strike, flowing on from the one before.
            int hits = Mathf.Max(1, move.hits);
            for (int h = 0; h < hits; h++)
            {
                alreadyHit.Clear();
                Swing?.Invoke(move);
                if (visual != null) visual.PlayAttack(move, move.StyleForHit(h), h < hits - 1);

                if (move.startup > 0f) yield return new WaitForSeconds(move.startup);

                if (move.lunge > 0f) motor.AddImpulse(motor.FacingDirection * (h == 0 ? move.lunge : move.lunge * 0.5f));
                for (float t = 0f; t < move.active; t += Time.deltaTime)
                {
                    Sweep(move);
                    yield return null;
                }
                Sweep(move);

                if (move.recovery > 0f) yield return new WaitForSeconds(move.recovery);
            }
            Finish();
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
                    knockback = motor.FacingDirection * move.knockback + Vector3.up * move.knockUp,
                    source = gameObject,
                    hitStop = move.hitStop * (crit ? 1.6f : 1f),
                    heavy = move.slot != MoveSlot.Light,
                    crit = crit,
                };
                if (!target.TakeDamage(info)) continue;
                float heal = move.healOnHit + Lifesteal;
                if (heal > 0f) self.Heal(info.amount * heal);
                HitLanded?.Invoke(target, move);
            }
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
