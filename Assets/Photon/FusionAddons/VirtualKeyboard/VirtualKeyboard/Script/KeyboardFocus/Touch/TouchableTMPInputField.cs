using Fusion.XR.Shared.Core.Touch;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace Fusion.Addons.VirtualKeyboard.Touch
{
/**
* 
* TouchableTMPInputField is used for VR 3D interaction.
* When the player touches the 3D box collider :
* - the keyboard visibility is toggled,
* - KeyboardFocusManager is informed
*
* KeyboardFocusManager is also informed when the text field is updated
* 
**/

    public class TouchableTMPInputField : MonoBehaviour, ITextFocusable
    {
#pragma warning disable CS0414
	[SerializeField] bool disableVirtualKeyboardOnAndroid = false;
#pragma warning restore CS0414
        public Touchable touchable;
        public BoxCollider box;
        public RectTransform inputFieldRectTransform;
        public RectTransform rectTransform;
        public TMP_InputField inputfield;
        public float defaultWidth = 100;
        public float defaultHeight = 100;
        public bool adaptSize = true;
        public Transform preferredKeyboardSpawnPosition;

        public AudioSource audioSource;

        private bool hasFocus = false;
        public bool looseFocusOnDisable = true;
        public bool toggleFocusOnTouch = true;
        public bool canReceiveFocus = true;

        public UnityEvent onTextChange = new UnityEvent();
        public UnityEvent onFocusChange = new UnityEvent();
        public UnityEvent onSubmit = new UnityEvent();

        public bool isDefaultFocus = false;

        float lastTMPSelect = -1;
        float lastTMPSelectBounceProtectionduration = 0.1f;

        #region ITouchableUIExtension
        public System.Type ExtenableUIComponent => typeof(TMP_InputField);
        #endregion

        string lastChangedText;
        public enum KeyboardInputValidationMode
        {
            NoChanges,
            RemoveNewLines
        }


        bool? changeInputFieldFocusRequest = null;



        private void Update()
        {
            if (changeInputFieldFocusRequest != null) 
            { 
                ChangeInputFieldFocus(changeInputFieldFocusRequest.GetValueOrDefault());
                changeInputFieldFocusRequest = null;
            }

            if (isDefaultFocus && KeyboardFocusManager.Instance && KeyboardFocusManager.Instance.IsAvailableForDefaultFocus && canReceiveFocus)
            {
                HasFocus = true;
            }
        }
        public KeyboardInputValidationMode keyboardValidationMode = KeyboardInputValidationMode.NoChanges;

        #region ITextFocusable
        public bool HasFocus
        {
            get => hasFocus;
            set
            {
                if (value == hasFocus) return;

                if (value == true && canReceiveFocus == false)
                    return;

                hasFocus = value;
                OnFocusChanged();
            }
        }
        public string Text
        {
            get
            {
                if (inputfield == null) inputfield = GetComponentInParent<TMP_InputField>();
                return inputfield.text;
            }

            set
            {
                if (inputfield == null) inputfield = GetComponentInParent<TMP_InputField>();
                string text = ValidateText(value);
                if (text == inputfield.text) return;
                inputfield.text = text;
                OnTextchange();
            }
        }
        public Transform PreferredKeyboardSpawnPosition => preferredKeyboardSpawnPosition;

        public void OnReturnPressed()
        {
            if (keyboardValidationMode == KeyboardInputValidationMode.RemoveNewLines)
            {
                OnSubmit();
            }
        }

        #endregion

        private void Awake()
        {
            if (inputfield == null) inputfield = GetComponentInParent<TMP_InputField>();
            box = GetComponentInParent<BoxCollider>();
            inputFieldRectTransform = inputfield.GetComponent<RectTransform>();
            rectTransform = GetComponent<RectTransform>();
            touchable = GetComponent<Touchable>();
        }

        private void Start()
        {
            if (inputfield.lineLimit == 1 && keyboardValidationMode != KeyboardInputValidationMode.RemoveNewLines)
            {
                // Changing validation mode to remove new line, to match the unique line of input
                keyboardValidationMode = KeyboardInputValidationMode.RemoveNewLines;
            }
        }

        protected virtual string ValidateText(string text)
        {
            switch (keyboardValidationMode)
            {
                case KeyboardInputValidationMode.RemoveNewLines:
                    text = text.Replace("\n", "").Replace("\r", ""); break;
            }
            if (inputfield.characterLimit != 0 && text.Length > inputfield.characterLimit)
            {
                text = text.Substring(0, inputfield.characterLimit);
            }
            return text;
        }

        #region TMP_InputField callbacks
        private void OnInputFieldDeSelect(string text)
        {
            if (hasFocus == false) return;

            hasFocus = false;
            OnFocusChanged();
        }

        private void LateUpdate()
        {
            if (lastTMPSelect == -1 || (Time.time - lastTMPSelect) < lastTMPSelectBounceProtectionduration) return;
            lastTMPSelect = -1;
        }

        private void OnInputFieldSelect(string text)
        {
            if (hasFocus) return;

            if (hasFocus == false && canReceiveFocus == false)
            {
                ChangeInputFieldFocus(false, force:true);
                return;
            }

            hasFocus = true;
            lastTMPSelect = Time.time;
            OnFocusChanged();
        }

        private void OnInputFieldValueChange(string text)
        {
            OnTextchange();
        }
        #endregion

        private void OnEnable()
        {
            if (adaptSize)
                StartCoroutine(AdaptSize());


#if UNITY_ANDROID && !UNITY_EDITOR
            
    if (disableVirtualKeyboardOnAndroid)
    {
        Debug.LogError("Touch detected but disableVirtualKeyboardOnAndroid is true => VR keyboard will not be openned");
        return;
    }
#endif


            if (touchable)
                touchable.onTouchStart.AddListener(OnTouch);

            inputfield.onSelect.AddListener(OnInputFieldSelect);
            inputfield.onSubmit.AddListener(OnSubmit);
            inputfield.onDeselect.AddListener(OnInputFieldDeSelect);
            inputfield.onValueChanged.AddListener(OnInputFieldValueChange);

            if (hasFocus)
            {
                OnFocusChanged();
            }
        }

        private void OnDisable()
        {
            if (looseFocusOnDisable)
            {
                hasFocus = false;
                OnFocusChanged();
            }
            if (touchable)
                touchable.onTouchStart.RemoveListener(OnTouch);

            inputfield.onSelect.RemoveListener(OnInputFieldSelect);
            inputfield.onSubmit.RemoveListener(OnSubmit);
            inputfield.onDeselect.RemoveListener(OnInputFieldDeSelect);
        }

        // Adapt the size of the 3D button collider according to the UI
        IEnumerator AdaptSize()
        {
            // We have to wait one frame for rect sizes to be properly set by Unity
            yield return new WaitForEndOfFrame();

            Vector3 newSize = new Vector3(inputFieldRectTransform.rect.size.x / inputfield.gameObject.transform.localScale.x, inputFieldRectTransform.rect.size.y / inputfield.gameObject.transform.localScale.y, box.size.z);
            rectTransform.sizeDelta = new Vector2(newSize.x, newSize.y);
            box.size = newSize;
        }



        // OnTouch event triggered when the player touches the 3D button is forwarded to the UI button 
        private void OnTouch()
        {

            if (hasFocus && lastTMPSelect != -1 && (Time.time - lastTMPSelect) < lastTMPSelectBounceProtectionduration)
            {
                //Debug.LogError("Avoid double focus change due to touch/pointer");
                return;
            }

            if (hasFocus == false && canReceiveFocus == false)
                return;


            if (hasFocus && toggleFocusOnTouch == false)
            {
                return;
            }

            hasFocus = !hasFocus;

            ChangeInputFieldFocus(hasFocus);

            OnFocusChanged();
        }
        

        private void ChangeInputFieldFocus(bool newFocusState, bool force=true)
        {
            changeInputFieldFocusRequest = null;

            if (force == false && inputfield.isFocused == newFocusState) return;

            var eventSystem = UnityEngine.EventSystems.EventSystem.current;
            if (eventSystem.alreadySelecting)
            {

                changeInputFieldFocusRequest = newFocusState;
                return;
            }


            if (newFocusState)
            {
                eventSystem.SetSelectedGameObject(inputfield.gameObject);
                inputfield.ActivateInputField();
            }
            else
            {
                eventSystem.SetSelectedGameObject(null);
                inputfield.DeactivateInputField();
            }
        }


        private void OnFocusChanged()
        {
            if (KeyboardFocusManager.Instance) KeyboardFocusManager.Instance.OnFocusChange(this);
            onFocusChange.Invoke();
            ChangeInputFieldFocus(hasFocus, force:false); 
        }

        private void OnTextchange()
        {
            if (lastChangedText == Text) return;
            
            lastChangedText = Text;
            if (KeyboardFocusManager.Instance) KeyboardFocusManager.Instance.OnTextChange(this);
            UpdateCaretPosition();
            onTextChange.Invoke();
        }

        private void UpdateCaretPosition()
        {
            inputfield.stringPosition = Text.Length;
        }
        private void OnSubmit(string value)
        {
            OnSubmit();
        }

        private void OnSubmit()
        {
            if (onSubmit != null) onSubmit.Invoke();
        }
    }

}
