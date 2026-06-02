#if UNITY_EDITOR
using UnityEditor;
using Fusion.XR.Shared.Utils;

namespace Fusion.XR.Shared.Core
{
    [InitializeOnLoad]
    public class XRSharedCoreWeaver
    {
        public const string ADDON_ASMDEF_NAME_ASMDEF_NAME = "XRShared.Core";

        static XRSharedCoreWeaver()
        {
            AddonWeaver.AddAssemblyToWeaver(ADDON_ASMDEF_NAME_ASMDEF_NAME);
        }

    }
}
#endif