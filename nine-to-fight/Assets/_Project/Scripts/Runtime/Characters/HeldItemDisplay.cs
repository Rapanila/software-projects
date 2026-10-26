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
        RigFollower follower;
        CharacterVisual visual;
        int posedBody = -1;
        string idleProp;
        bool initialized;

        public string CurrentProp { get; private set; }
        public PropCatalog Catalog => catalog;
        public ItemHoldPose CurrentGrip => catalog != null ? catalog.GetGrip(CurrentProp, follower != null ? follower.Active : 0) : null;

        void Awake() => EnsureInit();

        void EnsureInit()
        {
            if (initialized) return;
            initialized = true;
            if (handSocket == null) handSocket = transform;
            follower = GetComponent<RigFollower>();
            visual = GetComponent<CharacterVisual>();
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
            if (string.IsNullOrEmpty(id))
            {
                if (visual != null) visual.SetTrailTip(null);
                return;
            }

            if (!spawned.TryGetValue(id, out var item))
            {
                var prefab = catalog != null ? catalog.Get(id) : null;
                item = prefab != null ? Instantiate(prefab, handSocket, false) : null;
                if (item != null)
                    foreach (var c in item.GetComponentsInChildren<Collider>()) Destroy(c);
                spawned[id] = item;
            }
            if (item != null)
            {
                ApplyGrip(item.transform);
                item.SetActive(true);
            }
            else if (visual != null) visual.SetTrailTip(null);
        }

        void LateUpdate()
        {
            int body = follower != null ? follower.Active : 0;
            if (body != posedBody && !string.IsNullOrEmpty(CurrentProp) &&
                spawned.TryGetValue(CurrentProp, out var item) && item != null)
                ApplyGrip(item.transform);
        }

        void ApplyGrip(Transform item)
        {
            var pose = CurrentGrip;
            item.localPosition = pose != null ? pose.position : Vector3.zero;
            item.localRotation = pose != null ? Quaternion.Euler(pose.eulerAngles) : Quaternion.identity;
            posedBody = follower != null ? follower.Active : 0;
            if (visual != null) visual.SetTrailTip(ItemTip(item, handSocket));
        }

        /// <summary>
        /// The point of the item farthest from the hand, in hand-socket space: where a swing's trail belongs
        /// (the head of a golf club, the end of a rolled report), measured from the item's own meshes.
        /// </summary>
        public static Vector3? ItemTip(Transform item, Transform socket)
        {
            Vector3? best = null;
            float bestDistance = -1f;
            foreach (var filter in item.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                if (mesh == null) continue;
                var b = mesh.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    var local = b.center + Vector3.Scale(b.extents, new Vector3(
                        (corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                    var p = socket.InverseTransformPoint(filter.transform.TransformPoint(local));
                    float d = p.sqrMagnitude;
                    if (d > bestDistance) { bestDistance = d; best = p; }
                }
            }
            return best;
        }
    }
}
