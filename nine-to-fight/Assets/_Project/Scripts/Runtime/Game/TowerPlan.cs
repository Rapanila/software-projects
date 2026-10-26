using System.Collections.Generic;
using NineToFight.Combat;
using UnityEngine;

namespace NineToFight.Game
{
    public enum BossRank { None, MiniBoss, FinalBoss }

    /// <summary>A twist rolled for a floor so no two climbs play the same.</summary>
    public enum FloorEvent { None, CasualFriday, ReviewWeek, FireDrill, PowerOutage, AllHands, PizzaParty }

    /// <summary>A themed coworker for one floor: a base worker class re-dressed and re-titled.</summary>
    public struct Archetype
    {
        /// <summary>Asset name of the WorkerClass to start from (Intern, ITSupport, Accountant, HRRep, MiddleManager).</summary>
        public string baseId;
        public string title;
        public string shirt, tie;
        public BossRank boss;
        /// <summary>Optional office item this coworker fights with instead of the base class's (null keeps it).</summary>
        public string prop;
        public AttackStyle style;

        public Archetype(string baseId, string title, string shirt, string tie, BossRank boss = BossRank.None)
        {
            this.baseId = baseId; this.title = title; this.shirt = shirt; this.tie = tie; this.boss = boss;
            prop = null; style = AttackStyle.Jab;
        }

        /// <summary>The same archetype fighting with its own item (and the motion that suits it).</summary>
        public Archetype With(string item, AttackStyle motion)
        {
            var copy = this;
            copy.prop = item;
            copy.style = motion;
            return copy;
        }
    }

    public struct SpawnPoint
    {
        public Archetype who;
        public Vector3 position;
        /// <summary>Tougher "Senior" version: more health, bigger, better XP.</summary>
        public bool elite;
        /// <summary>For All-Hands floors: which wave this coworker arrives in.</summary>
        public int wave;
    }

    public class FloorPlan
    {
        public int number;
        public string name;
        public string tagline;
        public Color lightTint = Color.white;
        public Color carpetTint = Color.white;
        /// <summary>Health multiplier for every coworker on this floor.</summary>
        public float healthScale = 1f;
        public float damageScale = 1f;
        public float xpScale = 1f;
        public readonly List<SpawnPoint> spawns = new List<SpawnPoint>();
        public readonly List<Vector3> coffee = new List<Vector3>();
        public readonly List<Vector3> reviews = new List<Vector3>();
        /// <summary>Training seminars on this floor: where, and the move each one teaches.</summary>
        public readonly List<(Vector3 position, string moveId)> seminars = new List<(Vector3 position, string moveId)>();
        public FloorEvent floorEvent;
        /// <summary>Chance a beaten coworker drops coffee.</summary>
        public float coffeeDropChance = 0.2f;
        /// <summary>Fire Drill: seconds until the elevator locks again.</summary>
        public float timer;
        public int waveCount = 1;
        public bool HasBoss => spawns.Exists(s => s.who.boss != BossRank.None);
        public bool IsFinal => number == TowerPlan.FloorCount;
    }

    /// <summary>
    /// The nine floors between the lobby and the corner office. Floor 1 is the hand-built
    /// scene layout; floors 2-9 are generated here from themed rosters, harder each time.
    /// </summary>
    public static class TowerPlan
    {
        public const int FloorCount = 9;
        public const float FirstEncounterX = 9f;
        public const float LastEncounterX = 46f;
        const float LaneDepth = 2.2f;

        struct FloorTheme
        {
            public string name, tagline, light, carpet;
            public Archetype[] crew;
            public Archetype boss;
            public bool hasBoss;
        }

