using Fusion.XR.Shared.Base;

namespace Fusion.Addons.Meta
{
    public class MetaBridgeHardwareHeadset : HardwareHeadset
    {


        protected override void Awake()
        {
            base.Awake();
            // We let the meta rig deal with gameobject status
            disabledGameObjectWhenNotTracked = false;
        }
    }
}
