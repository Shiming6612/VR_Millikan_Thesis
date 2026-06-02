#if UNITY_EDITOR
using UnityEditor;
using System;

namespace Fusion.Addons.DynamicAudioGroup
{
    [InitializeOnLoad]
    public class DynamicAudioGroupWeaver
    {
        static DynamicAudioGroupWeaver()
        {
            Fusion.XR.Shared.Utils.AddonWeaver.AddAssemblyToWeaver("DynamicAudioGroup");
        }
    }
}
#endif