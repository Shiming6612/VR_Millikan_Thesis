#if UNITY_EDITOR
using UnityEditor;
using Fusion.XR.Shared.Utils;

namespace Fusion.Addons.XRHandsSynchronization
{
    [InitializeOnLoad]
    public class LineDrawingWeaver
    {
        static LineDrawingWeaver()
        {
            AddonWeaver.AddAssemblyToWeaver(asmDefName: "LineDrawing");
        }
    }
}
#endif