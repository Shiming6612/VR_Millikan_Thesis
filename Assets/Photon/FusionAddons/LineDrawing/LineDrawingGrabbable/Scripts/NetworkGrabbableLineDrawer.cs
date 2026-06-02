using Fusion.XR.Shared.Core;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fusion.Addons.LineDrawing

{
    public class NetworkGrabbableLineDrawer : NetworkLineDrawer
    {
        [Header("Drawing input")]
        [SerializeField] InputActionProperty leftTriggerAction = new InputActionProperty(new InputAction());
        [SerializeField] InputActionProperty rightTriggerAction = new InputActionProperty(new InputAction());
        INetworkGrabbable grabbable;
        public bool IsGrabbed => grabbable != null && grabbable.IsGrabbed;

        protected IFeedbackHandler feedback;

        [Header("Feedback")]
        [SerializeField] string audioType;
        [SerializeField] float hapticAmplitudeFactor = 0.1f;
        [SerializeField] FeedbackMode feedbackMode = FeedbackMode.AudioAndHaptic;

        public InputActionProperty? CurrentInput { 
            get
            {
                if (grabbable == null || grabbable.CurrentGrabber == null || grabbable.CurrentGrabber.RigPart == null)
                {
                    return null;
                }
                return (grabbable.CurrentGrabberSide() == RigPartSide.Left) ? leftTriggerAction : rightTriggerAction;
            }
        }

        public float Pressure { 
            get
            {
                var input = CurrentInput;
                if (input == null) return 0;
                return input?.action.ReadValue<float>() ?? 0;
            }
        } 

        protected override void Awake()
        {
            base.Awake();
            leftTriggerAction.EnableWithDefaultXRBindings(side: RigPartSide.Left, new List<string> { "trigger" });
            rightTriggerAction.EnableWithDefaultXRBindings(side: RigPartSide.Right, new List<string> { "trigger" });
            grabbable = GetComponentInChildren<INetworkGrabbable>();
            feedback = GetComponent<IFeedbackHandler>();
        }

        public override void Render()
        {
            base.Render();
            if (Object.HasStateAuthority)
            {
                VolumeDrawing();
            }
        }

        void VolumeDrawing()
        {
            var pressure = Pressure;
            if (pressure > 0.01f)
            {
                AddPoint(pressure: pressure);
                if (feedback != null)
                {
                    feedback.PlayAudioAndHapticFeedback(audioType: audioType, audioOverwrite: false, hapticAmplitude: Mathf.Clamp01(pressure * hapticAmplitudeFactor), feedbackMode: feedbackMode);
                }
            }
            else if(IsDrawingLine)
            {
                StopLine();
                if (feedback != null)
                {
                    feedback.StopAudioFeedback();
                }
            }

            if (IsGrabbed == false && currentDrawing != null)
            {
                StopDrawing();
                if (feedback != null)
                {
                    feedback.StopAudioFeedback();
                }
            }
        }
    }
}
