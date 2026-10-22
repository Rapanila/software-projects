using NineToFight.Core;
using UnityEngine;

namespace NineToFight.Enemies
{
    /// <summary>Job title and health bar floating above a coworker.</summary>
    [RequireComponent(typeof(EnemyBrain))]
    public class EnemyOverheadDisplay : MonoBehaviour
    {
        [SerializeField] Material backMaterial;
        [SerializeField] Material fillMaterial;
        [SerializeField] float height = 2.55f;
        [SerializeField] float width = 1f;
        [SerializeField] float barHeight = 0.1f;

        Health health;
        Transform root;
        Transform fill;

        void Start()
        {
            health = GetComponent<Health>();
            var brain = GetComponent<EnemyBrain>();

            root = new GameObject("Overhead").transform;
            root.SetParent(transform, false);
            root.localPosition = Vector3.up * height;

            CreateQuad("BarBack", backMaterial, new Vector3(width + 0.04f, barHeight + 0.04f, 1f), 0f);
            fill = CreateQuad("BarFill", fillMaterial, new Vector3(width, barHeight, 1f), -0.01f);

            var font = UI.UiKit.Heading;
            var labelGo = new GameObject("Title");
            labelGo.transform.SetParent(root, false);
            labelGo.transform.localPosition = new Vector3(0f, barHeight + 0.06f, 0f);
            var label = labelGo.AddComponent<TextMesh>();
            label.text = brain.WorkerClass != null ? brain.WorkerClass.displayName.ToUpperInvariant() : "COWORKER";
            label.font = font;
            label.fontSize = 48;
            label.characterSize = 0.045f;
            label.anchor = TextAnchor.LowerCenter;
            label.alignment = TextAlignment.Center;
            label.color = brain.IsElite ? new Color(1f, 0.84f, 0.3f, 1f) : new Color(1f, 1f, 1f, 0.9f);
            labelGo.GetComponent<MeshRenderer>().sharedMaterial = font.material;

            health.Died += () => root.gameObject.SetActive(false);
        }

        Transform CreateQuad(string name, Material material, Vector3 scale, float z)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(quad.GetComponent<Collider>());
            quad.name = name;
            quad.transform.SetParent(root, false);
            quad.transform.localPosition = new Vector3(0f, 0f, z);
            quad.transform.localScale = scale;
            var r = quad.GetComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (material != null) r.sharedMaterial = material;
            return quad.transform;
        }

        void LateUpdate()
        {
            if (fill == null) return;
            float n = health.Normalized;
            fill.localScale = new Vector3(width * n, barHeight, 1f);
            fill.localPosition = new Vector3(-(1f - n) * width * 0.5f, 0f, -0.01f);
        }
    }
}
