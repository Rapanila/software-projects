using NineToFight.Game;
using NineToFight.Player;
using UnityEngine;

namespace NineToFight.World
{
    /// <summary>Exit of each floor: stays shut until every coworker on the floor is down.</summary>
    [RequireComponent(typeof(BoxCollider))]
    public class ElevatorDoor : MonoBehaviour
    {
        [SerializeField] Transform leftDoor;
        [SerializeField] Transform rightDoor;
        [SerializeField] Renderer indicator;
        [SerializeField] Material lockedMaterial;
        [SerializeField] Material openMaterial;
        [SerializeField] float openDistance = 0.85f;
        [SerializeField] float openSpeed = 1.5f;

        Vector3 leftClosed, rightClosed;
        float openAmount;

        public bool IsOpen { get; private set; }

        void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
            if (!TryGetComponent<Rigidbody>(out var body)) body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            if (leftDoor != null) leftClosed = leftDoor.localPosition;
            if (rightDoor != null) rightClosed = rightDoor.localPosition;
            if (indicator != null && lockedMaterial != null) indicator.sharedMaterial = lockedMaterial;
        }

        public void Open()
        {
            if (!IsOpen) Audio.GameAudio.Play(Audio.Sfx.ElevatorDing);
            IsOpen = true;
            if (indicator != null && openMaterial != null) indicator.sharedMaterial = openMaterial;
        }

        /// <summary>Shuts the doors again for the next floor.</summary>
        public void Close()
        {
            IsOpen = false;
            if (indicator != null && lockedMaterial != null) indicator.sharedMaterial = lockedMaterial;
        }

        void Update()
        {
            openAmount = Mathf.MoveTowards(openAmount, IsOpen ? 1f : 0f, openSpeed * Time.deltaTime);
            float offset = Mathf.SmoothStep(0f, openDistance, openAmount);
            if (leftDoor != null) leftDoor.localPosition = leftClosed + Vector3.left * offset;
            if (rightDoor != null) rightDoor.localPosition = rightClosed + Vector3.right * offset;
        }

        void OnTriggerEnter(Collider other)
        {
            if (!IsOpen || other.GetComponentInParent<PlayerController>() == null) return;
            GameFlow.Instance?.OnElevatorReached();
        }
    }
}
