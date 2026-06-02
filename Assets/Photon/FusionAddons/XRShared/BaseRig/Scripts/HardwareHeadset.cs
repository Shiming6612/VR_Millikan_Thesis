using Fusion.XR.Shared.Core;
using Fusion.XR.Shared.Core.Interaction;
using UnityEngine;

namespace Fusion.XR.Shared.Base
{
    public class HardwareHeadset : BaseHardwareRigPart, IHardwareHeadset, IFadeable
    {
        public override RigPartKind Kind => RigPartKind.Headset;

        public ICameraFader Fader { get; set; }

        protected override void Awake()
        {
            base.Awake();
            Fader = GetComponentInChildren<ICameraFader>(true);
        }
    }
}