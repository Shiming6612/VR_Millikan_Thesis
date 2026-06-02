using Fusion.XR.Shared.Core;
using Fusion.XR.Shared.Core.Interaction;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fusion.XR.Shared.Core
{
    [DefaultExecutionOrder(INetworkRig.EXECUTION_ORDER)]
    public abstract class NetworkRigPart : NetworkBehaviour, INetworkRigPart
    {
        public const int EXECUTION_ORDER = INetworkRig.EXECUTION_ORDER + 10;
        [Header("Visualization")]
        [Tooltip("Adapt renderers to tracking status. Will add a RigPartVisualizer if none is present on this component. If present, will set its adaptRenderersDuringUpdate to false, to manually call its update logic during Render")]
        public bool adaptRenderersToTrackingStatus = true;
        [DrawIf(nameof(adaptRenderersToTrackingStatus), true, Hide = true)]
        public bool hideRenderersForStateAuthority = false;

        [Header("Position modifiers")]
        [Tooltip("Allow position modifiers to adapt the rig part position (to reflect blocking UIs, ...)")]
        public bool allowPositionModifiers = true;
        [DrawIf(nameof(allowPositionModifiers), true, Hide = true)]
        public bool addHapticFeedbackOnPositionModifications = true;
        [DrawIf(nameof(allowPositionModifiers), true, Hide = true)]
        public float positionOffsetForMaxHapticFeedback = 0.3f;
        [DrawIf(nameof(allowPositionModifiers), true, Hide = true)]
        public float positionOffsetHapticAmplitude = 1f;
        [DrawIf(nameof(allowPositionModifiers), true, Hide = true)]
        public float positionOffsetHapticDuration = 0.05f;

        [Header("Debug")]
        [SerializeField] GameObject localHardwareRigPartGameObject;

        [Networked, OnChangedRender(nameof(OnTrackingStatusChange))]
        public RigPartTrackingstatus TrackingStatus { get; set; } = RigPartTrackingstatus.NotTracked;

        protected IHardwareRig localHardwareRig = null;
        protected IHardwareRigPart _localHardwareRigPart = null;
        protected List<IRigPartPositionModifier> localHardwareRigPartPositionModifiers = new List<IRigPartPositionModifier>();
        protected List<IRigPartPositionModifier> rigPartPositionModifiers = new List<IRigPartPositionModifier>();

        public INetworkRig networkRig;

        public Vector3 DisplayedPositionWithoutModifiers { get; set; } = Vector3.zero;
        public Quaternion DisplayedRotationWithoutModifiers { get; set; } = Quaternion.identity;


        #region INetworkRigPart

        public abstract RigPartKind Kind { get; }
        public IHardwareRigPart LocalHardwareRigPart => _localHardwareRigPart;
        public IRig Rig => networkRig;

        RigPartVisualizer rigPartVisualizer;
        #endregion


        protected virtual void Awake()
        {
            networkRig = GetComponentInParent<INetworkRig>();
            rigPartVisualizer = GetComponent<RigPartVisualizer>();
            networkRig?.RegisterNetworkRigPart(this);
            if (adaptRenderersToTrackingStatus)
            {
                if (rigPartVisualizer == null)
                {
                    rigPartVisualizer = gameObject.AddComponent<RigPartVisualizer>();
                    rigPartVisualizer.mode = RigPartVisualizer.Mode.DisplayWhileOnline;
                }
                rigPartVisualizer.adaptRenderersDuringUpdate = false;
            }
            rigPartPositionModifiers = new List<IRigPartPositionModifier>(GetComponentsInChildren<IRigPartPositionModifier>());
        }

        #region NetworkBehaviour

        public override void Spawned()
        {
            base.Spawned();
            OnTrackingStatusChange();
            Application.onBeforeRender += OnBeforeRender;
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            base.Despawned(runner, hasState);
            networkRig?.UnregisterNetworkRigPart(this);
            Application.onBeforeRender -= OnBeforeRender;
        }

        public override void Render()
        {
            base.Render();
            if (Object.HasStateAuthority)
            {
                DetectHardwareRig();
                DetectedHardwareRigPart();

            }
            if (this.RequiredExtrapolationTiming() == ExtrapolationTiming.DuringFusionRender)
            {
                AdaptDisplayedPosition();
            }
        }

        protected virtual void AdaptDisplayedPosition()
        {
            if (Object.HasStateAuthority)
            {
                ExtrapolateWithLocalHardwareRigPart();
            }
            DisplayedPositionWithoutModifiers = transform.position;
            DisplayedRotationWithoutModifiers = transform.rotation;
            ApplyPositionModifiers();
        }

        public override void FixedUpdateNetwork()
        {
            base.FixedUpdateNetwork();
            UpdateWithLocalHardwareRigPart();
        }
        #endregion

        [BeforeRenderOrder(NetworkRigPart.EXECUTION_ORDER)]
        protected virtual void OnBeforeRender()
        {
            if (this.RequiredExtrapolationTiming() == ExtrapolationTiming.DuringUnityOnBeforeRender)
            {
                AdaptDisplayedPosition();
            }
        }

        protected virtual void UpdateWithLocalHardwareRigPart()
        {
            if (_localHardwareRigPart == null) return;
            _localHardwareRigPart.UpdateTrackingStatus();
            TrackingStatus = _localHardwareRigPart.TrackingStatus;
            var hardwareRigPartPose = _localHardwareRigPart.RigPartPose;
            transform.position = hardwareRigPartPose.position;
            transform.rotation = hardwareRigPartPose.rotation;
        }

        protected virtual void ExtrapolateWithLocalHardwareRigPart()
        {
            if (_localHardwareRigPart == null) return;
            var hardwareRigPartPose = _localHardwareRigPart.RigPartPose;
            transform.position = hardwareRigPartPose.position;
            transform.rotation = hardwareRigPartPose.rotation;
        }

        void ApplyPositionModifiers()
        {
            if (allowPositionModifiers)
            {
                foreach (var rigPartPositionModifier in rigPartPositionModifiers)
                {
                    if (TryApplyPositionModifiers(rigPartPositionModifier))
                    {
                        // We only apply one position modifier changes
                        return;
                    }
                }
                foreach (var localHardwareRigPartPositionModifier in localHardwareRigPartPositionModifiers)
                {
                    if (TryApplyPositionModifiers(localHardwareRigPartPositionModifier))
                    {
                        // We only apply one position modifier changes
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Return true if a position modifier has changed the positions
        /// </summary>
        bool TryApplyPositionModifiers(IRigPartPositionModifier positionModifier)
        {
            // Check if a modification of pose is required
            if (positionModifier == null)
            {
                return false;
            }
            if (positionModifier is IRigPartPositionModifierProxy proxy)
            {
                // If the modified is in fact a proxy, we forward the request to the proxified modifier
                return TryApplyPositionModifiers(proxy.ActualModifier);
            }
            if (positionModifier.IsModificationActive == false)
            {
                // If the modification is not active, nothing to do
                return false;
            }
            if (positionModifier.ApplyOnlyLocally && Object.HasStateAuthority == false)
            {
                // We are on a proxy, and the modifier request to change the position only for the local user
                return false;
            }

            // Actual transform pose modification
            Vector3 positionOffset;
            if (positionModifier.PositioningMode == IRigPartPositionModifier.ModificationPositioningMode.Offset)
            {
                // The change is an offset, we use PositionModification/RotationModification as an offset pose to modify the transform pose
                positionOffset = positionModifier.PositionModification;
                transform.rotation = transform.rotation * positionModifier.RotationModification;
                transform.position += positionOffset;
            }
            else
            {
                // The change is absolute, we use PositionModification/RotationModification as a world pose to override the transform pose
                positionOffset = positionModifier.PositionModification - transform.position;
                transform.rotation = positionModifier.RotationModification;
                transform.position = positionModifier.PositionModification;
            }

            // Haptic feedback
            if (Object.HasStateAuthority && positionModifier.IsHapticFeedbackRequired && addHapticFeedbackOnPositionModifications && _localHardwareRigPart is IHapticFeedbackProviderRigPart hapticProvider)
            {
                // If the modifier request an haptic feedback, we apply one proportionnal to the position offset
                float amplitude = positionOffsetHapticAmplitude * Mathf.Clamp01(positionOffset.magnitude / positionOffsetForMaxHapticFeedback);
                hapticProvider.SendHapticImpulse(amplitude: amplitude, duration: positionOffsetHapticDuration);
            }
            return true;
        }

        #region Detection
        protected virtual void DetectHardwareRig()
        {
            if (Object.HasStateAuthority && localHardwareRig == null)
            {
                // Detect the matching hardware rig
                localHardwareRig = FindHardwareRig();
                if (localHardwareRig == null)
                {
                    Debug.LogError("Hardware rig not found");
                }
            }
        }

        protected virtual IHardwareRig FindHardwareRig()
        {
            // Detect the matching hardware rig matching this runner - if several rig are present (multi peer scenario), we use the runner to differenciate
            var hardwareRig = HardwareRigsRegistry.GetHardwareRig(Runner);
            // Register the runner in the hardware rig if required
            if (hardwareRig != null && hardwareRig.Runner == null)
            {
                hardwareRig.SetRunner(Runner);
            }
            return hardwareRig;
        }

        protected virtual bool IsMatchingHardwareRigPart(IHardwareRigPart rigPart)
        {
            if (rigPart.Kind == Kind)
            {
                // Matching kind
                if (this is ILateralizedRigPart networkPart && rigPart is ILateralizedRigPart hardwarePart && networkPart.Side != hardwarePart.Side)
                {
                    if (networkPart.Side == RigPartSide.Undefined)
                    {
                        Debug.LogError($"[Network rig issue] Please affect side on {name}");
                    }
                    return false;
                }
                return true;
            }
            return false;
        }

        protected virtual void DetectedHardwareRigPart()
        {
            if (localHardwareRig != null && _localHardwareRigPart == null)
            {
                foreach (var rigPart in localHardwareRig.RigParts)
                {
                    if (IsMatchingHardwareRigPart(rigPart))
                    {
                        _localHardwareRigPart = rigPart;
                        _localHardwareRigPart.RegisterLocalUserNetworkRigPart(this);
                        // For debug purposes
                        localHardwareRigPartGameObject = rigPart.gameObject;

                        localHardwareRigPartPositionModifiers = new List<IRigPartPositionModifier>(_localHardwareRigPart.gameObject.GetComponentsInChildren<IRigPartPositionModifier>(true));
                    }
                }
            }
        }
        #endregion


        protected virtual void OnTrackingStatusChange()
        {
            if (adaptRenderersToTrackingStatus && rigPartVisualizer)
            {
                var shouldDisplay = rigPartVisualizer.ShouldDisplay();
                if (shouldDisplay)
                {
                    shouldDisplay = TrackingStatus == RigPartTrackingstatus.Tracked;
                }
                if (shouldDisplay && hideRenderersForStateAuthority)
                {
                    shouldDisplay = Object.HasStateAuthority == false;
                }
                rigPartVisualizer.Adapt(shouldDisplay);
            }
        }
    }
}

