using UnityEditor;
using UnityEngine;

namespace NineToFight.EditorTools
{
    /// <summary>
    /// Imports the Mixamo clips in Art/Animations/Mixamo as humanoid animation, so they retarget onto the
    /// office workers. Body rotation and height are baked into the pose (spins and jumps show); sideways
    /// travel is dropped, since the motor moves the character.
    /// </summary>
    public class MixamoImport : AssetPostprocessor
    {
        public const string Folder = "Assets/_Project/Art/Animations/Mixamo";

        [MenuItem("Nine to Fight/Reimport Mixamo Animations", priority = 2)]
        public static void Reimport()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Folder }))
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
        }

        static bool IsMixamo(string path) => path.StartsWith(Folder + "/") && path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase);

        static bool Loops(string path)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            return name == "fight_idle" || name == "block_idle";
        }

        void OnPreprocessModel()
        {
            if (!IsMixamo(assetPath)) return;
            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.resampleCurves = true;
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;
        }

        void OnPreprocessAnimation()
        {
            if (!IsMixamo(assetPath)) return;
            var importer = (ModelImporter)assetImporter;
            var clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return;
            string name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            for (int i = 0; i < clips.Length; i++)
            {
                var c = clips[i];
                c.name = name;
                c.loopTime = Loops(assetPath);
                // This 30 fps take waits in guard for a second before the knockout begins.
                // Start at the blow so a killing hit immediately flows into the fall.
                if (name == "knockout") c.firstFrame += 30f;
                // The whole body turns with the move (hips into a punch, round on a spin): dropping that turn
                // left the hips locked while the spine and head kept twisting, breaking the body. The clip is
                // lined up on the body's own orientation, so it faces forward instead of wherever the actor stood.
                c.lockRootRotation = true;
                c.keepOriginalOrientation = false;
                c.rotationOffset = 0f;
                c.lockRootHeightY = true;           // bake hops and jumps into the pose
                c.keepOriginalPositionY = false;    // ...measured from the feet at the start, so nobody floats
                c.lockRootPositionXZ = false;       // travel goes to root motion, which is ignored
                c.keepOriginalPositionXZ = false;
                c.heightFromFeet = true;
                c.mirror = false;
            }
            importer.clipAnimations = new[] { clips[0] };
        }
    }
}
