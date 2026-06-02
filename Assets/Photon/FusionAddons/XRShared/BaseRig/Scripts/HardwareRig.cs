using Fusion.XR.Shared.Core;
using Fusion.XR.Shared.Core.Interaction;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Fusion.XR.Shared.Base
{
    public class HardwareRig : MonoBehaviour, IMovableHardwareRig
    {
        public List<IHardwareRigPart> RigParts { get; } = new List<IHardwareRigPart>();
        public ICameraFader headsetFader;

        [Tooltip("In case of multiple runner (multi-peer mode,...), specifiy the NetworkRunner for this hardware rig. Otherwise, will be automatically set by the BaseNetworkRig upon HardwareRig detection")]
        [SerializeField] NetworkRunner _runner;

        #region IHardwarerig
        public virtual IHeadset Headset { get; set; }

        public NetworkRunner Runner => _runner;

        protected virtual void OnEnable()
        {
            HardwareRigsRegistry.RegisterAvailableHardwareRig(this);
        }

        protected virtual void OnDisable()
        {
            HardwareRigsRegistry.UnregisterAvailableHardwareRig(this);
        }

        public void SetRunner(NetworkRunner runner)
        {
            _runner = runner;
        }

        INetworkRig _localUserNetworkRig = null;
        public INetworkRig LocalUserNetworkRig => _localUserNetworkRig;

        public void RegisterLocalUserNetworkRig(INetworkRig localUserNetworkRig) {
            _localUserNetworkRig = localUserNetworkRig;
        }

        #endregion

        #region Rig parts handling
        public virtual void RegisterHardwareRigPart(IHardwareRigPart rigPart)
        {
            if (RigParts.Contains(rigPart) == false)
            {
                RigParts.Add(rigPart);

                if (rigPart is IHeadset headset)
                {
                    Headset = headset;
                }
            }
        }

        public void UnregisterHardwareRigPart(IHardwareRigPart rigPart)
        {
            if (RigParts.Contains(rigPart))
            {
                RigParts.Remove(rigPart);
            }
        }
        #endregion

        #region Locomotion
        // Update the hardware rig rotation. 
        public virtual void Rotate(float angle)
        {
            this.RotateAroundHeadset(angle);
        }

        // Update the hardware rig position. 
        public virtual void Teleport(Vector3 position)
        {
            Vector3 previousPosition = transform.position;
            this.TeleportHeadsetGroundProjection(position);
        }

        protected virtual void DetectFader()
        {
            if(headsetFader == null && Headset is IFadeable fadeable && fadeable.Fader != null)
            {
                headsetFader = fadeable.Fader;
            }
        }

        // Teleport the rig with a fader
        public virtual IEnumerator FadedTeleport(Vector3 position)
        {
            DetectFader();
            teleportInProgress = true;
            if (headsetFader != null) yield return headsetFader.FadeIn();
            Teleport(position);
            if (headsetFader != null) yield return headsetFader.WaitBlinkDuration();
            if (headsetFader != null) yield return headsetFader.FadeOut();
            teleportInProgress = false;
        }

        // Rotate the rig with a fader
        public virtual IEnumerator FadedRotate(float angle)
        {
            DetectFader();
            rotationInProgress = true;
            if (headsetFader != null) yield return headsetFader.FadeIn();
            Rotate(angle);
            if (headsetFader != null) yield return headsetFader.WaitBlinkDuration();
            if (headsetFader != null) yield return headsetFader.FadeOut();
            rotationInProgress = false;
        }
        #endregion

        #region IMovableHardwareRig

        bool rotationInProgress = false;
        bool teleportInProgress = false;
        public bool SnapMovementInProgress => rotationInProgress || teleportInProgress;

        public void Rotate(float angle, bool addSnapMovementVisualProtection)
        {
            if (addSnapMovementVisualProtection)
            {
                StartCoroutine(FadedRotate(angle));
            }
            else
            {
                Rotate(angle);
            }
        }

        public void Teleport(Vector3 position, bool addSnapMovementVisualProtection)
        {
            if (addSnapMovementVisualProtection)
            {
                StartCoroutine(FadedTeleport(position));
            }
            else
            {
                Teleport(position);
            }
        }
        #endregion

        private void Update()
        {
            int i = 0;
            while(i < RigParts.Count){
                var rigPart = RigParts[i];
                if (rigPart.gameObject.activeSelf == false)
                {
                    // The rig part might not be able to update its tracking status itself, as it is disabled
                    rigPart.UpdateTrackingStatus();
                }
                i++;
            }
        }
    }
}

