using System.Collections.Generic;

namespace NineToFight.Combat
{
    /// <summary>
    /// Each department's career moves: learned on promotion after the starting light/heavy/signature
    /// (which live on the PlayerClass asset). Keyed by the PlayerClass asset name.
    /// Every class move has its own animation style within that class.
    /// </summary>
    public static class ClassMoveBook
    {
        static FightingMove M(string id, string name, MoveSlot slot, int level, string prop, AttackStyle style, string description,
            float damage, float reach, float radius, float knockback, float knockUp,
            float startup, float active, float recovery,
            float cooldown = 0f, float lunge = 0f, float hitStop = 0.05f, int hits = 1, float healOnHit = 0f, float moveMultiplier = 0.15f,
            AttackStyle[] combo = null)
        {
            return new FightingMove
            {
                id = id, displayName = name, slot = slot, unlockLevel = level, prop = prop, style = style, description = description, combo = combo,
                damage = damage, reach = reach, radius = radius, height = 1.2f, knockback = knockback, knockUp = knockUp,
                startup = startup, active = active, recovery = recovery, cooldown = cooldown, lunge = lunge, hitStop = hitStop,
                hits = hits, healOnHit = healOnHit, moveMultiplier = moveMultiplier,
            };
        }

        static readonly Dictionary<string, FightingMove[]> Book = new Dictionary<string, FightingMove[]>
        {
            ["Accountant"] = new[]
            {
                M("double_entry", "Double Entry", MoveSlot.Special, 3, "ledger", AttackStyle.Backhand,
                    "Every hit is recorded twice: a sweeping debit, then a slashing credit.",
                    11f, 1f, 0.75f, 5f, 1f, 0.14f, 0.08f, 0.14f, cooldown: 2f, lunge: 2f, hits: 2,
                    combo: new[] { AttackStyle.DiagonalSlash }),
                M("number_crunch", "Number Crunch", MoveSlot.Light, 5, "calculator", AttackStyle.LeadJab,
                    "Jab, hook, then an uppercut that rounds them up.",
                    5.5f, 0.95f, 0.65f, 2.5f, 0f, 0.17f, 0.06f, 0.14f, lunge: 1.5f, hitStop: 0.03f, hits: 3,
                    combo: new[] { AttackStyle.Hook, AttackStyle.Uppercut }),
                M("compound_interest", "Compound Interest", MoveSlot.Heavy, 6, "briefcase", AttackStyle.Uppercut,
                    "Small investment, huge returns. Launches.",
                    28f, 1f, 0.85f, 7f, 9f, 0.28f, 0.12f, 0.32f, lunge: 3f, hitStop: 0.09f),
                M("tax_bracket", "Tax Bracket", MoveSlot.Special, 7, "binder", AttackStyle.Cleave,
                    "A wide two-handed sweep. Nobody is exempt.",
                    30f, 1.2f, 1.6f, 12f, 3f, 0.32f, 0.14f, 0.34f, cooldown: 3f, hitStop: 0.1f),
                M("fiscal_year_end", "Fiscal Year End", MoveSlot.Super, 9, "audit_stamp", AttackStyle.Pound,
                    "Four stamps of APPROVED: pound, elbow, knee, then one big seal. Heals you for a quarter of the damage.",
                    13f, 1f, 1.4f, 6f, 2f, 0.2f, 0.08f, 0.12f, cooldown: 6f, hitStop: 0.06f, hits: 4, healOnHit: 0.25f,
                    combo: new[] { AttackStyle.Elbow, AttackStyle.Knee, AttackStyle.Stamp }),
            },
            ["ITSupport"] = new[]
            {
                M("ping_flood", "Ping Flood", MoveSlot.Special, 3, "ethernet_cable", AttackStyle.LeadJab,
                    "Jab, cross, jab, then a hook. Request timed out.",
                    4.5f, 1.3f, 0.55f, 1.5f, 0f, 0.11f, 0.05f, 0.13f, cooldown: 1.8f, lunge: 1f, hitStop: 0.025f, hits: 4,
                    combo: new[] { AttackStyle.Jab, AttackStyle.LeadJab, AttackStyle.Hook }),
                M("ctrl_alt_del", "Ctrl+Alt+Del", MoveSlot.Light, 5, "mouse", AttackStyle.Hook,
                    "Ctrl (hook), Alt (backhand), Del (straight down).",
                    5f, 1f, 0.6f, 2.5f, 0f, 0.17f, 0.05f, 0.14f, lunge: 1.5f, hitStop: 0.03f, hits: 3,
                    combo: new[] { AttackStyle.Backhand, AttackStyle.Chop }),
                M("firewall", "Firewall", MoveSlot.Heavy, 6, "laptop", AttackStyle.Slam,
                    "Blocks all incoming traffic. Permanently.",
                    24f, 1f, 1f, 16f, 4f, 0.28f, 0.12f, 0.3f, lunge: 2f, hitStop: 0.08f),
                M("hotfix", "Hotfix", MoveSlot.Special, 7, "smartphone", AttackStyle.Uppercut,
                    "Deployed straight to production. Launches.",
                    26f, 1f, 0.85f, 6f, 10f, 0.28f, 0.1f, 0.3f, cooldown: 2.5f, lunge: 4f, hitStop: 0.09f),
                M("rm_rf", "rm -rf /", MoveSlot.Super, 9, "laptop", AttackStyle.Cleave,
                    "Deletes everything in a very wide radius. No undo.",
                    52f, 1.2f, 1.7f, 16f, 8f, 0.36f, 0.16f, 0.4f, cooldown: 5f, hitStop: 0.14f),
            },
            ["SalesRep"] = new[]
            {
                M("upsell", "Upsell", MoveSlot.Special, 3, "golf_club", AttackStyle.Backhand,
                    "A wide swing, then a hook. Two for the price of one.",
                    13f, 1.1f, 0.8f, 6f, 1f, 0.16f, 0.08f, 0.14f, cooldown: 2f, lunge: 4f, hits: 2,
                    combo: new[] { AttackStyle.Hook }),
                M("speed_dial", "Speed Dial", MoveSlot.Light, 5, "smartphone", AttackStyle.LeadJab,
                    "Jab, cross, then a flying kick to hang up on them.",
                    5.5f, 0.95f, 0.6f, 3f, 0f, 0.11f, 0.05f, 0.13f, lunge: 2.5f, hitStop: 0.03f, hits: 3,
                    combo: new[] { AttackStyle.Jab, AttackStyle.FlyingKick }),
                M("hole_in_one", "Hole in One", MoveSlot.Heavy, 6, "golf_club", AttackStyle.Uppercut,
                    "Fore! Sends them sky-high.",
                    30f, 1.1f, 0.85f, 8f, 10f, 0.28f, 0.12f, 0.32f, lunge: 5f, hitStop: 0.1f),
                M("hard_sell", "Hard Sell", MoveSlot.Special, 7, "briefcase", AttackStyle.Chop,
                    "A huge running overhead with the briefcase. Closes any distance.",
                    32f, 1.5f, 0.9f, 13f, 4f, 0.3f, 0.14f, 0.34f, cooldown: 3f, lunge: 12f, hitStop: 0.11f),
                M("quarterly_quota", "Quarterly Quota", MoveSlot.Super, 9, "desk_phone", AttackStyle.Windmill,
                    "Wind up, sweep through, slam it down, charging forward. Targets will be met.",
                    15f, 1.2f, 1.1f, 9f, 3f, 0.34f, 0.14f, 0.26f, cooldown: 6f, lunge: 7f, hitStop: 0.07f, hits: 3,
                    combo: new[] { AttackStyle.Sweep, AttackStyle.Slam }),
            },
            ["HRRep"] = new[]
            {
                M("mandatory_training", "Mandatory Training", MoveSlot.Special, 3, "binder", AttackStyle.Cleave,
                    "Everyone around you attends. Big knockback.",
                    18f, 0.4f, 1.7f, 15f, 3f, 0.32f, 0.14f, 0.34f, cooldown: 2.5f, hitStop: 0.08f),
                M("written_warning", "Written Warning", MoveSlot.Light, 5, "clipboard", AttackStyle.Hook,
                    "A hook, then the clipboard to the face. Second offence.",
                    7.5f, 0.95f, 0.65f, 6f, 0f, 0.17f, 0.06f, 0.14f, lunge: 1.5f, hitStop: 0.04f, hits: 2,
                    combo: new[] { AttackStyle.Elbow }),
                M("restraining_order", "Restraining Order", MoveSlot.Heavy, 6, "rolled_report", AttackStyle.Dropkick,
                    "A two-footed dropkick. Stay 500 feet away.",
                    24f, 1.1f, 0.85f, 18f, 4f, 0.26f, 0.12f, 0.3f, lunge: 3f, hitStop: 0.09f),
                M("wellness_check", "Wellness Check", MoveSlot.Special, 7, "coffee_cup", AttackStyle.Hurl,
                    "A thrown mug of concern. Heals you for 60% of the damage.",
                    20f, 1.4f, 1.2f, 9f, 3f, 0.32f, 0.14f, 0.3f, cooldown: 3f, hitStop: 0.07f, healOnHit: 0.6f),
                M("mass_layoff", "Mass Layoff", MoveSlot.Super, 9, "megaphone", AttackStyle.Stamp,
                    "A shockwave that lets go of everyone nearby.",
                    44f, 0.3f, 2.6f, 20f, 7f, 0.38f, 0.18f, 0.4f, cooldown: 6f, hitStop: 0.14f),
            },
        };

        static readonly FightingMove[] None = new FightingMove[0];

        public static IReadOnlyList<FightingMove> For(string classId) =>
            classId != null && Book.TryGetValue(classId, out var moves) ? moves : None;

        public static IEnumerable<string> ClassIds => Book.Keys;
    }
}
