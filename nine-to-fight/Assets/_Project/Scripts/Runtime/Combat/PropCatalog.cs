using System;
using System.Collections.Generic;
using UnityEngine;

namespace NineToFight.Combat
{
    /// <summary>Maps prop ids used by FightingMove.prop to held-item prefabs.</summary>
    [CreateAssetMenu(menuName = "Nine to Fight/Prop Catalog", fileName = "PropCatalog")]
    public class PropCatalog : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string id;
            public string displayName;
            [Tooltip("Grip at the origin; the item extends along local -Y (away from the shoulder).")]
            public GameObject prefab;
        }

        [SerializeField] List<Entry> entries = new List<Entry>();

        public IReadOnlyList<Entry> Entries => entries;

        public void SetEntries(IEnumerable<Entry> values)
        {
            entries.Clear();
            entries.AddRange(values);
        }

        public GameObject Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var e in entries)
                if (e.id == id) return e.prefab;
            return null;
        }

        public bool Contains(string id) => Get(id) != null;
    }
}
