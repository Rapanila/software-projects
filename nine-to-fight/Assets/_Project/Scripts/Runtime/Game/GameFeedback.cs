using NineToFight.Audio;
using NineToFight.CameraRig;
using NineToFight.Core;
using NineToFight.Enemies;
using NineToFight.Fx;
using NineToFight.Player;
using UnityEngine;

namespace NineToFight.Game
{
    /// <summary>Turns combat events into sound, sparks, damage numbers and knockout callouts.</summary>
    public class GameFeedback : MonoBehaviour
    {
        const float HeavyHitDamage = 14f;

        static readonly Color PaperWhite = new Color(0.97f, 0.96f, 0.9f);
        static readonly Color SparkYellow = new Color(1f, 0.82f, 0.25f);
        static readonly Color HurtRed = new Color(1f, 0.32f, 0.28f);

        static readonly string[] KnockoutLines =
        {
            "CLOCKED OUT!", "LET GO!", "OUT OF OFFICE!", "DOWNSIZED!", "ON LEAVE!",
            "REASSIGNED!", "LOGGED OFF!", "CC'D ON THAT!", "PTO!", "TERMINATED!",
        };

        /// <summary>Below this share of health the player hears their heartbeat.</summary>
        public const float LowHealth = 0.25f;

        PlayerController player;
        float nextHeartbeat;

        void OnEnable()
        {
            Health.AnyDamaged += OnDamaged;
            EnemyBrain.AnyDefeated += OnDefeated;
            EnemyBrain.Callout += OnCallout;
            Characters.CharacterVisual.Stepped += OnStepped;
            Combat.Projectile.Reflected += OnReflected;
        }

        void OnCallout(EnemyBrain enemy, string text, Color color)
        {
            Vector3 at = enemy.transform.position + Vector3.up * 2.9f * enemy.transform.lossyScale.y;
            HitEffects.Text(at, text, color, text.Length > 6 ? 1.1f : 1.4f, 0.9f);
            if (text == "GUARD BREAK!") GameAudio.Play(Sfx.HitHeavy, 1f);
            else if (text == "WINDED") GameAudio.Play(Sfx.Dodge, 0.6f);
        }

        void OnReflected(Combat.Projectile projectile)
        {
            GameAudio.Play(Sfx.ComboUp, 0.8f);
            HitEffects.Text(projectile.transform.position + Vector3.up * 0.6f, "RETURN TO SENDER!", SparkYellow, 1.2f, 0.9f);
            HitEffects.Burst(projectile.transform.position, Vector3.up, SparkYellow, 8, 6f, 0.05f);
        }

        void OnDisable()
        {
            Health.AnyDamaged -= OnDamaged;
            EnemyBrain.AnyDefeated -= OnDefeated;
            EnemyBrain.Callout -= OnCallout;
            Characters.CharacterVisual.Stepped -= OnStepped;
            Combat.Projectile.Reflected -= OnReflected;
            if (player != null)
            {
                player.Attack.Swing -= OnPlayerAttack;
                player.Dodged -= OnDodge;
                player.Progression.Promoted -= OnPromoted;
                if (player.Ability != null) player.Ability.Cast -= OnAbility;
            }
        }

        void Start()
        {
            player = PlayerController.Instance;
            if (player == null) return;
            player.Attack.Swing += OnPlayerAttack;
            player.Dodged += OnDodge;
            player.Progression.Promoted += OnPromoted;
            if (player.Ability != null) player.Ability.Cast += OnAbility;
        }

        void Update()
        {
            // Low on health: a heartbeat that quickens as it drops. Game time, so it stops while paused.
            if (player == null || player.Health.IsDead || !GameFlow.IsPlaying) return;
            float health = player.Health.Normalized;
            if (health >= LowHealth || Time.time < nextHeartbeat) return;
            GameAudio.Play(Sfx.Heartbeat, Mathf.Lerp(0.9f, 0.5f, health / LowHealth), 0f);
            nextHeartbeat = Time.time + Mathf.Lerp(0.55f, 0.95f, health / LowHealth);
        }

        void OnPlayerAttack(Combat.FightingMove move) => GameAudio.Play(Sfx.Whoosh, 0.45f + Mathf.Min(move.damage, 30f) / 60f);

        void OnDodge()
        {
            GameAudio.Play(Sfx.Dodge, 0.6f);
            if (player != null) HitEffects.Dust(player.transform.position, 0.7f, 5, -player.Motor.FacingDirection * 1.5f);
        }

