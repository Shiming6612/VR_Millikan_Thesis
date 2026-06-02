using Fusion;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Fusion.XR.Shared.Core
{
    [DefaultExecutionOrder(INetworkRig.EXECUTION_ORDER)]
    public class NetworkRig : NetworkBehaviour, INetworkRig
    {
        public const int EXECUTION_ORDER = INetworkRig.EXECUTION_ORDER;
        [Header("Options")]
        [SerializeField] bool useNetworkRigAsPlayerObject = false;

        [Header("Interpolation/extrapolation")]
        
        public ExtrapolationTiming extrapolationTiming = ExtrapolationTiming.DuringFusionRender;

        IHardwareRig localHardwareRig;
        INetworkHeadset networkHeadset;
        #region INetworkRig
        public IHeadset Headset => networkHeadset;
        public void RegisterNetworkRigPart(INetworkRigPart rigPart) {
            if (RigParts.Contains(rigPart) == false)
            {
                RigParts.Add(rigPart);

                if (rigPart is INetworkHeadset headset)
                {
                    networkHeadset = headset;
                }
            }
        }
        public void UnregisterNetworkRigPart(INetworkRigPart rigPart) {
            if (RigParts.Contains(rigPart))
            {
                RigParts.Remove(rigPart);
            }
        }
        public List<INetworkRigPart> RigParts { get; } = new List<INetworkRigPart>();
        public ExtrapolationTiming RequiredExtrapolationTiming => extrapolationTiming;

        #endregion

        #region NetworkBehaviour
        public override void Spawned()
        {
            base.Spawned();
            if (Object.HasStateAuthority) RegisterLocalNetworkUserRig();
            if (useNetworkRigAsPlayerObject)
            {
                // Set the networkRig as the PlayerObject to find it easily
                Runner.SetPlayerObject(Object.StateAuthority, Object);
            }
            Application.onBeforeRender += OnBeforeRender;

        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            base.Despawned(runner, hasState);
            Application.onBeforeRender -= OnBeforeRender;
        }

        public override void Render()
        {
            base.Render();
            if (Object.HasStateAuthority)
            {
                // We also do it in the render in case of late availability of the hardware rig
                RegisterLocalNetworkUserRig();

                if (extrapolationTiming == ExtrapolationTiming.DuringFusionRender)
                {
                    ExtrapolateWithLocalHardwareRig();
                }
            }
        }
        #endregion

        [BeforeRenderOrder(NetworkRig.EXECUTION_ORDER)]
        protected virtual void OnBeforeRender()
        {
            if (Object.HasStateAuthority)
            {
                if (extrapolationTiming == ExtrapolationTiming.DuringUnityOnBeforeRender)
                {
                    ExtrapolateWithLocalHardwareRig();
                }
            }
        }

        void RegisterLocalNetworkUserRig()
        {
            if (Object.HasStateAuthority)
            {
                foreach (var rig in HardwareRigsRegistry.GetAvailableHardwareRigs())
                {
                    localHardwareRig = rig;
                    localHardwareRig.RegisterLocalUserNetworkRig(this);
                }
            }
        }

        public override void FixedUpdateNetwork()
        {
            base.FixedUpdateNetwork();
            UpdateWithLocalHardwareRig();
        }

        protected virtual void UpdateWithLocalHardwareRig()
        {
            if (localHardwareRig == null) return;
            transform.position = localHardwareRig.transform.position;
            transform.rotation = localHardwareRig.transform.rotation;
            transform.localScale = localHardwareRig.transform.localScale;
        }

        protected void ExtrapolateWithLocalHardwareRig()
        {
            if (localHardwareRig == null) return;
            transform.position = localHardwareRig.transform.position;
            transform.rotation = localHardwareRig.transform.rotation;
            transform.localScale = localHardwareRig.transform.localScale;
        }
    }
}
