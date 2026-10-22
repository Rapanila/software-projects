using System.Collections.Generic;
using NineToFight.Combat;
using UnityEngine;

namespace NineToFight.Characters
{
    /// <summary>
    /// Puts the right office item in the character's hand: the idle item normally,
    /// and the current move's item while attacking.
    /// </summary>
    public class HeldItemDisplay : MonoBehaviour
    {
        [SerializeField] PropCatalog catalog;
        [SerializeField] Transform handSocket;

        readonly Dictionary<string, GameObject> spawned = new Dictionary<string, GameObject>();
        AttackExecutor attack;
        string idleProp;
        bool initialized;

        public string CurrentProp { get; private set; }
        public PropCatalog Catalog => catalog;

        void Awake() => EnsureInit();

        void EnsureInit()
        {
            if (initialized) return;
            initialized = true;
            if (handSocket == null) handSocket = transform;
            attack = GetComponentInParent<AttackExecutor>();
            if (attack != null)
            {
                attack.AttackStarted += move => Show(move.prop);
                attack.AttackEnded += () => Show(idleProp);
            }
        }

        /// <summary>The item carried between attacks (usually the light attack's).</summary>
        public void SetIdleProp(string id)
        {
            EnsureInit();
            idleProp = id;
            if (attack == null || !attack.IsAttacking) Show(id);
        }

        public void Show(string id)
        {
            EnsureInit();
            if (id == CurrentProp) return;

            if (!string.IsNullOrEmpty(CurrentProp) && spawned.TryGetValue(CurrentProp, out var previous) && previous != null)
                previous.SetActive(false);
            CurrentProp = id;
            if (string.IsNullOrEmpty(id)) return;

            if (!spawned.TryGetValue(id, out var item))
            {
                var prefab = catalog != null ? catalog.Get(id) : null;
                item = prefab != null ? Instantiate(prefab, handSocket, false) : null;
                if (item != null)
                    foreach (var c in item.GetComponentsInChildren<Collider>()) Destroy(c);
                spawned[id] = item;
            }
            if (item != null) item.SetActive(true);
        }
    }
}
