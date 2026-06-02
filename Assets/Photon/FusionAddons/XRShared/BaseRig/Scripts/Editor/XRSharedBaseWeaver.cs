#if UNITY_EDITOR
using UnityEditor;
using Fusion.XR.Shared.Utils;

namespace Fusion.XR.Shared.Base
{
    [InitializeOnLoad]
    public class XRSharedBaseWeaver
    {
        public const string ADDON_ASMDEF_NAME_ASMDEF_NAME = "XRShared.BaseRig";

        static XRSharedBaseWeaver()
        {
            AddonWeaver.AddAssemblyToWeaver(ADDON_ASMDEF_NAME_ASMDEF_NAME);
        }

    }
}
#endif