using TMPro;
using UnityEngine;

public class ForceOpenTMPKeyboard : MonoBehaviour
{
    public TMP_InputField inputField;
    private TouchScreenKeyboard keyboard;

    public void OpenKeyboard()
    {
        if (inputField == null)
            return;

        inputField.ActivateInputField();
        inputField.Select();

        keyboard = TouchScreenKeyboard.Open(
            inputField.text,
            TouchScreenKeyboardType.Default,
            false,
            false,
            false,
            false,
            "Enter name"
        );
    }

    private void Update()
    {
        if (keyboard != null && inputField != null)
        {
            inputField.text = keyboard.text;
        }
    }
}