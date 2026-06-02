#if UNITY_EDITOR
using UnityEditor;
using Fusion.XR.Shared.Utils;

namespace Fusion.XR.Shared.XRIT
{
    [InitializeOnLoad]
    public class XRSharedXRITWeaver
    {
        public const string ADDON_ASMDEF_NAME_ASMDEF_NAME = "XRShared.XRIT";

        static XRSharedXRITWeaver()
        {
            AddonWeaver.AddAssemblyToWeaver(ADDON_ASMDEF_NAME_ASMDEF_NAME);
        }

    }
}
#endif