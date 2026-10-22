using System;
using UnityEngine;

namespace NineToFight.Combat
{
    public enum AbilityKind
    {
        [Tooltip("Restores 'amount' health.")] Heal,
        [Tooltip("Freezes every enemy within 'radius' for 'duration' seconds.")] StunBurst,
        [Tooltip("Multiplies speed by 'amount' and damage by 'damageMultiplier' for 'duration' seconds.")] Rush,
        [Tooltip("Blocks all damage for 'duration' seconds.")] Shield,
    }

    /// <summary>A class's Q ability: a small utility power on its own cooldown, separate from the attack hotbar.</summary>
    [Serializable]
    public class ClassAbility
    {
        public string id;
        public string displayName;
        [TextArea] public string description;
        public AbilityKind kind;
        public float cooldown = 10f;
        public float amount;
        public float damageMultiplier = 1f;
        public float radius = 4f;
        public float duration = 3f;
        public Color color = Color.white;
    }
}