        static readonly FloorTheme[] Themes =
        {
            new FloorTheme { name = "The Cubicle Farm", tagline = "Fight your way to the elevator.", light = "FFF1DC", carpet = "FFFFFF" },
            new FloorTheme
            {
                name = "Accounting", tagline = "The spreadsheets fight back.", light = "E4F5DC", carpet = "B9D3B0",
                crew = new[]
                {
                    new Archetype("Accountant", "Junior Accountant", "9DB89A", "2E5E3A"),
                    new Archetype("Intern", "Audit Intern", "E8E4C9", "2E5E3A").With("stapler", AttackStyle.Jab),
                    new Archetype("Accountant", "Auditor", "5E7D6A", "D4AF37"),
                },
            },
            new FloorTheme
            {
                name = "Human Resources", tagline = "This will go on your permanent record.", light = "FFE3EA", carpet = "E7B8C6",
                crew = new[]
                {
                    new Archetype("HRRep", "HR Rep", "D9A5B3", "7A3E55"),
                    new Archetype("Intern", "Recruiter", "F2C6D0", "7A3E55"),
                },
                boss = new Archetype("MiddleManager", "HR Director", "7A3E55", "F2C6D0", BossRank.MiniBoss), hasBoss = true,
            },
            new FloorTheme
            {
                name = "IT Department", tagline = "Have you tried turning them off and on again?", light = "D6E6FF", carpet = "8FA6C8",
                crew = new[]
                {
                    new Archetype("ITSupport", "IT Support", "6C8EBF", "1F3A5F"),
                    new Archetype("Intern", "Help Desk Temp", "A7C4E5", "1F3A5F"),
                    new Archetype("Accountant", "Sysadmin", "2B3A55", "6CFF8E"),
                },
            },
            new FloorTheme
            {
                name = "Sales Floor", tagline = "Always Be Clobbering.", light = "FFE6C4", carpet = "E0B07A",
                crew = new[]
                {
                    new Archetype("HRRep", "Sales Rep", "E9A23B", "1D3557").With("golf_club", AttackStyle.Golf),
                    new Archetype("Intern", "Cold Caller", "F6D49A", "E9A23B").With("desk_phone", AttackStyle.Hook),
                    new Archetype("ITSupport", "Closer", "1D3557", "E9A23B").With("smartphone", AttackStyle.Whip),
                },
            },
            new FloorTheme
            {
                name = "Legal", tagline = "Objection! (Sustained, with a fist.)", light = "EDE6D8", carpet = "8C7E6A",
                crew = new[]
                {
                    new Archetype("Accountant", "Paralegal", "3D405B", "B5838D").With("briefcase", AttackStyle.Sweep),
                    new Archetype("HRRep", "Associate", "2F3E46", "CAD2C5"),
                },
                boss = new Archetype("MiddleManager", "Senior Counsel", "1B1B2F", "E0C097", BossRank.MiniBoss), hasBoss = true,
            },
            new FloorTheme
            {
                name = "Marketing", tagline = "Synergy, weaponized.", light = "F6DDFF", carpet = "C38BE0",
                crew = new[]
                {
                    new Archetype("Intern", "Social Media Intern", "FF8FAB", "3A0CA3"),
                    new Archetype("ITSupport", "Growth Hacker", "7209B7", "F72585"),
                    new Archetype("HRRep", "Brand Strategist", "4CC9F0", "3A0CA3").With("megaphone", AttackStyle.Blast),
                },
            },
            new FloorTheme
            {
                name = "Executive Suite", tagline = "Mahogany. Leather. Violence.", light = "FFE9C9", carpet = "7A4E3A",
                crew = new[]
                {
                    new Archetype("MiddleManager", "Middle Manager", "4A4E69", "C9184A"),
                    new Archetype("HRRep", "Executive Assistant", "E5E5E5", "22223B"),
                    new Archetype("Accountant", "CFO's Analyst", "264653", "E9C46A"),
                },
                boss = new Archetype("MiddleManager", "VP of Synergy", "22223B", "FFD60A", BossRank.MiniBoss), hasBoss = true,
            },
            new FloorTheme
            {
                name = "The Boardroom", tagline = "The corner office is right there.", light = "FFD9C2", carpet = "5B2333",
                crew = new[]
                {
                    new Archetype("MiddleManager", "Board Member", "2B2D42", "8D99AE"),
                    new Archetype("HRRep", "Chief of Staff", "1A1A2E", "E94560"),
                },
                boss = new Archetype("MiddleManager", "THE CEO", "111111", "FFD60A", BossRank.FinalBoss), hasBoss = true,
            },
        };

