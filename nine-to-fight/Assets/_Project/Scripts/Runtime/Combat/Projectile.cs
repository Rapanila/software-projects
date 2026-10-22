using NineToFight.Core;
using UnityEngine;

namespace NineToFight.Combat
{
    /// <summary>
    /// A thrown office item flying down the lane at hip height. Jump over it, dodge through it
    /// (invulnerable), or step out of its depth lane. Smacking it with an attack knocks it back.
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        static readonly Collider[] Buffer = new Collider[8];
        public static readonly System.Collections.Generic.List<Projectile> Live = new System.Collections.Generic.List<Projectile>();
        public static event System.Action<Projectile> Reflected;

        void OnEnable() => Live.Add(this);
        void OnDisable() => Live.Remove(this);

        Vector3 velocity;
        float damage, knockback, dieAt;
        Team team;
        GameObject source;
        Transform spinner;

        public Team Team => team;

        public static Projectile Launch(GameObject prefab, Vector3 position, Vector3 direction, float speed, float damage, Team team, GameObject source)
        {
            var root = new GameObject("Projectile");
            root.transform.position = position;
            var p = root.AddComponent<Projectile>();
            p.velocity = direction.normalized * speed;
            p.damage = damage;
            p.knockback = 6f;
            p.team = team;
            p.source = source;
            p.dieAt = Time.time + 2.2f;

            p.spinner = new GameObject("Spin").transform;
            p.spinner.SetParent(root.transform, false);
            if (prefab != null)
            {
                var item = Instantiate(prefab, p.spinner);
                item.transform.localPosition = new Vector3(0f, 0.25f, 0f);
                item.transform.localScale = Vector3.one * 1.5f;
                foreach (var c in item.GetComponentsInChildren<Collider>()) Destroy(c);
            }
            else
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(cube.GetComponent<Collider>());
                cube.transform.SetParent(p.spinner, false);
                cube.transform.localScale = Vector3.one * 0.25f;
            }
            return p;
        }

        /// <summary>Sends it back the other way; it now hurts its thrower's side.</summary>
        public void Reflect(Team newTeam, GameObject newSource)
        {
            velocity = -velocity * 1.3f;
            team = newTeam;
            source = newSource;
            damage *= 1.5f;
            dieAt = Time.time + 2f;
            Reflected?.Invoke(this);
        }

        void Update()
        {
            if (Time.time >= dieAt)
            {
                Destroy(gameObject);
                return;
            }
            transform.position += velocity * Time.deltaTime;
            spinner.Rotate(0f, 0f, -900f * Time.deltaTime * Mathf.Sign(velocity.x == 0f ? 1f : velocity.x), Space.Self);

            int count = Physics.OverlapSphereNonAlloc(transform.position, 0.4f, Buffer, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var target = Buffer[i].GetComponentInParent<Health>();
                if (target == null || target.Team == team || target.IsDead) continue;
                bool hit = target.TakeDamage(new DamageInfo
                {
                    amount = damage,
                    knockback = new Vector3(Mathf.Sign(velocity.x), 0f, 0f) * knockback + Vector3.up * 2f,
                    source = source,
                    hitStop = 0.05f,
                });
                // Invulnerable dodgers pass straight through; anything else stops it.
                if (hit || !target.IsInvulnerable)
                {
                    Destroy(gameObject);
                    return;
                }
            }
        }
    }
}
