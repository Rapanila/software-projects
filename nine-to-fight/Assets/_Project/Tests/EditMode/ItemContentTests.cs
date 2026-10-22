using System.Linq;
using NineToFight.Combat;
using NineToFight.Enemies;
using NineToFight.Player;
using NUnit.Framework;
using UnityEditor;

namespace NineToFight.Tests
{
    /// <summary>Checks the generated content: every move someone can use has a real item to hold.</summary>
    public class ItemContentTests
    {
        static T[] LoadAll<T>() where T : UnityEngine.Object =>
            AssetDatabase.FindAssets("t:" + typeof(T).Name)
                .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)))
                .ToArray();

        static PropCatalog Catalog()
        {
            var catalog = LoadAll<PropCatalog>().FirstOrDefault();
            Assert.IsNotNull(catalog, "PropCatalog asset missing; run Nine to Fight > Build Starter Content");
            return catalog;
        }

        [Test]
        public void EveryPlayerClassMoveHasAnItem()
        {
            var catalog = Catalog();
            var classes = LoadAll<PlayerClass>();
            Assert.IsNotEmpty(classes);
            foreach (var c in classes)
            foreach (var move in c.BuildMoveSet())
                Assert.IsTrue(catalog.Contains(move.prop), $"{c.displayName}: '{move.displayName}' has no item (prop '{move.prop}')");
        }

        [Test]
        public void EveryEnemyAttackHasAnItem()
        {
            var catalog = Catalog();
            foreach (var w in LoadAll<WorkerClass>())
                Assert.IsTrue(catalog.Contains(w.attack.prop), $"{w.displayName}: attack has no item (prop '{w.attack.prop}')");
        }

        [Test]
        public void DefaultMovesHaveItems()
        {
            var catalog = Catalog();
            foreach (var move in MoveLibrary.All)
                Assert.IsTrue(catalog.Contains(move.prop), $"{move.displayName} has no item");
        }
    }
}

