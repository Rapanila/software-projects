using UnityEngine;

namespace NineToFight.Characters
{
    /// <summary>
    /// Damped spring on a rotation, with angular velocity in the parent's frame so it can be
    /// "kicked" (hit reactions, impacts) and wobble back. Frequency is how fast it reacts (Hz); damping
    /// below 1 overshoots a little and settles, so a jolt reads as having weight instead of snapping.
    /// </summary>
    public struct SpringRotation
    {
        public Quaternion value;
        public Vector3 velocity; // radians per second
        bool initialised;

        public Quaternion Step(Quaternion target, float frequency, float damping, float dt)
        {
            if (!initialised)
            {
                Reset(target);
                return value;
            }
            float w = 2f * Mathf.PI * frequency;
            int steps = Mathf.Clamp(Mathf.CeilToInt(w * dt / 0.4f), 1, 24);
            float h = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                if (Quaternion.Dot(target, value) < 0f) target = new Quaternion(-target.x, -target.y, -target.z, -target.w);
                var delta = target * Quaternion.Inverse(value);
                delta.ToAngleAxis(out float angle, out Vector3 axis);
                if (angle > 180f) angle -= 360f;
                Vector3 error = float.IsNaN(axis.x) || axis.sqrMagnitude < 1e-6f ? Vector3.zero : axis * (angle * Mathf.Deg2Rad);
                velocity += (w * w * error - 2f * damping * w * velocity) * h;
                float speed = velocity.magnitude;
                if (speed > 1e-6f) value = Quaternion.AngleAxis(speed * h * Mathf.Rad2Deg, velocity / speed) * value;
            }
            value = Normalise(value);
            return value;
        }

        public void Kick(Vector3 angularVelocity)
        {
            if (initialised) velocity += angularVelocity;
        }

        public void Reset(Quaternion to)
        {
            value = to;
            velocity = Vector3.zero;
            initialised = true;
        }

        static Quaternion Normalise(Quaternion q)
        {
            float m = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            return m > 1e-6f ? new Quaternion(q.x / m, q.y / m, q.z / m, q.w / m) : Quaternion.identity;
        }
    }
}
