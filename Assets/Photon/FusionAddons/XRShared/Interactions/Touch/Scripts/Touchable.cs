using UnityEngine;
using UnityEngine.Events;

namespace Fusion.XR.Shared.Core.Touch
{
    /***
     * 
     * Touchable provides : 
     *  - visual & audio feedback when the element is touched
     *  - anti-bounce system to prevent duplicated touch
     *  - touch events (filtered after anti-bounce check)
     * 
     ***/
    public class Touchable : MonoBehaviour, ITouchable, IHapticConsumer
    {
        [Header("Current state")]
        public bool isTouchableIsTouched = false;
        

        [Header("Anti-bounce")]
        [Tooltip("If not zero, make sure that no other touch can occurs before this delay")]
        public float timeBetweenTouchTrigger = 0.3f;

        [Header("Feedback")]
        public const string DefaultAudioTouchFeedback = "OnTouchButton";
        [SerializeField] string audioType;
        [SerializeField] private bool playSoundWhenTouched = true;
        [SerializeField] private bool playHapticFeedbackOnToucher = true;
        [SerializeField] float toucherHapticAmplitude = 0.2f;
        [SerializeField] float toucherHapticDuration = 0.05f;

        [SerializeField] IFeedbackHandler feedback;

        [Header("Unity Event")]
        public UnityEvent onTouchStart;
        public UnityEvent onTouchEnd;

        float lastTouchEnd = -1;

        public bool WasRecentlyTouched => lastTouchEnd != -1 && timeBetweenTouchTrigger > 0 && (Time.time - lastTouchEnd) < timeBetweenTouchTrigger;


        private void Awake()
        {
            if (feedback == null)
                feedback = GetComponentInParent<IFeedbackHandler>();
        }

        private void OnEnable()
        {
            // We need to clear if component was disabled 
            isTouchableIsTouched = false;
        }


        private void TouchStartAnalysis(Toucher toucher)
        {
            if (WasRecentlyTouched) return;

            var touchableWasTouched = isTouchableIsTouched;
            isTouchableIsTouched = true;

            if (touchableWasTouched == false)
            {
                TriggerOnTouchStartCallback();
            }
            TouchFeedback(toucher);
        }

        private void TouchEndAnalysis(Toucher toucher)
        {
            var touchableWasTouched = isTouchableIsTouched;
            isTouchableIsTouched = false;

            if (touchableWasTouched)
            {
                TriggerOnTouchEndCallback();
                lastTouchEnd = Time.time;
            }
        }

        public void SimulateInstantTouch(Toucher toucher)
        {
            TouchStartAnalysis(toucher);
            TouchEndAnalysis(toucher);
        }

        #region  Feedback
   
        public void HapticFeedback(IHapticFeedbackProviderRigPart hapticFeedbackProviderRigPart)
        {
            if (playHapticFeedbackOnToucher && feedback != null)
            {
                feedback.PlayHapticFeedback(hapticAmplitude: toucherHapticAmplitude, hardwareRigPart: hapticFeedbackProviderRigPart, hapticDuration: toucherHapticDuration);                
            }
        }

        public void HapticFeedbackOnToucher(Toucher toucher)
        {
            if (playHapticFeedbackOnToucher && toucher != null)
            {
                if (toucher.rigPart.RelatedLocalHardwareRigPart() is IHapticFeedbackProviderRigPart hardwareRigPart)
                {
                    // Play haptic if there is a feedback handler on the touchable object
                    HapticFeedback(hardwareRigPart);
                    // Play haptic if there is a feedback handler on the toucher itself
                    var feedbackHandler = toucher.gameObject.GetComponentInParent<IFeedbackHandler>();
                    if (feedbackHandler != null)
                    {
                        feedbackHandler.PlayHapticFeedback(hapticAmplitude: toucherHapticAmplitude, hardwareRigPart: hardwareRigPart, hapticDuration: toucherHapticDuration);
                    }
                }
            }
        }


        void TouchFeedback(Toucher toucher)
        {
            if (playSoundWhenTouched && feedback != null && feedback.IsAudioFeedbackIsPlaying() == false)
            {
                if(audioType != null)
                    feedback.PlayAudioFeedback(audioType);
                else
                    feedback.PlayAudioFeedback(DefaultAudioTouchFeedback);
            }
                

            HapticFeedbackOnToucher(toucher);
        }
        #endregion 

        #region ITouchable
        public virtual void OnToucherContactStart(Toucher toucher)
        {
            TouchStartAnalysis(toucher);
        }

        public virtual void OnToucherStay(Toucher toucher) { }

        public virtual void OnToucherContactEnd(Toucher toucher)
        {
            TouchEndAnalysis(toucher);
        }
        #endregion

        #region Callbacks
        public virtual void TriggerOnTouchEndCallback()
        {
            if (onTouchEnd != null) onTouchEnd.Invoke();
        }

        public virtual void TriggerOnTouchStartCallback()
        {
            if (onTouchStart != null) onTouchStart.Invoke();
        }

        #endregion

        #region Inspector

        [ContextMenu("SimulateTouchStart")]
        public void SimulateTouchStart()
        {
            TouchStartAnalysis(null);
        }

        [ContextMenu("SimulateTouchEnd")]
        public void SimulateTouchEnd()
        {
            TouchEndAnalysis(null);
        }

        [ContextMenu("SimulateInstantTouch")]
        public void SimulateInstantTouch()
        {
            Debug.LogError("SimulateInstantTouch");
            TouchStartAnalysis(null);
            TouchEndAnalysis(null);
        }
        #endregion
    }
}
