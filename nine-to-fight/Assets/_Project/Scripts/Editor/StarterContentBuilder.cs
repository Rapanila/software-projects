using System.Collections.Generic;
using System.IO;
using System.Linq;
using NineToFight.CameraRig;
using NineToFight.Characters;
using NineToFight.Combat;
using NineToFight.Core;
using NineToFight.Enemies;
using NineToFight.Game;
using NineToFight.Items;
using NineToFight.Player;
using NineToFight.Progression;
using NineToFight.UI;
using NineToFight.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace NineToFight.EditorTools
{
    /// <summary>
    /// Generates the starter content (materials, classes, prefabs, Floor 1 scene) from code,
    /// so the whole slice can be rebuilt after tweaking numbers here.
    /// </summary>
    public static partial class StarterContentBuilder
    {
        const string Root = "Assets/_Project";
        const string MaterialsDir = Root + "/Materials";
        const string PrefabsDir = Root + "/Prefabs";
        const string PlayerClassesDir = Root + "/Data/PlayerClasses";
        const string WorkerClassesDir = Root + "/Data/WorkerClasses";
        const string ScenesDir = Root + "/Scenes";
        public const string FloorScenePath = ScenesDir + "/Floor01_CubicleFarm.unity";

        const float LaneDepth = 2.5f;
        const float LevelStartX = -4f;
        const float LevelEndX = 56f;

        const string PropsDir = PrefabsDir + "/Props";
        public const string PropCatalogPath = Root + "/Data/PropCatalog.asset";

        static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        static PropCatalog propCatalog;

        [MenuItem("Nine to Fight/Build Starter Content (Floor 1)", priority = 0)]
        public static void BuildAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Materials.Clear();
            CharacterParts.Clear();
            foreach (var dir in new[] { MaterialsDir, PrefabsDir, PropsDir, PlayerClassesDir, WorkerClassesDir, ScenesDir })
                EnsureFolder(dir);

            BuildFxAssets();
            propCatalog = BuildProps();
            var playerClasses = CreatePlayerClasses();
            var workers = CreateWorkerClasses();
            AssetDatabase.SaveAssets();
            // Opening the new scene unloads saved assets nothing holds on to, so the scene loads these by path.
            var classPaths = playerClasses.Select(AssetDatabase.GetAssetPath).ToArray();
            var workerPaths = workers.ToDictionary(w => w.Key, w => AssetDatabase.GetAssetPath(w.Value));
            fightAnimations = BuildFightAnimations();
            var playerPrefab = BuildPlayerPrefab();
            var enemyPrefab = BuildEnemyPrefab();
            BuildFloorScene(playerPrefab, enemyPrefab, classPaths, workerPaths);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Nine to Fight] Starter content built. Open {FloorScenePath} and press Play.");
        }

        // ------------------------------------------------------------------ data

        static PlayerClass[] CreatePlayerClasses()
        {
            var accountant = Asset<PlayerClass>(PlayerClassesDir, "Accountant");
            accountant.displayName = "Accountant";
            accountant.department = "Finance";
            accountant.tagline = "\"The numbers don't lie. Neither do bruises.\"";
            accountant.perk = "Balanced books: no strengths, no weaknesses. Good first hire.";
            accountant.shirtColor = Hex("E8EEF5");
            accountant.tieColor = Hex("2E7D5B");
            accountant.outfit = "Woman_Suit";
            accountant.maxHealth = 100f; accountant.healthPerLevel = 15f;
            accountant.moveSpeed = 6f; accountant.depthSpeed = 4f; accountant.jumpHeight = 1.6f;
            accountant.damageMultiplier = 1f; accountant.dodgeCooldown = 0.55f;
            accountant.lightAttack = Move("calculator_jab", "Calculator Jab", MoveSlot.Light, 1, "Quick and precise, to two decimal places.",
                8f, 0.9f, 0.6f, 3f, 0f, 0.09f, 0.08f, 0.16f, lunge: 2f, hitStop: 0.03f, prop: "calculator", style: AttackStyle.Jab);
            accountant.heavyAttack = Move("ledger_slam", "Ledger Slam", MoveSlot.Heavy, 1, "A hardback ledger, swung with conviction.",
                18f, 1.1f, 0.8f, 9f, 3f, 0.24f, 0.12f, 0.32f, lunge: 4f, hitStop: 0.07f, prop: "ledger", style: AttackStyle.DiagonalSlash);
            accountant.signatureMove = Move("audit", "The Audit", MoveSlot.Special, 2, "Wide-arc smack that finds every discrepancy.",
                26f, 1.4f, 1.3f, 11f, 4f, 0.28f, 0.16f, 0.36f, cooldown: 2.5f, hitStop: 0.09f, prop: "audit_stamp", style: AttackStyle.Stamp);

            var it = Asset<PlayerClass>(PlayerClassesDir, "ITSupport");
            it.displayName = "IT Support";
            it.department = "Tech";
            it.tagline = "\"Have you tried turning them off and on again?\"";
            it.perk = "Lightning fast with a near-instant dodge, but bruises easily.";
            it.shirtColor = Hex("3D5A80");
            it.tieColor = Hex("98C1D9");
            it.outfit = "Man_Hoodie";
            it.maxHealth = 80f; it.healthPerLevel = 12f;
            it.moveSpeed = 7.2f; it.depthSpeed = 4.8f; it.jumpHeight = 1.8f;
            it.damageMultiplier = 0.9f; it.dodgeCooldown = 0.45f;
            it.lightAttack = Move("cable_whip", "Cable Whip", MoveSlot.Light, 1, "Ethernet cable. Category 6 pain.",
                7f, 1.2f, 0.55f, 3f, 0f, 0.2f, 0.08f, 0.16f, lunge: 1.5f, hitStop: 0.03f, prop: "ethernet_cable", style: AttackStyle.Whip);
            it.heavyAttack = Move("mouse_flail", "Mouse Flail", MoveSlot.Heavy, 1, "A wired mouse spun like a flail.",
                15f, 1.3f, 0.8f, 8f, 2f, 0.32f, 0.14f, 0.24f, lunge: 3f, hitStop: 0.06f, prop: "mouse", style: AttackStyle.Windmill);
            it.signatureMove = Move("hard_reboot", "Hard Reboot", MoveSlot.Special, 2, "Turns your opponent off. And on. And off.",
                24f, 1f, 1.1f, 15f, 7f, 0.22f, 0.14f, 0.34f, cooldown: 2f, lunge: 6f, hitStop: 0.1f, prop: "laptop", style: AttackStyle.Shove);

            var sales = Asset<PlayerClass>(PlayerClassesDir, "SalesRep");
            sales.displayName = "Sales Rep";
            sales.department = "Sales";
            sales.tagline = "\"Always. Be. Closing. Distance.\"";
            sales.perk = "Hits the hardest and lunges far. Commission-based violence.";
            sales.shirtColor = Hex("C0392B");
            sales.tieColor = Hex("F1C40F");
            sales.outfit = "Man_Suit";
            sales.maxHealth = 90f; sales.healthPerLevel = 14f;
            sales.moveSpeed = 6.4f; sales.depthSpeed = 4.2f; sales.jumpHeight = 1.6f;
            sales.damageMultiplier = 1.15f; sales.dodgeCooldown = 0.5f;
            sales.lightAttack = Move("smartphone_slap", "Smartphone Slap", MoveSlot.Light, 1, "Latest model. Cracked screen. Worth it.",
                8f, 0.9f, 0.6f, 3.5f, 0f, 0.16f, 0.08f, 0.18f, lunge: 3f, hitStop: 0.03f, prop: "smartphone", style: AttackStyle.Sweep);
            sales.heavyAttack = Move("closing_pitch", "Closing Pitch", MoveSlot.Heavy, 1, "Charge in and seal the deal.",
                19f, 1.1f, 0.8f, 10f, 3f, 0.26f, 0.14f, 0.32f, lunge: 8f, hitStop: 0.08f, prop: "golf_club", style: AttackStyle.Golf);
            sales.signatureMove = Move("cold_call", "Cold Call", MoveSlot.Special, 2, "A long-reach lunge nobody asked for.",
                28f, 2f, 0.9f, 12f, 3f, 0.3f, 0.18f, 0.4f, cooldown: 2.5f, lunge: 12f, hitStop: 0.1f, prop: "desk_phone", style: AttackStyle.Lunge);

            var hr = Asset<PlayerClass>(PlayerClassesDir, "HRRep");
            hr.displayName = "HR Rep";
            hr.department = "People";
            hr.tagline = "\"This is going on your permanent record.\"";
            hr.perk = "Slow but very hard to fire. Huge knockback keeps crowds back.";
            hr.shirtColor = Hex("8E6C8A");
            hr.tieColor = Hex("F2D0A4");
            hr.outfit = "Woman_Dress";
            hr.maxHealth = 130f; hr.healthPerLevel = 20f;
            hr.moveSpeed = 5.2f; hr.depthSpeed = 3.6f; hr.jumpHeight = 1.4f;
            hr.damageMultiplier = 1.05f; hr.dodgeCooldown = 0.7f;
            hr.lightAttack = Move("policy_slap", "Policy Slap", MoveSlot.Light, 1, "Section 4, paragraph 2: no running.",
                9f, 0.9f, 0.65f, 5f, 0f, 0.15f, 0.08f, 0.18f, lunge: 1.5f, hitStop: 0.04f, prop: "clipboard", style: AttackStyle.Backhand);
            hr.heavyAttack = Move("binder_of_rules", "Binder of Rules", MoveSlot.Heavy, 1, "Three inches of policy, laminated.",
                20f, 1.1f, 0.9f, 13f, 4f, 0.28f, 0.14f, 0.36f, lunge: 3f, hitStop: 0.08f, prop: "binder", style: AttackStyle.Slam);
            hr.signatureMove = Move("disciplinary_action", "Disciplinary Action", MoveSlot.Special, 2, "A shockwave of paperwork that hits everyone nearby.",
                24f, 0.3f, 2f, 14f, 5f, 0.35f, 0.18f, 0.4f, cooldown: 3f, hitStop: 0.1f, prop: "megaphone", style: AttackStyle.Blast);

            accountant.ability = new ClassAbility
            {
                id = "tax_refund", displayName = "Tax Refund", kind = AbilityKind.Heal,
                description = "Claim it all back. Instantly restores 35 HP.",
                amount = 35f, cooldown = 12f, color = Hex("2ECC71"),
            };
            it.ability = new ClassAbility
            {
                id = "blue_screen", displayName = "Blue Screen", kind = AbilityKind.StunBurst,
                description = "Crashes every coworker nearby. They freeze for 2 seconds.",
                radius = 4.5f, duration = 2.2f, cooldown = 11f, color = Hex("1E6BFF"),
            };
            sales.ability = new ClassAbility
            {
                id = "hustle", displayName = "Hustle", kind = AbilityKind.Rush,
                description = "Coffee-fuelled sprint: 50% faster and 30% more damage for 4 seconds.",
                amount = 1.5f, damageMultiplier = 1.3f, duration = 4f, cooldown = 13f, color = Hex("F5B700"),
            };
            hr.ability = new ClassAbility
            {
                id = "formal_complaint", displayName = "Formal Complaint", kind = AbilityKind.Shield,
                description = "Nobody wants the paperwork. Untouchable for 3 seconds.",
                duration = 3f, cooldown = 14f, color = Hex("D96CFF"),
            };

            var all = new[] { accountant, it, sales, hr };
            foreach (var c in all) EditorUtility.SetDirty(c);
            return all;
        }

        static Dictionary<string, WorkerClass> CreateWorkerClasses()
        {
            var result = new Dictionary<string, WorkerClass>();

            void Define(string id, string name, string tagline, int tier, string shirt, string tie, float scale,
                        float hp, float speed, int xp, float poise, float cooldown, FightingMove attack)
            {
                var w = Asset<WorkerClass>(WorkerClassesDir, id);
                w.displayName = name; w.tagline = tagline; w.tier = tier;
                w.shirtColor = Hex(shirt); w.tieColor = Hex(tie); w.scale = scale;
                w.maxHealth = hp; w.moveSpeed = speed; w.xpReward = xp; w.poise = poise;
                w.attackCooldown = cooldown; w.aggroRange = 9f;
                w.attack = attack;
                EditorUtility.SetDirty(w);
                result[id] = w;
            }

            Define("Intern", "Intern", "Unpaid and unhinged.", 1, "F4E4A1", "7A6A3A", 0.92f,
                28f, 3.4f, 15, 0f, 1.5f,
                Move("coffee_fling", "Coffee Run", MoveSlot.Light, 1, "", 5f, 0.85f, 0.6f, 4f, 0f, 0.45f, 0.1f, 0.35f, moveMultiplier: 0f, prop: "coffee_cup", style: AttackStyle.Toss));
            Define("ITSupport", "IT Support", "Your ticket has been closed.", 1, "6C8EBF", "1F3A5F", 1f,
                38f, 3.8f, 25, 0f, 1.2f,
                Move("cable_lash", "Cable Lash", MoveSlot.Light, 1, "", 7f, 1.1f, 0.55f, 5f, 0f, 0.4f, 0.1f, 0.3f, moveMultiplier: 0f, prop: "ethernet_cable", style: AttackStyle.Whip));
            Define("Accountant", "Accountant", "Crunches numbers. And bones.", 1, "9DB89A", "2E5E3A", 1f,
                48f, 2.7f, 25, 0.15f, 1.8f,
                Move("ledger_bonk", "Ledger Bonk", MoveSlot.Heavy, 1, "", 10f, 1f, 0.7f, 7f, 2f, 0.55f, 0.12f, 0.4f, moveMultiplier: 0f, prop: "ledger", style: AttackStyle.DiagonalSlash));
            Define("HRRep", "HR Rep", "This will go on your record.", 2, "D9A5B3", "7A3E55", 1f,
                55f, 2.9f, 35, 0.2f, 1.6f,
                Move("write_up", "Write-Up", MoveSlot.Heavy, 1, "", 11f, 1f, 0.75f, 8f, 2f, 0.5f, 0.12f, 0.4f, moveMultiplier: 0f, prop: "clipboard", style: AttackStyle.Backhand));
            Define("MiddleManager", "Middle Manager", "Let's circle back to your beatdown.", 3, "4A4E69", "C9184A", 1.22f,
                140f, 2.6f, 90, 0.55f, 1.9f,
                Move("performance_review", "Performance Review", MoveSlot.Heavy, 1, "", 15f, 1.2f, 0.95f, 11f, 4f, 0.6f, 0.15f, 0.45f, moveMultiplier: 0f, lunge: 3f, prop: "rolled_report", style: AttackStyle.Chop));

            return result;
        }

        static FightingMove Move(string id, string name, MoveSlot slot, int unlockLevel, string description,
            float damage, float reach, float radius, float knockback, float knockUp,
            float startup, float active, float recovery,
            float cooldown = 0f, float lunge = 0f, float hitStop = 0.05f, float moveMultiplier = 0.15f, string prop = null,
            AttackStyle style = AttackStyle.Jab)
        {
            return new FightingMove
            {
                id = id, displayName = name, slot = slot, prop = prop, style = style, unlockLevel = unlockLevel, description = description,
                damage = damage, reach = reach, radius = radius, height = 1.2f, knockback = knockback, knockUp = knockUp,
                startup = startup, active = active, recovery = recovery, cooldown = cooldown,
                lunge = lunge, hitStop = hitStop, moveMultiplier = moveMultiplier,
            };
        }

        // ------------------------------------------------------------------ props

        /// <summary>
        /// Unposed office items. Per-item, per-body holding transforms and finger poses are authored
        /// against the original T-pose in HeldItemGrips.json, rather than inferred from item bounds.
        /// </summary>
        [MenuItem("Nine to Fight/Rebuild Held Items", priority = 10)]
        public static void RebuildHeldItems()
        {
            Materials.Clear();
            propCatalog = BuildProps();
            foreach (var name in new[] { "Player", "Coworker" })
            {
                string path = $"{PrefabsDir}/{name}.prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var follower = root.GetComponentInChildren<RigFollower>(true);
                    var socket = (Transform)new SerializedObject(follower).FindProperty("handSocket").objectReferenceValue;
                    var rig = follower.ActiveRig;
                    socket.SetPositionAndRotation(rig.hand.TransformPoint(rig.grip), rig.hand.rotation * rig.gripRotation);
                    for (int i = 0; i < follower.RigCount; i++)
                    {
                        var body = follower.GetRig(i);
                        var neutral = body.animator.avatar.humanDescription.skeleton.ToDictionary(b => b.name, b => b.rotation);
                        body.fingerOpen = body.fingers.Select(f => neutral[f.name]).ToArray();
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
        }

        static PropCatalog BuildProps()
        {
            var entries = new List<PropCatalog.Entry>();
            void Prop(string id, string name, System.Action<Transform> build)
            {
                var root = new GameObject("Prop_" + id);
                build(root.transform);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PropsDir}/{id}.prefab");
                Object.DestroyImmediate(root);
                entries.Add(new PropCatalog.Entry { id = id, displayName = name, prefab = prefab });
            }

            var plasticDark = Mat("Prop_PlasticDark", Hex("2B2E35"), 0.4f);
            var plasticLight = Mat("Prop_PlasticLight", Hex("D9DCE1"), 0.4f);
            var metal = Mat("Prop_Metal", Hex("B8C0CC"), 0.8f);
            var paper = Mat("Paper", Hex("F5F1E6"));
            var leather = Mat("Prop_Leather", Hex("6B4226"), 0.45f);
            var red = Mat("Prop_Red", Hex("C0392B"), 0.4f);

            Prop("calculator", "Calculator", t =>
            {
                Part(PrimitiveType.Cube, "Body", t, new Vector3(0f, -0.15f, 0f), new Vector3(0.05f, 0.3f, 0.2f), plasticDark);
                Part(PrimitiveType.Cube, "Display", t, new Vector3(0.03f, -0.06f, 0f), new Vector3(0.01f, 0.07f, 0.16f),
                    Mat("Prop_LCD", Hex("A8C686"), emission: Hex("2F4020")));
                Part(PrimitiveType.Cube, "Keypad", t, new Vector3(0.03f, -0.2f, 0f), new Vector3(0.01f, 0.16f, 0.16f), plasticLight);
            });
            Prop("ledger", "Ledger", t =>
            {
                Part(PrimitiveType.Cube, "Cover", t, new Vector3(0f, -0.24f, 0f), new Vector3(0.09f, 0.5f, 0.38f), Mat("Prop_LedgerGreen", Hex("1E5631"), 0.3f));
                Part(PrimitiveType.Cube, "Pages", t, new Vector3(0f, -0.24f, 0.01f), new Vector3(0.07f, 0.47f, 0.37f), paper);
                Part(PrimitiveType.Cube, "Label", t, new Vector3(0.046f, -0.18f, 0f), new Vector3(0.005f, 0.1f, 0.2f), Mat("Prop_Gold", Hex("D4AF37"), 0.8f));
            });
            Prop("audit_stamp", "Audit Stamp", t =>
            {
                var wood = Mat("Prop_Wood", Hex("8B5A2B"), 0.3f);
                Part(PrimitiveType.Sphere, "Knob", t, new Vector3(0f, 0.095f, 0f), new Vector3(0.05f, 0.05f, 0.05f), wood);
                Part(PrimitiveType.Cylinder, "Handle", t, Vector3.zero, new Vector3(0.022f, 0.09f, 0.022f), wood);
                Part(PrimitiveType.Cube, "Block", t, new Vector3(0f, -0.14f, 0f), new Vector3(0.16f, 0.1f, 0.24f), red);
                Part(PrimitiveType.Cube, "Rubber", t, new Vector3(0f, -0.2f, 0f), new Vector3(0.15f, 0.03f, 0.22f), Mat("Prop_Ink", Hex("6E0E0A")));
            });
            Prop("ethernet_cable", "Ethernet Cable", t =>
            {
                var blue = Mat("Prop_CableBlue", Hex("2F80ED"), 0.5f);
                Part(PrimitiveType.Cube, "Cable", t, new Vector3(0f, -0.3f, 0f), new Vector3(0.015f, 0.6f, 0.015f), blue);
                var tip = Part(PrimitiveType.Cube, "Cable Tip", t, new Vector3(0f, -0.7f, 0.08f), new Vector3(0.015f, 0.25f, 0.015f), blue);
                tip.transform.localRotation = Quaternion.Euler(-35f, 0f, 0f);
                Part(PrimitiveType.Cube, "Connector", t, new Vector3(0f, -0.82f, 0.16f), new Vector3(0.05f, 0.08f, 0.05f),
                    Mat("Prop_ClearPlastic", new Color(0.85f, 0.9f, 0.95f), 0.9f));
            });
            Prop("mouse", "Computer Mouse", t =>
            {
                Part(PrimitiveType.Cube, "Cord", t, new Vector3(0f, -0.24f, 0f), new Vector3(0.015f, 0.48f, 0.015f), plasticDark);
                Part(PrimitiveType.Sphere, "Mouse", t, new Vector3(0f, -0.55f, 0f), new Vector3(0.1f, 0.18f, 0.12f), plasticLight);
                Part(PrimitiveType.Cube, "Wheel", t, new Vector3(0f, -0.49f, 0.055f), new Vector3(0.015f, 0.03f, 0.02f), plasticDark);
            });
            Prop("laptop", "Laptop", t =>
            {
                Part(PrimitiveType.Cube, "Shell", t, new Vector3(0f, -0.2f, 0f), new Vector3(0.05f, 0.38f, 0.3f), metal);
                Part(PrimitiveType.Cube, "Logo", t, new Vector3(0.027f, -0.2f, 0f), new Vector3(0.005f, 0.06f, 0.06f),
                    Mat("Prop_Glow", Hex("FFFFFF"), emission: Hex("BBBBBB")));
            });
            Prop("smartphone", "Smartphone", t =>
            {
                Part(PrimitiveType.Cube, "Body", t, new Vector3(0f, -0.1f, 0f), new Vector3(0.02f, 0.18f, 0.09f), plasticDark);
                Part(PrimitiveType.Cube, "Screen", t, new Vector3(0.012f, -0.1f, 0f), new Vector3(0.005f, 0.16f, 0.08f),
                    Mat("Prop_PhoneScreen", Hex("4FC3F7"), 0.9f, emission: Hex("1E88E5")));
            });
            Prop("golf_club", "Golf Club", t =>
            {
                Part(PrimitiveType.Cylinder, "Grip", t, new Vector3(0f, -0.06f, 0f), new Vector3(0.022f, 0.1f, 0.022f), plasticDark);
                Part(PrimitiveType.Cylinder, "Shaft", t, new Vector3(0f, -0.6f, 0f), new Vector3(0.018f, 0.5f, 0.018f), metal);
                Part(PrimitiveType.Cube, "Head", t, new Vector3(0f, -1.12f, 0.07f), new Vector3(0.08f, 0.07f, 0.2f), Mat("Prop_ClubHead", Hex("3A3F47"), 0.9f));
            });
            Prop("desk_phone", "Desk Phone Handset", t =>
            {
                var beige = Mat("Prop_Beige", Hex("D8CFC0"), 0.3f);
                Part(PrimitiveType.Cube, "Handle", t, new Vector3(0f, -0.16f, 0f), new Vector3(0.045f, 0.34f, 0.05f), beige);
                Part(PrimitiveType.Cube, "Earpiece", t, new Vector3(0f, -0.01f, 0.04f), new Vector3(0.09f, 0.08f, 0.11f), beige);
                Part(PrimitiveType.Cube, "Mouthpiece", t, new Vector3(0f, -0.32f, 0.04f), new Vector3(0.09f, 0.08f, 0.11f), beige);
                var cord = Part(PrimitiveType.Cube, "Cord", t, new Vector3(0f, -0.48f, -0.04f), new Vector3(0.02f, 0.3f, 0.02f), plasticDark);
                cord.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
            });
            Prop("clipboard", "Clipboard", t =>
            {
                Part(PrimitiveType.Cube, "Board", t, new Vector3(0f, -0.22f, 0f), new Vector3(0.025f, 0.42f, 0.3f), Mat("Prop_Masonite", Hex("9C6B3C"), 0.2f));
                Part(PrimitiveType.Cube, "Paper", t, new Vector3(0.018f, -0.24f, 0f), new Vector3(0.006f, 0.36f, 0.26f), paper);
                Part(PrimitiveType.Cube, "Clip", t, new Vector3(0.02f, -0.04f, 0f), new Vector3(0.03f, 0.05f, 0.12f), metal);
            });
            Prop("binder", "Binder of Rules", t =>
            {
                Part(PrimitiveType.Cube, "Binder", t, new Vector3(0f, -0.22f, 0f), new Vector3(0.14f, 0.44f, 0.36f), Mat("Prop_BinderBlue", Hex("1F4E8C"), 0.5f));
                Part(PrimitiveType.Cube, "Label", t, new Vector3(0.072f, -0.22f, 0f), new Vector3(0.005f, 0.14f, 0.22f), paper);
                Part(PrimitiveType.Cube, "Rings", t, new Vector3(0f, -0.22f, -0.185f), new Vector3(0.1f, 0.4f, 0.015f), metal);
            });
            Prop("megaphone", "Megaphone", t =>
            {
                Part(PrimitiveType.Cylinder, "Handle", t, Vector3.zero, new Vector3(0.025f, 0.085f, 0.025f), plasticDark);
                var body = Part(PrimitiveType.Cylinder, "Body", t, new Vector3(0f, 0.15f, 0.08f), new Vector3(0.12f, 0.14f, 0.12f), plasticLight);
                body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var bell = Part(PrimitiveType.Cylinder, "Bell", t, new Vector3(0f, 0.15f, 0.28f), new Vector3(0.24f, 0.05f, 0.24f), Mat("Prop_Orange", Hex("F39C12"), 0.4f));
                bell.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            });
            Prop("keyboard", "Keyboard", t =>
            {
                Part(PrimitiveType.Cube, "Base", t, new Vector3(0f, -0.36f, 0f), new Vector3(0.04f, 0.72f, 0.22f), plasticDark);
                Part(PrimitiveType.Cube, "Keys", t, new Vector3(0.025f, -0.36f, 0f), new Vector3(0.012f, 0.66f, 0.18f), plasticLight);
                Part(PrimitiveType.Cube, "Space Bar", t, new Vector3(0.033f, -0.36f, -0.07f), new Vector3(0.008f, 0.3f, 0.03f), plasticDark);
            });
            Prop("stapler", "Stapler", t =>
            {
                Part(PrimitiveType.Cube, "Base", t, new Vector3(0f, -0.14f, -0.02f), new Vector3(0.06f, 0.28f, 0.05f), plasticDark);
                Part(PrimitiveType.Cube, "Top", t, new Vector3(0f, -0.14f, 0.03f), new Vector3(0.065f, 0.26f, 0.05f), red);
            });
            Prop("briefcase", "Briefcase", t =>
            {
                // Carried like a suitcase: the bar runs through the fist, the posts pass the ends of the fist
                // and the case hangs off the knuckle side.
                Part(PrimitiveType.Cylinder, "Handle", t, Vector3.zero, new Vector3(0.022f, 0.09f, 0.022f), plasticDark);
                foreach (float y in new[] { -0.08f, 0.08f })
                    Part(PrimitiveType.Cube, "Post", t, new Vector3(0f, y, 0.035f), new Vector3(0.02f, 0.02f, 0.07f), plasticDark);
                Part(PrimitiveType.Cube, "Case", t, new Vector3(0f, 0f, 0.25f), new Vector3(0.14f, 0.52f, 0.38f), leather);
                Part(PrimitiveType.Cube, "Clasp L", t, new Vector3(0.072f, -0.12f, 0.12f), new Vector3(0.01f, 0.05f, 0.04f), Mat("Prop_Gold", Hex("D4AF37"), 0.8f));
                Part(PrimitiveType.Cube, "Clasp R", t, new Vector3(0.072f, 0.12f, 0.12f), new Vector3(0.01f, 0.05f, 0.04f), Mat("Prop_Gold", Hex("D4AF37"), 0.8f));
            });
            Prop("coffee_cup", "Coffee Cup", t =>
            {
                Part(PrimitiveType.Cylinder, "Cup", t, new Vector3(0f, -0.08f, 0.04f), new Vector3(0.13f, 0.09f, 0.13f), Mat("Mug", Hex("F2F2F2")));
                Part(PrimitiveType.Cylinder, "Sleeve", t, new Vector3(0f, -0.08f, 0.04f), new Vector3(0.135f, 0.04f, 0.135f), Mat("Cup_Sleeve", Hex("A0522D")));
                Part(PrimitiveType.Cylinder, "Lid", t, new Vector3(0f, 0.015f, 0.04f), new Vector3(0.14f, 0.01f, 0.14f), plasticDark);
            });
            Prop("rolled_report", "Rolled-Up Report", t =>
            {
                Part(PrimitiveType.Cylinder, "Roll", t, new Vector3(0f, -0.3f, 0f), new Vector3(0.022f, 0.3f, 0.022f), paper);
                Part(PrimitiveType.Cylinder, "Band", t, new Vector3(0f, -0.3f, 0f), new Vector3(0.026f, 0.02f, 0.026f), red);
            });

            var catalog = Asset<PropCatalog>(Root + "/Data", "PropCatalog");
            catalog.SetEntries(entries);
            HeldItemFitting.AssignProfiles(catalog);
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        // ------------------------------------------------------------------ prefabs

        static GameObject BuildPlayerPrefab()
        {
            var root = CharacterRoot("Player", Team.Player, 100f, 0.45f, 1);
            var visual = BuildOfficeWorker(root.transform);
            root.AddComponent<PlayerInputReader>();
            root.AddComponent<PlayerProgression>();
            root.AddComponent<PlayerController>();
            var caster = root.AddComponent<AbilityCaster>();
            Wire(caster, "effectMaterial", Mat("FX_AbilityRing", Color.white, unlit: true));
            Wire(root.GetComponent<AttackExecutor>(), "visual", visual);
            return SavePrefab(root, "Player");
        }

        static GameObject BuildEnemyPrefab()
        {
            var root = CharacterRoot("Coworker", Team.Enemy, 30f, 0f, -1);
            var visual = BuildOfficeWorker(root.transform);
            root.AddComponent<EnemyBrain>();
            var overhead = root.AddComponent<EnemyOverheadDisplay>();
            SetValue(overhead, "height", so => so.floatValue = 2.7f);
            Wire(overhead, "backMaterial", Mat("UI_BarBack", new Color(0.08f, 0.08f, 0.1f), unlit: true));
            Wire(overhead, "fillMaterial", Mat("UI_BarFill", Hex("E8554B"), unlit: true));
            Wire(root.GetComponent<AttackExecutor>(), "visual", visual);
            return SavePrefab(root, "Coworker");
        }

        static GameObject CharacterRoot(string name, Team team, float hp, float hitInvulnerability, int facing)
        {
            var root = new GameObject(name);
            var cc = root.AddComponent<CharacterController>();
            cc.center = new Vector3(0f, 1.05f, 0f);
            cc.height = 2.1f;
            cc.radius = 0.4f;
            cc.skinWidth = 0.05f;
            cc.stepOffset = 0.3f;
            cc.minMoveDistance = 0f;

            var health = root.AddComponent<Health>();
            SetValue(health, "team", so => so.enumValueIndex = (int)team);
            SetValue(health, "maxHealth", so => so.floatValue = hp);
            SetValue(health, "hitInvulnerability", so => so.floatValue = hitInvulnerability);

            var motor = root.AddComponent<CharacterMotor2_5D>();
            motor.minDepth = -LaneDepth;
            motor.maxDepth = LaneDepth;
            SetValue(motor, "startFacing", so => so.intValue = facing);
            root.AddComponent<AttackExecutor>();
            return root;
        }

        static GameObject SavePrefab(GameObject root, string name)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabsDir}/{name}.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        // ------------------------------------------------------------------ scene

        static void BuildFloorScene(GameObject playerPrefab, GameObject enemyPrefab, string[] classPaths,
            Dictionary<string, string> workerPaths)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var playerClasses = classPaths.Select(AssetDatabase.LoadAssetAtPath<PlayerClass>).ToArray();
            var workers = workerPaths.ToDictionary(w => w.Key, w => AssetDatabase.LoadAssetAtPath<WorkerClass>(w.Value));

            BuildLighting();
            var office = new GameObject("Office").transform;
            BuildShell(office);
            BuildCubicles(office);
            BuildForeground(office);
            BuildDecor(office);
            var elevator = BuildElevator(office, new Vector3(54.5f, 0f, 0f));

            var player = ((GameObject)PrefabUtility.InstantiatePrefab(playerPrefab)).GetComponent<PlayerController>();
            player.transform.position = new Vector3(0f, 0f, 0f);

            var enemies = new GameObject("Coworkers").transform;
            void Spawn(string id, float x, float z)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefab, enemies);
                go.name = workers[id].displayName;
                go.transform.position = new Vector3(x, 0f, z);
                Wire(go.GetComponent<EnemyBrain>(), "workerClass", workers[id]);
            }
            Spawn("Intern", 10f, -1f);
            Spawn("Intern", 12.5f, 1.2f);
            Spawn("ITSupport", 21f, 0f);
            Spawn("Intern", 26f, -1.5f);
            Spawn("Accountant", 28f, 1f);
            Spawn("HRRep", 36f, 0f);
            Spawn("ITSupport", 38f, -1.6f);
            Spawn("MiddleManager", 46f, 0f);

            var pickups = new GameObject("Pickups").transform;
            BuildPickup(pickups, PickupKind.Coffee, new Vector3(17f, 0f, 1.6f));
            BuildPickup(pickups, PickupKind.PerformanceReview, new Vector3(24f, 0f, -1.8f));
            BuildPickup(pickups, PickupKind.Coffee, new Vector3(33f, 0f, -1.4f));
            BuildPickup(pickups, PickupKind.TrainingSeminar, new Vector3(41f, 0f, 1.7f));
            BuildPickup(pickups, PickupKind.Coffee, new Vector3(50f, 0f, 0f));

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 36f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 200f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Hex("2A3350");
            ConfigureAtmosphere();
            camGo.AddComponent<AudioListener>();
            var follow = camGo.AddComponent<SideScrollCamera>();
            Wire(follow, "target", player.transform);
            SetValue(follow, "minX", so => so.floatValue = LevelStartX + 4f);
            SetValue(follow, "maxX", so => so.floatValue = LevelEndX - 5f);
            camGo.transform.position = new Vector3(0f, 3.4f, -12f);
            camGo.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 1.3f, 0f) - new Vector3(0f, 3.4f, -12f));

            var hud = new GameObject("HUD").AddComponent<GameHud>();
            var flow = new GameObject("Game Flow").AddComponent<GameFlow>();
            Wire(flow, "player", player);
            Wire(flow, "hud", hud);
            Wire(flow, "elevator", elevator);
            SetArray(flow, "playerClasses", playerClasses);

            MarkStatic(office, elevator.transform);
            EditorSceneManager.SaveScene(scene, FloorScenePath);
            AddSceneToBuildSettingsFirst(FloorScenePath);
        }

        static void BuildPickup(Transform parent, PickupKind kind, Vector3 position)
        {
            var root = Group(kind.ToString(), parent);
            root.position = position;
            var trigger = root.gameObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 0.8f, 0f);
            trigger.radius = 0.7f;

            var visual = Group("Visual", root);
            visual.localPosition = new Vector3(0f, 0.8f, 0f);
            switch (kind)
            {
                case PickupKind.Coffee:
                    Part(PrimitiveType.Cylinder, "Cup", visual, Vector3.zero, new Vector3(0.28f, 0.2f, 0.28f), Mat("Mug", Hex("F2F2F2")));
                    Part(PrimitiveType.Cylinder, "Coffee", visual, new Vector3(0f, 0.19f, 0f), new Vector3(0.24f, 0.02f, 0.24f), Mat("Coffee", Hex("4E2A1E"), 0.8f));
                    Part(PrimitiveType.Cube, "Sleeve", visual, Vector3.zero, new Vector3(0.3f, 0.12f, 0.3f), Mat("Cup_Sleeve", Hex("A0522D")));
                    break;
                case PickupKind.PerformanceReview:
                    Part(PrimitiveType.Cube, "Folder", visual, Vector3.zero, new Vector3(0.5f, 0.62f, 0.04f), Mat("Review_Folder", Hex("F7D154"), emission: Hex("5A4A10")));
                    Part(PrimitiveType.Cube, "Star", visual, new Vector3(0f, 0.08f, -0.03f), new Vector3(0.18f, 0.18f, 0.02f), Mat("Review_Star", Hex("E74C3C")));
                    break;
                case PickupKind.TrainingSeminar:
                    Part(PrimitiveType.Cube, "Book", visual, Vector3.zero, new Vector3(0.5f, 0.66f, 0.14f), Mat("Seminar_Book", Hex("7B4FB0"), emission: Hex("2E1452")));
                    Part(PrimitiveType.Cube, "Pages", visual, new Vector3(0.02f, 0f, 0f), new Vector3(0.46f, 0.62f, 0.15f), Mat("Paper", Hex("F5F1E6")));
                    break;
            }

            var pickup = root.gameObject.AddComponent<Pickup>();
            SetValue(pickup, "kind", so => so.enumValueIndex = (int)kind);
            Wire(pickup, "visual", visual);
        }

        // ------------------------------------------------------------------ build settings

        static void AddSceneToBuildSettingsFirst(string path)
        {
            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(path, true) };
            foreach (var s in EditorBuildSettings.scenes)
                if (s.path != path) scenes.Add(new EditorBuildSettingsScene(s.path, false));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ------------------------------------------------------------------ helpers

        static T Asset<T>(string dir, string name) where T : ScriptableObject
        {
            string path = $"{dir}/{name}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static Material Mat(string name, Color color, float smoothness = 0.25f, Color? emission = null, bool unlit = false)
        {
            if (Materials.TryGetValue(name, out var cached)) return cached;

            string path = $"{MaterialsDir}/{name}.mat";
            var shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }

            mat.SetColor("_BaseColor", color);
            if (!unlit) mat.SetFloat("_Smoothness", smoothness);
            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission.Value);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EditorUtility.SetDirty(mat);
            Materials[name] = mat;
            return mat;
        }

        static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 localScale,
            Material material, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static Transform Group(string name, Transform parent)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        static void Blocker(Transform parent, string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.AddComponent<BoxCollider>().size = new Vector3(1f, 8f, 14f);
        }

        static void WorldText(Transform parent, string text, Vector3 position, float size, Color color)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(Root + "/Resources/Fonts/BarlowCondensed-Bold.ttf")
                       ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject("Text: " + text.Replace('\n', ' '));
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var mesh = go.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.font = font;
            mesh.fontSize = 64;
            mesh.characterSize = size;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = color;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static Color Hex(string hex) => ColorUtility.TryParseHtmlString("#" + hex, out var c) ? c : Color.magenta;

        static SerializedProperty Find(SerializedObject so, string property)
        {
            var prop = so.FindProperty(property);
            if (prop == null) throw new System.ArgumentException($"{so.targetObject.GetType().Name} has no serialized field '{property}'");
            return prop;
        }

        static void Wire(Object target, string property, Object value)
        {
            var so = new SerializedObject(target);
            Find(so, property).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetValue(Object target, string property, System.Action<SerializedProperty> set)
        {
            var so = new SerializedObject(target);
            set(Find(so, property));
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetArray(Object target, string property, params Object[] values)
        {
            var so = new SerializedObject(target);
            var prop = Find(so, property);
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