        /// <summary>The move a floor's training seminar teaches, or null if the floor has none (floor 1's is in the scene).</summary>
        public static string SeminarMove(int floor)
        {
            switch (floor)
            {
                case 3: return MoveLibrary.KeyboardSmash.id;
                case 5: return MoveLibrary.ReplyAll.id;
                case 7: return MoveLibrary.KeyboardSmash.id;
                default: return null;
            }
        }

        public static string FloorName(int floor) => Themes[Mathf.Clamp(floor, 1, FloorCount) - 1].name;

        public static string Tagline(int floor) => Themes[Mathf.Clamp(floor, 1, FloorCount) - 1].tagline;

        public static string NextFloorTeaser(int floor) =>
            floor >= FloorCount
                ? "The corner office is yours."
                : $"Floor {floor + 1}: {FloorName(floor + 1)}. {Tagline(floor + 1)}";

        /// <summary>Lays out one floor. Deterministic for a given floor and seed.</summary>
        public static FloorPlan Build(int floor, int seed = 0)
        {
            floor = Mathf.Clamp(floor, 1, FloorCount);
            var theme = Themes[floor - 1];
            var plan = new FloorPlan
            {
                number = floor,
                name = theme.name,
                tagline = theme.tagline,
                lightTint = Hex(theme.light),
                carpetTint = Hex(theme.carpet),
                healthScale = 1f + 0.2f * (floor - 1),
                damageScale = 1f + 0.1f * (floor - 1),
                xpScale = 1f + 0.15f * (floor - 1),
            };
            if (theme.crew == null) return plan; // floor 1 uses the hand-placed scene layout

            var rng = new System.Random(floor * 1009 + seed);
            const int encounters = 5;
            for (int e = 0; e < encounters; e++)
            {
                float x = Mathf.Lerp(FirstEncounterX, LastEncounterX - 6f, e / (float)(encounters - 1));
                // Groups grow through the floor and up the tower.
                int count = 1 + (e + floor) / 3;
                bool finalEncounter = e == encounters - 1;
                if (finalEncounter && theme.hasBoss) count = Mathf.Max(1, count - 1);
                for (int i = 0; i < count; i++)
                {
                    var who = theme.crew[rng.Next(theme.crew.Length)];
                    plan.spawns.Add(new SpawnPoint
                    {
                        who = who,
                        position = new Vector3(x + (float)rng.NextDouble() * 4f, 0f, Mathf.Lerp(-LaneDepth, LaneDepth, (float)rng.NextDouble())),
                        elite = floor >= 3 && rng.NextDouble() < 0.05f * floor,
                    });
                }
            }
            if (theme.hasBoss)
                plan.spawns.Add(new SpawnPoint { who = theme.boss, position = new Vector3(LastEncounterX, 0f, 0f) });

            // A coffee between each encounter, a performance review mid-floor.
            for (int e = 0; e < encounters - 1; e++)
            {
                float x = Mathf.Lerp(FirstEncounterX, LastEncounterX - 6f, (e + 0.5f) / (encounters - 1)) + 2f;
                if (e % 2 == 0 || floor >= 6) plan.coffee.Add(new Vector3(x, 0f, e % 2 == 0 ? 1.6f : -1.6f));
            }
            plan.reviews.Add(new Vector3((FirstEncounterX + LastEncounterX) / 2f, 0f, -1.8f));
            plan.coffee.Add(new Vector3(LastEncounterX + 4f, 0f, 0f));
            // A training seminar on every other floor teaches a shared move early (or pays out XP if it's known).
            string seminar = SeminarMove(floor);
            if (seminar != null)
                plan.seminars.Add((new Vector3(Mathf.Lerp(FirstEncounterX, LastEncounterX, 0.3f), 0f, 1.8f), seminar));
            return plan;
        }

