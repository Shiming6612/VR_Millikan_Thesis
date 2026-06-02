#if UNITY_EDITOR
using UnityEditor;
using Fusion.XR.Shared.Utils;

namespace Fusion.Addons.TextureDrawing
{
    [InitializeOnLoad]
    public class TextureDrawingWeaver
    {
        public const string ADDON_ASMDEF_NAME_ASMDEF_NAME = "TextureDrawing";

        static TextureDrawingWeaver()
        {
            AddonWeaver.AddAssemblyToWeaver(ADDON_ASMDEF_NAME_ASMDEF_NAME);
        }

    }
}
#endif