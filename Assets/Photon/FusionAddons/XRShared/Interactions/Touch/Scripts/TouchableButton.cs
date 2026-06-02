using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Fusion.XR.Shared.Core.Touch
{
    /***
     * 
     * TouchableButton can be used as a press, toggle or radio button.
     * It provides visual & audio feedback when the button is touched
     *  
     * The class also handles material changes for the button mesh renderer when the button is pressed or released. 
     *
     * If primaryIcon is set, it displays primary and secondary icons based on button state. 
     * It updates the icons based on the button type (press button, toggle button, or radio button) and the current button status. 
     *
     * 
     ***/
    public class TouchableButton : MonoBehaviour, ITouchable, IHapticConsumer
    {
        [Header("Button")]
        public ButtonType buttonType = ButtonType.PressButton;

        [Header("Current state")]
        public bool isButtonPressed = false;
        protected MeshRenderer meshRenderer;
        public bool isButtonOn = false;

        public enum ButtonType
        {
            PressButton,
            RadioButton,
            ToggleButton
        }


        [Header("Toggle Button")]
        public bool toggleStatus = false;

        [Header("Radio Button")]
        [SerializeField]
        List<TouchableButton> radioGroupButtons = new List<TouchableButton>();
        public bool isRadioGroupDefaultButton = false;

        [Header("Anti-bounce")]
        public float timeBetweenTouchTrigger = 0.3f;

        [Header("Feedback")]
        [SerializeField] string audioType;
        [SerializeField] protected Material touchMaterial;
        [SerializeField] private bool playSoundWhenTouched = true;
        [SerializeField] private bool playHapticFeedbackOnToucher = true;
        [SerializeField] float toucherHapticAmplitude = 0.2f;
        [SerializeField] float toucherHapticDuration = 0.05f;
        protected Material materialAtStart;
        [SerializeField] IFeedbackHandler feedback;
        [SerializeField] bool shouldUpdateMaterial = true;

        [Header("Sibling button")]
        [SerializeField]
        bool doNotallowTouchIfSiblingTouched = true;
        [SerializeField]
        bool doNotallowTouchIfSiblingWasRecentlyTouched = true;
        [SerializeField]
        bool automaticallyDetectSiblings = true;
        [SerializeField]
        List<TouchableButton> siblingButtons = new List<TouchableButton>();

        [Header("Icons")]
        [SerializeField] GameObject primaryIcon;
        [SerializeField] GameObject secondaryIcon;


        [Header("Unity Event")]
        public UnityEvent<bool> onStatusChanged;
        public UnityEvent onOnStatus;
        public UnityEvent onOffStatus;

        public UnityEvent onButtonTouchStart = new UnityEvent ();
        public UnityEvent onButtonTouchEnd = new UnityEvent();

        float lastTouchEnd = -1;

        public bool WasRecentlyTouched => lastTouchEnd != -1 && (Time.time - lastTouchEnd) < timeBetweenTouchTrigger;
        public bool IsPressButton => buttonType == ButtonType.PressButton;
        public bool IsToggleButton => buttonType == ButtonType.ToggleButton;
        public bool IsRadioButton => buttonType == ButtonType.RadioButton;

        private void Awake()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer) materialAtStart = meshRenderer.material;

            if (feedback == null)
                feedback = GetComponentInParent<IFeedbackHandler>();
        }

        private void OnEnable()
        {
            UpdateButton();
        }

        private void OnDisable()
        {
            // We need to clear if component is disabled 
            isButtonPressed = false;
        }

        private void Start()
        {
            if (automaticallyDetectSiblings && transform.parent)
            {
                foreach (Transform child in transform.parent)
                {
                    if (child == transform) continue;
                    if (child.TryGetComponent<TouchableButton>(out var sibling))
                    {
                        siblingButtons.Add(sibling);
                    }
                }
            }


            if (IsRadioButton && radioGroupButtons.Count == 0)
            {
                foreach (var siblingButton in siblingButtons)
                {
                    if (siblingButton.IsRadioButton)
                    {
                        radioGroupButtons.Add(siblingButton);
                    }
                }
            }

            if (isRadioGroupDefaultButton)
            {
                ChangeRadioButtonsStatus();
            }
        }

        bool CheckIfTouchIsAllowed()
        {
            if (WasRecentlyTouched)
            {
                // Local anti-bounce 
                return false;
            }
            if (doNotallowTouchIfSiblingTouched)
            {
                foreach (var sibling in siblingButtons)
                {
                    if (sibling.isButtonPressed)
                    {
                        //  Debug.LogError("Preventing due to active " + sibling);
                        return false;
                    }
                    else if (doNotallowTouchIfSiblingWasRecentlyTouched && sibling.WasRecentlyTouched)
                    {
                        // Sibling anti-bounce 
                        //   Debug.LogError("Preventing due to recently active" + sibling);
                        return false;

                    }
                }
            }
            return true;
        }

        public void ChangeButtonStatus(bool status)
        {
            bool previousStatus = toggleStatus;
            if (IsToggleButton || IsRadioButton)
            {
                toggleStatus = status;
            }

            UpdateButton();
            
            if (previousStatus != toggleStatus || IsPressButton)
            {
                isButtonOn = IsPressButton ? isButtonPressed : toggleStatus;
                TriggerStatusChangeCallback();
            }
        }

        public void ChangeRadioButtonsStatus()
        {
            ChangeButtonStatus(true);
            foreach (var button in radioGroupButtons)
            {
                button.ChangeButtonStatus(false);
            }
        }

        private void TouchStartAnalysis(Toucher toucher)
        {
            if (CheckIfTouchIsAllowed() == false) return;
           
            var buttonWasActive = isButtonPressed;
            isButtonPressed = true;

            if (buttonWasActive == false)
            {
                TriggerOnTouchStartCallback();
            }

            
            if (IsToggleButton)
            {
                ChangeButtonStatus(!toggleStatus);
            }
            else if (IsRadioButton)
            {
                ChangeRadioButtonsStatus();
            }
            else if (IsPressButton)
            {
                ChangeButtonStatus(true);
            }

            TouchFeedback(toucher);
        }

        private void TouchEndAnalysis(Toucher toucher)
        {
            var buttonWasActive = isButtonPressed;
            isButtonPressed = false;

            if (buttonWasActive)
            {
                TriggerOnTouchEndCallback();
                lastTouchEnd = Time.time;
                if (IsPressButton)
                {
                    ChangeButtonStatus(false);
                }
            }

            UpdateButton();
        }

        public void SimulateInstantTouch(Toucher toucher)
        {
            TouchStartAnalysis(toucher);
            TouchEndAnalysis(toucher);
        }

        #region Visual effect & feedback
        public virtual void UpdateButton()
        {
            UpdateMeshRenderer();
            UpdateIcons();
        }

        void UpdateMeshRenderer()
        {
            if (!meshRenderer) return;
            if (shouldUpdateMaterial == false) return;

            bool boutonActivated = false;

            if (IsToggleButton || IsRadioButton)
            {
                boutonActivated = toggleStatus;
            }           
            else if (IsPressButton)
            {
                boutonActivated = isButtonPressed;
            }

            if (touchMaterial && boutonActivated)
            {
                meshRenderer.material = touchMaterial;
            }
            else if (materialAtStart && boutonActivated == false)
            {
                RestoreMaterial();
            }
        }

        void UpdateIcons()
        {
            if (IsPressButton)
            {
                if (primaryIcon != null)
                {
                    primaryIcon.SetActive(true);

                    if (secondaryIcon != null)
                    {
                        primaryIcon.SetActive(!isButtonPressed);
                        secondaryIcon.SetActive(isButtonPressed);
                    }
                }
            }

            if (IsToggleButton || IsRadioButton)
            {
                if (primaryIcon != null)
                {
                    primaryIcon.SetActive(toggleStatus);

                    if (secondaryIcon != null)
                        secondaryIcon.SetActive(!toggleStatus);
                }
            }
        }

        // Restore initial material
        protected async void RestoreMaterial()
        {
            await System.Threading.Tasks.Task.Delay(100);
            if (meshRenderer) meshRenderer.material = materialAtStart;
        }

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
                feedback.PlayAudioFeedback(audioType);

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
            if (onButtonTouchEnd != null) onButtonTouchEnd.Invoke();
        }

        public virtual void TriggerOnTouchStartCallback()
        {
            if (onButtonTouchStart != null) onButtonTouchStart.Invoke();
        }

        public virtual void TriggerStatusChangeCallback()
        {
            if (onStatusChanged != null)
            {
                onStatusChanged.Invoke(isButtonOn);
            }

            if (isButtonOn && onOnStatus != null)
            {
                onOnStatus.Invoke();
            }

            if (isButtonOn == false && onOffStatus != null)
            {
                onOffStatus.Invoke();
            }
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
