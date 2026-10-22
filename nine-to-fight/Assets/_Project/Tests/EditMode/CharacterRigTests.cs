using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace NineToFight.Tests
{
    /// <summary>Checks the built character prefabs: every body's hands are balled into fists.</summary>
    public class CharacterRigTests
    {
        static Transform Bone(Transform figure, string name) =>
            figure.GetComponentsInChildren<Transform>(true).First(t => t.name == name);

        [Test]
        public void HandsAreBalledIntoFists()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Coworker.prefab");
            Assert.IsNotNull(prefab);
            var figures = prefab.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Figure_")).ToArray();
            Assert.GreaterOrEqual(figures.Length, 2);
            var report = new System.Text.StringBuilder();
            bool open = false;
            foreach (var figure in figures)
                foreach (var side in new[] { "L", "R" })
                    foreach (var finger in new[] { "Index", "Middle", "Ring", "Pinky" })
                    {
                        Vector3 Pos(int i) => Bone(figure, $"{finger}{i}.{side}").position;
                        // Fingers fold back on themselves: the last segment points back against the palm.
                        float fold = Vector3.Angle(Pos(2) - Pos(1), Pos(4) - Pos(3));
                        report.AppendLine($"{figure.name} {finger}.{side}: {fold:0}°");
                        if (fold < 120f) open = true;
                    }
            Assert.IsFalse(open, "open hands:\n" + report);
        }
    }
}
