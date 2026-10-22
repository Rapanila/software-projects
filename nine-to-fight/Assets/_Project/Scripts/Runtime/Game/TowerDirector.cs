using System.Collections.Generic;
using NineToFight.Enemies;
using NineToFight.Items;
using UnityEngine;

namespace NineToFight.Game
{
    /// <summary>
    /// Re-stages the Floor 1 office as each higher floor: spawns that floor's coworkers and
    /// supplies, and re-lights the room. Uses the scene's own coworker and pickups as templates,
    /// so new floors need no extra scenes or prefabs.
    /// </summary>
    public class TowerDirector : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        Transform templates, spawned;
        EnemyBrain enemyTemplate;
        readonly Dictionary<string, WorkerClass> workerClasses = new Dictionary<string, WorkerClass>();
        readonly Dictionary<PickupKind, Pickup> pickupTemplates = new Dictionary<PickupKind, Pickup>();
        readonly List<Object> runtimeAssets = new List<Object>();

        Light keyLight;
        Color keyLightColor;
        readonly List<(Renderer renderer, Color color)> carpets = new List<(Renderer, Color)>();
        MaterialPropertyBlock block;
        TextMesh floorSign, cornerOfficeSign;
        readonly List<Light> roomLights = new List<Light>();
        float keyLightIntensity;
        Color ambientSky, ambientEquator, ambientGround;
        Light flashlight;
        readonly List<SpawnPoint> pendingWaves = new List<SpawnPoint>();
        int wave;

        public bool HasPendingWave => pendingWaves.Count > 0;
        /// <summary>Overrides the next floor's random event once (tests, debugging).</summary>
        public static FloorEvent? ForceEvent;
        public int Wave => wave;

        public FloorPlan Current { get; private set; }

