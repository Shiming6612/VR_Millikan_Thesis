using Fusion.XR.Shared.Core;
using Fusion.XR.Shared.Desktop;
using Fusion.XR.Shared.Rig;
using UnityEngine;


namespace Fusion.Addons.VirtualKeyboard
{
    public interface ITextFocusable
    {
        bool HasFocus { get; set; }
        string Text { get; set; }
        Transform PreferredKeyboardSpawnPosition { get; }
        void OnReturnPressed();
    }

    public interface ITextFocusListener {
        void OnFocusChange(ITextFocusable focusable);
        void OnTextChange(ITextFocusable focusable);
    }


    /***
     * 
     * KeyboardFocusManager is in charge to manage keyboard focus requests from objects in the scene.
     * Objects that needs a keyboard must implement the ITextFocusable interface.
     * They can request the focus with OnFocusChange() or inform the KeyboardFocusManager that text has changed with OnTextChange() (input field can be updated by real keyboard).
     * The KeyboardFocusManager updates the keyboard position & buffer when a new object get the focus.
     * Also, the current "KeyboardFocus" object is informed when the keyboard buffer changed or when the focus is lost thanks to the KeyboardManager callbacks.
     * 
     ***/
    public class KeyboardFocusManager : MonoBehaviour, ITextFocusListener
    {
        public static KeyboardFocusManager Instance;

        [SerializeField] protected KeyboardManager keyboardManager;
        [SerializeField] protected RigInfo rigInfo;

        [Header("Keyboard spawn position")]
        public float keyboardSpawnVerticalOffset = -0.4f;
        public float keyboardSpawnDistance = 0.35f;
        public bool moveKeyboardOnFocusChange = true;
        public Quaternion computedKeyboardRotationOffset = Quaternion.Euler(-120, 0, 0);
        [Header("Desktop context")]
        public bool allowDefaultFocusOnDesktopContext = false;

        protected ITextFocusable _currentKeyboardFocus = null;
        public ITextFocusable CurrentKeyboardFocus => _currentKeyboardFocus;
        public virtual bool KeyboardRequired => rigInfo == null || rigInfo.localHardwareRigKind == RigInfo.RigKind.VR;
        public virtual bool IsInDesktopMode => rigInfo != null && rigInfo.localHardwareRigKind != RigInfo.RigKind.VR;
        public virtual bool AllowDefaultFocus => allowDefaultFocusOnDesktopContext || KeyboardRequired;
        public virtual bool IsAvailableForDefaultFocus => AllowDefaultFocus && CurrentKeyboardFocus == null;

        DesktopController disabledDesktopController = null;

