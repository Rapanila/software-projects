using System;
using System.Collections.Generic;
using NineToFight.Combat;
using NineToFight.Core;
using UnityEngine;

namespace NineToFight.Progression
{
    /// <summary>Scene-side wrapper around ProgressionState; applies stat growth on promotion.</summary>
    [RequireComponent(typeof(Health))]
    public class PlayerProgression : MonoBehaviour
    {
        [SerializeField] float baseMaxHealth = 100f;
        [SerializeField] float healthPerLevel = 15f;

        Health health;

        public ProgressionState State { get; private set; }
        public int Level => State.Level;
        public string Rank => State.Rank;

        public event Action Changed;
        public event Action<int, string> Promoted;
        public event Action<FightingMove> MoveLearned;

        void Awake()
        {
            health = GetComponent<Health>();
            SetState(new ProgressionState());
            health.SetMax(baseMaxHealth, true);
        }

        /// <summary>Starts a fresh career with a class's stats and move set.</summary>
        public void Configure(IReadOnlyList<FightingMove> moveSet, float maxHealth, float perLevel)
        {
            baseMaxHealth = maxHealth;
            healthPerLevel = perLevel;
            SetState(new ProgressionState(moveSet));
            health.SetMax(baseMaxHealth, true);
            Changed?.Invoke();
        }

        void SetState(ProgressionState state)
        {
            if (State != null)
            {
                State.LeveledUp -= OnLeveledUp;
                State.MoveUnlocked -= OnMoveUnlocked;
            }
            State = state;
            State.LeveledUp += OnLeveledUp;
            State.MoveUnlocked += OnMoveUnlocked;
        }

        void OnLeveledUp(int level)
        {
            health.SetMax(baseMaxHealth + healthPerLevel * (level - 1) + extraMaxHealth, true);
            Promoted?.Invoke(level, CareerLadder.RankTitle(level));
        }

        void OnMoveUnlocked(FightingMove move) => MoveLearned?.Invoke(move);

        public void AddXp(int amount)
        {
            State.AddXp(amount);
            Changed?.Invoke();
        }

        public bool LearnMove(string moveId)
        {
            bool learned = State.UnlockMove(moveId);
            if (learned) Changed?.Invoke();
            return learned;
        }

        public FightingMove MoveFor(MoveSlot slot) => State.BestMove(slot);

        float extraMaxHealth;

        /// <summary>Flat max-HP bonus on top of class and rank (perks). Refills on change.</summary>
        public float ExtraMaxHealth
        {
            get => extraMaxHealth;
            set
            {
                extraMaxHealth = value;
                health.SetMax(baseMaxHealth + healthPerLevel * (State.Level - 1) + extraMaxHealth, true);
                Changed?.Invoke();
            }
        }
    }
}
