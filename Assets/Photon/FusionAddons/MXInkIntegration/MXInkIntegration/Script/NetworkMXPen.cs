using Fusion.Addons.LineDrawing;
using Fusion.XR.Shared.Core;
using Fusion.XR.Shared.Core.Interaction.Contact;
using System.Collections.Generic;
using UnityEngine;


namespace Fusion.Addons.MXPenIntegration {

    [DefaultExecutionOrder(NetworkMXPen.EXECUTION_ORDER)]
    public class NetworkMXPen : NetworkRigPart
    {
        public new const int EXECUTION_ORDER = INetworkGrabbable.EXECUTION_ORDER + 1;

        #region NetworkRigPart
        public override RigPartKind Kind => RigPartKind.Stylus;
        #endregion

        [Networked]
        public NetworkBool IsReplacingRightHand { get; set; }

        [SerializeField] bool automaticallyDetectNetworkHands = true;

        protected NetworkLineDrawer networkLineDrawer;

        IContactHandler[] contactHandlers;

        [Tooltip("If true, if any component implementing IContactHandler returns true for IsHandlingContact, the tip pressure drawing will be ignored")]
        [SerializeField] bool ignoreContactPressureIfVirtualContactAlreadyHandled = true;

        protected IFeedbackHandler feedback;
        [Header("Drawing Feedback")]
        [SerializeField] string audioType;

        protected override void Awake()
        {
            base.Awake();
            contactHandlers = GetComponentsInChildren<IContactHandler>();
            networkLineDrawer = GetComponentInChildren<NetworkLineDrawer>();
            feedback = GetComponent<IFeedbackHandler>();
            if (automaticallyDetectNetworkHands)
            {
                var rig = GetComponentInParent<NetworkRig>();
            }
        }

        protected override bool IsMatchingHardwareRigPart(IHardwareRigPart rigPart)
        {
            bool matching = base.IsMatchingHardwareRigPart(rigPart);
            // Additional check in case of several stylus integration cohabit
            if (matching && rigPart is IMXInkStateProvider mxInkStateProvider)
            {
                return true;
            }
            return false;
        }

        public override void FixedUpdateNetwork()
        {
            base.FixedUpdateNetwork();

            if (LocalHardwareRigPart is IMXInkStateProvider mxInkStateProvider)
            {
                IsReplacingRightHand = mxInkStateProvider.CurrentState.isOnRightHand;
            }
        }

        public override void Render()
        {
            base.Render();
         
            if (LocalHardwareRigPart is IMXInkStateProvider mxInkStateProvider)
            {
                if (networkLineDrawer)
                {
                    VolumeDrawing(mxInkStateProvider);
                }
            }
        }

        protected virtual void VolumeDrawing(IMXInkStateProvider mxInkStateProvider)
        {
            var pressure = mxInkStateProvider.CurrentState.cluster_middle_value;
            bool shouldIgnoreContactPressure = false;
            if (ignoreContactPressureIfVirtualContactAlreadyHandled)
            {
                foreach (var handler in contactHandlers)
                {
                    if (handler.IsHandlingContact)
                    {
                        shouldIgnoreContactPressure = true;
                        break;
                    }
                }
            }
            if (shouldIgnoreContactPressure == false)
            {
                var tipPressure = mxInkStateProvider.CurrentState.tip_value;
                pressure = Mathf.Max(pressure, tipPressure);
            }
            if (pressure > 0.01f)
            {
                networkLineDrawer.AddPoint(pressure: pressure);
                if (feedback != null && feedback.IsAudioFeedbackIsPlaying() == false)
                {
                    feedback.PlayAudioAndHapticFeedback(audioType: audioType, audioOverwrite: false, hapticAmplitude: pressure, hardwareRigPart: LocalHardwareRigPart as IHapticFeedbackProviderRigPart);
                }
            }
            else if (networkLineDrawer.IsDrawingLine)
            {
                networkLineDrawer.StopLine();
               
                if (feedback != null)
                {
                    feedback.StopAudioFeedback();
                }
            }

            // Stop drawing causes
            bool shouldStopCurrentDrawing = ShouldStopCurrentVolumeDrawing(mxInkStateProvider);
            if (pressure == 0 && shouldStopCurrentDrawing)
            {
                networkLineDrawer.StopDrawing();
                if (feedback != null)
                {
                    feedback.StopAudioFeedback();
                }
            }
            if (networkLineDrawer.IsDrawing && TrackingStatus == RigPartTrackingstatus.NotTracked)
            {
                networkLineDrawer.StopDrawing();
                if (feedback != null)
                {
                    feedback.StopAudioFeedback();
                }
            }
        }

        protected virtual bool ShouldStopCurrentVolumeDrawing(IMXInkStateProvider mxInkStateProvider)
        {
            return mxInkStateProvider.CurrentState.cluster_back_value || mxInkStateProvider.CurrentState.cluster_front_value;
        }
    }
}