        // ------------------------------------------------------------------ floor events

        public static string EventName(FloorEvent e)
        {
            switch (e)
            {
                case FloorEvent.CasualFriday: return "Casual Friday";
                case FloorEvent.ReviewWeek: return "Performance Review Week";
                case FloorEvent.FireDrill: return "Fire Drill";
                case FloorEvent.PowerOutage: return "Power Outage";
                case FloorEvent.AllHands: return "All-Hands Meeting";
                case FloorEvent.PizzaParty: return "Pizza Party";
                default: return "";
            }
        }

        public static string EventDescription(FloorEvent e)
        {
            switch (e)
            {
                case FloorEvent.CasualFriday: return "Everyone's in shorts. More coworkers, but they go down easy.";
                case FloorEvent.ReviewWeek: return "Everyone's trying to impress. Enemies hit 25% harder, XP is doubled.";
                case FloorEvent.FireDrill: return "The elevator is open now. Get there before the drill ends and it locks, or fight everyone.";
                case FloorEvent.PowerOutage: return "The lights are out. Fight by the glow of your phone's flashlight.";
                case FloorEvent.AllHands: return "Mandatory meeting. Coworkers arrive in waves from both sides.";
                case FloorEvent.PizzaParty: return "Free pizza everywhere. Coworkers drop slices more often.";
                default: return "";
            }
        }

        /// <summary>Picks an event for a floor, never repeating one already seen this climb. Boss floors stay focused.</summary>
        public static FloorEvent RollEvent(int floor, System.Random rng, ICollection<FloorEvent> used)
        {
            if (floor <= 1 || Themes[Mathf.Clamp(floor, 1, FloorCount) - 1].hasBoss) return FloorEvent.None;
            var pool = new List<FloorEvent>();
            foreach (FloorEvent e in System.Enum.GetValues(typeof(FloorEvent)))
                if (e != FloorEvent.None && (used == null || !used.Contains(e))) pool.Add(e);
            return pool.Count == 0 ? FloorEvent.None : pool[rng.Next(pool.Count)];
        }

        /// <summary>Reshapes a floor plan for its event.</summary>
        public static void ApplyEvent(FloorPlan plan, FloorEvent e, System.Random rng)
        {
            plan.floorEvent = e;
            switch (e)
            {
                case FloorEvent.CasualFriday:
                    plan.healthScale *= 0.65f;
                    int extra = Mathf.CeilToInt(plan.spawns.Count * 0.5f);
                    for (int i = 0; i < extra; i++)
                    {
                        var s = plan.spawns[i % plan.spawns.Count];
                        if (s.who.boss != BossRank.None) continue;
                        s.position.x = Mathf.Min(s.position.x + 1.2f + (float)rng.NextDouble(), LastEncounterX);
                        s.position.z = Mathf.Lerp(-LaneDepth, LaneDepth, (float)rng.NextDouble());
                        s.elite = false;
                        plan.spawns.Add(s);
                    }
                    break;
                case FloorEvent.ReviewWeek:
                    plan.xpScale *= 2f;
                    plan.damageScale *= 1.25f;
                    break;
                case FloorEvent.FireDrill:
                    plan.timer = 50f;
                    break;
                case FloorEvent.AllHands:
                    plan.waveCount = 3;
                    for (int i = 0; i < plan.spawns.Count; i++)
                    {
                        var s = plan.spawns[i];
                        s.wave = i * plan.waveCount / plan.spawns.Count;
                        plan.spawns[i] = s;
                    }
                    break;
                case FloorEvent.PizzaParty:
                    plan.coffeeDropChance = 0.55f;
                    int count = plan.coffee.Count;
                    for (int i = 0; i < count; i++) plan.coffee.Add(plan.coffee[i] + new Vector3(3f, 0f, 0f));
                    break;
            }
        }

        public static Color Hex(string hex) =>
            ColorUtility.TryParseHtmlString("#" + hex, out var c) ? c : Color.white;
    }
}
