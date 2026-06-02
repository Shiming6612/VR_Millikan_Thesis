#if UNITY_EDITOR
using UnityEditor;
using Fusion.XR.Shared.Utils;

namespace Fusion.Addons.XRHandsSynchronization
{
    [InitializeOnLoad]
    public class XRHandsSynchronizationWeaver
    {
        public const string ADDON_ASMDEF_NAME_ASMDEF_NAME = "XRHandsSynchronization";

        static XRHandsSynchronizationWeaver()
        {
            AddonWeaver.AddAssemblyToWeaver(ADDON_ASMDEF_NAME_ASMDEF_NAME);
        }

    }
}
#endif