#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using WebSocketSharp;

namespace Fusion.XR.Shared.Automatization
{
    public static class AutomatisationTools
    {
        /// <summary>
        /// Try to find asset based on its name
        /// </summary>
        /// <param name="requiredPathElements">Optional required string in the found asset path</param>
        /// <returns>True if a matching asset has been found</returns>
        public static bool TryFindAsset<T>(string name, out T asset, string extension = null, string[] requiredPathElements = null) where T : UnityEngine.Object
        {
            asset = default;
            if(string.IsNullOrEmpty(extension) == false)
            {
                name += " t:"+extension;
            }
#if UNITY_EDITOR
            var guids = AssetDatabase.FindAssets(name);
            foreach (var guid in guids)
            {
                if (TryFindAssetByGuid(guid, out asset, requiredPathElements))
                {
                    return true;
                }
            }
#endif
            Debug.LogError($"Asset not found \"{name}\" with selected filter");
            return false;
        }

        public static bool TryFindAsset<T>(string name, out T asset, string extension = null, string requiredPathElement = null) where T : UnityEngine.Object
        {
            return TryFindAsset<T>(name, out asset, extension, new string[] { requiredPathElement });
        }
        /// <summary>
        /// Try to find asset based on its guid
        /// </summary>
        /// <param name="requiredPathElements">Optional required string in the found asset path</param>
        /// <returns>True if a matching asset has been found</returns>
        public static bool TryFindAssetByGuid<T>(string guid, out T asset, string[] requiredPathElements = null) where T : UnityEngine.Object
        {
            asset = default;
#if UNITY_EDITOR
            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(assetPath))
            {
                return false;
            }
            if (requiredPathElements != null)
            {
                foreach(var requiredPathElement in requiredPathElements)
                {
                    if (string.IsNullOrEmpty(requiredPathElement) == false && assetPath.Contains(requiredPathElement) == false)
                    {
                        return false;
                    }
                }
            }

            asset = (T)AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset == null)
            {
                return false;
            }
            return true;
#else
            return false;
#endif
        }
    }
}

