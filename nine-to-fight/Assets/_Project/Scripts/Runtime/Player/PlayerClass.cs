using System.Collections.Generic;
using System.Linq;
using NineToFight.Combat;
using UnityEngine;

namespace NineToFight.Player
{
    /// <summary>
    /// The player's department: base stats, look and a class-specific move set.
    /// Rank (Intern to CEO) is separate and comes from XP; the class decides how you fight.
    /// </summary>
    [CreateAssetMenu(menuName = "Nine to Fight/Player Class", fileName = "PlayerClass")]
    public class PlayerClass : ScriptableObject
    {
        public string displayName = "Accountant";
        public string department = "Finance";
        [TextArea] public string tagline;
        [TextArea] public string perk;

        [Header("Look")]
        public Color shirtColor = Color.white;
        public Color tieColor = Color.red;
        [Tooltip("Outfit the department wears (CharacterLook outfit name, e.g. Man_Suit). Empty picks one.")]
        public string outfit;

        [Header("Stats")]
        public float maxHealth = 100f;
        public float healthPerLevel = 15f;
        public float moveSpeed = 6f;
        public float depthSpeed = 4f;
        public float jumpHeight = 1.6f;
        public float damageMultiplier = 1f;
        public float dodgeCooldown = 0.55f;

        [Header("Moves")]
        public FightingMove lightAttack = new FightingMove { slot = MoveSlot.Light };
        public FightingMove heavyAttack = new FightingMove { slot = MoveSlot.Heavy };
        [Tooltip("Class signature special, learned on the first promotion.")]
        public FightingMove signatureMove = new FightingMove { slot = MoveSlot.Special, unlockLevel = 2 };

        [Header("Q Ability")]
        public ClassAbility ability = new ClassAbility();

        public IReadOnlyList<FightingMove> BuildMoveSet()
        {
            var moves = new List<FightingMove> { lightAttack, heavyAttack, signatureMove };
            moves.AddRange(ClassMoveBook.For(name));
            moves.AddRange(MoveLibrary.Shared);
            // Hotbar order follows the career: earliest moves on the first keys (stable sort keeps 1 = light, 2 = heavy).
            return moves.OrderBy(m => m.unlockLevel).ToList();
        }
    }
}
