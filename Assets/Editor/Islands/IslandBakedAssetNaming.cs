using System.IO;
using UnityEditor;

namespace Lighthouse.World.Islands.Editor
{
    internal static class IslandBakedAssetNaming
    {
        public const string OutputRoot = "Assets/ThirdParty/World/FFinal/LegacyMeshCandidates";

        // Moves the folder and renames the assets inside it; GUIDs stay, so scene references survive.
        public static string Rename(string sourceName, string finalName)
        {
            string oldFolder = $"{OutputRoot}/{sourceName}";
            string newFolder = $"{OutputRoot}/{finalName}";
            if (AssetDatabase.IsValidFolder(oldFolder) && !AssetDatabase.IsValidFolder(newFolder))
            {
                string error = AssetDatabase.MoveAsset(oldFolder, newFolder);
                if (!string.IsNullOrEmpty(error))
                {
                    throw new IOException(error);
                }
            }

            foreach (string guid in AssetDatabase.FindAssets(string.Empty, new[] { newFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = Path.GetFileNameWithoutExtension(path);
                if (fileName.StartsWith(sourceName))
                {
                    string error = AssetDatabase.RenameAsset(path, finalName + fileName.Substring(sourceName.Length));
                    if (!string.IsNullOrEmpty(error))
                    {
                        throw new IOException(error);
                    }
                }
            }

            return newFolder;
        }
    }
}
