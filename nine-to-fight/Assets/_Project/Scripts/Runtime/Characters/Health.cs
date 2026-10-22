using System;
using UnityEngine;

namespace NineToFight.Core
{
    public enum Team { Player, Enemy }

    public struct DamageInfo
    {
        public float amount;
        public Vector3 knockback;
        public GameObject source;
        public float hitStop;
        /// <summary>Heavy, special and super moves: break guards.</summary>
        public bool heavy;
        /// <summary>Set by a guard that absorbed most of the hit.</summary>
        public bool blocked;
        public bool crit;
    }

    public class Health : MonoBehaviour
    {
        [SerializeField] Team team = Team.Enemy;
        [SerializeField] float maxHealth = 100f;
        [Tooltip("Invulnerability window after taking a hit.")]
        [SerializeField] float hitInvulnerability;

        float current = -1f;
        float invulnerableUntil;

        public Team Team => team;
        public float Max => maxHealth;
        public float Current { get { EnsureInit(); return current; } }
        public float Normalized => Max > 0f ? Current / Max : 0f;
        public bool IsDead { get; private set; }
        public bool IsInvulnerable => Time.time < invulnerableUntil;

        public event Action<DamageInfo> Damaged;
        public event Action Died;

        /// <summary>Lets the owner reshape incoming hits (guards, damage resistances) before they land.</summary>
        public Func<DamageInfo, DamageInfo> ModifyIncoming;
        /// <summary>Asked when a hit would be lethal; return true to survive (the handler restores health).</summary>
        public Func<bool> PreventDeath;

        /// <summary>Raised for every hit on anyone; feeds global effects (sound, sparks, combo).</summary>
        public static event Action<Health, DamageInfo> AnyDamaged;
        public static event Action<Health> AnyDied;

        void Awake() => EnsureInit();

        void EnsureInit()
        {
            if (current < 0f) current = maxHealth;
        }

        public void SetMax(float value, bool refill)
        {
            EnsureInit();
            maxHealth = Mathf.Max(1f, value);
            current = refill ? maxHealth : Mathf.Min(current, maxHealth);
        }

        public bool TakeDamage(DamageInfo info)
        {
            EnsureInit();
            if (IsDead || IsInvulnerable || info.amount <= 0f) return false;
            if (ModifyIncoming != null) info = ModifyIncoming(info);

            current = Mathf.Max(0f, current - info.amount);
            if (hitInvulnerability > 0f) invulnerableUntil = Time.time + hitInvulnerability;
            Damaged?.Invoke(info);
            AnyDamaged?.Invoke(this, info);

            if (current <= 0f && PreventDeath != null && PreventDeath()) return true;
            if (current <= 0f)
            {
                IsDead = true;
                Died?.Invoke();
                AnyDied?.Invoke(this);
            }
            return true;
        }

        public void Heal(float amount)
        {
            EnsureInit();
            if (IsDead || amount <= 0f) return;
            current = Mathf.Min(maxHealth, current + amount);
        }

        public void MakeInvulnerable(float duration) =>
            invulnerableUntil = Mathf.Max(invulnerableUntil, Time.time + duration);
    }
}
