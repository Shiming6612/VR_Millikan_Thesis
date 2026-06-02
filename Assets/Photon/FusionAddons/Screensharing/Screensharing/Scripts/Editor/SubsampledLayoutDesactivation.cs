#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class SubsampledLayoutDesactivation
{
    static SubsampledLayoutDesactivation()
    {
#if OPENXR_AVAILABLE
#if META_XR_SDK_CORE_AVAILABLE
        var settings = UnityEngine.XR.OpenXR.OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android); // BuildTargetGroup.Android
        var ext = settings.GetFeature<Meta.XR.MetaXRSubsampledLayout>();
        if (ext)
        {
            if (ext.enabled)
            {
                Debug.LogError("MetaXRSubsampledLayout is only compatible with Vulkan, that cannot be used with the Photon Video SDK: disabling it.");
                ext.enabled = false;
            }
        }            
#endif
#endif
    }
}
#endif