        void OnStepped(Characters.CharacterVisual visual, Vector3 at, float strength)
        {
            bool isPlayer = player != null && visual == player.Visual;
            HitEffects.Dust(at, 0.35f + 0.15f * strength, strength > 1.4f ? 6 : 2);
            if (isPlayer) GameAudio.Play(Sfx.Footstep, Mathf.Clamp(0.18f * strength, 0.12f, 0.6f), 0.12f);
        }

        void OnPromoted(int level, string rank) => GameAudio.Play(Sfx.Promote, 0.9f, 0f);

        void OnAbility(Combat.ClassAbility ability)
        {
            GameAudio.Play(Sfx.ComboUp, 0.8f, 0f);
            GameAudio.Play(Sfx.HitHeavy, 0.4f);
        }

        void OnDamaged(Health target, DamageInfo info)
        {
            Vector3 at = target.transform.position + Vector3.up * 1.25f * target.transform.lossyScale.y;
            Vector3 away = info.knockback.sqrMagnitude > 0.01f ? info.knockback : Vector3.up;
            bool heavy = info.amount >= HeavyHitDamage;

            if (target.Team == Team.Player)
            {
                GameAudio.Play(Sfx.PlayerHurt, 0.85f);
                HitEffects.Burst(at, away, HurtRed, 8, 5f, 0.07f);
                HitEffects.Flash(at, HurtRed, 1.3f);
                ScreenFx.Kick(0.5f);
                if (GameSettings.DamageNumbers) HitEffects.Text(at + Vector3.up * 0.6f, Mathf.CeilToInt(info.amount).ToString(), HurtRed, 0.9f);
                return;
            }

            if (info.blocked)
            {
                // A dull tick and a spark: it didn't get through.
                GameAudio.Play(Sfx.UiMove, 0.5f, 0.15f);
                HitEffects.Burst(at, away, new Color(0.7f, 0.85f, 1f), 5, 5f, 0.04f);
                return;
            }
            GameAudio.Play(heavy || info.crit ? Sfx.HitHeavy : Sfx.HitLight, heavy || info.crit ? 1f : 0.8f);
            if (info.crit) HitEffects.Text(at + Vector3.up * 1.2f, "CRIT!", new Color(1f, 0.84f, 0.04f), 1.4f, 0.7f);
            // Paper scraps fly off coworkers; heavy hits throw sparks too.
            HitEffects.Burst(at, away, PaperWhite, heavy ? 14 : 8, heavy ? 8f : 6f);
            if (heavy) HitEffects.Burst(at, away, SparkYellow, 6, 10f, 0.05f);
            HitEffects.Flash(at, heavy || info.crit ? SparkYellow : PaperWhite, heavy ? 1.9f : 1.2f);
            if (heavy || info.crit) ScreenFx.Kick(heavy ? 0.55f : 0.4f);

            float size = Mathf.Lerp(0.8f, 1.6f, Mathf.InverseLerp(5f, 30f, info.amount));
            Color color = heavy ? SparkYellow : PaperWhite;
            if (GameSettings.DamageNumbers) HitEffects.Text(at + Vector3.up * 0.7f, Mathf.CeilToInt(info.amount).ToString(), color, size);
            if (heavy) SideScrollCamera.Shake(0.12f, 0.1f);
        }

        void OnDefeated(EnemyBrain enemy)
        {
            Vector3 at = enemy.transform.position + Vector3.up * 1.4f * enemy.transform.lossyScale.y;
            bool boss = enemy.Boss != BossRank.None;
            GameAudio.Play(Sfx.KnockOut, boss ? 1f : 0.85f);
            HitEffects.Flash(at, SparkYellow, boss ? 4f : 2.4f);
            HitEffects.Dust(enemy.transform.position, boss ? 1.2f : 0.8f, boss ? 10 : 6);
            ScreenFx.Kick(boss ? 1f : 0.35f);
            HitEffects.Burst(at, Vector3.up, PaperWhite, boss ? 60 : 22, boss ? 12f : 8f, boss ? 0.14f : 0.1f);
            HitEffects.Burst(at, Vector3.up, enemy.WorkerClass != null ? enemy.WorkerClass.tieColor : SparkYellow, boss ? 30 : 8, 9f);

            string line = boss
                ? enemy.Boss == BossRank.FinalBoss ? "HOSTILE TAKEOVER!" : "RESTRUCTURED!"
                : KnockoutLines[Random.Range(0, KnockoutLines.Length)];
            HitEffects.Text(at + Vector3.up * 0.9f, line, SparkYellow, boss ? 2.2f : 1.3f, boss ? 2f : 1.1f);
        }
    }
}