        private void Awake()
        {
            Instance = this;
            if (rigInfo == null) rigInfo = RigInfo.FindRigInfo(allowSceneSearch: true);

            if (keyboardManager == null)
            {
                keyboardManager = FindAnyObjectByType<KeyboardManager>(FindObjectsInactive.Include);
            }

            keyboardManager.onBufferChanged.AddListener(OnKeyboardBufferChanged);
            keyboardManager.onKeyboardStatusChanged.AddListener(OnKeyboardStatusChanged);
            keyboardManager.onReturnPressed.AddListener(OnReturnPressed);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        #region KeyboardManager callbacks
        void OnKeyboardBufferChanged()
        {
            if (CurrentKeyboardFocus == null) return;
            string text = keyboardManager.Buffer;
            CurrentKeyboardFocus.Text = text;
        }

        void OnKeyboardStatusChanged(bool isKeyboardActive)
        {
            if (CurrentKeyboardFocus == null) return;
            if (!isKeyboardActive)
            {
                CurrentKeyboardFocus.HasFocus = false;
            }
        }

        void OnReturnPressed()
        {
            if (CurrentKeyboardFocus == null) return;
            CurrentKeyboardFocus.OnReturnPressed();
        }
        #endregion

        #region ITextFocusListener

        // OnFocusChange is called by objects requiring the keyboard focus
        public void OnFocusChange(ITextFocusable focusable)
        {
            if (focusable.HasFocus)
            {
                // exit if the focusable is the same
                if (focusable == CurrentKeyboardFocus) return;

                // backup CurrentKeyboardFocus in previousFocus and update the CurrentKeyboardFocus
                ITextFocusable previousFocus = null;
                if (CurrentKeyboardFocus != null)
                {
                    previousFocus = CurrentKeyboardFocus;
                }
                _currentKeyboardFocus = focusable;

                // update the previousFocus that the focus has been lost
                if (previousFocus != null)
                {
                    previousFocus.HasFocus = false;
                }
                else
                {
                    // ask the keyboardManager to open the keyboard
                    OnExistingCurrentFocus();
                }
                // updates the keyboard buffer with the CurrentKeyboardFocus text & move the keyboard at the correct position
                OnCurrentFocusChange();
            }
            else
            {
                // No focus required
                if (focusable == CurrentKeyboardFocus)
                {
                    // clean the CurrentKeyboardFocus
                    _currentKeyboardFocus = null;

                    // hides the keyboard if it is opened
                    OnNoCurrentFocus();
                    OnCurrentFocusChange();
                }
            }
        }

        // OnTextChange asks the keyboardManager to update the buffer with the focusable text
        public void OnTextChange(ITextFocusable focusable)
        {
            // Do nothing if the player is not in VR
            if (!KeyboardRequired) return;

            // Do nothing if the focusable do not have the focus
            if (focusable != CurrentKeyboardFocus)
            {
                return;
            }

            // Ask the keyboardManager to update the buffer with the focusable text
            keyboardManager.UpdateKeyboardBuffer(focusable.Text);
        }
        #endregion

        // OnExistingCurrentFocus ask the keyboardManager to open the keyboard
        void OnExistingCurrentFocus()
        {
            //Debug.LogError("OnExistingCurrentFocus");
            DisableDesktopController();

            // Do nothing if the player is not in VR
            if (!KeyboardRequired) return;

            // else, open the keyboard
            if (keyboardManager.IsKeyboardActive()) return;
            keyboardManager.OpenKeyboard();
        }


        private bool keyboardPositionNotYetInitialized = true;

        // Update the keyboard position according to the CurrentKeyboardFocus or the hardware rig
        void MoveKeyboardOnFocusChange()
        {
            if (moveKeyboardOnFocusChange == false && keyboardPositionNotYetInitialized == false) return;
            keyboardPositionNotYetInitialized = false;

            if (CurrentKeyboardFocus != null && CurrentKeyboardFocus.PreferredKeyboardSpawnPosition)
            {
                // Update the keyboard position according to the CurrentKeyboardFocus 
                keyboardManager.grabbableKeyboard.transform.position = CurrentKeyboardFocus.PreferredKeyboardSpawnPosition.position;
                keyboardManager.grabbableKeyboard.transform.rotation = CurrentKeyboardFocus.PreferredKeyboardSpawnPosition.rotation;
            }
            else
            {
                // Update the keyboard position according to the hardware rig 
                var headset = HardwareRigsRegistry.GetHardwareRig().Headset;
                var forward = headset.transform.forward;
                keyboardManager.grabbableKeyboard.transform.position = headset.transform.position + forward * keyboardSpawnDistance + Vector3.up * keyboardSpawnVerticalOffset;
                var rot = Quaternion.LookRotation(keyboardManager.grabbableKeyboard.transform.position - headset.transform.position);
                keyboardManager.grabbableKeyboard.transform.rotation = rot * computedKeyboardRotationOffset;
            }
        }

        // OnCurrentFocusChange updates the keyboard buffer with the CurrentKeyboardFocus text & move the keyboard at the correct position
        void OnCurrentFocusChange()
        {
            // Exit if the player is not in VR (nothing to do)
            if (!KeyboardRequired) return;

            // Exit if there is no CurrentKeyboardFocus
            if (CurrentKeyboardFocus == null) return;

            // Update the keyboard buffer with the CurrentKeyboardFocus text
            keyboardManager.UpdateKeyboardBuffer(CurrentKeyboardFocus.Text);

            // update the keyboard position accoring to the CurrentKeyboardFocus
            MoveKeyboardOnFocusChange();
        }

        // OnNoCurrentFocus hides the keyboard if it is opened
        void OnNoCurrentFocus()
        {
            //Debug.LogError("OnNoCurrentFocus");
            ReEnableDesktopController();

            // Exit if the player is not in VR (nothing to do)
            if (!KeyboardRequired) return;

            // Exit if the keyboard if not displayed
            if (!keyboardManager.IsKeyboardActive()) return;

            // Close the keyboard
            keyboardManager.CloseKeyboard();
        }

        void DisableDesktopController()
        {
            // Exit if the player is not in desktop mode
            if (rigInfo == null || rigInfo.localHardwareRigKind != RigInfo.RigKind.Desktop) return;

            // Disable the desktop controller
            disabledDesktopController = rigInfo.localHardwareRig.gameObject.GetComponentInChildren<DesktopController>();
            if(disabledDesktopController != null)
                disabledDesktopController.enabled = false;
        }

        void ReEnableDesktopController()
        {
            // Exit if the player is not in desktop mode
            if (disabledDesktopController == null) return;

            // Enable the desktop controller
            disabledDesktopController.enabled = true;
        }
    }

}
