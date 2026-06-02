using Fusion.XR.Shared.Core;
using UnityEngine;
using UnityEngine.Events;

namespace Fusion.Addons.Hover

{
    /**
     * Allow to touch an object with a BeamToucher, and trigger events. Should be associated with a trigger Collider
     * 
     * Provide visual, audio and haptic feedback
     */
    public class BeamHoverable : MonoBehaviour, IBeamHoverListener
    {
        [Header("Visual Feedback")]
        public Renderer targetRenderer;
        public GameObject onHoverVisualFeedback;
        public Material onHoverableMaterial;
        Material initialMaterial;

        [Header("Audio & Haptic Feedback")]
        [SerializeField] IFeedbackHandler feedback;
        public string audioFeedbackType = "OnTouchButton";

        [Header("Events")]
        public UnityEvent onBeamRelease = new UnityEvent();
        public UnityEvent onBeamHoverStart;
        public UnityEvent onBeamHoverEnd;

        private void Awake()
        {
            if (targetRenderer == null) targetRenderer = GetComponent<Renderer>();
            if (targetRenderer) initialMaterial = targetRenderer.material;

            if (feedback == null)
                feedback = GetComponentInParent<IFeedbackHandler>();
        }

        public void OnHoverEnd(BeamHoverer beamHoverer)
        {
            if (onHoverVisualFeedback)
                onHoverVisualFeedback.SetActive(false);

            if (targetRenderer) 
                targetRenderer.material = initialMaterial;

            if (onBeamHoverEnd != null) onBeamHoverEnd.Invoke();
        }

        public void OnHoverStart(BeamHoverer beamHoverer)
        {
            if (onHoverVisualFeedback)
                onHoverVisualFeedback.SetActive(true);

            if (targetRenderer && onHoverableMaterial) 
                targetRenderer.material = onHoverableMaterial;

            if (onBeamHoverStart != null) onBeamHoverStart.Invoke();

            if (feedback != null && feedback.IsAudioFeedbackIsPlaying() == false)
                feedback.PlayAudioAndHapticFeedback(audioType: audioFeedbackType, hardwareRigPart: beamHoverer.hapticFeedbackProvider, feedbackMode: FeedbackMode.AudioAndHaptic, audioOverwrite: false);
        }

        public void OnHoverRelease(BeamHoverer beamHoverer)
        {
            if (targetRenderer)
                targetRenderer.material = initialMaterial;

            if (onBeamRelease != null) onBeamRelease.Invoke();
        }
    }

}
