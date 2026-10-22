using UnityEngine;

namespace NineToFight.Characters
{
    /// <summary>
    /// Belt-scroller movement in a 3D world: X is the main run axis, Z is a shallow
    /// depth lane, Y is jumping. Shared by the player and every office worker.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class CharacterMotor2_5D : MonoBehaviour
    {
        [Header("Movement")]
        public float moveSpeed = 6f;
        [Tooltip("Speed along the depth (Z) lane; usually slower than horizontal speed.")]
        public float depthSpeed = 4f;
        public float acceleration = 60f;
        public float deceleration = 70f;
        [Range(0f, 1f)] public float airControl = 0.6f;

        [Header("Jump")]
        public float jumpHeight = 1.6f;
        public float gravity = -32f;
        public float fallGravityMultiplier = 1.6f;
        public float coyoteTime = 0.1f;
        public float jumpBufferTime = 0.12f;

        [Header("Depth Lane")]
        public float minDepth = -2.5f;
        public float maxDepth = 2.5f;

        [Header("Knockback")]
        public float knockbackDrag = 30f;

        [Header("Facing")]
        [SerializeField] Transform visual;
        [SerializeField] int startFacing = 1;
        public float turnSpeed = 1440f;

        CharacterController controller;
        Vector3 planarVelocity;
        Vector3 externalVelocity;
        float verticalVelocity;
        Vector2 moveInput;
        float lastGroundedTime = float.NegativeInfinity;
        float lastJumpPressTime = float.NegativeInfinity;
        Vector3 dashVelocity;
        float dashUntil;
        bool facingLocked;

        /// <summary>Last left/right facing (+1 / -1); used for camera look-ahead.</summary>
        public int Facing { get; private set; }
        /// <summary>Full facing on the ground plane: +Z is the back wall, -Z is the camera.</summary>
        public Vector3 FacingDirection { get; private set; } = Vector3.right;
        public bool IsGrounded { get; private set; }
        public bool IsDashing => Time.time < dashUntil;
        public bool CanMove { get; set; } = true;
        public float SpeedMultiplier { get; set; } = 1f;
        /// <summary>Temporary speed boost from abilities; stacks with SpeedMultiplier.</summary>
        public float BuffSpeedMultiplier { get; set; } = 1f;
        /// <summary>Permanent run bonus (perks).</summary>
        public float PerkSpeedMultiplier { get; set; } = 1f;
        public Vector3 Velocity => planarVelocity + externalVelocity + Vector3.up * verticalVelocity;
        public Transform Visual => visual;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (visual == null) visual = transform.Find("Visual");
            Facing = startFacing >= 0 ? 1 : -1;
            FacingDirection = Vector3.right * Facing;
            if (visual != null) visual.localRotation = Quaternion.Euler(0f, YawFromDirection(FacingDirection), 0f);
        }

        /// <summary>Yaw in degrees for a ground-plane direction: 0 = back wall, 90 = right, 180 = camera, -90 = left.</summary>
        public static float YawFromDirection(Vector3 direction) =>
            Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

        /// <summary>Converts stick/WASD input (x = right, y = into the screen) to a facing direction.</summary>
        public static bool TryInputDirection(Vector2 input, out Vector3 direction)
        {
            direction = new Vector3(input.x, 0f, input.y);
            if (direction.sqrMagnitude < 0.04f) return false;
            direction.Normalize();
            return true;
        }

        void SetFacing(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;
            FacingDirection = direction.normalized;
            if (Mathf.Abs(FacingDirection.x) > 0.1f) Facing = FacingDirection.x > 0f ? 1 : -1;
        }

        public void SetMoveInput(Vector2 input) => moveInput = Vector2.ClampMagnitude(input, 1f);

        public void RequestJump() => lastJumpPressTime = Time.time;

        public void AddImpulse(Vector3 impulse)
        {
            externalVelocity += new Vector3(impulse.x, 0f, impulse.z);
            if (impulse.y > 0f) verticalVelocity = Mathf.Max(verticalVelocity, impulse.y);
        }

        public void Dash(Vector3 direction, float speed, float duration)
        {
            direction.y = 0f;
            dashVelocity = direction.normalized * speed;
            dashUntil = Time.time + duration;
            planarVelocity = Vector3.zero;
        }

        /// <summary>Turns to face a point on the ground plane (e.g. an enemy looking at the player).</summary>
        public void FaceTowards(Vector3 worldPosition)
        {
            if (facingLocked) return;
            Vector3 delta = worldPosition - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude > 0.0025f) SetFacing(delta);
        }

        /// <summary>Keeps the current facing during attacks so input can't spin the character mid-swing.</summary>
        public void LockFacing(bool locked) => facingLocked = locked;

        public void Stop()
        {
            moveInput = Vector2.zero;
            planarVelocity = Vector3.zero;
            externalVelocity = Vector3.zero;
            dashUntil = 0f;
        }

        void Update()
        {
            if (!controller.enabled) return;

            float dt = Time.deltaTime;
            IsGrounded = controller.isGrounded;
            if (IsGrounded)
            {
                lastGroundedTime = Time.time;
                if (verticalVelocity < 0f) verticalVelocity = -2f;
            }

            Vector2 input = CanMove ? moveInput : Vector2.zero;
            Vector3 target = new Vector3(input.x * moveSpeed, 0f, input.y * depthSpeed) * (SpeedMultiplier * BuffSpeedMultiplier * PerkSpeedMultiplier);
            float rate = target.sqrMagnitude > 0.01f ? acceleration : deceleration;
            if (!IsGrounded) rate *= airControl;
            planarVelocity = Vector3.MoveTowards(planarVelocity, target, rate * dt);

            bool jumpBuffered = Time.time - lastJumpPressTime <= jumpBufferTime;
            bool canCoyoteJump = Time.time - lastGroundedTime <= coyoteTime;
            if (CanMove && jumpBuffered && canCoyoteJump)
            {
                verticalVelocity = Mathf.Sqrt(2f * jumpHeight * -gravity);
                lastJumpPressTime = float.NegativeInfinity;
                lastGroundedTime = float.NegativeInfinity;
            }

            float g = verticalVelocity < 0f ? gravity * fallGravityMultiplier : gravity;
            verticalVelocity += g * dt;

            Vector3 horizontal = IsDashing ? dashVelocity : planarVelocity + externalVelocity;
            Vector3 delta = new Vector3(horizontal.x, verticalVelocity, horizontal.z) * dt;

            // Keep everyone inside the depth lane without fighting the CharacterController.
            float z = transform.position.z;
            delta.z = Mathf.Clamp(z + delta.z, minDepth, maxDepth) - z;

            controller.Move(delta);
            externalVelocity = Vector3.MoveTowards(externalVelocity, Vector3.zero, knockbackDrag * dt);

            // Turn toward the way we're moving: W faces the back wall, S faces the camera, diagonals in between.
            if (!facingLocked && CanMove && TryInputDirection(input, out var inputDirection))
                SetFacing(inputDirection);

            if (visual != null)
            {
                float yaw = Mathf.MoveTowardsAngle(visual.localEulerAngles.y, YawFromDirection(FacingDirection), turnSpeed * dt);
                visual.localRotation = Quaternion.Euler(0f, yaw, 0f);
            }
        }
    }
}
