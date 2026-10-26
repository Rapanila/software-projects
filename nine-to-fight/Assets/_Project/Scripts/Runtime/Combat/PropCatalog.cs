using System;
using System.Collections.Generic;
using NineToFight.Characters;
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
            [Tooltip("Unposed item geometry. Each body has its own fitted holding transform below.")]
            public GameObject prefab;
            public ItemHoldPose manGrip;
            public ItemHoldPose womanGrip;
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

        public ItemHoldPose GetGrip(string id, int body)
        {
            foreach (var entry in entries)
                if (entry.id == id) return body == 1 ? entry.womanGrip : entry.manGrip;
            return null;
        }
    }
}
