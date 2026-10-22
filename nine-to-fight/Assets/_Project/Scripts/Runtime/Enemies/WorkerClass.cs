using NineToFight.Combat;
using UnityEngine;

namespace NineToFight.Enemies
{
    /// <summary>How a coworker fights. Auto picks from the job (IT throws, Accountants guard...).</summary>
    public enum EnemyRole
    {
        Auto,
        [Tooltip("Walks up and swings.")] Brawler,
        [Tooltip("Keeps its distance and throws office supplies down the lane.")] Thrower,
        [Tooltip("Blocks light hits from the front; heavies or hits from behind break through.")] Guard,
        [Tooltip("Hangs back and heals and buffs nearby coworkers.")] Support,
        [Tooltip("Bull-rushes across the floor; winded if it misses.")] Charger,
    }

    /// <summary>An office job archetype: stats, look and signature attack for one kind of coworker.</summary>
    [CreateAssetMenu(menuName = "Nine to Fight/Worker Class", fileName = "WorkerClass")]
    public class WorkerClass : ScriptableObject
    {
        public string displayName = "Intern";
        [TextArea] public string tagline;
        [Tooltip("Rung on the corporate ladder; higher tiers appear on higher floors.")]
        public int tier = 1;

        [Header("Look")]
        public Color shirtColor = Color.white;
        public Color tieColor = Color.red;
        public float scale = 1f;

        [Header("Stats")]
        public float maxHealth = 30f;
        public float moveSpeed = 3f;
        public float aggroRange = 9f;
        public int xpReward = 15;
        [Range(0f, 1f), Tooltip("Resistance to hit stun and knockback.")]
        public float poise;

        [Tooltip("Fighting style. Auto: decided by the job.")]
        public EnemyRole role;

        [Header("Attack")]
        public float attackCooldown = 1.5f;
        public FightingMove attack = new FightingMove
        {
            displayName = "Shove",
            damage = 6f, reach = 0.9f, radius = 0.6f, knockback = 5f,
            startup = 0.45f, active = 0.1f, recovery = 0.35f, moveMultiplier = 0f,
        };

        /// <summary>The role this coworker actually uses.</summary>
        public EnemyRole ResolvedRole
        {
            get
            {
                if (role != EnemyRole.Auto) return role;
                // Runtime copies are named "Base (Title)".
                string baseId = name;
                int paren = baseId.IndexOf(" (", System.StringComparison.Ordinal);
                if (paren > 0) baseId = baseId.Substring(0, paren);
                switch (baseId)
                {
                    case "ITSupport": return EnemyRole.Thrower;
                    case "Accountant": return EnemyRole.Guard;
                    case "HRRep": return EnemyRole.Support;
                    case "MiddleManager": return EnemyRole.Charger;
                    default: return EnemyRole.Brawler;
                }
            }
        }
    }
}
