using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System;
using UnityEngine.Events;
using Fusion.XR.Shared.Core.Touch;
using Fusion.XR.Shared.Core;

namespace Fusion.Addons.VirtualKeyboard
{
    /***
     * 
     * KeyboardManager manages the keyboard object.
     * Each letter/digit key on the keyboard calls the KeyTouch() method to update the keyboard buffer.
     * Some methods handles the special keys (return, space, backspace, caps lock, letters & numbers toggle, etc.)
     * It provides methods to : 
     *  - read/write the keyboard buffer
     *  - open/close the keyboard
     *  - get the keyboard status (IsKeyboardActive)
     *  
     **/
    public class KeyboardManager : MonoBehaviour
    {
        [SerializeField] private string buffer;
        public string Buffer => buffer;

        [SerializeField] private GameObject LettersKeyboard;
        [SerializeField] private GameObject NumbersKeyboard;
        [SerializeField] private List<TextMeshProUGUI> letters = new List<TextMeshProUGUI>();
        [SerializeField] private IFeedbackHandler feedback;
        [SerializeField] private string audioFeedbackType = Touchable.DefaultAudioTouchFeedback;
        public GameObject grabbableKeyboard;

        public UnityEvent onBufferChanged;
        public UnityEvent onReturnPressed;
        public UnityEvent<bool> onKeyboardStatusChanged = new UnityEvent<bool>();

        private bool capsLock = false;

        [SerializeField] private float timeBetweenTouchTrigger = 0.15f;
        private float lastTouchTime;
        private float timeSinceLastTouch;

        // Start is called before the first frame update
        void Start()
        {
            LettersKeyboard.SetActive(true);
            NumbersKeyboard.SetActive(false);
            if (feedback == null) 
                feedback = GetComponent<IFeedbackHandler>();
        }

        private void Awake()
        {
            if (!grabbableKeyboard)
            {
                var grabbable = GetComponentInChildren<IGrabbable>();
                if (grabbable != null)
                    grabbableKeyboard = grabbable.gameObject;
                else
                    Debug.LogError("Missing grabbable");
            }
            if (grabbableKeyboard)
                CloseKeyboard();
        }
        public void KeyTouch(TextMeshProUGUI key)
        {
            var isTouchTimerExpired = IsTouchTimerExpired();
            if (isTouchTimerExpired)
            {
                buffer += key.text;
                BufferChanged();
            }
        }

        public void KeyTouchSpace()
        {
            if (IsTouchTimerExpired())
            {
                buffer = buffer + " ";
                BufferChanged();
            }
        }

        public void KeyTouchReturn()
        {
            if (IsTouchTimerExpired())
            {
                buffer = buffer + Environment.NewLine;
                BufferChanged();
                if (onReturnPressed != null) onReturnPressed.Invoke();
            }
        }

        public void KeyTouchBackSpace()
        {
            if (IsTouchTimerExpired())
            {
                if (buffer.Length > 0)
                {
                    buffer = buffer.Substring(0, buffer.Length - 1);
                    BufferChanged();
                }
            }
        }

        public void LettersAndNumbersToggle()
        {
            if (IsTouchTimerExpired())
            {
                if (LettersKeyboard.activeSelf)
                {
                    LettersKeyboard.SetActive(false);
                    NumbersKeyboard.SetActive(true);
                }
                else
                {
                    LettersKeyboard.SetActive(true);
                    NumbersKeyboard.SetActive(false);
                }
            }
        }

        public void CapsLockToggle()
        {
            if (IsTouchTimerExpired())
            {
                if (capsLock)
                {
                    foreach (TextMeshProUGUI letter in letters)
                        letter.text = letter.text.ToLower();

                }
                else
                {
                    foreach (TextMeshProUGUI letter in letters)
                        letter.text = letter.text.ToUpper();
                }
                capsLock = !capsLock;
            }
        }

        private bool IsTouchTimerExpired()
        {
            timeSinceLastTouch = Time.time - lastTouchTime;
            lastTouchTime = Time.time;
            if (timeSinceLastTouch > timeBetweenTouchTrigger)
                return true;
            else
                return false;
        }

        public void UpdateKeyboardBuffer(string newbuffer)
        {
            buffer = newbuffer;
            if (onBufferChanged != null)
            {
                onBufferChanged.Invoke();
            }
        }

        private void BufferChanged()
        {
            if (onBufferChanged != null)
            {
                onBufferChanged.Invoke();
            }
            if (feedback != null)
                feedback.PlayAudioFeedback(audioFeedbackType);
        }

        public void CloseKeyboard()
        {
            if (grabbableKeyboard.activeSelf)
                grabbableKeyboard.SetActive(false);
            onKeyboardStatusChanged.Invoke(false);
        }

        public void OpenKeyboard()
        {
            if (!grabbableKeyboard.activeSelf)
                grabbableKeyboard.SetActive(true);
            onKeyboardStatusChanged.Invoke(true);
        }

        public bool IsKeyboardActive()
        {
            return grabbableKeyboard.activeSelf;
        }
    }

}
