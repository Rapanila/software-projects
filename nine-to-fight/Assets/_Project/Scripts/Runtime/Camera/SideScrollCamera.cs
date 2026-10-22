using NineToFight.Characters;
using UnityEngine;

namespace NineToFight.CameraRig
{
    /// <summary>What the camera is doing: following play, drifting behind menus, or framing a character.</summary>
    public enum CameraShot
    {
        /// <summary>Normal play: side-on follow with look-ahead.</summary>
        Follow,
        /// <summary>Title screen: slow drift across the office, a little higher and wider.</summary>
        Attract,
        /// <summary>Department select: close on the player, placed left of centre for the panel on the right.</summary>
        Showcase,
    }

    /// <summary>
    /// 2.5D side-on follow camera: perspective, fixed viewing angle, eases ahead of the
    /// player's facing and only partly follows depth so the lane stays readable. Also does the
    /// presentation shots (title drift, department showcase) and timed focus pulls for boss intros.
    /// </summary>
    public class SideScrollCamera : MonoBehaviour
    {
        public static SideScrollCamera Main { get; private set; }

        [SerializeField] Transform target;
        [SerializeField] Vector3 offset = new Vector3(0f, 3.4f, -12f);
        [SerializeField] Vector3 lookOffset = new Vector3(0f, 1.3f, 0f);
        [SerializeField] float smoothTime = 0.18f;
        [SerializeField] float lookAhead = 1.6f;
        [SerializeField] float lookAheadSmoothTime = 0.45f;
        [Range(0f, 1f), Tooltip("How much the camera follows the player along the depth lane.")]
        [SerializeField] float depthFollow = 0.35f;
        [Range(0f, 1f)]
        [SerializeField] float jumpFollow = 0.4f;
        [SerializeField] float minX = -1f;
        [SerializeField] float maxX = 52f;

        CharacterMotor2_5D targetMotor;
        Vector3 velocity;
        float ahead, aheadVelocity;
        float shakeAmplitude, shakeDuration, shakeTimeLeft;

        CameraShot shot = CameraShot.Follow;
        float zoom = 1f, zoomVelocity, shift, shiftVelocity;
        Transform focusTarget;
        float focusUntil;

        public CameraShot Shot => shot;

        void Awake()
        {
            Main = this;
            SetTarget(target);
            Snap();
        }

        void OnDestroy()
        {
            if (Main == this) Main = null;
        }

        public void SetTarget(Transform value)
        {
            target = value;
            targetMotor = target != null ? target.GetComponent<CharacterMotor2_5D>() : null;
        }

        public void SetBounds(float min, float max)
        {
            minX = min;
            maxX = max;
        }

        public static void SetShot(CameraShot value)
        {
            if (Main != null) Main.shot = value;
        }

        /// <summary>Pulls the camera onto something else for a moment (boss entrances), then hands back.</summary>
        public static void FocusOn(Transform t, float seconds)
        {
            if (Main == null) return;
            Main.focusTarget = t;
            Main.focusUntil = Time.unscaledTime + seconds;
        }

        public static void Shake(float amplitude, float duration)
        {
            if (Main == null) return;
            amplitude *= Game.GameSettings.ScreenShake;
            if (amplitude <= 0f) return;
            if (amplitude >= Main.shakeAmplitude * (Main.shakeTimeLeft / Mathf.Max(Main.shakeDuration, 0.0001f)))
            {
                Main.shakeAmplitude = amplitude;
                Main.shakeDuration = duration;
                Main.shakeTimeLeft = duration;
            }
        }

        Vector3 Focus()
        {
            bool focusing = focusTarget != null && Time.unscaledTime < focusUntil;
            if (shot == CameraShot.Attract && !focusing)
            {
                // A slow dolly back and forth along the floor, like a lobby screensaver.
                float t = Time.unscaledTime * 0.045f;
                float x = Mathf.Lerp(minX + 11f, minX + 31f, 0.5f - 0.5f * Mathf.Cos(t * Mathf.PI * 2f));
                return new Vector3(x, 0.35f, 0.6f);
            }
            Vector3 p = (focusing ? focusTarget : target).position;
            float lead = shot == CameraShot.Follow && !focusing ? ahead : 0f;
            return new Vector3(Mathf.Clamp(p.x + lead + shift, minX - 3f, maxX + 3f), p.y * jumpFollow, p.z * depthFollow);
        }

        void Snap()
        {
            if (target == null) return;
            transform.position = Focus() + offset;
            transform.rotation = Quaternion.LookRotation(lookOffset - offset);
        }

        void LateUpdate()
        {
            if (target == null) return;
            float dt = Time.unscaledDeltaTime;
            bool focusing = focusTarget != null && Time.unscaledTime < focusUntil;

            float aheadTarget = targetMotor != null ? targetMotor.Facing * lookAhead : 0f;
            ahead = Mathf.SmoothDamp(ahead, aheadTarget, ref aheadVelocity, lookAheadSmoothTime);

            // Shot framing: how close, and where the subject sits in frame.
            float zoomTarget = focusing ? 0.62f : shot == CameraShot.Showcase ? 0.42f : shot == CameraShot.Attract ? 1.12f : 1f;
            float shiftTarget = shot == CameraShot.Showcase && !focusing ? 1.25f : 0f;
            zoom = Mathf.SmoothDamp(zoom, zoomTarget, ref zoomVelocity, 0.45f, Mathf.Infinity, dt);
            shift = Mathf.SmoothDamp(shift, shiftTarget, ref shiftVelocity, 0.45f, Mathf.Infinity, dt);

            Vector3 framedOffset = new Vector3(offset.x, offset.y * Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(zoom)), offset.z * zoom);
            Vector3 framedLook = lookOffset + Vector3.up * (1f - Mathf.Clamp01(zoom)) * 0.25f;
            Vector3 desired = Focus() + framedOffset;
            float smooth = shot == CameraShot.Follow && !focusing ? smoothTime : 0.35f;
            Vector3 position = Vector3.SmoothDamp(transform.position - ShakeOffsetLastFrame, desired, ref velocity, smooth, Mathf.Infinity, dt);

            // Shake runs on unscaled time so it still reads during hit-stop.
            Vector3 shake = Vector3.zero;
            if (shakeTimeLeft > 0f)
            {
                shakeTimeLeft -= dt;
                float strength = shakeAmplitude * Mathf.Clamp01(shakeTimeLeft / shakeDuration);
                shake = (Vector3)(Random.insideUnitCircle * strength);
            }
            ShakeOffsetLastFrame = shake;

            transform.position = position + shake;
            transform.rotation = Quaternion.LookRotation(framedLook - framedOffset);
        }

        Vector3 ShakeOffsetLastFrame { get; set; }
    }
}
