using Fusion.XR.Shared.Core.Touch;
using UnityEngine;

public class DemoTouchableButtonTarget : MonoBehaviour
{
    TouchableButton touchableButton;

    private void Awake()
    {
        touchableButton = GetComponent<TouchableButton>();
        touchableButton.onButtonTouchStart.AddListener(OnTouch);
        touchableButton.onButtonTouchEnd.AddListener(OnUnTouch);
    }

    private void OnTouch()
    {
        Debug.LogError($"[{name}] OnTouch");
    }

    private void OnUnTouch()
    {
        Debug.LogError($"[{name}] OnUnTouch");
    }
}
