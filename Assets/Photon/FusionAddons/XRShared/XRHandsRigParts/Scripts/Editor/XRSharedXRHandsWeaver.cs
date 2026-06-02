#if UNITY_EDITOR
using UnityEditor;
using Fusion.XR.Shared.Utils;

namespace Fusion.XR.Shared.XRHands
{
    [InitializeOnLoad]
    public class XRSharedXRHandsWeaver
    {
        public const string ADDON_ASMDEF_NAME_ASMDEF_NAME = "XRShared.XRHands";

        static XRSharedXRHandsWeaver()
        {
            AddonWeaver.AddAssemblyToWeaver(ADDON_ASMDEF_NAME_ASMDEF_NAME);
        }

    }
}
#endif