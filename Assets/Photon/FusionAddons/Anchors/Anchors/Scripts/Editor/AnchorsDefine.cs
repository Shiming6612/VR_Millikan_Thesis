#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

[InitializeOnLoad]
public class AnchorsDefine
{
    const string OPENCV_DEFINE = "OPENCV_FOR_UNITY_AVAILABLE"; 

    static AnchorsDefine()
    {
        bool isOpenCVForUnityMissing = Type.GetType("OpenCVForUnity.CoreModule.MatOfPoint3f, EnoxSoftware.OpenCVForUnity") == null;
        if (isOpenCVForUnityMissing == false)
        {
            var group = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
            var defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(group));

            if (defines.Contains(OPENCV_DEFINE) == false)
            {
                defines = $"{defines};{OPENCV_DEFINE}";
                PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(group), defines);
            }
        }
    }
}
#endif