        /// <summary>Snapshots templates from the hand-built Floor 1 before anything is defeated or picked up.</summary>
        public void Capture()
        {
            templates = new GameObject("Floor Templates").transform;
            templates.gameObject.SetActive(false); // children stay dormant: no Awake, no AI
            templates.SetParent(transform, false);
            spawned = new GameObject("Spawned").transform;

            foreach (var enemy in FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None))
            {
                if (enemy.WorkerClass != null && !workerClasses.ContainsKey(enemy.WorkerClass.name))
                    workerClasses[enemy.WorkerClass.name] = enemy.WorkerClass;
                if (enemyTemplate == null) enemyTemplate = Instantiate(enemy, templates);
            }
            // If the source already built its runtime health bar, don't clone it twice.
            if (enemyTemplate != null)
            {
                var overhead = enemyTemplate.transform.Find("Overhead");
                if (overhead != null) DestroyImmediate(overhead.gameObject);
            }
            foreach (var pickup in FindObjectsByType<Pickup>(FindObjectsSortMode.None))
                if (!pickupTemplates.ContainsKey(pickup.Kind))
                    pickupTemplates[pickup.Kind] = Instantiate(pickup, templates);

            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional && (keyLight == null || light.name == "Key Light")) { keyLight = light; keyLightColor = light.color; }
                if (light.type == LightType.Point) roomLights.Add(light);
            }
            if (keyLight != null) keyLightIntensity = keyLight.intensity;
            ambientSky = RenderSettings.ambientSkyColor;
            ambientEquator = RenderSettings.ambientEquatorColor;
            ambientGround = RenderSettings.ambientGroundColor;

            block = new MaterialPropertyBlock();
            foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                var m = r.sharedMaterial;
                if (m != null && m.name.StartsWith("Carpet") && m.HasProperty(BaseColorId))
                    carpets.Add((r, m.GetColor(BaseColorId)));
            }

            // Wall signage gets repainted for each floor.
            foreach (var text in FindObjectsByType<TextMesh>(FindObjectsSortMode.None))
            {
                if (text.text.StartsWith("FLOOR 1")) floorSign = text;
                else if (text.text.StartsWith("CORNER OFFICE")) cornerOfficeSign = text;
            }

            EnemyBrain.AnyDefeated += OnEnemyDefeated;
            Current = TowerPlan.Build(1);
            Repaint(Current);
        }

        void OnDestroy()
        {
            EnemyBrain.AnyDefeated -= OnEnemyDefeated;
            foreach (var asset in runtimeAssets) if (asset != null) Destroy(asset);
        }

        /// <summary>Floor 1's hand-placed managers (tier 3+) fight as mini-bosses.</summary>
        public void PromoteSceneBosses()
        {
            foreach (var enemy in EnemyBrain.Active.ToArray())
                if (enemy.WorkerClass != null && enemy.WorkerClass.tier >= 3 && enemy.Boss == BossRank.None)
                    enemy.Configure(enemy.WorkerClass, BossRank.MiniBoss);
        }

        /// <summary>Clears the room and stages <paramref name="floor"/>. Returns the plan used.</summary>
        public FloorPlan Stage(int floor, ICollection<FloorEvent> usedEvents = null)
        {
            int seed = Random.Range(0, 100000);
            Current = TowerPlan.Build(floor, seed);
            var rng = new System.Random(seed);
            var floorEvent = ForceEvent ?? TowerPlan.RollEvent(floor, rng, usedEvents);
            ForceEvent = null;
            TowerPlan.ApplyEvent(Current, floorEvent, rng);
            usedEvents?.Add(floorEvent);
            pendingWaves.Clear();
            wave = 0;

            // Leftover supplies from the floor below don't ride the elevator.
            foreach (var pickup in FindObjectsByType<Pickup>(FindObjectsSortMode.None))
                if (pickup.transform.parent != templates) Destroy(pickup.gameObject);

            foreach (var spawn in Current.spawns)
            {
                if (spawn.wave == 0) SpawnCoworker(spawn);
                else pendingWaves.Add(spawn);
            }
            foreach (var p in Current.coffee) SpawnPickup(PickupKind.Coffee, p);
            foreach (var p in Current.reviews) SpawnPickup(PickupKind.PerformanceReview, p);
            Relight(Current);
            Repaint(Current);
            return Current;
        }

        void Repaint(FloorPlan plan)
        {
            if (floorSign != null) floorSign.text = $"FLOOR {plan.number}  ·  {plan.name.ToUpperInvariant()}";
            if (cornerOfficeSign != null)
            {
                int left = TowerPlan.FloorCount - plan.number;
                cornerOfficeSign.text = left == 0 ? "CORNER OFFICE: THIS WAY" : $"CORNER OFFICE: {left} FLOOR{(left == 1 ? "" : "S")} UP";
            }
        }

        /// <summary>All-Hands: the next wave files in from both sides of the player.</summary>
        public int SpawnNextWave(Vector3 around)
        {
            if (pendingWaves.Count == 0) return 0;
            wave++;
            int next = pendingWaves[0].wave;
            var group = pendingWaves.FindAll(s => s.wave == next);
            pendingWaves.RemoveAll(s => s.wave == next);
            for (int i = 0; i < group.Count; i++)
            {
                var s = group[i];
                float side = i % 2 == 0 ? 1f : -1f;
                float x = Mathf.Clamp(around.x + side * Random.Range(7f, 9.5f), -2f, 50f);
                s.position = new Vector3(x, 0f, Random.Range(-2.2f, 2.2f));
                var enemy = SpawnCoworker(s);
                if (enemy != null) enemy.Stun(0.6f, Color.white); // a beat to see them arrive
            }
            return group.Count;
        }

        void Relight(FloorPlan plan)
        {
            bool dark = plan.floorEvent == FloorEvent.PowerOutage;
            if (keyLight != null)
            {
                keyLight.color = keyLightColor * plan.lightTint;
                keyLight.intensity = dark ? keyLightIntensity * 0.12f : keyLightIntensity;
            }
            foreach (var l in roomLights) if (l != null) l.enabled = !dark;
            float ambient = dark ? 0.12f : 1f;
            RenderSettings.ambientSkyColor = ambientSky * ambient;
            RenderSettings.ambientEquatorColor = ambientEquator * ambient;
            RenderSettings.ambientGroundColor = ambientGround * ambient;
            SetFlashlight(dark);
            foreach (var (r, color) in carpets)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor(BaseColorId, color * plan.carpetTint);
                r.SetPropertyBlock(block);
            }
        }

        /// <summary>Power outage: the player carries a torch (phone flashlight, really).</summary>
        void SetFlashlight(bool on)
        {
            var player = Player.PlayerController.Instance;
            if (on && flashlight == null && player != null)
            {
                flashlight = new GameObject("Phone Flashlight").AddComponent<Light>();
                flashlight.type = LightType.Point;
                flashlight.color = new Color(1f, 0.95f, 0.85f);
                flashlight.intensity = 4f;
                flashlight.range = 7f;
                flashlight.shadows = LightShadows.None;
                flashlight.transform.SetParent(player.transform, false);
                flashlight.transform.localPosition = new Vector3(0f, 2.2f, -1.2f);
            }
            if (flashlight != null) flashlight.enabled = on;
        }

        public EnemyBrain SpawnCoworker(SpawnPoint spawn)
        {
            if (enemyTemplate == null) return null;
            var worker = BuildWorker(spawn.who, Current);
            if (worker == null) return null;
            if (spawn.elite && spawn.who.boss == BossRank.None)
            {
                // "Senior" staff: tougher, bigger, worth more.
                worker.displayName = "Senior " + worker.displayName;
                worker.maxHealth *= 1.8f;
                worker.scale *= 1.12f;
                worker.poise = Mathf.Max(worker.poise, 0.35f);
                worker.xpReward *= 2;
                worker.attack.damage *= 1.15f;
            }

            var enemy = Instantiate(enemyTemplate, spawn.position, Quaternion.identity, spawned);
            enemy.name = worker.displayName;
            enemy.Configure(worker, spawn.who.boss);
            enemy.IsElite = spawn.elite && spawn.who.boss == BossRank.None;
            enemy.gameObject.SetActive(true);
            return enemy;
        }

        /// <summary>A per-floor copy of a worker class: re-titled, re-coloured and scaled for the floor.</summary>
        WorkerClass BuildWorker(Archetype who, FloorPlan plan)
        {
            if (!workerClasses.TryGetValue(who.baseId, out var source)) return null;
            var w = Instantiate(source);
            runtimeAssets.Add(w);
            w.name = $"{source.name} ({who.title})";
            w.displayName = who.title;
            w.shirtColor = TowerPlan.Hex(who.shirt);
            w.tieColor = TowerPlan.Hex(who.tie);
            w.maxHealth *= plan.healthScale;
            w.xpReward = Mathf.RoundToInt(w.xpReward * plan.xpScale);
            w.attack = w.attack.Clone();
            w.attack.damage *= plan.damageScale;
            w.aggroRange = Mathf.Max(w.aggroRange, 10f);
            w.attackCooldown *= Mathf.Lerp(1f, 0.75f, (plan.number - 1) / (float)(TowerPlan.FloorCount - 1));

            switch (who.boss)
            {
                case BossRank.MiniBoss:
                    w.maxHealth *= 2.2f;
                    w.scale *= 1.12f;
                    w.poise = Mathf.Max(w.poise, 0.7f);
                    w.xpReward *= 3;
                    break;
                case BossRank.FinalBoss:
                    w.maxHealth *= 3.5f;
                    w.scale = 1.6f;
                    w.poise = 0.85f;
                    w.moveSpeed *= 1.25f;
                    w.attack.damage *= 1.3f;
                    w.attackCooldown *= 0.75f;
                    w.xpReward *= 5;
                    break;
            }
            return w;
        }

        public void SpawnPickup(PickupKind kind, Vector3 position)
        {
            if (!pickupTemplates.TryGetValue(kind, out var template)) return;
            var pickup = Instantiate(template, position, Quaternion.identity, spawned);
            pickup.gameObject.SetActive(true);
        }

        void OnEnemyDefeated(EnemyBrain enemy)
        {
            // Beaten coworkers sometimes drop their coffee; bosses always do.
            bool boss = enemy.Boss != BossRank.None;
            float chance = Current != null ? Current.coffeeDropChance : 0.2f;
            if (enemy.IsElite) SpawnPickup(PickupKind.PerformanceReview, new Vector3(enemy.transform.position.x + 0.6f, 0f, Mathf.Clamp(enemy.transform.position.z, -2.2f, 2.2f)));
            if (boss || Random.value < chance)
            {
                Vector3 p = enemy.transform.position;
                SpawnPickup(PickupKind.Coffee, new Vector3(p.x, 0f, Mathf.Clamp(p.z, -2.2f, 2.2f)));
            }
        }
    }
}
