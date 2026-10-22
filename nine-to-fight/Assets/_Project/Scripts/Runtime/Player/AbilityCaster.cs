using System;
using System.Collections;
using NineToFight.CameraRig;
using NineToFight.Combat;
using NineToFight.Enemies;
using NineToFight.Game;
using UnityEngine;
using UnityEngine.Rendering;

namespace NineToFight.Player
{
    /// <summary>Casts the player's class Q ability and runs its effect, cooldown and ring visuals.</summary>
    public class AbilityCaster : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Tooltip("Unlit material used for the ring effects; tinted per ability.")]
        [SerializeField] Material effectMaterial;

        readonly Cooldown cooldown = new Cooldown();
        PlayerController player;
        float activeUntil;
        Coroutine buffRoutine;

        public Material EffectMaterial => effectMaterial;
        public ClassAbility Ability => player != null && player.Class != null ? player.Class.ability : null;
        public bool IsReady => cooldown.IsReady(Time.time);
        public float CooldownRemaining => cooldown.Remaining(Time.time);
        public float CooldownFraction => cooldown.Fraction(Time.time);
        public bool IsActive => Time.time < activeUntil;

        public event Action<ClassAbility> Cast;
        /// <summary>Perk: shortens the ability cooldown.</summary>
        public float CooldownMultiplier { get; set; } = 1f;

        void Awake() => player = GetComponent<PlayerController>();

        public void ResetCooldown()
        {
            cooldown.Reset();
            EndBuff();
        }

        public bool TryCast()
        {
            var ability = Ability;
            if (ability == null || player.Health.IsDead || !IsReady) return false;

            cooldown.Trigger(Time.time, ability.cooldown * CooldownMultiplier);
            switch (ability.kind)
            {
                case AbilityKind.Heal:
                    player.Health.Heal(ability.amount);
                    Pulse(ability.color, 0.35f);
                    Ring(ability.color, 1.4f, 0.4f, follow: true);
                    break;

                case AbilityKind.StunBurst:
                    foreach (var enemy in EnemyBrain.Active.ToArray())
                    {
                        Vector3 d = enemy.transform.position - transform.position;
                        d.y = 0f;
                        if (d.magnitude <= ability.radius) enemy.Stun(ability.duration, ability.color);
                    }
                    Ring(ability.color, ability.radius, 0.3f, follow: false);
                    SideScrollCamera.Shake(0.3f, 0.2f);
                    GameFlow.HitStop(0.06f);
                    break;

                case AbilityKind.Rush:
                    StartBuff(ability, () =>
                    {
                        player.Motor.BuffSpeedMultiplier = ability.amount;
                        player.Attack.BuffDamageMultiplier = ability.damageMultiplier;
                    });
                    Ring(ability.color, 1.2f, 0.3f, follow: true);
                    break;

                case AbilityKind.Shield:
                    player.Health.MakeInvulnerable(ability.duration);
                    StartBuff(ability, null);
                    Ring(ability.color, 1.1f, ability.duration, follow: true, hold: true);
                    break;
            }

            Cast?.Invoke(ability);
            return true;
        }

        // ------------------------------------------------------------------ buffs

        void StartBuff(ClassAbility ability, Action apply)
        {
            EndBuff();
            apply?.Invoke();
            activeUntil = Time.time + ability.duration;
            buffRoutine = StartCoroutine(BuffRoutine(ability));
        }

        IEnumerator BuffRoutine(ClassAbility ability)
        {
            // Flicker the class colour so it's obvious the buff is running.
            while (Time.time < activeUntil)
            {
                Pulse(ability.color, 0.08f);
                yield return new WaitForSeconds(Mathf.Min(0.3f, activeUntil - Time.time));
            }
            EndBuff();
        }

        void EndBuff()
        {
            if (buffRoutine != null) StopCoroutine(buffRoutine);
            buffRoutine = null;
            activeUntil = 0f;
            if (player == null) return;
            player.Motor.BuffSpeedMultiplier = 1f;
            player.Attack.BuffDamageMultiplier = 1f;
        }

        void OnDisable() => EndBuff();

        void Pulse(Color color, float duration)
        {
            if (player.Visual != null) player.Visual.Flash(color, duration);
        }

        // ------------------------------------------------------------------ ring effect

        void Ring(Color color, float radius, float duration, bool follow, bool hold = false)
        {
            if (effectMaterial == null) return;
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Ability Ring";
            Destroy(ring.GetComponent<Collider>());
            var r = ring.GetComponent<Renderer>();
            r.sharedMaterial = effectMaterial;
            r.shadowCastingMode = ShadowCastingMode.Off;
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, color);
            r.SetPropertyBlock(block);

            if (follow) ring.transform.SetParent(transform, false);
            ring.transform.position = transform.position + Vector3.up * 0.04f;
            StartCoroutine(RingRoutine(ring.transform, radius, duration, hold));
        }

        static IEnumerator RingRoutine(Transform ring, float radius, float duration, bool hold)
        {
            const float grow = 0.2f;
            for (float t = 0f; t < duration && ring != null; t += Time.deltaTime)
            {
                float size = hold
                    ? radius * 2f * (Mathf.Min(1f, t / grow) + Mathf.Sin(t * 12f) * 0.04f)
                    : radius * 2f * Mathf.Sqrt(t / duration);
                float thickness = hold ? 0.02f : Mathf.Lerp(0.05f, 0.005f, t / duration);
                ring.localScale = new Vector3(size, thickness, size);
                yield return null;
            }
            if (ring != null) Destroy(ring.gameObject);
        }
    }
}
