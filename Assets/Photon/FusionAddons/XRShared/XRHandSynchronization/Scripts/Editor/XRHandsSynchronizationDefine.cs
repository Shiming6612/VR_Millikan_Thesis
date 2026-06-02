#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

[InitializeOnLoad]
public class XRHandsSynchronizationDefine
{
    const string DEFINE = "XRHANDS_SYNCHRONIZATION_ADDON_AVAILABLE"; 

    static XRHandsSynchronizationDefine()
    {
        var group = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
        var defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(group));

        // Meta core
        if (defines.Contains(DEFINE) == false)
        {
            defines = $"{defines};{DEFINE}";
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(group), defines);
        }
    }
}
#endif
