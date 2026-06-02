#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

[InitializeOnLoad]
public class XRSharedCoreDefine
{
    const string DEFINE = "XRSHARED_CORE_ADDON_AVAILABLE"; 

    static XRSharedCoreDefine()
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