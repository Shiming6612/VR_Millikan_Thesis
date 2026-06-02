using Fusion.XRShared.Tools;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

class PolyspatialBuildPreparation : IPreprocessBuildWithReport
{
    public int callbackOrder { get { return 100; } }
    public void OnPreprocessBuild(BuildReport report)
    {
        var packagesToSearch = new string[]{
                "com.unity.polyspatial",
                "com.unity.polyspatial.visionos",
                "com.unity.polyspatial.xr"
            };
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.VisionOS)
        {
#if UNITY_6000_0_OR_NEWER
            // Issue in Unity 6.0: Polyspatial cannot be installed during Android builds (https://discussions.unity.com/t/cannot-build-android-platform-with-visionos-installed/1537864/6)


            var packageRequest = new PackagePresenceCheck(packagesToSearch, (packageInfoList) => {
                if (packageInfoList.Count > 0)
                {
                    List<string> packagesToRemove = new List<string>();
                    foreach(var p in packageInfoList)
                    {
                        packagesToRemove.Add(p.Value.name);
                    }
                    throw new BuildFailedException("Polyspatial packages should be removed while building for Android on Unity 6: "+ string.Join(", ", packagesToRemove));
                }
            });
#endif
        } 
        else 
        {
            var packageRequest = new PackagePresenceCheck(packagesToSearch, (packageInfoList) => {
                if (packageInfoList.Count != packagesToSearch.Length)
                {
                    throw new BuildFailedException("Polyspatial packages should be installed while building for visionOS: " + string.Join(", ", packagesToSearch));
                }
            });
        }
    }
}