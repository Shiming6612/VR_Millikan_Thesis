#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using Fusion.XRShared.Tools;

[InitializeOnLoad]
public class CheckVisionOsHelpersPackage
{
    static string[] packagesToSearch = new string[] { "com.unity.polyspatial", "com.unity.polyspatial.visionos", "com.unity.polyspatial.xr" };
    static string[] packagesToInstall = new string[] { "com.unity.polyspatial", "com.unity.polyspatial.visionos", "com.unity.polyspatial.xr" };
    const string DEFINE = "POLYSPATIAL_SDK_AVAILABLE";
    const bool DISPLAY_ERROR_IF_MISSING = false;

    static CheckVisionOsHelpersPackage()
    {
#if UNITY_6000_0_OR_NEWER
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.VisionOS)
        {
            // Issue in Unity 6.0: Polyspatial cannot be installed during Android builds (https://discussions.unity.com/t/cannot-build-android-platform-with-visionos-installed/1537864/6)
            // Remove DEFINE on non-visionOS targets, and an error will be thrown in the build preprocessor if the packages are still here
            var group = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
            var defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(group));

            if (defines.Contains(DEFINE)) {
                defines = defines.Replace(DEFINE + ";", "");
                defines = defines.Replace(DEFINE, "");
            }
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(group), defines);
            return;
        }
#endif

        var packageRequest = new PackagePresenceCheck(packagesToSearch, (packageInfoList) => {
            if(packageInfoList.Count == packagesToSearch.Length)
            {
                // all packages are available
                var group = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
                var defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(group));

                if (defines.Contains(DEFINE) == false) { defines = $"{defines};{DEFINE}"; }
                PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(group), defines);
            }
            else
            {
                string packagesStr = (packagesToInstall.Length <= 1) ? "package " : "packages ";
                packagesStr += string.Join(", ", packagesToInstall);
                if (DISPLAY_ERROR_IF_MISSING)
                {
#pragma warning disable CS0162 // Unreachable code detected
                    Debug.LogError($"For the VisionOS helpers work, you need to install {packagesStr} (available in Unity 2022.x)");
#pragma warning restore CS0162 // Unreachable code detected
                }
            }
        });
    }
}
#endif
