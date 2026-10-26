using NineToFight.Game;
using NineToFight.Player;
using UnityEngine;

namespace NineToFight.Items
{
    public enum PickupKind
    {
        [Tooltip("Restores health.")] Coffee,
        [Tooltip("Grants XP towards the next promotion.")] PerformanceReview,
        [Tooltip("Teaches a move early.")] TrainingSeminar,
    }

    /// <summary>Office supplies lying around the floor, picked up by walking into them.</summary>
    [RequireComponent(typeof(Collider))]
    public class Pickup : MonoBehaviour
    {
        [SerializeField] PickupKind kind;
        [SerializeField] float healAmount = 35f;
        [SerializeField] int xpAmount = 40;
        [SerializeField] string moveId = "keyboard_smash";
        [SerializeField] Transform visual;
        [SerializeField] float bobHeight = 0.15f;
        [SerializeField] float bobSpeed = 2.5f;
        [SerializeField] float spinSpeed = 90f;

        Vector3 visualStart;

        public PickupKind Kind => kind;
        public string MoveId => moveId;

        /// <summary>Sets the move a training seminar teaches.</summary>
        public void Teach(string id) => moveId = id;

        /// <summary>Coffee is left on the desk while the player is already at full health.</summary>
        public static bool WouldWaste(PickupKind kind, float current, float max) => kind == PickupKind.Coffee && current >= max - 0.01f;

        void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
            if (!TryGetComponent<Rigidbody>(out var body)) body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            if (visual == null && transform.childCount > 0) visual = transform.GetChild(0);
            if (visual != null) visualStart = visual.localPosition;
        }

        void Update()
        {
            if (visual == null) return;
            visual.localPosition = visualStart + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobHeight);
            visual.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
        }

        void OnTriggerEnter(Collider other)
        {
            var player = other.GetComponentInParent<PlayerController>();
            if (player == null || player.Health.IsDead || !GameFlow.IsPlaying) return;
            if (WouldWaste(kind, player.Health.Current, player.Health.Max)) return; // save it for when it's needed
            string message = Apply(player);
            if (message != null) GameFlow.Instance?.Toast(message);
            Audio.GameAudio.Play(Audio.Sfx.Pickup);
            Fx.HitEffects.Burst(transform.position + Vector3.up * 0.8f, Vector3.up, kind == PickupKind.Coffee ? new Color(0.95f, 0.75f, 0.4f) : new Color(1f, 0.85f, 0.3f), 10, 4f, 0.07f);
            Destroy(gameObject);
        }

        string Apply(PlayerController player)
        {
            switch (kind)
            {
                case PickupKind.Coffee:
                    float heal = healAmount * player.Perks.CoffeeMultiplier;
                    player.Health.Heal(heal);
                    return $"Coffee break!  +{heal:0} HP";
                case PickupKind.PerformanceReview:
                    player.Progression.AddXp(xpAmount);
                    return $"Glowing performance review!  +{xpAmount} XP";
                case PickupKind.TrainingSeminar:
                    if (player.Progression.LearnMove(moveId)) return null; // the "new move" banner covers it
                    player.Progression.AddXp(xpAmount);
                    return $"You already know this one. Networking bonus  +{xpAmount} XP";
                default:
                    return null;
            }
        }
    }
}
