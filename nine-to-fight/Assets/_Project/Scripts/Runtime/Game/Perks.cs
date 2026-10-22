using System.Collections.Generic;
using System.Linq;

namespace NineToFight.Game
{
    public enum PerkId
    {
        DentalPlan, StockOptions, FlexTime, FreeCoffee, ExpenseAccount, TeamBuilding,
        CareerCoaching, PaidTimeOff, CornerDesk, GoldenParachute, HostileTakeover, Overtime,
    }

    public class Perk
    {
        public PerkId id;
        public string name;
        public string description;
        /// <summary>How many times it can be taken in one climb.</summary>
        public int maxStacks = 3;
        public UnityEngine.Color color = UnityEngine.Color.white;
    }

    /// <summary>The company benefits offered at the elevator between floors.</summary>
    public static class PerkLibrary
    {
        static UnityEngine.Color C(string hex) => UnityEngine.ColorUtility.TryParseHtmlString("#" + hex, out var c) ? c : UnityEngine.Color.white;

        public static readonly IReadOnlyList<Perk> All = new[]
        {
            new Perk { id = PerkId.DentalPlan, name = "Dental Plan", description = "+25 max HP and a full refill.", color = C("E8554B") },
            new Perk { id = PerkId.StockOptions, name = "Stock Options", description = "+15% damage on every hit.", color = C("F2A65A") },
            new Perk { id = PerkId.FlexTime, name = "Flex Time", description = "Dodge recharges 30% faster.", maxStacks = 2, color = C("4CC9F0") },
            new Perk { id = PerkId.FreeCoffee, name = "Free Coffee", description = "Coffee heals twice as much.", maxStacks = 2, color = C("A0522D") },
            new Perk { id = PerkId.ExpenseAccount, name = "Expense Account", description = "Heal for 6% of the damage you deal.", color = C("2ECC71") },
            new Perk { id = PerkId.TeamBuilding, name = "Team Building", description = "Combo window +1 second. Combos last longer.", maxStacks = 2, color = C("FAD63D") },
            new Perk { id = PerkId.CareerCoaching, name = "Career Coaching", description = "+30% XP from every knockout.", color = C("29C7B0") },
            new Perk { id = PerkId.PaidTimeOff, name = "Paid Time Off", description = "Q ability recharges 30% faster.", maxStacks = 2, color = C("D96CFF") },
            new Perk { id = PerkId.CornerDesk, name = "Corner Desk", description = "15% chance for a critical hit that deals double damage.", color = C("FFD60A") },
            new Perk { id = PerkId.GoldenParachute, name = "Golden Parachute", description = "Once per floor, survive a knockout with 35% HP.", maxStacks = 1, color = C("C9A44C") },
            new Perk { id = PerkId.HostileTakeover, name = "Hostile Takeover", description = "Knocked-out coworkers burst, hurting everyone near them.", maxStacks = 2, color = C("FF6B4A") },
            new Perk { id = PerkId.Overtime, name = "Overtime", description = "+12% move speed. Never stop moving.", maxStacks = 2, color = C("98C1D9") },
        };

        public static Perk Get(PerkId id) => All.First(p => p.id == id);

        /// <summary>Picks distinct perks the player can still take.</summary>
        public static List<Perk> Roll(System.Random rng, int count, PerkSet owned)
        {
            var pool = All.Where(p => owned == null || owned.Stacks(p.id) < p.maxStacks).ToList();
            var picks = new List<Perk>();
            while (picks.Count < count && pool.Count > 0)
            {
                int i = rng.Next(pool.Count);
                picks.Add(pool[i]);
                pool.RemoveAt(i);
            }
            return picks;
        }
    }

    /// <summary>Perks taken so far this climb, and the stat changes they add up to.</summary>
    public class PerkSet
    {
        readonly Dictionary<PerkId, int> stacks = new Dictionary<PerkId, int>();

        public event System.Action<Perk> Added;

        public int Stacks(PerkId id) => stacks.TryGetValue(id, out int n) ? n : 0;
        public int Count => stacks.Values.Sum();
        public IEnumerable<KeyValuePair<PerkId, int>> Owned => stacks;

        public bool Add(PerkId id)
        {
            var perk = PerkLibrary.Get(id);
            if (Stacks(id) >= perk.maxStacks) return false;
            stacks[id] = Stacks(id) + 1;
            Added?.Invoke(perk);
            return true;
        }

        public float MaxHealthBonus => 25f * Stacks(PerkId.DentalPlan);
        public float DamageMultiplier => 1f + 0.15f * Stacks(PerkId.StockOptions);
        public float DodgeCooldownMultiplier => UnityEngine.Mathf.Pow(0.7f, Stacks(PerkId.FlexTime));
        public float CoffeeMultiplier => 1f + Stacks(PerkId.FreeCoffee);
        public float Lifesteal => 0.06f * Stacks(PerkId.ExpenseAccount);
        public float ComboWindowBonus => 1f * Stacks(PerkId.TeamBuilding);
        public float XpMultiplier => 1f + 0.3f * Stacks(PerkId.CareerCoaching);
        public float AbilityCooldownMultiplier => UnityEngine.Mathf.Pow(0.7f, Stacks(PerkId.PaidTimeOff));
        public float CritChance => 0.15f * Stacks(PerkId.CornerDesk);
        public bool HasParachute => Stacks(PerkId.GoldenParachute) > 0;
        public float BurstDamage => 18f * Stacks(PerkId.HostileTakeover);
        public float SpeedMultiplier => 1f + 0.12f * Stacks(PerkId.Overtime);
    }
}
